// Compiled against a previous release by verify-upgrade.ps1, outside the app build.
using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Spacio;

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
            var previous = Updates.Current;
            var release = Updates.CheckAsync(CancellationToken.None).GetAwaiter().GetResult();
            Require(release != null && release.Version > previous, "No newer public release available for this previous executable.");
            string stage = Updates.DownloadAsync(release, null, CancellationToken.None).GetAwaiter().GetResult();
            using (var parent = Process.Start(new ProcessStartInfo(target, "--update-test-host " + UpdateInstaller.Quote(signal)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden }))
            {
                try
                {
                    Require(WaitForFile(signal + ".ready"), "Old application did not start.");
                    using (var helper = UpdateInstaller.Start(stage, target, parent, "--update-test-probe " + UpdateInstaller.Quote(marker)))
                    {
                        File.WriteAllText(signal + ".exit", "exit");
                        Require(helper.WaitForExit(20000) && helper.ExitCode == 0, "Installation did not finish successfully.");
                        Require(WaitForFile(marker) && File.ReadAllText(marker) == release.Version.ToString(3), "Upgraded application did not restart with the new version.");
                    }
                }
                finally { File.WriteAllText(signal + ".exit", "exit"); parent.WaitForExit(5000); }
            }
            File.WriteAllText(report, "PASS: previous release " + previous.ToString(3) + " discovers, downloads, verifies, installs and restarts public release " + release.Version.ToString(3) + "\nOnly an isolated application copy was replaced.");
            return 0;
        }
        catch (Exception e) { File.WriteAllText(report, e.ToString()); return 1; }
    }
}
