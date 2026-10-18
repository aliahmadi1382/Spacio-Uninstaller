using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Spacio
{
    internal sealed class LeftoverCandidate
    {
        internal UninstallSession Session;
        internal string Path, Kind, Evidence;
        internal bool Installation, Tentative;
        internal CacheSpec Cache;
    }
    internal sealed class UninstallTracker
    {
        private int settled;
        internal bool Observe(bool processKnown, bool exited, int? code, bool registrationKnown, bool registered, bool running)
        {
            if (!processKnown || !exited || code != 0 || !registrationKnown || registered || running) { settled = 0; return false; }
            return ++settled >= 3;
        }
    }
    internal sealed class UninstallSession : IDisposable
    {
        internal InstalledApp App;
        internal CacheRoots Roots;
        internal readonly List<LeftoverCandidate> Candidates = new List<LeftoverCandidate>();
        internal readonly UninstallTracker Tracker = new UninstallTracker();
        internal Process Process;
        internal bool ManualFinish;
        internal bool Exited { get { try { return Process == null || Process.HasExited; } catch (InvalidOperationException) { return false; } } }
        internal int? ExitCode { get { try { return Process != null && Process.HasExited ? (int?)Process.ExitCode : null; } catch (InvalidOperationException) { return null; } } }
        public void Dispose() { if (Process != null) Process.Dispose(); Process = null; }
    }
    internal static class AppLeftovers
    {
        internal static bool SameApp(InstalledApp app, InstalledApp original)
        { return app.Id == original.Id || (!String.IsNullOrEmpty(app.Command) && app.Command == original.Command) || String.Equals(app.Name, original.Name, StringComparison.OrdinalIgnoreCase); }
        private static bool Overlap(string a, string b)
        { return !String.IsNullOrWhiteSpace(a) && !String.IsNullOrWhiteSpace(b) && (Paths.Within(a, b) || Paths.Within(b, a)); }
        private static bool ProductSegment(string name)
        {
            if (String.IsNullOrWhiteSpace(name) || name.Length < 3 || name.Length > 100 || name != name.Trim() || name == "." || name == ".." || name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) return false;
            return !new [] { "Programs", "Program Files", "Program Files (x86)", "Common Files", "Microsoft", "Windows", "WindowsApps", "Users", "AppData", "Local", "Roaming", "Temp", "Spacio", "Desktop", "Documents", "Application", "Applications", "app", "bin", "data", "cache" }.Contains(name, StringComparer.OrdinalIgnoreCase);
        }
        internal static UninstallSession Capture(InstalledApp app, CacheRoots roots, CancellationToken token)
        {
            // Capture before registration disappears. Only bounded exact paths are
            // inspected later; product-name matches are suggestions, never proof.
            var session = new UninstallSession { App = app, Roots = roots };
            Action<string, string, string, bool, bool> add = (path, kind, evidence, installation, tentative) => {
                token.ThrowIfCancellationRequested(); if (String.IsNullOrWhiteSpace(path)) return;
                try
                {
                    path = Paths.Normalize(path); Paths.RequireLocal(path);
                    if (session.Candidates.Any(c => Paths.Equal(c.Path, path))) return;
                    session.Candidates.Add(new LeftoverCandidate { Session = session, Path = path, Kind = kind, Evidence = evidence, Installation = installation, Tentative = tentative });
                }
                catch (ArgumentException) { } catch (IOException) { } catch (NotSupportedException) { }
            };
            if (!String.IsNullOrWhiteSpace(app.Location) && ProductSegment(Path.GetFileName(app.Location.TrimEnd('\\'))))
                add(app.Location, "پوشهٔ نصب باقی‌مانده", "مسیر نصب ثبت‌شده پیش از اجرای حذف‌کننده", true, false);
            Action<string> known = path => add(path, "داده‌های برنامه", "مسیر دادهٔ شناخته‌شده؛ شامل تنظیمات، حساب‌ها و تاریخچه", false, false);
            switch (app.Family ?? "")
            {
                case "notion": known(Path.Combine(roots.Roaming, "Notion")); break;
                case "chrome": if (app.Name.Equals("Google Chrome", StringComparison.OrdinalIgnoreCase)) known(Path.Combine(roots.Local, @"Google\Chrome\User Data")); break;
                case "edge": if (app.Name.Equals("Microsoft Edge", StringComparison.OrdinalIgnoreCase)) known(Path.Combine(roots.Local, @"Microsoft\Edge\User Data")); break;
                case "brave": if (app.Name.Equals("Brave", StringComparison.OrdinalIgnoreCase) || app.Name.Equals("Brave Browser", StringComparison.OrdinalIgnoreCase)) known(Path.Combine(roots.Local, @"BraveSoftware\Brave-Browser\User Data")); break;
                case "firefox": known(Path.Combine(roots.Local, @"Mozilla\Firefox")); known(Path.Combine(roots.Roaming, @"Mozilla\Firefox")); break;
                case "code": known(Path.Combine(roots.Roaming, app.Name.IndexOf("Insiders", StringComparison.OrdinalIgnoreCase) >= 0 ? "Code - Insiders" : "Code")); break;
                case "discord": known(Path.Combine(roots.Roaming, app.Name.IndexOf("canary", StringComparison.OrdinalIgnoreCase) >= 0 ? "discordcanary" : app.Name.IndexOf("ptb", StringComparison.OrdinalIgnoreCase) >= 0 ? "discordptb" : "discord")); break;
                case "slack": known(Path.Combine(roots.Roaming, "Slack")); break;
            }
            if (app.Provider == "hermes") known(app.Location);
            var names = new List<string> { app.Name };
            if (!String.IsNullOrWhiteSpace(app.Location)) names.Add(Path.GetFileName(app.Location.TrimEnd('\\')));
            foreach (string name in names.Where(ProductSegment).Distinct(StringComparer.OrdinalIgnoreCase))
                foreach (string root in new [] { roots.Local, roots.Roaming, roots.Common })
                {
                    add(Path.Combine(root, name), "احتمالی؛ نیازمند بررسی", "تطبیق دقیق نام؛ ارتباط با برنامه تأیید نشده است", false, true);
                    if (ProductSegment(app.Publisher)) add(Path.Combine(root, app.Publisher, name), "احتمالی؛ نیازمند بررسی", "تطبیق نام سازنده و محصول؛ ارتباط تأیید نشده است", false, true);
                }
            AddCaches(session, token); return session;
        }
        private static void AddCaches(UninstallSession session, CancellationToken token)
        {
            if (String.IsNullOrEmpty(session.App.Family)) return;
            var knownRoots = session.Candidates.Where(c => !c.Installation && !c.Tentative && c.Cache == null).Select(c => c.Path).ToArray();
            foreach (var spec in CacheCatalog.Discover(session.Roots, token).Where(s => s.Family == session.App.Family &&
                (s.Shared || session.App.Family == "steam" || knownRoots.Any(p => Paths.Within(s.Path, p)))))
            {
                if (session.Candidates.Any(c => Paths.Equal(c.Path, spec.Path))) continue;
                session.Candidates.Add(new LeftoverCandidate { Session = session, Path = spec.Path, Cache = spec,
                    Kind = spec.Shared ? "کش مشترک" : "کش شناخته‌شده", Evidence = spec.Reason });
            }
        }
        internal static bool RunningAt(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return false;
            foreach (var process in Process.GetProcesses())
                using (process)
                    try { var module = process.MainModule; if (module != null && Paths.Within(module.FileName, path)) return true; }
                    catch (System.ComponentModel.Win32Exception) { } catch (InvalidOperationException) { } catch (NotSupportedException) { }
            return false;
        }
        internal static string Block(LeftoverCandidate candidate, List<InstalledApp> apps, bool running)
        {
            var session = candidate.Session;
            if (!session.Exited || running) return "حذف‌کننده یا برنامه هنوز در حال اجراست؛ پس از پایان دوباره بررسی کنید";
            bool registered = apps.Any(a => SameApp(a, session.App));
            if (registered && (candidate.Installation || !session.ManualFinish)) return "حذف برنامه هنوز تأیید نشده است؛ پس از پایان حذف‌کننده دوباره بررسی کنید";
            if (apps.Any(a => (candidate.Installation || !SameApp(a, session.App)) && Overlap(a.Location, candidate.Path))) return "این مسیر هنوز متعلق به یک برنامهٔ نصب‌شده است";
            if (candidate.Cache != null) return CacheCatalog.Running(candidate.Cache);
            return null;
        }
        internal static CleanupItem Inspect(LeftoverCandidate candidate, CancellationToken token, List<InstalledApp> apps = null, bool? running = null)
        {
            var item = new CleanupItem { Path = candidate.Path, Owner = candidate.Session.App.Name, Evidence = candidate.Evidence, Leftover = candidate, Candidate = true };
            try
            {
                apps = apps ?? InstalledApps.Read(token, true);
                bool ownerRunning = running ?? RunningAt(candidate.Session.App.Location);
                string blocked = Block(candidate, apps, ownerRunning);
                if (candidate.Cache != null)
                {
                    item = CacheCatalog.Inspect(candidate.Cache, token, candidate.Session.Roots); item.Leftover = candidate; item.Candidate = true;
                    if (blocked != null) item.Failure = L.T(blocked);
                }
                else
                {
                    // Program Files exception is limited to the captured registered
                    // installation, after absence and overlap checks, never name matches.
                    if (blocked == null)
                    {
                        string boundary = candidate.Installation ? candidate.Path : null;
                        item.Removal = ReviewedRemoval.Prepare(candidate.Path, token, boundary); item.Removal.AppData = true;
                        item.Entry = item.Removal.Root; item.Source = new Snapshot { Root = item.Entry, Entries = item.Removal.Nodes.Select(n => n.Entry).ToList() };
                    }
                    else
                    {
                        item.Source = new Scanner().Scan(candidate.Path, token, null); item.Entry = item.Source.Root; item.Failure = L.T(blocked);
                    }
                }
                bool shared = !String.IsNullOrEmpty(candidate.Session.App.Family) && apps.Any(a => !SameApp(a, candidate.Session.App) && a.Family == candidate.Session.App.Family);
                if (shared) item.Evidence = candidate.Evidence + "\n" + "برنامهٔ دیگری از همین خانواده نصب است؛ داده‌ها ممکن است مشترک باشند";
                if (candidate.Session.ManualFinish && apps.Any(a => SameApp(a, candidate.Session.App)))
                    item.Evidence = candidate.Evidence + "\n" + "برنامه هنوز ثبت‌شده است؛ این بررسی دستی تأیید موفقیت حذف نیست";
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception e) { item.Failure = e.Message; }
            item.Chosen = false; return item;
        }
        internal static List<CleanupItem> Scan(UninstallSession session, CancellationToken token, IProgress<string> progress = null)
        {
            var apps = InstalledApps.Read(token, true); bool running = RunningAt(session.App.Location);
            return Scan(session, token, apps, running, progress);
        }
        internal static List<CleanupItem> Scan(UninstallSession session, CancellationToken token, List<InstalledApp> apps, bool running, IProgress<string> progress = null)
        {
            AddCaches(session, token); var rows = new List<CleanupItem>();
            foreach (var candidate in session.Candidates)
            {
                token.ThrowIfCancellationRequested();
                try { File.GetAttributes(candidate.Path); }
                catch (FileNotFoundException) { continue; } catch (DirectoryNotFoundException) { continue; }
                catch (Exception e) { rows.Add(new CleanupItem { Path = candidate.Path, Owner = session.App.Name, Leftover = candidate, Candidate = true, Evidence = candidate.Evidence, Failure = e.Message }); continue; }
                if (progress != null) progress.Report(candidate.Path);
                rows.Add(Inspect(candidate, token, apps, running));
            }
            return rows;
        }
        internal static void VerifyForDeletion(LeftoverCandidate candidate, CancellationToken token)
        {
            var apps = InstalledApps.Read(token, true);
            string blocked = Block(candidate, apps, RunningAt(candidate.Session.App.Location));
            if (blocked != null) throw new IOException(L.T(blocked));
        }
    }
}
