<#
.SYNOPSIS
  Scenario tests for the installer using the TEST build (no administrator rights needed).

.DESCRIPTION
  The TEST build redirects every location (Program Files data folder, ProgramData legacy folder,
  Cura installation, roaming and local Cura profile, HKLM uninstall key -> HKCU\Software\EFNLTest)
  below a temporary folder, so the real system is never touched.

  Fixture sources:
    -OriginalsDir  folder with the original Cura 5.13.0 files (same relative layout as the Cura folder)
    -LegacyDir     folder with the payload of the 4.0.0 installer (same layout), for the upgrade test
#>
param(
    [Parameter(Mandatory)] [string] $TestExe,
    [Parameter(Mandatory)] [string] $OriginalsDir,
    [string] $LegacyDir,
    [string] $WorkDir = (Join-Path $env:TEMP ('efnl-tests-' + [guid]::NewGuid().ToString('N')))
)
$ErrorActionPreference = 'Stop'
$files = @('CuraEngine.exe',
    'share\cura\resources\definitions\fdmprinter.def.json',
    'share\cura\resources\i18n\tr_TR\fdmprinter.def.json.po',
    'share\cura\resources\i18n\tr_TR\LC_MESSAGES\fdmprinter.def.json.mo',
    'share\cura\resources\setting_visibility\expert.cfg')
$originalHashes = @('36e36d4618cd09a0cd8802a565c2cdbe54804eb750ac84303a0c6d6f784afde2',
    '8fbbf8b779e806bd8d0b0e2b8b9be17bdd26a575b7ea1875c20481eb2c7e11ee',
    'ace33455f77ad50ed5d10dbd2f1c2b5732d2c59dcc5e99a9e715ad1bd3f51a85',
    '16dda401341e0805149bdcd674677bf5081413163c6b059255c637187599d3ad',
    '9b646941fa24799eda46ce207f586ab72687d7b02f837264287bb022b720a4ba')
$legacyHashes = @('ba46bf125c4fa75bef081d9f33eb61cd69d9b8e1e47a41d320d8efeec4436cce',
    '04e4f00d856dc595252df7307d5dbce03140444bf7b16bbed64b75080c53f2bb',
    '90a950c7e07be4436052f5c7fc5d6d5f3f8834c0be28c4a4db9f9ae2caf0a55a',
    '1e9b6e085e99f99e6786458a366a61a88525a062144c7502c937a1941cb0d220',
    'd1ab34aab68519fa4017d62c632d28e1373abb4c21a6197e4ead0de5120e91a9')
$id = 'CuraElephantFootNLayers513'
$script:failures = 0
$script:passed = 0

function Sha([string] $p) { (Get-FileHash -LiteralPath $p -Algorithm SHA256).Hash.ToLower() }
function Check([string] $name, [bool] $condition) {
    if ($condition) { $script:passed++; Write-Host "  PASS  $name" }
    else { $script:failures++; Write-Host "  FAIL  $name" -ForegroundColor Red }
}
function CuraHashes { $files | ForEach-Object { Sha (Join-Path $cura $_) } }
function Same([object[]] $a, [object[]] $b) { ($a -join ',') -eq ($b -join ',') }

