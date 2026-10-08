param([switch]$NativeRead,[switch]$BuildPreview,[switch]$CaptureSmoke,[string]$NvidiaCs2Path,[string]$NvidiaValorantPath)
$ErrorActionPreference='Stop'
if($CaptureSmoke){throw '帧率记录功能已移除；CaptureSmoke 不再运行。'}
$projectRoot=Split-Path -Parent $PSScriptRoot
$compiler='C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$sources=@('NexaArena.cs','ModernUI.cs','ArenaDesign.cs','ValorantLocator.cs','TrueStretchService.cs','RestoreService.cs','ArenaModules.cs','Cs2Crosshair.cs','ToolboxCore.cs','SensitivityProjection.cs','DesktopIntegration.cs','DesktopIconLayout.cs','GameAudio.cs','GameOptimizer.cs','SystemTuning.cs','MemoryCleaner.cs','ToolboxPages.cs') | ForEach-Object {Join-Path $projectRoot $_}
$sources+=Join-Path $projectRoot 'OptimizationManagers.cs'
$sources+=Join-Path $projectRoot 'GameProcessProfiles.cs'
$sources+=Join-Path $projectRoot 'GamePrograms.cs'
$sources+=Join-Path $projectRoot 'NvidiaGameSettings.cs'
$sources+=Join-Path $projectRoot 'DeviceTuning.cs'
$sources+=Join-Path $projectRoot 'AppxManagement.cs'
$sources+=Join-Path $projectRoot 'ReadinessReport.cs'
$sources+=Join-Path $projectRoot 'SystemRepairJobs.cs'
$references=@('/reference:System.dll','/reference:System.Core.dll','/reference:System.Drawing.dll','/reference:System.Windows.Forms.dll','/reference:System.Xml.dll','/reference:System.Management.dll','/reference:System.Security.dll','/reference:System.Web.Extensions.dll','/reference:Microsoft.CSharp.dll')
$resources=@("/resource:$projectRoot\assets\ThirdPartyNotices.txt,NexaArena.ThirdPartyNotices")
$testExe=Join-Path $PSScriptRoot 'ToolboxTests.exe'
& $compiler /nologo /target:exe /platform:anycpu /main:NexaArena.ToolboxTests "/out:$testExe" @references @resources @sources "$PSScriptRoot\ToolboxTests.cs"
if($LASTEXITCODE -ne 0){throw '工具箱测试编译失败。'}
$testArguments=@()
if($NativeRead){$testArguments+='--native-read'}
if(-not [string]::IsNullOrWhiteSpace($NvidiaCs2Path)){$testArguments+='--nvidia-cs2-read';$testArguments+=$NvidiaCs2Path}
if(-not [string]::IsNullOrWhiteSpace($NvidiaValorantPath)){$testArguments+='--nvidia-valorant-read';$testArguments+=$NvidiaValorantPath}
& $testExe @testArguments
if($LASTEXITCODE -ne 0){throw '工具箱测试失败。'}
if($BuildPreview){
    & $compiler /nologo /target:winexe /platform:anycpu /main:NexaArena.ToolboxPreview "/out:$PSScriptRoot\NexaArenaPreview.exe" "/win32manifest:$PSScriptRoot\ui-probe.manifest" @references @resources @sources "$PSScriptRoot\ToolboxPreview.cs"
    if($LASTEXITCODE -ne 0){throw '预览版编译失败。'}
}
