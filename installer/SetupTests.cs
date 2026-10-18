using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;
using Spacio;

namespace SpacioSetup
{
    internal static class SetupTests
    {
        private static bool AppRunning()
        {
            var processes = Process.GetProcessesByName("Spacio");
            try { return processes.Length > 0; }
            finally { foreach (var process in processes) process.Dispose(); }
        }
        private static Dictionary<string, string> UserDataHashes()
        {
            string data = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spacio");
            var paths = new List<string>(); string scans = Path.Combine(data, "Scans");
            if (Directory.Exists(scans)) paths.AddRange(Directory.GetFiles(scans));
            if (File.Exists(Path.Combine(data, "language.txt"))) paths.Add(Path.Combine(data, "language.txt"));
            return paths.ToDictionary(p => p, SetupEngine.Hash);
        }
        private static void AwaitFixtureFiles(InstallContext context)
        {
            // Generated executables can still have transient readers after replacement
            // or shell/UI inspection. Wait only between fixture operations; intentional
            // locked-file rejection tests below still exercise immediate refusal.
            var watch = Stopwatch.StartNew();
            while (true)
            {
                try
                {
                    foreach (string name in SetupEngine.OwnedNames)
                    {
                        string path = Path.Combine(context.Directory, name);
                        if (File.Exists(path)) using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    }
                    return;
                }
                catch (IOException) { if (watch.ElapsedMilliseconds >= 15000) throw; Thread.Sleep(100); }
            }
        }
        internal static int Run(string report, string probe, bool publicUpgrade)
        {
            var log = new List<string>();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception("FAIL: " + label); log.Add("PASS: " + label); File.WriteAllLines(report, log); };
            Action<Action, string> reject = (action, label) => { bool failed = false; try { action(); } catch (IOException) { failed = true; } check(failed, label); };
            string workspace = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(report))).FullName;
            string area = Path.Combine(workspace, ".test-data", "setup-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(area);
            var context = new InstallContext { Directory = Path.Combine(area, "نصب آزمایشی", "Spacio"), DesktopLink = Path.Combine(area, "Desktop", "Spacio.lnk"), MenuLink = Path.Combine(area, "Start Menu", "Spacio.lnk"), RegistryPath = @"Software\SpacioSetupTests\" + Guid.NewGuid().ToString("N") };
            try
            {
                // Live copies legitimately rewrite their real cache. Do not lock, stop,
                // or assert immutability of that concurrent user's data during this test.
                var userData = AppRunning() ? null : UserDataHashes();
                string personal = Path.Combine(area, "personal.txt"); File.WriteAllText(personal, "keep this user file");
                var defaults = InstallContext.CurrentUser();
                check(defaults.Directory == Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Spacio"), "Default install is per-user and independent of scan/preference storage");
                SetupWindow.RunOnSta(() => SetupEngine.Install(context, true)).GetAwaiter().GetResult();
                check(AssemblyName.GetAssemblyName(context.Executable).Version == SetupEngine.Version && SetupEngine.OwnedNames.All(n => File.Exists(Path.Combine(context.Directory, n))), "Setup installs the verified executable, config and uninstaller");
                check(SetupEngine.Same(SetupEngine.ShortcutTarget(context.DesktopLink), context.Executable) && SetupEngine.Same(SetupEngine.ShortcutTarget(context.MenuLink), context.Executable), "Desktop and Start Menu shortcuts point to the installed executable");
                using (var key = Registry.CurrentUser.OpenSubKey(context.RegistryPath))
                    check(key != null && Convert.ToString(key.GetValue("DisplayVersion")) == SetupEngine.Version.ToString(3) && Convert.ToString(key.GetValue("UninstallString")).Contains("--uninstall"), "Windows Installed apps registration contains version and uninstaller");
                string executableHash = SetupEngine.Hash(context.Executable), linkHash = SetupEngine.Hash(context.DesktopLink), installerHash = SetupEngine.Hash(Path.Combine(context.Directory, "Spacio.Setup.exe"));
                AwaitFixtureFiles(context);
                SetupEngine.Install(context, true);
                check(SetupEngine.Hash(context.Executable) == executableHash && Directory.GetFiles(Path.GetDirectoryName(context.DesktopLink)).Length == 1, "Reinstall keeps one installation and one desktop shortcut");
                linkHash = SetupEngine.Hash(context.DesktopLink);
                reject(() => SetupEngine.Install(context, true, null, phase => { if (phase == "registration") throw new IOException("Injected failure after registration"); }), "A partial installation reports failure");
                check(SetupEngine.Hash(context.Executable) == executableHash && SetupEngine.Hash(context.DesktopLink) == linkHash, "Failed installation restores previous files and shortcuts");
                using (var key = Registry.CurrentUser.OpenSubKey(context.RegistryPath, true))
                {
                    check(InstallationRegistration.RefreshKey(key, context.Executable, new Version(9, 1, 0)), "Registered installation accepts its app version after an in-app update");
                    check(!InstallationRegistration.RefreshKey(key, Path.Combine(area, "Portable", "Spacio.exe"), new Version(9, 2, 0)) && Convert.ToString(key.GetValue("DisplayVersion")) == "9.1.0", "Portable copies cannot change the installed-app registration");
                    InstallationRegistration.RefreshKey(key, context.Executable, SetupEngine.Version);
                }
                using (var held = new FileStream(context.Executable, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    reject(() => SetupEngine.Install(context, true), "A running or locked app blocks reinstall without forcing it closed");
                    reject(() => SetupEngine.Uninstall(context), "A locked app blocks uninstall before any files are removed");
                }
                using (var held = new SetupLock(context.Directory))
                    check(Task.Run(() => { try { SetupEngine.Install(context, true); return false; } catch (IOException) { return true; } }).Result, "Concurrent setup operations cannot modify the same installation");
                string newerFolder = Path.Combine(area, "newer"); Directory.CreateDirectory(newerFolder);
                string newer = Path.Combine(newerFolder, "Spacio.exe");
                using (var provider = new Microsoft.CSharp.CSharpCodeProvider())
                {
                    var options = new System.CodeDom.Compiler.CompilerParameters { GenerateExecutable = true, OutputAssembly = newer, CompilerOptions = "/target:winexe" };
                    var compiled = provider.CompileAssemblyFromSource(options, "using System.Reflection; [assembly:AssemblyVersion(\"99.0.0.0\")] class P { static void Main() {} }");
                    check(!compiled.Errors.HasErrors, "Create isolated newer-version fixture");
                }
                string original = Path.Combine(area, "original.exe"); File.Copy(context.Executable, original);
                File.Copy(newer, context.Executable, true);
                reject(() => SetupEngine.Install(context, true), "An older Setup refuses to downgrade an app updated inside Spacio");
                check(SetupEngine.Hash(context.Executable) == SetupEngine.Hash(newer), "Downgrade rejection leaves the newer executable intact");
                File.Copy(original, context.Executable, true);
                if (probe != null)
                {
                    string upgradeReport = Path.Combine(area, "upgrade-report.txt");
                    using (var process = Process.Start(new ProcessStartInfo(probe, "\"" + context.Executable + "\" \"" + upgradeReport + "\"" + (publicUpgrade ? "" : " --local")) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                    { check(process.WaitForExit(60000) && process.ExitCode == 0, "Installed-app update, restart and saved-scan preservation: " + (File.Exists(upgradeReport) ? File.ReadAllText(upgradeReport) : "missing report")); }
                    check(SetupEngine.Hash(Path.Combine(context.Directory, "Spacio.Setup.exe")) == installerHash && SetupEngine.Same(SetupEngine.ShortcutTarget(context.DesktopLink), context.Executable), "In-app update keeps the uninstaller and desktop shortcut working");
                }
                string preferences = Path.Combine(area, "language.txt"); File.WriteAllText(preferences, "fa");
                var savedPaths = new List<string> { preferences };
                string savedScans = Path.Combine(area, "saved-scans");
                if (Directory.Exists(savedScans)) savedPaths.AddRange(Directory.GetFiles(savedScans));
                var savedData = savedPaths.ToDictionary(p => p, SetupEngine.Hash);
                string keep = Path.Combine(context.Directory, "my-notes.txt"); File.WriteAllText(keep, "unowned files stay");
                string rollback = Path.Combine(context.Directory, ".spacio-fixture.previous.exe"); File.WriteAllText(rollback, "user rollback copy");
                reject(() => SetupEngine.Uninstall(context, phase => { if (phase == "registration") throw new IOException("Injected uninstall failure"); }), "A partial uninstall reports failure");
                check(File.Exists(context.Executable) && File.Exists(context.DesktopLink), "Failed uninstall restores installed files and shortcuts");
                using (var window = new SetupWindow(context, false, true))
                { window.SetLanguage(false); window.SavePreview(report + "-en.png"); window.SetLanguage(true); window.SavePreview(report + "-fa.png"); window.Close(); }
                AwaitFixtureFiles(context);
                SetupEngine.Uninstall(context);
                check(SetupEngine.OwnedNames.All(n => !File.Exists(Path.Combine(context.Directory, n))) && !File.Exists(context.DesktopLink) && !File.Exists(context.MenuLink), "Uninstall removes only installed program files and owned shortcuts");
                using (var key = Registry.CurrentUser.OpenSubKey(context.RegistryPath)) check(key == null, "Uninstall removes its own Installed apps entry");
                check(File.ReadAllText(keep) == "unowned files stay" && File.ReadAllText(rollback) == "user rollback copy" && File.ReadAllText(personal) == "keep this user file", "Uninstall preserves user files and rollback copies");
                SetupEngine.Install(context, false);
                check(!File.Exists(context.DesktopLink) && File.Exists(context.MenuLink), "Desktop shortcut can be declined while Start Menu remains available");
                SetupEngine.WriteShortcut(context.DesktopLink, personal);
                AwaitFixtureFiles(context);
                reject(() => SetupEngine.Install(context, true), "An unrelated existing desktop shortcut is not overwritten");
                AwaitFixtureFiles(context);
                SetupEngine.Uninstall(context);
                check(File.Exists(context.DesktopLink) && SetupEngine.Same(SetupEngine.ShortcutTarget(context.DesktopLink), personal), "Uninstall preserves a shortcut repointed to an unrelated file");
                check(savedData.All(p => File.Exists(p.Key) && SetupEngine.Hash(p.Key) == p.Value), "Isolated saved scans and preferences survive uninstall, reinstall and failure recovery byte-for-byte");
                if (userData != null && !AppRunning())
                    check(userData.All(p => File.Exists(p.Key) && SetupEngine.Hash(p.Key) == p.Value), "Existing real saved scans and language preferences remain byte-for-byte unchanged");
                else log.Add("NOTE: A live Spacio copy owns the real data; real-data immutability was not asserted. Preservation checks used isolated saved scans and preferences.");
                log.Add("FIXTURE=" + area); File.WriteAllLines(report, log); return 0;
            }
            catch (Exception e) { log.Add(e.ToString()); log.Add("FIXTURE=" + area); File.WriteAllLines(report, log); return 1; }
        }
    }
}
