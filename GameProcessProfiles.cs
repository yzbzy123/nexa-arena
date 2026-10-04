using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;

namespace NexaArena
{
    public sealed class GameProcessProfile
    {
        public string Game,Priority="AboveNormal",Affinity="";
        public bool HighPower;
        internal void Validate()
        {
            GameOptimizer.ExecutableName(Game);
            if(Priority!="Normal"&&Priority!="AboveNormal"&&Priority!="High")throw new ArgumentException("不支持实时或未知优先级。");
            GameProcessProfiles.AffinityMask(Affinity,Environment.ProcessorCount);
        }
    }
    public sealed class GameProcessProfileFile { public List<GameProcessProfile> Items=new List<GameProcessProfile>(); }
    public sealed class ProcessSessionBackup
    {
        public int Pid;public long Started;public string Game;
        public ProcessPriorityClass Priority,WrittenPriority;
        public long Affinity,WrittenAffinity;
        public bool PriorityChanged,AffinityChanged;
    }
    public sealed class ProcessSessionBackups { public List<ProcessSessionBackup> Items=new List<ProcessSessionBackup>(); }
    internal sealed class GameProcessProfiles:IDisposable
    {
        private sealed class Snapshot
        {
            public int Pid;public long Started;public string Game;
            public ProcessPriorityClass Priority,WrittenPriority;
            public IntPtr Affinity,WrittenAffinity;
            public bool PriorityChanged,AffinityChanged;
        }
        private readonly Dictionary<int,Snapshot> originals=new Dictionary<int,Snapshot>();
        private readonly Timer timer=new Timer {Interval=2500};
        private readonly OptimizationManagers manager;
        private readonly string file,sessionFile;
        private GameProcessProfileFile profiles=new GameProcessProfileFile();
        private bool enabled,powerOwned,powerConsidered;
        private string message="尚未启动自动进程配置；重开工具不会自动启动。";
        internal GameProcessProfiles(OptimizationManagers manager)
        {
            this.manager=manager;file=Path.Combine(SavedState.DirectoryPath,"game-process-profiles.xml");
            sessionFile=Path.Combine(SavedState.DirectoryPath,"process-session-backups.bin");
            if(File.Exists(sessionFile))
            {
                if(new FileInfo(sessionFile).Length>65536)throw new InvalidOperationException("进程会话备份过大，未启动自动操作。");
                byte[] bytes=ProtectedData.Unprotect(File.ReadAllBytes(sessionFile),null,DataProtectionScope.CurrentUser);
                using(var memory=new MemoryStream(bytes))using(var reader=XmlReader.Create(memory,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
                {
                    var sessions=(ProcessSessionBackups)new XmlSerializer(typeof(ProcessSessionBackups)).Deserialize(reader);
                    if(sessions==null||sessions.Items==null||sessions.Items.Count>4)throw new InvalidOperationException("进程会话备份无效。");
                    foreach(var saved in sessions.Items)
                    {
                        GameOptimizer.ExecutableName(saved.Game);
                        if(saved.Pid<=0||saved.Started<=0||saved.Affinity==0||saved.WrittenAffinity==0||saved.Priority==ProcessPriorityClass.RealTime||saved.WrittenPriority==ProcessPriorityClass.RealTime||!Enum.IsDefined(typeof(ProcessPriorityClass),saved.Priority)||!Enum.IsDefined(typeof(ProcessPriorityClass),saved.WrittenPriority))throw new InvalidOperationException("进程会话备份字段无效。");
                        originals[saved.Pid]=new Snapshot {Pid=saved.Pid,Started=saved.Started,Game=saved.Game,Priority=saved.Priority,WrittenPriority=saved.WrittenPriority,Affinity=new IntPtr(saved.Affinity),WrittenAffinity=new IntPtr(saved.WrittenAffinity),PriorityChanged=saved.PriorityChanged,AffinityChanged=saved.AffinityChanged};
                    }
                    if(originals.Count>0)message="存在上次会话原值；请先停止监控/还原，按 PID 与启动时间核对，不会自动修改。";
                }
            }
            if(File.Exists(file))
                using(var reader=XmlReader.Create(file,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
                {
                    profiles=(GameProcessProfileFile)new XmlSerializer(typeof(GameProcessProfileFile)).Deserialize(reader);
                    if(profiles==null||profiles.Items==null||profiles.Items.Count>2||profiles.Items.Select(x=>x.Game).Distinct().Count()!=profiles.Items.Count)throw new InvalidOperationException("进程方案格式无效，未启动自动操作。");
                    foreach(var profile in profiles.Items)profile.Validate();
                }
            timer.Tick+=delegate{Tick();};
        }
        internal static ulong AffinityMask(string value,int processors)
        {
            if(string.IsNullOrWhiteSpace(value))return 0;
            if(processors<1||processors>64)throw new ArgumentException("超过 64 个逻辑 CPU 的多处理器组系统暂不设置亲和性。");
            string text=value.Trim();if(text.StartsWith("0x",StringComparison.OrdinalIgnoreCase))text=text.Substring(2);
            ulong mask;if(text.Length>16||!ulong.TryParse(text,NumberStyles.AllowHexSpecifier,CultureInfo.InvariantCulture,out mask)||mask==0)throw new ArgumentException("亲和性请输入非零十六进制 CPU 位掩码。");
            ulong allowed=processors==64?ulong.MaxValue:(1UL<<processors)-1;
            if((mask&~allowed)!=0)throw new ArgumentException("亲和性包含本机不存在的逻辑 CPU。");return mask;
        }
        private GameProcessProfile Profile(string game){return profiles.Items.FirstOrDefault(x=>x.Game==game)??new GameProcessProfile {Game=game};}
        internal object Status(string game)
        {GameOptimizer.ExecutableName(game);return new {enabled,message,profile=Profile(game),saved=profiles.Items.ToArray(),logicalCpus=Environment.ProcessorCount,affinitySupported=Environment.ProcessorCount<=64,highPowerAvailable=WindowsTuningBackend.HasHighPower(),activeSessions=originals.Count};}
        private void SaveSessions()
        {
            if(Program.UiPreview)return;
            var backups=new ProcessSessionBackups {Items=originals.Values.Select(x=>new ProcessSessionBackup {Pid=x.Pid,Started=x.Started,Game=x.Game,Priority=x.Priority,WrittenPriority=x.WrittenPriority,Affinity=x.Affinity.ToInt64(),WrittenAffinity=x.WrittenAffinity.ToInt64(),PriorityChanged=x.PriorityChanged,AffinityChanged=x.AffinityChanged}).ToList()};
            byte[] bytes;using(var memory=new MemoryStream()){new XmlSerializer(typeof(ProcessSessionBackups)).Serialize(memory,backups);bytes=ProtectedData.Protect(memory.ToArray(),null,DataProtectionScope.CurrentUser);}
            Directory.CreateDirectory(Path.GetDirectoryName(sessionFile));File.WriteAllBytes(sessionFile+".tmp",bytes);if(File.Exists(sessionFile))File.Replace(sessionFile+".tmp",sessionFile,sessionFile+".bak");else File.Move(sessionFile+".tmp",sessionFile);
        }
        internal object Save(GameProcessProfile profile)
        {
            if(enabled)throw new InvalidOperationException("先停止监控并还原本次会话，再修改进程方案。");profile.Validate();
            profiles.Items.RemoveAll(x=>x.Game==profile.Game);profiles.Items.Add(profile);
            Directory.CreateDirectory(Path.GetDirectoryName(file));using(var stream=File.Create(file+".tmp"))new XmlSerializer(typeof(GameProcessProfileFile)).Serialize(stream,profiles);
            if(File.Exists(file))File.Replace(file+".tmp",file,file+".bak");else File.Move(file+".tmp",file);
            message="已保存方案；未更改任何进程或电源。";return Status(profile.Game);
        }
        internal object Configure(string game,bool start)
        {
            GameOptimizer.ExecutableName(game);
            if(start)
            {
                if(profiles.Items.Count==0)throw new InvalidOperationException("请先保存至少一个游戏方案。");
                if(originals.Count>0)throw new InvalidOperationException("先还原尚未结束的进程会话，再启动新监控。");
                if(profiles.Items.Any(x=>x.HighPower)&&!WindowsTuningBackend.HasHighPower())throw new InvalidOperationException("系统没有已有高性能计划，请在方案中关闭自动电源切换。");
                enabled=true;timer.Start();message="正在监控已保存方案的游戏；未保存方案的游戏不会更改。";Tick();
            }
            else {enabled=false;timer.Stop();try{Restore();message=originals.Count==0?"监控已停止，本次进程原值已处理。":"监控已停止，部分进程无法访问或有外部改动，原值备份保留。";}catch(Exception ex){message="监控已停止，部分原值仍需核对："+ex.Message;}}
            return Status(game);
        }
        private void Tick()
        {
            if(!enabled)return;
            try
            {
                bool needPower=false;
                foreach(var profile in profiles.Items)
                    using(var process=GameOptimizer.FindRunning(profile.Game))
                    {
                        if(process==null)continue;needPower|=profile.HighPower;
                        string path=process.MainModule.FileName;if(!GameOptimizer.IsExecutable(profile.Game,path))continue;
                        long started=process.StartTime.ToUniversalTime().Ticks;Snapshot saved;
                        if(originals.TryGetValue(process.Id,out saved)&&saved.Started==started)continue;
                        var snapshot=new Snapshot {Pid=process.Id,Started=started,Game=profile.Game,Priority=process.PriorityClass,Affinity=process.ProcessorAffinity};
                        if(snapshot.Priority==ProcessPriorityClass.RealTime)throw new InvalidOperationException("现有实时优先级不会被自动方案覆盖。");
                        snapshot.WrittenPriority=(ProcessPriorityClass)Enum.Parse(typeof(ProcessPriorityClass),profile.Priority);
                        ulong mask=AffinityMask(profile.Affinity,Environment.ProcessorCount);snapshot.WrittenAffinity=mask==0?snapshot.Affinity:new IntPtr(unchecked((long)mask));
                        originals[process.Id]=snapshot;
                        snapshot.PriorityChanged=snapshot.Priority!=snapshot.WrittenPriority;
                        snapshot.AffinityChanged=snapshot.Affinity!=snapshot.WrittenAffinity;
                        SaveSessions();
                        if(snapshot.PriorityChanged)process.PriorityClass=snapshot.WrittenPriority;
                        if(snapshot.AffinityChanged)process.ProcessorAffinity=snapshot.WrittenAffinity;
                        if(process.PriorityClass!=snapshot.WrittenPriority||process.ProcessorAffinity!=snapshot.WrittenAffinity)throw new InvalidOperationException("进程配置读回校验未通过，停止并尝试还原。");
                        message="已处理 "+profile.Game+" 当前进程；游戏退出或停止监控后结束本次会话。";
                    }
                int beforeCleanup=originals.Count;
                foreach(var entry in originals.ToArray())
                {
                    try{using(var process=Process.GetProcessById(entry.Key))if(process.StartTime.ToUniversalTime().Ticks!=entry.Value.Started)originals.Remove(entry.Key);}
                    catch(ArgumentException){originals.Remove(entry.Key);}
                }
                if(originals.Count!=beforeCleanup)SaveSessions();
                if(needPower&&!powerConsidered)
                {
                    if(manager.HasBackup("power","active"))throw new InvalidOperationException("已有电源会话备份，请先从系统管理中的电源会话核对或还原。");
                    manager.Change("power","active","8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c",false);powerOwned=manager.HasBackup("power","active");powerConsidered=true;
                }
                if(!needPower){if(powerOwned){manager.Change("power","active",null,true);powerOwned=false;}powerConsidered=false;}
            }
            catch(Exception ex){enabled=false;timer.Stop();try{Restore();}catch(Exception restoreError){ErrorLog.Write(restoreError);}message="监控已停止："+ex.Message;ErrorLog.Write(ex);}
        }
        private void Restore()
        {
            foreach(var entry in originals.ToArray())
            {
                try
                {
                    using(var process=Process.GetProcessById(entry.Key))
                    {
                        if(process.StartTime.ToUniversalTime().Ticks!=entry.Value.Started){originals.Remove(entry.Key);continue;}
                        var saved=entry.Value;
                        if(!string.Equals(process.ProcessName,Path.GetFileNameWithoutExtension(GameOptimizer.ExecutableName(saved.Game)),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("进程身份不匹配，未覆盖。");
                        bool affinityDone=!saved.AffinityChanged||process.ProcessorAffinity==saved.Affinity,priorityDone=!saved.PriorityChanged||process.PriorityClass==saved.Priority;
                        if(!affinityDone&&process.ProcessorAffinity==saved.WrittenAffinity){process.ProcessorAffinity=saved.Affinity;affinityDone=process.ProcessorAffinity==saved.Affinity;}
                        if(!priorityDone&&process.PriorityClass==saved.WrittenPriority){process.PriorityClass=saved.Priority;priorityDone=process.PriorityClass==saved.Priority;}
                        if(affinityDone&&priorityDone)originals.Remove(entry.Key);
                    }
                }
                catch(ArgumentException){originals.Remove(entry.Key);}
                catch(Exception ex){ErrorLog.Write(ex);}
            }
            SaveSessions();
            if(powerOwned){manager.Change("power","active",null,true);powerOwned=false;}powerConsidered=false;
        }
        public void Dispose(){enabled=false;timer.Stop();if(!Program.UiPreview)try{Restore();}catch(Exception ex){ErrorLog.Write(ex);}timer.Dispose();}
    }
}
