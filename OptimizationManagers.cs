using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace NexaArena
{
    public sealed class ManagerBackup
    {
        public string Module,Id,Original,Written,Identity;
    }
    public sealed class ManagerBackups { public List<ManagerBackup> Items=new List<ManagerBackup>(); }
    internal sealed class ManagerChoice { public string value,label; }
    internal sealed class ManagerItem
    {
        public string id,title,detail,current,effect,blocked,currentChoice;
        public bool supported,applied;
        public List<ManagerChoice> choices=new List<ManagerChoice>();
        internal string Raw,Identity;
    }
    internal interface IManagerBackend
    {
        List<ManagerItem> Scan(string module);
        ManagerItem Read(string module,string id);
        string Desired(string module,string id,string choice);
        void Write(string module,string id,string value);
    }
    internal sealed class OptimizationManagers
    {
        private readonly IManagerBackend backend;
        private readonly string file;
        private readonly ManagerBackups ledger;
        private readonly object gate=new object();
        internal OptimizationManagers():this(new WindowsManagerBackend(),Path.Combine(SavedState.DirectoryPath,"manager-backups.bin")){}
        internal OptimizationManagers(IManagerBackend backend,string file)
        {
            this.backend=backend;this.file=file;ledger=new ManagerBackups();
            if(File.Exists(file))
            {
                if(new FileInfo(file).Length>8*1024*1024)throw new InvalidOperationException("管理模块备份过大，请保留文件并检查。");
                byte[] bytes=ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser);
                using(var memory=new MemoryStream(bytes))using(var reader=XmlReader.Create(memory,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024}))
                    ledger=(ManagerBackups)new XmlSerializer(typeof(ManagerBackups)).Deserialize(reader);
                if(ledger==null||ledger.Items==null||ledger.Items.Count>4096)throw new InvalidOperationException("管理模块备份格式无效，未更改设置。");
            }
        }
        private ManagerBackup Find(string module,string id){return ledger.Items.FirstOrDefault(x=>x.Module==module&&x.Id==id);}
        internal bool HasBackup(string module,string id){lock(gate)return Find(module,id)!=null;}
        private void AddBackup(ManagerBackup saved)
        {ledger.Items.Add(saved);try{Save();}catch{ledger.Items.Remove(saved);throw;}}
        private void RemoveBackup(ManagerBackup saved)
        {int index=ledger.Items.IndexOf(saved);ledger.Items.Remove(saved);try{Save();}catch{if(index>=0)ledger.Items.Insert(index,saved);throw;}}
        internal void SelectGamePath(string game,string path){lock(gate){var windows=backend as WindowsManagerBackend;if(windows!=null)windows.SelectGamePath(game,path);}}
        private void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file));byte[] bytes;
            using(var memory=new MemoryStream()){new XmlSerializer(typeof(ManagerBackups)).Serialize(memory,ledger);bytes=ProtectedData.Protect(memory.ToArray(),null,DataProtectionScope.CurrentUser);}
            File.WriteAllBytes(file+".tmp",bytes);
            if(File.Exists(file))File.Replace(file+".tmp",file,file+".bak");else File.Move(file+".tmp",file);
        }
        internal object Catalog(string module)
        {
            lock(gate)
            {
                var items=backend.Scan(module);
                foreach(var saved in ledger.Items.Where(x=>x.Module==module))
                    if(!items.Any(x=>x.id==saved.Id))
                    {
                        try{items.Add(backend.Read(module,saved.Id));}
                        catch(Exception ex){items.Add(new ManagerItem {id=saved.Id,title="已保存原值的项目",detail=saved.Id,current="目标不可用",supported=false,blocked=ex.Message});}
                    }
                foreach(var item in items){item.applied=Find(module,item.id)!=null;var currentChoice=item.choices.FirstOrDefault(x=>x.value==item.Raw);item.currentChoice=currentChoice==null?string.Empty:currentChoice.value;}
                return new {items=items.OrderBy(x=>x.title,StringComparer.CurrentCultureIgnoreCase).ToArray()};
            }
        }
        internal object Change(string module,string id,string choice,bool restore)
        {
            lock(gate)
            {
                ManagerItem current=backend.Read(module,id);
                if(!current.supported)throw new InvalidOperationException(current.blocked??"此项目不可修改。");
                ManagerBackup saved=Find(module,id);
                if(saved!=null&&saved.Identity!=current.Identity)throw new InvalidOperationException("目标定义或设备身份已改变，备份保留，未覆盖新目标。");
                if(restore)
                {
                    if(saved==null)throw new InvalidOperationException("没有此项目的原值备份。");
                    if(current.Raw==saved.Original){RemoveBackup(saved);return new {ok=true,message="现值已是原值，已清理对应备份"};}
                    if(current.Raw!=saved.Written)throw new InvalidOperationException("现值已被其他程序修改，已保留备份；不会覆盖。");
                    backend.Write(module,id,saved.Original);
                    var restored=backend.Read(module,id);
                    if(restored.Identity!=saved.Identity||restored.Raw!=saved.Original)throw new InvalidOperationException("原值或目标身份读回不一致，备份保留。");
                    RemoveBackup(saved);
                }
                else
                {
                    string desired=backend.Desired(module,id,choice);
                    if(saved!=null)
                    {
                        if(current.Raw!=saved.Written)throw new InvalidOperationException("现值已被其他程序修改，请先核对或还原。");
                        if(desired==current.Raw)return new {ok=true,message="当前已是所选值"};
                        throw new InvalidOperationException("该项目已有原值备份，请先还原再选择其他值。");
                    }
                    if(current.Raw==desired)return new {ok=true,message="当前已是所选值，无需修改"};
                    saved=new ManagerBackup {Module=module,Id=id,Identity=current.Identity,Original=current.Raw,Written=desired};
                    AddBackup(saved);
                    try
                    {
                        backend.Write(module,id,desired);
                        var after=backend.Read(module,id);
                        if(after.Identity!=saved.Identity||after.Raw!=desired)throw new InvalidOperationException("修改后读回校验失败。");
                    }
                    catch
                    {
                        try
                        {
                            if(backend.Read(module,id).Identity==saved.Identity){backend.Write(module,id,current.Raw);var reverted=backend.Read(module,id);if(reverted.Identity==saved.Identity&&reverted.Raw==current.Raw)RemoveBackup(saved);}
                        }
                        catch { }
                        throw;
                    }
                }
                return new {ok=true,message=restore?"已还原原值":"已应用并保存原值"};
            }
        }
    }
    internal sealed class WindowsManagerBackend:IManagerBackend
    {
        private readonly Dictionary<string,string> gamePaths=new Dictionary<string,string>();
        internal void SelectGamePath(string game,string path){GameOptimizer.ExecutableName(game);if(!string.IsNullOrEmpty(path)&&GameOptimizer.IsExecutable(game,path))gamePaths[game]=Path.GetFullPath(path);}
        private NvidiaGameSettings Nvidia(string module)
        {if(module=="nvidia-global")return new NvidiaGameSettings("GLOBAL",null);string game=module.Substring(7),path;GameOptimizer.ExecutableName(game);if(!gamePaths.TryGetValue(game,out path))path=GameOptimizer.RunningPath(game);return new NvidiaGameSettings(game,path);}
        private const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
        private static readonly string[] ProtectedTokens={"vanguard","vgc","vgk","riotclient","anticheat","anti-cheat","easyanticheat","battleye","windows defender","securityhealth","msascuil","avast","avgui","kaspersky","360tray","huorong","hipsdaemon","nexaarena","sophos","sentinel","crowdstrike","csfalcon","carbonblack",@"\ESET\","ekrn","mbam","malwarebytes","bitdefender","norton","symantec","mcafee","antimalware","qax","sangfor"};
        internal static bool Protected(string text){return ProtectedTokens.Any(x=>(text??string.Empty).IndexOf(x,StringComparison.OrdinalIgnoreCase)>=0);}
        internal static string Hash(string text){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-","");}
        private static string Encode(string value){return Convert.ToBase64String(Encoding.UTF8.GetBytes(value));}
        private static string Decode(string value){if(value==null||value.Length>40000)throw new ArgumentException("项目标识无效。");return Encoding.UTF8.GetString(Convert.FromBase64String(value));}
        private static ManagerChoice C(string value,string label){return new ManagerChoice {value=value,label=label};}
        public List<ManagerItem> Scan(string module)
        {
            if(module=="nvidia-CS2"||module=="nvidia-VALORANT"||module=="nvidia-global")using(var nvidia=Nvidia(module))return nvidia.Scan();
            if(module=="startup")return StartupScan();
            if(module=="tasks")return TaskScan();
            if(module=="network")return NetworkScan();
            if(module=="network-advanced")return AdvancedScan();
            if(module=="power")return PowerScan();
            if(module=="device-msi"||module=="devices"||module=="interrupt-affinity")return DeviceTuning.Scan(module);
            if(module=="services")return ServiceScan();
            if(module=="ifeo")return IfeoScan();
            throw new ArgumentException("未知管理模块。");
        }
        public ManagerItem Read(string module,string id)
        {
            if(module=="nvidia-CS2"||module=="nvidia-VALORANT"||module=="nvidia-global")using(var nvidia=Nvidia(module))return nvidia.Read(id);
            if(module=="startup")return StartupRead(id);
            if(module=="tasks")return TaskRead(id);
            if(module=="device-msi"||module=="devices"||module=="interrupt-affinity")return DeviceTuning.Read(module,id);
            var item=Scan(module).FirstOrDefault(x=>x.id==id);
            if(item==null)throw new InvalidOperationException("目标已消失，请刷新列表。");return item;
        }
        public string Desired(string module,string id,string choice)
        {
            var item=Read(module,id);
            if(!item.supported||!item.choices.Any(x=>x.value==choice))throw new ArgumentException("目标值不在此设备/项目提供的合法选项中。");
            return choice;
        }
        public void Write(string module,string id,string value)
        {
            if(module=="nvidia-CS2"||module=="nvidia-VALORANT"||module=="nvidia-global"){using(var nvidia=Nvidia(module))nvidia.Write(id,value);return;}
            if(module=="startup"){StartupWrite(id,value);return;}
            if(module=="tasks"){TaskWrite(id,value);return;}
            if(module=="network"){NetworkWrite(id,value);return;}
            if(module=="network-advanced"){AdvancedWrite(id,value);return;}
            if(module=="power"){Guid scheme;if(id!="active"||!Guid.TryParse(value,out scheme))throw new ArgumentException("电源目标无效。");new WindowsTuningBackend().Write("high-power",string.Empty,scheme.ToString());return;}
            if(module=="device-msi"||module=="devices"||module=="interrupt-affinity"){DeviceTuning.Write(module,id,value);return;}
            if(module=="services"){var item=Read(module,id);if(!item.supported)throw new InvalidOperationException(item.blocked);WindowsTuningBackend.WriteOptionalService(id,value);return;}
            if(module=="ifeo"){IfeoWrite(id,value);return;}
            throw new ArgumentException("未知管理模块。");
        }
        private static RegistryKey StartupHive(string code)
        {
            if(code=="cu64")return RegistryKey.OpenBaseKey(RegistryHive.CurrentUser,RegistryView.Registry64);
            if(code=="lm64")return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
            if(code=="lm32")return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32);
            throw new ArgumentException("启动项位置无效。");
        }
        private static string[] StartupId(string id)
        {
            string[] parts=(id??string.Empty).Split(':');if(parts.Length!=2)throw new ArgumentException("启动项标识无效。");
            string name=Decode(parts[1]);if(name.Length==0||name.Length>16383||name.Contains("\0"))throw new ArgumentException("启动项名称无效。");
            return new[]{parts[0],name};
        }
        private static List<ManagerItem> StartupScan()
        {
            var result=new List<ManagerItem>();
            foreach(string code in new[]{"cu64","lm64","lm32"})
                using(var hive=StartupHive(code))using(var key=hive.OpenSubKey(RunKey))
                    if(key!=null)foreach(string name in key.GetValueNames())result.Add(StartupRead(code+":"+Encode(name)));
            return result;
        }
        private static ManagerItem StartupRead(string id)
        {
            string[] parts=StartupId(id);string raw="missing",command=string.Empty;bool supported=true;
            using(var hive=StartupHive(parts[0]))using(var key=hive.OpenSubKey(RunKey))
            {
                object value=key==null?null:key.GetValue(parts[1],null,RegistryValueOptions.DoNotExpandEnvironmentNames);
                if(value!=null)
                {
                    RegistryValueKind kind=key.GetValueKind(parts[1]);supported=kind==RegistryValueKind.String||kind==RegistryValueKind.ExpandString;
                    command=Convert.ToString(value,CultureInfo.InvariantCulture);raw=(kind==RegistryValueKind.ExpandString?"expand:":"string:")+Encode(command);
                }
            }
            bool protect=Protected(parts[1]+" "+command);
            return new ManagerItem {id=id,title=parts[1],detail=(parts[0]=="cu64"?"当前用户":"所有用户")+" · Run 登录启动项 · "+command,current=raw=="missing"?"已移出 Run 键":"存在于 Run 键",Raw=raw,Identity=Hash(parts[0]+"|"+parts[1]),supported=supported&&!protect,blocked=protect?"安全软件、反作弊或工具自身启动项受保护。":!supported?"不支持此注册表值类型。":null,effect="下次登录；不结束当前运行的程序",choices=new List<ManagerChoice>{C("missing","移出登录启动项（保存原值）")}};
        }
        private static void StartupWrite(string id,string value)
        {
            var current=StartupRead(id);if(!current.supported)throw new InvalidOperationException(current.blocked);
            string[] parts=StartupId(id);RegistryValueKind kind=RegistryValueKind.String;string command=null;
            if(value!="missing")
            {
                int colon=value.IndexOf(':');if(colon<0||!(value.StartsWith("string:")||value.StartsWith("expand:")))throw new ArgumentException("启动项备份值无效。");
                command=Decode(value.Substring(colon+1));kind=value.StartsWith("expand:")?RegistryValueKind.ExpandString:RegistryValueKind.String;
                if(Protected(parts[1]+" "+command))throw new InvalidOperationException("受保护的启动项不会被更改。");
            }
            using(var hive=StartupHive(parts[0]))using(var key=hive.CreateSubKey(RunKey))
                if(value=="missing")key.DeleteValue(parts[1],false);else key.SetValue(parts[1],command,kind);
        }
        private static void Release(object value){if(value!=null&&Marshal.IsComObject(value))Marshal.ReleaseComObject(value);}
        private static T TaskService<T>(Func<dynamic,T> call)
        {
            object service=Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service",true));
            try{((dynamic)service).Connect();return call(service);}finally{Release(service);}
        }
        internal static bool TaskProtected(string path,string xml)
        {
            string actions=string.Empty;
            if(!string.IsNullOrEmpty(xml))
            {
                var document=new XmlDocument {XmlResolver=null};using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))document.Load(reader);
                var namespaces=new XmlNamespaceManager(document.NameTable);namespaces.AddNamespace("t",document.DocumentElement.NamespaceURI);
                actions=string.Join(" ",document.SelectNodes("/t:Task/t:Actions//text()",namespaces).Cast<XmlNode>().Select(x=>x.Value).ToArray());
            }
            if(Protected(path+" "+actions))return true;
            if(!path.StartsWith(@"\Microsoft\Windows\",StringComparison.OrdinalIgnoreCase))return false;
            return !(path.StartsWith(@"\Microsoft\Windows\Customer Experience Improvement Program\",StringComparison.OrdinalIgnoreCase)||path.Equals(@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",StringComparison.OrdinalIgnoreCase));
        }
        internal static string TaskIdentity(string xml)
        {
            var document=new XmlDocument {XmlResolver=null};using(var reader=XmlReader.Create(new StringReader(xml),new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null}))document.Load(reader);
            var namespaces=new XmlNamespaceManager(document.NameTable);namespaces.AddNamespace("t",document.DocumentElement.NamespaceURI);
            foreach(XmlNode node in document.SelectNodes("/t:Task/t:Settings/t:Enabled",namespaces))node.ParentNode.RemoveChild(node);
            return Hash(document.OuterXml);
        }
        private static ManagerItem TaskItem(dynamic task)
        {
            string path=task.Path,xml=task.Xml;bool enabled=task.Enabled,protect=TaskProtected(path,xml);
            return new ManagerItem {id=path,title=task.Name,detail=path,current=enabled?"已启用":"已禁用",Raw=enabled?"true":"false",Identity=TaskIdentity(xml),supported=!protect,blocked=protect?"Windows 关键任务、安全或反作弊任务受保护。":null,effect="只改变将来触发；不停止正在运行的任务",choices=new List<ManagerChoice>{C("false","禁用将来触发"),C("true","启用任务")}};
        }
        private static void TaskFolder(dynamic folder,List<ManagerItem> result,int depth)
        {
            if(depth>32||result.Count>4096)return;object tasks=null,folders=null;
            try
            {
                tasks=folder.GetTasks(1);
                for(int i=1;i<=((dynamic)tasks).Count;i++){object task=null;try{task=((dynamic)tasks).Item(i);result.Add(TaskItem(task));}catch(System.Runtime.InteropServices.COMException){}finally{Release(task);}}
                folders=folder.GetFolders(0);
                for(int i=1;i<=((dynamic)folders).Count;i++){object child=null;try{child=((dynamic)folders).Item(i);TaskFolder(child,result,depth+1);}catch(System.Runtime.InteropServices.COMException){}finally{Release(child);}}
            }
            finally{Release(tasks);Release(folders);}
        }
        private static List<ManagerItem> TaskScan()
        {return TaskService(delegate(dynamic service){var result=new List<ManagerItem>();object root=service.GetFolder("\\");try{TaskFolder(root,result,0);}finally{Release(root);}return result;});}
        private static ManagerItem TaskRead(string id)
        {
            if(string.IsNullOrEmpty(id)||!id.StartsWith("\\")||id.Contains("\0"))throw new ArgumentException("任务标识无效。");
            return TaskService(delegate(dynamic service){object folder=service.GetFolder("\\"),task=null;try{task=((dynamic)folder).GetTask(id);return TaskItem(task);}finally{Release(task);Release(folder);}});
        }
        private static void TaskWrite(string id,string value)
        {
            if(value!="true"&&value!="false")throw new ArgumentException("任务状态无效。");
            var item=TaskRead(id);if(!item.supported)throw new InvalidOperationException(item.blocked);
            TaskService(delegate(dynamic service){object folder=service.GetFolder("\\"),task=null;try{task=((dynamic)folder).GetTask(id);((dynamic)task).Enabled=value=="true";return true;}finally{Release(task);Release(folder);}});
        }
        private static string NetworkRaw(string guid)
        {
            Guid parsed;if(!Guid.TryParse(guid,out parsed))throw new ArgumentException("网卡 GUID 无效。");
            using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces\"+parsed.ToString("B")))
            {string value=key==null?string.Empty:Convert.ToString(key.GetValue("NameServer",string.Empty));return string.IsNullOrWhiteSpace(value)?"automatic":"dns:"+string.Join(",",value.Split(new[]{',',';',' '},StringSplitOptions.RemoveEmptyEntries));}
        }
        private static List<ManagerItem> NetworkScan()
        {
            var result=new List<ManagerItem>();
            using(var searcher=new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True"))using(var rows=searcher.Get())
                foreach(ManagementObject row in rows)using(row)
                {
                    string guid=Convert.ToString(row["SettingID"]);Guid parsed;if(!Guid.TryParse(guid,out parsed))continue;guid=parsed.ToString("D");string raw=NetworkRaw(guid);
                    result.Add(new ManagerItem {id=guid,title=Convert.ToString(row["Description"]),detail="IPv4 DNS · 当前解析服务器："+string.Join(", ",row["DNSServerSearchOrder"] as string[]??new string[0]),current=raw=="automatic"?"自动 / DHCP":"手动 · "+raw.Substring(4),Raw=raw,Identity=Hash(guid+"|"+Convert.ToString(row["MACAddress"])),supported=true,effect="可能短暂影响域名解析；不更改 IP、网关或 IPv6",choices=new List<ManagerChoice>{C("automatic","自动获取 DNS"),C("dns:223.5.5.5,223.6.6.6","阿里公共 DNS · 223.5.5.5 / 223.6.6.6"),C("dns:1.1.1.1,1.0.0.1","Cloudflare DNS · 1.1.1.1 / 1.0.0.1"),C("dns:8.8.8.8,8.8.4.4","Google DNS · 8.8.8.8 / 8.8.4.4")}});
                }
            return result;
        }
        internal static string[] DnsValues(string raw)
        {
            if(raw=="automatic")return null;
            if(raw==null||!raw.StartsWith("dns:"))throw new ArgumentException("DNS 配置格式无效。");
            string[] values=raw.Substring(4).Split(',');IPAddress address;
            if(values.Length<1||values.Length>8||values.Any(x=>!IPAddress.TryParse(x,out address)||address.AddressFamily!=System.Net.Sockets.AddressFamily.InterNetwork))throw new ArgumentException("只支持有效的 IPv4 DNS 地址。");return values;
        }
        private static void NetworkWrite(string id,string value)
        {
            var current=new WindowsManagerBackend().Read("network",id);string[] dns=DnsValues(value);
            using(var searcher=new ManagementObjectSearcher("SELECT * FROM Win32_NetworkAdapterConfiguration WHERE IPEnabled=True"))using(var rows=searcher.Get())
                foreach(ManagementObject row in rows)using(row)
                    if(string.Equals(Convert.ToString(row["SettingID"]).Trim('{','}'),id,StringComparison.OrdinalIgnoreCase))
                    {using(var parameters=row.GetMethodParameters("SetDNSServerSearchOrder")){parameters["DNSServerSearchOrder"]=dns;using(var result=row.InvokeMethod("SetDNSServerSearchOrder",parameters,null)){uint status=Convert.ToUInt32(result["ReturnValue"]);if(status>1)throw new InvalidOperationException("Windows DNS 设置失败："+status);}}return;}
            throw new InvalidOperationException("网卡已消失。");
        }
        private static List<ManagerItem> AdvancedScan()
        {
            var result=new List<ManagerItem>();
            using(var searcher=new ManagementObjectSearcher(@"root\StandardCimv2","SELECT * FROM MSFT_NetAdapterAdvancedPropertySettingData"))using(var rows=searcher.Get())
                foreach(ManagementObject row in rows)using(row)
                {
                    string instance=Convert.ToString(row["InstanceID"]),keyword=Convert.ToString(row["RegistryKeyword"]),name=Convert.ToString(row["DisplayName"]);
                    string[] values=row["RegistryValue"] as string[]??new string[0],valid=row["ValidRegistryValues"] as string[]??new string[0],labels=row["ValidDisplayValues"] as string[]??new string[0];
                    var choices=valid.Select((v,i)=>C(Encode(v),i<labels.Length?labels[i]:v)).ToList();
                    result.Add(new ManagerItem {id=Hash(instance),title=string.IsNullOrEmpty(name)?keyword:name,detail=Convert.ToString(row["Name"])+" · "+keyword,current=Convert.ToString(row["DisplayValue"]),Raw=Encode(string.Join("\0",values)),Identity=Hash(instance+"|"+keyword),supported=choices.Count>0&&values.Length==1,blocked=choices.Count==0?"此参数不是驱动提供的枚举项；暂不接收任意数值。":values.Length!=1?"此参数包含复合值；暂不修改。":null,effect="驱动高级属性；可能需要手动重连或重启，可能降低吞吐/影响唤醒",choices=choices});
                }
            return result;
        }
        private static List<ManagerItem> PowerScan()
        {
            string current=WindowsTuningBackend.ActivePower().ToString();bool high=WindowsTuningBackend.HasHighPower();
            return new List<ManagerItem>{new ManagerItem {id="active",title="当前电源计划会话",detail="监控用的电源原值单独保存；异常退出后仍可从这里还原。",current=current,Raw=current,Identity=Hash("active-power-plan"),supported=true,effect="Windows 全局，影响功耗和温度",choices=high?new List<ManagerChoice>{C("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c","切换已有高性能计划")}:new List<ManagerChoice>()}};
        }
        private static List<ManagerItem> ServiceScan()
        {
            var optional=new HashSet<string>(new[]{"WSearch","SysMain","DiagTrack","MapsBroker","Fax","WMPNetworkSvc","RetailDemo","WerSvc","Spooler","SSDPSRV","upnphost","RemoteRegistry","lfsvc","PhoneSvc","TrkWks","XblAuthManager","XblGameSave","XboxGipSvc","XboxNetApiSvc"},StringComparer.OrdinalIgnoreCase);
            var result=new List<ManagerItem>();
            using(var searcher=new ManagementObjectSearcher("SELECT Name,DisplayName,PathName,State FROM Win32_Service"))using(var rows=searcher.Get())
                foreach(ManagementObject row in rows)using(row)
                {
                    string name=Convert.ToString(row["Name"]),path=Convert.ToString(row["PathName"]),state=Convert.ToString(row["State"]),raw="unavailable",blocked=null;
                    bool supported=optional.Contains(name)&&!Protected(name+" "+path);
                    try{raw=WindowsTuningBackend.ReadOptionalService(name);}catch(Exception ex){supported=false;blocked=ex.Message;}
                    if(!supported&&blocked==null)blocked="仅修改明确定义的可选服务；关键系统、安全、音频、网络及未知依赖的服务只读。";
                    result.Add(new ManagerItem {id=name,title=Convert.ToString(row["DisplayName"]),detail=name+" · 当前运行状态 "+state,current=raw=="unavailable"?"不可读取":WindowsTuningBackend.DisplayValue(raw),Raw=raw,Identity=Hash(name+"|"+path),supported=supported,blocked=blocked,effect="只更改将来的启动配置，不强停当前服务；禁用可能影响搜索、打印、定位、Xbox 或媒体共享。重启后核对。",choices=new List<ManagerChoice>{C("svc:4:0","禁用启动（功能可能不可用）"),C("svc:3:0","手动 / 触发启动"),C("svc:2:0","自动启动"),C("svc:2:1","自动（延迟启动）")}});
                }
            return result;
        }
        private static List<ManagerItem> IfeoScan()
        {
            var items=new List<ManagerItem>();foreach(string game in new[]{"CS2","VALORANT"})
            {
                string keyPath=@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\"+GameOptimizer.ExecutableName(game);
                string raw="missing",detail="没有显式调试器启动重定向";
                using(var key=Registry.LocalMachine.OpenSubKey(keyPath))
                {
                    object debugger=key==null?null:key.GetValue("Debugger",null,RegistryValueOptions.DoNotExpandEnvironmentNames);
                    if(debugger!=null)
                    {
                        var kind=key.GetValueKind("Debugger");
                        if(kind!=RegistryValueKind.String&&kind!=RegistryValueKind.ExpandString){items.Add(new ManagerItem {id=game,title=game+" IFEO 调试器启动项",current="不支持的类型",Raw="unsupported",Identity=Hash(keyPath),blocked="现有调试器值不是字符串，不更改。"});continue;}
                        raw=(kind==RegistryValueKind.ExpandString?"expand:":"string:")+Encode((string)debugger);detail=(string)debugger;
                    }
                }
                items.Add(new ManagerItem {id=game,title=game+" IFEO 调试器启动项",detail=detail,current=raw=="missing"?"未配置":"存在调试器重定向",Raw=raw,Identity=Hash(keyPath),supported=true,effect="影响下一次游戏启动；用于排查异常重定向，不是帧率开关。依赖调试器的环境勿移除。",choices=new List<ManagerChoice>{C("missing","移除当前调试器重定向（备份原值）")}});
            }
            return items;
        }
        private static void IfeoWrite(string game,string value)
        {
            GameOptimizer.ExecutableName(game);string command=null;RegistryValueKind kind=RegistryValueKind.String;
            if(value!="missing"){if(!value.StartsWith("string:")&&!value.StartsWith("expand:"))throw new ArgumentException("IFEO 备份值无效。");command=Decode(value.Substring(value.IndexOf(':')+1));kind=value.StartsWith("expand:")?RegistryValueKind.ExpandString:RegistryValueKind.String;}
            using(var key=Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Image File Execution Options\"+GameOptimizer.ExecutableName(game)))if(command==null)key.DeleteValue("Debugger",false);else key.SetValue("Debugger",command,kind);
        }
        private static void AdvancedWrite(string id,string value)
        {
            var current=new WindowsManagerBackend().Read("network-advanced",id);if(!current.supported)throw new InvalidOperationException(current.blocked);
            string raw=Decode(value);if(raw.Contains("\0"))throw new ArgumentException("不接受网卡复合目标值。");
            using(var searcher=new ManagementObjectSearcher(@"root\StandardCimv2","SELECT * FROM MSFT_NetAdapterAdvancedPropertySettingData"))using(var rows=searcher.Get())
                foreach(ManagementObject row in rows)using(row)
                    if(Hash(Convert.ToString(row["InstanceID"]))==id)
                    {
                        string[] valid=row["ValidRegistryValues"] as string[]??new string[0];if(!valid.Contains(raw))throw new ArgumentException("此驱动不支持目标值。");
                        row["RegistryValue"]=new[]{raw};var options=new PutOptions {Type=PutType.UpdateOnly};options.Context.Add("NoRestart",true);row.Put(options);return;
                    }
            throw new InvalidOperationException("网卡高级属性已消失。");
        }
        internal static object NetworkTest(string host)
        {
            IPAddress address;
            if(host==null||host.Length>253||(!IPAddress.TryParse(host,out address)&&Uri.CheckHostName(host)!=UriHostNameType.Dns))throw new ArgumentException("请输入有效的 IP 或主机名，不要填写网址或命令。");
            var milliseconds=new List<long>();int lost=0;
            using(var ping=new Ping())for(int i=0;i<8;i++){try{var reply=ping.Send(host,750);if(reply.Status==IPStatus.Success)milliseconds.Add(reply.RoundtripTime);else lost++;}catch(PingException){lost++;}}
            double average=milliseconds.Count==0?0:milliseconds.Average();
            return new {host,sent=8,received=milliseconds.Count,lost,averageMs=average,minMs=milliseconds.Count==0?0:milliseconds.Min(),maxMs=milliseconds.Count==0?0:milliseconds.Max(),jitterMs=milliseconds.Count<2?0:milliseconds.Zip(milliseconds.Skip(1),(a,b)=>Math.Abs(a-b)).Average(),note="ICMP 往返延迟，不代表 CS2/VALORANT UDP 延迟或缓冲膨胀测试。"};
        }
    }
}
