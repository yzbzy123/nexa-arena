param([switch]$Preview,[switch]$Publish,[string]$OutputDirectory)
$ErrorActionPreference='Stop'
$projectRoot=Split-Path -Parent $MyInvocation.MyCommand.Path
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sdk=Join-Path $projectRoot 'tools\webview2-sdk\net462'
$web=Join-Path $projectRoot 'web-ui'
$output=if([string]::IsNullOrWhiteSpace($OutputDirectory)){Join-Path $projectRoot 'dist-web'}else{
    if([System.IO.Path]::IsPathRooted($OutputDirectory)){[System.IO.Path]::GetFullPath($OutputDirectory)}else{[System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))}
}
$resolvedProject=[System.IO.Path]::GetFullPath($projectRoot).TrimEnd('\')
if(-not $output.StartsWith($resolvedProject+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Output directory must be a child of the project workspace.'}
$exe=Join-Path $output $(if($Preview){'NexaArenaWebPreview.exe'}else{'NexaArena.exe'})
if($Preview -and $Publish){throw 'Preview builds cannot be published.'}
if(-not(Test-Path -LiteralPath (Join-Path $sdk 'Microsoft.Web.WebView2.WinForms.dll'))){throw 'WebView2 SDK is missing.'}
if(-not(Test-Path -LiteralPath (Join-Path $web 'dist\index.html'))){throw 'Build web-ui first with npm run build.'}
New-Item -ItemType Directory -Path $output -Force|Out-Null
$source=@('NexaArena.cs','ModernUI.cs','ArenaDesign.cs','ValorantLocator.cs','TrueStretchService.cs',
    'RestoreService.cs','ArenaModules.cs','Cs2Crosshair.cs','ToolboxCore.cs','SensitivityProjection.cs','DesktopIntegration.cs',
    'DesktopIconLayout.cs','GameAudio.cs','GameOptimizer.cs','GameProcessProfiles.cs','SystemTuning.cs','MemoryCleaner.cs','OptimizationManagers.cs','NvidiaGameSettings.cs','DeviceTuning.cs','AppxManagement.cs','ReadinessReport.cs','SystemRepairJobs.cs','ToolboxPages.cs','WebMainForm.cs') |
    ForEach-Object {Join-Path $projectRoot $_}
$previewSource=Join-Path $projectRoot 'tools\WebPreview.cs'
$manifest=Join-Path $projectRoot $(if($Preview){'tools\ui-probe.manifest'}else{'app.manifest'})
$entry=if($Preview){'/main:NexaArena.WebPreview'}else{'/main:NexaArena.Program'}
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /define:WEB_UI $entry "/out:$exe" "/win32manifest:$manifest" `
    "/win32icon:$projectRoot\assets\NexaArena.ico" `
    /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll /reference:System.Xml.dll /reference:System.Web.Extensions.dll /reference:System.Management.dll /reference:System.Security.dll /reference:Microsoft.CSharp.dll `
    "/reference:$sdk\Microsoft.Web.WebView2.Core.dll" `
    "/reference:$sdk\Microsoft.Web.WebView2.WinForms.dll" `
    "/resource:$projectRoot\assets\ThirdPartyNotices.txt,NexaArena.ThirdPartyNotices" `
    @source $(if($Preview){$previewSource})
if($LASTEXITCODE -ne 0){throw 'WebView2 host compilation failed.'}
foreach($file in 'Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll'){
    Copy-Item -LiteralPath (Join-Path $sdk $file) -Destination (Join-Path $output $file) -Force
}
Copy-Item -LiteralPath (Join-Path $projectRoot 'tools\webview2-sdk\LICENSE.txt') -Destination (Join-Path $output 'WebView2-SDK-LICENSE.txt') -Force
$webLicenses=[System.Text.StringBuilder]::new()
foreach($package in 'react','react-dom','radix-ui','@radix-ui/colors','@fontsource-variable/manrope','lucide-react','shadcn','sonner','class-variance-authority','cn','tailwindcss','tw-animate-css'){
    $packagePath=Join-Path (Join-Path $web 'node_modules') $package
    $license=Get-ChildItem -LiteralPath $packagePath -File | Where-Object {$_.Name -match '^licen[cs]e(?:\.md|\.txt)?$'} | Select-Object -First 1
    if($license){[void]$webLicenses.AppendLine($package);[void]$webLicenses.AppendLine((Get-Content -LiteralPath $license.FullName -Raw));[void]$webLicenses.AppendLine()}
}
[System.IO.File]::WriteAllText((Join-Path $output 'Web-ThirdParty-LICENSES.txt'),$webLicenses.ToString(),[System.Text.UTF8Encoding]::new($false))
$webTarget=Join-Path $output 'web'
if(Test-Path -LiteralPath $webTarget){
    $resolvedOutput=(Resolve-Path -LiteralPath $output).Path.TrimEnd('\')
    $resolvedWeb=(Resolve-Path -LiteralPath $webTarget).Path
    if(-not $resolvedWeb.StartsWith($resolvedOutput+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe web output path.'}
    Remove-Item -LiteralPath $resolvedWeb -Recurse -Force
}
Copy-Item -LiteralPath (Join-Path $web 'dist') -Destination $webTarget -Recurse
if($Publish){
    $release=Join-Path $projectRoot 'release'
    if(-not(Test-Path -LiteralPath $release)){New-Item -ItemType Directory -Path $release|Out-Null}
    $currentWeb=Join-Path $release 'web'
    if(Test-Path -LiteralPath $currentWeb){
        $resolvedRelease=(Resolve-Path -LiteralPath $release).Path.TrimEnd('\')
        $resolvedTarget=(Resolve-Path -LiteralPath $currentWeb).Path
        if(-not $resolvedTarget.StartsWith($resolvedRelease+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe published web path.'}
        Remove-Item -LiteralPath $resolvedTarget -Recurse -Force
    }
    Copy-Item -LiteralPath $webTarget -Destination $currentWeb -Recurse
    foreach($file in 'Microsoft.Web.WebView2.Core.dll','Microsoft.Web.WebView2.WinForms.dll','WebView2Loader.dll'){
        Copy-Item -LiteralPath (Join-Path $output $file) -Destination (Join-Path $release $file) -Force
    }
    Copy-Item -LiteralPath (Join-Path $output 'WebView2-SDK-LICENSE.txt') -Destination (Join-Path $release 'WebView2-SDK-LICENSE.txt') -Force
    Copy-Item -LiteralPath (Join-Path $output 'Web-ThirdParty-LICENSES.txt') -Destination (Join-Path $release 'Web-ThirdParty-LICENSES.txt') -Force
    Copy-Item -LiteralPath $exe -Destination (Join-Path $release 'NexaArena.exe') -Force
    Write-Host "Published: $release\NexaArena.exe"
}
Write-Host "Built: $exe"
