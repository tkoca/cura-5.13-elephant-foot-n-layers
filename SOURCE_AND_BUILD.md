# Source and Build Guide

[English](SOURCE_AND_BUILD.md) · [Türkçe](SOURCE_AND_BUILD_tr.md)

## Repository layout

| Path | Purpose |
| --- | --- |
| `patches/CuraEngine-5.13.0.patch` | CuraEngine change (`src/slicer.cpp`): which horizontal expansion each of the first N layers gets. |
| `patches/Cura-5.13.0.patch` | Cura resources: the two setting definitions, Turkish translations, Expert visibility. |
| `installer/Installer.cs`, `installer/app.manifest` | Single-file Windows installer / uninstaller (.NET Framework 4.x, x64, Turkish/English UI). |
| `scripts/build-curaengine.ps1` | Builds the patched CuraEngine 5.13.0 from the upstream source archive. |
| `scripts/build-installer.ps1` | Builds the payload and the setup executable, and refuses binaries that contain the builder's user or computer name. |
| `scripts/make_payload.py` | Takes three resource files from the Cura 5.13.0 source archive, checks them, applies the Cura patch, compiles the `.mo` and writes a deterministic `payload.zip`. |
| `scripts/apply_patch.py`, `scripts/po2mo.py` | Small helpers without dependencies: strict patch applier, `.po` → `.mo` compiler. |
| `tests/installer-tests.ps1` | Install, uninstall, update and failure scenarios with the test build of the installer. |
| `tests/engine/slice-tests.ps1` (+ `analyze_gcode.py`, `prepare_definitions.py`, `box20.stl`) | Slices a 20 mm test box with the official and the patched CuraEngine and checks the G-code. |

## The CuraEngine change

In `Slicer::makePolygons`, upstream CuraEngine uses `xy_offset_layer_0` for the first printed layer and `xy_offset` for all other layers. The patch keeps the first-layer rule unchanged. For the following layers of the compensation range (zero-based printed layer index `i = 1 … N-1`) it uses:

- gradual compensation off: `xy_offset_layer_0`
- gradual compensation on: `xy_offset_layer_0 + (xy_offset - xy_offset_layer_0) × i / N`, rounded to the nearest micrometre

With `N = 1` the result is identical to upstream; this is checked by comparing G-code with the official engine. The example table in the README counts layers from 1.

## Requirements

- Windows 10/11 x64.
- Visual Studio 2022 Build Tools with MSVC v143, a Windows 10/11 SDK and "C++ CMake tools for Windows".
- Python 3.12 with Conan 2 (`pip install conan`).
- The UltiMaker Conan configuration, installed into the Conan home used for the build (see below):
  `conan config install https://github.com/Ultimaker/conan-config.git`
  This provides the `cura.jinja` profile and the UltiMaker package remote.
- The source archives of the `5.13.0` tags:
  - `https://github.com/Ultimaker/CuraEngine/archive/refs/tags/5.13.0.zip`
  - `https://github.com/Ultimaker/Cura/archive/refs/tags/5.13.0.zip`

## Build

Use short folders **outside your user profile** for the Conan home and the work folder. The dependency builds (gRPC, protobuf, abseil, …) compile their source file paths into the executable, so a path like `C:\Users\<name>\…` would end up in `CuraEngine.exe`. `build-curaengine.ps1` refuses such paths and also moves `TEMP` into the Conan home. `build-installer.ps1` checks the final files again.

```powershell
$env:CONAN_HOME = 'C:\conan-efnl'
conan config install https://github.com/Ultimaker/conan-config.git

# 1. CuraEngine. The first run builds all Conan dependencies and can take a few hours.
.\scripts\build-curaengine.ps1 -SourceZip CuraEngine-5.13.0.zip -WorkDir C:\efb -ConanHome C:\conan-efnl `
    -VsInstallPath "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools"

# 2. Setup (+ the TEST build used by the tests, in dist\obj)
.\scripts\build-installer.ps1 -CuraSourceZip Cura-5.13.0.zip `
    -CuraEngine C:\efb\CuraEngine-5.13.0\build\Release\CuraEngine.exe `
    -VsInstallPath "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools" -TestBuild
