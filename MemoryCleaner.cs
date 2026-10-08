using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace NexaArena
{
    public sealed class MemoryCleanerOptions
    {
        public long ListMb=1024,FreeMb=1024;
        public bool OnlyGame=true;
        public double TimerMs=1;
    }
    internal sealed class MemorySnapshot
    {
        public long totalMb,availableMb,freeMb,standbyMb;
        public bool supported;
        public string error;
        public double minimumTimerMs,maximumTimerMs,currentTimerMs;
    }
    internal sealed class MemoryCleaner:IDisposable
    {
        [StructLayout(LayoutKind.Sequential)] private struct MemoryStatus {public uint length,load;public ulong totalPhysical,availablePhysical,totalPage,availablePage,totalVirtual,availableVirtual,extended;}
        [StructLayout(LayoutKind.Sequential)] private struct Luid {public uint low;public int high;}
        [StructLayout(LayoutKind.Sequential)] private struct TokenPrivileges {public uint count;public Luid luid;public uint attributes;}
        [DllImport("kernel32.dll",SetLastError=true)] private static extern bool GlobalMemoryStatusEx(ref MemoryStatus info);
        [DllImport("ntdll.dll")] private static extern int NtQuerySystemInformation(int kind,IntPtr info,int length,out int returned);
        [DllImport("ntdll.dll")] private static extern int NtSetSystemInformation(int kind,ref int command,int length);
        [DllImport("ntdll.dll")] private static extern int NtQueryTimerResolution(out uint minimum,out uint maximum,out uint current);
        [DllImport("ntdll.dll")] private static extern int NtSetTimerResolution(uint desired,[MarshalAs(UnmanagedType.Bool)]bool set,out uint current);
        [DllImport("advapi32.dll",SetLastError=true)] private static extern bool OpenProcessToken(IntPtr process,uint access,out IntPtr token);
        [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)] private static extern bool LookupPrivilegeValue(string system,string name,out Luid luid);
        [DllImport("advapi32.dll",SetLastError=true)] private static extern bool AdjustTokenPrivileges(IntPtr token,bool disable,ref TokenPrivileges state,int length,out TokenPrivileges previous,out int returned);
        [DllImport("kernel32.dll")] private static extern bool CloseHandle(IntPtr handle);
        private readonly Timer monitor=new Timer {Interval=5000};
        private long listThreshold=1024,freeThreshold=1024;
        private bool automatic,onlyGame=true;
        private uint timerRequest;
        private int cleanCount;
        private DateTime lastClean=DateTime.MinValue;
        private string lastMessage="尚未清理";
        private bool disposed;
        private MemoryCleanerOptions preferences;
        private readonly string preferenceFile;
        private readonly Func<MemorySnapshot> readMemory;
        public MemoryCleaner():this(Path.Combine(SavedState.DirectoryPath,"memory-options.xml"),Read){}
        internal MemoryCleaner(string preferenceFile,Func<MemorySnapshot> reader)
        {
            this.preferenceFile=preferenceFile;readMemory=reader;
            preferences=new MemoryCleanerOptions();
            if(File.Exists(preferenceFile))
            {
                try
                {
                    preferences=LoadPreferences(preferenceFile);
                    ValidatePreferences(preferences,readMemory());
                }
                catch(Exception ex){preferences=new MemoryCleanerOptions();lastMessage="偏好未读取，使用默认阈值："+ex.Message;}
            }
            listThreshold=preferences.ListMb;freeThreshold=preferences.FreeMb;onlyGame=preferences.OnlyGame;
            monitor.Tick+=delegate{AutomaticTick();};
        }
        private static MemoryCleanerOptions LoadPreferences(string path)
        {
            if(new FileInfo(path).Length>65536)throw new InvalidOperationException("内存偏好文件过大。");
            using(var input=XmlReader.Create(path,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
                return (MemoryCleanerOptions)new XmlSerializer(typeof(MemoryCleanerOptions)).Deserialize(input);
        }
        internal static void ValidatePreferences(MemoryCleanerOptions next,MemorySnapshot info)
        {
            if(next==null||next.ListMb<128||next.FreeMb<128||next.ListMb>info.totalMb||next.FreeMb>info.totalMb)
                throw new ArgumentException("内存阈值须在 128 MB 到总内存之间。");
            if(double.IsNaN(next.TimerMs)||double.IsInfinity(next.TimerMs)||next.TimerMs<=0||
                (info.maximumTimerMs>0&&(next.TimerMs<info.minimumTimerMs||next.TimerMs>info.maximumTimerMs)))
                throw new ArgumentException("定时器参数超出系统支持范围。");
        }
        private void SavePreferences(MemoryCleanerOptions next)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(preferenceFile));string temporary=preferenceFile+".tmp";
            using(var output=File.Create(temporary))new XmlSerializer(typeof(MemoryCleanerOptions)).Serialize(output,next);
            if(File.Exists(preferenceFile))File.Replace(temporary,preferenceFile,preferenceFile+".bak");else File.Move(temporary,preferenceFile);
            var stored=LoadPreferences(preferenceFile);
            if(stored.ListMb!=next.ListMb||stored.FreeMb!=next.FreeMb||stored.OnlyGame!=next.OnlyGame||stored.TimerMs!=next.TimerMs)
                throw new InvalidOperationException("内存参数保存后读回不一致，未更新运行状态。");
            preferences=stored;listThreshold=stored.ListMb;freeThreshold=stored.FreeMb;onlyGame=stored.OnlyGame;
        }
        public object SaveInputs(long listMb,long freeMb,bool gameOnly,double milliseconds)
        {
            var next=new MemoryCleanerOptions {ListMb=listMb,FreeMb=freeMb,OnlyGame=gameOnly,TimerMs=milliseconds};ValidatePreferences(next,readMemory());
            if(automatic&&(listMb!=listThreshold||freeMb!=freeThreshold||gameOnly!=onlyGame))throw new InvalidOperationException("请先停止监控再修改清理条件。");
            SavePreferences(next);return Status();
        }

        public static MemorySnapshot Read()
        {
            var native=new MemoryStatus {length=(uint)Marshal.SizeOf(typeof(MemoryStatus))};
            if(!GlobalMemoryStatusEx(ref native))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            var value=new MemorySnapshot {totalMb=(long)(native.totalPhysical/1048576),availableMb=(long)(native.availablePhysical/1048576)};
            int length=IntPtr.Size*22,returned;IntPtr p=Marshal.AllocHGlobal(length);
            try
            {
                for(int i=0;i<length;i++)Marshal.WriteByte(p,i,0);
                int status=NtQuerySystemInformation(80,p,length,out returned);
                if(status<0)throw new InvalidOperationException("无法读取待机内存列表：0x"+status.ToString("X8"));
                ulong pages=0;
                for(int i=5;i<13;i++)pages+=ReadPointer(p,i);
                ulong free=ReadPointer(p,0)+ReadPointer(p,1);
                value.standbyMb=(long)(pages*(ulong)Environment.SystemPageSize/1048576);
                value.freeMb=(long)(free*(ulong)Environment.SystemPageSize/1048576);value.supported=true;
            }
            catch(Exception ex){value.supported=false;value.error=ex.Message;}
            finally{Marshal.FreeHGlobal(p);}
            uint min,max,current;
            if(NtQueryTimerResolution(out min,out max,out current)>=0){value.minimumTimerMs=max/10000.0;value.maximumTimerMs=min/10000.0;value.currentTimerMs=current/10000.0;}
            return value;
        }
        private static ulong ReadPointer(IntPtr p,int index)
        {return IntPtr.Size==8?(ulong)Marshal.ReadInt64(p,index*8):(uint)Marshal.ReadInt32(p,index*4);}
        public object Status()
        {return new {memory=readMemory(),automatic,onlyGame,listThreshold,freeThreshold,timerRequestedMs=timerRequest/10000.0,timerTargetMs=preferences.TimerMs,cleanCount,lastMessage};}
        internal static bool ShouldClean(long standby,long free,long listMinimum,long freeMaximum,bool onlyWhileGaming,bool gaming,bool cooldown)
        {return !cooldown&&(!onlyWhileGaming||gaming)&&standby>=listMinimum&&free<freeMaximum;}
        private static bool Gaming()
        {
            foreach(string name in new[]{"cs2","VALORANT-Win64-Shipping"})
            {
                Process[] processes=Process.GetProcessesByName(name);bool found=processes.Length>0;
                foreach(Process process in processes)process.Dispose();if(found)return true;
            }
            return false;
        }
        public object Configure(bool enabled,long listMb,long freeMb,bool gameOnly)
        {
            var info=readMemory();
            if(listMb<128||freeMb<128||listMb>info.totalMb||freeMb>info.totalMb)throw new ArgumentException("内存阈值须在 128 MB 到总内存之间。");
            if(enabled&&!info.supported)throw new InvalidOperationException(info.error);
            if(!enabled){automatic=false;monitor.Stop();lastMessage="自动清理已停止";}
            SaveInputs(listMb,freeMb,gameOnly,preferences.TimerMs);automatic=enabled;
            if(enabled){lastMessage="正在监控阈值";monitor.Start();}
            return Status();
        }
        private void AutomaticTick()
        {
            if(!automatic)return;
            try
            {
                var m=readMemory();
                if(!m.supported)throw new InvalidOperationException(m.error);
                if(ShouldClean(m.standbyMb,m.freeMb,listThreshold,freeThreshold,onlyGame,Gaming(),(DateTime.UtcNow-lastClean).TotalSeconds<30))Purge();
            }
            catch(Exception ex){automatic=false;monitor.Stop();lastMessage="自动清理已停止："+ex.Message;ErrorLog.Write(ex);}
        }
        public object Purge()
        {
            using(Process own=Process.GetCurrentProcess())
            {
                IntPtr token;
                if(!OpenProcessToken(own.Handle,0x28,out token))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                TokenPrivileges old=new TokenPrivileges();bool adjusted=false;
                try
                {
                    Luid luid;if(!LookupPrivilegeValue(null,"SeProfileSingleProcessPrivilege",out luid))throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
                    var desired=new TokenPrivileges {count=1,luid=luid,attributes=2};int returned;
                    if(!AdjustTokenPrivileges(token,false,ref desired,Marshal.SizeOf(typeof(TokenPrivileges)),out old,out returned)||Marshal.GetLastWin32Error()==1300)
                        throw new InvalidOperationException("待机清理需要管理员权限，请以管理员身份运行工具。");
                    adjusted=true;int command=4;
                    int status=NtSetSystemInformation(80,ref command,4);
                    if(status<0)throw new InvalidOperationException("待机清理失败：0x"+status.ToString("X8"));
                    cleanCount++;lastClean=DateTime.UtcNow;lastMessage="已清理待机列表 · "+DateTime.Now.ToString("HH:mm:ss");
                }
                finally
                {
                    if(adjusted&&old.count>0){TokenPrivileges ignored;int size;AdjustTokenPrivileges(token,false,ref old,Marshal.SizeOf(typeof(TokenPrivileges)),out ignored,out size);}
                    CloseHandle(token);
                }
            }
            return Status();
        }
        public object SetTimer(bool enabled,double milliseconds)
        {
            var info=readMemory();
            if(enabled&&(double.IsNaN(milliseconds)||double.IsInfinity(milliseconds)||info.maximumTimerMs<=0||milliseconds<info.minimumTimerMs||milliseconds>info.maximumTimerMs))
                throw new ArgumentException("请求精度超出系统支持范围。");
            uint requested=enabled?(uint)Math.Round(milliseconds*10000):0;
            if(requested==timerRequest)return Status();
            if(enabled)SaveInputs(listThreshold,freeThreshold,onlyGame,milliseconds);
            uint old=timerRequest,current;
            if(old!=0&&NtSetTimerResolution(old,false,out current)<0)throw new InvalidOperationException("未能释放原定时器请求。");
            timerRequest=0;
            if(requested!=0)
            {
                int status=NtSetTimerResolution(requested,true,out current);
                if(status<0){if(old!=0&&NtSetTimerResolution(old,true,out current)>=0)timerRequest=old;throw new InvalidOperationException("定时器请求失败：0x"+status.ToString("X8"));}
                timerRequest=requested;
            }
            return Status();
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            monitor.Stop();monitor.Dispose();
            if(timerRequest!=0){uint current;NtSetTimerResolution(timerRequest,false,out current);timerRequest=0;}
        }
    }
}
