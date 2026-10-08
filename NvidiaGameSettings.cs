using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NexaArena
{
    // Public DRS ABI; the control-panel background-limit pair uses the
    // compatible DRS entry points also used by NVIDIA Profile Inspector. No injection.
    internal sealed class NvidiaGameSettings:IDisposable
    {
        internal const int SettingSize=12320,CurrentValueOffset=8220,ApplicationSize=20492,ProfileSize=4116;
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern IntPtr LoadLibrary(string name);
        [DllImport("kernel32.dll",CharSet=CharSet.Ansi,ExactSpelling=true)] private static extern IntPtr GetProcAddress(IntPtr library,string name);
        [DllImport("kernel32.dll")] private static extern bool FreeLibrary(IntPtr library);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate IntPtr Query(uint id);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Simple();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Create(out IntPtr session);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Session(IntPtr session);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Find(IntPtr session,IntPtr path,out IntPtr profile,IntPtr application);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int BaseProfile(IntPtr session,out IntPtr profile);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int ProfileInfo(IntPtr session,IntPtr profile,IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EnumApplications(IntPtr session,IntPtr profile,uint startIndex,ref uint count,IntPtr applications);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Get(IntPtr session,IntPtr profile,uint id,IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CompatibleGet(IntPtr session,IntPtr profile,uint id,IntPtr info,ref uint flags);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int CompatibleSet(IntPtr session,IntPtr profile,IntPtr info,uint flags,uint reserved);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Set(IntPtr session,IntPtr profile,IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Delete(IntPtr session,IntPtr profile,uint id);
        private IntPtr library,session,profile;
        private Query query;
        private string profileName;
        private uint applications;
        private bool predefinedProfile;
        private List<ApplicationAssociation> associations=new List<ApplicationAssociation>();
        private string associationError;
        internal sealed class ApplicationAssociation
        {
            internal string Name,Launcher,FileInFolder,CommandLine;
            internal uint Flags;
            internal bool Predefined;
        }
        private readonly string game,path;
        internal static readonly uint[] Settings={0x1057eb71,0x00a879cf,0x10835002,0x00198fff,0x00ce2691,0x00e73211,0x0084cd70,0x0019bb68,
            0x10d2bb16,0x1074c972,0x107d639d,0x10fc2d9c,0x0098c1ac,0x20c1221e,0x20fdd1f9,0x002ecaf2,0x0064b541,0x10835005,0x00ac8497};
        internal NvidiaGameSettings(string game,string path)
        {
            this.game=game;this.path=path;
            if(game!="GLOBAL"&&(!GameOptimizer.IsExecutable(game,path)||!File.Exists(path)))throw new InvalidOperationException("请先在目标游戏中选择真实主程序 EXE，再打开 NVIDIA 设置。");
            library=LoadLibrary(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"nvapi64.dll"));
            if(library==IntPtr.Zero)throw new InvalidOperationException("未检测到可用 NVIDIA 64 位驱动接口。");
            try
            {
                IntPtr export=GetProcAddress(library,"nvapi_QueryInterface");if(export==IntPtr.Zero)throw new InvalidOperationException("NVIDIA 驱动接口不可用。");
                query=(Query)Marshal.GetDelegateForFunctionPointer(export,typeof(Query));Check(Function<Simple>(0x0150e828)());Check(Function<Create>(0x0694d52e)(out session));Check(Function<Session>(0x375dbd6b)(session));
                if(game=="GLOBAL")Check(Function<BaseProfile>(0x617bff9f)(session,out profile));
                else
                {
                    string fullPath=Path.GetFullPath(path);if(fullPath.Length>=2048)throw new ArgumentException("游戏路径超过 NVIDIA 接口长度上限。");
                    IntPtr app=Buffer(ApplicationSize,4),text=Marshal.AllocHGlobal(4096);
                    try{Marshal.Copy(new byte[4096],0,text,4096);byte[] unicode=System.Text.Encoding.Unicode.GetBytes(fullPath);Marshal.Copy(unicode,0,text,unicode.Length);Check(Function<Find>(0xeee566b2)(session,text,out profile,app));}finally{Marshal.FreeHGlobal(app);Marshal.FreeHGlobal(text);}
                }
                IntPtr info=Buffer(ProfileSize,1);try{Check(Function<ProfileInfo>(0x61cd6fd6)(session,profile,info));profileName=Marshal.PtrToStringUni(IntPtr.Add(info,4),2048).TrimEnd('\0');predefinedProfile=Marshal.ReadInt32(info,4104)!=0;applications=unchecked((uint)Marshal.ReadInt32(info,4108));}finally{Marshal.FreeHGlobal(info);}
                if(game!="GLOBAL")
                    try{associations=ReadApplications();}
                    catch(Exception ex){associationError="无法完整核对驱动配置关联的程序，此处只读："+ex.Message;}
            }
            catch{Dispose();throw;}
        }
        private T Function<T>(uint id) where T:class
        {IntPtr pointer=query(id);if(pointer==IntPtr.Zero)throw new InvalidOperationException("此 NVIDIA 驱动不支持所需接口。");return Marshal.GetDelegateForFunctionPointer(pointer,typeof(T)) as T;}
        private static void Check(int status){if(status!=0)throw new InvalidOperationException(status==-160?"驱动接口未找到此设置（-160）；未修改。":"NVIDIA DRS 操作失败："+status+"；未修改。");}
        internal static bool IsBackgroundLimit(uint setting){return setting==0x10835005||setting==0x10835006;}
        internal static int RawValueCount(uint setting){return setting==0x10d2bb16||setting==0x10835005?2:1;}
        private static IntPtr Buffer(int size,int version)
        {IntPtr buffer=Marshal.AllocHGlobal(size);Marshal.Copy(new byte[size],0,buffer,size);Marshal.WriteInt32(buffer,size|(version<<16));return buffer;}
        private List<ApplicationAssociation> ReadApplications()
        {
            if(applications==0||applications>128)throw new InvalidOperationException("关联程序数量不在可核对范围内。");
            uint capacity=applications,count=capacity;IntPtr buffer=Buffer(checked((int)capacity*ApplicationSize),0);
            try
            {
                for(int i=0;i<capacity;i++)Marshal.WriteInt32(buffer,i*ApplicationSize,ApplicationSize|(4<<16));
                Check(Function<EnumApplications>(0x7fa2173a)(session,profile,0,ref count,buffer));
                if(count!=capacity)throw new InvalidOperationException("关联程序列表不完整或已改变，请刷新。");
                var result=new List<ApplicationAssociation>();
                for(int i=0;i<count;i++)
                {
                    IntPtr item=IntPtr.Add(buffer,i*ApplicationSize);
                    result.Add(new ApplicationAssociation {Name=ApplicationText(item,8),Launcher=ApplicationText(item,8200),
                        FileInFolder=ApplicationText(item,12296),Flags=unchecked((uint)Marshal.ReadInt32(item,16392)),
                        CommandLine=ApplicationText(item,16396),Predefined=Marshal.ReadInt32(item,4)!=0});
                }
                return result;
            }
            finally{Marshal.FreeHGlobal(buffer);}
        }
        private static string ApplicationText(IntPtr item,int offset)
        {return Marshal.PtrToStringUni(IntPtr.Add(item,offset),2048).TrimEnd('\0');}
        internal static string AssociationBlockReason(string game,string selectedPath,uint expectedCount,IList<ApplicationAssociation> items,bool predefined)
        {
            if(game=="GLOBAL")return null;
            if((game!="CS2"&&game!="VALORANT")||!GameOptimizer.IsExecutable(game,selectedPath))return "无法确认所选游戏主程序，此处只读。";
            if(items==null||expectedCount==0||expectedCount>128||items.Count!=expectedCount)return "无法完整确认驱动配置的关联程序，此处只读。";
            if(items.Count>1&&(!predefined||items.Any(x=>x==null||!x.Predefined)))return "此配置包含未经确认的自定义程序关联，此处只读。";
            string selected;
            try{selected=Path.GetFullPath(selectedPath);}catch{return "所选主程序路径无效，此处只读。";}
            bool primary=false;var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in items)
            {
                if(item==null||string.IsNullOrEmpty(item.Name))return "驱动配置含未知程序关联，此处只读。";
                if(item.Flags!=0||!string.IsNullOrEmpty(item.Launcher)||!string.IsNullOrEmpty(item.FileInFolder)||!string.IsNullOrEmpty(item.CommandLine))
                    return "关联程序带有未支持的启动条件，此处只读。";
                string name=item.Name,executable;
                try{executable=Path.GetFileName(name);}catch{return "关联程序名称无效，此处只读。";}
                bool main=string.Equals(executable,GameOptimizer.ExecutableName(game),StringComparison.OrdinalIgnoreCase);
                bool alias=(game=="CS2"&&string.Equals(executable,"csgos2.exe",StringComparison.OrdinalIgnoreCase))||
                    (game=="VALORANT"&&string.Equals(executable,"valorant.exe",StringComparison.OrdinalIgnoreCase));
                if(!main&&!alias)return "关联程序不属于已确认的 "+game+" 主程序："+name+"；此处只读。";
                if(!string.Equals(name,executable,StringComparison.Ordinal))
                {
                    bool fullyQualified=name.StartsWith(@"\\",StringComparison.Ordinal)||(name.Length>2&&name[1]==':'&&(name[2]=='\\'||name[2]=='/'));
                    if(!fullyQualified)return "关联程序使用未确认的相对路径，此处只读。";
                    try
                    {
                        name=Path.GetFullPath(name);
                        bool same=string.Equals(name,selected,StringComparison.OrdinalIgnoreCase);
                        string aliasFolder=Path.GetDirectoryName(selected);
                        if(game=="VALORANT"&&alias)
                        {
                            var win64=new DirectoryInfo(aliasFolder);
                            aliasFolder=win64.Name.Equals("Win64",StringComparison.OrdinalIgnoreCase)&&win64.Parent!=null&&win64.Parent.Name.Equals("Binaries",StringComparison.OrdinalIgnoreCase)&&
                                win64.Parent.Parent!=null&&win64.Parent.Parent.Name.Equals("ShooterGame",StringComparison.OrdinalIgnoreCase)&&win64.Parent.Parent.Parent!=null?win64.Parent.Parent.Parent.FullName:null;
                        }
                        bool sameAliasFolder=alias&&aliasFolder!=null&&string.Equals(Path.GetDirectoryName(name),aliasFolder,StringComparison.OrdinalIgnoreCase);
                        if(!same&&!sameAliasFolder)return "驱动配置还关联了其他安装路径："+item.Name+"；此处只读。";
                    }
                    catch{return "无法确认关联程序路径，此处只读。";}
                }
                if(!names.Add(name))return "驱动配置包含重复的关联程序，无法确认范围，此处只读。";
                primary|=main;
            }
            return primary?null:"驱动配置未包含所选游戏主程序，此处只读。";
        }
        internal static string AssociationIdentity(string game,string path,string name,uint count,IList<ApplicationAssociation> items)
        {
            string identity=game+"|"+(game=="GLOBAL"?"global":Path.GetFullPath(path).ToLowerInvariant())+"|"+name+"|"+count;
            // Keep existing single-application/global backup identities compatible.
            if(game!="GLOBAL"&&count>1)
            {
                Func<string,string> encode=x=>Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(x??string.Empty));
                identity+="|"+string.Join(";",items.Select(x=>encode(x.Name.ToLowerInvariant())+","+encode(x.Launcher)+","+encode(x.FileInFolder)+","+encode(x.CommandLine)+","+x.Flags+","+x.Predefined)
                    .OrderBy(x=>x,StringComparer.Ordinal).ToArray());
            }
            return WindowsManagerBackend.Hash(identity);
        }
        private static uint SettingId(string id)
        {uint value;if(!uint.TryParse(id,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value)||!Settings.Contains(value))throw new ArgumentException("未知 NVIDIA 调优项。");return value;}
        internal List<ManagerItem> Scan()
        {
            var items=new List<ManagerItem>();foreach(uint setting in Settings)
                try{items.Add(Read(setting.ToString("X8")));}catch(Exception ex){items.Add(new ManagerItem {id=setting.ToString("X8"),title=Title(setting),current="此驱动配置未提供参数",blocked=ex.Message,supported=false,Raw="unavailable",Identity=Identity()});}
            return items;
        }
        private string Identity(){return AssociationIdentity(game,path,profileName,applications,associations);}
        private static string Title(uint setting)
        {switch(setting){case 0x1057eb71:return "NVIDIA 电源管理模式";case 0x00a879cf:return "NVIDIA 垂直同步";case 0x10835002:return "NVIDIA 最大帧率";case 0x00198fff:return "NVIDIA 着色器缓存";case 0x00ce2691:return "NVIDIA 纹理过滤质量";case 0x00e73211:return "NVIDIA 各向异性采样优化";case 0x0084cd70:return "NVIDIA 各向异性过滤优化";case 0x0019bb68:return "NVIDIA 负 LOD 偏移";
            case 0x10d2bb16:return "NVIDIA 各向异性过滤倍率";case 0x1074c972:return "NVIDIA 抗锯齿 FXAA";case 0x107d639d:return "NVIDIA 抗锯齿 Gamma 校正";case 0x10fc2d9c:return "NVIDIA 透明度多重采样";case 0x0098c1ac:return "NVIDIA 多帧采样抗锯齿 MFAA";case 0x20c1221e:return "NVIDIA 线程优化（OpenGL）";case 0x20fdd1f9:return "NVIDIA 三重缓冲（OpenGL）";case 0x002ecaf2:return "NVIDIA 三线性过滤优化";case 0x0064b541:return "NVIDIA 首选刷新率";case 0x10835005:return "NVIDIA 后台应用最大帧率";case 0x00ac8497:return "NVIDIA 着色器缓存容量（全局）";default:throw new ArgumentException("未知驱动项。");}}
        internal static List<ManagerChoice> Choices(uint setting)
        {
            if(setting==0x1057eb71)return new List<ManagerChoice>{new ManagerChoice {value="explicit:1",label="最高性能优先（增加功耗）"},new ManagerChoice {value="explicit:0",label="自适应"},new ManagerChoice {value="explicit:5",label="最佳功耗"}};
            if(setting==0x00a879cf)return new List<ManagerChoice>{new ManagerChoice {value="explicit:"+0x60925292u,label="由应用程序控制"},new ManagerChoice {value="explicit:"+0x08416747u,label="关闭（可能撕裂）"},new ManagerChoice {value="explicit:"+0x47814940u,label="打开（可能增加延迟）"}};
            if(setting==0x00ce2691)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="质量"},new ManagerChoice {value="explicit:"+0xfffffff6u,label="高质量"},new ManagerChoice {value="explicit:10",label="性能（可能降低画质）"},new ManagerChoice {value="explicit:20",label="高性能（可能降低画质）"}};
            if(setting==0x00198fff)return new List<ManagerChoice>{new ManagerChoice {value="explicit:1",label="启用缓存"},new ManagerChoice {value="explicit:0",label="关闭缓存（可能增加编译卡顿）"}};
            if(setting==0x0019bb68)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="允许负 LOD"},new ManagerChoice {value="explicit:1",label="钳制负 LOD"}};
            if(setting==0x00e73211||setting==0x0084cd70)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="关闭"},new ManagerChoice {value="explicit:1",label="开启（可能降低过滤质量）"}};
            if(setting==0x10d2bb16)return new[]{new ManagerChoice {value="inherited|inherited",label="继承全局 / 驱动预设"},new ManagerChoice {value="explicit:0|inherited",label="由应用程序控制"},new ManagerChoice {value="explicit:1|explicit:1",label="关闭强制各向异性过滤"}}.Concat(new uint[]{2,4,8,16}.Select(x=>new ManagerChoice {value="explicit:1|explicit:"+x,label=x+"×（同时设置覆盖模式和倍率）"})).ToList();
            if(setting==0x107d639d)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="关闭"},new ManagerChoice {value="explicit:1",label="仅全屏开启"},new ManagerChoice {value="explicit:2",label="始终开启"}};
            if(setting==0x10fc2d9c)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="关闭"},new ManagerChoice {value="explicit:4",label="开启透明度多重采样"}};
            if(setting==0x20c1221e)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="自动"},new ManagerChoice {value="explicit:1",label="开启"},new ManagerChoice {value="explicit:2",label="关闭"}};
            if(setting==0x0064b541)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="由应用程序控制"},new ManagerChoice {value="explicit:1",label="最高可用刷新率"}};
            if(setting==0x00ac8497)return new uint[]{0,128,256,512,1024,5120,10240,16384,102400,uint.MaxValue}.Select(x=>new ManagerChoice {value="explicit:"+x,label=x==0?"禁用容量缓存":x==uint.MaxValue?"不限容量（占用磁盘空间）":x<1024?x+" MB":(x/1024)+" GB"}).ToList();
            if(setting==0x1074c972||setting==0x0098c1ac||setting==0x20fdd1f9||setting==0x002ecaf2)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="关闭"},new ManagerChoice {value="explicit:1",label="开启"}};
            if(setting==0x10835005)return new[]{new ManagerChoice {value="inherited|inherited",label="继承全局 / 驱动预设"}}.Concat(new uint[]{0,20,30,60,90,120,144,165,200}.Select(x=>new ManagerChoice {value="explicit:"+x+"|explicit:"+x,label=x==0?"关闭后台帧率限制":x+" FPS"})).ToList();
            if(setting==0x10835002)return new uint[]{0,30,60,75,90,100,120,144,165,180,200,240,280,300,360,400,500,600,800,1000}.Select(x=>new ManagerChoice {value="explicit:"+x,label=x==0?"关闭驱动帧率限制":x+" FPS"}).ToList();
            throw new ArgumentException("未知驱动项。");
        }
        private sealed class DwordSetting {internal uint Value;internal bool Inherited;internal string Raw {get{return Inherited?"inherited":"explicit:"+Value;}}}
        private DwordSetting ReadDword(uint setting)
        {
            IntPtr buffer=Buffer(SettingSize,1);
            try
            {
                if(IsBackgroundLimit(setting))
                {
                    uint flags=0;
                    Check(Function<CompatibleGet>(0xea99498d)(session,profile,setting,buffer,ref flags));
                    if(flags!=0)throw new InvalidOperationException("后台限帧参数带有未支持的编码标记，此处只读。");
                }
                else Check(Function<Get>(0x73bf8338)(session,profile,setting,buffer));
                if(Marshal.ReadInt32(buffer,4104)!=0)throw new InvalidOperationException("该驱动设置不是预期 DWORD 类型，未修改。");
                return new DwordSetting {Value=unchecked((uint)Marshal.ReadInt32(buffer,CurrentValueOffset)),Inherited=Marshal.ReadInt32(buffer,4108)!=0||Marshal.ReadInt32(buffer,4112)!=0};
            }
            finally{Marshal.FreeHGlobal(buffer);}
        }
        internal static string Effect(uint setting)
        {
            if(setting==0x20c1221e||setting==0x20fdd1f9)return "仅适用于 OpenGL；CS2/VALORANT 的 DirectX 渲染不因该项获得同样效果。";
            if(setting==0x00ac8497)return "驱动全局磁盘缓存容量，不是内存；限制过低可能增加着色器重新编译，需重启游戏核对。";
            if(setting==0x0064b541)return "首选刷新策略，VRR/G-SYNC 或游戏策略可能限制效果；不直接切换 Windows 显示模式。";
            if(setting==0x10835005)return "只限制游戏切到后台后的帧率；同时保存实际限帧和控制面板值，不改变前台最大帧率。";
            if(setting==0x1074c972||setting==0x107d639d||setting==0x10fc2d9c||setting==0x0098c1ac||setting==0x10d2bb16)return "渲染 API、游戏和驱动是否接受覆盖决定实际画质；重启游戏核对，不等于保证画质或帧率改变。";
            return "重启所选游戏后核对；不关闭显示器或音频设备。";
        }
        internal ManagerItem Read(string id)
        {
            uint setting=SettingId(id);var state=ReadDword(setting);string raw=state.Raw,current;
            {
                var choices=Choices(setting);var choice=choices.FirstOrDefault(x=>x.value=="explicit:"+state.Value);
                current=choice==null?"值 "+state.Value:choice.label;
                if(setting==0x10d2bb16){var level=ReadDword(0x101e61a9);raw=state.Raw+"|"+level.Raw;current=state.Value==0?"由应用程序控制":state.Value==1?(level.Value<=1?"未强制各向异性过滤":level.Value+"×"):"驱动条件覆盖（模式 "+state.Value+"）";state.Inherited|=level.Inherited;}
                else if(setting==0x10835005)
                {
                    var panel=ReadDword(0x10835006);raw=state.Raw+"|"+panel.Raw;
                    current=state.Value==0?"关闭后台帧率限制":state.Value+" FPS";
                    if(panel.Value!=state.Value)current+=" · 控制面板值 "+panel.Value+" FPS（不一致）";
                }
                else choices.Add(new ManagerChoice {value="inherited",label="继承全局 / 驱动预设"});
                string blocked=associationError??AssociationBlockReason(game,path,applications,associations,predefinedProfile);
                if(setting==0x10835005&&(query(0x8a2cf5f5)==IntPtr.Zero||query(0xd20d29df)==IntPtr.Zero))blocked="此驱动未提供后台限帧的兼容写入和还原接口，此处只读。";
                if(setting==0x00ac8497&&game!="GLOBAL")blocked="缓存容量按驱动全局管理，请在 NVIDIA 全局设置中调整。";
                string names=game=="GLOBAL"||associations.Count==0?string.Empty:"（"+string.Join("、",associations.Take(3).Select(x=>x.Name).ToArray())+(associations.Count>3?"…":"")+"）";
                string[] rawParts=raw.Split('|');string origin=rawParts.All(x=>x=="inherited")?" · 继承/驱动预设":rawParts.Any(x=>x=="inherited")?" · 部分参数继承，其余独立覆盖":" · 独立覆盖";
                return new ManagerItem {id=id,title=Title(setting),detail=(game=="GLOBAL"?"NVIDIA 全局（所有程序）":game)+" · 驱动配置："+profileName+" · 关联程序 "+applications+names,current=current+origin,Raw=raw,Identity=Identity(),supported=blocked==null,blocked=blocked,effect=(game=="GLOBAL"?"全局项影响全部 NVIDIA 程序和功耗。":"")+Effect(setting),choices=choices};
            }
        }
        internal static string[] ValidateRawValues(string raw,int count)
        {
            string[] parts=(raw??"").Split('|');uint value;
            if(parts.Length!=count||parts.Any(x=>x!="inherited"&&(!x.StartsWith("explicit:")||!uint.TryParse(x.Substring(9),NumberStyles.None,CultureInfo.InvariantCulture,out value))))throw new ArgumentException("NVIDIA 备份值无效。");
            return parts;
        }
        private void WriteDword(uint setting,string value)
        {
            if(value=="inherited"){if(!ReadDword(setting).Inherited)Check(Function<Delete>(IsBackgroundLimit(setting)?0xd20d29dfu:0xe4a26362u)(session,profile,setting));return;}
            uint number=uint.Parse(value.Substring(9),CultureInfo.InvariantCulture);IntPtr buffer=Buffer(SettingSize,1);
            try{Marshal.WriteInt32(buffer,4100,unchecked((int)setting));Marshal.WriteInt32(buffer,4104,0);Marshal.WriteInt32(buffer,CurrentValueOffset,unchecked((int)number));if(IsBackgroundLimit(setting))Check(Function<CompatibleSet>(0x8a2cf5f5)(session,profile,buffer,0,0));else Check(Function<Set>(0x577dd202)(session,profile,buffer));}
            finally{Marshal.FreeHGlobal(buffer);}
        }
        internal void Write(string id,string value)
        {
            uint setting=SettingId(id);var current=Read(id);if(!current.supported)throw new InvalidOperationException(current.blocked);
            string[] parts=ValidateRawValues(value,RawValueCount(setting));WriteDword(setting,parts[0]);if(parts.Length==2)WriteDword(setting==0x10835005?0x10835006u:0x101e61a9u,parts[1]);
            Check(Function<Session>(0xfcbc7e14)(session));
        }
        public void Dispose()
        {
            if(session!=IntPtr.Zero&&query!=null){try{Function<Session>(0xdad9cff8)(session);}catch{}session=IntPtr.Zero;}
            if(library!=IntPtr.Zero){if(query!=null){try{Function<Simple>(0xd22bdd7e)();}catch{}}FreeLibrary(library);library=IntPtr.Zero;}
        }
    }
}
