using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NexaArena
{
    internal sealed class GameSectionPage : UserControl
    {
        private readonly Button firstButton;
        private readonly Button secondButton;
        private readonly Control firstPage;
        private readonly Control secondPage;

        public GameSectionPage(string firstName, Control first, string secondName, Control second)
        {
            Dock = DockStyle.Fill;
            BackColor = ModernTheme.Background;
            firstPage = first;
            secondPage = second;
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,52F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,100F));
            Controls.Add(root);
            Panel tabs = new Panel();
            tabs.Dock = DockStyle.Fill;
            tabs.Margin = new Padding(0);
            tabs.Padding = new Padding(22, 8, 22, 6);
            tabs.BackColor = ModernTheme.Background;
            root.Controls.Add(tabs,0,0);
            firstButton = SectionButton(firstName);
            secondButton = SectionButton(secondName);
            firstButton.Location = new Point(22, 8);
            secondButton.Location = new Point(176, 8);
            tabs.Controls.Add(firstButton);
            tabs.Controls.Add(secondButton);
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Margin = new Padding(0);
            host.BackColor = ModernTheme.Background;
            root.Controls.Add(host,0,1);
            first.Dock = DockStyle.Fill;
            second.Dock = DockStyle.Fill;
            host.Controls.Add(first);
            host.Controls.Add(second);
            firstButton.Click += delegate { ShowPage(true); };
            secondButton.Click += delegate { ShowPage(false); };
            ShowPage(true);
        }

        private static Button SectionButton(string text)
        {
            Button button = ModernTheme.Button(text, ModernTheme.Background, ModernTheme.Text);
            button.Width = 146;
            button.Height = 38;
            button.Margin = new Padding(0);
            return button;
        }

        private void ShowPage(bool first)
        {
            firstPage.Visible = first;
            secondPage.Visible = !first;
            (first ? firstPage : secondPage).BringToFront();
            firstButton.BackColor = first ? ModernTheme.AccentSoft : ModernTheme.Background;
            firstButton.ForeColor = first ? ModernTheme.Accent : ModernTheme.Text;
            secondButton.BackColor = first ? ModernTheme.Background : ModernTheme.AccentSoft;
            secondButton.ForeColor = first ? ModernTheme.Text : ModernTheme.Accent;
        }
    }


    internal sealed class CrosshairPreset
    {
        public string Name;
        public string Style;
        public string Code;
        public string ShareCode;
        public Color Color;
        public int Length;
        public int Gap;
        public int Thickness;
        public bool Dot;
        public bool TShape;
        public bool Valorant;
        public string AssetKey;
    }

    internal sealed class CrosshairCard : ModernCard
    {
        public CrosshairCard(CrosshairPreset preset, bool valorant)
        {
            Height = 108;
            Dock = DockStyle.Fill;
            Margin = new Padding(0, 0, 10, 14);
            Padding = new Padding(16);
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.RowCount = 2;
            layout.ColumnCount=2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,194F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,50F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent,50F));
            layout.Controls.Add(ModernTheme.Label(preset.Name, 10.5F, FontStyle.Bold, ModernTheme.Text), 0, 0);
            Label parameters = ModernTheme.Label(preset.Style, 8.5F, FontStyle.Regular, ModernTheme.Muted);
            parameters.AutoEllipsis = true;
            layout.Controls.Add(parameters, 0, 1);
            Button copy = MakeCopyButton(valorant ? "复制导入代码" : "复制准星导入码", preset.Code);
            copy.Dock = DockStyle.Fill;
            copy.Margin=new Padding(14,12,0,12);
            layout.Controls.Add(copy,1,0);layout.SetRowSpan(copy,2);
            Controls.Add(layout);
        }

        private static Button MakeCopyButton(string label, string value)
        {
            Button button = ModernTheme.Button(label, ModernTheme.AccentSoft, ModernTheme.Accent);
            button.Margin = new Padding(4,4,4,0);
            Timer feedbackTimer = new Timer {Interval=1200};
            feedbackTimer.Tick += delegate {feedbackTimer.Stop();if(!button.IsDisposed)button.Text=label;};
            button.Disposed += delegate {feedbackTimer.Dispose();};
            button.Click += delegate
            {
                try
                {
                    Clipboard.SetText(value ?? string.Empty);
                    button.Text = "已复制";
                    feedbackTimer.Stop();feedbackTimer.Start();
                }
                catch(Exception ex) { MessageBox.Show("复制失败："+ex.Message); }
            };
            return button;
        }
    }

    internal sealed class CrosshairLibraryPage : UserControl
    {
        public CrosshairLibraryPage(bool valorant)
        {
            Dock = DockStyle.Fill;
            BackColor = ModernTheme.Background;
            ResponsiveStack stack = new ResponsiveStack();
            Controls.Add(stack);

            ModernCard intro = new ModernCard();
            intro.Height = 144;
            TableLayoutPanel introLayout = new TableLayoutPanel();
            introLayout.Dock = DockStyle.Fill;
            introLayout.RowCount = 3;
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            introLayout.Controls.Add(ModernTheme.Label(valorant ? "VALORANT 准心库" : "Counter-Strike 2 准心库",
                12F, FontStyle.Bold, ModernTheme.Text), 0, 0);
            introLayout.Controls.Add(ModernTheme.Label(valorant ?
                "复制后进入 设置 → 准心 → 导入。不同分辨率下请直接在靶场检查实际像素效果。" :
                "复制准星导入码，在游戏设置的准星/瞄准镜页面导入。",
                8.5F, FontStyle.Regular, ModernTheme.Muted), 0, 1);
            LinkLabel more = new LinkLabel();
            more.Text = valorant ? "查看更多 VALORANT 准心 →" : "查看更多 CS2 准心 →";
            more.Dock = DockStyle.Fill;
            more.Font = ModernTheme.Font(9F,FontStyle.Bold);
            more.LinkColor = ModernTheme.Accent;
            more.ActiveLinkColor = ModernTheme.Blue;
            more.VisitedLinkColor = ModernTheme.Accent;
            more.TextAlign = ContentAlignment.MiddleLeft;
            string moreUrl = valorant ? "https://www.valorantcrosshairdb.com/crosshairs/professional/" : "https://crosshair.club/builder";
            more.LinkClicked += delegate
            {
                try
                {
                    ProcessStartInfo info = new ProcessStartInfo(moreUrl);
                    info.UseShellExecute = true;
                    Process.Start(info);
                }
                catch(Exception ex) { MessageBox.Show("无法打开网页："+ex.Message); }
            };
            introLayout.Controls.Add(more,0,2);
            intro.Controls.Add(introLayout);
            stack.Controls.Add(intro);

            List<CrosshairPreset> presets = valorant ? ValorantPresets() : Cs2Presets();
            for (int i = 0; i < presets.Count; i++)
            {
                stack.Controls.Add(new CrosshairCard(presets[i], valorant));
            }
        }

        internal static List<CrosshairPreset> ValorantPresets()
        {
            return new List<CrosshairPreset> {
                P("TenZ · 紧凑青色", "网站当前参数：长度 2 · 粗细 2 · 间距 1", "0;s;1;P;c;5;o;0;f;0;0l;2;0v;2;0g;1;0o;1;0a;1;0f;0;1b;0", Color.Cyan, 2, 1, 2, false, false),
                P("Demon1 · 白色小十字", "网站当前参数：长度 3 · 粗细 1 · 间距 2", "0;s;1;P;o;1;f;0;0t;1;0l;3;0o;2;0a;1;0f;0;1b;0", Color.White, 3, 2, 1, false, false),
                P("aspas · 青色中心点", "主流突破手中心点 · 大小 3", "0;P;c;5;o;1;d;1;z;3;f;0;0b;0;1b;0", Color.Cyan, 0, 1, 3, true, false),
                P("yay · 零间距方框", "长度 4 · 间距 0 · 完全静态", "0;P;h;0;f;0;0l;4;0o;0;0a;1;0f;0;1b;0", Color.White, 4, 0, 1, false, false)
            };
        }

        internal static List<CrosshairPreset> Cs2Presets()
        {
            return new List<CrosshairPreset> {
                Cs("donk", "1280 × 960", "CS44bpzNrNkisKXePKKWy7fZkfWjeNxiHAbQRO5oQjMWQj"),
                Cs("NiKo", "1280 × 960", "CSr8ZYF3mqQhy3TvUiz5MrppUPLTZR8S3YPvqKGRbJhO9C"),
                Cs("m0NESY", "1280 × 960", "CStLmGWwjzBrK84RGQeTkArGmNrWYe2cO7tBmvFAnXBEEQ"),
                Cs("ZywOo", "1280 × 960", "CS5VD6DDR67rots4FLBGBbyRsPrGBRcnUQpjsfZJrsiFTY"),
                Cs("XANTARES", "1024 × 768", "CSbnbYtHZNuyBSi9NqDTdEcScry4Pp9Ch8uiACYcZe3mhD"),
                Cs("s1mple", "1280 × 960", "CSTFwEeJqTGeVVoCvo5qkmF8pS8vX9KzqFpnNsMOB2xOym"),
                Cs("magixx", "1920 × 1080", "CSztCTDvFfLcUvLQzkjtnm3AWDRvskhERbYWPzLv625Vab"),
                Cs("broky", "1280 × 960", "CSWnevbWAB6jTFP3PUL6VXOFEtGSKm8MzXmBe5ZUJtjhPn")
            };
        }
        private static CrosshairPreset P(string name, string style, string code, Color color, int length, int gap, int thickness, bool dot, bool tShape)
        {
            return new CrosshairPreset { Name = name, Style = style, Code = code, Color = color,
                Length = length, Gap = gap, Thickness = thickness, Dot = dot, TShape = tShape, Valorant = true,
                ShareCode = code,
                AssetKey = name.StartsWith("TenZ") ? "val_tenz" : name.StartsWith("Demon1") ? "val_demon1" :
                    name.StartsWith("aspas") ? "val_aspas" : "val_yay" };
        }

        private static CrosshairPreset Cs(string name, string sourceResolution, string shareCode)
        {
            if(!Cs2ShareCode.IsCurrent(shareCode))
                throw new InvalidOperationException("CS2 准星分享码无效：" + name);
            return new CrosshairPreset {Name=name,Style=sourceResolution,Code=shareCode,
                ShareCode=shareCode,Valorant=false,Color=Color.White};
        }
    }

    internal sealed class FilterPreset
    {
        public string Name;
        public string Description;
        public Color Swatch;
        public double Red;
        public double Green;
        public double Blue;
        public double Gamma;
        public double Contrast;
    }

    internal static class DisplayFilterService
    {
        [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("gdi32.dll")] private static extern bool GetDeviceGammaRamp(IntPtr hdc, IntPtr ramp);
        [DllImport("gdi32.dll")] private static extern bool SetDeviceGammaRamp(IntPtr hdc, IntPtr ramp);

        private static ushort[] original;

        public static void Apply(FilterPreset preset)
        {
            IntPtr hdc = GetDC(IntPtr.Zero);
            if (hdc == IntPtr.Zero) throw new InvalidOperationException("无法访问显示器颜色控制。");
            IntPtr memory = Marshal.AllocHGlobal(768 * 2);
            try
            {
                if (original == null)
                {
                    if (!GetDeviceGammaRamp(hdc, memory)) throw new InvalidOperationException("显卡驱动不支持 Gamma Ramp。");
                    original = new ushort[768];
                    short[] signed = new short[768];
                    Marshal.Copy(memory, signed, 0, 768);
                    for (int i = 0; i < 768; i++) original[i] = unchecked((ushort)signed[i]);
                }
                ushort[] ramp = BuildRamp(preset);
                short[] values = ramp.Select(x => unchecked((short)x)).ToArray();
                Marshal.Copy(values, 0, memory, values.Length);
                if (!SetDeviceGammaRamp(hdc, memory)) throw new InvalidOperationException("应用滤镜失败，可能被 HDR 或显卡驱动阻止。");
            }
            finally { Marshal.FreeHGlobal(memory); ReleaseDC(IntPtr.Zero, hdc); }
        }

        public static void Restore()
        {
            if (original == null) return;
            IntPtr hdc = GetDC(IntPtr.Zero);
            IntPtr memory = Marshal.AllocHGlobal(768 * 2);
            try
            {
                short[] values = original.Select(x => unchecked((short)x)).ToArray();
                Marshal.Copy(values, 0, memory, values.Length);
                SetDeviceGammaRamp(hdc, memory);
                original = null;
            }
            finally { Marshal.FreeHGlobal(memory); ReleaseDC(IntPtr.Zero, hdc); }
        }

        private static ushort[] BuildRamp(FilterPreset preset)
        {
            ushort[] result = new ushort[768];
            double[] channels = { preset.Red, preset.Green, preset.Blue };
            for (int channel = 0; channel < 3; channel++)
            for (int i = 0; i < 256; i++)
            {
                double x = i / 255.0;
                x = Math.Pow(x, 1.0 / Math.Max(0.2, preset.Gamma));
                x = ((x - 0.5) * preset.Contrast + 0.5) * channels[channel];
                x = Math.Max(0, Math.Min(1, x));
                result[channel * 256 + i] = (ushort)Math.Round(x * 65535);
            }
            return result;
        }
    }

    internal sealed class GameFilterPage : UserControl
    {
        public GameFilterPage()
        {
            Dock = DockStyle.Fill;
            BackColor = ModernTheme.Background;
            ResponsiveStack stack = new ResponsiveStack();
            Controls.Add(stack);
            ModernCard intro = new ModernCard();
            intro.Height = 140;
            Label introText = ModernTheme.Label("CS2 轻量画面滤镜\r\n使用显示器 Gamma Ramp 调整冷暖与对比度，不注入游戏。无边框窗口模式效果最稳定；HDR 可能阻止滤镜。",
                9.5F, FontStyle.Bold, ModernTheme.Text);
            introText.AutoEllipsis = false;
            intro.Controls.Add(introText);
            stack.Controls.Add(intro);
            List<FilterPreset> filters = Presets();
            for (int i = 0; i < filters.Count; i += 2)
            {
                TableLayoutPanel row = new TableLayoutPanel();
                row.Height = 172;
                row.ColumnCount = 2;
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
                row.Controls.Add(FilterCard(filters[i]), 0, 0);
                if (i + 1 < filters.Count) row.Controls.Add(FilterCard(filters[i + 1]), 1, 0);
                stack.Controls.Add(row);
            }
            ModernCard resetCard = new ModernCard();
            resetCard.Height = 72;
            Button reset = ModernTheme.Button("恢复原始颜色", Color.White, ModernTheme.Text);
            reset.Dock = DockStyle.Fill;
            reset.Click += delegate { DisplayFilterService.Restore(); };
            resetCard.Controls.Add(reset);
            stack.Controls.Add(resetCard);
        }

        internal static List<FilterPreset> Presets()
        {
            return new List<FilterPreset> {
                F("竞技清晰", "轻微暖色＋高对比，突出敌人与场景边缘", Color.FromArgb(255,190,100), 1.04,1.01,0.92,0.95,1.10),
                F("暖色竞技 · 推荐", "偏暖但不过黄，兼顾人物辨识与长时间观感", Color.FromArgb(255,165,85), 1.06,1.00,0.88,0.96,1.12),
                F("职业均衡", "保守 Gamma＋轻微鲜艳，适合多数地图", Color.FromArgb(235,195,135), 1.01,1.00,0.97,0.98,1.08),
                F("职业鲜艳", "中等鲜艳与层次，颜色更集中但不过曝", Color.FromArgb(255,185,95), 1.03,1.01,0.96,0.96,1.14),
                F("敌人突出", "暖色轮廓＋较强层次，优先提高人物辨识", Color.FromArgb(255,135,70), 1.05,1.00,0.91,0.93,1.15),
                F("高对比", "加强层次与边缘，亮部更亮", Color.FromArgb(90,140,255), 1.00,1.00,1.00,0.92,1.18)
            };
        }

        private static Control FilterCard(FilterPreset filter)
        {
            ModernCard card = new ModernCard();
            card.Dock = DockStyle.Fill;
            card.Margin = new Padding(0, 0, 10, 12);
            card.Padding = new Padding(14);
            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 60F));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            Panel swatch = new Panel();
            swatch.Dock = DockStyle.Fill;
            swatch.Margin = new Padding(0, 4, 12, 4);
            swatch.BackColor = filter.Swatch;
            layout.Controls.Add(swatch, 0, 0);
            TableLayoutPanel text = new TableLayoutPanel();
            text.Dock = DockStyle.Fill;
            text.RowCount = 3;
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 28F));
            text.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            text.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            text.Controls.Add(ModernTheme.Label(filter.Name, 10F, FontStyle.Bold, ModernTheme.Text), 0, 0);
            text.Controls.Add(ModernTheme.Label(filter.Description, 8F, FontStyle.Regular, ModernTheme.Muted), 0, 1);
            Button apply = ModernTheme.Button("应用滤镜", ModernTheme.Accent, Color.White);
            apply.Dock = DockStyle.Fill;
            apply.Margin = new Padding(0, 4, 0, 0);
            apply.Click += delegate
            {
                try { DisplayFilterService.Apply(filter); }
                catch (Exception ex) { MessageBox.Show(ex.Message, "滤镜", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            };
            text.Controls.Add(apply, 0, 2);
            layout.Controls.Add(text, 1, 0);
            card.Controls.Add(layout);
            return card;
        }

        private static FilterPreset F(string name, string desc, Color swatch, double r, double g, double b, double gamma, double contrast)
        {
            return new FilterPreset { Name=name, Description=desc, Swatch=swatch, Red=r, Green=g, Blue=b, Gamma=gamma, Contrast=contrast };
        }
    }

