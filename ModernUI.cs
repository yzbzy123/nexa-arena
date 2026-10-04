using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NexaArena
{
    internal static class ModernTheme
    {
        public static readonly Color Accent = Color.FromArgb(184, 65, 45);
        public static readonly Color AccentSoft = Color.FromArgb(252, 238, 233);
        public static readonly Color Navy = Color.FromArgb(20, 25, 32);
        public static readonly Color NavyLight = Color.FromArgb(36, 43, 52);
        public static readonly Color Background = Color.FromArgb(245, 246, 247);
        public static readonly Color Card = Color.White;
        public static readonly Color Border = Color.FromArgb(220, 225, 230);
        public static readonly Color Text = Color.FromArgb(31, 38, 47);
        public static readonly Color Muted = Color.FromArgb(97, 109, 121);
        public static readonly Color Green = Color.FromArgb(23, 125, 94);
        public static readonly Color Blue = Color.FromArgb(57, 113, 146);

        public static Font Font(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }

        public static Label Label(string text, float size, FontStyle style, Color color)
        {
            Label label = new Label();
            label.Text = text;
            label.AutoSize = false;
            label.Font = Font(size, style);
            label.ForeColor = color;
            label.BackColor = Color.Transparent;
            label.TextAlign = ContentAlignment.MiddleLeft;
            label.Margin = new Padding(0);
            label.Dock = DockStyle.Fill;
            label.AutoEllipsis = true;
            return label;
        }

        public static Button Button(string text, Color backColor, Color foreColor)
        {
            Button button = new ArenaButton();
            button.Text = text;
            button.FlatStyle = FlatStyle.Flat;
            button.FlatAppearance.BorderSize = backColor == Color.White ? 1 : 0;
            button.FlatAppearance.BorderColor = Border;
            button.BackColor = backColor;
            button.ForeColor = foreColor;
            button.Font = Font(10F, FontStyle.Bold);
            button.Cursor = Cursors.Hand;
            button.Margin = new Padding(6);
            return button;
        }
    }

    internal class ModernCard : Panel
    {
        public ModernCard()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = ModernTheme.Card;
            Margin = new Padding(0, 0, 0, 16);
            Padding = new Padding(20);
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.Clear(Parent==null?ModernTheme.Background:Parent.BackColor);
            e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var path=ArenaDrawing.Round(new RectangleF(0,0,Math.Max(1,Width-1),Math.Max(1,Height-1)),12*DeviceDpi/96F))
            using(var brush=new SolidBrush(BackColor))e.Graphics.FillPath(brush,path);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using(var outline=ArenaDrawing.Round(new RectangleF(0.5F,0.5F,Math.Max(1,Width-1),Math.Max(1,Height-1)),12*DeviceDpi/96F))
            using (Pen pen = new Pen(BackColor==ModernTheme.Navy?ModernTheme.NavyLight:ModernTheme.Border)) e.Graphics.DrawPath(pen,outline);
        }
    }

    internal sealed class ResponsiveStack : FlowLayoutPanel
    {
        private bool arranging;

        public ResponsiveStack()
        {
            DoubleBuffered = true;
            Dock = DockStyle.Fill;
            FlowDirection = FlowDirection.TopDown;
            WrapContents = false;
            AutoScroll = true;
            BackColor = ModernTheme.Background;
            Padding = new Padding(24, 20, 24, 24);
        }

        protected override void OnLayout(LayoutEventArgs levent)
        {
            if (!arranging)
            {
                arranging = true;
                int scrollbar = VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0;
                int width = Math.Max(200, ClientSize.Width - Padding.Horizontal - scrollbar - 4);
                foreach (Control control in Controls)
                    control.Width = width;
                arranging = false;
            }
            base.OnLayout(levent);
        }

        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            PerformLayout();
        }
    }

    internal sealed class ModernNavButton : ArenaButton
    {
        private bool selected;
        public string IconName = "aim";

        public bool Selected
        {
            get { return selected; }
            set { selected = value; ApplyStyle(); }
        }

        public ModernNavButton(string text)
        {
            Text = text;
            Height = 44;
            Width = 206;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            TextAlign = ContentAlignment.MiddleLeft;
            Padding = new Padding(12, 0, 0, 0);
            Margin = new Padding(0, 3, 0, 3);
            Font = ModernTheme.Font(9F, FontStyle.Regular);
            Cursor = Cursors.Hand;
            ApplyStyle();
        }

        private void ApplyStyle()
        {
            BackColor = selected ? ModernTheme.NavyLight : ModernTheme.Navy;
            ForeColor = selected ? Color.FromArgb(255, 197, 174) : Color.FromArgb(174, 186, 197);
            FlatAppearance.MouseOverBackColor = ModernTheme.NavyLight;
        }

        protected override void OnPaint(PaintEventArgs pevent)
        {
            pevent.Graphics.Clear(ModernTheme.Navy);
            pevent.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            if (selected || ClientRectangle.Contains(PointToClient(Cursor.Position)))
            {
                using(var path=ArenaDrawing.Round(new RectangleF(0,0,Width-1,Height-1),8*DeviceScale))
                using (Brush brush = new SolidBrush(ModernTheme.NavyLight))pevent.Graphics.FillPath(brush,path);
            }
            int icon=(int)(18*DeviceScale);
            ArenaDrawing.Icon(pevent.Graphics,IconName,new Rectangle((int)(14*DeviceScale),(Height-icon)/2,icon,icon),ForeColor);
            TextRenderer.DrawText(pevent.Graphics,Text,Font,new Rectangle((int)(46*DeviceScale),0,Width-(int)(52*DeviceScale),Height),ForeColor,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(pevent.Graphics,Rectangle.Inflate(ClientRectangle,-4,-4));
        }
    }

    internal sealed class ModernPresetPanel : TableLayoutPanel
    {
        public event EventHandler SelectedModeChanged;
        public event EventHandler HeightChangedByItems;
        private readonly List<Button> buttons = new List<Button>();
        private DisplayMode selected;

        public DisplayMode SelectedMode { get { return selected; } }

        internal void SelectMode(int width,int height)
        {
            Button match=buttons.FirstOrDefault(b=>((DisplayMode)b.Tag).Width==width&&((DisplayMode)b.Tag).Height==height);
            if(match==null)throw new InvalidOperationException("目标分辨率不在当前预设中，请先添加模式。");
            SelectButton(match);
        }

        public ModernPresetPanel()
        {
            Dock = DockStyle.Fill;
            ColumnCount = 2;
            RowCount = 1;
            BackColor = Color.White;
            Margin = new Padding(0);
            Padding = new Padding(0);
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
            ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        }

        public void AddMode(int width, int height, string caption, bool select)
        {
            Button existing = buttons.FirstOrDefault(b=>((DisplayMode)b.Tag).Width==width && ((DisplayMode)b.Tag).Height==height);
            if(existing!=null) { if(select)SelectButton(existing); return; }
            DisplayMode mode = DisplayModeService.FindBest(width, height);
            bool supported = DisplayModeService.IsSupported(width, height);
            int index = buttons.Count;
            int row = index / 2;
            if (row >= RowCount)
            {
                RowCount = row + 1;
                RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
            }
            else if (RowStyles.Count == 0)
            {
                RowStyles.Add(new RowStyle(SizeType.Absolute, 86F));
            }

            Button button = new ArenaPresetButton { ModeWidth=width,ModeHeight=height,Caption=supported?caption:caption+" · 驱动未支持",Text=width+" × "+height+" "+caption,BackColor=Color.White,ForeColor=ModernTheme.Text,Cursor=Cursors.Hand };
            button.Dock = DockStyle.Fill;
            button.Margin = new Padding(index % 2 == 0 ? 0 : 7, 4, index % 2 == 0 ? 7 : 0, 8);
            button.FlatAppearance.BorderColor = ModernTheme.Border;
            button.TextAlign = ContentAlignment.MiddleLeft;
            button.Padding = new Padding(18, 0, 0, 0);
            button.Tag = mode;
            button.Click += delegate { SelectButton(button); };
            buttons.Add(button);
            Controls.Add(button, index % 2, row);
            if (select) SelectButton(button);
            if (HeightChangedByItems != null) HeightChangedByItems(this, EventArgs.Empty);
        }

        public int DesiredHeight { get { return Math.Max(86, RowCount * 86); } }

        private void SelectButton(Button selectedButton)
        {
            foreach (Button button in buttons)
            {
                button.BackColor = Color.White;
                button.ForeColor = ModernTheme.Text;
                button.FlatAppearance.BorderColor = ModernTheme.Border;
                button.FlatAppearance.BorderSize = 1;
            }
            selectedButton.BackColor = ModernTheme.AccentSoft;
            selectedButton.ForeColor = ModernTheme.Accent;
            selectedButton.FlatAppearance.BorderColor = ModernTheme.Accent;
            selectedButton.FlatAppearance.BorderSize = 2;
            selected = (DisplayMode)selectedButton.Tag;
            if (SelectedModeChanged != null) SelectedModeChanged(this, EventArgs.Empty);
        }
    }

    internal sealed class ModernSwitchPage : UserControl
    {
        public ModernPresetPanel Presets { get; private set; }
        public Button SwitchButton { get; private set; }
        public Button CalibrationButton { get; private set; }
        public Button RestoreButton { get; private set; }
        public CheckBox AutoRestoreOnGameExit { get; private set; }
        public Label CurrentLabel { get; private set; }
        public Label OriginalLabel { get; private set; }
        public Label StatusLabel { get; private set; }
        public Label ShortcutLabel { get; private set; }
        public event Action<int, int> CustomModeAdded;
        public DisplayMode IntermediateMode
        {
            get
            {
                DisplayModeChoice choice = intermediateBox.SelectedItem as DisplayModeChoice;
                return choice == null ? DisplayModeService.FindBest(1280,800) : choice.Mode;
            }
        }

        private readonly ModernCard presetCard;
        private readonly TextBox widthBox;
        private readonly TextBox heightBox;
        private readonly ComboBox intermediateBox;

        public ModernSwitchPage()
        {
            Dock = DockStyle.Fill;
            BackColor = ModernTheme.Background;
            ResponsiveStack stack = new ResponsiveStack();
            var pageLayout=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=1,ColumnCount=2,Margin=new Padding(0),Padding=new Padding(0)};
            pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100F));
            pageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,61F));
            pageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,39F));
            Controls.Add(pageLayout);pageLayout.Controls.Add(stack,0,0);
            stack.Padding=new Padding(24,12,14,12);
            var workflow=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=3,ColumnCount=1,Margin=new Padding(0),Padding=new Padding(0,12,24,12)};
            workflow.RowStyles.Add(new RowStyle(SizeType.Absolute,100F));
            workflow.RowStyles.Add(new RowStyle(SizeType.Absolute,184F));
            workflow.RowStyles.Add(new RowStyle(SizeType.Percent,100F));
            workflow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F));
            pageLayout.Controls.Add(workflow,1,0);

            ModernCard statusCard = new ModernCard();
            statusCard.Height = 100;
            statusCard.Padding=new Padding(18,10,18,10);
            statusCard.BackColor=ModernTheme.AccentSoft;
            TableLayoutPanel statusLayout = new TableLayoutPanel();
            statusLayout.Dock = DockStyle.Fill;
            statusLayout.ColumnCount = 1;
            statusLayout.RowCount = 1;
            statusLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            CurrentLabel = ModernTheme.Label("正在读取显示设置…", 17F, FontStyle.Bold, ModernTheme.Accent);
            OriginalLabel = ModernTheme.Label("尚未保存原始显示状态", 9F, FontStyle.Regular, ModernTheme.Green);
            TableLayoutPanel statusText = new TableLayoutPanel();
            statusText.Dock = DockStyle.Fill;
            statusText.RowCount=2;statusText.ColumnCount=1;
            statusText.RowStyles.Add(new RowStyle(SizeType.Absolute,36F));
            statusText.RowStyles.Add(new RowStyle(SizeType.Percent,100F));
            statusText.Controls.Add(CurrentLabel,0,0);
            statusText.Controls.Add(OriginalLabel,0,1);
            statusLayout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            statusLayout.Controls.Add(statusText, 0, 0);
            statusCard.Controls.Add(statusLayout);
            statusCard.Dock=DockStyle.Fill;
            statusCard.Margin=new Padding(0,0,0,12);
            workflow.Controls.Add(statusCard,0,0);

            presetCard = new ModernCard();
            presetCard.Height = 224;
            TableLayoutPanel presetLayout = new TableLayoutPanel();
            presetLayout.Dock = DockStyle.Fill;
            presetLayout.RowCount = 2;
            presetLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44F));
            presetLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label presetTitle = ModernTheme.Label("选择目标分辨率", 11F, FontStyle.Bold, ModernTheme.Text);
            Label presetHint = ModernTheme.Label("选择后不会立即生效", 8.5F, FontStyle.Regular, ModernTheme.Muted);
            presetHint.TextAlign = ContentAlignment.MiddleRight;
            TableLayoutPanel titleLine = new TableLayoutPanel();
            titleLine.Dock = DockStyle.Fill;
            titleLine.ColumnCount = 2;
            titleLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55F));
            titleLine.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45F));
            titleLine.Controls.Add(presetTitle, 0, 0);
            titleLine.Controls.Add(presetHint, 1, 0);
            Presets = new ModernPresetPanel();
            Presets.HeightChangedByItems += delegate { presetCard.Height = 84 + Presets.DesiredHeight; };
            presetLayout.Controls.Add(titleLine, 0, 0);
            presetLayout.Controls.Add(Presets, 0, 1);
            presetCard.Controls.Add(presetLayout);
            stack.Controls.Add(presetCard);

            ModernCard customCard = new ModernCard();
            customCard.Height = 64;
            customCard.Padding=new Padding(20,12,20,12);
            TableLayoutPanel customLayout = new TableLayoutPanel();
            customLayout.Dock = DockStyle.Fill;
            customLayout.RowCount = 3;
            customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
            customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            customLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 0F));
            Button customTitle = ModernTheme.Button("＋ 自定义分辨率与中转设置", Color.White, ModernTheme.Text);
            customTitle.Dock=DockStyle.Fill;customTitle.Margin=new Padding(0);customTitle.TextAlign=ContentAlignment.MiddleLeft;customTitle.FlatAppearance.BorderSize=0;
            customLayout.Controls.Add(customTitle, 0, 0);
            TableLayoutPanel customInputs = new TableLayoutPanel();
            customInputs.Dock = DockStyle.Fill;
            customInputs.ColumnCount=4;customInputs.RowCount=1;customInputs.Margin=new Padding(0);
            customInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50F));
            customInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,32F));
            customInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50F));
            customInputs.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,142F));
            widthBox = Input("宽度");
            heightBox = Input("高度");
            Label multiply = ModernTheme.Label("×", 11F, FontStyle.Bold, ModernTheme.Muted);
            multiply.AutoEllipsis = false;
            multiply.Width = 32;
            multiply.Height = 34;
            multiply.TextAlign = ContentAlignment.MiddleCenter;
            widthBox.Dock=heightBox.Dock=DockStyle.Fill;
            widthBox.Margin=heightBox.Margin=new Padding(0,6,0,0);
            Button addButton = ModernTheme.Button("添加模式", ModernTheme.Navy, Color.White);
            addButton.Width = 128;
            addButton.Height = 36;
            addButton.Margin = new Padding(12, 0, 0, 0);
            addButton.Dock=DockStyle.Fill;
            addButton.Click += AddCustomMode;
            customInputs.Controls.Add(widthBox,0,0);
            customInputs.Controls.Add(multiply,1,0);
            customInputs.Controls.Add(heightBox,2,0);
            customInputs.Controls.Add(addButton,3,0);
            Label customHint = ModernTheme.Label("仅显示驱动已支持的分辨率", 8.5F, FontStyle.Regular, ModernTheme.Muted);
            customHint.Width = 260;
            customHint.Height = 36;
            customHint.Margin = new Padding(16, 0, 0, 0);
            customHint.Dispose();
            customLayout.Controls.Add(customInputs, 0, 1);
            FlowLayoutPanel intermediateRow = new FlowLayoutPanel();
            intermediateRow.Dock = DockStyle.Fill;
            intermediateRow.FlowDirection = FlowDirection.LeftToRight;
            intermediateRow.WrapContents = false;
            Label intermediateLabel = ModernTheme.Label("中转分辨率", 9F, FontStyle.Bold, ModernTheme.Text);
            intermediateLabel.Width = 120;
            intermediateLabel.Height = 36;
            intermediateLabel.Dock=DockStyle.None;
            intermediateBox = new ComboBox();
            intermediateBox.DropDownStyle = ComboBoxStyle.DropDownList;
            intermediateBox.Width = 180;
            intermediateBox.Font = ModernTheme.Font(9F, FontStyle.Regular);
            intermediateBox.Margin = new Padding(0,3,12,0);
            List<DisplayMode> bridgeModes = DisplayModeService.GetSupportedModes()
                .Where(m => m.Width >= 1024 && m.Width <= 1920 && Math.Abs((double)m.Width / m.Height - 1.6) < 0.025)
                .GroupBy(m => m.Width + "x" + m.Height).Select(g => g.First()).OrderBy(m => m.Width).ToList();
            foreach (DisplayMode mode in bridgeModes) intermediateBox.Items.Add(new DisplayModeChoice(mode));
            int bridgeIndex = -1;
            for (int i = 0; i < intermediateBox.Items.Count; i++)
            {
                DisplayModeChoice choice = (DisplayModeChoice)intermediateBox.Items[i];
                if (choice.Mode.Width == 1280 && choice.Mode.Height == 800)
                { bridgeIndex = i; break; }
            }
            if (bridgeIndex >= 0) intermediateBox.SelectedIndex = bridgeIndex;
            intermediateBox.SelectedIndexChanged += delegate
            {
                DisplayModeChoice selected = intermediateBox.SelectedItem as DisplayModeChoice;
                if (selected != null) SaveIntermediateChoice(selected.Key);
            };
            intermediateBox.Enabled = false;
            Label intermediateHint = ModernTheme.Label("自动选择可用中转模式", 8F, FontStyle.Regular, ModernTheme.Muted);
            intermediateHint.Width = 260;
            intermediateHint.Height = 36;
            intermediateHint.Dock=DockStyle.None;
            intermediateRow.Controls.Add(intermediateLabel);
            intermediateRow.Controls.Add(intermediateBox);
            intermediateRow.Controls.Add(intermediateHint);
            customLayout.Controls.Add(intermediateRow,0,2);
            customInputs.Visible=intermediateRow.Visible=false;
            customTitle.Click+=delegate
            {
                bool expanded=!customInputs.Visible;
                customLayout.SuspendLayout();customInputs.Visible=intermediateRow.Visible=expanded;
                float scale=DeviceDpi/96F;
                customLayout.RowStyles[1].Height=expanded?44*scale:0;customLayout.RowStyles[2].Height=expanded?38*scale:0;
                customCard.Height=(int)((expanded?146:64)*scale);customTitle.Text=expanded?"－ 自定义分辨率 · 仅支持驱动已有模式":"＋ 自定义分辨率与中转设置";
                customLayout.ResumeLayout();
                if(expanded){stack.PerformLayout();stack.ScrollControlIntoView(customCard);}
            };
            customCard.Controls.Add(customLayout);
            stack.Controls.Add(customCard);

            ModernCard actionsCard = new ModernCard();
            actionsCard.Dock=DockStyle.Fill;
            actionsCard.Margin=new Padding(0,0,0,12);
            actionsCard.Padding = new Padding(10);
            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 1;
            actions.RowCount = 4;
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            actions.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            SwitchButton = ModernTheme.Button("一键真实拉伸", ModernTheme.Accent, Color.White);
            SwitchButton.Dock = DockStyle.Fill;
            CalibrationButton = ModernTheme.Button("修复游戏配置", ModernTheme.Border, ModernTheme.Text);
            CalibrationButton.Enabled = true;
            CalibrationButton.Dock = DockStyle.Fill;
            RestoreButton = ModernTheme.Button("恢复", Color.White, ModernTheme.Text);
            RestoreButton.Dock = DockStyle.Fill;
            actions.Controls.Add(SwitchButton, 0, 0);
            actions.Controls.Add(CalibrationButton, 0, 1);
            actions.Controls.Add(RestoreButton, 0, 2);
            AutoRestoreOnGameExit = new CheckBox();
            AutoRestoreOnGameExit.Text = "退出后自动恢复分辨率、监视器和桌面图标";
            AutoRestoreOnGameExit.Checked = LoadAutoRestoreChoice();
            AutoRestoreOnGameExit.Dock = DockStyle.Fill;
            AutoRestoreOnGameExit.Font = ModernTheme.Font(8.5F, FontStyle.Regular);
            AutoRestoreOnGameExit.ForeColor = ModernTheme.Text;
            AutoRestoreOnGameExit.Margin = new Padding(10,2,0,0);
            AutoRestoreOnGameExit.CheckedChanged += delegate { SaveAutoRestoreChoice(AutoRestoreOnGameExit.Checked); };
            actions.Controls.Add(AutoRestoreOnGameExit,0,3);
            actionsCard.Controls.Add(actions);
            workflow.Controls.Add(actionsCard,0,1);

            ModernCard footerCard = new ModernCard();
            footerCard.Dock=DockStyle.Fill;
            footerCard.Margin=new Padding(0,0,0,0);
            footerCard.Padding=new Padding(16,8,16,8);
            footerCard.BackColor=ModernTheme.Background;
            TableLayoutPanel footer = new TableLayoutPanel();
            footer.Dock = DockStyle.Fill;
            footer.RowCount = 2;
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 30F));
            StatusLabel = ModernTheme.Label("先退出游戏修复配置，再执行一键真实拉伸。", 9F, FontStyle.Regular, ModernTheme.Text);
            Label keys = ModernTheme.Label("Ctrl + Alt + S  切换     Ctrl + Alt + H  恢复", 8.5F, FontStyle.Regular, ModernTheme.Muted);
            ShortcutLabel = keys;
            footer.Controls.Add(StatusLabel, 0, 0);
            footer.Controls.Add(keys, 0, 1);
            footerCard.Controls.Add(footer);
            workflow.Controls.Add(footerCard,0,2);
        }

        private static string IntermediateChoicePath
        {
            get { return Path.Combine(SavedState.DirectoryPath,"intermediate-mode.txt"); }
        }

        private static string AutoRestoreChoicePath
        {
            get { return Path.Combine(SavedState.DirectoryPath,"auto-restore.txt"); }
        }

        private static bool LoadAutoRestoreChoice()
        {
            try
            {
                if (!File.Exists(AutoRestoreChoicePath)) return true;
                return !string.Equals(File.ReadAllText(AutoRestoreChoicePath).Trim(),"false",StringComparison.OrdinalIgnoreCase);
            }
            catch { return true; }
        }

        private static void SaveAutoRestoreChoice(bool enabled)
        {
            if(Program.UiPreview)return;
            try
            {
                Directory.CreateDirectory(SavedState.DirectoryPath);
                File.WriteAllText(AutoRestoreChoicePath,enabled ? "true" : "false");
            }
            catch { }
        }

        private static string LoadIntermediateChoice()
        {
            try { return File.Exists(IntermediateChoicePath) ? File.ReadAllText(IntermediateChoicePath).Trim() : string.Empty; }
            catch { return string.Empty; }
        }

        private static void SaveIntermediateChoice(string value)
        {
            if(Program.UiPreview)return;
            try { Directory.CreateDirectory(SavedState.DirectoryPath); File.WriteAllText(IntermediateChoicePath,value); }
            catch { }
        }

        private sealed class DisplayModeChoice
        {
            public DisplayMode Mode { get; private set; }
            public string Key { get { return Mode.Width + "x" + Mode.Height; } }
            public DisplayModeChoice(DisplayMode mode) { Mode=mode; }
            public override string ToString() { return Mode.Width + " × " + Mode.Height; }
        }

        private static TextBox Input(string placeholder)
        {
            TextBox box = new ArenaInput {Hint=placeholder,AccessibleName=placeholder,BackColor=ModernTheme.Background};
            box.Width = 116;
            box.Height = 34;
            box.Font = ModernTheme.Font(10F, FontStyle.Regular);
            box.BorderStyle = BorderStyle.FixedSingle;
            box.Margin = new Padding(0);
            box.Tag = placeholder;
            return box;
        }

        private void AddCustomMode(object sender, EventArgs e)
        {
            int width, height;
            if (!int.TryParse(widthBox.Text.Trim(), out width) || !int.TryParse(heightBox.Text.Trim(), out height) ||
                width < 640 || width > 10000 || height < 480 || height > 10000)
            {
                MessageBox.Show("请输入有效的宽和高。", "自定义模式", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (!DisplayModeService.IsSupported(width, height))
            {
                MessageBox.Show("显卡驱动当前未提供这个模式。请先在显卡控制面板中创建该分辨率。",
                    "驱动不支持", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (CustomModeAdded != null) CustomModeAdded(width, height);
            widthBox.Clear();
            heightBox.Clear();
        }
    }

    internal sealed class ModernMonitorPage : UserControl
    {
        public CheckBox DisableSecondary { get; private set; }
        private readonly ResponsiveStack stack;
        private readonly ModernCard listCard;
        private readonly FlowLayoutPanel list;

        public ModernMonitorPage()
        {
            Dock = DockStyle.Fill;
            BackColor = ModernTheme.Background;
            stack = new ResponsiveStack();
            Controls.Add(stack);

            ModernCard settingCard = new ModernCard();
            settingCard.Height = 132;
            TableLayoutPanel setting = new TableLayoutPanel();
            setting.Dock = DockStyle.Fill;
            setting.RowCount = 3;
            setting.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
            setting.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            setting.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Label title = ModernTheme.Label("真实拉伸所需设备状态", 11F, FontStyle.Bold, ModernTheme.Text);
            DisableSecondary = new CheckBox();
            DisableSecondary.Text = "切换时临时禁用设备管理器中的监视器设备";
            DisableSecondary.Checked = true;
            DisableSecondary.Enabled = false;
            DisableSecondary.Dock = DockStyle.Fill;
            DisableSecondary.Font = ModernTheme.Font(10F, FontStyle.Bold);
            DisableSecondary.ForeColor = ModernTheme.Text;
            Label description = ModernTheme.Label("这不会关闭显卡输出；目的是让 Valorant 无法读取显示器原生宽高比。恢复时会自动重新启用。",
                8.5F, FontStyle.Regular, ModernTheme.Muted);
            description.AutoEllipsis = false;
            setting.Controls.Add(title, 0, 0);
            setting.Controls.Add(DisableSecondary, 0, 1);
            setting.Controls.Add(description, 0, 2);
            settingCard.Controls.Add(setting);
            stack.Controls.Add(settingCard);

            listCard = new ModernCard();
            listCard.Height = 180;
            TableLayoutPanel listLayout = new TableLayoutPanel();
            listLayout.Dock = DockStyle.Fill;
            listLayout.RowCount = 2;
            listLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 46F));
            listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            TableLayoutPanel listHeader = new TableLayoutPanel();
            listHeader.Dock = DockStyle.Fill;
            listHeader.ColumnCount = 2;
            listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70F));
            listHeader.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30F));
            Label listTitle = ModernTheme.Label("已连接的显示器", 11F, FontStyle.Bold, ModernTheme.Text);
            Button refresh = ModernTheme.Button("刷新列表", Color.White, ModernTheme.Text);
            refresh.Dock = DockStyle.Right;
            refresh.Width = 120;
            refresh.Margin = new Padding(0, 0, 0, 8);
            refresh.Click += delegate { RefreshList(); };
            listHeader.Controls.Add(listTitle, 0, 0);
            listHeader.Controls.Add(refresh, 1, 0);
            list = new FlowLayoutPanel();
            list.Dock = DockStyle.Fill;
            list.FlowDirection = FlowDirection.TopDown;
            list.WrapContents = false;
            list.AutoScroll = false;
            list.BackColor = Color.White;
            listLayout.Controls.Add(listHeader, 0, 0);
            listLayout.Controls.Add(list, 0, 1);
            listCard.Controls.Add(listLayout);
            stack.Controls.Add(listCard);
            RefreshList();
        }

        public void RefreshList()
        {
            list.Controls.Clear();
            List<MonitorDeviceInfo> devices;
            try { devices = MonitorDeviceService.GetDevices(false); }
            catch { devices = new List<MonitorDeviceInfo>(); }
            int itemWidth = Math.Max(300, list.ClientSize.Width - 4);
            for (int i = 0; i < devices.Count; i++)
            {
                MonitorDeviceInfo device = devices[i];
                Panel item = new Panel();
                item.Width = itemWidth;
                item.Height = 68;
                item.Margin = new Padding(0, 0, 0, 8);
                item.BackColor = ModernTheme.Background;
                TableLayoutPanel row = new TableLayoutPanel();
                row.Dock = DockStyle.Fill;
                row.Padding = new Padding(14, 8, 14, 8);
                row.ColumnCount = 2;
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 66F));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 34F));
                Label name = ModernTheme.Label((string.IsNullOrWhiteSpace(device.Name) ? "监视器设备 " + (i + 1) : device.Name),
                    9.5F, FontStyle.Bold, device.Started ? ModernTheme.Green : ModernTheme.Muted);
                Label details = ModernTheme.Label(device.Started ? "已启用" : "已禁用",
                    8.5F, FontStyle.Regular, device.Started ? ModernTheme.Green : ModernTheme.Accent);
                details.TextAlign = ContentAlignment.MiddleRight;
                row.Controls.Add(name, 0, 0);
                row.Controls.Add(details, 1, 0);
                item.Controls.Add(row);
                list.Controls.Add(item);
            }
            listCard.Height = 92 + Math.Max(1, devices.Count) * 76;
        }
    }

    internal sealed class ModernClientPage : UserControl
    {
        private readonly Func<DisplayMode> selectedMode;
        private readonly ListBox files;
        private readonly Label status;

        public ModernClientPage(Func<DisplayMode> selectedModeProvider)
        {
            selectedMode = selectedModeProvider;
            Dock = DockStyle.Fill;
            BackColor = ModernTheme.Background;
            ResponsiveStack stack = new ResponsiveStack();
            Controls.Add(stack);

            ModernCard info = new ModernCard();
            info.Height = 116;
            TableLayoutPanel infoLayout = new TableLayoutPanel();
            infoLayout.Dock = DockStyle.Fill;
            infoLayout.RowCount = 2;
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 52F));
            infoLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 48F));
            infoLayout.Controls.Add(ModernTheme.Label("配置文件检测（仅用于排查）", 11F, FontStyle.Bold, ModernTheme.Text), 0, 0);
            Label infoDescription = ModernTheme.Label("真实拉伸主流程不再修改 GameUserSettings.ini；这里只显示国服/国际服实际配置位置。",
                8.5F, FontStyle.Regular, ModernTheme.Muted);
            infoDescription.AutoEllipsis = false;
            infoLayout.Controls.Add(infoDescription, 0, 1);
            info.Controls.Add(infoLayout);
            stack.Controls.Add(info);

            ModernCard config = new ModernCard();
            config.Height = 430;
            TableLayoutPanel configLayout = new TableLayoutPanel();
            configLayout.Dock = DockStyle.Fill;
            configLayout.RowCount = 4;
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 64F));
            configLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
            configLayout.Controls.Add(ModernTheme.Label("检测到的配置文件", 10F, FontStyle.Bold, ModernTheme.Text), 0, 0);
            files = new ListBox();
            files.Dock = DockStyle.Fill;
            files.Font = ModernTheme.Font(8.5F, FontStyle.Regular);
            files.BorderStyle = BorderStyle.FixedSingle;
            configLayout.Controls.Add(files, 0, 1);
            TableLayoutPanel actions = new TableLayoutPanel();
            actions.Dock = DockStyle.Fill;
            actions.ColumnCount = 2;
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35F));
            actions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 65F));
            Button scan = ModernTheme.Button("重新扫描", Color.White, ModernTheme.Text);
            scan.Dock = DockStyle.Fill;
            scan.Click += delegate { Scan(); };
            Button repair = ModernTheme.Button("配置修复（不可用）", ModernTheme.Border, ModernTheme.Muted);
            repair.Dock = DockStyle.Fill;
            repair.Enabled = false;
            actions.Controls.Add(scan, 0, 0);
            actions.Controls.Add(repair, 1, 0);
            configLayout.Controls.Add(actions, 0, 2);
            status = ModernTheme.Label("", 8.5F, FontStyle.Regular, ModernTheme.Muted);
            configLayout.Controls.Add(status, 0, 3);
            config.Controls.Add(configLayout);
            stack.Controls.Add(config);
            Scan();
        }

        public void Scan()
        {
            files.Items.Clear();
            foreach (ValorantConfigEntry entry in ValorantLocator.FindConfigFiles()) files.Items.Add(entry);
            if (files.Items.Count > 0) files.SelectedIndex = 0;
            if (files.Items.Count == 0)
                status.Text = "未找到配置。已扫描国服安装目录、Riot 国际服目录和正在运行的游戏路径。";
            else if (ValorantLocator.IsGameRunning())
                status.Text = "已找到 " + files.Items.Count + " 个配置。检测到游戏正在运行，请退出游戏后再修复。";
            else
                status.Text = "已找到 " + files.Items.Count + " 个配置；修复前会自动创建带时间戳的备份。";
        }

        private void RepairSelected(object sender, EventArgs e)
        {
            if (files.SelectedItem == null)
            {
                MessageBox.Show("请先选择一个配置文件。", "客户端配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            if (ValorantLocator.IsGameRunning())
            {
                MessageBox.Show("请先完全退出 Valorant 再修改配置。运行中的游戏会覆盖文件，导致设置不生效。",
                    "游戏正在运行", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DisplayMode mode = selectedMode();
            if (mode == null)
            {
                MessageBox.Show("请先选择目标分辨率。", "客户端配置", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            ValorantConfigEntry entry = files.SelectedItem as ValorantConfigEntry;
            string file = entry == null ? Convert.ToString(files.SelectedItem) : entry.Path;
            if (MessageBox.Show(string.Format("备份后将配置修改为 {0} × {1}、全屏窗口模式。", mode.Width, mode.Height),
                "确认修复", MessageBoxButtons.OKCancel, MessageBoxIcon.Question) != DialogResult.OK) return;
            try
            {
                string backup = ValorantConfig.Repair(file, mode.Width, mode.Height);
                status.Text = "修复完成。备份文件：" + backup;
                MessageBox.Show("配置已修复，并已创建备份。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                MessageBox.Show("修复失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    internal sealed class ModernTutorialPage : UserControl
    {
        private readonly Button[] moduleButtons;
        private readonly Control[] modulePages;

        public ModernTutorialPage()
        {
            Dock = DockStyle.Fill;
            BackColor = ModernTheme.Background;
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Margin = new Padding(0);
            root.Padding = new Padding(0);
            root.ColumnCount = 1;
            root.RowCount = 2;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 108F));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            Controls.Add(root);

            FlowLayoutPanel moduleBar = new FlowLayoutPanel();
            moduleBar.Dock = DockStyle.Fill;
            moduleBar.FlowDirection = FlowDirection.LeftToRight;
            moduleBar.WrapContents = true;
            moduleBar.Padding = new Padding(20, 10, 12, 8);
            moduleBar.Margin = new Padding(0);
            moduleBar.BackColor = Color.White;
            root.Controls.Add(moduleBar, 0, 0);

            modulePages = new Control[] {
                BuildGuide("VALORANT 真实拉伸", "只包含分辨率、监视器和恢复流程。",
                    new[]{"退出游戏并选择目标","修复游戏配置","开始真实拉伸","进入 3D 画面","确认应用目标","直接更换目标","恢复显示"},
                    new[]{
                        "完全退出 Valorant，以管理员身份运行本工具，选择需要的 4:3、5:4 或自定义目标分辨率。",
                        "点击“修复游戏配置”。工具会备份检测到的配置，并锁定为目标分辨率、全屏窗口＋填充。",
                        "点击“一键真实拉伸”并确认。工具会保存当前实际分辨率与刷新率、禁用监视器，再自动选择可用中转。",
                        "中转窗口出现后启动或返回游戏，进入能看到武器和 HUD 的靶场或对局；无需手动修改画面模式。",
                        "进入 3D 画面后 Alt+Tab 返回确认窗口点击“是”；点击“否”会恢复原始显示和配置。",
                        "拉伸启用后，选择另一个目标并再次点击主按钮即可直接切换，不经过中转，也不需要退出游戏。",
                        "默认开启退出自动恢复。进程消失约 2 秒后会恢复分辨率、刷新率、监视器和配置；也可按 Ctrl+Alt+H。"
                    }, "本功能不注入游戏进程；原始显示状态会在禁用监视器前保存并校验。"),
                BuildGuide("VALORANT 准心", "只包含瓦准心代码的复制与导入。",
                    new[]{"打开准心库","选择常用准心","复制导入代码","在游戏内导入","进入靶场核对"},
                    new[]{
                        "进入 VALORANT，点击上方“准心代码”模块。",
                        "按名称和参数选择需要的职业选手或常用准心。",
                        "点击“复制导入代码”，完整代码会进入剪贴板。",
                        "进入游戏设置 → 准心 → 导入准心配置代码，粘贴后确认。",
                        "不同分辨率会改变准心的实际像素观感，请在当前比例的靶场中核对后再使用。"
                    }, "准心页面不绘制可能失真的模拟图；实际效果以游戏渲染为准。"),
                BuildGuide("CS2 准心", "使用新版准星导入码。",
                    new[]{"打开准心库","选择准心","复制导入码","打开准星设置","粘贴并导入"},
                    new[]{
                        "进入 COUNTER-STRIKE 2，点击上方“准心代码”模块。",
                        "选择 donk、NiKo、m0NESY、ZywOo 等常用准心。",
                        "点击“复制准星导入码”。",
                        "进入 CS2 设置 → 准星/瞄准镜 → 分享或导入。",
                        "粘贴导入码并确认，再进入地图检查实际显示。"
                    }, "CS2 会在更换分辨率时自动缩放准星像素尺寸；实际效果以游戏显示为准。"),
                BuildGuide("CS2 竞技滤镜", "只包含显示颜色滤镜的应用与恢复。",
                    new[]{"打开滤镜模块","选择竞技方案","偏暖推荐","应用与切换","恢复原始颜色"},
                    new[]{
                        "进入 COUNTER-STRIKE 2，点击上方“画面滤镜”模块。",
                        "竞技清晰与高对比适合追求边缘和层次；职业均衡、职业鲜艳和敌人突出提供不同强度。",
                        "喜欢偏暖色调可优先选择“暖色竞技 · 推荐”，它会降低蓝色通道但不过度泛黄。",
                        "点击“应用滤镜”即可生效；可直接点击另一套预设切换，不需要先恢复。",
                        "点击“恢复原始颜色”或正常退出工具会还原启动前的 Gamma Ramp；HDR 可能阻止滤镜生效。"
                    }, "滤镜作用于 Windows 显示输出，不注入 CS2；无边框窗口模式通常最稳定。"),
                BuildGuide("游戏优化", "按游戏选择 Windows 系统优化选项。",
                    new[]{"选择游戏","调整进程调度","配置图形首选项"},
                    new[]{
                        "在 WebView2 版游戏优化页选择 CS2 或 VALORANT，先查看进程是否正在运行。",
                        "可仅对选中的游戏进程应用高于普通的调度优先级；不会结束后台程序。",
                        "可打开 Windows 图形设置，为该游戏主程序选择高性能 GPU 或窗口化优化。"
                    }, "图形设置由 Windows 管理；游戏模式和电源模式属于全局设置，会明确标注。")
            };

            string[] names = { "VALORANT 拉伸", "瓦准心", "CS2 准心", "CS2 滤镜", "游戏优化" };
            moduleButtons = new Button[names.Length];
            Panel host = new Panel();
            host.Dock = DockStyle.Fill;
            host.Margin = new Padding(0);
            host.BackColor = ModernTheme.Background;
            root.Controls.Add(host, 0, 1);
            for (int i = 0; i < modulePages.Length; i++)
            {
                int index = i;
                Button button = ModernTheme.Button(names[i], Color.White, ModernTheme.Text);
                button.Width = 140;
                button.Height = 42;
                button.Margin = new Padding(2);
                button.Click += delegate { ShowModule(index); };
                moduleButtons[i] = button;
                moduleBar.Controls.Add(button);
                modulePages[i].Dock = DockStyle.Fill;
                host.Controls.Add(modulePages[i]);
            }
            ShowModule(0);
        }

        private void ShowModule(int selected)
        {
            for (int i = 0; i < modulePages.Length; i++)
            {
                bool active = i == selected;
                modulePages[i].Visible = active;
                moduleButtons[i].BackColor = active ? ModernTheme.AccentSoft : Color.White;
                moduleButtons[i].ForeColor = active ? ModernTheme.Accent : ModernTheme.Text;
            }
            modulePages[selected].BringToFront();
        }

        private static Control BuildGuide(string title, string subtitle, string[] steps, string[] descriptions, string tip)
        {
            ResponsiveStack stack = new ResponsiveStack();
            ModernCard intro = new ModernCard();
            intro.Height = 112;
            TableLayoutPanel introLayout = new TableLayoutPanel();
            introLayout.Dock = DockStyle.Fill;
            introLayout.RowCount = 2;
            introLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            introLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            introLayout.Controls.Add(ModernTheme.Label(title, 12F, FontStyle.Bold, ModernTheme.Text), 0, 0);
            Label subtitleLabel = ModernTheme.Label(subtitle, 8.5F, FontStyle.Regular, ModernTheme.Muted);
            subtitleLabel.AutoEllipsis = false;
            introLayout.Controls.Add(subtitleLabel, 0, 1);
            intro.Controls.Add(introLayout);
            stack.Controls.Add(intro);

            for (int i = 0; i < steps.Length; i++)
            {
                ModernCard card = new ModernCard();
                card.Height = 154;
                TableLayoutPanel row = new TableLayoutPanel();
                row.Dock = DockStyle.Fill;
                row.ColumnCount = 2;
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70F));
                row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                Label number = ModernTheme.Label((i + 1).ToString("00"), 12F, FontStyle.Bold, ModernTheme.Accent);
                number.TextAlign = ContentAlignment.MiddleCenter;
                TableLayoutPanel text = new TableLayoutPanel();
                text.Dock = DockStyle.Fill;
                text.RowCount = 2;
                text.RowStyles.Add(new RowStyle(SizeType.Absolute, 38F));
                text.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
                text.Controls.Add(ModernTheme.Label(steps[i], 10.5F, FontStyle.Bold, ModernTheme.Text), 0, 0);
                Label description = ModernTheme.Label(descriptions[i], 8.5F, FontStyle.Regular, ModernTheme.Muted);
                description.AutoEllipsis = false;
                description.TextAlign = ContentAlignment.TopLeft;
                text.Controls.Add(description, 0, 1);
                row.Controls.Add(number, 0, 0);
                row.Controls.Add(text, 1, 0);
                card.Controls.Add(row);
                stack.Controls.Add(card);
            }

            ModernCard note = new ModernCard();
            note.Height = 118;
            note.BackColor = ModernTheme.AccentSoft;
            Label noteText = ModernTheme.Label("注意\r\n" + tip, 9F, FontStyle.Bold, ModernTheme.Accent);
            noteText.AutoEllipsis = false;
            note.Controls.Add(noteText);
            stack.Controls.Add(note);
            return stack;
        }
    }

    internal class ModernMainForm : Form
    {
        private const int WmHotkey = 0x0312;
        private const int WmDisplayChange = 0x007E;
        private const int HotkeySwitch = 0x6201;
        private const int HotkeyRestore = 0x6202;
        private const uint ModAlt = 0x0001;
        private const uint ModControl = 0x0002;

        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint virtualKey);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hwnd, int id);

        private readonly ModernSwitchPage switchPage;
        private readonly Label pageTitle;
        private readonly Label headerStatus;
        private readonly Label pageSubtitle;
        private readonly Control[] pages;
        private readonly ModernNavButton[] navButtons;
        private readonly System.Windows.Forms.Timer rollbackTimer;
        private readonly System.Windows.Forms.Timer displayRefreshTimer;
        private readonly System.Windows.Forms.Timer gameExitMonitorTimer;
        private readonly System.Windows.Forms.Timer gameStateMonitorTimer;
        private readonly ValorantGameStateLogWatcher gameStateLogWatcher = new ValorantGameStateLogWatcher();
        private SavedState pendingState;
        private int secondsLeft;
        private bool closingAfterRestore;
        private bool gameWasRunningDuringStretch;
        private int gameMissingTicks;
        private readonly RestoreJobRunner restoreJob = new RestoreJobRunner();
        private bool closeWhenRestoreCompletes;
        private bool switchInProgress;
        private ToolboxSettings toolboxSettings = Program.UiPreview ? new ToolboxSettings() : ToolboxSettings.Load();
        private HotkeyManager hotkeyManager;
        private readonly ToolboxSettingsPage toolboxSettingsPage;
        private readonly GameAudioPage audioPage;
        private NotifyIcon trayIcon;
        private ContextMenuStrip trayMenu;
        private string hotkeyNotice;

        public ModernMainForm()
        {
            Text = Program.UiPreview ? "Nexa Arena · UI 预览（不修改系统设置）" : "Nexa Arena · CS2 / VALORANT 游戏工具箱";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1180, 800);
            MinimumSize = new Size(1040, 720);
            DoubleBuffered = true;
            BackColor = ModernTheme.Background;
            Font = ModernTheme.Font(9F, FontStyle.Regular);
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); }
            catch { Icon = SystemIcons.Application; }
            AutoScaleDimensions = new SizeF(96F, 96F);
            AutoScaleMode = AutoScaleMode.Dpi;

            TableLayoutPanel shell = new TableLayoutPanel();
            shell.Dock = DockStyle.Fill;
            shell.ColumnCount = 1;
            shell.RowCount = 4;
            shell.Margin = new Padding(0);
            shell.Padding = new Padding(0);
            shell.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 70F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 52F));
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
            Controls.Add(shell);

            TableLayoutPanel commandBar = new TableLayoutPanel();
            commandBar.Dock = DockStyle.Fill;
            commandBar.Margin = new Padding(0);
            commandBar.BackColor = ModernTheme.Navy;
            commandBar.ColumnCount = 2;
            commandBar.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 212F));
            commandBar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            shell.Controls.Add(commandBar, 0, 0);
            commandBar.Controls.Add(new ArenaBrand(), 0, 0);
            Label commandHint = ModernTheme.Label("CS2  /  VALORANT   ·   DISPLAY  /  AIM  /  PERFORMANCE",
                8.5F, FontStyle.Regular, Color.FromArgb(174, 186, 197));
            commandHint.TextAlign = ContentAlignment.MiddleRight;
            commandHint.Margin = new Padding(0, 0, 24, 0);
            commandBar.Controls.Add(commandHint, 1, 0);

            FlowLayoutPanel navigation = new FlowLayoutPanel();
            navigation.Dock = DockStyle.Fill;
            navigation.FlowDirection = FlowDirection.LeftToRight;
            navigation.WrapContents = false;
            navigation.AutoScroll = false;
            navigation.Margin = new Padding(0);
            navigation.Padding = new Padding(22, 3, 22, 2);
            navigation.BackColor = ModernTheme.Navy;
            shell.Controls.Add(navigation, 0, 1);

            TableLayoutPanel content = new TableLayoutPanel();
            content.Dock = DockStyle.Fill;
            content.Margin = new Padding(0);
            content.RowCount = 2;
            content.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            content.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            shell.Controls.Add(content, 0, 2);

            TableLayoutPanel topbar = new TableLayoutPanel();
            topbar.ColumnCount = 2;
            topbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 68F));
            topbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 32F));
            topbar.Dock = DockStyle.Fill;
            topbar.Margin = new Padding(0);
            topbar.Padding = new Padding(26, 10, 26, 8);
            topbar.BackColor = ModernTheme.Background;
            content.Controls.Add(topbar, 0, 0);
            pageTitle = ModernTheme.Label("VALORANT", 19F, FontStyle.Bold, ModernTheme.Text);
            var titleGroup = new Panel { Dock = DockStyle.Fill, Margin = new Padding(0) };
            pageSubtitle = ModernTheme.Label("显示比例、游戏配置与恢复", 8.5F, FontStyle.Regular, ModernTheme.Muted);
            pageTitle.Dock = DockStyle.Top; pageTitle.Height = 36;
            pageSubtitle.Dock = DockStyle.Top; pageSubtitle.Height = 23;
            titleGroup.Controls.Add(pageSubtitle); titleGroup.Controls.Add(pageTitle);
            topbar.Controls.Add(titleGroup, 0, 0);
            topbar.RowCount = 1;
            topbar.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
            headerStatus = ModernTheme.Label("正在读取显示设置…", 8.5F, FontStyle.Regular, ModernTheme.Muted);
            headerStatus.TextAlign = ContentAlignment.MiddleRight;
            topbar.Controls.Add(headerStatus, 1, 0);

            Panel pageHost = new Panel();
            pageHost.Dock = DockStyle.Fill;
            pageHost.Margin = new Padding(0);
            pageHost.BackColor = ModernTheme.Background;
            content.Controls.Add(pageHost, 0, 1);

            Label footer = ModernTheme.Label("免费游戏工具箱     作者：2ndWind     QQ：2583053476  ·  点击复制",
                8F, FontStyle.Regular, Color.FromArgb(174, 186, 197));
            footer.BackColor = ModernTheme.Navy;
            footer.Padding = new Padding(26, 0, 0, 0);
            footer.Cursor = Cursors.Hand;
            footer.AccessibleName = "复制作者 QQ 2583053476";
            footer.Click += delegate { ToolUi.Copy("2583053476"); UpdateStatus("作者 QQ 已复制：2583053476"); };
            shell.Controls.Add(footer, 0, 3);
            switchPage = new ModernSwitchPage();
            CrosshairLibraryPage valorantCrosshairs = new CrosshairLibraryPage(true);
            CrosshairLibraryPage cs2Crosshairs = new CrosshairLibraryPage(false);
            GameFilterPage cs2Filters = new GameFilterPage();
            ModernTutorialPage tutorialPage = new ModernTutorialPage();
            GameSectionPage valorantSection = new GameSectionPage("真实拉伸", switchPage, "准心代码", valorantCrosshairs);
            ToolTabs cs2Section = new ToolTabs(new[] { "准心代码", "画面滤镜", "指令生成器" }, cs2Crosshairs, cs2Filters, new Cs2CommandsPage());
            SensitivityPage sensitivityPage = new SensitivityPage(delegate { return toolboxSettings; }, ApplyToolboxSettings);
            audioPage = new GameAudioPage();
            toolboxSettingsPage = new ToolboxSettingsPage(delegate { return toolboxSettings; }, ApplyToolboxSettings);
            ToolTabs toolsSection = new ToolTabs(new[] { "游戏音频" }, audioPage);
            string[] names = { "VALORANT", "COUNTER-STRIKE 2", "灵敏度中心", "游戏工具", "托盘 / 快捷键", "使用指南" };
            string[] navNames = { "VALORANT", "CS2", "灵敏度", "工具", "快捷键", "指南" };
            pages = new Control[] { valorantSection, cs2Section, sensitivityPage, toolsSection, toolboxSettingsPage, tutorialPage };
            navButtons = new ModernNavButton[names.Length];
            for (int i = 0; i < pages.Length; i++)
            {
                int index = i;
                pages[i].Dock = DockStyle.Fill;
                pages[i].Visible = i == 0;
                pageHost.Controls.Add(pages[i]);
                ModernNavButton button = new ModernNavButton(navNames[i]);
                button.Selected = i == 0;
                button.IconName = new[] {"aim","monitor","aim","bolt","settings","guide"}[i];
                button.Margin = new Padding(4, 0, 4, 0);
                button.Click += delegate { ShowPage(index, names[index]); };
                navButtons[i] = button;
                navigation.Controls.Add(button);
            }
            Action arrangeNavigation = delegate
            {
                navigation.SuspendLayout();
                int width = Math.Max(130, (navigation.ClientSize.Width - navigation.Padding.Horizontal - navButtons.Length * 8) / navButtons.Length);
                foreach (ModernNavButton button in navButtons)
                {
                    button.Height=42;
                    button.Width=width;
                }
                navigation.ResumeLayout();
            };
            navigation.SizeChanged += delegate { arrangeNavigation(); };
            if (Program.UiPreview) { switchPage.SwitchButton.Enabled=false;switchPage.CalibrationButton.Enabled=false;switchPage.RestoreButton.Enabled=false;switchPage.AutoRestoreOnGameExit.Enabled=false;cs2Filters.Enabled=false; }

            switchPage.Presets.AddMode(1280, 960, "经典 4:3", true);
            switchPage.Presets.AddMode(1280, 1024, "5:4", false);
            switchPage.Presets.AddMode(1440, 1080, "清晰 4:3", false);
            switchPage.Presets.AddMode(1568, 1080, "宽体拉伸", false);
            foreach (Point custom in CustomPresetStore.Load())
                switchPage.Presets.AddMode(custom.X, custom.Y, "自定义", false);
            switchPage.CustomModeAdded += delegate(int width, int height)
            {
                switchPage.Presets.AddMode(width, height, "自定义", true);
                if(!Program.UiPreview)CustomPresetStore.Add(width, height);
            };
            switchPage.SwitchButton.Click += delegate { BeginSwitch(); };
            switchPage.CalibrationButton.Click += delegate { RepairGameConfig(); };
            switchPage.RestoreButton.Click += delegate { Restore(); };
            switchPage.Presets.SelectedModeChanged += delegate
            {
                TrueStretchStage activeStage = TrueStretchStageStore.Load();
                if (activeStage != null && activeStage.Step >= 2)
                    switchPage.SwitchButton.Text = "直接切换到 " + switchPage.Presets.SelectedMode.Width + " × " + switchPage.Presets.SelectedMode.Height;
                UpdateStatus("已选择 " + switchPage.Presets.SelectedMode + "。游戏进入靶场后可执行真实拉伸。");
            };

            rollbackTimer = new System.Windows.Forms.Timer();
            rollbackTimer.Interval = 1000;
            rollbackTimer.Tick += RollbackTick;
            displayRefreshTimer = new System.Windows.Forms.Timer();
            displayRefreshTimer.Interval = 1000;
            displayRefreshTimer.Tick += delegate { RefreshDisplayLabels(); };
            gameExitMonitorTimer = new System.Windows.Forms.Timer();
            gameExitMonitorTimer.Interval = 1000;
            gameExitMonitorTimer.Tick += GameExitMonitorTick;
            gameStateMonitorTimer = new System.Windows.Forms.Timer();
            gameStateMonitorTimer.Interval = 1000;
            gameStateMonitorTimer.Tick += GameStateMonitorTick;
            Shown += delegate
            {
                arrangeNavigation();
                ShowPage(0, "VALORANT");
                RefreshDisplayLabels();
                if (!Program.UiPreview) { WarnIfStateExists(); displayRefreshTimer.Start(); gameExitMonitorTimer.Start(); }
                UpdateHotkeyHint();
                if (!string.IsNullOrEmpty(hotkeyNotice)) toolboxSettingsPage.SetStatus(hotkeyNotice);
            };
            FormClosing += OnFormClosing;
            InitializeTray();
            Resize += delegate
            {
                if (WindowState == FormWindowState.Minimized && toolboxSettings.MinimizeToTray && trayIcon != null)
                {
                    Hide(); trayIcon.ShowBalloonTip(2000, "Nexa Arena 已最小化", "双击托盘图标打开；右键可恢复显示或退出。", ToolTipIcon.Info);
                }
            };
            FormClosed += delegate
            {
                if (trayIcon != null) { trayIcon.Visible = false; trayIcon.Dispose(); }
                if (trayMenu != null) trayMenu.Dispose();
            };
        }

        private void InitializeTray()
        {
            trayMenu = new ContextMenuStrip();
            trayMenu.Items.Add("打开主窗口", null, delegate { ShowMainWindow(); });
            trayMenu.Items.Add("切换 4:3", null, delegate { ShowMainWindow(); BeginSwitch(); });
            trayMenu.Items.Add("恢复显示", null, delegate { ShowMainWindow(); Restore(); });
            trayMenu.Items.Add("麦克风静音 / 取消", null, delegate { ToggleMicrophone(); });
            trayMenu.Items.Add(new ToolStripSeparator());
            trayMenu.Items.Add("恢复并退出", null, delegate { ShowMainWindow(); Close(); });
            trayIcon = new NotifyIcon { Icon = Icon, Text = "Nexa Arena · 游戏工具箱", ContextMenuStrip = trayMenu, Visible = true };
            trayIcon.DoubleClick += delegate { ShowMainWindow(); };
        }

        private void ShowMainWindow()
        {
            Show(); if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
            Activate();
        }

        private void ToggleMicrophone()
        {
            try
            {
                string message = GameAudio.ToggleDefaultMicrophone();
                if (trayIcon != null) trayIcon.ShowBalloonTip(2500, "麦克风", message, ToolTipIcon.Info);
                audioPage.RefreshAudio();
            }
            catch (Exception ex) { ShowMainWindow(); MessageBox.Show(this, ex.Message, "麦克风操作失败", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
        }

        private void ApplyToolboxSettings(ToolboxSettings next)
        {
            ToolboxSettings.Validate(next);
            if (Program.UiPreview) throw new InvalidOperationException("当前为 UI 预览；不保存设置或注册全局快捷键。");
            ToolboxSettings previous = toolboxSettings;
            if (hotkeyManager != null) hotkeyManager.Apply(next.Hotkeys);
            try { next.Save(); toolboxSettings = next; }
            catch { if (hotkeyManager != null) hotkeyManager.Apply(previous.Hotkeys); throw; }
            UpdateHotkeyHint();
        }

        private void UpdateHotkeyHint()
        {
            if (switchPage == null || switchPage.ShortcutLabel == null) return;
            switchPage.ShortcutLabel.Text = toolboxSettings.Hotkeys.First(k => k.Action == "switch") + "  切换     " +
                toolboxSettings.Hotkeys.First(k => k.Action == "restore") + "  恢复";
        }

        private void ShowPage(int index, string title)
        {
            for (int i = 0; i < pages.Length; i++)
            {
                pages[i].Visible = i == index;
                navButtons[i].Selected = i == index;
            }
            pages[index].BringToFront();
            pageTitle.Text = title;
            string[] subtitles={"显示比例、游戏配置与恢复。","准星、画面滤镜与实用指令。","跨游戏换算与 DPI 调整。","音频、帧率与性能管理。","托盘行为与快捷键。","按功能查找操作步骤。"};
            pageSubtitle.Text=subtitles[index];
        }

        // The WebView2 presentation calls these same tested actions; display and
        // recovery state remain owned by ModernMainForm and its existing services.
        internal object WebDisplayState()
        {
            DisplayMode current=DisplayModeService.GetCurrent();
            SavedState saved=SavedState.Load();
            TrueStretchStage stage=TrueStretchStageStore.Load();
            DisplayMode selected=switchPage.Presets.SelectedMode;
            return new {
                width=current.Width,height=current.Height,refresh=current.RefreshRate,
                saved=saved==null?null:(object)new {width=saved.Width,height=saved.Height,refresh=saved.RefreshRate},
                stage=stage==null?0:stage.Step,
                selected=selected==null?null:(object)new {width=selected.Width,height=selected.Height},
                autoRestore=switchPage.AutoRestoreOnGameExit.Checked,
                status=switchPage.StatusLabel.Text,
                restoring=restoreJob.IsRunning,
                switching=switchInProgress,
                configRepaired=ConfigSessionStore.Exists
            };
        }

        internal void WebSelectMode(int width,int height){switchPage.Presets.SelectMode(width,height);}
        internal void WebAddMode(int width,int height)
        {
            if(width<640||height<480||width>10000||height>10000||!DisplayModeService.IsSupported(width,height))
                throw new InvalidOperationException("显卡驱动尚未提供该分辨率，请先在显卡控制面板中创建。");
            switchPage.Presets.AddMode(width,height,"自定义",true);
            if(!Program.UiPreview)CustomPresetStore.Add(width,height);
        }
        internal void WebRepairConfig(){RepairGameConfig();}
        internal void WebSwitch(){BeginSwitch();}
        internal void WebRestore(){Restore();}
        internal void WebAutoRestore(bool enabled){switchPage.AutoRestoreOnGameExit.Checked=enabled;}
        internal ToolboxSettings WebSettings(){return toolboxSettings.Copy();}
        internal void WebSaveSettings(ToolboxSettings next){ApplyToolboxSettings(next);}

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (Program.UiPreview) return;
            hotkeyManager = new HotkeyManager(new WindowsHotkeyBackend(Handle));
            try { hotkeyManager.Apply(toolboxSettings.Hotkeys); hotkeyNotice = null; }
            catch (Exception ex) { hotkeyNotice = ex.Message + " 请在托盘 / 快捷键页面修改；其他功能仍可使用。"; }
        }

        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (hotkeyManager != null) { hotkeyManager.Dispose(); hotkeyManager = null; }
            base.OnHandleDestroyed(e);
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmDisplayChange)
            {
                BeginInvoke((Action)delegate { RefreshDisplayLabels(); });
                base.WndProc(ref m);
                return;
            }
            if (m.Msg == WmHotkey)
            {
                string action = hotkeyManager == null ? null : hotkeyManager.ActionFor(m.WParam.ToInt32());
                if (action == "switch") { ShowMainWindow(); BeginSwitch(); }
                if (action == "restore") { ShowMainWindow(); Restore(); }
                if (action == "window") ShowMainWindow();
                if (action == "microphone") ToggleMicrophone();
                return;
            }
            base.WndProc(ref m);
        }

        private void WarnIfStateExists()
        {
            TrueStretchStage stage = TrueStretchStageStore.Load();
            SavedState state = SavedState.Load();
            if(stage==null && ConfigSessionStore.Exists)
                UpdateStatus("游戏配置已修复并锁定，可以启动游戏或执行一键真实拉伸；点击恢复可撤销。");
            if(state==null && stage!=null && stage.GetOriginalState()==null)
            {
                // 处理超时看门狗留下的孤立会话。只有当前
                // 已不是目标/中转且监视器明确 Started 时才安全清理。
                try
                {
                    DisplayMode current=DisplayModeService.GetCurrent();
                    List<MonitorDeviceInfo> monitors=MonitorDeviceService.GetDevices(true);
                    bool nativeAgain=(current.Width!=stage.TargetWidth || current.Height!=stage.TargetHeight) &&
                        (current.Width!=stage.BridgeWidth || current.Height!=stage.BridgeHeight);
                    if(nativeAgain && monitors.Count>0 && monitors.All(x=>x.Started))
                    {
                        if(ConfigSessionStore.Exists) ConfigSessionStore.Restore();
                        TrueStretchStageStore.Complete();
                        stage=null;
                        UpdateStatus("已清理失效会话；当前显示状态正常，可以重新开始。");
                    }
                }
                catch(Exception ex) { ErrorLog.Write(ex); }
            }
            if(state==null && stage!=null) state=stage.GetOriginalState();
            if (state != null)
                switchPage.OriginalLabel.Text = string.Format("已保存原始状态：{0} × {1} @ {2}Hz", state.Width, state.Height, state.RefreshRate);
            if (stage == null) return;
            if (stage.Step == 1)
            {
                switchPage.SwitchButton.Text = "校准完成后应用目标";
                UpdateStatus("中转分辨率已应用。启动或返回游戏，进入可见武器与 HUD 的画面后再点击主按钮。");
            }
            else
            {
                gameWasRunningDuringStretch = true;
                gameStateLogWatcher.ResetToEnd();
                switchPage.SwitchButton.Text = "真实拉伸已启用";
                switchPage.SwitchButton.BackColor = ModernTheme.Green;
                UpdateStatus("真实拉伸会话正在运行；结束游戏后点击恢复。");
            }
        }

        private void GameStateMonitorTick(object sender,EventArgs e)
        {
            // 不监听对局切换，避免选人或第二局时擅自重设显示模式。
        }

        private void GameExitMonitorTick(object sender, EventArgs e)
        {
            if (restoreJob.IsRunning || switchInProgress || !switchPage.AutoRestoreOnGameExit.Checked)
            {
                gameMissingTicks=0;
                return;
            }
            TrueStretchStage stage = TrueStretchStageStore.Load();
            if (stage == null || stage.Step < 2)
            {
                gameWasRunningDuringStretch=false;
                gameMissingTicks=0;
                return;
            }
            bool running = ValorantLocator.IsGameRunning();
            if (running)
            {
                gameWasRunningDuringStretch=true;
                gameMissingTicks=0;
                return;
            }
            if (!gameWasRunningDuringStretch) return;
            gameMissingTicks++;
            if (gameMissingTicks == 1)
                UpdateStatus("检测到 Valorant 进程已退出，正在确认并准备恢复…");
            // 对局、选人和加载不会结束 Valorant 主进程；连续两次未检测到
            // 已足以过滤进程列表的瞬时读取波动，无需固定等待 30 秒。
            if (gameMissingTicks < 2) return;
            gameMissingTicks=0;
            UpdateStatus("检测到 Valorant 已退出，正在自动恢复…");
            RestoreTrueStretch();
        }

        private void BeginSwitch()
        {
            if (Program.UiPreview) return;
            if (restoreJob.IsRunning || switchInProgress) return;
            switchInProgress = true;
            try { BeginSwitchCore(); }
            finally
            {
                switchInProgress = false;
                if (closeWhenRestoreCompletes && !restoreJob.IsRunning) Close();
            }
        }

        private void BeginSwitchCore()
        {
            // 优化顺序：先修复并锁定配置 → 禁用监视器 → 自动选择中转 →
            // 游戏读取全屏窗口＋填充配置 → 应用目标并保持配置锁定。
            TrueStretchStage existing = TrueStretchStageStore.Load();
            if (existing != null && existing.Step == 1)
            {
                ApplyTargetAfterCalibration(existing);
                return;
            }
            if (existing != null && existing.Step >= 2)
            {
                DirectSwitchActiveSession(existing);
                return;
            }
            try
            {
                DisplayMode target = RequireTarget(false);
                if(!ConfigSessionStore.Exists)
                {
                    MessageBox.Show("请先完全退出 Valorant，点击“修复游戏配置”。修复完成后再执行一键真实拉伸。",
                        "请先修复游戏配置",MessageBoxButtons.OK,MessageBoxIcon.Information);
                    return;
                }
                if(MessageBox.Show("点击“是”后，软件会保存当前分辨率和刷新率、禁用监视器，并自动选择可用的中转分辨率。\r\n\r\n游戏配置已锁定为全屏窗口＋填充，无需再手动修改画面设置。",
                    "开始真实拉伸",MessageBoxButtons.YesNo,MessageBoxIcon.Question,
                    MessageBoxDefaultButton.Button2)!=DialogResult.Yes) return;
                SyncStretchSessionConfigs(target.Width,target.Height);
                RunIntermediateFlow(target);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                RestoreTrueStretch();
                MessageBox.Show("切换失败：" + ex.Message, "Nexa Arena", MessageBoxButtons.OK, MessageBoxIcon.Error);
                RefreshDisplayLabels();
            }
        }

        private void DirectSwitchActiveSession(TrueStretchStage stage)
        {
            if (restoreJob.IsRunning) return;
            try
            {
                DisplayMode target = RequireTarget(true);
                UpdateStatus("真实拉伸已启用，正在直接切换到 " + target.Width + " × " + target.Height + "…");
                Application.DoEvents();
                SyncStretchSessionConfigs(target.Width,target.Height);
                DisplayMode currentMode=DisplayModeService.GetCurrent();
                if(currentMode.Width!=target.Width || currentMode.Height!=target.Height)
                {
                    SavedState original=SavedState.Load() ?? stage.GetOriginalState();
                    int refresh=original==null ? currentMode.RefreshRate : original.RefreshRate;
                    DisplayModeService.ApplyTargetMode(target.Width,target.Height,refresh);
                }
                stage.TargetWidth = target.Width;
                stage.TargetHeight = target.Height;
                stage.Step = 2;
                TrueStretchStageStore.Save(stage);
                gameStateLogWatcher.ResetToEnd();
                gameWasRunningDuringStretch=true;
                gameMissingTicks=0;
                switchPage.SwitchButton.Text = "真实拉伸已启用";
                switchPage.SwitchButton.BackColor = ModernTheme.Green;
                UpdateStatus("已直接切换到 " + target.Width + " × " + target.Height + "；不需要中转或退出游戏。");
                RefreshDisplayLabels();
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                MessageBox.Show("切换其他分辨率失败：" + ex.Message, "Nexa Arena",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RunIntermediateFlow(DisplayMode target)
        {
                TrueStretchStage stage = PrepareStage(target);
                UpdateStatus("已确认禁用监视器，正在自动选择中转分辨率…");
                Application.DoEvents();
                DisplayMode bridge = SelectAndApplyBridgeMode(target);
                stage.BridgeWidth=bridge.Width;
                stage.BridgeHeight=bridge.Height;
                stage.Step = 1;
                TrueStretchStageStore.Save(stage);
                UpdateStatus("中转已完成；启动或返回游戏，进入可见武器与 HUD 的画面后再回来点“是”。");
                DialogResult ready = MessageBox.Show(bridge.Width+"×"+bridge.Height+" 中转已经应用，监视器保持禁用。\r\n\r\n现在启动或返回 Valorant。游戏会读取已锁定的“全屏窗口＋填充”配置；进入能看到武器和 HUD 的画面后，Alt+Tab 回到本窗口点击“是”，软件会应用目标分辨率。\r\n\r\n点击“否”会恢复原始显示设置和游戏配置。",
                    "等待游戏进入 3D 画面", MessageBoxButtons.YesNo, MessageBoxIcon.Information,
                    MessageBoxDefaultButton.Button2);
                if (ready != DialogResult.Yes)
                {
                    RestoreTrueStretch();
                    return;
                }
                ApplyTargetAfterCalibration(stage);
        }

        private DisplayMode SelectAndApplyBridgeMode(DisplayMode target)
        {
            DisplayMode current=DisplayModeService.GetCurrent();
            int[,] candidates = new int[,] {
                {1280,800}, {1280,960}, {1280,768},
                {1366,768}, {1600,900}, {1280,720}
            };
            List<string> attempted=new List<string>();
            for(int i=0;i<candidates.GetLength(0);i++)
            {
                int width=candidates[i,0],height=candidates[i,1];
                if((width==target.Width&&height==target.Height)||
                   (width==current.Width&&height==current.Height)) continue;
                if(!DisplayModeService.IsSupported(width,height)) continue;
                attempted.Add(width+"×"+height);
                if(DisplayModeService.TrySetBridgeMode(width,height))
                    return DisplayModeService.GetCurrent();
            }
            throw new InvalidOperationException("显卡驱动没有接受可用的中转分辨率。已尝试："+
                (attempted.Count==0 ? "无（请先在显卡控制面板创建 1280×800）" : string.Join("、",attempted)));
        }

        private bool ConfirmInsideMatch()
        {
            return MessageBox.Show("你现在是否已经进入靶场或正式对局，并且画面中能看到武器和 HUD？\r\n\r\n主菜单、选人界面和加载界面都不能确认。未进入请点“否”。",
                "确认已进入 3D 对局",MessageBoxButtons.YesNo,MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2)==DialogResult.Yes;
        }

        private void RepairGameConfig()
        {
            if (Program.UiPreview) return;
            if (restoreJob.IsRunning || switchInProgress) return;
            if (ValorantLocator.IsGameRunning())
            {
                MessageBox.Show("修复前请先完全退出 Valorant，避免游戏覆盖配置。", "游戏正在运行",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                // 若上一次修复会话未完成，先还原，避免把已修改文件当成“原始备份”。
                if(ConfigSessionStore.Exists) ConfigSessionStore.Restore();
                DisplayMode target=switchPage.Presets.SelectedMode;
                if(target==null) throw new InvalidOperationException("请先选择目标分辨率。");
                List<ValorantConfigEntry> configs = ValorantLocator.FindConfigFiles();
                if (configs.Count == 0) throw new InvalidOperationException("没有找到 Valorant 配置文件。");
                List<ConfigRepairRecord> records=new List<ConfigRepairRecord>();
                foreach (ValorantConfigEntry config in configs)
                {
                    FileAttributes attributes=File.GetAttributes(config.Path);
                    string backup=ValorantConfig.Repair(config.Path,target.Width,target.Height,true);
                    records.Add(new ConfigRepairRecord {
                        ConfigPath=config.Path,
                        BackupPath=backup,
                        WasReadOnly=(attributes & FileAttributes.ReadOnly)!=0
                    });
                    ConfigSessionStore.Save(records);
                }
                switchPage.CalibrationButton.Text="配置已修复";
                UpdateStatus("已按目标 " + target.Width + " × " + target.Height + " 修复并锁定 " + configs.Count + " 个配置。");
                MessageBox.Show("配置处理完成。\r\n\r\n已将扫描到的配置统一为目标 "+target.Width+"×"+target.Height+"、全屏窗口＋填充，并在拉伸会话中保持锁定。点击恢复会还原原配置和文件属性。",
                    "修复完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                MessageBox.Show("配置修复失败：" + ex.Message, "Nexa Arena", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BeginCalibration()
        {
            if (restoreJob.IsRunning) return;
            if (TrueStretchStageStore.Exists)
            {
                MessageBox.Show("当前已有真实拉伸会话，请先恢复后再重新校准。", "首次校准",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            try
            {
                DisplayMode target = RequireTarget(true);
                TrueStretchStage stage = PrepareStage(target);
                UpdateStatus("已禁用监视器设备，正在应用中转分辨率…");
                Application.DoEvents();
                DisplayMode bridge = SelectAndApplyBridgeMode(target);
                stage.BridgeWidth=bridge.Width;
                stage.BridgeHeight=bridge.Height;
                if (!ConfirmVisible("中转画面确认", "画面正常请点击“是”保留。之后回到游戏，把显示模式改为“全屏窗口”、纵横比改为“填充”并应用，再返回本工具点击主按钮。\r\n\r\n若屏幕不可见，40 秒后会自动恢复。"))
                {
                    RestoreTrueStretch();
                    return;
                }
                stage.Step = 1;
                TrueStretchStageStore.Save(stage);
                TrueStretchStageStore.ConfirmVisible();
                switchPage.SwitchButton.Text = "校准完成后应用目标";
                UpdateStatus("请回游戏设置“全屏窗口＋填充”并应用；完成后返回，点击主按钮。");
                RefreshDisplayLabels();
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                RestoreTrueStretch();
                MessageBox.Show("校准失败：" + ex.Message, "Nexa Arena", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ApplyTargetAfterCalibration(TrueStretchStage stage)
        {
            if (restoreJob.IsRunning) return;
            try
            {
                if (!ValorantLocator.IsGameRunning())
                    throw new InvalidOperationException("没有检测到 Valorant。请保持游戏在靶场中运行。");
                UpdateStatus("正在应用目标分辨率 " + stage.TargetWidth + " × " + stage.TargetHeight + "…");
                Application.DoEvents();
                SyncStretchSessionConfigs(stage.TargetWidth,stage.TargetHeight);
                SavedState original=SavedState.Load() ?? stage.GetOriginalState();
                int refresh=original==null ? DisplayModeService.GetCurrent().RefreshRate : original.RefreshRate;
                DisplayModeService.ApplyTargetMode(stage.TargetWidth,stage.TargetHeight,refresh);
                stage.Step = 2;
                TrueStretchStageStore.Save(stage);
                TrueStretchStageStore.ConfirmVisible();
                gameWasRunningDuringStretch=true;
                gameMissingTicks=0;
                gameStateLogWatcher.ResetToEnd();
                switchPage.SwitchButton.Text = "真实拉伸已启用";
                switchPage.SwitchButton.BackColor = ModernTheme.Green;
                UpdateStatus("首次校准完成。以后保持游戏为“全屏窗口＋填充”，即可使用一键真实拉伸。");
                RefreshDisplayLabels();
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                RestoreTrueStretch();
                MessageBox.Show("应用目标分辨率失败：" + ex.Message, "Nexa Arena", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void SyncStretchSessionConfigs(int width,int height)
        {
            List<ValorantConfigEntry> configs=ValorantLocator.SelectActiveConfigs(ValorantLocator.FindConfigFiles());
            if(configs.Count==0) throw new InvalidOperationException("没有找到可同步的 Valorant 配置文件。");
            List<ConfigRepairRecord> records=ConfigSessionStore.LoadRecords();
            foreach(ValorantConfigEntry config in configs)
            {
                ConfigRepairRecord existing=records.FirstOrDefault(x=>
                    string.Equals(x.ConfigPath,config.Path,StringComparison.OrdinalIgnoreCase));
                if(existing==null)
                {
                    FileAttributes attributes=File.GetAttributes(config.Path);
                    string backup=ValorantConfig.Repair(config.Path,width,height,true);
                    records.Add(new ConfigRepairRecord {
                        ConfigPath=config.Path,
                        BackupPath=backup,
                        WasReadOnly=(attributes & FileAttributes.ReadOnly)!=0
                    });
                }
                else
                {
                    ValorantConfig.ApplyRuntimeBridge(config.Path,width,height);
                    File.SetAttributes(config.Path,File.GetAttributes(config.Path)|FileAttributes.ReadOnly);
                }
            }
            ConfigSessionStore.Save(records);
        }

        private DisplayMode RequireTarget(bool requireGame)
        {
            DisplayMode target = switchPage.Presets.SelectedMode;
            if (target == null) throw new InvalidOperationException("请先选择目标分辨率。");
            if (requireGame && !ValorantLocator.IsGameRunning())
                throw new InvalidOperationException("没有检测到 Valorant。请先以“全屏＋填充”进入靶场，再返回本工具。");
            int[,] candidates=new int[,] {{1280,800},{1280,960},{1280,768},{1366,768},{1600,900},{1280,720}};
            bool bridgeSupported=false;
            for(int i=0;i<candidates.GetLength(0);i++)
                if(DisplayModeService.IsSupported(candidates[i,0],candidates[i,1])) { bridgeSupported=true; break; }
            if(!bridgeSupported)
                throw new InvalidOperationException("没有检测到可用的中转分辨率；请先在显卡控制面板创建 1280×800。");
            return target;
        }

        private TrueStretchStage PrepareStage(DisplayMode target)
        {
            SavedState original = SavedState.Capture();
            original.Save();
            SavedState verifiedOriginal=SavedState.Load();
            if(verifiedOriginal==null || verifiedOriginal.Width!=original.Width ||
                verifiedOriginal.Height!=original.Height || verifiedOriginal.RefreshRate!=original.RefreshRate)
                throw new InvalidOperationException("原始显示快照写入后校验失败，程序已停止，未禁用监视器。");
            switchPage.OriginalLabel.Text = "原始状态：" + original.Width + " × " + original.Height + " @ " + original.RefreshRate + "Hz";
            List<string> ids = MonitorDeviceService.GetDevices(true).Where(x => x.Started)
                .Select(x => x.InstanceId).ToList();
            if (ids.Count == 0) throw new InvalidOperationException("没有找到已启用的监视器设备。");
            UpdateStatus("正在保存桌面图标位置…");
            DesktopIconLayout.Capture(original);
            TrueStretchStage stage = new TrueStretchStage {
                Step = 0,
                TargetWidth = target.Width,
                TargetHeight = target.Height,
                BridgeWidth = 0,
                BridgeHeight = 0,
                OriginalWidth = original.Width,
                OriginalHeight = original.Height,
                OriginalRefreshRate = original.RefreshRate,
                OriginalBitsPerPixel = original.BitsPerPixel,
                OriginalFixedOutput = original.FixedOutput,
                MonitorIds = ids
            };
            TrueStretchStageStore.Save(stage);
            TrueStretchStageStore.StartWatchdog();
            List<string> actuallyDisabled=MonitorDeviceService.DisableStartedDevices();
            if(actuallyDisabled.Count==0)
                throw new InvalidOperationException("监视器设备没有被实际禁用。");
            stage.MonitorIds=actuallyDisabled;
            TrueStretchStageStore.Save(stage);
            return stage;
        }

        private bool ConfirmVisible(string title, string message)
        {
            return MessageBox.Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question,
                MessageBoxDefaultButton.Button2) == DialogResult.Yes;
        }

        private void KeepChanges()
        {
            rollbackTimer.Stop();
            pendingState = null;
            switchPage.SwitchButton.Text = "切换到目标分辨率";
            switchPage.SwitchButton.BackColor = ModernTheme.Accent;
            UpdateStatus("已保留更改。结束游戏后可恢复原始显示设置。");
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
            switchPage.SwitchButton.Text = "保留更改 · " + secondsLeft + " 秒";
        }

        private void Restore()
        {
            if (Program.UiPreview) return;
            if (restoreJob.IsRunning || switchInProgress) return;
            if (TrueStretchStageStore.Exists || SavedState.Load() != null)
            {
                RestoreTrueStretch();
                return;
            }
            rollbackTimer.Stop();
            SavedState state = pendingState ?? SavedState.Load();
            if (state == null && !ConfigSessionStore.Exists)
            {
                UpdateStatus("没有可恢复的原始显示状态。");
                RefreshDisplayLabels();
                return;
            }
            if (pendingState == null && ValorantLocator.IsGameRunning())
            {
                MessageBox.Show("请先退出 Valorant 再恢复。这样可以避免游戏覆盖已备份的配置。",
                    "请先退出游戏", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                UpdateStatus("正在恢复原始显示设置…");
                Application.DoEvents();
                if (state != null)
                {
                    if (state.MonitorCount > 1) TopologyService.Extend();
                    DisplayModeService.SetReferenceStyle(new DisplayMode(state.Width, state.Height, state.RefreshRate,
                        state.BitsPerPixel <= 0 ? 32 : state.BitsPerPixel, state.FixedOutput));
                    SavedState.Delete();
                }
                ConfigSessionStore.Restore();
                pendingState = null;
                switchPage.OriginalLabel.Text = "尚未保存原始显示状态";
                switchPage.SwitchButton.Text = "切换到目标分辨率";
                switchPage.SwitchButton.BackColor = ModernTheme.Accent;
                UpdateStatus("原始显示设置已恢复。");
            }
            catch (Exception ex)
            {
                ErrorLog.Write(ex);
                if (ConfigSessionStore.Exists) ConfigSessionStore.Restore();
                MessageBox.Show("自动恢复失败：" + ex.Message + "\r\n请使用 Windows 设置手动恢复。",
                    "恢复失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            RefreshDisplayLabels();
        }

        private void RestoreTrueStretch()
        {
            if (Program.UiPreview) return;
            if (restoreJob.IsRunning) return;
            try
            {
                TrueStretchStage stage = TrueStretchStageStore.Load();
                SavedState state = SavedState.Load() ?? pendingState;
                SetRestoreControlsBusy(true);
                UpdateStatus("正在后台恢复显示设置，请稍候…");
                RestoreTrace.Write("Restore queued; stage=" + (stage == null ? "none" : stage.Step.ToString()));
                restoreJob.TryStart(delegate
                {
                    RestoreTrace.Write("Restore worker started");
                    RestoreResult result = RestoreSession.Run(stage, state, new NativeRestoreSessionBackend(),
                        delegate(string message)
                        {
                            RestoreTrace.Write(message);
                            PostRestoreUi(delegate { UpdateStatus(message); });
                        });
                    RestoreTrace.Write(result.Succeeded ? "Restore verified and completed" :
                        "Restore incomplete: " + string.Join("; ", result.Errors));
                    return result;
                }, FinishRestore, PostRestoreUi);
            }
            catch (Exception ex)
            {
                FinishRestore(RestoreResult.Failed(ex));
            }
        }

        private void PostRestoreUi(Action action)
        {
            // The form stays open during recovery so the display-change message loop can run.
            if (IsDisposed || !IsHandleCreated)
                throw new InvalidOperationException("恢复界面已关闭。");
            BeginInvoke(action);
        }

        private void SetRestoreControlsBusy(bool busy)
        {
            foreach (Control page in pages) page.Enabled = !busy;
            if (busy)
            {
                rollbackTimer.Stop();
                displayRefreshTimer.Stop();
                gameExitMonitorTimer.Stop();
            }
            else
            {
                displayRefreshTimer.Start();
                gameExitMonitorTimer.Start();
            }
        }

        private void FinishRestore(RestoreResult result)
        {
            SetRestoreControlsBusy(false);
            gameWasRunningDuringStretch = false;
            gameMissingTicks = 0;
            if (result.Succeeded)
            {
                pendingState = null;
                switchPage.OriginalLabel.Text = "尚未保存原始显示状态";
                switchPage.SwitchButton.Text = "一键真实拉伸";
                switchPage.SwitchButton.BackColor = ModernTheme.Accent;
                switchPage.CalibrationButton.Text = "修复游戏配置";
                UpdateStatus(result.ActualMode == null ? "游戏配置已恢复。" :
                    "已验证恢复到 " + result.ActualMode + "，监视器设备已重新启用。");
                if(result.Warnings.Count>0)
                {
                    UpdateStatus("显示与监视器已恢复；桌面图标还原需要注意。");
                    MessageBox.Show(this,string.Join("\r\n",result.Warnings),"桌面图标提示",MessageBoxButtons.OK,MessageBoxIcon.Information);
                }
                if (closeWhenRestoreCompletes)
                {
                    closingAfterRestore = true;
                    Close();
                    return;
                }
            }
            else
            {
                // No automatic retry loop: leave the snapshot intact and the window open.
                ShowMainWindow();
                closeWhenRestoreCompletes = false;
                ErrorLog.Write(new InvalidOperationException(string.Join("\r\n", result.Errors)));
                UpdateStatus("恢复未完成，已停止自动重试；请检查显示设置后再点恢复。");
                MessageBox.Show("恢复未完成：\r\n\r\n" + string.Join("\r\n", result.Errors) +
                    "\r\n\r\n未强制切换或降低刷新率。详细步骤见 restore.log。", "恢复失败",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                    pendingState.RefreshRate, pendingState.BitsPerPixel <= 0 ? 32 : pendingState.BitsPerPixel,
                    pendingState.FixedOutput));
                SavedState.Delete();
                ConfigSessionStore.Restore();
            }
            catch (Exception ex) { ErrorLog.Write(ex); }
            if (ConfigSessionStore.Exists) ConfigSessionStore.Restore();
            pendingState = null;
        }

        private void RefreshDisplayLabels()
        {
            if (restoreJob.IsRunning) return;
            try
            {
                DisplayMode current = DisplayModeService.GetCurrent();
                switchPage.CurrentLabel.Text = current.ToString();
                headerStatus.Text = string.Format("{0} × {1}  ·  {2} Hz", current.Width, current.Height, current.RefreshRate);
            }
            catch (Exception ex)
            {
                switchPage.CurrentLabel.Text = "无法读取显示设置";
                headerStatus.Text = ex.Message;
            }
        }

        private void UpdateStatus(string text)
        {
            switchPage.StatusLabel.Text = text;
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (Program.UiPreview) return;
            if (restoreJob.IsRunning || switchInProgress)
            {
                e.Cancel = true;
                closeWhenRestoreCompletes = true;
                UpdateStatus("正在完成显示操作，恢复完成后自动关闭；恢复失败将保留窗口。");
                return;
            }
            if (!closingAfterRestore && (TrueStretchStageStore.Exists || SavedState.Load() != null ||
                ConfigSessionStore.Exists))
            {
                e.Cancel = true;
                closeWhenRestoreCompletes = true;
                RestoreTrueStretch();
                return;
            }
            DisplayFilterService.Restore();
            displayRefreshTimer.Stop();
            gameExitMonitorTimer.Stop();
            gameStateMonitorTimer.Stop();
        }
    }
}
