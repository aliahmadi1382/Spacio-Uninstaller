using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using Microsoft.Win32;
using Spacio;

namespace SpacioSetup
{
    // Unicode Shell links; avoid WScript automation's locale-dependent dispatch.
    [ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface IShellLinkW
    {
        void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, IntPtr data, uint flags);
        void GetIDList(out IntPtr id);
        void SetIDList(IntPtr id);
        void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count);
        void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string path);
        void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder text, int count);
        void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string text);
        void GetHotkey(out short hotkey);
        void SetHotkey(short hotkey);
        void GetShowCmd(out int command);
        void SetShowCmd(int command);
        void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder path, int count, out int index);
        void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string path, int index);
        void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string path, uint reserved);
        void Resolve(IntPtr owner, uint flags);
        void SetPath([MarshalAs(UnmanagedType.LPWStr)] string path);
    }
    internal sealed class SetupLock : IDisposable
    {
        private readonly Mutex mutex;
        internal SetupLock(string path)
        {
            string name;
            using (var hash = SHA256.Create()) name = BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))).Replace("-", "");
            mutex = new Mutex(false, "Local\\Spacio.Setup." + name);
            try { if (!mutex.WaitOne(0)) { mutex.Dispose(); throw new IOException("Another Setup operation is already running."); } }
            catch (AbandonedMutexException) { }
        }
        public void Dispose() { mutex.ReleaseMutex(); mutex.Dispose(); }
    }
    internal sealed class InstallContext
    {
        internal string Directory, DesktopLink, MenuLink, RegistryPath;
        internal string Executable { get { return Path.Combine(Directory, "Spacio.exe"); } }
        internal static InstallContext CurrentUser()
        {
            return new InstallContext { Directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "Spacio"),
                DesktopLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Spacio.lnk"),
                MenuLink = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "Spacio.lnk"), RegistryPath = InstallationRegistration.KeyPath };
        }
    }
    internal sealed class RegistryBackup
    {
        private readonly Dictionary<string, Tuple<object, RegistryValueKind>> values = new Dictionary<string, Tuple<object, RegistryValueKind>>();
        private readonly bool existed;
        internal RegistryBackup(RegistryKey root, string path)
        {
            using (var key = root.OpenSubKey(path))
            { existed = key != null; if (key != null) foreach (string name in key.GetValueNames()) values[name] = Tuple.Create(key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames), key.GetValueKind(name)); }
        }
        internal void Restore(RegistryKey root, string path)
        {
            if (!existed) { root.DeleteSubKey(path, false); return; }
            using (var key = root.CreateSubKey(path))
            {
                foreach (var name in key.GetValueNames()) if (!values.ContainsKey(name)) key.DeleteValue(name);
                foreach (var pair in values) key.SetValue(pair.Key, pair.Value.Item1, pair.Value.Item2);
            }
        }
    }
    internal static class SetupEngine
    {
        internal const string Identity = "Spacio per-user installation v1";
        internal static readonly string[] PayloadNames = { "Spacio.exe", "Spacio.exe.config", "README.md", "CHANGELOG.md" };
        internal static readonly string[] OwnedNames = PayloadNames.Concat(new [] { "Spacio.Setup.exe", "spacio-installation.txt" }).ToArray();
        internal static Version Version { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        internal static string Self { get { return Assembly.GetExecutingAssembly().Location; } }
        internal static bool Same(string a, string b) { return String.Equals(Path.GetFullPath(a).TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase); }
        internal static string Hash(string path)
        { using (var file = File.OpenRead(path)) using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-", "").ToLowerInvariant(); }
        internal static void CheckAncestors(string path)
        {
            string current = Path.GetFullPath(path);
            while (current != null)
            {
                if ((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked installation paths are not supported: " + current);
                current = Path.GetDirectoryName(current);
            }
        }
        private static void Validate(InstallContext context)
        {
            string directory = Path.GetFullPath(context.Directory);
            if (Same(directory, Path.GetPathRoot(directory))) throw new IOException("Invalid installation directory.");
            CheckAncestors(directory);
            using (var root = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            using (var key = root.OpenSubKey(context.RegistryPath))
                if (key != null && !Same(Convert.ToString(key.GetValue("InstallLocation")), directory)) throw new IOException("The existing registration belongs to another directory.");
            foreach (string name in OwnedNames)
            {
                string path = Path.Combine(directory, name); CheckAncestors(path);
                if (File.Exists(path)) using (File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
            }
        }
        internal static string ShortcutTarget(string path)
        {
            object link = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046")));
            try
            {
                ((IPersistFile)link).Load(path, 0);
                var target = new StringBuilder(32768);
                ((IShellLinkW)link).GetPath(target, target.Capacity, IntPtr.Zero, 4);
                return target.ToString();
            }
            finally { Marshal.FinalReleaseComObject(link); }
        }
        internal static void WriteShortcut(string path, string executable)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            object link = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("00021401-0000-0000-C000-000000000046")));
            try
            {
                var shortcut = (IShellLinkW)link;
                shortcut.SetPath(executable); shortcut.SetWorkingDirectory(Path.GetDirectoryName(executable));
                shortcut.SetIconLocation(executable, 0); shortcut.SetDescription("Spacio");
                ((IPersistFile)link).Save(path, true);
            }
            finally { Marshal.FinalReleaseComObject(link); }
        }
        private static Dictionary<string, byte[]> BackupLinks(InstallContext context, bool desktop)
        {
            var result = new Dictionary<string, byte[]>();
            foreach (var path in (desktop ? new [] { context.MenuLink, context.DesktopLink } : new [] { context.MenuLink }))
            {
                if (File.Exists(path) && ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || !Same(ShortcutTarget(path), context.Executable))) throw new IOException("A different shortcut already uses this name: " + path);
                result[path] = File.Exists(path) ? File.ReadAllBytes(path) : null;
            }
            return result;
        }
        private static void RestoreLinks(Dictionary<string, byte[]> links)
        { foreach (var pair in links) { if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); } else File.WriteAllBytes(pair.Key, pair.Value); } }
        private static void Extract(string stage)
        {
            var assembly = Assembly.GetExecutingAssembly(); Dictionary<string, string> hashes;
            using (var reader = new StreamReader(assembly.GetManifestResourceStream("Spacio.Payload.txt")))
                hashes = reader.ReadToEnd().Split(new [] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Split('=')).ToDictionary(s => s[0], s => s[1]);
            using (var stream = assembly.GetManifestResourceStream("Spacio.Package.zip"))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
                foreach (string name in PayloadNames)
                {
                    var entries = archive.Entries.Where(e => e.FullName == name).ToArray(); if (entries.Length != 1) throw new IOException("Invalid Setup payload: " + name);
                    string path = Path.Combine(stage, name);
                    using (var input = entries[0].Open()) using (var output = File.Create(path)) input.CopyTo(output);
                    if (Hash(path) != hashes[name]) throw new IOException("Setup payload checksum mismatch: " + name);
                }
            var identity = AssemblyName.GetAssemblyName(Path.Combine(stage, "Spacio.exe"));
            if (identity.Name != "Spacio" || identity.Version != Version) throw new IOException("Setup payload version mismatch.");
            File.Copy(Self, Path.Combine(stage, "Spacio.Setup.exe"));
            File.WriteAllText(Path.Combine(stage, "spacio-installation.txt"), Identity);
        }
        private static void Register(RegistryKey root, InstallContext context)
        {
            using (var key = root.CreateSubKey(context.RegistryPath))
            {
                key.SetValue("DisplayName", "Spacio"); key.SetValue("DisplayVersion", Version.ToString(3)); key.SetValue("Publisher", "Spacio");
                key.SetValue("InstallLocation", context.Directory); key.SetValue("DisplayIcon", context.Executable + ",0");
                key.SetValue("UninstallString", "\"" + Path.Combine(context.Directory, "Spacio.Setup.exe") + "\" --uninstall");
                key.SetValue("URLInfoAbout", "https://github.com/aliahmadi1382/Spacio-Uninstaller");
                key.SetValue("NoModify", 1, RegistryValueKind.DWord); key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                key.SetValue("EstimatedSize", (int)(OwnedNames.Sum(n => new FileInfo(Path.Combine(context.Directory, n)).Length) / 1024), RegistryValueKind.DWord);
            }
        }
        private static void ClearWork(string work)
        {
            // Only known generated files, no recursive deletion or wildcard cleanup.
            foreach (string child in new [] { "new", "old" })
            {
                string directory = Path.Combine(work, child); CheckAncestors(directory);
                foreach (string name in OwnedNames) { string file = Path.Combine(directory, name); if (File.Exists(file)) File.Delete(file); }
                if (Directory.Exists(directory) && !Directory.EnumerateFileSystemEntries(directory).Any()) Directory.Delete(directory);
            }
            if (Directory.Exists(work) && !Directory.EnumerateFileSystemEntries(work).Any()) Directory.Delete(work);
        }
        internal static void Install(InstallContext context, bool desktop, Action<string> progress = null, Action<string> fault = null)
        { using (var held = new SetupLock(context.Directory)) InstallCore(context, desktop, progress, fault); }
        private static void InstallCore(InstallContext context, bool desktop, Action<string> progress, Action<string> fault)
        {
            Validate(context);
            if (File.Exists(context.Executable))
            {
                var existing = AssemblyName.GetAssemblyName(context.Executable);
                if (existing.Name != "Spacio" || existing.Version > Version) throw new IOException("A newer or different application is already installed. Use the latest Spacio Setup.");
            }
            var links = BackupLinks(context, desktop);
            string work = Path.Combine(Path.GetDirectoryName(context.Directory), ".Spacio-Setup-" + Guid.NewGuid().ToString("N"));
            string fresh = Path.Combine(work, "new"), backup = Path.Combine(work, "old");
            CheckAncestors(work); Directory.CreateDirectory(fresh); Directory.CreateDirectory(backup);
            bool clean = false; var touched = new List<string>(); var existed = new HashSet<string>();
            using (var registry = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                var registration = new RegistryBackup(registry, context.RegistryPath);
                try
                {
                    if (progress != null) progress("Verifying files"); Extract(fresh); Directory.CreateDirectory(context.Directory);
                    foreach (string name in OwnedNames)
                    {
                        string target = Path.Combine(context.Directory, name); CheckAncestors(target);
                        if (File.Exists(target)) { File.Copy(target, Path.Combine(backup, name)); existed.Add(name); }
                        touched.Add(name);
                        if (existed.Contains(name)) File.Replace(Path.Combine(fresh, name), target, null); else File.Move(Path.Combine(fresh, name), target);
                    }
                    if (fault != null) fault("files");
                    if (progress != null) progress("Creating shortcuts");
                    foreach (string path in links.Keys) WriteShortcut(path, context.Executable);
                    Register(registry, context); if (fault != null) fault("registration"); clean = true;
                }
                catch
                {
                    // Retain backups if rollback itself fails, instead of deleting recovery data.
                    foreach (string name in touched.AsEnumerable().Reverse())
                    {
                        string target = Path.Combine(context.Directory, name);
                        if (existed.Contains(name)) File.Copy(Path.Combine(backup, name), target, true); else if (File.Exists(target)) File.Delete(target);
                    }
                    RestoreLinks(links); registration.Restore(registry, context.RegistryPath); clean = true; throw;
                }
                finally { if (clean) ClearWork(work); }
            }
        }
        internal static void Uninstall(InstallContext context, Action<string> fault = null)
        { using (var held = new SetupLock(context.Directory)) UninstallCore(context, fault); }
        private static void UninstallCore(InstallContext context, Action<string> fault)
        {
            Validate(context);
            string marker = Path.Combine(context.Directory, "spacio-installation.txt");
            if (!File.Exists(marker) || File.ReadAllText(marker) != Identity) throw new IOException("This is not a registered Spacio Setup installation.");
            // An unrelated shortcut is preserved, even if the user repointed it later.
            var links = new Dictionary<string, byte[]>();
            foreach (var path in new [] { context.MenuLink, context.DesktopLink })
                if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0 && Same(ShortcutTarget(path), context.Executable)) links[path] = File.ReadAllBytes(path);
            string work = Path.Combine(Path.GetDirectoryName(context.Directory), ".Spacio-Uninstall-" + Guid.NewGuid().ToString("N"));
            string backup = Path.Combine(work, "old"); CheckAncestors(work); Directory.CreateDirectory(backup);
            var moved = new List<string>(); bool clean = false;
            using (var registry = RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Registry64))
            {
                var registration = new RegistryBackup(registry, context.RegistryPath);
                try
                {
                    foreach (string name in OwnedNames)
                    { string path = Path.Combine(context.Directory, name); if (File.Exists(path)) { File.Move(path, Path.Combine(backup, name)); moved.Add(name); } }
                    if (fault != null) fault("files");
                    foreach (string path in links.Keys) File.Delete(path);
                    registry.DeleteSubKey(context.RegistryPath, false); if (fault != null) fault("registration"); clean = true;
                }
                catch
                {
                    foreach (string name in moved) File.Move(Path.Combine(backup, name), Path.Combine(context.Directory, name));
                    RestoreLinks(links); registration.Restore(registry, context.RegistryPath); clean = true; throw;
                }
                finally { if (clean) ClearWork(work); }
            }
            if (!Directory.EnumerateFileSystemEntries(context.Directory).Any()) Directory.Delete(context.Directory);
        }
    }
}
