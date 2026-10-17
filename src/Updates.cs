using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace Spacio
{
    public sealed class ReleaseInfo
    {
        public Version Version;
        public string AssetUrl, Sha256, PageUrl;
        public long Size;
    }

    public static class Updates
    {
        public const string Repository = "https://github.com/aliahmadi1382/Spacio-Uninstaller";
        public const string LatestApi = "https://api.github.com/repos/aliahmadi1382/Spacio-Uninstaller/releases/latest";
        public static Version Current { get { return Assembly.GetExecutingAssembly().GetName().Version; } }
        public static string VersionText { get { return Current.ToString(3); } }
        private const long MaxPackageSize = 64 * 1024 * 1024;

        public static ReleaseInfo ParseRelease(string json, Version current)
        {
            var serializer = new JavaScriptSerializer { MaxJsonLength = 1024 * 1024, RecursionLimit = 32 };
            var release = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (release == null) throw new InvalidDataException("Invalid release information.");
            if (Flag(release, "draft") || Flag(release, "prerelease")) return null;
            string tag = Text(release, "tag_name");
            if (!Regex.IsMatch(tag, @"^v\d+\.\d+\.\d+$")) return null;
            Version version;
            if (!Version.TryParse(tag.Substring(1) + ".0", out version)) return null;
            if (version <= current) return null;
            object raw; var assets = release.TryGetValue("assets", out raw) ? raw as object[] : null;
            string name = "Spacio-" + version.ToString(3) + "-win-x64.zip";
            if (assets != null) foreach (var item in assets)
            {
                var asset = item as Dictionary<string, object>;
                if (asset == null || Text(asset, "name") != name || Text(asset, "state") != "uploaded") continue;
                string url = Text(asset, "browser_download_url"), digest = Text(asset, "digest"); long size;
                string expectedUrl = Repository + "/releases/download/" + tag + "/" + name;
                if (!String.Equals(url, expectedUrl, StringComparison.Ordinal) || !Regex.IsMatch(digest, @"^sha256:[0-9a-fA-F]{64}$") ||
                    !Int64.TryParse(Text(asset, "size"), out size) || size <= 0 || size > MaxPackageSize)
                    throw new InvalidDataException("اطلاعات بستهٔ آپدیت قابل تأیید نیست.");
                return new ReleaseInfo { Version = version, AssetUrl = url, Sha256 = digest.Substring(7), Size = size, PageUrl = Repository + "/releases/tag/" + tag };
            }
            throw new InvalidDataException("بستهٔ آپدیت هنوز آماده نیست؛ بعداً دوباره بررسی کنید.");
        }
        private static string Text(Dictionary<string, object> data, string key) { object value; return data.TryGetValue(key, out value) && value != null ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : ""; }
        private static bool Flag(Dictionary<string, object> data, string key) { object value; return data.TryGetValue(key, out value) && value is bool && (bool)value; }
        internal static bool AllowedHost(Uri uri)
        {
            return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && String.IsNullOrEmpty(uri.UserInfo) &&
                new [] { "api.github.com", "github.com", "release-assets.githubusercontent.com", "objects.githubusercontent.com" }.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);
        }
        private static async Task<HttpResponseMessage> Get(HttpClient client, string url, CancellationToken cancel)
        {
            var uri = new Uri(url);
            for (int redirects = 0; redirects < 6; redirects++)
            {
                if (!AllowedHost(uri)) throw new InvalidDataException("Untrusted update host.");
                using (var request = new HttpRequestMessage(HttpMethod.Get, uri))
                {
                    request.Headers.UserAgent.ParseAdd("Spacio/" + VersionText);
                    if (uri.Host == "api.github.com") { request.Headers.Accept.ParseAdd("application/vnd.github+json"); request.Headers.Add("X-GitHub-Api-Version", "2022-11-28"); }
                    var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancel).ConfigureAwait(false);
                    int code = (int)response.StatusCode;
                    if (code >= 300 && code <= 399)
                    {
                        var location = response.Headers.Location; response.Dispose();
                        if (location == null) throw new InvalidDataException("Missing update redirect.");
                        uri = new Uri(uri, location); continue;
                    }
                    try { response.EnsureSuccessStatusCode(); return response; } catch { response.Dispose(); throw; }
                }
            }
            throw new InvalidDataException("Too many update redirects.");
        }
        public static async Task<ReleaseInfo> CheckAsync(CancellationToken cancel, Version installed = null)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
            using (var client = new HttpClient(handler))
            {
                timeout.CancelAfter(TimeSpan.FromSeconds(20));
                using (var response = await Get(client, LatestApi, timeout.Token).ConfigureAwait(false))
                using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                using (var data = new MemoryStream())
                {
                    await CopyBounded(source, data, 1024 * 1024, null, timeout.Token).ConfigureAwait(false);
                    return ParseRelease(Encoding.UTF8.GetString(data.ToArray()), installed ?? Current);
                }
            }
        }
        internal static async Task<long> CopyBounded(Stream source, Stream destination, long limit, IProgress<int> progress, CancellationToken cancel)
        {
            var buffer = new byte[65536]; long count = 0; int read;
            while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancel).ConfigureAwait(false)) != 0)
            {
                count += read; if (count > limit) throw new InvalidDataException("Update exceeds its expected size.");
                await destination.WriteAsync(buffer, 0, read, cancel).ConfigureAwait(false);
                if (progress != null) progress.Report((int)(100 * count / limit));
            }
            return count;
        }
        public static string Hash(string path)
        {
            using (var stream = File.OpenRead(path)) using (var hash = SHA256.Create()) return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }
        public static async Task<string> DownloadAsync(ReleaseInfo release, IProgress<int> progress, CancellationToken cancel)
        {
            string stage = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spacio", "Updates", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage); string zip = Path.Combine(stage, "package.zip");
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancel))
            using (var handler = new HttpClientHandler { AllowAutoRedirect = false })
            using (var client = new HttpClient(handler))
            {
                timeout.CancelAfter(TimeSpan.FromMinutes(10));
                try
                {
                    using (var response = await Get(client, release.AssetUrl, timeout.Token).ConfigureAwait(false))
                    using (var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
                    using (var output = File.Create(zip))
                    {
                        long count = await CopyBounded(source, output, release.Size, progress, timeout.Token).ConfigureAwait(false);
                        if (count != release.Size) throw new InvalidDataException("Incomplete update download.");
                    }
                    timeout.Token.ThrowIfCancellationRequested();
                    await Task.Run(() => StagePackage(zip, stage, release), timeout.Token).ConfigureAwait(false);
                    return stage;
                }
                finally { if (File.Exists(zip)) File.Delete(zip); }
            }
        }
        internal static void StagePackage(string zip, string stage, ReleaseInfo release)
        {
            if (!String.Equals(Hash(zip), release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("صحت فایل آپدیت تأیید نشد؛ دوباره تلاش کنید.");
            using (var archive = new ZipArchive(File.OpenRead(zip), ZipArchiveMode.Read))
            {
                foreach (string name in new [] { "Spacio.exe", "Spacio.exe.config" })
                {
                    // Select exact root filenames; never extract arbitrary archive paths.
                    var matches = archive.Entries.Where(e => String.Equals(e.FullName, name, StringComparison.Ordinal)).ToList();
                    if (matches.Count != 1 || matches[0].Length <= 0 || matches[0].Length > MaxPackageSize) throw new InvalidDataException("Invalid update package.");
                    using (var input = matches[0].Open()) using (var output = File.Create(Path.Combine(stage, name)))
                    {
                        var buffer = new byte[65536]; long total = 0; int read;
                        while ((read = input.Read(buffer, 0, buffer.Length)) > 0) { total += read; if (total > MaxPackageSize) throw new InvalidDataException("Oversized update entry."); output.Write(buffer, 0, read); }
                        if (total != matches[0].Length) throw new InvalidDataException("Truncated update entry.");
                    }
                }
            }
            string executable = Path.Combine(stage, "Spacio.exe");
            var identity = AssemblyName.GetAssemblyName(executable);
            if (identity.Name != "Spacio" || identity.Version != release.Version) throw new InvalidDataException("Update version does not match the release.");
        }
    }

    public sealed class InstallJob
    {
        public string Target { get; set; }
        public int ParentId { get; set; }
        public long ParentStarted { get; set; }
        public string ExecutableHash { get; set; }
        public string ConfigHash { get; set; }
        public string Version { get; set; }
        public bool Persian { get; set; }
        public string RestartArguments { get; set; }
    }

    public static class UpdateInstaller
    {
        public static string Quote(string path) { return "\"" + path.Replace("\"", "") + "\""; }
        public static void CheckWritable(string target)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(target));
            if (!File.Exists(target)) throw new FileNotFoundException("Application executable not found.");
            if ((File.GetAttributes(target) & (FileAttributes.ReadOnly | FileAttributes.ReparsePoint)) != 0) throw new IOException("پوشهٔ برنامه قابل نوشتن نیست؛ برنامه را به پوشهٔ شخصی خود منتقل کنید.");
            string probe = Path.Combine(directory, ".spacio-write-" + Guid.NewGuid().ToString("N"));
            using (new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose)) { }
        }
        public static Process Start(string stage, string target, Process parent, string restartArguments = "")
        {
            CheckWritable(target);
            // Run the trusted installed updater code, not code from a downloaded package.
            string helper = Path.Combine(stage, "Spacio.Update.exe");
            File.Copy(Assembly.GetExecutingAssembly().Location, helper, true);
            string ownConfig = Assembly.GetExecutingAssembly().Location + ".config";
            if (File.Exists(ownConfig)) File.Copy(ownConfig, helper + ".config", true);
            var job = new InstallJob { Target = Path.GetFullPath(target), ParentId = parent.Id, ParentStarted = parent.StartTime.ToUniversalTime().Ticks,
                ExecutableHash = Updates.Hash(Path.Combine(stage, "Spacio.exe")), ConfigHash = Updates.Hash(Path.Combine(stage, "Spacio.exe.config")),
                Version = AssemblyName.GetAssemblyName(Path.Combine(stage, "Spacio.exe")).Version.ToString(), Persian = L.Persian, RestartArguments = restartArguments };
            string jobPath = Path.Combine(stage, "install.json");
            File.WriteAllText(jobPath, new JavaScriptSerializer().Serialize(job), Encoding.UTF8);
            var process = Process.Start(new ProcessStartInfo(helper, "--apply-update " + Quote(jobPath)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = stage });
            if (process == null) throw new IOException("Could not start update helper.");
            return process;
        }
        internal static void ReplaceFiles(string stage, InstallJob job, Action beforeExecutableReplace = null)
        {
            string incoming = Path.Combine(stage, "Spacio.exe"), config = incoming + ".config";
            if (Updates.Hash(incoming) != job.ExecutableHash || Updates.Hash(config) != job.ConfigHash || AssemblyName.GetAssemblyName(incoming).Version.ToString() != job.Version)
                throw new InvalidDataException("Update files changed after verification.");
            CheckWritable(job.Target);
            string directory = Path.GetDirectoryName(job.Target), id = Guid.NewGuid().ToString("N");
            string newExe = Path.Combine(directory, ".spacio-" + id + ".exe"), newConfig = newExe + ".config";
            string oldExe = Path.Combine(directory, ".spacio-" + id + ".previous.exe"), oldConfig = oldExe + ".config";
            string targetConfig = job.Target + ".config";
            bool hadConfig = File.Exists(targetConfig), configChanged = false;
            try
            {
                File.Copy(incoming, newExe); File.Copy(config, newConfig);
                if (hadConfig) File.Replace(newConfig, targetConfig, oldConfig); else File.Move(newConfig, targetConfig);
                configChanged = true;
                if (beforeExecutableReplace != null) beforeExecutableReplace();
                File.Replace(newExe, job.Target, oldExe);
                // Keep the previous executable/config together for manual recovery.
            }
            catch
            {
                if (configChanged)
                {
                    if (hadConfig) File.Copy(oldConfig, targetConfig, true); else File.Delete(targetConfig);
                }
                throw;
            }
            finally
            {
                if (File.Exists(newExe)) File.Delete(newExe);
                if (File.Exists(newConfig)) File.Delete(newConfig);
            }
        }
        public static int Run(string jobPath)
        {
            InstallJob job = null; string stage = AppDomain.CurrentDomain.BaseDirectory;
            try
            {
                if (!Paths.Equal(Path.GetDirectoryName(Path.GetFullPath(jobPath)), stage)) throw new InvalidDataException("Invalid update job location.");
                job = new JavaScriptSerializer().Deserialize<InstallJob>(File.ReadAllText(jobPath, Encoding.UTF8));
                L.Persian = job.Persian;
                try
                {
                    using (var parent = Process.GetProcessById(job.ParentId))
                        if (parent.StartTime.ToUniversalTime().Ticks == job.ParentStarted && !parent.WaitForExit(60000)) throw new IOException("برنامه هنوز باز است؛ پس از بستن آن دوباره تلاش کنید.");
                }
                catch (ArgumentException) { }
                ReplaceFiles(stage, job);
                File.WriteAllText(Path.Combine(stage, "installed.txt"), job.Version);
                Process.Start(new ProcessStartInfo(job.Target, job.RestartArguments ?? "") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(job.Target) });
                return 0;
            }
            catch (Exception e)
            {
                try { File.WriteAllText(Path.Combine(stage, "install-error.txt"), e.ToString()); } catch (IOException) { } catch (UnauthorizedAccessException) { }
                System.Windows.MessageBox.Show(L.T("آپدیت نصب نشد. نسخهٔ قبلی و اسکن‌های ذخیره‌شده حفظ شده‌اند.") + "\n\n" + L.T(e.Message), "Spacio", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                if (job != null && File.Exists(job.Target)) try { Process.Start(new ProcessStartInfo(job.Target) { UseShellExecute = false }); } catch { }
                return 1;
            }
        }
    }
}
