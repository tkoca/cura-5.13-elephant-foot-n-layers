// Cura 5.13 - Elephant Foot N Layers installer (Windows x64, .NET Framework 4.x)
//
// Single executable. The interactive process runs with the invoking user's rights
// (asInvoker) and only the part that touches Program Files / HKLM is executed in an
// elevated child process of the same, already verified executable. Per-user cleanup
// (Cura profile and definition cache) therefore always runs as the real user and
// never with administrator rights inside a user-writable folder.
//
// Machine state:
//   %ProgramFiles%\CuraElephantFootNLayers513\          (admin-only ACL)
//       backup\<original Cura files>
//       backup\backup.complete                          (manifest)
//       Uninstall.exe
//   HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\CuraElephantFootNLayers513
//
// Safety rules:
//   * Cura files are only replaced if they are byte-identical to UltiMaker Cura 5.13.0
//     (known SHA-256 list) or to a payload of this project (upgrade).
//   * Restored files must match the known Cura 5.13.0 hashes; the manifest alone is
//     never trusted.
//   * A Cura file that is neither the original nor a payload of this project (for
//     example after Cura was repaired or updated) is left untouched.
//   * Reparse points are rejected on every path that is written or deleted.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("Cura 5.13 - Elephant Foot N Layers Setup")]
[assembly: AssemblyProduct("Cura 5.13 - Elephant Foot N Layers")]
[assembly: AssemblyVersion(Installer.Version + ".0")]
[assembly: AssemblyFileVersion(Installer.Version + ".0")]
[assembly: AssemblyInformationalVersion(Installer.Version)]

internal static class Installer
{
    public const string Version = "5.0.0";
    static readonly bool Turkish = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr";
    static string L(string tr, string en) { return Turkish ? tr : en; }
    static readonly string Title = L("Cura 5.13 - Fil Ayağı N Katman", "Cura 5.13 - Elephant Foot N Layers");
    const string Id = "CuraElephantFootNLayers513";
    const string UninstallKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\" + Id;
    const string CuraDisplayName = "UltiMaker Cura 5.13.0";
    const string Layers = "elephant_foot_compensation_layers";
    const string Taper = "elephant_foot_compensation_taper";

    static readonly string[] Files = {
        "CuraEngine.exe",
        @"share\cura\resources\definitions\fdmprinter.def.json",
        @"share\cura\resources\i18n\tr_TR\fdmprinter.def.json.po",
        @"share\cura\resources\i18n\tr_TR\LC_MESSAGES\fdmprinter.def.json.mo",
        @"share\cura\resources\setting_visibility\expert.cfg"
    };

    // SHA-256 of the files shipped with UltiMaker Cura 5.13.0 for Windows x64 (same order as Files).
    static readonly string[] OriginalHashes = {
        "36e36d4618cd09a0cd8802a565c2cdbe54804eb750ac84303a0c6d6f784afde2",
        "8fbbf8b779e806bd8d0b0e2b8b9be17bdd26a575b7ea1875c20481eb2c7e11ee",
        "ace33455f77ad50ed5d10dbd2f1c2b5732d2c59dcc5e99a9e715ad1bd3f51a85",
        "16dda401341e0805149bdcd674677bf5081413163c6b059255c637187599d3ad",
        "9b646941fa24799eda46ce207f586ab72687d7b02f837264287bb022b720a4ba"
    };

