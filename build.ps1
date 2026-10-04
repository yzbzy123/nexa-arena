param(
    [switch]$Clean,
    [switch]$NoPublish,
    [switch]$LegacyUi
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

if (-not $LegacyUi) {
    Push-Location (Join-Path $projectRoot 'web-ui')
    try {
        & npm run build
        if ($LASTEXITCODE -ne 0) { throw '网页界面构建失败。' }
    } finally { Pop-Location }
    & (Join-Path $projectRoot 'build-web.ps1') -Publish:(-not $NoPublish)
    if ($LASTEXITCODE -ne 0) { throw 'WebView2 宿主构建失败。' }
    return
}

$outputDirectory = Join-Path $projectRoot 'dist'
$releaseDirectory = Join-Path $projectRoot 'release'
$compiler = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'

if (-not (Test-Path -LiteralPath $compiler)) {
    throw "找不到 .NET Framework C# 编译器：$compiler"
}

if ($Clean -and (Test-Path -LiteralPath $outputDirectory)) {
    Remove-Item -LiteralPath $outputDirectory -Recurse -Force
}
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /debug:pdbonly `
    /out:"$outputDirectory\NexaArena.exe" `
    /win32manifest:"$projectRoot\app.manifest" `
    /win32icon:"$projectRoot\assets\NexaArena.ico" `
    /reference:System.dll `
    /reference:System.Core.dll `
    /reference:System.Drawing.dll `
    /reference:System.Windows.Forms.dll `
    /reference:System.Xml.dll `
    "/resource:$projectRoot\assets\ThirdPartyNotices.txt,NexaArena.ThirdPartyNotices" `
    "$projectRoot\NexaArena.cs" `
    "$projectRoot\ModernUI.cs" `
    "$projectRoot\ArenaDesign.cs" `
    "$projectRoot\ValorantLocator.cs" `
    "$projectRoot\TrueStretchService.cs" `
    "$projectRoot\RestoreService.cs" `
    "$projectRoot\ToolboxCore.cs" `
    "$projectRoot\SensitivityProjection.cs" `
    "$projectRoot\DesktopIntegration.cs" `
    "$projectRoot\DesktopIconLayout.cs" `
    "$projectRoot\GameAudio.cs" `
    "$projectRoot\GameOptimizer.cs" `
    "$projectRoot\SystemTuning.cs" `
    "$projectRoot\MemoryCleaner.cs" `
    "$projectRoot\ToolboxPages.cs" `
    "$projectRoot\Cs2Crosshair.cs" `
    "$projectRoot\ArenaModules.cs"

if ($LASTEXITCODE -ne 0) {
    throw "编译失败，退出代码：$LASTEXITCODE"
}

Write-Host "Legacy WinForms build (not published): $outputDirectory\NexaArena.exe"
