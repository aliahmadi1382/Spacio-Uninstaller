# اسپاسیو | Spacio

![Spacio logo](assets/spacio-128.png)

برنامهٔ دسکتاپ ویندوز برای بررسی حجم فایل‌ها و پوشه‌ها، با رابط فارسی و انگلیسی.

نسخهٔ ۱.۲ اسکن‌های کامل را ذخیره می‌کند. با انتخاب دوبارهٔ مسیر یا زیرپوشه‌های آن، نتیجهٔ قبلی بدون پیمایش مجدد فایل‌ها باز می‌شود؛ این قابلیت پس از بستن برنامه هم کار می‌کند. [نسخهٔ قابل حمل را از Releases دریافت کنید](https://github.com/aliahmadi1382/Spacio-Uninstaller/releases/latest). اسکن‌های نسخه‌های قبل از ۱.۲ ذخیره نشده‌اند و اولین بار در نسخهٔ جدید به اسکن نیاز دارند.

## اجرا

فایل `Spacio.exe` را از بستهٔ نسخهٔ جدید اجرا کنید. فایل `Spacio.exe.config` باید کنار آن بماند. نصب، اینترنت یا دسترسی Administrator لازم نیست. سیستم موردنیاز: Windows 10/11 نسخهٔ ۶۴ بیتی با .NET Framework 4.8.

1. از کلید بالای پنجره برای تغییر **فارسی / English** استفاده کنید. زبان انتخابی برای اجرای بعدی ذخیره می‌شود.
2. روی درایو در نوار کناری کلیک کنید، یا پوشه‌ای انتخاب کنید و **باز کردن / اسکن** را بزنید. مسیر جدید اسکن می‌شود؛ مسیر قبلاً اسکن‌شده از نتیجهٔ ذخیره‌شده باز می‌شود. برای بررسی تغییرات جدید، **اسکن مجدد** را بزنید.
3. نقشهٔ فضا و جدول حجم کل زیرپوشه‌ها را نشان می‌دهند. برای ورود به یک پوشه روی نقشه کلیک کنید یا روی ردیف آن دوبار کلیک کنید.
4. از **فایل‌های بزرگ**، **فایل‌های قدیمی** و **نوع فایل‌ها** استفاده کنید. جست‌وجو زیرپوشه‌ها را هم بررسی می‌کند. فیلتر حجم، تاریخ و ترتیب نمایش در بالای جدول قرار دارند.
5. با Ctrl یا Shift چند ردیف را انتخاب کنید. **انتقال به سطل بازیافت** ابتدا فهرست دقیق مسیرها را برای تأیید نشان می‌دهد. کلید Delete همین پنجرهٔ تأیید را باز می‌کند.
6. فایل‌ها تا زمانی که سطل بازیافت را در ویندوز خالی نکنید، قابل بازیابی هستند و همچنان فضای دیسک را اشغال می‌کنند. بعد از انتقال، برای به‌روز شدن اطلاعات دوباره اسکن کنید.

کلیک راست روی ردیف: ورود به پوشه، نمایش در Explorer و کپی مسیر. تاریخ ایجاد و تاریخ تغییر اصلی در توضیح شناور هر ردیف نمایش داده می‌شود. خروجی CSV شامل تمام نتایج فیلترشده است؛ جدول برای پاسخ‌گویی سریع‌تر حداکثر ۲٬۰۰۰ ردیف را نمایش می‌دهد.

برای حذف نرم‌افزارهای نصب‌شده، دکمهٔ **برنامه‌های نصب‌شده** تنظیمات مربوط ویندوز را باز می‌کند.

## رفتار اسکن و پاک‌سازی

- اسکن تنها فرادادهٔ فایل‌ها را می‌خواند؛ فایل‌ها و زمان تغییرشان را دست‌کاری نمی‌کند. برنامه مسیر انتخابی را خودکار اسکن نمی‌کند؛ شروع با انتخاب شماست.
- درایوهای محلی و USB آماده قابل اسکن هستند. مسیرهای شبکه پشتیبانی نمی‌شوند. هیچ محتوای فایلی به اینترنت ارسال نمی‌شود.
- حجم‌ها **منطقی** هستند و با واحدهای KiB / MiB / GiB نمایش داده می‌شوند. فشرده‌سازی، فایل‌های sparse و hard link می‌توانند باعث اختلاف با فضای اشغال‌شدهٔ واقعی شوند. hard linkها در هر مسیر شمرده می‌شوند.
- تاریخ پوشه برابر جدیدترین زمان تغییر خود پوشه یا محتوای آن است. تاریخ‌ها میلادی و به وقت محلی‌اند. قدیمی بودن یک فایل نشان‌دهندهٔ بی‌استفاده بودن آن نیست. نمای فایل‌های قدیمی از ۱۸۰ روز شروع می‌شود.
- مسیرهای غیرقابل‌دسترسی، junction، symbolic link و placeholderهای ابری با علامت ناقص گزارش می‌شوند؛ پیوندها دنبال نمی‌شوند. جزئیات حداکثر ۵۰۰ خطای اول قابل مشاهده‌اند. ردیف بدون دسترسی، اندازهٔ واقعی آن مسیر را نشان نمی‌دهد.
- اسکن قابل توقف است و نتیجهٔ متوقف‌شده واضحاً ناقص اعلام می‌شود. پاک‌سازی بر اساس اسکن متوقف‌شده فعال نمی‌شود.
- اسکن‌های پایان‌یافته به‌صورت فشرده در `%LOCALAPPDATA%\Spacio\Scans` ذخیره می‌شوند؛ خطاها و موارد بررسی‌نشده نیز حفظ می‌شوند. اسکن لغوشده نتیجهٔ کامل قبلی را جایگزین نمی‌کند. ذخیرهٔ نتیجه ممکن است بسته به تعداد فایل‌ها چند ثانیه طول بکشد و فضای دیسک مصرف می‌کند. اگر ذخیره ممکن نباشد، پیام مشخصی نمایش داده می‌شود و نتیجه در حافظهٔ همین اجرا باقی می‌ماند.
- زمان اسکن کنار عنوان نقشهٔ فضا نمایش داده می‌شود. نتیجهٔ ذخیره‌شده نمای زندهٔ فایل‌ها نیست و خودکار منقضی یا دوباره اسکن نمی‌شود. تغییرات بیرون از برنامه با **اسکن مجدد** دیده می‌شوند. نتیجهٔ ناقص یا خرابِ ذخیره‌شده بارگذاری نمی‌شود؛ برای مسیر بدون نتیجهٔ معتبر، اسکن تازه لازم است.
- بعد از تلاش برای پاک‌سازی، نتیجه‌های ذخیره‌شدهٔ مرتبط «تغییریافته» علامت می‌خورند؛ همچنان قابل مشاهده‌اند، اما پاک‌سازی بعدی به اسکن تازه نیاز دارد. بررسی تغییر فایل انتخابی پیش از حذف همچنان انجام می‌شود.
- ریشهٔ اسکن، مسیرهای سیستم و برنامه‌ها، AppData، پروفایل کاربران، محل اجرای خود برنامه و پوشه‌های حاوی موارد سیستمی یا بررسی‌نشده محافظت می‌شوند.
- قبل از انتقال، فرادادهٔ فایل یا تمام درخت پوشه با نتیجهٔ اسکن مقایسه می‌شود. تغییرات مشاهده‌شده عملیات را متوقف می‌کنند. این مقایسه قفل فایل یا snapshot اتمیک نیست؛ هنگام پاک‌سازی از تغییر هم‌زمان پوشهٔ انتخابی خودداری کنید.
- عملیات از Windows Shell و `IFileOperation` استفاده می‌کند. اگر ویندوز امکان بازیافت را نداشته باشد، callback پیش از حذف دائمی آن را رد می‌کند. حذف دائمی یا خالی‌کردن خودکار سطل بازیافت در برنامه وجود ندارد.
- انتخاب هم‌زمان پوشه و فرزندش فقط یک بار پردازش می‌شود. پس از هر تلاش پاک‌سازی، نتیجهٔ اسکن کهنه اعلام می‌شود و پاک‌سازی بعدی به اسکن تازه نیاز دارد.
- زبان در `%LOCALAPPDATA%\Spacio\language.txt` ذخیره می‌شود. گزارش CSV تنها در مسیر انتخاب‌شده توسط شما نوشته می‌شود.

## English

Download the portable ZIP from [Releases](https://github.com/aliahmadi1382/Spacio-Uninstaller/releases/latest), extract it, and run `Spacio.exe` alongside `Spacio.exe.config`. Requires 64-bit Windows 10/11 and .NET Framework 4.8. No installation or administrator access is required.

**Version 1.2:** Open / scan and drive shortcuts reuse a completed scan of the selected folder or an ancestor. Results survive application restarts and are timestamped above the space map. Rescan explicitly reads the filesystem again and replaces the saved result. Scans from older app versions were not persisted and need one initial scan in 1.2. Cancelled scans never replace completed results. Saved results do not update automatically when files change outside the app.

Compressed, versioned snapshots are stored under `%LOCALAPPDATA%\Spacio\Scans`; they use a data-only format and atomic replacement. Memory retention is bounded, and loading supports cancellation. If storage fails, a message explains that the result is retained only in the running app. A corrupt snapshot is ignored. Cleanup marks overlapping snapshots stale across restarts, and stale results cannot authorize another cleanup until an explicit rescan. Individual cleanup targets still undergo their normal metadata verification.

Switch to **English** at the top of the window. Select a drive or folder to scan; explore the space map, drill into folders, and filter by size, last change, or file category. Search includes descendants. Use Ctrl/Shift to select multiple rows, then review the exact paths before moving them to the Recycle Bin. Space is reclaimed when you empty the Recycle Bin in Windows. Rescan after a cleanup.

Scanning reads metadata only. Junctions, symbolic links, cloud placeholders, and inaccessible items are reported as skipped. Logical sizes may differ from disk allocation, and hard links are counted per path. Dates use the Gregorian calendar and local time; folder dates reflect the most recent change in the folder or its descendants. The older-files view starts at 180 days. Creation and original modification dates are available in each row's tooltip.

System/application paths, user profiles, AppData, scan roots, and incomplete results cannot authorize recycling. Metadata is rechecked before each operation, but this is not an atomic filesystem snapshot. Windows operations that would permanently delete an item are vetoed. The application never empties the Recycle Bin. The Installed apps button opens Windows Settings for application uninstallation.

The table displays up to 2,000 matching rows; CSV exports every matching result. Do not sum folder and child rows together because folder totals already include their descendants. Reports use UTF-8 and neutralize formula-like cell content.

## Build and verification

Uses native C# / WPF and the C# compiler included with .NET Framework. No NuGet packages or downloaded SDK are needed.

Clone the repository, then build and test from its root directory:

```powershell
git clone https://github.com/aliahmadi1382/Spacio-Uninstaller.git
cd Spacio-Uninstaller
.\build.ps1
.\test.ps1

# Build/test a separate version while the previous executable remains open:
.\build.ps1 -Destination .\dist\v1.2
.\test.ps1 -Executable .\dist\v1.2\Spacio.exe
```

`test.ps1` creates isolated fixtures under `.test-data`, including a junction to another generated fixture, paths longer than 260 characters, and 10,000 empty files. Its recycle integration checks move only generated test files and a generated test folder to the Recycle Bin. It leaves these fixtures available for inspection. Reports and off-screen Persian/English screenshots are saved to `artifacts`.

Cache checks cover persistence, path normalization, Unicode, descendant reuse, file changes, invalidation, corruption, cancellation, and atomic replacement. UI checks exercise cached reopening and explicit rescanning, then launch a second process to verify restoration after restart. UI checks use an isolated cache under `artifacts`, not your saved scans.

Git tracks the full source, UI, translations, vector/raster brand assets, build scripts, tests, and documentation. Generated executables are distributed through Releases. Disposable test fixtures (`.test-data`), test reports/screenshots (`artifacts`), build output (`dist`), local credentials, and saved scan data are ignored. See [CHANGELOG.md](CHANGELOG.md) for version notes.

Source files: `src\Core.cs` (scan/metadata/safety), `src\Recycle.cs` (Windows Shell interop), `src\Locales.cs` (language), `src\App.cs` (application), `src\MainWindow.xaml` (interface), `src\SelfTests.cs` (checks).

Brand assets: `assets\spacio.svg` and `assets\BrandMark.xaml` contain the vector leaf/storage-map mark. `tools\build-brand.ps1` renders the matching PNGs and a seven-resolution Windows ICO (16–256 px). The icon and vector mark are embedded in the executable; no separate logo file is required at runtime. The language loader also reads the prior version's saved preference when the new settings file does not exist.

The Windows integration follows Microsoft's [IFileOperation documentation](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation) and [operation flags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags).
