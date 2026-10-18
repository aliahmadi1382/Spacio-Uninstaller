using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace Spacio
{
    // Explicitly selected data, not an inferred cache. A plan exists only in the
    // deletion review and is never persisted in the saved scan format.
    internal sealed class ReviewedRemoval
    {
        internal sealed class Node
        {
            internal Entry Entry;
            internal NativeRemoval.Info Identity;
            internal string Reparse = "", Target = "";
            internal bool Link { get { return (Identity.Attributes & 0x400) != 0; } }
        }
        internal readonly List<Node> Nodes = new List<Node>();
        internal Entry Root { get { return Nodes[0].Entry; } }
        internal int LinkCount { get { return Nodes.Count(n => n.Link); } }
        internal bool AppData;
        private string installBoundary;

        internal static string ProtectedReason(string path, string installation = null)
        {
            path = Paths.Normalize(path);
            string disk = Path.GetPathRoot(path);
            if (Paths.Equal(path, disk)) return "ریشهٔ درایو قابل حذف نیست";
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var user = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var programRoots = new [] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86) };
            bool reviewedInstallation = !String.IsNullOrWhiteSpace(installation) && Paths.Within(path, installation) &&
                programRoots.Any(p => !String.IsNullOrEmpty(p) && !Paths.Equal(installation, p) && Paths.Within(installation, p));
            foreach (string programRoot in programRoots.Where(p => !String.IsNullOrEmpty(p)))
                foreach (string reserved in new [] { "Common Files", "WindowsApps", "Windows Defender", "Windows NT", "Microsoft.NET" })
                    if (Paths.Within(path, Path.Combine(programRoot, reserved))) return "مسیر محافظت‌شدهٔ سیستم";
            if (Paths.Equal(Path.GetDirectoryName(path), Path.Combine(disk, "Users")) || path.EndsWith(@"\AppData", StringComparison.OrdinalIgnoreCase) ||
                path.EndsWith(@"\AppData\Local", StringComparison.OrdinalIgnoreCase) || path.EndsWith(@"\AppData\Roaming", StringComparison.OrdinalIgnoreCase) || path.EndsWith(@"\AppData\LocalLow", StringComparison.OrdinalIgnoreCase))
                return "پوشهٔ اصلی داده‌ها یا پروفایل قابل حذف نیست؛ یک زیرپوشه انتخاب کنید";
            // These containers may have selected children, but never authorize a
            // whole-profile/container wipe as a side effect of choosing a parent.
            foreach (var container in new [] { local, roaming, common, user, Path.Combine(local, "Programs"), Path.Combine(disk, "Users") })
                if (!String.IsNullOrEmpty(container) && Paths.Within(container, path)) return "پوشهٔ اصلی داده‌ها یا پروفایل قابل حذف نیست؛ یک زیرپوشه انتخاب کنید";
            var protectedPaths = new [] { Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                AppDomain.CurrentDomain.BaseDirectory, Path.Combine(local, "Spacio"), Path.Combine(local, "Programs", "Spacio"),
                Path.Combine(local, "Microsoft", "Windows"), Path.Combine(local, "Microsoft", "WindowsApps"), Path.Combine(local, "Packages"),
                Path.Combine(roaming, "Microsoft", "Windows"), Path.Combine(common, "Microsoft") };
            foreach (var root in protectedPaths)
                if (!String.IsNullOrEmpty(root) && !(reviewedInstallation && programRoots.Any(p => !String.IsNullOrEmpty(p) && Paths.Equal(root, p))) && (Paths.Within(path, root) || Paths.Within(root, path))) return "مسیر محافظت‌شدهٔ سیستم یا Spacio؛ حذف‌کنندهٔ برنامه را به‌کار ببرید";
            foreach (var name in new [] { "Windows", "Program Files", "Program Files (x86)", "Recovery", "$RECYCLE.BIN", "System Volume Information", "Boot", "EFI", "MSOCache" })
                if (!(reviewedInstallation && (name == "Program Files" || name == "Program Files (x86)")) && Paths.Within(path, Path.Combine(disk, name))) return "مسیر محافظت‌شدهٔ سیستم";
            // Protect scan storage in isolated fixtures and non-default user profiles too.
            string normalized = path.Replace('/', '\\');
            if (normalized.IndexOf(@"\AppData\Local\Spacio\", StringComparison.OrdinalIgnoreCase) >= 0 || normalized.EndsWith(@"\AppData\Local\Spacio", StringComparison.OrdinalIgnoreCase))
                return "اسکن‌ها و تنظیمات Spacio محافظت می‌شوند";
            return null;
        }

        internal static ReviewedRemoval Prepare(string path, CancellationToken token, string installation = null)
        {
            path = Paths.Normalize(path); Paths.RequireLocal(path);
            if (installation != null && !Paths.Equal(path, installation)) throw new IOException("Invalid installation boundary.");
            string blocked = ProtectedReason(path, installation); if (blocked != null) throw new IOException(L.T(blocked));
            var plan = new ReviewedRemoval { installBoundary = installation, AppData = path.IndexOf(@"\AppData\", StringComparison.OrdinalIgnoreCase) >= 0 || Paths.Within(path, Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)) };
            var held = NativeRemoval.PinAncestors(path);
            try
            {
                var rootHandle = NativeRemoval.Open(path, false); held.Add(rootHandle);
                var root = Read(path, null, rootHandle, installation); plan.Nodes.Add(root);
                var pending = new Stack<Node>(); if (root.Entry.IsDirectory && !root.Link) pending.Push(root);
                while (pending.Count > 0)
                {
                    token.ThrowIfCancellationRequested(); var parent = pending.Pop();
                    foreach (var childPath in Directory.EnumerateFileSystemEntries(parent.Entry.Path))
                    {
                        token.ThrowIfCancellationRequested();
                        var handle = NativeRemoval.Open(childPath, false);
                        try
                        {
                            var node = Read(childPath, parent.Entry, handle, installation); plan.Nodes.Add(node); parent.Entry.Children.Add(node.Entry);
                            if (node.Entry.IsDirectory && !node.Link) { held.Add(handle); handle = null; pending.Push(node); }
                        }
                        finally { if (handle != null) handle.Dispose(); }
                    }
                }
                for (int i = plan.Nodes.Count - 1; i > 0; i--)
                {
                    var entry = plan.Nodes[i].Entry; var parent = entry.Parent;
                    parent.Bytes += entry.Bytes; parent.FileCount += entry.FileCount; parent.FolderCount += entry.FolderCount + (entry.IsDirectory ? 1 : 0);
                    if (entry.LatestUtc > parent.LatestUtc) parent.LatestUtc = entry.LatestUtc;
                }
                token.ThrowIfCancellationRequested(); return plan;
            }
            finally { foreach (var handle in held.AsEnumerable().Reverse()) handle.Dispose(); }
        }
        private static Node Read(string path, Entry parent, SafeFileHandle handle, string installation = null)
        {
            var info = NativeRemoval.Read(handle); var attributes = (FileAttributes)info.Attributes;
            if ((attributes & FileAttributes.Directory) != 0 && (attributes & FileAttributes.ReparsePoint) == 0)
            { string protectedReason = ProtectedReason(path, installation); if (protectedReason != null) throw new IOException(L.T(protectedReason) + "\n" + path); }
            if ((info.Attributes & (4u | 0x1000u | 0x40000u | 0x400000u)) != 0) throw new IOException(L.T("فایل سیستمی یا ابری قابل حذف نیست") + "\n" + path);
            var node = new Node { Identity = info, Entry = new Entry { Path = path, Name = Path.GetFileName(path), Parent = parent,
                IsDirectory = (attributes & FileAttributes.Directory) != 0, Attributes = attributes, CreatedUtc = DateTime.FromFileTimeUtc(info.Creation),
                ModifiedUtc = DateTime.FromFileTimeUtc(info.Modified), LatestUtc = DateTime.FromFileTimeUtc(info.Modified), TypeBytes = new long[7] } };
            if (node.Link)
            {
                byte[] data = NativeRemoval.Reparse(handle); uint tag = BitConverter.ToUInt32(data, 0);
                if (tag != 0xA0000003 && tag != 0xA000000C) throw new IOException(L.T("نوع پیوند یا فایل ابری پشتیبانی نمی‌شود") + "\n" + path);
                int offset = BitConverter.ToUInt16(data, 8), length = BitConverter.ToUInt16(data, 10), start = tag == 0xA0000003 ? 16 : 20;
                if (start + offset + length > data.Length) throw new IOException("Invalid reparse metadata: " + path);
                node.Target = System.Text.Encoding.Unicode.GetString(data, start + offset, length);
                if (node.Target.StartsWith(@"\??\Volume{", StringComparison.OrdinalIgnoreCase)) throw new IOException(L.T("نقطهٔ اتصال درایو قابل حذف نیست") + "\n" + path);
                node.Reparse = Convert.ToBase64String(data);
                node.Entry.Note = L.T("فقط خود پیوند حذف می‌شود؛ مقصد حفظ می‌شود") + "\n" + node.Target;
            }
            else if (!node.Entry.IsDirectory) { node.Entry.Bytes = info.Length; node.Entry.FileCount = 1; }
            return node;
        }
        private void Verify(Node expected, SafeFileHandle handle, bool directoryContentsChanged = false)
        {
            var actual = Read(expected.Entry.Path, null, handle, installBoundary);
            if (!expected.Identity.Same(actual.Identity, directoryContentsChanged) || expected.Reparse != actual.Reparse)
                throw new IOException(L.T("مورد از زمان پیش‌نمایش تغییر کرده است؛ دوباره بررسی کنید") + "\n" + expected.Entry.Path);
        }
        internal void DeletePermanent(CancellationToken token)
        {
            string blocked = ProtectedReason(Root.Path, installBoundary); if (blocked != null) throw new IOException(L.T(blocked));
            var held = NativeRemoval.PinAncestors(Root.Path);
            var directories = new Dictionary<string, SafeFileHandle>(StringComparer.OrdinalIgnoreCase);
            int deleted = 0; string active = Root.Path;
            try
            {
                // Pin every directory before using any descendant path. Renames and
                // link swaps cannot redirect deletion outside the reviewed tree.
                foreach (var node in Nodes.Where(n => n.Entry.IsDirectory && !n.Link).OrderBy(n => n.Entry.Path.Length))
                {
                    token.ThrowIfCancellationRequested(); active = node.Entry.Path;
                    var handle = NativeRemoval.Open(active, true); held.Add(handle); Verify(node, handle); directories.Add(active, handle);
                }
                foreach (var node in Nodes)
                {
                    token.ThrowIfCancellationRequested(); active = node.Entry.Path;
                    if (directories.ContainsKey(active))
                    {
                        var expected = new HashSet<string>(node.Entry.Children.Select(e => e.Path), StringComparer.OrdinalIgnoreCase);
                        if (!expected.SetEquals(Directory.EnumerateFileSystemEntries(active))) throw new IOException(L.T("محتوای پوشه از زمان اسکن تغییر کرده است. دوباره اسکن کنید."));
                    }
                    else using (var handle = NativeRemoval.Open(active, false, true)) Verify(node, handle);
                }
                foreach (var node in Nodes.OrderByDescending(n => n.Entry.Path.Length))
                {
                    token.ThrowIfCancellationRequested(); active = node.Entry.Path; SafeFileHandle directory;
                    if (directories.TryGetValue(active, out directory))
                    { Verify(node, directory, true); NativeRemoval.Delete(directory); directory.Dispose(); }
                    else using (var handle = NativeRemoval.Open(active, true)) { Verify(node, handle); NativeRemoval.Delete(handle); }
                    deleted++;
                }
            }
            catch (Exception e)
            {
                if (!(e is IOException) && !(e is UnauthorizedAccessException) && !(e is OperationCanceledException)) throw;
                throw new IOException(L.T("تعداد موارد حذف‌شده") + ": " + deleted + "\n" + active + "\n" + e.Message, e);
            }
            finally { foreach (var handle in held.AsEnumerable().Reverse()) handle.Dispose(); }
        }
        internal void VerifyForRecycle(CancellationToken token)
        {
            if (LinkCount > 0) throw new IOException(L.T("پوشهٔ دارای پیوند به سطل بازیافت منتقل نمی‌شود؛ حذف دائمی فقط با تأیید شما انجام می‌شود"));
            var fresh = Prepare(Root.Path, token, installBoundary);
            if (Nodes.Count != fresh.Nodes.Count) throw new IOException(L.T("مورد از زمان پیش‌نمایش تغییر کرده است؛ دوباره بررسی کنید"));
            var byPath = fresh.Nodes.ToDictionary(n => n.Entry.Path, StringComparer.OrdinalIgnoreCase);
            foreach (var node in Nodes)
            { Node other; if (!byPath.TryGetValue(node.Entry.Path, out other) || !node.Identity.Same(other.Identity, false)) throw new IOException(L.T("مورد از زمان پیش‌نمایش تغییر کرده است؛ دوباره بررسی کنید")); }
        }
    }
    internal static class NativeRemoval
    {
        [StructLayout(LayoutKind.Sequential)]
        internal struct Info
        {
            internal uint Attributes, CreationLow, CreationHigh, AccessLow, AccessHigh, ModifiedLow, ModifiedHigh, Volume, SizeHigh, SizeLow, Links, IndexHigh, IndexLow;
            internal long Creation { get { return (long)(((ulong)CreationHigh << 32) | CreationLow); } }
            internal long Modified { get { return (long)(((ulong)ModifiedHigh << 32) | ModifiedLow); } }
            internal long Length { get { return (long)(((ulong)SizeHigh << 32) | SizeLow); } }
            internal bool Same(Info other, bool ownChanges)
            { return Volume == other.Volume && IndexHigh == other.IndexHigh && IndexLow == other.IndexLow && Creation == other.Creation && Attributes == other.Attributes && (ownChanges || (Modified == other.Modified && Length == other.Length)); }
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetFileInformationByHandle(SafeFileHandle handle, out Info info);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(SafeFileHandle handle, uint code, IntPtr input, int inputSize, byte[] output, int outputSize, out int returned, IntPtr overlapped);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetFileInformationByHandle(SafeFileHandle handle, int kind, byte[] data, uint size);
        private static IOException Error() { return new IOException(new Win32Exception(Marshal.GetLastWin32Error()).Message); }
        internal static SafeFileHandle Open(string path, bool delete, bool shared = false)
        {
            // OPEN_REPARSE_POINT opens the link itself, never its target.
            var handle = CreateFileW(@"\\?\" + Paths.Normalize(path), 0x80u | (delete ? 0x10000u : 0u), shared ? 7u : delete ? 1u : 3u,
                IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
            if (handle.IsInvalid) { var error = Error(); handle.Dispose(); throw error; } return handle;
        }
        internal static Info Read(SafeFileHandle handle) { Info info; if (!GetFileInformationByHandle(handle, out info)) throw Error(); return info; }
        internal static byte[] Reparse(SafeFileHandle handle)
        { byte[] data = new byte[16384]; int count; if (!DeviceIoControl(handle, 0x900A8, IntPtr.Zero, 0, data, data.Length, out count, IntPtr.Zero)) throw Error(); return data.Take(count).ToArray(); }
        internal static void Delete(SafeFileHandle handle)
        {
            // Ignore the read-only bit on the selected directory entry without
            // changing attributes shared by other hard links. Keep image checks.
            bool readOnly = (Read(handle).Attributes & 1) != 0;
            if (!SetFileInformationByHandle(handle, readOnly ? 21 : 4, readOnly ? BitConverter.GetBytes(0x15u) : new byte[] { 1 }, readOnly ? 4u : 1u)) throw Error();
        }
        internal static List<SafeFileHandle> PinAncestors(string path)
        {
            var paths = new Stack<string>(); for (string parent = Path.GetDirectoryName(Paths.Normalize(path)); parent != null; parent = Path.GetDirectoryName(parent)) paths.Push(parent);
            var held = new List<SafeFileHandle>();
            try
            {
                foreach (var parent in paths)
                {
                    var handle = Open(parent, false); held.Add(handle);
                    if ((Read(handle).Attributes & (0x400u | 0x1000u)) != 0) throw new IOException(L.T("مسیر شامل پیوند، junction یا فایل ابری است.") + "\n" + parent);
                }
                return held;
            }
            catch { foreach (var handle in held.AsEnumerable().Reverse()) handle.Dispose(); throw; }
        }
    }
}
