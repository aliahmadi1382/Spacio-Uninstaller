# Spacio project instructions

## Cleanup behavior

The user selected permanent deletion as the default, after a detailed preview
and explicit confirmation every time. Keep recycling as a separate selectable
mode with no permanent fallback. Additional/shared/unverified cache candidates
must not be preselected. Only exact supported cache directories may use the
narrow AppData exception; never treat a folder name alone as proof. Preserve
Spacio scans/preferences and reject incomplete, linked, cloud and system targets.
Use registered uninstallers for applications and require separate review of
remaining caches. Do not claim an exhaustive per-file cache association.
Verify destructive behavior using generated isolated fixtures only.

## Version control workflow

The user has explicitly authorized committing and pushing completed changes to
this repository as part of every future development task. This standing
authorization applies without asking for confirmation again, unless the user
changes it.

- Repository: https://github.com/aliahmadi1382/Spacio-Uninstaller.git
- Remote: `origin`; default development branch: `main`.
- After each completed change, run the checks appropriate to the change, review
  the diff, and commit all task-related source, assets, scripts, and documentation
  with a clear message. Keep generated builds, scan data, credentials, and
  disposable test output out of Git according to `.gitignore`.
- Push the commit to `origin` on `main`, or to another branch explicitly selected
  by the user. Commit and push are part of completing the task, not optional
  follow-up work.
- Preserve existing user changes and published history. Do not force-push,
  overwrite remote work, or rewrite published commits. Use new commits for fixes
  and reversions so earlier versions remain recoverable.
- If the remote has changed, inspect and reconcile the changes before pushing.
  If a check, commit, or push fails, resolve it when possible and accurately
  report any remaining failure; never claim synchronization without verifying it.
- After pushing, verify the repository status and briefly report the result to
  the user in Persian, including the commit identifier when useful.

## Distributing app updates

The user wants other users to receive updates inside Spacio. For completed
user-facing app changes, increment the assembly/file versions together, update
`CHANGELOG.md`, build and run appropriate checks, and package with
`tools/package.ps1`. Publish a stable GitHub Release on the matching version tag
after pushing the tested source. Build Setup using `tools/build-setup.ps1` after
packaging, and verify it with `tools/verify-setup.ps1`. Upload the versioned ZIP,
Setup executable and checksum file to a
draft first, then publish the complete release. Follow the naming and packaging
contract documented in `README.md` so installed apps can discover and verify it.
Keep older releases and their assets for rollback. Documentation-only changes
need a commit and push but do not need an app release.

This is a workflow for work performed in this project; it does not require a
background watcher or a recurring automation.

Setup installs per user under `%LOCALAPPDATA%\Programs\Spacio`. Preserve its
shortcuts and uninstaller during ZIP-based updates. Uninstall only the fixed
installed-file list and owned shortcuts, retaining saved data and unknown files.
Test installation/removal only with isolated directories and registry keys.

## Preserve saved scans across updates

The user explicitly requires scans to survive application updates. Keep their
storage independent of the executable/version directory. Updates must not remove
or overwrite `%LOCALAPPDATA%\Spacio\Scans`, preferences, or scanned user files.
Keep existing cache formats readable. If a future format change requires a
migration, preserve the original data until a complete replacement is verified.
Open the saved result first, then reconcile filesystem changes in the background
at startup and monitor the open scan live, as requested by the user. Never clear
the displayed result or overwrite a good cache with a cancelled/failed refresh.
Unchanged validation must retain the original scan timestamp and cache bytes.
Manual Refresh must only rescan the path shown in the path field and its
descendants. Folder navigation must update that field. Recalculate ancestor
totals from the cached tree without scanning siblings or widening to the drive.
Automatic monitoring, startup reconciliation, periodic validation and overflow
recovery must also follow the currently viewed folder, not its cached ancestor.
Changing folders must cancel an obsolete automatic refresh and retarget monitoring.
Manual Refresh must be able to supersede an active automatic refresh. An
unavailable watched folder must retain the saved view without widening its scope.
Verify upgrades with
`tools/verify-upgrade.ps1`, which checks cache bytes, the original scan timestamp,
user-file contents, and restoration in the newly installed app.
