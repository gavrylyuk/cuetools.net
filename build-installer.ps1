[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')][string]$Configuration = 'Release',
    [switch]$NoBuild,
    [switch]$Experimental,
    [string]$InnoCompiler
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
function Get-VerifiedDownload {
    param([string]$Url, [string]$Path, [string]$Publisher)
    if (-not (Test-Path -LiteralPath $Path)) { Invoke-WebRequest -Uri $Url -OutFile $Path }
    $signature = Get-AuthenticodeSignature -LiteralPath $Path
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch $Publisher) {
        throw "Invalid Authenticode signature: $Path"
    }
}
Push-Location $PSScriptRoot
try {
    $tools = Join-Path $PSScriptRoot 'bin/tools'
    $prerequisites = Join-Path $PSScriptRoot 'bin/installer-prerequisites'
    New-Item -ItemType Directory -Path $tools, $prerequisites -Force | Out-Null
    if (-not $InnoCompiler) {
        $InnoCompiler = Join-Path $tools 'InnoSetup/ISCC.exe'
        if (-not (Test-Path -LiteralPath $InnoCompiler)) {
            $bootstrap = Join-Path $tools 'innosetup-7.1.1-x64.exe'
            Get-VerifiedDownload 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe' $bootstrap 'Pyrsys'
            $toolDirectory = Join-Path $tools 'InnoSetup'
            $process = Start-Process -FilePath $bootstrap -ArgumentList @('/VERYSILENT', '/SUPPRESSMSGBOXES', '/NORESTART', '/CURRENTUSER', "/DIR=`"$toolDirectory`"", '/NOICONS') -WindowStyle Hidden -Wait -PassThru
            if ($process.ExitCode -ne 0) { throw "Inno Setup installation failed: $($process.ExitCode)" }
        }
    }
    $redist = Join-Path $prerequisites 'vc_redist.x64.exe'
    Get-VerifiedDownload 'https://aka.ms/vc14/vc_redist.x64.exe' $redist 'Microsoft Corporation'
    & ./publish-net10.ps1 -Configuration $Configuration -NoBuild:$NoBuild -SelfContained -Experimental:$Experimental
    $distribution = if ($Experimental) { 'cuetools-parallel-experimental' } else { 'cuetools-only-self-contained' }
    $payload = Join-Path $PSScriptRoot "bin/$Configuration/$distribution/win-x64/CUETools"
    foreach ($file in @('CUETools.exe', 'coreclr.dll', 'System.Windows.Forms.dll', 'user_profiles_enabled', 'plugins/x64/CUETools.Codecs.TTA.dll')) {
        if (-not (Test-Path -LiteralPath (Join-Path $payload $file))) { throw "Missing installer payload file: $file" }
    }
    $output = Join-Path $PSScriptRoot "bin/$Configuration/installer"
    New-Item -ItemType Directory -Path $output -Force | Out-Null
    $version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $payload 'CUETools.dll'))
    $appVersion = "$($version.FileMajorPart).$($version.FileMinorPart).$($version.FileBuildPart).$($version.FilePrivatePart)"
    $variant = if ($Experimental) { 'parallel-preview' } else { 'net10' }
    [string[]]$defines = @(if ($Experimental) { '/DExperimental=1' })
    & $InnoCompiler @defines "/DVariant=$variant" "/DPayloadDir=$payload" "/DRedistPath=$redist" "/DOutputDir=$output" "/DAppVersion=$appVersion" (Join-Path $PSScriptRoot 'installer/CUETools.iss')
    if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed: $LASTEXITCODE" }
    $installer = Join-Path $output "CUETools-$appVersion-$variant-win-x64-Setup.exe"
    $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash
    "$hash  $([IO.Path]::GetFileName($installer))" | Set-Content -LiteralPath "$installer.sha256"
    Write-Host "Installer: $installer"
}
finally { Pop-Location }
