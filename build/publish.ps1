<#
.SYNOPSIS
    Builds a Hunches & Herrings release: a Release build (and tests, once there are any), the self-contained game and
    the installer artifacts\HunchesAndHerrings-Setup-<version>.exe.

.DESCRIPTION
    Run from anywhere (paths are relative to the repo root), or double-click publish.bat at the root:
        powershell -ExecutionPolicy Bypass -File build\publish.ps1
    Steps (stops at the first failure):
      1. Reads the version from Directory.Build.props (<Version>).
      2. Builds HunchesAndHerrings.slnx in Release, and runs the tests if a tests\ project exists.
      3. Publishes HunchesAndHerrings (WPF) self-contained for win-x64 (a folder, not single-file, no trimming) to
         artifacts\app\ and checks the game and licence files came with it.
      4. Optionally signs HunchesAndHerrings.exe (-Sign; off for now).
      5. Compiles installer\HunchesAndHerrings.iss with Inno Setup 6 into artifacts\, then signs the setup (if -Sign)
         and writes its SHA-256 next to it.
    Needs: the .NET 10 SDK and Inno Setup 6 (https://jrsoftware.org/isinfo.php; a build tool, not shipped).
    Mirrors Pixel8's and Throng's release routine.

.PARAMETER SkipTests
    Skip the build check and tests (not for real releases).

.PARAMETER Sign
    Sign HunchesAndHerrings.exe and the setup with signtool (needs -CertThumbprint and signtool on PATH).

.PARAMETER InnoCompiler
    Full path to ISCC.exe if Inno Setup 6 isn't in one of its usual folders.
#>
param(
    [switch] $SkipTests,
    [switch] $Sign,
    [string] $CertThumbprint = '',
    [string] $InnoCompiler = ''
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root 'artifacts'
$solution = Join-Path $root 'HunchesAndHerrings.slnx'
$appProject = Join-Path $root 'src\HunchesAndHerrings\HunchesAndHerrings.csproj'

function Step([string] $text) { Write-Host "`n== $text" -ForegroundColor Cyan }
function Check-Exit([string] $what) { if ($LASTEXITCODE -ne 0) { throw "$what failed (exit code $LASTEXITCODE)." } }

# ---- 1. Version
[xml] $props = Get-Content (Join-Path $root 'Directory.Build.props')
$version = ($props.Project.PropertyGroup | Where-Object { $_.Version } | Select-Object -First 1).Version
if (-not $version) { throw 'No <Version> in Directory.Build.props.' }
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw "The version '$version' isn't major.minor.patch." }
Step "Hunches & Herrings $version"

if (Test-Path $artifacts) { Remove-Item $artifacts -Recurse -Force }
New-Item -ItemType Directory -Path $artifacts | Out-Null

# ---- 2. Build check (and tests, when there are any)
if (-not $SkipTests) {
    $testsFolder = Join-Path $root 'tests'
    $hasTests = (Test-Path $testsFolder) -and (Get-ChildItem $testsFolder -Filter *.csproj -Recurse | Select-Object -First 1)
    if ($hasTests) {
        Step 'Tests'
        dotnet test $solution -c Release --nologo
        Check-Exit 'The tests'
    }
    else {
        Step 'Release build'
        dotnet build $solution -c Release --nologo
        Check-Exit 'The Release build'
    }
}

# ---- 3. The game (self-contained, so players need no .NET runtime)
Step 'HunchesAndHerrings (self-contained win-x64)'
$appOut = Join-Path $artifacts 'app'
dotnet publish $appProject -c Release -r win-x64 --self-contained true `
    -p:PublishTrimmed=false -p:PublishSingleFile=false -o $appOut --nologo
Check-Exit 'Publishing HunchesAndHerrings'
Get-ChildItem $appOut -Filter *.pdb -Recurse | Remove-Item -Force
foreach ($needed in 'HunchesAndHerrings.exe', 'HunchesAndHerrings.Core.dll', 'LICENSE', 'THIRD-PARTY-NOTICES.md') {
    if (-not (Test-Path (Join-Path $appOut $needed))) { throw "The game publish lacks $needed." }
}

# ---- 4. Signing (off for now)
function Sign-Files([string[]] $files) {
    if (-not $Sign) { return }
    if (-not $CertThumbprint) { throw '-Sign needs -CertThumbprint.' }
    & signtool sign /sha1 $CertThumbprint /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $files
    Check-Exit 'signtool'
}
Sign-Files @(Join-Path $appOut 'HunchesAndHerrings.exe')

# ---- 5. The installer
Step 'Installer'
if (-not $InnoCompiler) {
    $InnoCompiler = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe",
                      "$env:LocalAppData\Programs\Inno Setup 6\ISCC.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
}
if (-not $InnoCompiler) {
    $onPath = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($onPath) { $InnoCompiler = $onPath.Source }
}
if (-not $InnoCompiler) { throw 'Inno Setup 6 (ISCC.exe) was not found: install it or pass -InnoCompiler.' }
& $InnoCompiler "/DAppVersion=$version" "/O$artifacts" (Join-Path $root 'installer\HunchesAndHerrings.iss')
Check-Exit 'Inno Setup'

$setupName = "HunchesAndHerrings-Setup-$version.exe"
$setup = Join-Path $artifacts $setupName
Sign-Files @($setup)
Step "Done: $setup"
$hash = (Get-FileHash $setup -Algorithm SHA256).Hash
Write-Host "SHA-256 $hash"
"$hash  $setupName" | Set-Content (Join-Path $artifacts "HunchesAndHerrings-Setup-$version.sha256.txt")
