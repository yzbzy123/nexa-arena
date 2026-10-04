using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace NexaArena
{
    // This boundary lets regression tests exercise recovery without touching a display.
    internal interface IRestoreModeBackend
    {
        DisplayMode ReadCurrent();
        int Apply(DisplayMode mode);
        void Pause(int milliseconds);
    }

    internal static class RestoreModePolicy
    {
        private const int PollCount = 24;
        private const int PollMilliseconds = 250;

        public static bool Matches(DisplayMode actual, DisplayMode expected)
        {
            return actual != null && expected != null &&
                actual.Width == expected.Width && actual.Height == expected.Height &&
                (expected.RefreshRate <= 0 || actual.RefreshRate == expected.RefreshRate) &&
                (expected.BitsPerPixel <= 0 || actual.BitsPerPixel == expected.BitsPerPixel);
        }

        public static DisplayMode Restore(DisplayMode mode, IRestoreModeBackend backend)
        {
            if (mode == null || mode.Width < 320 || mode.Height < 200)
                throw new InvalidOperationException("原始显示快照无效，未提交显示更改。");

            // Enabling a PnP monitor does not mean its display mode is readable yet.
            DisplayMode current = WaitForStableMode(backend, null);
            if (Matches(current, mode)) return current;

            // One ordinary restore only. Never reuse the stretch flags or fall back to 60 Hz.
            int result = backend.Apply(mode);
            if (result != 0)
                throw new InvalidOperationException("恢复原始显示模式 " + mode +
                    " 失败（代码 " + result + "）。未继续强制切换，原始状态已保留。");
            return WaitForStableMode(backend, mode);
        }

        private static DisplayMode WaitForStableMode(IRestoreModeBackend backend, DisplayMode expected)
        {
            DisplayMode previous = null;
            DisplayMode actual = null;
            int stableReads = 0;
            string lastError = "未读到有效模式";
            for (int i = 0; i < PollCount; i++)
            {
                try
                {
                    actual = backend.ReadCurrent();
                    bool valid = actual != null && actual.Width >= 320 && actual.Height >= 200;
                    if (valid && (expected == null || Matches(actual, expected)))
                    {
                        stableReads = Matches(actual, previous) ? stableReads + 1 : 1;
                        previous = actual;
                        if (stableReads >= 3) return actual;
                    }
                    else { stableReads = 0; previous = null; }
                    lastError = actual == null ? "无显示模式" : actual.ToString();
                }
                catch (Exception ex)
                {
                    stableReads = 0;
                    previous = null;
                    lastError = ex.Message;
                }
                if (i + 1 < PollCount) backend.Pause(PollMilliseconds);
            }
            throw new InvalidOperationException(expected == null
                ? "监视器启用后显示模式仍未稳定：" + lastError
                : "恢复校验未通过，目标 " + expected + "，实际/最后错误：" + lastError);
        }
    }

    internal sealed class RestoreResult
    {
        public DisplayMode ActualMode;
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();
        public bool Succeeded { get { return Errors.Count == 0; } }

        public static RestoreResult Failed(Exception error)
        {
            RestoreResult result = new RestoreResult();
            result.Errors.Add(error.Message);
            return result;
        }
    }

    internal interface IRestoreSessionBackend
    {
        void EnableMonitors(IEnumerable<string> instanceIds);
        DisplayMode RestoreMode(DisplayMode mode);
        void RestoreConfigs();
        void RestoreDesktop(SavedState state);
        void Complete();
    }

    internal sealed class NativeRestoreSessionBackend : IRestoreSessionBackend
    {
        public void EnableMonitors(IEnumerable<string> instanceIds) { MonitorDeviceService.EnableDevices(instanceIds); }
        public DisplayMode RestoreMode(DisplayMode mode) { return DisplayModeService.RestoreAndVerify(mode); }
        public void RestoreConfigs() { if (ConfigSessionStore.Exists) ConfigSessionStore.Restore(); }
        public void RestoreDesktop(SavedState state) { DesktopIconLayout.Restore(state); }
        public void Complete()
        {
            TrueStretchStageStore.Complete();
            SavedState.Delete();
        }
    }

    internal static class RestoreSession
    {
        public static RestoreResult Run(TrueStretchStage stage, SavedState state,
            IRestoreSessionBackend backend, Action<string> progress)
        {
            RestoreResult result = new RestoreResult();
            if (state == null && stage != null) state = stage.GetOriginalState();
            try
            {
                progress("正在重新启用监视器设备…");
                if (stage != null) backend.EnableMonitors(stage.MonitorIds);
            }
            catch (Exception ex) { result.Errors.Add("监视器：" + ex.Message); }

            // Do not change modes while monitor recovery is incomplete.
            if (!result.Succeeded) return result;
            try
            {
                if (stage != null && state == null)
                    throw new InvalidOperationException("拉伸会话存在，但原始显示快照丢失；已保留会话，未提交显示更改。");
                if (state != null)
                {
                    DisplayMode target = new DisplayMode(state.Width, state.Height, state.RefreshRate,
                        state.BitsPerPixel <= 0 ? 32 : state.BitsPerPixel, state.FixedOutput);
                    progress("正在等待显示模式稳定并恢复 " + target + "…");
                    result.ActualMode = backend.RestoreMode(target);
                    if (!RestoreModePolicy.Matches(result.ActualMode, target))
                        throw new InvalidOperationException("实际显示模式与原始快照不一致，未清理恢复状态。");
                }
                progress("显示恢复已完成，正在还原配置和清理会话…");
                backend.RestoreConfigs();
                if(state!=null)
                {
                    progress("显示已恢复，正在还原桌面图标位置…");
                    try {backend.RestoreDesktop(state);}
                    catch(Exception ex)
                    {
                        result.Warnings.Add(ex.Message);
                        RestoreTrace.Write("Desktop icon restore warning: "+ex.Message);
                    }
                }
                backend.Complete();
            }
            catch (Exception ex) { result.Errors.Add(ex.Message); }
            return result;
        }
    }

    internal sealed class RestoreJobRunner
    {
        private int running;
        public bool IsRunning { get { return Interlocked.CompareExchange(ref running, 0, 0) != 0; } }

        public bool TryStart(Func<RestoreResult> work, Action<RestoreResult> completed, Action<Action> post)
        {
            if (Interlocked.CompareExchange(ref running, 1, 0) != 0) return false;
            try
            {
                bool queued = ThreadPool.QueueUserWorkItem(delegate
                {
                    RestoreResult result;
                    try { result = work(); }
                    catch (Exception ex) { result = RestoreResult.Failed(ex); }
                    try
                    {
                        post(delegate
                        {
                            Interlocked.Exchange(ref running, 0);
                            completed(result);
                        });
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Exchange(ref running, 0);
                        ErrorLog.Write(ex);
                        RestoreTrace.Write("UI completion callback unavailable: " + ex.Message);
                    }
                });
                if (!queued) throw new InvalidOperationException("无法启动后台恢复任务。");
                return true;
            }
            catch { Interlocked.Exchange(ref running, 0); throw; }
        }
    }

    internal static class RestoreTrace
    {
        private static readonly object Sync = new object();
        public static void Write(string message)
        {
            try
            {
                lock (Sync)
                {
                    File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "restore.log"),
                        DateTime.Now.ToString("o") + " [thread " + Thread.CurrentThread.ManagedThreadId + "] " +
                        message + Environment.NewLine);
                }
            }
            catch { }
        }
    }
}
