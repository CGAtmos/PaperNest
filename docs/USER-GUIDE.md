# PaperNest user guide

[Project home](../README.md) · [中文首页](../README.zh-CN.md)

## Your first notes

Start PaperNest from the desktop shortcut or Start menu. The first run creates three sample notes: a checklist, a photo and a text note. Click the title or body to start writing. You can keep, hide or move those samples to the trash.

Create your own note from the tray's right-click menu, a note's **⋮** menu, or the desktop context menu. Choose **text**, **checklist** or **photo**. The **+** button creates a text note.

## The system tray and library

The small PaperNest icon lives near the clock, sometimes inside Windows' hidden-icons area. **Double-click it to open the Note Library.** Repeated double-clicks focus the same library window and do not create notes or unfold hidden notes.

The tray's right-click menu also offers **Show all notes**, **Hide all notes**, **Recover Notes**, **Settings**, **About** and **Quit PaperNest**. Show all is an explicit action that reveals existing notes outside the trash.

The **×** button on a paper hides that paper; it does not delete it. Open it again from the library. To remove a note from normal use, choose the trash action in its menu. The library's **Trash** view can restore it.

When you launch PaperNest again, notes keep their saved content and layout. If every note is hidden, the library opens and the papers stay hidden. To stop the app entirely, choose **Quit PaperNest** from the tray menu.

## Writing, checklists and images

**Text notes:** click the body to write. Paste text with Ctrl+V. Paste a screenshot or image from the clipboard, drag an image file into the note, or use Add image in its menu. Text and images can share one note.

**Checklists:** check a box to mark an item complete. Enter adds an item; Shift+Enter adds a line inside an item. Pasted multiline text stays intact within the item rather than discarding text after the first line.

**Photo notes:** choose a picture and write a caption. Double-click the photo to replace it. When the note is locked, double-click opens the original image instead.

Paper edges, icons and text are rendered natively as you resize. Photos keep their source pixels; enlarging a small photo cannot add detail that was not in the original.

## Arrange and protect your papers

Drag an empty part of the top area to move a paper. Drag the lower-right corner to resize it. The **⋮** menu contains paper colors, tape/clip decorations, content size, tilt and **Always on top**. The tilt control can choose a random angle as well as a manual angle.

Hover over a paper to reveal its controls. Moving away hides them. Controls remain reachable when navigating with Tab or while a menu is open.

Click the lock button or press **Ctrl+L** to lock that note. Its text, images, task state, position, size, content zoom and tilt become protected from accidental edits. You can still read it, select and copy text, hide it and change always-on-top. Unlock using the same button, shortcut or context menu. Locking is not encryption or password protection.

## Language and Windows preferences

Open **Settings → Language**, choose English or 简体中文, and select **Save settings**. Labels update immediately; your writing is never translated or replaced.

**Start with Windows** is optional and off by default. **Show visible notes at startup** controls whether saved visible papers appear at Windows sign-in. The desktop context-menu integration is also optional. On Windows 11, the classic PaperNest menu may be under **Show more options**.

## Saving and recovering content

You do not need to press Save after each edit. Watch the small save-status line at the bottom of the paper. Ctrl+S forces an immediate save. Normal quit also saves; if saving fails, PaperNest stays open and reports the error.

Use the right recovery tool for the job:

| Situation | Where to go |
| --- | --- |
| I closed a paper with × | Note Library → find it → Open |
| I put a paper in the trash | Note Library → Trash → Restore |
| I need something I wrote earlier | Recover Notes → search → preview a version → recover |
| I am moving computers or reinstalling Windows | Settings → Export backup |

Recovering a historical version creates a separate paper while retaining current content. Repeating the same recovery reopens the recovered copy. Older text-only records are identified as such, so a current image is not presented as an old photo.

Data lives in `%LocalAppData%\PaperNest\Data`. Keep the complete folder together: the note index, images, documents, drafts and document versions refer to each other. Uninstalling the app preserves this data. Exported backups include local content and should be treated as private. Keep a copy outside the same computer; local history alone cannot protect against disk loss.

## Keyboard shortcuts

| Shortcut | Action |
| --- | --- |
| Ctrl+N | New text note |
| Ctrl+Shift+N | New photo note |
| Ctrl+L | Lock / unlock current note |
| Ctrl+mouse wheel | Change content size |
| Ctrl+W | Hide current note |
| Ctrl+S | Save now |

## Troubleshooting

**I cannot see a note:** open the library before creating another. Search its title or contents, and check Trash. A hidden paper is still saved.

**I cannot edit or move a note:** check its lock status. Hover to show the lock control, or use Ctrl+L.

**Saving reports an error:** leave the app open, check available disk space and the data folder's write access, and export a backup if possible. Avoid force-ending it while you have unsaved changes.

**The app will not start:** check that .NET Framework 4.8 is installed and use the latest release. Report the version and exact error, with personal paths and note contents removed. The current installer is unsigned.

**I found a bug:** use [GitHub Issues](https://github.com/CGAtmos/PaperNest/issues). Include clear steps, Windows version and display scaling. Use fictional text in shared examples.

## 中文速查

- 双击托盘图标打开收纳盒；重复双击不会新建便签。
- × 只是收起；托盘右键里的“彻底退出纸间”才会结束程序。
- 设置里选择简体中文后保存，立即生效，不影响你写的内容。
- 小锁或 Ctrl+L 可锁定 / 解锁单张便签，防止误改与误拖动。
- 收纳盒找回收起或放入废纸篓的便签；“找回便签”恢复过去的内容版本。
- 日常自动保存，换电脑或重装前请在设置中导出备份。
