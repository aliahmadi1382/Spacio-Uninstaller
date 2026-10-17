# Changelog

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
