using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.Win32;

namespace NexaArena
{
    public sealed class GameProgramPreferences
    {
        public string LastGame="CS2",Cs2Path="",ValorantPath="";
    }
    internal sealed class GameProgramResolution
    {
        public string Path,Source,Message;
        public string[] Candidates=new string[0];
    }
    internal sealed class GamePrograms
    {
        private readonly string file;
        private readonly Func<string,string> running;
        private readonly Func<string,IEnumerable<string>> discover;
        private GameProgramPreferences preferences=new GameProgramPreferences();
        private readonly Dictionary<string,GameProgramResolution> cache=new Dictionary<string,GameProgramResolution>();
        private readonly Dictionary<string,DateTime> expiry=new Dictionary<string,DateTime>();
        internal string Warning {get;private set;}
        internal string LastGame {get{return preferences.LastGame;}}
        internal GamePrograms():this(System.IO.Path.Combine(SavedState.DirectoryPath,"game-programs.xml"),GameOptimizer.RunningPath,Discover){}
        internal GamePrograms(string file,Func<string,string> running,Func<string,IEnumerable<string>> discover)
        {
            this.file=file;this.running=running;this.discover=discover;
            try
            {
                if(File.Exists(file))
                {
                    if(new FileInfo(file).Length>65536)throw new InvalidOperationException("游戏路径配置过大。");
                    using(var reader=XmlReader.Create(file,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
                        preferences=(GameProgramPreferences)new XmlSerializer(typeof(GameProgramPreferences)).Deserialize(reader);
                    if(preferences==null)throw new InvalidOperationException("游戏路径配置为空。");
                    GameOptimizer.ExecutableName(preferences.LastGame);
                }
            }
            catch(Exception ex){preferences=new GameProgramPreferences();Warning="保存的游戏选择未读取，需重新核对："+ex.Message;}
        }
        internal static string ValidPath(string game,string path)
        {
            try{return !string.IsNullOrWhiteSpace(path)&&System.IO.Path.IsPathRooted(path)&&GameOptimizer.IsExecutable(game,path)&&File.Exists(path)?System.IO.Path.GetFullPath(path):null;}
            catch{return null;}
        }
        private void Persist(GameProgramPreferences next)
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var output=File.Create(temp))new XmlSerializer(typeof(GameProgramPreferences)).Serialize(output,next);
                if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);
                preferences=next;Warning=null;
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        internal void SelectGame(string game)
        {
            GameOptimizer.ExecutableName(game);
            if(game==preferences.LastGame)return;
            Persist(new GameProgramPreferences {LastGame=game,Cs2Path=preferences.Cs2Path,ValorantPath=preferences.ValorantPath});
        }
        internal void SelectPath(string game,string path)
        {
            GameOptimizer.ExecutableName(game);string valid=ValidPath(game,path);
            if(valid==null)throw new ArgumentException("请选择存在的游戏主程序 "+GameOptimizer.ExecutableName(game)+"，不要选择启动器。");
            Persist(new GameProgramPreferences {LastGame=game,Cs2Path=game=="CS2"?valid:preferences.Cs2Path,ValorantPath=game=="VALORANT"?valid:preferences.ValorantPath});
            cache.Remove(game);expiry.Remove(game);
        }
        internal GameProgramResolution Resolve(string game,bool force=false)
        {
            GameOptimizer.ExecutableName(game);
            string active=ValidPath(game,running(game));
            if(active!=null)return new GameProgramResolution {Path=active,Source="正在运行的游戏",Candidates=new[]{active}};
            string saved=ValidPath(game,game=="CS2"?preferences.Cs2Path:preferences.ValorantPath);
            if(saved!=null)return new GameProgramResolution {Path=saved,Source="已保存的主程序",Candidates=new[]{saved}};
            GameProgramResolution prior;DateTime until;
            if(!force&&cache.TryGetValue(game,out prior)&&expiry.TryGetValue(game,out until)&&DateTime.UtcNow<until&&
                (prior.Candidates.Length==0||prior.Candidates.All(x=>ValidPath(game,x)!=null)))return prior;
            string[] candidates=discover(game).Select(x=>ValidPath(game,x)).Where(x=>x!=null).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToArray();
            var result=new GameProgramResolution {Candidates=candidates,Path=candidates.Length==1?candidates[0]:null,
                Source=candidates.Length==1?"自动识别安装记录":null,
                Message=candidates.Length>1?"发现多个游戏安装，请选择要管理的主程序。":candidates.Length==0?"未找到有效安装路径，请选择主程序；不会全盘扫描。":null};
            cache[game]=result;expiry[game]=DateTime.UtcNow.AddSeconds(30);return result;
        }
        internal static bool LocalPath(string path)
        {
            try{return !string.IsNullOrWhiteSpace(path)&&path.Length>2&&char.IsLetter(path[0])&&path[1]==':'&&(path[2]=='\\'||path[2]=='/')&&
                new DriveInfo(System.IO.Path.GetPathRoot(path)).DriveType!=DriveType.Network;}
            catch{return false;}
        }
        internal static Dictionary<string,string> ReadVdfValues(string text)
        {
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            if(text==null||text.Length>2*1024*1024)return result;
            foreach(Match match in Regex.Matches(text,@"""((?:\\.|[^""\\])*)""\s*""((?:\\.|[^""\\])*)"""))
                result[match.Groups[1].Value]=match.Groups[2].Value.Replace(@"\\",@"\");
            return result;
        }
        internal static string[] SteamLibraries(string text)
        {
            if(text==null||text.Length>2*1024*1024)return new string[0];
            var result=new List<string>();
            foreach(Match match in Regex.Matches(text,@"""(path|[0-9]+)""\s*""((?:\\.|[^""\\])*)""",RegexOptions.IgnoreCase))
            {string path=match.Groups[2].Value.Replace(@"\\",@"\");if(LocalPath(path))result.Add(path);}
            return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }
        private static string ReadSmall(string path)
        {try{return File.Exists(path)&&new FileInfo(path).Length<=2*1024*1024?File.ReadAllText(path):null;}catch{return null;}}
        private static void AddRoot(List<string> paths,string game,string root)
        {
            if(!LocalPath(root))return;
            try
            {
                if(game=="CS2")
                {paths.Add(System.IO.Path.Combine(root,"game","bin","win64","cs2.exe"));paths.Add(System.IO.Path.Combine(root,"cs2.exe"));}
                else foreach(string relative in new[]{@"live\ShooterGame\Binaries\Win64",@"ShooterGame\Binaries\Win64",@"VALORANT\live\ShooterGame\Binaries\Win64"})
                    paths.Add(System.IO.Path.Combine(root,relative,"VALORANT-Win64-Shipping.exe"));
            }
            catch{}
        }
        private static IEnumerable<string> Discover(string game)
        {
            var paths=new List<string>();var steam=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var hiveName in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine})
            foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32})
            try
            {
                using(var hive=RegistryKey.OpenBaseKey(hiveName,view))
                {
                    if(hiveName==RegistryHive.CurrentUser)using(var graphics=hive.OpenSubKey(@"Software\Microsoft\DirectX\UserGpuPreferences"))
                        if(graphics!=null)foreach(string name in graphics.GetValueNames().Take(4096))
                            if(LocalPath(name)&&GameOptimizer.IsExecutable(game,name))paths.Add(name);
                    if(game=="CS2")using(var key=hive.OpenSubKey(@"Software\Valve\Steam"))
                        if(key!=null)foreach(string name in new[]{"SteamPath","InstallPath"}){string root=Convert.ToString(key.GetValue(name));if(LocalPath(root))steam.Add(root);}
                    using(var uninstall=hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                        if(uninstall!=null)foreach(string name in uninstall.GetSubKeyNames().Take(4096))
                        {
                            if(game=="CS2"&&!name.Equals("Steam App 730",StringComparison.OrdinalIgnoreCase))continue;
                            using(var app=uninstall.OpenSubKey(name))
                            {
                                if(app==null)continue;string title=Convert.ToString(app.GetValue("DisplayName"));
                                if(game=="VALORANT"&&title.IndexOf("VALORANT",StringComparison.OrdinalIgnoreCase)<0&&title.IndexOf("无畏契约",StringComparison.OrdinalIgnoreCase)<0)continue;
                                AddRoot(paths,game,Convert.ToString(app.GetValue("InstallLocation")));
                            }
                        }
                }
            }
            catch{}
            if(game=="CS2")
            {
                steam.Add(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Steam"));
                var libraries=new HashSet<string>(steam,StringComparer.OrdinalIgnoreCase);
                foreach(string root in steam)foreach(string sub in new[]{@"steamapps\libraryfolders.vdf",@"config\libraryfolders.vdf"})
                    foreach(string library in SteamLibraries(ReadSmall(System.IO.Path.Combine(root,sub))))libraries.Add(library);
                foreach(string library in libraries)
                {
                    var manifest=ReadVdfValues(ReadSmall(System.IO.Path.Combine(library,"steamapps","appmanifest_730.acf")));string directory;
                    if(manifest.TryGetValue("installdir",out directory)&&!System.IO.Path.IsPathRooted(directory)&&directory==System.IO.Path.GetFileName(directory)&&directory!="."&&directory!="..")
                        AddRoot(paths,game,System.IO.Path.Combine(library,"steamapps","common",directory));
                    AddRoot(paths,game,System.IO.Path.Combine(library,"steamapps","common","Counter-Strike Global Offensive"));
                }
            }
            else
            {
                string metadata=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Riot Games","Metadata");
                try
                {
                    if(Directory.Exists(metadata))foreach(string folder in Directory.GetDirectories(metadata,"valorant*",SearchOption.TopDirectoryOnly).Take(32))
                        foreach(string file in Directory.GetFiles(folder,"*.product_settings.yaml",SearchOption.TopDirectoryOnly).Take(8))
                        {
                            string text=ReadSmall(file);if(text==null)continue;
                            foreach(Match match in Regex.Matches(text,@"(?m)^\s*(product_install_full_path|product_install_root|install_dir)\s*:\s*([^\r\n]+)$"))
                                AddRoot(paths,game,match.Groups[2].Value.Trim().Trim('"','\''));
                        }
                }
                catch{}
                foreach(var drive in DriveInfo.GetDrives().Where(x=>x.DriveType==DriveType.Fixed))
                    foreach(string parent in new[]{"Riot Games","Tencent Games"})AddRoot(paths,game,System.IO.Path.Combine(drive.RootDirectory.FullName,parent,"VALORANT"));
            }
            return paths;
        }
    }
}
