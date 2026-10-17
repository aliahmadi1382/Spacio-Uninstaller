using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Spacio
{
    public sealed class CachedScan
    {
        public Snapshot Snapshot;
        public Entry Directory;
    }

    // A versioned data-only format; no object deserialization and no filesystem enumeration on a hit.
    public sealed class SnapshotCache
    {
        private const int Magic = 0x53504332, Version = 1, EndMarker = 0x454E4432;
        private readonly string folder;
        private readonly List<Snapshot> memory = new List<Snapshot>();
        public SnapshotCache(string directory = null)
        {
            folder = directory ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spacio", "Scans");
        }
        public string FileFor(string root)
        {
            using (var hash = SHA256.Create())
                return Path.Combine(folder, BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(Paths.Normalize(root).ToUpperInvariant()))).Replace("-", "") + ".scan");
        }
        public void Remember(Snapshot data)
        {
            if (data.Cancelled) return;
            memory.RemoveAll(s => Paths.Equal(s.Root.Path, data.Root.Path)); memory.Insert(0, data);
            while (memory.Count > 1 && (memory.Count > 2 || memory.Sum(s => (long)s.Entries.Count) > 500000)) memory.RemoveAt(memory.Count - 1);
        }
        public CachedScan Find(string path, CancellationToken cancel)
        {
            path = Paths.Normalize(path);
            var candidates = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
            foreach (var data in memory) if (Paths.Within(path, data.Root.Path)) candidates[data.Root.Path] = data.StartedUtc;
            string root = path;
            while (root != null)
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    using (var stream = File.Open(FileFor(root), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
                    using (var reader = new BinaryReader(stream, Encoding.UTF8))
                    {
                        string stored; DateTime started; ReadHeader(reader, out stored, out started);
                        if (Paths.Equal(stored, root) && (!candidates.ContainsKey(root) || candidates[root] < started)) candidates[root] = started;
                    }
                }
                catch (InvalidDataException) { } catch (IOException) { } catch (UnauthorizedAccessException) { } catch (ArgumentException) { }
                var parent = Directory.GetParent(root); root = parent == null ? null : parent.FullName;
            }
            foreach (var candidate in candidates.OrderByDescending(p => p.Value).ThenByDescending(p => p.Key.Length))
            {
                cancel.ThrowIfCancellationRequested();
                Snapshot data = memory.FirstOrDefault(s => Paths.Equal(s.Root.Path, candidate.Key) && s.StartedUtc == candidate.Value);
                if (data == null)
                {
                    try { data = Read(candidate.Key, cancel); }
                    catch (InvalidDataException) { continue; } catch (IOException) { continue; } catch (UnauthorizedAccessException) { continue; } catch (ArgumentException) { continue; } catch (OverflowException) { continue; }
                }
                if (data == null) continue;
                Entry target = data.Entries.FirstOrDefault(e => e.IsDirectory && String.Equals(e.Path, path, StringComparison.OrdinalIgnoreCase));
                if (target == null) continue;
                data.IsStale |= IsMarkedStale(data);
                Remember(data);
                return new CachedScan { Snapshot = data, Directory = target };
            }
            return null;
        }
        public void Save(Snapshot data, CancellationToken cancel)
        {
            if (data.Cancelled) return; // Never replace a complete result with a cancelled scan.
            Remember(data); Directory.CreateDirectory(folder);
            string target = FileFor(data.Root.Path), temporary = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    using (var header = new BinaryWriter(file, Encoding.UTF8, true))
                    {
                        header.Write(Magic); header.Write(Version); WriteText(header, data.Root.Path); header.Write(data.StartedUtc.Ticks);
                    }
                    using (var compressed = new GZipStream(file, CompressionLevel.Fastest, true))
                    using (var writer = new BinaryWriter(compressed, Encoding.UTF8))
                    {
                        writer.Write(data.Elapsed.Ticks); writer.Write(data.IsStale); writer.Write(data.ErrorCount);
                        writer.Write(data.Errors.Count); foreach (var error in data.Errors) WriteText(writer, error);
                        writer.Write(data.Entries.Count);
                        var parents = new Dictionary<Entry, int>();
                        for (int i = 0; i < data.Entries.Count; i++)
                        {
                            cancel.ThrowIfCancellationRequested(); var e = data.Entries[i];
                            writer.Write(i == 0 ? -1 : parents[e.Parent]); WriteText(writer, e.Name);
                            writer.Write(e.IsDirectory); writer.Write((int)e.Attributes);
                            writer.Write(e.CreatedUtc.Ticks); writer.Write(e.ModifiedUtc.Ticks); writer.Write(e.LatestUtc.Ticks);
                            writer.Write(e.Bytes); writer.Write(e.FileCount); writer.Write(e.FolderCount); writer.Write(e.Issues);
                            writer.Write(e.ContainsSystemEntries); WriteText(writer, e.Note);
                            if (e.IsDirectory)
                            {
                                parents[e] = i;
                                for (int c = 0; c < 7; c++) writer.Write(e.TypeBytes == null ? 0 : e.TypeBytes[c]);
                            }
                        }
                        writer.Write(EndMarker);
                    }
                    file.Flush(true);
                }
                cancel.ThrowIfCancellationRequested();
                if (File.Exists(target)) File.Replace(temporary, target, null); else File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
        private Snapshot Read(string root, CancellationToken cancel)
        {
            using (var file = File.Open(FileFor(root), FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete))
            {
                string savedRoot; DateTime started;
                using (var header = new BinaryReader(file, Encoding.UTF8, true)) ReadHeader(header, out savedRoot, out started);
                if (!Paths.Equal(savedRoot, root)) throw new InvalidDataException("Cache root mismatch.");
                using (var compressed = new GZipStream(file, CompressionMode.Decompress))
                using (var reader = new BinaryReader(compressed, Encoding.UTF8))
                {
                    var data = new Snapshot { StartedUtc = started, Elapsed = TimeSpan.FromTicks(Nonnegative(reader.ReadInt64())), IsStale = reader.ReadBoolean(), ErrorCount = Count(reader, int.MaxValue) };
                    int errors = Count(reader, 500); for (int i = 0; i < errors; i++) data.Errors.Add(ReadText(reader));
                    int count = Count(reader, 10000000); if (count == 0) throw new InvalidDataException("Empty cache.");
                    for (int i = 0; i < count; i++)
                    {
                        cancel.ThrowIfCancellationRequested();
                        int parentIndex = reader.ReadInt32(); string name = ReadText(reader);
                        if (i == 0 ? parentIndex != -1 : parentIndex < 0 || parentIndex >= i || !data.Entries[parentIndex].IsDirectory) throw new InvalidDataException("Invalid parent index.");
                        if (i != 0 && (String.IsNullOrEmpty(name) || name == "." || name == ".." || name.IndexOfAny(new [] { '\\', '/', ':', '\0' }) >= 0)) throw new InvalidDataException("Invalid cached name.");
                        var parent = i == 0 ? null : data.Entries[parentIndex];
                        var e = new Entry { Name = name, Path = i == 0 ? savedRoot : Path.Combine(parent.Path, name), Parent = parent, IsDirectory = reader.ReadBoolean(), Attributes = (FileAttributes)reader.ReadInt32(),
                            CreatedUtc = ReadDate(reader), ModifiedUtc = ReadDate(reader), LatestUtc = ReadDate(reader),
                            Bytes = Nonnegative(reader.ReadInt64()), FileCount = Nonnegative(reader.ReadInt64()), FolderCount = Nonnegative(reader.ReadInt64()), Issues = Count(reader, int.MaxValue), ContainsSystemEntries = reader.ReadBoolean(), Note = ReadText(reader) };
                        if (e.IsDirectory) { e.TypeBytes = new long[7]; for (int c = 0; c < 7; c++) e.TypeBytes[c] = Nonnegative(reader.ReadInt64()); }
                        if (i == 0) { if (!e.IsDirectory) throw new InvalidDataException("Cache root is not a directory."); data.Root = e; }
                        else parent.Children.Add(e);
                        data.Entries.Add(e);
                    }
                    if (reader.ReadInt32() != EndMarker || reader.Read() != -1) throw new InvalidDataException("Incomplete cache.");
                    return data;
                }
            }
        }
        private bool IsMarkedStale(Snapshot data)
        {
            try { string marker = FileFor(data.Root.Path) + ".changed"; return File.Exists(marker) && File.GetLastWriteTimeUtc(marker) >= data.StartedUtc; }
            catch (IOException) { return true; } catch (UnauthorizedAccessException) { return true; }
        }
        public void MarkChanged(IEnumerable<string> changedPaths)
        {
            var changed = changedPaths.Select(Paths.Normalize).ToArray();
            foreach (var data in memory) if (Overlaps(data.Root.Path, changed)) data.IsStale = true;
            if (!Directory.Exists(folder)) return;
            foreach (var path in Directory.EnumerateFiles(folder, "*.scan", SearchOption.TopDirectoryOnly))
            {
                string root;
                try { using (var file = File.OpenRead(path)) using (var reader = new BinaryReader(file, Encoding.UTF8)) { DateTime started; ReadHeader(reader, out root, out started); } }
                catch (InvalidDataException) { continue; } catch (EndOfStreamException) { continue; }
                if (Overlaps(root, changed)) File.WriteAllText(path + ".changed", DateTime.UtcNow.ToString("o"));
            }
        }
        private static bool Overlaps(string root, string[] changed) { return changed.Any(p => Paths.Within(root, p) || Paths.Within(p, root)); }
        private static void ReadHeader(BinaryReader reader, out string root, out DateTime started)
        {
            if (reader.ReadInt32() != Magic || reader.ReadInt32() != Version) throw new InvalidDataException("Unsupported cache format.");
            root = Paths.Normalize(ReadText(reader)); started = ReadDate(reader);
        }
        private static DateTime ReadDate(BinaryReader reader) { return new DateTime(reader.ReadInt64(), DateTimeKind.Utc); }
        private static long Nonnegative(long value) { if (value < 0) throw new InvalidDataException("Negative cache value."); return value; }
        private static int Count(BinaryReader reader, int maximum) { int value = reader.ReadInt32(); if (value < 0 || value > maximum) throw new InvalidDataException("Invalid cache count."); return value; }
        private static void WriteText(BinaryWriter writer, string text) { byte[] value = Encoding.UTF8.GetBytes(text ?? ""); writer.Write(value.Length); writer.Write(value); }
        private static string ReadText(BinaryReader reader)
        {
            int length = Count(reader, 262144); byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length != length) throw new EndOfStreamException();
            return new UTF8Encoding(false, true).GetString(bytes);
        }
    }
}
