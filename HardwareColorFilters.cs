using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace NexaArena
{
    public sealed class MonitorColorLevel
    {
        public uint Min,Value,Max;
        public MonitorColorLevel Copy(){return new MonitorColorLevel {Min=Min,Value=Value,Max=Max};}
    }
    public sealed class MonitorColorSnapshot
    {
        public string Device,Identity;
        public MonitorColorLevel Contrast;
        public MonitorColorLevel[] Gains;
        public uint Preset;
        public void Validate()
        {
            if(string.IsNullOrEmpty(Device)||!Device.StartsWith(@"\\.\DISPLAY",StringComparison.Ordinal)||Device.Length>32||string.IsNullOrEmpty(Identity)||Identity.Length>2048)throw new InvalidOperationException("显示器身份不完整。");
            if(Contrast==null||Gains==null||Gains.Length!=3||Gains.Any(x=>x==null)||Preset<1||Preset>255)throw new InvalidOperationException("硬件颜色备份不完整或预设异常。");
            foreach(var value in Gains.Concat(new[]{Contrast}))if(value.Max<=value.Min||value.Max>65535||value.Value<value.Min||value.Value>value.Max)throw new InvalidOperationException("显示器返回了无效范围。");
        }
        public MonitorColorSnapshot Copy(){return new MonitorColorSnapshot {Device=Device,Identity=Identity,Contrast=Contrast.Copy(),Gains=Gains.Select(x=>x.Copy()).ToArray(),Preset=Preset};}
        internal static bool SameTarget(MonitorColorSnapshot a,MonitorColorSnapshot b)
        {return a!=null&&b!=null&&a.Device==b.Device&&a.Identity==b.Identity&&a.Gains.Zip(b.Gains,(x,y)=>x.Min==y.Min&&x.Max==y.Max).All(x=>x)&&a.Contrast.Min==b.Contrast.Min&&a.Contrast.Max==b.Contrast.Max;}
        internal static bool SameValues(MonitorColorSnapshot a,MonitorColorSnapshot b,bool preset)
        {return SameTarget(a,b)&&a.Contrast.Value==b.Contrast.Value&&a.Gains.Zip(b.Gains,(x,y)=>x.Value==y.Value).All(x=>x)&&(!preset||a.Preset==b.Preset);}
    }
    public sealed class HardwareColorRecord
    {
        public int Version=1;
        public MonitorColorSnapshot Original,Applied;
        public bool Pending;
        public void Validate(){if(Version!=1||Original==null)throw new InvalidOperationException("未知硬件颜色备份。");Original.Validate();if(Applied!=null){Applied.Validate();if(!MonitorColorSnapshot.SameTarget(Original,Applied))throw new InvalidOperationException("颜色备份的设备身份冲突。");}}
    }
    internal interface IHardwareColorOutput
    {
        MonitorColorSnapshot Read();
        void Contrast(uint value);
        void Gain(int channel,uint value);
        void Preset(uint value);
    }
    internal interface IHardwareColorStore
    {
        HardwareColorRecord Load();
        void Save(HardwareColorRecord record);
        void Clear();
    }
    internal sealed class HardwareColorStore:IHardwareColorStore
    {
        private readonly string file;
        internal HardwareColorStore(string file=null){this.file=file??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"NexaArena","monitor-color.dat");}
        public HardwareColorRecord Load()
        {
            if(!File.Exists(file))return null;
            if(new FileInfo(file).Length>65536)throw new InvalidOperationException("颜色备份大小异常。");
            byte[] data=ProtectedData.Unprotect(File.ReadAllBytes(file),null,DataProtectionScope.CurrentUser);
            using(var stream=new MemoryStream(data))using(var reader=XmlReader.Create(stream,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
            {var result=(HardwareColorRecord)new XmlSerializer(typeof(HardwareColorRecord)).Deserialize(reader);result.Validate();return result;}
        }
        public void Save(HardwareColorRecord record)
        {
            record.Validate();Directory.CreateDirectory(Path.GetDirectoryName(file));byte[] bytes;
            using(var stream=new MemoryStream()){new XmlSerializer(typeof(HardwareColorRecord)).Serialize(stream,record);bytes=ProtectedData.Protect(stream.ToArray(),null,DataProtectionScope.CurrentUser);}
            string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try{File.WriteAllBytes(temp,bytes);if(File.Exists(file))File.Replace(temp,file,null);else File.Move(temp,file);var check=Load();if(check.Pending!=record.Pending||!MonitorColorSnapshot.SameValues(check.Original,record.Original,true)||((check.Applied==null)!=(record.Applied==null))||(record.Applied!=null&&!MonitorColorSnapshot.SameValues(check.Applied,record.Applied,true)))throw new IOException("原值备份写后核验失败。");}
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        public void Clear(){if(File.Exists(file))File.Delete(file);if(File.Exists(file))throw new IOException("原值备份未能清理。");}
    }
    internal sealed class HardwareColorSession
    {
        private readonly IHardwareColorOutput output;
        private readonly IHardwareColorStore store;
        internal string Status="尚未应用硬件滤镜。";
        internal HardwareColorSession(IHardwareColorOutput output,IHardwareColorStore store){this.output=output;this.store=store;}
        internal static string Describe(MonitorColorSnapshot state){return "对比度 "+state.Contrast.Value+" · R "+state.Gains[0].Value+" / G "+state.Gains[1].Value+" / B "+state.Gains[2].Value+" · 色彩预设 "+state.Preset;}
        private MonitorColorSnapshot Read(){var state=output.Read();state.Validate();return state;}
        private static uint Scale(MonitorColorLevel level,double factor)
        {double value=level.Min+(level.Value-level.Min)*factor;return (uint)Math.Max(level.Min,Math.Min(level.Max,Math.Round(value,MidpointRounding.AwayFromZero)));}
        internal static MonitorColorSnapshot Target(MonitorColorSnapshot original,FilterPreset preset)
        {
            original.Validate();var result=original.Copy();
            foreach(double x in new[]{preset.Red,preset.Green,preset.Blue,preset.Contrast})if(double.IsNaN(x)||double.IsInfinity(x)||x<.8||x>1.25)throw new ArgumentException("硬件滤镜强度超出范围。");
            result.Contrast.Value=Scale(original.Contrast,preset.Contrast);double[] gain={preset.Red,preset.Green,preset.Blue};
            for(int i=0;i<3;i++)result.Gains[i].Value=Scale(original.Gains[i],gain[i]);return result;
        }
        private void Ensure(MonitorColorSnapshot expected){if(!MonitorColorSnapshot.SameTarget(Read(),expected))throw new InvalidOperationException("显示器身份或参数范围已变化，未继续写入。");}
        private void Write(MonitorColorSnapshot target,bool restorePreset)
        {
            Ensure(target);output.Contrast(target.Contrast.Value);
            for(int i=0;i<3;i++)output.Gain(i,target.Gains[i].Value);
            if(restorePreset)output.Preset(target.Preset);
        }
        internal void Apply(FilterPreset preset)
        {
            var current=Read();var record=store.Load();
            if(record!=null){record.Validate();if(record.Pending)throw new InvalidOperationException("上次硬件颜色操作未完成，请先恢复原值。");if(!MonitorColorSnapshot.SameValues(current,record.Applied??record.Original,true))throw new InvalidOperationException("显示器设置已被外部修改或设备变化，未覆盖；原值备份仍保留。");}
            else record=new HardwareColorRecord {Original=current.Copy()};
            var desired=Target(record.Original,preset);record.Pending=true;store.Save(record);
            try
            {
                Write(desired,false);var readback=Read();if(!MonitorColorSnapshot.SameValues(readback,desired,false))throw new InvalidOperationException("硬件写入后读取不一致，未判为成功。");
                record.Applied=readback;record.Pending=false;store.Save(record);
                Status=preset.Name+" · 显示器硬件参数已读取核验，效果为硬件近似。\r\n原值："+Describe(record.Original)+"\r\n当前："+Describe(readback);
            }
            catch(Exception error)
            {
                try{if(!MonitorColorSnapshot.SameValues(Read(),record.Original,true))Write(record.Original,true);if(!MonitorColorSnapshot.SameValues(Read(),record.Original,true))throw new InvalidOperationException("回滚读取不一致。");store.Clear();Status="应用失败，原硬件参数已确认；未重复写入已恢复的参数。";}
                catch(Exception rollback){Status="应用未完成，回滚也未完成；原值备份保留。";throw new InvalidOperationException(Status+" "+error.Message+" / "+rollback.Message);}
                throw new InvalidOperationException(Status+" "+error.Message);
            }
        }
        internal void Verify()
        {
            var record=store.Load();if(record==null){Status="尚无硬件滤镜会话。";return;}record.Validate();
            var current=Read();Status=(MonitorColorSnapshot.SameValues(current,record.Applied??record.Original,true)&&!record.Pending?"当前硬件参数与已核验会话一致；没有重应用。":"硬件参数已变化或操作未完成；未自动写入，请核对后恢复原值。")+"\r\n备份原值："+Describe(record.Original)+"\r\n当前："+Describe(current);
        }
        internal void Restore()
        {
            var record=store.Load();if(record==null){Status="尚无需要恢复的硬件颜色备份。";return;}record.Validate();var current=Read();
            if(!MonitorColorSnapshot.SameTarget(current,record.Original))throw new InvalidOperationException("目标显示器身份已变化，备份保留；不会写入其他显示器。");
            if(MonitorColorSnapshot.SameValues(current,record.Original,true)){store.Clear();Status="原硬件参数已核验；没有重复写入。";return;}
            if(!record.Pending&&!MonitorColorSnapshot.SameValues(current,record.Applied??record.Original,true))throw new InvalidOperationException("硬件参数被外部修改，未强制覆盖；原值备份保留。");
            record.Pending=true;store.Save(record);Write(record.Original,true);
            if(!MonitorColorSnapshot.SameValues(Read(),record.Original,true))throw new InvalidOperationException("还原后读取不一致，原值备份仍保留。");
            store.Clear();Status="原显示器硬件参数已还原并读取核验。";
        }
    }

    internal sealed class PhysicalColorOutput:IHardwareColorOutput
    {
        private static readonly object busLock=new object();
        private static readonly System.Diagnostics.Stopwatch busClock=System.Diagnostics.Stopwatch.StartNew();
        private static long lastCommand=-120;
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Physical
        {public IntPtr Handle;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;}
        [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)]private struct Device
        {public int cb;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=32)]public string Name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Description;public uint Flags;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Id;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=128)]public string Key;}
        [DllImport("user32.dll",CharSet=CharSet.Unicode)]private static extern bool EnumDisplayDevices(string device,uint index,ref Device result,uint flags);
        [DllImport("user32.dll")]private static extern IntPtr MonitorFromPoint(System.Drawing.Point point,uint flags);
        [DllImport("dxva2.dll",SetLastError=true)]private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr monitor,out uint count);
        [DllImport("dxva2.dll",SetLastError=true)]private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr monitor,uint count,[Out]Physical[] result);
        [DllImport("dxva2.dll")]private static extern bool DestroyPhysicalMonitors(uint count,Physical[] monitors);
        [DllImport("dxva2.dll",SetLastError=true)]private static extern bool GetVCPFeatureAndVCPFeatureReply(IntPtr monitor,byte code,out uint type,out uint value,out uint max);
        [DllImport("dxva2.dll",SetLastError=true)]private static extern bool SetVCPFeature(IntPtr monitor,byte code,uint value);
        private readonly string device,identity;
        internal PhysicalColorOutput(string device){this.device=device;identity=Identity();}
        private string Identity()
        {
            var item=new Device {cb=Marshal.SizeOf(typeof(Device))};
            if(!EnumDisplayDevices(device,0,ref item,1)||(item.Flags&1)==0||string.IsNullOrEmpty(item.Id))throw new InvalidOperationException("无法确认物理显示器身份。");return item.Id+"|"+item.Key;
        }
        private T WithMonitor<T>(Func<IntPtr,T> action)
        {
            if(Identity()!=identity)throw new InvalidOperationException("显示器身份已变化。");
            var screen=Screen.AllScreens.SingleOrDefault(x=>x.DeviceName==device);if(screen==null)throw new InvalidOperationException("原显示器未连接。");var bounds=screen.Bounds;
            IntPtr logical=MonitorFromPoint(new System.Drawing.Point(bounds.Left+bounds.Width/2,bounds.Top+bounds.Height/2),0);uint count;
            Check(GetNumberOfPhysicalMonitorsFromHMONITOR(logical,out count)&&count==1,"仅支持一对一物理显示器；不修改镜像或未识别设备。");
            var monitors=new Physical[1];Check(GetPhysicalMonitorsFromHMONITOR(logical,1,monitors),"无法打开显示器 DDC/CI。");
            try{if(Identity()!=identity)throw new InvalidOperationException("显示器连接已变化。");return action(monitors[0].Handle);}finally{DestroyPhysicalMonitors(1,monitors);}
        }
        private static void Check(bool success,string message){if(!success)throw new InvalidOperationException(message+"（代码 "+Marshal.GetLastWin32Error()+"）");}
        private static bool IsTransient(int code)
        {uint value=unchecked((uint)code);return value==0xC0262582||value==0xC0262583||(value>=0xC0262585&&value<=0xC026258C);}
        private static void Pace(){int wait=(int)(120-(busClock.ElapsedMilliseconds-lastCommand));if(wait>0)System.Threading.Thread.Sleep(wait);}
        private static MonitorColorLevel ReadFeature(IntPtr handle,byte feature)
        {
            lock(busLock)
            {
                for(int attempt=0;;attempt++)
                {
                    Pace();uint type,current,max;bool ok=GetVCPFeatureAndVCPFeatureReply(handle,feature,out type,out current,out max);int error=Marshal.GetLastWin32Error();lastCommand=busClock.ElapsedMilliseconds;
                    if(ok)return new MonitorColorLevel {Min=0,Value=current,Max=max};
                    if(attempt<2&&IsTransient(error)){System.Threading.Thread.Sleep(120*(attempt+1));continue;}
                    string reason=unchecked((uint)error)==0xC0262584?"显示器明确报告不支持该项目。":"DDC/CI 读取失败，不能据此判定显示器不支持。";
                    throw new InvalidOperationException(reason+" VCP 0x"+feature.ToString("X2")+"，错误 0x"+error.ToString("X8")+"；未重应用颜色。");
                }
            }
        }
        private static void WriteFeature(IntPtr handle,byte feature,uint value)
        {
            lock(busLock)
            {
                Pace();bool ok=SetVCPFeature(handle,feature,value);int error=Marshal.GetLastWin32Error();lastCommand=busClock.ElapsedMilliseconds;
                if(!ok)throw new InvalidOperationException("DDC/CI 写入返回失败，未自动重试写入。VCP 0x"+feature.ToString("X2")+"，错误 0x"+error.ToString("X8"));
            }
        }
        public MonitorColorSnapshot Read()
        {
            return WithMonitor(delegate(IntPtr handle){var state=new MonitorColorSnapshot {Device=device,Identity=identity,Contrast=ReadFeature(handle,0x12),Gains=new MonitorColorLevel[3]};
                byte[] features={0x16,0x18,0x1a};for(int i=0;i<3;i++)state.Gains[i]=ReadFeature(handle,features[i]);
                state.Preset=ReadFeature(handle,0x14).Value;state.Validate();return state;});
        }
        public void Contrast(uint value){WithMonitor(delegate(IntPtr handle){WriteFeature(handle,0x12,value);return 0;});}
        public void Gain(int channel,uint value){if(channel<0||channel>2)throw new ArgumentException("无效 RGB 通道。");WithMonitor(delegate(IntPtr handle){WriteFeature(handle,new byte[]{0x16,0x18,0x1a}[channel],value);return 0;});}
        public void Preset(uint value){WithMonitor(delegate(IntPtr handle){WriteFeature(handle,0x14,value);return 0;});}
    }
}
