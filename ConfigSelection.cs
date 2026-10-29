using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace NexaArena
{
    public sealed class ConfigAliasEntry { public string Path="",Name=""; }
    public sealed class ConfigAliasOptions { public List<ConfigAliasEntry> Entries=new List<ConfigAliasEntry>(); }
    internal sealed class ConfigAliases
    {
        private readonly string file;
        private readonly Dictionary<string,string> entries=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        internal string Warning {get;private set;}
        internal ConfigAliases():this(System.IO.Path.Combine(SavedState.DirectoryPath,"config-aliases.xml")){}
        internal ConfigAliases(string file)
        {
            this.file=file;
            try
            {
                if(!File.Exists(file))return;
                if(new FileInfo(file).Length>262144)throw new InvalidOperationException("账号备注记录过大。");
                ConfigAliasOptions data;
                using(var reader=XmlReader.Create(file,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=262144}))
                    data=(ConfigAliasOptions)new XmlSerializer(typeof(ConfigAliasOptions)).Deserialize(reader);
                if(data.Entries==null||data.Entries.Count>256)throw new InvalidOperationException("账号备注记录数量异常。");
                foreach(var item in data.Entries)
                {
                    if(item==null)throw new InvalidOperationException("账号备注记录不完整。");
                    string path=Normalize(item.Path),name=ValidateName(item.Name);
                    if(name.Length==0||entries.ContainsKey(path))throw new InvalidOperationException("账号备注记录为空或重复。");
                    entries.Add(path,name);
                }
            }
            catch(Exception ex){entries.Clear();Warning="账号备注未读取："+ex.Message;}
        }
        private static string Normalize(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||!System.IO.Path.IsPathRooted(path)||!string.Equals(System.IO.Path.GetFileName(path),"GameUserSettings.ini",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("账号备注必须关联实际配置路径。");
            string full=System.IO.Path.GetFullPath(path);if(full.Length>2048)throw new ArgumentException("配置路径过长。");return full;
        }
        private static string ValidateName(string name)
        {name=(name??"").Trim();if(name.Length>40||name.Any(char.IsControl))throw new ArgumentException("账号备注最多 40 个字符，不能包含换行或控制字符。");return name;}
        internal string Get(string path){string name;return entries.TryGetValue(Normalize(path),out name)?name:"";}
        internal void Set(string path,string name)
        {
            path=Normalize(path);name=ValidateName(name);var latest=new ConfigAliases(file);
            if(latest.Warning!=null)throw new InvalidOperationException(latest.Warning+" 原记录保留，未覆盖。");
            var next=new Dictionary<string,string>(latest.entries,StringComparer.OrdinalIgnoreCase);
            if(name.Length==0)next.Remove(path);else next[path]=name;
            if(next.Count>256)throw new InvalidOperationException("最多保存 256 个账号备注。");
            var options=new ConfigAliasOptions {Entries=next.OrderBy(x=>x.Key,StringComparer.OrdinalIgnoreCase).Select(x=>new ConfigAliasEntry {Path=x.Key,Name=x.Value}).ToList()};
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));string temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            try
            {
                using(var output=File.Create(temp))new XmlSerializer(typeof(ConfigAliasOptions)).Serialize(output,options);
                var staged=new ConfigAliases(temp);if(staged.Warning!=null||staged.entries.Count!=next.Count||next.Any(x=>staged.Get(x.Key)!=x.Value))throw new IOException("账号备注保存前核对失败。");
                if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);
                var reopened=new ConfigAliases(file);if(reopened.Warning!=null||reopened.entries.Count!=next.Count||next.Any(x=>reopened.Get(x.Key)!=x.Value))throw new IOException("账号备注保存后核对失败。");
                entries.Clear();foreach(var pair in next)entries.Add(pair.Key,pair.Value);Warning=null;
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
    }
    public sealed class ConfigSelectionOptions { public string Path=""; }
    internal sealed class ConfigSelection
    {
        private readonly string file;
        internal string SelectedPath {get;private set;}
        internal string Warning {get;private set;}
        internal ConfigSelection():this(System.IO.Path.Combine(SavedState.DirectoryPath,"valorant-config.xml")){}
        internal ConfigSelection(string file)
        {
            this.file=file;
            try
            {
                if(!File.Exists(file))return;
                if(new FileInfo(file).Length>65536)throw new InvalidOperationException("配置选择记录过大。");
                using(var reader=XmlReader.Create(file,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=65536}))
                    SelectedPath=((ConfigSelectionOptions)new XmlSerializer(typeof(ConfigSelectionOptions)).Deserialize(reader)).Path;
                if(!File.Exists(SelectedPath)){SelectedPath=null;Warning="已保存的配置路径已不可用，请重新扫描或选择。";}
            }
            catch(Exception ex){SelectedPath=null;Warning="配置选择记录未读取："+ex.Message;}
        }
        internal static string Validate(string path)
        {
            if(string.IsNullOrWhiteSpace(path)||!System.IO.Path.IsPathRooted(path)||!string.Equals(System.IO.Path.GetFileName(path),"GameUserSettings.ini",StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请选择 VALORANT 的 GameUserSettings.ini，不能选择游戏 EXE 或其他文件。");
            string full=System.IO.Path.GetFullPath(path);var info=new FileInfo(full);
            if(!info.Exists)throw new FileNotFoundException("配置文件不存在，请先进入游戏生成配置，再退出游戏选择。",full);
            if(info.Length>4*1024*1024)throw new InvalidOperationException("配置文件大小异常，未修改。");
            using(var stream=new FileStream(full,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))
            using(var reader=new StreamReader(stream,true))
                if(reader.ReadToEnd().IndexOf(ValorantConfig.ShooterSettingsSection,StringComparison.OrdinalIgnoreCase)<0)
                    throw new InvalidOperationException("此文件未包含 VALORANT 游戏设置段，不能确认属于本游戏，未修改。");
            return full;
        }
        internal void Select(string path)
        {
            string full=Validate(path),temp=file+"."+Guid.NewGuid().ToString("N")+".tmp";
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(file));
            try
            {
                using(var output=File.Create(temp))new XmlSerializer(typeof(ConfigSelectionOptions)).Serialize(output,new ConfigSelectionOptions {Path=full});
                if(File.Exists(file))File.Replace(temp,file,file+".bak");else File.Move(temp,file);
                var reopened=new ConfigSelection(file);
                if(reopened.Warning!=null||!string.Equals(reopened.SelectedPath,full,StringComparison.OrdinalIgnoreCase))throw new IOException("所选配置路径保存后核对失败。");
                SelectedPath=full;Warning=null;
            }
            finally{if(File.Exists(temp))File.Delete(temp);}
        }
        internal static IEnumerable<string> Enumerate(string root,int maximumDirectories=1024)
        {
            var queue=new Queue<Tuple<string,int>>();queue.Enqueue(Tuple.Create(root,0));int visited=0;
            while(queue.Count>0&&visited++<maximumDirectories)
            {
                var next=queue.Dequeue();string candidate=System.IO.Path.Combine(next.Item1,"GameUserSettings.ini");
                if(File.Exists(candidate))yield return candidate;
                if(next.Item2>=5)continue;
                string[] directories;
                try{directories=Directory.GetDirectories(next.Item1);}catch{continue;}
                foreach(string directory in directories)
                    try{if((File.GetAttributes(directory)&FileAttributes.ReparsePoint)==0)queue.Enqueue(Tuple.Create(directory,next.Item2+1));}catch{}
            }
        }
    }
}
