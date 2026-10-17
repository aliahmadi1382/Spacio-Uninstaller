# Changelog

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
