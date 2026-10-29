$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testExe = Join-Path $PSScriptRoot 'RestoreRegressionTests.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:anycpu /main:NexaArena.RestoreRegressionTests `
    /out:$testExe /reference:System.dll /reference:System.Core.dll `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    /reference:System.Management.dll /reference:System.Security.dll /reference:System.Xml.dll /reference:System.Web.Extensions.dll /reference:Microsoft.CSharp.dll `
    "$projectRoot\NexaArena.cs" "$projectRoot\ModernUI.cs" "$projectRoot\ValorantLocator.cs" `
    "$projectRoot\NativeDisplay.cs" "$projectRoot\NativeOptimizer.cs" "$projectRoot\NativeCrosshairs.cs" "$projectRoot\ConfigSelection.cs" "$projectRoot\HardwareColorFilters.cs" `
    "$projectRoot\GameOptimizer.cs" "$projectRoot\GamePrograms.cs" "$projectRoot\GameProcessProfiles.cs" "$projectRoot\SystemTuning.cs" "$projectRoot\MemoryCleaner.cs" `
    "$projectRoot\OptimizationManagers.cs" "$projectRoot\NvidiaGameSettings.cs" "$projectRoot\DeviceTuning.cs" "$projectRoot\AppxManagement.cs" "$projectRoot\ReadinessReport.cs" "$projectRoot\SystemRepairJobs.cs" `
    "$projectRoot\ArenaDesign.cs" `
    "$projectRoot\DesktopIconLayout.cs" `
    "$projectRoot\TrueStretchService.cs" "$projectRoot\RestoreService.cs" "$projectRoot\ArenaModules.cs" "$projectRoot\Cs2Crosshair.cs" `
    "$projectRoot\ToolboxCore.cs" "$projectRoot\DesktopIntegration.cs" "$projectRoot\GameAudio.cs" "$projectRoot\ToolboxPages.cs" `
    "$projectRoot\SensitivityProjection.cs" `
    "$PSScriptRoot\RestoreRegressionTests.cs"
if ($LASTEXITCODE -ne 0) { throw '恢复回归测试编译失败。' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw '恢复回归测试失败。' }
