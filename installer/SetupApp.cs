using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace SpacioSetup
{
    internal sealed class SetupWindow : Form
    {
        private readonly InstallContext context;
        private readonly bool uninstall, preview;
        private bool persian, busy, done;
        private readonly Label title = new Label(), subtitle = new Label(), description = new Label(), pathLabel = new Label(), status = new Label();
        private readonly CheckBox desktop = new CheckBox();
        private readonly Button action = new Button(), close = new Button(), language = new Button();
        private readonly ProgressBar progress = new ProgressBar();
        internal SetupWindow(InstallContext installContext, bool remove, bool hiddenPreview = false)
        {
            context = installContext; uninstall = remove; preview = hiddenPreview;
            persian = System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "fa";
            Text = "Spacio Setup"; ClientSize = new Size(760, 520); MinimumSize = MaximumSize = Size;
            FormBorderStyle = FormBorderStyle.FixedDialog; MaximizeBox = false; MinimizeBox = true;
            StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(16, 24, 28); ForeColor = Color.FromArgb(228, 239, 235); Font = new Font("Segoe UI", 10);
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spacio.ico")) Icon = new Icon(stream);
            var logo = new PictureBox { Bounds = new Rectangle(30, 30, 66, 66), SizeMode = PictureBoxSizeMode.Zoom };
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Spacio.png")) using (var image = Image.FromStream(stream)) logo.Image = new Bitmap(image);
            Controls.Add(logo);
            title.SetBounds(116, 27, 460, 48); title.Font = new Font("Segoe UI", 25, FontStyle.Bold); Controls.Add(title);
            subtitle.SetBounds(118, 80, 510, 26); subtitle.ForeColor = Color.FromArgb(128, 213, 177); Controls.Add(subtitle);
            language.SetBounds(620, 35, 110, 38); StyleButton(language, false); language.Click += delegate { SetLanguage(!persian); }; Controls.Add(language);
            description.SetBounds(30, 135, 700, 93); description.Font = new Font("Segoe UI", 11); Controls.Add(description);
            pathLabel.SetBounds(30, 238, 700, 48); pathLabel.ForeColor = Color.FromArgb(150, 174, 166); pathLabel.Font = new Font("Segoe UI", 9); pathLabel.Text = context.Directory; Controls.Add(pathLabel);
            desktop.SetBounds(30, 302, 700, 32); desktop.Checked = true; desktop.Visible = !uninstall; Controls.Add(desktop);
            status.SetBounds(30, 345, 700, 75); status.ForeColor = Color.FromArgb(162, 196, 180); Controls.Add(status);
            progress.SetBounds(30, 426, 700, 5); progress.Style = ProgressBarStyle.Marquee; progress.Visible = false; Controls.Add(progress);
            close.SetBounds(470, 455, 110, 40); StyleButton(close, false); close.Click += delegate { Close(); }; Controls.Add(close);
            action.SetBounds(590, 455, 140, 40); StyleButton(action, true); action.Click += async delegate { await Act(); }; Controls.Add(action);
            FormClosing += (s, e) => { if (busy) e.Cancel = true; };
            SetLanguage(persian);
        }
        protected override bool ShowWithoutActivation { get { return preview; } }
        private static void StyleButton(Button button, bool primary)
        {
            button.FlatStyle = FlatStyle.Flat; button.FlatAppearance.BorderSize = 0;
            button.BackColor = primary ? Color.FromArgb(105, 221, 183) : Color.FromArgb(35, 49, 54);
            button.ForeColor = primary ? Color.FromArgb(18, 62, 45) : Color.FromArgb(225, 239, 232); button.Cursor = Cursors.Hand;
        }
        internal void SetLanguage(bool value)
        {
            persian = value; RightToLeft = value ? RightToLeft.Yes : RightToLeft.No;
            pathLabel.RightToLeft = RightToLeft.No;
            title.Text = value ? (uninstall ? "حذف اسپاسیو" : "نصب اسپاسیو") : (uninstall ? "Uninstall Spacio" : "Install Spacio");
            subtitle.Text = "SPACIO  /  " + SetupEngine.Version.ToString(3); subtitle.RightToLeft = RightToLeft.No;
            language.Text = value ? "English" : "فارسی";
            description.Text = value ? (uninstall ? "فایل‌های برنامه و میان‌برهای آن حذف می‌شوند.\nاسکن‌های ذخیره‌شده، تنظیمات و فایل‌های شخصی شما باقی می‌مانند.\nپیش از ادامه، Spacio را ببندید." : "بررسی فضای دیسک، مدیریت فایل‌ها و پاک‌سازی با تأیید شما.\nنصب برای حساب فعلی ویندوز، همراه با آپدیت داخل برنامه.\nاسکن‌ها و تنظیمات قبلی حفظ می‌شوند.") : (uninstall ? "Remove the application and its shortcuts.\nYour saved scans, preferences and personal files will remain.\nClose Spacio before continuing." : "Explore disk usage, manage files and review cleanup.\nInstall for your Windows account, with in-app updates.\nExisting saved scans and preferences are preserved.");
            desktop.Text = value ? "ساخت میان‌بر Spacio روی دسکتاپ" : "Create a Spacio desktop shortcut";
            close.Text = value ? "بستن" : "Close";
            action.Text = done ? (value ? (uninstall ? "پایان" : "اجرای Spacio") : (uninstall ? "Finish" : "Open Spacio")) : (value ? (uninstall ? "تأیید حذف برنامه" : "نصب") : (uninstall ? "Uninstall" : "Install"));
            if (!busy) status.Text = done ? (value ? "انجام شد. اسکن‌ها و فایل‌های شما حفظ شدند." : "Done. Your saved scans and files are preserved.") : (value ? "نصب در مسیر زیر انجام می‌شود. دسترسی Administrator لازم نیست." : "Installs to the location above. Administrator access is not required.");
        }
        private async Task Act()
        {
            if (busy) return;
            if (done) { if (!uninstall) Process.Start(new ProcessStartInfo(context.Executable) { WorkingDirectory = context.Directory }); Close(); return; }
            busy = true; action.Enabled = close.Enabled = language.Enabled = desktop.Enabled = false; progress.Visible = true;
            status.Text = persian ? "در حال انجام عملیات… لطفاً منتظر بمانید." : "Working… please wait.";
            try
            {
                bool shortcut = desktop.Checked;
                await RunOnSta(() => { if (uninstall) SetupEngine.Uninstall(context); else SetupEngine.Install(context, shortcut); });
                done = true;
            }
            catch (Exception e) { status.Text = (persian ? "عملیات کامل نشد. Spacio را ببندید و جزئیات زیر را بررسی کنید:\n" : "Operation did not finish. Close Spacio and check the details below:\n") + e.Message; }
            finally { busy = false; action.Enabled = close.Enabled = language.Enabled = true; desktop.Enabled = !done; progress.Visible = false; if (done) SetLanguage(persian); }
        }
        internal static Task RunOnSta(Action action)
        {
            var completion = new TaskCompletionSource<bool>();
            var thread = new Thread(() => { try { action(); completion.SetResult(true); } catch (Exception e) { completion.SetException(e); } });
            thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start(); return completion.Task;
        }
        internal void SavePreview(string path)
        {
            ShowInTaskbar = false; StartPosition = FormStartPosition.Manual; Location = new Point(-16000, -16000); Show(); Application.DoEvents();
            using (var bitmap = new Bitmap(Width, Height)) { DrawToBitmap(bitmap, new Rectangle(0, 0, Width, Height)); bitmap.Save(path, System.Drawing.Imaging.ImageFormat.Png); }
        }
    }
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length >= 2 && args[0] == "--self-test") return SetupTests.Run(args[1], args.Length > 2 ? args[2] : null, args.Length > 3 && args[3] == "--public");
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full"))
                    if (key == null || Convert.ToInt32(key.GetValue("Release", 0)) < 528040) throw new IOException("Spacio needs .NET Framework 4.8 or later. Download it from https://dotnet.microsoft.com/download/dotnet-framework/net48");
                if (!Environment.Is64BitOperatingSystem || Environment.OSVersion.Version.Major < 10) throw new IOException("Spacio requires 64-bit Windows 10 or Windows 11.");
                bool uninstall = args.Length == 1 && args[0] == "--uninstall";
                if (args.Length > 0 && !uninstall) throw new IOException("Unknown Setup argument.");
                var context = InstallContext.CurrentUser();
                if (uninstall && SetupEngine.Same(SetupEngine.Self, Path.Combine(context.Directory, "Spacio.Setup.exe")))
                {
                    // Run from a unique temporary copy so the installed uninstaller can
                    // be removed without a shell command, elevation or forced reboot.
                    string helperDirectory = Path.Combine(Path.GetTempPath(), "Spacio-Uninstall-" + Guid.NewGuid().ToString("N"));
                    SetupEngine.CheckAncestors(helperDirectory); Directory.CreateDirectory(helperDirectory);
                    string helper = Path.Combine(helperDirectory, "Spacio.Setup.exe"); File.Copy(SetupEngine.Self, helper);
                    Process.Start(new ProcessStartInfo(helper, "--uninstall") { UseShellExecute = false, WorkingDirectory = helperDirectory }); return 0;
                }
                using (var window = new SetupWindow(context, uninstall)) Application.Run(window); return 0;
            }
            catch (Exception e) { MessageBox.Show(e.Message, "Spacio Setup", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
        }
    }
}
