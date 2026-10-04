using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace NexaArena
{
    // Only targets the selected game's own process. Windows graphics and power
    // preferences are opened in Settings rather than changed through undocumented keys.
    internal static class GameOptimizer
    {
        private sealed class PrioritySnapshot
        {
            public long Started;
            public ProcessPriorityClass Original;
        }

        private static readonly Dictionary<int,PrioritySnapshot> originalPriorities = new Dictionary<int,PrioritySnapshot>();

        internal static string ExecutableName(string game)
        {
            if(game=="CS2")return "cs2.exe";
            if(game=="VALORANT")return "VALORANT-Win64-Shipping.exe";
            throw new ArgumentException("只支持 CS2 和 VALORANT。", "game");
        }

        internal static bool IsExecutable(string game,string path)
        {
            return !string.IsNullOrWhiteSpace(path) &&
                string.Equals(Path.GetFileName(path),ExecutableName(game),StringComparison.OrdinalIgnoreCase);
        }

        internal static Process FindRunning(string game)
        {
            string name=Path.GetFileNameWithoutExtension(ExecutableName(game));
            Process found=null;
            foreach(Process process in Process.GetProcessesByName(name))
            {
                try
                {
                    if(found==null && string.Equals(process.ProcessName,name,StringComparison.OrdinalIgnoreCase))
                    { found=process;continue; }
                }
                catch { }
                process.Dispose();
            }
            return found;
        }

        private static bool CanRestore(Process process)
        {
            PrioritySnapshot saved;
            return originalPriorities.TryGetValue(process.Id,out saved) &&
                process.StartTime.ToUniversalTime().Ticks==saved.Started;
        }

        internal static object Inspect(string game)
        {
            string executable=ExecutableName(game);
            using(Process process=FindRunning(game))
            {
                if(process==null)return new {game,executable,running=false,pid=0,path=(string)null,
                    priority=(string)null,canRestore=false,canApply=false};
                string path=null,priority=null;
                bool canRestore=false,canApply=false;
                try { path=process.MainModule.FileName; } catch { }
                try
                {
                    ProcessPriorityClass current=process.PriorityClass;
                    priority=current.ToString();canRestore=CanRestore(process);
                    canApply=!canRestore && (current==ProcessPriorityClass.Normal ||
                        current==ProcessPriorityClass.BelowNormal || current==ProcessPriorityClass.Idle);
                }
                catch { }
                return new {game,executable,running=true,pid=process.Id,path,priority,canRestore,canApply};
            }
        }

        internal static object SetPriority(string game,bool enabled)
        {
            ExecutableName(game);
            using(Process process=FindRunning(game))
            {
                if(process==null)throw new InvalidOperationException("未检测到所选游戏进程，请先启动游戏。 ");
                long started=process.StartTime.ToUniversalTime().Ticks;
                if(enabled)
                {
                    ProcessPriorityClass current=process.PriorityClass;
                    if(current!=ProcessPriorityClass.Normal && current!=ProcessPriorityClass.BelowNormal &&
                        current!=ProcessPriorityClass.Idle)
                        throw new InvalidOperationException("当前进程已经不是普通或更低优先级，未做更改。 ");
                    originalPriorities[process.Id]=new PrioritySnapshot {Started=started,Original=current};
                    try { process.PriorityClass=ProcessPriorityClass.AboveNormal; }
                    catch { originalPriorities.Remove(process.Id);throw; }
                }
                else
                {
                    PrioritySnapshot saved;
                    if(!originalPriorities.TryGetValue(process.Id,out saved)||saved.Started!=started)
                        throw new InvalidOperationException("没有这次会话保存的原优先级，不会擅自覆盖现有设置。 ");
                    if(process.PriorityClass!=ProcessPriorityClass.AboveNormal)
                        throw new InvalidOperationException("游戏优先级已被其他程序修改，请在任务管理器中确认；此处不会覆盖。 ");
                    process.PriorityClass=saved.Original;
                    originalPriorities.Remove(process.Id);
                }
            }
            return Inspect(game);
        }

        internal static string RunningPath(string game)
        {
            ExecutableName(game);
            using(Process process=FindRunning(game))
            {
                if(process==null)return null;
                try { return process.MainModule.FileName; }
                catch { return null; }
            }
        }

        internal static void RestoreAll()
        {
            foreach(var entry in new List<KeyValuePair<int,PrioritySnapshot>>(originalPriorities))
            {
                try
                {
                    using(Process process=Process.GetProcessById(entry.Key))
                        if(process.StartTime.ToUniversalTime().Ticks==entry.Value.Started &&
                            process.PriorityClass==ProcessPriorityClass.AboveNormal)
                            process.PriorityClass=entry.Value.Original;
                }
                catch(Exception ex) { ErrorLog.Write(ex); }
                finally { originalPriorities.Remove(entry.Key); }
            }
        }
    }
}
