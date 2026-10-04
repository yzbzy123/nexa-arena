using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using Microsoft.Win32;

namespace NexaArena
{
    internal static class ReadinessReport
    {
        internal static object Build(string game,string path)
        {
            var hardware=new List<object>();
            foreach(string query in new[]{"SELECT Name,NumberOfCores,NumberOfLogicalProcessors FROM Win32_Processor","SELECT Name,DriverVersion,Status FROM Win32_VideoController"})
                using(var searcher=new ManagementObjectSearcher(query))using(var rows=searcher.Get())foreach(ManagementObject row in rows)using(row)
                    hardware.Add(new {name=Convert.ToString(row["Name"]),details=string.Join(" · ",row.Properties.Cast<PropertyData>().Where(x=>x.Name!="Name"&&x.Value!=null).Select(x=>x.Name+"="+Convert.ToString(x.Value)).ToArray())});
            string gameMode="Windows 默认",hvci="未显式配置";
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\GameBar"))if(key!=null&&key.GetValue("AutoGameModeEnabled")!=null)gameMode=Convert.ToString(key.GetValue("AutoGameModeEnabled"));
            using(var key=Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"))if(key!=null&&key.GetValue("Enabled")!=null)hvci=Convert.ToString(key.GetValue("Enabled"));
            string cpu="不可用";
            try{using(var searcher=new ManagementObjectSearcher("SELECT PercentProcessorTime FROM Win32_PerfFormattedData_PerfOS_Processor WHERE Name='_Total'"))using(var rows=searcher.Get())foreach(ManagementObject row in rows)using(row)cpu=Convert.ToString(row["PercentProcessorTime"])+"%";}catch(Exception ex){cpu="读取失败："+ex.Message;}
            var memory=MemoryCleaner.Read();
            return new {game,path,process=GameOptimizer.Inspect(game),hardware,cpuLoad=cpu,memoryTotalMb=memory.totalMb,memoryAvailableMb=memory.availableMb,gameMode,memoryIntegrityRegistry=hvci,activePowerPlan=WindowsTuningBackend.ActivePower().ToString(),sampledUtc=DateTime.UtcNow.ToString("o"),note="当前负载和配置检查，不是 FPS 记录或瓶颈百分比。注册表状态不等同于安全功能的运行时证明；不为性能关闭安全防护。"};
        }
    }
}