    // SHA-256 of the payload of the earlier 4.0.0 installer (upgrade / removal support).
    static readonly string[] LegacyPayloadHashes = {
        "ba46bf125c4fa75bef081d9f33eb61cd69d9b8e1e47a41d320d8efeec4436cce",
        "04e4f00d856dc595252df7307d5dbce03140444bf7b16bbed64b75080c53f2bb",
        "90a950c7e07be4436052f5c7fc5d6d5f3f8834c0be28c4a4db9f9ae2caf0a55a",
        "1e9b6e085e99f99e6786458a366a61a88525a062144c7502c937a1941cb0d220",
        "d1ab34aab68519fa4017d62c632d28e1373abb4c21a6197e4ead0de5120e91a9"
    };

#if TEST
    // Test build: every location is redirected below EFNL_TEST_ROOT and no elevation is used.
    static readonly string TestRoot = Environment.GetEnvironmentVariable("EFNL_TEST_ROOT");
    static string DataDir { get { return Path.Combine(TestRoot, "ProgramFiles", Id); } }
    static string LegacyDataDir { get { return Path.Combine(TestRoot, "ProgramData", Id); } }
    static string RoamingProfile { get { return Path.Combine(TestRoot, "Roaming", "cura", "5.13"); } }
    static string LocalProfile { get { return Path.Combine(TestRoot, "Local", "cura", "5.13"); } }
    static RegistryKey MachineHive { get { return Registry.CurrentUser.CreateSubKey(@"Software\EFNLTest"); } }
    static bool IsElevated() { return true; }
#else
    static string DataDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Id); } }
    static string LegacyDataDir { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), Id); } }
    static string RoamingProfile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "cura", "5.13"); } }
    static string LocalProfile { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "cura", "5.13"); } }
    static RegistryKey MachineHive { get { return RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64); } }
    static bool IsElevated()
    {
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
#endif
    static string BackupDir { get { return Path.Combine(DataDir, "backup"); } }
    static string UninstallerPath { get { return Path.Combine(DataDir, "Uninstall.exe"); } }

    static bool silent;
    static readonly List<string> warnings = new List<string>();

    // ------------------------------------------------------------------ entry

    [STAThread]
    static int Main(string[] args)
    {
        silent = Has(args, "/silent");
        try
        {
            if (Has(args, "/machine-install")) { MachineInstall(); return ExitWithWarnings(); }
            if (Has(args, "/machine-uninstall")) { MachineUninstall(); return ExitWithWarnings(); }
            if (Has(args, "/uninstall")) { Uninstall(); return ExitWithWarnings(); }
            if (Has(args, "/install") || silent) { Install(); return ExitWithWarnings(); }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm(IsInstalled(), NeedsUpgrade()));
            return 0;
        }
        catch (Exception ex)
        {
            Report(ex.Message, true);
            return 1;
        }
    }

    static bool Has(string[] args, string name)
    {
        foreach (string arg in args) if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static int ExitWithWarnings()
    {
        if (warnings.Count > 0) Report(string.Join(Environment.NewLine + Environment.NewLine, warnings.ToArray()), false);
        return 0;
    }

    static void Report(string message, bool error)
    {
        if (silent) { Console.Error.WriteLine((error ? "ERROR: " : "WARNING: ") + message); return; }
        MessageBox.Show(message, Title, MessageBoxButtons.OK, error ? MessageBoxIcon.Error : MessageBoxIcon.Warning);
    }

    static bool NeedsUpgrade()
    {
        if (File.Exists(Path.Combine(LegacyDataDir, "backup", "backup.complete"))) return true;
        if (!File.Exists(Path.Combine(BackupDir, "backup.complete"))) return false;
        using (RegistryKey hive = MachineHive)
        using (RegistryKey key = hive.OpenSubKey(UninstallKey))
            return key == null || !string.Equals(key.GetValue("DisplayVersion") as string, Version, StringComparison.Ordinal);
    }

    static bool IsInstalled()
    {
        return File.Exists(Path.Combine(BackupDir, "backup.complete")) || File.Exists(Path.Combine(LegacyDataDir, "backup", "backup.complete"));
    }

    // --------------------------------------------------------------------- UI

    sealed class MainForm : Form
    {
        readonly List<Button> buttons = new List<Button>();

        public MainForm(bool installed, bool upgrade)
        {
            Text = Title + " " + Version;
            ClientSize = new Size(520, 175);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.White;
            Font = SystemFonts.MessageBoxFont;
            string text;
            if (upgrade) text = L("Eklentinin önceki bir sürümü kurulu. \"Güncelle\" yeni sürümü kurar; \"Kaldır\" özgün Cura dosyalarını geri yükler ve eklenti ayarlarını Cura kullanıcı profilinizden temizler.", "An earlier version of the add-on is installed. \"Update\" installs this version; \"Remove\" restores the original Cura files and removes the add-on settings from your Cura profile.");
            else if (installed) text = L("Eklenti kurulu. Kaldırma işlemi özgün Cura dosyalarını geri yükler, eklenti ayarlarını Cura kullanıcı profilinizden ve tanım önbelleğinden temizler.", "The add-on is installed. Removing it restores the original Cura files and removes the add-on settings from your Cura profile and definition cache.");
            else text = L("UltiMaker Cura 5.13.0 için ilk N katmanda fil ayağı telafisini kurar. Kurulumdan önce Cura dosyalarının özgün 5.13.0 sürümü olduğu doğrulanır.", "Installs elephant foot compensation for the first N layers in UltiMaker Cura 5.13.0. Before installing, the Cura files are checked to be the original 5.13.0 files.");
            Controls.Add(new Label { Left = 20, Top = 20, Width = 480, Height = 85, Text = text });

            int right = 500;
            Button cancel = AddButton(L("İptal", "Cancel"), ref right, 90);
            cancel.Click += delegate { Close(); };
            CancelButton = cancel;
            if (installed)
            {
                Button remove = AddButton(L("Kaldır", "Remove"), ref right, 120);
                remove.Click += delegate { Run(false); };
                AcceptButton = remove;
            }
            if (!installed || upgrade)
            {
                Button install = AddButton(upgrade ? L("Güncelle", "Update") : L("Kur", "Install"), ref right, 120);
                install.Click += delegate { Run(true); };
                AcceptButton = install;
            }
        }

        Button AddButton(string text, ref int right, int width)
        {
            Button button = new Button { Left = right - width, Top = 120, Width = width, Height = 34, Text = text };
            right -= width + 10;
            Controls.Add(button);
            buttons.Add(button);
            return button;
        }

        void Run(bool install)
        {
            if (!install && MessageBox.Show(L("Eklenti kaldırılsın ve Cura özgün hâline getirilsin mi?", "Remove the add-on and restore the original Cura files?"), Title,
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            foreach (Button button in buttons) button.Enabled = false;
            Cursor = Cursors.WaitCursor;
            try
            {
                if (install) Install(); else Uninstall();
                string done = install
                    ? L("Eklenti kuruldu. Cura'yı yeniden başlatın. Ayarlar: Duvarlar > \"Fil Ayağı Telafi Katman Sayısı\" ve \"Fil Ayağı Kademeli Telafi\" (Uzman görünümü).", "The add-on is installed. Restart Cura. Settings: Walls > \"Elephant Foot Compensation Layer Count\" and \"Elephant Foot Gradual Compensation\" (Expert visibility).")
                    : L("Eklenti kaldırıldı. Cura dosyaları geri yüklendi; profil ayarları ve tanım önbelleği temizlendi.", "The add-on was removed. The Cura files were restored; the profile settings and the definition cache were cleaned.");
                if (warnings.Count > 0) done += Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine, warnings.ToArray());
                MessageBox.Show(done, Title, MessageBoxButtons.OK, warnings.Count > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
                Close();
            }
            catch (OperationCanceledException ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Information);
                foreach (Button button in buttons) button.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, Title, MessageBoxButtons.OK, MessageBoxIcon.Error);
                foreach (Button button in buttons) button.Enabled = true;
            }
            finally { Cursor = Cursors.Default; }
        }
    }

    // ------------------------------------------------------ user-level actions

    static void Install()
    {
        RunMachinePart("/machine-install");
    }

    static void Uninstall()
    {
        RunMachinePart("/machine-uninstall");
        // Per-user cleanup runs in the invoking user's context (not elevated when started normally).
        CleanProfile(RoamingProfile);
        CleanCache(LocalProfile);
    }

    static void RunMachinePart(string argument)
    {
        if (IsElevated())
        {
            if (argument == "/machine-install") MachineInstall(); else MachineUninstall();
            return;
        }
        ProcessStartInfo start = new ProcessStartInfo(Application.ExecutablePath, argument + (silent ? " /silent" : ""))
        {
            UseShellExecute = true,
            Verb = "runas",
            WorkingDirectory = Environment.SystemDirectory
        };
        Process process;
        try { process = Process.Start(start); }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            throw new OperationCanceledException(L("Yönetici onayı verilmedi; hiçbir değişiklik yapılmadı.", "Administrator approval was not given; nothing was changed."));
        }
        using (process)
        {
            process.WaitForExit();
            // The elevated process has already shown its own error or warning message.
            if (process.ExitCode != 0) throw new OperationCanceledException(L("İşlem tamamlanamadı (kod ", "The operation did not complete (code ") + process.ExitCode + ").");
        }
    }

    // ------------------------------------------------------ machine: install

    static void MachineInstall()
    {
        if (!IsElevated()) throw new UnauthorizedAccessException(L("Bu adım yönetici yetkisi gerektirir.", "This step requires administrator rights."));
        string root = FindCura(null);
        RejectReparsePoints(root);
        EnsureCuraStopped(root);

        using (Payload payload = Payload.Open())
        {
            // Determine where the original files come from.
            bool legacy = File.Exists(Path.Combine(LegacyDataDir, "backup", "backup.complete"));
            bool current = File.Exists(Path.Combine(BackupDir, "backup.complete"));
            string originalsFrom;
            if (current)
            {
                // Same or older version of this installer: reinstall / upgrade in place.
                originalsFrom = BackupDir;
            }
            else if (legacy)
            {
                originalsFrom = Path.Combine(LegacyDataDir, "backup");
            }
            else
            {
                originalsFrom = root;
            }
            RejectReparsePoints(originalsFrom);

            // Every Cura file must be the original 5.13.0 file or a payload of this project.
            for (int i = 0; i < Files.Length; i++)
            {
                string present = Hash(Path.Combine(root, Files[i]));
                if (!Is(present, OriginalHashes[i]) && !Is(present, LegacyPayloadHashes[i]) && !Is(present, payload.Hashes[i]))
                    throw new InvalidOperationException(L("Cura dosyası UltiMaker Cura 5.13.0 ile eşleşmiyor; kurulum yapılmadı:", "A Cura file does not match UltiMaker Cura 5.13.0; nothing was installed:") +
                        Environment.NewLine + Files[i] + Environment.NewLine + Environment.NewLine +
                        L("Cura'yı onarın veya yeniden kurun, ardından tekrar deneyin.", "Repair or reinstall Cura, then try again."));
                string original = Path.Combine(originalsFrom, Files[i]);
                if (!File.Exists(original) || !Is(Hash(original), OriginalHashes[i]))
                    throw new InvalidOperationException(L("Özgün Cura dosyası doğrulanamadı; kurulum yapılmadı: ", "An original Cura file could not be verified; nothing was installed: ") + Files[i]);
            }

            // Build the new data folder next to the final location, then move it into place.
            string stage = PrepareDataStage();
            try
            {
                string stageBackup = Path.Combine(stage, "backup");
                StringBuilder manifest = new StringBuilder();
                for (int i = 0; i < Files.Length; i++)
                {
                    string target = Path.Combine(stageBackup, Files[i]);
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(Path.Combine(originalsFrom, Files[i]), target);
                    if (!Is(Hash(target), OriginalHashes[i])) throw new IOException(L("Yedek doğrulanamadı: ", "The backup could not be verified: ") + Files[i]);
                    manifest.Append(Files[i]).Append('|').Append(OriginalHashes[i]).Append('|').Append(payload.Hashes[i]).Append("\r\n");
                }
                File.WriteAllText(Path.Combine(stageBackup, "cura-root.txt"), root, new UTF8Encoding(false));
                File.WriteAllText(Path.Combine(stageBackup, "backup.complete"), manifest.ToString(), new UTF8Encoding(false));
                File.Copy(Application.ExecutablePath, Path.Combine(stage, "Uninstall.exe"));
                ReplaceDataDir(stage);
                stage = null;
            }
            finally
            {
                if (stage != null) DeleteDirectory(stage);
            }

            // Replace the Cura files. On any failure the originals are put back.
            try
            {
                for (int i = 0; i < Files.Length; i++)
                    using (Stream content = payload.Extract(Files[i]))
                        WriteVerified(content, Path.Combine(root, Files[i]), payload.Hashes[i]);
                using (RegistryKey hive = MachineHive)
                using (RegistryKey key = hive.CreateSubKey(UninstallKey))
                {
                    key.SetValue("DisplayName", Title);
                    key.SetValue("DisplayVersion", Version);
                    key.SetValue("Publisher", L("Topluluk projesi (resmi değil)", "Community project (unofficial)"));
                    key.SetValue("InstallLocation", root);
                    key.SetValue("DisplayIcon", Path.Combine(root, "UltiMaker-Cura.exe"));
                    key.SetValue("UninstallString", "\"" + UninstallerPath + "\" /uninstall");
                    key.SetValue("QuietUninstallString", "\"" + UninstallerPath + "\" /uninstall /silent");
                    key.SetValue("EstimatedSize", (int)(new FileInfo(Path.Combine(root, Files[0])).Length / 1024 + 2048), RegistryValueKind.DWord);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch
            {
                try { RestoreCura(root, payload); } catch (Exception inner) { warnings.Add(L("Geri alma sırasında hata: ", "Error while rolling back: ") + inner.Message); throw; }
                DeleteDirectory(DataDir);
                throw;
            }

            // The previous (4.0.0) installation lived in ProgramData; it is now fully migrated.
            if (legacy) RemoveLegacyData();
        }
    }

    static string PrepareDataStage()
    {
        string parent = Path.GetDirectoryName(DataDir);
        RejectReparsePoints(parent);
        string stage = Path.Combine(parent, Id + ".new-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(stage);
        ProtectDirectory(stage);
        return stage;
    }

    static void ReplaceDataDir(string stage)
    {
        if (Directory.Exists(DataDir))
        {
            RejectReparsePoints(DataDir);
            string old = DataDir + ".old-" + Guid.NewGuid().ToString("N");
            Directory.Move(DataDir, old);
            Directory.Move(stage, DataDir);
            DeleteDirectory(old);
        }
        else
        {
            Directory.Move(stage, DataDir);
        }
    }

    // ---------------------------------------------------- machine: uninstall

    static void MachineUninstall()
    {
        if (!IsElevated()) throw new UnauthorizedAccessException(L("Bu adım yönetici yetkisi gerektirir.", "This step requires administrator rights."));
        bool current = File.Exists(Path.Combine(BackupDir, "backup.complete"));
        bool legacy = File.Exists(Path.Combine(LegacyDataDir, "backup", "backup.complete"));
        if (!current && !legacy)
        {
            // Nothing to restore; make sure no stale uninstall entry is left behind.
            DeleteUninstallKey();
            return;
        }
        string backup = current ? BackupDir : Path.Combine(LegacyDataDir, "backup");
        RejectReparsePoints(backup);

        string root = null;
        try { root = FindCura(Path.Combine(backup, "cura-root.txt")); }
        catch (DirectoryNotFoundException)
        {
            warnings.Add(L("UltiMaker Cura 5.13.0 bulunamadı; Cura dosyaları geri yüklenmedi (Cura kaldırılmış olabilir).", "UltiMaker Cura 5.13.0 was not found; no Cura files were restored (Cura may have been uninstalled)."));
        }

        using (Payload payload = Payload.Open())
        {
            if (root != null)
            {
                RejectReparsePoints(root);
                EnsureCuraStopped(root);
                RestoreCura(root, payload, backup);
            }
        }
        DeleteUninstallKey();
        if (current) DeleteDataDirSelfAware();
        if (legacy) RemoveLegacyData();
    }

    static void RestoreCura(string root, Payload payload)
    {
        RestoreCura(root, payload, BackupDir);
    }

    static void RestoreCura(string root, Payload payload, string backup)
    {
        // Validate everything before the first write.
        bool[] restore = new bool[Files.Length];
        for (int i = 0; i < Files.Length; i++)
        {
            string destination = Path.Combine(root, Files[i]);
            string source = Path.Combine(backup, Files[i]);
            if (!File.Exists(destination))
            {
                warnings.Add(L("Cura dosyası bulunamadı, atlandı: ", "Cura file not found, skipped: ") + Files[i]);
                continue;
            }
            string present = Hash(destination);
            if (Is(present, OriginalHashes[i])) continue;
            if (!Is(present, payload.Hashes[i]) && !Is(present, LegacyPayloadHashes[i]))
            {
                warnings.Add(L("Cura dosyası kurulumdan sonra değişmiş (Cura güncellenmiş veya onarılmış olabilir); üzerine yazılmadı: ", "This Cura file changed after installation (Cura may have been updated or repaired); it was not overwritten: ") + Files[i]);
                continue;
            }
            if (!File.Exists(source) || !Is(Hash(source), OriginalHashes[i]))
                throw new InvalidDataException(L("Yedekteki özgün dosya doğrulanamadı; hiçbir dosya değiştirilmedi ve yedek korundu: ", "An original file in the backup could not be verified; nothing was changed and the backup was kept: ") + Files[i]);
            restore[i] = true;
        }
        for (int i = 0; i < Files.Length; i++)
            if (restore[i])
                using (FileStream input = File.OpenRead(Path.Combine(backup, Files[i])))
                    WriteVerified(input, Path.Combine(root, Files[i]), OriginalHashes[i]);
    }

    static void DeleteUninstallKey()
    {
        using (RegistryKey hive = MachineHive)
            hive.DeleteSubKeyTree(UninstallKey, false);
    }

    static void DeleteDataDirSelfAware()
    {
        string self = Path.GetFullPath(Application.ExecutablePath);
        bool running = self.StartsWith(Path.GetFullPath(DataDir) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        if (!running)
        {
            // Rename first so that a partially deleted folder is never mistaken for a valid backup.
            string doomed = DataDir + ".delete-" + Guid.NewGuid().ToString("N");
            Directory.Move(DataDir, doomed);
            DeleteDirectory(doomed);
            return;
        }
        // Our own image is inside the folder (started from "Apps & features"), so the folder cannot be
        // renamed. The manifest goes first: without it the folder is no longer treated as an installation.
        File.Delete(Path.Combine(BackupDir, "backup.complete"));
        DeleteDirectory(BackupDir);
        ScheduleDelete(DataDir);
    }

    static void ScheduleDelete(string directory)
    {
        // Retries for up to ~10 minutes until every process started from the folder has exited.
        string cmd = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        string ping = Path.Combine(Environment.SystemDirectory, "PING.EXE");
        string script = "for /l %i in (1,1,300) do @(if not exist \"" + directory + "\" exit /b 0) & (rmdir /s /q \"" + directory +
            "\" 2>nul) & (\"" + ping + "\" -n 3 127.0.0.1 >nul)";
        ProcessStartInfo start = new ProcessStartInfo(cmd, "/d /q /c \"" + script + "\"")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Environment.SystemDirectory
        };
        Process.Start(start).Dispose();
    }

    static void RemoveLegacyData()
    {
        // The 4.0.0 installer kept its data in ProgramData, where standard users may create entries.
        // Nothing there is executed or trusted (restored content is verified against the known Cura
        // hashes after copying), and only the files that installer created are deleted, by exact name.
        string legacy = LegacyDataDir;
        string backup = Path.Combine(legacy, "backup");
        List<string> known = new List<string>();
        foreach (string file in Files) known.Add(Path.Combine(backup, file));
        foreach (string name in new[] { "backup.complete", "cura-root.txt", "profile-root.txt", "local-profile-root.txt" }) known.Add(Path.Combine(backup, name));
        known.Add(Path.Combine(legacy, "Uninstall.exe"));
        foreach (string file in known)
        {
            try
            {
                RejectReparsePoints(file);
                if (File.Exists(file)) { File.SetAttributes(file, FileAttributes.Normal); File.Delete(file); }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                warnings.Add(L("Eski sürümden kalan dosya silinemedi: ", "A file left by the earlier version could not be deleted: ") + file);
            }
        }
        string[] folders = {
            @"backup\share\cura\resources\i18n\tr_TR\LC_MESSAGES", @"backup\share\cura\resources\i18n\tr_TR", @"backup\share\cura\resources\i18n",
            @"backup\share\cura\resources\definitions", @"backup\share\cura\resources\setting_visibility", @"backup\share\cura\resources",
            @"backup\share\cura", @"backup\share", "backup", ""
        };
        foreach (string folder in folders)
        {
            string path = folder.Length == 0 ? legacy : Path.Combine(legacy, folder);
            try
            {
                if (!Directory.Exists(path)) continue;
                RejectReparsePoints(path);
                Directory.Delete(path, false);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                warnings.Add(L("Eski sürümün klasörü boş olmadığı için bırakıldı: ", "The folder of the earlier version is not empty and was left in place: ") + path);
                return;
            }
        }
    }

    // ------------------------------------------------------------ Cura lookup

    static string FindCura(string savedRootFile)
    {
        List<string> candidates = new List<string>();
        if (savedRootFile != null && File.Exists(savedRootFile)) candidates.Add(File.ReadAllText(savedRootFile).Trim());
#if TEST
        candidates.Add(Path.Combine(TestRoot, "Cura"));
#else
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), CuraDisplayName));
        using (RegistryKey hive = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64))
        using (RegistryKey parent = hive.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
        {
            if (parent != null)
                foreach (string name in parent.GetSubKeyNames())
                    using (RegistryKey key = parent.OpenSubKey(name))
                    {
                        if (key == null || !string.Equals(key.GetValue("DisplayName") as string, CuraDisplayName, StringComparison.OrdinalIgnoreCase)) continue;
                        string location = key.GetValue("InstallLocation") as string;
                        if (!string.IsNullOrWhiteSpace(location)) candidates.Add(location.Trim().Trim('"'));
                        string command = key.GetValue("UninstallString") as string;
                        if (!string.IsNullOrWhiteSpace(command))
                        {
                            int index = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                            if (index > 0) candidates.Add(Path.GetDirectoryName(command.Substring(0, index + 4).Trim().Trim('"')));
                        }
                    }
        }
#endif
        foreach (string candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            string root;
            try { root = Path.GetFullPath(candidate).TrimEnd(Path.DirectorySeparatorChar); }
            catch (ArgumentException) { continue; }
            catch (NotSupportedException) { continue; }
            if (File.Exists(Path.Combine(root, Files[0])) && File.Exists(Path.Combine(root, Files[1]))) return root;
        }
        throw new DirectoryNotFoundException(CuraDisplayName + L(" bulunamadı.", " was not found."));
    }

    static void EnsureCuraStopped(string root)
    {
        string prefix = root + Path.DirectorySeparatorChar;
        foreach (Process process in Process.GetProcesses())
        {
            using (process)
            {
                string path = null;
                try { path = process.MainModule.FileName; }
                catch (Win32Exception) { }
                catch (InvalidOperationException) { }
                if (path != null && path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    throw new IOException(L("Cura çalışıyor (", "Cura is running (") + Path.GetFileName(path) + L("). Programı kapatıp yeniden deneyin.", "). Close it and try again."));
            }
        }
    }

    // ------------------------------------------------------ per-user cleanup

    static void CleanProfile(string profile)
    {
        string full = Path.GetFullPath(profile).TrimEnd(Path.DirectorySeparatorChar);
        if (!Directory.Exists(full)) return;
        RejectReparsePoints(full);
        foreach (string file in EnumerateFiles(full, "*.cfg", "plugins"))
        {
            bool visibility = string.Equals(Path.GetFileName(file), "cura.cfg", StringComparison.OrdinalIgnoreCase)
                && string.Equals(Path.GetDirectoryName(file), full, StringComparison.OrdinalIgnoreCase);
            CleanFile(file, visibility);
        }
    }

    static void CleanCache(string localProfile)
    {
        string cache = Path.Combine(Path.GetFullPath(localProfile).TrimEnd(Path.DirectorySeparatorChar), "cache");
        if (!Directory.Exists(cache)) return;
        RejectReparsePoints(cache);
        byte[] needle = Encoding.ASCII.GetBytes(Layers);
        foreach (string file in EnumerateFiles(cache, "*", null))
            if (Contains(File.ReadAllBytes(file), needle)) File.Delete(file);
    }

    static IEnumerable<string> EnumerateFiles(string directory, string pattern, string skipDirectory)
    {
        RejectReparsePoints(directory);
        foreach (string file in Directory.GetFiles(directory, pattern, SearchOption.TopDirectoryOnly))
        {
            if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new IOException(L("Reparse point engellendi: ", "Stopped for safety: reparse point: ") + file);
            yield return file;
        }
        foreach (string child in Directory.GetDirectories(directory))
        {
            if (skipDirectory != null && string.Equals(Path.GetFileName(child), skipDirectory, StringComparison.OrdinalIgnoreCase)) continue;
            foreach (string file in EnumerateFiles(child, pattern, skipDirectory)) yield return file;
        }
    }

    static void CleanFile(string path, bool visibility)
    {
        string old = File.ReadAllText(path, Encoding.UTF8);
        if (old.IndexOf(Layers, StringComparison.Ordinal) < 0 && old.IndexOf(Taper, StringComparison.Ordinal) < 0) return;
        // Setting values ("elephant_foot_compensation_layers = 4") in profiles and instance containers.
        string updated = Regex.Replace(old, @"(?m)^[ \t]*elephant_foot_compensation_(?:layers|taper)[ \t]*=[^\r\n]*(?:\r?\n|\z)", "");
        if (visibility)
        {
            // Setting-key lists in cura.cfg: visible settings and expanded setting groups.
            updated = Regex.Replace(updated, @"(?m)^((?:custom_)?visible_settings[ \t]*=[ \t]*|categories_expanded[ \t]*=[ \t]*)([^\r\n]*)", match =>
            {
                List<string> kept = new List<string>();
                foreach (string part in match.Groups[2].Value.Split(';'))
                    if (part.Trim() != Layers && part.Trim() != Taper) kept.Add(part);
                return match.Groups[1].Value + string.Join(";", kept.ToArray());
            });
        }
        if (updated != old)
        {
            string temp = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (FileStream output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (StreamWriter writer = new StreamWriter(output, new UTF8Encoding(false))) writer.Write(updated);
                RejectReparsePoints(path);
                File.Replace(temp, path, null);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        if (updated.IndexOf(Layers, StringComparison.Ordinal) >= 0 || updated.IndexOf(Taper, StringComparison.Ordinal) >= 0)
            warnings.Add(L("Eklenti ayarı bu dosyada tanınmayan bir biçimde kaldı; Cura bunu yok sayar, isterseniz elle silebilirsiniz: ", "An add-on setting remains in this file in an unrecognised form; Cura ignores it and you may delete it by hand: ") + path);
    }

    // ----------------------------------------------------------- file helpers

    static void WriteVerified(Stream content, string destination, string expectedHash)
    {
        RejectReparsePoints(destination);
        string temp = destination + ".efnl-" + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (FileStream output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) content.CopyTo(output);
            if (!Is(Hash(temp), expectedHash)) throw new IOException(L("Dosya doğrulanamadı: ", "File could not be verified: ") + Path.GetFileName(destination));
            File.Replace(temp, destination, null);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        if (!Is(Hash(destination), expectedHash)) throw new IOException(L("Dosya doğrulanamadı: ", "File could not be verified: ") + Path.GetFileName(destination));
    }

    static void ProtectDirectory(string directory)
    {
#if !TEST
        // Administrators and SYSTEM: full control; everyone else read-only. Inheritance from the parent is removed.
        DirectorySecurity security = new DirectorySecurity();
        security.SetAccessRuleProtection(true, false);
        InheritanceFlags inherit = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null), FileSystemRights.FullControl, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inherit, PropagationFlags.None, AccessControlType.Allow));
        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null));
        Directory.SetAccessControl(directory, security);
