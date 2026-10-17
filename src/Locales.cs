using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace Spacio
{
    public static class L
    {
        public static bool Persian = true;
        private static readonly Dictionary<string, string> English = new Dictionary<string, string> {
            {"اسپاسیو", "Spacio"},
            {"باز کردن / اسکن", "Open / scan"}, {"اسکن مجدد", "Rescan"},
            {"در حال باز کردن مسیر…", "Opening path…"}, {"در حال ذخیرهٔ نتیجهٔ اسکن…", "Saving scan result…"},
            {"نتیجهٔ ذخیره‌شده", "Saved scan"}, {"نتیجهٔ اسکن فعلی", "Current scan"},
            {"اسکن کامل و ذخیره شد", "Scan complete and saved"},
            {"نتیجهٔ قبلی بدون اسکن دوباره باز شد", "Previous result opened without rescanning"},
            {"نتیجه فقط تا بستن برنامه نگه داشته می‌شود", "Result retained only until the app closes"},
            {"نتیجهٔ قبلی باز می‌شود؛ فقط مسیر جدید اسکن می‌شود.", "Opens saved results; only new paths are scanned."},
            {"این مسیر را دوباره از روی دیسک بررسی و نتیجهٔ ذخیره‌شده را جایگزین می‌کند.", "Scans this path from disk again and replaces its saved result."},
            {"این اطلاعات مربوط به زمان اسکن است. برای دیدن تغییرات جدید «اسکن مجدد» را بزنید.", "These results are from the displayed scan time. Use Rescan to see newer changes."},
            {"فضای دیسک، زیر ذره‌بین.", "Make room for what matters."},
            {"یک نمای روشن از فایل‌ها، پوشه‌ها و فضای مصرف‌شده", "A clear view of your files, folders, and storage."},
            {"کاوشگر فضا", "Storage explorer"}, {"تحلیل فضای ذخیره‌سازی", "STORAGE INTELLIGENCE"},
            {"نمای کلی", "Overview"}, {"فایل‌های بزرگ", "Largest files"}, {"فایل‌های قدیمی", "Older files"},
            {"نوع فایل‌ها", "File types"}, {"درایوهای شما", "YOUR DRIVES"}, {"ابزارهای ویندوز", "WINDOWS TOOLS"},
            {"برنامه‌های نصب‌شده", "Installed apps"}, {"سطل بازیافت", "Recycle Bin"},
            {"اسکن فقط خواندنی است", "Read-only scanning"},
            {"هیچ فایلی هنگام اسکن تغییر نمی‌کند. پاک‌سازی فقط با انتخاب و تأیید شما انجام می‌شود.", "Scanning leaves your files untouched. You choose and confirm every cleanup."},
            {"انتخاب پوشه", "Choose folder"}, {"شروع اسکن", "Scan storage"}, {"توقف", "Stop"},
            {"دوباره اسکن کن", "Scan again"}, {"حجم بررسی‌شده", "SCANNED SIZE"}, {"فایل", "FILES"}, {"پوشه", "Folders"},
            {"حجم منطقی فایل‌ها", "Logical file size"}, {"پوشه‌های بررسی‌شده", "FOLDERS FOUND"},
            {"موارد بررسی‌نشده", "SKIPPED / INACCESSIBLE"}, {"مشاهدهٔ جزئیات", "View details"},
            {"نقشهٔ فضا", "Space map"}, {"برای ورود، روی پوشه کلیک کنید", "Click a folder to explore"},
            {"پوشه‌ها و فایل‌ها", "Folders & files"}, {"خروجی CSV", "Export CSV"}, {"پوشهٔ بالاتر", "Up"},
            {"جست‌وجو در این پوشه و زیرپوشه‌ها…", "Search this folder and its descendants…"},
            {"همهٔ اندازه‌ها", "All sizes"}, {"بیشتر از ۱۰ MiB", "Over 10 MiB"}, {"بیشتر از ۱۰۰ MiB", "Over 100 MiB"}, {"بیشتر از ۱ GiB", "Over 1 GiB"},
            {"همهٔ تاریخ‌ها", "All dates"}, {"قدیمی‌تر از ۳۰ روز", "Older than 30 days"}, {"قدیمی‌تر از ۹۰ روز", "Older than 90 days"},
            {"قدیمی‌تر از ۱۸۰ روز", "Older than 180 days"}, {"قدیمی‌تر از یک سال", "Older than a year"},
            {"حجم: بزرگ به کوچک", "Size: largest first"}, {"تاریخ: جدید به قدیم", "Date: newest first"},
            {"تاریخ: قدیم به جدید", "Date: oldest first"}, {"نام: الفبا", "Name: A to Z"},
            {"نام", "Name"}, {"اندازه", "Size"}, {"سهم از پوشه", "Share of folder"}, {"آخرین تغییر محتوا", "Latest content change"},
            {"تعداد فایل", "Files"}, {"وضعیت", "Status"}, {"محافظت‌شده", "Protected"}, {"ناقص", "Incomplete"}, {"قابل بررسی", "Review"},
            {"برای شروع، یک درایو یا پوشه را اسکن کنید", "Start with a drive or folder"},
            {"درایو را از نوار کناری انتخاب کنید، یا مسیر دلخواه را وارد کنید.", "Choose a drive in the sidebar or enter a folder path above."},
            {"موردی با این فیلترها پیدا نشد", "No items match these filters"}, {"آمادهٔ اسکن", "Ready to scan"},
            {"در حال اسکن…", "Scanning…"}, {"اسکن کامل شد", "Scan complete"}, {"اسکن متوقف شد؛ نتیجه ناقص است", "Scan stopped; results are incomplete"},
            {"انتخابی ندارید", "No items selected"}, {"انتقال به سطل بازیافت", "Move to Recycle Bin"},
            {"نمایش در Explorer", "Show in Explorer"}, {"کپی مسیر", "Copy path"}, {"ورود به پوشه", "Open folder"},
            {"آزاد", "free"}, {"از", "of"}, {"اسکن این درایو", "Scan this drive"}, {"ثانیه", "seconds"},
            {"مورد", "items"}, {"انتخاب‌شده", "selected"}, {"نمایش", "Showing"}, {"نتایج", "results"},
            {"لغو", "Cancel"}, {"بستن", "Close"}, {"تأیید انتقال", "Confirm move"}, {"مرور انتخاب‌ها", "Review your selection"},
            {"موارد زیر به سطل بازیافت منتقل می‌شوند:", "These items will be moved to the Recycle Bin:"},
            {"فضا پس از خالی کردن سطل بازیافت در ویندوز آزاد می‌شود. از همان‌جا می‌توانید فایل‌ها را بازیابی کنید.", "Space is freed after you empty the Recycle Bin in Windows. You can restore items from there first."},
            {"در حال بررسی تغییرات و انتقال…", "Checking for changes and recycling…"},
            {"انتقال تمام شد. برای به‌روزرسانی نتیجه دوباره اسکن کنید.", "Recycling finished. Scan again to update the results."},
            {"انتقال انجام نشد", "Could not recycle"}, {"انجام شد", "Done"}, {"خطا", "Error"},
            {"گزارش ذخیره شد", "Report saved"}, {"ذخیرهٔ گزارش", "Save report"},
            {"گزارش دسترسی و موارد ردشده", "Skipped and inaccessible items"},
            {"حداکثر ۵۰۰ پیام اول نمایش داده می‌شود.", "Showing up to the first 500 messages."},
            {"هیچ مورد بررسی‌نشده‌ای وجود ندارد.", "No skipped or inaccessible items."},
            {"خروجی شامل همهٔ نتایج فیلترشده است؛ اندازهٔ پوشه‌ها را دوباره با فایل‌ها جمع نکنید.", "Export includes all filtered results. Folder sizes already include their files."},
            {"این مورد در نتیجهٔ اسکن فعلی نیست.", "This item is not part of the current scan."},
            {"پوشه پیدا نشد یا در دسترس نیست.", "The folder does not exist or is inaccessible."},
            {"یک درایو یا پوشه انتخاب کنید.", "Choose a drive or folder."},
            {"در این نسخه یک مسیر محلی مثل C:\\ یا D:\\Data انتخاب کنید.", "Choose a local path such as C:\\ or D:\\Data."},
            {"درایو محلی آماده نیست یا پشتیبانی نمی‌شود.", "The local drive is not ready or is unsupported."},
            {"مسیر شامل پیوند، junction یا فایل ابری است.", "The path contains a link, junction, or cloud placeholder."},
            {"مورد در حین اسکن جابه‌جا شده است.", "The item moved or disappeared during the scan."},
            {"محتوای پوشه از زمان اسکن تغییر کرده است. دوباره اسکن کنید.", "The folder contents have changed since scanning. Scan again."},
            {"فایل از زمان اسکن تغییر کرده است. دوباره اسکن کنید.", "The file has changed since scanning. Scan again."},
            {"ابتدا اسکن کامل انجام دهید", "Complete a scan first"}, {"ریشهٔ اسکن قابل حذف نیست", "The scan root cannot be recycled"},
            {"مورد به این اسکن تعلق ندارد", "Item does not belong to this scan"}, {"اسکن ناقص یا مسیر پیوندی", "Incomplete scan or linked path"},
            {"فایل سیستمی، ابری یا پیوندی", "System file, cloud placeholder, or link"},
            {"مسیر محافظت‌شده؛ از تنظیمات ویندوز مدیریت شود", "Protected path; manage it in Windows Settings"},
            {"مسیر محافظت‌شدهٔ سیستم", "Protected system path"},
            {"داده‌های برنامه؛ از تنظیمات برنامه مدیریت شود", "Application data; manage it in the application"},
            {"پوشهٔ پروفایل کاربر", "User profile folder"}, {"پوشهٔ کاربران", "Users folder"},
            {"پوشهٔ مدیریت‌شدهٔ ویندوز؛ اسکن نشد", "Windows-managed folder; skipped"},
            {"پیوند یا فایل ابری؛ دنبال نشد", "Link or cloud placeholder; not followed"},
            {"خواندن مشخصات ممکن نیست: ", "Cannot read metadata: "}, {"دسترسی کامل ممکن نیست: ", "Cannot fully access: "},
            {"ویدیو", "Video"}, {"تصویر", "Images"}, {"صدا", "Audio"}, {"آرشیو", "Archives"}, {"سند", "Documents"}, {"برنامه", "Applications"}, {"سایر", "Other"},
            {"زمان ایجاد", "Created"}, {"زمان تغییر", "Modified"}, {"نوع", "Type"}, {"مسیر", "Path"},
            {"این اندازه با فضای اشغال‌شدهٔ واقعی دیسک فرق دارد؛ فشرده‌سازی، hard link و فایل‌های sparse می‌توانند اختلاف ایجاد کنند.", "Logical size may differ from disk usage because of compression, hard links, and sparse files."},
            {"تاریخ پوشه، جدیدترین تغییر خود پوشه یا محتوای آن است؛ قدیمی بودن به معنی بی‌استفاده بودن نیست.", "Folder dates show the latest change in the folder or its contents. Age does not mean a file is unused."},
            {"حذف دائمی مجاز نیست؛ ویندوز امکان بازیافت این مورد را ندارد.", "Permanent deletion is disabled; Windows cannot recycle this item."},
            {"عملیات لغو شد یا کامل نشد.", "The operation was cancelled or did not complete."},
            {"داده‌ها تغییر کرده‌اند؛ دوباره اسکن کنید", "Results are stale; scan again"},
            {"فایل‌های قدیمی‌تر از ۱۸۰ روز", "Files last modified over 180 days ago"},
            {"گروه را برای دیدن فایل‌هایش انتخاب کنید", "Select a category to see its files"},
            {"همهٔ نوع‌ها", "All file types"}, {"اسکن در حال اجراست", "A scan is running"},
            {"فایل اجرایی و کد منبع برنامه", "Application executable and source files"},
            {"در حال آماده‌سازی فهرست…", "Preparing results…"}, {"در حال ذخیرهٔ گزارش…", "Saving report…"},
            {"بازگشت به ریشه", "Back to scan root"}, {"نتیجهٔ اسکن قبلی", "Previous scan results"},
            {"خطا در ذخیرهٔ زبان؛ این انتخاب فقط برای اجرای فعلی است.", "Could not save the language; this choice applies to this session only."}
        };

        public static string T(string text)
        {
            if (Persian || text == null) return text;
            string value;
            if (English.TryGetValue(text, out value)) return value;
            foreach (var pair in English)
                if (text.Contains(pair.Key)) text = text.Replace(pair.Key, pair.Value);
            return text;
        }
        private static string SettingsPath { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Spacio", "language.txt"); } }
        public static void Load()
        {
            try
            {
                // Read the previous application's preference without changing a running installation.
                string previousPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskLens", "language.txt");
                string path = File.Exists(SettingsPath) ? SettingsPath : previousPath;
                if (File.Exists(path)) Persian = File.ReadAllText(path).Trim() != "en";
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
        }
        public static void Save()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath));
            File.WriteAllText(SettingsPath, Persian ? "fa" : "en");
        }
    }
}
