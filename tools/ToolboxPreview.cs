using System;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;

namespace NexaArena
{
    internal static class ToolboxPreview
    {
        [STAThread]
        private static void Main(string[] args)
        {
            int capture=Array.IndexOf(args,"--capture");
            if(capture>=0&&capture+1<args.Length)
            {
                string folder=Path.GetFullPath(args[capture+1]);bool started=false;
                Application.Idle+=async delegate
                {
                    if(started||Application.OpenForms.Count==0)return;started=true;
                    var form=Application.OpenForms[0] as ModernMainForm;
                    try
                    {
                        Directory.CreateDirectory(folder);form.ClientSize=new Size(1200,800);await Task.Delay(300);
                        var buttons=(ModernNavButton[])typeof(ModernMainForm).GetField("navButtons",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                        string[] names={"display","aim","audio","optimizer","hotkeys","guide"};
                        for(int i=0;i<buttons.Length;i++){buttons[i].PerformClick();form.PerformLayout();await Task.Delay(100);Capture(form,Path.Combine(folder,names[i]+".png"));}
                        buttons[1].PerformClick();var pages=(Control[])typeof(ModernMainForm).GetField("pages",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);var aim=(ToolTabs)pages[1];var aimBar=aim.Controls[0].Controls[0];
                        ((Button)aimBar.Controls[1]).PerformClick();await Task.Delay(80);Capture(form,Path.Combine(folder,"sensitivity.png"));
                        ((Button)aimBar.Controls[2]).PerformClick();await Task.Delay(80);Capture(form,Path.Combine(folder,"commands.png"));
                        var game=FindNamedCombo(form,"当前游戏");game.SelectedItem="CS2";buttons[0].PerformClick();await Task.Delay(80);Capture(form,Path.Combine(folder,"cs2-filter.png"));
                        buttons[1].PerformClick();((Button)aimBar.Controls[0]).PerformClick();await Task.Delay(80);Capture(form,Path.Combine(folder,"cs2-crosshair.png"));game.SelectedItem="VALORANT";
                        buttons[3].PerformClick();await Task.Delay(80);var optimizer=Find<NativeOptimizerPage>(form);var tabs=(TabControl)typeof(NativeOptimizerPage).GetField("tabs",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(optimizer);
                        for(int i=1;i<tabs.TabPages.Count;i++){tabs.SelectedIndex=i;await Task.Delay(80);Capture(form,Path.Combine(folder,"optimizer-"+i+".png"));}
                        buttons[0].PerformClick();form.Size=new Size(1040,720);await Task.Delay(100);Capture(form,Path.Combine(folder,"compact.png"));
                        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: native WinForms pages rendered; preview mode, no system actions.\r\n"+form.ClientSize+" DPI="+form.DeviceDpi);
                    }
                    catch(Exception ex){File.WriteAllText(Path.Combine(folder,"result.txt"),"FAIL: "+ex);}
                    finally{form.Close();}
                };
            }
            if(Array.IndexOf(args,"--compact")>=0||Path.GetFileName(Application.ExecutablePath).IndexOf("Compact",StringComparison.OrdinalIgnoreCase)>=0)
            {
                EventHandler resize=null;
                resize=delegate
                {
                    if(Application.OpenForms.Count==0)return;
                    Application.Idle-=resize;
                    Application.OpenForms[0].Size=new Size(1040,720);
                };
                Application.Idle+=resize;
            }
            typeof(Program).GetMethod("Main",BindingFlags.Static|BindingFlags.NonPublic)
                .Invoke(null,new object[]{new string[]{"--ui-preview"}});
        }
        private static T Find<T>(Control root) where T:Control
        {if(root is T)return (T)root;foreach(Control child in root.Controls){var found=Find<T>(child);if(found!=null)return found;}return null;}
        private static ComboBox FindNamedCombo(Control root,string name){var combo=root as ComboBox;if(combo!=null&&combo.AccessibleName==name)return combo;foreach(Control child in root.Controls){var found=FindNamedCombo(child,name);if(found!=null)return found;}return null;}
        private static void Capture(Form form,string path)
        {using(var image=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
    }
}
