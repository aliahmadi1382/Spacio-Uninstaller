using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace Spacio
{
    internal sealed class UninstallPoll
    {
        internal bool ProcessKnown, Exited, RegistrationKnown, Registered, Running;
        internal int? ExitCode;
    }
    public sealed partial class Controller
    {
        private UninstallSession lastUninstall, leftoverView;
        private async Task BeginUninstall(InstalledApp app)
        {
            var operation = new CancellationTokenSource(); var token = operation.Token;
            cleanupCancel = operation; SetBusy(true, false); UninstallSession session = null;
            try
            {
                var currentApp = (await Task.Run(() => InstalledApps.Read(token, true), token)).FirstOrDefault(a => a.Id == app.Id);
                token.ThrowIfCancellationRequested();
                if (currentApp == null || currentApp.Command != app.Command) throw new System.IO.IOException(L.T("ثبت برنامه تغییر کرده است؛ دوباره بررسی کنید"));
                session = await Task.Run(() => AppLeftovers.Capture(currentApp, cleanupRoots, token), token);
                token.ThrowIfCancellationRequested();
                session.Process = System.Diagnostics.Process.Start(InstalledApps.StartInfo(currentApp));
                if (lastUninstall != null) lastUninstall.Dispose(); lastUninstall = session;
            }
            finally { if (cleanupCancel == operation) cleanupCancel = null; operation.Dispose(); SetBusy(false, false); }
            await ContinueUninstall(session);
        }
        private async Task ContinueUninstall(UninstallSession session)
        {
            if (WaitForUninstaller(session)) await OpenCleanupPage(false, session);
            else CleanupStatus("پیگیری متوقف شد؛ حذف‌کننده بسته نشده است. بررسی باقی‌مانده‌ها را بعداً باز کنید");
            UpdateCleanupControls();
        }
        private bool WaitForUninstaller(UninstallSession session, Func<CancellationToken, Task<UninstallPoll>> probe = null, Action<Window> onLoaded = null, int interval = 1500)
        {
            var cancellation = new CancellationTokenSource(); var token = cancellation.Token; cleanupCancel = cancellation; SetBusy(true, false);
            var dialog = new Window { Owner = Window, Title = L.T("پیگیری حذف برنامه"), Width = 620, SizeToContent = SizeToContent.Height, ResizeMode = ResizeMode.NoResize,
                Background = B("#10181C"), Foreground = B("#E4EFEB"), FontFamily = Window.FontFamily, FontSize = 13, FlowDirection = Window.FlowDirection,
                WindowStartupLocation = WindowStartupLocation.CenterOwner, Resources = Window.Resources };
            var panel = new StackPanel { Margin = new Thickness(24) }; dialog.Content = panel;
            panel.Children.Add(new TextBlock { Text = session.App.Name, FontSize = 23, FontWeight = FontWeights.SemiBold });
            panel.Children.Add(new TextBlock { Text = L.T("پس از پایان حذف‌کننده، مسیر و حجم باقی‌مانده‌ها نمایش داده می‌شود. هیچ فایلی خودکار حذف نمی‌شود."), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 14) });
            var activity = new ProgressBar { IsIndeterminate = true, Height = 4, Foreground = B("#69DDB7") }; panel.Children.Add(activity);
            var state = new TextBlock { Name = "UninstallState", TextWrapping = TextWrapping.Wrap, MinHeight = 65, Margin = new Thickness(0, 14, 0, 12), Foreground = B("#A9C3BA") }; panel.Children.Add(state);
            var buttons = new StackPanel { Orientation = Orientation.Horizontal }; panel.Children.Add(buttons);
            var later = ReviewButton("بعداً؛ فقط توقف پیگیری"); later.Name = "UninstallLater"; buttons.Children.Add(later); later.Click += delegate { dialog.DialogResult = false; };
            var finished = ReviewButton("حذف‌کننده تمام شده؛ بررسی باقی‌مانده‌ها"); finished.Name = "UninstallFinished"; finished.IsEnabled = false; buttons.Children.Add(finished);
            finished.Click += delegate { session.ManualFinish = true; dialog.DialogResult = true; };
            if (probe == null) probe = ct => Task.Run(() => {
                var poll = new UninstallPoll { ProcessKnown = session.Process != null, Exited = session.Exited, ExitCode = session.ExitCode };
                var apps = InstalledApps.Read(ct, true); poll.RegistrationKnown = true; poll.Registered = apps.Any(a => AppLeftovers.SameApp(a, session.App));
                poll.Running = AppLeftovers.RunningAt(session.App.Location); return poll;
            }, ct);
            dialog.Closed += delegate { cancellation.Cancel(); };
            dialog.Loaded += async delegate {
                if (onLoaded != null) onLoaded(dialog);
                try
                {
                    while (!token.IsCancellationRequested)
                    {
                        UninstallPoll poll;
                        try { poll = await probe(token); }
                        catch (OperationCanceledException) { throw; }
                        catch { poll = new UninstallPoll { ProcessKnown = session.Process != null, Exited = session.Exited, ExitCode = session.ExitCode }; }
                        token.ThrowIfCancellationRequested();
                        bool ready = session.Tracker.Observe(poll.ProcessKnown, poll.Exited, poll.ExitCode, poll.RegistrationKnown, poll.Registered, poll.Running);
                        if (ready) { session.ManualFinish = false; dialog.DialogResult = true; return; }
                        finished.IsEnabled = poll.Exited && !poll.Running;
                        state.Text = L.T(!poll.Exited || poll.Running ? "منتظر پایان حذف‌کننده یا برنامه…" :
                            !poll.RegistrationKnown ? "وضعیت ثبت برنامه قابل بررسی نیست؛ پس از اتمام حذف‌کننده می‌توانید بررسی دستی را شروع کنید" :
                            poll.Registered ? "حذف برنامه هنوز تأیید نشده است؛ خروج پنجرهٔ اولیه ممکن است به معنی پایان حذف نباشد" :
                            poll.ExitCode != 0 ? "خروج موفق حذف‌کننده تأیید نشد؛ پس از بررسی نتیجه می‌توانید باقی‌مانده‌ها را مرور کنید" : "در حال تأیید پایان حذف و آماده‌سازی بررسی باقی‌مانده‌ها…");
                        activity.IsIndeterminate = !poll.Exited || (poll.ExitCode == 0 && poll.RegistrationKnown && !poll.Registered);
                        await Task.Delay(interval, token);
                    }
                }
                catch (OperationCanceledException) { if (dialog.IsVisible) dialog.DialogResult = false; }
            };
            try { return dialog.ShowDialog() == true; }
            finally { cancellation.Cancel(); if (cleanupCancel == cancellation) cleanupCancel = null; SetBusy(false, false); cancellation.Dispose(); }
        }
    }
}
