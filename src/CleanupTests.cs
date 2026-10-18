using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Spacio
{
    internal static class CleanupTests
    {
        internal static string Put(string root, string relative, string content = "fixture only")
        {
            string path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content); return path;
        }
        internal static void Settle(string root)
        {
            foreach (var path in Directory.GetDirectories(root, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length).Concat(new [] { root }))
                for (int attempt = 0; ; attempt++)
                { try { Directory.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-2)); break; } catch (IOException) { if (attempt >= 20) throw; Thread.Sleep(50); } }
        }
        private static CleanupItem Ordinary(string root, string path)
        {
            var snapshot = new Scanner().Scan(root, CancellationToken.None, null);
            return new CleanupItem { Path = path, Owner = "Test fixture", Evidence = "Generated test data", Source = snapshot, Entry = snapshot.Entries.Single(e => Paths.Equal(e.Path, path)), Chosen = true };
        }
        private static List<string> Delete(CleanupItem item, CacheRoots roots, CancellationToken token)
        {
            return CleanupEngine.Execute(new List<CleanupItem> { item }, true, IntPtr.Zero, token, roots).GetAwaiter().GetResult();
        }
        internal static int Run(string report)
        {
            var lines = new List<string>();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception("FAIL: " + label); lines.Add("PASS: " + label); };
            Action<Action, string> rejects = (action, label) => { bool rejected = false; try { action(); } catch (IOException) { rejected = true; } check(rejected, label); };
            string workspace = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(report))).FullName;
            string area = Path.Combine(workspace, ".test-data", "cleanup-" + Guid.NewGuid().ToString("N"));
            var roots = new CacheRoots { Local = Path.Combine(area, "AppData", "Local"), Roaming = Path.Combine(area, "AppData", "Roaming") };
            var token = CancellationToken.None;
            try
            {
                string chrome = Put(roots.Local, @"Google\Chrome\User Data\Default\Cache\Cache_Data\data_0");
                Put(roots.Local, @"Google\Chrome\User Data\Default\Code Cache\js\test");
                string cookies = Put(roots.Local, @"Google\Chrome\User Data\Default\Network\Cookies", "keep cookies");
                string login = Put(roots.Local, @"Google\Chrome\User Data\Default\Login Data", "keep passwords");
                string storage = Put(roots.Local, @"Google\Chrome\User Data\Default\Local Storage\test", "keep user data");
                Put(roots.Local, @"Mozilla\Firefox\Profiles\test.default\cache2\entries\test");
                string code = Put(roots.Roaming, @"Code\Cache\test");
                Put(roots.Roaming, @"Adobe\Common\Media Cache Files\test");
                string shader = Put(roots.Local, @"NVIDIA\DXCache\shader.bin");
                string personal = Put(area, @"Documents\cache\notes.txt", "not known cache");
                string protectedScan = Put(roots.Local, @"Spacio\Scans\saved.scan", "unchanged scan bytes");
                string canary = Put(area, @"Outside\keep.txt", "keep outside");
                Settle(area);
                var specs = CacheCatalog.Discover(roots, token);
                check(specs.Count == 6, "Catalogue discovers only the six generated known cache directories");
                check(specs.All(s => !new [] { cookies, login, storage, protectedScan, personal }.Any(p => Paths.Within(p, s.Path))), "Cookies, passwords, local storage, Spacio scans and name-only candidates are excluded");
                var inspected = specs.Select(s => CacheCatalog.Inspect(s, token, roots)).ToList();
                check(inspected.All(i => i.Entry != null && i.Entry.Issues == 0 && i.Entry.FileCount == 1), "Preview records exact complete file counts and sizes");
                check(inspected.Single(i => i.Spec.Family == "chrome" && Path.GetFileName(i.Path) == "Cache").Contents().Any(e => e.Path == chrome), "Full preview includes nested paths, not just cache roots");
                var shared = inspected.Single(i => i.Spec.Family == "adobe-media");
                check(shared.Spec.Shared && !shared.Chosen, "Shared Adobe cache is labelled and not preselected");
                var scan = new Scanner().Scan(area, token, null);
                var candidates = CacheCatalog.Candidates(scan);
                check(candidates.Any(i => Paths.Equal(i.Path, Path.GetDirectoryName(personal)) && i.Candidate && !i.Chosen), "A name-only cache candidate is unverified and unchecked");
                check(candidates.Where(i => Paths.Within(i.Path, roots.Local) || Paths.Within(i.Path, roots.Roaming)).All(i => !i.CanChoose), "A cache folder name cannot bypass AppData protection");
                var gpu = inspected.Single(i => i.Spec.Family == "nvidia");
                check(CacheCatalog.BlockCache(gpu, roots) == null, "Only authenticated exact known cache roots can use the narrow AppData exception");
                var spoof = Ordinary(area, Path.GetDirectoryName(protectedScan));
                spoof.Spec = new CacheSpec { Path = spoof.Path, Family = "nvidia", Processes = new string[0] };
                spoof.Source = new Scanner().Scan(spoof.Path, token, null); spoof.Entry = spoof.Source.Root;
                check(CacheCatalog.BlockCache(spoof, roots) != null && CacheCatalog.BlockCache(spoof, roots, false) != null, "Spoofed known-cache identity cannot authorize deleting Spacio scans");
                gpu.Spec.Processes = new [] { Process.GetCurrentProcess().ProcessName };
                check(CacheCatalog.BlockCache(gpu, roots) != null && Delete(gpu, roots, token).Any(s => s.StartsWith("✕")) && File.Exists(shader), "A running owner blocks cleanup without terminating processes");
                gpu.Spec.Processes = new string[0];
                string added = Put(gpu.Path, "added-after-preview.bin");
                check(Delete(gpu, roots, token).Any(s => s.StartsWith("✕")) && File.Exists(shader) && File.Exists(added), "A file added after preview blocks the entire reviewed cache group");
                gpu = CacheCatalog.Inspect(gpu.Spec, token, roots);
                File.AppendAllText(shader, "changed");
                check(Delete(gpu, roots, token).Any(s => s.StartsWith("✕")) && File.Exists(shader), "A modified reviewed cache file blocks stale deletion");
                File.SetAttributes(shader, FileAttributes.System);
                gpu = CacheCatalog.Inspect(gpu.Spec, token, roots);
                check(!gpu.CanChoose && CacheCatalog.BlockCache(gpu, roots) != null, "System-marked descendants disable cache deletion");
                File.SetAttributes(shader, FileAttributes.Normal); Settle(gpu.Path);
                gpu = CacheCatalog.Inspect(gpu.Spec, token, roots);
                var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                check(Delete(gpu, roots, cancelled.Token).Count == 1 && File.Exists(shader), "Cancellation leaves unprocessed cache files intact and reports the stop");
                var deleted = Delete(gpu, roots, token);
                check(deleted.All(s => s.StartsWith("✓")) && Directory.Exists(gpu.Path) && Directory.GetFileSystemEntries(gpu.Path).Length == 0, "Permanent cleanup removes only reviewed contents and retains the cache root: " + String.Join(" | ", deleted));
                check(File.ReadAllText(cookies) == "keep cookies" && File.ReadAllText(login) == "keep passwords" && File.ReadAllText(storage) == "keep user data" && File.ReadAllText(protectedScan) == "unchanged scan bytes", "User profile data and saved scan bytes survive actual cache deletion");
                string ordinaryPath = Put(area, @"User files\Remove\nested\فارسی.txt");
                string ordinaryRoot = Path.Combine(area, "User files"); Settle(ordinaryRoot);
                var ordinary = Ordinary(ordinaryRoot, Path.Combine(ordinaryRoot, "Remove"));
                ordinary.Source.IsStale = true;
                string newContent = Put(ordinary.Path, "added-before-review.txt", "new contents must appear in review");
                var freshSelection = CleanupEngine.InspectSelection(ordinary.Entry, ordinary.Source, token);
                check(freshSelection.CanChoose && freshSelection.Contents().Any(e => e.Path == newContent) && !freshSelection.Source.IsStale && ordinary.Source.IsStale,
                    "A stale saved view gets a fresh independent preview of only the selected subtree");
                check(!ordinary.Entry.Children.Any(e => e.Path == newContent) && !Object.ReferenceEquals(freshSelection.Entry, ordinary.Entry), "Preparing deletion does not mutate the displayed saved tree");
                ordinary.Source.Cancelled = true;
                check(!CleanupEngine.InspectSelection(ordinary.Entry, ordinary.Source, token).CanChoose, "Cancelled scans cannot authorize fresh selection cleanup");
                ordinary.Source.Cancelled = false; ordinary.Entry.Issues = 1;
                check(!CleanupEngine.InspectSelection(ordinary.Entry, ordinary.Source, token).CanChoose, "Incomplete selections remain blocked");
                ordinary.Entry.Issues = 0;
                check(!CleanupEngine.InspectSelection(ordinary.Source.Root, ordinary.Source, token).CanChoose, "Fresh preview cannot authorize deletion of the scan root");
                var protectedSelection = scan.Entries.Single(e => e.Path == protectedScan);
                check(!CleanupEngine.InspectSelection(protectedSelection, scan, token).CanChoose, "Fresh preview never bypasses protected AppData or scan storage");
                File.AppendAllText(newContent, " edited after review");
                check(Delete(freshSelection, roots, token).Any(s => s.StartsWith("✕")) && File.Exists(newContent), "Changes after the fresh preview still block deletion");
                ordinary = CleanupEngine.InspectSelection(ordinary.Entry, ordinary.Source, token);
                check(CleanupEngine.Collapse(new [] { ordinary, new CleanupItem { Path = ordinaryPath } }).Count == 1, "Parent and child selections collapse to one deletion");
                deleted = Delete(ordinary, roots, token);
                check(deleted.Count == 1 && deleted[0].StartsWith("✓") && !Directory.Exists(ordinary.Path), "Permanent folder removal works on generated Unicode fixtures: " + String.Join(" | ", deleted));
                string locked = Put(ordinaryRoot, "locked.txt"); var lockedItem = Ordinary(ordinaryRoot, locked);
                using (var held = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.Read))
                    check(Delete(lockedItem, roots, token).Any(s => s.StartsWith("✕")) && File.Exists(locked), "Locked files remain intact with a per-path error");
                check(new RecycleSink().PreDeleteItem(0, null) < 0 && new RecycleSink(true).PreDeleteItem(0, null) == 0, "Only explicit permanent mode permits permanent Shell operations");
                string link = Path.Combine(gpu.Path, "linked");
                using (var process = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + Path.GetDirectoryName(canary) + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
                { string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd(); process.WaitForExit(); check(process.ExitCode == 0, "Create isolated junction fixture: " + output.Trim()); }
                gpu = CacheCatalog.Inspect(gpu.Spec, token, roots);
                check(!gpu.CanChoose && Delete(gpu, roots, token).Any(s => s.StartsWith("✕")) && File.ReadAllText(canary) == "keep outside", "Cache cleanup refuses junctions and never touches their external target");
                var app = new InstalledApp { Name = "Fixture app", Location = ordinaryRoot, Command = "\"" + Process.GetCurrentProcess().MainModule.FileName + "\" --fixture-argument" };
                check(InstalledApps.Owner(locked, new [] { app }) == app && InstalledApps.Owner(ordinaryRoot + "-sibling\\file", new [] { app }) == null, "App association uses registered install-location boundaries");
                var start = InstalledApps.StartInfo(app);
                check(start.FileName == Process.GetCurrentProcess().MainModule.FileName && start.Arguments == "--fixture-argument", "Quoted registered executable and arguments parse without launching an uninstaller");
                app.Command = Process.GetCurrentProcess().MainModule.FileName + " --fixture";
                check(InstalledApps.StartInfo(app).Arguments == "--fixture", "Unquoted absolute executable command parses without shell expansion");
                app.Command = "relative.exe /delete"; rejects(() => InstalledApps.StartInfo(app), "Relative uninstaller commands are rejected");
                app.Command = "\"" + Path.Combine(Environment.SystemDirectory, "cmd.exe") + "\" /c echo unsafe";
                rejects(() => InstalledApps.StartInfo(app), "Shell launcher uninstall commands use Windows Settings instead");
                app.Msi = true; app.Command = "MsiExec.exe /I{12345678-1234-1234-1234-123456789ABC}";
                check(InstalledApps.StartInfo(app).Arguments == "/x {12345678-1234-1234-1234-123456789ABC}", "MSI uninstall uses the registered product GUID and /x without quiet or destructive extras");
                app.Command = "MsiExec.exe /I malformed"; rejects(() => InstalledApps.StartInfo(app), "MSI commands without a valid product GUID are rejected");
                check(InstalledApps.Family("My file named Chrome.txt") == "" && InstalledApps.Family("Adobe Acrobat") == "" && InstalledApps.Family("Adobe Premiere Pro 2026") == "adobe-media", "Unrelated filenames and Acrobat are not assigned Adobe media/Chrome caches");
                check(File.ReadAllText(canary) == "keep outside" && File.ReadAllText(protectedScan) == "unchanged scan bytes", "All deletion tests preserve outside canaries and saved scans");
                lines.Add("FIXTURE=" + area); File.WriteAllLines(report, lines); return 0;
            }
            catch (Exception e) { lines.Add(e.ToString()); lines.Add("FIXTURE=" + area); File.WriteAllLines(report, lines); return 1; }
        }
    }

    public sealed partial class Controller
    {
        private static T ReviewControl<T>(DependencyObject root, string name) where T : FrameworkElement
        {
            var element = root as T; if (element != null && element.Name == name) return element;
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++) { var match = ReviewControl<T>(VisualTreeHelper.GetChild(root, i), name); if (match != null) return match; }
            return null;
        }
        public async Task CheckCleanupUI(string prefix)
        {
            var original = snapshot; var roots = cleanupRoots; bool language = L.Persian;
            double width = Window.Width, height = Window.Height;
            string area = Path.GetFullPath(prefix + "-cleanup-fixture");
            cleanupRoots = new CacheRoots { Local = Path.Combine(area, "AppData", "Local"), Roaming = Path.Combine(area, "AppData", "Roaming") };
            string fixture = CleanupTests.Put(cleanupRoots.Local, @"NVIDIA\DXCache\shader.bin");
            CleanupTests.Put(cleanupRoots.Roaming, @"Adobe\Common\Media Cache Files\conformed.cfa");
            CleanupTests.Put(cleanupRoots.Local, @"Google\Chrome\User Data\Default\Cache\Cache_Data\data_0");
            CleanupTests.Settle(area);
            Window.Width = Window.MinWidth; Window.Height = Window.MinHeight;
            await OpenCleanupPage(false); Window.UpdateLayout();
            if (!cleanupPageOpen || U<Grid>("Workspace").IsVisible || U<DataGrid>("CacheGrid").ActualHeight < 300 || U<ScrollViewer>("DriveScroller").ActualHeight < 100) throw new Exception("Cache full page clips content at the minimum window size.");
            if (snapshot != original || cleanupItems.Count < 3) throw new Exception("Cache page changed the saved scan or missed fixture caches.");
            U<TextBox>("CleanupSearch").Text = "NVIDIA";
            if (U<DataGrid>("CacheGrid").Items.Count != 1) throw new Exception("Cache search did not filter by app.");
            U<TextBox>("CleanupSearch").Text = "";
            foreach (bool persian in new [] { false, true })
            {
                L.Persian = persian; ApplyLanguage(); Window.UpdateLayout(); await Task.Delay(100);
                SavePreview(prefix + "-cache-page-" + (persian ? "fa" : "en") + ".png");
                Exception failure = null;
                var item = cleanupItems.Single(i => i.Spec != null && i.Spec.Family == "nvidia"); item.Chosen = true;
                var additional = cleanupItems.Single(i => i.Spec != null && i.Spec.Family == "adobe-media"); additional.Chosen = false;
                var choice = ShowCleanupReview(new List<CleanupItem> { item, additional }, null, async dialog => {
                    try
                    {
                        dialog.ShowInTaskbar = false; await Task.Delay(200); dialog.UpdateLayout();
                        var permanent = ReviewControl<CheckBox>(dialog, "ReviewPermanent");
                        var acknowledge = ReviewControl<CheckBox>(dialog, "ReviewAcknowledge");
                        var apply = ReviewControl<Button>(dialog, "ReviewApply");
                        var details = ReviewControl<DataGrid>(dialog, "ReviewDetails");
                        if (permanent.IsChecked != true || apply.IsEnabled || acknowledge.IsChecked == true) throw new Exception("Permanent default must require explicit acknowledgement.");
                        dialog.Height = Math.Min(600, dialog.Height); dialog.UpdateLayout();
                        var footerPosition = apply.TranslatePoint(new Point(0, apply.ActualHeight), (UIElement)dialog.Content);
                        if (footerPosition.Y > ((FrameworkElement)dialog.Content).ActualHeight + 1 || details.ActualHeight < 25) throw new Exception("Confirmation or preview is clipped on a short display.");
                        if (additional.Chosen || details.Items.Count != item.Contents().Count) throw new Exception("Review omitted contents or selected an additional cache.");
                        var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)dialog.ActualWidth, (int)dialog.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(dialog);
                        Startup.SaveBitmap(bitmap, prefix + "-cleanup-review-" + (persian ? "fa" : "en") + ".png");
                        acknowledge.IsChecked = true; acknowledge.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                        if (!apply.IsEnabled) throw new Exception("Explicit acknowledgement did not enable confirmation.");
                        ReviewControl<Button>(dialog, "ReviewCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception e) { failure = e; dialog.Close(); }
                });
                if (failure != null) throw failure;
                if (choice.Confirmed || !File.Exists(fixture)) throw new Exception("Cancel must never authorize deletion.");
                if (!persian && (item.Evidence.Any(c => c >= '\u0600' && c <= '\u06ff') || U<TextBlock>("CleanupStatus").Text.Any(c => c >= '\u0600' && c <= '\u06ff'))) throw new Exception("Cached labels did not change to English.");
            }
            await OpenCleanupPage(true);
            installedApps = new List<InstalledApp> { new InstalledApp { Name = "Spacio test application", Version = "1.0", Publisher = "Fixture publisher", Location = area, Family = "", Command = "fixture only" } };
            foreach (bool persian in new [] { false, true })
            {
                L.Persian = persian; ApplyLanguage(); await Task.Delay(150); Window.UpdateLayout();
                var appGrid = U<DataGrid>("InstalledAppsGrid");
                var nameCell = appGrid.Columns[0].GetCellContent(appGrid.Items[0]) as TextBlock;
                if (nameCell == null || nameCell.Text != installedApps[0].Name || appGrid.Columns.Any(c => c.ActualWidth < 30)) throw new Exception("Installed app columns did not render their content.");
                SavePreview(prefix + "-apps-page-" + (persian ? "fa" : "en") + ".png");
            }
            U<Button>("CleanupBack").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await RefreshRowsAsync();
            if (cleanupPageOpen || !U<Grid>("Workspace").IsVisible || snapshot != original) throw new Exception("Back from cleanup did not retain the scanned file tree.");
            string root = original.Root.Path;
            string target = CleanupTests.Put(root, @"Reviewed cleanup\remove.txt", "reviewed deletion fixture");
            string sibling = CleanupTests.Put(root, @"Unrelated cleanup sibling\keep.txt", "saved sibling");
            CleanupTests.Settle(Path.GetDirectoryName(target)); CleanupTests.Settle(Path.GetDirectoryName(sibling));
            await ScanAsync(root, true);
            long savedSibling = snapshot.Entries.Single(e => e.Path == sibling).Bytes;
            File.AppendAllText(sibling, " changed independently after the scan");
            var selected = new CleanupItem { Path = target, Entry = snapshot.Entries.Single(e => e.Path == target), Source = snapshot, Owner = "Generated test file", Evidence = "Generated fixture only", Chosen = true };
            Exception confirmFailure = null;
            var confirmed = ShowCleanupReview(new List<CleanupItem> { selected }, null, dialog => {
                try
                {
                    dialog.ShowInTaskbar = false;
                    var ack = ReviewControl<CheckBox>(dialog, "ReviewAcknowledge"); ack.IsChecked = true; ack.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    ReviewControl<Button>(dialog, "ReviewApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                catch (Exception e) { confirmFailure = e; dialog.Close(); }
            });
            if (confirmFailure != null) throw confirmFailure;
            if (!confirmed.Confirmed || !confirmed.Permanent) throw new Exception("Explicit permanent confirmation was not captured.");
            selected.Source = new Snapshot { Root = snapshot.Root, Entries = snapshot.Entries };
            cache.MarkChanged(new [] { target });
            var result = await CleanupEngine.Execute(confirmed.Items, confirmed.Permanent, IntPtr.Zero, CancellationToken.None);
            if (File.Exists(target) || !result.All(r => r.StartsWith("✓"))) throw new Exception("Confirmed fixture deletion failed: " + String.Join(" | ", result));
            await ReconcileCleanup(new [] { target }, false);
            if (snapshot.Entries.Any(e => e.Path == target) || snapshot.IsStale || snapshot.Entries.Single(e => e.Path == sibling).Bytes != savedSibling) throw new Exception("Post-cleanup reconciliation widened to an unrelated sibling or retained the deleted file.");
            var reopened = new SnapshotCache(cache.DirectoryPath).Find(root, CancellationToken.None);
            if (reopened == null || reopened.Snapshot.IsStale || reopened.Snapshot.Entries.Any(e => e.Path == target)) throw new Exception("Cleanup result was not persisted for restart.");
            await CheckSelectionReviewWorkflow(root);
            cleanupRoots = roots; cleanupItems.Clear(); Window.Width = width; Window.Height = height; L.Persian = language; ApplyLanguage();
            File.WriteAllText(prefix + ".cleanup.ok.txt", "PASS: EN/FA cache and installed-app full pages; short-display confirmation; full preview contents; permanent acknowledgement; shared caches unchecked; Cancel preserves fixtures; actual selection-review workflow supersedes automatic refresh, revalidates stale selections and permanently deletes only a confirmed fixture; reconciliation and restart persistence. No real user data deleted or applications uninstalled.");
        }
        private async Task CheckSelectionReviewWorkflow(string root)
        {
            string target = CleanupTests.Put(root, @"Responsive cleanup\selected.txt");
            await ScanAsync(root, true);
            Navigate(snapshot.Entries.Single(e => e.Path == Path.GetDirectoryName(target))); await RefreshRowsAsync();
            U<DataGrid>("FilesGrid").SelectedItem = U<DataGrid>("FilesGrid").Items.Cast<Row>().Single(r => r.Entry.Path == target);
            snapshot.IsStale = stale = true; File.AppendAllText(target, " latest preview contents");
            StartLiveUpdates(); liveTimer.Stop();
            bool reviewed = false; Exception failure = null;
            using (var held = File.Open(cache.FileFor(root), FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var automatic = RefreshSnapshotAsync(false); await Task.Delay(120);
                if (!refreshingAutomatically || !U<Button>("RecycleButton").IsEnabled) throw new Exception("Automatic validation blocks deletion review.");
                await ReviewSelectedCleanup(dialog => {
                    try
                    {
                        var row = (CleanupItem)ReviewControl<DataGrid>(dialog, "ReviewItems").Items[0];
                        if (refreshingAutomatically || !row.CanChoose || row.Entry.Bytes != new FileInfo(target).Length || row.Source.IsStale) throw new Exception("Deletion review did not cancel automatic refresh and validate its own target.");
                        reviewed = true; ReviewControl<Button>(dialog, "ReviewCancel").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    }
                    catch (Exception e) { failure = e; dialog.Close(); }
                });
                await automatic;
            }
            if (failure != null) throw failure;
            if (!reviewed || !File.Exists(target)) throw new Exception("Cancelled selection review changed the fixture.");
            bool reported = false;
            var closeReport = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            closeReport.Tick += delegate {
                foreach (var dialog in Application.Current.Windows.Cast<Window>().Where(w => w.Owner == Window && (w.Title == L.T("گزارش پاک‌سازی") || w.Title == L.T("خطا"))).ToArray())
                { reported = dialog.Title == L.T("گزارش پاک‌سازی"); dialog.Close(); }
            };
            closeReport.Start();
            try
            {
                await ReviewSelectedCleanup(dialog => {
                    var acknowledge = ReviewControl<CheckBox>(dialog, "ReviewAcknowledge"); acknowledge.IsChecked = true; acknowledge.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
                    ReviewControl<Button>(dialog, "ReviewApply").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                });
            }
            finally { closeReport.Stop(); StopLiveUpdates(); liveEnabled = false; }
            if (!reported || File.Exists(target) || snapshot.Entries.Any(e => e.Path == target)) throw new Exception("Actual confirmed selection workflow failed to delete/reconcile a fixture from a stale saved view.");
        }
    }
}
