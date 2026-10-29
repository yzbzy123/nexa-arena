using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Serialization;

namespace NexaArena
{
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
