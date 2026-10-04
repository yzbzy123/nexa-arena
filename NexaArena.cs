using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Reflection.AssemblyTitle("Nexa Arena")]
[assembly: System.Reflection.AssemblyDescription("VALORANT and CS2 game toolbox")]
[assembly: System.Reflection.AssemblyProduct("Nexa Arena")]
[assembly: System.Reflection.AssemblyVersion("1.0.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("1.0.0.0")]

namespace NexaArena
{
    internal static class Program
    {
        internal static bool UiPreview { get; private set; }
        [STAThread]
        private static void Main(string[] args)
        {
            if (args.Length >= 2 && string.Equals(args[0], "--monitor-watchdog", StringComparison.OrdinalIgnoreCase))
            {
                int parentPid=0;
                if(args.Length>=3) int.TryParse(args[2],out parentPid);
                Environment.ExitCode = MonitorWatchdog.Run(args[1],parentPid);
                return;
            }
            if (args.Any(a => string.Equals(a, "--self-test", StringComparison.OrdinalIgnoreCase)))
            {
                Environment.ExitCode = SelfTest.Run();
                return;
            }

            UiPreview = args.Any(a => string.Equals(a, "--ui-preview", StringComparison.OrdinalIgnoreCase));
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                ErrorLog.Write(e.Exception);
                MessageBox.Show("程序遇到错误：\r\n" + e.Exception.Message + "\r\n\r\n详细信息已写入 error.log。",
                    "Nexa Arena", MessageBoxButtons.OK, MessageBoxIcon.Error);
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ErrorLog.Write(e.ExceptionObject as Exception ?? new Exception(Convert.ToString(e.ExceptionObject)));
            };
#if WEB_UI
            Application.Run(new WebMainForm());
#else
            Application.Run(new ModernMainForm());
#endif
        }
    }

    internal static class Theme
    {
        public static readonly Color Accent = Color.FromArgb(255, 70, 91);
        public static readonly Color AccentDark = Color.FromArgb(224, 49, 70);
        public static readonly Color Green = Color.FromArgb(65, 188, 112);
        public static readonly Color Background = Color.FromArgb(244, 246, 249);
        public static readonly Color Card = Color.White;
        public static readonly Color Text = Color.FromArgb(37, 43, 54);
        public static readonly Color Muted = Color.FromArgb(120, 129, 145);
        public static readonly Font DefaultFont = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);
        public static readonly Font SmallFont = new Font("Microsoft YaHei UI", 8.5F, FontStyle.Regular);

        public static Button Button(string text, Color color, int height)
        {
            Button button = new Button();
            button.Text = text;
            button.Height = height;
            button.Dock = DockStyle.Top;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = 0;
            button.BackColor = color;
            button.ForeColor = Color.White;
            button.Cursor = Cursors.Hand;
            button.Font = new Font("Microsoft YaHei UI", 11F, FontStyle.Bold);
            button.Margin = new Padding(0, 6, 0, 6);
            return button;
        }

        public static Label Label(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = true;
            label.Font = new Font("Microsoft YaHei UI", size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            return label;
        }
    }

    internal sealed class DisplayMode
    {
        public int Width { get; private set; }
        public int Height { get; private set; }
        public int RefreshRate { get; private set; }
        public int BitsPerPixel { get; private set; }
        public int FixedOutput { get; private set; }

        public DisplayMode(int width, int height, int refreshRate, int bitsPerPixel)
            : this(width, height, refreshRate, bitsPerPixel, 0)
        {
        }

        public DisplayMode(int width, int height, int refreshRate, int bitsPerPixel, int fixedOutput)
        {
            Width = width;
            Height = height;
            RefreshRate = refreshRate;
            BitsPerPixel = bitsPerPixel;
            FixedOutput = fixedOutput;
        }

        public override string ToString()
        {
            return string.Format("{0} × {1} @ {2}Hz", Width, Height, RefreshRate);
        }
    }

    internal static class DisplayModeService
    {
        private const int EnumCurrentSettings = -1;
        private const int DispChangeSuccessful = 0;
        private const uint CdsUpdateRegistry = 0x00000001;
        private const uint CdsTest = 0x00000002;
        private const uint DmBitsPerPel = 0x00040000;
        private const uint DmPelsWidth = 0x00080000;
        private const uint DmPelsHeight = 0x00100000;
        private const uint DmDisplayFrequency = 0x00400000;
        private const uint DmDisplayFixedOutput = 0x20000000;
        private const uint DmdfoStretch = 2;
        // 按显示驱动的提交标志依次尝试支持的模式。
        private static readonly int[] DisplayChangeRetryFlags = new int[] {
            unchecked((int)0x10000001),
            unchecked((int)0x40000001),
            unchecked((int)0x40000101),
            unchecked((int)0x00000001)
        };

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public ushort dmSpecVersion;
            public ushort dmDriverVersion;
            public ushort dmSize;
            public ushort dmDriverExtra;
            public uint dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public uint dmDisplayOrientation;
            public uint dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public ushort dmLogPixels;
            public uint dmBitsPerPel;
            public uint dmPelsWidth;
            public uint dmPelsHeight;
            public uint dmDisplayFlags;
            public uint dmDisplayFrequency;
            public uint dmICMMethod;
            public uint dmICMIntent;
            public uint dmMediaType;
            public uint dmDitherType;
            public uint dmReserved1;
            public uint dmReserved2;
            public uint dmPanningWidth;
            public uint dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettingsEx(string deviceName, ref DevMode devMode,
            IntPtr hwnd, uint flags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int ChangeDisplaySettings(ref DevMode devMode, int flags);

        public static string PrimaryDeviceName
        {
            get { return Screen.PrimaryScreen == null ? null : Screen.PrimaryScreen.DeviceName; }
        }

        private static DevMode EmptyDevMode()
        {
            DevMode dm = new DevMode();
            dm.dmDeviceName = new string('\0', 32);
            dm.dmFormName = new string('\0', 32);
            dm.dmSize = (ushort)Marshal.SizeOf(typeof(DevMode));
            return dm;
        }

        public static DisplayMode GetCurrent()
        {
            DevMode dm = EmptyDevMode();
            if (!EnumDisplaySettings(PrimaryDeviceName, EnumCurrentSettings, ref dm))
                throw new InvalidOperationException("无法读取当前显示模式。");
            return new DisplayMode((int)dm.dmPelsWidth, (int)dm.dmPelsHeight,
                (int)dm.dmDisplayFrequency, (int)dm.dmBitsPerPel, (int)dm.dmDisplayFixedOutput);
        }

        public static List<DisplayMode> GetSupportedModes()
        {
            List<DisplayMode> modes = new List<DisplayMode>();
            for (int i = 0; i < 4096; i++)
            {
                DevMode dm = EmptyDevMode();
                if (!EnumDisplaySettings(PrimaryDeviceName, i, ref dm)) break;
                DisplayMode mode = new DisplayMode((int)dm.dmPelsWidth, (int)dm.dmPelsHeight,
                    (int)dm.dmDisplayFrequency, (int)dm.dmBitsPerPel);
                if (!modes.Any(x => x.Width == mode.Width && x.Height == mode.Height &&
                    x.RefreshRate == mode.RefreshRate && x.BitsPerPixel == mode.BitsPerPixel))
                    modes.Add(mode);
            }
            return modes.OrderBy(x => x.Width).ThenBy(x => x.Height).ThenByDescending(x => x.RefreshRate).ToList();
        }

        public static DisplayMode FindBest(int width, int height)
        {
            DisplayMode current = GetCurrent();
            List<DisplayMode> exact = GetSupportedModes().Where(x => x.Width == width && x.Height == height).ToList();
            if (exact.Count == 0) return new DisplayMode(width, height, current.RefreshRate, current.BitsPerPixel);
            DisplayMode sameRefresh = exact.FirstOrDefault(x => x.RefreshRate == current.RefreshRate && x.BitsPerPixel == current.BitsPerPixel);
            if (sameRefresh != null) return sameRefresh;
            return exact.OrderByDescending(x => x.BitsPerPixel).ThenByDescending(x => x.RefreshRate).First();
        }

        public static bool IsSupported(int width, int height)
        {
            return GetSupportedModes().Any(x => x.Width == width && x.Height == height);
        }

        public static void Set(DisplayMode mode)
        {
            Set(mode, false);
        }

        public static void Set(DisplayMode mode, bool forceStretch)
        {
            DevMode dm = EmptyDevMode();
            dm.dmPelsWidth = (uint)mode.Width;
            dm.dmPelsHeight = (uint)mode.Height;
            dm.dmDisplayFrequency = (uint)mode.RefreshRate;
            dm.dmBitsPerPel = (uint)Math.Max(mode.BitsPerPixel, 32);
            dm.dmDisplayFixedOutput = forceStretch ? DmdfoStretch : (uint)Math.Max(0, mode.FixedOutput);
            dm.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency | DmBitsPerPel | DmDisplayFixedOutput;

            int test = ChangeDisplaySettingsEx(PrimaryDeviceName, ref dm, IntPtr.Zero, CdsTest, IntPtr.Zero);
            if (test != DispChangeSuccessful)
                throw new InvalidOperationException("显卡驱动拒绝该显示模式（代码 " + test + "）。请先在显卡控制面板中创建自定义分辨率。");

            int result = ChangeDisplaySettingsEx(PrimaryDeviceName, ref dm, IntPtr.Zero, CdsUpdateRegistry, IntPtr.Zero);
            if (result != DispChangeSuccessful)
                throw new InvalidOperationException("切换分辨率失败（代码 " + result + "）。");
        }

        public static void SetReferenceStyle(DisplayMode mode)
        {
            DevMode dm = EmptyDevMode();
            if (!EnumDisplaySettings(null, EnumCurrentSettings, ref dm))
                throw new InvalidOperationException("无法读取默认显示设备的当前模式。");
            dm.dmPelsWidth = (uint)mode.Width;
            dm.dmPelsHeight = (uint)mode.Height;
            dm.dmFields = DmPelsWidth | DmPelsHeight;
            int result = ChangeDisplaySettings(ref dm, 0);
            if (result != DispChangeSuccessful)
                throw new InvalidOperationException("参考模式切换失败（代码 " + result + "）。");
            System.Threading.Thread.Sleep(250);
            DisplayMode current = GetCurrent();
            if (current.Width != mode.Width || current.Height != mode.Height)
                throw new InvalidOperationException("Windows 返回切换成功，但实际分辨率仍为 " + current.Width + " × " + current.Height + "。");
        }

        public static void ForceReferenceStyle(DisplayMode mode)
        {
            DevMode dm = EmptyDevMode();
            if (!EnumDisplaySettings(null, EnumCurrentSettings, ref dm))
                throw new InvalidOperationException("无法读取默认显示设备的当前模式。");
            dm.dmPelsWidth = (uint)mode.Width;
            dm.dmPelsHeight = (uint)mode.Height;
            dm.dmFields = DmPelsWidth | DmPelsHeight;
            int result = ChangeDisplaySettings(ref dm, unchecked((int)0x40000000));
            if (result != DispChangeSuccessful)
                throw new InvalidOperationException("强制重应用分辨率失败（代码 " + result + "）。");
            System.Threading.Thread.Sleep(250);
            DisplayMode current = GetCurrent();
            if (current.Width != mode.Width || current.Height != mode.Height)
                throw new InvalidOperationException("强制重应用后实际分辨率仍为 " + current.Width + " × " + current.Height + "。");
        }

        public static bool TrySetBridgeMode(int width, int height)
        {
            DevMode dm = EmptyDevMode();
            if (!EnumDisplaySettings(null, EnumCurrentSettings, ref dm)) return false;
            dm.dmPelsWidth = (uint)width;
            dm.dmPelsHeight = (uint)height;
            dm.dmFields = DmPelsWidth | DmPelsHeight;
            int result = ChangeDisplaySettings(ref dm, unchecked((int)CdsUpdateRegistry));
            if (result != DispChangeSuccessful) return false;
            System.Threading.Thread.Sleep(250);
            DisplayMode current = GetCurrent();
            return current.Width == width && current.Height == height;
        }

        public static void ApplyTargetMode(int width, int height, int preferredRefresh)
        {
            List<int> refreshRates = new List<int>();
            if (preferredRefresh > 0) refreshRates.Add(preferredRefresh);
            if (!refreshRates.Contains(60)) refreshRates.Add(60);
            int lastResult = -999;

            foreach (int refresh in refreshRates)
            {
                DevMode dm = EmptyDevMode();
                if (!EnumDisplaySettings(null, EnumCurrentSettings, ref dm))
                    throw new InvalidOperationException("无法读取默认显示设备的当前模式。");
                dm.dmPelsWidth = (uint)width;
                dm.dmPelsHeight = (uint)height;
                dm.dmBitsPerPel = 32;
                dm.dmDisplayFrequency = (uint)refresh;
                // 同时提交色深、宽、高和刷新率。
                dm.dmFields = DmBitsPerPel | DmPelsWidth | DmPelsHeight | DmDisplayFrequency;

                foreach (int flags in DisplayChangeRetryFlags)
                {
                    lastResult = ChangeDisplaySettings(ref dm, flags);
                    System.Threading.Thread.Sleep(200);
                    DisplayMode current = GetCurrent();
                    if (lastResult == DispChangeSuccessful &&
                        current.Width == width && current.Height == height)
                    {
                        // 原程序 hpVgg... 在 CISR... 已把桌面切到目标后，仍会
                        // 无条件再调用一次 NbeUv...：仅写宽、高、刷新率并使用
                        // CDS_UPDATEREGISTRY。这个同模式最终提交用于让 Valorant
                        // 的无边框窗口重新绑定当前桌面；不能在首次命中时提前返回。
                        if(CommitDisplayMode(width,height,refresh,out lastResult)) return;
                        break;
                    }
                }

                // 对应原程序 NbeUv... 的最终回退：保留当前 DEVMODE，
                // 只写宽、高、刷新率并使用 CDS_UPDATEREGISTRY。
                if(CommitDisplayMode(width,height,refresh,out lastResult)) return;
            }

            DisplayMode actual = GetCurrent();
            throw new InvalidOperationException("无法应用目标显示模式（最后代码 " +
                lastResult + "，实际 " + actual.Width + " × " + actual.Height + "）。");
        }

        private static bool CommitDisplayMode(int width,int height,int refresh,out int result)
        {
            DevMode dm=EmptyDevMode();
            if(!EnumDisplaySettings(null,EnumCurrentSettings,ref dm))
            {
                result=-999;
                return false;
            }
            dm.dmPelsWidth=(uint)width;
            dm.dmPelsHeight=(uint)height;
            dm.dmDisplayFrequency=(uint)refresh;
            dm.dmFields=DmPelsWidth|DmPelsHeight|DmDisplayFrequency;
            result=ChangeDisplaySettings(ref dm,unchecked((int)CdsUpdateRegistry));
            System.Threading.Thread.Sleep(300);
            DisplayMode actual=GetCurrent();
            return result==DispChangeSuccessful && actual.Width==width && actual.Height==height;
        }

        public static DisplayMode RestoreAndVerify(DisplayMode mode)
        {
            return RestoreModePolicy.Restore(mode, new NativeRestoreModeBackend());
        }

        private static DevMode PrepareRestoreMode(DevMode current, DisplayMode original)
        {
            // Preserve the current DEVMODE; do not submit
            // dmDisplayFixedOutput, topology, position or stretch-specific reset flags.
            current.dmPelsWidth = (uint)original.Width;
            current.dmPelsHeight = (uint)original.Height;
            current.dmBitsPerPel = (uint)(original.BitsPerPixel > 0 ? original.BitsPerPixel : 32);
            current.dmFields = DmPelsWidth | DmPelsHeight | DmBitsPerPel;
            if (original.RefreshRate > 0)
            {
                current.dmDisplayFrequency = (uint)original.RefreshRate;
                current.dmFields |= DmDisplayFrequency;
            }
            return current;
        }

        private sealed class NativeRestoreModeBackend : IRestoreModeBackend
        {
            public DisplayMode ReadCurrent()
            {
                DevMode dm = EmptyDevMode();
                // Avoid WinForms Screen caches after PnP re-enumeration.
                if (!EnumDisplaySettings(null, EnumCurrentSettings, ref dm))
                    throw new InvalidOperationException("默认显示设备的当前模式暂不可读。");
                return new DisplayMode((int)dm.dmPelsWidth, (int)dm.dmPelsHeight,
                    (int)dm.dmDisplayFrequency, (int)dm.dmBitsPerPel, (int)dm.dmDisplayFixedOutput);
            }

            public int Apply(DisplayMode mode)
            {
                DevMode dm = EmptyDevMode();
                if (!EnumDisplaySettings(null, EnumCurrentSettings, ref dm))
                    throw new InvalidOperationException("恢复提交前无法读取默认显示设备，未继续切换。");
                dm = PrepareRestoreMode(dm, mode);
                RestoreTrace.Write("ChangeDisplaySettings begin: target=" + mode +
                    ", fields=0x" + dm.dmFields.ToString("X") + ", flags=CDS_UPDATEREGISTRY");
                int result = ChangeDisplaySettings(ref dm, (int)CdsUpdateRegistry);
                RestoreTrace.Write("ChangeDisplaySettings returned " + result);
                return result;
            }

            public void Pause(int milliseconds) { Thread.Sleep(milliseconds); }
        }
    }

    internal sealed class SavedState
    {
        public int Width;
        public int Height;
        public int RefreshRate;
        public int BitsPerPixel;
        public int FixedOutput;
        public int MonitorCount;
        public DateTime SavedAt;

        public static string DirectoryPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexaArena"); }
        }

        public static string FilePath
        {
            get { return Path.Combine(DirectoryPath, "display-state.ini"); }
        }

        public static SavedState Capture()
        {
            DisplayMode current = DisplayModeService.GetCurrent();
            SavedState state = new SavedState();
            state.Width = current.Width;
            state.Height = current.Height;
            state.RefreshRate = current.RefreshRate;
            state.BitsPerPixel = current.BitsPerPixel;
            state.FixedOutput = current.FixedOutput;
            state.MonitorCount = Screen.AllScreens.Length;
            state.SavedAt = DateTime.Now;
            return state;
        }

        public void Save()
        {
            Directory.CreateDirectory(DirectoryPath);
            string text = string.Join(Environment.NewLine, new[] {
                "Width=" + Width,
                "Height=" + Height,
                "RefreshRate=" + RefreshRate,
                "BitsPerPixel=" + BitsPerPixel,
                "FixedOutput=" + FixedOutput,
                "MonitorCount=" + MonitorCount,
                "SavedAt=" + SavedAt.ToString("o")
            });
            File.WriteAllText(FilePath, text, Encoding.UTF8);
        }

        public static SavedState Load()
        {
            if (!File.Exists(FilePath)) return null;
            Dictionary<string, string> values = File.ReadAllLines(FilePath)
                .Where(x => x.Contains("="))
                .Select(x => x.Split(new[] { '=' }, 2))
                .ToDictionary(x => x[0], x => x[1], StringComparer.OrdinalIgnoreCase);
            SavedState state = new SavedState();
            state.Width = Parse(values, "Width");
            state.Height = Parse(values, "Height");
            state.RefreshRate = Parse(values, "RefreshRate");
            state.BitsPerPixel = Parse(values, "BitsPerPixel");
            state.FixedOutput = Parse(values, "FixedOutput");
            state.MonitorCount = Parse(values, "MonitorCount");
            DateTime when;
            if (values.ContainsKey("SavedAt") && DateTime.TryParse(values["SavedAt"], out when)) state.SavedAt = when;
            if (state.Width < 320 || state.Height < 200) return null;
            return state;
        }

        private static int Parse(Dictionary<string, string> values, string key)
        {
            int number;
            return values.ContainsKey(key) && int.TryParse(values[key], out number) ? number : 0;
        }

        public static void Delete()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
        }
    }

    internal static class TopologyService
    {
        public static void PrimaryOnly()
        {
            Run("/internal");
        }

        public static void Extend()
        {
            Run("/extend");
        }

        private static void Run(string argument)
        {
            string displaySwitch = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "DisplaySwitch.exe");
            if (!File.Exists(displaySwitch)) throw new FileNotFoundException("找不到 Windows DisplaySwitch.exe。", displaySwitch);
            ProcessStartInfo info = new ProcessStartInfo(displaySwitch, argument);
            info.UseShellExecute = false;
            info.CreateNoWindow = true;
            using (Process process = Process.Start(info))
            {
                if (process != null) process.WaitForExit(6000);
            }
            Thread.Sleep(900);
        }
    }

    internal sealed class PresetPanel : FlowLayoutPanel
    {
        public event EventHandler SelectedModeChanged;
        private readonly List<Button> modeButtons = new List<Button>();
        private DisplayMode selected;

        public DisplayMode SelectedMode { get { return selected; } }

        public PresetPanel()
        {
            FlowDirection = FlowDirection.LeftToRight;
            WrapContents = true;
            AutoScroll = false;
            Height = 150;
            Dock = DockStyle.Top;
            BackColor = Color.Transparent;
            Padding = new Padding(0);
        }

        public void AddMode(int width, int height, string caption, bool select)
        {
            DisplayMode mode = DisplayModeService.FindBest(width, height);
            bool supported = DisplayModeService.IsSupported(width, height);
            Button button = new Button();
            button.Width = 385;
            button.Height = 58;
            button.Margin = new Padding(0, 0, 12, 12);
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderColor = supported ? Color.FromArgb(220, 224, 230) : Color.FromArgb(235, 185, 90);
            button.FlatAppearance.BorderSize = 1;
            button.BackColor = Color.White;
            button.ForeColor = Theme.Text;
            button.Font = new Font("Microsoft YaHei UI", 10.5F, FontStyle.Bold);
            button.Text = string.Format("{0} × {1}\r\n{2}{3}", width, height, caption,
                supported ? "" : " · 驱动未报告");
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.Cursor = Cursors.Hand;
            button.Tag = mode;
            button.Click += delegate { SelectButton(button); };
            modeButtons.Add(button);
            Controls.Add(button);
            if (select) SelectButton(button);
        }

        private void SelectButton(Button button)
        {
            foreach (Button item in modeButtons)
            {
                item.BackColor = Color.White;
                item.ForeColor = Theme.Text;
                item.FlatAppearance.BorderSize = 1;
            }
            button.BackColor = Color.FromArgb(255, 244, 246);
            button.ForeColor = Theme.Accent;
            button.FlatAppearance.BorderColor = Theme.Accent;
            button.FlatAppearance.BorderSize = 2;
            selected = (DisplayMode)button.Tag;
            if (SelectedModeChanged != null) SelectedModeChanged(this, EventArgs.Empty);
        }
    }

    internal sealed class SwitchPage : Panel
    {
        public PresetPanel Presets { get; private set; }
        public Button SwitchButton { get; private set; }
        public Button RestoreButton { get; private set; }
        public Label CurrentLabel { get; private set; }
        public Label OriginalLabel { get; private set; }
        public Label StatusLabel { get; private set; }
        public event Action<int, int> CustomModeAdded;

        private TextBox widthBox;
        private TextBox heightBox;

        public SwitchPage()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = Theme.Background;
            Padding = new Padding(18, 16, 18, 16);

            Panel content = new Panel();
            content.Width = 820;
            content.Height = 650;
            content.Dock = DockStyle.Top;
            content.BackColor = Color.Transparent;
            Controls.Add(content);

            Panel statusCard = new Panel();
            statusCard.Dock = DockStyle.Top;
            statusCard.Height = 64;
            statusCard.BackColor = Color.White;
            content.Controls.Add(statusCard);
            CurrentLabel = Theme.Label("正在读取显示设置…", 11F, FontStyle.Bold, Theme.Text);
            CurrentLabel.Location = new Point(18, 13);
            statusCard.Controls.Add(CurrentLabel);
            OriginalLabel = Theme.Label("原始状态：未保存", 8.5F, FontStyle.Regular, Theme.Green);
            OriginalLabel.Location = new Point(18, 38);
            statusCard.Controls.Add(OriginalLabel);

            Panel presetsCard = new Panel();
            presetsCard.Dock = DockStyle.Top;
            presetsCard.Height = 220;
            presetsCard.Padding = new Padding(18, 14, 18, 10);
            presetsCard.BackColor = Color.White;
            content.Controls.Add(presetsCard);
            presetsCard.BringToFront();
            Label choose = Theme.Label("选择目标分辨率", 10F, FontStyle.Bold, Theme.Text);
            choose.Dock = DockStyle.Top;
            choose.Height = 30;
            presetsCard.Controls.Add(choose);
            Presets = new PresetPanel();
            Presets.Dock = DockStyle.Fill;
            presetsCard.Controls.Add(Presets);

            Panel customCard = new Panel();
            customCard.Dock = DockStyle.Top;
            customCard.Height = 92;
            customCard.Padding = new Padding(18, 12, 18, 8);
            customCard.BackColor = Color.White;
            content.Controls.Add(customCard);
            customCard.BringToFront();
            Label customTitle = Theme.Label("自定义分辨率（仅添加显卡驱动已支持的模式）", 9.5F, FontStyle.Bold, Theme.Text);
            customTitle.Location = new Point(18, 10);
            customCard.Controls.Add(customTitle);
            widthBox = new TextBox();
            widthBox.Location = new Point(18, 45);
            widthBox.Size = new Size(120, 30);
            heightBox = new TextBox();
            heightBox.Location = new Point(172, 45);
            heightBox.Size = new Size(120, 30);
            Label xLabel = Theme.Label("×", 11F, FontStyle.Bold, Theme.Muted);
            xLabel.Location = new Point(146, 47);
            Button add = new Button();
            add.Text = "＋ 添加";
            add.Location = new Point(310, 42);
            add.Size = new Size(120, 34);
            add.FlatStyle = FlatStyle.Flat;
            add.FlatAppearance.BorderSize = 0;
            add.BackColor = Color.FromArgb(239, 242, 247);
            add.ForeColor = Theme.Text;
            add.Cursor = Cursors.Hand;
            add.Click += AddCustomMode;
            customCard.Controls.Add(widthBox);
            customCard.Controls.Add(xLabel);
            customCard.Controls.Add(heightBox);
            customCard.Controls.Add(add);

            Panel actions = new Panel();
            actions.Dock = DockStyle.Top;
            actions.Height = 150;
            actions.Padding = new Padding(0, 12, 0, 0);
            actions.BackColor = Color.Transparent;
            content.Controls.Add(actions);
            actions.BringToFront();
            RestoreButton = Theme.Button("↶  一键恢复（原分辨率 + 扩展显示器）", Theme.Green, 56);
            SwitchButton = Theme.Button("▶  一键切换（分辨率 + 显示器）", Theme.Accent, 64);
            actions.Controls.Add(RestoreButton);
            actions.Controls.Add(SwitchButton);

            Panel footer = new Panel();
            footer.Dock = DockStyle.Top;
            footer.Height = 74;
            footer.BackColor = Color.White;
            footer.Padding = new Padding(16);
            content.Controls.Add(footer);
            footer.BringToFront();
            StatusLabel = Theme.Label("提示：选择分辨率后切换；15 秒内未确认会自动恢复。", 9F, FontStyle.Regular, Theme.Muted);
            StatusLabel.Dock = DockStyle.Top;
            footer.Controls.Add(StatusLabel);
            Label hotkeys = Theme.Label("快捷键：Ctrl + Alt + S 切换 · Ctrl + Alt + H 恢复", 8.5F, FontStyle.Regular, Theme.Muted);
            hotkeys.Dock = DockStyle.Bottom;
            footer.Controls.Add(hotkeys);

            // DockStyle.Top lays controls from the back of the z-order toward the front.
            // Keep the intended visual order stable on high-DPI systems.
            content.Controls.SetChildIndex(statusCard, 4);
            content.Controls.SetChildIndex(presetsCard, 3);
            content.Controls.SetChildIndex(customCard, 2);
            content.Controls.SetChildIndex(actions, 1);
            content.Controls.SetChildIndex(footer, 0);
        }

        private void AddCustomMode(object sender, EventArgs e)
        {
            int width, height;
            if (!int.TryParse(widthBox.Text.Trim(), out width) || !int.TryParse(heightBox.Text.Trim(), out height) ||
                width < 640 || width > 10000 || height < 480 || height > 10000)
            {
                MessageBox.Show("请输入有效的宽和高（宽 640–10000，高 480–10000）。", "自定义分辨率",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!DisplayModeService.IsSupported(width, height))
            {
                MessageBox.Show("显卡驱动当前没有提供这个模式。请先在 NVIDIA / AMD / Intel 控制面板中创建自定义分辨率，再回来添加。",
                    "驱动不支持", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (CustomModeAdded != null) CustomModeAdded(width, height);
            widthBox.Clear();
            heightBox.Clear();
        }
    }

    internal sealed class MonitorPage : Panel
    {
        public CheckBox DisableSecondary { get; private set; }
        private FlowLayoutPanel list;

        public MonitorPage()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = Theme.Background;
            Padding = new Padding(24);
            Label title = Theme.Label("监视器", 16F, FontStyle.Bold, Theme.Text);
            title.Dock = DockStyle.Top;
            title.Height = 42;
            Controls.Add(title);
            Label help = Theme.Label("一键切换时可暂时只保留主显示器；恢复时会重新启用扩展桌面。", 9F, FontStyle.Regular, Theme.Muted);
            help.Dock = DockStyle.Top;
            help.Height = 38;
            Controls.Add(help);
            DisableSecondary = new CheckBox();
            DisableSecondary.Text = "切换时暂时禁用其它监视器";
            DisableSecondary.Checked = true;
            DisableSecondary.AutoSize = true;
            DisableSecondary.Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Bold);
            DisableSecondary.ForeColor = Theme.Text;
            DisableSecondary.Dock = DockStyle.Top;
            DisableSecondary.Height = 42;
            Controls.Add(DisableSecondary);
            Button refresh = new Button();
            refresh.Text = "刷新监视器列表";
            refresh.Dock = DockStyle.Top;
            refresh.Height = 38;
            refresh.FlatStyle = FlatStyle.Flat;
            refresh.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 225);
            refresh.Click += delegate { RefreshList(); };
            Controls.Add(refresh);
            list = new FlowLayoutPanel();
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = true;
            list.Padding = new Padding(0, 14, 0, 0);
            Controls.Add(list);
            list.BringToFront();
            refresh.BringToFront();
            DisableSecondary.BringToFront();
            help.BringToFront();
            title.BringToFront();
            RefreshList();
        }

        public void RefreshList()
        {
            list.Controls.Clear();
            Screen[] screens = Screen.AllScreens;
            for (int i = 0; i < screens.Length; i++)
            {
                Screen screen = screens[i];
                Panel card = new Panel();
                card.Width = 760;
                card.Height = 72;
                card.BackColor = Color.White;
                card.Margin = new Padding(0, 0, 0, 10);
                Label name = Theme.Label((screen.Primary ? "主显示器" : "显示器 " + (i + 1)) + "  " + screen.DeviceName,
                    10F, FontStyle.Bold, screen.Primary ? Theme.Green : Theme.Text);
                name.Location = new Point(16, 12);
                card.Controls.Add(name);
                Label detail = Theme.Label(string.Format("{0} × {1} · 坐标 {2}, {3}", screen.Bounds.Width,
                    screen.Bounds.Height, screen.Bounds.X, screen.Bounds.Y), 8.5F, FontStyle.Regular, Theme.Muted);
                detail.Location = new Point(16, 40);
                card.Controls.Add(detail);
                list.Controls.Add(card);
            }
        }
    }

    internal sealed class ClientPage : Panel
    {
        private readonly Func<DisplayMode> selectedMode;
        private ListBox files;
        private Label status;

        public ClientPage(Func<DisplayMode> selectedModeProvider)
        {
            selectedMode = selectedModeProvider;
            Dock = DockStyle.Fill;
            BackColor = Theme.Background;
            Padding = new Padding(24);
            Label title = Theme.Label("Valorant 客户端配置", 16F, FontStyle.Bold, Theme.Text);
            title.Dock = DockStyle.Top;
            title.Height = 42;
            Controls.Add(title);
            Label description = Theme.Label("可选功能：备份并修复 GameUserSettings.ini，使游戏采用当前目标分辨率和全屏窗口模式。",
                9F, FontStyle.Regular, Theme.Muted);
            description.Dock = DockStyle.Top;
            description.Height = 42;
            Controls.Add(description);
            files = new ListBox();
            files.Dock = DockStyle.Top;
            files.Height = 280;
            files.Font = Theme.SmallFont;
            files.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(files);
            Button repair = Theme.Button("备份并修复选中的配置", Theme.Accent, 48);
            repair.Click += RepairSelected;
            Controls.Add(repair);
            Button scan = new Button();
            scan.Text = "重新扫描配置";
            scan.Dock = DockStyle.Top;
            scan.Height = 40;
            scan.FlatStyle = FlatStyle.Flat;
            scan.FlatAppearance.BorderColor = Color.FromArgb(210, 215, 225);
            scan.Click += delegate { Scan(); };
            Controls.Add(scan);
            status = Theme.Label("", 8.5F, FontStyle.Regular, Theme.Muted);
            status.Dock = DockStyle.Top;
            status.Height = 55;
            Controls.Add(status);
            status.BringToFront();
            scan.BringToFront();
            repair.BringToFront();
            files.BringToFront();
            description.BringToFront();
            title.BringToFront();
            Scan();
        }

        private static IEnumerable<string> ConfigFiles()
        {
            string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "VALORANT", "Saved", "Config");
            if (!Directory.Exists(root)) return Enumerable.Empty<string>();
            try { return Directory.GetFiles(root, "GameUserSettings.ini", SearchOption.AllDirectories); }
            catch { return Enumerable.Empty<string>(); }
        }

        private void Scan()
        {
            files.Items.Clear();
            foreach (string file in ConfigFiles()) files.Items.Add(file);
            if (files.Items.Count > 0) files.SelectedIndex = 0;
            status.Text = files.Items.Count == 0 ? "未找到配置。请先启动一次 Valorant 客户端。" :
                "找到 " + files.Items.Count + " 个配置文件。修复前会在同目录生成带时间戳的备份。";
        }

        private void RepairSelected(object sender, EventArgs e)
        {
            if (files.SelectedItem == null)
            {
                MessageBox.Show("请先选择一个配置文件。", "客户端配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            DisplayMode mode = selectedMode();
            if (mode == null)
            {
                MessageBox.Show("请先在“分辨率切换”页选择目标分辨率。", "客户端配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string file = Convert.ToString(files.SelectedItem);
            DialogResult answer = MessageBox.Show(string.Format("将把配置备份后修改为 {0} × {1}、全屏窗口模式。\r\n\r\n{2}",
                mode.Width, mode.Height, file), "确认修复", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (answer != DialogResult.OK) return;
            try
            {
                string backup = ValorantConfig.Repair(file, mode.Width, mode.Height);
                status.Text = "修复完成。备份：" + backup;
                MessageBox.Show("配置已修复，并已创建备份。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                MessageBox.Show("修复失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal static class ValorantConfig
    {
        private static readonly Dictionary<string, Func<int, int, string>> Desired =
            new Dictionary<string, Func<int, int, string>>(StringComparer.OrdinalIgnoreCase)
            {
                { "bUseVSync", (w, h) => "False" },
                { "bShouldLetterbox", (w, h) => "False" },
                { "bLastConfirmedShouldLetterbox", (w, h) => "False" },
                { "bUseDesiredScreenHeight", (w, h) => "False" },
                { "ResolutionSizeX", (w, h) => w.ToString() },
                { "ResolutionSizeY", (w, h) => h.ToString() },
                { "LastUserConfirmedResolutionSizeX", (w, h) => w.ToString() },
                { "LastUserConfirmedResolutionSizeY", (w, h) => h.ToString() },
                { "DesiredScreenWidth", (w, h) => w.ToString() },
                { "DesiredScreenHeight", (w, h) => h.ToString() },
                { "LastUserConfirmedDesiredScreenWidth", (w, h) => w.ToString() },
                { "LastUserConfirmedDesiredScreenHeight", (w, h) => h.ToString() },
                { "FullscreenMode", (w, h) => "1" },
                { "LastConfirmedFullscreenMode", (w, h) => "1" },
                { "PreferredFullscreenMode", (w, h) => "1" }
            };
        internal const string ShooterSettingsSection="[/Script/ShooterGame.ShooterGameUserSettings]";
        internal const string EngineSettingsSection="[/Script/Engine.GameUserSettings]";

        public static string Repair(string path, int width, int height)
        {
            return Repair(path, width, height, false);
        }

        public static string Repair(string path, int width, int height, bool makeReadOnly)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("配置文件不存在。", path);
            FileAttributes attributes = File.GetAttributes(path);
            if ((attributes & FileAttributes.ReadOnly) != 0)
                File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
            string backup = path + ".nexaarena." + DateTime.Now.ToString("yyyyMMdd-HHmmss-fff") + ".bak";
            File.Copy(path, backup, false);
            WriteDesiredValues(path,width,height);
            if (makeReadOnly) File.SetAttributes(path, File.GetAttributes(path) | FileAttributes.ReadOnly);
            return backup;
        }

        public static void ApplyRuntimeBridge(string path,int width,int height)
        {
            if(!File.Exists(path)) throw new FileNotFoundException("配置文件不存在。",path);
            FileAttributes attributes=File.GetAttributes(path);
            if((attributes&FileAttributes.ReadOnly)!=0)
                File.SetAttributes(path,attributes&~FileAttributes.ReadOnly);
            WriteDesiredValues(path,width,height);
        }

        private static void WriteDesiredValues(string path,int width,int height)
        {
            string[] lines = File.ReadAllLines(path);
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            List<string> output = new List<string>();
            string section=string.Empty;
            bool shooterFound=false;
            bool engineFound=false;
            for (int i = 0; i < lines.Length; i++)
            {
                string trimmed=lines[i].Trim();
                if(trimmed.StartsWith("[")&&trimmed.EndsWith("]"))
                {
                    AppendMissingForSection(output,seen,section,width,height);
                    section=trimmed;
                    if(string.Equals(section,ShooterSettingsSection,StringComparison.OrdinalIgnoreCase)) shooterFound=true;
                    if(string.Equals(section,EngineSettingsSection,StringComparison.OrdinalIgnoreCase)) engineFound=true;
                    output.Add(lines[i]);
                    continue;
                }
                Match match = Regex.Match(lines[i], @"^\s*([A-Za-z0-9_]+)\s*=.*$");
                if (!match.Success) { output.Add(lines[i]); continue; }
                string key = match.Groups[1].Value;
                Func<int, int, string> value;
                if (!Desired.TryGetValue(key, out value)) { output.Add(lines[i]); continue; }
                string expectedSection=ExpectedSection(key);
                // 删除落在错误 INI 节中的旧版残留键；游戏只读取正确节。
                if(!string.Equals(section,expectedSection,StringComparison.OrdinalIgnoreCase)) continue;
                if (seen.Contains(key)) continue;
                output.Add(key + "=" + value(width, height));
                seen.Add(key);
            }
            AppendMissingForSection(output,seen,section,width,height);
            if(!shooterFound)
            {
                output.Add("");
                output.Add(ShooterSettingsSection);
                AppendMissingForSection(output,seen,ShooterSettingsSection,width,height);
            }
            if(!engineFound)
            {
                output.Add("");
                output.Add(EngineSettingsSection);
                AppendMissingForSection(output,seen,EngineSettingsSection,width,height);
            }
            File.WriteAllLines(path, output.ToArray(), new UTF8Encoding(false));
        }

        private static string ExpectedSection(string key)
        {
            return string.Equals(key,"bUseDesiredScreenHeight",StringComparison.OrdinalIgnoreCase)
                ? EngineSettingsSection : ShooterSettingsSection;
        }

        private static void AppendMissingForSection(List<string> output,HashSet<string> seen,
            string section,int width,int height)
        {
            if(!string.Equals(section,ShooterSettingsSection,StringComparison.OrdinalIgnoreCase)&&
               !string.Equals(section,EngineSettingsSection,StringComparison.OrdinalIgnoreCase)) return;
            foreach(KeyValuePair<string,Func<int,int,string>> item in Desired)
            {
                if(!string.Equals(ExpectedSection(item.Key),section,StringComparison.OrdinalIgnoreCase)||seen.Contains(item.Key)) continue;
                output.Add(item.Key+"="+item.Value(width,height));
                seen.Add(item.Key);
            }
        }

        public static bool IsBorderlessFillAt(string path,int width,int height,out string details)
        {
            details=Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)))+": 无法读取";
            try
            {
                Dictionary<string,string> values=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                string section=string.Empty;
                foreach(string line in File.ReadAllLines(path))
                {
                    string trimmed=line.Trim();
                    if(trimmed.StartsWith("[")&&trimmed.EndsWith("]")) { section=trimmed; continue; }
                    if(!string.Equals(section,ShooterSettingsSection,StringComparison.OrdinalIgnoreCase)) continue;
                    Match match=Regex.Match(line,@"^\s*([A-Za-z0-9_]+)\s*=\s*(.*?)\s*$");
                    if(match.Success) values[match.Groups[1].Value]=match.Groups[2].Value;
                }
                string mode=values.ContainsKey("FullscreenMode")?values["FullscreenMode"]:"缺失";
                string x=values.ContainsKey("ResolutionSizeX")?values["ResolutionSizeX"]:"缺失";
                string y=values.ContainsKey("ResolutionSizeY")?values["ResolutionSizeY"]:"缺失";
                string letterbox=values.ContainsKey("bShouldLetterbox")?values["bShouldLetterbox"]:"缺失";
                details=Path.GetFileName(Path.GetDirectoryName(Path.GetDirectoryName(path)))+
                    ": Mode="+mode+", Resolution="+x+"×"+y+", Letterbox="+letterbox;
                return mode=="1" && x==width.ToString() && y==height.ToString() &&
                    string.Equals(letterbox,"False",StringComparison.OrdinalIgnoreCase);
            }
            catch(Exception ex)
            {
                details=path+": "+ex.Message;
                return false;
            }
        }
    }

    internal sealed class TutorialPage : Panel
    {
        public TutorialPage()
        {
            Dock = DockStyle.Fill;
            AutoScroll = true;
            BackColor = Theme.Background;
            Padding = new Padding(24);
            Label title = Theme.Label("使用教程", 16F, FontStyle.Bold, Theme.Text);
            title.Dock = DockStyle.Top;
            title.Height = 45;
            Controls.Add(title);
            string[] steps = {
                "1. 先退出 Valorant，选择一个显卡驱动支持的目标分辨率。",
                "2. 点击“一键切换”。如果启用了监视器选项，Windows 会暂时只保留主显示器。",
                "3. 画面正常时点击“保留更改”；15 秒内不确认会自动恢复。",
                "4. 进入游戏后将显示模式设为“全屏窗口”，纵横比方式设为“填充”。",
                "5. 结束游戏后点击“一键恢复”，或按 Ctrl + Alt + H。",
                "6. 如果目标分辨率显示“驱动未报告”，请先在显卡控制面板中创建该模式。"
            };
            FlowLayoutPanel flow = new FlowLayoutPanel();
            flow.Dock = DockStyle.Fill;
            flow.FlowDirection = FlowDirection.TopDown;
            flow.WrapContents = false;
            flow.AutoScroll = true;
            Controls.Add(flow);
            flow.BringToFront();
            title.BringToFront();
            foreach (string step in steps)
            {
                Label card = Theme.Label(step, 10F, FontStyle.Regular, Theme.Text);
                card.BackColor = Color.White;
                card.Padding = new Padding(18, 16, 18, 16);
                card.Margin = new Padding(0, 0, 0, 10);
                card.Width = 770;
                card.Height = 68;
                flow.Controls.Add(card);
            }
            Label warning = Theme.Label("安全说明：本工具不注入游戏、不修改进程、不绕过 Vanguard；仅调用 Windows 显示 API，并可选修改本地配置文件。",
                9F, FontStyle.Bold, Theme.AccentDark);
            warning.BackColor = Color.FromArgb(255, 239, 242);
            warning.Padding = new Padding(18, 16, 18, 16);
            warning.Margin = new Padding(0, 8, 0, 0);
            warning.Width = 770;
            warning.Height = 76;
            flow.Controls.Add(warning);
        }
    }

    internal sealed class MainForm : Form
    {
        private const int WmHotkey = 0x0312;
        private const int HotkeySwitch = 0x5101;
        private const int HotkeyRestore = 0x5102;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        private readonly SwitchPage switchPage;
        private readonly MonitorPage monitorPage;
        private readonly System.Windows.Forms.Timer rollbackTimer;
        private SavedState pendingState;
        private int secondsLeft;
        private bool closingAfterRestore;

        public MainForm()
        {
            Text = "Nexa Arena · 无畏契约 4:3 切换助手";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(900, 760);
            MinimumSize = new Size(820, 680);
            BackColor = Theme.Background;
            Font = Theme.DefaultFont;
            Icon = SystemIcons.Application;

            TableLayoutPanel rootLayout = new TableLayoutPanel();
            rootLayout.Dock = DockStyle.Fill;
            rootLayout.ColumnCount = 1;
            rootLayout.RowCount = 3;
            rootLayout.Margin = new Padding(0);
            rootLayout.Padding = new Padding(0);
            rootLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 88F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            rootLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(rootLayout);

            Panel header = new Panel();
            header.Height = 88;
            header.Dock = DockStyle.Fill;
            header.Margin = new Padding(0);
            header.BackColor = Color.White;
            rootLayout.Controls.Add(header, 0, 0);
            Label logo = Theme.Label("Nexa Arena", 22F, FontStyle.Bold, Theme.Accent);
            logo.AutoSize = false;
            logo.Dock = DockStyle.Top;
            logo.Height = 50;
            logo.TextAlign = ContentAlignment.BottomCenter;
            header.Controls.Add(logo);
            Label subtitle = Theme.Label("无畏契约 4:3 一键切换助手 · 免费开源复刻", 9F, FontStyle.Regular, Theme.Muted);
            subtitle.AutoSize = false;
            subtitle.Dock = DockStyle.Bottom;
            subtitle.Height = 30;
            subtitle.TextAlign = ContentAlignment.TopCenter;
            header.Controls.Add(subtitle);

            switchPage = new SwitchPage();
            monitorPage = new MonitorPage();
            ClientPage clientPage = new ClientPage(delegate { return switchPage.Presets.SelectedMode; });
            TutorialPage tutorialPage = new TutorialPage();

            Panel navigation = new Panel();
            navigation.Dock = DockStyle.Fill;
            navigation.Height = 50;
            navigation.Margin = new Padding(0);
            navigation.BackColor = Color.White;
            rootLayout.Controls.Add(navigation, 0, 1);

            Panel pageHost = new Panel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.Margin = new Padding(0);
            pageHost.BackColor = Theme.Background;
            rootLayout.Controls.Add(pageHost, 0, 2);

            Control[] pages = { switchPage, clientPage, monitorPage, tutorialPage };
            string[] names = { "分辨率切换", "客户端", "监视器", "教程" };
            Button[] navButtons = new Button[names.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Dock = DockStyle.Fill;
                pages[i].Visible = i == 0;
                pageHost.Controls.Add(pages[i]);

                int pageIndex = i;
                Button navButton = new Button();
                navButton.Text = names[i];
                navButton.Width = 150;
                navButton.Height = 50;
                navButton.Location = new Point(i * 150, 0);
                navButton.FlatStyle = FlatStyle.Flat;
                navButton.FlatAppearance.BorderSize = 0;
                navButton.Font = new Font("Microsoft YaHei UI", 10F, i == 0 ? FontStyle.Bold : FontStyle.Regular);
                navButton.ForeColor = i == 0 ? Theme.Accent : Theme.Text;
                navButton.BackColor = i == 0 ? Color.FromArgb(255, 244, 246) : Color.White;
                navButton.Cursor = Cursors.Hand;
                navButton.Click += delegate
                {
                    for (int p = 0; p < pages.Length; p++)
                    {
                        pages[p].Visible = p == pageIndex;
                        navButtons[p].ForeColor = p == pageIndex ? Theme.Accent : Theme.Text;
                        navButtons[p].BackColor = p == pageIndex ? Color.FromArgb(255, 244, 246) : Color.White;
                        navButtons[p].Font = new Font("Microsoft YaHei UI", 10F,
                            p == pageIndex ? FontStyle.Bold : FontStyle.Regular);
                    }
                    pages[pageIndex].BringToFront();
                };
                navButtons[i] = navButton;
                navigation.Controls.Add(navButton);
            }

            switchPage.Presets.AddMode(1280, 960, "最常用", true);
            switchPage.Presets.AddMode(1280, 1024, "5:4", false);
            switchPage.Presets.AddMode(1440, 1080, "清晰", false);
            switchPage.Presets.AddMode(1568, 1080, "职业同款", false);
            foreach (Point custom in CustomPresetStore.Load())
                switchPage.Presets.AddMode(custom.X, custom.Y, "自定义", false);
            switchPage.CustomModeAdded += delegate(int w, int h)
            {
                switchPage.Presets.AddMode(w, h, "自定义", true);
                CustomPresetStore.Add(w, h);
            };
            switchPage.SwitchButton.Click += delegate { BeginSwitch(); };
            switchPage.RestoreButton.Click += delegate { Restore(); };
            switchPage.Presets.SelectedModeChanged += delegate { UpdateStatus("已选择 " + switchPage.Presets.SelectedMode); };

            rollbackTimer = new System.Windows.Forms.Timer();
            rollbackTimer.Interval = 1000;
            rollbackTimer.Tick += RollbackTick;
            Shown += delegate { RefreshDisplayLabels(); WarnIfStateExists(); };
            FormClosing += OnFormClosing;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            RegisterHotKey(Handle, HotkeySwitch, ModControl | ModAlt, (uint)Keys.S);
            RegisterHotKey(Handle, HotkeyRestore, ModControl | ModAlt, (uint)Keys.H);
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            UnregisterHotKey(Handle, HotkeySwitch);
            UnregisterHotKey(Handle, HotkeyRestore);
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                int id = m.WParam.ToInt32();
                if (id == HotkeySwitch) BeginSwitch();
                if (id == HotkeyRestore) Restore();
                return;
            }
            base.WndProc(ref m);
        }

        private void WarnIfStateExists()
        {
            SavedState state = SavedState.Load();
            if (state == null) return;
            switchPage.OriginalLabel.Text = string.Format("已保存原始：{0} × {1} @ {2}Hz", state.Width, state.Height, state.RefreshRate);
            UpdateStatus("检测到上次保存的原始状态，可点击“一键恢复”。");
        }

        private void BeginSwitch()
        {
            if (pendingState != null)
            {
                KeepChanges();
                return;
            }
            DisplayMode target = switchPage.Presets.SelectedMode;
            if (target == null)
            {
                MessageBox.Show("请先选择目标分辨率。", "Nexa Arena", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                SavedState existing = SavedState.Load();
                pendingState = existing ?? SavedState.Capture();
                pendingState.Save();
                switchPage.OriginalLabel.Text = string.Format("原始 {0} × {1} @ {2}Hz", pendingState.Width,
                    pendingState.Height, pendingState.RefreshRate);
                if (monitorPage.DisableSecondary.Checked && Screen.AllScreens.Length > 1)
                {
                    UpdateStatus("正在切换为主显示器…");
                    Application.DoEvents();
                    TopologyService.PrimaryOnly();
                }
                target = DisplayModeService.FindBest(target.Width, target.Height);
                UpdateStatus("正在切换分辨率…");
                Application.DoEvents();
                DisplayModeService.Set(target);
                secondsLeft = 15;
                switchPage.SwitchButton.Text = "✓  保留更改（15 秒）";
                switchPage.SwitchButton.BackColor = Theme.Green;
                UpdateStatus("如果画面正常，请点击“保留更改”；否则会自动恢复。");
                rollbackTimer.Start();
                RefreshDisplayLabels();
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                TryRestorePending();
                MessageBox.Show("切换失败：" + ex.Message, "Nexa Arena", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RefreshDisplayLabels();
            }
        }

        private void KeepChanges()
        {
            rollbackTimer.Stop();
            pendingState = null;
            switchPage.SwitchButton.Text = "▶  一键切换（分辨率 + 显示器）";
            switchPage.SwitchButton.BackColor = Theme.Accent;
            UpdateStatus("已保留更改。结束游戏后点击“一键恢复”。");
        }

        private void RollbackTick(object sender, EventArgs e)
        {
            secondsLeft--;
            if (secondsLeft <= 0)
            {
                rollbackTimer.Stop();
                Restore();
                return;
            }
            switchPage.SwitchButton.Text = "✓  保留更改（" + secondsLeft + " 秒）";
        }

        private void Restore()
        {
            rollbackTimer.Stop();
            SavedState state = pendingState ?? SavedState.Load();
            if (state == null)
            {
                UpdateStatus("没有可恢复的原始状态。");
                RefreshDisplayLabels();
                return;
            }
            try
            {
                UpdateStatus("正在恢复显示器和分辨率…");
                Application.DoEvents();
                if (state.MonitorCount > 1) TopologyService.Extend();
                DisplayModeService.Set(new DisplayMode(state.Width, state.Height, state.RefreshRate,
                    state.BitsPerPixel <= 0 ? 32 : state.BitsPerPixel));
                SavedState.Delete();
                pendingState = null;
                switchPage.OriginalLabel.Text = "原始状态：未保存";
                switchPage.SwitchButton.Text = "▶  一键切换（分辨率 + 显示器）";
                switchPage.SwitchButton.BackColor = Theme.Accent;
                UpdateStatus("显示设置已恢复。");
                monitorPage.RefreshList();
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                MessageBox.Show("自动恢复失败：" + ex.Message + "\r\n请使用 Windows 设置手动恢复显示器。",
                    "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshDisplayLabels();
        }

        private void TryRestorePending()
        {
            if (pendingState == null) return;
            try
            {
                if (pendingState.MonitorCount > 1) TopologyService.Extend();
                DisplayModeService.Set(new DisplayMode(pendingState.Width, pendingState.Height,
                    pendingState.RefreshRate, pendingState.BitsPerPixel <= 0 ? 32 : pendingState.BitsPerPixel));
                SavedState.Delete();
            }
            catch (Exception restoreError) { ErrorLog.Write(restoreError); }
            pendingState = null;
        }

        private void RefreshDisplayLabels()
        {
            try
            {
                DisplayMode current = DisplayModeService.GetCurrent();
                switchPage.CurrentLabel.Text = "● 当前  " + current;
            }
            catch (Exception ex) { switchPage.CurrentLabel.Text = "无法读取当前显示模式：" + ex.Message; }
        }

        private void UpdateStatus(string text)
        {
            switchPage.StatusLabel.Text = text;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (closingAfterRestore || pendingState == null) return;
            e.Cancel = true;
            Restore();
            closingAfterRestore = true;
            Close();
        }
    }

    internal static class CustomPresetStore
    {
        private static string FilePath
        {
            get { return Path.Combine(SavedState.DirectoryPath, "custom-modes.txt"); }
        }

        public static IEnumerable<Point> Load()
        {
            if (!File.Exists(FilePath)) yield break;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                Match match = Regex.Match(line, @"^(\d+)x(\d+)$", RegexOptions.IgnoreCase);
                int width, height;
                if (match.Success && int.TryParse(match.Groups[1].Value, out width) &&
                    int.TryParse(match.Groups[2].Value, out height)) yield return new Point(width, height);
            }
        }

        public static void Add(int width, int height)
        {
            Directory.CreateDirectory(SavedState.DirectoryPath);
            string value = width + "x" + height;
            List<string> items = File.Exists(FilePath) ? File.ReadAllLines(FilePath).ToList() : new List<string>();
            if (!items.Any(x => string.Equals(x, value, StringComparison.OrdinalIgnoreCase)))
            {
                items.Add(value);
                File.WriteAllLines(FilePath, items.ToArray(), Encoding.UTF8);
            }
        }
    }

    internal static class ErrorLog
    {
        public static void Write(Exception exception)
        {
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "error.log");
                File.AppendAllText(path, DateTime.Now.ToString("o") + Environment.NewLine + exception +
                    Environment.NewLine + Environment.NewLine, Encoding.UTF8);
            }
            catch { }
        }
    }

    internal static class SelfTest
    {
        public static int Run()
        {
            StringBuilder report = new StringBuilder();
            int code = 0;
            try
            {
                report.AppendLine("Nexa Arena self-test");
                report.AppendLine("Time=" + DateTime.Now.ToString("o"));
                DisplayMode current = DisplayModeService.GetCurrent();
                List<DisplayMode> modes = DisplayModeService.GetSupportedModes();
                report.AppendLine("Current=" + current);
                report.AppendLine("SupportedModeCount=" + modes.Count);
                report.AppendLine("Screens=" + Screen.AllScreens.Length);
                List<ValorantConfigEntry> configs = ValorantLocator.FindConfigFiles();
                report.AppendLine("ValorantConfigCount=" + configs.Count);
                report.AppendLine("ValorantGameRunning=" + ValorantLocator.IsGameRunning());
                List<MonitorDeviceInfo> monitorDevices = MonitorDeviceService.GetDevices(true);
                report.AppendLine("PnPMonitorDeviceCount=" + monitorDevices.Count);
                report.AppendLine("PnPMonitorStates=" + string.Join(",", monitorDevices.Select(x =>
                    x.InstanceId + ":" + (x.Started ? "Started" : "Stopped")).ToArray()));
                if (configs.Count > 0)
                    report.AppendLine("NewestConfig=" + configs[0].Path);
                report.AppendLine("DisplaySwitch=" + File.Exists(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.System), "DisplaySwitch.exe")));
                if (modes.Count == 0) throw new InvalidOperationException("No display modes were enumerated.");
                string testConfig=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"self-test-config.ini");
                string testBackup=null;
                try
                {
                    File.WriteAllText(testConfig,
                        ValorantConfig.ShooterSettingsSection+"\r\nResolutionSizeX=1920\r\nResolutionSizeX=1280\r\nResolutionSizeY=1080\r\n"+
                        "LastUserConfirmedResolutionSizeX=1280\r\nLastUserConfirmedResolutionSizeY=720\r\n"+
                        "bShouldLetterbox=True\r\n[ScalabilityGroups]\r\nsg.TextureQuality=2\r\n"+
                        ValorantConfig.EngineSettingsSection+"\r\nbUseDesiredScreenHeight=True\r\n"+
                        "[Internationalization.AssetGroupCultures]\r\nMature=zh-CN\r\nFullscreenMode=0\r\n",Encoding.UTF8);
                    testBackup=ValorantConfig.Repair(testConfig,2560,1440,true);
                    string normalized=File.ReadAllText(testConfig);
                    int shooterIndex=normalized.IndexOf(ValorantConfig.ShooterSettingsSection,StringComparison.OrdinalIgnoreCase);
                    int modeIndex=normalized.IndexOf("FullscreenMode=1",StringComparison.OrdinalIgnoreCase);
                    int nextSection=normalized.IndexOf("[ScalabilityGroups]",StringComparison.OrdinalIgnoreCase);
                    if(Regex.Matches(normalized,@"(?m)^FullscreenMode=").Count!=1 ||
                        modeIndex<shooterIndex || modeIndex>nextSection ||
                        !normalized.Contains("bShouldLetterbox=False") ||
                        !normalized.Contains("LastUserConfirmedResolutionSizeX=2560") ||
                        (File.GetAttributes(testConfig)&FileAttributes.ReadOnly)==0)
                        throw new InvalidOperationException("Config normalization self-test failed.");
                    report.AppendLine("ConfigNormalization=PASS");
                }
                finally
                {
                    try { if(File.Exists(testConfig)) { File.SetAttributes(testConfig,FileAttributes.Normal); File.Delete(testConfig); } } catch { }
                    try { if(!string.IsNullOrEmpty(testBackup)&&File.Exists(testBackup)) File.Delete(testBackup); } catch { }
                }
                report.AppendLine("Result=PASS (no settings changed)");
            }
            catch (Exception ex)
            {
                code = 1;
                report.AppendLine("Result=FAIL");
                report.AppendLine(ex.ToString());
            }
            try { File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "self-test.log"), report.ToString(), Encoding.UTF8); }
            catch { code = 2; }
            return code;
        }
    }
}
