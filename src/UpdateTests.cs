using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Web.Script.Serialization;

namespace Spacio
{
    public static class UpdateTests
    {
        private static void Reject(Action action, string message, Action<bool, string> check)
        {
            bool rejected = false; try { action(); } catch (InvalidDataException) { rejected = true; }
            check(rejected, message);
        }
        private static string Release(string tag = "v2.0.0", string digest = null, string url = null, bool preview = false)
        {
            return new JavaScriptSerializer().Serialize(new { tag_name = tag, draft = false, prerelease = preview, assets = new [] { new {
                name = "Spacio-2.0.0-win-x64.zip", state = "uploaded", size = 1024,
                digest = digest ?? "sha256:" + new String('a', 64),
                browser_download_url = url ?? Updates.Repository + "/releases/download/v2.0.0/Spacio-2.0.0-win-x64.zip"
            } } });
        }
        private static void Package(string path, bool duplicate = false)
        {
            using (var archive = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
            {
                foreach (string name in new [] { "Spacio.exe", "Spacio.exe.config" })
                    using (var input = File.OpenRead(Assembly.GetExecutingAssembly().Location + (name.EndsWith(".config") ? ".config" : "")))
                    using (var output = archive.CreateEntry(name).Open()) input.CopyTo(output);
                using (var writer = new StreamWriter(archive.CreateEntry("../outside.txt").Open())) writer.Write("must not be extracted");
                if (duplicate) using (var writer = new StreamWriter(archive.CreateEntry("Spacio.exe").Open())) writer.Write("duplicate");
            }
        }
        public static int Run(string report)
        {
            var log = new List<string>(); Action<bool, string> check = (ok, label) => { if (!ok) throw new Exception("FAIL: " + label); log.Add("PASS: " + label); };
            string workspace = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(report))).FullName;
            string area = Path.Combine(workspace, ".test-data", "updates-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(area);
            try
            {
                var old = new Version(1, 3, 0, 0);
                check(Updates.ParseRelease(Release(), old).Version == new Version(2, 0, 0, 0), "New stable release is offered");
                check(Updates.ParseRelease(Release(), new Version(2, 0, 0, 0)) == null, "Same version is never installed again");
                check(Updates.ParseRelease(Release(), new Version(2, 1, 0, 0)) == null, "Older release never downgrades the app");
                check(Updates.ParseRelease(Release(preview: true), old) == null, "Prereleases are excluded");
                check(Updates.ParseRelease(Release(tag: "v2.0.0-beta"), old) == null, "Nonstable tags are excluded");
                check(Updates.ParseRelease(Release().Replace("\"draft\":false", "\"draft\":true"), old) == null, "Draft releases are excluded");
                Reject(() => Updates.ParseRelease(Release(digest: ""), old), "Missing checksum is rejected", check);
                Reject(() => Updates.ParseRelease(Release(url: "https://example.com/update.zip"), old), "External package source is rejected", check);
                Reject(() => Updates.ParseRelease(Release().Replace("1024", "999999999"), old), "Oversized package is rejected", check);
                Reject(() => Updates.ParseRelease(Release().Replace("uploaded", "new"), old), "Incomplete release is rejected", check);
                check(Updates.AllowedHost(new Uri("https://release-assets.githubusercontent.com/file")) && !Updates.AllowedHost(new Uri("http://github.com/file")) && !Updates.AllowedHost(new Uri("https://github.com.example.org/file")) && !Updates.AllowedHost(new Uri("https://github.com:8080/file")), "Redirects require HTTPS and exact approved hosts");
                bool cancelled = false;
                using (var cancel = new CancellationTokenSource()) { cancel.Cancel(); try { Updates.CheckAsync(cancel.Token).GetAwaiter().GetResult(); } catch (OperationCanceledException) { cancelled = true; } }
                check(cancelled, "Update checks support cancellation");
                Reject(() => Updates.CopyBounded(new MemoryStream(new byte[20]), new MemoryStream(), 10, null, CancellationToken.None).GetAwaiter().GetResult(), "Streaming download rejects data beyond declared size", check);
                string stage = Path.Combine(area, "verified package"), zip = Path.Combine(area, "package.zip"); Directory.CreateDirectory(stage); Package(zip);
                var release = new ReleaseInfo { Version = Updates.Current, Sha256 = Updates.Hash(zip) };
                Updates.StagePackage(zip, stage, release);
                check(Updates.Hash(Path.Combine(stage, "Spacio.exe")) == Updates.Hash(Assembly.GetExecutingAssembly().Location), "Verified package extracts the exact application");
                check(!File.Exists(Path.Combine(area, "outside.txt")), "Archive traversal entry is never extracted");
                Reject(() => Updates.StagePackage(zip, stage, new ReleaseInfo { Version = Updates.Current, Sha256 = new String('0', 64) }), "Tampered package fails SHA256 verification", check);
                Reject(() => Updates.StagePackage(zip, stage, new ReleaseInfo { Version = new Version(99, 0, 0, 0), Sha256 = Updates.Hash(zip) }), "Executable version must match release version", check);
                string duplicate = Path.Combine(area, "duplicate.zip"); Package(duplicate, true);
                Reject(() => Updates.StagePackage(duplicate, stage, new ReleaseInfo { Version = Updates.Current, Sha256 = Updates.Hash(duplicate) }), "Duplicate application entries are rejected", check);
                string targetDir = Path.Combine(area, "نصب آزمایشی portable"); Directory.CreateDirectory(targetDir); string target = Path.Combine(targetDir, "Spacio.exe");
                File.WriteAllText(target, "old executable fixture"); File.WriteAllText(target + ".config", "old config fixture");
                string originalHash = Updates.Hash(target);
                var job = new InstallJob { Target = target, ExecutableHash = Updates.Hash(Path.Combine(stage, "Spacio.exe")), ConfigHash = Updates.Hash(Path.Combine(stage, "Spacio.exe.config")), Version = Updates.Current.ToString() };
                bool failed = false;
                try { UpdateInstaller.ReplaceFiles(stage, job, () => { throw new IOException("Simulated locked executable."); }); } catch (IOException) { failed = true; }
                check(failed && Updates.Hash(target) == originalHash && File.ReadAllText(target + ".config") == "old config fixture", "Replacement failure preserves old executable and restores its config");
                UpdateInstaller.ReplaceFiles(stage, job);
                check(Updates.Hash(target) == job.ExecutableHash && Updates.Hash(target + ".config") == job.ConfigHash, "Atomic replacement installs application and config");
                check(Directory.GetFiles(targetDir, "*.previous.exe").Any(p => Updates.Hash(p) == originalHash), "Previous executable remains available for recovery");
                File.AppendAllText(Path.Combine(stage, "Spacio.exe.config"), "tampered");
                Reject(() => UpdateInstaller.ReplaceFiles(stage, job), "Staged files are verified again before replacement", check);
                Updates.StagePackage(zip, stage, release);
                string signal = Path.Combine(area, "host"), result = Path.Combine(area, "restart.txt");
                using (var parent = Process.Start(new ProcessStartInfo(target, "--update-test-host " + UpdateInstaller.Quote(signal)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
                {
                    try
                    {
                        var watch = Stopwatch.StartNew(); while (!File.Exists(signal + ".ready") && watch.ElapsedMilliseconds < 10000) Thread.Sleep(50);
                        check(File.Exists(signal + ".ready"), "Isolated old application process starts");
                        using (var helper = UpdateInstaller.Start(stage, target, parent, "--update-test-probe " + UpdateInstaller.Quote(result)))
                        {
                            Thread.Sleep(200); check(!File.Exists(Path.Combine(stage, "installed.txt")), "Updater waits for the previous process to exit");
                            File.WriteAllText(signal + ".exit", "exit");
                            check(helper.WaitForExit(15000) && helper.ExitCode == 0, "Separate updater replaces files after application exit");
                            watch.Restart(); while (!File.Exists(result) && watch.ElapsedMilliseconds < 10000) Thread.Sleep(50);
                            check(File.Exists(result) && File.ReadAllText(result) == Updates.VersionText, "Installed executable restarts successfully from a Unicode path");
                        }
                    }
                    finally { File.WriteAllText(signal + ".exit", "exit"); parent.WaitForExit(5000); }
                }
                log.Add("All " + log.Count + " update checks passed.");
                File.WriteAllLines(report, log); return 0;
            }
            catch (Exception e) { log.Add(e.ToString()); File.WriteAllLines(report, log); return 1; }
        }
        public static int Live(string report)
        {
            try
            {
                var release = Updates.CheckAsync(CancellationToken.None, new Version(0, 0, 0, 0)).GetAwaiter().GetResult();
                if (release == null) throw new Exception("No stable release found.");
                string stage = Updates.DownloadAsync(release, null, CancellationToken.None).GetAwaiter().GetResult();
                File.WriteAllText(report, "PASS: official release discovery, HTTPS asset download, SHA256 and executable version verification\nVersion: " + release.Version + "\nStaged: " + stage); return 0;
            }
            catch (Exception e) { File.WriteAllText(report, e.ToString()); return 1; }
        }
    }
}
