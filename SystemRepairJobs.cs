using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace NexaArena
{
    internal sealed class SystemRepairJobs:IDisposable
    {
        private readonly object gate=new object();
        private Process process;private readonly List<string> output=new List<string>();
        private string current,last="尚未运行系统检查或修复。";
        internal static string[] Command(string id)
        {
            switch(id)
            {
                case "sfc-verify":return new[]{"sfc.exe","/verifyonly"};
                case "sfc-repair":return new[]{"sfc.exe","/scannow"};
                case "dism-scan":return new[]{"Dism.exe","/Online /Cleanup-Image /ScanHealth /NoRestart"};
                case "dism-repair":return new[]{"Dism.exe","/Online /Cleanup-Image /RestoreHealth /NoRestart"};
                case "dns-flush":return new[]{"ipconfig.exe","/flushdns"};
                default:throw new ArgumentException("未知系统检查/修复任务。");
            }
        }
        internal object Start(string id,bool confirmed)
        {
            if(!confirmed)throw new InvalidOperationException("请先确认系统任务的影响和运行时间。");
            string[] command=Command(id);
            lock(gate)
            {
                if(process!=null&&!process.HasExited)throw new InvalidOperationException("已有系统任务在运行，请等待结束。");
                if(process!=null)process.Dispose();output.Clear();current=id;
                var encoding=id.StartsWith("sfc-")?System.Text.Encoding.Unicode:System.Text.Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
                process=new Process {StartInfo=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),command[0]),command[1]) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=encoding,StandardErrorEncoding=encoding},EnableRaisingEvents=true};
                process.OutputDataReceived+=delegate(object sender,DataReceivedEventArgs e){Append(e.Data);};process.ErrorDataReceived+=delegate(object sender,DataReceivedEventArgs e){Append(e.Data);};
                process.Start();process.BeginOutputReadLine();process.BeginErrorReadLine();last="系统任务正在运行；不会自动重启。需要等待完成，不要重复运行。";return Status();
            }
        }
        private void Append(string text){if(string.IsNullOrEmpty(text))return;lock(gate){output.Add(text);if(output.Count>80)output.RemoveAt(0);}}
        internal object Status()
        {
            lock(gate)
            {
                bool running=process!=null&&!process.HasExited;int? exitCode=process!=null&&process.HasExited?(int?)process.ExitCode:null;
                if(exitCode.HasValue)last="系统任务退出，代码 "+exitCode.Value+"。请阅读输出；退出代码不等于已证实修复全部问题。";
                return new {running,current,message=last,exitCode,lines=output.ToArray()};
            }
        }
        internal static object TimingMeasure()
        {
            var times=new List<double>();var watch=new Stopwatch();
            for(int i=0;i<64;i++){watch.Restart();Thread.Sleep(1);watch.Stop();times.Add(watch.Elapsed.TotalMilliseconds);}
            times.Sort();return new {samples=times.Count,requestedMs=1,averageMs=times.Average(),minMs=times.First(),maxMs=times.Last(),p95Ms=times[(int)Math.Ceiling(times.Count*.95)-1],note="线程请求休眠 1 ms 后的唤醒耗时；受负载/计时器/调度影响，不是输入、屏幕或游戏端到端延迟。没有更改计时器请求。"};
        }
        public void Dispose(){lock(gate){if(process!=null){try{process.CancelOutputRead();}catch(InvalidOperationException){}try{process.CancelErrorRead();}catch(InvalidOperationException){}process.Dispose();process=null;}}}
    }
}
