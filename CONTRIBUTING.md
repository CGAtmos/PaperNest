# Contributing to PaperNest

Thanks for helping improve PaperNest. English and Simplified Chinese are both welcome in issues and pull requests.

## Report a bug

Include the app version (About PaperNest), Windows version, display scaling, steps to reproduce, expected behavior and actual behavior. For saving problems, say whether the app was hidden, quit normally or terminated unexpectedly. Share only a minimal fictional note that reproduces the problem; remove personal text, images and paths from attachments.

## Make a change

1. Fork the repository and create a branch for one focused change.
2. Build with `build.ps1` on Windows. Use a separate `--data-dir` while developing.
3. Add a meaningful regression check for storage or interaction bugs. Run `test.ps1` and, when installer/integration behavior changes, `package.ps1` followed by `qa.ps1`.
4. Check both English and Chinese text in the relevant surfaces. Translations live in `src/L.cs`.
5. Open a pull request describing the problem, resulting behavior and validation. Include screenshots for visible UI changes.

Keep compatibility with the .NET Framework 4.8 compiler used by the scripts. Do not commit real notes, recovery data, `qa/`, `build/`, logs, local paths or credentials. Do not change storage formats without migration and recovery tests.

## Project map

| Path | Purpose |
| --- | --- |
| `src/App.cs` | App lifetime, tray, single-instance commands, window management |
| `src/NoteWindow.cs` | Text, checklist, photo and per-note interaction |
| `src/Model.cs` | Local storage, drafts, backups and images |
| `src/History*.cs` | History grouping, preview and recovery |
| `src/L.cs` | English / Chinese interface strings |
| `src/Artwork.cs` | Native paper rendering and UI helpers |
| `Installer.cs` | Per-user installer and uninstaller |
| `src/*Regression.cs`, `tests/` | Isolated regression and integration utilities |

To recreate the public demo images, run `render-gallery.ps1`. It uses fictional English notes in a fresh test directory. Code contributions are made under the project's MIT license; preserve third-party notices and licenses.
