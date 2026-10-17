using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace Spacio
{
    public sealed class Entry
    {
        public string Path { get; set; }
        public string Name { get; set; }
        public Entry Parent { get; set; }
        public bool IsDirectory { get; set; }
        public FileAttributes Attributes { get; set; }
        public DateTime CreatedUtc { get; set; }
        public DateTime ModifiedUtc { get; set; }
        public DateTime LatestUtc { get; set; }
        public long Bytes { get; set; }
        public long FileCount { get; set; }
        public long FolderCount { get; set; }
        public int Issues { get; set; }
        public bool ContainsSystemEntries { get; set; }
        public long[] TypeBytes { get; set; }
        public string Note { get; set; }
        public List<Entry> Children { get; private set; }
        public Entry() { Children = new List<Entry>(); Note = ""; }
    }

    public sealed class ScanProgress
    {
        public string Path;
        public long Files, Folders, Bytes;
    }

    public sealed class Snapshot
    {
        public Entry Root;
        public List<Entry> Entries = new List<Entry>();
        public List<string> Errors = new List<string>();
        public int ErrorCount;
        public bool Cancelled;
        public bool IsStale;
        public DateTime StartedUtc = DateTime.UtcNow;
        public TimeSpan Elapsed;
    }

    public sealed class Scanner
    {
        // Directory enumeration runs on a worker. Junctions and cloud placeholders are never entered.
        public Snapshot Scan(string path, CancellationToken cancel, Action<ScanProgress> progress)
        {
            path = Paths.Normalize(path);
            Paths.RequireLocal(path);
            Paths.CheckAncestors(path);
            var rootInfo = new DirectoryInfo(path);
            if (!rootInfo.Exists) throw new DirectoryNotFoundException("پوشه پیدا نشد یا در دسترس نیست.");
            var watch = Stopwatch.StartNew();
            var snapshot = new Snapshot();
            snapshot.Root = ReadEntry(rootInfo, null);
            snapshot.Entries.Add(snapshot.Root);
            var pending = new Stack<Entry>();
            pending.Push(snapshot.Root);
            long files = 0, folders = 0, bytes = 0, lastReport = -500;
            while (pending.Count > 0 && !cancel.IsCancellationRequested)
            {
                var dir = pending.Pop();
                try
                {
                    foreach (var info in new DirectoryInfo(dir.Path).EnumerateFileSystemInfos())
                    {
                        if (cancel.IsCancellationRequested) break;
                        Entry child;
                        try { child = ReadEntry(info, dir); }
                        catch (Exception e)
                        {
                            if (!Expected(e)) throw;
                            child = new Entry { Path = info.FullName, Name = info.Name, Parent = dir, IsDirectory = info is DirectoryInfo };
                            AddIssue(snapshot, child, "خواندن مشخصات ممکن نیست: " + e.Message);
                        }
                        dir.Children.Add(child);
                        snapshot.Entries.Add(child);
                        bool special = (child.Attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0;
                        bool reserved = child.IsDirectory && (child.Name.Equals("$RECYCLE.BIN", StringComparison.OrdinalIgnoreCase) || child.Name.Equals("System Volume Information", StringComparison.OrdinalIgnoreCase));
                        if (special || reserved)
                        {
                            child.Bytes = 0;
                            child.FileCount = 0;
                            AddIssue(snapshot, child, reserved ? "پوشهٔ مدیریت‌شدهٔ ویندوز؛ اسکن نشد" : "پیوند یا فایل ابری؛ دنبال نشد");
                        }
                        else if (child.Issues == 0)
                        {
                            if (child.IsDirectory) { folders++; pending.Push(child); }
                            else { files++; bytes += child.Bytes; }
                        }
                        if (watch.ElapsedMilliseconds - lastReport > 180)
                        {
                            lastReport = watch.ElapsedMilliseconds;
                            if (progress != null) progress(new ScanProgress { Path = dir.Path, Files = files, Folders = folders, Bytes = bytes });
                        }
                    }
                }
                catch (Exception e)
                {
                    if (!Expected(e)) throw;
                    AddIssue(snapshot, dir, "دسترسی کامل ممکن نیست: " + e.Message);
                }
            }
            snapshot.Cancelled = cancel.IsCancellationRequested;
            if (snapshot.Cancelled) AddIssue(snapshot, snapshot.Root, "اسکن متوقف شد؛ نتیجه ناقص است");
            // Parents are always inserted before children, so reverse order aggregates without recursion.
            for (int i = snapshot.Entries.Count - 1; i > 0; i--)
            {
                var entry = snapshot.Entries[i];
                var parent = entry.Parent;
                parent.Bytes += entry.Bytes;
                parent.FileCount += entry.FileCount;
                parent.FolderCount += entry.FolderCount + (entry.IsDirectory ? 1 : 0);
                parent.Issues += entry.Issues;
                parent.ContainsSystemEntries |= entry.ContainsSystemEntries;
                if (entry.IsDirectory && entry.TypeBytes != null)
                    for (int category = 0; category < 7; category++) parent.TypeBytes[category] += entry.TypeBytes[category];
                else if (!entry.IsDirectory) parent.TypeBytes[Formats.CategoryIndex(entry)] += entry.Bytes;
                if (entry.LatestUtc > parent.LatestUtc) parent.LatestUtc = entry.LatestUtc;
            }
            snapshot.Elapsed = watch.Elapsed;
            return snapshot;
        }

        private static Entry ReadEntry(FileSystemInfo info, Entry parent)
        {
            info.Refresh();
            if (!info.Exists) throw new IOException("مورد در حین اسکن جابه‌جا شده است.");
            var attr = info.Attributes;
            var folder = (attr & FileAttributes.Directory) != 0;
            return new Entry {
                Path = Paths.Normalize(info.FullName), Name = info.Name, Parent = parent,
                IsDirectory = folder, Attributes = attr, CreatedUtc = info.CreationTimeUtc,
                ContainsSystemEntries = (attr & FileAttributes.System) != 0 || info.Name.Equals("AppData", StringComparison.OrdinalIgnoreCase),
                TypeBytes = folder ? new long[7] : null,
                ModifiedUtc = info.LastWriteTimeUtc, LatestUtc = info.LastWriteTimeUtc,
                Bytes = folder || (attr & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0 ? 0 : ((FileInfo)info).Length,
                FileCount = folder ? 0 : 1
            };
        }

        private static bool Expected(Exception e)
        {
            return e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException;
        }
        private static void AddIssue(Snapshot snapshot, Entry entry, string message)
        {
            entry.Issues++;
            entry.Note = message;
            snapshot.ErrorCount++;
            if (snapshot.Errors.Count < 500) snapshot.Errors.Add(entry.Path + " — " + message);
        }

        public static bool SameTree(Entry before, Entry after)
        {
            var queue = new Stack<Tuple<Entry, Entry>>();
            queue.Push(Tuple.Create(before, after));
            while (queue.Count > 0)
            {
                var pair = queue.Pop(); var a = pair.Item1; var b = pair.Item2;
                if (!Paths.Equal(a.Path, b.Path) || a.IsDirectory != b.IsDirectory || a.Bytes != b.Bytes ||
                    a.ModifiedUtc != b.ModifiedUtc || a.CreatedUtc != b.CreatedUtc || a.Attributes != b.Attributes ||
                    a.Issues != 0 || b.Issues != 0 || a.Children.Count != b.Children.Count) return false;
                var lookup = b.Children.ToDictionary(x => x.Name, StringComparer.OrdinalIgnoreCase);
                foreach (var child in a.Children)
                {
                    Entry other;
                    if (!lookup.TryGetValue(child.Name, out other)) return false;
                    queue.Push(Tuple.Create(child, other));
                }
            }
            return true;
        }

        public static void VerifyUnchanged(Entry entry, CancellationToken cancel)
        {
            Paths.CheckAncestors(entry.Path);
            if (entry.IsDirectory)
            {
                var fresh = new Scanner().Scan(entry.Path, cancel, null);
                cancel.ThrowIfCancellationRequested();
                if (!SameTree(entry, fresh.Root)) throw new IOException("محتوای پوشه از زمان اسکن تغییر کرده است. دوباره اسکن کنید.");
            }
            else
            {
                var fresh = ReadEntry(new FileInfo(entry.Path), entry.Parent);
                if (!SameTree(entry, fresh)) throw new IOException("فایل از زمان اسکن تغییر کرده است. دوباره اسکن کنید.");
            }
        }
    }

    public static class Paths
    {
        public static string Normalize(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) throw new ArgumentException("یک درایو یا پوشه انتخاب کنید.");
            string full = System.IO.Path.GetFullPath(path.Trim());
            string root = System.IO.Path.GetPathRoot(full);
            return full.Length == root.Length ? full : full.TrimEnd('\\', '/');
        }
        public static bool Equal(string a, string b) { return String.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase); }
        public static bool Within(string path, string root)
        {
            path = Normalize(path); root = Normalize(root);
            return Equal(path, root) || path.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase);
        }
        public static void RequireLocal(string path)
        {
            if (path.Length < 3 || path[1] != ':' || path[2] != '\\' || path.IndexOf(':', 2) >= 0)
                throw new IOException("در این نسخه یک مسیر محلی مثل C:\\ یا D:\\Data انتخاب کنید.");
            var drive = new DriveInfo(System.IO.Path.GetPathRoot(path));
            if (!drive.IsReady || (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable))
                throw new IOException("درایو محلی آماده نیست یا پشتیبانی نمی‌شود.");
        }
        public static void CheckAncestors(string path)
        {
            string current = Normalize(path);
            while (!String.IsNullOrEmpty(current))
            {
                var attributes = File.GetAttributes(current);
                if ((attributes & (FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0)
                    throw new IOException("مسیر شامل پیوند، junction یا فایل ابری است.");
                var parent = Directory.GetParent(current);
                current = parent == null ? null : parent.FullName;
            }
        }
    }

    public static class Safety
    {
        public static string BlockReason(Entry entry, Snapshot snapshot)
        {
            if (snapshot == null || snapshot.Cancelled) return "ابتدا اسکن کامل انجام دهید";
            if (snapshot.IsStale) return "داده‌ها تغییر کرده‌اند؛ دوباره اسکن کنید";
            if (!Paths.Within(entry.Path, snapshot.Root.Path) || Paths.Equal(entry.Path, snapshot.Root.Path)) return "ریشهٔ اسکن قابل حذف نیست";
            // Require membership by object identity, not by a caller-provided path alone.
            var ancestor = entry;
            while (ancestor.Parent != null) ancestor = ancestor.Parent;
            if (!Object.ReferenceEquals(ancestor, snapshot.Root)) return "مورد به این اسکن تعلق ندارد";
            if (entry.Issues > 0) return "اسکن ناقص یا مسیر پیوندی";
            if (entry.ContainsSystemEntries) return "مسیر محافظت‌شدهٔ سیستم";
            if ((entry.Attributes & (FileAttributes.System | FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0) return "فایل سیستمی، ابری یا پیوندی";
            var roots = new [] {
                Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                AppDomain.CurrentDomain.BaseDirectory
            };
            foreach (var root in roots)
                if (!String.IsNullOrEmpty(root) && (Paths.Within(entry.Path, root) || Paths.Within(root, entry.Path)))
                    return "مسیر محافظت‌شده؛ از تنظیمات ویندوز مدیریت شود";
            string disk = System.IO.Path.GetPathRoot(entry.Path);
            foreach (var name in new [] { "Windows", "Program Files", "Program Files (x86)", "ProgramData", "Recovery", "$RECYCLE.BIN", "System Volume Information", "Boot", "EFI", "MSOCache" })
            {
                string root = System.IO.Path.Combine(disk, name);
                if (Paths.Within(entry.Path, root) || Paths.Within(root, entry.Path)) return "مسیر محافظت‌شدهٔ سیستم";
            }
            var parent = entry;
            while (parent != null)
            {
                if (parent.Name.Equals("AppData", StringComparison.OrdinalIgnoreCase)) return "داده‌های برنامه؛ از تنظیمات برنامه مدیریت شود";
                parent = parent.Parent;
            }
            if (entry.IsDirectory && entry.Children.Any(x => x.Name.Equals("AppData", StringComparison.OrdinalIgnoreCase))) return "پوشهٔ پروفایل کاربر";
            if (entry.IsDirectory && Paths.Equal(entry.Path, System.IO.Path.Combine(disk, "Users"))) return "پوشهٔ کاربران";
            return null;
        }

        public static List<Entry> CollapseSelection(IEnumerable<Entry> source)
        {
            var selected = new HashSet<Entry>(source);
            return selected.Where(e => { var parent = e.Parent; while (parent != null) { if (selected.Contains(parent)) return false; parent = parent.Parent; } return true; }).ToList();
        }
    }

    public static class Formats
    {
        public static readonly string[] CategoryNames = { "ویدیو", "تصویر", "صدا", "آرشیو", "سند", "برنامه", "سایر" };
        private static readonly Dictionary<string, int> Extensions = BuildExtensions();
        private static Dictionary<string, int> BuildExtensions()
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            string[] groups = { ".mp4 .mkv .mov .avi .webm .wmv", ".jpg .jpeg .png .gif .webp .heic .svg .raw", ".mp3 .wav .flac .m4a .ogg .aac", ".zip .rar .7z .tar .gz .iso", ".pdf .doc .docx .xls .xlsx .pptx .txt .md", ".exe .msi .dll .sys" };
            for (int i = 0; i < groups.Length; i++) foreach (string extension in groups[i].Split(' ')) map[extension] = i;
            return map;
        }
        public static int CategoryIndex(Entry entry)
        {
            int index; return Extensions.TryGetValue(System.IO.Path.GetExtension(entry.Name), out index) ? index : 6;
        }
        public static string Size(long value)
        {
            double size = value; string[] units = { "B", "KiB", "MiB", "GiB", "TiB", "PiB" }; int i = 0;
            while (size >= 1024 && i < units.Length - 1) { size /= 1024; i++; }
            return size.ToString(i == 0 ? "N0" : "0.##", CultureInfo.InvariantCulture) + " " + units[i];
        }
        public static string Date(DateTime utc) { return utc == DateTime.MinValue ? "—" : utc.ToLocalTime().ToString("yyyy/MM/dd", CultureInfo.InvariantCulture); }
        public static string Category(Entry entry)
        {
            if (entry.IsDirectory) return "پوشه";
            return CategoryNames[CategoryIndex(entry)];
        }
        public static string Csv(string value)
        {
            value = value ?? "";
            // A CSV opened in Excel must not execute a formula from a filename.
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        public static void Export(string path, IEnumerable<Entry> entries, CancellationToken cancel)
        {
            using (var writer = new StreamWriter(path, false, new UTF8Encoding(true)))
            {
                writer.WriteLine("Path,Kind,LogicalBytes,Files,Folders,CreatedUTC,ModifiedUTC,LatestContentUTC,Issues,Note");
                foreach (var e in entries)
                {
                    cancel.ThrowIfCancellationRequested();
                    writer.WriteLine(String.Join(",", new [] { Csv(e.Path), Csv(e.IsDirectory ? "Folder" : "File"), e.Bytes.ToString(CultureInfo.InvariantCulture), e.FileCount.ToString(), e.FolderCount.ToString(), Csv(e.CreatedUtc.ToString("o")), Csv(e.ModifiedUtc.ToString("o")), Csv(e.LatestUtc.ToString("o")), e.Issues.ToString(), Csv(e.Note) }));
                }
            }
        }
    }
}
