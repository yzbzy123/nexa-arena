using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;

namespace NexaArena
{
    // No native display API or real session/config store is called by these tests.
    internal static class RestoreRegressionTests
    {
        private static int passed;
        private static DisplayMode Original { get { return new DisplayMode(1920, 1080, 280, 32); } }
        private static DisplayMode Stretched { get { return new DisplayMode(1568, 1080, 280, 32); } }
        private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); }
        private static void Throws(Action action)
        {
            try { action(); } catch (InvalidOperationException) { return; }
            throw new Exception("Expected recovery failure");
        }
        private static void Test(string name, Action action)
        {
            action(); passed++; Console.WriteLine("PASS " + name);
        }

        [STAThread]
        private static int Main()
        {
            try
            {
                Test("restore preserves native DEVMODE fields and excludes scaling", TestParameters);
                Test("one ordinary restore at the saved 280 Hz", delegate
                {
                    FakeMode api = new FakeMode();
                    Check(RestoreModePolicy.Matches(RestoreModePolicy.Restore(Original, api), Original), "target mismatch");
                    Check(api.ApplyCount == 1 && api.Applied.RefreshRate == 280, "unexpected mode retry");
                    Check(api.BeforeReads >= 3 && api.AfterReads >= 3, "must verify stable mode");
                });
                Test("already manually restored does not submit a mode change", delegate
                {
                    FakeMode api = new FakeMode(); api.Before = Original;
                    RestoreModePolicy.Restore(Original, api);
                    Check(api.ApplyCount == 0, "unnecessary display reset");
                });
                Test("waits for monitor re-enumeration", delegate
                {
                    FakeMode api = new FakeMode(); api.ReadFailures = 3;
                    RestoreModePolicy.Restore(Original, api);
                    Check(api.ApplyCount == 1 && api.BeforeReads >= 6, "did not wait for readable display");
                });
                Test("no mode submitted when device never becomes readable", delegate
                {
                    FakeMode api = new FakeMode(); api.ReadFailures = 100;
                    Throws(delegate { RestoreModePolicy.Restore(Original, api); });
                    Check(api.ApplyCount == 0 && api.Pauses <= 23, "unbounded recovery");
                });
                Test("driver rejection never triggers stretch flags or 60 Hz retry", delegate
                {
                    FakeMode api = new FakeMode(); api.Result = -2;
                    Throws(delegate { RestoreModePolicy.Restore(Original, api); });
                    Check(api.ApplyCount == 1 && api.Applied.RefreshRate == 280, "forced fallback");
                });
                Test("API success with wrong resolution is a failure", delegate
                {
                    FakeMode api = new FakeMode(); api.After = Stretched;
                    Throws(delegate { RestoreModePolicy.Restore(Original, api); });
                    Check(api.ApplyCount == 1, "mode was submitted again");
                });
                Test("wrong refresh rate is not accepted", delegate
                {
                    FakeMode api = new FakeMode(); api.After = new DisplayMode(1920, 1080, 240, 32);
                    Throws(delegate { RestoreModePolicy.Restore(Original, api); });
                });
                Test("wrong color depth is not accepted", delegate
                {
                    FakeMode api = new FakeMode(); api.After = new DisplayMode(1920, 1080, 280, 24);
                    Throws(delegate { RestoreModePolicy.Restore(Original, api); });
                });
                Test("one good sample cannot hide a later bad sample", delegate
                {
                    FakeMode api = new FakeMode(); api.TransientMatch = true;
                    Throws(delegate { RestoreModePolicy.Restore(Original, api); });
                });
                Test("invalid snapshot does not touch display", delegate
                {
                    FakeMode api = new FakeMode();
                    Throws(delegate { RestoreModePolicy.Restore(new DisplayMode(0, 0, 0, 32), api); });
                    Check(api.BeforeReads == 0 && api.ApplyCount == 0, "invalid state caused display access");
                });
                Test("session enables then verifies before clearing state", delegate
                {
                    FakeSession backend = new FakeSession();
                    Check(RunSession(backend, State()).Succeeded, "recovery failed");
                    Check(string.Join(",", backend.Calls) == "enable,mode,configs,icons,complete", "incorrect restore order");
                });
                Test("monitor failure preserves state and avoids mode switch", delegate
                {
                    FakeSession backend = new FakeSession(); backend.FailAt = "enable";
                    Check(!RunSession(backend, State()).Succeeded, "false success");
                    Check(string.Join(",", backend.Calls) == "enable", "continued despite device failure");
                });
                Test("display failure keeps both display and config backups", delegate
                {
                    FakeSession backend = new FakeSession(); backend.FailAt = "mode";
                    Check(!RunSession(backend, State()).Succeeded, "false success");
                    Check(string.Join(",", backend.Calls) == "enable,mode", "deleted recovery state");
                });
                Test("incorrect display result cannot clear the snapshot", delegate
                {
                    FakeSession backend = new FakeSession(); backend.ModeResult = Stretched;
                    Check(!RunSession(backend, State()).Succeeded && !backend.Calls.Contains("complete"), "false success");
                });
                Test("missing snapshot still enables monitors but preserves session", delegate
                {
                    FakeSession backend = new FakeSession();
                    Check(!RunSession(backend, null).Succeeded, "accepted missing snapshot");
                    Check(string.Join(",", backend.Calls) == "enable", "unsafe fallback");
                });
                Test("legacy stage original mode is a valid fallback", delegate
                {
                    FakeSession backend = new FakeSession();
                    TrueStretchStage stage = new TrueStretchStage { OriginalWidth = 1920, OriginalHeight = 1080,
                        OriginalRefreshRate = 280, OriginalBitsPerPixel = 32 };
                    Check(RestoreSession.Run(stage, null, backend, Ignore).Succeeded, "lost legacy backup");
                });
                Test("config restore error does not complete display session", delegate
                {
                    FakeSession backend = new FakeSession(); backend.FailAt = "configs";
                    Check(!RunSession(backend, State()).Succeeded && !backend.Calls.Contains("complete"), "deleted backup");
                });
                Test("background execution, single flight, UI completion and retry", TestJobRunner);
                Test("icon failure warns without repeating monitor or display recovery",delegate
                {
                    FakeSession backend=new FakeSession();backend.FailAt="icons";
                    RestoreResult result=RunSession(backend,State());
                    Check(result.Succeeded&&result.Warnings.Count==1,"icon error incorrectly invalidated successful display recovery");
                    Check(string.Join(",",backend.Calls)=="enable,mode,configs,icons,complete","icon error retried or skipped cleanup");
                });
                Test("desktop matching uses identity and ignores added removed or ambiguous items",delegate
                {
                    var saved=new DesktopIconSnapshot {Topology="display",Icons=new List<DesktopIconPosition> {
                        new DesktopIconPosition {Identity="C:\\Desktop\\A.lnk",X=60,Y=80},
                        new DesktopIconPosition {Identity="C:\\Desktop\\B.lnk",X=160,Y=80},
                        new DesktopIconPosition {Identity="removed",X=20,Y=20}}};
                    var current=new List<DesktopIconPosition> {
                        new DesktopIconPosition {Identity="C:\\Desktop\\B.lnk"},
                        new DesktopIconPosition {Identity="C:\\Desktop\\A.lnk"},
                        new DesktopIconPosition {Identity="new"}};
                    var matched=DesktopIconPolicy.Match(saved,current,"display");
                    Check(matched.Count==2&&matched[0].X==160&&matched[1].X==60,"matched by enumeration index rather than identity");
                    current.Add(new DesktopIconPosition {Identity="C:\\Desktop\\A.lnk"});
                    Check(DesktopIconPolicy.Match(saved,current,"display").Count==1,"ambiguous identity was applied");
                });
                Test("desktop restore rejects wrong topology auto arrangement or corrupt coordinates",delegate
                {
                    var item=new DesktopIconPosition {Identity="desktop item",X=20,Y=30};
                    var saved=new DesktopIconSnapshot {Topology="original",Icons=new List<DesktopIconPosition>{item}};
                    var current=new List<DesktopIconPosition>{item};
                    Throws(delegate{DesktopIconPolicy.Match(saved,current,"different");});
                    saved.AutoArrange=true;Throws(delegate{DesktopIconPolicy.Match(saved,current,"original");});
                    saved.AutoArrange=false;item.X=int.MinValue;Throws(delegate{DesktopIconPolicy.Match(saved,current,"original");});
                });
                Console.WriteLine("Result=PASS Tests=" + passed + " (no display or user settings changed)");
                return 0;
            }
            catch (Exception ex) { Console.WriteLine("FAIL " + ex); return 1; }
        }

        private static SavedState State()
        {
            return new SavedState { Width = 1920, Height = 1080, RefreshRate = 280, BitsPerPixel = 32 };
        }
        private static RestoreResult RunSession(FakeSession backend, SavedState state)
        {
            return RestoreSession.Run(new TrueStretchStage(), state, backend, Ignore);
        }
        private static void Ignore(string message) { }

        private static void TestParameters()
        {
            Type devMode = typeof(DisplayModeService).GetNestedType("DevMode", BindingFlags.NonPublic);
            object current = Activator.CreateInstance(devMode);
            devMode.GetField("dmPositionX").SetValue(current, -1920);
            devMode.GetField("dmDisplayOrientation").SetValue(current, (uint)2);
            devMode.GetField("dmDisplayFixedOutput").SetValue(current, (uint)2);
            devMode.GetField("dmDisplayFlags").SetValue(current, (uint)7);
            object restored = typeof(DisplayModeService).GetMethod("PrepareRestoreMode", BindingFlags.NonPublic | BindingFlags.Static)
                .Invoke(null, new object[] { current, Original });
            Check((uint)devMode.GetField("dmFields").GetValue(restored) == 0x005C0000, "unexpected fields mask");
            Check((uint)devMode.GetField("dmPelsWidth").GetValue(restored) == 1920, "width");
            Check((uint)devMode.GetField("dmPelsHeight").GetValue(restored) == 1080, "height");
            Check((uint)devMode.GetField("dmDisplayFrequency").GetValue(restored) == 280, "refresh");
            Check((uint)devMode.GetField("dmBitsPerPel").GetValue(restored) == 32, "color depth");
            Check((int)devMode.GetField("dmPositionX").GetValue(restored) == -1920, "position overwritten");
            Check((uint)devMode.GetField("dmDisplayOrientation").GetValue(restored) == 2, "orientation overwritten");
            Check((uint)devMode.GetField("dmDisplayFixedOutput").GetValue(restored) == 2, "scaling overwritten");
            Check((uint)devMode.GetField("dmDisplayFlags").GetValue(restored) == 7, "flags overwritten");
        }

        private static void TestJobRunner()
        {
            RestoreJobRunner runner = new RestoreJobRunner();
            int uiThread = Thread.CurrentThread.ManagedThreadId;
            int workerThread = uiThread;
            bool completed = false;
            RestoreResult received = null;
            Action posted = null;
            using (ManualResetEvent release = new ManualResetEvent(false))
            using (ManualResetEvent queued = new ManualResetEvent(false))
            {
                Action<Action> post = delegate(Action callback) { posted = callback; queued.Set(); };
                Action<RestoreResult> complete = delegate(RestoreResult result)
                {
                    Check(Thread.CurrentThread.ManagedThreadId == uiThread, "UI callback on wrong thread");
                    completed = true; received = result;
                };
                Check(runner.TryStart(delegate
                {
                    workerThread = Thread.CurrentThread.ManagedThreadId;
                    if (!release.WaitOne(3000)) throw new Exception("test timeout");
                    return new RestoreResult();
                }, complete, post), "did not start");
                Check(runner.IsRunning && !completed, "startup blocked until completion");
                Check(!runner.TryStart(delegate { throw new Exception("duplicate work"); }, complete, post), "duplicate accepted");
                release.Set();
                Check(queued.WaitOne(3000), "completion not posted");
                Check(workerThread != uiThread && runner.IsRunning && !completed, "worker/UI guard invalid");
                posted();
                Check(completed && received.Succeeded && !runner.IsRunning, "completion did not unlock");
                queued.Reset(); completed = false;
                Check(runner.TryStart(delegate { throw new InvalidOperationException("injected failure"); }, complete, post), "retry rejected");
                Check(queued.WaitOne(3000), "failure not posted"); posted();
                Check(completed && !received.Succeeded && !runner.IsRunning, "failure did not release guard");
            }
        }

        private sealed class FakeMode : IRestoreModeBackend
        {
            public DisplayMode Before = Stretched, After = Original, Applied;
            public int BeforeReads, AfterReads, ReadFailures, ApplyCount, Pauses, Result;
            public bool TransientMatch;
            public DisplayMode ReadCurrent()
            {
                if (ApplyCount == 0)
                {
                    BeforeReads++;
                    if (ReadFailures-- > 0) throw new InvalidOperationException("display not enumerated");
                    return Before;
                }
                AfterReads++;
                return TransientMatch ? (AfterReads == 1 ? Original : Stretched) : After;
            }
            public int Apply(DisplayMode mode) { ApplyCount++; Applied = mode; return Result; }
            public void Pause(int milliseconds) { Pauses++; }
        }

        private sealed class FakeSession : IRestoreSessionBackend
        {
            public readonly List<string> Calls = new List<string>();
            public string FailAt;
            public DisplayMode ModeResult = Original;
            private void Call(string name) { Calls.Add(name); if (FailAt == name) throw new InvalidOperationException(name); }
            public void EnableMonitors(IEnumerable<string> ids) { Call("enable"); }
            public DisplayMode RestoreMode(DisplayMode mode) { Call("mode"); return ModeResult; }
            public void RestoreConfigs() { Call("configs"); }
            public void RestoreDesktop(SavedState state) { Call("icons"); }
            public void Complete() { Call("complete"); }
        }
    }
}
