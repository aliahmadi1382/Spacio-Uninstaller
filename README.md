# اسپاسیو | Spacio

![Spacio logo](assets/spacio-128.png)

برنامهٔ دسکتاپ ویندوز برای بررسی حجم فایل‌ها و پوشه‌ها، با رابط فارسی و انگلیسی.

در نسخهٔ **۱.۴.۱** بخش‌های **پوشه‌ها و فایل‌ها، فایل‌های بزرگ، فایل‌های قدیمی و نوع فایل‌ها** صفحهٔ کامل دارند. آن‌ها را از نوار کناری باز کنید، یا بالای جدول نمای کلی **نمایش در صفحهٔ کامل** را بزنید. جدول جای کارت‌ها و نقشه را می‌گیرد تا ردیف‌های بیشتری دیده شوند؛ جست‌وجو، فیلترها، به‌روزرسانی زنده و خروجی CSV در دسترس می‌مانند. با **بازگشت به نمای کلی** دوباره کارت‌ها و نقشه نمایش داده می‌شوند.

نسخهٔ **۱.۴** نتیجهٔ ذخیره‌شده را ابتدا باز می‌کند و سپس تغییرات فایل‌ها را در پس‌زمینه دنبال می‌کند: حذف، جابه‌جایی، اضافه‌شدن و تغییر حجم. Refresh جدول را خالی نمی‌کند. [آخرین نسخهٔ قابل حمل را دریافت کنید](https://github.com/aliahmadi1382/Spacio-Uninstaller/releases/latest). اسکن‌های کامل نسخهٔ ۱.۲ به بعد همچنان قابل استفاده‌اند.

در نسخهٔ **۱.۳.۲** صفحهٔ شروع حرکت پیوسته دارد: حلقه‌های چرخان، لوگوی شناور، نورهای سبز و ذرات متحرک. این صفحه هنگام آماده‌شدن برنامه و بارگذاری نتیجهٔ قبلی هم متحرک می‌ماند و پس از یک معرفی کوتاه وارد محیط اصلی می‌شود. آپدیت، نتایج اسکن و فایل‌های اسکن‌شدهٔ شما را پاک نمی‌کند؛ نسخهٔ جدید همان نتیجهٔ ذخیره‌شدهٔ قبلی را باز می‌کند.

## اجرا

فایل `Spacio.exe` را از بستهٔ نسخهٔ جدید اجرا کنید. فایل `Spacio.exe.config` باید کنار آن بماند. نصب، اینترنت یا دسترسی Administrator لازم نیست. سیستم موردنیاز: Windows 10/11 نسخهٔ ۶۴ بیتی با .NET Framework 4.8.

1. از کلید بالای پنجره برای تغییر **فارسی / English** استفاده کنید. زبان انتخابی برای اجرای بعدی ذخیره می‌شود.
2. روی درایو در نوار کناری کلیک کنید، یا پوشه‌ای انتخاب کنید و **باز کردن / اسکن** را بزنید. مسیر جدید اسکن می‌شود؛ مسیر قبلی از نتیجهٔ ذخیره‌شده باز می‌شود و تغییرات خودکار بررسی می‌شوند. **تازه‌سازی ↻ / Refresh** فقط مسیر داخل کادر و زیرپوشه‌هایش را در پس‌زمینه بررسی می‌کند. با ورود به پوشه، مسیر کادر نیز تغییر می‌کند. مثلاً در `C:\Projects\Git-Hub`، Refresh کل درایو C را دوباره نمی‌خواند؛ فقط حجم مجموع پوشه‌های بالاتر با دادهٔ جدید محاسبه می‌شود. جدول هنگام بررسی در دسترس می‌ماند.
3. نقشهٔ فضا و جدول حجم کل زیرپوشه‌ها را نشان می‌دهند. برای ورود به یک پوشه روی نقشه کلیک کنید یا روی ردیف آن دوبار کلیک کنید.
4. از **فایل‌های بزرگ**، **فایل‌های قدیمی** و **نوع فایل‌ها** استفاده کنید. جست‌وجو زیرپوشه‌ها را هم بررسی می‌کند. فیلتر حجم، تاریخ و ترتیب نمایش در بالای جدول قرار دارند.
5. با Ctrl یا Shift چند ردیف را انتخاب کنید. **انتقال به سطل بازیافت** ابتدا فهرست دقیق مسیرها را برای تأیید نشان می‌دهد. کلید Delete همین پنجرهٔ تأیید را باز می‌کند.
6. فایل‌ها تا خالی‌کردن سطل بازیافت در ویندوز همچنان فضای دیسک را اشغال می‌کنند. پس از انتقال، اطلاعات به‌صورت خودکار به‌روز می‌شوند؛ کارت فضای آزاد درایو نیز هر ده ثانیه تازه می‌شود.

برای آزمایش: یک مسیر را اسکن کنید و پس از ذخیره برنامه را ببندید. یک فایل آزمایشی را حذف یا جابه‌جا کنید و برنامه را دوباره باز کنید. نتیجهٔ قبلی ابتدا نمایش داده می‌شود و تغییرات در پس‌زمینه اعمال و ذخیره می‌شوند. هنگام باز بودن برنامه هم تغییرات مسیر اسکن‌شده بدون فشردن Refresh دنبال می‌شوند. فایل‌های جدید معمولاً پس از چند ثانیه دیده می‌شوند؛ تعداد زیاد تغییرات، درایو کند یا بررسی کامل زمان بیشتری می‌خواهد.

تغییرات معمول در بسته‌های کوتاه پردازش می‌شوند. در شروع، پس از ازدست‌رفتن اعلان‌های ویندوز و هر پنج دقیقه، پایش خودکار یک بررسی کامل در پس‌زمینه انجام می‌دهد. Refresh دستی مستقل از این پایش است و فقط مسیر داخل کادر را بررسی می‌کند. روی درایو بزرگ این بررسی زمان می‌برد، اما نتیجهٔ ذخیره‌شده قابل مرور باقی می‌ماند.

## آپدیت داخل برنامه

در شروع برنامه و هر شش ساعت در زمان باز بودن آن، آخرین نسخهٔ پایدار از Releases همین مخزن بررسی می‌شود. در صورت وجود نسخهٔ جدید، اعلان بالای پنجره نمایش داده می‌شود؛ **دانلود و نصب** را بزنید تا پس از دریافت و بررسی فایل، برنامه بسته و با نسخهٔ جدید باز شود. **بررسی آپدیت** در نوار کناری نیز در دسترس است. هنگام اسکن یا پاک‌سازی، نصب تا پایان عملیات غیرفعال است. دانلود را می‌توان لغو کرد.

آپدیت به اینترنت و امکان نوشتن در پوشهٔ خود برنامه نیاز دارد؛ برای استفادهٔ قابل حمل، بسته را در پوشه‌ای متعلق به خودتان استخراج کنید. اسکن‌ها و زبان در LocalAppData نگهداری می‌شوند و با تعویض فایل اجرایی از بین نمی‌روند. قطع اینترنت جلوی استفاده از برنامه را نمی‌گیرد. کاربران نسخهٔ ۱.۲ باید یک بار نسخهٔ ۱.۳ یا جدیدتر را از Releases دریافت کنند؛ اعلان آپدیت از نسخهٔ ۱.۳ فعال است.

کلیک راست روی ردیف: ورود به پوشه، نمایش در Explorer و کپی مسیر. تاریخ ایجاد و تاریخ تغییر اصلی در توضیح شناور هر ردیف نمایش داده می‌شود. خروجی CSV شامل تمام نتایج فیلترشده است؛ جدول برای پاسخ‌گویی سریع‌تر حداکثر ۲٬۰۰۰ ردیف را نمایش می‌دهد.

اگر هنگام حذف در Explorer پیام **Folder In Use** ظاهر شد، پنجره‌ها یا برنامه‌هایی را که از آن پوشه یا زیرپوشه‌هایش استفاده می‌کنند ببندید و دوباره امتحان کنید. «نمایش در Explorer» مورد انتخابی را در پوشهٔ والد نشان می‌دهد و برنامه‌های دیگر را نمی‌بندد. پایش زنده با تغییر نام یا حذف پوشه سازگار است؛ این رفتار با یک پوشهٔ آزمایشی بررسی می‌شود.

برای حذف نرم‌افزارهای نصب‌شده، دکمهٔ **برنامه‌های نصب‌شده** تنظیمات مربوط ویندوز را باز می‌کند.

## رفتار اسکن و پاک‌سازی

- اسکن فقط فراداده را می‌خواند؛ فایل‌های شما را تغییر نمی‌دهد. شروع اولین اسکن با انتخاب شماست. پس از آن، نتیجهٔ بازشده به‌صورت خودکار دنبال می‌شود؛ سایر اسکن‌های ذخیره‌شده هنگام بازکردنشان بررسی می‌شوند.
- درایوهای محلی و USB آماده قابل اسکن هستند. مسیرهای شبکه پشتیبانی نمی‌شوند. هیچ محتوای فایلی به اینترنت ارسال نمی‌شود.
- حجم‌ها **منطقی** هستند و با واحدهای KiB / MiB / GiB نمایش داده می‌شوند. فشرده‌سازی، فایل‌های sparse و hard link می‌توانند باعث اختلاف با فضای اشغال‌شدهٔ واقعی شوند. hard linkها در هر مسیر شمرده می‌شوند.
- تاریخ پوشه برابر جدیدترین زمان تغییر خود پوشه یا محتوای آن است. تاریخ‌ها میلادی و به وقت محلی‌اند. قدیمی بودن یک فایل نشان‌دهندهٔ بی‌استفاده بودن آن نیست. نمای فایل‌های قدیمی از ۱۸۰ روز شروع می‌شود.
- مسیرهای غیرقابل‌دسترسی، junction، symbolic link و placeholderهای ابری با علامت ناقص گزارش می‌شوند؛ پیوندها دنبال نمی‌شوند. جزئیات حداکثر ۵۰۰ خطای اول قابل مشاهده‌اند. ردیف بدون دسترسی، اندازهٔ واقعی آن مسیر را نشان نمی‌دهد.
- اسکن قابل توقف است و نتیجهٔ متوقف‌شده واضحاً ناقص اعلام می‌شود. پاک‌سازی بر اساس اسکن متوقف‌شده فعال نمی‌شود.
- اسکن‌های پایان‌یافته به‌صورت فشرده در `%LOCALAPPDATA%\Spacio\Scans` ذخیره می‌شوند؛ خطاها و موارد بررسی‌نشده نیز حفظ می‌شوند. اسکن لغوشده نتیجهٔ کامل قبلی را جایگزین نمی‌کند. ذخیرهٔ نتیجه ممکن است بسته به تعداد فایل‌ها چند ثانیه طول بکشد و فضای دیسک مصرف می‌کند. اگر ذخیره ممکن نباشد، پیام مشخصی نمایش داده می‌شود و نتیجه در حافظهٔ همین اجرا باقی می‌ماند.
- زمان آخرین نتیجه کنار نقشهٔ فضا نمایش داده می‌شود. بررسی بدون تغییر، همان زمان و فایل ذخیرهٔ قبلی را حفظ می‌کند. نتیجهٔ خراب یا ناقص با اسکن خودکار جایگزین نمی‌شود؛ برای مسیر بدون نتیجهٔ معتبر، اسکن را بزنید. قطع دسترسی یا توقف Refresh نتیجهٔ سالم قبلی را پاک نمی‌کند. محتوای قبلاً شناخته‌شدهٔ مسیرهایی که دسترسی‌شان قطع شده حفظ و ناقص علامت‌گذاری می‌شود.
- پس از تلاش پاک‌سازی، نتیجه‌های مرتبط «تغییریافته» علامت می‌خورند. بررسی خودکار نتیجهٔ بازشده را به‌روز می‌کند؛ تا پایان بررسی، پاک‌سازی بعدی غیرفعال است. فایل انتخابی پیش از حذف دوباره بررسی می‌شود.
- ریشهٔ اسکن، مسیرهای سیستم و برنامه‌ها، AppData، پروفایل کاربران، محل اجرای خود برنامه و پوشه‌های حاوی موارد سیستمی یا بررسی‌نشده محافظت می‌شوند.
- قبل از انتقال، فرادادهٔ فایل یا تمام درخت پوشه با نتیجهٔ اسکن مقایسه می‌شود. تغییرات مشاهده‌شده عملیات را متوقف می‌کنند. این مقایسه قفل فایل یا snapshot اتمیک نیست؛ هنگام پاک‌سازی از تغییر هم‌زمان پوشهٔ انتخابی خودداری کنید.
- عملیات از Windows Shell و `IFileOperation` استفاده می‌کند. اگر ویندوز امکان بازیافت را نداشته باشد، callback پیش از حذف دائمی آن را رد می‌کند. حذف دائمی یا خالی‌کردن خودکار سطل بازیافت در برنامه وجود ندارد.
- انتخاب هم‌زمان پوشه و فرزندش فقط یک بار پردازش می‌شود.
- زبان در `%LOCALAPPDATA%\Spacio\language.txt` ذخیره می‌شود. گزارش CSV تنها در مسیر انتخاب‌شده توسط شما نوشته می‌شود.

## English

**Full results pages in 1.4.1:** Open Folders & files, Largest files, Older files or File types from the sidebar. The dashboard table also has **Open full page**. The results use the space occupied by the overview cards and map, retaining the same virtualized table, selection, search and filters. Live updates, folder navigation, cleanup and CSV export remain available. **Back to overview** restores the dashboard.

Download the portable ZIP from [Releases](https://github.com/aliahmadi1382/Spacio-Uninstaller/releases/latest), extract it, and run `Spacio.exe` alongside `Spacio.exe.config`. Requires 64-bit Windows 10/11 and .NET Framework 4.8. No installation or administrator access is required.

**Version 1.4 — live results:** Saved results appear first, then background validation reconciles edits made while the app was closed. The open scan is monitored for file/folder creation, deletion, rename, size/date and attribute changes. Notifications are batched; ordinary events read affected paths and rebuild aggregates on a worker. Large bursts may take longer. **Refresh** validates only the path in the input and its descendants, without clearing the table or blocking navigation. Opening a folder updates that path. A subfolder refresh keeps the surrounding cached drive scan and recalculates its ancestor totals from saved sibling data; it does not walk the rest of the drive. Drive free-space cards refresh every ten seconds. Opening another cached path switches live monitoring to that snapshot. Existing 1.2 caches remain readable; missing/corrupt caches do not trigger an automatic scan.

**Recovery checks:** Automatic background validation runs at startup, after notification overflow, and every five minutes to recover missed events (including sleep or removable-media interruptions). A whole-drive validation can take time; the saved view remains usable. Changes arriving during a refresh stay queued for the next pass. Cache and updater writes are excluded from notifications to prevent feedback. See Microsoft’s [FileSystemWatcher buffer documentation](https://learn.microsoft.com/en-us/dotnet/api/system.io.filesystemwatcher.internalbuffersize?view=netframework-4.8.1).

**Updates:** Spacio checks the official repository's stable GitHub Releases at startup and every six hours while running. You can also use **Check for updates** in the sidebar. The in-app notification offers **Download & install** and restarts the application after verification. Scans and language settings are retained. Installation waits for scanning/cleanup to finish, needs internet and a writable application folder, and runs without elevation. Download cancellation and network failure leave the installed app usable. Version 1.2 users need one manual download to gain the updater.

**Live startup in 1.3.2:** Rotating rings, a floating leaf, moving green light, particles, and a travelling highlight animate throughout startup. A separate WPF UI dispatcher keeps this scene responsive while the main window is constructed. A short 1.5-second introduction shows the ready state if loading finishes sooner, then fades into the workspace. The scan format and location remain unchanged. See Microsoft's [WPF threading model](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/advanced/threading-model) for the independent-window dispatcher pattern.

Packages are accepted only from the repository's release URL and approved HTTPS GitHub asset hosts. The updater checks size, the API's SHA256 digest, and assembly identity/version; it rechecks staged files before replacing the executable. An installed-code helper waits for the old process to exit. The previous executable/config are kept as `.spacio-<id>.previous.exe` and `.config` beside the application; config replacement is rolled back if executable replacement fails. An installation error is shown and logged under `%LOCALAPPDATA%\Spacio\Updates`. Scan data is never part of the update package. The update feed uses the [GitHub Releases API](https://docs.github.com/en/rest/releases/releases#get-the-latest-release).

Compressed, versioned snapshots remain under `%LOCALAPPDATA%\Spacio\Scans` with the same data-only format and atomic replacement. A refresh constructs a separate replacement tree and saves it before switching the displayed result. Cancellation, unavailable roots or failed saves keep the prior good result. Previously known contents of newly inaccessible paths are retained and marked incomplete. No-change validation preserves cache bytes and scan time. Cleanup still invalidates related caches and verifies every selected target.

Switch to **English** at the top of the window. Select a drive or folder to scan; explore the space map, drill into folders, and filter by size, last change, or file category. Search includes descendants. Use Ctrl/Shift to select multiple rows, then review the exact paths before moving them to the Recycle Bin. Space is reclaimed when you empty the Recycle Bin in Windows. Results update automatically after cleanup.

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
.\build.ps1 -Destination .\dist\v1.4
.\test.ps1 -Executable .\dist\v1.4\Spacio.exe
.\tools\package.ps1 -Executable .\dist\v1.4\Spacio.exe
```

`test.ps1` creates isolated fixtures under `.test-data`, including a junction to another generated fixture, paths longer than 260 characters, and 10,000 empty files. Its recycle integration checks move only generated test files and a generated test folder to the Recycle Bin. It leaves these fixtures available for inspection. Reports and off-screen Persian/English screenshots are saved to `artifacts`.

Live checks cover actual Windows create/delete/rename/resize notifications, startup reconciliation, incremental aggregate correctness, overflow fallback, cancellation, exact cache preservation and UI heartbeat responsiveness while a cache write is blocked.

Cache checks cover persistence, startup selection, offline paths, normalization, Unicode, descendant reuse, file changes, invalidation, corruption, cancellation, and atomic replacement. UI checks exercise the Refresh button against generated additions, moves, and deletions, then launch separate processes to verify automatic restoration and the actual loading-screen startup. UI checks use an isolated cache under `artifacts`, not your saved scans. Update checks cover release selection, trusted hosts, tampered packages, rollback, and a separate updater process replacing/restarting a generated application copy. No test installs over the user's app.

## Publishing updates

For each user-facing release, increment both versions in `src/AssemblyInfo.cs`, update this changelog, build, test, and run `tools/package.ps1`. Commit and push the source, tag that commit as `vMAJOR.MINOR.PATCH`, then publish a stable GitHub Release with `Spacio-MAJOR.MINOR.PATCH-win-x64.zip` and its SHA256SUMS file. Keep `Spacio.exe` and `Spacio.exe.config` at the ZIP root. Upload all assets to a draft before publishing it; the GitHub API must expose an asset SHA256 digest. Keep old release assets available for rollback. Commits alone do not notify installed apps; publishing the stable release makes the update discoverable. Use the Releases/latest link when sharing the app so users always reach the newest stable version.

After publication, `tools/verify-upgrade.ps1 -PreviousExecutable <path-to-an-older-Spacio.exe>` verifies the actual public feed, download, checksums, installation, and restart using that older release's updater. Its isolated startup smoke mode creates a saved scan with the old version and verifies restoration before live monitoring starts; the cache bytes, scan timestamp and original scanned file must remain unchanged. Use a previous version from 1.3.0 onward. The script creates isolated application copies under `.test-data`; it never replaces the supplied original executable. This integration check needs internet and a newer public release. Separate live tests verify subsequent automatic reconciliation. The loading smoke check also captures 48 rendered frames and verifies central-scene movement while the main UI thread is blocked.

Git tracks the full source, UI, translations, vector/raster brand assets, build scripts, tests, and documentation. Generated executables are distributed through Releases. Disposable test fixtures (`.test-data`), test reports/screenshots (`artifacts`), build output (`dist`), local credentials, and saved scan data are ignored. See [CHANGELOG.md](CHANGELOG.md) for version notes.

Full results navigation and minimum-size layout checks are in `src\ResultsPages.cs`.

Source files: `src\Core.cs` (scan/metadata/safety), `src\Recycle.cs` (Windows Shell interop), `src\Locales.cs` (language), `src\App.cs` (application), `src\MainWindow.xaml` (interface), `src\SelfTests.cs` (checks), `src\LiveScanner.cs` (incremental reconciliation and watcher), `src\LiveUI.cs` (live lifecycle), `src\LiveTests.cs` (live integration checks).

Brand assets: `assets\spacio.svg` and `assets\BrandMark.xaml` contain the vector leaf/storage-map mark. `tools\build-brand.ps1` renders the matching PNGs and a seven-resolution Windows ICO (16–256 px). The icon and vector mark are embedded in the executable; no separate logo file is required at runtime. The language loader also reads the prior version's saved preference when the new settings file does not exist.

The Windows integration follows Microsoft's [IFileOperation documentation](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nn-shobjidl_core-ifileoperation) and [operation flags](https://learn.microsoft.com/en-us/windows/win32/api/shobjidl_core/nf-shobjidl_core-ifileoperation-setoperationflags).
