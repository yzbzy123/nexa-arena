using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace NexaArena
{
    internal static class ToolUi
    {
        public static Label Text(string text,float size=9F)
        {
            Label label=ModernTheme.Label(text,size,FontStyle.Regular,ModernTheme.Text);
            label.AutoEllipsis=false;return label;
        }
        public static ModernCard Card(string title,string description,int height,out Panel body)
        {
            Label hint;
            return Card(title,description,height,out body,out hint);
        }
        public static ModernCard Card(string title,string description,int height,out Panel body,out Label hint)
        {
            ModernCard card=new ModernCard {Height=height};
            body=new Panel {Dock=DockStyle.Fill,Margin=new Padding(0)};
            Label heading=ModernTheme.Label(title,11F,FontStyle.Bold,ModernTheme.Text);heading.Dock=DockStyle.Top;heading.Height=32;
            hint=Text(description,8.5F);hint.ForeColor=ModernTheme.Muted;hint.Dock=DockStyle.Top;hint.Height=50;
            card.Controls.Add(body);card.Controls.Add(hint);card.Controls.Add(heading);return card;
        }
        public static TableLayoutPanel Grid(params int[] heights)
        {
            var grid=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=heights.Length,Margin=new Padding(0)};
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,36));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,64));
            foreach(int h in heights)grid.RowStyles.Add(new RowStyle(h==0?SizeType.Percent:SizeType.Absolute,h==0?100:h));return grid;
        }
        public static void Field(TableLayoutPanel grid,int row,string name,Control control)
        {
            grid.Controls.Add(Text(name),0,row);control.Dock=DockStyle.Fill;control.Margin=new Padding(0,5,0,5);grid.Controls.Add(control,1,row);
        }
        public static void Wide(TableLayoutPanel grid,int row,Control control)
        {
            control.Dock=DockStyle.Fill;grid.Controls.Add(control,0,row);grid.SetColumnSpan(control,2);
        }
        public static ComboBox Choice(params string[] choices)
        {
            ComboBox box=new ComboBox {DropDownStyle=ComboBoxStyle.DropDownList,Font=ModernTheme.Font(9F,FontStyle.Regular)};
            box.FlatStyle=FlatStyle.Flat;box.BackColor=ModernTheme.Background;box.ForeColor=ModernTheme.Text;
            box.Items.AddRange(choices);if(box.Items.Count>0)box.SelectedIndex=0;return box;
        }
        public static NumericUpDown Number(decimal min,decimal max,decimal value,int decimals=0)
        {
            return new NumericUpDown {Minimum=min,Maximum=max,Value=Math.Max(min,Math.Min(max,value)),DecimalPlaces=decimals,
                Increment=decimals>0?0.01m:1m,ThousandsSeparator=false,Font=ModernTheme.Font(10F,FontStyle.Regular),BackColor=ModernTheme.Background,ForeColor=ModernTheme.Text,BorderStyle=BorderStyle.FixedSingle};
        }
        public static CheckBox Check(string text,bool selected=false) {return new CheckBox {Text=text,Checked=selected,Dock=DockStyle.Fill,AutoSize=false,Margin=new Padding(0)};}
        public static Button Button(string text,Action action)
        {
            Button button=ModernTheme.Button(text,ModernTheme.Accent,Color.White);button.Dock=DockStyle.Fill;button.Margin=new Padding(3);
            button.Click+=delegate {try{action();}catch(Exception ex){MessageBox.Show(ex.Message,"Nexa Arena",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};return button;
        }
        public static TableLayoutPanel Buttons(params Button[] buttons)
        {
            var row=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=buttons.Length,RowCount=1,Margin=new Padding(0)};
            for(int i=0;i<buttons.Length;i++){if(i>0){buttons[i].BackColor=Color.White;buttons[i].ForeColor=ModernTheme.Text;buttons[i].FlatAppearance.BorderSize=1;buttons[i].FlatAppearance.BorderColor=ModernTheme.Border;}row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100F/buttons.Length));row.Controls.Add(buttons[i],i,0);}return row;
        }
        public static TextBox Output() {return new TextBox {Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,WordWrap=false,Dock=DockStyle.Fill,Font=new Font("Consolas",9F),BackColor=Color.White};}
        public static void Copy(string text){if(!string.IsNullOrEmpty(text))Clipboard.SetText(text);}
        public static void ShowNotices(IWin32Window owner)
        {
            using(var dialog=new Form {Text="第三方组件与授权说明",Size=new Size(720,520),StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false})
            {
                TextBox text=Output();text.WordWrap=true;text.ScrollBars=ScrollBars.Vertical;
                using(Stream stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("NexaArena.ThirdPartyNotices"))
                    text.Text=stream==null?"开发构建未包含授权资源，请参阅 assets/ThirdPartyNotices.txt。":new StreamReader(stream).ReadToEnd();
                dialog.Controls.Add(text);dialog.ShowDialog(owner);
            }
        }
    }

    internal sealed class ToolTabs : UserControl
    {
        internal readonly Control[] ModulePages;
        public ToolTabs(string[] names,params Control[] pages)
        {
            Dock=DockStyle.Fill;ModulePages=pages;
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,RowCount=2,ColumnCount=1,Margin=new Padding(0)};
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,52));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));Controls.Add(root);
            var bar=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(24,6,0,6),Margin=new Padding(0),BackColor=ModernTheme.Background,WrapContents=false};
            var host=new Panel {Dock=DockStyle.Fill,Margin=new Padding(0)};root.Controls.Add(bar,0,0);root.Controls.Add(host,0,1);
            Button[] tabs=new Button[pages.Length];
            Action<int> select=delegate(int selected){for(int j=0;j<pages.Length;j++){pages[j].Visible=j==selected;tabs[j].BackColor=j==selected?ModernTheme.AccentSoft:ModernTheme.Background;tabs[j].ForeColor=j==selected?ModernTheme.Accent:ModernTheme.Muted;}pages[selected].BringToFront();};
            for(int i=0;i<pages.Length;i++){int index=i;pages[i].Dock=DockStyle.Fill;host.Controls.Add(pages[i]);tabs[i]=ModernTheme.Button(names[i],ModernTheme.Background,ModernTheme.Muted);tabs[i].Size=new Size(146,38);tabs[i].Margin=new Padding(0,0,8,0);tabs[i].Click+=delegate{select(index);};bar.Controls.Add(tabs[i]);}select(0);
        }
    }

    internal sealed class SensitivityPage : UserControl
    {
        private readonly ComboBox source,target,sourceResolution,targetResolution,formula;
        private readonly NumericUpDown dpi,targetDpi,sensitivity,customRatio;
        private readonly Label result,details,methodHint,customRatioLabel;
        private readonly ModernCard inputCard;
        private readonly TableLayoutPanel inputGrid;
        private readonly Label resolutionHint;
        private readonly Button copyResult,swapDirection,saveInputs;
        private bool swapping;
        private double converted;
        public SensitivityPage(Func<ToolboxSettings> read,Action<ToolboxSettings> save)
        {
            Dock=DockStyle.Fill;var stack=new ResponsiveStack();Controls.Add(stack);Panel body;
            inputCard=ToolUi.Card("配置换算","",366,out body,out resolutionHint);stack.Controls.Add(inputCard);
            ToolboxSettings settings=read();source=ToolUi.Choice("CS2","VALORANT");target=ToolUi.Choice("CS2","VALORANT");source.SelectedItem=settings.SourceGame;target.SelectedItem=settings.TargetGame;
            formula=ToolUi.Choice(SensitivityFormulas.Names);formula.SelectedIndex=Array.IndexOf(SensitivityFormulas.Ids,settings.SensitivityMethod);
            customRatio=ToolUi.Number(0.01m,100,settings.CustomCsPerValorant,6);
            sourceResolution=ResolutionBox(settings.SourceResolution);targetResolution=ResolutionBox(settings.TargetResolution);
            dpi=ToolUi.Number(50,100000,settings.SourceDpi);targetDpi=ToolUi.Number(50,100000,settings.TargetDpi);sensitivity=ToolUi.Number(0.000001m,100,settings.Sensitivity,6);
            inputGrid=ToolUi.Grid(36,132,36,0,40);body.Controls.Add(inputGrid);
            // A hidden custom-parameter row is absolute zero, not a percentage filler row.
            inputGrid.RowStyles[3].SizeType=SizeType.Absolute;
            ToolUi.Field(inputGrid,0,"换算方案",formula);
            var sides=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};
            sides.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));sides.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
            TableLayoutPanel left=Side("来源设置",source,sourceResolution,dpi),right=Side("目标设置",target,targetResolution,targetDpi);
            left.Margin=new Padding(0,0,10,0);right.Margin=new Padding(10,0,0,0);sides.Controls.Add(left,0,0);sides.Controls.Add(right,1,0);ToolUi.Wide(inputGrid,1,sides);
            ToolUi.Field(inputGrid,2,"来源游戏灵敏度",sensitivity);
            customRatioLabel=ToolUi.Text("经验比值（CS / 瓦）");
            inputGrid.Controls.Add(customRatioLabel,0,3);
            customRatio.Dock=DockStyle.Fill;customRatio.Margin=new Padding(0,5,0,5);inputGrid.Controls.Add(customRatio,1,3);
            copyResult=ToolUi.Button("复制换算结果",delegate{ToolUi.Copy(SensitivityProjection.DisplayNumber(converted));copyResult.Text="已复制结果";});
            swapDirection=ToolUi.Button("互换来源与目标",SwapDirection);
            saveInputs=ToolUi.Button("保存当前输入",delegate
            {
                ToolboxSettings next=read().Copy();next.SourceGame=(string)source.SelectedItem;next.TargetGame=(string)target.SelectedItem;
                next.SourceResolution=((SensitivityResolution)sourceResolution.SelectedItem).Key;next.TargetResolution=((SensitivityResolution)targetResolution.SelectedItem).Key;
                next.SensitivityMethod=SensitivityFormulas.Ids[formula.SelectedIndex];next.CustomCsPerValorant=customRatio.Value;
                next.SourceDpi=dpi.Value;next.TargetDpi=targetDpi.Value;next.Sensitivity=sensitivity.Value;save(next);
                saveInputs.Text="输入已保存";
            });
            ToolUi.Wide(inputGrid,4,ToolUi.Buttons(copyResult,swapDirection,saveInputs));
            var output=ToolUi.Card("参考结果","",226,out body,out methodHint);stack.Controls.Add(output);
            output.BackColor=ModernTheme.Navy;
            foreach(Label label in output.Controls.OfType<Label>())label.ForeColor=Color.FromArgb(171,194,179);
            var values=ToolUi.Grid(56,48);body.Controls.Add(values);result=ModernTheme.Label("",25F,FontStyle.Bold,Color.FromArgb(192,237,170));details=ToolUi.Text("");details.ForeColor=Color.FromArgb(186,207,193);ToolUi.Wide(values,0,result);ToolUi.Wide(values,1,details);
            source.SelectedIndexChanged+=delegate{Calculate();};target.SelectedIndexChanged+=delegate{Calculate();};
            formula.SelectedIndexChanged+=delegate{Calculate();};customRatio.ValueChanged+=delegate{Calculate();};
            sourceResolution.SelectedIndexChanged+=delegate{Calculate();};targetResolution.SelectedIndexChanged+=delegate{Calculate();};
            dpi.ValueChanged+=delegate{Calculate();};targetDpi.ValueChanged+=delegate{Calculate();};sensitivity.ValueChanged+=delegate{Calculate();};Calculate();
        }
        private static ComboBox ResolutionBox(string selected)
        {
            ComboBox box=ToolUi.Choice();box.DropDownWidth=240;box.MaxDropDownItems=12;
            box.Items.AddRange(SensitivityProjection.Presets);box.SelectedItem=SensitivityProjection.Resolve(selected);return box;
        }
        private static TableLayoutPanel Side(string title,ComboBox game,ComboBox resolution,NumericUpDown dpi)
        {
            var grid=ToolUi.Grid(24,36,36,36);
            ToolUi.Wide(grid,0,ModernTheme.Label(title,9.5F,FontStyle.Bold,ModernTheme.Text));
            ToolUi.Field(grid,1,"游戏",game);ToolUi.Field(grid,2,"分辨率",resolution);ToolUi.Field(grid,3,"鼠标 DPI",dpi);return grid;
        }
        private void Calculate()
        {
            if(result==null||swapping)return;string a=(string)source.SelectedItem,b=(string)target.SelectedItem;
            copyResult.Text="复制换算结果";saveInputs.Text="保存当前输入";
            var from=(SensitivityResolution)sourceResolution.SelectedItem;var to=(SensitivityResolution)targetResolution.SelectedItem;
            string method=SensitivityFormulas.Ids[formula.SelectedIndex];
            bool custom=method==SensitivityFormulas.CustomRatio;
            inputGrid.SuspendLayout();
            float scale=DeviceDpi/96F;
            inputGrid.RowStyles[3].Height=custom?36*scale:0;
            customRatioLabel.Visible=custom;customRatio.Visible=custom;
            customRatio.Enabled=custom&&a!=b;
            inputCard.Height=(int)Math.Round((custom?402:366)*scale);
            inputGrid.ResumeLayout();
            sourceResolution.Enabled=targetResolution.Enabled=method==SensitivityFormulas.ScreenCenter;
            resolutionHint.Text=method==SensitivityFormulas.ScreenCenter?"4:3、5:4 等比例默认全屏拉伸；修改左右设置后，结果会实时更新。":"此方案只使用灵敏度、DPI"+(custom?"和经验比值":"")+"；分辨率不参与计算，因此暂不可编辑。";
            converted=SensitivityFormulas.Convert(method,a,b,from,to,(double)sensitivity.Value,(double)dpi.Value,(double)targetDpi.Value,(double)customRatio.Value);
            result.Text=b+"  "+SensitivityProjection.DisplayNumber(converted);
            if(method==SensitivityFormulas.TurnDistance)
                methodHint.Text="保持相同的转身鼠标距离；CS2 使用默认 m_yaw。不按分辨率修正，不保证拉伸后的视觉手感一致。";
            else if(method==SensitivityFormulas.ScreenCenter)
                methodHint.Text="同屏未开镜水平微调，CS2 默认 m_yaw；瓦非 16:9 需真实拉伸。无法同时保证所有甩枪和转身距离一致。";
            else
                methodHint.Text=a==b?"同一游戏仅按 DPI 换算，跨游戏经验比值不参与；不额外修正视野。":
                    "个人经验参数，非通用推荐；比值始终表示同 DPI 下的 CS / 瓦，反向自动换算，不再叠加视野修正。";
            details.Text=(method==SensitivityFormulas.ScreenCenter?a+" "+from.Ratio+" → "+b+" "+to.Ratio+" · 全屏拉伸":a+" → "+b+" · 本方案不使用分辨率修正")+
                "\r\n当前组合倍率（含 DPI）：×"+SensitivityProjection.DisplayNumber(converted/(double)sensitivity.Value);
        }

        private void SwapDirection()
        {
            // Reuse the calculated result as the new input so reversing direction is meaningful.
            decimal next=decimal.Round((decimal)converted,sensitivity.DecimalPlaces);
            if(next<sensitivity.Minimum||next>sensitivity.Maximum)
                throw new InvalidOperationException("换算结果超出可输入范围，无法自动互换；请手动填写目标灵敏度。");
            swapping=true;
            try
            {
                object game=source.SelectedItem,resolution=sourceResolution.SelectedItem;decimal oldDpi=dpi.Value;
                source.SelectedItem=target.SelectedItem;sourceResolution.SelectedItem=targetResolution.SelectedItem;dpi.Value=targetDpi.Value;
                target.SelectedItem=game;targetResolution.SelectedItem=resolution;targetDpi.Value=oldDpi;
                sensitivity.Value=next;
            }
            finally {swapping=false;Calculate();}
        }

        protected override void OnDpiChangedAfterParent(EventArgs e) {base.OnDpiChangedAfterParent(e);Calculate();}
    }

    internal sealed class Cs2CommandsPage : UserControl
    {
        private readonly NumericUpDown fps;
        private readonly CheckBox showFps,practice,ammo,buyAnywhere,grenade;
        private readonly ComboBox key;
        private readonly CheckedListBox items;
        private readonly TextBox output;
        public Cs2CommandsPage()
        {
            Dock=DockStyle.Fill;var stack=new ResponsiveStack();Controls.Add(stack);Panel body;
            stack.Controls.Add(ToolUi.Card("CS2 指令生成器","仅生成文本，不向游戏发送按键，不注入进程，不自动改写 autoexec。练习参数仅用于本地练习房。",510,out body));
            var grid=ToolUi.Grid(40,30,30,30,30,30,40,100,44);body.Controls.Add(grid);
            fps=ToolUi.Number(0,1000,300);ToolUi.Field(grid,0,"FPS 上限（0＝不限）",fps);
            showFps=ToolUi.Check("显示游戏内 FPS");practice=ToolUi.Check("加入本地练习房参数（包含 sv_cheats 1）");ammo=ToolUi.Check("无限弹药（无需换弹）",true);buyAnywhere=ToolUi.Check("练习房任意位置购买",true);grenade=ToolUi.Check("练习房投掷物预览",true);
            ToolUi.Wide(grid,1,showFps);ToolUi.Wide(grid,2,practice);ToolUi.Wide(grid,3,ammo);ToolUi.Wide(grid,4,buyAnywhere);ToolUi.Wide(grid,5,grenade);
            key=ToolUi.Choice("F6","F7","F8","F9","F10","F11","B","V","C","X","Z");ToolUi.Field(grid,6,"一键购买绑定",key);
            items=new CheckedListBox {Dock=DockStyle.Fill,CheckOnClick=true,MultiColumn=true,ColumnWidth=160,IntegralHeight=false};items.Items.AddRange(Cs2Commands.ItemNames);ToolUi.Wide(grid,7,items);
            ToolUi.Wide(grid,8,ToolUi.Button("生成指令",Generate));
            stack.Controls.Add(ToolUi.Card("预览与导出","先阅读生成内容，再复制到控制台，或保存为 .cfg。只生成你勾选的购买物品；未勾选则不改绑定。",350,out body));
            var preview=ToolUi.Grid(0,44);body.Controls.Add(preview);output=ToolUi.Output();ToolUi.Wide(preview,0,output);
            ToolUi.Wide(preview,1,ToolUi.Buttons(ToolUi.Button("复制指令",delegate{ToolUi.Copy(output.Text);}),ToolUi.Button("导出 .cfg",delegate
            {
                if(Program.UiPreview)throw new InvalidOperationException("预览模式不导出文件。");
                using(var dialog=new SaveFileDialog {Filter="CS2 配置 (*.cfg)|*.cfg",FileName="nexaarena.cfg",DefaultExt="cfg",AddExtension=true,OverwritePrompt=true})
                    if(dialog.ShowDialog(this)==DialogResult.OK)File.WriteAllText(dialog.FileName,output.Text,new UTF8Encoding(false));
            })));
            stack.Controls.Add(ToolUi.Card("用途与还原","fps_max：帧率限制；cl_showfps：帧率显示；bind + buy：购买绑定；练习项用于延长回合、弹药和投掷物练习。",255,out body));body.Controls.Add(ToolUi.Text(Cs2Commands.RestoreHelp,8.5F));
            practice.CheckedChanged+=delegate{ammo.Enabled=buyAnywhere.Enabled=grenade.Enabled=practice.Checked;};ammo.Enabled=buyAnywhere.Enabled=grenade.Enabled=false;Generate();
        }
        private void Generate()
        {
            output.Text=Cs2Commands.Generate(new Cs2CommandOptions {FpsLimit=(int)fps.Value,ShowFps=showFps.Checked,Practice=practice.Checked,UnlimitedAmmo=ammo.Checked,BuyAnywhere=buyAnywhere.Checked,GrenadePreview=grenade.Checked,BuyKey=(string)key.SelectedItem,Items=items.CheckedIndices.Cast<int>().Select(i=>Cs2Commands.ItemIds[i]).ToArray()});
        }
    }

    internal sealed class ToolboxSettingsPage : UserControl
    {
        private readonly DataGridView keys;
        private readonly CheckBox tray;
        private readonly Label status;
        public void SetStatus(string text){status.Text=text;}
        public ToolboxSettingsPage(Func<ToolboxSettings> read,Action<ToolboxSettings> save)
        {
            Dock=DockStyle.Fill;var stack=new ResponsiveStack();Controls.Add(stack);Panel body;
            stack.Controls.Add(ToolUi.Card("托盘与快捷键","最小化可驻留托盘。关闭按钮仍按原逻辑恢复显示后退出，不会悄悄转到后台。",460,out body));
            var grid=ToolUi.Grid(36,190,50,44);body.Controls.Add(grid);tray=ToolUi.Check("最小化时隐藏到系统托盘",read().MinimizeToTray);ToolUi.Wide(grid,0,tray);
            keys=new DataGridView {Dock=DockStyle.Fill,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AllowUserToResizeRows=false,RowHeadersVisible=false,BackgroundColor=Color.White,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill,SelectionMode=DataGridViewSelectionMode.CellSelect};
            keys.BorderStyle=BorderStyle.None;keys.CellBorderStyle=DataGridViewCellBorderStyle.SingleHorizontal;keys.GridColor=ModernTheme.Border;
            keys.EnableHeadersVisualStyles=false;keys.ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None;
            keys.ColumnHeadersDefaultCellStyle.BackColor=ModernTheme.AccentSoft;keys.ColumnHeadersDefaultCellStyle.ForeColor=ModernTheme.Text;
            keys.DefaultCellStyle.SelectionBackColor=ModernTheme.AccentSoft;keys.DefaultCellStyle.SelectionForeColor=ModernTheme.Text;
            keys.AlternatingRowsDefaultCellStyle.BackColor=ModernTheme.Background;keys.RowTemplate.Height=32;
            keys.Columns.Add(new DataGridViewTextBoxColumn {Name="action",HeaderText="功能",ReadOnly=true,FillWeight=170});
            keys.Columns.Add(new DataGridViewCheckBoxColumn {Name="enabled",HeaderText="启用",FillWeight=55});
            foreach(string mod in new[]{"Ctrl","Alt","Shift"})keys.Columns.Add(new DataGridViewCheckBoxColumn {Name=mod,HeaderText=mod,FillWeight=55});
            var keyColumn=new DataGridViewComboBoxColumn {Name="key",HeaderText="按键",FillWeight=75};
            keyColumn.Items.AddRange(Enumerable.Range((int)Keys.A,26).Concat(Enumerable.Range((int)Keys.D0,10)).Concat(Enumerable.Range((int)Keys.F1,11)).Select(k=>((Keys)k).ToString()).ToArray());keys.Columns.Add(keyColumn);
            LoadKeys(read().Hotkeys);ToolUi.Wide(grid,1,keys);status=ToolUi.Text("至少使用 Ctrl 或 Alt；先检查组合是否重复，再检测 Windows 注册冲突。",8.5F);ToolUi.Wide(grid,2,status);
            ToolUi.Wide(grid,3,ToolUi.Buttons(ToolUi.Button("保存并应用",delegate
            {
                keys.EndEdit();ToolboxSettings next=read().Copy();next.MinimizeToTray=tray.Checked;next.Hotkeys=new List<HotkeyOption>();
                for(int i=0;i<keys.Rows.Count;i++)
                {
                    var cells=keys.Rows[i].Cells;uint mods=(Convert.ToBoolean(cells[2].Value)?2U:0U)|(Convert.ToBoolean(cells[3].Value)?1U:0U)|(Convert.ToBoolean(cells[4].Value)?4U:0U);
                    next.Hotkeys.Add(new HotkeyOption {Action=HotkeyRules.Actions[i],Enabled=Convert.ToBoolean(cells[1].Value),Modifiers=mods,Key=(int)(Keys)Enum.Parse(typeof(Keys),Convert.ToString(cells[5].Value))});
                }
                save(next);status.Text="设置已保存，快捷键已应用。";
            }),ToolUi.Button("填入默认值",delegate{LoadKeys(ToolboxSettings.Defaults());tray.Checked=true;status.Text="已填入默认值；点击保存并应用后生效。";})));
            stack.Controls.Add(ToolUi.Card("使用说明","麦克风快捷键默认不启用，启用后控制 Windows 默认通信麦克风的静音状态，不录音。",220,out body));
            body.Controls.Add(ToolUi.Text("托盘右键：打开主窗口、切换 4:3、恢复显示、麦克风静音、恢复并退出。\r\n按键被其他软件占用时会拒绝新配置，并尝试保留原来的快捷键。\r\n恢复显示进行中，不响应重复切换；原始状态和失败日志仍会保留。",8.5F));
            Button notices=ToolUi.Button("第三方组件与授权说明",delegate{ToolUi.ShowNotices(this);});notices.Dock=DockStyle.Bottom;notices.Height=30;body.Controls.Add(notices);
        }
        private void LoadKeys(List<HotkeyOption> values)
        {
            keys.Rows.Clear();for(int i=0;i<HotkeyRules.Actions.Length;i++){var k=values.First(v=>v.Action==HotkeyRules.Actions[i]);keys.Rows.Add(HotkeyRules.Titles[i],k.Enabled,(k.Modifiers&2)!=0,(k.Modifiers&1)!=0,(k.Modifiers&4)!=0,((Keys)k.Key).ToString());}
        }
    }

    internal sealed class GameAudioPage : UserControl
    {
        private readonly ComboBox output,mic;
        private readonly NumericUpDown outputVolume,micVolume,sessionVolume;
        private readonly CheckBox outputMute,micMute,sessionMute;
        private readonly ListBox sessions;
        private readonly Label status;
        private bool refreshing;
        public GameAudioPage()
        {
            Dock=DockStyle.Fill;var stack=new ResponsiveStack();Controls.Add(stack);Panel body;
            output=ToolUi.Choice();mic=ToolUi.Choice();outputVolume=ToolUi.Number(0,100,100);micVolume=ToolUi.Number(0,100,100);outputMute=ToolUi.Check("静音此输出设备");micMute=ToolUi.Check("静音此麦克风");
            var render=ToolUi.Card("游戏输出设备","切换系统默认耳机/音箱；游戏若指定了固定音频设备，需要在游戏内改为默认设备或重启游戏。",300,out body);stack.Controls.Add(render);
            var r=ToolUi.Grid(40,40,30,44);body.Controls.Add(r);ToolUi.Field(r,0,"输出设备",output);ToolUi.Field(r,1,"设备总音量（%）",outputVolume);ToolUi.Wide(r,2,outputMute);
            ToolUi.Wide(r,3,ToolUi.Buttons(ToolUi.Button("设为默认输出",delegate{SetDefault(output,0);}),ToolUi.Button("应用输出音量",delegate{ApplyVolume(output,outputVolume,outputMute);})));
            stack.Controls.Add(ToolUi.Card("麦克风控制","只调整设备音量/静音，不采集或保存声音。默认标记表示 Windows 默认通信麦克风。",300,out body));
            var m=ToolUi.Grid(40,40,30,44);body.Controls.Add(m);ToolUi.Field(m,0,"输入设备",mic);ToolUi.Field(m,1,"麦克风音量（%）",micVolume);ToolUi.Wide(m,2,micMute);
            ToolUi.Wide(m,3,ToolUi.Buttons(ToolUi.Button("设为默认麦克风",delegate{SetDefault(mic,1);}),ToolUi.Button("应用麦克风设置",delegate{ApplyVolume(mic,micVolume,micMute);})));
            stack.Controls.Add(ToolUi.Card("应用音量混合器","显示当前选中输出设备上的音频会话。游戏或程序开始播放声音后，点击刷新；这里不会改变全局总音量。",390,out body));
            var s=ToolUi.Grid(120,40,30,44);body.Controls.Add(s);sessions=new ListBox {Dock=DockStyle.Fill,IntegralHeight=false};ToolUi.Wide(s,0,sessions);sessionVolume=ToolUi.Number(0,100,100);sessionMute=ToolUi.Check("静音此音频会话");ToolUi.Field(s,1,"应用音量（%）",sessionVolume);ToolUi.Wide(s,2,sessionMute);
            ToolUi.Wide(s,3,ToolUi.Buttons(ToolUi.Button("刷新设备与会话",RefreshAudio),ToolUi.Button("应用会话音量",delegate
            {
                AudioSessionInfo selected=sessions.SelectedItem as AudioSessionInfo;if(selected==null)throw new InvalidOperationException("请先选择音频会话。");
                GameAudio.SetSession(selected,(int)sessionVolume.Value,sessionMute.Checked);RefreshSessions();status.Text="会话音量已应用。";
            })));
            var statusCard=new ModernCard {Height=80};status=ToolUi.Text("页面只在打开或刷新时读取设备，修改必须点击应用。",8.5F);statusCard.Controls.Add(status);stack.Controls.Add(statusCard);
            output.SelectedIndexChanged+=delegate{if(!refreshing){Fill(output,outputVolume,outputMute);SafeRefreshSessions();}};
            mic.SelectedIndexChanged+=delegate{if(!refreshing)Fill(mic,micVolume,micMute);};
            sessions.SelectedIndexChanged+=delegate{var selected=sessions.SelectedItem as AudioSessionInfo;if(selected!=null){sessionVolume.Value=selected.Volume;sessionMute.Checked=selected.Muted;}};
            VisibleChanged+=delegate{if(Visible)RefreshAudio();};
        }
        private static void Fill(ComboBox box,NumericUpDown level,CheckBox muted){var info=box.SelectedItem as AudioDeviceInfo;if(info!=null){level.Value=info.Volume;muted.Checked=info.Muted;}}
        public void RefreshAudio()
        {
            if(status==null)return;
            refreshing=true;
            try
            {
                string outputId=(output.SelectedItem as AudioDeviceInfo ?? new AudioDeviceInfo()).Id;
                string micId=(mic.SelectedItem as AudioDeviceInfo ?? new AudioDeviceInfo()).Id;
                LoadDevices(output,GameAudio.Devices(0),outputId);LoadDevices(mic,GameAudio.Devices(1),micId);
                Fill(output,outputVolume,outputMute);Fill(mic,micVolume,micMute);RefreshSessions();
                status.Text="读取完成。改变数值不会立即生效，请点击对应的应用按钮。";
            }
            catch(Exception ex){status.Text="音频读取失败："+ex.Message;}
            finally{refreshing=false;}
        }
        private static void LoadDevices(ComboBox combo,List<AudioDeviceInfo> devices,string selected)
        {
            combo.Items.Clear();combo.Items.AddRange(devices.ToArray());var item=devices.FirstOrDefault(d=>d.Id==selected)??devices.FirstOrDefault(d=>d.IsDefault)??devices.FirstOrDefault();if(item!=null)combo.SelectedItem=item;
        }
        private void SafeRefreshSessions(){try{RefreshSessions();}catch(Exception ex){status.Text=ex.Message;}}
        private void RefreshSessions()
        {
            sessions.Items.Clear();var selected=output.SelectedItem as AudioDeviceInfo;if(selected==null)return;
            sessions.Items.AddRange(GameAudio.Sessions(selected.Id).ToArray());if(sessions.Items.Count>0)sessions.SelectedIndex=0;
        }
        private void SetDefault(ComboBox combo,int flow)
        {
            var info=combo.SelectedItem as AudioDeviceInfo;if(info==null)throw new InvalidOperationException("没有可用设备。");
            if(MessageBox.Show("将“"+info.Name+"”设为系统默认"+(flow==0?"输出":"输入")+"及通信设备？这会影响其他使用默认设备的程序。","切换默认设备",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
            GameAudio.SetDefault(info.Id,flow);RefreshAudio();status.Text="默认设备已切换。游戏固定选择的设备不会被强制修改。";
        }
        private void ApplyVolume(ComboBox combo,NumericUpDown volume,CheckBox muted)
        {
            var info=combo.SelectedItem as AudioDeviceInfo;if(info==null)throw new InvalidOperationException("没有可用设备。");
            GameAudio.SetEndpointVolume(info.Id,(int)volume.Value,muted.Checked);RefreshAudio();status.Text="设备音量/静音已应用。";
        }
    }

}
