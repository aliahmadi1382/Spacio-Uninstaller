using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Spacio
{
    public static class LiveTests
    {
        public static int Run(string report)
        {
            var log = new List<string>();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception("FAIL: " + label); log.Add("PASS: " + label); };
            string workspace = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(report))).FullName;
            string area = Path.Combine(workspace, ".test-data", "live-" + Guid.NewGuid().ToString("N"));
            string root = Path.Combine(area, "Data"), folder = Path.Combine(root, "پوشه فارسی");
            Directory.CreateDirectory(folder);
            string first = Path.Combine(folder, "first.txt"), second = Path.Combine(folder, "second.mp3");
            File.WriteAllText(first, "before"); File.WriteAllText(second, "music");
            var token = CancellationToken.None;
            try
            {
                var baseline = new Scanner().Scan(root, token, null);
                var cache = new SnapshotCache(Path.Combine(area, "Cache")); cache.Save(baseline, token);
                byte[] original = File.ReadAllBytes(cache.FileFor(root));
                var unchanged = LiveScanner.Refresh(baseline, null, token, null);
                check(Object.ReferenceEquals(baseline, unchanged), "No-change validation preserves the original snapshot and timestamp" +
                    (Object.ReferenceEquals(baseline, unchanged) ? "" : " / " + String.Join("; ", baseline.Entries.Join(unchanged.Entries, a => a.Path, b => b.Path, (a, b) => a.Name + ": " + a.Attributes + " -> " + b.Attributes + ", created " + a.CreatedUtc.Ticks + " -> " + b.CreatedUtc.Ticks + ", modified " + a.ModifiedUtc.Ticks + " -> " + b.ModifiedUtc.Ticks + ", bytes " + a.Bytes + " -> " + b.Bytes))));
                File.WriteAllText(first, "larger changed document"); File.Delete(second);
                string added = Path.Combine(folder, "added.bin"); File.WriteAllText(added, "new content");
                var updated = LiveScanner.Refresh(baseline, new [] { first, second, added, folder }, token, null);
                check(Scanner.SameTree(updated.Root, new Scanner().Scan(root, token, null).Root), "Incremental resize, deletion, creation and parent notifications match a full scan");
                check(baseline.Root.Bytes == 11 && baseline.Entries.Any(e => e.Path == second), "Preparing changes never mutates the displayed snapshot");
                check(updated.Root.TypeBytes.Sum() == updated.Root.Bytes && updated.Root.FileCount == 2 && updated.Root.FolderCount == 1, "Incremental aggregate bytes, types and counts are consistent");
                check(Safety.BlockReason(updated.Entries.Single(e => e.Path == first), updated) == null, "Updated entries retain identity membership for cleanup safety");
                string renamed = Path.Combine(root, "renamed"); Directory.Move(folder, renamed);
                var moved = LiveScanner.Refresh(updated, new [] { folder, renamed, Path.Combine(renamed, "first.txt") }, token, null);
                check(Scanner.SameTree(moved.Root, new Scanner().Scan(root, token, null).Root), "Directory rename preserves contents under the new path and removes the old tree");
                string nested = Path.Combine(root, "new", "deep", "file.bin"); Directory.CreateDirectory(Path.GetDirectoryName(nested)); File.WriteAllText(nested, "deep");
                var tree = LiveScanner.Refresh(moved, new [] { nested }, token, null);
                check(Scanner.SameTree(tree.Root, new Scanner().Scan(root, token, null).Root), "A nested arrival reads its unknown ancestor and reconstructs all parent links");
                File.AppendAllText(Path.Combine(renamed, "first.txt"), "unreported edit");
                File.AppendAllText(nested, "reported edit");
                var partial = LiveScanner.Refresh(tree, new [] { nested }, token, null);
                check(partial.Entries.Single(e => e.Name == "first.txt").Bytes == tree.Entries.Single(e => e.Name == "first.txt").Bytes, "An ordinary event does not re-enumerate unrelated subtrees");
                File.AppendAllText(nested, "another off-scope edit");
                long previousRootBytes = partial.Root.Bytes;
                var scoped = LiveScanner.RefreshFolder(partial, renamed, token, null);
                check(scoped.Entries.Single(e => e.Path == nested).Bytes == partial.Entries.Single(e => e.Path == nested).Bytes && scoped.Entries.Single(e => e.Name == "first.txt").Bytes == new FileInfo(Path.Combine(renamed, "first.txt")).Length,
                    "Folder Refresh recursively updates its descendants without reading an edited sibling");
                check(scoped.Root.Bytes == previousRootBytes + new FileInfo(Path.Combine(renamed, "first.txt")).Length - partial.Entries.Single(e => e.Name == "first.txt").Bytes,
                    "Folder Refresh updates ancestor totals using saved sibling data");
                check(Object.ReferenceEquals(scoped, LiveScanner.RefreshFolder(scoped, renamed, token, null)), "Unchanged folder Refresh retains the original snapshot and timestamp");
                string entered = Path.Combine(root, "unseen", "only-this-folder"); Directory.CreateDirectory(entered); File.WriteAllText(Path.Combine(entered, "entered.txt"), "entered");
                var enteredScan = LiveScanner.RefreshFolder(scoped, entered, token, null);
                check(Paths.Equal(enteredScan.Root.Path, entered) && enteredScan.Root.FileCount == 1, "A newly entered uncached path is scanned exactly, without widening to an ancestor");
                var reconciled = LiveScanner.Refresh(partial, null, token, null);
                check(Scanner.SameTree(reconciled.Root, new Scanner().Scan(root, token, null).Root), "Startup and missed-event validation reconcile off-time changes");
                cache.Save(reconciled, token); original = File.ReadAllBytes(cache.FileFor(root));
                var cancel = new CancellationTokenSource(); cancel.Cancel(); bool cancelled = false;
                try { LiveScanner.Refresh(reconciled, null, cancel.Token, null); } catch (OperationCanceledException) { cancelled = true; }
                check(cancelled && original.SequenceEqual(File.ReadAllBytes(cache.FileFor(root))), "Cancellation leaves the saved cache bytes intact");
                Directory.Move(root, root + "-offline"); bool offline = false;
                try { LiveScanner.Refresh(reconciled, null, token, null); } catch (IOException) { offline = true; }
                finally { Directory.Move(root + "-offline", root); }
                check(offline && original.SequenceEqual(File.ReadAllBytes(cache.FileFor(root))), "An unavailable root never replaces saved contents with an empty result");
                var originalAcl = Directory.GetAccessControl(renamed);
                try
                {
                    var restricted = Directory.GetAccessControl(renamed);
                    restricted.AddAccessRule(new FileSystemAccessRule(WindowsIdentity.GetCurrent().User, FileSystemRights.ListDirectory, AccessControlType.Deny));
                    Directory.SetAccessControl(renamed, restricted);
                    var retained = LiveScanner.Refresh(reconciled, null, token, null);
                    check(retained.Root.Bytes == reconciled.Root.Bytes && retained.Root.Issues > 0 && retained.Entries.Any(e => e.Name == "first.txt"), "Newly inaccessible contents retain their known sizes and are marked incomplete");
                }
                finally
                {
                    // SetAccessControl persists modified sections only; a freshly read ACL has
                    // no dirty sections, so explicitly mark the restored access section dirty.
                    var restoredAcl = new DirectorySecurity();
                    restoredAcl.SetSecurityDescriptorSddlForm(originalAcl.GetSecurityDescriptorSddlForm(AccessControlSections.Access), AccessControlSections.Access);
                    Directory.SetAccessControl(renamed, restoredAcl);
                }
                check(new Scanner().Scan(root, token, null).ErrorCount == 0, "The isolated access-denied fixture restores its original permissions");
                string longFolder = root;
                while (longFolder.Length < 280) longFolder = Path.Combine(longFolder, "long-directory-123456789");
                Directory.CreateDirectory(longFolder); string longFile = Path.Combine(longFolder, "فارسی.txt"); File.WriteAllText(longFile, "long path");
                var longTree = LiveScanner.Refresh(reconciled, new [] { longFile }, token, null);
                check(longTree.Entries.Any(e => e.Path == longFile && e.Bytes == 9), "Incremental arrival supports Unicode and paths beyond 260 characters");
                reconciled = LiveScanner.Refresh(longTree, null, token, null); cache.Save(reconciled, token);
                using (var monitor = new LiveMonitor(area, cache.DirectoryPath))
                {
                    monitor.Enqueue(cache.FileFor(root)); check(!monitor.HasPending, "Cache writes are excluded from the live notification queue");
                    monitor.Enqueue(first); monitor.Enqueue(first);
                    check(monitor.Take().Length == 1, "Duplicate notifications are batched once");
                    for (int i = 0; i < 4100; i++) monitor.Enqueue(Path.Combine(root, "event-" + i));
                    check(monitor.Take() == null, "A bounded queue falls back to full validation on overflow");
                }
                string deletable = Path.Combine(area, "Watched empty folder"); Directory.CreateDirectory(deletable);
                using (var monitor = new LiveMonitor(deletable))
                {
                    string movedWatched = deletable + "-moved";
                    Directory.Move(deletable, movedWatched); Directory.Delete(movedWatched);
                    check(!Directory.Exists(movedWatched), "Live monitoring does not lock its watched folder against rename or deletion");
                }
                // The UI suite below exercises actual OS events and the same startup hook used by users.
                var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
                var controller = new Controller(cache.DirectoryPath); app.MainWindow = controller.Window; Startup.HideForPreview(controller.Window);
                int result = 0;
                controller.Window.Loaded += async delegate {
                    try { await controller.CheckLiveUI(root, cache.FileFor(root), check); }
                    catch (Exception e) { log.Add(e.ToString()); result = 1; }
                    finally { controller.Window.Close(); }
                };
                app.Run(controller.Window);
                File.WriteAllLines(report, log); return result;
            }
            catch (Exception e) { log.Add(e.ToString()); File.WriteAllLines(report, log); return 1; }
        }
    }

    public sealed partial class Controller
    {
        private async Task AwaitLive(Func<bool> condition)
        {
            var watch = Stopwatch.StartNew();
            while (!condition()) { if (watch.Elapsed.TotalSeconds > 25) throw new Exception("Live reconciliation timed out: " + liveState + " / " + U<TextBlock>("StatusText").Text); await Task.Delay(40); }
        }
        public async Task CheckLiveUI(string root, string cacheFile, Action<bool, string> check)
        {
            string offlineFile = Path.Combine(root, "changed-while-closed.txt"); File.WriteAllText(offlineFile, "offline");
            await ScanAsync(root, false, true);
            check(viewingCached && !snapshot.Entries.Any(e => e.Path == offlineFile), "Startup first presents the saved snapshot without waiting for validation");
            StartLiveUpdates();
            await AwaitLive(() => !busy && snapshot.Entries.Any(e => e.Path == offlineFile));
            check(cacheSaved, "Startup automatically reconciles and saves changes made while closed");
            U<Button>("FilesNav").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); await RefreshRowsAsync();
            string renamed = offlineFile + ".moved"; File.Move(offlineFile, renamed);
            string added = Path.Combine(root, "watcher-created.bin"); File.WriteAllBytes(added, new byte[121]);
            await AwaitLive(() => !busy && snapshot.Entries.Any(e => e.Path == renamed) && snapshot.Entries.Any(e => e.Path == added));
            check(!snapshot.Entries.Any(e => e.Path == offlineFile), "Real Windows rename/create events update the table without clicking Refresh");
            File.WriteAllBytes(added, new byte[300]); File.Delete(renamed);
            await AwaitLive(() => !busy && !snapshot.Entries.Any(e => e.Path == renamed) && snapshot.Entries.Single(e => e.Path == added).Bytes == 300);
            var observed = new Scanner().Scan(root, CancellationToken.None, null);
            check(snapshot.Root.Bytes == observed.Root.Bytes, "Real delete/resize events update displayed byte totals (live=" + snapshot.Root.Bytes + ", disk=" + observed.Root.Bytes + ")");
            check(resultsPageOpen && filtered.Any(e => e.Path == added), "The full results page keeps receiving live filesystem changes");
            var rows = U<DataGrid>("FilesGrid").ItemsSource;
            var oldSnapshot = snapshot; byte[] cacheBefore = File.ReadAllBytes(cacheFile);
            File.AppendAllText(added, "force refresh");
            int beats = 0; var heartbeat = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            heartbeat.Tick += delegate { beats++; }; heartbeat.Start();
            using (var held = File.Open(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var task = RefreshSnapshotAsync(true);
                await Task.Delay(120);
                check(refreshing && Object.ReferenceEquals(rows, U<DataGrid>("FilesGrid").ItemsSource) && snapshot == oldSnapshot, "Refresh retains visible rows and the previous snapshot while saving on a worker");
                refreshCancel.Cancel(); await task;
            }
            heartbeat.Stop();
            check(beats >= 3, "The main UI dispatcher stays responsive during a blocked refresh write");
            check(snapshot == oldSnapshot && cacheBefore.SequenceEqual(File.ReadAllBytes(cacheFile)), "Stopping Refresh preserves both displayed results and exact saved bytes");
            await RefreshSnapshotAsync(true);
            check(snapshot.Root.Bytes > oldSnapshot.Root.Bytes, "A later Refresh completes after cancellation");
            liveMonitor.RequireFull(); await LiveTickAsync();
            L.Persian = false; ApplyLanguage(); check(U<TextBlock>("LiveStatus").Text.Contains("Live"), "Live status supports English");
            L.Persian = true; ApplyLanguage(); check(U<TextBlock>("LiveStatus").Text.Contains("زنده"), "Live status supports Persian");
            await Task.Delay(200);
            SavePreview(Path.Combine(Path.GetDirectoryName(cacheFile), "live-fa.png"));
            string newRoot = Path.Combine(Path.GetDirectoryName(root), "FirstScan"); Directory.CreateDirectory(newRoot);
            File.WriteAllText(Path.Combine(newRoot, "first.txt"), "first");
            await ScanAsync(newRoot);
            check(liveMonitor != null && liveMonitor.Take() != null, "The watcher starts before a first scan without scheduling a redundant full rescan");
            string newLiveFile = Path.Combine(newRoot, "live.txt"); File.WriteAllText(newLiveFile, "live");
            await AwaitLive(() => !busy && snapshot.Entries.Any(e => e.Path == newLiveFile));
            check(snapshot.Root.FileCount == 2, "Live monitoring follows a newly scanned path after switching roots");
            StopLiveUpdates();
            liveEnabled = false;
            string scope = Path.Combine(newRoot, "Only this folder"); Directory.CreateDirectory(scope);
            string inside = Path.Combine(scope, "inside.txt"), outside = Path.Combine(newRoot, "outside.txt");
            File.WriteAllText(inside, "before"); File.WriteAllText(outside, "outside");
            await ScanAsync(newRoot, true);
            var scopedBefore = snapshot;
            Navigate(snapshot.Entries.Single(e => e.Path == scope)); await RefreshRowsAsync();
            check(U<TextBox>("PathBox").Text == scope, "Opening a folder updates the Refresh path to the folder being viewed");
            File.AppendAllText(inside, " inside edit"); File.AppendAllText(outside, " unrelated edit");
            U<Button>("RescanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await AwaitLive(() => !busy);
            check(current.Path == scope && snapshot.Entries.Single(e => e.Path == inside).Bytes == new FileInfo(inside).Length && snapshot.Entries.Single(e => e.Path == outside).Bytes == 7,
                "The actual Refresh button refreshes the viewed folder and leaves edited siblings at their saved values");
            check(snapshot.Root.Path == newRoot && snapshot.Root.Bytes == scopedBefore.Root.Bytes + 12 && cacheSaved,
                "Scoped UI Refresh retains the ancestor scan and saves the updated totals");
            var persistedScope = new SnapshotCache(cache.DirectoryPath).Find(newRoot, CancellationToken.None).Snapshot;
            check(persistedScope.Entries.Single(e => e.Path == outside).Bytes == 7 && persistedScope.Entries.Single(e => e.Path == inside).Bytes == 18,
                "Scoped changes persist without replacing unrefreshed sibling metadata");
        }
    }
}
