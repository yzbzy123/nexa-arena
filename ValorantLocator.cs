using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;

namespace NexaArena
{
    internal sealed class ValorantGameStateLogWatcher
    {
        private string logPath;
        private long position;

        public void ResetToEnd()
        {
            logPath=FindNewestLog();
            try { position=string.IsNullOrEmpty(logPath) ? 0 : new FileInfo(logPath).Length; }
            catch { position=0; }
        }

        public string Poll()
        {
            string newest=FindNewestLog();
            if(string.IsNullOrEmpty(newest)) return null;
            if(!string.Equals(newest,logPath,StringComparison.OrdinalIgnoreCase))
            {
                logPath=newest;
                try { position=new FileInfo(logPath).Length; } catch { position=0; }
                return null;
            }
            try
            {
                using(FileStream stream=new FileStream(logPath,FileMode.Open,FileAccess.Read,
                    FileShare.ReadWrite|FileShare.Delete))
                {
                    if(stream.Length<position) position=stream.Length;
                    stream.Seek(position,SeekOrigin.Begin);
                    using(StreamReader reader=new StreamReader(stream,Encoding.UTF8,true,4096,true))
                    {
                        string text=reader.ReadToEnd();
                        position=stream.Position;
                        string state=null;
                        foreach(string line in text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))
                        {
                            if(line.IndexOf("Loopstate changed",StringComparison.OrdinalIgnoreCase)>=0 &&
                                line.IndexOf("to PREGAME",StringComparison.OrdinalIgnoreCase)>=0) state="PREGAME";
                            if((line.IndexOf("Loopstate changed",StringComparison.OrdinalIgnoreCase)>=0 &&
                                line.IndexOf("to INGAME",StringComparison.OrdinalIgnoreCase)>=0) ||
                                line.IndexOf("Current state InGame entering",StringComparison.OrdinalIgnoreCase)>=0) state="INGAME";
                        }
                        return state;
                    }
                }
            }
            catch { return null; }
        }

