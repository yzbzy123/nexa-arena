using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NexaArena
{
    internal sealed class AudioDeviceInfo
    {
        public string Id, Name;
        public int Flow;
        public bool IsDefault, Muted;
        public int Volume;
        public override string ToString() { return Name + (IsDefault ? "（默认）" : ""); }
    }
    internal sealed class AudioSessionInfo
    {
        public string DeviceId, SessionId, Name;
        public int Volume;
        public bool Muted;
        public override string ToString() { return Name + " · " + Volume + "%" + (Muted ? " · 静音" : ""); }
    }

    internal static class GameAudio
    {
        private static readonly Guid EndpointGuid = new Guid("5CDF2C82-841E-4546-9722-0CF74078229A");
        private static readonly Guid SessionsGuid = new Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F");
        private static void Release(object value) { if (value != null && Marshal.IsComObject(value)) Marshal.ReleaseComObject(value); }
        private static void HR(int value) { Marshal.ThrowExceptionForHR(value); }
        private static IMMDeviceEnumerator Enumerator() { return (IMMDeviceEnumerator)new MMDeviceEnumeratorComObject(); }
        private static string DefaultId(IMMDeviceEnumerator enumerator, int flow, int role)
        {
            IMMDevice device = null;
            try { if (enumerator.GetDefaultAudioEndpoint(flow, role, out device) < 0) return null; string id; HR(device.GetId(out id)); return id; }
            finally { Release(device); }
        }
        public static List<AudioDeviceInfo> Devices(int flow)
        {
            var list = new List<AudioDeviceInfo>();
            IMMDeviceEnumerator enumerator = Enumerator(); IMMDeviceCollection collection = null;
            try
            {
                string defaultId = DefaultId(enumerator, flow, flow == 1 ? 2 : 1);
                HR(enumerator.EnumAudioEndpoints(flow, 1, out collection)); uint count; HR(collection.GetCount(out count));
                for (uint i = 0; i < count; i++)
                {
                    IMMDevice device = null;
                    try
                    {
                        HR(collection.Item(i, out device));
                        AudioDeviceInfo info = Describe(device, flow); info.IsDefault = info.Id == defaultId; list.Add(info);
                    }
                    catch (Exception ex) { ErrorLog.Write(ex); }
                    finally { Release(device); }
                }
            }
            finally { Release(collection); Release(enumerator); }
            return list;
        }
        private static AudioDeviceInfo Describe(IMMDevice device, int flow)
        {
            string id; HR(device.GetId(out id)); string name = id;
            IPropertyStore properties = null;
            try
            {
                HR(device.OpenPropertyStore(0, out properties));
                PropertyKey key = new PropertyKey { FormatId = new Guid("A45C254E-DF1C-4EFD-8020-67D146A850E0"), Id = 14 };
                PropVariant value; HR(properties.GetValue(ref key, out value));
                try { if (value.Type == 31) name = Marshal.PtrToStringUni(value.Pointer); }
                finally { PropVariantClear(ref value); }
            }
            finally { Release(properties); }
            object volumeObject = null;
            try
            {
                Guid iid = EndpointGuid; HR(device.Activate(ref iid, 23, IntPtr.Zero, out volumeObject));
                var volume = (IAudioEndpointVolume)volumeObject; float level; int muted;
                HR(volume.GetMasterVolumeLevelScalar(out level)); HR(volume.GetMute(out muted));
                return new AudioDeviceInfo { Id=id, Name=name, Flow=flow, Volume=(int)Math.Round(level*100), Muted=muted!=0 };
            }
            finally { Release(volumeObject); }
        }
        public static void SetEndpointVolume(string id, int volume, bool mute)
        {
            if (Program.UiPreview) throw new InvalidOperationException("预览模式不修改音频设置。");
            if (volume < 0 || volume > 100) throw new ArgumentOutOfRangeException("volume");
            IMMDeviceEnumerator enumerator=Enumerator(); IMMDevice device=null; object obj=null;
            try
            {
                HR(enumerator.GetDevice(id,out device)); Guid iid=EndpointGuid;
                HR(device.Activate(ref iid,23,IntPtr.Zero,out obj)); var control=(IAudioEndpointVolume)obj;
                Guid context=Guid.Empty; HR(control.SetMasterVolumeLevelScalar(volume/100f,ref context));
                HR(control.SetMute(mute?1:0,ref context));
            }
            finally { Release(obj); Release(device); Release(enumerator); }
        }
        public static string ToggleDefaultMicrophone()
        {
            if (Program.UiPreview) throw new InvalidOperationException("预览模式不修改麦克风。");
            IMMDeviceEnumerator enumerator=Enumerator(); IMMDevice device=null; object obj=null;
            try
            {
                HR(enumerator.GetDefaultAudioEndpoint(1,2,out device)); AudioDeviceInfo info=Describe(device,1);
                Guid iid=EndpointGuid; HR(device.Activate(ref iid,23,IntPtr.Zero,out obj));
                var volume=(IAudioEndpointVolume)obj; int mute; HR(volume.GetMute(out mute));
                Guid context=Guid.Empty; HR(volume.SetMute(mute==0?1:0,ref context));
                int actual; HR(volume.GetMute(out actual));
                return info.Name + (actual!=0 ? "：已静音" : "：已取消静音");
            }
            finally { Release(obj); Release(device); Release(enumerator); }
        }
        public static void SetDefault(string id, int flow)
        {
            if (Program.UiPreview) throw new InvalidOperationException("预览模式不修改默认音频设备。");
            if (!Devices(flow).Exists(d=>d.Id==id)) throw new InvalidOperationException("设备已断开，请刷新列表。");
            IMMDeviceEnumerator enumerator=Enumerator(); object policyObject=null;
            string[] previous=new string[3];
            try
            {
                for(int i=0;i<3;i++) previous[i]=DefaultId(enumerator,flow,i);
                policyObject=new PolicyConfigClient(); var policy=(IPolicyConfig)policyObject;
                try
                {
                    for(int i=0;i<3;i++) HR(policy.SetDefaultEndpoint(id,i));
                    for(int i=0;i<3;i++) if(DefaultId(enumerator,flow,i)!=id) throw new InvalidOperationException("系统未确认新的默认设备。");
                }
                catch(Exception ex)
                {
                    bool rollbackFailed=false;
                    for(int i=0;i<3;i++) if(previous[i]!=null) { try { HR(policy.SetDefaultEndpoint(previous[i],i)); } catch { rollbackFailed=true; } }
                    throw new InvalidOperationException("切换默认设备失败："+ex.Message+(rollbackFailed?" 请在 Windows 声音设置检查默认设备。":" 已尝试还原原默认设备。"),ex);
                }
            }
            finally { Release(policyObject); Release(enumerator); }
        }
        public static List<AudioSessionInfo> Sessions(string deviceId)
        {
            List<AudioSessionInfo> list=new List<AudioSessionInfo>();
            VisitSessions(deviceId,delegate(IAudioSessionControl2 control,ISimpleAudioVolume volume)
            {
                int state; HR(control.GetState(out state)); if(state==2) return;
                string id,name; uint pid; HR(control.GetSessionInstanceIdentifier(out id)); HR(control.GetDisplayName(out name)); HR(control.GetProcessId(out pid));
                if(pid==0||control.IsSystemSoundsSession()==0) name="系统声音";
                else { try { using(Process p=Process.GetProcessById((int)pid)) name=p.ProcessName; } catch {
                    if(!string.IsNullOrEmpty(name)&&name.StartsWith("@",StringComparison.Ordinal))
                    {var label=new System.Text.StringBuilder(512);if(SHLoadIndirectString(name,label,(uint)label.Capacity,IntPtr.Zero)==0)name=label.ToString();}
                    if(string.IsNullOrWhiteSpace(name)||name.StartsWith("@",StringComparison.Ordinal))name="应用音频 · PID "+pid;
                } }
                float value; int mute; HR(volume.GetMasterVolume(out value)); HR(volume.GetMute(out mute));
                list.Add(new AudioSessionInfo {DeviceId=deviceId,SessionId=id,Name=name,Volume=(int)Math.Round(value*100),Muted=mute!=0});
            });
            return list;
        }
        public static void SetSession(AudioSessionInfo session,int level,bool mute)
        {
            if (Program.UiPreview) throw new InvalidOperationException("预览模式不修改应用音量。");
            if(level<0||level>100) throw new ArgumentOutOfRangeException("level");
            bool found=false;
            VisitSessions(session.DeviceId,delegate(IAudioSessionControl2 control,ISimpleAudioVolume volume)
            {
                string id; HR(control.GetSessionInstanceIdentifier(out id)); if(id!=session.SessionId) return;
                Guid context=Guid.Empty; HR(volume.SetMasterVolume(level/100f,ref context)); HR(volume.SetMute(mute?1:0,ref context)); found=true;
            });
            if(!found) throw new InvalidOperationException("该音频会话已结束，请刷新列表。");
        }
        private static void VisitSessions(string deviceId,Action<IAudioSessionControl2,ISimpleAudioVolume> visit)
        {
            IMMDeviceEnumerator enumerator=Enumerator(); IMMDevice device=null; object managerObject=null; IAudioSessionEnumerator sessions=null;
            try
            {
                HR(enumerator.GetDevice(deviceId,out device)); Guid iid=SessionsGuid;
                HR(device.Activate(ref iid,23,IntPtr.Zero,out managerObject));
                HR(((IAudioSessionManager2)managerObject).GetSessionEnumerator(out sessions)); int count; HR(sessions.GetCount(out count));
                for(int i=0;i<count;i++)
                {
                    object session=null;
                    try { HR(sessions.GetSession(i,out session)); visit((IAudioSessionControl2)session,(ISimpleAudioVolume)session); }
                    finally { Release(session); }
                }
            }
            finally { Release(sessions); Release(managerObject); Release(device); Release(enumerator); }
        }

        [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid FormatId; public uint Id; }
        [StructLayout(LayoutKind.Explicit,Size=24)] private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
        [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
        [DllImport("shlwapi.dll",CharSet=CharSet.Unicode,ExactSpelling=true)] private static extern int SHLoadIndirectString(string source,System.Text.StringBuilder output,uint characters,IntPtr reserved);
        [ComImport,Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumeratorComObject { }
        [ComImport,Guid("870AF99C-171D-4F9E-AF0D-E63DF40C2BC9")] private class PolicyConfigClient { }
        [ComImport,Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceEnumerator
        {
            [PreserveSig] int EnumAudioEndpoints(int flow,uint state,out IMMDeviceCollection devices);
            [PreserveSig] int GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);
            [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)]string id,out IMMDevice device);
            [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr callback);
            [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr callback);
        }
        [ComImport,Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDeviceCollection { [PreserveSig] int GetCount(out uint count); [PreserveSig] int Item(uint index,out IMMDevice device); }
        [ComImport,Guid("D666063F-1587-4E43-81F1-B948E807363F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IMMDevice
        {
            [PreserveSig] int Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)]out object result);
            [PreserveSig] int OpenPropertyStore(uint mode,out IPropertyStore store);
            [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)]out string id);
            [PreserveSig] int GetState(out uint state);
        }
        [ComImport,Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPropertyStore
        {
            [PreserveSig] int GetCount(out uint count); [PreserveSig] int GetAt(uint index,out PropertyKey key);
            [PreserveSig] int GetValue(ref PropertyKey key,out PropVariant value);
            [PreserveSig] int SetValue(ref PropertyKey key,ref PropVariant value); [PreserveSig] int Commit();
        }
        [ComImport,Guid("5CDF2C82-841E-4546-9722-0CF74078229A"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioEndpointVolume
        {
            void RegisterControlChangeNotify(IntPtr callback); void UnregisterControlChangeNotify(IntPtr callback); void GetChannelCount(out uint count);
            [PreserveSig] int SetMasterVolumeLevel(float db,ref Guid context);
            [PreserveSig] int SetMasterVolumeLevelScalar(float value,ref Guid context);
            [PreserveSig] int GetMasterVolumeLevel(out float db);
            [PreserveSig] int GetMasterVolumeLevelScalar(out float value);
            void SetChannelVolumeLevel(uint channel,float db,ref Guid context); void SetChannelVolumeLevelScalar(uint channel,float value,ref Guid context);
            void GetChannelVolumeLevel(uint channel,out float db); void GetChannelVolumeLevelScalar(uint channel,out float value);
            [PreserveSig] int SetMute(int mute,ref Guid context); [PreserveSig] int GetMute(out int mute);
        }
        [ComImport,Guid("77AA99A0-1BD6-484F-8BC7-2C654C9A9B6F"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionManager2
        {
            void GetAudioSessionControl(ref Guid session,uint flags,out IntPtr control);
            void GetSimpleAudioVolume(ref Guid session,uint flags,out IntPtr volume);
            [PreserveSig] int GetSessionEnumerator(out IAudioSessionEnumerator enumerator);
        }
        [ComImport,Guid("E2F5BB11-0570-40CA-ACDD-3AA01277DEE8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionEnumerator { [PreserveSig] int GetCount(out int count); [PreserveSig] int GetSession(int index,[MarshalAs(UnmanagedType.IUnknown)]out object session); }
        [ComImport,Guid("BFB7FF88-7239-4FC9-8FA2-07C950BE9C6D"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IAudioSessionControl2
        {
            [PreserveSig] int GetState(out int state);
            [PreserveSig] int GetDisplayName([MarshalAs(UnmanagedType.LPWStr)]out string name);
            void SetDisplayName([MarshalAs(UnmanagedType.LPWStr)]string name,ref Guid context);
            void GetIconPath([MarshalAs(UnmanagedType.LPWStr)]out string path); void SetIconPath([MarshalAs(UnmanagedType.LPWStr)]string path,ref Guid context);
            void GetGroupingParam(out Guid group); void SetGroupingParam(ref Guid group,ref Guid context);
            void RegisterAudioSessionNotification(IntPtr callback); void UnregisterAudioSessionNotification(IntPtr callback);
            void GetSessionIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
            [PreserveSig] int GetSessionInstanceIdentifier([MarshalAs(UnmanagedType.LPWStr)]out string id);
            [PreserveSig] int GetProcessId(out uint id);
            [PreserveSig] int IsSystemSoundsSession(); void SetDuckingPreference(int optOut);
        }
        [ComImport,Guid("87CE5498-68D6-44E5-9215-6DA47EF883D8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ISimpleAudioVolume
        {
            [PreserveSig] int SetMasterVolume(float value,ref Guid context); [PreserveSig] int GetMasterVolume(out float value);
            [PreserveSig] int SetMute(int mute,ref Guid context); [PreserveSig] int GetMute(out int mute);
        }
        // Default-device switching uses the Windows PolicyConfig COM interface,
        // as used by EarTrumpet. Only SetDefaultEndpoint is invoked; preceding slots are placeholders.
        [ComImport,Guid("F8679F50-850A-41CF-9C72-430F290290C8"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IPolicyConfig
        {
            void Unused1(); void Unused2(); void Unused3(); void Unused4(); void Unused5(); void Unused6(); void Unused7(); void Unused8(); void Unused9(); void Unused10();
            [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)]string id,int role);
        }
    }
}
