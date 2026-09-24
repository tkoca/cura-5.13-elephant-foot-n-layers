<#
.SYNOPSIS
  Slicing tests: compares the patched CuraEngine with the official Cura 5.13.0 CuraEngine.

  1. With layer count 1 (default) the G-code must be identical to the official engine.
  2. With N layers the outer wall of the first N layers must be offset as specified,
     both without and with gradual compensation.
#>
param(
    [Parameter(Mandatory)] [string] $OfficialEngine,   # CuraEngine.exe from UltiMaker Cura 5.13.0
    [Parameter(Mandatory)] [string] $PatchedEngine,
    [Parameter(Mandatory)] [string] $PrinterDefinition,   # PATCHED fdmprinter.def.json (from the payload)
    [Parameter(Mandatory)] [string] $ExtruderDefinition,  # fdmextruder.def.json of Cura 5.13.0
    [string] $CuraDir = 'C:\Program Files\UltiMaker Cura 5.13.0',   # runtime DLLs (Arcus, clipper, TBB, MSVC runtime)
    [string] $Python = 'python',
    [string] $WorkDir = (Join-Path $env:TEMP ('efnl-slice-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
New-Item -ItemType Directory -Force $WorkDir | Out-Null
$Definitions = Join-Path $WorkDir 'definitions'
& $Python (Join-Path $PSScriptRoot 'prepare_definitions.py') $PrinterDefinition $ExtruderDefinition $Definitions
if ($LASTEXITCODE -ne 0) { throw 'prepare_definitions.py failed' }
$env:CURA_ENGINE_SEARCH_PATH = $Definitions
$here = $PSScriptRoot
$model = Join-Path $here 'box20.stl'
# Run each engine the way Cura does: next to Cura's runtime DLLs.
function Stage-Engine([string] $engine, [string] $name) {
    $dir = Join-Path $WorkDir $name
    New-Item -ItemType Directory -Force $dir | Out-Null
    Copy-Item (Join-Path $CuraDir '*.dll') $dir -Force
    Copy-Item $engine (Join-Path $dir 'CuraEngine.exe') -Force
    return (Join-Path $dir 'CuraEngine.exe')
}
$OfficialEngine = Stage-Engine $OfficialEngine 'official-engine'
$PatchedEngine = Stage-Engine $PatchedEngine 'patched-engine'
$script:failures = 0; $script:passed = 0
function Check([string] $name, [bool] $ok) {
    if ($ok) { $script:passed++; Write-Host "  PASS  $name" } else { $script:failures++; Write-Host "  FAIL  $name" -ForegroundColor Red }
}

function Slice([string] $engine, [string] $name, [string[]] $settings) {
    $out = Join-Path $WorkDir "$name.gcode"
    $common = @('machine_width=220', 'machine_depth=220', 'machine_height=250', 'machine_center_is_zero=false',
        'layer_height=0.2', 'layer_height_0=0.2', 'adhesion_type=none', 'support_enable=false',
        'roofing_layer_count=0', 'flooring_layer_count=0', 'flooring_extruder_nr=0', 'roofing_extruder_nr=0') + $settings
    $sets = @(); foreach ($s in $common) { $sets += @('-s', $s) }
    # Global settings, then extruder 0 (fdmextruder.def.json) with the same overrides, then the mesh.
    $engineArgs = @('slice', '-j', (Join-Path $Definitions 'fdmprinter.def.json')) + $sets +
        @('-e0', '-j', (Join-Path $Definitions 'fdmextruder.def.json')) + $sets +
        @('-l', $model, '-s', 'mesh_position_x=100', '-s', 'mesh_position_y=100') + $sets + @('-o', $out)
    $saved = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    & $engine @engineArgs 2>&1 | Out-File (Join-Path $WorkDir "$name.log") -Encoding utf8
    $code = $LASTEXITCODE
    $ErrorActionPreference = $saved
    if ($code -ne 0 -or -not (Test-Path $out)) { throw "slice failed: $name (exit $code), see $WorkDir\$name.log" }
    return $out
}
function Body([string] $gcode) {
    # Ignore header lines that may legitimately differ between engine builds.
    Get-Content $gcode | Where-Object { $_ -notmatch '^;(Generated with|TIME|Filament used|PRINT.TIME)' }
}
function Widths([string] $gcode) {
    $saved = $ErrorActionPreference; $ErrorActionPreference = 'Continue'
    $rows = & $Python (Join-Path $here 'analyze_gcode.py') $gcode 8
    $ErrorActionPreference = $saved
    $map = @{}
    foreach ($r in $rows) { $p = $r -split ' '; $map[[int]$p[0]] = [double]$p[1] }
    return $map
}

Write-Host "S1 layer count 1 == official engine (defaults)"
$a = Slice $OfficialEngine 'official-default' @()
$b = Slice $PatchedEngine 'patched-default' @()
Check 'identical G-code' ((Compare-Object (Body $a) (Body $b) -SyncWindow 0) -eq $null)

Write-Host "S2 layer count 1 == official engine (initial layer expansion -0.2)"
$a = Slice $OfficialEngine 'official-xy0' @('xy_offset_layer_0=-0.2')
$b = Slice $PatchedEngine 'patched-xy0' @('xy_offset_layer_0=-0.2', 'elephant_foot_compensation_layers=1', 'elephant_foot_compensation_taper=true')
Check 'identical G-code' ((Compare-Object (Body $a) (Body $b) -SyncWindow 0) -eq $null)
$base = Widths $b

Write-Host "S3 N=4 without taper"
$w = Widths (Slice $PatchedEngine 'n4' @('xy_offset_layer_0=-0.2', 'elephant_foot_compensation_layers=4', 'elephant_foot_compensation_taper=false'))
$ref = $base[5]
for ($l = 0; $l -lt 4; $l++) { Check ("layer {0}: width {1:N3} = ref-0.4" -f $l, $w[$l]) ([math]::Abs(($ref - $w[$l]) - 0.4) -lt 0.02) }
Check ("layer 4: width {0:N3} = ref" -f $w[4]) ([math]::Abs($ref - $w[4]) -lt 0.02)

Write-Host "S4 N=4 with taper (-0.20, -0.15, -0.10, -0.05, then 0)"
$w = Widths (Slice $PatchedEngine 'n4t' @('xy_offset_layer_0=-0.2', 'elephant_foot_compensation_layers=4', 'elephant_foot_compensation_taper=true'))
$expected = @(0.4, 0.3, 0.2, 0.1, 0.0)
for ($l = 0; $l -lt 5; $l++) { Check ("layer {0}: inset {1:N3} ~ {2:N3}" -f $l, ($ref - $w[$l]), $expected[$l]) ([math]::Abs(($ref - $w[$l]) - $expected[$l]) -lt 0.02) }

Write-Host "S5 taper toward a non-zero Horizontal Expansion (+0.1)"
$w0 = Widths (Slice $PatchedEngine 'xy01' @('xy_offset=0.1'))
$w = Widths (Slice $PatchedEngine 'n2t' @('xy_offset=0.1', 'xy_offset_layer_0=-0.1', 'elephant_foot_compensation_layers=2', 'elephant_foot_compensation_taper=true'))
$r = $w0[5]
Check ("layer 0 inset {0:N3} ~ 0.4" -f ($r - $w[0])) ([math]::Abs(($r - $w[0]) - 0.4) -lt 0.02)
Check ("layer 1 inset {0:N3} ~ 0.2" -f ($r - $w[1])) ([math]::Abs(($r - $w[1]) - 0.2) -lt 0.02)
Check ("layer 2 inset {0:N3} ~ 0" -f ($r - $w[2])) ([math]::Abs($r - $w[2]) -lt 0.02)

Write-Host ""
Write-Host "RESULT: $script:passed passed, $script:failures failed   (G-code: $WorkDir)"
if ($script:failures -gt 0) { exit 1 }
