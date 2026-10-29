param([switch]$Clean,[switch]$NoPublish,[switch]$LegacyUi,[switch]$Preview)
$ErrorActionPreference='Stop'
$projectRoot=$PSScriptRoot
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output=Join-Path $projectRoot 'dist'
if(-not(Test-Path -LiteralPath $output)){New-Item -ItemType Directory -Path $output|Out-Null}
$name=if($Preview){'NexaArenaPreview.exe'}else{'NexaArena.exe'}
$entry=if($Preview){'/main:NexaArena.ToolboxPreview'}else{'/main:NexaArena.Program'}
$manifest=Join-Path $projectRoot $(if($Preview){'tools\ui-probe.manifest'}else{'app.manifest'})
$sources=@('NexaArena.cs','ModernUI.cs','NativeDisplay.cs','NativeOptimizer.cs','NativeCrosshairs.cs','ConfigSelection.cs','ArenaDesign.cs','ValorantLocator.cs','TrueStretchService.cs','RestoreService.cs','ToolboxCore.cs','SensitivityProjection.cs','DesktopIntegration.cs','DesktopIconLayout.cs','GameAudio.cs','GameOptimizer.cs','GamePrograms.cs','GameProcessProfiles.cs','SystemTuning.cs','MemoryCleaner.cs','OptimizationManagers.cs','NvidiaGameSettings.cs','DeviceTuning.cs','AppxManagement.cs','ReadinessReport.cs','SystemRepairJobs.cs','ToolboxPages.cs','Cs2Crosshair.cs','ArenaModules.cs')|ForEach-Object {Join-Path $projectRoot $_}
$sources+=Join-Path $projectRoot 'HardwareColorFilters.cs'
if($Preview){$sources+=Join-Path $projectRoot 'tools\ToolboxPreview.cs'}
$references=@('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Xml.dll','/reference:System.Management.dll','/reference:System.Security.dll','/reference:System.Web.Extensions.dll','/reference:Microsoft.CSharp.dll')
& $compiler /nologo /target:winexe /platform:x64 /optimize+ $entry "/out:$output\$name" "/win32manifest:$manifest" "/win32icon:$projectRoot\assets\NexaArena.ico" "/resource:$projectRoot\assets\ThirdPartyNotices.txt,NexaArena.ThirdPartyNotices" "/resource:$projectRoot\assets\aspect-model.png,NexaArena.AspectModel" @references @sources
if($LASTEXITCODE -ne 0){throw '原生桌面程序编译失败。'}
if(-not $NoPublish -and -not $Preview){
    if(Get-Process -Name NexaArena -ErrorAction SilentlyContinue){throw 'Nexa Arena 仍在运行，请从托盘正常退出后再替换。'}
    $release=Join-Path $projectRoot 'release'
    if(-not(Test-Path -LiteralPath $release)){New-Item -ItemType Directory -Path $release|Out-Null}
    Copy-Item -LiteralPath (Join-Path $output $name) -Destination (Join-Path $release 'NexaArena.exe') -Force
    Write-Host "Published: $release\NexaArena.exe"
}
Write-Host "Built native Windows app: $output\$name"
