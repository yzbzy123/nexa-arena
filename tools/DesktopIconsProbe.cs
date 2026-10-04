using System;
using System.Linq;
using System.Threading;
using System.Reflection;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.IO;

internal static class DesktopIconsProbe
{
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Assembly app=Assembly.LoadFrom(args[0]);
            Type type=app.GetType("NexaArena.ShellDesktopIconView",true);
            object view=Activator.CreateInstance(type,true);
            try {Console.WriteLine("STA read count="+Read(type,view).Count);}finally{((IDisposable)view).Dispose();}
            Exception failure=null;
            var thread=new Thread(delegate()
            {
                object worker=null;
                try
                {
                    worker=Activator.CreateInstance(type,true);
                    var positions=Read(type,worker);
                    Console.WriteLine("MTA read count="+positions.Count+" autoArrange="+type.GetProperty("AutoArrange").GetValue(worker,null));
                    Type snapshotType=app.GetType("NexaArena.DesktopIconSnapshot",true);
                    object snapshot=Activator.CreateInstance(snapshotType);
                    snapshotType.GetField("Icons").SetValue(snapshot,type.GetMethod("Read").Invoke(worker,null));
                    var serializer=new XmlSerializer(snapshotType);
                    using(var stream=new MemoryStream())
                    {
                        serializer.Serialize(stream,snapshot);stream.Position=0;
                        object loaded=serializer.Deserialize(stream);
                        if(((System.Collections.ICollection)snapshotType.GetField("Icons").GetValue(loaded)).Count!=positions.Count)throw new Exception("Snapshot serialization failed");
                    }
                    if(args.Length>1&&args[1]=="--same-position")
                    {
                        if((bool)type.GetProperty("AutoArrange").GetValue(worker,null))throw new Exception("Auto arrange enabled; no position write tested");
                        object nativeItems=type.GetMethod("Read").Invoke(worker,null);
                        if(!Equal(positions,Read(type,worker)))throw new Exception("Desktop changed during probe; no position write tested");
                        type.GetMethod("Position").Invoke(worker,new object[]{nativeItems});
                        Thread.Sleep(1200);
                        if(!Equal(positions,Read(type,worker)))throw new Exception("Same-position roundtrip changed desktop layout");
                        Console.WriteLine("Same-position Shell API roundtrip verified; all icon positions unchanged.");
                    }
                }
                catch(Exception ex){failure=ex;}
                finally{if(worker!=null)((IDisposable)worker).Dispose();}
            });
            thread.SetApartmentState(ApartmentState.MTA);thread.Start();thread.Join();
            if(failure!=null)throw failure;
            Console.WriteLine("PASS: STA/MTA desktop access and XML roundtrip.");return 0;
        }
        catch(Exception ex){Console.WriteLine(ex);return 1;}
    }
    private static Dictionary<string,string> Read(Type type,object view)
    {
        var values=(System.Collections.IEnumerable)type.GetMethod("Read").Invoke(view,null);
        var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(object value in values)
        {
            Type item=value.GetType();
            result.Add((string)item.GetField("Identity").GetValue(value),item.GetField("X").GetValue(value)+","+item.GetField("Y").GetValue(value));
        }
        return result;
    }
    private static bool Equal(Dictionary<string,string> a,Dictionary<string,string> b)
    {return a.Count==b.Count&&a.All(p=>b.ContainsKey(p.Key)&&b[p.Key]==p.Value);}
}
