using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Spacio
{
    public static class SelfTests
    {
        private static readonly List<string> lines = new List<string>();
        private static void Check(bool condition, string message)
        {
            if (!condition) throw new Exception("FAIL: " + message);
            lines.Add("PASS: " + message);
        }
        private static void Expect(Action action, string message)
        {
            bool threw = false; try { action(); } catch (IOException) { threw = true; } catch (ArgumentException) { threw = true; }
            Check(threw, message);
        }
        private static string FixtureFile(string root, string name, int bytes, int daysOld)
        {
            string path = Path.Combine(root, name); Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) stream.SetLength(bytes);
            File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddDays(-daysOld));
            return path;
        }
        public static int Run(string report)
        {
            string workspace = Directory.GetParent(Path.GetDirectoryName(Path.GetFullPath(report))).FullName;
            string parent = Path.Combine(workspace, ".test-data", "run-" + Guid.NewGuid().ToString("N"));
            string fixture = Path.Combine(parent, "My storage");
            Directory.CreateDirectory(fixture);
            try
            {
                string video = FixtureFile(fixture, "Videos\\Mountain-film.mp4", 5 * 1024 * 1024, 40);
                FixtureFile(fixture, "Videos\\Screen-recording.mkv", 2 * 1024 * 1024, 220);
                FixtureFile(fixture, "Archives\\Project-backup.zip", 3 * 1024 * 1024, 430);
                FixtureFile(fixture, "Photos\\Summer.jpg", 1024 * 1024, 12);
                FixtureFile(fixture, "Documents\\یادداشت فارسی.txt", 2048, 700);
                FixtureFile(fixture, "Documents\\Budget.xlsx", 8192, 15);
                FixtureFile(fixture, "Music\\Piano.flac", 1536 * 1024, 50);
                FixtureFile(fixture, "Installers\\Setup.msi", 768 * 1024, 300);
                FixtureFile(fixture, "Loose-file.bin", 4096, 200);
                FixtureFile(fixture, "=formula.txt", 23, 1);
                Directory.CreateDirectory(Path.Combine(fixture, "Empty folder"));
                var before = Directory.GetFiles(fixture, "*", SearchOption.AllDirectories).ToDictionary(p => p, p => Tuple.Create(new FileInfo(p).Length, File.GetLastWriteTimeUtc(p)));
                var scanner = new Scanner(); var scan = scanner.Scan(fixture, CancellationToken.None, null);
                Check(scan.Root.Bytes == before.Sum(p => p.Value.Item1), "Recursive byte totals are exact");
                Check(scan.Root.TypeBytes.Sum() == scan.Root.Bytes && scan.Root.TypeBytes[0] == 7 * 1024 * 1024, "Cached type totals match the recursive total and video bytes");
                Check(scan.Root.FileCount == before.Count, "Recursive file counts are exact");
                Check(scan.Root.FolderCount == 7, "Recursive folder counts include empty folders");
                Check(scan.ErrorCount == 0 && !scan.Cancelled, "Complete scan has no errors");
                Check(before.All(p => File.Exists(p.Key) && new FileInfo(p.Key).Length == p.Value.Item1 && File.GetLastWriteTimeUtc(p.Key) == p.Value.Item2), "Scanning preserves fixture files and timestamps");
                Check(scan.Entries.Any(e => e.Name == "یادداشت فارسی.txt"), "Unicode and Persian filenames are scanned");
                Check(scan.Entries.Single(e => e.Path == video).Bytes == 5 * 1024 * 1024, "Individual file size is correct");
                Check(scan.Entries.Single(e => e.Name == "Videos").LatestUtc >= File.GetLastWriteTimeUtc(video), "Folder dates aggregate the latest content change");
                Check(Formats.Category(scan.Entries.Single(e => e.Path == video)) == "ویدیو", "Extension categories classify videos");
                Check(Safety.BlockReason(scan.Root, scan) != null, "Scan root cannot be recycled");
                var candidate = scan.Entries.Single(e => e.Name == "Archives");
                Check(Safety.BlockReason(candidate, scan) == null, "Ordinary fully scanned data folder is eligible for review");
                Check(Safety.CollapseSelection(new [] { candidate, candidate.Children[0] }).Count == 1, "Nested selections are collapsed without double deletion");
                Check(Paths.Within(Path.Combine(fixture, "Videos"), fixture) && !Paths.Within(fixture + "-sibling", fixture), "Containment enforces a path-component boundary");
                Expect(() => Paths.RequireLocal("\\\\server\\share"), "Network paths are rejected");
                Expect(() => Paths.RequireLocal("C:\\test.txt:stream"), "Alternate data stream paths are rejected");
                var cancelled = new CancellationTokenSource(); cancelled.Cancel();
                var partial = scanner.Scan(fixture, cancelled.Token, null);
                Check(partial.Cancelled && Safety.BlockReason(partial.Root, partial) != null, "Cancelled scan is incomplete and cannot authorize recycling");
                var midCancel = new CancellationTokenSource();
                var midPartial = scanner.Scan(fixture, midCancel.Token, p => midCancel.Cancel());
                Check(midPartial.Cancelled && midPartial.Root.Issues > 0, "Cancellation during enumeration returns an explicit partial result");
                Scanner.VerifyUnchanged(candidate, CancellationToken.None);
                lines.Add("PASS: Unchanged folder passes metadata verification");
                string changed = candidate.Children[0].Path;
                File.AppendAllText(changed, "a new byte sequence");
                Expect(() => Scanner.VerifyUnchanged(candidate, CancellationToken.None), "Changed descendant blocks folder recycling");
                var refreshed = scanner.Scan(fixture, CancellationToken.None, null);
                var docs = refreshed.Entries.Single(e => e.Name == "Documents");
                FixtureFile(fixture, "Documents\\new-file.txt", 7, 0);
                Expect(() => Scanner.VerifyUnchanged(docs, CancellationToken.None), "New descendant blocks stale folder recycling");
                string protectedFile = FixtureFile(fixture, "SystemFixture\\system-marker.bin", 32, 1);
                File.SetAttributes(protectedFile, FileAttributes.System);
                var systemScan = scanner.Scan(fixture, CancellationToken.None, null);
                Check(Safety.BlockReason(systemScan.Entries.Single(e => e.Name == "SystemFixture"), systemScan) != null, "Folder containing a system file is protected");
                File.SetAttributes(protectedFile, FileAttributes.Normal);
                string external = Path.Combine(parent, "Outside"); Directory.CreateDirectory(external);
                string canary = FixtureFile(external, "canary.txt", 1234, 0);
                string link = Path.Combine(fixture, "Linked folder");
                var start = new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + external + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = Process.Start(start)) { string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd(); process.WaitForExit(); Check(process.ExitCode == 0, "Junction fixture is created: " + output.Trim()); }
                var linked = scanner.Scan(fixture, CancellationToken.None, null);
                var linkedEntry = linked.Entries.Single(e => e.Name == "Linked folder");
                Check(linkedEntry.Issues > 0 && linkedEntry.Bytes == 0 && linkedEntry.Children.Count == 0, "Junctions are reported and never traversed");
                Check(!linked.Entries.Any(e => e.Name == "canary.txt") && new FileInfo(canary).Length == 1234, "Junction target remains untouched");
                Expect(() => scanner.Scan(link, CancellationToken.None, null), "Scanning a junction root is rejected");
                Expect(() => scanner.Scan(Path.Combine(link, "nested"), CancellationToken.None, null), "An invalid linked descendant fails safely");
                Check(Safety.BlockReason(linkedEntry, linked) != null, "Linked entries cannot be recycled");
                Check(Formats.Csv("=SUM(1,1)").StartsWith("\"'="), "CSV formula injection is neutralized");
                Check(Formats.Csv("a\"b") == "\"a\"\"b\"", "CSV quotes are escaped");
                string csv = Path.Combine(parent, "export.csv"); Formats.Export(csv, linked.Entries, CancellationToken.None);
                Check(File.ReadAllLines(csv).Length == linked.Entries.Count + 1 && File.ReadAllText(csv).Contains("یادداشت فارسی"), "CSV exports all entries and Unicode names");
                var sink = new RecycleSink();
                Check(sink.PreDeleteItem(0, null) < 0 && sink.PermanentDeleteBlocked, "Recycle sink vetoes permanent deletion");
                Check(new RecycleSink().PreDeleteItem(0x80, null) == 0, "Recycle sink allows Windows recycle operations");
                string disposable = FixtureFile(fixture, "Recycle-test-only.txt", 48, 0);
                var recycleScan = scanner.Scan(fixture, CancellationToken.None, null);
                var disposableEntry = recycleScan.Entries.Single(e => e.Path == disposable);
                var result = Recycler.RecycleAsync(new List<Entry> { disposableEntry }, recycleScan, IntPtr.Zero, CancellationToken.None).GetAwaiter().GetResult();
                Check(result.Count == 1 && result[0].StartsWith("✓") && !File.Exists(disposable), "Windows Shell recycles the generated test file: " + String.Join(" ", result));
                Check(File.Exists(canary), "Recycle test never touches external canary");
                string folderFixture = Path.Combine(fixture, "Recycle-folder-test");
                FixtureFile(folderFixture, "nested\\small.txt", 14, 0);
                Directory.SetLastWriteTimeUtc(Path.Combine(folderFixture, "nested"), DateTime.UtcNow.AddDays(-1));
                Directory.SetLastWriteTimeUtc(folderFixture, DateTime.UtcNow.AddDays(-1));
                var folderScan = scanner.Scan(fixture, CancellationToken.None, null);
                var folderEntry = folderScan.Entries.Single(e => e.Path == folderFixture);
                var folderResult = Recycler.RecycleAsync(new List<Entry> { folderEntry }, folderScan, IntPtr.Zero, CancellationToken.None).GetAwaiter().GetResult();
                Check(folderResult.Count == 1 && folderResult[0].StartsWith("✓") && !Directory.Exists(folderFixture), "Windows Shell recycles a verified test folder and its descendants: " + String.Join(" ", folderResult));
                string longRoot = Path.Combine(parent, "Long paths");
                string longPath = FixtureFile(longRoot, new string('a', 95) + "\\" + new string('b', 95) + "\\" + new string('c', 95) + "\\long.txt", 37, 0);
                var longScan = scanner.Scan(longRoot, CancellationToken.None, null);
                Check(longPath.Length > 260 && longScan.Root.Bytes == 37 && longScan.ErrorCount == 0, "Paths longer than 260 characters are scanned");
                string scaleRoot = Path.Combine(parent, "Scale fixture"); Directory.CreateDirectory(scaleRoot);
                var scaleWatch = Stopwatch.StartNew();
                for (int i = 0; i < 10000; i++) FixtureFile(scaleRoot, "file-" + i.ToString("D5") + ".bin", 0, i % 700);
                scaleWatch.Restart(); var scaleScan = scanner.Scan(scaleRoot, CancellationToken.None, null);
                Check(scaleScan.Root.FileCount == 10000 && scaleScan.ErrorCount == 0, "10,000-file scan completes accurately in " + scaleWatch.Elapsed.TotalSeconds.ToString("0.00") + " seconds");
                L.Persian = false; Check(L.T("شروع اسکن") == "Scan storage", "English interface translations work");
                L.Persian = true; Check(L.T("شروع اسکن") == "شروع اسکن", "Persian interface translations work");
                lines.Add("FIXTURE=" + fixture);
                lines.Add("All " + lines.Count(s => s.StartsWith("PASS:")) + " checks passed.");
                File.WriteAllLines(report, lines, new System.Text.UTF8Encoding(true));
                return 0;
            }
            catch (Exception e)
            {
                lines.Add(e.ToString()); lines.Add("FIXTURE=" + fixture);
                File.WriteAllLines(report, lines, new System.Text.UTF8Encoding(true)); return 1;
            }
        }
    }
}