        private static string FindNewestLog()
        {
            List<string> candidates=new List<string>();
            foreach(string root in ValorantLocator.FindConfigRoots())
            {
                try
                {
                    DirectoryInfo config=new DirectoryInfo(root);
                    DirectoryInfo saved=config.Parent;
                    if(saved==null) continue;
                    string path=Path.Combine(saved.FullName,"Logs","ShooterGame.log");
                    if(File.Exists(path)) candidates.Add(path);
                }
                catch { }
            }
            return candidates.OrderByDescending(File.GetLastWriteTime).FirstOrDefault();
        }
    }

    internal sealed class ValorantConfigEntry
    {
        public string Path { get; private set; }
        public DateTime LastWriteTime { get; private set; }
        public bool IsShared { get; private set; }

        public ValorantConfigEntry(string path)
        {
            Path = path;
            LastWriteTime = File.GetLastWriteTime(path);
            DirectoryInfo windowsClient = Directory.GetParent(path);
            IsShared = windowsClient != null && windowsClient.Parent != null &&
                string.Equals(windowsClient.Parent.Name, "Config", StringComparison.OrdinalIgnoreCase);
        }

        public override string ToString()
        {
            string kind = IsShared ? "公共配置" : "账号配置";
            return string.Format("[{0}]  {1:yyyy-MM-dd HH:mm}  ·  {2}", kind, LastWriteTime, Path);
        }
    }

    internal static class ValorantLocator
    {
        public static bool IsGameRunning()
        {
            return RunningGameProcesses().Count > 0;
        }

        public static List<string> RunningGameProcesses()
        {
            List<string> names = new List<string>();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    string name = process.ProcessName;
                    if (name.Equals("VALORANT", StringComparison.OrdinalIgnoreCase) ||
                        name.IndexOf("VALORANT-Win64-Shipping", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("ShooterGame", StringComparison.OrdinalIgnoreCase) >= 0)
                        names.Add(name);
                }
                catch { }
                finally { process.Dispose(); }
            }
            return names.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }

        public static List<ValorantConfigEntry> FindConfigFiles()
        {
            HashSet<string> roots = FindConfigRoots();
            HashSet<string> files = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in roots)
            {
                if (!Directory.Exists(root)) continue;
                try
                {
                    foreach (string file in Directory.EnumerateFiles(root, "GameUserSettings.ini", SearchOption.AllDirectories))
                        files.Add(System.IO.Path.GetFullPath(file));
                }
                catch { }
            }
            return files.Select(path => new ValorantConfigEntry(path))
                .OrderByDescending(entry => entry.LastWriteTime).ToList();
        }

        public static List<ValorantConfigEntry> SelectActiveConfigs(List<ValorantConfigEntry> all)
        {
            List<ValorantConfigEntry> selected = new List<ValorantConfigEntry>();
            ValorantConfigEntry shared = all.Where(x => x.IsShared).OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
            ValorantConfigEntry account = all.Where(x => !x.IsShared).OrderByDescending(x => x.LastWriteTime).FirstOrDefault();
            if (shared != null) selected.Add(shared);
            if (account != null) selected.Add(account);
            if (selected.Count == 0 && all.Count > 0) selected.Add(all[0]);
            return selected;
        }

        public static HashSet<string> FindConfigRoots()
        {
            HashSet<string> roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            AddConfigRoot(roots, System.IO.Path.Combine(local, "VALORANT", "Saved", "Config"));

            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || drive.DriveType == DriveType.CDRom) continue;
                    AddInstallRoot(roots, System.IO.Path.Combine(drive.RootDirectory.FullName, "Tencent Games", "VALORANT"));
                    AddInstallRoot(roots, System.IO.Path.Combine(drive.RootDirectory.FullName, "Riot Games", "VALORANT"));
                    AddInstallRoot(roots, System.IO.Path.Combine(drive.RootDirectory.FullName, "VALORANT"));
                }
                catch { }
            }

            foreach (string installRoot in RegistryInstallRoots()) AddInstallRoot(roots, installRoot);
            foreach (string processRoot in RunningInstallRoots()) AddInstallRoot(roots, processRoot);
            return roots;
        }

        private static void AddInstallRoot(HashSet<string> roots, string installRoot)
        {
            if (string.IsNullOrWhiteSpace(installRoot)) return;
            AddConfigRoot(roots, System.IO.Path.Combine(installRoot, "live", "ShooterGame", "Saved", "Config"));
            AddConfigRoot(roots, System.IO.Path.Combine(installRoot, "ShooterGame", "Saved", "Config"));
        }

        private static void AddConfigRoot(HashSet<string> roots, string root)
        {
            try { if (Directory.Exists(root)) roots.Add(System.IO.Path.GetFullPath(root)); }
            catch { }
        }

        private static IEnumerable<string> RegistryInstallRoots()
        {
            List<string> roots = new List<string>();
            foreach (RegistryHive hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
            foreach (RegistryView view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                    using (RegistryKey uninstall = baseKey.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                    {
                        if (uninstall == null) continue;
                        foreach (string subName in uninstall.GetSubKeyNames())
                        using (RegistryKey app = uninstall.OpenSubKey(subName))
                        {
                            if (app == null) continue;
                            string displayName = Convert.ToString(app.GetValue("DisplayName"));
                            if (displayName.IndexOf("VALORANT", StringComparison.OrdinalIgnoreCase) < 0 &&
                                displayName.IndexOf("无畏契约", StringComparison.OrdinalIgnoreCase) < 0) continue;
                            foreach (string valueName in new[] { "InstallLocation", "DisplayIcon", "UninstallString" })
                            {
                                string raw = Convert.ToString(app.GetValue(valueName));
                                string path = ExtractExecutablePath(raw);
                                if (!string.IsNullOrWhiteSpace(path))
                                {
                                    string directory = Directory.Exists(path) ? path : System.IO.Path.GetDirectoryName(path);
                                    if (!string.IsNullOrWhiteSpace(directory)) roots.Add(directory);
                                }
                            }
                        }
                    }
                }
                catch { }
            }
            return roots.Distinct(StringComparer.OrdinalIgnoreCase);
        }

        private static string ExtractExecutablePath(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            raw = Environment.ExpandEnvironmentVariables(raw.Trim());
            if (raw.StartsWith("\""))
            {
                int end = raw.IndexOf('"', 1);
                return end > 1 ? raw.Substring(1, end - 1) : raw.Trim('"');
            }
            int exe = raw.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            return exe >= 0 ? raw.Substring(0, exe + 4) : raw;
        }

        private static IEnumerable<string> RunningInstallRoots()
        {
            List<string> roots = new List<string>();
            foreach (Process process in Process.GetProcesses())
            {
                try
                {
                    if (process.ProcessName.IndexOf("VALORANT", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    string path = process.MainModule.FileName;
                    DirectoryInfo directory = new FileInfo(path).Directory;
                    while (directory != null)
                    {
                        if (string.Equals(directory.Name, "live", StringComparison.OrdinalIgnoreCase))
                        {
                            if (directory.Parent != null) roots.Add(directory.Parent.FullName);
                            break;
                        }
                        directory = directory.Parent;
                    }
                }
                catch { }
                finally { process.Dispose(); }
            }
            return roots;
        }
    }

    internal sealed class ConfigRepairRecord
    {
        public string ConfigPath = string.Empty;
        public string BackupPath = string.Empty;
        public bool WasReadOnly = false;
    }

    internal static class ConfigSessionStore
    {
        private static string FilePath
        {
            get { return System.IO.Path.Combine(SavedState.DirectoryPath, "config-session.txt"); }
        }

        public static bool Exists { get { return File.Exists(FilePath); } }

        public static void Save(IEnumerable<ConfigRepairRecord> records)
        {
            Directory.CreateDirectory(SavedState.DirectoryPath);
            List<string> lines = new List<string>();
            foreach (ConfigRepairRecord record in records)
                lines.Add(Encode(record.ConfigPath) + "|" + Encode(record.BackupPath) + "|" + (record.WasReadOnly ? "1" : "0"));
            File.WriteAllLines(FilePath, lines.ToArray(), Encoding.UTF8);
        }

        public static List<ConfigRepairRecord> LoadRecords()
        {
            List<ConfigRepairRecord> records=new List<ConfigRepairRecord>();
            if(!File.Exists(FilePath)) return records;
            foreach(string line in File.ReadAllLines(FilePath))
            {
                string[] parts=line.Split('|');
                if(parts.Length!=3) continue;
                try
                {
                    records.Add(new ConfigRepairRecord {
                        ConfigPath=Decode(parts[0]),
                        BackupPath=Decode(parts[1]),
                        WasReadOnly=parts[2]=="1"
                    });
                }
                catch { }
            }
            return records;
        }

        public static void Restore()
        {
            if (!File.Exists(FilePath)) return;
            foreach (string line in File.ReadAllLines(FilePath))
            {
                string[] parts = line.Split('|');
                if (parts.Length != 3) continue;
                try
                {
                    string config = Decode(parts[0]);
                    string backup = Decode(parts[1]);
                    bool wasReadOnly = parts[2] == "1";
                    if (File.Exists(config)) File.SetAttributes(config, File.GetAttributes(config) & ~FileAttributes.ReadOnly);
                    if (File.Exists(backup)) File.Copy(backup, config, true);
                    if (wasReadOnly && File.Exists(config)) File.SetAttributes(config, File.GetAttributes(config) | FileAttributes.ReadOnly);
                }
                catch { }
            }
            File.Delete(FilePath);
        }

        public static void UnlockForRuntimeChange()
        {
            if(!File.Exists(FilePath)) return;
            foreach(string line in File.ReadAllLines(FilePath))
            {
                string[] parts=line.Split('|');
                if(parts.Length!=3) continue;
                try
                {
                    string config=Decode(parts[0]);
                    if(File.Exists(config))
                        File.SetAttributes(config,File.GetAttributes(config)&~FileAttributes.ReadOnly);
                }
                catch { }
            }
        }

        private static string Encode(string value)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value ?? string.Empty));
        }

        private static string Decode(string value)
        {
            return Encoding.UTF8.GetString(Convert.FromBase64String(value));
        }
    }
}
