using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Spacio
{
    // A separate UI dispatcher keeps the scene moving during main-window construction.
    internal sealed class LoadingHost
    {
        private Window window;
        private int finishing, frames;
        private readonly TaskCompletionSource<bool> shown = new TaskCompletionSource<bool>();
        private readonly TaskCompletionSource<bool> stopped = new TaskCompletionSource<bool>();
        public Task Shown { get { return shown.Task; } }
        public Task Stopped { get { return stopped.Task; } }
        public int CapturedFrames { get { return Volatile.Read(ref frames); } }
        public LoadingHost(bool hidden, Action closedByUser)
        {
            var thread = new Thread(() => {
                try
                {
                    window = Startup.Create();
                    if (hidden) Startup.HideForPreview(window);
                    window.Loaded += delegate { shown.TrySetResult(true); };
                    window.Closed += delegate {
                        if (Interlocked.CompareExchange(ref finishing, 0, 0) == 0) closedByUser();
                        window.Dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                    };
                    window.Show(); Dispatcher.Run();
                }
                catch (Exception e) { if (!shown.TrySetException(e)) closedByUser(); }
                finally { stopped.TrySetResult(true); }
            }) { IsBackground = true, Name = "Spacio loading animation" };
            thread.SetApartmentState(ApartmentState.STA); thread.Start();
        }
        public void SetStatus(string text)
        {
            if (window == null || window.Dispatcher.HasShutdownStarted) return;
            window.Dispatcher.BeginInvoke(new Action(() => ((TextBlock)window.FindName("SplashStatus")).Text = text));
        }
        public void Close()
        {
            Interlocked.Exchange(ref finishing, 1);
            if (window != null && !window.Dispatcher.HasShutdownStarted) window.Dispatcher.BeginInvoke(new Action(() => window.Close()));
        }
        public async Task FadeOutAsync()
        {
            Interlocked.Exchange(ref finishing, 1);
            if (window != null && !window.Dispatcher.HasShutdownStarted)
                await window.Dispatcher.InvokeAsync(new Action(() => {
                    var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                    fade.Completed += delegate { window.Close(); }; window.BeginAnimation(UIElement.OpacityProperty, fade);
                }));
            await stopped.Task;
        }
        public Task RecordAsync(string prefix)
        {
            var completed = new TaskCompletionSource<bool>();
            window.Dispatcher.BeginInvoke(new Action(async () => {
                try
                {
                    string folder = prefix + "-frames"; Directory.CreateDirectory(folder);
                    byte[] first = null; int changed = 0;
                    for (int i = 0; i < 48; i++)
                    {
                        var bitmap = Startup.Capture(window);
                        // Check the actual central scene, independently of the progress bar.
                        var scene = new Int32Rect(230, 70, 220, 240); var pixels = new byte[scene.Width * scene.Height * 4];
                        bitmap.CopyPixels(scene, pixels, scene.Width * 4, 0);
                        if (first == null) first = pixels;
                        if (i == 20) for (int p = 0; p < pixels.Length; p += 4)
                            if (Math.Abs(pixels[p] - first[p]) + Math.Abs(pixels[p + 1] - first[p + 1]) + Math.Abs(pixels[p + 2] - first[p + 2]) > 12) changed++;
                        Startup.SaveBitmap(bitmap, Path.Combine(folder, "frame-" + i.ToString("D3") + ".png"));
                        Interlocked.Increment(ref frames); await Task.Delay(65);
                    }
                    if (changed < 150) throw new Exception("The central loading scene remained static.");
                    File.WriteAllText(prefix + ".motion.txt", "PASS: " + changed + " central-scene pixels changed; 48 animated frames captured.");
                    completed.SetResult(true);
                }
                catch (Exception e) { completed.SetException(e); }
            }));
            return completed.Task;
        }
    }
}
