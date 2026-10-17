using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Spacio
{
    public sealed class Row
    {
        public Entry Entry { get; set; }
        public string Name { get { return Entry.Name; } }
        public string Size { get { return Formats.Size(Entry.Bytes); } }
        public string Date { get { return Formats.Date(Entry.LatestUtc); } }
        public string Count { get { return Entry.IsDirectory ? Entry.FileCount.ToString("N0") : "—"; } }
        public Geometry IconPath { get { return Geometry.Parse(Entry.IsDirectory ? "M1,5 L1,17 L21,17 L21,5 L11,5 L8,2 L1,2 Z" : "M4,1 L13,1 L20,8 L20,21 L4,21 Z M13,1 L13,8 L20,8"); } }
        public TextAlignment NameAlignment { get { return L.Persian ? TextAlignment.Right : TextAlignment.Left; } }
        public string State { get; set; }
        public string Detail { get; set; }
        public Brush Color { get; set; }
        public double Percent { get; set; }
        public string PercentText { get { return Percent.ToString("0.#", CultureInfo.InvariantCulture) + "%"; } }
    }
    public sealed class Tile
    {
        public string Label; public long Bytes; public Entry Entry; public string Category; public int Color;
    }

    public sealed partial class Controller
    {
        public Window Window { get; private set; }
        private Snapshot snapshot;
        private Entry current;
        private CancellationTokenSource scanCancel, queryCancel;
        private bool busy, stale, changingLanguage, closing;
        private bool viewingCached, cacheSaved;
        private bool savingScan, closeAfterSave;
        private readonly SnapshotCache cache;
        private int mode, queryVersion;
        private string category;
        private List<Entry> filtered = new List<Entry>();
        private List<Tile> tiles = new List<Tile>();
        private DispatcherTimer searchTimer;
        private readonly string[] palette = { "#347F6C", "#375F7C", "#695B86", "#886849", "#3E7278", "#79576C", "#536F4D", "#4D5E77", "#78644F", "#51646C", "#574F6C", "#4B6560", "#374A53" };
        private readonly string[] navNames = { "OverviewNav", "LargestNav", "OldNav", "TypesNav" };
        private readonly string[] titles = { "پوشه‌ها و فایل‌ها", "فایل‌های بزرگ", "فایل‌های قدیمی", "نوع فایل‌ها" };
        private readonly string[] categories = { "همهٔ نوع‌ها", "ویدیو", "تصویر", "صدا", "آرشیو", "سند", "برنامه", "سایر" };
        public Controller(string cacheDirectory = null)
        {
            cache = new SnapshotCache(cacheDirectory);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spacio.MainWindow.xaml")) Window = (Window)XamlReader.Load(stream);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spacio.BrandMark.xaml")) U<Image>("BrandLogo").Source = (DrawingImage)XamlReader.Load(stream);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spacio.ico")) Window.Icon = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            searchTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(240) };
            searchTimer.Tick += delegate { searchTimer.Stop(); RefreshRows(); };
            U<Button>("LanguageButton").Click += delegate { L.Persian = !L.Persian; ApplyLanguage(); try { L.Save(); } catch (Exception e) { if (e is IOException || e is UnauthorizedAccessException) SetStatus(L.T("خطا در ذخیرهٔ زبان؛ این انتخاب فقط برای اجرای فعلی است.")); else throw; } };
            U<Button>("BrowseButton").Click += delegate { Browse(); };
            U<Button>("ScanButton").Click += async delegate { await ScanAsync(U<TextBox>("PathBox").Text); };
            U<Button>("RescanButton").Click += async delegate { await ScanAsync(U<TextBox>("PathBox").Text, true); };
            U<Button>("StopButton").Click += delegate { if (scanCancel != null) scanCancel.Cancel(); };
            U<TextBox>("PathBox").KeyDown += async (s, e) => { if (e.Key == Key.Enter && !busy) await ScanAsync(U<TextBox>("PathBox").Text); };
            U<TextBox>("SearchBox").TextChanged += delegate { U<TextBlock>("SearchPlaceholder").Visibility = U<TextBox>("SearchBox").Text.Length == 0 ? Visibility.Visible : Visibility.Collapsed; searchTimer.Stop(); searchTimer.Start(); };
            foreach (var name in new [] { "SizeFilter", "AgeFilter", "SortFilter" }) U<ComboBox>(name).SelectionChanged += delegate { if (!changingLanguage) RefreshRows(); };
            for (int i = 0; i < navNames.Length; i++) { int selected = i; U<Button>(navNames[i]).Click += delegate { SetMode(selected); }; }
            U<Button>("UpButton").Click += delegate { if (current != null && current.Parent != null) Navigate(current.Parent); };
            U<Button>("IssuesButton").Click += delegate { ShowIssues(); };
            U<Button>("AppsButton").Click += delegate { OpenShell("ms-settings:appsfeatures", null); };
            U<Button>("BinButton").Click += delegate { OpenShell("explorer.exe", "shell:RecycleBinFolder"); };
            U<Button>("RevealButton").Click += delegate { Reveal(); };
            U<Button>("RecycleButton").Click += async delegate { await RecycleSelected(); };
            U<Button>("ExportButton").Click += async delegate { await ExportAsync(); };
            U<DataGrid>("FilesGrid").SelectionChanged += delegate { UpdateSelection(); };
            U<DataGrid>("FilesGrid").MouseDoubleClick += (s, e) => { var row = Selected().FirstOrDefault(); var cell = FindParent<DataGridRow>(e.OriginalSource as DependencyObject); if (row != null && cell != null && row.Entry.IsDirectory) Navigate(row.Entry); };
            U<DataGrid>("FilesGrid").PreviewKeyDown += async (s, e) => {
                if (e.Key == Key.Enter) { var row = Selected().FirstOrDefault(); if (row != null && row.Entry.IsDirectory) Navigate(row.Entry); e.Handled = true; }
                if (e.Key == Key.Delete) { await RecycleSelected(); e.Handled = true; }
                if (e.Key == Key.Back && current != null && current.Parent != null) { Navigate(current.Parent); e.Handled = true; }
            };
            U<Canvas>("SpaceMap").SizeChanged += delegate { DrawMap(); };
            Window.Closing += (s, e) => {
                if (savingScan) { closeAfterSave = true; e.Cancel = true; return; }
                if (busy && scanCancel == null) { e.Cancel = true; return; }
                closing = true; StopUpdates(); if (scanCancel != null) scanCancel.Cancel(); if (queryCancel != null) queryCancel.Cancel();
            };
            Window.Loaded += delegate { DrawMap(); };
            U<TextBox>("PathBox").Text = System.IO.Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
            InitializeUpdates(); ApplyLanguage();
        }
        private T U<T>(string name) where T : class { return Window.FindName(name) as T; }
        private static Brush B(string color) { var brush = (SolidColorBrush)new BrushConverter().ConvertFromString(color); brush.Freeze(); return brush; }
        private static T FindParent<T>(DependencyObject source) where T : DependencyObject { while (source != null) { if (source is T) return (T)source; source = VisualTreeHelper.GetParent(source); } return null; }

        public void ApplyLanguage()
        {
            changingLanguage = true;
            Window.FlowDirection = L.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
            Window.Title = L.Persian ? "اسپاسیو — کاوشگر فضا" : "Spacio — Storage Explorer";
            Localize(Window);
            U<Button>("LanguageButton").Content = L.Persian ? "English  ⇄" : "فارسی  ⇄";
            U<Button>("ScanButton").ToolTip = L.T("نتیجهٔ قبلی باز می‌شود؛ فقط مسیر جدید اسکن می‌شود.");
            U<Button>("RescanButton").ToolTip = L.T("حذف‌ها، جابه‌جایی‌ها و تغییر حجم در مسیر بالا بررسی و نتیجهٔ ذخیره‌شده به‌روز می‌شود.");
            Populate("SizeFilter", new [] { "همهٔ اندازه‌ها", "بیشتر از ۱۰ MiB", "بیشتر از ۱۰۰ MiB", "بیشتر از ۱ GiB" });
            Populate("AgeFilter", new [] { "همهٔ تاریخ‌ها", "قدیمی‌تر از ۳۰ روز", "قدیمی‌تر از ۹۰ روز", "قدیمی‌تر از ۱۸۰ روز", "قدیمی‌تر از یک سال" });
            Populate("SortFilter", new [] { "حجم: بزرگ به کوچک", "تاریخ: جدید به قدیم", "تاریخ: قدیم به جدید", "نام: الفبا" });
            string[] headers = { "نام", "اندازه", "سهم از پوشه", "آخرین تغییر محتوا", "تعداد فایل", "وضعیت" };
            for (int i = 0; i < headers.Length; i++) U<DataGrid>("FilesGrid").Columns[i].Header = L.T(headers[i]);
            U<TextBlock>("BytesMetric").ToolTip = L.T("این اندازه با فضای اشغال‌شدهٔ واقعی دیسک فرق دارد؛ فشرده‌سازی، hard link و فایل‌های sparse می‌توانند اختلاف ایجاد کنند.");
            U<ComboBox>("AgeFilter").ToolTip = L.T("تاریخ پوشه، جدیدترین تغییر خود پوشه یا محتوای آن است؛ قدیمی بودن به معنی بی‌استفاده بودن نیست.");
            U<Button>("ExportButton").ToolTip = L.T("خروجی شامل همهٔ نتایج فیلترشده است؛ اندازهٔ پوشه‌ها را دوباره با فایل‌ها جمع نکنید.");
            var menu = new ContextMenu();
            AddMenu(menu, "ورود به پوشه", delegate { var row = Selected().FirstOrDefault(); if (row != null && row.Entry.IsDirectory) Navigate(row.Entry); });
            AddMenu(menu, "نمایش در Explorer", Reveal);
            AddMenu(menu, "کپی مسیر", delegate { var row = Selected().FirstOrDefault(); if (row != null) try { Clipboard.SetText(row.Entry.Path); } catch (System.Runtime.InteropServices.COMException e) { Error(e); } });
            U<DataGrid>("FilesGrid").ContextMenu = menu;
            changingLanguage = false;
            RefreshDrives(); UpdateMetrics(); SetMode(mode); UpdateSelection();
            if (snapshot == null) SetStatus(L.T("آمادهٔ اسکن"));
            else SetStatus(L.T(stale ? "داده‌ها تغییر کرده‌اند؛ دوباره اسکن کنید" : snapshot.Cancelled ? "اسکن متوقف شد؛ نتیجه ناقص است" : viewingCached ? "نتیجهٔ قبلی بدون اسکن دوباره باز شد" : cacheSaved ? "اسکن کامل و ذخیره شد" : "اسکن کامل شد"));
            UpdateLabels();
        }
        private void Localize(DependencyObject node)
        {
            var element = node as FrameworkElement;
            if (element != null && element.Tag is string)
            {
                var text = element as TextBlock; var button = element as Button;
                if (text != null) text.Text = L.T((string)element.Tag);
                if (button != null) button.Content = L.T((string)element.Tag);
            }
            foreach (var child in LogicalTreeHelper.GetChildren(node)) if (child is DependencyObject) Localize((DependencyObject)child);
        }
        private void Populate(string name, string[] values)
        {
            var combo = U<ComboBox>(name); int selected = Math.Max(0, combo.SelectedIndex);
            combo.ItemsSource = values.Select(L.T).ToArray(); combo.SelectedIndex = selected;
        }
        private static void AddMenu(ContextMenu menu, string label, Action action) { var item = new MenuItem { Header = L.T(label) }; item.Click += delegate { action(); }; menu.Items.Add(item); }
        private void Browse()
        {
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.Description = L.T("انتخاب پوشه"); dialog.ShowNewFolderButton = false;
                if (Directory.Exists(U<TextBox>("PathBox").Text)) dialog.SelectedPath = U<TextBox>("PathBox").Text;
                if (dialog.ShowDialog(new Win32Owner(new WindowInteropHelper(Window).Handle)) == System.Windows.Forms.DialogResult.OK) U<TextBox>("PathBox").Text = dialog.SelectedPath;
            }
        }
        private void RefreshDrives()
        {
            var panel = U<StackPanel>("DriveList"); panel.Children.Clear();
            foreach (var drive in DriveInfo.GetDrives())
            {
                try
                {
                    if (!drive.IsReady || (drive.DriveType != DriveType.Fixed && drive.DriveType != DriveType.Removable)) continue;
                    string root = drive.Name;
                    var label = new TextBlock { Text = root.TrimEnd('\\') + "   " + drive.VolumeLabel, FontSize = 12, FontWeight = FontWeights.SemiBold, FlowDirection = FlowDirection.LeftToRight, TextTrimming = TextTrimming.CharacterEllipsis };
                    var bar = new ProgressBar { Height = 4, Maximum = 100, Value = drive.TotalSize == 0 ? 0 : 100.0 * (drive.TotalSize - drive.TotalFreeSpace) / drive.TotalSize, Background = B("#303C44"), Foreground = B("#76CDB7"), BorderThickness = new Thickness(0), Margin = new Thickness(0, 11, 0, 7), FlowDirection = FlowDirection.LeftToRight };
                    var caption = new TextBlock { Text = (L.Persian ? "آزاد:  " : "Free:  ") + "\u200e" + Formats.Size(drive.AvailableFreeSpace) + " / " + Formats.Size(drive.TotalSize) + "\u200e", FontSize = 10, Foreground = B("#91A6B2"), FlowDirection = L.Persian ? FlowDirection.RightToLeft : FlowDirection.LeftToRight };
                    var stack = new StackPanel(); stack.Children.Add(label); stack.Children.Add(bar); stack.Children.Add(caption);
                    var button = new Button { Content = stack, Background = B("#1E2931"), Padding = new Thickness(12), Margin = new Thickness(0, 0, 0, 9), HorizontalContentAlignment = HorizontalAlignment.Stretch, ToolTip = L.T("باز کردن / اسکن") + " " + root, IsEnabled = !busy };
                    // Default button template centers content; give drive cards a stable content width.
                    stack.Width = 140;
                    button.Click += async delegate { U<TextBox>("PathBox").Text = root; await ScanAsync(root); };
                    panel.Children.Add(button);
                }
                catch (IOException) { } catch (UnauthorizedAccessException) { }
            }
        }
        private void SetBusy(bool value, bool scanning)
        {
            busy = value;
            foreach (var name in new [] { "ScanButton", "RescanButton", "BrowseButton", "LanguageButton" }) U<Button>(name).IsEnabled = !value;
            U<TextBox>("PathBox").IsEnabled = !value;
            U<Button>("StopButton").Visibility = value && scanning ? Visibility.Visible : Visibility.Collapsed;
            U<ProgressBar>("Activity").Visibility = value ? Visibility.Visible : Visibility.Collapsed;
            foreach (var child in U<StackPanel>("DriveList").Children.OfType<Button>()) child.IsEnabled = !value;
            U<Button>("ExportButton").IsEnabled = !value && filtered.Count > 0;
            UpdateSelection();
            UpdateLabels();
        }
        public async Task RestoreLastAsync()
        {
            string path = await Task.Run(() => cache.LastPath());
            if (!String.IsNullOrEmpty(path)) { U<TextBox>("PathBox").Text = path; await ScanAsync(path, false, true); }
        }
        private void RememberLastPath(string path)
        {
            try { cache.SaveLastPath(path); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public async Task ScanAsync(string path, bool forceRescan = false, bool savedOnly = false)
        {
            if (busy) return;
            searchTimer.Stop(); if (queryCancel != null) queryCancel.Cancel(); queryVersion++;
            scanCancel = new CancellationTokenSource();
            if (snapshot != null) cache.Remember(snapshot);
            snapshot = null; current = null; stale = false; viewingCached = false; cacheSaved = false; filtered.Clear(); tiles.Clear();
            U<DataGrid>("FilesGrid").ItemsSource = null; U<TextBlock>("Breadcrumb").Text = ""; U<TextBlock>("ResultCount").Text = "";
            SetBusy(true, true); UpdateMetrics(); DrawMap();
            U<StackPanel>("EmptyState").Visibility = Visibility.Visible;
            U<TextBlock>("EmptyTitle").Text = L.T("در حال باز کردن مسیر…"); U<TextBlock>("EmptyHint").Text = "";
            SetStatus(L.T("در حال باز کردن مسیر…"));
            try
            {
                var token = scanCancel.Token;
                CachedScan saved = forceRescan ? null : await Task.Run(() => cache.Find(path, token), token);
                if (saved != null)
                {
                    if (closing) return;
                    snapshot = saved.Snapshot; current = saved.Directory; stale = snapshot.IsStale; viewingCached = true;
                    U<TextBox>("PathBox").Text = current.Path; U<TextBox>("SearchBox").Text = "";
                    SetStatus(L.T("نتیجهٔ قبلی بدون اسکن دوباره باز شد"));
                    RememberLastPath(current.Path);
                    UpdateMetrics(); BuildMap(); await RefreshRowsAsync();
                    return;
                }
                if (savedOnly)
                {
                    SetStatus(L.T("نتیجهٔ ذخیره‌شده در دسترس نیست؛ برای شروع اسکن را بزنید."));
                    U<TextBlock>("EmptyTitle").Text = L.T("برای شروع، یک درایو یا پوشه را اسکن کنید");
                    return;
                }
                SetStatus(L.T("در حال اسکن…")); U<TextBlock>("EmptyTitle").Text = L.T("در حال اسکن…");
                bool scanning = true;
                var progress = new Progress<ScanProgress>(p => {
                    if (closing || !busy || !scanning) return;
                    U<TextBlock>("BytesMetric").Text = Formats.Size(p.Bytes);
                    U<TextBlock>("FilesMetric").Text = p.Files.ToString("N0");
                    U<TextBlock>("FoldersMetric").Text = p.Folders.ToString("N0");
                    SetStatus(L.T("در حال اسکن…") + "  " + p.Path);
                });
                var scanned = await Task.Run(() => new Scanner().Scan(path, token, p => ((IProgress<ScanProgress>)progress).Report(p)));
                scanning = false;
                if (closing) return;
                snapshot = scanned; current = snapshot.Root;
                savingScan = !snapshot.Cancelled;
                U<TextBox>("PathBox").Text = current.Path;
                U<TextBox>("SearchBox").Text = "";
                SetStatus(L.T(snapshot.Cancelled ? "اسکن متوقف شد؛ نتیجه ناقص است" : "اسکن کامل شد") + "  ·  " + snapshot.Elapsed.TotalSeconds.ToString("0.0") + " " + L.T("ثانیه"));
                UpdateMetrics(); BuildMap(); await RefreshRowsAsync();
                if (!snapshot.Cancelled)
                {
                    cache.Remember(snapshot);
                    SetStatus(L.T("در حال ذخیرهٔ نتیجهٔ اسکن…"));
                    try
                    {
                        await Task.Run(() => cache.Save(scanned, token), token);
                        cacheSaved = true; RememberLastPath(current.Path); SetStatus(L.T("اسکن کامل و ذخیره شد"));
                    }
                    catch (OperationCanceledException) { SetStatus(L.T("نتیجه فقط تا بستن برنامه نگه داشته می‌شود")); }
                    catch (Exception e)
                    {
                        if (!(e is IOException) && !(e is UnauthorizedAccessException)) throw;
                        SetStatus(L.T("نتیجه فقط تا بستن برنامه نگه داشته می‌شود") + "  ·  " + L.T(e.Message));
                    }
                    UpdateMetrics();
                }
            }
            catch (OperationCanceledException) { if (!closing) SetStatus(L.T("لغو")); }
            catch (Exception e) { if (!closing) { SetStatus(L.T(e.Message)); if (!savedOnly) Error(e); U<TextBlock>("EmptyTitle").Text = L.T("برای شروع، یک درایو یا پوشه را اسکن کنید"); } }
            finally
            {
                savingScan = false;
                scanCancel.Dispose(); scanCancel = null;
                if (!closing) { SetBusy(false, false); UpdateSelection(); }
                if (closeAfterSave) Window.Close();
            }
        }
        private void UpdateMetrics()
        {
            U<TextBlock>("BytesMetric").Text = snapshot == null ? "—" : Formats.Size(snapshot.Root.Bytes);
            U<TextBlock>("FilesMetric").Text = snapshot == null ? "—" : snapshot.Root.FileCount.ToString("N0");
            U<TextBlock>("FoldersMetric").Text = snapshot == null ? "—" : snapshot.Root.FolderCount.ToString("N0");
            U<TextBlock>("IssuesMetric").Text = snapshot == null ? "—" : snapshot.ErrorCount.ToString("N0");
            U<TextBlock>("TimeMetric").Text = snapshot == null ? "—" : snapshot.Elapsed.TotalSeconds.ToString("0.0") + " " + L.T("ثانیه");
            U<TextBlock>("ScopeMetric").Text = snapshot == null ? "" : snapshot.Root.Path;
            string label = snapshot == null ? "" : L.T(snapshot.Cancelled ? "اسکن متوقف شد؛ نتیجه ناقص است" : stale ? "داده‌ها تغییر کرده‌اند؛ دوباره اسکن کنید" : viewingCached ? "نتیجهٔ ذخیره‌شده" : cacheSaved ? "اسکن کامل و ذخیره شد" : "نتیجهٔ اسکن فعلی") + "  ·  " + snapshot.StartedUtc.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture);
            U<TextBlock>("SnapshotInfo").Text = label;
            U<TextBlock>("SnapshotInfo").ToolTip = L.T("این اطلاعات مربوط به زمان اسکن است. برای دیدن تغییرات جدید «تازه‌سازی» را بزنید.");
        }
        private void SetMode(int next)
        {
            mode = next; category = null;
            for (int i = 0; i < navNames.Length; i++) { U<Button>(navNames[i]).Background = B(i == mode ? "#233D36" : "#151C22"); U<Button>(navNames[i]).Foreground = B(i == mode ? "#8CE9CE" : "#9CB0BC"); }
            U<TextBlock>("ListTitle").Text = L.T(titles[mode]);
            BuildCategoryButtons(); BuildMap(); RefreshRows();
        }
        private void Navigate(Entry entry)
        {
            if (entry == null || !entry.IsDirectory) return;
            current = entry; U<TextBox>("SearchBox").Text = ""; category = null;
            if (!snapshot.Cancelled && (cacheSaved || viewingCached)) RememberLastPath(entry.Path);
            SetMode(0);
        }
        private void BuildCategoryButtons()
        {
            var panel = U<StackPanel>("Categories"); panel.Children.Clear();
            if (mode != 3)
            {
                panel.Children.Add(new TextBlock { Text = L.T(mode == 2 ? "فایل‌های قدیمی‌تر از ۱۸۰ روز" : "تاریخ پوشه، جدیدترین تغییر خود پوشه یا محتوای آن است؛ قدیمی بودن به معنی بی‌استفاده بودن نیست."), FontSize = 10, Foreground = B("#738E9F"), TextTrimming = TextTrimming.CharacterEllipsis, MaxWidth = 850 });
                return;
            }
            foreach (var value in categories)
            {
                string key = value == categories[0] ? null : value;
                var button = new Button { Content = L.T(value), FontSize = 10, Padding = new Thickness(10, 5, 10, 5), Margin = new Thickness(0, 0, 5, 4), Background = B(category == key ? "#2F6656" : "#26333B") };
                button.Click += delegate { category = key; BuildCategoryButtons(); RefreshRows(); };
                panel.Children.Add(button);
            }
        }
        private async void RefreshRows() { await RefreshRowsAsync(); }
        public async Task RefreshRowsAsync()
        {
            if (changingLanguage || closing) return;
            int version = ++queryVersion;
            if (queryCancel != null) { queryCancel.Cancel(); queryCancel.Dispose(); }
            queryCancel = new CancellationTokenSource(); var token = queryCancel.Token;
            U<Button>("UpButton").IsEnabled = current != null && current.Parent != null;
            U<Button>("ExportButton").IsEnabled = false;
            if (snapshot == null || current == null) { U<DataGrid>("FilesGrid").ItemsSource = null; filtered.Clear(); UpdateSelection(); return; }
            var capturedSnapshot = snapshot; var capturedCurrent = current; int capturedMode = mode;
            string search = U<TextBox>("SearchBox").Text.Trim(); string capturedCategory = category;
            long min = new long[] { 0, 10L * 1024 * 1024, 100L * 1024 * 1024, 1024L * 1024 * 1024 }[Math.Max(0, U<ComboBox>("SizeFilter").SelectedIndex)];
            int days = new [] { 0, 30, 90, 180, 365 }[Math.Max(0, U<ComboBox>("AgeFilter").SelectedIndex)];
            if (capturedMode == 2) days = Math.Max(180, days);
            DateTime cutoff = days == 0 ? DateTime.MaxValue : DateTime.UtcNow.AddDays(-days);
            int order = U<ComboBox>("SortFilter").SelectedIndex;
            U<TextBlock>("Breadcrumb").Text = current.Path;
            U<TextBlock>("Breadcrumb").ToolTip = current.Path;
            U<TextBlock>("ResultCount").Text = L.T("در حال آماده‌سازی فهرست…");
            U<DataGrid>("FilesGrid").ItemsSource = null; filtered = new List<Entry>();
            try
            {
                var result = await Task.Run(() => {
                    string prefix = capturedCurrent.Path.TrimEnd('\\') + "\\";
                    IEnumerable<Entry> source = capturedMode == 0 && search.Length == 0 ? capturedCurrent.Children : capturedSnapshot.Entries.Where(e => !Object.ReferenceEquals(e, capturedCurrent) && e.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
                    source = source.Where(e => {
                        token.ThrowIfCancellationRequested();
                        return (capturedMode == 0 || !e.IsDirectory) && e.Bytes >= min && e.LatestUtc <= cutoff &&
                            (capturedCategory == null || Formats.Category(e) == capturedCategory) &&
                            (search.Length == 0 || e.Path.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0);
                    });
                    IOrderedEnumerable<Entry> ordered;
                    if (order == 1) ordered = source.OrderByDescending(e => e.LatestUtc);
                    else if (order == 2) ordered = source.OrderBy(e => e.LatestUtc);
                    else if (order == 3) ordered = source.OrderBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase);
                    else ordered = source.OrderByDescending(e => e.Bytes);
                    return ordered.ThenBy(e => e.Path, StringComparer.OrdinalIgnoreCase).ToList();
                }, token);
                if (version != queryVersion || closing) return;
                filtered = result;
                var rows = result.Take(2000).Select((e, i) => MakeRow(e, i)).ToList();
                U<DataGrid>("FilesGrid").ItemsSource = rows;
                U<StackPanel>("EmptyState").Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
                U<TextBlock>("EmptyTitle").Text = L.T("موردی با این فیلترها پیدا نشد"); U<TextBlock>("EmptyHint").Text = "";
                U<TextBlock>("ResultCount").Text = L.T("نمایش") + " " + rows.Count.ToString("N0") + " / " + result.Count.ToString("N0") + " " + L.T("نتایج") + (result.Count > 2000 ? "  ·  " + L.T("خروجی CSV") + ": " + result.Count.ToString("N0") : "");
                U<Button>("ExportButton").IsEnabled = !busy && result.Count > 0;
                UpdateSelection();
            }
            catch (OperationCanceledException) { }
            catch (Exception e) { if (version == queryVersion) Error(e); }
        }
        private Row MakeRow(Entry entry, int i)
        {
            string reason = Safety.BlockReason(entry, snapshot);
            return new Row { Entry = entry, Percent = current.Bytes == 0 ? 0 : 100.0 * entry.Bytes / current.Bytes,
                Color = B(entry.IsDirectory ? "#7ED6B8" : palette[i % palette.Length]),
                State = L.T(entry.Issues > 0 ? "ناقص" : reason != null ? "محافظت‌شده" : "قابل بررسی"),
                Detail = entry.Path + "\n" + L.T("زمان ایجاد") + ": " + Formats.Date(entry.CreatedUtc) + "   ·   " + L.T("زمان تغییر") + ": " + Formats.Date(entry.ModifiedUtc) + "\n" + L.T(reason ?? entry.Note) };
        }
        private List<Row> Selected() { return U<DataGrid>("FilesGrid").SelectedItems.Cast<Row>().ToList(); }
        private void UpdateSelection()
        {
            var selected = Safety.CollapseSelection(Selected().Select(r => r.Entry));
            U<Button>("RevealButton").IsEnabled = selected.Count == 1;
            string reason = snapshot == null ? "ابتدا اسکن کامل انجام دهید" : selected.Select(e => Safety.BlockReason(e, snapshot)).FirstOrDefault(x => x != null);
            U<Button>("RecycleButton").IsEnabled = !busy && !stale && selected.Count > 0 && reason == null;
            U<Button>("RecycleButton").ToolTip = stale ? L.T("داده‌ها تغییر کرده‌اند؛ دوباره اسکن کنید") : L.T(reason);
            U<TextBlock>("SelectionInfo").Text = selected.Count == 0 ? L.T("انتخابی ندارید") : selected.Count.ToString("N0") + " " + L.T("انتخاب‌شده") + "  ·  " + Formats.Size(selected.Sum(x => x.Bytes)) + (reason == null ? "" : "  ·  " + L.T(reason));
        }
        private void BuildMap()
        {
            tiles.Clear();
            U<TextBlock>("MapTitle").Text = L.T(mode == 3 ? "نوع فایل‌ها" : "نقشهٔ فضا");
            U<TextBlock>("MapHint").Text = L.T(mode == 3 ? "گروه را برای دیدن فایل‌هایش انتخاب کنید" : "برای ورود، روی پوشه کلیک کنید");
            if (snapshot != null && current != null)
            {
                if (mode == 3)
                {
                    if (current.TypeBytes != null) tiles = Enumerable.Range(0, 7).Select(i => new Tile { Label = L.T(Formats.CategoryNames[i]), Category = Formats.CategoryNames[i], Bytes = current.TypeBytes[i] }).Where(t => t.Bytes > 0).OrderByDescending(t => t.Bytes).ToList();
                }
                else
                {
                    var children = current.Children.Where(e => e.Bytes > 0).OrderByDescending(e => e.Bytes).ToList();
                    tiles = children.Take(12).Select(e => new Tile { Label = e.Name, Entry = e, Bytes = e.Bytes }).ToList();
                    if (children.Count > 12) tiles.Add(new Tile { Label = L.T("سایر"), Bytes = children.Skip(12).Sum(e => e.Bytes) });
                }
                for (int i = 0; i < tiles.Count; i++) tiles[i].Color = i % palette.Length;
            }
            DrawMap();
        }
        private void DrawMap()
        {
            var canvas = U<Canvas>("SpaceMap"); canvas.Children.Clear();
            if (tiles.Count == 0)
            {
                var empty = new TextBlock { Text = snapshot == null ? L.T("برای شروع، یک درایو یا پوشه را اسکن کنید") : "0 B", Foreground = B("#6F8B9A"), FontSize = 12 };
                Canvas.SetLeft(empty, 18); Canvas.SetTop(empty, 48); canvas.Children.Add(empty); return;
            }
            DrawTiles(tiles, 0, 0, canvas.ActualWidth, canvas.ActualHeight);
        }
        private void DrawTiles(List<Tile> list, double x, double y, double width, double height)
        {
            if (width < 1 || height < 1 || list.Count == 0) return;
            if (list.Count == 1)
            {
                Tile tile = list[0];
                var text = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(11, 4, 11, 4) };
                if (width > 48 && height > 25) text.Children.Add(new TextBlock { Text = tile.Label, TextTrimming = TextTrimming.CharacterEllipsis, Foreground = Brushes.White, FontWeight = FontWeights.SemiBold, FontSize = width > 150 && height > 65 ? 14 : 11 });
                if (width > 65 && height > 47) text.Children.Add(new TextBlock { Text = Formats.Size(tile.Bytes), Foreground = B("#D3E8E2"), FontSize = 11, Margin = new Thickness(0, 5, 0, 0) });
                var border = new Border { Width = Math.Max(0, width - 4), Height = Math.Max(0, height - 4), Background = B(palette[tile.Color]), CornerRadius = new CornerRadius(5), Child = text, ClipToBounds = true, Cursor = Cursors.Hand, ToolTip = tile.Label + "\n" + Formats.Size(tile.Bytes) + (tile.Entry == null ? "" : "\n" + tile.Entry.Path) };
                border.MouseLeftButtonUp += delegate { if (tile.Category != null) { category = tile.Category; BuildCategoryButtons(); RefreshRows(); } else if (tile.Entry != null && tile.Entry.IsDirectory) Navigate(tile.Entry); else if (tile.Entry != null) { var rows = U<DataGrid>("FilesGrid").ItemsSource as List<Row>; var row = rows == null ? null : rows.FirstOrDefault(r => r.Entry == tile.Entry); if (row != null) { U<DataGrid>("FilesGrid").SelectedItem = row; U<DataGrid>("FilesGrid").ScrollIntoView(row); } } };
                border.MouseEnter += delegate { border.Opacity = 0.82; }; border.MouseLeave += delegate { border.Opacity = 1; };
                Canvas.SetLeft(border, x + 2); Canvas.SetTop(border, y + 2); U<Canvas>("SpaceMap").Children.Add(border);
                return;
            }
            double total = list.Sum(t => (double)t.Bytes); double accumulated = 0; int split = 1;
            for (int i = 0; i < list.Count - 1; i++) { accumulated += list[i].Bytes; split = i + 1; if (accumulated >= total / 2) break; }
            double fraction = accumulated / total;
            if (width >= height) { DrawTiles(list.Take(split).ToList(), x, y, width * fraction, height); DrawTiles(list.Skip(split).ToList(), x + width * fraction, y, width * (1 - fraction), height); }
            else { DrawTiles(list.Take(split).ToList(), x, y, width, height * fraction); DrawTiles(list.Skip(split).ToList(), x, y + height * fraction, width, height * (1 - fraction)); }
        }
        private void Reveal()
        {
            var row = Selected().FirstOrDefault(); if (row == null) return;
            OpenShell("explorer.exe", "/select,\"" + row.Entry.Path + "\"");
        }
        private void OpenShell(string path, string arguments)
        {
            try { Process.Start(new ProcessStartInfo(path) { Arguments = arguments ?? "", UseShellExecute = true }); }
            catch (Exception e) { Error(e); }
        }
        private async Task RecycleSelected()
        {
            if (busy || stale || snapshot == null) return;
            var entries = Safety.CollapseSelection(Selected().Select(r => r.Entry)); if (entries.Count == 0) return;
            foreach (var entry in entries) { var reason = Safety.BlockReason(entry, snapshot); if (reason != null) { Error(new InvalidOperationException(reason)); return; } }
            string body = L.T("موارد زیر به سطل بازیافت منتقل می‌شوند:") + "\n\n" + String.Join("\n\n", entries.Select(e => e.Path + "\n" + Formats.Size(e.Bytes))) + "\n\n" + L.T("فضا پس از خالی کردن سطل بازیافت در ویندوز آزاد می‌شود. از همان‌جا می‌توانید فایل‌ها را بازیابی کنید.");
            if (!ShowDialogText("مرور انتخاب‌ها", body, true)) return;
            SetBusy(true, false); SetStatus(L.T("در حال بررسی تغییرات و انتقال…"));
            try
            {
                // Invalidate before any write, including partial failure or cancellation.
                // The approved batch keeps its own validity flag while cached views are invalidated.
                var approved = new Snapshot { Root = snapshot.Root, Entries = snapshot.Entries, Cancelled = snapshot.Cancelled, IsStale = snapshot.IsStale };
                stale = true; snapshot.IsStale = true;
                await Task.Run(() => cache.MarkChanged(entries.Select(e => e.Path)));
                UpdateMetrics();
                var report = await Recycler.RecycleAsync(entries, approved, new WindowInteropHelper(Window).Handle, CancellationToken.None);
                ShowDialogText("انجام شد", String.Join("\n\n", report) + "\n\n" + L.T("انتقال تمام شد. برای به‌روزرسانی نتیجه دوباره اسکن کنید."), false);
                SetStatus(L.T("داده‌ها تغییر کرده‌اند؛ دوباره اسکن کنید")); RefreshDrives();
            }
            catch (Exception e) { Error(e); }
            finally { SetBusy(false, false); UpdateSelection(); }
        }
        private async Task ExportAsync()
        {
            if (busy || filtered.Count == 0) return;
            var dialog = new SaveFileDialog { Title = L.T("ذخیرهٔ گزارش"), Filter = "CSV (*.csv)|*.csv", FileName = "Spacio-" + DateTime.Now.ToString("yyyy-MM-dd-HHmm") + ".csv", OverwritePrompt = true };
            if (dialog.ShowDialog(Window) != true) return;
            var entries = filtered.ToList(); SetBusy(true, false); SetStatus(L.T("در حال ذخیرهٔ گزارش…"));
            try { await Task.Run(() => Formats.Export(dialog.FileName, entries, CancellationToken.None)); SetStatus(L.T("گزارش ذخیره شد") + "  " + dialog.FileName); }
            catch (Exception e) { Error(e); }
            finally { SetBusy(false, false); }
        }
        private void ShowIssues()
        {
            string text = snapshot == null || snapshot.Errors.Count == 0 ? L.T("هیچ مورد بررسی‌نشده‌ای وجود ندارد.") : String.Join("\n\n", snapshot.Errors.Select(L.T)) + "\n\n" + L.T("حداکثر ۵۰۰ پیام اول نمایش داده می‌شود.");
            ShowDialogText("گزارش دسترسی و موارد ردشده", text, false);
        }
        private bool ShowDialogText(string title, string text, bool confirm)
        {
            var dialog = new Window { Owner = Window, Title = L.T(title), Width = 680, Height = confirm ? 480 : 520, MinHeight = 300, MinWidth = 480, Background = B("#182128"), Foreground = B("#E2EDF1"), FontFamily = Window.FontFamily, WindowStartupLocation = WindowStartupLocation.CenterOwner, FlowDirection = Window.FlowDirection };
            var layout = new Grid { Margin = new Thickness(22) }; layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); layout.RowDefinitions.Add(new RowDefinition()); layout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            var heading = new TextBlock { Text = L.T(title), FontSize = 21, Margin = new Thickness(0, 0, 0, 17) }; layout.Children.Add(heading);
            var body = new TextBox { Text = text, IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Background = B("#111A20"), Foreground = B("#C5D6DF"), BorderThickness = new Thickness(0), Padding = new Thickness(14), FontSize = 13, FlowDirection = FlowDirection.LeftToRight }; Grid.SetRow(body, 1); layout.Children.Add(body);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 17, 0, 0) }; Grid.SetRow(buttons, 2); layout.Children.Add(buttons);
            var cancel = new Button { Content = L.T(confirm ? "لغو" : "بستن"), IsCancel = true, Padding = new Thickness(22, 10, 22, 10), Margin = new Thickness(0, 0, 10, 0), Style = (Style)Window.FindResource(typeof(Button)) }; cancel.Click += delegate { dialog.DialogResult = false; }; buttons.Children.Add(cancel);
            if (confirm) { var ok = new Button { Content = L.T("تأیید انتقال"), Background = B("#884E49"), Foreground = Brushes.White, Padding = new Thickness(22, 10, 22, 10), Style = (Style)Window.FindResource(typeof(Button)) }; ok.Click += delegate { dialog.DialogResult = true; }; buttons.Children.Add(ok); }
            dialog.Content = layout; return dialog.ShowDialog() == true;
        }
        private void Error(Exception e) { ShowDialogText("خطا", L.T(e.Message), false); }
        private void SetStatus(string text) { U<TextBlock>("StatusText").Text = text; U<TextBlock>("StatusText").ToolTip = text; }

        // Used by the off-screen UI checks with a generated fixture, never with a user's drive.
        public async Task LoadForTest(Snapshot data)
        {
            snapshot = data; current = data.Root; U<TextBox>("PathBox").Text = data.Root.Path; UpdateMetrics(); BuildMap(); await RefreshRowsAsync(); SetStatus(L.T("اسکن کامل شد"));
        }
        public void SavePreview(string path)
        {
            Window.UpdateLayout(); DrawMap(); Window.UpdateLayout();
            var content = (FrameworkElement)Window.Content;
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(Window);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
        }
        public async Task CheckUI()
        {
            foreach (var selectedMode in new [] { 0, 1, 2, 3 }) { SetMode(selectedMode); await RefreshRowsAsync(); if (mode != selectedMode) throw new Exception("Navigation failed"); }
            SetMode(0); await RefreshRowsAsync();
            var folder = current.Children.FirstOrDefault(e => e.IsDirectory && e.Issues == 0);
            if (folder != null) { Navigate(folder); await RefreshRowsAsync(); if (current != folder) throw new Exception("Drill-down failed"); Navigate(snapshot.Root); await RefreshRowsAsync(); }
            U<TextBox>("SearchBox").Text = "a-query-that-cannot-match-983472"; await RefreshRowsAsync(); if (filtered.Count != 0) throw new Exception("Search failed");
            U<TextBox>("SearchBox").Text = ""; await RefreshRowsAsync();
            U<ComboBox>("SizeFilter").SelectedIndex = 3; await RefreshRowsAsync(); if (filtered.Any(e => e.Bytes < 1024L * 1024 * 1024)) throw new Exception("Size filter failed");
            U<ComboBox>("SizeFilter").SelectedIndex = 0; await RefreshRowsAsync();
            U<ComboBox>("SortFilter").SelectedIndex = 2; await RefreshRowsAsync(); if (!filtered.Select(e => e.LatestUtc).SequenceEqual(filtered.Select(e => e.LatestUtc).OrderBy(x => x))) throw new Exception("Sort failed");
            U<ComboBox>("SortFilter").SelectedIndex = 0; await RefreshRowsAsync();
        }
        public async Task CheckCacheUI()
        {
            var completed = snapshot;
            await ScanAsync(completed.Root.Path);
            if (!viewingCached || !Object.ReferenceEquals(completed, snapshot)) throw new Exception("Repeated path rescanned instead of reopening.");
            var child = completed.Root.Children.FirstOrDefault(e => e.IsDirectory && e.Issues == 0);
            if (child != null)
            {
                await ScanAsync(child.Path);
                if (!viewingCached || current != child || snapshot != completed) throw new Exception("Cached descendant was rescanned.");
            }
            await ScanAsync(completed.Root.Path, true);
            if (viewingCached || Object.ReferenceEquals(completed, snapshot) || !cacheSaved) throw new Exception("Explicit rescan did not replace and save the snapshot.");
            await ScanAsync(completed.Root.Path);
            string deleted = Path.Combine(completed.Root.Path, "spacio-refresh-delete.tmp"), moved = Path.Combine(completed.Root.Path, "spacio-refresh-move.tmp"), destination = moved + ".moved", added = deleted + ".added";
            File.WriteAllText(deleted, "delete fixture"); File.WriteAllText(moved, "move fixture");
            await ScanAsync(completed.Root.Path, true);
            File.Delete(deleted); File.Move(moved, destination); File.WriteAllText(added, "new file");
            await ScanAsync(completed.Root.Path);
            if (!snapshot.Entries.Any(e => Paths.Equal(e.Path, deleted)) || snapshot.Entries.Any(e => Paths.Equal(e.Path, added))) throw new Exception("Saved result unexpectedly rescanned external changes.");
            U<Button>("RescanButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            while (busy) await Task.Delay(20);
            if (snapshot.Entries.Any(e => Paths.Equal(e.Path, deleted) || Paths.Equal(e.Path, moved)) || !snapshot.Entries.Any(e => Paths.Equal(e.Path, destination)) || !snapshot.Entries.Any(e => Paths.Equal(e.Path, added)) || !cacheSaved) throw new Exception("Refresh button did not persist deletions, moves and additions.");
            File.WriteAllText(Path.Combine(completed.Root.Path, "spacio-after-close-marker.tmp"), "Must not appear on automatic restoration");
        }
        public void AssertRestoredCache()
        {
            if (!viewingCached || snapshot == null || snapshot.Cancelled || U<TextBlock>("SnapshotInfo").Text.Length == 0) throw new Exception("Saved scan was not restored after restarting the app.");
            if (snapshot.Entries.Any(e => e.Name == "spacio-after-close-marker.tmp")) throw new Exception("Startup rescanned the drive instead of loading the saved result.");
        }
    }

    internal sealed class Win32Owner : System.Windows.Forms.IWin32Window { public IntPtr Handle { get; private set; } public Win32Owner(IntPtr handle) { Handle = handle; } }

    public static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 1 && args[0] == "--apply-update") return UpdateInstaller.Run(args[1]);
            if (args.Length > 1 && args[0] == "--update-test") return UpdateTests.Run(args[1]);
            if (args.Length > 1 && args[0] == "--update-live-test") return UpdateTests.Live(args[1]);
            if (args.Length > 1 && args[0] == "--update-test-probe") { File.WriteAllText(args[1], Updates.VersionText); return 0; }
            if (args.Length > 1 && args[0] == "--update-test-host")
            {
                File.WriteAllText(args[1] + ".ready", "ready"); var watch = Stopwatch.StartNew();
                while (!File.Exists(args[1] + ".exit") && watch.ElapsedMilliseconds < 30000) Thread.Sleep(50);
                return 0;
            }
            if (args.Length > 0 && args[0] == "--self-test") return SelfTests.Run(args.Length > 1 ? args[1] : System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test-results.txt"));
            if (args.Length > 1 && args[0] == "--cache-test") return CacheTests.Run(args[1]);
            bool startupTest = args.Length >= 3 && args[0] == "--startup-smoke";
            bool preview = startupTest || (args.Length >= 3 && args[0] == "--ui-smoke");
            if (!preview) L.Load();
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            app.DispatcherUnhandledException += (s, e) => {
                if (preview) { File.WriteAllText(args[2] + ".error.txt", e.Exception.ToString()); app.Shutdown(1); }
                else MessageBox.Show(L.T(e.Exception.Message), "Spacio", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            try
            {
                if (startupTest) return Startup.Run(app, args[1], args[2]);
                if (!preview) return Startup.Run(app);
                var controller = new Controller(preview ? args[2] + "-cache" : null); app.MainWindow = controller.Window;
                if (preview)
                {
                    controller.Window.ShowActivated = false; controller.Window.ShowInTaskbar = false; controller.Window.WindowStartupLocation = WindowStartupLocation.Manual; controller.Window.Left = -20000; controller.Window.Top = -20000;
                    controller.Window.Loaded += async delegate {
                        try
                        {
                            bool reopen = args.Length > 3 && args[3] == "--reopen";
                            if (reopen) await controller.RestoreLastAsync();
                            else await controller.ScanAsync(args[1], true);
                            if (reopen) controller.AssertRestoredCache();
                            else { await controller.CheckUI(); await controller.CheckCacheUI(); }
                            L.Persian = false; controller.ApplyLanguage(); await controller.RefreshRowsAsync();
                            await Task.Delay(350); controller.SavePreview(args[2] + "-en.png");
                            L.Persian = true; controller.ApplyLanguage(); await controller.RefreshRowsAsync();
                            await Task.Delay(350); controller.SavePreview(args[2] + "-fa.png");
                            controller.ShowUpdatePreview(); await Task.Delay(100); controller.SavePreview(args[2] + "-update-fa.png");
                            L.Persian = false; controller.ApplyLanguage(); await Task.Delay(100); controller.SavePreview(args[2] + "-update-en.png");
                            var splash = Startup.Create(); splash.ShowInTaskbar = false; splash.ShowActivated = false; splash.WindowStartupLocation = WindowStartupLocation.Manual; splash.Left = -20000; splash.Top = -20000; splash.Show();
                            await Task.Delay(150); Startup.SaveImage(splash, args[2] + "-startup-en.png");
                            L.Persian = true; Startup.Localize(splash); Startup.SaveImage(splash, args[2] + "-startup-fa.png"); splash.Close();
                            File.WriteAllText(args[2] + (reopen ? ".reopen.ok.txt" : ".ok.txt"), reopen ? "PASS: automatic restoration in a new process without rescanning external changes; EN/FA rendering" : "PASS: render EN/FA, navigation, search, filters, cached path/descendant reuse, Refresh detects and saves deleted/moved/added files, update banner and splash rendering");
                            controller.Window.Close();
                        }
                        catch (Exception e) { File.WriteAllText(args[2] + ".error.txt", e.ToString()); app.Shutdown(1); }
                    };
                }
                return app.Run(controller.Window);
            }
            catch (Exception e)
            {
                if (preview) File.WriteAllText(args[2] + ".error.txt", e.ToString());
                else MessageBox.Show(e.ToString(), "Spacio", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }
}
