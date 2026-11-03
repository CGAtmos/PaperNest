# Changelog

## 1.7.1 — 2026-11-03

First public open-source release.

### Fixed

- Double-clicking the tray icon now opens or focuses one Note Library window. It no longer unfolds all previously hidden notes.
- Repeated and rapidly queued double-clicks do not create notes or additional library windows. A minimized library is restored.
- Opening a fully hidden workspace now opens the library and preserves the hidden notes.
- Right-button double-clicks do not trigger the library action.

- Concurrent readers no longer block atomic note-index and draft saves during rapid app commands.

### Included

- English and Simplified Chinese, live language switching and bilingual installation.
- Text, photo and checklist notes; images, resize, content zoom, tilt and paper decorations.
- Per-note always-on-top and locking, with hover controls.
- Local autosaving, content drafts, history, recoverable trash and backup export.
- Tray controls, optional startup at sign-in, desktop creation menu and About with Kaysar attribution.

### Validation

The Windows release passed 82 core checks across six regression suites and 33 installer/lifecycle checks. Three complete exit/reopen cycles preserved the existing workspace on the creator's machine. These checks do not imply certification across every Windows configuration.

## Before the public release

Versions through 1.7.0 were developed and tested locally. The public repository does not include private note data or local recovery snapshots.
