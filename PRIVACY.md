# Privacy Policy

**Effective date:** 7 October 2026
**Applies to:** the Pouchy app for Windows, all versions from 1.0.0 on, downloaded from this repository's [releases](https://github.com/editorrylix/Pouchy/releases).

## The short version

- **No accounts, ads, analytics, telemetry or crash reporting.** Pouchy doesn't collect anything about you or send it to anyone, including the developer.
- **Your data stays on your PC.** Everything Pouchy keeps is stored in folders on your own computer.
- **Pouchy only goes online for two optional features.** Both can be turned off:
  - **Link previews:** a web page's title and icon for links you add.
  - **Update checks:** asking GitHub whether a new version exists.
- **Clipboard history is off unless you turn it on,** and it never saves copies that password managers mark as private.

The rest of this page explains each of these in detail.

## 1. What Pouchy stores on your PC

Pouchy keeps its data in `%AppData%\Pouchy` (for example `C:\Users\you\AppData\Roaming\Pouchy`). Nothing in this folder is uploaded anywhere.

| What | Where | Contains |
| --- | --- | --- |
| **Your shelves** | `shelf_state.json` | The items in your pouch: for files and folders, their **path** (not a copy of the file); for notes, links and colours, their **text**; plus labels, pins and when each item was added |
| **Pictures** | `images\` | Images you paste or drop, screenshots you take with Pouchy, and the icons of websites you've added as links |
| **Settings** | `settings.json` | Your preferences, including the list of folders you recently sent items to |
| **Your additions** | `themes\`, `actions\`, `sounds\` | Themes, scripts and sound files you put there yourself |
| **Log** | `debug.log` and `debug.old.log` | A technical log for troubleshooting (see below) |

Pouchy also uses a few other places on your PC:

- **Temporary files** in `%TEMP%\Pouchy` (an image saved for the Windows share sheet) and `%TEMP%\Pouchy-update` (a downloaded update, deleted after it's installed).
- **Windows registry entries for the current user**, only for features you turn on (or that the installer sets up): starting with Windows, the **Add to Pouchy** Explorer menu, and the installer's entry in **Apps & features**.

**These files are not encrypted.** Text and images in your shelves, including clipboard history, are stored as ordinary files that anyone using your Windows account can open. Don't keep secrets in Pouchy.

### The log file

`debug.log` records what Pouchy does, so problems can be fixed. It can include:
- file and folder names and paths
- web addresses of links whose preview failed
- names of apps Pouchy was told to ignore
- error messages

It never leaves your PC by itself. It's only shared if you attach it to a bug report yourself, so read it first and remove anything you'd rather not share.

The log doesn't grow forever: when it gets large it becomes `debug.old.log` and a new one starts, replacing any earlier old log.

## 2. What Pouchy reads while it runs

| What | Why | What happens to it |
| --- | --- | --- |
| **Mouse movement and buttons** | To notice the shake and screen-edge gestures that open the pouch | Checked as it happens, never recorded |
| **Whether Ctrl, Shift or Alt is held** | Only while you drag, for the optional "hold a key while dragging" gesture | Checked as it happens, never recorded. Pouchy never records what you type. Its keyboard shortcuts are registered with Windows, which tells Pouchy only when that exact shortcut is pressed. |
| **The app in front** | To stay closed over fullscreen apps and apps you chose to ignore | Checked as it happens, never recorded |
| **Files you add** | To show their name, size and a thumbnail | The thumbnail comes from Windows. Pouchy stores only the path. |
| **The clipboard** | Only when you paste into Pouchy, when you take a screenshot with Pouchy, or when **clipboard history** is turned on | See the next section |

### Clipboard history

Clipboard history is **off by default**. When you turn it on:

- Pouchy saves what you copy (text, links, images and file references) to a **Clipboard** shelf on your PC.
- **Copies marked private are skipped.** Pouchy recognises the standard Windows markers that password managers and other apps use for this.
- **Pouchy's own copies are skipped.**
- **Only a limited number is kept** (50 by default), and older ones are removed.
- **Turning it off** stops collection straight away. Items already saved stay on the shelf until you remove them.

## 3. When Pouchy connects to the internet

Pouchy contacts these two services only, and never sends your files, shelves or settings to either.

### Link previews (optional)

When you add a web address to the pouch, Pouchy downloads that page once to read its title and icon, the same way a browser would.
- **What the website sees:** your IP address and a standard request.
- **What Pouchy keeps:** only the title and the icon.
- **Turn it off:** in **Settings → General → Fetch link titles and icons**. The link then shows its web address instead.

### Updates (optional)

- **How often:** about once a day, Pouchy asks GitHub's public API whether a newer release exists.
- **What GitHub sees:** your IP address and Pouchy's version number. No identifier is sent.
- **Installing an update:** downloads the release files from GitHub and checks them against the published SHA-256 checksums.
- **Turn it off:** in **Settings → About → Check for updates automatically**. You can still check by hand.

Requests to GitHub are covered by the [GitHub Privacy Statement](https://docs.github.com/site-policy/privacy-policies/github-general-privacy-statement). Requests to websites for link previews are covered by those websites' own policies.

### Things Windows does for Pouchy

- **Text recognition** ("Extract text") runs on your PC using Windows' built-in OCR. Images are not uploaded.
- **The share sheet** and **screen snip** are Windows features. If you share an item, it goes only to the app or person you choose.
- **Scripts in the actions folder** run with your permission and can do whatever the script is written to do. Only use scripts you trust.

## 4. Your control over your data

- **See it:** **Settings → Advanced → Open data folder** opens `%AppData%\Pouchy`.
- **Remove items:** remove them from the pouch, use **Clear**, or turn on **Remove old items** in Settings to have them deleted automatically.
- **Turn off online features:** link previews and update checks, as described above.
- **Delete everything:** uninstall Pouchy (installer version: **Windows Settings → Apps**), then delete the `%AppData%\Pouchy` folder. Uninstalling removes Pouchy's registry entries. Your files on disk are never touched by uninstalling.

Since no data is sent to the developer, there is nothing held by the developer to request, correct or delete.

## 5. Children

Pouchy doesn't collect personal information from anyone, including children.

## 6. Changes to this policy

If a future version of Pouchy changes how it handles data, this policy will be updated before or with that release. The change will be listed in the [changelog](CHANGELOG.md), and the effective date at the top will change. You can see every past version of this file in the repository's history.

## 7. Contact

Questions about privacy in Pouchy? [Open an issue](https://github.com/editorrylix/Pouchy/issues/new/choose) on GitHub. Please don't include personal information in a public issue.
