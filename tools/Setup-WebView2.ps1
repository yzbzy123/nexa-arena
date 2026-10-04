param([string]$Version='1.0.4258.31')
$ErrorActionPreference='Stop'
if($Version -notmatch '^\d+\.\d+\.\d+\.\d+$'){throw 'Invalid WebView2 SDK version.'}
$projectRoot=Split-Path -Parent $PSScriptRoot
$sdkRoot=Join-Path $PSScriptRoot 'webview2-sdk'
$destination=Join-Path $sdkRoot 'net462'
$files=@{
    'lib/net462/Microsoft.Web.WebView2.Core.dll'='Microsoft.Web.WebView2.Core.dll'
    'lib/net462/Microsoft.Web.WebView2.WinForms.dll'='Microsoft.Web.WebView2.WinForms.dll'
    'runtimes/win-x64/native/WebView2Loader.dll'='WebView2Loader.dll'
}
if(($files.Values | Where-Object {-not(Test-Path -LiteralPath (Join-Path $destination $_))}).Count -eq 0){Write-Output 'WebView2 SDK is ready.';return}
$downloadDirectory=Join-Path $sdkRoot ('download-'+[Guid]::NewGuid().ToString('N'))
$resolvedSdk=[IO.Path]::GetFullPath($sdkRoot).TrimEnd('\')
$resolvedDownload=[IO.Path]::GetFullPath($downloadDirectory)
if(-not $resolvedDownload.StartsWith($resolvedSdk+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe SDK download directory.'}
New-Item -ItemType Directory -Path $downloadDirectory -Force | Out-Null
New-Item -ItemType Directory -Path $destination -Force | Out-Null
try{
    $archive=Join-Path $downloadDirectory 'sdk.zip'
    Invoke-WebRequest -Uri "https://api.nuget.org/v3-flatcontainer/microsoft.web.webview2/$Version/microsoft.web.webview2.$Version.nupkg" -OutFile $archive
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $zip=[IO.Compression.ZipFile]::OpenRead($archive)
    try{
        foreach($item in $files.GetEnumerator()){
            $entry=$zip.GetEntry($item.Key)
            if($null -eq $entry){throw "SDK entry missing: $($item.Key)"}
            [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,(Join-Path $destination $item.Value),$true)
        }
    }finally{$zip.Dispose()}
    foreach($file in $files.Values){
        $signature=Get-AuthenticodeSignature -LiteralPath (Join-Path $destination $file)
        if($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation'){throw "SDK signature verification failed: $file"}
    }
    Write-Output "WebView2 SDK ready: $Version"
}finally{
    if(Test-Path -LiteralPath $resolvedDownload){
        $verified=(Resolve-Path -LiteralPath $resolvedDownload).Path
        if(-not $verified.StartsWith($resolvedSdk+'\',[StringComparison]::OrdinalIgnoreCase)){throw 'Unsafe SDK temporary cleanup target.'}
        Remove-Item -LiteralPath $verified -Recurse -Force
    }
}
