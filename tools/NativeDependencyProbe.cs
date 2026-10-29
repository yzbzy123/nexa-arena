using System;
using System.Linq;
using System.Reflection;
class NativeDependencyProbe
{
    static int Main(string[] args)
    {
        var assembly=Assembly.ReflectionOnlyLoadFrom(args[0]);
        var references=assembly.GetReferencedAssemblies();
        if(references.Any(x=>x.Name.IndexOf("WebView",StringComparison.OrdinalIgnoreCase)>=0)){Console.Error.WriteLine("Unexpected WebView dependency");return 1;}
        Console.WriteLine("PASS: native executable has no WebView2 assembly dependency.");
        Console.WriteLine("Entry: "+assembly.EntryPoint.DeclaringType.FullName+"."+assembly.EntryPoint.Name);
        Console.WriteLine("Version: "+assembly.GetName().Version);return 0;
    }
}
