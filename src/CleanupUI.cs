using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Interop;
using System.Windows.Media;

namespace Spacio
{
    internal sealed class CleanupChoice
    {
        internal bool Confirmed, Permanent, Uninstall;
        internal List<CleanupItem> Items;
    }
    internal sealed class CleanupDetail
    {
        public string Path { get; set; }
        public string Size { get; set; }
        public string Kind { get; set; }
        public string Date { get; set; }
        public string Status { get; set; }
    }
    public sealed partial class Controller
    {
        private bool cleanupPageOpen, cleanupAppsPage;
        private CancellationTokenSource cleanupCancel;
        private List<CleanupItem> cleanupItems = new List<CleanupItem>();
        private List<InstalledApp> installedApps = new List<InstalledApp>();
        private string cleanupFamily = "";
        private string cleanupStatusText = "";
        private CacheRoots cleanupRoots = new CacheRoots();
        private void InitializeCleanup()
        {
            U<Button>("CacheNav").Click += async delegate { await OpenCleanupPage(false); };
            U<Button>("AppsButton").Click += async delegate { await OpenCleanupPage(true); };
            U<Button>("CleanupBack").Click += delegate { if (cleanupCancel == null) SetMode(0, true); };
            U<Button>("CleanupLanguage").Click += delegate { U<Button>("LanguageButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            U<Button>("CleanupScan").Click += async delegate { await ScanCleanupPage(); };
            U<Button>("CleanupStop").Click += delegate { if (cleanupCancel != null) cleanupCancel.Cancel(); };
            U<Button>("CleanupReview").Click += async delegate {
                if (busy) return;
                if (cleanupAppsPage) { var app = U<DataGrid>("InstalledAppsGrid").SelectedItem as InstalledApp; if (app != null) await ReviewApplication(app); }
                else try { await ReviewCleanupItems(U<DataGrid>("CacheGrid").SelectedItems.Cast<CleanupItem>().ToList(), null); } catch (OperationCanceledException) { CleanupStatus("بررسی متوقف شد؛ نتیجهٔ قبلی حفظ شده است"); } catch (Exception e) { Error(e); }
            };
            U<Button>("CleanupSettings").Click += delegate { OpenShell("ms-settings:appsfeatures", null); };
            U<Button>("CleanupLeftovers").Click += async delegate { if (lastUninstall != null && !busy) try { await ContinueUninstall(lastUninstall); } catch (Exception e) { Error(e); } };
            U<TextBox>("CleanupSearch").TextChanged += delegate { FilterCleanup(); };
            U<DataGrid>("CacheGrid").SelectionChanged += delegate { UpdateCleanupControls(); };
            U<DataGrid>("InstalledAppsGrid").SelectionChanged += delegate { UpdateCleanupControls(); };
        }
        private async Task OpenCleanupPage(bool apps, UninstallSession leftovers = null)
        {
            if (installingUpdate || cleanupCancel != null || (busy && !refreshingAutomatically)) return;
            if (leftoverView != leftovers) cleanupItems.Clear();
            cleanupPageOpen = true; cleanupAppsPage = apps; cleanupFamily = ""; leftoverView = leftovers;
            if (leftovers != null) U<TextBox>("CleanupSearch").Text = "";
            U<Grid>("Workspace").Visibility = Visibility.Collapsed;
            U<Grid>("CleanupWorkspace").Visibility = Visibility.Visible;
            foreach (string name in navNames.Concat(new [] { "FilesNav", "CacheNav", "AppsButton" })) SetNavActive(name, name == (apps ? "AppsButton" : "CacheNav"));
            LocalizeCleanup();
            if (refreshingAutomatically && activeRefreshTask != null) { refreshCancel.Cancel(); await activeRefreshTask; }
            if (liveMonitor != null) { liveMonitor.Dispose(); liveMonitor = null; }
            if (!busy) await ScanCleanupPage();
        }
        private void CloseCleanupPage()
        {
            cleanupPageOpen = false; leftoverView = null;
            U<Grid>("CleanupWorkspace").Visibility = Visibility.Collapsed; U<Grid>("Workspace").Visibility = Visibility.Visible;
            SetNavActive("CacheNav", false); SetNavActive("AppsButton", false);
            AttachLive(true);
        }
        private void LocalizeCleanup()
        {
            U<TextBlock>("CleanupTitle").Text = cleanupAppsPage ? L.T("برنامه‌های نصب‌شده") : "Cache";
            U<TextBlock>("CleanupSubtitle").Text = L.T(cleanupAppsPage ? "حذف‌کنندهٔ برنامه و بررسی کش‌های باقی‌مانده" : "مسیر، حجم و وابستگی کش‌ها، پیش از هر پاک‌سازی");
            U<TextBlock>("CleanupCoverage").Text = L.T(cleanupAppsPage ? "برنامه‌های ثبت‌شدهٔ دسکتاپ نمایش داده می‌شوند. حذف برنامه با حذف‌کنندهٔ خودش انجام می‌شود؛ کش‌های باقی‌مانده جداگانه مرور و تأیید می‌شوند." : "کش‌های شناخته‌شدهٔ کاربر فعلی و پوشه‌های احتمالی در اسکن بازشده. این فهرست تضمین همهٔ کش‌های سیستم نیست. کش مشترک، فایل شخصی و پوشهٔ احتمالی یکسان نیستند.");
            if (leftoverView != null)
            {
                U<TextBlock>("CleanupTitle").Text = L.T("باقی‌مانده‌های برنامه") + " · " + leftoverView.App.Name;
                U<TextBlock>("CleanupSubtitle").Text = L.T("مسیر و حجم داده‌ها و کش‌ها؛ انتخاب و تأیید جداگانه پیش از حذف");
                U<TextBlock>("CleanupCoverage").Text = L.T("همهٔ موارد بدون تیک‌اند. موارد احتمالی و مشترک را بررسی کنید؛ داده‌های برنامه می‌توانند شامل تنظیمات و اطلاعات حساب باشند. پوشش همهٔ مسیرهای سفارشی تضمین نمی‌شود.");
            }
            U<TextBlock>("CleanupEmpty").Text = L.T(leftoverView == null ? "برای دیدن موارد، بررسی را اجرا کنید" : "برای این برنامه موردی در فهرست نیست؛ وضعیت بررسی را پایین صفحه ببینید");
            U<Button>("CleanupLanguage").Content = L.Persian ? "English ⇄" : "فارسی ⇄";
            U<TextBox>("CleanupSearch").ToolTip = L.T("جست‌وجو با نام برنامه یا مسیر");
            U<TextBlock>("CleanupStatus").Text = L.T(cleanupStatusText);
            string[] cacheHeaders = { "برنامه و مسیر کش", "اندازه", "تعداد فایل", "نوع", "وضعیت" };
            for (int i = 0; i < cacheHeaders.Length; i++) U<DataGrid>("CacheGrid").Columns[i].Header = L.T(cacheHeaders[i]);
            string[] appHeaders = { "نام", "نسخه", "سازنده", "مسیر نصب" };
            for (int i = 0; i < appHeaders.Length; i++) U<DataGrid>("InstalledAppsGrid").Columns[i].Header = L.T(appHeaders[i]);
            U<DataGrid>("CacheGrid").Visibility = cleanupAppsPage ? Visibility.Collapsed : Visibility.Visible;
            U<DataGrid>("InstalledAppsGrid").Visibility = cleanupAppsPage ? Visibility.Visible : Visibility.Collapsed;
            U<Button>("CleanupReview").Content = L.T(cleanupAppsPage ? "مرور حذف برنامه و کش" : "مرور پاک‌سازی");
            FilterCleanup(); UpdateCleanupControls();
        }
        private void FilterCleanup()
        {
            string search = U<TextBox>("CleanupSearch").Text.Trim(); int count;
            U<TextBlock>("CleanupSearchPlaceholder").Visibility = search.Length == 0 ? Visibility.Visible : Visibility.Collapsed;
            if (cleanupAppsPage)
            {
                var rows = installedApps.Where(a => (a.Name + " " + a.Publisher + " " + a.Location).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                U<DataGrid>("InstalledAppsGrid").ItemsSource = rows; count = rows.Count;
            }
            else
            {
                var rows = cleanupItems.Where(i => (cleanupFamily.Length == 0 || (i.Spec != null && i.Spec.Family == cleanupFamily)) && (i.Path + " " + i.Owner).IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0).OrderByDescending(i => i.Entry == null ? 0 : i.Entry.Bytes).ToList();
                U<DataGrid>("CacheGrid").ItemsSource = rows; count = rows.Count;
            }
            U<TextBlock>("CleanupCount").Text = count.ToString("N0") + " " + L.T("مورد");
            U<TextBlock>("CleanupEmpty").Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        private void UpdateCleanupControls()
        {
            if (U<Button>("CleanupReview") == null) return;
            U<Button>("CleanupReview").IsEnabled = !busy && (cleanupAppsPage ? U<DataGrid>("InstalledAppsGrid").SelectedItem != null : U<DataGrid>("CacheGrid").SelectedItems.Count > 0);
            U<Button>("CleanupScan").IsEnabled = !busy;
            U<Button>("CleanupLanguage").IsEnabled = !busy;
            U<Button>("CleanupBack").IsEnabled = cleanupCancel == null;
            U<Button>("CleanupStop").Visibility = cleanupCancel == null ? Visibility.Collapsed : Visibility.Visible;
            U<Button>("CleanupLeftovers").Visibility = lastUninstall == null || leftoverView != null ? Visibility.Collapsed : Visibility.Visible;
            U<Button>("CleanupLeftovers").IsEnabled = !busy && cleanupCancel == null;
            if (cleanupCancel != null && !cleanupPageOpen) U<Button>("StopButton").Visibility = Visibility.Visible;
        }
        private void CleanupStatus(string message)
        {
            cleanupStatusText = message; U<TextBlock>("CleanupStatus").Text = L.T(message); U<TextBlock>("CleanupStatus").ToolTip = L.T(message);
        }
        private async Task ScanCleanupPage()
        {
            if (busy) return;
            cleanupCancel = new CancellationTokenSource(); var token = cleanupCancel.Token;
            SetBusy(true, false); CleanupStatus(cleanupAppsPage ? "در حال خواندن فهرست برنامه‌ها…" : "در حال بررسی کش‌ها؛ فایل‌ها تغییر نمی‌کنند");
            try
            {
                if (leftoverView != null)
                {
                    var session = leftoverView;
                    var progress = new Progress<string>(p => { if (!token.IsCancellationRequested) CleanupStatus(L.T("بررسی باقی‌مانده‌های برنامه") + " · " + p); });
                    var rows = await Task.Run(() => AppLeftovers.Scan(session, token, progress), token);
                    token.ThrowIfCancellationRequested(); cleanupItems = rows;
                }
                else if (cleanupAppsPage) installedApps = await Task.Run(() => InstalledApps.Read(token), token);
                else
                {
                    var basis = snapshot;
                    var progress = new Progress<string>(p => { if (cleanupCancel != null && !token.IsCancellationRequested) CleanupStatus("در حال بررسی کش‌ها؛ فایل‌ها تغییر نمی‌کنند" + "  " + p); });
                    var rows = await Task.Run(() => {
                        var found = new List<CleanupItem>();
                        foreach (var spec in CacheCatalog.Discover(cleanupRoots, token)) { token.ThrowIfCancellationRequested(); ((IProgress<string>)progress).Report(spec.Path); found.Add(CacheCatalog.Inspect(spec, token, cleanupRoots)); }
                        found.AddRange(CacheCatalog.Candidates(basis).Where(c => !found.Any(i => Paths.Within(c.Path, i.Path))));
                        return found;
                    }, token);
                    token.ThrowIfCancellationRequested(); cleanupItems = rows;
                }
                FilterCleanup(); CleanupStatus(leftoverView != null && cleanupItems.Count == 0 ? "در مسیرهای بررسی‌شده باقی‌مانده‌ای پیدا نشد؛ مسیرهای سفارشی ممکن است پوشش داده نشوند" : "بررسی تمام شد؛ برای دیدن جزئیات موارد را انتخاب کنید");
            }
            catch (OperationCanceledException) { CleanupStatus("بررسی متوقف شد؛ نتیجهٔ قبلی حفظ شده است"); }
            catch (Exception e) { CleanupStatus(e.Message); }
            finally { cleanupCancel.Dispose(); cleanupCancel = null; SetBusy(false, false); }
        }
        private async Task ReviewApplication(InstalledApp app)
        {
            await PrepareCleanupReview(new List<CleanupItem>(), app, app.Family.Length == 0 ? new string[0] : new [] { app.Family });
        }
        private async Task ReviewSelectedCleanup(Action<Window> onLoaded = null)
        {
            if (installingUpdate || reviewingCleanup || cleanupCancel != null || (busy && !refreshingAutomatically) || snapshot == null || snapshot.Cancelled) return;
            var paths = Safety.CollapseSelection(Selected().Select(r => r.Entry)).Select(e => e.Path).ToArray(); if (paths.Length == 0) return;
            reviewingCleanup = true;
            try
            {
                if (refreshingAutomatically && activeRefreshTask != null) { refreshCancel.Cancel(); await activeRefreshTask; }
                if (closing) return;
                SetBusy(true, false);
                var entries = snapshot.Entries.Where(e => paths.Contains(e.Path, StringComparer.OrdinalIgnoreCase)).ToList();
                if (entries.Count != paths.Length) throw new IOException(L.T("مورد در حین اسکن جابه‌جا شده است."));
                var apps = await Task.Run(() => InstalledApps.Read(CancellationToken.None));
                var rows = new List<CleanupItem>(); var families = new HashSet<string>(); InstalledApp selectedApp = null;
                using (var operation = new CancellationTokenSource())
                {
                    cleanupCancel = operation; UpdateCleanupControls();
                    try
                    {
                        var basis = snapshot;
                        foreach (var entry in entries)
                        {
                            operation.Token.ThrowIfCancellationRequested();
                            var app = InstalledApps.Owner(entry.Path, apps);
                            if (app != null) { if (entries.Count == 1) selectedApp = app; if (app.Family.Length > 0) families.Add(app.Family); }
                            var row = app == null ? await Task.Run(() => CleanupEngine.InspectSelection(entry, basis, operation.Token), operation.Token) :
                                new CleanupItem { Path = entry.Path, Owner = app.Name, Entry = entry, Source = basis, Evidence = "موردی که در فهرست فایل‌ها انتخاب کردید", Failure = "نرم‌افزار نصب‌شده؛ حذف‌کنندهٔ برنامه را اجرا کنید" };
                            rows.Add(row);
                        }
                    }
                    finally { cleanupCancel = null; }
                }
                foreach (var spec in await Task.Run(() => CacheCatalog.Discover(cleanupRoots, CancellationToken.None)))
                    if (entries.Any(e => Paths.Within(e.Path, spec.Path))) families.Add(spec.Family);
                SetBusy(false, false);
                await PrepareCleanupReview(rows, selectedApp, families.ToArray(), onLoaded);
            }
            catch (OperationCanceledException) { SetStatus(L.T("بررسی متوقف شد؛ نتیجهٔ قبلی حفظ شده است")); }
            catch (Exception e) { Error(e); }
            finally { reviewingCleanup = false; SetBusy(false, false); }
        }
        private async Task PrepareCleanupReview(List<CleanupItem> selected, InstalledApp app, string[] families, Action<Window> onLoaded = null)
        {
            if (busy) return;
            var operation = new CancellationTokenSource(); cleanupCancel = operation; var token = operation.Token; SetBusy(true, false);
            SetStatus(L.T("در حال بررسی کش‌ها؛ فایل‌ها تغییر نمی‌کنند"));
            try
            {
                if (app != null && app.Provider == "hermes")
                {
                    var scan = await Task.Run(() => new Scanner().Scan(app.Location, token, null), token); token.ThrowIfCancellationRequested();
                    selected = new List<CleanupItem> { new CleanupItem { Path = app.Location, Owner = app.Name, Entry = scan.Root, Source = scan,
                        Evidence = "فایل‌ها و داده‌های برنامه؛ حذف با حذف‌کنندهٔ رسمی", Failure = "نرم‌افزار نصب‌شده؛ حذف‌کنندهٔ برنامه را اجرا کنید" } };
                }
                var related = await Task.Run(() => CacheCatalog.Discover(cleanupRoots, token).Where(s => families.Contains(s.Family)).Select(s => CacheCatalog.Inspect(s, token, cleanupRoots)).ToList(), token);
                foreach (var row in related) { row.Chosen = false; selected.Add(row); }
                SetBusy(false, false);
                if (cleanupCancel == operation) cleanupCancel = null;
                await ReviewCleanupItems(selected, app, false, onLoaded);
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { Error(e); }
            finally { if (cleanupCancel == operation) cleanupCancel = null; operation.Dispose(); SetBusy(false, false); }
        }
        private async Task ReviewCleanupItems(List<CleanupItem> selected, InstalledApp app, bool refresh = true, Action<Window> onLoaded = null)
        {
            if (selected.Count == 0 && app == null) return;
            if (refresh)
            {
                var operation = new CancellationTokenSource(); cleanupCancel = operation; SetBusy(true, false);
                try
                {
                    selected = await Task.Run(() => selected.Select(i => {
                        if (i.Leftover != null) return AppLeftovers.Inspect(i.Leftover, operation.Token);
                        if (i.Spec != null) return CacheCatalog.Inspect(i.Spec, operation.Token, cleanupRoots);
                        if (i.Entry == null || app != null) return i;
                        var fresh = CleanupEngine.InspectSelection(i.Entry, i.Source, operation.Token); fresh.Candidate = i.Candidate; return fresh;
                    }).ToList(), operation.Token);
                    operation.Token.ThrowIfCancellationRequested();
                    foreach (var row in selected) row.Chosen = row.CanChoose && !row.Candidate && !(row.Spec != null && row.Spec.Shared);
                }
                finally { cleanupCancel = null; operation.Dispose(); SetBusy(false, false); }
            }
            bool alreadyReviewing = reviewingCleanup; reviewingCleanup = true; CleanupChoice choice;
            try { choice = ShowCleanupReview(selected, app, onLoaded); }
            finally { reviewingCleanup = alreadyReviewing; }
            if (!choice.Confirmed) return;
            if (choice.Uninstall)
            {
                try
                {
                    await BeginUninstall(app);
                }
                catch (OperationCanceledException) { CleanupStatus("بررسی متوقف شد؛ نتیجهٔ قبلی حفظ شده است"); }
                catch (Exception e) { Error(e); }
                return;
            }
            await ExecuteCleanupChoice(choice);
        }
        private async Task ExecuteCleanupChoice(CleanupChoice choice)
        {
            var selected = CleanupEngine.Collapse(choice.Items.Where(i => i.Chosen && i.CanChoose));
            if (selected.Count == 0) return;
            bool wasStale = snapshot != null && snapshot.IsStale;
            // Cache invalidation must not mutate the already reviewed safety snapshot.
            foreach (var item in selected) if (item.Spec == null) item.Source = new Snapshot { Root = item.Source.Root, Entries = item.Source.Entries, Cancelled = item.Source.Cancelled, IsStale = item.Source.IsStale };
            bool ownToken = cleanupCancel == null; if (ownToken) cleanupCancel = new CancellationTokenSource();
            var token = cleanupCancel.Token; SetBusy(true, false);
            try
            {
                await Task.Run(() => cache.MarkChanged(selected.Select(i => i.Path)));
                if (snapshot != null) stale = snapshot.IsStale;
                UpdateMetrics();
                var reports = await CleanupEngine.Execute(selected, choice.Permanent, new WindowInteropHelper(Window).Handle, token);
                bool reconciled = await ReconcileCleanup(selected.Select(i => i.Path).ToArray(), wasStale);
                ShowDialogText("گزارش پاک‌سازی", String.Join("\n\n", reports), false);
                if (reconciled) CleanupStatus("پاک‌سازی پایان یافت؛ گزارش شامل موارد انجام‌شده و خطاهاست");
                SetStatus(L.T(reconciled ? "پاک‌سازی پایان یافت؛ گزارش شامل موارد انجام‌شده و خطاهاست" : "داده‌ها تغییر کرده‌اند؛ دوباره اسکن کنید"));
                cleanupItems.Clear(); FilterCleanup(); RefreshDrives();
            }
            catch (Exception e) { Error(e); }
            finally
            {
                if (ownToken) { cleanupCancel.Dispose(); cleanupCancel = null; }
                SetBusy(false, false); UpdateSelection();
            }
            if (leftoverView != null && cleanupCancel == null && !closing) await ScanCleanupPage();
        }
        private async Task<bool> ReconcileCleanup(string[] paths, bool previouslyStale)
        {
            if (snapshot == null || snapshot.Cancelled || !paths.Any(p => Paths.Within(p, snapshot.Root.Path) || Paths.Within(snapshot.Root.Path, p))) return true;
            var basis = snapshot;
            try
            {
                string currentPath = current.Path;
                var updated = await Task.Run(() => {
                    var copy = new Snapshot { Root = basis.Root, Entries = basis.Entries, IsStale = previouslyStale, StartedUtc = basis.StartedUtc, Elapsed = basis.Elapsed, ErrorCount = basis.ErrorCount, Errors = basis.Errors };
                    var changes = paths.Where(p => Paths.Within(p, basis.Root.Path)).ToArray();
                    if (changes.Length == 0) changes = new [] { basis.Root.Path };
                    var result = LiveScanner.Refresh(copy, changes, CancellationToken.None, null, true);
                    cache.Save(result, CancellationToken.None); return result;
                });
                snapshot = updated; current = updated.Entries.FirstOrDefault(e => e.IsDirectory && Paths.Equal(e.Path, currentPath)) ?? updated.Root;
                stale = updated.IsStale; cacheSaved = true; viewingCached = false;
                U<TextBox>("PathBox").Text = current.Path;
                UpdateMetrics(); BuildMap(); await RefreshRowsAsync(); await SaveLastPathAsync(current.Path);
                return true;
            }
            catch (Exception e) { if (liveMonitor != null) liveMonitor.RequireFull(); CleanupStatus("داده‌ها تغییر کرده‌اند؛ دوباره اسکن کنید" + " · " + e.Message); return false; }
        }

        private CleanupChoice ShowCleanupReview(List<CleanupItem> items, InstalledApp app, Action<Window> onLoaded = null)
        {
            var choice = new CleanupChoice { Items = items };
            var work = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(Window).Handle).WorkingArea;
            var source = PresentationSource.FromVisual(Window);
            var available = source.CompositionTarget.TransformFromDevice.Transform(new Point(work.Width, work.Height));
            double width = Math.Min(1120, available.X - 24), height = Math.Min(790, available.Y - 24);
            var dialog = new Window { Owner = Window, Title = L.T("مرور حذف و کش‌های مرتبط"), Width = width, Height = height, MinWidth = Math.Min(980, width), MinHeight = Math.Min(500, height),
                Background = B("#10181C"), Foreground = B("#E4EFEB"), FontFamily = Window.FontFamily, FontSize = 12, FlowDirection = Window.FlowDirection,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Resources = Window.Resources };
            var layout = new Grid { Margin = new Thickness(22) }; dialog.Content = layout;
            foreach (var rowHeight in new [] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) layout.RowDefinitions.Add(new RowDefinition { Height = rowHeight });
            var heading = new StackPanel { Margin = new Thickness(0, 0, 0, 12) }; layout.Children.Add(heading);
            heading.Children.Add(new TextBlock { Text = app == null ? L.T("قبل از حذف، مسیرها و محتوای دقیق را بررسی کنید") : app.Name + "  ·  " + app.Version, FontSize = 20, FontWeight = FontWeights.SemiBold });
            heading.Children.Add(new TextBlock { Text = L.T("کش‌های اضافی و موارد مشترک یا احتمالی خودکار انتخاب نمی‌شوند. انتخاب ردیف، همهٔ فایل‌های آن را در جدول پایین نشان می‌دهد."), Foreground = B("#9EBBB0"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 9, 0, 0) });
            heading.Children.Add(new TextBlock { Text = L.T(app == null ? "برای هر فایل، کش اختصاصی قابل تشخیص نیست؛ فقط ارتباط‌های شناخته‌شده نمایش داده می‌شوند." : "حذف‌کنندهٔ برنامه جدا اجرا می‌شود. کش‌ها فقط با انتخاب و تأیید پاک‌سازی حذف می‌شوند."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0), Foreground = B("#B8ACA0") });
            if (app != null) heading.Children.Add(new TextBox { Text = app.Command, IsReadOnly = true, FlowDirection = FlowDirection.LeftToRight, FontSize = 11, Height = 32, Margin = new Thickness(0, 10, 0, 0), Padding = new Thickness(6, 5, 6, 5), ToolTip = app.Command, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto });
            if (app != null && app.Provider == "hermes") heading.Children.Add(new TextBlock { Text = L.T("حذف‌کنندهٔ رسمی Hermes باز می‌شود؛ برای حذف برنامه و داده‌ها، Full uninstall را انتخاب و تأیید کنید. سرویس‌ها، ورودی PATH، تنظیمات و تاریخچه ممکن است حذف شوند. این کار فقط حذف کش نیست."), TextWrapping = TextWrapping.Wrap, Foreground = B("#EDB2A4"), Margin = new Thickness(0, 8, 0, 0) });
            var grid = CleanupTable(); Grid.SetRow(grid, 1); layout.Children.Add(grid);
            grid.Name = "ReviewItems";
            var checkTemplate = new DataTemplate(); var check = new FrameworkElementFactory(typeof(CheckBox));
            check.SetBinding(CheckBox.IsCheckedProperty, new Binding("Chosen") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged });
            check.SetBinding(CheckBox.IsEnabledProperty, new Binding("CanChoose")); check.SetValue(CheckBox.HorizontalAlignmentProperty, HorizontalAlignment.Center); checkTemplate.VisualTree = check;
            if (app == null) grid.Columns.Add(new DataGridTemplateColumn { Header = L.T("انتخاب"), Width = 60, CellTemplate = checkTemplate });
            grid.Columns.Add(CleanupColumn("برنامه و مسیر کش", "Path", new DataGridLength(1, DataGridLengthUnitType.Star)));
            grid.Columns.Add(CleanupColumn("اندازه", "Size", 95)); grid.Columns.Add(CleanupColumn("نوع", "Kind", 125)); grid.Columns.Add(CleanupColumn("وضعیت", "Status", 185));
            grid.ItemsSource = items;
            var information = new StackPanel { Margin = new Thickness(0, 7, 0, 7) }; Grid.SetRow(information, 2); layout.Children.Add(information);
            var notice = new TextBox { Name = "ReviewNotice", IsReadOnly = true, TextWrapping = TextWrapping.Wrap, MaxHeight = 76, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, BorderThickness = new Thickness(0), Background = B("#10181C"), Foreground = B("#EDB2A4"), Padding = new Thickness(0, 2, 0, 5) }; information.Children.Add(notice);
            var detailsLabel = new TextBlock { Text = L.T("محتوای دقیق ردیف انتخاب‌شده"), Foreground = B("#8DD9BC") }; information.Children.Add(detailsLabel);
            var details = CleanupTable(); details.IsReadOnly = true; Grid.SetRow(details, 3); layout.Children.Add(details);
            details.Name = "ReviewDetails";
            details.Columns.Add(CleanupColumn("مسیر کامل", "Path", new DataGridLength(1, DataGridLengthUnitType.Star)));
            details.Columns.Add(CleanupColumn("اندازه", "Size", 110)); details.Columns.Add(CleanupColumn("نوع", "Kind", 120));
            details.Columns.Add(CleanupColumn("آخرین تغییر محتوا", "Date", 125));
            details.Columns.Add(CleanupColumn("وضعیت و پیوند", "Status", 210));
            int detailsVersion = 0;
            grid.SelectionChanged += async delegate {
                var item = grid.SelectedItem as CleanupItem;
                int version = ++detailsVersion; details.ItemsSource = null; notice.Text = item == null ? "" : item.ReviewNotice;
                var rows = item == null ? null : await Task.Run(() => item.Contents().Select(e => new CleanupDetail { Path = e.Path, Size = Formats.Size(e.Bytes), Kind = L.T((e.Attributes & FileAttributes.ReparsePoint) != 0 ? "پیوند" : e.IsDirectory ? "پوشه" : "فایل"), Date = Formats.Date(e.LatestUtc), Status = L.T(e.Note) }).ToList());
                if (version != detailsVersion) return; details.ItemsSource = rows;
                detailsLabel.Text = item == null ? L.T("محتوای دقیق ردیف انتخاب‌شده") : item.Owner + " · " + item.Evidence;
            };
            if (items.Count > 0) grid.SelectedIndex = 0;
            var footer = new Grid(); Grid.SetRow(footer, 4); layout.Children.Add(footer);
            footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); footer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var permanent = new CheckBox { Content = L.T("حذف دائمی به‌جای سطل بازیافت"), IsChecked = true, Margin = new Thickness(0, 10, 0, 6), Foreground = B("#EDB2A4") }; footer.Children.Add(permanent);
            permanent.Name = "ReviewPermanent";
            permanent.Visibility = app == null ? Visibility.Visible : Visibility.Collapsed;
            var acknowledge = new CheckBox { Content = L.T("می‌دانم موارد حذف دائمی از سطل بازیافت قابل بازگردانی نیستند"), Foreground = B("#EDB2A4"), Visibility = Visibility.Collapsed }; Grid.SetRow(acknowledge, 1); footer.Children.Add(acknowledge);
            acknowledge.Name = "ReviewAcknowledge";
            var dataAcknowledge = new CheckBox { Name = "ReviewDataAcknowledge", Content = L.T("حذف همهٔ داده‌های انتخابی و خودِ پیوندها را تأیید می‌کنم؛ مقصد پیوندها حذف نمی‌شود"), Foreground = B("#EDB2A4"), Visibility = Visibility.Collapsed, Margin = new Thickness(0, 6, 0, 0) }; Grid.SetRow(dataAcknowledge, 2); footer.Children.Add(dataAcknowledge);
            var summary = new TextBlock { HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(8, 10, 0, 0), Foreground = B("#9EBBB0"), Visibility = app == null ? Visibility.Visible : Visibility.Collapsed }; footer.Children.Add(summary);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) }; Grid.SetRow(buttons, 3); footer.Children.Add(buttons);
            var cancel = ReviewButton("لغو"); cancel.Click += delegate { dialog.DialogResult = false; }; buttons.Children.Add(cancel);
            cancel.Name = "ReviewCancel";
            if (app != null)
            {
                var settings = ReviewButton("تنظیمات برنامه‌های ویندوز"); buttons.Children.Add(settings);
                settings.Click += delegate { OpenShell("ms-settings:appsfeatures", null); };
                var uninstall = ReviewButton("اجرای حذف‌کنندهٔ برنامه"); buttons.Children.Add(uninstall);
                uninstall.Name = "ReviewUninstall"; uninstall.Background = B("#285D4C"); uninstall.Foreground = B("#E4FFF2");
                uninstall.ToolTip = app.Command;
                uninstall.Click += delegate { choice.Confirmed = true; choice.Uninstall = true; dialog.DialogResult = true; };
            }
            var apply = ReviewButton("تأیید انتقال به سطل بازیافت"); apply.Background = B("#694139"); buttons.Children.Add(apply);
            apply.Name = "ReviewApply";
            apply.Visibility = app == null ? Visibility.Visible : Visibility.Collapsed;
            Action update = () => { var chosen = CleanupEngine.Collapse(items.Where(i => i.Chosen && i.CanChoose)); bool data = chosen.Any(i => i.Removal != null && (i.Removal.AppData || i.Removal.LinkCount > 0));
                dataAcknowledge.Visibility = app == null && data ? Visibility.Visible : Visibility.Collapsed;
                bool recycleLinks = permanent.IsChecked != true && chosen.Any(i => i.Removal != null && i.Removal.LinkCount > 0);
                apply.ToolTip = recycleLinks ? L.T("پوشهٔ دارای پیوند به سطل بازیافت منتقل نمی‌شود؛ حذف دائمی فقط با تأیید شما انجام می‌شود") : null;
                summary.Text = chosen.Count + " " + L.T("مورد") + " · " + Formats.Size(chosen.Sum(i => i.Entry.Bytes)); acknowledge.Visibility = app == null && permanent.IsChecked == true ? Visibility.Visible : Visibility.Collapsed; apply.Content = L.T(permanent.IsChecked == true ? "تأیید حذف دائمی" : "تأیید انتقال به سطل بازیافت"); apply.IsEnabled = app == null && chosen.Count > 0 && !recycleLinks && (!data || dataAcknowledge.IsChecked == true) && (permanent.IsChecked != true || acknowledge.IsChecked == true); };
            dataAcknowledge.Click += delegate { update(); };
            permanent.Click += delegate { acknowledge.IsChecked = false; update(); }; acknowledge.Click += delegate { update(); };
            grid.AddHandler(CheckBox.CheckedEvent, new RoutedEventHandler((s, e) => dialog.Dispatcher.BeginInvoke(update)));
            grid.AddHandler(CheckBox.UncheckedEvent, new RoutedEventHandler((s, e) => dialog.Dispatcher.BeginInvoke(update)));
            apply.Click += delegate { grid.CommitEdit(); update(); if (!apply.IsEnabled) return; choice.Confirmed = true; choice.Permanent = permanent.IsChecked == true; dialog.DialogResult = true; };
            if (onLoaded != null) dialog.Loaded += delegate { onLoaded(dialog); };
            update(); dialog.ShowDialog(); return choice;
        }
        private Button ReviewButton(string title) { return new Button { Content = L.T(title), Margin = new Thickness(5, 0, 0, 0), Padding = new Thickness(14, 9, 14, 9) }; }
        private DataGrid CleanupTable()
        {
            return new DataGrid { AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = false, HeadersVisibility = DataGridHeadersVisibility.Column,
                SelectionMode = DataGridSelectionMode.Single, SelectionUnit = DataGridSelectionUnit.FullRow, Background = B("#161E24"), BorderThickness = new Thickness(0),
                GridLinesVisibility = DataGridGridLinesVisibility.None, RowHeight = 38, EnableRowVirtualization = true, EnableColumnVirtualization = true };
        }
        private DataGridTextColumn CleanupColumn(string title, string property, DataGridLength width)
        {
            var style = new Style(typeof(TextBlock)); style.Setters.Add(new Setter(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis));
            style.Setters.Add(new Setter(TextBlock.ToolTipProperty, new Binding(property)));
            if (property == "Path") style.Setters.Add(new Setter(TextBlock.FlowDirectionProperty, FlowDirection.LeftToRight));
            return new DataGridTextColumn { Header = L.T(title), Binding = new Binding(property), Width = width, IsReadOnly = true, ElementStyle = style };
        }
    }
}
