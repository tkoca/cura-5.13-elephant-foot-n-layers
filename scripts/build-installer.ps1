<#
.SYNOPSIS
  Builds Cura-5.13-Elephant-Foot-N-Layers-Setup.exe (and optionally the test build).

.EXAMPLE
  .\scripts\build-installer.ps1 -CuraSourceZip Cura-5.13.0.zip -CuraEngine build\CuraEngine.exe -VsInstallPath "C:\BuildTools" -OutDir dist
#>
param(
    [Parameter(Mandatory)] [string] $CuraSourceZip,   # UltiMaker Cura 5.13.0 source archive (GitHub tag 5.13.0)
    [Parameter(Mandatory)] [string] $CuraEngine,      # CuraEngine.exe built by build-curaengine.ps1
    [Parameter(Mandatory)] [string] $VsInstallPath,   # Visual Studio / Build Tools (for the Roslyn C# compiler)
    [string] $OutDir = (Join-Path (Split-Path -Parent $PSScriptRoot) 'dist'),
    [string] $Python = 'python',
    [switch] $TestBuild
)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$csc = Join-Path $VsInstallPath 'MSBuild\Current\Bin\Roslyn\csc.exe'
if (-not (Test-Path $csc)) { throw "Roslyn csc.exe not found: $csc" }
$fx = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
New-Item -ItemType Directory -Force $OutDir | Out-Null
$obj = Join-Path $OutDir 'obj'
New-Item -ItemType Directory -Force $obj | Out-Null

# Refuse binaries that contain the builder's user name, profile path or computer name
# (for example source paths compiled into CuraEngine.exe by the dependency builds).
function Assert-NoPersonalData([string] $file) {
    $bytes = [IO.File]::ReadAllBytes($file)
    $texts = @([Text.Encoding]::GetEncoding(28591).GetString($bytes), [Text.Encoding]::Unicode.GetString($bytes))
    $needles = @($env:USERPROFILE, $env:USERNAME, $env:COMPUTERNAME) | Where-Object { $_ -and $_.Length -ge 3 }
    foreach ($needle in $needles) {
        foreach ($text in $texts) {
            if ($text.IndexOf($needle, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
                throw "$file contains local user or machine information ('$needle'). Rebuild with neutral paths (see SOURCE_AND_BUILD.md)."
            }
        }
    }
}
Assert-NoPersonalData $CuraEngine

$payload = Join-Path $obj 'payload.zip'
$ErrorActionPreference = 'Continue'
& $Python (Join-Path $repo 'scripts\make_payload.py') $CuraSourceZip $CuraEngine $payload 2>&1 | ForEach-Object { "$_" }
$ErrorActionPreference = 'Stop'
if ($LASTEXITCODE -ne 0) { throw 'payload build failed' }

function Invoke-Csc([string] $out, [string[]] $extra) {
    $cscArgs = @('/nologo', '/noconfig', '/deterministic+', '/optimize+', '/debug-', '/platform:x64', '/target:winexe',
        '/langversion:7.3', '/warnaserror+', '/utf8output', "/pathmap:$repo=.",
        "/win32manifest:$(Join-Path $repo 'installer\app.manifest')",
        "/resource:$payload,payload.zip",
        "/reference:$fx\mscorlib.dll", "/reference:$fx\System.dll", "/reference:$fx\System.Core.dll",
        "/reference:$fx\System.Drawing.dll", "/reference:$fx\System.Windows.Forms.dll", "/reference:$fx\System.IO.Compression.dll",
        "/out:$out") + $extra + @((Join-Path $repo 'installer\Installer.cs'))
    $ErrorActionPreference = 'Continue'
    & $csc @cscArgs 2>&1 | ForEach-Object { "$_" }
    $ErrorActionPreference = 'Stop'
    if ($LASTEXITCODE -ne 0) { throw "csc failed for $out" }
}

$setup = Join-Path $OutDir 'Cura-5.13-Elephant-Foot-N-Layers-Setup.exe'
Invoke-Csc $setup @()
Assert-NoPersonalData $setup
if ($TestBuild) { Invoke-Csc (Join-Path $obj 'Setup.Test.exe') @('/define:TEST') }
Get-FileHash $setup -Algorithm SHA256 | ForEach-Object { "{0}  {1}" -f $_.Hash.ToLower(), (Split-Path -Leaf $_.Path) } |
    Tee-Object -Variable sums
# ASCII so that `sha256sum -c SHA256SUMS.txt` works
[IO.File]::WriteAllText((Join-Path $OutDir 'SHA256SUMS.txt'), ($sums -join "`n") + "`n", [Text.Encoding]::ASCII)
