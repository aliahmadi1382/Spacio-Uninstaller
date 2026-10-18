# Changelog

## 1.7.1

- Keep Download & install available during automatic folder checking. Pause the live watcher and cancel/await the current automatic refresh before downloading, preserving the displayed result and saved scan. Manual scans, cleanup and deletion reviews still finish before installation.
- Hold the update's busy state through refresh completion so timer ticks, manual Refresh, cleanup and repeated installation cannot start competing work. Show bilingual preparation status, and restore scoped monitoring after cancellation or download failure while respecting Pause live.
- Add generated-fixture UI checks for updates during blocked cache writes, cancellation before/during download, retry after network failure, preserved cache bytes/timestamp, and resumed monitoring. The saved-scan format, Setup layout and verified update package contract are unchanged.

## 1.7.0

- Automatically follow app uninstallers launched from Spacio and open an app-specific leftovers page after a successful process exit and three stable checks of registration absence. Early bootstrap exits, cancelled/failed uninstallers and unavailable status are not reported as success; provide a labelled manual inspection fallback and stop-following control without terminating processes.
- Capture the app identity, installation directory, supported data locations and exact-name suggestions before uninstall registration disappears. Show remaining paths, logical sizes, file counts and association evidence. Keep all candidates unchecked, label possible/shared data, exclude unrelated apps and allow reopening the last uninstall within the current session.
- Support Notion's Roaming data and Cache/Code Cache/GPUCache paths. Refresh the scoped leftovers after confirmed removal; detect newly created candidates and drop removed paths. Keep full preview, data-loss acknowledgement and a separate final deletion confirmation.
- Recheck registration, overlapping installations, running ownership and native file identities before cleanup. Limit the Program Files exception to a captured dedicated installation after uninstall; retain protections for shared Windows components and Spacio data. Never remove registry entries or broaden scanning to an entire drive.
- Verify delayed/early exits, cancelled/unknown status, a real generated helper process, unchecked discovery, exact sizes, similar-name exclusions, shared data warnings, changed previews and bilingual end-to-end selection/removal using isolated fixtures. Saved-scan formats and updater contracts are unchanged.

## 1.6.0

- Allow full removal of explicitly selected application-data folders after a fresh preview and separate data-loss acknowledgement. Keep automatic cache discovery narrow and additional/shared/unverified candidates unchecked.
- Preview recognized junctions and symbolic links as link objects, showing their destinations without enumerating them. Permanent removal unlinks only those objects; their target files remain untouched. Linked trees cannot silently fall back from recycling to permanent deletion.
- Revalidate native file identities, reparse metadata and complete directory membership before removal. Pin ancestor/directory handles against replacement, delete through no-follow handles, support confirmed read-only entries, and report the failed path and partial progress. Preserve system/cloud data, profile containers, volume mounts and Spacio storage; never terminate owners or change ACLs.
- Detect the supported default Hermes Agent installation from its launcher and project/uninstaller metadata even without Windows uninstall registration. Open its interactive vendor uninstaller with instructions to choose Full uninstall for program plus data. Recheck discovery before launch, keep remaining data/cache review separate, and use a working directory outside the application being removed.
- Display complete blocking reasons and data-loss notices in Persian/English, add link details and keep confirmation controls visible on short displays. Preserve scan formats, saved results and existing update behavior.
- Verify removal only with isolated fixtures: AppData, changed previews, locked/read-only files, junction destinations, replaced identities, cancellation, protected scan data, supported-uninstaller command construction and bilingual confirmation UI. No installed user application is uninstalled by these tests.

## 1.5.2

