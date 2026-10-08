using System;
using System.IO;
using System.Linq;
using System.Reflection;

// Optional live-driver round trip. Writes only when explicitly invoked with
// --write-roundtrip, requires the user-selected CS2 pair to be exactly 30 FPS.
class NvidiaBackgroundTests
{
    static Type nvidiaType,managerType;
    const BindingFlags Members=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    static object Read(string game,string path)
    {
        using(var driver=(IDisposable)Activator.CreateInstance(nvidiaType,Members,null,new object[]{game,path},null))
            return nvidiaType.GetMethod("Read",Members).Invoke(driver,new object[]{"10835005"});
    }
    static string Raw(object item){return (string)item.GetType().GetField("Raw",Members).GetValue(item);}
    static string Snapshot(string game,string path,bool omitBackground)
    {
        using(var driver=(IDisposable)Activator.CreateInstance(nvidiaType,Members,null,new object[]{game,path},null))
        {
            var items=(System.Collections.IEnumerable)nvidiaType.GetMethod("Scan",Members).Invoke(driver,null);
            return string.Join(";",items.Cast<object>().Where(x=>!omitBackground||(string)x.GetType().GetField("id",Members).GetValue(x)!="10835005").Select(x=>x.GetType().GetField("id",Members).GetValue(x)+"="+Raw(x)).ToArray());
        }
    }
    static object Manager(Assembly assembly,string path,string file)
    {
        object backend=Activator.CreateInstance(assembly.GetType("NexaArena.WindowsManagerBackend",true),true);
        backend.GetType().GetMethod("SelectGamePath",Members).Invoke(backend,new object[]{"CS2",path});
        return Activator.CreateInstance(managerType,Members,null,new object[]{backend,file},null);
    }
    static bool HasBackup(object manager){return (bool)managerType.GetMethod("HasBackup",Members).Invoke(manager,new object[]{"nvidia-CS2","10835005"});}
    static void Change(object manager,string value,bool restore){managerType.GetMethod("Change",Members).Invoke(manager,new object[]{"nvidia-CS2","10835005",value,restore});}
    static void Check(bool condition,string message){if(!condition)throw new InvalidOperationException(message);}
    static int Main(string[] args)
    {
        if(args.Length<4){Console.Error.WriteLine("Usage: host.exe CS2.exe VALORANT.exe scratch-directory [--write-roundtrip]");return 2;}
        var assembly=Assembly.LoadFrom(args[0]);nvidiaType=assembly.GetType("NexaArena.NvidiaGameSettings",true);managerType=assembly.GetType("NexaArena.OptimizationManagers",true);
        string original=Raw(Read("CS2",args[1]));Console.WriteLine("CS2 original pair="+original);
        Console.WriteLine("VALORANT pair="+Raw(Read("VALORANT",args[2])));
        if(!args.Contains("--write-roundtrip")){Console.WriteLine("PASS readonly; no driver writes requested.");return 0;}
        Check(original=="explicit:30|explicit:30","Live write test requires the confirmed 30 FPS baseline; nothing changed.");
        string global=Snapshot("GLOBAL","",false),valorant=Snapshot("VALORANT",args[2],false),cs2=Snapshot("CS2",args[1],true);
        Directory.CreateDirectory(args[3]);string file=Path.Combine(args[3],"background-roundtrip.bin");
        Check(!File.Exists(file),"Test ledger already exists; preserve and inspect it before retrying.");
        object manager=Manager(assembly,args[1],file);
        try
        {
            Change(manager,"explicit:60|explicit:60",false);
            Check(Raw(Read("CS2",args[1]))=="explicit:60|explicit:60","60 FPS did not persist in both driver parameters.");
            Console.WriteLine("PASS persisted actual limit and control-panel value: 60 / 60 FPS");
            manager=Manager(assembly,args[1],file);Check(HasBackup(manager),"Original pair backup missing after reopen.");
            Change(manager,"",true);
            Check(Raw(Read("CS2",args[1]))==original,"Original 30 FPS pair was not restored.");
            Check(!HasBackup(manager),"Successful restore left a live backup.");
            Check(Snapshot("GLOBAL","",false)==global,"Global settings changed.");
            Check(Snapshot("VALORANT",args[2],false)==valorant,"VALORANT settings changed.");
            Check(Snapshot("CS2",args[1],true)==cs2,"Unrelated CS2 settings changed.");
            Console.WriteLine("PASS restored actual limit and control-panel value: 30 / 30 FPS");
            Console.WriteLine("PASS global, VALORANT and unrelated CS2 settings unchanged");return 0;
        }
        catch(Exception ex){Console.Error.WriteLine(ex.InnerException==null?ex.Message:ex.InnerException.Message);return 1;}
        finally
        {
            if(HasBackup(manager))
            {
                try{Change(manager,"",true);Console.WriteLine("Emergency restore pair="+Raw(Read("CS2",args[1])));}
                catch(Exception ex){Console.Error.WriteLine("RESTORE FAILED; retain ledger "+file+": "+ex.Message);}
            }
        }
    }
}
