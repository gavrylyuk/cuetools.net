[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoBuild
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    if (-not $NoBuild) { & (Join-Path $PSScriptRoot 'build-net10.ps1') -Configuration $Configuration }
    $distribution = Join-Path $PSScriptRoot "bin\$Configuration\net10-dist\win-x64"
    $pluginSource = Join-Path $PSScriptRoot "bin\$Configuration\net10.0\plugins"
    $nativeSource = Join-Path $PSScriptRoot $(if ($Configuration -eq 'Debug') { 'ThirdPartyDebug\x64' } else { 'ThirdParty\x64' })
    $apps = [ordered]@{
        CUETools = @('CUETools/CUETools.Net10.csproj', 'net10.0-windows')
        CUERipper = @('CUERipper/CUERipper.Net10.csproj', 'net10.0-windows')
        'CUERipper.WPF' = @('CUERipper.WPF/CUERipper.WPF.Net10.csproj', 'net10.0-windows')
        CUEPlayer = @('CUEPlayer/CUEPlayer.Net10.csproj', 'net10.0-windows')
        BluTools = @('CUETools.eac3ui/BLUTools.Net10.csproj', 'net10.0-windows')
        Converter = @('CUETools.Converter/CUETools.Converter.csproj', 'net10.0')
        Flake = @('CUETools.Flake/CUETools.Flake.csproj', 'net10.0')
        ALACEnc = @('CUETools.ALACEnc/CUETools.ALACEnc.csproj', 'net10.0')
        LossyWAV = @('CUETools.LossyWAV/CUETools.LossyWAV.csproj', 'net10.0')
        ARCUE = @('CUETools.ARCUE/CUETools.ARCUE.csproj', 'net10.0')
        CTDBConverter = @('CUETools.CTDB.Converter/CUETools.CTDB.Converter.csproj', 'net10.0')
        eac3to = @('CUETools.eac3to/CUETools.eac3to.csproj', 'net10.0')
        ConsoleRipper = @('CUETools.Ripper.Console/CUETools.ConsoleRipper.csproj', 'net10.0')
        FLACCL = @('CUETools.FLACCL.cmd/CUETools.FLACCL.cmd.Net10.csproj', 'net10.0')
        ChaptersToCue = @('CUETools.ChaptersToCue/CUETools.ChaptersToCue.Net10.csproj', 'net10.0')
    }
    foreach ($appName in $apps.Keys) {
        $appOutput = Join-Path $distribution $appName
        & dotnet publish $apps[$appName][0] -c $Configuration -f $apps[$appName][1] -r win-x64 --self-contained false -o $appOutput --nologo
        if ($LASTEXITCODE -ne 0) { throw "Publishing $appName failed with exit code $LASTEXITCODE" }
        $plugins = Join-Path $appOutput 'plugins'
        New-Item -ItemType Directory -Path $plugins -Force | Out-Null
        # Keep each app's own resolved dependencies intact; portable and Windows Processor DLLs differ.
        if (Test-Path $pluginSource) { Copy-Item -Path "$pluginSource\*" -Destination $plugins -Recurse -Force }
        $nativeDestination = Join-Path $plugins 'x64'
        New-Item -ItemType Directory -Path $nativeDestination -Force | Out-Null
        foreach ($nativeFile in @('libFLAC_dynamic.dll', 'MACLibDll.dll', 'wavpackdll.dll')) {
            Copy-Item -LiteralPath (Join-Path $nativeSource $nativeFile) -Destination $nativeDestination -Force
        }
        foreach ($nativeFile in @('hdcd.dll', 'libmp3lame.dll', 'Unrar.dll')) {
            Copy-Item -LiteralPath (Join-Path $PSScriptRoot "ThirdParty\x64\$nativeFile") -Destination $nativeDestination -Force
        }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot "bin\x64\$Configuration\CUETools.AVX.dll") -Destination $appOutput -Force
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'ThirdParty\ICSharpCode.SharpZipLib.dll') -Destination $plugins -Force
        # RAR's managed adapter is architecture-neutral, despite its legacy win32 output directory.
        $rarAdapter = Join-Path $pluginSource 'win32\CUETools.Compression.Rar.dll'
        if (Test-Path $rarAdapter) { Copy-Item -LiteralPath $rarAdapter -Destination $nativeDestination -Force }
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GPL.txt'), (Join-Path $PSScriptRoot 'License.txt') -Destination $appOutput -Force
    }
    Write-Host "Published framework-dependent x64 apps: $distribution"
    Write-Host 'Install the .NET 10 Desktop Runtime for desktop apps. EAC integration remains a separate host-validation step.'
}
finally { Pop-Location }
