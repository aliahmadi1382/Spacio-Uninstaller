using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Spacio
{
    public sealed partial class Controller
    {
        private bool liveEnabled, refreshing, driveRefreshRunning, refreshingAutomatically, manualRefreshPending;
        private bool livePaused, changingLivePause;
        private int automaticFailures;
        private Task activeRefreshTask;
        private LiveMonitor liveMonitor;
        private DispatcherTimer liveTimer;
        private DateTime nextLiveAttempt, lastDriveRefresh, lastFullCheck;
        private string liveState;
        private CancellationTokenSource refreshCancel;
        private async Task ToggleLiveAsync()
        {
            if (changingLivePause || installingUpdate) return;
            changingLivePause = true; livePaused = !livePaused; UpdateLiveToggle();
            try
            {
                if (livePaused)
                {
                    if (liveMonitor != null) { liveMonitor.Dispose(); liveMonitor = null; }
                    if (refreshingAutomatically && activeRefreshTask != null) { refreshCancel.Cancel(); await activeRefreshTask; }
                    SetLiveState("پایش زنده متوقف است؛ Refresh دستی فعال است");
                    SetStatus(L.T("پایش زنده متوقف است؛ Refresh دستی فعال است"));
                }
                else { automaticFailures = 0; AttachLive(true); }
            }
            finally { changingLivePause = false; UpdateLiveToggle(); }
        }
        private void UpdateLiveToggle()
        {
            U<Button>("LiveToggle").Content = L.T(livePaused ? "ادامهٔ پایش" : "توقف پایش");
            U<Button>("LiveToggle").IsEnabled = !installingUpdate && !changingLivePause && liveEnabled && snapshot != null && !snapshot.Cancelled;
            U<Button>("LiveToggle").ToolTip = L.T("پایش زنده تغییرات پوشهٔ بازشده را دنبال می‌کند؛ Refresh دستی مستقل است");
        }
        private LiveMonitor CreateLiveMonitor(string path)
        {
            Paths.RequireLocal(Paths.Normalize(path)); Paths.CheckAncestors(path);
            return new LiveMonitor(path, cache.DirectoryPath,
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spacio", "Updates"));
        }

        public void StartLiveUpdates()
        {
            liveEnabled = true;
            if (liveTimer == null)
            {
                liveTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                liveTimer.Tick += async delegate { await LiveTickAsync(); };
            }
            AttachLive(true); liveTimer.Start();
        }
        private void AttachLive(bool reconcile)
        {
            if (installingUpdate || cleanupPageOpen || livePaused || !liveEnabled || snapshot == null || current == null || snapshot.Cancelled || closing) return;
            if (liveMonitor != null && Paths.Equal(liveMonitor.Root, current.Path) && !liveMonitor.Failed)
            {
                if (reconcile) liveMonitor.RequireFull();
                SetLiveState(liveMonitor.HasPending ? "تغییرات در صف بررسی" : "زنده · منتظر تغییرات");
                return;
            }
            // A saved drive tree is a cache, not the scope of every background operation.
            // Leaving a folder also cancels the old automatic traversal before following the new one.
            if (refreshingAutomatically && refreshCancel != null) refreshCancel.Cancel();
            if (liveMonitor != null) { liveMonitor.Dispose(); liveMonitor = null; }
            nextLiveAttempt = DateTime.MinValue; lastFullCheck = DateTime.MinValue; automaticFailures = 0;
            try
            {
                liveMonitor = CreateLiveMonitor(current.Path);
                liveMonitor.RequireFull();
                SetLiveState("بررسی تغییرات در پس‌زمینه…");
                SetStatus(L.T("بررسی خودکار پوشه") + "  " + current.Path);
            }
            catch (Exception e)
            {
                if (!(e is IOException) && !(e is UnauthorizedAccessException) && !(e is ArgumentException)) throw;
                SetLiveState("مسیر در دسترس نیست؛ نتیجهٔ ذخیره‌شده حفظ شده است");
                nextLiveAttempt = DateTime.UtcNow.AddSeconds(15);
            }
        }
        private void StopLiveUpdates()
        {
            if (liveTimer != null) liveTimer.Stop();
            if (liveMonitor != null) { liveMonitor.Dispose(); liveMonitor = null; }
            if (refreshCancel != null) refreshCancel.Cancel();
        }
        private void SetLiveState(string state)
        {
            liveState = livePaused ? "پایش زنده متوقف است؛ Refresh دستی فعال است" : state;
            UpdateLiveToggle();
            if (liveEnabled)
            {
                U<TextBlock>("LiveStatus").Text = "● " + L.T(liveState);
                U<TextBlock>("LiveStatus").ToolTip = L.T("مسیر پایش زنده") + ": " + (current == null ? "" : current.Path);
            }
        }
        private async Task LiveTickAsync()
        {
            if (closing || installingUpdate) return;
            if ((DateTime.UtcNow - lastDriveRefresh).TotalSeconds >= 10) RefreshDrives();
            if (cleanupPageOpen || livePaused || busy || refreshing || manualRefreshPending || reviewingCleanup || snapshot == null || snapshot.Cancelled) return;
            if (DateTime.UtcNow < nextLiveAttempt) return;
            AttachLive(false);
            if (liveMonitor == null) return;
            // Periodic validation also covers notifications Windows could not deliver (sleep,
            // removable media, and changes to the watched root itself).
            if ((DateTime.UtcNow - lastFullCheck).TotalMinutes >= 5) liveMonitor.RequireFull();
            if (liveMonitor.Ready) await RefreshSnapshotAsync(false);
        }
        private Task RefreshSnapshotAsync(bool full, string requestedPath = null)
        {
            if (installingUpdate || busy || refreshing || snapshot == null || closing) return Task.FromResult(0);
            activeRefreshTask = RunRefreshSnapshotAsync(full, requestedPath);
            return activeRefreshTask;
        }
        private async Task RunRefreshSnapshotAsync(bool full, string requestedPath)
        {
            var basis = snapshot;
            string viewAtStart = current == null ? basis.Root.Path : current.Path;
            var monitor = liveMonitor;
            bool automatic = !full && requestedPath == null;
            var duration = Stopwatch.StartNew();
            string scope = requestedPath ?? (automatic ? (monitor == null ? viewAtStart : monitor.Root) : basis.Root.Path);
            bool scoped = !Paths.Equal(scope, basis.Root.Path);
            string[] changes = full || monitor == null ? null : monitor.Take();
            if (full && monitor != null && Paths.Equal(scope, monitor.Root)) monitor.Take();
            Func<bool> superseded = () => automatic && (monitor != liveMonitor || current == null || !Paths.Equal(scope, current.Path));
            refreshing = true; refreshingAutomatically = automatic; refreshCancel = new CancellationTokenSource();
            var operation = refreshCancel;
            var token = refreshCancel.Token;
            SetBusy(true, true); SetLiveState("بررسی تغییرات در پس‌زمینه…");
            SetStatus(L.T(automatic ? "بررسی خودکار پوشه" : "تازه‌سازی پوشه") + "  " + scope);
            try
            {
                var progress = new Progress<ScanProgress>(p => {
                    if (!closing && refreshing && operation == refreshCancel && !token.IsCancellationRequested && !superseded())
                        SetStatus(L.T(automatic ? "بررسی خودکار پوشه" : "تازه‌سازی پوشه") + "  " + scope + "  ·  " + p.Path);
                });
                var result = await Task.Run(() => {
                    // An unavailable watched folder must never turn recovery into a scan of its parent.
                    if (automatic) Paths.CheckAncestors(scope);
                    var updated = changes == null ? LiveScanner.RefreshFolder(basis, scope, token, p => ((IProgress<ScanProgress>)progress).Report(p)) :
                        LiveScanner.Refresh(basis, changes.Where(p => Paths.Within(p, scope)).ToArray(), token, p => ((IProgress<ScanProgress>)progress).Report(p));
                    if (scoped && !Object.ReferenceEquals(updated, basis)) updated.IsStale |= basis.IsStale;
                    token.ThrowIfCancellationRequested();
                    var directories = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
                    foreach (var entry in updated.Entries)
                    {
                        token.ThrowIfCancellationRequested(); if (entry.IsDirectory) directories[entry.Path] = entry;
                    }
                    // Preserve the old on-disk snapshot through cancellation or interrupted writes.
                    if (!Object.ReferenceEquals(updated, basis)) cache.Save(updated, token);
                    return Tuple.Create(updated, directories);
                }, token);
                if (closing) return;
                if (changes == null && !superseded() && monitor == liveMonitor && (monitor == null || Paths.Equal(scope, monitor.Root))) lastFullCheck = DateTime.UtcNow;
                // Once atomic persistence completes, publish that committed result even if Stop
                // was clicked at the same instant. Cancellation before commit keeps the old one.
                string path = current == null ? result.Item1.Root.Path : current.Path;
                if (requestedPath != null && Paths.Equal(path, viewAtStart)) path = requestedPath;
                Entry directory;
                while (!result.Item2.TryGetValue(path, out directory))
                {
                    path = Path.GetDirectoryName(path);
                    if (path == null) { directory = result.Item1.Root; break; }
                }
                snapshot = result.Item1; current = directory; stale = snapshot.IsStale;
                AttachLive(false);
                if (requestedPath != null) U<TextBox>("PathBox").Text = current.Path;
                if (!Object.ReferenceEquals(snapshot, basis)) { viewingCached = false; cacheSaved = true; }
                UpdateMetrics(); BuildMap(); await RefreshRowsAsync();
                await SaveLastPathAsync(current.Path);
                if (!superseded())
                {
                    automaticFailures = 0;
                    SetLiveState(monitor != null && monitor.HasPending ? "تغییرات در صف بررسی" : "زنده · منتظر تغییرات");
                    SetStatus(L.T("تغییرات بررسی و ذخیره شد") + "  " + scope);
                    // Give the UI and disk a rest after expensive transactions. Event
                    // batches remain queued; ordinary bursts still settle within seconds.
                    nextLiveAttempt = DateTime.UtcNow.AddSeconds(automatic ? Math.Max(3, Math.Min(30, duration.Elapsed.TotalSeconds)) : 3);
                }
                RefreshDrives();
            }
            catch (OperationCanceledException)
            {
                if (!superseded())
                {
                    if (!closing) { SetLiveState("بررسی متوقف شد؛ نتیجهٔ قبلی حفظ شده است"); SetStatus(L.T("بررسی متوقف شد؛ نتیجهٔ قبلی حفظ شده است")); }
                    if (monitor != null && monitor == liveMonitor && Paths.Equal(scope, monitor.Root)) monitor.RequireFull();
                    nextLiveAttempt = DateTime.UtcNow.AddSeconds(30);
                }
            }
            catch (Exception e)
            {
                if (!closing && !superseded())
                {
                    SetLiveState("بررسی ممکن نشد؛ تلاش دوباره به‌صورت خودکار");
                    SetStatus(L.T("بررسی ممکن نشد؛ تلاش دوباره به‌صورت خودکار") + "  ·  " + L.T(e.Message));
                }
                if (!superseded())
                {
                    if (monitor != null && monitor == liveMonitor && Paths.Equal(scope, monitor.Root)) monitor.RequireFull();
                    automaticFailures = Math.Min(automaticFailures + 1, 5);
                    nextLiveAttempt = DateTime.UtcNow.AddSeconds(Math.Min(300, 15 * Math.Pow(2, automaticFailures - 1)));
                }
            }
            finally
            {
                refreshing = false; refreshingAutomatically = false; refreshCancel.Dispose(); refreshCancel = null;
                if (!closing) SetBusy(false, false);
            }
        }
    }
}
