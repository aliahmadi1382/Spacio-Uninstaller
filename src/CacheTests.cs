using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Spacio
{
    public static class CacheTests
    {
        public static int Run(string report)
        {
            var log = new List<string>();
            Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception("FAIL: " + label); log.Add("PASS: " + label); };
            string workspace = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(report))).FullName;
            string area = Path.Combine(workspace, ".test-data", "cache-" + Guid.NewGuid().ToString("N"));
            string root = Path.Combine(area, "Data"), cachePath = Path.Combine(area, "Saved scans"), child = Path.Combine(root, "پوشه فارسی");
            Directory.CreateDirectory(child);
            string file = Path.Combine(child, "یادداشت.txt"); File.WriteAllText(file, "original content");
            File.WriteAllText(Path.Combine(root, "music.mp3"), "music");
            try
            {
                var scanner = new Scanner(); var token = CancellationToken.None;
                var scan = scanner.Scan(root, token, null); var cache = new SnapshotCache(cachePath);
                check(cache.Find(root, token) == null, "A new path has no cached result");
                cache.Save(scan, token);
                check(File.Exists(cache.FileFor(root)), "Completed scan is saved on disk");
                check(Object.ReferenceEquals(cache.Find(root, token).Snapshot, scan), "Repeat lookup reuses the in-memory snapshot");
                var restarted = new SnapshotCache(cachePath); var restored = restarted.Find(root.ToUpperInvariant() + "\\", token);
                check(restored != null && Scanner.SameTree(scan.Root, restored.Snapshot.Root), "A new cache instance restores the full tree with normalized case and trailing slash");
                check(restored.Snapshot.Entries.Count == scan.Entries.Count && restored.Snapshot.StartedUtc == scan.StartedUtc, "Restoration preserves entry count and exact scan time");
                check(restored.Snapshot.Root.TypeBytes.SequenceEqual(scan.Root.TypeBytes), "Restoration preserves file type totals");
                check(restored.Snapshot.Entries.Skip(1).All(e => e.Parent != null && e.Parent.Children.Contains(e)), "Restoration reconnects parent and child references");
                check(restarted.Find(child, token).Directory.Name == "پوشه فارسی", "A descendant opens from its saved ancestor scan with Unicode names");
                check(restarted.Find(root + "-sibling", token) == null, "Similar path prefixes do not reuse unrelated snapshots");
                long savedBytes = restored.Snapshot.Root.Bytes;
                File.AppendAllText(file, " edited after scanning");
                var offline = new SnapshotCache(cachePath).Find(root, token);
                check(offline.Snapshot.Root.Bytes == savedBytes, "Opening a saved path does not rescan files changed on disk");
                bool changedBlocked = false;
                try { Scanner.VerifyUnchanged(offline.Snapshot.Entries.Single(e => Paths.Equal(e.Path, file)), token); } catch (IOException) { changedBlocked = true; }
                check(changedBlocked, "Cached metadata still blocks recycling a file changed since scanning");
                var refreshed = scanner.Scan(root, token, null); cache.Save(refreshed, token);
                check(new SnapshotCache(cachePath).Find(root, token).Snapshot.Root.Bytes > savedBytes, "An explicit new scan replaces the persisted result");
                var cancel = new CancellationTokenSource(); cancel.Cancel();
                var incomplete = scanner.Scan(root, cancel.Token, null); cache.Save(incomplete, token);
                check(new SnapshotCache(cachePath).Find(root, token).Snapshot.StartedUtc == refreshed.StartedUtc, "Cancelled scan does not overwrite a completed result");
                bool cancelled = false;
                try { new SnapshotCache(cachePath).Find(root, cancel.Token); } catch (OperationCanceledException) { cancelled = true; }
                check(cancelled, "Cache loading supports cancellation");
                var childScan = scanner.Scan(child, token, null); cache.Save(childScan, token);
                Thread.Sleep(20); var newerParent = scanner.Scan(root, token, null); cache.Save(newerParent, token);
                check(new SnapshotCache(cachePath).Find(child, token).Snapshot.StartedUtc == newerParent.StartedUtc, "A newer ancestor scan wins over an older exact-folder cache");
                var approved = new Snapshot { Root = newerParent.Root, Entries = newerParent.Entries };
                cache.MarkChanged(new [] { file });
                var stale = new SnapshotCache(cachePath).Find(root, token);
                check(stale.Snapshot.IsStale && new SnapshotCache(cachePath).Find(child, token).Snapshot.IsStale, "Cleanup invalidates overlapping caches across restarts");
                check(Safety.BlockReason(stale.Snapshot.Entries.Single(e => Paths.Equal(e.Path, file)), stale.Snapshot) != null, "Invalidated cache cannot authorize another cleanup");
                check(Safety.BlockReason(approved.Entries.Single(e => Paths.Equal(e.Path, file)), approved) == null, "Approved cleanup remains valid while its displayed cache is invalidated");
                Thread.Sleep(20); var afterCleanup = scanner.Scan(root, token, null); cache.Save(afterCleanup, token);
                check(!new SnapshotCache(cachePath).Find(root, token).Snapshot.IsStale, "Explicit rescan clears an older invalidation marker");
                var withIssues = scanner.Scan(root, token, null); withIssues.ErrorCount = 1; withIssues.Root.Issues = 1; withIssues.Errors.Add("A reported access error"); cache.Save(withIssues, token);
                var restoredIssues = new SnapshotCache(cachePath).Find(root, token).Snapshot;
                check(restoredIssues.ErrorCount == 1 && restoredIssues.Root.Issues == 1 && restoredIssues.Errors.Single() == "A reported access error", "Skipped-item counts and error messages survive persistence");
                byte[] valid = File.ReadAllBytes(cache.FileFor(root));
                File.WriteAllBytes(cache.FileFor(root), valid.Take(valid.Length / 2).ToArray());
                check(new SnapshotCache(cachePath).Find(root, token) == null, "Truncated snapshot is rejected without returning partial data");
                File.WriteAllBytes(cache.FileFor(root), new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
                check(new SnapshotCache(cachePath).Find(root, token) == null, "Unknown cache format is ignored safely");
                cache.Save(afterCleanup, token);
                check(new SnapshotCache(cachePath).Find(root, token) != null, "A new scan can replace a corrupt cache atomically");
                check(!Directory.EnumerateFiles(cachePath, "*.tmp").Any(), "Atomic writes leave no temporary files behind");
                log.Add("All " + log.Count + " cache checks passed.");
                File.WriteAllLines(report, log, new System.Text.UTF8Encoding(true)); return 0;
            }
            catch (Exception e) { log.Add(e.ToString()); File.WriteAllLines(report, log, new System.Text.UTF8Encoding(true)); return 1; }
        }
    }
}
