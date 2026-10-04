$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$testExe = Join-Path $PSScriptRoot 'RestoreRegressionTests.exe'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:exe /platform:anycpu /main:NexaArena.RestoreRegressionTests `
    /out:$testExe /reference:System.dll /reference:System.Core.dll `
    /reference:System.Drawing.dll /reference:System.Windows.Forms.dll `
    "$projectRoot\NexaArena.cs" "$projectRoot\ModernUI.cs" "$projectRoot\ValorantLocator.cs" `
    "$projectRoot\ArenaDesign.cs" `
    "$projectRoot\DesktopIconLayout.cs" `
    "$projectRoot\TrueStretchService.cs" "$projectRoot\RestoreService.cs" "$projectRoot\ArenaModules.cs" "$projectRoot\Cs2Crosshair.cs" `
    "$projectRoot\ToolboxCore.cs" "$projectRoot\DesktopIntegration.cs" "$projectRoot\GameAudio.cs" "$projectRoot\ToolboxPages.cs" `
    "$projectRoot\SensitivityProjection.cs" `
    "$PSScriptRoot\RestoreRegressionTests.cs"
if ($LASTEXITCODE -ne 0) { throw '恢复回归测试编译失败。' }
& $testExe
if ($LASTEXITCODE -ne 0) { throw '恢复回归测试失败。' }