```

CuraEngine is built with the default options of its `conanfile.py`, like the official Cura build: Arcus (connection to the Cura front end) and engine plugin support (gRPC) are enabled. Sentry crash reporting, which only the official UltiMaker build uses, stays disabled.

`build-curaengine.ps1` sets `VSLANG=1033`. With a Visual Studio in another language (for example Turkish) the translated `/showIncludes` prefix breaks resource-compiler steps in dependency builds (`RC1107`).

## Known Cura 5.13.0 files

SHA-256 of the files of UltiMaker Cura 5.13.0 for Windows x64 that the add-on replaces:

| File | SHA-256 |
| --- | --- |
| `CuraEngine.exe` | `36e36d4618cd09a0cd8802a565c2cdbe54804eb750ac84303a0c6d6f784afde2` |
| `definitions/fdmprinter.def.json` | `8fbbf8b779e806bd8d0b0e2b8b9be17bdd26a575b7ea1875c20481eb2c7e11ee` |
| `i18n/tr_TR/fdmprinter.def.json.po` | `ace33455f77ad50ed5d10dbd2f1c2b5732d2c59dcc5e99a9e715ad1bd3f51a85` |
| `i18n/tr_TR/LC_MESSAGES/fdmprinter.def.json.mo` | `16dda401341e0805149bdcd674677bf5081413163c6b059255c637187599d3ad` |
| `setting_visibility/expert.cfg` | `9b646941fa24799eda46ce207f586ab72687d7b02f837264287bb022b720a4ba` |

- The installer changes or restores a file only if it matches this list (or the add-on's own version of the file).
- `make_payload.py` checks the three text files (`fdmprinter.def.json`, the `.po`, `expert.cfg`) in the Cura source archive against this list. The `.mo` is compiled from the patched `.po`, and `CuraEngine.exe` is your own build.

## Reproducibility

- `payload.zip` is deterministic (fixed timestamps and order). The resource files and the `.mo` are byte-identical for everyone.
- The C# compiler runs with `/deterministic` and `/pathmap`, without a PDB, so the same `payload.zip` and the same compiler version always give the same setup executable.
- `CuraEngine.exe` is not bit-for-bit reproducible (MSVC link time stamp, build date, toolchain version). Its SHA-256 is published with each release. Anyone can rebuild it from the same sources and compare its behaviour with `tests/engine/slice-tests.ps1`.

## Tests

### Installer

```powershell
.\tests\installer-tests.ps1 -TestExe dist\obj\Setup.Test.exe -OriginalsDir <folder> [-LegacyDir <folder>]
```

- `-OriginalsDir`: a folder with the five Cura 5.13.0 files laid out as in the Cura installation folder (`CuraEngine.exe`, `share\cura\resources\…`).
- `-LegacyDir` (optional): the same layout with the files of the earlier 4.0.0 test version. It enables the update and removal tests for that version.
- The test build (`/define:TEST`) redirects every folder and the uninstall registry key into a temporary folder and `HKCU\Software\EFNLTest`, and does not ask for administrator rights. Nothing on the real system is changed.
- Scenarios: fresh install, reinstall, uninstall with profile/cache cleanup, uninstall when nothing is installed, unknown Cura files, a Cura file changed after installation, Cura removed first, damaged backup, rollback when a file is locked, junction inside the profile, update from 4.0.0 and removal of 4.0.0.

### Slicing

```powershell
.\tests\engine\slice-tests.ps1 -OfficialEngine <CuraEngine.exe of Cura 5.13.0> -PatchedEngine <your CuraEngine.exe> `
    -PrinterDefinition <patched fdmprinter.def.json from payload.zip> -ExtruderDefinition <fdmextruder.def.json of Cura 5.13.0> `
    [-CuraDir "C:\Program Files\UltiMaker Cura 5.13.0"] [-Python python]
```

- Both engines are copied next to the runtime DLLs of an installed Cura 5.13.0 (`-CuraDir`) and run from there.
- Checks:
  - With layer count 1 the G-code of both engines is identical, both with default settings and with an initial layer expansion of −0.2 mm.
  - With N = 4, gradual compensation off and on, the outer wall of each layer is inset by the expected amount.
  - With N = 2, gradual compensation on and a Horizontal Expansion of +0.1 mm, the steps lead toward +0.1 mm.

## Upstream

- Cura 5.13.0: https://github.com/Ultimaker/Cura/tree/5.13.0 (LGPL-3.0-or-later)
- CuraEngine 5.13.0: https://github.com/Ultimaker/CuraEngine/tree/5.13.0 (AGPL-3.0-or-later)

## Linux

The Linux version uses the same patches and resource files; its source and build are in [cura-5.13-elephant-foot-n-layers-linux](https://github.com/tkoca/cura-5.13-elephant-foot-n-layers-linux).
