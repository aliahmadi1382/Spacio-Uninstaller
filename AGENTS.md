# Spacio project instructions

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
after pushing the tested source. Upload the versioned ZIP and checksum file to a
draft first, then publish the complete release. Follow the naming and packaging
contract documented in `README.md` so installed apps can discover and verify it.
Keep older releases and their assets for rollback. Documentation-only changes
need a commit and push but do not need an app release.

This is a workflow for work performed in this project; it does not require a
background watcher or a recurring automation.
