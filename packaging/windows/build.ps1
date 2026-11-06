<#
.SYNOPSIS
    Builds DisplaySwitch release artifacts for Windows: a portable zip and an installer.

.DESCRIPTION
    1. Runs the test suites (skip with -SkipTests).
    2. Publishes a trimmed, self-contained win-x64 build (no .NET install needed on target machines).
    3. Zips it as a portable build.
    4. Compiles the Inno Setup installer.
    5. Signs both executables when a certificate is configured; otherwise reports clearly that the
       build is unsigned. Unsigned builds work but show a SmartScreen warning on first run.

.PARAMETER SkipTests
    Skip running tests (for quick local packaging iterations only).

.EXAMPLE
    ./packaging/windows/build.ps1

.NOTES
    Signing: set SIGN_CERT_PATH (a .pfx file) and SIGN_CERT_PASSWORD, and have signtool.exe
    (Windows SDK) on PATH.
#>
[CmdletBinding()]
param([switch]$SkipTests)

$ErrorActionPreference = 'Stop'
$root = Resolve-Path (Join-Path $PSScriptRoot '..\..')
$appProject = Join-Path $root 'src\DisplaySwitch.App\DisplaySwitch.App.csproj'
$publishDir = Join-Path $root 'artifacts\publish\win-x64'
$installerDir = Join-Path $root 'artifacts\installer'

function Invoke-Checked([scriptblock]$Command, [string]$What) {
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed with exit code $LASTEXITCODE." }
}

function Find-InnoCompiler {
    $onPath = Get-Command iscc.exe -ErrorAction SilentlyContinue
    $candidates = @(
        $(if ($onPath) { $onPath.Source })
        "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe"
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "$env:ProgramFiles\Inno Setup 6\ISCC.exe"
    )
    $found = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
    if (-not $found) { throw 'Inno Setup 6 not found. Install it with: winget install JRSoftware.InnoSetup' }
    return $found
}

function Find-SignTool {
    $onPath = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($onPath) { return $onPath.Source }
    # Fall back to the newest Windows SDK copy.
    $sdk = Get-ChildItem "${env:ProgramFiles(x86)}\Windows Kits\10\bin\*\x64\signtool.exe" -ErrorAction SilentlyContinue |
        Sort-Object FullName -Descending | Select-Object -First 1
    if (-not $sdk) { throw 'signtool.exe not found. Install the Windows SDK.' }
    return $sdk.FullName
}

function Invoke-Signing([string]$File) {
    if (-not $env:SIGN_CERT_PATH) {
        Write-Warning "UNSIGNED: $([IO.Path]::GetFileName($File)) (set SIGN_CERT_PATH to sign)."
        return
    }
    $signtool = Find-SignTool
    Invoke-Checked { & $signtool sign /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 /f $env:SIGN_CERT_PATH /p $env:SIGN_CERT_PASSWORD $File } "Signing $File"
}

$version = (& dotnet msbuild $appProject -getProperty:Version).Trim()
Write-Host "Building DisplaySwitch $version" -ForegroundColor Cyan

if (-not $SkipTests) {
    Invoke-Checked { dotnet test (Join-Path $root 'DisplaySwitch.slnx') -c Release } 'Tests'
}

Remove-Item $publishDir, $installerDir -Recurse -Force -ErrorAction SilentlyContinue
Invoke-Checked { dotnet publish $appProject -c Release -r win-x64 -o $publishDir } 'Publish'
Invoke-Signing (Join-Path $publishDir 'DisplaySwitch.exe')

New-Item -ItemType Directory -Force $installerDir | Out-Null
$zip = Join-Path $installerDir "DisplaySwitch-$version-win-x64-portable.zip"
Compress-Archive -Path (Join-Path $publishDir '*') -DestinationPath $zip -CompressionLevel Optimal

$iscc = Find-InnoCompiler
Invoke-Checked { & $iscc /Q "/DAppVersion=$version" (Join-Path $PSScriptRoot 'DisplaySwitch.iss') } 'Installer compilation'
$setup = Join-Path $installerDir "DisplaySwitch-$version-win-x64-setup.exe"
Invoke-Signing $setup

Get-ChildItem $installerDir | ForEach-Object {
    $hash = (Get-FileHash $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    "{0}  {1}" -f $hash, $_.Name
} | Set-Content -Encoding ascii (Join-Path $installerDir 'SHA256SUMS.txt')

Write-Host "`nArtifacts in $installerDir" -ForegroundColor Green
Get-ChildItem $installerDir | Format-Table Name, @{ Label = 'Size (MB)'; Expression = { '{0:N1}' -f ($_.Length / 1MB) } } -AutoSize
