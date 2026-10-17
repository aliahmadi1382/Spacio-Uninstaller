using System;
using System.Collections.Generic;
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
        private bool liveEnabled, refreshing, driveRefreshRunning;
        private LiveMonitor liveMonitor;
        private DispatcherTimer liveTimer;
        private DateTime nextLiveAttempt, lastDriveRefresh, lastFullCheck;
        private string liveState;
        private CancellationTokenSource refreshCancel;
        private LiveMonitor CreateLiveMonitor(string path)
        {
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
            if (!liveEnabled || snapshot == null || snapshot.Cancelled || closing) return;
            if (liveMonitor != null && Paths.Equal(liveMonitor.Root, snapshot.Root.Path) && !liveMonitor.Failed)
            {
                if (reconcile) liveMonitor.RequireFull();
                SetLiveState(liveMonitor.HasPending ? "بررسی تغییرات در پس‌زمینه…" : "زنده · تغییرات خودکار دنبال می‌شوند");
                return;
            }
            if (liveMonitor != null) { liveMonitor.Dispose(); liveMonitor = null; }
            try
            {
                liveMonitor = CreateLiveMonitor(snapshot.Root.Path);
                liveMonitor.RequireFull();
                SetLiveState("بررسی تغییرات در پس‌زمینه…");
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
            liveState = state;
            if (liveEnabled) U<TextBlock>("LiveStatus").Text = "● " + L.T(state);
        }
        private async Task LiveTickAsync()
        {
            if (closing) return;
            if ((DateTime.UtcNow - lastDriveRefresh).TotalSeconds >= 10) RefreshDrives();
            if (busy || refreshing || reviewingCleanup || snapshot == null || snapshot.Cancelled) return;
            if (DateTime.UtcNow < nextLiveAttempt) return;
            AttachLive(false);
            if (liveMonitor == null) return;
            // Periodic validation also covers notifications Windows could not deliver (sleep,
            // removable media, and changes to the watched root itself).
            if ((DateTime.UtcNow - lastFullCheck).TotalMinutes >= 5) liveMonitor.RequireFull();
            if (liveMonitor.HasPending) await RefreshSnapshotAsync(false);
        }
        private async Task RefreshSnapshotAsync(bool full, string requestedPath = null)
        {
            if (busy || refreshing || snapshot == null || closing) return;
            var basis = snapshot;
            string viewAtStart = current == null ? basis.Root.Path : current.Path;
            bool scoped = requestedPath != null && !Paths.Equal(requestedPath, basis.Root.Path);
            string[] changes = full || liveMonitor == null ? null : liveMonitor.Take();
            if (full && !scoped && liveMonitor != null) liveMonitor.Take();
            refreshing = true; refreshCancel = new CancellationTokenSource();
            var token = refreshCancel.Token;
            SetBusy(true, true); SetLiveState("بررسی تغییرات در پس‌زمینه…");
            SetStatus(L.T("بررسی تغییرات در پس‌زمینه…") + "  " + (requestedPath ?? basis.Root.Path));
            if (changes == null && !scoped) lastFullCheck = DateTime.UtcNow;
            try
            {
                var progress = new Progress<ScanProgress>(p => {
                    if (!closing && refreshing) SetStatus(L.T("بررسی تغییرات در پس‌زمینه…") + "  " + p.Path);
                });
                var result = await Task.Run(() => {
                    var updated = scoped ? LiveScanner.RefreshFolder(basis, requestedPath, token, p => ((IProgress<ScanProgress>)progress).Report(p)) :
                        LiveScanner.Refresh(basis, changes, token, p => ((IProgress<ScanProgress>)progress).Report(p));
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
                if (changes == null && !scoped) lastFullCheck = DateTime.UtcNow;
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
                if (!Paths.Equal(basis.Root.Path, snapshot.Root.Path)) AttachLive(true);
                if (requestedPath != null) U<TextBox>("PathBox").Text = current.Path;
                if (!Object.ReferenceEquals(snapshot, basis)) { viewingCached = false; cacheSaved = true; }
                UpdateMetrics(); BuildMap(); await RefreshRowsAsync();
                await SaveLastPathAsync(current.Path);
                SetLiveState("زنده · تغییرات خودکار دنبال می‌شوند");
                SetStatus(L.T("تغییرات بررسی و ذخیره شد") + "  " + (requestedPath ?? basis.Root.Path)); RefreshDrives();
                nextLiveAttempt = DateTime.UtcNow.AddSeconds(3);
            }
            catch (OperationCanceledException)
            {
                if (!closing) { SetLiveState("بررسی متوقف شد؛ نتیجهٔ قبلی حفظ شده است"); SetStatus(L.T("بررسی متوقف شد؛ نتیجهٔ قبلی حفظ شده است")); }
                if (liveMonitor != null && !scoped) liveMonitor.RequireFull();
                nextLiveAttempt = DateTime.UtcNow.AddSeconds(30);
            }
            catch (Exception e)
            {
                if (!closing)
                {
                    SetLiveState("بررسی ممکن نشد؛ تلاش دوباره به‌صورت خودکار");
                    SetStatus(L.T("بررسی ممکن نشد؛ تلاش دوباره به‌صورت خودکار") + "  ·  " + L.T(e.Message));
                }
                if (liveMonitor != null && !scoped) liveMonitor.RequireFull();
                nextLiveAttempt = DateTime.UtcNow.AddSeconds(15);
            }
            finally
            {
                refreshing = false; refreshCancel.Dispose(); refreshCancel = null;
                if (!closing) SetBusy(false, false);
            }
        }
    }
}
