using System;
using System.IO;
using System.Reflection;
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
            Localize(window); return window;
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
            var splash = Create(); bool ready = false, cancelled = false;
            if (previewPrefix != null) HideForPreview(splash);
            splash.Closed += delegate { if (!ready) { cancelled = true; app.Shutdown(); } };
            splash.Loaded += async delegate {
                try
                {
                    // Give the loading screen its first rendered frame before building the main UI.
                    await Task.Delay(80);
                    if (cancelled) return;
                    var controller = new Controller(cacheDirectory);
                    await controller.RestoreLastAsync();
                    if (cancelled) return;
                    ready = true; app.MainWindow = controller.Window; app.ShutdownMode = ShutdownMode.OnMainWindowClose;
                    if (previewPrefix != null) HideForPreview(controller.Window);
                    controller.Window.Show(); splash.Close();
                    if (previewPrefix != null)
                    {
                        controller.AssertRestoredCache();
                        await Task.Delay(100); controller.SavePreview(previewPrefix + "-restored.png");
                        File.WriteAllText(previewPrefix + ".startup.ok.txt", "PASS: actual loading-screen startup restores the last saved scan before showing the main window");
                        controller.Window.Close();
                    }
                    else controller.StartUpdateChecks();
                }
                catch (Exception e) { if (previewPrefix != null) File.WriteAllText(previewPrefix + ".error.txt", e.ToString()); else MessageBox.Show(L.T(e.Message), "Spacio", MessageBoxButton.OK, MessageBoxImage.Error); ready = true; app.Shutdown(1); }
            };
            return app.Run(splash);
        }
        private static void HideForPreview(Window window)
        {
            window.ShowInTaskbar = false; window.ShowActivated = false; window.WindowStartupLocation = WindowStartupLocation.Manual; window.Left = -20000; window.Top = -20000;
        }
        public static void SaveImage(Window window, string path)
        {
            window.UpdateLayout(); var content = (FrameworkElement)window.Content;
            var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32); bitmap.Render(content);
            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using (var stream = File.Create(path)) encoder.Save(stream);
        }
    }
}
