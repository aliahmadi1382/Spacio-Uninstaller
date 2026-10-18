using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace Spacio
{
    // Only metadata is read. Each refresh builds a separate tree on a worker; the displayed
    // tree and the last good cache remain intact until the replacement is complete.
    public static class LiveScanner
    {
        private static bool SameMetadata(Entry a, Entry b)
        {
            return a.IsDirectory == b.IsDirectory && a.Attributes == b.Attributes &&
                a.CreatedUtc == b.CreatedUtc && a.ModifiedUtc == b.ModifiedUtc &&
                (a.IsDirectory || a.Bytes == b.Bytes) && a.Note == b.Note;
        }
        public static Snapshot RefreshFolder(Snapshot before, string path, CancellationToken token, Action<ScanProgress> progress)
        {
            path = Paths.Normalize(path); token.ThrowIfCancellationRequested();
            if (!Paths.Within(path, before.Root.Path)) throw new ArgumentException("Folder is outside the saved scan.");
            if (Paths.Equal(path, before.Root.Path)) return Refresh(before, null, token, progress);
            // A newly entered path absent from this snapshot gets its own scan, never a scan
            // of a broader ancestor just to discover it.
            if (!before.Entries.Any(e => { token.ThrowIfCancellationRequested(); return e.IsDirectory && String.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase); }))
            {
                var fresh = new Scanner().Scan(path, token, progress); token.ThrowIfCancellationRequested(); return fresh;
            }
            return Refresh(before, new [] { path }, token, progress, true);
        }
        public static Snapshot Refresh(Snapshot before, string[] changes, CancellationToken token, Action<ScanProgress> progress, bool recursivePaths = false)
        {
            token.ThrowIfCancellationRequested();
            Paths.RequireLocal(before.Root.Path); Paths.CheckAncestors(before.Root.Path);
            if (changes == null)
            {
                var fresh = new Scanner().Scan(before.Root.Path, token, progress);
                token.ThrowIfCancellationRequested();
                fresh = PreserveUnavailable(fresh, before.Root, token);
                // Preserve the original cache bytes and timestamp when nothing actually changed.
                if (!before.IsStale && Equivalent(before, fresh, token)) return before;
                return fresh;
            }
            var index = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in before.Entries) { token.ThrowIfCancellationRequested(); index[entry.Path] = entry; }
            var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string change in changes)
            {
                string path = Paths.Normalize(change);
                if (!Paths.Within(path, before.Root.Path)) continue;
                // A root Changed notification is metadata, not an overflow signal.
                // Full validation is requested explicitly; noisy root events must not
                // repeatedly enumerate an otherwise unchanged drive.
                if (recursivePaths && Paths.Equal(path, before.Root.Path)) return Refresh(before, null, token, progress);
                if (Paths.Equal(path, before.Root.Path)) { targets.Add(path); continue; }
                // If a whole new tree arrived, read its first unknown ancestor once.
                string parent = Path.GetDirectoryName(path);
                while (parent != null && !index.ContainsKey(parent)) { path = parent; parent = Path.GetDirectoryName(path); }
                targets.Add(path);
            }
            var replacementsByPath = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var covered = new List<string>();
            foreach (string path in targets.OrderBy(p => p.Length))
            {
                token.ThrowIfCancellationRequested();
                if (covered.Any(p => Paths.Within(path, p))) continue;
                Entry old; index.TryGetValue(path, out old);
                // Do not enter a skipped directory because one of its descendants generated an event.
                string ancestor = Path.GetDirectoryName(path); bool skipped = false;
                while (ancestor != null && index.ContainsKey(ancestor))
                {
                    var directory = index[ancestor];
                    if ((directory.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0 || Reserved(directory)) { skipped = true; break; }
                    ancestor = Path.GetDirectoryName(ancestor);
                }
                if (skipped) continue;
                Entry next = null;
                try
                {
                    var attributes = File.GetAttributes(path);
                    Paths.CheckAncestors(Path.GetDirectoryName(path) ?? path);
                    bool folder = (attributes & FileAttributes.Directory) != 0;
                    next = Scanner.ReadEntry(folder ? (FileSystemInfo)new DirectoryInfo(path) : new FileInfo(path), null);
                    if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0 || Reserved(next))
                    {
                        next.Bytes = 0; next.FileCount = 0;
                        next.Note = Reserved(next) ? "پوشهٔ مدیریت‌شدهٔ ویندوز؛ اسکن نشد" : "پیوند یا فایل ابری؛ دنبال نشد";
                    }
                    else if (folder)
                    {
                        // LastWrite notifications on an existing directory do not imply changes
                        // to every descendant. Child notifications (or overflow recovery) cover those.
                        if (!recursivePaths && old != null && old.IsDirectory && old.Note.Length == 0 && old.CreatedUtc == next.CreatedUtc && old.Attributes == next.Attributes)
                        {
                            if (SameMetadata(old, next)) continue;
                            next.Children.AddRange(old.Children);
                        }
                        else
                        {
                            var subtree = new Scanner().Scan(path, token, progress); token.ThrowIfCancellationRequested();
                            subtree = PreserveUnavailable(subtree, old, token);
                            next = subtree.Root;
                            covered.Add(path);
                            if (old != null && EquivalentEntries(old, next, token)) continue;
                        }
                    }
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
                // Access denied and sharing errors abort the transaction, retaining the good snapshot.
                if (old == null && next == null) continue;
                if (next == null || !next.IsDirectory) covered.Add(path);
                if (old != null && next != null && !next.IsDirectory && SameMetadata(old, next)) continue;
                replacementsByPath[path] = next;
            }
            if (replacementsByPath.Count == 0) return before;
            // Update ancestor dates too; these are cheap metadata reads, never recursive rescans.
            foreach (string path in replacementsByPath.Keys.ToArray())
            {
                string parent = Path.GetDirectoryName(path);
                while (parent != null && index.ContainsKey(parent))
                {
                    if (!replacementsByPath.ContainsKey(parent))
                    {
                        var next = Scanner.ReadEntry(new DirectoryInfo(parent), null);
                        next.Note = index[parent].Note; next.Children.AddRange(index[parent].Children);
                        replacementsByPath[parent] = next;
                    }
                    parent = Path.GetDirectoryName(parent);
                }
            }
            var rebuilt = Rebuild(before, replacementsByPath, token);
            if (recursivePaths) rebuilt.IsStale = before.IsStale;
            return rebuilt;
        }
        private static Snapshot PreserveUnavailable(Snapshot fresh, Entry previous, CancellationToken token)
        {
            // A transient access failure is not evidence that saved contents disappeared.
            var unavailable = fresh.Entries.Where(e => e.Note.StartsWith("دسترسی کامل ممکن نیست", StringComparison.Ordinal) || e.Note.StartsWith("خواندن مشخصات ممکن نیست", StringComparison.Ordinal)).ToList();
            if (unavailable.Any(e => e == fresh.Root)) throw new IOException("پوشه پیدا نشد یا در دسترس نیست.");
            if (previous == null || unavailable.Count == 0) return fresh;
            var notes = unavailable.ToDictionary(e => e.Path, e => e.Note, StringComparer.OrdinalIgnoreCase);
            var replacements = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            var pending = new Stack<Entry>(); pending.Push(previous);
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested(); var old = pending.Pop(); string note;
                if (notes.TryGetValue(old.Path, out note))
                {
                    var retained = Copy(old, null); retained.Note = note; retained.Children.AddRange(old.Children);
                    replacements[old.Path] = retained;
                }
                foreach (var child in old.Children) pending.Push(child);
            }
            return replacements.Count == 0 ? fresh : Rebuild(fresh, replacements, token);
        }
        private static bool Reserved(Entry entry)
        {
            return entry.IsDirectory && (entry.Name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) || entry.Name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase));
        }
        private static Entry Copy(Entry source, Entry parent)
        {
            return new Entry { Path = source.Path, Name = source.Name, Parent = parent, IsDirectory = source.IsDirectory,
                Attributes = source.Attributes, CreatedUtc = source.CreatedUtc, ModifiedUtc = source.ModifiedUtc,
                LatestUtc = source.ModifiedUtc, Bytes = source.IsDirectory ? 0 : source.Bytes,
                FileCount = source.IsDirectory ? 0 : source.FileCount, TypeBytes = source.IsDirectory ? new long[7] : null,
                Note = source.Note, Issues = String.IsNullOrEmpty(source.Note) ? 0 : 1,
                ContainsSystemEntries = (source.Attributes & FileAttributes.System) != 0 || source.Name.Equals("AppData", StringComparison.OrdinalIgnoreCase) };
        }
        private static Snapshot Rebuild(Snapshot before, Dictionary<string, Entry> replacements, CancellationToken token)
        {
            var additions = replacements.Where(p => p.Value != null).GroupBy(p => Path.GetDirectoryName(p.Key) ?? "", StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Select(p => p.Value).ToList(), StringComparer.OrdinalIgnoreCase);
            var result = new Snapshot();
            var pending = new Stack<Tuple<Entry, Entry>>(); pending.Push(Tuple.Create(before.Root, (Entry)null));
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested();
                var pair = pending.Pop(); Entry source = pair.Item1, replacement;
                if (replacements.TryGetValue(source.Path, out replacement)) source = replacement;
                if (source == null) continue;
                var entry = Copy(source, pair.Item2);
                if (entry.Parent == null) result.Root = entry; else entry.Parent.Children.Add(entry);
                result.Entries.Add(entry);
                if (entry.Issues > 0) { result.ErrorCount++; if (result.Errors.Count < 500) result.Errors.Add(entry.Path + " — " + entry.Note); }
                if (!entry.IsDirectory) continue;
                List<Entry> extra;
                if (additions.TryGetValue(entry.Path, out extra))
                {
                    var names = new HashSet<string>(source.Children.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
                    foreach (var child in extra) if (!names.Contains(child.Path)) pending.Push(Tuple.Create(child, entry));
                }
                foreach (var child in source.Children) pending.Push(Tuple.Create(child, entry));
            }
            for (int i = result.Entries.Count - 1; i > 0; i--)
            {
                token.ThrowIfCancellationRequested(); var entry = result.Entries[i]; var parent = entry.Parent;
                parent.Bytes += entry.Bytes; parent.FileCount += entry.FileCount;
                parent.FolderCount += entry.FolderCount + (entry.IsDirectory ? 1 : 0);
                parent.Issues += entry.Issues; parent.ContainsSystemEntries |= entry.ContainsSystemEntries;
                if (entry.IsDirectory) for (int c = 0; c < 7; c++) parent.TypeBytes[c] += entry.TypeBytes[c];
                else parent.TypeBytes[Formats.CategoryIndex(entry)] += entry.Bytes;
                if (entry.LatestUtc > parent.LatestUtc) parent.LatestUtc = entry.LatestUtc;
            }
            result.Elapsed = DateTime.UtcNow - result.StartedUtc;
            return result;
        }
        private static bool Equivalent(Snapshot a, Snapshot b, CancellationToken token)
        {
            if (a.Entries.Count != b.Entries.Count || a.Root.Bytes != b.Root.Bytes || a.ErrorCount != b.ErrorCount) return false;
            return EquivalentEntries(a.Root, b.Root, token);
        }
        private static bool EquivalentEntries(Entry a, Entry b, CancellationToken token)
        {
            var pending = new Stack<Tuple<Entry, Entry>>(); pending.Push(Tuple.Create(a, b));
            while (pending.Count > 0)
            {
                token.ThrowIfCancellationRequested(); var pair = pending.Pop();
                if (!SameMetadata(pair.Item1, pair.Item2) || pair.Item1.Children.Count != pair.Item2.Children.Count) return false;
                var children = pair.Item2.Children.ToDictionary(e => e.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var child in pair.Item1.Children)
                {
                    Entry other; if (!children.TryGetValue(child.Name, out other)) return false;
                    pending.Push(Tuple.Create(child, other));
                }
            }
            return true;
        }
    }

    // Watcher callbacks only enqueue names; no disk access or dispatcher posts per event.
    public sealed class LiveMonitor : IDisposable
    {
        private readonly object gate = new object();
        private readonly HashSet<string> pending = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly string[] excluded;
        private FileSystemWatcher watcher;
        private bool full, failed, disposed;
        private DateTime firstPending, lastPending;
        public readonly string Root;
        public LiveMonitor(string root, params string[] excludedPaths)
        {
            Root = Paths.Normalize(root); excluded = excludedPaths;
            watcher = new FileSystemWatcher(Root) { IncludeSubdirectories = true, InternalBufferSize = 32768,
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.Size | NotifyFilters.LastWrite | NotifyFilters.Attributes | NotifyFilters.CreationTime };
            watcher.Created += Changed; watcher.Deleted += Changed; watcher.Changed += Changed;
            watcher.Renamed += (s, e) => { Enqueue(e.OldFullPath); Enqueue(e.FullPath); };
            watcher.Error += (s, e) => { lock (gate) { if (disposed) return; full = true; failed = !(e.GetException() is InternalBufferOverflowException); pending.Clear(); } };
            try { watcher.EnableRaisingEvents = true; }
            catch { watcher.Dispose(); watcher = null; throw; }
        }
        private void Changed(object sender, FileSystemEventArgs e) { Enqueue(e.FullPath); }
        public void Enqueue(string path)
        {
            if (excluded.Any(p => !String.IsNullOrEmpty(p) && (String.Equals(path, p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase)))) return;
            lock (gate)
            {
                if (disposed || full) return;
                var now = DateTime.UtcNow; if (pending.Count == 0) firstPending = now; lastPending = now;
                if (pending.Count >= 4096) { full = true; pending.Clear(); } else pending.Add(path);
            }
        }
        public bool Failed { get { lock (gate) return failed; } }
        public bool HasPending { get { lock (gate) return full || pending.Count > 0; } }
        public bool Ready { get { lock (gate) return full || (pending.Count > 0 && ((DateTime.UtcNow - lastPending).TotalMilliseconds >= 800 || (DateTime.UtcNow - firstPending).TotalSeconds >= 5)); } }
        public void RequireFull() { lock (gate) { full = true; pending.Clear(); } }
        public string[] Take()
        {
            lock (gate) { var result = full ? null : pending.ToArray(); full = false; pending.Clear(); return result; }
        }
        public void Dispose() { lock (gate) disposed = true; if (watcher != null) { watcher.Dispose(); watcher = null; } }
    }
}
