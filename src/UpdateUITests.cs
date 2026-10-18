using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Spacio
{
    public sealed partial class Controller
    {
        // Invoked only by --live-test with its generated scan, file and cache.
        private async Task CheckUpdateDuringLive(string inside, Action<bool, string> check)
        {
            string scope = current.Path, cacheFile = cache.FileFor(snapshot.Root.Path);
            availableUpdate = new ReleaseInfo { Version = new Version(Updates.Current.Major, Updates.Current.Minor, Updates.Current.Build + 1) };
            U<FrameworkElement>("UpdateBanner").Visibility = Visibility.Visible; UpdateLabels();
            bool downloadReached = false;
            Func<ReleaseInfo, IProgress<int>, CancellationToken, Task<string>> pendingDownload = async (release, progress, token) => {
                downloadReached = true;
                await Task.Delay(5000, token);
                throw new IOException("Fixture download timeout");
            };

            File.AppendAllText(inside, " update check");
            var saved = snapshot; byte[] savedBytes = File.ReadAllBytes(cacheFile);
            DateTime savedTime = snapshot.StartedUtc;
            using (var held = File.Open(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                var manual = RefreshSnapshotAsync(true, scope); await Task.Delay(120);
                bool disabled = refreshing && !refreshingAutomatically && !U<Button>("InstallUpdateButton").IsEnabled;
                await InstallUpdateAsync(pendingDownload);
                bool notStarted = !installingUpdate && !downloadReached;
                refreshCancel.Cancel(); await manual;
                check(disabled && notStarted, "Update installation still waits for a deliberate manual refresh");
            }
            reviewingCleanup = true; UpdateLabels();
            bool reviewBlocked = !CanInstallUpdate && !U<Button>("InstallUpdateButton").IsEnabled;
            reviewingCleanup = false;
            cleanupCancel = new CancellationTokenSource(); UpdateLabels();
            bool cleanupBlocked = !CanInstallUpdate && !U<Button>("InstallUpdateButton").IsEnabled;
            cleanupCancel.Dispose(); cleanupCancel = null; UpdateLabels();
            check(reviewBlocked && cleanupBlocked, "Cleanup review and active removal cannot be interrupted by an update");

            liveTimer.Start();
            using (var held = File.Open(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                liveMonitor.RequireFull();
                var automatic = RefreshSnapshotAsync(false); await Task.Delay(120);
                bool offered = refreshingAutomatically && U<Button>("InstallUpdateButton").IsEnabled;
                var update = InstallUpdateAsync(pendingDownload);
                bool claimed = installingUpdate && busy && !U<Button>("RescanButton").IsEnabled && !U<Button>("LiveToggle").IsEnabled;
                await ScanAsync(scope, true);
                await OpenCleanupPage(false);
                await InstallUpdateAsync(pendingDownload);
                await AwaitLive(() => downloadReached || update.IsCompleted);
                bool settled = downloadReached && automatic.IsCompleted && !refreshing && refreshCancel == null;
                var lastTask = activeRefreshTask;
                await LiveTickAsync(); AttachLive(true);
                bool exclusive = busy && installingUpdate && !liveTimer.IsEnabled && liveMonitor == null && activeRefreshTask == lastTask && !cleanupPageOpen;
                bool preserved = snapshot == saved && savedTime == snapshot.StartedUtc && savedBytes.SequenceEqual(File.ReadAllBytes(cacheFile));
                L.Persian = false; UpdateLabels();
                bool english = U<TextBlock>("UpdateTitle").Text.Contains("Downloading");
                L.Persian = true; UpdateLabels();
                bool persian = U<TextBlock>("UpdateTitle").Text.Contains("دانلود");
                U<Button>("DismissUpdateButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await update; await automatic;
                check(offered && claimed && settled, "Install remains available while checking and waits for cancelled automatic work before downloading");
                check(exclusive, "Update owns the busy state after refresh completion and prevents timers, manual refresh, cleanup and duplicate installation");
                check(preserved, "Interrupting automatic checking for an update retains displayed results, original timestamp and exact saved cache bytes");
                check(english && persian, "Update progress remains localized in English and Persian");
                check(!busy && !installingUpdate && downloadCancel == null && liveTimer.IsEnabled && liveMonitor != null && liveMonitor.Root == scope && liveMonitor.HasPending,
                    "Cancelling through the update banner restores monitoring and queues reconciliation only for the viewed folder");
            }
            liveTimer.Stop(); nextLiveAttempt = DateTime.MinValue; await LiveTickAsync();
            check(snapshot.Entries.Single(e => e.Path == inside).Bytes == new FileInfo(inside).Length, "Resumed monitoring reconciles changes missed while preparing the update");

            // A quick second cancellation must also work before the download begins.
            File.AppendAllText(inside, " cancel preparation");
            saved = snapshot; savedBytes = File.ReadAllBytes(cacheFile); downloadReached = false;
            using (var held = File.Open(cacheFile, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                liveMonitor.RequireFull(); var automatic = RefreshSnapshotAsync(false); await Task.Delay(120);
                var update = InstallUpdateAsync(pendingDownload);
                U<Button>("DismissUpdateButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await update; await automatic;
                check(!downloadReached && !busy && snapshot == saved && savedBytes.SequenceEqual(File.ReadAllBytes(cacheFile)),
                    "Cancelling update preparation starts no download and preserves the saved view");
            }
            nextLiveAttempt = DateTime.MinValue; await LiveTickAsync();

            await InstallUpdateAsync((release, progress, token) => { throw new IOException("Simulated offline download"); });
            check(!busy && !installingUpdate && downloadCancel == null && CanInstallUpdate && liveMonitor != null && liveMonitor.Root == scope && updateState == "آپدیت نصب نشد",
                "A failed download leaves installation retryable and restores scoped live monitoring");
            await ToggleLiveAsync();
            saved = snapshot; savedBytes = File.ReadAllBytes(cacheFile);
            await InstallUpdateAsync((release, progress, token) => { throw new IOException("Simulated offline download"); });
            check(livePaused && liveMonitor == null && !busy && snapshot == saved && savedBytes.SequenceEqual(File.ReadAllBytes(cacheFile)),
                "A failed update respects an existing Pause live choice and leaves saved results intact");
            await ToggleLiveAsync(); nextLiveAttempt = DateTime.MinValue; await LiveTickAsync();
            availableUpdate = null; U<FrameworkElement>("UpdateBanner").Visibility = Visibility.Collapsed; UpdateLabels();
        }
    }
}
