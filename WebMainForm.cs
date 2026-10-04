using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace NexaArena
{
    internal sealed class WebMainForm : ModernMainForm
    {
        private const string AppOrigin = "https://nexa.local/";
        private readonly WebView2 browser;
        private readonly Control legacyRoot;
        private readonly JavaScriptSerializer json = new JavaScriptSerializer { MaxJsonLength = 4000000 };
        private readonly MemoryCleaner memoryCleaner=new MemoryCleaner();
        private SystemTuning tuning;
        private OptimizationManagers managers;
        private GameProcessProfiles processProfiles;
        private readonly SystemRepairJobs repairJobs=new SystemRepairJobs();
        private readonly Dictionary<string,string> optimizationPaths=new Dictionary<string,string>();
        private readonly string screenshotDirectory;

        public WebMainForm(string screenshotDirectory=null)
        {
            this.screenshotDirectory=screenshotDirectory;
            Text = Program.UiPreview ? "Nexa Arena · Web UI 预览" : "Nexa Arena · CS2 / VALORANT 游戏工具箱";
            legacyRoot = Controls[0];
            legacyRoot.Visible = false;
            Controls.Remove(legacyRoot);
            browser = new WebView2 { Dock = DockStyle.Fill, DefaultBackgroundColor = Color.FromArgb(244,245,247) };
            Controls.Add(browser);
            browser.BringToFront();
            browser.Bounds = ClientRectangle;
            Resize += delegate { if(!browser.IsDisposed && browser.Parent==this) browser.Bounds=ClientRectangle; };
            FormClosed += delegate { if(processProfiles!=null)processProfiles.Dispose();repairJobs.Dispose();if(!Program.UiPreview)GameOptimizer.RestoreAll(); };
            Shown += async delegate
            {
                try
                {
                    string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"web");
                    if(!File.Exists(Path.Combine(folder,"index.html")))
                        throw new FileNotFoundException("网页界面文件缺失。",Path.Combine(folder,"index.html"));
                    string profile=Program.UiPreview?
                        Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"web-preview-profile"):
                        Path.Combine(SavedState.DirectoryPath,"webview2-profile");
                    Directory.CreateDirectory(profile);
                    CoreWebView2Environment env=await CoreWebView2Environment.CreateAsync(null,profile);
                    await browser.EnsureCoreWebView2Async(env);
                    browser.CoreWebView2.Settings.AreDevToolsEnabled=false;
                    browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled=false;
                    browser.CoreWebView2.Settings.IsStatusBarEnabled=false;
                    browser.CoreWebView2.SetVirtualHostNameToFolderMapping("nexa.local",folder,
                        CoreWebView2HostResourceAccessKind.DenyCors);
                    browser.CoreWebView2.NavigationStarting += delegate(object sender,CoreWebView2NavigationStartingEventArgs e)
                    { if(!e.Uri.StartsWith(AppOrigin,StringComparison.OrdinalIgnoreCase))e.Cancel=true; };
                    browser.CoreWebView2.NewWindowRequested += delegate(object sender,CoreWebView2NewWindowRequestedEventArgs e)
                    { e.Handled=true; };
                    browser.CoreWebView2.WebMessageReceived += MessageReceived;
                    if(Program.UiPreview&&!string.IsNullOrEmpty(screenshotDirectory))
                        browser.NavigationCompleted += async delegate(object sender,CoreWebView2NavigationCompletedEventArgs completed)
                        {
                            if(!completed.IsSuccess)return;
                            try
                            {
                                Directory.CreateDirectory(screenshotDirectory);
                                await System.Threading.Tasks.Task.Delay(900);
                                await WritePreviewSize("valorant");
                                await CaptureScreenshot("valorant.png");
                                await browser.ExecuteScriptAsync("document.querySelector('.page-scroll').scrollTop = document.querySelector('.page-scroll').scrollHeight");
                                await System.Threading.Tasks.Task.Delay(350);
                                await CaptureScreenshot("aspect.png");
                                await browser.ExecuteScriptAsync("document.querySelector('.page-scroll').scrollTop = 0");
                                Size originalSize=ClientSize;
                                ClientSize=new Size(1024,700);
                                await System.Threading.Tasks.Task.Delay(350);
                                await CaptureScreenshot("valorant-compact.png");
                                ClientSize=originalSize;
                                await System.Threading.Tasks.Task.Delay(250);
                                await browser.ExecuteScriptAsync("document.querySelector('[aria-label=\"CS2\"]')?.click()");
                                await System.Threading.Tasks.Task.Delay(500);
                                await WritePreviewSize("cs2");
                                await CaptureScreenshot("cs2.png");
                                await ClickPreviewTab(1);
                                await System.Threading.Tasks.Task.Delay(250);
                                string filterVisible=await browser.ExecuteScriptAsync("document.body.innerText.includes('恢复原始颜色')");
                                if(filterVisible!="true")throw new InvalidOperationException("CS2 filter tab did not open.");
                                await CaptureScreenshot("cs2-filters.png");
                                await ClickPreviewTab(2);
                                await System.Threading.Tasks.Task.Delay(250);
                                string commandsVisible=await browser.ExecuteScriptAsync("document.body.innerText.includes('指令选项')");
                                if(commandsVisible!="true")throw new InvalidOperationException("CS2 commands tab did not open.");
                                await CaptureScreenshot("cs2-commands.png");
                                await browser.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(x => x.textContent.includes('生成指令'))?.click()");
                                await System.Threading.Tasks.Task.Delay(350);
                                string generated=await browser.ExecuteScriptAsync("document.querySelector('textarea')?.value || ''");
                                if(!generated.Contains("fps_max"))throw new InvalidOperationException("CS2 command bridge did not produce output.");
                                await browser.ExecuteScriptAsync("document.querySelector('[aria-label=\"灵敏度\"]')?.click()");
                                await System.Threading.Tasks.Task.Delay(500);
                                await CaptureScreenshot("sensitivity.png");
                                foreach(string pageName in new[]{"游戏音频","游戏优化","快捷键","使用指南"})
                                {
                                    await browser.ExecuteScriptAsync("document.querySelector('[aria-label=\""+pageName+"\"]')?.click()");
                                    await System.Threading.Tasks.Task.Delay(400);
                                    await WritePreviewSize(pageName);
                                    await CaptureScreenshot(pageName+".png");
                                    if(pageName=="游戏优化")
                                    {
                                        string tuningRows=await browser.ExecuteScriptAsync("document.querySelectorAll('.tuning-row').length");
                                        if(tuningRows!=SystemTuning.Definitions.Length.ToString())throw new InvalidOperationException("Optimization bridge did not render the complete tuning catalog.");
                                        string tuningErrors=await browser.ExecuteScriptAsync("JSON.stringify(Array.from(document.querySelectorAll('.tuning-error')).map(x=>x.textContent))");
                                        var errors=json.Deserialize<string[]>(json.Deserialize<string>(tuningErrors));
                                        foreach(string error in errors)
                                            if(error!="请先选择对应游戏的主程序 EXE。"&&error!="系统未提供高性能计划；可使用 Windows 电源模式设置。"&&error!="Windows 操作失败：2"&&error!="此系统未安装该可选服务。"&&error!="该服务保留了延迟启动配置；请使用系统服务管理器。"&&error!="当前 Windows 版本不支持此策略；请使用系统隐私设置。")
                                                throw new InvalidOperationException("Optimization catalog read failed: "+error);
                                        File.WriteAllText(Path.Combine(screenshotDirectory,"optimization-status.txt"),"PASS: all "+tuningRows+" tuning rows rendered; only documented target/capability errors allowed.\r\n"+string.Join("\r\n",errors));
                                        ClientSize=new Size(1024,700);await System.Threading.Tasks.Task.Delay(300);await CaptureScreenshot("optimizer-compact.png");ClientSize=originalSize;await System.Threading.Tasks.Task.Delay(250);
                                        await browser.ExecuteScriptAsync("{const input=document.querySelector('#optimization-search');const setter=Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set;setter.call(input,'服务');input.dispatchEvent(new Event('input',{bubbles:true}));}");
                                        await System.Threading.Tasks.Task.Delay(250);
                                        string serviceRows=await browser.ExecuteScriptAsync("document.querySelectorAll('.tuning-row').length");
                                        if(serviceRows!="8")throw new InvalidOperationException("Optimization search did not filter the eight service options.");
                                        await browser.ExecuteScriptAsync("document.querySelector('.page-scroll').scrollTop=0");await CaptureScreenshot("optimizer-services.png");
                                        await browser.ExecuteScriptAsync("{const input=document.querySelector('#optimization-search');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,'');input.dispatchEvent(new Event('input',{bubbles:true}));}");await System.Threading.Tasks.Task.Delay(200);
                                        await browser.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(x=>x.textContent==='选择基础项目')?.click()");await System.Threading.Tasks.Task.Delay(150);
                                        await browser.ExecuteScriptAsync("Array.from(document.querySelectorAll('button')).find(x=>x.textContent==='应用选中项')?.click()");await System.Threading.Tasks.Task.Delay(200);
                                        string reviewSafe=await browser.ExecuteScriptAsync("{const d=document.querySelector('[data-slot=alert-dialog-content]');!!d&&d.innerText.includes('游戏模式')&&d.querySelector('[data-slot=alert-dialog-action]').disabled}");
                                        if(reviewSafe!="true")throw new InvalidOperationException("Optimization review dialog is missing or preview permits applying settings.");
                                        await CaptureScreenshot("optimizer-confirm.png");await browser.ExecuteScriptAsync("document.querySelector('[data-slot=alert-dialog-cancel]').click()");await System.Threading.Tasks.Task.Delay(150);
                                        await browser.ExecuteScriptAsync("document.querySelector('.page-scroll').scrollTop=document.querySelector('.page-scroll').scrollHeight");await System.Threading.Tasks.Task.Delay(150);await CaptureScreenshot("optimizer-bottom.png");
                                        await browser.ExecuteScriptAsync("document.querySelector('.page-scroll').scrollTop=0");
                                        await ClickPreviewTab(1);await System.Threading.Tasks.Task.Delay(450);await CaptureScreenshot("memory.png");
                                        await ClickPreviewTab(2);await System.Threading.Tasks.Task.Delay(250);await CaptureScreenshot("system-tools.png");
                                        foreach(string moduleLabel in new[]{"登录启动项","计划任务","服务管理","网卡 DNS","网卡高级属性","NVIDIA 全局设置","设备音频控制","MSI 配置（实验性）","中断路由（实验性）","游戏 IFEO 重定向","APPX / MSIX 应用","负载与就绪检查","Windows 检查与修复"})
                                        {
                                            await ChoosePreviewManager(moduleLabel);
                                            await System.Threading.Tasks.Task.Delay(moduleLabel=="APPX / MSIX 应用"?3500:900);
                                            for(int attempt=0;attempt<25;attempt++)
                                            {
                                                string loading=await browser.ExecuteScriptAsync("document.body.innerText.includes('正在读取…')||document.body.innerText.includes('正在读取当前用户应用…')");
                                                if(loading!="true")break;await System.Threading.Tasks.Task.Delay(200);
                                            }
                                            await browser.ExecuteScriptAsync("document.querySelector('.page-scroll').scrollTop=0");
                                            string failed=await browser.ExecuteScriptAsync("document.body.innerText.includes('列表读取失败')");
                                            if(failed=="true")throw new InvalidOperationException("Manager UI read failed: "+moduleLabel);
                                            string rows=await browser.ExecuteScriptAsync("document.querySelectorAll('.manager-list-row').length");
                                            File.AppendAllText(Path.Combine(screenshotDirectory,"manager-status.txt"),moduleLabel+" rows="+rows+Environment.NewLine);
                                            if(rows!="0"){await browser.ExecuteScriptAsync("document.querySelector('.manager-list-row').click()");await System.Threading.Tasks.Task.Delay(100);}
                                            await CaptureScreenshot("manager-"+moduleLabel.Replace('/','_')+".png");
                                            if(moduleLabel=="网卡高级属性")
                                            {
                                                ClientSize=new Size(1024,700);await System.Threading.Tasks.Task.Delay(250);await CaptureScreenshot("manager-compact.png");
                                                string noOverflow=await browser.ExecuteScriptAsync("Array.from(document.querySelectorAll('.page-scroll,.manager-list,.manager-inspector')).every(x=>x.scrollWidth<=x.clientWidth+2)");
                                                if(noOverflow!="true")throw new InvalidOperationException("Compact manager layout has horizontal overflow.");ClientSize=originalSize;
                                            }
                                        }
                                    }
                                }
                                ClientSize=new Size(originalSize.Width,originalSize.Height+120);
                                await System.Threading.Tasks.Task.Delay(450);
                                await WritePreviewSize("使用指南-加高窗口");
                                await CaptureScreenshot("guide-tall.png");
                                string sidebarCoverage=await browser.ExecuteScriptAsync("{const s=document.querySelector('.app-sidebar').getBoundingClientRect(),f=document.querySelector('.app-footer').getBoundingClientRect();Math.abs(s.bottom-f.top)<2&&Math.abs(f.bottom-innerHeight)<2}");
                                if(sidebarCoverage!="true")throw new InvalidOperationException("Sidebar does not cover the resized viewport.");
                                ClientSize=originalSize;
                                File.WriteAllText(Path.Combine(screenshotDirectory,"result.txt"),"PASS: WebView2 rendered every UI page and compact layout.");
                            }
                            catch(Exception screenshotError)
                            {
                                File.WriteAllText(Path.Combine(screenshotDirectory,"result.txt"),screenshotError.ToString());
                            }
                            Close();
                        };
                    browser.Source=new Uri(AppOrigin+"index.html");
                }
                catch(Exception ex)
                {
                    ErrorLog.Write(ex);
                    browser.Visible=false;
                    Controls.Remove(browser);
                    Controls.Add(legacyRoot);
                    legacyRoot.Visible=true;
                    legacyRoot.BringToFront();
                    MessageBox.Show(this,"网页界面暂不可用，已显示原界面。\r\n"+ex.Message,
                        "Nexa Arena",MessageBoxButtons.OK,MessageBoxIcon.Warning);
                }
            };
        }

        private async System.Threading.Tasks.Task CaptureScreenshot(string file)
        {
            using(var stream=new FileStream(Path.Combine(screenshotDirectory,file),FileMode.Create,FileAccess.Write,FileShare.None))
                await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,stream);
        }

        private async System.Threading.Tasks.Task WritePreviewSize(string page)
        {
            string metrics=await browser.ExecuteScriptAsync("{const s=document.querySelector('.app-shell');const c=getComputedStyle(s);({viewport:innerHeight,root:document.querySelector('#root')?.getBoundingClientRect().height,shell:s.getBoundingClientRect().height,sidebar:document.querySelector('.app-sidebar')?.getBoundingClientRect().height,main:document.querySelector('.app-main')?.getBoundingClientRect().height,body:document.body.getBoundingClientRect().height,rows:c.gridTemplateRows,align:c.alignContent,gap:c.rowGap})}");
            File.AppendAllText(Path.Combine(screenshotDirectory,"sizes.txt"),page+" form="+ClientSize.Height+" browser="+browser.ClientSize.Height+" web="+metrics+Environment.NewLine);
        }

        private async System.Threading.Tasks.Task ClickPreviewTab(int index)
        {
            string rectangle=await browser.ExecuteScriptAsync("{const r=document.querySelectorAll('[data-slot=\"tabs-trigger\"]')["+index+"].getBoundingClientRect();({x:r.x+r.width/2,y:r.y+r.height/2})}");
            var point=json.Deserialize<Dictionary<string,object>>(rectangle);
            double x=Convert.ToDouble(point["x"]),y=Convert.ToDouble(point["y"]);
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                json.Serialize(new {type="mousePressed",x,y,button="left",clickCount=1}));
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",
                json.Serialize(new {type="mouseReleased",x,y,button="left",clickCount=1}));
        }
        private async System.Threading.Tasks.Task ChoosePreviewManager(string label)
        {
            await ClickPreviewScript("document.querySelector('[data-management-module] [data-slot=select-trigger]')");
            await System.Threading.Tasks.Task.Delay(100);
            string script="Array.from(document.querySelectorAll('[role=option]')).find(x=>x.textContent.trim()==="+json.Serialize(label)+")";
            await browser.ExecuteScriptAsync("("+script+").scrollIntoView({block:'nearest'})");
            await ClickPreviewScript(script);await System.Threading.Tasks.Task.Delay(150);
        }
        private async System.Threading.Tasks.Task ClickPreviewScript(string element)
        {
            string rectangle=await browser.ExecuteScriptAsync("{const r=("+element+").getBoundingClientRect();({x:r.x+r.width/2,y:r.y+r.height/2})}");
            var point=json.Deserialize<Dictionary<string,object>>(rectangle);double x=Convert.ToDouble(point["x"]),y=Convert.ToDouble(point["y"]);
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",json.Serialize(new {type="mousePressed",x,y,button="left",clickCount=1}));
            await browser.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent",json.Serialize(new {type="mouseReleased",x,y,button="left",clickCount=1}));
        }

        private async void MessageReceived(object sender,CoreWebView2WebMessageReceivedEventArgs e)
        {
            if(!e.Source.StartsWith(AppOrigin,StringComparison.OrdinalIgnoreCase))return;
            int id=0;
            try
            {
                var request=json.Deserialize<Dictionary<string,object>>(e.WebMessageAsJson);
                id=Int(request,"id",0);
                string action=String(request,"action",null);
                var data=request.ContainsKey("data")?request["data"] as Dictionary<string,object>:null;
                if(string.IsNullOrEmpty(action))throw new InvalidOperationException("操作名称缺失。");
                if(Program.UiPreview&&!IsReadOnly(action))
                    throw new InvalidOperationException("预览模式不会修改显示、游戏、音频或系统设置。");
                data=data??new Dictionary<string,object>();object result;
                if(action=="manager.inspect")
                {var manager=Managers();string module=String(data,"module","startup"),game=module=="nvidia-VALORANT"?"VALORANT":"CS2";manager.SelectGamePath(game,OptimizationPath(game));result=await System.Threading.Tasks.Task.Run(()=>manager.Catalog(module));}
                else if(action=="manager.change")
                {var manager=Managers();string module=String(data,"module",null),target=String(data,"target",null),choice=String(data,"choice",null);bool restore=Bool(data,"restore",false);if((module=="device-msi"||module=="interrupt-affinity")&&!Bool(data,"riskAcknowledged",false))throw new InvalidOperationException("请先确认实验性设备参数风险。");result=await System.Threading.Tasks.Task.Run(()=>manager.Change(module,target,choice,restore));}
                else if(action=="network.test")
                {string host=String(data,"host",null);result=await System.Threading.Tasks.Task.Run(()=>WindowsManagerBackend.NetworkTest(host));}
                else if(action=="appx.list")result=await System.Threading.Tasks.Task.Run(()=>AppxManagement.List());
                else if(action=="appx.remove")
                {string package=String(data,"package",null);bool acknowledged=Bool(data,"acknowledged",false);result=await System.Threading.Tasks.Task.Run(()=>AppxManagement.Remove(package,acknowledged));}
                else if(action=="appx.install")
                {
                    using(var dialog=new OpenFileDialog {Filter="Windows 应用包|*.appx;*.appxbundle;*.msix;*.msixbundle",CheckFileExists=true})
                    {
                        if(dialog.ShowDialog(this)!=DialogResult.OK)result=new {ok=false};
                        else
                        {
                            string packagePath=dialog.FileName;
                            string confirmation="安装包：\r\n"+packagePath+"\r\n\r\n将安装或更新当前 Windows 用户的应用。此操作不是普通可逆调优，不能使用原值备份撤销；请先备份需要的应用数据。\r\n\r\nWindows 将验证包签名和依赖，工具不会导入证书或绕过签名。是否确认安装？";
                            if(MessageBox.Show(this,confirmation,"确认安装/更新应用",MessageBoxButtons.OKCancel,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)!=DialogResult.OK)result=new {ok=false};
                            else result=await System.Threading.Tasks.Task.Run(()=>AppxManagement.Install(packagePath));
                        }
                    }
                }
                else if(action=="readiness.inspect")
                {string game=String(data,"game","CS2"),path=OptimizationPath(game);result=await System.Threading.Tasks.Task.Run(()=>ReadinessReport.Build(game,path));}
                else if(action=="timing.measure")result=await System.Threading.Tasks.Task.Run(()=>SystemRepairJobs.TimingMeasure());
                else result=HandleAction(action,data);
                Reply(id,true,result,null);
            }
            catch(Exception ex)
            {
                ErrorLog.Write(ex);
                Reply(id,false,null,ex.Message);
            }
        }
        private static bool IsReadOnly(string action)
        {
            return new[]{"bootstrap","display.state","audio.devices","audio.sessions",
                "settings.get","sensitivity.calculate","commands.generate","optimizer.inspect","memory.status","manager.inspect","process.status","appx.list","readiness.inspect","repair.status","timing.measure"}.Contains(action);
        }
        private void Reply(int id,bool success,object data,string error)
        {
            if(browser.IsDisposed||!browser.IsHandleCreated||browser.CoreWebView2==null)return;
            browser.CoreWebView2.PostWebMessageAsJson(json.Serialize(new {replyTo=id,ok=success,data,error}));
        }
        private void SendEvent(string name,object data)
        {
            if(browser.CoreWebView2==null)return;
            browser.CoreWebView2.PostWebMessageAsJson(json.Serialize(new {eventName=name,data}));
        }

        private object HandleAction(string action,Dictionary<string,object> data)
        {
            switch(action)
            {
                case "bootstrap": return Bootstrap();
                case "display.state": return WebDisplayState();
                case "display.select": WebSelectMode(Int(data,"width",0),Int(data,"height",0));return WebDisplayState();
                case "display.add": WebAddMode(Int(data,"width",0),Int(data,"height",0));return WebDisplayState();
                case "display.repair": WebRepairConfig();return WebDisplayState();
                case "display.switch": WebSwitch();return WebDisplayState();
                case "display.restore": WebRestore();return WebDisplayState();
                case "display.auto": WebAutoRestore(Bool(data,"enabled",false));return WebDisplayState();
                case "clipboard.set": Clipboard.SetText(String(data,"text",""));return true;
                case "crosshair.more": Process.Start(new ProcessStartInfo("https://crosshair.club/builder") {UseShellExecute=true});return true;
                case "filter.apply":
                    FilterPreset preset=GameFilterPage.Presets().FirstOrDefault(x=>x.Name==String(data,"name",null));
                    if(preset==null)throw new InvalidOperationException("未知滤镜方案。");
                    DisplayFilterService.Apply(preset);return true;
                case "filter.restore": DisplayFilterService.Restore();return true;
                case "commands.generate": return Cs2Commands.Generate(CommandOptions(data));
                case "commands.export": return ExportCommands(CommandOptions(data));
                case "sensitivity.calculate": return Calculate(data);
                case "sensitivity.save": return SaveSensitivity(data);
                case "audio.devices": return new {output=GameAudio.Devices(0),input=GameAudio.Devices(1)};
                case "audio.sessions": return GameAudio.Sessions(String(data,"deviceId",null));
                case "audio.default": GameAudio.SetDefault(String(data,"id",null),Int(data,"flow",0));return true;
                case "audio.volume": GameAudio.SetEndpointVolume(String(data,"id",null),Int(data,"volume",0),Bool(data,"muted",false));return true;
                case "audio.session": return SetAudioSession(data);
                case "audio.micToggle": return GameAudio.ToggleDefaultMicrophone();
                case "optimizer.inspect": return OptimizationInspect(String(data,"game","CS2"));
                case "optimizer.pick": return PickOptimizationGame(String(data,"game","CS2"));
                case "optimizer.apply": return Tuning().Change(StringArray(data,"ids"),OptimizationPath(String(data,"game","CS2")),Bool(data,"restore",false));
                case "optimizer.profileExport":return ExportOptimizationProfile(String(data,"game","CS2"),StringArray(data,"ids"));
                case "optimizer.profileImport":return ImportOptimizationProfile();
                case "process.status":return ProcessProfiles().Status(String(data,"game","CS2"));
                case "process.save":return ProcessProfiles().Save(new GameProcessProfile {Game=String(data,"game","CS2"),Priority=String(data,"priority","AboveNormal"),Affinity=String(data,"affinity",""),HighPower=Bool(data,"highPower",false)});
                case "process.configure":return ProcessProfiles().Configure(String(data,"game","CS2"),Bool(data,"enabled",false));
                case "repair.status":return repairJobs.Status();
                case "repair.start":return repairJobs.Start(String(data,"task",null),Bool(data,"confirmed",false));
                case "optimizer.priority": return GameOptimizer.SetPriority(String(data,"game","CS2"),Bool(data,"enabled",false));
                case "optimizer.graphics": return OpenGraphicsSettings(String(data,"game","CS2"));
                case "optimizer.gameMode": OpenSettings("ms-settings:gaming-gamemode");return true;
                case "optimizer.power": OpenSettings("ms-settings:powersleep");return true;
                case "optimizer.settings": return OptimizationSettings(String(data,"page","graphics"));
                case "memory.status":return memoryCleaner.Status();
                case "memory.configure":return memoryCleaner.Configure(Bool(data,"enabled",false),Int(data,"listMb",1024),Int(data,"freeMb",1024),Bool(data,"onlyGame",true));
                case "memory.purge":return memoryCleaner.Purge();
                case "memory.timer":return memoryCleaner.SetTimer(Bool(data,"enabled",false),(double)Decimal(data,"milliseconds",1));
                case "settings.get": return SettingsSnapshot();
                case "settings.save": return SaveSettings(data);
                default: throw new InvalidOperationException("未知操作："+action);
            }
        }

        private object Bootstrap()
        {
            var modes=DisplayModeService.GetSupportedModes().Where(x=>x.Width>=640&&x.Height>=480)
                .GroupBy(x=>x.Width+"x"+x.Height).Select(g=>new {width=g.First().Width,height=g.First().Height}).ToArray();
            return new {display=WebDisplayState(),modes,
                cs2=CrosshairLibraryPage.Cs2Presets().Select(x=>new {name=x.Name,code=x.Code,resolution=x.Style,preview=Cs2ShareCode.Decode(x.Code)}).ToArray(),
                valorant=CrosshairLibraryPage.ValorantPresets().Select(x=>new {name=x.Name,code=x.Code,description=x.Style}).ToArray(),
                filters=GameFilterPage.Presets().Select(x=>new {name=x.Name,description=x.Description,color=ColorTranslator.ToHtml(x.Swatch)}).ToArray(),
                sensitivity=new {settings=SettingsSnapshot(),resolutions=SensitivityProjection.Presets.Select(x=>new {key=x.Key,label=x.ToString()}).ToArray(),
                    methods=SensitivityFormulas.Ids.Select((id,index)=>new {id,label=SensitivityFormulas.Names[index]}).ToArray()},
                buyItems=Cs2Commands.ItemIds.Select((id,index)=>new {id,name=Cs2Commands.ItemNames[index]}).ToArray(),
                preview=Program.UiPreview};
        }

        private object SettingsSnapshot()
        {
            ToolboxSettings s=WebSettings();
            return new {minimizeToTray=s.MinimizeToTray,sourceGame=s.SourceGame,targetGame=s.TargetGame,
                sourceResolution=s.SourceResolution,targetResolution=s.TargetResolution,method=s.SensitivityMethod,
                ratio=s.CustomCsPerValorant,sourceDpi=s.SourceDpi,targetDpi=s.TargetDpi,sensitivity=s.Sensitivity,
                hotkeys=s.Hotkeys.Select(k=>new {action=k.Action,enabled=k.Enabled,modifiers=k.Modifiers,key=k.Key}).ToArray()};
        }
        private object SaveSettings(Dictionary<string,object> data)
        {
            ToolboxSettings s=WebSettings();
            s.MinimizeToTray=Bool(data,"minimizeToTray",s.MinimizeToTray);
            object hotkeys;
            if(data.TryGetValue("hotkeys",out hotkeys))
            {
                var rows=hotkeys as System.Collections.IEnumerable;
                if(rows==null)throw new ArgumentException("快捷键格式无效。");
                s.Hotkeys=rows.Cast<Dictionary<string,object>>().Select(row=>new HotkeyOption {
                    Action=String(row,"action",null),Enabled=Bool(row,"enabled",false),
                    Modifiers=(uint)Int(row,"modifiers",0),Key=Int(row,"key",0)}).ToList();
            }
            WebSaveSettings(s);return SettingsSnapshot();
        }
        private object SaveSensitivity(Dictionary<string,object> data)
        {
            ToolboxSettings s=WebSettings();
            s.SourceGame=String(data,"sourceGame",s.SourceGame);s.TargetGame=String(data,"targetGame",s.TargetGame);
            s.SourceResolution=String(data,"sourceResolution",s.SourceResolution);
            s.TargetResolution=String(data,"targetResolution",s.TargetResolution);
            s.SensitivityMethod=String(data,"method",s.SensitivityMethod);
            s.CustomCsPerValorant=Decimal(data,"ratio",s.CustomCsPerValorant);
            s.SourceDpi=Decimal(data,"sourceDpi",s.SourceDpi);
            s.TargetDpi=Decimal(data,"targetDpi",s.TargetDpi);
            s.Sensitivity=Decimal(data,"sensitivity",s.Sensitivity);
            WebSaveSettings(s);return SettingsSnapshot();
        }
        private object Calculate(Dictionary<string,object> data)
        {
            string from=String(data,"sourceGame",null),to=String(data,"targetGame",null);
            string method=String(data,"method",null);
            double value=SensitivityFormulas.Convert(method,from,to,
                SensitivityProjection.Resolve(String(data,"sourceResolution",null)),
                SensitivityProjection.Resolve(String(data,"targetResolution",null)),
                (double)Decimal(data,"sensitivity",0),
                (double)Decimal(data,"sourceDpi",0),(double)Decimal(data,"targetDpi",0),
                (double)Decimal(data,"ratio",0));
            return new {value,formatted=SensitivityProjection.DisplayNumber(value),targetGame=to};
        }
        private static Cs2CommandOptions CommandOptions(Dictionary<string,object> data)
        {
            return new Cs2CommandOptions {FpsLimit=Int(data,"fpsLimit",300),ShowFps=Bool(data,"showFps",false),
                Practice=Bool(data,"practice",false),UnlimitedAmmo=Bool(data,"unlimitedAmmo",false),
                BuyAnywhere=Bool(data,"buyAnywhere",false),GrenadePreview=Bool(data,"grenadePreview",false),
                BuyKey=String(data,"buyKey","F6"),Items=StringArray(data,"items")};
        }
        private object ExportCommands(Cs2CommandOptions options)
        {
            string value=Cs2Commands.Generate(options);
            using(var dialog=new SaveFileDialog {Filter="CS2 配置 (*.cfg)|*.cfg",FileName="nexaarena.cfg",
                DefaultExt="cfg",AddExtension=true,OverwritePrompt=true})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return new {saved=false};
                File.WriteAllText(dialog.FileName,value,new UTF8Encoding(false));
                return new {saved=true,path=dialog.FileName};
            }
        }
        private object SetAudioSession(Dictionary<string,object> data)
        {
            string deviceId=String(data,"deviceId",null),sessionId=String(data,"sessionId",null);
            AudioSessionInfo session=GameAudio.Sessions(deviceId).FirstOrDefault(x=>x.SessionId==sessionId);
            if(session==null)throw new InvalidOperationException("音频会话已结束，请刷新列表。");
            GameAudio.SetSession(session,Int(data,"volume",session.Volume),Bool(data,"muted",session.Muted));return true;
        }
        private object OpenGraphicsSettings(string game)
        {
            string expected=GameOptimizer.ExecutableName(game);
            string path=GameOptimizer.RunningPath(game);
            if(string.IsNullOrEmpty(path))
            {
                using(var dialog=new OpenFileDialog {Title="选择 "+game+" 的游戏主程序（"+expected+"）",
                    Filter="游戏程序 (*.exe)|*.exe",CheckFileExists=true})
                {
                    if(dialog.ShowDialog(this)!=DialogResult.OK)return new {opened=false,path=(string)null};
                    path=dialog.FileName;
                }
            }
            if(!GameOptimizer.IsExecutable(game,path))
                throw new InvalidOperationException("请选择游戏主程序 "+expected+"，不要选择启动器或其他 EXE。");
            Clipboard.SetText(path);
            OpenSettings("ms-settings:display-advancedgraphics");
            return new {opened=true,path};
        }
        private SystemTuning Tuning(){return tuning??(tuning=new SystemTuning());}
        private OptimizationManagers Managers(){return managers??(managers=new OptimizationManagers());}
        private GameProcessProfiles ProcessProfiles(){return processProfiles??(processProfiles=new GameProcessProfiles(Managers()));}
        private object ExportOptimizationProfile(string game,string[] ids)
        {
            var profile=new TuningProfile {Version=1,Game=game,Ids=ids==null?null:ids.ToList()};profile.Validate();
            using(var dialog=new SaveFileDialog {Filter="Nexa 优化方案 (*.nexa.xml)|*.nexa.xml",FileName=game+"-optimization.nexa.xml",OverwritePrompt=true,AddExtension=true,DefaultExt="nexa.xml"})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return new {saved=false};
                using(var stream=File.Create(dialog.FileName))new System.Xml.Serialization.XmlSerializer(typeof(TuningProfile)).Serialize(stream,profile);
                return new {saved=true};
            }
        }
        private object ImportOptimizationProfile()
        {
            using(var dialog=new OpenFileDialog {Filter="Nexa 优化方案 (*.nexa.xml)|*.nexa.xml",CheckFileExists=true})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return new {loaded=false};
                if(new FileInfo(dialog.FileName).Length>65536)throw new InvalidOperationException("方案文件过大；未应用任何设置。");
                using(var stream=File.OpenRead(dialog.FileName))
                {var profile=TuningProfile.Read(stream);return new {loaded=true,game=profile.Game,ids=profile.Ids};}
            }
        }
        private string OptimizationPath(string game)
        {
            GameOptimizer.ExecutableName(game);
            string path=GameOptimizer.RunningPath(game);
            if(string.IsNullOrEmpty(path))optimizationPaths.TryGetValue(game,out path);
            return path;
        }
        private object OptimizationInspect(string game)
        {
            string path=OptimizationPath(game);
            return new {process=GameOptimizer.Inspect(game),path,options=Tuning().Catalog(path),memory=memoryCleaner.Status()};
        }
        private object PickOptimizationGame(string game)
        {
            string expected=GameOptimizer.ExecutableName(game);
            using(var dialog=new OpenFileDialog {Title="选择 "+expected,Filter="游戏程序 (*.exe)|*.exe",CheckFileExists=true})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return OptimizationInspect(game);
                if(!GameOptimizer.IsExecutable(game,dialog.FileName))throw new InvalidOperationException("请选择 "+expected+"。");
                optimizationPaths[game]=dialog.FileName;
            }
            return OptimizationInspect(game);
        }
        private static object OptimizationSettings(string page)
        {
            switch(page)
            {
                case "graphics":OpenSettings("ms-settings:display-advancedgraphics");break;
                case "graphics-default":OpenSettings("ms-settings:display-advancedgraphics-default");break;
                case "startup":OpenSettings("ms-settings:startupapps");break;
                case "network":OpenSettings("ms-settings:network-status");break;
                case "services":Process.Start(new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"services.msc")) {UseShellExecute=true});break;
                default:throw new ArgumentException("未知系统设置页。");
            }
            return true;
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing)memoryCleaner.Dispose();
            base.Dispose(disposing);
        }
        private static void OpenSettings(string uri)
        {
            Process.Start(new ProcessStartInfo(uri) {UseShellExecute=true});
        }
        private static string String(Dictionary<string,object> data,string key,string fallback)
        {object value;return data!=null&&data.TryGetValue(key,out value)&&value!=null?Convert.ToString(value):fallback;}
        private static int Int(Dictionary<string,object> data,string key,int fallback)
        {object value;return data!=null&&data.TryGetValue(key,out value)&&value!=null?Convert.ToInt32(value):fallback;}
        private static bool Bool(Dictionary<string,object> data,string key,bool fallback)
        {object value;return data!=null&&data.TryGetValue(key,out value)&&value!=null?Convert.ToBoolean(value):fallback;}
        private static decimal Decimal(Dictionary<string,object> data,string key,decimal fallback)
        {object value;return data!=null&&data.TryGetValue(key,out value)&&value!=null?Convert.ToDecimal(value):fallback;}
        private static string[] StringArray(Dictionary<string,object> data,string key)
        {object value;if(data==null||!data.TryGetValue(key,out value)||value==null)return new string[0];var list=value as System.Collections.IEnumerable;return list==null||value is string?new string[0]:list.Cast<object>().Select(Convert.ToString).ToArray();}
        private static int[] IntArray(Dictionary<string,object> data,string key)
        {object value;if(data==null||!data.TryGetValue(key,out value)||value==null)return new int[0];var list=value as System.Collections.IEnumerable;return list==null||value is string?new int[0]:list.Cast<object>().Select(Convert.ToInt32).ToArray();}
    }
}
