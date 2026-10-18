using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace Spacio
{
    internal sealed class CacheRoots
    {
        internal string Local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        internal string Roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        internal string Common = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
    }
    internal sealed class CacheSpec
    {
        internal string Path, Owner, Family, Reason;
        internal string[] Processes;
        internal bool Shared;
    }
    internal sealed class CleanupItem
    {
        internal Entry Entry;
        internal Snapshot Source;
        internal CacheSpec Spec;
        internal bool Candidate;
        internal ReviewedRemoval Removal;
        internal LeftoverCandidate Leftover;
        internal string Failure = "";
        public bool Chosen { get; set; }
        public string Path { get; internal set; }
        private string owner, evidence;
        public string Owner { get { return L.T(owner); } internal set { owner = value; } }
        public string Evidence { get { return L.T(evidence); } internal set { evidence = value; } }
        public string Detail { get { return Owner + "\n" + Path + "\n" + Evidence + "\n" + Status; } }
        public string Kind { get { return L.T(Leftover != null ? Leftover.Kind : Spec != null ? (Spec.Shared ? "کش مشترک" : "کش شناخته‌شده") : Candidate ? "نیازمند بررسی" : "مورد انتخابی"); } }
        public string Size { get { return Entry == null ? "—" : Formats.Size(Entry.Bytes); } }
        public string Count { get { return Entry == null ? "—" : Entry.FileCount.ToString("N0"); } }
        public string Status { get { return L.T(Failure.Length > 0 ? Failure : Entry == null ? "بررسی نشده" : Entry.Issues > 0 ? "ناقص" : "آمادهٔ بررسی"); } }
        public bool CanChoose { get { return Entry != null && Entry.Issues == 0 && Failure.Length == 0; } }
        public string ReviewNotice { get { return Failure.Length > 0 ? Status : Removal == null ? Evidence :
            L.T(Removal.AppData ? "این پوشه داده‌های برنامه است، نه فقط کش؛ تنظیمات، حساب‌ها و تاریخچهٔ داخل آن هم حذف می‌شوند" : "تمام فایل‌ها و زیرپوشه‌های نمایش‌داده‌شده حذف می‌شوند") +
            (Removal.LinkCount > 0 ? "\n" + L.T("تعداد پیوندها؛ فقط خود پیوند حذف می‌شود و مقصد حفظ می‌شود") + ": " + Removal.LinkCount : ""); } }
        internal List<Entry> Contents()
        {
            var result = new List<Entry>(); if (Entry == null) return result;
            var pending = new Stack<Entry>(); pending.Push(Entry);
            while (pending.Count > 0) { var e = pending.Pop(); result.Add(e); foreach (var child in e.Children) pending.Push(child); }
            return result;
        }
    }

    // Only exact cache directories in a versioned, built-in catalogue can bypass the
    // ordinary AppData protection. Names such as "cache" alone never grant that permission.
    internal static class CacheCatalog
    {
        internal static List<CacheSpec> Discover(CacheRoots roots, CancellationToken token)
        {
            var found = new List<CacheSpec>();
            Action<string, string, string, string[], bool> add = (path, owner, family, processes, shared) => {
                token.ThrowIfCancellationRequested();
                if (Directory.Exists(path)) found.Add(new CacheSpec { Path = Paths.Normalize(path), Owner = owner, Family = family, Processes = processes,
                    Shared = shared, Reason = shared ? "مسیر شناخته‌شده؛ بین چند برنامه یا پروژه مشترک است" : "مسیر اختصاصی کش در فهرست پشتیبانی‌شده" });
            };
            Action<string, string, string, string> chromium = (relative, owner, family, process) => {
                string data = System.IO.Path.Combine(roots.Local, relative);
                foreach (string profile in Directories(data))
                {
                    string name = System.IO.Path.GetFileName(profile);
                    if (!(name == "Default" || name == "Guest Profile" || name.StartsWith("Profile ", StringComparison.Ordinal))) continue;
                    foreach (string suffix in new [] { "Cache", "Code Cache", "GPUCache" })
                        add(System.IO.Path.Combine(profile, suffix), owner + " · " + name, family, new [] { process }, false);
                }
            };
            chromium(@"Google\Chrome\User Data", "Google Chrome", "chrome", "chrome");
            chromium(@"Microsoft\Edge\User Data", "Microsoft Edge", "edge", "msedge");
            chromium(@"BraveSoftware\Brave-Browser\User Data", "Brave", "brave", "brave");
            foreach (string profile in Directories(System.IO.Path.Combine(roots.Local, @"Mozilla\Firefox\Profiles")))
                foreach (string suffix in new [] { "cache2", "startupCache", "shader-cache" })
                    add(System.IO.Path.Combine(profile, suffix), "Firefox · " + System.IO.Path.GetFileName(profile), "firefox", new [] { "firefox" }, false);
            foreach (string product in new [] { "Code", "Code - Insiders", "discord", "discordptb", "discordcanary", "Slack", "Notion" })
                foreach (string suffix in new [] { "Cache", "Code Cache", "GPUCache" })
                    add(System.IO.Path.Combine(roots.Roaming, product, suffix), product, product.StartsWith("Code", StringComparison.Ordinal) ? "code" : product.StartsWith("discord", StringComparison.Ordinal) ? "discord" : product == "Notion" ? "notion" : "slack",
                        product.StartsWith("Code", StringComparison.Ordinal) ? new [] { "Code", "Code - Insiders" } : product.StartsWith("discord", StringComparison.Ordinal) ? new [] { "Discord", "DiscordPTB", "DiscordCanary" } : new [] { product }, false);
            foreach (string suffix in new [] { "Media Cache", "Media Cache Files" })
                add(System.IO.Path.Combine(roots.Roaming, "Adobe", "Common", suffix), "Adobe · Premiere / After Effects / Media Encoder", "adobe-media",
                    new [] { "Adobe Premiere Pro", "AfterFX", "Adobe Media Encoder" }, true);
            add(System.IO.Path.Combine(roots.Local, "D3DSCache"), "DirectX", "directx", new string[0], true);
            foreach (string suffix in new [] { "DXCache", "GLCache" })
                add(System.IO.Path.Combine(roots.Local, "NVIDIA", suffix), "NVIDIA", "nvidia", new string[0], true);
            foreach (string suffix in new [] { "DxCache", "DxcCache", "GLCache" })
                add(System.IO.Path.Combine(roots.Local, "AMD", suffix), "AMD", "amd", new string[0], true);
            add(System.IO.Path.Combine(roots.Local, "Steam", "htmlcache"), "Steam", "steam", new [] { "steam", "steamwebhelper" }, false);
            return found.GroupBy(s => s.Path, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToList();
        }
        private static IEnumerable<string> Directories(string root)
        {
            try { if (!Directory.Exists(root)) return new string[0]; Paths.CheckAncestors(root); return Directory.GetDirectories(root); }
            catch (IOException) { return new string[0]; } catch (UnauthorizedAccessException) { return new string[0]; }
        }
        internal static string Running(CacheSpec spec)
        {
            if (spec == null) return null;
            foreach (string name in spec.Processes)
            {
                var processes = Process.GetProcessesByName(name);
                try { if (processes.Length > 0) return "ابتدا برنامه را ببندید" + ": " + name; }
                finally { foreach (var process in processes) process.Dispose(); }
            }
            return null;
        }
        internal static CleanupItem Inspect(CacheSpec spec, CancellationToken token, CacheRoots roots = null)
        {
            var item = new CleanupItem { Path = spec.Path, Owner = spec.Owner, Evidence = spec.Reason, Spec = spec };
            try
            {
                item.Source = new Scanner().Scan(spec.Path, token, null); token.ThrowIfCancellationRequested();
                item.Entry = item.Source.Root;
                item.Failure = BlockCache(item, roots ?? new CacheRoots(), false) ?? "";
            }
            catch (Exception e) { if (!(e is IOException) && !(e is UnauthorizedAccessException)) throw; item.Failure = e.Message; }
            return item;
        }
        internal static string BlockCache(CleanupItem item, CacheRoots roots, bool authenticate = true)
        {
            if (item.Spec == null || item.Entry == null || item.Source == null || item.Source.Cancelled || item.Entry.Issues != 0) return "اسکن ناقص یا مسیر پیوندی";
            if (!Paths.Equal(item.Path, item.Spec.Path) || !Paths.Equal(item.Entry.Path, item.Path) || item.Entry != item.Source.Root) return "مسیر کش قابل تأیید نیست";
            if (authenticate && !Discover(roots, CancellationToken.None).Any(s => Paths.Equal(s.Path, item.Path) && s.Family == item.Spec.Family)) return "مسیر کش قابل تأیید نیست";
            foreach (var entry in item.Contents())
            {
                if (!Paths.Within(entry.Path, item.Path) || (entry.Attributes & (FileAttributes.System | FileAttributes.ReparsePoint | FileAttributes.Offline)) != 0) return "فایل سیستمی، ابری یا پیوندی";
                string spacio = System.IO.Path.Combine(roots.Local, "Spacio");
                if (Paths.Within(entry.Path, spacio) || Paths.Within(spacio, entry.Path)) return "اسکن‌های ذخیره‌شدهٔ Spacio محافظت می‌شوند";
                if (Paths.Within(entry.Path, AppDomain.CurrentDomain.BaseDirectory) || Paths.Within(AppDomain.CurrentDomain.BaseDirectory, entry.Path)) return "مسیر محافظت‌شده؛ از تنظیمات ویندوز مدیریت شود";
            }
            return Running(item.Spec);
        }
        internal static List<CleanupItem> Candidates(Snapshot snapshot)
        {
            if (snapshot == null) return new List<CleanupItem>();
            return snapshot.Entries.Where(e => e.IsDirectory && (e.Name.Equals("cache", StringComparison.OrdinalIgnoreCase) || e.Name.Equals(".cache", StringComparison.OrdinalIgnoreCase)))
                .Select(e => new CleanupItem { Path = e.Path, Owner = "از اسکن بازشده", Evidence = "فقط نام پوشه؛ ارتباط و قابل‌بازسازی‌بودن تأیید نشده",
                    Candidate = true, Entry = e, Source = snapshot, Failure = Safety.BlockReason(e, snapshot) ?? "" }).ToList();
        }
    }

    internal sealed class InstalledApp
    {
        internal string Id, Command, Family, DisplayIcon, Provider;
        internal bool Msi;
        public string Name { get; internal set; }
        public string Publisher { get; internal set; }
        public string Version { get; internal set; }
        public string Location { get; internal set; }
    }
    internal static class InstalledApps
    {
        internal static string Family(string name)
        {
            name = (name ?? "").ToLowerInvariant();
            if (name.StartsWith("google chrome")) return "chrome";
            if (name == "microsoft edge") return "edge";
            if (name.StartsWith("mozilla firefox")) return "firefox";
            if (name == "brave" || name.StartsWith("brave browser")) return "brave";
            if (name.Contains("visual studio code")) return "code";
            if (name.StartsWith("discord")) return "discord";
            if (name == "slack") return "slack";
            if (name == "notion" || System.Text.RegularExpressions.Regex.IsMatch(name, @"^notion \d+(\.\d+)*$")) return "notion";
            if (name == "steam") return "steam";
            if (name.StartsWith("adobe premiere") || name.StartsWith("adobe after effects") || name.StartsWith("adobe media encoder")) return "adobe-media";
            return "";
        }
        internal static List<InstalledApp> Read(CancellationToken token, bool strict = false)
        {
            var result = new List<InstalledApp>();
            foreach (var hive in new [] { RegistryHive.CurrentUser, RegistryHive.LocalMachine })
                foreach (var view in new [] { RegistryView.Registry64, RegistryView.Registry32 })
                    try
                    {
                        using (var root = RegistryKey.OpenBaseKey(hive, view))
                        using (var uninstall = root.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (uninstall == null) continue;
                            foreach (string name in uninstall.GetSubKeyNames())
                            {
                                token.ThrowIfCancellationRequested();
                                using (var key = uninstall.OpenSubKey(name))
                                {
                                    if (key == null || (!strict && (Convert.ToString(key.GetValue("SystemComponent")) == "1" || key.GetValue("ParentKeyName") != null))) continue;
                                    string label = Convert.ToString(key.GetValue("DisplayName")), command = Convert.ToString(key.GetValue("UninstallString"));
                                    // Ownership checks must include hidden child components
                                    // even when they have no separately callable uninstaller.
                                    if (!strict && (String.IsNullOrWhiteSpace(label) || String.IsNullOrWhiteSpace(command))) continue;
                                    if (String.IsNullOrWhiteSpace(label)) label = name;
                                    var app = new InstalledApp { Id = hive + "/" + view + "/" + name, Name = label, Version = Convert.ToString(key.GetValue("DisplayVersion")), Publisher = Convert.ToString(key.GetValue("Publisher")),
                                        Location = Convert.ToString(key.GetValue("InstallLocation")).Trim('"', ' '), DisplayIcon = Convert.ToString(key.GetValue("DisplayIcon")),
                                        Command = command, Msi = Convert.ToString(key.GetValue("WindowsInstaller")) == "1", Family = Family(label) };
                                    app.Location = ResolveLocation(app); result.Add(app);
                                }
                            }
                        }
                    }
                    catch (System.Security.SecurityException) { if (strict) throw; } catch (UnauthorizedAccessException) { if (strict) throw; } catch (IOException) { if (strict) throw; }
            var hermes = DetectHermes(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "hermes"));
            if (hermes != null && !result.Any(a => !String.IsNullOrEmpty(a.Location) && Paths.Equal(a.Location, hermes.Location))) result.Add(hermes);
            return result.GroupBy(a => a.Name + "\n" + a.Version + "\n" + a.Command, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).OrderBy(a => a.Name).ToList();
        }
        internal static InstalledApp DetectHermes(string root)
        {
            try
            {
                root = Paths.Normalize(root); if (ReviewedRemoval.ProtectedReason(root) != null) return null;
                string executable = Path.Combine(root, "bin", "hermes.exe"), project = Path.Combine(root, "hermes-agent", "pyproject.toml"),
                    uninstaller = Path.Combine(root, "hermes-agent", "hermes_cli", "uninstall.py"), parser = Path.Combine(root, "hermes-agent", "hermes_cli", "subcommands", "uninstall.py");
                foreach (var file in new [] { executable, project, uninstaller, parser }) { if (!File.Exists(file)) return null; Paths.CheckAncestors(file); }
                if (new FileInfo(project).Length > 1024 * 1024 || new FileInfo(parser).Length > 128 * 1024) return null;
                string metadata = File.ReadAllText(project), commands = File.ReadAllText(parser);
                if (!System.Text.RegularExpressions.Regex.IsMatch(metadata, @"(?m)^name\s*=\s*[""']hermes-agent[""']\s*$") || !metadata.Contains("hermes_cli.main:main") || !commands.Contains("--dry-run") || !commands.Contains("--full")) return null;
                return new InstalledApp { Id = "supported/hermes/" + root, Name = "Hermes Agent", Publisher = "Nous Research", Version = "", Location = root,
                    Command = "\"" + executable + "\" uninstall", Family = "", Provider = "hermes" };
            }
            catch (IOException) { return null; } catch (UnauthorizedAccessException) { return null; } catch (ArgumentException) { return null; }
        }
        internal static InstalledApp Owner(string path, IEnumerable<InstalledApp> apps)
        {
            return apps.Where(a => {
                try { return !String.IsNullOrWhiteSpace(a.Location) && System.IO.Path.IsPathRooted(a.Location) && Paths.Normalize(a.Location).Length > 3 && Paths.Within(path, a.Location) &&
                    (a.Provider != "hermes" || Paths.Equal(path, a.Location) || Paths.Within(path, Path.Combine(a.Location, "hermes-agent")) || Paths.Within(path, Path.Combine(a.Location, "bin"))); }
                catch (ArgumentException) { return false; } catch (NotSupportedException) { return false; }
            }).OrderByDescending(a => a.Location.Length).FirstOrDefault();
        }
        internal static string ResolveLocation(InstalledApp app, string[] programRoots = null)
        {
            if (!String.IsNullOrWhiteSpace(app.Location)) return Environment.ExpandEnvironmentVariables(app.Location);
            if (app.Msi || String.IsNullOrWhiteSpace(app.DisplayIcon)) return "";
            try
            {
                // Some per-user installers (including Notion) omit InstallLocation.
                // Require two agreeing registered executable paths inside a dedicated
                // program directory. Never infer ownership from the folder name.
                string executable = StartInfo(app).FileName;
                string icon = Environment.ExpandEnvironmentVariables(app.DisplayIcon).Trim();
                int comma = icon.LastIndexOf(','); int index;
                if (comma >= 0 && Int32.TryParse(icon.Substring(comma + 1).Trim(), out index)) icon = icon.Substring(0, comma).Trim();
                icon = icon.Trim('"');
                if (!Path.IsPathRooted(icon) || !icon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(icon)) return "";
                string directory = Paths.Normalize(Path.GetDirectoryName(executable));
                if (!Paths.Equal(directory, Path.GetDirectoryName(icon))) return "";
                var roots = programRoots ?? new [] { Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs") };
                if (!roots.Any(root => !String.IsNullOrWhiteSpace(root) && !Paths.Equal(directory, root) && Paths.Within(directory, root))) return "";
                Paths.RequireLocal(icon); Paths.CheckAncestors(icon); Paths.CheckAncestors(executable);
                return directory;
            }
            catch (IOException) { return ""; } catch (UnauthorizedAccessException) { return ""; }
            catch (ArgumentException) { return ""; } catch (NotSupportedException) { return ""; } catch (System.Security.SecurityException) { return ""; }
        }
        internal static ProcessStartInfo StartInfo(InstalledApp app)
        {
            string command = Environment.ExpandEnvironmentVariables(app.Command).Trim(), executable, arguments;
            if (app.Msi)
            {
                var match = System.Text.RegularExpressions.Regex.Match(command, @"\{[0-9A-Fa-f]{8}-(?:[0-9A-Fa-f]{4}-){3}[0-9A-Fa-f]{12}\}");
                if (!match.Success) throw new IOException(L.T("فرمان حذف قابل تأیید نیست؛ از تنظیمات ویندوز استفاده کنید"));
                executable = System.IO.Path.Combine(Environment.SystemDirectory, "msiexec.exe"); arguments = "/x " + match.Value;
            }
            else
            {
                if (command.StartsWith("\"", StringComparison.Ordinal))
                {
                    int end = command.IndexOf('"', 1); if (end < 2) throw new IOException(L.T("فرمان حذف قابل تأیید نیست؛ از تنظیمات ویندوز استفاده کنید"));
                    executable = command.Substring(1, end - 1); arguments = command.Substring(end + 1).Trim();
                }
                else
                {
                    int end = command.IndexOf(".exe", StringComparison.OrdinalIgnoreCase); if (end < 1) throw new IOException(L.T("فرمان حذف قابل تأیید نیست؛ از تنظیمات ویندوز استفاده کنید"));
                    executable = command.Substring(0, end + 4); arguments = command.Substring(end + 4).Trim();
                }
                if (!System.IO.Path.IsPathRooted(executable) || !executable.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(executable)) throw new IOException(L.T("فرمان حذف قابل تأیید نیست؛ از تنظیمات ویندوز استفاده کنید"));
                Paths.RequireLocal(Paths.Normalize(executable));
                // Registered commands are shown for review, but shell/script launchers
                // belong in Windows Settings instead of this executable-only workflow.
                string name = System.IO.Path.GetFileNameWithoutExtension(executable);
                if (new [] { "cmd", "powershell", "pwsh", "wscript", "cscript", "mshta", "rundll32", "regsvr32" }.Contains(name, StringComparer.OrdinalIgnoreCase))
                    throw new IOException(L.T("فرمان حذف قابل تأیید نیست؛ از تنظیمات ویندوز استفاده کنید"));
            }
            Paths.CheckAncestors(executable);
            var start = new ProcessStartInfo(executable, arguments) { UseShellExecute = true, WorkingDirectory = Environment.SystemDirectory };
            if (app.Provider == "hermes")
            {
                start.UseShellExecute = false; start.EnvironmentVariables["HERMES_HOME"] = app.Location;
                start.EnvironmentVariables.Remove("HERMES_PROFILE");
            }
            return start;
        }
    }

    internal static class CleanupEngine
    {
        // Fresh previews authorize only eligible selections, never a stale drive tree.
        internal static CleanupItem InspectSelection(Entry selected, Snapshot saved, CancellationToken token)
        {
            var item = new CleanupItem { Path = selected.Path, Owner = selected.Name, Entry = selected, Source = saved,
                Evidence = "موردی که در فهرست فایل‌ها انتخاب کردید" };
            if (saved == null || saved.Cancelled) { item.Failure = "ابتدا اسکن کامل انجام دهید"; return item; }
            var ancestor = selected; while (ancestor.Parent != null) ancestor = ancestor.Parent;
            if (!Object.ReferenceEquals(ancestor, saved.Root) || !Paths.Within(selected.Path, saved.Root.Path)) { item.Failure = "مورد به این اسکن تعلق ندارد"; return item; }
            if (Paths.Equal(selected.Path, saved.Root.Path)) { item.Failure = "ریشهٔ اسکن قابل حذف نیست"; return item; }
            try
            {
                token.ThrowIfCancellationRequested();
                var plan = ReviewedRemoval.Prepare(selected.Path, token); var fresh = plan.Root;
                var entries = plan.Nodes.Select(n => n.Entry).ToList();
                // Copy ancestor identities for safety checks, without scanning siblings
                // or changing any entry in the displayed or persisted snapshot.
                var ancestors = new Stack<Entry>();
                for (var parent = selected.Parent; parent != null; parent = parent.Parent) ancestors.Push(parent);
                Entry root = null, current = null;
                foreach (var parent in ancestors)
                {
                    var copy = new Entry { Path = parent.Path, Name = parent.Name, IsDirectory = true, Attributes = parent.Attributes, Parent = current };
                    if (current == null) root = copy; else current.Children.Add(copy);
                    current = copy; entries.Add(copy);
                }
                fresh.Parent = current; current.Children.Add(fresh);
                item.Entry = fresh; item.Source = new Snapshot { Root = root, Entries = entries };
                item.Removal = plan; item.Chosen = item.CanChoose;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { item.Failure = e.Message; item.Chosen = false; }
            return item;
        }
        internal static List<CleanupItem> Collapse(IEnumerable<CleanupItem> items)
        {
            var result = new List<CleanupItem>();
            foreach (var item in items.OrderBy(i => i.Path.Length))
                if (!result.Any(p => Paths.Within(item.Path, p.Path))) result.Add(item);
            return result;
        }
        internal static Task<List<string>> Execute(List<CleanupItem> items, bool permanent, IntPtr owner, CancellationToken token, CacheRoots roots = null)
        {
            var completion = new TaskCompletionSource<List<string>>();
            var thread = new Thread(() => {
                var reports = new List<string>();
                try
                {
                    foreach (var item in Collapse(items))
                    {
                        token.ThrowIfCancellationRequested();
                        try
                        {
                            if (item.Leftover != null) AppLeftovers.VerifyForDeletion(item.Leftover, token);
                            if (item.Removal != null)
                            {
                                if (!item.CanChoose || !Paths.Equal(item.Path, item.Removal.Root.Path)) throw new IOException("Invalid reviewed removal plan.");
                                if (permanent) item.Removal.DeletePermanent(token);
                                else { item.Removal.VerifyForRecycle(token); Recycler.DeleteReviewedPath(item.Path, owner, false); }
                                reports.Add("✓ " + item.Path); continue;
                            }
                            string reason = item.Spec == null ? Safety.BlockReason(item.Entry, item.Source) : CacheCatalog.BlockCache(item, roots ?? new CacheRoots());
                            if (reason != null) throw new IOException(L.T(reason));
                            Paths.RequireLocal(Paths.Normalize(item.Path)); Paths.CheckAncestors(item.Path);
                            Scanner.VerifyUnchanged(item.Entry, token);
                            // Keep cache roots themselves. Delete only the contents shown in review.
                            var entries = item.Spec == null ? new [] { item.Entry } : item.Entry.Children.ToArray();
                            foreach (var entry in entries)
                            {
                                token.ThrowIfCancellationRequested();
                                try
                                {
                                    if (item.Spec != null)
                                    {
                                        reason = CacheCatalog.Running(item.Spec); if (reason != null) throw new IOException(reason);
                                        if (!Paths.Within(entry.Path, item.Path) || Paths.Equal(entry.Path, item.Path)) throw new IOException("Invalid cleanup boundary.");
                                    }
                                    Paths.CheckAncestors(entry.Path); Scanner.VerifyUnchanged(entry, token);
                                    Recycler.DeleteReviewedPath(entry.Path, owner, permanent);
                                    reports.Add("✓ " + entry.Path);
                                }
                                catch (Exception e) { if (e is OperationCanceledException) throw; reports.Add("✕ " + entry.Path + "\n" + L.T(e.Message)); }
                            }
                            if (entries.Length == 0) reports.Add("— " + item.Path + " · " + L.T("خالی است"));
                        }
                        catch (Exception e) { if (e is OperationCanceledException) throw; reports.Add("✕ " + item.Path + "\n" + L.T(e.Message)); }
                    }
                    completion.SetResult(reports);
                }
                catch (OperationCanceledException) { reports.Add(L.T("عملیات متوقف شد؛ موارد انجام‌شده در گزارش مشخص‌اند")); completion.SetResult(reports); }
                catch (Exception e) { completion.SetException(e); }
            });
            thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); return completion.Task;
        }
    }
}
