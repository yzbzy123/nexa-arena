using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace NexaArena
{
    internal static class WebPreview
    {
        [STAThread]
        private static void Main(string[] args)
        {
            typeof(Program).GetProperty("UiPreview",BindingFlags.Static|BindingFlags.NonPublic)
                .GetSetMethod(true).Invoke(null,new object[]{true});
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new WebMainForm(args.Length==2&&args[0]=="--capture"?args[1]:null));
        }
    }
}