- Recognize applications such as Notion whose uninstall registration omits InstallLocation, using agreeing registered uninstaller and executable-icon paths under a dedicated standard program directory. Preserve exact path boundaries and reject missing/mismatched/shared/linked fallback evidence.
- Route application reviews to a prominent registered-uninstaller action and Windows Settings. Hide file-deletion controls for app removal; retain separate review of remaining caches and all AppData protections.
- Treat root Changed notifications as metadata instead of unconditional whole-tree scans. Debounce rapid event batches and add a cooldown after costly automatic work, with increasing delays after repeated refresh errors.
- Add bilingual Pause live / Resume live controls and distinct waiting/queued states. Pausing cancels automatic work and releases its watcher without clearing results; manual Refresh remains scoped and usable. Resume validates the current folder. Keep five-minute scoped recovery checks.
- Verify Notion-shaped registration and the local Notion registration without launching an uninstaller. Add application-action UI checks, duplicate/root-event checks, idle cache preservation and pause/manual-refresh/resume integration tests. Stabilize generated rename/arrival fixture timestamps.
- Make Setup self-tests coexist with a running user copy: verify isolated saved scans/preferences across upgrade, uninstall and reinstall, and explicitly omit real-data immutability assertions while live Spacio owns those files.

## 1.5.1

- Add a bilingual Windows Setup executable with the green Spacio icon, default desktop shortcut, Start Menu entry and Windows Installed apps registration. Install per user without administrator access.
- Keep the existing ZIP-based in-app updater compatible with Setup installations; retain shortcuts/uninstaller and refresh the registered version on app startup. Preserve scans, preferences and user files across installation, updates, reinstall and uninstall.
- Verify embedded payload hashes, reject downgrades, preserve unrelated files/shortcuts, serialize Setup operations and roll back failed installation/removal. Never force a running application closed.
- Keep deletion review available during automatic validation. Cancel and await automatic work, then freshly inspect only eligible selected targets before review, including from stale saved views; keep unrelated cached branches untouched and all existing system/link/cloud/incomplete protections.
- Fit cleanup confirmation to the available monitor size and keep the confirmation controls visible on short displays. Keep permanent deletion dependent on acknowledgement and confirmation each time.
- Test actual confirmed fixture deletion through the selection UI, automatic-refresh cancellation, stale-view revalidation, short-display layout and Setup/updater integration. Destructive checks use generated isolated fixtures only.

## 1.5.0

- Add full-height Cache and Installed apps pages in Persian and English, with search, paths, logical sizes, file counts, ownership and explicit known/shared/unverified classifications.
- Replace direct recycling with a review dialog listing every identified target and its complete scanned contents. Default to permanent deletion, requiring acknowledgement and confirmation each time; recycling remains an option and still vetoes permanent fallback.
- Discover supported current-user browser, Electron, Adobe media and GPU caches using exact known directories. Additional/shared caches and name-only candidates are not selected automatically. Keep profile data, cookies, passwords, local storage and Spacio saved scans outside cache cleanup.
- Use registered desktop-app uninstallers, with command review and registration revalidation; remaining caches require a separate review after the uninstaller finishes. Keep Windows Settings available for unsupported apps and commands.
- Verify reviewed metadata again before deletion, reject changed/incomplete/system/link/cloud targets, block known running cache owners and report locked files without forcing processes to close. Keep cache root folders and delete only reviewed contents.
- Reconcile affected saved subtrees after cleanup and retain the scan format/location. Pause file monitoring while browsing cleanup pages; returning resumes the current folder's monitoring.
- Add isolated permanent-delete, stale-preview, locked-file, junction, ownership, command-parser and bilingual UI checks. No real user files are deleted and no installed apps are uninstalled by the tests.

## 1.4.2

- Fix automatic startup, periodic and missed-notification checks refreshing a cached whole drive while a subfolder is open. Watch and reconcile only the visible folder; keep the surrounding saved tree and recalculate its totals.
- Cancel obsolete automatic work when navigating to another folder, suppress its late progress messages and immediately show the new monitoring path.
- Keep the path input, folder picker and manual Refresh usable during automatic work. A manual request cancels and awaits the automatic transaction before refreshing its requested path.
- Keep an unavailable watched folder's saved view instead of falling back to a parent scan; validate watcher paths against junctions and offline placeholders.
- Reproduce the 1.4.1 bug and verify the fix against identical isolated Adobe-shaped folders. Cover startup, periodic checks, notification overflow, navigation cancellation, manual supersession and real scoped filesystem events.
- Preserve scan storage and cache format; stabilize the no-change test fixture with explicit directory timestamps.

## 1.4.1

