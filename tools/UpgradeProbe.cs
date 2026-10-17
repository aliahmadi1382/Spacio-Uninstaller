// Compiled against a previous release by verify-upgrade.ps1, outside the app build.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Spacio;

[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8")]

internal static class UpgradeProbe
{
    private static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    private static bool WaitForFile(string path)
    {
        var watch = Stopwatch.StartNew();
        while (!File.Exists(path) && watch.ElapsedMilliseconds < 10000) Thread.Sleep(50);
        return File.Exists(path);
    }
    public static int Main(string[] args)
    {
        string target = args[0], report = args[1];
        string marker = Path.Combine(Path.GetDirectoryName(report), "upgrade-result.txt"), signal = marker + ".host";
        try
        {
            string data = Path.Combine(Path.GetDirectoryName(report), "saved-data"), cachePath = Path.Combine(Path.GetDirectoryName(report), "saved-scans");
            Directory.CreateDirectory(data); string originalFile = Path.Combine(data, "keep-this-file.txt");
            File.WriteAllText(originalFile, "User files must survive an application update.");
            var cache = new SnapshotCache(cachePath); var snapshot = new Scanner().Scan(data, CancellationToken.None, null);
            cache.Save(snapshot, CancellationToken.None); cache.SaveLastPath(data);
            string scanHash = Updates.Hash(cache.FileFor(data)), originalHash = Updates.Hash(originalFile);
            // This file would enter the results if startup silently rescanned after upgrading.
            File.WriteAllText(Path.Combine(data, "spacio-after-close-marker.tmp"), "created after saved scan");
            var previous = Updates.Current;
            var release = Updates.CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
            Require(release != null && release.Version > previous, "No newer public release available for this previous executable.");
            string stage = Updates.DownloadAsync(release, null, CancellationToken.None).GetAwaiter().GetResult();
            using (var parent = Process.Start(new ProcessStartInfo(target, "--update-test-host " + UpdateInstaller.Quote(signal)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
            {
                try
                {
                    Require(WaitForFile(signal + ".ready"), "Old application did not start.");
                    using (var helper = UpdateInstaller.Start(stage, target, parent, "--startup-smoke " + UpdateInstaller.Quote(cachePath) + " " + UpdateInstaller.Quote(marker)))
                    {
                        File.WriteAllText(signal + ".exit", "exit");
                        Require(helper.WaitForExit(20000) && helper.ExitCode == 0, "Installation did not finish successfully.");
                        Require(WaitForFile(marker + ".startup.ok.txt") && System.Reflection.AssemblyName.GetAssemblyName(target).Version == release.Version, "Upgraded application did not restore the old saved scan after restarting.");
                        Require(Updates.Hash(cache.FileFor(data)) == scanHash && Updates.Hash(originalFile) == originalHash, "Update changed the saved scan or the scanned file.");
                        Require(new SnapshotCache(cachePath).Find(data, CancellationToken.None).Snapshot.StartedUtc == snapshot.StartedUtc, "Original scan timestamp was not preserved.");
                    }
                }
                finally { File.WriteAllText(signal + ".exit", "exit"); parent.WaitForExit(5000); }
            }
            File.WriteAllText(report, "PASS: previous release " + previous.ToString(3) + " discovers, downloads, verifies, installs and restarts public release " + release.Version.ToString(3) + "\nPASS: original scan file, scan timestamp and scanned user file are unchanged; the new app restores the old scan without rescanning.\nOnly an isolated application copy was replaced.");
            return 0;
        }
        catch (Exception e) { File.WriteAllText(report, e.ToString()); return 1; }
    }
}
