using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Spacio
{
    public static class Startup
    {
        public static Window Create()
        {
            Window window;
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spacio.StartupWindow.xaml")) window = (Window)XamlReader.Load(stream);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spacio.BrandMark.xaml")) ((Image)window.FindName("SplashLogo")).Source = (DrawingImage)XamlReader.Load(stream);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spacio.ico")) window.Icon = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            ((Button)window.FindName("CloseSplash")).Click += delegate { window.Close(); };
            window.MouseLeftButtonDown += delegate { window.DragMove(); };
            Localize(window); LoadingMotion.Attach(window); return window;
        }
        public static void Localize(Window window)
        {
            ((TextBlock)window.FindName("SplashTagline")).Text = L.T("فضای بیشتر، خیال آسوده‌تر.");
            ((TextBlock)window.FindName("SplashStatus")).Text = L.T("در حال بازگرداندن فضای کار شما…");
            ((TextBlock)window.FindName("SplashFootnote")).Text = L.T("اسکن‌های شما ماندگارند");
            ((TextBlock)window.FindName("SplashVersion")).Text = "v" + Updates.VersionText;
        }
        public static int Run(Application app, string cacheDirectory = null, string previewPrefix = null)
        {
            app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            bool cancelled = false;
            var splash = new LoadingHost(previewPrefix != null, () => app.Dispatcher.BeginInvoke(new Action(() => { cancelled = true; app.Shutdown(); })));
            app.Exit += delegate { splash.Close(); };
            app.Startup += async delegate {
                try
                {
                    await splash.Shown;
                    var visibleTime = Stopwatch.StartNew();
                    if (cancelled) return;
                    splash.SetStatus(L.T("در حال آماده‌سازی برنامه…"));
                    var controller = new Controller(cacheDirectory);
                    splash.SetStatus(L.T("در حال بازگرداندن فضای کار شما…"));
                    await controller.RestoreLastAsync();
                    if (cancelled) return;
                    splash.SetStatus(L.T("فضای کار شما آماده است"));
                    // A brief intro makes the live motion visible even on a fast cached start.
                    int remaining = 1500 - (int)visibleTime.ElapsedMilliseconds;
                    if (remaining > 0) await Task.Delay(remaining);
                    if (cancelled) return;
                    app.MainWindow = controller.Window; app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    if (previewPrefix != null) HideForPreview(controller.Window);
                    await splash.FadeOutAsync();
                    if (cancelled) return;
                    controller.Window.Show();
                    if (previewPrefix != null)
                    {
                        controller.AssertRestoredCache();
                        await Task.Delay(100); controller.SavePreview(previewPrefix + "-restored.png");
                        File.WriteAllText(previewPrefix + ".startup.ok.txt", "PASS: actual loading-screen startup restores the last saved scan before showing the main window");
                        controller.Window.Close();
                    }
                    else { controller.StartLiveUpdates(); controller.StartUpdateChecks(); }
                }
                catch (Exception e) { if (previewPrefix != null) File.WriteAllText(previewPrefix + ".error.txt", e.ToString()); else MessageBox.Show(L.T(e.Message), "Spacio", MessageBoxButton.OK, MessageBoxImage.Error); splash.Close(); app.Shutdown(1); }
            };
            return app.Run();
        }
        internal static void HideForPreview(Window window)
        {
            window.ShowInTaskbar = false; window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -20000; window.Top = -20000;
        }
        public static void SaveImage(Window window, string path)
        {
            SaveBitmap(Capture(window), path);
        }
        internal static RenderTargetBitmap Capture(Window window)
        {
            window.UpdateLayout(); var content = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            return bitmap;
        }
        internal static void SaveBitmap(RenderTargetBitmap bitmap, string path)
        {
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
        }
        public static int CheckMotion(string prefix)
        {
            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var splash = new LoadingHost(true, () => { });
            app.Startup += async delegate {
                try
                {
                    await splash.Shown; var recording = splash.RecordAsync(prefix);
                    // The splash must keep animating while the main UI dispatcher is busy.
                    Thread.Sleep(1800);
                    if (splash.CapturedFrames < 3) throw new Exception("Loading animation stalled with the main UI thread.");
                    await recording; await splash.FadeOutAsync();
                    File.AppendAllText(prefix + ".motion.txt", "\nPASS: animation advanced while the main UI thread was blocked, and its dispatcher stopped after closing.");
                    app.Shutdown();
                }
                catch (Exception e) { File.WriteAllText(prefix + ".motion.error.txt", e.ToString()); splash.Close(); app.Shutdown(1); }
            };
            return app.Run();
        }
    }
}
