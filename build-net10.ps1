[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Debug',
    [switch]$SkipNative,
    [switch]$SkipTests
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Invoke-Checked {
    param([string]$Tool, [string[]]$Arguments)
    & $Tool @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$Tool failed with exit code $LASTEXITCODE" }
}
Push-Location $PSScriptRoot
try {
    $sdkVersion = & dotnet --version
    if ($LASTEXITCODE -ne 0 -or [version]($sdkVersion.Split('-')[0]) -lt [version]'10.0') {
        throw 'Install the .NET 10 SDK before building.'
    }
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    $msbuild = & $vswhere -latest -products '*' -version '[18.0,19.0)' -requires Microsoft.Component.MSBuild -find 'MSBuild\Current\Bin\MSBuild.exe' | Select-Object -First 1
    if (-not $msbuild) { throw 'Visual Studio 2026 MSBuild 18 with C++/CLI support is required for native builds.' }

    Invoke-Checked -Tool dotnet -Arguments @('build', 'CUETools.Codecs/CUETools.Codecs.csproj', '-c', $Configuration, '-f', 'net10.0', '--nologo')
    if (-not $SkipNative) {
        $nativeProjects = @(
            'ThirdParty/flac/src/libFLAC/libFLAC_dynamic.vcxproj',
            'ThirdParty/MAC_SDK/Source/Projects/VS2022/MACLibDll/MACLibDll.vcxproj',
            'ThirdParty/WavPack/src/libwavpack.vcxproj',
            'ThirdParty/WavPack/wavpackdll/wavpackdll.vcxproj',
            'CUETools.AVX/CUETools.AVX.vcxproj',
            'CUETools.Codecs.TTA/CUETools.Codecs.TTA.Net10.vcxproj'
        )
        foreach ($project in $nativeProjects) {
            Invoke-Checked -Tool $msbuild -Arguments @($project, '/t:Build', "/p:Configuration=$Configuration", '/p:Platform=x64', "/p:SolutionDir=$PSScriptRoot\", '/v:minimal', '/nologo')
        }
    }

    Invoke-Checked -Tool dotnet -Arguments @('build', 'CUETools.Net10.slnx', '-c', $Configuration, '--nologo')
    # Build standalone managed plugins and console tools that are discovered dynamically.
    $solutionText = [IO.File]::ReadAllText((Join-Path $PSScriptRoot 'CUETools.sln'))
    $projectMatches = [regex]::Matches($solutionText, '"([^"\r\n]+\.csproj)"')
    foreach ($match in $projectMatches) {
        $project = $match.Groups[1].Value
        $projectXml = [xml][IO.File]::ReadAllText((Join-Path $PSScriptRoot $project))
        if (-not $projectXml.DocumentElement.HasAttribute('Sdk')) { continue }
        $frameworkNodes = $projectXml.SelectNodes('/Project/PropertyGroup/TargetFrameworks')
        if (@($frameworkNodes | Where-Object { $_.InnerText.Split(';') -contains 'net10.0' }).Count -eq 0) { continue }
        Invoke-Checked -Tool dotnet -Arguments @('build', $project, '-c', $Configuration, '-f', 'net10.0', '--nologo')
    }
    if (-not $SkipTests) {
        $results = "bin/TestResults/net10-$Configuration"
        Invoke-Checked -Tool dotnet -Arguments @('test', 'CUETools/CUETools.TestParity/CUETools.TestParity.csproj', '-c', $Configuration, '-f', 'net10.0', '--no-build', '--logger', 'trx', '--results-directory', $results)
        foreach ($testProject in @(
            'CUETools/CUETools.TestCodecs/CUETools.TestCodecs.Net10.csproj',
            'CUETools/CUETools.TestProcessor/CUETools.TestProcessor.Net10.csproj',
            'CUETools.Net10.Tests/CUETools.Net10.Tests.csproj'
        )) {
            Invoke-Checked -Tool dotnet -Arguments @('test', $testProject, '-c', $Configuration, '--no-build', '--logger', 'trx', '--results-directory', $results)
        }
    }
    Write-Host "Modern $Configuration build completed. Desktop binaries: bin\$Configuration\net10.0-windows"
}
finally { Pop-Location }
