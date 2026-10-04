using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NexaArena
{
    // NVIDIA public DRS ABI (SDK nvapi.h / NvApiDriverSettings.h). No injection.
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
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Get(IntPtr session,IntPtr profile,uint id,IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Set(IntPtr session,IntPtr profile,IntPtr info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int Delete(IntPtr session,IntPtr profile,uint id);
        private IntPtr library,session,profile;
        private Query query;
        private string profileName;
        private uint applications;
        private readonly string game,path;
        private static readonly uint[] Settings={0x1057eb71,0x00a879cf,0x10835002,0x00198fff,0x00ce2691,0x00e73211,0x0084cd70,0x0019bb68};
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
                IntPtr info=Buffer(ProfileSize,1);try{Check(Function<ProfileInfo>(0x61cd6fd6)(session,profile,info));profileName=Marshal.PtrToStringUni(IntPtr.Add(info,4),2048).TrimEnd('\0');applications=unchecked((uint)Marshal.ReadInt32(info,4108));}finally{Marshal.FreeHGlobal(info);}
            }
            catch{Dispose();throw;}
        }
        private T Function<T>(uint id) where T:class
        {IntPtr pointer=query(id);if(pointer==IntPtr.Zero)throw new InvalidOperationException("此 NVIDIA 驱动不支持所需接口。");return Marshal.GetDelegateForFunctionPointer(pointer,typeof(T)) as T;}
        private static void Check(int status){if(status!=0)throw new InvalidOperationException("NVIDIA DRS 操作失败："+status+"；未绕过驱动限制。");}
        private static IntPtr Buffer(int size,int version)
        {IntPtr buffer=Marshal.AllocHGlobal(size);Marshal.Copy(new byte[size],0,buffer,size);Marshal.WriteInt32(buffer,size|(version<<16));return buffer;}
        private static uint SettingId(string id)
        {uint value;if(!uint.TryParse(id,NumberStyles.HexNumber,CultureInfo.InvariantCulture,out value)||!Settings.Contains(value))throw new ArgumentException("未知 NVIDIA 调优项。");return value;}
        internal List<ManagerItem> Scan()
        {
            var items=new List<ManagerItem>();foreach(uint setting in Settings)
                try{items.Add(Read(setting.ToString("X8")));}catch(Exception ex){items.Add(new ManagerItem {id=setting.ToString("X8"),title=Title(setting),current="驱动不支持",blocked=ex.Message,supported=false,Raw="unavailable",Identity=Identity()});}
            return items;
        }
        private string Identity(){return WindowsManagerBackend.Hash(game+"|"+(game=="GLOBAL"?"global":Path.GetFullPath(path).ToLowerInvariant())+"|"+profileName+"|"+applications);}
        private static string Title(uint setting)
        {switch(setting){case 0x1057eb71:return "NVIDIA 电源管理模式";case 0x00a879cf:return "NVIDIA 垂直同步";case 0x10835002:return "NVIDIA 最大帧率";case 0x00198fff:return "NVIDIA 着色器缓存";case 0x00ce2691:return "NVIDIA 纹理过滤质量";case 0x00e73211:return "NVIDIA 各向异性采样优化";case 0x0084cd70:return "NVIDIA 各向异性过滤优化";case 0x0019bb68:return "NVIDIA 负 LOD 偏移";default:throw new ArgumentException("未知驱动项。");}}
        private static List<ManagerChoice> Choices(uint setting)
        {
            if(setting==0x1057eb71)return new List<ManagerChoice>{new ManagerChoice {value="explicit:1",label="最高性能优先（增加功耗）"},new ManagerChoice {value="explicit:0",label="自适应"},new ManagerChoice {value="explicit:5",label="最佳功耗"}};
            if(setting==0x00a879cf)return new List<ManagerChoice>{new ManagerChoice {value="explicit:"+0x60925292u,label="由应用程序控制"},new ManagerChoice {value="explicit:"+0x08416747u,label="关闭（可能撕裂）"},new ManagerChoice {value="explicit:"+0x47814940u,label="打开（可能增加延迟）"}};
            if(setting==0x00ce2691)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="质量"},new ManagerChoice {value="explicit:"+0xfffffff6u,label="高质量"},new ManagerChoice {value="explicit:10",label="性能（可能降低画质）"},new ManagerChoice {value="explicit:20",label="高性能（可能降低画质）"}};
            if(setting==0x00198fff)return new List<ManagerChoice>{new ManagerChoice {value="explicit:1",label="启用缓存"},new ManagerChoice {value="explicit:0",label="关闭缓存（可能增加编译卡顿）"}};
            if(setting==0x0019bb68)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="允许负 LOD"},new ManagerChoice {value="explicit:1",label="钳制负 LOD"}};
            if(setting==0x00e73211||setting==0x0084cd70)return new List<ManagerChoice>{new ManagerChoice {value="explicit:0",label="关闭"},new ManagerChoice {value="explicit:1",label="开启（可能降低过滤质量）"}};
            return new uint[]{0,60,120,144,165,240,280,360,500}.Select(x=>new ManagerChoice {value="explicit:"+x,label=x==0?"关闭驱动帧率限制":x+" FPS"}).ToList();
        }
        internal ManagerItem Read(string id)
        {
            uint setting=SettingId(id);IntPtr buffer=Buffer(SettingSize,1);
            try
            {
                Check(Function<Get>(0x73bf8338)(session,profile,setting,buffer));
                if(Marshal.ReadInt32(buffer,4104)!=0)throw new InvalidOperationException("该驱动设置不是预期 DWORD 类型，未修改。");
                uint value=unchecked((uint)Marshal.ReadInt32(buffer,CurrentValueOffset));bool inherited=Marshal.ReadInt32(buffer,4108)!=0||Marshal.ReadInt32(buffer,4112)!=0;
                string raw=inherited?"inherited":"explicit:"+value;
                var choices=Choices(setting);var choice=choices.FirstOrDefault(x=>x.value=="explicit:"+value);
                bool shared=game!="GLOBAL"&&applications!=1;
                return new ManagerItem {id=id,title=Title(setting),detail=(game=="GLOBAL"?"NVIDIA 全局（所有程序）":game)+" · 驱动配置："+profileName+" · 关联程序 "+applications,current=(choice==null?"值 "+value:choice.label)+(inherited?" · 继承/驱动预设":" · 独立覆盖"),Raw=raw,Identity=Identity(),supported=!shared,blocked=shared?"此驱动配置包含多个程序，不能当作仅作用于所选游戏；此处只读，不修改共享配置。":null,effect=game=="GLOBAL"?"影响全部 NVIDIA 程序及电池功耗；重启游戏后核对。可能增加温度或改变同步行为。":"重启所选游戏后核对；不关闭显示器或音频设备",choices=choices};
            }
            finally{Marshal.FreeHGlobal(buffer);}
        }
        internal void Write(string id,string value)
        {
            uint setting=SettingId(id);var current=Read(id);if(!current.supported)throw new InvalidOperationException(current.blocked);
            if(value=="inherited")Check(Function<Delete>(0xe4a26362)(session,profile,setting));
            else
            {
                uint number;if(!value.StartsWith("explicit:")||!uint.TryParse(value.Substring(9),out number))throw new ArgumentException("NVIDIA 备份值无效。");
                IntPtr buffer=Buffer(SettingSize,1);try{Marshal.WriteInt32(buffer,4100,unchecked((int)setting));Marshal.WriteInt32(buffer,4104,0);Marshal.WriteInt32(buffer,CurrentValueOffset,unchecked((int)number));Check(Function<Set>(0x577dd202)(session,profile,buffer));}finally{Marshal.FreeHGlobal(buffer);}
            }
            Check(Function<Session>(0xfcbc7e14)(session));
        }
        public void Dispose()
        {
            if(session!=IntPtr.Zero&&query!=null){try{Function<Session>(0xdad9cff8)(session);}catch{}session=IntPtr.Zero;}
            if(library!=IntPtr.Zero){if(query!=null){try{Function<Simple>(0xd22bdd7e)();}catch{}}FreeLibrary(library);library=IntPtr.Zero;}
        }
    }
}
