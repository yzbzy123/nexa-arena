using System;
using System.Reflection;
using System.Windows.Forms;
using System.Drawing;

namespace NexaArena
{
    internal static class ToolboxPreview
    {
        [STAThread]
        private static void Main(string[] args)
        {
            if(Array.IndexOf(args,"--compact")>=0)
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
    }
}
