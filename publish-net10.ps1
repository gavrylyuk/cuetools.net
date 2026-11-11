[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$SelfContained,
    [switch]$Experimental
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
Push-Location $PSScriptRoot
try {
    if (-not $NoBuild) { & (Join-Path $PSScriptRoot 'build-net10.ps1') -Configuration $Configuration }
    $distributionName = if ($Experimental) { 'cuetools-parallel-experimental' } elseif ($SelfContained) { 'cuetools-only-self-contained' } else { 'cuetools-only' }
    $distribution = Join-Path $PSScriptRoot "bin\$Configuration\$distributionName\win-x64"
    $pluginSource = Join-Path $PSScriptRoot "bin\$Configuration\net10.0\plugins"
    $nativeSource = Join-Path $PSScriptRoot $(if ($Configuration -eq 'Debug') { 'ThirdPartyDebug\x64' } else { 'ThirdParty\x64' })
    $apps = [ordered]@{ CUETools = @('CUETools/CUETools.Net10.csproj', 'net10.0-windows') }
    foreach ($appName in $apps.Keys) {
        $appOutput = Join-Path $distribution $appName
        & dotnet publish $apps[$appName][0] -c $Configuration -f $apps[$appName][1] -r win-x64 --self-contained $SelfContained.IsPresent.ToString().ToLowerInvariant() -p:PublishTrimmed=false -p:PublishSingleFile=false -o $appOutput --nologo
        if ($LASTEXITCODE -ne 0) { throw "Publishing $appName failed with exit code $LASTEXITCODE" }
        if ($Experimental) { Set-Content -LiteralPath (Join-Path $appOutput 'parallel_experiment') -Value 'Parallel FLAC preview: isolated settings and single-instance identity.' }
        $plugins = Join-Path $appOutput 'plugins'
        New-Item -ItemType Directory -Path $plugins -Force | Out-Null
        # Keep CUETools runtime dependencies intact; omit cached hardware-ripper plugins.
        if (Test-Path $pluginSource) { Copy-Item -Path "$pluginSource\*" -Destination $plugins -Recurse -Force -Exclude 'CUETools.Ripper.SCSI*', 'Bwg.*' }
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
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'GPL.txt'), (Join-Path $PSScriptRoot 'License.txt'), (Join-Path $PSScriptRoot 'CUETools/user_profiles_enabled') -Destination $appOutput -Force
    }
    Write-Host "Published x64 CUETools (self-contained: $($SelfContained.IsPresent)): $distribution"
    if (-not $SelfContained) { Write-Host 'Install the .NET 10 Desktop Runtime and Visual C++ x64 runtime.' }
}
finally { Pop-Location }