#endif
    }

    static void DeleteDirectory(string directory)
    {
        if (!Directory.Exists(directory)) return;
        RejectReparsePoints(directory);
        foreach (string child in Directory.GetDirectories(directory))
        {
            if ((File.GetAttributes(child) & FileAttributes.ReparsePoint) != 0) Directory.Delete(child, false); // removes the link only
            else DeleteDirectory(child);
        }
        foreach (string file in Directory.GetFiles(directory))
        {
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }
        Directory.Delete(directory, false);
    }

    static void RejectReparsePoints(string path)
    {
        string full = Path.GetFullPath(path);
        string current = Path.GetPathRoot(full);
        foreach (string part in full.Substring(current.Length).Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new IOException(L("Güvenlik nedeniyle durduruldu: yol bir bağlantı (reparse point) içeriyor: ", "Stopped for safety: the path contains a link (reparse point): ") + current);
        }
    }

    static bool Contains(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            int j = 0;
            while (j < needle.Length && haystack[i + j] == needle[j]) j++;
            if (j == needle.Length) return true;
        }
        return false;
    }

    static bool Is(string left, string right)
    {
        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    static string Hash(string path)
    {
        using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)) return Hash(stream);
    }

    static string Hash(Stream stream)
    {
        using (SHA256 sha = SHA256.Create())
        {
            StringBuilder result = new StringBuilder(64);
            foreach (byte value in sha.ComputeHash(stream)) result.Append(value.ToString("x2"));
            return result.ToString();
        }
    }

    // ----------------------------------------------------------------- payload

    sealed class Payload : IDisposable
    {
        readonly Stream resource;
        readonly ZipArchive zip;
        public readonly string[] Hashes = new string[Files.Length];

        Payload(Stream resource)
        {
            this.resource = resource;
            zip = new ZipArchive(resource, ZipArchiveMode.Read);
            HashSet<string> expected = new HashSet<string>(StringComparer.Ordinal);
            foreach (string file in Files) expected.Add(file.Replace('\\', '/'));
            foreach (ZipArchiveEntry entry in zip.Entries)
                if (!expected.Contains(entry.FullName)) throw new InvalidDataException(L("Kurulum paketi beklenmeyen dosya içeriyor: ", "The setup package contains an unexpected file: ") + entry.FullName);
            for (int i = 0; i < Files.Length; i++)
                using (Stream input = Extract(Files[i])) Hashes[i] = Hash(input);
        }

        public static Payload Open()
        {
            Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.zip");
            if (stream == null) throw new InvalidDataException(L("Kurulum dosyası bozuk: payload.zip yok.", "The setup file is damaged: payload.zip is missing."));
            return new Payload(stream);
        }

        public Stream Extract(string relative)
        {
            ZipArchiveEntry entry = zip.GetEntry(relative.Replace('\\', '/'));
            if (entry == null) throw new InvalidDataException(L("Kurulum paketinde dosya eksik: ", "A file is missing from the setup package: ") + relative);
            return entry.Open();
        }

        public void Dispose()
        {
            zip.Dispose();
            resource.Dispose();
        }
    }
}
