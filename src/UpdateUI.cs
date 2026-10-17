using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace Spacio
{
    public sealed partial class Controller
    {
        private ReleaseInfo availableUpdate;
        private bool checkingUpdate, installingUpdate;
        private string updateState = "", updateDetail = "";
        private readonly CancellationTokenSource updateLifetime = new CancellationTokenSource();
        private CancellationTokenSource downloadCancel;
        private DispatcherTimer updateTimer;

        private void InitializeUpdates()
        {
            U<TextBlock>("VersionLabel").Text = "SPACIO  /  " + Updates.VersionText;
            U<Button>("UpdateButton").Click += async delegate { await CheckUpdatesAsync(true); };
            U<Button>("InstallUpdateButton").Click += async delegate { await InstallUpdateAsync(); };
            U<Button>("ReleaseNotesButton").Click += delegate { OpenShell(availableUpdate == null ? Updates.Repository + "/releases/latest" : availableUpdate.PageUrl, null); };
            U<Button>("DismissUpdateButton").Click += delegate {
                if (installingUpdate) { if (downloadCancel != null) downloadCancel.Cancel(); }
                else U<FrameworkElement>("UpdateBanner").Visibility = Visibility.Collapsed;
            };
        }
        public async void StartUpdateChecks()
        {
            updateTimer = new DispatcherTimer { Interval = TimeSpan.FromHours(6) };
            updateTimer.Tick += async delegate { await CheckUpdatesAsync(false); }; updateTimer.Start();
            await CheckUpdatesAsync(false);
        }
        private void StopUpdates()
        {
            if (updateTimer != null) updateTimer.Stop();
            updateLifetime.Cancel();
            if (downloadCancel != null) downloadCancel.Cancel();
        }
        private void UpdateLabels()
        {
            U<TextBlock>("UpdateTitle").Text = L.T(updateState) + (availableUpdate == null ? "" : "  ·  " + availableUpdate.Version.ToString(3));
            U<TextBlock>("UpdateDetail").Text = L.T(updateDetail);
            U<Button>("UpdateButton").Content = L.T(checkingUpdate ? "در حال بررسی آپدیت…" : "بررسی آپدیت");
            U<Button>("UpdateButton").IsEnabled = !checkingUpdate && !installingUpdate;
            U<Button>("InstallUpdateButton").IsEnabled = !busy && !installingUpdate && !checkingUpdate;
            U<Button>("InstallUpdateButton").Visibility = availableUpdate != null && !installingUpdate ? Visibility.Visible : Visibility.Collapsed;
            U<Button>("DismissUpdateButton").Content = L.T(installingUpdate ? "لغو دانلود" : "بعداً");
            U<ProgressBar>("UpdateProgress").Visibility = installingUpdate ? Visibility.Visible : Visibility.Collapsed;
        }
        public async Task CheckUpdatesAsync(bool manual)
        {
            if (checkingUpdate || installingUpdate || closing) return;
            checkingUpdate = true; updateState = "در حال بررسی آپدیت…"; updateDetail = "دریافت نسخهٔ رسمی از GitHub";
            if (manual) U<FrameworkElement>("UpdateBanner").Visibility = Visibility.Visible;
            UpdateLabels();
            try
            {
                var release = await Updates.CheckAsync(updateLifetime.Token);
                if (closing) return;
                availableUpdate = release;
                updateState = release == null ? "برنامه به‌روز است" : "نسخهٔ جدید آماده است";
                updateDetail = release == null ? "آخرین نسخهٔ منتشرشده را دارید." : "پس از دانلود، برنامه دوباره باز می‌شود. اسکن‌های ذخیره‌شده حفظ می‌شوند.";
                if (release != null) U<FrameworkElement>("UpdateBanner").Visibility = Visibility.Visible;
            }
            catch (Exception e)
            {
                if (closing) return;
                updateState = "بررسی آپدیت ممکن نشد";
                updateDetail = "اتصال اینترنت را بررسی کنید و دوباره تلاش کنید. اسکن آفلاین همچنان در دسترس است.";
                U<Button>("UpdateButton").ToolTip = L.T(updateDetail) + "\n" + e.Message;
            }
            finally { checkingUpdate = false; if (!closing) UpdateLabels(); }
        }
        private async Task InstallUpdateAsync()
        {
            if (availableUpdate == null || busy || installingUpdate || checkingUpdate || closing) return;
            installingUpdate = true; SetBusy(true, false);
            downloadCancel = CancellationTokenSource.CreateLinkedTokenSource(updateLifetime.Token);
            updateState = "در حال دانلود آپدیت…"; updateDetail = "فایل دریافت و صحت آن بررسی می‌شود؛ اسکن‌های شما حفظ می‌شوند.";
            U<ProgressBar>("UpdateProgress").Value = 0; UpdateLabels();
            try
            {
                string target = System.Reflection.Assembly.GetExecutingAssembly().Location;
                UpdateInstaller.CheckWritable(target);
                var progress = new Progress<int>(value => { if (!closing && installingUpdate) U<ProgressBar>("UpdateProgress").Value = value; });
                string stage = await Updates.DownloadAsync(availableUpdate, progress, downloadCancel.Token);
                downloadCancel.Token.ThrowIfCancellationRequested();
                updateState = "در حال نصب و باز کردن دوبارهٔ برنامه…"; UpdateLabels();
                using (var helper = UpdateInstaller.Start(stage, target, Process.GetCurrentProcess())) { }
                installingUpdate = false; SetBusy(false, false); Window.Close();
            }
            catch (OperationCanceledException) { updateState = "دانلود لغو شد"; updateDetail = "نسخهٔ فعلی و اسکن‌های شما حفظ شده‌اند."; }
            catch (Exception e) { updateState = "آپدیت نصب نشد"; updateDetail = L.T(e.Message); }
            finally
            {
                downloadCancel.Dispose(); downloadCancel = null; installingUpdate = false;
                if (!closing) { SetBusy(false, false); UpdateLabels(); }
            }
        }
        public void ShowUpdatePreview()
        {
            availableUpdate = new ReleaseInfo { Version = new Version(1, 4, 0, 0), PageUrl = Updates.Repository + "/releases/latest" };
            updateState = "نسخهٔ جدید آماده است"; updateDetail = "پس از دانلود، برنامه دوباره باز می‌شود. اسکن‌های ذخیره‌شده حفظ می‌شوند.";
            U<FrameworkElement>("UpdateBanner").Visibility = Visibility.Visible; UpdateLabels();
        }
    }
}
