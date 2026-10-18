using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Spacio
{
    internal static class AppLeftoverTests
    {
        internal static void Run(string area, Action<bool, string> check)
        {
            var token = CancellationToken.None;
            string root = Path.Combine(area, "Post uninstall");
            var roots = new CacheRoots { Local = Path.Combine(root, "AppData", "Local"), Roaming = Path.Combine(root, "AppData", "Roaming"), Common = Path.Combine(root, "ProgramData") };
            string installation = Path.Combine(roots.Local, "Programs", "Notion");
            string launcher = CleanupTests.Put(installation, "Uninstall.exe"); string installLog = CleanupTests.Put(installation, "uninstall.log");
            string settings = CleanupTests.Put(roots.Roaming, @"Notion\settings.json", "account fixture");
            string cache = CleanupTests.Put(roots.Roaming, @"Notion\Cache\data", "cached fixture");
            string tentative = CleanupTests.Put(roots.Local, @"Notion\keep.txt", "possible match");
            string near = CleanupTests.Put(roots.Local, @"Notion Helper\keep.txt", "unrelated");
            string other = CleanupTests.Put(roots.Roaming, @"Slack\Cache\keep.txt", "another app");
            var app = new InstalledApp { Id = "fixture/notion", Name = "Notion", Publisher = "Notion Labs", Location = installation, Family = "notion", Command = "fixture only" };
            var session = AppLeftovers.Capture(app, roots, token);
            File.Delete(launcher); // Only the generated fake uninstaller file.
            var rows = AppLeftovers.Scan(session, token, new List<InstalledApp>(), false);
            check(rows.Any(i => i.Path == installation && i.CanChoose) && File.Exists(installLog), "Captured installation identity survives removal of the generated uninstaller");
            var data = rows.Single(i => i.Path == Path.GetDirectoryName(settings));
            check(data.Removal != null && data.Entry.Bytes == new FileInfo(settings).Length + new FileInfo(cache).Length, "Leftover data preview reports exact combined size and contents");
            check(rows.Any(i => i.Spec != null && i.Path == Path.GetDirectoryName(cache)) && rows.Any(i => i.Leftover.Tentative && i.Path == Path.GetDirectoryName(tentative)), "Known caches and unverified exact-name candidates are labelled separately");
            check(rows.All(i => !i.Chosen) && File.ReadAllText(settings) == "account fixture", "Automatic leftover discovery leaves every row unchecked and all files intact");
            check(!rows.Any(i => Paths.Within(near, i.Path) || Paths.Within(other, i.Path)), "Scoped leftover scan excludes similar names and unrelated application caches");
            string stableCache = CleanupTests.Put(roots.Local, @"Google\Chrome\User Data\Default\Cache\keep.txt", "stable channel data");
            var beta = new InstalledApp { Id = "fixture/beta", Name = "Google Chrome Beta", Publisher = "Google", Location = Path.Combine(root, "Chrome Beta"), Family = "chrome", Command = "fixture only" };
            check(!AppLeftovers.Capture(beta, roots, token).Candidates.Any(c => Paths.Within(stableCache, c.Path)), "An unsupported browser channel does not inherit the stable channel's data or cache paths");
            var candidate = session.Candidates.Single(c => c.Installation);
            check(AppLeftovers.Block(candidate, new List<InstalledApp> { app }, false) != null, "A cancelled or still-registered application cannot authorize installation cleanup");
            var replacement = new InstalledApp { Id = "fixture/other", Name = "Other fixture", Location = Path.Combine(installation, "Shared") };
            check(AppLeftovers.Block(candidate, new List<InstalledApp> { replacement }, false) != null, "Another registered application inside the captured installation blocks removal");
            check(AppLeftovers.Block(data.Leftover, new List<InstalledApp> { new InstalledApp { Id = "fixture/data-owner", Name = "Different application", Location = data.Path } }, false) != null, "Data and name-match candidates cannot remove another registered application's installation");
            string registryName = "Spacio-Leftover-Test-" + Guid.NewGuid().ToString("N");
            string registryPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\" + registryName;
            using (var existing = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(registryPath))
                if (existing != null) throw new Exception("Generated registry fixture already exists.");
            try
            {
                using (var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(registryPath))
                { key.SetValue("DisplayName", registryName); key.SetValue("SystemComponent", 1); key.SetValue("InstallLocation", installation); }
                check(!InstalledApps.Read(token).Any(a => a.Name == registryName) && InstalledApps.Read(token, true).Any(a => a.Name == registryName && a.Location == installation), "Strict ownership checks include isolated hidden components without exposing them as uninstallable apps");
            }
            finally { Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree(registryPath, false); }
            check(AppLeftovers.Block(candidate, new List<InstalledApp>(), true) != null, "A running application blocks leftover cleanup");
            File.AppendAllText(settings, " changed after review");
            bool changed = false; try { data.Removal.DeletePermanent(token); } catch (IOException) { changed = true; }
            check(changed && File.Exists(cache), "A changed leftover preview cannot remove unreviewed data");
            string missing = Path.Combine(roots.Common, "Notion"); CleanupTests.Put(missing, "late.log", "created during uninstall");
            rows = AppLeftovers.Scan(session, token, new List<InstalledApp>(), false);
            check(rows.Any(i => i.Path == missing), "Post-uninstall rescan finds captured candidate paths created after launch");
            var sharedApp = new InstalledApp { Id = "fixture/same-family", Name = "Notion variant", Location = Path.Combine(root, "Variant"), Family = "notion" };
            check(AppLeftovers.Scan(session, token, new List<InstalledApp> { sharedApp }, false).Any(i => i.Evidence.Contains(L.T("برنامهٔ دیگری از همین خانواده نصب است؛ داده‌ها ممکن است مشترک باشند"))), "Surviving applications in the same family add a shared-data warning");
            var tracker = new UninstallTracker();
            check(!tracker.Observe(true, false, null, true, false, false) && !tracker.Observe(true, true, 0, true, true, false), "Running uninstallers and early bootstrap exits do not imply completion");
            check(!tracker.Observe(true, true, 0, false, false, false) && !tracker.Observe(true, true, 1602, true, false, false) && !tracker.Observe(false, true, null, true, false, false), "Unknown registration, cancellation/error exits and missing process handles require manual review");
            check(!tracker.Observe(true, true, 0, true, false, false) && !tracker.Observe(true, true, 0, true, false, false) && tracker.Observe(true, true, 0, true, false, false), "Successful exit plus stable registration absence enables automatic leftover discovery");
            check(!tracker.Observe(true, true, 0, true, true, false) && !tracker.Observe(true, true, 0, true, false, false), "Reappearing registration resets the completion settling window");
            string hostRoot = Path.Combine(root, "Generated process"); Directory.CreateDirectory(hostRoot);
            string hostExe = Path.Combine(hostRoot, "Spacio.exe"), sourceExe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            File.Copy(sourceExe, hostExe); File.Copy(sourceExe + ".config", hostExe + ".config");
            string marker = Path.Combine(hostRoot, "process-marker");
            using (var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(hostExe, "--update-test-host \"" + marker + "\"") { UseShellExecute = false, CreateNoWindow = true }))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    while (!File.Exists(marker + ".ready") && watch.ElapsedMilliseconds < 10000) Thread.Sleep(50);
                    session.Process = process;
                    check(File.Exists(marker + ".ready") && !session.Exited && AppLeftovers.Block(candidate, new List<InstalledApp>(), false) != null, "A real generated process keeps leftover deletion blocked while running");
                }
                finally { File.WriteAllText(marker + ".exit", "exit generated helper"); process.WaitForExit(10000); }
                check(session.Exited && session.ExitCode == 0, "Tracking observes the generated process's normal exit without terminating it");
                session.Process = null;
            }
            string programs = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), scope = Path.Combine(programs, "Spacio isolated policy example");
            check(ReviewedRemoval.ProtectedReason(scope) != null && ReviewedRemoval.ProtectedReason(scope, scope) == null && ReviewedRemoval.ProtectedReason(Path.Combine(programs, "Other"), scope) != null, "Installation policy exception is scoped to an exact captured folder, not neighboring Program Files data");
            check(ReviewedRemoval.ProtectedReason(programs, programs) != null && ReviewedRemoval.ProtectedReason(Path.Combine(programs, "Common Files"), Path.Combine(programs, "Common Files")) != null, "Program Files containers and shared Windows components remain protected");
            var cancelled = new CancellationTokenSource(); cancelled.Cancel(); bool stopped = false;
            try { AppLeftovers.Scan(session, cancelled.Token, new List<InstalledApp>(), false); } catch (OperationCanceledException) { stopped = true; }
            check(stopped && File.Exists(settings) && File.Exists(near), "Cancellation preserves previously discovered and unrelated fixture files");
            var fresh = AppLeftovers.Inspect(candidate, token, new List<InstalledApp>(), false);
            fresh.Removal.DeletePermanent(token);
            check(!Directory.Exists(installation) && File.Exists(settings) && File.Exists(tentative) && File.Exists(near) && File.Exists(other), "Removing one reviewed installation fixture preserves all unselected data and other apps");
            check(AppLeftovers.Scan(session, token, new List<InstalledApp>(), false).All(i => i.Path != installation && !i.Chosen), "Rescan removes deleted paths and resets every remaining selection");
        }
    }
    public sealed partial class Controller
    {
        private async Task CheckLeftoverUI(string prefix)
        {
            string area = prefix + "-leftover-fixture";
            var roots = new CacheRoots { Local = Path.Combine(area, "AppData", "Local"), Roaming = Path.Combine(area, "AppData", "Roaming"), Common = Path.Combine(area, "ProgramData") };
            string product = "Spacio generated app " + Guid.NewGuid().ToString("N").Substring(0, 8);
            string settings = CleanupTests.Put(roots.Roaming, product + @"\settings.json", "fixture account data");
            string installation = Path.Combine(area, "Installed", product); CleanupTests.Put(installation, "leftover.log");
            var app = new InstalledApp { Id = "fixture/ui", Name = product, Publisher = "Fixture vendor", Location = installation, Family = "", Command = "fixture only" };
            var session = AppLeftovers.Capture(app, roots, CancellationToken.None);
            Exception failure = null;
            int polls = 0;
            bool ready = WaitForUninstaller(session, ct => Task.FromResult(new UninstallPoll { ProcessKnown = true, Exited = ++polls > 1, ExitCode = polls > 1 ? (int?)0 : null, RegistrationKnown = true, Registered = polls < 3 }),
                dialog => { dialog.ShowInTaskbar = false; }, 20);
            if (!ready || polls < 5 || !File.Exists(settings)) throw new Exception("Automatic post-uninstall tracking failed or changed data.");
            foreach (bool persian in new [] { false, true })
            {
                L.Persian = persian; ApplyLanguage();
                lastUninstall = session; await OpenCleanupPage(false, session); await Task.Delay(100);
                if (cleanupItems.Count < 2 || cleanupItems.Any(i => i.Chosen) || !U<TextBlock>("CleanupTitle").Text.Contains(product)) throw new Exception("Scoped leftover page failed to show unchecked app-specific rows.");
                var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)Window.ActualWidth, (int)Window.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(Window);
                Startup.SaveBitmap(bitmap, prefix + "-leftovers-" + (persian ? "fa" : "en") + ".png");
                var data = cleanupItems.Single(i => i.Path == Path.GetDirectoryName(settings));
                await ReviewCleanupItems(new List<CleanupItem> { data }, null, true, dialog => {
                    try
                    {
                        dialog.ShowInTaskbar = false;
                        var row = (CleanupItem)ReviewControl<DataGrid>(dialog, "ReviewItems").Items[0];
                        if (row.Chosen || !row.CanChoose || ReviewControl<Button>(dialog, "ReviewApply").IsEnabled) throw new Exception("Leftovers were selected or deletion enabled without explicit selection.");
                        ReviewControl<Button>(dialog, "ReviewCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception e) { failure = e; dialog.Close(); }
                });
                if (failure != null) throw failure;
                if (!File.Exists(settings)) throw new Exception("Cancelling leftover review removed data.");
                await OpenCleanupPage(true);
                if (!U<Button>("CleanupLeftovers").IsVisible) throw new Exception("Last uninstall review cannot be reopened.");
            }
            await OpenCleanupPage(false, session);
            bool reported = false;
            var closeReport = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            closeReport.Tick += delegate {
                foreach (var dialog in Application.Current.Windows.Cast<Window>().Where(w => w.Owner == Window && (w.Title == L.T("گزارش پاک‌سازی") || w.Title == L.T("خطا"))).ToArray())
                { reported = dialog.Title == L.T("گزارش پاک‌سازی"); dialog.Close(); }
            };
            closeReport.Start();
            try
            {
                await ReviewCleanupItems(new List<CleanupItem> { cleanupItems.Single(i => i.Path == Path.GetDirectoryName(settings)) }, null, true, dialog => {
                    try
                    {
                        dialog.ShowInTaskbar = false;
                        var row = (CleanupItem)ReviewControl<DataGrid>(dialog, "ReviewItems").Items[0]; row.Chosen = true;
                        var ack = ReviewControl<CheckBox>(dialog, "ReviewAcknowledge"); ack.IsChecked = true; ack.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                        var dataAck = ReviewControl<CheckBox>(dialog, "ReviewDataAcknowledge"); dataAck.IsChecked = true; dataAck.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                        if (!ReviewControl<Button>(dialog, "ReviewApply").IsEnabled) throw new Exception("Explicitly selected leftover cannot be confirmed.");
                        ReviewControl<Button>(dialog, "ReviewApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception e) { failure = e; dialog.Close(); }
                });
            }
            finally { closeReport.Stop(); }
            if (failure != null) throw failure;
            if (!reported || File.Exists(settings) || !File.Exists(Path.Combine(installation, "leftover.log")) || cleanupItems.Any(i => i.Path == Path.GetDirectoryName(settings) || i.Chosen))
                throw new Exception("Confirmed leftover removal did not preserve unselected data or refresh scoped results.");
            bool cancelled = WaitForUninstaller(session, ct => Task.FromResult(new UninstallPoll { ProcessKnown = true, Exited = false, RegistrationKnown = true, Registered = true }),
                dialog => { dialog.ShowInTaskbar = false; ReviewControl<Button>(dialog, "UninstallLater").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); }, 20);
            if (cancelled || !File.Exists(Path.Combine(installation, "leftover.log"))) throw new Exception("Stopping tracking changed files.");
            lastUninstall = null; leftoverView = null; UpdateCleanupControls();
            File.WriteAllText(prefix + ".leftovers.ok.txt", "PASS: automatic tracking waits for process exit and stable registration absence; EN/FA scoped leftover pages; fresh review leaves all rows unchecked; cancellation preserves data; last uninstall can be reopened; actual confirmed selection removes only the chosen fixture and refreshes scoped results. Only generated fixtures used.");
        }
    }
}
