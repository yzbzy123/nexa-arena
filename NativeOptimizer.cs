using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace NexaArena
{
    internal sealed class NativeOptimizerPage:UserControl
    {
        private readonly GamePrograms programs;
        private readonly MemoryCleaner memory;
        private readonly SystemRepairJobs repairs=new SystemRepairJobs();
        private readonly TabControl tabs=new WorkbenchSectionTabs {Dock=DockStyle.Fill};
        private readonly ComboBox game=ToolUi.Choice("CS2","VALORANT"),modules=ToolUi.Choice(),managerChoice=ToolUi.Choice(),priority=ToolUi.Choice("Normal","AboveNormal","High"),repairTask=ToolUi.Choice("检查系统文件","修复系统文件","检查 Windows 映像","修复 Windows 映像","刷新 DNS 缓存");
        private readonly Label pathLabel=ToolUi.Text("尚未读取游戏路径"),notice=ToolUi.Text("只检查所选项目；不会自动应用系统优化。");
        private readonly ListView tuningList=List(),managerList=List(),appList=List();
        private readonly ComboBox tuningCategory=ToolUi.Choice("全部设置");
        private readonly TextBox tuningSearch=new ArenaInput {Hint="查找设置"};
        private string inspectedId;
        private readonly FlowLayoutPanel tuningCards=new FlowLayoutPanel {Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0),BackColor=Color.White};
        private readonly Label tuningSummary=ToolUi.Text("正在读取设置…",9F);
        private readonly List<TuningCard> cardRows=new List<TuningCard>();
        private readonly TextBox tuningDetails=Output(),managerDetails=Output(),report=Output(),processDetails=Output(),memoryDetails=Output(),affinity=new TextBox(),networkHost=new TextBox {Text="1.1.1.1"};
        private readonly NumericUpDown listThreshold=ToolUi.Number(128,1048576,1024),freeThreshold=ToolUi.Number(128,1048576,1024),timerTarget=ToolUi.Number(.1m,20,1,3);
        private readonly CheckBox gameOnly=ToolUi.Check("只在 CS2 / VALORANT 运行时自动清理",true),highPower=ToolUi.Check("游戏运行时切换已有高性能计划（增加功耗）");
        private readonly System.Windows.Forms.Timer statusTimer=new System.Windows.Forms.Timer {Interval=2000},saveTimer=new System.Windows.Forms.Timer {Interval=400};
        private readonly Button managerApply,managerRestore,monitorButton,timerButton;
        private SystemTuning tuning;
        private OptimizationManagers managers;
        private GameProcessProfiles processes;
        private string path;
        private bool working,loadingMemory,automatic,timerActive,loaded,memoryLoaded;
        internal event Action<bool> BusyChanged;
        private string Game {get{return InvokeRequired?(string)Invoke((Func<string>)(()=>Game)):Convert.ToString(game.SelectedItem)??"CS2";}}
        private sealed class Module {internal string Id,Name;public override string ToString(){return Name;}}
        private static ListView List(){return new ListView {Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,HideSelection=false,MultiSelect=false,BorderStyle=BorderStyle.FixedSingle,Font=ModernTheme.Font(9F,FontStyle.Regular)};}
        private static TextBox Output(){var box=ToolUi.Output();box.WordWrap=true;box.ScrollBars=ScrollBars.Vertical;box.Font=ModernTheme.Font(9F,FontStyle.Regular);return box;}
        private static Dictionary<string,object> Map(object value){return value as Dictionary<string,object>??new JavaScriptSerializer {MaxJsonLength=8000000}.Deserialize<Dictionary<string,object>>(new JavaScriptSerializer {MaxJsonLength=8000000}.Serialize(value));}
        private new static string Text(Dictionary<string,object> value,string key){object result;return value.TryGetValue(key,out result)&&result!=null?Convert.ToString(result):"";}
        private static string Shown(Dictionary<string,object> value,string key){string text=Text(value,key);return string.IsNullOrEmpty(text)?"—":text;}
        private static bool Flag(Dictionary<string,object> value,string key){object result;return value.TryGetValue(key,out result)&&result is bool&&(bool)result;}
        private static object[] ArrayOf(object value){return value as object[]??((IEnumerable)value).Cast<object>().ToArray();}
        private static string Describe(object value)
        {
            if(value==null)return "操作未返回结果。";
            if(value is string)return (string)value;
            var serializer=new JavaScriptSerializer {MaxJsonLength=8000000};object normalized=serializer.DeserializeObject(serializer.Serialize(value));
            return Format(normalized,0);
        }
        private static string Format(object value,int depth)
        {
            var map=value as Dictionary<string,object>;
            if(map!=null)return string.Join("\r\n",map.Select(x=>new string(' ',depth*2)+x.Key+"："+Format(x.Value,depth+1)).ToArray());
            var array=value as object[];if(array!=null)return string.Join("\r\n",array.Select(x=>Format(x,depth)).ToArray());return value==null?"—":Convert.ToString(value);
        }
        private Button ActionButton(string title,Func<object> action,Action<object> after=null,bool mutation=false,bool background=true)
        {
            var button=ModernTheme.Button(title,Color.White,ModernTheme.Text);button.AutoSize=false;button.Width=Math.Max(100,TextRenderer.MeasureText(title,button.Font).Width+32);button.Height=34;button.MinimumSize=new Size(92,32);button.Margin=new Padding(0,0,8,0);button.Enabled=!Program.UiPreview;
            button.Click+=async delegate{await Work(action,after,mutation,background);};return button;
        }
        private async Task Work(Func<object> action,Action<object> after=null,bool mutation=false,bool background=true)
        {
            if(working||IsDisposed)return;
            if(mutation&&Program.UiPreview){notice.Text="安全预览不更改系统、进程、应用或参数。";return;}
            working=true;tabs.Enabled=false;game.Enabled=false;if(BusyChanged!=null)BusyChanged(true);notice.Text="正在处理，请等待…";
            try{object result=background?await Task.Run(action):action();if(IsDisposed)return;if(after!=null)after(result);var normalized=new JavaScriptSerializer().DeserializeObject(new JavaScriptSerializer().Serialize(result));var info=normalized as Dictionary<string,object>;notice.Text=Failed(normalized)?"有项目未完成，请查看详细结果；没有将失败项当作成功。":result is string?(string)result:info!=null&&!string.IsNullOrEmpty(Text(info,"message"))?Text(info,"message"):"处理完成；请核对页面结果。";}
            catch(Exception ex){if(!IsDisposed){notice.Text="未完成："+ex.Message;MessageBox.Show(this,ex.Message,"操作未完成",MessageBoxButtons.OK,MessageBoxIcon.Warning);}}
            finally{working=false;if(!IsDisposed){tabs.Enabled=true;game.Enabled=true;if(BusyChanged!=null)BusyChanged(false);}}
        }
        private FlowLayoutPanel Toolbar(params Control[] controls){var row=new FlowLayoutPanel {Dock=DockStyle.Fill,WrapContents=true,AutoSize=false,Margin=new Padding(0),Padding=new Padding(0,4,0,0)};row.Controls.AddRange(controls);return row;}
        private static bool Failed(object value){var map=value as Dictionary<string,object>;if(map!=null)return map.ContainsKey("ok")&&!Flag(map,"ok")||map.Values.Any(Failed);var array=value as object[];return array!=null&&array.Any(Failed);}
        internal bool IsBusy {get{return working;}}
        internal string SelectedGame {get{return Game;}}
        private TableLayoutPanel Page(string title,params int[] rows)
        {
            var tab=new TabPage(title) {Padding=new Padding(16),BackColor=Color.White};tabs.TabPages.Add(tab);
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=rows.Length,Margin=new Padding(0)};
            foreach(int height in rows)root.RowStyles.Add(new RowStyle(height==0?SizeType.Percent:SizeType.Absolute,height==0?100:height));tab.Controls.Add(root);return root;
        }
        private SystemTuning Tuning(){return tuning??(tuning=new SystemTuning());}
        private OptimizationManagers Managers(){return managers??(managers=new OptimizationManagers());}
        private GameProcessProfiles Processes(){return processes??(processes=new GameProcessProfiles(Managers()));}
        internal NativeOptimizerPage(MemoryCleaner memoryOverride=null,GamePrograms programsOverride=null)
        {
            memory=memoryOverride??new MemoryCleaner();programs=programsOverride??new GamePrograms();
            Dock=DockStyle.Fill;BackColor=Color.White;Padding=new Padding(22,8,22,12);tabs.Font=ModernTheme.Font(10F,FontStyle.Regular);tabs.ItemSize=new Size(112,42);tabs.SizeMode=TabSizeMode.Fixed;
            var root=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=new Padding(0)};
            foreach(int height in new[]{42,34,0,34})root.RowStyles.Add(new RowStyle(height==0?SizeType.Percent:SizeType.Absolute,height==0?100:height));Controls.Add(root);
            game.Width=140;game.SelectedItem=programs.LastGame;game.Visible=false;game.TabStop=false;
            root.Controls.Add(Toolbar(game,ActionButton("选择主程序…",PickGame,null,false,false),ActionButton("重新识别",FindGame,x=>pathLabel.Text=Describe(x))),0,0);root.Controls.Add(pathLabel,0,1);root.Controls.Add(tabs,0,2);root.Controls.Add(notice,0,3);
            BuildTuning();BuildMemory();
            managerApply=ActionButton("检查并应用",()=>ChangeManager(false),x=>{managerDetails.Text=Describe(x);RefreshManager();},true);
            managerRestore=ActionButton("还原原值",()=>ChangeManager(true),x=>{managerDetails.Text=Describe(x);RefreshManager();},true);
            BuildManager();BuildProcesses();BuildApps();BuildRepair();
            monitorButton=FindButton(tabs.TabPages[1],"开始监控");timerButton=FindButton(tabs.TabPages[1],"请求定时器");
            game.SelectedIndexChanged+=delegate{if(!Program.UiPreview)programs.SelectGame(Game);path=null;pathLabel.Text="已切换游戏，请重新识别或选择主程序。";if(loaded)ReloadActive();};
            tabs.SelectedIndexChanged+=delegate{if(saveTimer.Enabled)SaveMemoryDraft();if(loaded)ReloadActive();};
            VisibleChanged+=delegate{if(Visible&&!loaded){loaded=true;if(Program.UiPreview)PreviewData();else{FindGame();ReloadActive();statusTimer.Start();}}};
            statusTimer.Tick+=delegate{if(working)return;if(tabs.SelectedIndex==1)RefreshMemory(false);if(tabs.SelectedIndex==5)report.Text=Describe(repairs.Status());};
            saveTimer.Tick+=delegate{SaveMemoryDraft();};
            foreach(var input in new[]{listThreshold,freeThreshold,timerTarget})input.ValueChanged+=delegate{if(!loadingMemory){saveTimer.Stop();saveTimer.Start();}};
            gameOnly.CheckedChanged+=delegate{if(!loadingMemory){saveTimer.Stop();saveTimer.Start();}};
        }
        private static Button FindButton(Control root,string text){foreach(Control child in root.Controls){var button=child as Button;if(button!=null&&button.Text==text)return button;var found=FindButton(child,text);if(found!=null)return found;}return null;}
        internal void SelectGame(string selected){if(!working&&(selected=="CS2"||selected=="VALORANT"))game.SelectedItem=selected;}
        private void SaveMemoryDraft(){saveTimer.Stop();if(memoryLoaded&&!loadingMemory&&!Program.UiPreview)try{memory.SaveInputs((long)listThreshold.Value,(long)freeThreshold.Value,gameOnly.Checked,(double)timerTarget.Value);notice.Text="内存参数已保存；未自动开启监控或定时器。";}catch(Exception ex){notice.Text="参数未保存："+ex.Message;}}
        internal void LoadMemoryInputs(){RefreshMemory(true);}
        internal void FlushMemoryInputs(){SaveMemoryDraft();}
        private object FindGame()
        {var result=programs.Resolve(Game,true);path=result.Path;string message=path==null?(result.Message??"请选择主程序；不扫描整块磁盘。"):result.Source+" · "+path;if(InvokeRequired)BeginInvoke((Action)(()=>pathLabel.Text=message));else pathLabel.Text=message;return message;}
        private object PickGame()
        {using(var dialog=new OpenFileDialog {Filter="游戏主程序|*.exe",Title="选择 "+GameOptimizer.ExecutableName(Game),CheckFileExists=true})if(dialog.ShowDialog(this)==DialogResult.OK){programs.SelectPath(Game,dialog.FileName);return FindGame();}return "已取消选择。";}
        private void ReloadActive()
        {
            if(Program.UiPreview)return;
            if(path==null)FindGame();
            if(tabs.SelectedIndex==0)RefreshTuning();else if(tabs.SelectedIndex==1)RefreshMemory(true);else if(tabs.SelectedIndex==2)RefreshManager();else if(tabs.SelectedIndex==3)HydrateProcessProfile(Processes().Status(Game));
        }
        private void BuildTuning()
        {
            var page=Page("系统调优",44,28,0,48);tuningList.CheckBoxes=true;
            tuningCategory.Width=118;tuningCategory.AccessibleName="优化设置类别";tuningSearch.Width=160;tuningSearch.Font=ModernTheme.Font(10F,FontStyle.Regular);tuningSearch.Margin=new Padding(0,2,8,0);tuningSearch.AccessibleName="查找优化设置";
            page.Controls.Add(Toolbar(tuningCategory,tuningSearch,ActionButton("刷新状态",()=>Tuning().Catalog(path),FillTuning),ActionButton("选择基础项目",delegate{foreach(ListViewItem row in tuningList.Items){var item=(Dictionary<string,object>)row.Tag;row.Checked=Flag(item,"supported")&&new[]{"game-mode","game-capture","gamebar-controller","mouse-acceleration","sticky-shortcut","filter-shortcut","toggle-shortcut"}.Contains(Text(item,"id"));}SyncCards();return "已选择基础项目；尚未应用。";},null,false,false),ActionButton("清空选择",delegate{foreach(ListViewItem row in tuningList.Items)row.Checked=false;SyncCards();return "已清空选择。";},null,false,false)),0,0);
            tuningSummary.ForeColor=ModernTheme.Muted;page.Controls.Add(tuningSummary,0,1);
            var body=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,66));body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,34));page.Controls.Add(body,0,2);body.Controls.Add(tuningCards,0,0);
            var inspector=new Panel {Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(16),Margin=new Padding(14,0,0,8)};var inspectorTitle=ModernTheme.Label("设置详情",12F,FontStyle.Bold,ModernTheme.Text);inspectorTitle.Dock=DockStyle.Top;inspectorTitle.Height=38;tuningDetails.BorderStyle=BorderStyle.None;tuningDetails.BackColor=Color.White;tuningDetails.Font=ModernTheme.Font(10F,FontStyle.Regular);tuningDetails.Text="点击一项设置，查看影响范围、当前值与目标值。\r\n\r\n勾选仅代表选择，不会立即修改系统。";inspector.Controls.Add(tuningDetails);inspector.Controls.Add(inspectorTitle);body.Controls.Add(inspector,1,0);
            var apply=ActionButton("应用勾选项",()=>Tuning().Change(SelectedIds(),path,false),x=>{tuningDetails.Text=Describe(x);RefreshTuning();},true);apply.BackColor=ModernTheme.Text;apply.ForeColor=Color.White;
            page.Controls.Add(Toolbar(apply,ActionButton("还原所选",()=>Tuning().Change(SelectedIds(),path,true),x=>{tuningDetails.Text=Describe(x);RefreshTuning();},true),ActionButton("导入方案",ImportProfile,x=>SyncCards(),false,false),ActionButton("导出方案",ExportProfile,null,false,false)),0,3);
            tuningList.ItemCheck+=delegate(object sender,ItemCheckEventArgs e){if(Program.UiPreview||!Flag((Dictionary<string,object>)tuningList.Items[e.Index].Tag,"supported"))e.NewValue=CheckState.Unchecked;};
            tuningList.SelectedIndexChanged+=delegate{if(tuningList.SelectedItems.Count==0)return;var item=(Dictionary<string,object>)tuningList.SelectedItems[0].Tag;tuningDetails.Text=Text(item,"description")+"\r\n作用："+Text(item,"scope")+" · "+Shown(item,"effect")+"\r\n当前值："+Shown(item,"currentValue")+" → 目标值："+Shown(item,"desiredValue")+"\r\n"+Text(item,"error");};
            tuningSearch.TextChanged+=delegate{RenderCards();};tuningCategory.SelectedIndexChanged+=delegate{RenderCards();};tuningCategory.SelectedIndex=0;tuningCards.SizeChanged+=delegate{SizeCards();};
        }
        private string[] SelectedIds(){if(InvokeRequired)return (string[])Invoke((Func<string[]>)SelectedIds);return tuningList.Items.Cast<ListViewItem>().Where(x=>x.Checked&&Flag((Dictionary<string,object>)x.Tag,"supported")).Select(x=>Text((Dictionary<string,object>)x.Tag,"id")).ToArray();}
        private void FillTuning(object value)
        {var selected=new HashSet<string>(SelectedIds());string category=Convert.ToString(tuningCategory.SelectedItem);tuningList.BeginUpdate();try{tuningList.Items.Clear();foreach(object entry in ArrayOf(value)){var item=Map(entry);var row=new ListViewItem(new[]{Text(item,"title"),Text(item,"category"),Text(item,"scope"),Flag(item,"applied")?"已保存原值":Text(item,"state")}) {Tag=item};tuningList.Items.Add(row);row.Checked=selected.Contains(Text(item,"id"))&&Flag(item,"supported");}}finally{tuningList.EndUpdate();}tuningCategory.BeginUpdate();tuningCategory.Items.Clear();tuningCategory.Items.Add("全部设置");foreach(string name in tuningList.Items.Cast<ListViewItem>().Select(x=>Text((Dictionary<string,object>)x.Tag,"category")).Distinct())if(!string.IsNullOrEmpty(name))tuningCategory.Items.Add(name);tuningCategory.SelectedItem=tuningCategory.Items.Contains(category??"")?category:"全部设置";tuningCategory.EndUpdate();RenderCards();}
        private void SizeCards(){int width=Math.Max(260,tuningCards.ClientSize.Width-SystemInformation.VerticalScrollBarWidth-4);foreach(Control card in tuningCards.Controls)card.Width=width;}
        private void SyncCards(){foreach(var card in cardRows)card.Sync();tuningSummary.Text="共 "+tuningList.Items.Count+" 项设置 · 已选择 "+SelectedIds().Length+" 项 · 仅应用明确勾选的项目";}
        private void RenderCards()
        {
            if(tuningCards.IsDisposed)return;tuningCards.SuspendLayout();foreach(Control child in tuningCards.Controls.Cast<Control>().ToArray())child.Dispose();cardRows.Clear();tuningCards.Controls.Clear();
            string category=Convert.ToString(tuningCategory.SelectedItem),query=tuningSearch.Text.Trim(),group=null;Action first=null,selected=null;
            foreach(ListViewItem row in tuningList.Items)
            {
                var item=(Dictionary<string,object>)row.Tag;string raw=Text(item,"category");
                if(category!="全部设置"&&!string.IsNullOrEmpty(category)&&raw!=category)continue;
                if(query.Length>0&&(Text(item,"title")+" "+Text(item,"description")+" "+raw).IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                string label=raw=="游戏"||raw=="后台"?"游戏与后台":raw=="图形"||raw=="外设"?"图形与输入":raw;
                if(group!=label){var title=ModernTheme.Label(label,10F,FontStyle.Bold,ModernTheme.Text);title.Height=32;title.BackColor=ModernTheme.Background;title.Padding=new Padding(10,0,0,0);title.Margin=new Padding(0,8,0,0);tuningCards.Controls.Add(title);group=label;}
                TuningCard card=null;Action inspect=delegate{inspectedId=Text(item,"id");foreach(var peer in cardRows)peer.SetSelected(peer==card);tuningDetails.Text=Text(item,"title")+"\r\n\r\n"+Text(item,"description")+"\r\n\r\n作用范围："+Text(item,"scope")+"\r\n生效方式："+Shown(item,"effect")+"\r\n\r\n当前值："+Shown(item,"currentValue")+"\r\n目标值："+Shown(item,"desiredValue")+"\r\n\r\n应用前自动保存原值，完成后读取核验。\r\n"+Text(item,"error");};
                card=new TuningCard(item,row,inspect,SyncCards);cardRows.Add(card);tuningCards.Controls.Add(card);if(first==null)first=inspect;if(Text(item,"id")==inspectedId)selected=inspect;
            }
            if(selected!=null)selected();else if(first!=null)first();else{tuningDetails.Text="没有匹配的设置。请调整类别或搜索词。";var empty=ToolUi.Text("没有匹配的设置",10F);empty.Height=70;tuningCards.Controls.Add(empty);}
            SizeCards();SyncCards();tuningCards.ResumeLayout();
        }
        private sealed class TuningCard:Panel
        {
            private readonly CheckBox check;private readonly ListViewItem model;private bool syncing;
            internal TuningCard(Dictionary<string,object> item,ListViewItem row,Action inspect,Action changed)
            {
                model=row;Height=84;Margin=new Padding(0);Padding=new Padding(10,5,10,5);BackColor=Color.White;TabStop=false;
                var grid=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=3,Margin=new Padding(0)};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));grid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,100));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,28));grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,20));Controls.Add(grid);
                check=new CheckBox {Text=Text(item,"title"),Dock=DockStyle.Fill,AutoEllipsis=true,Margin=new Padding(0),Font=ModernTheme.Font(10F,FontStyle.Bold),Enabled=Flag(item,"supported")&&!Program.UiPreview,AccessibleName=Text(item,"title")};grid.Controls.Add(check,0,0);
                string state=!Flag(item,"supported")?"暂不可用":Flag(item,"applied")?"已保存原值":Text(item,"state");var badge=ModernTheme.Label(state,8.5F,FontStyle.Regular,Flag(item,"supported")?ModernTheme.Accent:ModernTheme.Muted);badge.TextAlign=ContentAlignment.MiddleRight;grid.Controls.Add(badge,1,0);
                var description=ToolUi.Text(Text(item,"description"),9F);description.ForeColor=ModernTheme.Muted;description.Click+=delegate{inspect();};grid.Controls.Add(description,0,1);grid.SetColumnSpan(description,2);
                check.BackColor=Color.Transparent;var scope=ToolUi.Text(Text(item,"scope"),8.5F);scope.ForeColor=ModernTheme.Muted;grid.Controls.Add(scope,0,2);var detail=new LinkLabel {Text="查看详情",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleRight,LinkColor=ModernTheme.Accent,Font=ModernTheme.Font(9F,FontStyle.Regular),Margin=new Padding(0)};detail.LinkClicked+=delegate{inspect();};grid.Controls.Add(detail,1,2);
                check.CheckedChanged+=delegate{if(syncing)return;model.Checked=check.Checked;Sync();inspect();changed();};Click+=delegate{inspect();};Sync();
            }
            internal void SetSelected(bool selected){BackColor=selected?ModernTheme.AccentSoft:Color.White;Invalidate();}
            protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var pen=new Pen(ModernTheme.Border))e.Graphics.DrawLine(pen,8,Height-1,Width-8,Height-1);}
            internal void Sync(){syncing=true;check.Checked=model.Checked;syncing=false;}
        }
        private void RefreshTuning(){if(working){BeginInvoke((Action)RefreshTuning);return;}var ignored=Work(()=>Tuning().Catalog(path),FillTuning);}
        private object ExportProfile()
        {using(var dialog=new SaveFileDialog {Filter="调优方案|*.xml",FileName="NexaArena-"+Game+".xml"}){if(dialog.ShowDialog(this)!=DialogResult.OK)return "已取消导出。";var profile=new TuningProfile {Version=1,Game=Game,Ids=SelectedIds().ToList()};profile.Validate();using(var stream=File.Create(dialog.FileName))new XmlSerializer(typeof(TuningProfile)).Serialize(stream,profile);return "已导出选择，不包含设备身份或原值备份。";}}
        private object ImportProfile()
        {using(var dialog=new OpenFileDialog {Filter="调优方案|*.xml",CheckFileExists=true}){if(dialog.ShowDialog(this)!=DialogResult.OK)return "已取消导入。";TuningProfile profile;using(var stream=File.OpenRead(dialog.FileName))profile=TuningProfile.Read(stream);if(profile.Game!=Game)throw new InvalidOperationException("方案属于 "+profile.Game+"，请先切换游戏。");foreach(ListViewItem row in tuningList.Items)row.Checked=Flag((Dictionary<string,object>)row.Tag,"supported")&&profile.Ids.Contains(Text((Dictionary<string,object>)row.Tag,"id"));return "仅导入当前支持项目的勾选项，尚未应用任何设置。";}}
        private void BuildMemory()
        {
            var page=Page("内存与待机",130,170,90,0);
            var fields=ToolUi.Grid(42,42,42);ToolUi.Field(fields,0,"待机列表至少（MB）",listThreshold);ToolUi.Field(fields,1,"且空闲内存低于（MB）",freeThreshold);ToolUi.Field(fields,2,"自动清理条件",gameOnly);page.Controls.Add(fields,0,0);page.Controls.Add(memoryDetails,0,1);
            var timerRow=ToolUi.Grid(42,42);ToolUi.Field(timerRow,0,"定时器请求值（ms）",timerTarget);
            timerRow.Controls.Add(Toolbar(ActionButton("保存参数",()=>memory.SaveInputs((long)listThreshold.Value,(long)freeThreshold.Value,gameOnly.Checked,(double)timerTarget.Value),x=>RefreshMemory(false),true,false),ActionButton("开始监控",delegate{memory.Configure(!automatic,(long)listThreshold.Value,(long)freeThreshold.Value,gameOnly.Checked);RefreshMemory(false);return "监控状态已更新。";},null,true,false),ActionButton("手动清理待机",()=>memory.Purge(),x=>RefreshMemory(false),true,false),ActionButton("请求定时器",delegate{memory.SetTimer(!timerActive,(double)timerTarget.Value);RefreshMemory(false);return "请求状态已更新。";},null,true,false)),0,1);timerRow.SetColumnSpan(timerRow.GetControlFromPosition(0,1),2);page.Controls.Add(timerRow,0,2);
            page.Controls.Add(ToolUi.Text("两个阈值同时满足才清理；每 5 秒检查，两次清理至少间隔 30 秒。\r\n定时器请求独立可选，关闭工具会释放。参数自动保存，但不会自动开始监控。"),0,3);
        }
        private void RefreshMemory(bool inputs)
        {
            try
            {
                var state=Map(memory.Status());automatic=Flag(state,"automatic");timerActive=Convert.ToDouble(state["timerRequestedMs"])>0;var snapshot=Map(state["memory"]);
                memoryDetails.Text="总内存："+Text(snapshot,"totalMb")+" MB    可用（含待机）："+Text(snapshot,"availableMb")+" MB\r\n待机列表："+Text(snapshot,"standbyMb")+" MB    空闲（不含待机）："+Text(snapshot,"freeMb")+" MB\r\n本次清理："+Text(state,"cleanCount")+" 次\r\n系统定时器："+Text(snapshot,"currentTimerMs")+" ms    当前请求："+Text(state,"timerRequestedMs")+" ms\r\n"+Text(state,"lastMessage")+"\r\n"+Text(snapshot,"error");
                if(inputs){loadingMemory=true;try{listThreshold.Value=Convert.ToDecimal(state["listThreshold"]);freeThreshold.Value=Convert.ToDecimal(state["freeThreshold"]);gameOnly.Checked=Flag(state,"onlyGame");timerTarget.Value=Math.Max(timerTarget.Minimum,Math.Min(timerTarget.Maximum,Convert.ToDecimal(state["timerTargetMs"])));memoryLoaded=true;}finally{loadingMemory=false;}}
                listThreshold.Enabled=freeThreshold.Enabled=gameOnly.Enabled=!automatic;timerTarget.Enabled=!timerActive;if(monitorButton!=null)monitorButton.Text=automatic?"停止监控":"开始监控";if(timerButton!=null)timerButton.Text=timerActive?"释放定时器":"请求定时器";
            }
            catch(Exception ex){memoryDetails.Text="内存状态读取失败："+ex.Message;}
        }
        private void BuildManager()
        {
            string[] ids={"nvidia-game","nvidia-global","startup","tasks","services","network","network-advanced","power","devices","device-msi","interrupt-affinity","ifeo"};
            string[] names={"NVIDIA · 当前游戏","NVIDIA · 全局设置","登录启动项","计划任务","可选系统服务","网卡 DNS","网卡高级属性","电源计划","设备音频控制","MSI（实验性）","中断路由（实验性）","游戏启动重定向"};
            for(int i=0;i<ids.Length;i++)modules.Items.Add(new Module {Id=ids[i],Name=names[i]});modules.SelectedIndex=0;modules.Width=230;
            var page=Page("系统管理",50,0,44);page.Controls.Add(Toolbar(modules,ActionButton("刷新列表",ManagerCatalog,FillManager)),0,0);
            var split=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Margin=new Padding(0)};split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,53));split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,47));page.Controls.Add(split,0,1);
            managerList.Font=ModernTheme.Font(10F,FontStyle.Regular);managerList.Columns.Add("设置项目",245);managerList.Columns.Add("当前值",150);managerList.Columns.Add("状态",90);split.Controls.Add(managerList,0,0);
            var detail=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=1,RowCount=5,Margin=new Padding(16,0,0,0),Padding=new Padding(16),BackColor=Color.White};foreach(int height in new[]{38,0,28,40,86})detail.RowStyles.Add(new RowStyle(height==0?SizeType.Percent:SizeType.Absolute,height==0?100:height));var heading=ModernTheme.Label("当前设置与影响",12F,FontStyle.Bold,ModernTheme.Text);detail.Controls.Add(heading,0,0);managerDetails.BorderStyle=BorderStyle.None;managerDetails.Font=ModernTheme.Font(10F,FontStyle.Regular);detail.Controls.Add(managerDetails,0,1);detail.Controls.Add(ToolUi.Text("目标操作 / 值",9F),0,2);managerChoice.Dock=DockStyle.Fill;detail.Controls.Add(managerChoice,0,3);var operations=new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,Margin=new Padding(0),Padding=new Padding(0,8,0,0)};operations.Controls.Add(managerApply);operations.Controls.Add(managerRestore);managerApply.Margin=managerRestore.Margin=new Padding(0,0,0,8);detail.Controls.Add(operations,0,4);split.Controls.Add(detail,1,0);networkHost.Width=150;
            page.Controls.Add(Toolbar(ToolUi.Text("DNS/连通性："),networkHost,ActionButton("测试连接",()=>WindowsManagerBackend.NetworkTest(networkHost.Text.Trim()),x=>managerDetails.Text=Describe(x))),0,2);
            managerList.SelectedIndexChanged+=delegate{if(managerList.SelectedItems.Count==0)return;var item=(Dictionary<string,object>)managerList.SelectedItems[0].Tag;managerDetails.Text=Text(item,"detail")+"\r\n当前："+Text(item,"current")+"\r\n影响："+Shown(item,"effect")+"\r\n"+Text(item,"blocked");managerChoice.Items.Clear();foreach(object entry in ArrayOf(item["choices"]))managerChoice.Items.Add(new Choice(Map(entry)));if(managerChoice.Items.Count>0)managerChoice.SelectedIndex=0;managerApply.Enabled=Flag(item,"supported")&&managerChoice.Items.Count>0&&!Program.UiPreview;managerRestore.Enabled=Flag(item,"applied")&&!Program.UiPreview;};
            modules.SelectedIndexChanged+=delegate{if(loaded)RefreshManager();};managerApply.Enabled=managerRestore.Enabled=false;
        }
        private sealed class Choice {internal string Value,Label;internal Choice(Dictionary<string,object> item){Value=Text(item,"value");Label=Text(item,"label");}public override string ToString(){return Label;}}
        private string ModuleId(){if(InvokeRequired)return (string)Invoke((Func<string>)ModuleId);string id=((Module)modules.SelectedItem).Id;return id=="nvidia-game"?"nvidia-"+Game:id;}
        private object ManagerCatalog(){var manager=Managers();manager.SelectGamePath(Game,path);return manager.Catalog(ModuleId());}
        private void FillManager(object value){managerList.Items.Clear();managerChoice.Items.Clear();managerApply.Enabled=managerRestore.Enabled=false;foreach(object entry in ArrayOf(Map(value)["items"])){var item=Map(entry);managerList.Items.Add(new ListViewItem(new[]{Text(item,"title"),Text(item,"current"),Flag(item,"applied")?"已保存原值":Flag(item,"supported")?"可管理":"只读"}) {Tag=item});}}
        private void RefreshManager(){if(working){BeginInvoke((Action)RefreshManager);return;}var ignored=Work(ManagerCatalog,FillManager);}
        private object ChangeManager(bool restore)
        {
            if(InvokeRequired)return Invoke((Func<object>)(()=>ChangeManager(restore)));
            if(managerList.SelectedItems.Count==0)throw new InvalidOperationException("请先选择一个项目。");
            var item=(Dictionary<string,object>)managerList.SelectedItems[0].Tag;var choice=managerChoice.SelectedItem as Choice;string module=ModuleId();
            string warning=Text(item,"title")+"\r\n"+Shown(item,"effect")+"\r\n\r\n"+(restore?"确认核对并还原原值？":"确认应用所选值："+(choice==null?"":choice.Label)+"？");
            if(module=="device-msi"||module=="interrupt-affinity")warning+="\r\n实验性设备参数可能导致设备异常；重启后核对，请确保有恢复手段。";
            if(MessageBox.Show(this,warning,"确认所选项目",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.OK)return "已取消，未修改。";
            return Managers().Change(module,Text(item,"id"),choice==null?null:choice.Value,restore);
        }
        private void BuildProcesses()
        {
            var page=Page("进程方案",155,42,0);var fields=ToolUi.Grid(40,40,60);ToolUi.Field(fields,0,"游戏优先级",priority);ToolUi.Field(fields,1,"CPU 亲和性（十六进制）",affinity);ToolUi.Wide(fields,2,highPower);page.Controls.Add(fields,0,0);
            page.Controls.Add(Toolbar(ActionButton("保存当前游戏方案",()=>Processes().Save(ProcessInputSnapshot()),HydrateProcessProfile,true,false),ActionButton("开始方案监控",()=>Processes().Configure(Game,true),HydrateProcessProfile,true,false),ActionButton("停止监控并还原",()=>Processes().Configure(Game,false),HydrateProcessProfile,true,false),ActionButton("刷新状态",()=>Processes().Status(Game),HydrateProcessProfile,false,false)),0,1);page.Controls.Add(processDetails,0,2);
        }
        internal void HydrateProcessProfile(object value)
        {
            var state=Map(value);var profile=Map(state["profile"]);string savedPriority=Text(profile,"Priority");
            if(!priority.Items.Contains(savedPriority))throw new InvalidOperationException("保存的进程优先级不受支持，未替换为默认值。");
            priority.SelectedItem=savedPriority;affinity.Text=Text(profile,"Affinity");highPower.Checked=Flag(profile,"HighPower");
            bool editable=!Flag(state,"enabled");priority.Enabled=affinity.Enabled=highPower.Enabled=editable;processDetails.Text=Describe(value);
        }
        internal GameProcessProfile ProcessInputSnapshot(){return new GameProcessProfile {Game=Game,Priority=Convert.ToString(priority.SelectedItem),Affinity=affinity.Text.Trim(),HighPower=highPower.Checked};}
        private void BuildApps()
        {
            var page=Page("应用管理",42,0,65);appList.Columns.Add("应用包",480);appList.Columns.Add("状态",120);
            page.Controls.Add(Toolbar(ActionButton("读取当前用户应用",()=>AppxManagement.List(),FillApps),ActionButton("卸载所选应用",RemoveApp,x=>notice.Text=Describe(x),true),ActionButton("安装应用包…",InstallApp,x=>notice.Text=Describe(x),true)),0,0);page.Controls.Add(appList,0,1);page.Controls.Add(ToolUi.Text("系统核心、框架和不可卸载应用受保护。卸载不是普通可逆优化，可能移除应用数据。\r\n安装仅接受可信 APPX / MSIX 包；Windows 校验签名，不导入证书或绕过签名。"),0,2);
        }
        private void FillApps(object value){appList.Items.Clear();foreach(object entry in ArrayOf(value)){var item=Map(entry);appList.Items.Add(new ListViewItem(new[]{Text(item,"name"),Flag(item,"protectedPackage")?"受保护":"可选择卸载"}) {Tag=item});}}
        private object OnUi(Func<object> action){return InvokeRequired?Invoke(action):action();}
        private object RemoveApp()
        {
            string package=(string)OnUi(delegate{
                if(appList.SelectedItems.Count==0)throw new InvalidOperationException("先选择一个应用包。");
                var item=(Dictionary<string,object>)appList.SelectedItems[0].Tag;
                if(Flag(item,"protectedPackage"))throw new InvalidOperationException("该应用受保护。");
                return MessageBox.Show(this,"将卸载 "+Text(item,"name")+"，可能移除应用数据。不能通过原值备份撤销。","确认卸载",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.OK?Text(item,"package"):null;
            });
            return package==null?(object)"已取消，未修改。":AppxManagement.Remove(package,true);
        }
        private object InstallApp()
        {
            string package=(string)OnUi(delegate{
                using(var dialog=new OpenFileDialog {Filter="Windows 应用包|*.appx;*.appxbundle;*.msix;*.msixbundle",CheckFileExists=true})
                {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return null;
                    return MessageBox.Show(this,"将为当前用户安装或更新应用，不能通过设置原值备份撤销。\\r\\n"+dialog.FileName,"确认可信安装包",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.OK?dialog.FileName:null;
                }
            });
            return package==null?(object)"已取消，未修改。":AppxManagement.Install(package);
        }
        private void BuildRepair()
        {
            var page=Page("检查与修复",42,0,64);repairTask.Width=210;
            page.Controls.Add(Toolbar(repairTask,ActionButton("运行所选任务",StartRepair,x=>report.Text=Describe(x),true,false),ActionButton("负载与就绪检查",()=>ReadinessReport.Build(Game,path),x=>report.Text=Describe(x)),ActionButton("测量调度间隔",()=>SystemRepairJobs.TimingMeasure(),x=>report.Text=Describe(x))),0,0);page.Controls.Add(report,0,1);page.Controls.Add(ToolUi.Text("系统检查与修复可能耗时，不会自动重启；退出代码不代表已证实解决全部问题。\r\n这里只展示诊断信息，不自动更改安全软件、游戏进程或全局电源计划。"),0,2);
        }
        private object StartRepair(){string[] ids={"sfc-verify","sfc-repair","dism-scan","dism-repair","dns-flush"};if(MessageBox.Show(this,"确认运行“"+repairTask.SelectedItem+"”？系统修复不是普通可逆优化，可能需要等待较长时间。","确认系统任务",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.OK)return "已取消。";return repairs.Start(ids[repairTask.SelectedIndex],true);}
        private void PreviewData(){pathLabel.Text="安全预览 · 以下为布局示意，不读取或更改实际配置";tuningDetails.Text="选择设置后查看当前值、目标值和影响。\r\n\r\n此处状态仅作示意。";FillTuning(new[]{new {id="game-mode",title="启用 Windows 游戏模式",description="由 Windows 管理游戏期间的资源分配。",category="游戏",scope="Windows 全局",state="可配置",supported=true},new {id="game-capture",title="关闭 Windows 游戏录制",description="关闭系统录制；Win + G 录制和回溯片段将受影响。",category="后台",scope="Windows 全局",state="已是目标值",supported=true},new {id="mouse-acceleration",title="关闭桌面鼠标加速",description="作用于 Windows 指针，不改变使用原始输入的游戏灵敏度。",category="外设",scope="Windows 全局",state="可配置",supported=true},new {id="gpu",title="游戏使用高性能 GPU",description="为所选游戏设置 Windows 图形性能首选项。",category="图形",scope="所选游戏",state="可配置",supported=true},new {id="sticky-shortcut",title="关闭粘滞键快捷键",description="关闭连续按 Shift 的唤起快捷键，保留辅助功能本身。",category="外设",scope="Windows 全局",state="可配置",supported=true}});memoryDetails.Text="总内存、待机列表和空闲内存会在正式程序中实时读取。\r\n1024 / 2048 MB 仅作界面示意，不会开启监控。";loadingMemory=true;freeThreshold.Value=2048;loadingMemory=false;}
        protected override void Dispose(bool disposing)
        {
            if(disposing){saveTimer.Stop();statusTimer.Stop();SaveMemoryDraft();saveTimer.Dispose();statusTimer.Dispose();memory.Dispose();if(processes!=null)processes.Dispose();repairs.Dispose();}
            base.Dispose(disposing);
        }
    }
}