- Limit manual Refresh to the path in the input and its descendants, including when that folder belongs to a cached whole-drive scan. Recalculate ancestor totals from the refreshed subtree and saved siblings.
- Keep the path input synchronized with folder navigation; retain unrelated pending live events and preserve inaccessible saved contents during a folder refresh.
- Give Folders & files, Largest files, Older files and File types dedicated full-height results pages with sidebar navigation and a return to Overview.
- Add Open full page to the dashboard table; reclaim the metric/map area for browsing without resetting the table, selection, search or filters.
- Keep live updates, folder navigation, cleanup controls and CSV export available on the expanded pages; preserve the active category when changing language.
- Widen count, date and status columns so larger values are easier to read; compact sidebar navigation and the read-only notice to keep drive cards visible at the minimum window size.
- Verify the enlarged table at the minimum window size in Persian and English, live updates, scoped Refresh through the actual button, saved-scan restoration and renaming/deleting a watched test folder.

## 1.4.0

- Keep the existing table, navigation, filters and map available during Refresh; perform drive queries, result preparation and persistence on workers.
- Restore saved scans first, then automatically reconcile changes made while the app was closed.
- Monitor the open scan for creates, deletes, renames, size/date and attribute changes; batch notifications and read affected paths instead of rescanning the whole drive for ordinary events.
- Add Persian/English live status and refresh drive free-space cards every ten seconds. Validate in the background on startup, Refresh, notification overflow and every five minutes as a recovery check.
- Build replacement trees independently and atomically save them; cancellation, unavailable roots and failed writes retain the last good snapshot. Preserve previously known contents of newly inaccessible paths and mark them incomplete.
- Keep the existing cache format, upgrade restoration and unchanged scan timestamps; exclude Spacio cache/update writes from notification feedback.
- Avoid redundant metadata queries during directory enumeration. Verify real filesystem notifications, off-time changes, incremental totals, queue overflow, cancellation, saved bytes and UI responsiveness during a blocked cache write.

## 1.3.2

- Replace the subtle startup effect with continuous rotating rings, a floating leaf logo, moving green light, drifting particles, and a light sweep.
- Run the loading window on its own UI dispatcher so animation continues during main-window initialization and scan restoration.
- Add a short 1.5-second introduction and a soft transition into the workspace; display the ready state once restoration finishes.
- Verify actual rendered animation frames while the main UI is blocked and stop the animation dispatcher on close.
- Extend the real-release upgrade check to verify unchanged saved scans, scan timestamps, and scanned files, then restore the old result in the new app without rescanning.

## 1.3.1

- Update drive free-space cards after a successful Refresh, alongside the persisted file/folder results.

## 1.3.0

- Automatically restore the last viewed saved scan at startup without walking the drive again, including existing 1.2 caches.
- Add a green animated loading screen in Persian and English while the workspace is restored.
- Rename Rescan to Refresh and verify that it persists file additions, deletions, moves, and size changes.
- Wait for a completed scan to finish saving when the user closes the window during persistence.
- Check official GitHub Releases at startup and every six hours while running, with a manual check button and an in-app update notification.
- Download and install updates inside the app, verifying HTTPS origins, package size, SHA256, and executable version before replacement.
- Restart after installation, preserve saved scans, retain the previous executable/config, and roll back config replacement on failure.
- Add update integration checks, actual splash/startup checks, and a repeatable portable packaging script.

## 1.2.0

- Persist completed scans and reopen saved results after restarting the application.
- Reuse saved ancestor scans for subfolder navigation.
- Add a separate Rescan action and show the scan timestamp.
- Preserve complete results when a rescan is cancelled.
- Mark affected saved results stale before cleanup and retain deletion-time verification.
- Add cache validation, corruption handling, atomic writes, and restart checks.

## 1.1.0

- Rename the application to Spacio / اسپاسیو.
- Add the green leaf and storage-map logo, vector assets, and Windows executable/window icons.

## 1.0.0

- Introduce the native Windows storage explorer with Persian and English interfaces.
- Add folder totals, a space map, large/old file views, type grouping, search, and CSV export.
- Add read-only scans, cancellation, skipped-path reporting, and user-confirmed recycling.

The repository starts with the complete 1.2.0 source tree; these earlier version notes describe development before Git tracking was enabled.