function Reset-Fixture {
    if (Test-Path $WorkDir) { cmd /c "rmdir /s /q `"$WorkDir`"" | Out-Null }
    New-Item -ItemType Directory $WorkDir | Out-Null
    $script:cura = Join-Path $WorkDir 'Cura'
    foreach ($f in $files) {
        $t = Join-Path $cura $f
        New-Item -ItemType Directory -Force (Split-Path $t) | Out-Null
        Copy-Item (Join-Path $OriginalsDir $f) $t
    }
    Remove-Item 'HKCU:\Software\EFNLTest' -Recurse -Force -ErrorAction SilentlyContinue
    # Profile fixtures
    $roam = Join-Path $WorkDir 'Roaming\cura\5.13'
    New-Item -ItemType Directory -Force "$roam\user", "$roam\quality_changes", "$roam\plugins\SomePlugin" | Out-Null
    Set-Content "$roam\cura.cfg" -Encoding UTF8 -Value "[general]`r`nvisible_settings = xy_offset;elephant_foot_compensation_layers;xy_offset_layer_0;elephant_foot_compensation_taper`r`ntheme = cura-light`r`ncategories_expanded = ;alternate_extra_perimeter;elephant_foot_compensation_layers;elephant_foot_compensation_taper;fill_outline_gaps"
    Set-Content "$roam\user\custom_user.inst.cfg" -Encoding UTF8 -Value "[general]`r`nversion = 4`r`n`r`n[values]`r`nelephant_foot_compensation_layers = 4`r`nelephant_foot_compensation_taper = True`r`nxy_offset_layer_0 = -0.2"
    Set-Content "$roam\quality_changes\my_profile.inst.cfg" -Encoding UTF8 -Value "[values]`r`nelephant_foot_compensation_layers = 3`r`nlayer_height = 0.2"
    Set-Content "$roam\plugins\SomePlugin\plugin.cfg" -Encoding UTF8 -Value "elephant_foot_compensation_layers = keep"
    $cache = Join-Path $WorkDir 'Local\cura\5.13\cache\definitions\5.13.0'
    New-Item -ItemType Directory -Force $cache | Out-Null
    Set-Content "$cache\fdmprinter" -Value 'binary... elephant_foot_compensation_layers ...'
    Set-Content "$cache\other_printer" -Value 'no added settings'
}

function Run([string[]] $arguments) {
    $env:EFNL_TEST_ROOT = $WorkDir
    $p = Start-Process -FilePath $TestExe -ArgumentList $arguments -Wait -PassThru -NoNewWindow `
        -RedirectStandardError (Join-Path $WorkDir 'stderr.txt') -RedirectStandardOutput (Join-Path $WorkDir 'stdout.txt')
    $script:lastError = Get-Content (Join-Path $WorkDir 'stderr.txt') -Raw -ErrorAction SilentlyContinue
    if ($script:lastError) { Write-Host ("        " + $script:lastError.Trim().Replace("`n", "`n        ")) -ForegroundColor DarkGray }
    return $p.ExitCode
}

function Data { Join-Path $WorkDir "ProgramFiles\$id" }
function RegKey { Get-ItemProperty "HKCU:\Software\EFNLTest\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\$id" -ErrorAction SilentlyContinue }

# Sanity: fixtures
for ($i = 0; $i -lt $files.Count; $i++) {
    if ((Sha (Join-Path $OriginalsDir $files[$i])) -ne $originalHashes[$i]) { throw "OriginalsDir does not contain Cura 5.13.0: $($files[$i])" }
}

Write-Host "T1 fresh install"
Reset-Fixture
Check 'exit 0' ((Run @('/install', '/silent')) -eq 0)
$payloadHashes = CuraHashes
Check 'cura files replaced' (-not (Same $payloadHashes $originalHashes))
Check 'engine differs from 4.0.0 payload' ($payloadHashes[0] -ne $legacyHashes[0])
Check 'manifest written' (Test-Path (Join-Path (Data) 'backup\backup.complete'))
Check 'uninstaller copied' (Test-Path (Join-Path (Data) 'Uninstall.exe'))
Check 'registry entry 5.0.0' ((RegKey).DisplayVersion -eq '5.0.0')
Check 'no staging folders left' (@(Get-ChildItem (Join-Path $WorkDir 'ProgramFiles')).Count -eq 1)

Write-Host "T2 reinstall over itself"
Check 'exit 0' ((Run @('/install', '/silent')) -eq 0)
Check 'cura files unchanged' (Same (CuraHashes) $payloadHashes)
Check 'backup still original' ((Sha (Join-Path (Data) "backup\$($files[1])")) -eq $originalHashes[1])

Write-Host "T3 uninstall restores Cura and cleans profile/cache"
Check 'exit 0' ((Run @('/uninstall', '/silent')) -eq 0)
Check 'originals restored byte-exact' (Same (CuraHashes) $originalHashes)
Check 'data folder removed' (-not (Test-Path (Data)))
Check 'registry entry removed' ($null -eq (RegKey))
$cfg = Get-Content (Join-Path $WorkDir 'Roaming\cura\5.13\cura.cfg') -Raw
Check 'cura.cfg visibility cleaned' ($cfg -notmatch 'elephant_foot' -and $cfg -match 'visible_settings = xy_offset;xy_offset_layer_0' -and $cfg -match 'theme = cura-light')
Check 'cura.cfg expanded groups cleaned' ($cfg -match 'categories_expanded = ;alternate_extra_perimeter;fill_outline_gaps')
$user = Get-Content (Join-Path $WorkDir 'Roaming\cura\5.13\user\custom_user.inst.cfg') -Raw
Check 'user profile cleaned, other values kept' ($user -notmatch 'elephant_foot' -and $user -match 'xy_offset_layer_0 = -0.2')
$qc = Get-Content (Join-Path $WorkDir 'Roaming\cura\5.13\quality_changes\my_profile.inst.cfg') -Raw
Check 'custom profile (quality_changes) cleaned' ($qc -notmatch 'elephant_foot' -and $qc -match 'layer_height = 0.2')
Check 'plugins folder untouched' ((Get-Content (Join-Path $WorkDir 'Roaming\cura\5.13\plugins\SomePlugin\plugin.cfg') -Raw) -match 'elephant_foot')
Check 'stale definition cache deleted' (-not (Test-Path (Join-Path $WorkDir 'Local\cura\5.13\cache\definitions\5.13.0\fdmprinter')))
Check 'unrelated cache kept' (Test-Path (Join-Path $WorkDir 'Local\cura\5.13\cache\definitions\5.13.0\other_printer'))

Write-Host "T4 uninstall when nothing is installed"
Check 'exit 0' ((Run @('/uninstall', '/silent')) -eq 0)
Check 'cura untouched' (Same (CuraHashes) $originalHashes)

Write-Host "T5 refuses unknown Cura files"
Reset-Fixture
Add-Content (Join-Path $cura $files[4]) 'user_edit'
$before = CuraHashes
Check 'exit 1' ((Run @('/install', '/silent')) -eq 1)
Check 'nothing changed' (Same (CuraHashes) $before)
Check 'no data folder' (-not (Test-Path (Data)))

Write-Host "T6 Cura changed after install (e.g. repaired) -> file left alone"
Reset-Fixture
Run @('/install', '/silent') | Out-Null
[IO.File]::AppendAllText((Join-Path $cura $files[0]), 'x')
$changed = Sha (Join-Path $cura $files[0])
Check 'exit 0 with warning' (((Run @('/uninstall', '/silent')) -eq 0) -and $script:lastError -match 'WARNING')
Check 'changed file not overwritten' ((Sha (Join-Path $cura $files[0])) -eq $changed)
Check 'other files restored' (Same ((CuraHashes)[1..4]) $originalHashes[1..4])
Check 'data folder removed' (-not (Test-Path (Data)))

Write-Host "T7 Cura removed before the plugin"
Reset-Fixture
Run @('/install', '/silent') | Out-Null
cmd /c "rmdir /s /q `"$cura`"" | Out-Null
Check 'exit 0 with warning' (((Run @('/uninstall', '/silent')) -eq 0) -and $script:lastError -match 'WARNING')
Check 'data folder removed' (-not (Test-Path (Data)))
Check 'registry entry removed' ($null -eq (RegKey))

Write-Host "T8 tampered backup is rejected"
Reset-Fixture
Run @('/install', '/silent') | Out-Null
Add-Content (Join-Path (Data) "backup\$($files[1])") 'tampered'
Check 'exit 1' ((Run @('/uninstall', '/silent')) -eq 1)
Check 'cura still installed (nothing half-restored)' (Same (CuraHashes) $payloadHashes)
Check 'backup kept for recovery' (Test-Path (Join-Path (Data) 'backup\backup.complete'))

Write-Host "T9 rollback when a Cura file is locked during install"
Reset-Fixture
$lock = [IO.File]::Open((Join-Path $cura $files[4]), 'Open', 'Read', 'Read')
try { Check 'exit 1' ((Run @('/install', '/silent')) -eq 1) } finally { $lock.Dispose() }
Check 'originals intact after rollback' (Same (CuraHashes) $originalHashes)
Check 'data folder removed' (-not (Test-Path (Data)))
Check 'no registry entry' ($null -eq (RegKey))

Write-Host "T10 junction inside the profile is refused"
Reset-Fixture
Run @('/install', '/silent') | Out-Null
$outside = Join-Path $WorkDir 'Outside'
New-Item -ItemType Directory $outside | Out-Null
Set-Content "$outside\victim.cfg" -Encoding UTF8 -Value 'elephant_foot_compensation_layers = 2'
cmd /c "mklink /J `"$(Join-Path $WorkDir 'Roaming\cura\5.13\user\link')`" `"$outside`"" | Out-Null
Check 'exit 1' ((Run @('/uninstall', '/silent')) -eq 1)
Check 'file behind junction untouched' ((Get-Content "$outside\victim.cfg" -Raw) -match 'elephant_foot')
Check 'machine part still completed' (Same (CuraHashes) $originalHashes)

if ($LegacyDir) {
    Write-Host "T11 upgrade from the 4.0.0 installer (ProgramData)"
    Reset-Fixture
    $legacyData = Join-Path $WorkDir "ProgramData\$id"
    $manifest = @()
    for ($i = 0; $i -lt $files.Count; $i++) {
        Copy-Item (Join-Path $LegacyDir $files[$i]) (Join-Path $cura $files[$i]) -Force
        $b = Join-Path $legacyData "backup\$($files[$i])"
        New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null
        Copy-Item (Join-Path $OriginalsDir $files[$i]) $b
        $manifest += "$($files[$i])|$($originalHashes[$i].ToUpper())"
    }
    Set-Content (Join-Path $legacyData 'backup\backup.complete') -Value $manifest -Encoding UTF8
    Set-Content (Join-Path $legacyData 'backup\cura-root.txt') -Value $cura -Encoding UTF8
    Set-Content (Join-Path $legacyData 'backup\profile-root.txt') -Value 'x' -Encoding UTF8
    Set-Content (Join-Path $legacyData 'Uninstall.exe') -Value 'old'
    Check 'legacy state recognised' (Same (CuraHashes) $legacyHashes)
    Check 'exit 0' ((Run @('/install', '/silent')) -eq 0)
    Check 'new payload installed' (Same (CuraHashes) $payloadHashes)
    Check 'legacy ProgramData folder removed' (-not (Test-Path $legacyData))
    Check 'backup migrated' ((Sha (Join-Path (Data) "backup\$($files[0])")) -eq $originalHashes[0])
    Check 'uninstall exit 0' ((Run @('/uninstall', '/silent')) -eq 0)
    Check 'originals restored' (Same (CuraHashes) $originalHashes)

    Write-Host "T12 removal of a 4.0.0 installation without upgrading"
    Reset-Fixture
    for ($i = 0; $i -lt $files.Count; $i++) {
        Copy-Item (Join-Path $LegacyDir $files[$i]) (Join-Path $cura $files[$i]) -Force
        $b = Join-Path $legacyData "backup\$($files[$i])"
        New-Item -ItemType Directory -Force (Split-Path $b) | Out-Null
        Copy-Item (Join-Path $OriginalsDir $files[$i]) $b
    }
    Set-Content (Join-Path $legacyData 'backup\backup.complete') -Value $manifest -Encoding UTF8
    New-Item -ItemType Directory (Join-Path $legacyData 'backup\stray-user-folder') | Out-Null
    Check 'exit 0' ((Run @('/uninstall', '/silent')) -eq 0)
    Check 'originals restored' (Same (CuraHashes) $originalHashes)
    Check 'known legacy files deleted' (-not (Test-Path (Join-Path $legacyData 'backup\backup.complete')))
    Check 'unknown content left in place (not deleted blindly)' (Test-Path (Join-Path $legacyData 'backup\stray-user-folder'))
}

Remove-Item 'HKCU:\Software\EFNLTest' -Recurse -Force -ErrorAction SilentlyContinue
cmd /c "rmdir /s /q `"$WorkDir`"" | Out-Null
Write-Host ""
Write-Host "RESULT: $script:passed passed, $script:failures failed"
if ($script:failures -gt 0) { exit 1 }
