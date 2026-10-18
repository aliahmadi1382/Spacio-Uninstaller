using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;

namespace Spacio
{
    internal static class ReviewedRemovalTests
    {
        internal static void Run(string area, Action<bool, string> check)
        {
            var token = CancellationToken.None;
            Action<Action, string> reject = (action, label) => { bool failed = false; try { action(); } catch (IOException) { failed = true; } check(failed, label); };
            string root = Path.Combine(area, "Removal plans"); Directory.CreateDirectory(root);
            string data = Path.Combine(root, "AppData", "Local", "ExampleApp");
            string secret = CleanupTests.Put(data, ".env", "generated secret fixture");
            string document = CleanupTests.Put(data, "nested\\my-document.txt", "generated personal fixture");
            var plan = ReviewedRemoval.Prepare(data, token);
            check(plan.AppData && plan.Nodes.Any(n => n.Entry.Path == secret) && plan.Root.Bytes > 0, "Explicit app-data preview includes settings and personal files, not merely caches");
            File.AppendAllText(document, " changed");
            reject(() => plan.DeletePermanent(token), "Editing a file after review blocks deletion before any fixture is removed");
            check(File.Exists(secret) && File.Exists(document), "Changed-preview rejection preserves every file");
            plan = ReviewedRemoval.Prepare(data, token);
            string added = CleanupTests.Put(data, "not-reviewed.txt");
            reject(() => plan.DeletePermanent(token), "Unreviewed additions invalidate the entire removal plan");
            check(File.Exists(added) && File.Exists(secret), "New files and existing data both survive an invalidated review");
            plan = ReviewedRemoval.Prepare(data, token);
            using (var held = new FileStream(secret, FileMode.Open, FileAccess.Read, FileShare.Read))
                reject(() => plan.DeletePermanent(token), "An in-use file reports failure without forcing its owner closed");
            plan = ReviewedRemoval.Prepare(data, token); plan.DeletePermanent(token);
            check(!Directory.Exists(data), "Permanent removal deletes a reviewed app-data folder and its nested data");
            string keep = CleanupTests.Put(root, "Outside\\keep.txt", "outside target must survive");
            string linkedRoot = Path.Combine(root, "With links"); Directory.CreateDirectory(linkedRoot);
            string link = Path.Combine(linkedRoot, "external-junction");
            using (var p = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + link + "\" \"" + Path.GetDirectoryName(keep) + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(); check(p.ExitCode == 0, "Create a junction only between generated fixture directories"); }
            CleanupTests.Put(linkedRoot, "regular.txt"); plan = ReviewedRemoval.Prepare(linkedRoot, token);
            check(plan.LinkCount == 1 && !plan.Nodes.Any(n => n.Entry.Path.EndsWith("keep.txt")) && plan.Nodes.Single(n => n.Link).Target.Contains("Outside"), "Link preview shows its target but never enumerates that target");
            reject(() => plan.VerifyForRecycle(token), "Recycling a linked tree cannot silently fall back to permanent deletion");
            var cancelled = new CancellationTokenSource(); cancelled.Cancel();
            reject(() => plan.DeletePermanent(cancelled.Token), "Cancelled removal reports the stop and keeps the reviewed tree");
            plan.DeletePermanent(token);
            check(!Directory.Exists(linkedRoot) && File.ReadAllText(keep) == "outside target must survive", "Permanent deletion removes only the junction object and preserves all target contents");
            string swap = CleanupTests.Put(root, "Swap\\same-name.txt", "old"); plan = ReviewedRemoval.Prepare(swap, token);
            string old = swap + ".old"; File.Move(swap, old); File.WriteAllText(swap, "new");
            reject(() => plan.DeletePermanent(token), "Replacing a reviewed path with a different file identity blocks removal");
            check(File.ReadAllText(swap) == "new", "Replacement-file rejection leaves new contents intact");
            string beforeSwap = Path.Combine(root, "Directory swap"); CleanupTests.Put(beforeSwap, "reviewed.txt"); plan = ReviewedRemoval.Prepare(beforeSwap, token);
            Directory.Move(beforeSwap, beforeSwap + "-original");
            using (var p = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /J \"" + beforeSwap + "\" \"" + Path.GetDirectoryName(keep) + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(); check(p.ExitCode == 0, "Create a generated directory-swap fixture"); }
            reject(() => plan.DeletePermanent(token), "A directory swapped for a junction after review cannot redirect removal");
            check(File.ReadAllText(keep) == "outside target must survive", "Directory-swap rejection preserves the external target");
            var linkOnly = ReviewedRemoval.Prepare(beforeSwap, token); linkOnly.DeletePermanent(token);
            check(!Directory.Exists(beforeSwap) && File.Exists(keep), "A directly selected junction is unlinked without deleting its destination");
            string readOnly = CleanupTests.Put(root, "readonly.txt"); File.SetAttributes(readOnly, FileAttributes.ReadOnly);
            plan = ReviewedRemoval.Prepare(readOnly, token); plan.DeletePermanent(token);
            check(!File.Exists(readOnly), "An explicitly confirmed read-only fixture can be removed without changing ACLs");
            string hardTarget = CleanupTests.Put(root, "Outside\\read-only-hardlink.txt", "hard link canary");
            string hardLink = Path.Combine(root, "selected-hardlink.txt");
            using (var p = Process.Start(new ProcessStartInfo("cmd.exe", "/d /c mklink /H \"" + hardLink + "\" \"" + hardTarget + "\"") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
            { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(); check(p.ExitCode == 0, "Create a hard link between generated fixture paths"); }
            File.SetAttributes(hardTarget, FileAttributes.ReadOnly); plan = ReviewedRemoval.Prepare(hardLink, token); plan.DeletePermanent(token);
            check(!File.Exists(hardLink) && File.ReadAllText(hardTarget) == "hard link canary" && (File.GetAttributes(hardTarget) & FileAttributes.ReadOnly) != 0, "Removing a read-only hard link preserves outside content and attributes");
            File.SetAttributes(hardTarget, FileAttributes.Normal);
            string system = CleanupTests.Put(root, "system.txt"); File.SetAttributes(system, FileAttributes.System);
            reject(() => ReviewedRemoval.Prepare(system, token), "Actual system-marked files remain protected"); File.SetAttributes(system, FileAttributes.Normal);
            string offline = CleanupTests.Put(root, "offline.txt"); File.SetAttributes(offline, FileAttributes.Offline);
            reject(() => ReviewedRemoval.Prepare(offline, token), "Offline/cloud data is not silently deleted"); File.SetAttributes(offline, FileAttributes.Normal);
            string scan = CleanupTests.Put(root, "AppData\\Local\\Spacio\\Scans\\keep.scan", "saved scan");
            reject(() => ReviewedRemoval.Prepare(scan, token), "Explicit data removal still protects Spacio scan storage");
            check(ReviewedRemoval.ProtectedReason(Path.GetPathRoot(root)) != null && ReviewedRemoval.ProtectedReason(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) != null, "Drive and entire AppData containers remain protected");
            string hermes = Path.Combine(root, "Hermes fixture"); CleanupTests.Put(hermes, "bin\\hermes.exe");
            check(InstalledApps.DetectHermes(hermes) == null, "A file or folder named Hermes alone does not identify an uninstaller");
            CleanupTests.Put(hermes, "hermes-agent\\pyproject.toml", "name = \"hermes-agent\"\nhermes = \"hermes_cli.main:main\"\n");
            CleanupTests.Put(hermes, "hermes-agent\\hermes_cli\\uninstall.py");
            CleanupTests.Put(hermes, "hermes-agent\\hermes_cli\\subcommands\\uninstall.py", "--dry-run --full");
            var app = InstalledApps.DetectHermes(hermes); var command = InstalledApps.StartInfo(app);
            check(app != null && InstalledApps.Owner(hermes, new [] { app }) == app && command.Arguments == "uninstall" && !command.Arguments.Contains("--yes"), "Supported Hermes layout routes to its own interactive uninstaller without executing it");
            check(InstalledApps.Owner(Path.Combine(hermes, "sessions"), new [] { app }) == null && command.EnvironmentVariables["HERMES_HOME"] == hermes && !Paths.Within(command.WorkingDirectory, hermes), "Hermes data remains separately selectable and the uninstaller does not lock its own folder as current directory");
            check(File.ReadAllText(keep) == "outside target must survive" && File.ReadAllText(scan) == "saved scan", "All removal-plan tests preserve outside targets and saved scans");
        }
    }
}
