using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace NexaArena
{
    internal sealed class AppxItem
    {
        public string name="",package="",location="";
        public bool framework=false,resource=false,nonRemovable=false,protectedPackage=false;
    }
    internal static class AppxManagement
    {
        private static readonly string[] Essential={"Microsoft.WindowsStore","Microsoft.StorePurchaseApp","Microsoft.DesktopAppInstaller","Microsoft.SecHealthUI","Microsoft.Windows.ShellExperienceHost","Microsoft.Windows.StartMenuExperienceHost","Microsoft.AAD.BrokerPlugin","Microsoft.AccountsControl","Microsoft.Windows.CloudExperienceHost","Microsoft.WindowsAppRuntime","Microsoft.VCLibs","Microsoft.NET.Native","Microsoft.Win32WebViewHost"};
        private static string Literal(string text){return "([Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('"+Convert.ToBase64String(Encoding.UTF8.GetBytes(text))+"')))";}
        private static string Run(string script,bool readOnly)
        {
            // APPX is a Windows PowerShell Desktop-only module. Do not bundle a second runtime.
            string shell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
            string code="$ErrorActionPreference='Stop';[Console]::OutputEncoding=[Text.Encoding]::UTF8;"+script;
            using(var process=new Process {StartInfo=new ProcessStartInfo(shell,"-NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(code))) {UseShellExecute=false,CreateNoWindow=true,WindowStyle=ProcessWindowStyle.Hidden,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8}})
            {
                process.Start();Task<string> output=process.StandardOutput.ReadToEndAsync(),errors=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(readOnly?30000:110000)){if(readOnly)process.Kill();throw new InvalidOperationException(readOnly?"APPX 列表读取超时。":"APPX 部署仍可能在后台运行，请等待后刷新，不要重复操作。");}
                string text=output.GetAwaiter().GetResult().Trim('\uFEFF','\r','\n',' '),error=errors.GetAwaiter().GetResult();
                if(process.ExitCode!=0)throw new InvalidOperationException("Windows APPX 操作未完成："+(error.Length>1200?error.Substring(0,1200):error));
                if(text.Length>8*1024*1024)throw new InvalidOperationException("APPX 返回结果过大。");return text;
            }
        }
        internal static bool IsProtected(AppxItem item)
        {return item.framework||item.resource||item.nonRemovable||Essential.Any(x=>item.name.StartsWith(x,StringComparison.OrdinalIgnoreCase))||item.location.StartsWith(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"SystemApps"),StringComparison.OrdinalIgnoreCase);}
        internal static AppxItem[] List()
        {
            string json=Run("$nexaPackages=@(Get-AppxPackage | ForEach-Object { [pscustomobject]@{name=$_.Name;package=$_.PackageFullName;location=$_.InstallLocation;framework=[bool]$_.IsFramework;resource=[bool]$_.IsResourcePackage;nonRemovable=[bool]$_.NonRemovable} });ConvertTo-Json -InputObject $nexaPackages -Compress -Depth 3",true);
            var items=new JavaScriptSerializer {MaxJsonLength=8*1024*1024}.Deserialize<AppxItem[]>(json);
            foreach(var item in items){item.name=item.name??"";item.location=item.location??"";item.protectedPackage=IsProtected(item);}return items.OrderBy(x=>x.name).ToArray();
        }
        internal static void VerifyRemoved(string package,AppxItem[] remaining)
        {
            if(remaining==null||remaining.Any(x=>string.Equals(x.package,package,StringComparison.OrdinalIgnoreCase)))
                throw new InvalidOperationException("尚未验证此应用已从当前用户移除，请刷新列表核对，不要重复卸载。");
        }
        internal static object Remove(string package,bool acknowledged)
        {
            if(!acknowledged)throw new InvalidOperationException("请确认卸载不是普通可逆优化，应用数据或安装文件可能被移除。");
            if(string.IsNullOrEmpty(package)||package.Length>512||!Regex.IsMatch(package,@"^[A-Za-z0-9_.-]+$"))throw new ArgumentException("应用包标识无效。");
            var item=List().FirstOrDefault(x=>x.package==package);if(item==null)throw new InvalidOperationException("应用已不存在，请刷新。");if(item.protectedPackage)throw new InvalidOperationException("系统核心、框架、资源或不可卸载的应用受保护。");
            Run("$nexaPackage="+Literal(package)+";Remove-AppxPackage -Package $nexaPackage -ErrorAction Stop;Write-Output 'OK'",false);
            VerifyRemoved(package,List());
            return new {ok=true,verified=true,message="已核对：此应用已从当前用户移除；恢复需可信安装包或 Microsoft Store 重装，不承诺恢复应用数据。"};
        }
        internal static object Install(string path)
        {
            string extension=Path.GetExtension(path).ToLowerInvariant();if(!File.Exists(path)||!new[]{".appx",".appxbundle",".msix",".msixbundle"}.Contains(extension))throw new ArgumentException("请选择存在的 APPX/MSIX 安装包。");
            Run("$nexaPackagePath="+Literal(Path.GetFullPath(path))+";Add-AppxPackage -Path $nexaPackagePath -ErrorAction Stop;Write-Output 'OK'",false);
            return new {ok=true,verified=false,message="Windows 安装命令已完成；工具尚未独立核对目标包的注册状态，请刷新列表确认。Windows 验证签名和依赖，工具未绕过签名。"};
        }
    }
}
