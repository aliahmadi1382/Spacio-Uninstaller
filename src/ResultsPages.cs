using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Spacio
{
    public sealed partial class Controller
    {
        private bool resultsPageOpen;

        private void SetResultsPage(bool open)
        {
            bool changed = resultsPageOpen != open;
            resultsPageOpen = open;
            var rows = U<Grid>("Workspace").RowDefinitions;
            rows[0].Height = new GridLength(open ? 68 : 76);
            rows[2].Height = new GridLength(open ? 0 : 121);
            rows[3].Height = new GridLength(open ? 0 : 170);
            U<FrameworkElement>("OverviewMetrics").Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            U<FrameworkElement>("OverviewMap").Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            U<Button>("OpenResultsPageButton").Visibility = open ? Visibility.Collapsed : Visibility.Visible;
            U<Button>("BackOverviewButton").Visibility = open ? Visibility.Visible : Visibility.Collapsed;
            U<TextBlock>("PageTitle").Text = L.T(open ? titles[mode] : "فضای دیسک، زیر ذره‌بین.");
            U<TextBlock>("PageDescription").Text = L.T(open ? "مرور کامل محتوا، جست‌وجو و فیلتر در یک صفحه" : "یک نمای روشن از فایل‌ها، پوشه‌ها و فضای مصرف‌شده");
            U<Border>("ReadOnlyNote").ToolTip = L.T("هیچ فایلی هنگام اسکن تغییر نمی‌کند. پاک‌سازی فقط با انتخاب و تأیید شما انجام می‌شود.");
            for (int i = 0; i < navNames.Length; i++) SetNavActive(navNames[i], i == mode && (i != 0 || !open));
            SetNavActive("FilesNav", open && mode == 0);
            // The same virtualized grid remains mounted: expanding does not reset selection,
            // scrolling, search, filters or the active snapshot, and never initiates a scan.
            if (changed) BuildMap();
        }
        private void SetNavActive(string name, bool active)
        {
            U<Button>(name).Background = B(active ? "#233D36" : "#151C22");
            U<Button>(name).Foreground = B(active ? "#8CE9CE" : "#9CB0BC");
        }

        // Check actual layout and user actions at the minimum supported window size.
        public async Task CheckResultsPages(string prefix)
        {
            double width = Window.Width, height = Window.Height;
            bool persian = L.Persian;
            var original = snapshot;
            Window.Width = Window.MinWidth; Window.Height = Window.MinHeight;
            SetMode(0, false); await RefreshRowsAsync(); Window.UpdateLayout();
            double compactHeight = U<DataGrid>("FilesGrid").ActualHeight;
            U<TextBox>("SearchBox").Text = "spacio-refresh";
            await RefreshRowsAsync();
            var source = U<DataGrid>("FilesGrid").ItemsSource;
            var selection = (source as System.Collections.Generic.List<Row>).FirstOrDefault();
            U<DataGrid>("FilesGrid").SelectedItem = selection;
            U<Button>("OpenResultsPageButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Window.UpdateLayout();
            double pageHeight = U<DataGrid>("FilesGrid").ActualHeight;
            if (pageHeight < compactHeight + 280 || pageHeight < 300) throw new Exception("Results page did not reclaim dashboard space: " + compactHeight + " -> " + pageHeight);
            if (snapshot != original || !Object.ReferenceEquals(source, U<DataGrid>("FilesGrid").ItemsSource) || U<DataGrid>("FilesGrid").SelectedItem != selection || U<TextBox>("SearchBox").Text != "spacio-refresh") throw new Exception("Expanding results reset the snapshot, table or filters.");
            U<TextBox>("SearchBox").Text = "";
            await RefreshRowsAsync();
            foreach (bool language in new [] { false, true })
            {
                L.Persian = language; ApplyLanguage();
                foreach (string nav in new [] { "FilesNav", "LargestNav", "OldNav", "TypesNav" })
                {
                    U<Button>(nav).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    await RefreshRowsAsync(); Window.UpdateLayout();
                    if (!resultsPageOpen || U<DataGrid>("FilesGrid").ActualHeight < 300 || U<FrameworkElement>("OverviewMap").IsVisible) throw new Exception("Section did not open a full results page: " + nav);
                    if (U<ScrollViewer>("DriveScroller").ActualHeight < 100) throw new Exception("Sidebar drives are clipped at the minimum window size.");
                }
                category = "سند"; BuildCategoryButtons(); ApplyLanguage();
                if (category != "سند" || !resultsPageOpen) throw new Exception("Language change reset the full page/category.");
                U<Button>("FilesNav").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                await RefreshRowsAsync(); await Task.Delay(100);
                SavePreview(prefix + "-files-page-" + (language ? "fa" : "en") + ".png");
            }
            U<Button>("BackOverviewButton").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await RefreshRowsAsync(); Window.UpdateLayout();
            if (resultsPageOpen || !U<FrameworkElement>("OverviewMap").IsVisible || !U<FrameworkElement>("OverviewMetrics").IsVisible || snapshot != original) throw new Exception("Returning to overview did not restore the dashboard.");
            System.IO.File.WriteAllText(prefix + ".pages.ok.txt", "PASS: four full result pages in EN/FA; minimum-size grid height " + compactHeight + " -> " + pageHeight + "; expanding retains table, selection and search; language retains page/category; return restores dashboard without scanning.");
            Window.Width = width; Window.Height = height; L.Persian = persian; ApplyLanguage(); await RefreshRowsAsync();
        }
    }
}
