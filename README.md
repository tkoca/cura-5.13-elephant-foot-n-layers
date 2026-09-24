# Cura 5.13.0 — Elephant Foot Compensation for N Layers

[English](README.md) · [Türkçe](README_tr.md)

An unofficial add-on for **UltiMaker Cura 5.13.0 on Windows x64**. It applies *Initial Layer Horizontal Expansion* to the first **N** printed layers instead of only the first one, and can optionally step the value back to the normal *Horizontal Expansion* over those layers.

> This is a community project. It is not made, endorsed or supported by UltiMaker.

## Settings

Two settings are added directly below *Initial Layer Horizontal Expansion* (category *Walls*). They are part of the *Expert* visibility preset; you can also find them with the settings search.

| Setting | Default | Effect |
| --- | --- | --- |
| Elephant Foot Compensation Layer Count | `1` | How many of the first printed layers use *Initial Layer Horizontal Expansion*. `1` is exactly the standard Cura behaviour. Cura shows a warning above 20. |
| Elephant Foot Gradual Compensation | off | Steps the value from *Initial Layer Horizontal Expansion* toward *Horizontal Expansion* over those layers. Only shown when the layer count is greater than 1. |

Example: *Initial Layer Horizontal Expansion* `-0.20 mm`, *Horizontal Expansion* `0.00 mm`, layer count `4`:

| Printed layer | Gradual off | Gradual on |
| --- | --- | --- |
| 1 | -0.20 | -0.20 |
| 2 | -0.20 | -0.15 |
| 3 | -0.20 | -0.10 |
| 4 | -0.20 | -0.05 |
| 5 and above | 0.00 | 0.00 |

All N layers are compensated; the layer after them uses the normal value. If *Horizontal Expansion* is not zero, the steps lead toward that value instead of 0.

In the Turkish interface the settings are called *Fil Ayağı Telafi Katman Sayısı* and *Fil Ayağı Kademeli Telafi*.

## Install

1. Download `Cura-5.13-Elephant-Foot-N-Layers-Setup.exe` from [Releases](../../releases). Check that its SHA-256 matches the value in the release notes (PowerShell prints it in capital letters; upper/lower case does not matter):
   `Get-FileHash .\Cura-5.13-Elephant-Foot-N-Layers-Setup.exe -Algorithm SHA256`
2. Close Cura.
3. Run the setup and click **Install**. Windows asks for administrator approval because files in `C:\Program Files\UltiMaker Cura 5.13.0` are replaced.
4. Start Cura.

The setup is shown in Turkish when the Windows display language is Turkish, and in English otherwise. It is not code-signed, so Windows SmartScreen may warn about an unknown publisher (*More info → Run anyway*). Only continue if the SHA-256 matches.

Before anything is changed, the setup checks that the five affected Cura files are exactly the files of UltiMaker Cura 5.13.0 (or this add-on's own files, when updating). If not, it stops without changing anything.

## Uninstall

*Settings → Apps → Installed apps → Cura 5.13 - Elephant Foot N Layers → Uninstall*, or run the setup again and click **Remove**.

- The original Cura files are restored from the backup. Each restored file is checked against the known Cura 5.13.0 file.
- A Cura file that changed after installation (for example because Cura was repaired or updated) is **not** overwritten; the setup tells you which file it skipped.
- The two settings are removed from your Cura profile (`%APPDATA%\cura\5.13`, including your saved custom profiles), and outdated setting-cache files are deleted (`%LOCALAPPDATA%\cura\5.13\cache`). When you uninstall from *Installed apps* or the setup window, this part runs as your own Windows user, not as administrator.
- The add-on can also be removed after Cura itself was uninstalled.

Remove the add-on before upgrading Cura to another version.

## Updating from the earlier test version (4.0.0)

Only relevant if you installed the earlier 4.0.0 test version, which kept its backup in `C:\ProgramData`. Run the new setup and click **Update**. The backup is moved to `C:\Program Files\CuraElephantFootNLayers513` and the old folder is removed.

## What is changed on your computer

| Location | Content |
| --- | --- |
| `C:\Program Files\UltiMaker Cura 5.13.0\CuraEngine.exe` | Patched CuraEngine 5.13.0 |
| `…\share\cura\resources\definitions\fdmprinter.def.json` | The two new settings |
| `…\share\cura\resources\i18n\tr_TR\fdmprinter.def.json.po` and `LC_MESSAGES\fdmprinter.def.json.mo` | Turkish names and descriptions |
| `…\share\cura\resources\setting_visibility\expert.cfg` | Settings listed in the Expert preset |
| `C:\Program Files\CuraElephantFootNLayers513\` | Backup of the original files and `Uninstall.exe` (only administrators can change it) |
| Registry: `HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CuraElephantFootNLayers513` | Entry in *Installed apps* |

### Command line (for administrators)

```text
Cura-5.13-Elephant-Foot-N-Layers-Setup.exe /install /silent
"C:\Program Files\CuraElephantFootNLayers513\Uninstall.exe" /uninstall /silent
```

Exit code `0` means success; `1` means nothing or not everything was done. Run the commands from an administrator command prompt: then there is no UAC dialog and warnings (such as a skipped file) are written to standard error. The profile cleanup applies to the account that runs the command.

## Source and build

Everything needed to rebuild the setup is in this repository: the patches, the installer source, the build scripts and the tests. See [SOURCE_AND_BUILD.md](SOURCE_AND_BUILD.md).

## License

GNU Affero General Public License v3.0 or later ([LICENSE](LICENSE)), the license of CuraEngine. The Cura resource files are © UltiMaker, LGPL-3.0-or-later. The source code for the `CuraEngine.exe` included in the setup is UltiMaker CuraEngine 5.13.0 plus [`patches/CuraEngine-5.13.0.patch`](patches/CuraEngine-5.13.0.patch), built with [`scripts/build-curaengine.ps1`](scripts/build-curaengine.ps1).

Use at your own risk; try it on a small calibration print first.
