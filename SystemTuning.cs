using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace NexaArena
{
    public sealed class TuningBackup
    {
        public string Id,Path,Original,Written;
    }
    public sealed class TuningBackups
    {
        public List<TuningBackup> Items=new List<TuningBackup>();
    }
    public sealed class TuningProfile
    {
        public int Version;
        public string Game;
        public List<string> Ids;
        internal void Validate()
        {
            if(Version!=1||(Game!="CS2"&&Game!="VALORANT")||Ids==null||Ids.Count>SystemTuning.Definitions.Length||Ids.Distinct().Count()!=Ids.Count||Ids.Any(id=>!SystemTuning.Definitions.Any(d=>d.Id==id)))
                throw new ArgumentException("方案格式、版本或优化项无效；未应用任何设置。");
        }
        internal static TuningProfile Read(Stream stream)
        {
            var settings=new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536};
            using(var reader=System.Xml.XmlReader.Create(stream,settings))
            {var profile=(TuningProfile)new XmlSerializer(typeof(TuningProfile)).Deserialize(reader);profile.Validate();return profile;}
        }
    }
    internal sealed class TuningDefinition
    {
        public string Id,Title,Category,Description;
        public bool PerGame,Restart;
        public string Risk="常规",Effect="立即或重新打开相关界面",Source="Windows 设置";
    }
    internal interface ITuningBackend
    {
        string Read(string id,string path);
        string Desired(string id,string original);
        void Write(string id,string path,string value);
    }
    internal sealed class SystemTuning
    {
        private readonly ITuningBackend backend;
        private readonly string file;
        private readonly TuningBackups backups;
        internal static readonly TuningDefinition[] Definitions={
            D("game-mode","启用 Windows 游戏模式","游戏","由 Windows 管理游戏期间的资源分配。"),
            D("game-capture","关闭 Windows 游戏录制","后台","关闭系统游戏录制；Win+G 录制和回溯片段会受到影响。"),
            D("gamebar-controller","关闭手柄唤起 Game Bar","后台","避免手柄按钮在游戏中意外打开 Game Bar。"),
            D("mouse-acceleration","关闭桌面鼠标加速","外设","作用于 Windows 指针；使用原始鼠标输入的游戏不会因此改变灵敏度。"),
            D("sticky-shortcut","关闭粘滞键快捷键","外设","关闭连按 Shift 的唤起快捷键，保留粘滞键功能本身。"),
            D("filter-shortcut","关闭筛选键快捷键","外设","关闭长按 Shift 的唤起快捷键，保留现有辅助功能状态。"),
            D("toggle-shortcut","关闭切换键快捷键","外设","关闭长按 Num Lock 的唤起快捷键。"),
            D("client-animation","关闭窗口内部动画","桌面","减少系统控件的界面动画，作用于桌面操作。"),
            D("transparency","关闭桌面透明效果","桌面","使用不透明的 Windows 界面表面。",false,true),
            D("gpu-high","游戏使用高性能 GPU","图形","为所选游戏 EXE 写入 Windows 图形性能首选项；多 GPU 电脑适用。",true,true),
            D("fullscreen","禁用该游戏全屏优化","图形","兼容性选项，可能改善或降低表现；更改后重启游戏并核对。",true,true),
            D("high-power","切换已有高性能电源计划","电源","仅使用系统已提供的高性能计划；影响整机功耗与温度。"),
            D("usb-suspend","交流电下关闭 USB 选择性暂停","电源","更改当前电源计划的交流电参数，增加外设耗电。"),
            D("pcie-link","交流电下关闭 PCIe 链路节能","电源","更改当前电源计划的交流电参数，可能增加功耗和温度。"),
            D("menu-animation","关闭菜单展开动画","桌面","减少桌面菜单动画；不改变游戏内动画。"),
            D("combo-animation","关闭下拉框动画","桌面","关闭支持该系统设置的下拉框展开效果。"),
            D("listbox-smooth","关闭列表平滑滚动","桌面","关闭传统 Windows 列表框的平滑滚动。"),
            D("selection-fade","关闭选中项目淡出","桌面","关闭传统菜单选中后淡出的效果。"),
            D("tooltip-animation","关闭提示框动画","桌面","关闭系统提示框弹出动画。"),
            D("tooltip-fade","关闭提示框淡入淡出","桌面","关闭支持系统设置的提示框渐隐效果。"),
            D("menu-fade","关闭菜单淡入淡出","桌面","只关闭菜单渐隐效果，不删除菜单功能。"),
            D("cursor-shadow","关闭鼠标指针阴影","外设","仅改变桌面指针显示；不改变游戏准心。"),
            D("drop-shadow","关闭传统菜单阴影","桌面","仅影响支持该系统设置的菜单；不保证改变所有现代窗口。"),
            S("service-search","关闭 Windows Search 索引","WSearch","停止下次启动后的后台索引；文件内容搜索会变慢，开始菜单搜索可能受影响。"),
            S("service-sysmain","关闭 SysMain 预加载","SysMain","停止下次启动后的预加载；应用冷启动可能变慢，仅用于排查磁盘后台负载。"),
            S("service-diagnostics","关闭连接体验诊断服务","DiagTrack","减少该服务的诊断活动；不代表关闭全部 Windows 诊断或遥测。"),
            S("service-maps","关闭下载地图服务","MapsBroker","禁用下载地图服务；离线地图更新和相关应用可能不可用。"),
            S("service-fax","关闭传真服务","Fax","不使用传真时可选；Windows 传真功能将不可用。"),
            S("service-media-share","关闭媒体网络共享","WMPNetworkSvc","禁用 Windows Media Player 网络共享；DLNA 媒体共享受影响。"),
            S("service-retail","关闭零售演示服务","RetailDemo","不使用零售演示模式时可选；缺少该组件时显示不可用。"),
            S("service-error-report","关闭 Windows 错误报告服务","WerSvc","停止下次启动后的该服务；应用崩溃报告与部分问题诊断受影响。"),
            P("cpu-min","交流电处理器最低状态 100%","可能减少降频，但明显增加待机功耗和温度；笔记本或 X3D 不建议盲目应用。"),
            P("cpu-max","交流电处理器最高状态 100%","允许当前计划使用完整处理器性能；不超频，不保证帧率收益。"),
            P("cpu-cooling","交流电优先主动散热","提高散热需求时优先加速风扇；噪声可能增加，固件可能不支持。"),
            P("cpu-parking","交流电最小非停泊核心 100%","关闭该计划的核心停泊；增加耗电，可能破坏 X3D/混合架构的调度收益。"),
            P("disk-idle","交流电不自动关闭硬盘","避免空闲硬盘停止旋转；增加耗电，SSD 不一定有实际效果。"),
            P("sleep-idle","交流电不自动睡眠","将当前计划空闲睡眠超时设为从不；离开电脑也不会自动睡眠。"),
            P("hibernate-idle","交流电不自动休眠","将当前计划自动休眠超时设为从不；不删除休眠文件，不改变手动休眠。"),
            P("display-idle","交流电不自动关闭屏幕","将当前计划关闭屏幕超时设为从不；增加显示器耗电，不改变分辨率。"),
            R("privacy-tailored","关闭诊断数据个性化建议","隐私","阻止基于诊断数据的个性化建议；不是关闭必需诊断数据，也不保证提升 FPS。"),
            R("privacy-search-history","关闭资源管理器搜索历史建议","隐私","不显示及保存资源管理器搜索框近期条目；不关闭文件搜索。"),
            R("background-apps","禁止商店应用后台运行","后台","限制 Windows 应用后台活动；消息、邮件等后台通知可能延迟。不影响普通 Win32 后台进程。")
        };
        private static TuningDefinition D(string id,string title,string category,string description,bool perGame=false,bool restart=false)
        {return new TuningDefinition {Id=id,Title=title,Category=category,Description=description,PerGame=perGame,Restart=restart};}
        private static TuningDefinition S(string id,string title,string service,string description)
        {return new TuningDefinition {Id=id,Title=title,Category="服务",Description=description,Risk="功能影响",Restart=true,Effect="重启 Windows 后；不强停正在运行的服务",Source="Service Control Manager · "+service};}
        private static TuningDefinition P(string id,string title,string description)
        {return new TuningDefinition {Id=id,Title=title,Category="电源",Description=description,Risk="功耗 / 温度",Effect="当前计划 · 仅交流电",Source="Windows Power API"};}
        private static TuningDefinition R(string id,string title,string category,string description)
        {return new TuningDefinition {Id=id,Title=title,Category=category,Description=description,Risk="功能影响",Restart=true,Effect="注销或重启 Windows 后",Source="Microsoft Windows 策略"};}
        public SystemTuning():this(new WindowsTuningBackend(),System.IO.Path.Combine(SavedState.DirectoryPath,"optimization-backups.xml")){}
        internal SystemTuning(ITuningBackend api,string path)
        {
            backend=api;file=path;
            if(File.Exists(file))using(var stream=File.OpenRead(file))backups=(TuningBackups)new XmlSerializer(typeof(TuningBackups)).Deserialize(stream);
            else backups=new TuningBackups();
            if(backups==null||backups.Items==null)throw new InvalidOperationException("优化备份无法读取，请保留文件并检查。");
        }
        private void AddBackup(TuningBackup saved)
        {backups.Items.Add(saved);try{Save();}catch{backups.Items.Remove(saved);throw;}}
        private void RemoveBackup(TuningBackup saved)
        {int index=backups.Items.IndexOf(saved);backups.Items.Remove(saved);try{Save();}catch{if(index>=0)backups.Items.Insert(index,saved);throw;}}
        private static TuningDefinition Definition(string id)
        {var d=Definitions.FirstOrDefault(x=>x.Id==id);if(d==null)throw new ArgumentException("未知优化项："+id);return d;}
        private static string Target(TuningDefinition d,string path)
        {
            if(!d.PerGame)return string.Empty;
            if(string.IsNullOrEmpty(path)||!File.Exists(path)||
                !(GameOptimizer.IsExecutable("CS2",path)||GameOptimizer.IsExecutable("VALORANT",path)))
                throw new InvalidOperationException("请先选择对应游戏的主程序 EXE。");
            return System.IO.Path.GetFullPath(path);
        }
        private TuningBackup Find(string id,string path)
        {return backups.Items.FirstOrDefault(x=>x.Id==id&&string.Equals(x.Path,path,StringComparison.OrdinalIgnoreCase));}
        private void Save()
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            string temp=file+".tmp";
            using(var stream=File.Create(temp))new XmlSerializer(typeof(TuningBackups)).Serialize(stream,backups);
            if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);
        }
        public object Catalog(string path)
        {
            return Definitions.Select(d=>{
                bool supported=true,optimized=false,applied=false;string state,error=null,currentValue=null,desiredValue=null;
                try
                {
                    string target=Target(d,path),current=backend.Read(d.Id,target);
                    optimized=current==backend.Desired(d.Id,current);applied=Find(d.Id,target)!=null;
                    currentValue=WindowsTuningBackend.DisplayValue(current);desiredValue=WindowsTuningBackend.DisplayValue(backend.Desired(d.Id,current));
                    state=optimized?"已是优化值":"可配置";
                }
                catch(Exception ex){supported=false;state="暂不可用";error=ex.Message;}
                string risk=d.Category=="电源"?"功耗 / 温度":d.Risk;
                return new {id=d.Id,title=d.Title,category=d.Category,description=d.Description,scope=d.PerGame?"所选游戏":"Windows 全局",restart=d.Restart,effect=d.PerGame?"重启所选游戏后":d.Effect,risk,source=d.Source,currentValue,desiredValue,supported,optimized,applied,state,error};
            }).ToArray();
        }
        public object Change(string[] ids,string path,bool restore)
        {
            if(ids==null||ids.Length>Definitions.Length)throw new ArgumentException("优化项列表无效。");
            var chosen=ids.Distinct().Select(Definition).OrderBy(x=>Array.IndexOf(Definitions,x)).ToArray();
            if(restore)Array.Reverse(chosen);
            var outcomes=new List<object>();
            foreach(var d in chosen)
            {
                try
                {
                    string target=Target(d,path),current=backend.Read(d.Id,target);
                    var saved=Find(d.Id,target);
                    if(saved!=null && saved.Written!=backend.Desired(d.Id,saved.Original))
                        throw new InvalidOperationException("备份校验失败，已保留文件且未修改设置。");
                    if(restore)
                    {
                        if(saved==null){outcomes.Add(new {id=d.Id,ok=true,message="没有需要还原的备份"});continue;}
                        if(current==saved.Original){RemoveBackup(saved);outcomes.Add(new {id=d.Id,ok=true,message="现值已是原值，已清理对应备份"});continue;}
                        if(current!=saved.Written)throw new InvalidOperationException("设置已被其他程序修改，已保留备份；请确认当前值。");
                        backend.Write(d.Id,target,saved.Original);
                        if(backend.Read(d.Id,target)!=saved.Original)throw new InvalidOperationException("原值恢复校验未通过，备份保留。");
                        RemoveBackup(saved);
                        outcomes.Add(new {id=d.Id,ok=true,message="已还原原值"});
                    }
                    else
                    {
                        if(saved!=null)
                        {
                            if(current!=saved.Written)throw new InvalidOperationException("现值与上次应用值不同，请先检查或还原。");
                            outcomes.Add(new {id=d.Id,ok=true,message="已应用"});continue;
                        }
                        string desired=backend.Desired(d.Id,current);
                        if(current==desired){outcomes.Add(new {id=d.Id,ok=true,message="现值已符合，无需修改"});continue;}
                        saved=new TuningBackup {Id=d.Id,Path=target,Original=current,Written=desired};
                        AddBackup(saved);
                        try
                        {
                            backend.Write(d.Id,target,desired);
                            if(backend.Read(d.Id,target)!=desired)throw new InvalidOperationException("写入校验未通过。");
                        }
                        catch
                        {
                            try {backend.Write(d.Id,target,current);if(backend.Read(d.Id,target)==current)RemoveBackup(saved);}catch { }
                            throw;
                        }
                        outcomes.Add(new {id=d.Id,ok=true,message="已应用并保存原值"});
                    }
                }
                catch(Exception ex){outcomes.Add(new {id=d.Id,ok=false,message=ex.Message});}
            }
            return new {outcomes,catalog=Catalog(path)};
        }
    }

    internal sealed class WindowsTuningBackend:ITuningBackend
    {
        [DllImport("user32.dll",SetLastError=true)] private static extern bool SystemParametersInfo(uint action,uint parameter,IntPtr value,uint flags);
        [DllImport("powrprof.dll")] private static extern uint PowerGetActiveScheme(IntPtr root,out IntPtr scheme);
        [DllImport("powrprof.dll")] private static extern uint PowerSetActiveScheme(IntPtr root,ref Guid scheme);
        [DllImport("powrprof.dll")] private static extern uint PowerEnumerate(IntPtr root,IntPtr scheme,IntPtr subgroup,uint access,uint index,byte[] buffer,ref uint size);
        [DllImport("powrprof.dll")] private static extern uint PowerReadACValueIndex(IntPtr root,ref Guid scheme,ref Guid subgroup,ref Guid setting,out uint value);
        [DllImport("powrprof.dll")] private static extern uint PowerWriteACValueIndex(IntPtr root,ref Guid scheme,ref Guid subgroup,ref Guid setting,uint value);
        [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
        private static readonly Guid HighPower=new Guid("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
        private static readonly Guid UsbGroup=new Guid("2a737441-1930-4402-8d77-b2bebba308a3"),UsbSetting=new Guid("48e6b7a6-50f5-4782-a5d4-53bb8f07e226");
        private static readonly Guid PciGroup=new Guid("501a4d13-42af-4429-9fd1-a8218c268e20"),PciSetting=new Guid("ee12f906-d277-404b-b6da-e5fa1a576df5");
        private static readonly Dictionary<string,uint> Effects=new Dictionary<string,uint> {
            {"menu-animation",0x1002},{"combo-animation",0x1004},{"listbox-smooth",0x1006},
            {"selection-fade",0x1014},{"tooltip-animation",0x1016},{"tooltip-fade",0x1018},
            {"menu-fade",0x1012},{"cursor-shadow",0x101a},{"drop-shadow",0x1024}
        };
        private static readonly Dictionary<string,string> Services=new Dictionary<string,string> {
            {"service-search","WSearch"},{"service-sysmain","SysMain"},{"service-diagnostics","DiagTrack"},
            {"service-maps","MapsBroker"},{"service-fax","Fax"},{"service-media-share","WMPNetworkSvc"},
            {"service-retail","RetailDemo"},{"service-error-report","WerSvc"}
        };
        private static readonly Dictionary<string,string[]> PowerOptions=new Dictionary<string,string[]> {
            {"cpu-min",new[]{"54533251-82be-4824-96c1-47b60b740d00","893dee8e-2bef-41e0-89c6-b55d0929964c","100"}},
            {"cpu-max",new[]{"54533251-82be-4824-96c1-47b60b740d00","bc5038f7-23e0-4960-96da-33abaf5935ec","100"}},
            {"cpu-cooling",new[]{"54533251-82be-4824-96c1-47b60b740d00","94d3a615-a899-4ac5-ae2b-e4d8f634367f","1"}},
            {"cpu-parking",new[]{"54533251-82be-4824-96c1-47b60b740d00","0cc5b647-c1df-4637-891a-dec35c318583","100"}},
            {"disk-idle",new[]{"0012ee47-9041-4b5d-9b77-535fba8b1442","6738e2c4-e8a5-4a42-b16a-e040e769756e","0"}},
            {"sleep-idle",new[]{"238c9fa8-0aad-41ed-83f4-97be242c8f20","29f6c1db-86da-48c5-9fdb-f2b67b1f44da","0"}},
            {"hibernate-idle",new[]{"238c9fa8-0aad-41ed-83f4-97be242c8f20","9d7815a6-7ee4-497e-8888-515a05f02364","0"}},
            {"display-idle",new[]{"7516b95f-f776-4464-8c53-06167f40cc99","3c0bc021-c8a8-4e07-a973-6b14cbcb2b7e","0"}}
        };
        private static readonly Dictionary<string,string[]> Policies=new Dictionary<string,string[]> {
            {"privacy-tailored",new[]{"user",@"Software\Policies\Microsoft\Windows\CloudContent","DisableTailoredExperiencesWithDiagnosticData","1"}},
            {"privacy-search-history",new[]{"user",@"Software\Policies\Microsoft\Windows\Explorer","DisableSearchBoxSuggestions","1"}},
            {"background-apps",new[]{"machine",@"Software\Policies\Microsoft\Windows\AppPrivacy","LetAppsRunInBackground","2"}}
        };
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr OpenSCManager(string machine,string database,uint access);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr OpenService(IntPtr manager,string name,uint access);
        [DllImport("advapi32.dll",SetLastError=true)] private static extern bool CloseServiceHandle(IntPtr handle);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool QueryServiceConfig(IntPtr service,IntPtr config,uint size,out uint needed);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool QueryServiceConfig2(IntPtr service,uint level,IntPtr info,uint size,out uint needed);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool ChangeServiceConfig(IntPtr service,uint type,uint start,uint error,string path,string group,IntPtr tag,string dependencies,string account,string password,string displayName);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool ChangeServiceConfig2(IntPtr service,uint level,IntPtr info);
        internal static string DisplayValue(string value)
        {
            if(value=="missing")return "未显式配置（Windows 默认）";
            if(value.Contains("|"))return string.Join(" / ",value.Split('|').Select(DisplayValue).ToArray());
            if(value.StartsWith("s:"))return RegText(value);
            if(value.StartsWith("d:"))return value.Substring(2);
            if(value.StartsWith("svc:")){var p=value.Split(':');return p[1]=="4"?"已禁用":p[1]=="3"?"手动 / 触发启动":p[2]=="1"?"自动（延迟启动）":"自动";}
            return value;
        }
        private static string EffectRead(uint action)
        {
            IntPtr p=Marshal.AllocHGlobal(4);try{Marshal.WriteInt32(p,0);if(!SystemParametersInfo(action,0,p,0))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());return Marshal.ReadInt32(p)==0?"0":"1";}finally{Marshal.FreeHGlobal(p);}
        }
        private static void EffectWrite(uint action,string value)
        {
            if(value!="0"&&value!="1")throw new ArgumentException("界面效果备份值无效。");
            if(!SystemParametersInfo(action,0,value=="0"?IntPtr.Zero:new IntPtr(1),3))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        private static void CheckPolicyEdition()
        {
            using(var key=Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
            {
                string edition=key==null?string.Empty:Convert.ToString(key.GetValue("EditionID"));
                if(!(edition.StartsWith("Professional",StringComparison.OrdinalIgnoreCase)||edition.StartsWith("Enterprise",StringComparison.OrdinalIgnoreCase)||edition.StartsWith("Education",StringComparison.OrdinalIgnoreCase)))
                    throw new InvalidOperationException("当前 Windows 版本不支持此策略；请使用系统隐私设置。");
            }
        }
        private static T WithService<T>(string name,bool write,Func<IntPtr,T> operation)
        {
            IntPtr manager=OpenSCManager(null,null,1);if(manager==IntPtr.Zero)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            try
            {
                IntPtr service=OpenService(manager,name,write?3u:1u);
                if(service==IntPtr.Zero){int error=Marshal.GetLastWin32Error();if(error==1060)throw new InvalidOperationException("此系统未安装该可选服务。");throw new System.ComponentModel.Win32Exception(error);}
                try{return operation(service);}finally{CloseServiceHandle(service);}
            }
            finally{CloseServiceHandle(manager);}
        }
        private static string ServiceRead(IntPtr service)
        {
            uint needed;QueryServiceConfig(service,IntPtr.Zero,0,out needed);
            int error=Marshal.GetLastWin32Error();if(error!=122||needed<12||needed>65536)throw new System.ComponentModel.Win32Exception(error);
            IntPtr p=Marshal.AllocHGlobal((int)needed);
            try
            {
                if(!QueryServiceConfig(service,p,needed,out needed))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                int type=Marshal.ReadInt32(p,0),start=Marshal.ReadInt32(p,4);
                if((type&0x30)==0||start<2||start>4)throw new InvalidOperationException("不支持更改该服务类型。");
                if(!QueryServiceConfig2(service,3,p,4,out needed))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                int delayed=Marshal.ReadInt32(p)==0?0:1;
                if(start!=2&&delayed!=0)throw new InvalidOperationException("该服务保留了延迟启动配置；请使用系统服务管理器。");
                return "svc:"+start+":"+delayed;
            }
            finally{Marshal.FreeHGlobal(p);}
        }
        private static void ServiceDelayed(IntPtr service,bool delayed)
        {
            IntPtr p=Marshal.AllocHGlobal(4);try{Marshal.WriteInt32(p,delayed?1:0);if(!ChangeServiceConfig2(service,3,p))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());}finally{Marshal.FreeHGlobal(p);}
        }
        private static void ServiceWrite(IntPtr service,string value)
        {WriteServiceState(value,delegate{return ServiceRead(service);},delegate(uint start){if(!ChangeServiceConfig(service,uint.MaxValue,start,uint.MaxValue,null,null,IntPtr.Zero,null,null,null,null))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());},delegate(bool delayed){ServiceDelayed(service,delayed);});}
        internal static void WriteServiceState(string value,Func<string> read,Action<uint> setStart,Action<bool> setDelayed)
        {
            string[] part=value.Split(':');uint start;
            if(part.Length!=3||part[0]!="svc"||!uint.TryParse(part[1],out start)||start<2||start>4||(part[2]!="0"&&part[2]!="1")||(start!=2&&part[2]!="0"))throw new ArgumentException("服务备份值无效。");
            if(read().EndsWith(":1"))setDelayed(false);
            setStart(start);
            if(start==2)setDelayed(part[2]=="1");
        }
        internal static string ReadOptionalService(string name){if(string.IsNullOrEmpty(name)||name.Length>256||name.Contains("\0")||name.Contains("\\"))throw new ArgumentException("服务名称无效。");return WithService(name,false,ServiceRead);}
        internal static void WriteOptionalService(string name,string value){ReadOptionalService(name);WithService(name,true,delegate(IntPtr handle){ServiceWrite(handle,value);return true;});}
        private static void Check(uint code){if(code!=0)throw new InvalidOperationException("Windows 操作失败："+code);}
        internal static Guid ActivePower()
        {
            IntPtr p;Check(PowerGetActiveScheme(IntPtr.Zero,out p));try{return (Guid)Marshal.PtrToStructure(p,typeof(Guid));}finally{LocalFree(p);}
        }
        internal static bool HasHighPower()
        {
            for(uint index=0;index<80;index++){uint size=16;var bytes=new byte[16];uint code=PowerEnumerate(IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,16,index,bytes,ref size);if(code!=0)return false;if(new Guid(bytes)==HighPower)return true;}return false;
        }
        private static string RegRead(string path,string name)
        {return RegRead(Registry.CurrentUser,path,name);}
        private static string RegRead(RegistryKey hive,string path,string name)
        {
            using(var key=hive.OpenSubKey(path))
            {
                object value=key==null?null:key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);
                if(value==null)return "missing";
                var kind=key.GetValueKind(name);
                if(kind==RegistryValueKind.DWord)return "d:"+Convert.ToInt32(value).ToString(CultureInfo.InvariantCulture);
                if(kind==RegistryValueKind.String)return "s:"+Convert.ToBase64String(Encoding.UTF8.GetBytes((string)value));
                throw new InvalidOperationException("现有设置类型不是预期类型，未修改。");
            }
        }
        private static void RegWrite(string path,string name,string value)
        {RegWrite(Registry.CurrentUser,path,name,value);}
        private static void RegWrite(RegistryKey hive,string path,string name,string value)
        {
            if(value!="missing"&&!value.StartsWith("d:")&&!value.StartsWith("s:"))throw new ArgumentException("备份值类型无效。");
            using(var key=hive.CreateSubKey(path))
            {
                if(value=="missing")key.DeleteValue(name,false);
                else if(value.StartsWith("d:"))key.SetValue(name,int.Parse(value.Substring(2),CultureInfo.InvariantCulture),RegistryValueKind.DWord);
                else if(value.StartsWith("s:"))key.SetValue(name,Encoding.UTF8.GetString(Convert.FromBase64String(value.Substring(2))),RegistryValueKind.String);
                else throw new ArgumentException("备份值类型无效。");
            }
        }
        private static string RegText(string value){return value=="missing"?string.Empty:Encoding.UTF8.GetString(Convert.FromBase64String(value.Substring(2)));}
        private static string TextValue(string text){return "s:"+Convert.ToBase64String(Encoding.UTF8.GetBytes(text));}
        private static uint AccessAction(string id,bool write)
        {return id=="sticky-shortcut"?(write?0x3bu:0x3au):id=="filter-shortcut"?(write?0x33u:0x32u):(write?0x35u:0x34u);}
        private static string ParametersRead(string id)
        {
            int size=id=="filter-shortcut"?24:id=="mouse-acceleration"?12:8;IntPtr p=Marshal.AllocHGlobal(size);
            try
            {
                for(int i=0;i<size;i+=4)Marshal.WriteInt32(p,i,0);
                if(id!="mouse-acceleration")Marshal.WriteInt32(p,size);
                if(!SystemParametersInfo(id=="mouse-acceleration"?3u:AccessAction(id,false),(uint)(id=="mouse-acceleration"?0:size),p,0))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                return string.Join(",",Enumerable.Range(0,size/4).Select(i=>Marshal.ReadInt32(p,i*4).ToString(CultureInfo.InvariantCulture)).ToArray());
            }
            finally{Marshal.FreeHGlobal(p);}
        }
        private static void ParametersWrite(string id,string value)
        {
            int[] data=value.Split(',').Select(x=>int.Parse(x,CultureInfo.InvariantCulture)).ToArray();
            int size=id=="filter-shortcut"?24:id=="mouse-acceleration"?12:8;
            if(data.Length*4!=size)throw new ArgumentException("辅助设置备份长度无效。");
            IntPtr p=Marshal.AllocHGlobal(size);
            try
            {
                for(int i=0;i<data.Length;i++)Marshal.WriteInt32(p,i*4,data[i]);
                if(!SystemParametersInfo(id=="mouse-acceleration"?4u:AccessAction(id,true),(uint)(id=="mouse-acceleration"?0:size),p,3))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            finally{Marshal.FreeHGlobal(p);}
        }
        public string Read(string id,string path)
        {
            uint effect;string service;string[] option;
            if(Effects.TryGetValue(id,out effect))return EffectRead(effect);
            if(Services.TryGetValue(id,out service))return WithService(service,false,ServiceRead);
            if(Policies.TryGetValue(id,out option)){CheckPolicyEdition();return RegRead(option[0]=="machine"?Registry.LocalMachine:Registry.CurrentUser,option[1],option[2]);}
            if(PowerOptions.TryGetValue(id,out option))
            {Guid scheme=ActivePower(),group=new Guid(option[0]),setting=new Guid(option[1]);uint index;Check(PowerReadACValueIndex(IntPtr.Zero,ref scheme,ref group,ref setting,out index));return scheme+":"+index;}
            switch(id)
            {
                case "game-mode":return RegRead(@"Software\Microsoft\GameBar","AutoGameModeEnabled");
                case "gamebar-controller":return RegRead(@"Software\Microsoft\GameBar","UseNexusForGameBarEnabled");
                case "game-capture":return RegRead(@"Software\Microsoft\Windows\CurrentVersion\GameDVR","AppCaptureEnabled")+"|"+RegRead(@"System\GameConfigStore","GameDVR_Enabled");
                case "transparency":return RegRead(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency");
                case "gpu-high":return RegRead(@"Software\Microsoft\DirectX\UserGpuPreferences",path);
                case "fullscreen":return RegRead(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",path);
                case "mouse-acceleration":case "sticky-shortcut":case "filter-shortcut":case "toggle-shortcut":return ParametersRead(id);
                case "client-animation":
                    IntPtr p=Marshal.AllocHGlobal(4);try{if(!SystemParametersInfo(0x1042,0,p,0))throw new System.ComponentModel.Win32Exception();return Marshal.ReadInt32(p).ToString();}finally{Marshal.FreeHGlobal(p);}
                case "high-power":if(!HasHighPower())throw new InvalidOperationException("系统未提供高性能计划；可使用 Windows 电源模式设置。");return ActivePower().ToString();
                case "usb-suspend":case "pcie-link":
                    Guid active=ActivePower(),group=id=="usb-suspend"?UsbGroup:PciGroup,setting=id=="usb-suspend"?UsbSetting:PciSetting;uint index;Check(PowerReadACValueIndex(IntPtr.Zero,ref active,ref group,ref setting,out index));return active+":"+index;
                default:throw new ArgumentException("未知优化项。");
            }
        }
        public string Desired(string id,string original)
        {
            if(Effects.ContainsKey(id))return "0";
            if(Services.ContainsKey(id))return "svc:4:0";
            string[] option;
            if(Policies.TryGetValue(id,out option))return "d:"+option[3];
            if(PowerOptions.TryGetValue(id,out option))return original.Substring(0,original.LastIndexOf(':')+1)+option[2];
            switch(id)
            {
                case "game-mode":return "d:1";
                case "game-capture":return "d:0|d:0";
                case "gamebar-controller":case "transparency":return "d:0";
                case "gpu-high":
                    string gpu=string.Join(";",RegText(original).Split(';').Where(x=>x.Length>0&&!x.TrimStart().StartsWith("GpuPreference=",StringComparison.OrdinalIgnoreCase)).ToArray());return TextValue((gpu.Length==0?"":gpu+";")+"GpuPreference=2;");
                case "fullscreen":
                    string flags=RegText(original);return flags.Split(' ').Contains("DISABLEDXMAXIMIZEDWINDOWEDMODE")?original:TextValue((flags.Length==0?"~":flags.Trim())+" DISABLEDXMAXIMIZEDWINDOWEDMODE");
                case "mouse-acceleration":return "0,0,0";
                case "sticky-shortcut":case "filter-shortcut":case "toggle-shortcut":
                    int[] words=original.Split(',').Select(int.Parse).ToArray();words[1]&=~4;return string.Join(",",words.Select(x=>x.ToString(CultureInfo.InvariantCulture)).ToArray());
                case "client-animation":return "0";
                case "high-power":return HighPower.ToString();
                case "usb-suspend":case "pcie-link":return original.Substring(0,original.LastIndexOf(':')+1)+"0";
                default:throw new ArgumentException("未知优化项。");
            }
        }
        public void Write(string id,string path,string value)
        {
            uint effect;string service;string[] option;
            if(Effects.TryGetValue(id,out effect)){EffectWrite(effect+1,value);return;}
            if(Services.TryGetValue(id,out service)){WithService(service,true,delegate(IntPtr handle){ServiceWrite(handle,value);return true;});return;}
            if(Policies.TryGetValue(id,out option)){CheckPolicyEdition();RegWrite(option[0]=="machine"?Registry.LocalMachine:Registry.CurrentUser,option[1],option[2],value);return;}
            if(PowerOptions.TryGetValue(id,out option))
            {
                string[] values=value.Split(':');Guid scheme;uint index;
                if(values.Length!=2||!Guid.TryParse(values[0],out scheme)||!uint.TryParse(values[1],out index))throw new ArgumentException("电源备份值无效。");
                Guid group=new Guid(option[0]),setting=new Guid(option[1]);Check(PowerWriteACValueIndex(IntPtr.Zero,ref scheme,ref group,ref setting,index));if(ActivePower()==scheme)Check(PowerSetActiveScheme(IntPtr.Zero,ref scheme));return;
            }
            switch(id)
            {
                case "game-mode":RegWrite(@"Software\Microsoft\GameBar","AutoGameModeEnabled",value);break;
                case "gamebar-controller":RegWrite(@"Software\Microsoft\GameBar","UseNexusForGameBarEnabled",value);break;
                case "game-capture":string[] capture=value.Split('|');if(capture.Length!=2)throw new ArgumentException("录制设置备份无效。");RegWrite(@"Software\Microsoft\Windows\CurrentVersion\GameDVR","AppCaptureEnabled",capture[0]);RegWrite(@"System\GameConfigStore","GameDVR_Enabled",capture[1]);break;
                case "transparency":RegWrite(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize","EnableTransparency",value);break;
                case "gpu-high":RegWrite(@"Software\Microsoft\DirectX\UserGpuPreferences",path,value);break;
                case "fullscreen":RegWrite(@"Software\Microsoft\Windows NT\CurrentVersion\AppCompatFlags\Layers",path,value);break;
                case "mouse-acceleration":case "sticky-shortcut":case "filter-shortcut":case "toggle-shortcut":ParametersWrite(id,value);break;
                case "client-animation":if(!SystemParametersInfo(0x1043,0,value=="0"?IntPtr.Zero:new IntPtr(1),3))throw new System.ComponentModel.Win32Exception();break;
                case "high-power":Guid scheme=new Guid(value);Check(PowerSetActiveScheme(IntPtr.Zero,ref scheme));break;
                case "usb-suspend":case "pcie-link":
                    string[] part=value.Split(':');Guid active=new Guid(part[0]),group=id=="usb-suspend"?UsbGroup:PciGroup,setting=id=="usb-suspend"?UsbSetting:PciSetting;uint index=uint.Parse(part[1]);Check(PowerWriteACValueIndex(IntPtr.Zero,ref active,ref group,ref setting,index));if(ActivePower()==active)Check(PowerSetActiveScheme(IntPtr.Zero,ref active));break;
                default:throw new ArgumentException("未知优化项。");
            }
        }
    }
}
