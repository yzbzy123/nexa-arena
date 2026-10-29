using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace NexaArena
{
    internal sealed class NativeChoiceBox:ComboBox
    {
        internal NativeChoiceBox(){DrawMode=DrawMode.OwnerDrawFixed;ItemHeight=24;}
        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if(e.Bounds.Width<=0||e.Bounds.Height<=0)return;e.DrawBackground();
            string text=e.Index>=0&&e.Index<Items.Count?GetItemText(Items[e.Index]):Text;
            TextRenderer.DrawText(e.Graphics,text,Font,Rectangle.Inflate(e.Bounds,-4,0),Enabled?((e.State&DrawItemState.Selected)!=0?SystemColors.HighlightText:ForeColor):SystemColors.GrayText,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            e.DrawFocusRectangle();base.OnDrawItem(e);
        }
    }
    internal sealed class NativeGamePage:Panel
    {
        private readonly Control first,second;
        internal NativeGamePage(Control first,Control second)
        {this.first=first;this.second=second;Dock=DockStyle.Fill;Margin=new Padding(0);first.Dock=second.Dock=DockStyle.Fill;Controls.Add(first);Controls.Add(second);SelectGame("VALORANT");}
        internal void SelectGame(string game){first.Visible=game=="VALORANT";second.Visible=!first.Visible;(first.Visible?first:second).BringToFront();}
    }
    internal sealed class NativeAspectPreview:Control
    {
        private readonly Image model;
        internal int TargetWidth=1440,TargetHeight=1080;
        internal static double StretchFactor(int width,int height)
        {if(width<=0||height<=0)throw new ArgumentException("预览比例无效。");return (16.0/9)/(width/(double)height);}
        internal NativeAspectPreview()
        {
            DoubleBuffered=true;ResizeRedraw=true;Dock=DockStyle.Fill;MinimumSize=new Size(240,165);
            using(var stream=typeof(NativeAspectPreview).Assembly.GetManifestResourceStream("NexaArena.AspectModel"))
                if(stream!=null)using(var source=Image.FromStream(stream))model=new Bitmap(source);
            AccessibleName="同一人物在 16:9 与所选分辨率全屏拉伸后的比例对照；不模拟游戏视野";
        }
        protected override void Dispose(bool disposing){if(disposing&&model!=null)model.Dispose();base.Dispose(disposing);}
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);var g=e.Graphics;g.Clear(Color.White);g.InterpolationMode=InterpolationMode.HighQualityBicubic;
            float gap=16,w=(ClientSize.Width-gap)/2F,h=Math.Min(w*9/16F,Math.Max(55,ClientSize.Height-74));
            float top=Math.Max(28,(ClientSize.Height-h-32)/2F);
            double factor=StretchFactor(TargetWidth,TargetHeight);
            float imageHeight=(float)(h*.98/Math.Max(1,factor*.62));
            for(int i=0;i<2;i++)
            {
                var frame=new RectangleF(i*(w+gap),top,w,h);
                using(var bg=new SolidBrush(Color.FromArgb(232,236,240)))g.FillRectangle(bg,frame);
                using(var p=new Pen(ModernTheme.Border))g.DrawRectangle(p,frame.X,frame.Y,frame.Width,frame.Height);
                var clip=g.Save();g.SetClip(frame);
                if(model!=null){float iw=imageHeight*model.Width/model.Height*(i==0?1:(float)factor);g.DrawImage(model,new RectangleF(frame.X+(frame.Width-iw)/2,frame.Bottom-imageHeight,iw,imageHeight));}
                else TextRenderer.DrawText(g,"人物预览未载入",Font,Rectangle.Round(frame),ModernTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                g.Restore(clip);
                string title=i==0?"16:9 参照":(TargetWidth*3==TargetHeight*4?"4:3":TargetWidth*4==TargetHeight*5?"5:4":"所选比例")+" 拉伸";
                TextRenderer.DrawText(g,title,Font,new Rectangle((int)frame.X,(int)top-28,(int)w,25),ModernTheme.Text,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
                string caption=i==0?"人物正常比例":"横向 × "+factor.ToString("0.###");
                TextRenderer.DrawText(g,caption,Font,new Rectangle((int)frame.X,(int)frame.Bottom+7,(int)w,25),ModernTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
            }
        }
    }
    internal sealed class NativeDisplayLayout:UserControl
    {
        internal readonly ModernPresetPanel Presets=new ModernPresetPanel();
        internal readonly ComboBox Targets=ToolUi.Choice(),Configs=ToolUi.Choice(),Intermediate=ToolUi.Choice();
        internal readonly TextBox WidthInput=new TextBox {Text="1440"},HeightInput=new TextBox {Text="1080"};
        internal readonly Button Repair,Apply,Restore;
        internal readonly CheckBox AutoRestore=ToolUi.Check("退出游戏后自动恢复分辨率、监视器和图标",true);
        internal readonly Label Current=ToolUi.Text("正在读取显示模式…"),Original=ToolUi.Text("尚未保存桌面状态"),Status=ToolUi.Text("先核对配置文件，再执行修复。"),Shortcuts=ToolUi.Text("Ctrl + Alt + S 切换  ·  Ctrl + Alt + H 恢复"),ConfigHint=ToolUi.Text("");
        internal readonly NativeAspectPreview Preview=new NativeAspectPreview();
        internal event EventHandler AddMode;
        private readonly ConfigSelection selection=Program.UiPreview?null:new ConfigSelection();
        private bool syncing;
        private readonly WorkbenchSteps progress=new WorkbenchSteps();
        private readonly System.Collections.Generic.List<RadioButton> presetChoices=new System.Collections.Generic.List<RadioButton>();
        internal string SelectedConfig {get{var item=Configs.SelectedItem as ValorantConfigEntry;return item==null?null:item.Path;}}
        private static Panel Surface(){return new Panel {Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(16),Margin=new Padding(0,0,12,0),BorderStyle=BorderStyle.FixedSingle};}
        private static Label Heading(string text){return ModernTheme.Label(text,11F,FontStyle.Bold,ModernTheme.Text);}
        internal NativeDisplayLayout()
        {
            Dock=DockStyle.Fill;BackColor=Color.White;Padding=new Padding(22,12,22,16);
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Margin=new Padding(0)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,76));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,44));Controls.Add(root);
            progress.Margin=new Padding(0,0,0,14);root.Controls.Add(progress,0,0);
            var body=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};
            body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,65));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));root.Controls.Add(body,0,1);
            var left=new ModernCard {Dock=DockStyle.Fill,Margin=new Padding(0,0,14,0),Padding=new Padding(18)};
            var right=new ModernCard {Dock=DockStyle.Fill,Margin=new Padding(0),Padding=new Padding(18),BackColor=ModernTheme.Background};body.Controls.Add(left,0,0);body.Controls.Add(right,1,0);
            var fields=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=7,Margin=new Padding(0)};
            foreach(int h in new[]{32,78,0,24,28,42,36})fields.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));left.Controls.Add(fields);
            fields.Controls.Add(Heading("目标分辨率"),0,0);
            var presets=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0,0,0,8)};
            presets.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            for(int i=0;i<3;i++)presets.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,33.333F));
            int column=0;foreach(var mode in new[]{new Size(1280,960),new Size(1280,1024),new Size(1440,1080)})
            {var size=mode;var radio=new WorkbenchPresetChoice {Tag=mode,AccessibleName=mode.Width+"乘"+mode.Height};if(column==2)radio.Margin=new Padding(0);radio.CheckedChanged+=delegate{if(radio.Checked&&!syncing)Presets.SelectMode(size.Width,size.Height);};presetChoices.Add(radio);presets.Controls.Add(radio,column++,0);}
            fields.Controls.Add(presets,0,1);fields.Controls.Add(Preview,0,2);
            var note=ToolUi.Text("同一人物等高对照 · 仅示意比例，不代表实际游戏视野",8.5F);note.ForeColor=ModernTheme.Muted;fields.Controls.Add(note,0,3);fields.Controls.Add(Heading("自定义分辨率"),0,4);
            var custom=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=5,RowCount=1,Margin=new Padding(0)};
            custom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,36));custom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));custom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,44));custom.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));custom.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,104));
            WidthInput.AccessibleName="自定义宽度";HeightInput.AccessibleName="自定义高度";WidthInput.Dock=HeightInput.Dock=DockStyle.Top;WidthInput.Font=HeightInput.Font=ModernTheme.Font(10F,FontStyle.Regular);WidthInput.Margin=HeightInput.Margin=new Padding(0,8,10,0);
            custom.Controls.Add(ToolUi.Text("宽度",9F),0,0);custom.Controls.Add(WidthInput,1,0);custom.Controls.Add(ToolUi.Text("高度",9F),2,0);custom.Controls.Add(HeightInput,3,0);
            var add=ToolUi.Button("添加模式",delegate{if(AddMode!=null)AddMode(this,EventArgs.Empty);});add.Dock=DockStyle.Fill;custom.Controls.Add(add,4,0);fields.Controls.Add(custom,0,5);
            var allModes=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};allModes.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,76));allModes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));allModes.Controls.Add(ToolUi.Text("全部模式",9F),0,0);Targets.Dock=DockStyle.Top;allModes.Controls.Add(Targets,1,0);fields.Controls.Add(allModes,0,6);
            var operations=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=11,Margin=new Padding(0)};
            foreach(int h in new[]{32,26,34,38,42,44,48,26,42,28,0})operations.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));right.Controls.Add(operations);
            operations.Controls.Add(Heading("配置与操作"),0,0);operations.Controls.Add(ToolUi.Text("游戏配置文件",9F),0,1);Configs.Dock=DockStyle.Top;Configs.DropDownWidth=700;operations.Controls.Add(Configs,0,2);
            Button scan=ToolUi.Button("重新扫描",RefreshConfigs),pick=ToolUi.Button("选择文件",PickConfig);operations.Controls.Add(ToolUi.Buttons(scan,pick),0,3);ConfigHint.Font=ModernTheme.Font(8.5F,FontStyle.Regular);ConfigHint.ForeColor=ModernTheme.Muted;operations.Controls.Add(ConfigHint,0,4);
            Repair=ToolUi.Button("修复所选配置",delegate{});Apply=ToolUi.Button("开始拉伸",delegate{});Restore=ToolUi.Button("恢复原始显示",delegate{});
            foreach(var button in new[]{Repair,Apply,Restore}){button.Dock=DockStyle.Fill;button.Margin=new Padding(0,3,0,5);}
            operations.Controls.Add(Repair,0,5);operations.Controls.Add(Apply,0,6);
            AutoRestore.Text="游戏退出后自动恢复";AutoRestore.Font=ModernTheme.Font(9F,FontStyle.Regular);operations.Controls.Add(AutoRestore,0,7);
            operations.Controls.Add(Restore,0,8);Original.ForeColor=ModernTheme.Muted;Original.Font=ModernTheme.Font(8.5F,FontStyle.Regular);operations.Controls.Add(Original,0,9);Status.Font=ModernTheme.Font(9F,FontStyle.Regular);operations.Controls.Add(Status,0,10);
            Shortcuts.Visible=true;Shortcuts.BackColor=ModernTheme.Background;Shortcuts.Padding=new Padding(14,0,0,0);Shortcuts.ForeColor=ModernTheme.Muted;Shortcuts.Font=ModernTheme.Font(9F,FontStyle.Regular);Shortcuts.Margin=new Padding(0,10,0,0);root.Controls.Add(Shortcuts,0,2);
            Presets.HeightChangedByItems+=delegate{SyncTargets();};Presets.SelectedModeChanged+=delegate{SyncTargets();UpdateState(false,false);};
            Targets.SelectedIndexChanged+=delegate{var mode=Targets.SelectedItem as DisplayMode;if(mode!=null&&!syncing)Presets.SelectMode(mode.Width,mode.Height);};
            Configs.SelectedIndexChanged+=delegate{if(!syncing&&SelectedConfig!=null&&!Program.UiPreview){try{selection.Select(SelectedConfig);ConfigHint.Text=SelectedConfig;}catch(Exception ex){ConfigHint.Text=ex.Message;}}UpdateState(false,false);};
            if(Program.UiPreview){scan.Enabled=pick.Enabled=add.Enabled=false;Configs.Items.Add("未找到游戏配置（示意）");Configs.SelectedIndex=0;ConfigHint.Text="先登录并进入游戏，再退出后扫描；也可手动选择文件。";}
            else HandleCreated+=delegate{BeginInvoke((Action)RefreshConfigs);};
        }
        private void SyncTargets()
        {
            if(syncing)return;syncing=true;
            try
            {
                var selected=Presets.SelectedMode;Targets.Items.Clear();foreach(Button button in Presets.Controls){var mode=button.Tag as DisplayMode;if(mode!=null)Targets.Items.Add(mode);}
                if(selected!=null){for(int i=0;i<Targets.Items.Count;i++){var mode=(DisplayMode)Targets.Items[i];if(mode.Width==selected.Width&&mode.Height==selected.Height)Targets.SelectedIndex=i;}foreach(var radio in presetChoices){var size=(Size)radio.Tag;radio.Checked=size.Width==selected.Width&&size.Height==selected.Height;}Preview.TargetWidth=selected.Width;Preview.TargetHeight=selected.Height;Preview.Invalidate();}
            }
            finally{syncing=false;}
        }
        internal void RefreshConfigs()
        {
            if(Program.UiPreview)return;string previous=SelectedConfig??selection.SelectedPath;syncing=true;
            try{Configs.Items.Clear();foreach(var item in ValorantLocator.FindConfigFiles())Configs.Items.Add(item);int chosen=-1;for(int i=0;i<Configs.Items.Count;i++)if(string.Equals(((ValorantConfigEntry)Configs.Items[i]).Path,previous,StringComparison.OrdinalIgnoreCase))chosen=i;Configs.SelectedIndex=chosen>=0?chosen:Configs.Items.Count>0?0:-1;
                ConfigHint.Text=Configs.Items.Count==0?"未找到游戏配置。先登录并进入游戏，再退出后扫描；也可手动选择文件。":"找到 "+Configs.Items.Count+" 个配置；请确认所选路径属于当前账号。";}
            catch(Exception ex){ConfigHint.Text="查找未完成："+ex.Message+"；可以手动选择文件。";}
            finally{syncing=false;UpdateState(false,false);}
        }
        private void PickConfig()
        {
            if(Program.UiPreview)return;
            using(var picker=new OpenFileDialog {Title="选择本次账号的 VALORANT 配置",Filter="VALORANT 配置|GameUserSettings.ini",FileName="GameUserSettings.ini",CheckFileExists=true})
                if(picker.ShowDialog(this)==DialogResult.OK){selection.Select(picker.FileName);RefreshConfigs();ConfigHint.Text=selection.SelectedPath;}
        }
        internal void UpdateState(bool busy,bool restored)
        {
            bool repaired=!Program.UiPreview&&ConfigSessionStore.Exists&&ConfigSessionStore.LoadRecords().Any(x=>string.Equals(x.ConfigPath,SelectedConfig,StringComparison.OrdinalIgnoreCase)),session=!Program.UiPreview&&(SavedState.Load()!=null||TrueStretchStageStore.Exists||ConfigSessionStore.Exists),selected=SelectedConfig!=null;
            Repair.Enabled=!Program.UiPreview&&!busy&&selected;Apply.Enabled=!Program.UiPreview&&!busy&&repaired;Restore.Enabled=!Program.UiPreview&&!busy&&(session||repaired);
            Repair.BackColor=repaired?Color.White:ModernTheme.Text;Repair.ForeColor=repaired?ModernTheme.Text:Color.White;Apply.BackColor=repaired?ModernTheme.Text:Color.White;Apply.ForeColor=repaired?Color.White:ModernTheme.Text;
            var stage=Program.UiPreview?null:TrueStretchStageStore.Load();
            progress.Update(repaired,stage!=null&&stage.Step>=2,session,restored);
        }
    }
}