#if LEGACY_BOOST
    internal sealed class BoostProcessItem
    {
        public int Id;
        public string Name;
        public long MemoryMb;
        public string Title;
        public override string ToString() { return string.Format("{0,-24} {1,6} MB   {2}", Name, MemoryMb, Title); }
    }

    internal sealed class GameBoostPage : UserControl
    {
        private readonly CheckedListBox processList;
        private readonly Label status;

        internal object WebList()
        {
            RefreshProcesses();
            return processList.Items.Cast<BoostProcessItem>().Select(x=>new {id=x.Id,name=x.Name,memoryMb=x.MemoryMb,title=x.Title}).ToArray();
        }
        internal string WebOptimizeGames(){OptimizeGames();return status.Text;}
        internal string WebCloseSelected(int[] ids)
        {
            HashSet<int> selected=new HashSet<int>(ids??new int[0]);
            RefreshProcesses();
            for(int i=0;i<processList.Items.Count;i++)
                processList.SetItemChecked(i,selected.Contains(((BoostProcessItem)processList.Items[i]).Id));
            CloseSelected();return status.Text;
        }

        public GameBoostPage()
        {
            Dock = DockStyle.Fill;
            BackColor = ModernTheme.Background;
            ResponsiveStack stack = new ResponsiveStack();
            Controls.Add(stack);
            ModernCard priority = new ModernCard();
            priority.Height = 118;
            TableLayoutPanel priorityLayout = new TableLayoutPanel();
            priorityLayout.Dock = DockStyle.Fill;
            priorityLayout.ColumnCount = 2;
            priorityLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
            priorityLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
            priorityLayout.Controls.Add(ModernTheme.Label("游戏优先级\r\n将正在运行的 VALORANT / CS2 设置为高优先级，不修改系统永久设置。",
                9.5F, FontStyle.Bold, ModernTheme.Text), 0, 0);
            Button optimize = ModernTheme.Button("优化游戏进程", ModernTheme.Accent, Color.White);
            optimize.Dock = DockStyle.Fill;
            optimize.Click += delegate { OptimizeGames(); };
            priorityLayout.Controls.Add(optimize, 1, 0);
            priority.Controls.Add(priorityLayout);
            stack.Controls.Add(priority);

            ModernCard background = new ModernCard();
            background.Height = 410;
            TableLayoutPanel bg = new TableLayoutPanel();
            bg.Dock = DockStyle.Fill;
            bg.RowCount = 4;
            bg.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            bg.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            bg.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            bg.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            bg.Controls.Add(ModernTheme.Label("可关闭的高占用窗口程序（仅列出当前用户的可见窗口）", 10F, FontStyle.Bold, ModernTheme.Text), 0, 0);
            processList = new CheckedListBox();
            processList.Dock = DockStyle.Fill;
            processList.CheckOnClick = true;
            processList.Font = new Font("Consolas", 9F);
            bg.Controls.Add(processList, 0, 1);
            TableLayoutPanel buttons = new TableLayoutPanel();
            buttons.Dock = DockStyle.Fill;
            buttons.ColumnCount = 2;
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            Button refresh = ModernTheme.Button("刷新列表", Color.White, ModernTheme.Text);
            refresh.Dock = DockStyle.Fill;
            refresh.Click += delegate { RefreshProcesses(); };
            Button close = ModernTheme.Button("关闭选中程序", ModernTheme.Navy, Color.White);
            close.Dock = DockStyle.Fill;
            close.Click += delegate { CloseSelected(); };
            buttons.Controls.Add(refresh, 0, 0);
            buttons.Controls.Add(close, 1, 0);
            bg.Controls.Add(buttons, 0, 2);
            status = ModernTheme.Label("不会强制结束系统进程；请保存浏览器和聊天软件中的工作。", 8F, FontStyle.Regular, ModernTheme.Muted);
            bg.Controls.Add(status, 0, 3);
            background.Controls.Add(bg);
            stack.Controls.Add(background);
            RefreshProcesses();
        }

        private void OptimizeGames()
        {
            int changed = 0;
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string name = process.ProcessName;
                    if (name.Equals("cs2", StringComparison.OrdinalIgnoreCase) ||
                        name.Equals("VALORANT", StringComparison.OrdinalIgnoreCase) ||
                        name.IndexOf("VALORANT-Win64-Shipping", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        process.PriorityClass = ProcessPriorityClass.High;
                        changed++;
                    }
                }
                catch { }
                finally { process.Dispose(); }
            }
            status.Text = changed == 0 ? "没有检测到正在运行的 VALORANT 或 CS2。" : "已优化 " + changed + " 个游戏进程的优先级。";
        }

        private void RefreshProcesses()
        {
            processList.Items.Clear();
            int currentSession = Process.GetCurrentProcess().SessionId;
            string[] excluded = { "explorer", "dwm", "cs2", "valorant", "nexaarena", "shellhost",
                "startmenuexperiencehost", "lockapp", "taskmgr" };
            List<BoostProcessItem> items = new List<BoostProcessItem>();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.Id == Process.GetCurrentProcess().Id || process.SessionId != currentSession ||
                        process.MainWindowHandle == IntPtr.Zero || excluded.Contains(process.ProcessName.ToLowerInvariant())) continue;
                    long memory = process.WorkingSet64 / 1024 / 1024;
                    if (memory < 40) continue;
                    items.Add(new BoostProcessItem { Id=process.Id, Name=process.ProcessName, MemoryMb=memory, Title=process.MainWindowTitle });
                }
                catch { }
                finally { process.Dispose(); }
            }
            foreach (BoostProcessItem item in items.OrderByDescending(x => x.MemoryMb)) processList.Items.Add(item);
            status.Text = "找到 " + items.Count + " 个可选择关闭的窗口程序。";
        }

        private void CloseSelected()
        {
            List<BoostProcessItem> selected = processList.CheckedItems.Cast<BoostProcessItem>().ToList();
            if (selected.Count == 0) { status.Text = "请先勾选要关闭的程序。"; return; }
            if (MessageBox.Show("将请求以下程序正常关闭：\r\n\r\n" + string.Join("、", selected.Select(x => x.Name)) +
                "\r\n\r\n请确认其中没有未保存的工作。未响应的程序会再次询问是否强制结束。", "关闭后台程序", MessageBoxButtons.OKCancel,
                MessageBoxIcon.Warning) != DialogResult.OK) return;
            int gracefulRequests = 0;
            foreach (BoostProcessItem item in selected)
            {
                try { using (Process process = Process.GetProcessById(item.Id)) if (process.CloseMainWindow()) gracefulRequests++; }
                catch { }
            }
            System.Threading.Thread.Sleep(1800);
            List<BoostProcessItem> remaining = new List<BoostProcessItem>();
            foreach (BoostProcessItem item in selected)
            {
                try { using (Process process = Process.GetProcessById(item.Id)) if (!process.HasExited) remaining.Add(item); }
                catch { }
            }
            int forced = 0;
            List<string> failed = new List<string>();
            if (remaining.Count > 0 && MessageBox.Show("以下程序没有响应正常关闭请求：\r\n\r\n"+
                string.Join("、",remaining.Select(x=>x.Name))+"\r\n\r\n是否强制结束？未保存的数据可能丢失。",
                "强制结束程序",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2)==DialogResult.Yes)
            {
                foreach(BoostProcessItem item in remaining)
                {
                    string forceError;
                    if (ForceKillTree(item,out forceError)) forced++;
                    else failed.Add(item.Name+"（"+forceError+"）");
                }
            }
            int gracefulClosed = selected.Count - remaining.Count;
            RefreshProcesses();
            status.Text = "正常关闭 " + gracefulClosed + " 个，强制结束 " + forced + " 个"+
                (failed.Count == 0 ? "。" : "；失败："+string.Join("、",failed.Distinct().ToArray()));
        }

        private static bool ForceKillTree(BoostProcessItem item, out string error)
        {
            error=string.Empty;
            List<string> messages=new List<string>();
            int session=Process.GetCurrentProcess().SessionId;
            for(int round=0;round<2;round++)
            {
                List<int> ids=new List<int>();
                if(round==0) ids.Add(item.Id);
                else
                {
                    foreach(Process process in Process.GetProcessesByName(item.Name))
                    {
                        try { if(process.SessionId==session) ids.Add(process.Id); }
                        catch { }
                        finally { process.Dispose(); }
                    }
                }
                if(ids.Count==0) return true;
                foreach(int id in ids.Distinct())
                {
                    string output;
                    if(!RunTaskKill(id,out output)) messages.Add("PID "+id+": "+output);
                }
                System.Threading.Thread.Sleep(900);
            }
            bool anyAlive=false;
            foreach(Process process in Process.GetProcessesByName(item.Name))
            {
                try { if(process.SessionId==session) anyAlive=true; }
                catch { }
                finally { process.Dispose(); }
            }
            error=messages.Count==0 ? "程序仍在运行或被自动重新启动" : string.Join(" | ",messages.ToArray());
            return !anyAlive;
        }

        private static bool RunTaskKill(int processId,out string output)
        {
            try
            {
                string exe=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"taskkill.exe");
                ProcessStartInfo info=new ProcessStartInfo(exe,"/PID "+processId+" /T /F");
                info.UseShellExecute=false;
                info.CreateNoWindow=true;
                info.RedirectStandardOutput=true;
                info.RedirectStandardError=true;
                using(Process process=Process.Start(info))
                {
                    string stdout=process.StandardOutput.ReadToEnd();
                    string stderr=process.StandardError.ReadToEnd();
                    process.WaitForExit(8000);
                    output=(stdout+" "+stderr).Trim();
                    if(!process.HasExited) return false;
                    if(process.ExitCode==0) return true;
                    try { Process.GetProcessById(processId).Dispose(); return false; }
                    catch { return true; }
                }
            }
            catch(Exception ex) { output=ex.Message; return false; }
        }
    }
#endif
}
