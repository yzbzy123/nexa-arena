using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace NexaArena
{
    internal sealed class MonitorDeviceInfo
    {
        public string InstanceId;
        public string Name;
        public bool Started;

        public override string ToString()
        {
            return string.Format("{0} · {1}", string.IsNullOrWhiteSpace(Name) ? "监视器设备" : Name,
                Started ? "已启用" : "已禁用");
        }
    }

    internal static class MonitorDeviceService
    {
        private static readonly Guid MonitorClassGuid = new Guid("4d36e96e-e325-11ce-bfc1-08002be10318");
        private static readonly IntPtr InvalidHandleValue = new IntPtr(-1);
        private const uint DigcfPresent = 0x00000002;
        private const uint SpdrpDeviceDesc = 0x00000000;
        private const uint SpdrpFriendlyName = 0x0000000C;
        private const int DifPropertyChange = 0x00000012;
        private const int DicsEnable = 1;
        private const int DicsDisable = 2;
        private const int DicsFlagGlobal = 1;
        private const uint DnStarted = 0x00000008;

        [StructLayout(LayoutKind.Sequential)]
        private struct SpDevinfoData
        {
            public int cbSize;
            public Guid ClassGuid;
            public uint DevInst;
            public IntPtr Reserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpClassinstallHeader
        {
            public int cbSize;
            public int InstallFunction;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SpPropchangeParams
        {
            public SpClassinstallHeader ClassInstallHeader;
            public int StateChange;
            public int Scope;
            public int HwProfile;
        }

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInfo(IntPtr deviceInfoSet, uint memberIndex, ref SpDevinfoData deviceInfoData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceInstanceId(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData,
            StringBuilder deviceInstanceId, int deviceInstanceIdSize, out int requiredSize);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetupDiGetDeviceRegistryProperty(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData,
            uint property, out uint propertyRegDataType, byte[] propertyBuffer, uint propertyBufferSize, out uint requiredSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiSetClassInstallParams(IntPtr deviceInfoSet, ref SpDevinfoData deviceInfoData,
            ref SpPropchangeParams classInstallParams, int classInstallParamsSize);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiCallClassInstaller(int installFunction, IntPtr deviceInfoSet,
            ref SpDevinfoData deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("cfgmgr32.dll")]
        private static extern int CM_Get_DevNode_Status(out uint status, out uint problemNumber, uint devInst, uint flags);

        public static List<MonitorDeviceInfo> GetDevices(bool presentOnly)
        {
            List<MonitorDeviceInfo> devices = new List<MonitorDeviceInfo>();
            Guid classGuid = MonitorClassGuid;
            IntPtr set = SetupDiGetClassDevs(ref classGuid, null, IntPtr.Zero, presentOnly ? DigcfPresent : 0);
            if (set == InvalidHandleValue) throw new Win32Exception(Marshal.GetLastWin32Error(), "无法枚举监视器设备。");
            try
            {
                for (uint index = 0; ; index++)
                {
                    SpDevinfoData data = new SpDevinfoData();
                    data.cbSize = Marshal.SizeOf(typeof(SpDevinfoData));
                    if (!SetupDiEnumDeviceInfo(set, index, ref data))
                    {
                        int error = Marshal.GetLastWin32Error();
                        if (error == 259) break;
                        throw new Win32Exception(error);
                    }
                    StringBuilder id = new StringBuilder(1024);
                    int required;
                    if (!SetupDiGetDeviceInstanceId(set, ref data, id, id.Capacity, out required)) continue;
                    uint status, problem;
                    bool started = CM_Get_DevNode_Status(out status, out problem, data.DevInst, 0) == 0 && (status & DnStarted) != 0;
                    string name = GetProperty(set, ref data, SpdrpFriendlyName);
                    if (string.IsNullOrWhiteSpace(name)) name = GetProperty(set, ref data, SpdrpDeviceDesc);
                    devices.Add(new MonitorDeviceInfo { InstanceId = id.ToString(), Name = name, Started = started });
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            return devices;
        }

        public static List<string> DisableStartedDevices()
        {
            List<string> changed = new List<string>();
            foreach (MonitorDeviceInfo device in GetDevices(true).Where(x => x.Started))
            {
                ChangeState(device.InstanceId, false);
                changed.Add(device.InstanceId);
            }
            if (changed.Count == 0) throw new InvalidOperationException("没有找到可禁用的已启用监视器设备。");
            return changed;
        }

        public static void EnableDevices(IEnumerable<string> instanceIds)
        {
            List<Exception> failures = new List<Exception>();
            foreach (string id in instanceIds.Distinct(StringComparer.OrdinalIgnoreCase))
            {
                // A manually recovered or rebooted monitor must not be reset again.
                if (GetStartedState(id) == true) continue;
                RestoreTrace.Write("Enable monitor begin: " + id);
                Exception last = null;
                for (int attempt = 0; attempt < 3; attempt++)
                {
                    try { ChangeState(id, true); last = null; break; }
                    catch (Exception ex) { last = ex; Thread.Sleep(400); }
                }
                if (last != null) failures.Add(last);
                RestoreTrace.Write("Enable monitor end: " + id + (last == null ? " OK" : " FAILED: " + last.Message));
            }
            if (failures.Count > 0) throw new AggregateException("部分监视器设备未能重新启用。", failures);
        }

        private static void ChangeState(string instanceId, bool enable)
        {
            Exception setupError = null;
            try { ChangeStateSetupApi(instanceId, enable); }
            catch (Exception ex) { setupError = ex; }
            Thread.Sleep(700);
            bool? started = GetStartedState(instanceId);
            // null 表示无法确认状态，不能把它当成“禁用成功”。旧逻辑因此
            // 会在设备管理器完全没变化时继续执行后续分辨率流程。
            if ((enable && started == true) || (!enable && started == false)) return;

            string pnputilOutput = RunPnPUtil(instanceId, enable);
            Thread.Sleep(900);
            started = GetStartedState(instanceId);
            if ((enable && started == true) || (!enable && started == false)) return;
            throw new InvalidOperationException((enable ? "启用" : "禁用") + "监视器设备后验证失败。" +
                " 当前状态=" + (started.HasValue ? (started.Value ? "Started" : "Stopped") : "Unknown") + "." +
                (setupError == null ? "" : " SetupAPI: " + setupError.Message) + " PnPUtil: " + pnputilOutput);
        }

        private static void ChangeStateSetupApi(string instanceId, bool enable)
        {
            Guid classGuid = MonitorClassGuid;
            IntPtr set = SetupDiGetClassDevs(ref classGuid, null, IntPtr.Zero, 0);
            if (set == InvalidHandleValue) throw new Win32Exception(Marshal.GetLastWin32Error());
            bool found = false;
            try
            {
                for (uint index = 0; ; index++)
                {
                    SpDevinfoData data = new SpDevinfoData();
                    data.cbSize = Marshal.SizeOf(typeof(SpDevinfoData));
                    if (!SetupDiEnumDeviceInfo(set, index, ref data))
                    {
                        if (Marshal.GetLastWin32Error() == 259) break;
                        continue;
                    }
                    StringBuilder id = new StringBuilder(1024);
                    int required;
                    if (!SetupDiGetDeviceInstanceId(set, ref data, id, id.Capacity, out required) ||
                        !string.Equals(id.ToString(), instanceId, StringComparison.OrdinalIgnoreCase)) continue;
                    found = true;
                    SpPropchangeParams parameters = new SpPropchangeParams();
                    parameters.ClassInstallHeader.cbSize = Marshal.SizeOf(typeof(SpClassinstallHeader));
                    parameters.ClassInstallHeader.InstallFunction = DifPropertyChange;
                    parameters.StateChange = enable ? DicsEnable : DicsDisable;
                    parameters.Scope = DicsFlagGlobal;
                    parameters.HwProfile = 0;
                    if (!SetupDiSetClassInstallParams(set, ref data, ref parameters, Marshal.SizeOf(typeof(SpPropchangeParams))) ||
                        !SetupDiCallClassInstaller(DifPropertyChange, set, ref data))
                        throw new Win32Exception(Marshal.GetLastWin32Error(), (enable ? "启用" : "禁用") + "监视器设备失败。");
                    break;
                }
            }
            finally { SetupDiDestroyDeviceInfoList(set); }
            if (!found) throw new InvalidOperationException("找不到监视器设备：" + instanceId);
        }

        private static bool? GetStartedState(string instanceId)
        {
            try
            {
                MonitorDeviceInfo device = GetDevices(false).FirstOrDefault(x =>
                    string.Equals(x.InstanceId, instanceId, StringComparison.OrdinalIgnoreCase));
                return device == null ? (bool?)null : device.Started;
            }
            catch { return null; }
        }

        private static string RunPnPUtil(string instanceId, bool enable)
        {
            string exe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "pnputil.exe");
            string safeId = (instanceId ?? string.Empty).Replace("\"", string.Empty);
            string arguments = (enable ? "/enable-device " : "/disable-device ") + "\"" + safeId + "\"" +
                (enable ? string.Empty : " /force");
            ProcessStartInfo info = new ProcessStartInfo(exe, arguments);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;
            using (Process process = Process.Start(info))
            {
                string output = process.StandardOutput.ReadToEnd();
                string error = process.StandardError.ReadToEnd();
                process.WaitForExit(15000);
                if (!process.HasExited || process.ExitCode != 0)
                    throw new InvalidOperationException("PnPUtil 退出代码 " + (process.HasExited ? process.ExitCode.ToString() : "超时") +
                        "：" + output + " " + error);
                return (output + " " + error).Trim();
            }
        }

        private static string GetProperty(IntPtr set, ref SpDevinfoData data, uint property)
        {
            byte[] buffer = new byte[2048];
            uint type, required;
            if (!SetupDiGetDeviceRegistryProperty(set, ref data, property, out type, buffer, (uint)buffer.Length, out required))
                return null;
            return Encoding.Unicode.GetString(buffer, 0, (int)Math.Min(required, (uint)buffer.Length)).TrimEnd('\0');
        }
    }

    internal sealed class TrueStretchStage
    {
        public int Step;
        public int TargetWidth;
        public int TargetHeight;
        public int BridgeWidth;
        public int BridgeHeight;
        public int OriginalWidth;
        public int OriginalHeight;
        public int OriginalRefreshRate;
        public int OriginalBitsPerPixel;
        public int OriginalFixedOutput;
        public List<string> MonitorIds = new List<string>();

        public SavedState GetOriginalState()
        {
            if(OriginalWidth<320 || OriginalHeight<200) return null;
            return new SavedState {
                Width=OriginalWidth,
                Height=OriginalHeight,
                RefreshRate=OriginalRefreshRate,
                BitsPerPixel=OriginalBitsPerPixel,
                FixedOutput=OriginalFixedOutput,
                MonitorCount=1,
                SavedAt=DateTime.Now
            };
        }
    }

    internal static class TrueStretchStageStore
    {
        public static string FilePath { get { return Path.Combine(SavedState.DirectoryPath, "true-stretch-stage.ini"); } }
        public static string KeepPath { get { return FilePath + ".keep"; } }
        public static string DonePath { get { return FilePath + ".done"; } }
        public static bool Exists { get { return File.Exists(FilePath); } }

        public static void Save(TrueStretchStage stage)
        {
            Directory.CreateDirectory(SavedState.DirectoryPath);
            List<string> lines = new List<string>();
            lines.Add("Step=" + stage.Step);
            lines.Add("TargetWidth=" + stage.TargetWidth);
            lines.Add("TargetHeight=" + stage.TargetHeight);
            lines.Add("BridgeWidth=" + stage.BridgeWidth);
            lines.Add("BridgeHeight=" + stage.BridgeHeight);
            lines.Add("OriginalWidth=" + stage.OriginalWidth);
            lines.Add("OriginalHeight=" + stage.OriginalHeight);
            lines.Add("OriginalRefreshRate=" + stage.OriginalRefreshRate);
            lines.Add("OriginalBitsPerPixel=" + stage.OriginalBitsPerPixel);
            lines.Add("OriginalFixedOutput=" + stage.OriginalFixedOutput);
            foreach (string id in stage.MonitorIds)
                lines.Add("Monitor=" + Convert.ToBase64String(Encoding.UTF8.GetBytes(id)));
            File.WriteAllLines(FilePath, lines.ToArray(), Encoding.UTF8);
        }

        public static TrueStretchStage Load()
        {
            if (!File.Exists(FilePath)) return null;
            TrueStretchStage stage = new TrueStretchStage();
            foreach (string line in File.ReadAllLines(FilePath))
            {
                int separator = line.IndexOf('=');
                if (separator < 0) continue;
                string key = line.Substring(0, separator);
                string value = line.Substring(separator + 1);
                int number;
                if (key == "Step" && int.TryParse(value, out number)) stage.Step = number;
                if (key == "TargetWidth" && int.TryParse(value, out number)) stage.TargetWidth = number;
                if (key == "TargetHeight" && int.TryParse(value, out number)) stage.TargetHeight = number;
                if (key == "BridgeWidth" && int.TryParse(value, out number)) stage.BridgeWidth = number;
                if (key == "BridgeHeight" && int.TryParse(value, out number)) stage.BridgeHeight = number;
                if (key == "OriginalWidth" && int.TryParse(value, out number)) stage.OriginalWidth = number;
                if (key == "OriginalHeight" && int.TryParse(value, out number)) stage.OriginalHeight = number;
                if (key == "OriginalRefreshRate" && int.TryParse(value, out number)) stage.OriginalRefreshRate = number;
                if (key == "OriginalBitsPerPixel" && int.TryParse(value, out number)) stage.OriginalBitsPerPixel = number;
                if (key == "OriginalFixedOutput" && int.TryParse(value, out number)) stage.OriginalFixedOutput = number;
                if (key == "Monitor")
                {
                    try { stage.MonitorIds.Add(Encoding.UTF8.GetString(Convert.FromBase64String(value))); }
                    catch { }
                }
            }
            return stage;
        }

        public static void StartWatchdog()
        {
            DeleteIfExists(KeepPath);
            DeleteIfExists(DonePath);
            ProcessStartInfo info = new ProcessStartInfo(ApplicationPath(), "--monitor-watchdog \"" + FilePath +
                "\" " + Process.GetCurrentProcess().Id);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            Process.Start(info);
        }

        public static void ConfirmVisible()
        {
            File.WriteAllText(KeepPath, "keep", Encoding.ASCII);
        }

        public static void Complete()
        {
            try { File.WriteAllText(DonePath, "done", Encoding.ASCII); } catch { }
            DeleteIfExists(FilePath);
            DeleteIfExists(KeepPath);
        }

        private static string ApplicationPath()
        {
            return Process.GetCurrentProcess().MainModule.FileName;
        }

        private static void DeleteIfExists(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { }
        }
    }

    internal static class MonitorWatchdog
    {
        public static int Run(string stageFile,int parentPid)
        {
            try
            {
                // 不再使用固定 120 秒超时。用户启动游戏、进入设置并确认可能
                // 超过两分钟；旧逻辑会在主程序仍正常运行时擅自启用监视器。
                // 现在只在主进程意外退出且没有正常完成标记时执行灾难恢复。
                while(parentPid>0)
                {
                    if (File.Exists(stageFile + ".done")) return 0;
                    bool alive=false;
                    try
                    {
                        using(Process parent=Process.GetProcessById(parentPid)) alive=!parent.HasExited;
                    }
                    catch { alive=false; }
                    if(!alive) break;
                    Thread.Sleep(500);
                }
                if(File.Exists(stageFile+".done")) return 0;
                RestoreTrace.Write("Watchdog recovery started after parent exit");
                RestoreResult result = RestoreSession.Run(TrueStretchStageStore.Load(), SavedState.Load(),
                    new NativeRestoreSessionBackend(), RestoreTrace.Write);
                if (!result.Succeeded)
                    throw new InvalidOperationException(string.Join("\r\n", result.Errors));
                RestoreTrace.Write("Watchdog recovery completed");
                return 0;
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                return 1;
            }
        }
    }
}
