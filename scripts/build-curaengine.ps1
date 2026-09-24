<#
.SYNOPSIS
  Builds CuraEngine 5.13.0 with the elephant-foot N-layer patch (Windows x64, MSVC).

.DESCRIPTION
  1. Extracts the upstream CuraEngine 5.13.0 source archive into a clean folder.
  2. Verifies the pristine src/slicer.cpp against a known SHA-256 (line endings normalised).
  3. Applies patches/CuraEngine-5.13.0.patch with scripts/apply_patch.py.
  4. Builds with Conan 2 + CMake + Ninja using the UltiMaker Conan configuration.

  Source file paths of the dependencies built by Conan end up in the executable (__FILE__ in
  assertions/logging of gRPC, protobuf, abseil, ...). Keep the Conan home, the work folder and
  TEMP on short neutral paths outside the user profile so that no user or machine name is
  embedded; the script refuses paths inside the user profile.

  VSLANG=1033 forces English compiler output. With a localized Visual Studio
  (for example Turkish) the "/showIncludes" prefix is translated and breaks
  resource-compiler steps (RC1107) inside dependency builds.
#>
param(
    [Parameter(Mandatory)] [string] $SourceZip,      # CuraEngine-5.13.0.zip (GitHub tag archive)
    [Parameter(Mandatory)] [string] $WorkDir,        # empty or disposable folder
    [Parameter(Mandatory)] [string] $VsInstallPath,  # folder that contains VC\Auxiliary\Build\vcvars64.bat
    [string] $Python = 'python',
    [string] $ConanHome = 'C:\conan-efnl'           # short, neutral path (see below)
)
$ErrorActionPreference = 'Stop'
# Native tools (conan, cmake) write progress to stderr; Windows PowerShell 5.1 would turn that into
# terminating errors under 'Stop', so native calls are checked through $LASTEXITCODE instead.
function Invoke-Native([scriptblock] $block) {
    $saved = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    try { & $block 2>&1 | ForEach-Object { "$_" } } finally { $ErrorActionPreference = $saved }
}
$repo = Split-Path -Parent $PSScriptRoot
$pristineSlicer = '0397cf03e606bd7f2f3653ee7fce28d0ad73b28c146e66aa78f3e7c49e89e5ef'

function Get-LfHash([string] $path) {
    $text = [IO.File]::ReadAllText($path).Replace("`r`n", "`n")
    $sha = [Security.Cryptography.SHA256]::Create()
    ($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)) | ForEach-Object { $_.ToString('x2') }) -join ''
}

# --- Visual Studio environment -------------------------------------------------
$vcvars = Join-Path $VsInstallPath 'VC\Auxiliary\Build\vcvars64.bat'
if (-not (Test-Path $vcvars)) { throw "vcvars64.bat not found: $vcvars" }
cmd /c "`"$vcvars`" >nul && set" | ForEach-Object {
    if ($_ -match '^([^=]+)=(.*)$') { [Environment]::SetEnvironmentVariable($matches[1], $matches[2]) }
}
$env:VSLANG = '1033'
$cmakeDir = Join-Path $VsInstallPath 'Common7\IDE\CommonExtensions\Microsoft\CMake'
$env:PATH = "$cmakeDir\CMake\bin;$cmakeDir\Ninja;$env:PATH"
foreach ($p in @($ConanHome, $WorkDir)) {
    if ([IO.Path]::GetFullPath($p).StartsWith($env:USERPROFILE, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Use a folder outside the user profile (it would be embedded in the binary): $p"
    }
}
$env:CONAN_HOME = $ConanHome
New-Item -ItemType Directory -Force (Join-Path $ConanHome 'tmp') | Out-Null
$env:TEMP = Join-Path $ConanHome 'tmp'
$env:TMP = $env:TEMP

# --- Clean source + patch ---------------------------------------------------
if (Test-Path $WorkDir) { Remove-Item $WorkDir -Recurse -Force }
New-Item -ItemType Directory $WorkDir | Out-Null
Expand-Archive -Path $SourceZip -DestinationPath $WorkDir
$src = Get-ChildItem $WorkDir -Directory | Select-Object -First 1 -ExpandProperty FullName
$slicer = Join-Path $src 'src\slicer.cpp'
$actual = Get-LfHash $slicer
if ($actual -ne $pristineSlicer) { throw "Unexpected upstream slicer.cpp ($actual). Is this CuraEngine 5.13.0?" }
Invoke-Native { & $Python (Join-Path $repo 'scripts\apply_patch.py') (Join-Path $repo 'patches\CuraEngine-5.13.0.patch') $src }
if ($LASTEXITCODE -ne 0) { throw 'Patch failed' }

# --- Build ------------------------------------------------------------------
Push-Location $src
try {
    Invoke-Native { & conan build . --build=missing -s build_type=Release }
    if ($LASTEXITCODE -ne 0) { throw "conan build failed ($LASTEXITCODE)" }
} finally { Pop-Location }

$exe = Get-ChildItem $src -Recurse -Filter CuraEngine.exe | Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $exe) { throw 'CuraEngine.exe not produced' }
Write-Host ("CuraEngine.exe: {0}" -f $exe.FullName)
Write-Host ("SHA256: {0}" -f (Get-FileHash $exe.FullName -Algorithm SHA256).Hash)
