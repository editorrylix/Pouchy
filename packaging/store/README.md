# Microsoft Store package

This folder builds Pouchy as an MSIX package for the Microsoft Store, for **x64 and ARM64** PCs running Windows 10 (2004) or later and Windows 11.

| File | What it is |
| --- | --- |
| `Package.appxmanifest` | The package manifest (identity filled in at build time) |
| `store-identity.json` | Your app's identity from Partner Center |
| `Assets\` | Logo images in every size Windows uses (made by `make_assets.py`) |
| `build-msix.ps1` | Builds the `.msix` files, the `.msixbundle` and the `.msixupload` |
| `install-test.ps1` | Installs a test-signed build on your PC, to try it before submitting |

## 1. Fill in your app identity (once)

1. In [Partner Center](https://partner.microsoft.com/dashboard), open your Pouchy app → **Product management** → **Product identity**.
2. Copy these three values into `store-identity.json`:

   | Partner Center | `store-identity.json` |
   | --- | --- |
   | Package/Identity/Name | `IdentityName` |
   | Package/Identity/Publisher | `Publisher` (starts with `CN=`) |
   | Package/Properties/PublisherDisplayName | `PublisherDisplayName` |

To have GitHub build the Store package for each release, also add them as repository **variables** (Settings → Secrets and variables → Actions → Variables): `STORE_IDENTITY_NAME`, `STORE_PUBLISHER` and `STORE_PUBLISHER_DISPLAY_NAME`. The release workflow then attaches `microsoft-store-package` to the run.

## 2. Build

```powershell
powershell -ExecutionPolicy Bypass -File packaging\store\build-msix.ps1
```

This takes a few minutes. The first run downloads Microsoft's packaging tools (the `Microsoft.Windows.SDK.BuildTools` NuGet package) into `.tools\`. The results are in `dist\msix\`:

- `Pouchy_1.1.0.0_x64_arm64.msixupload`: **upload this to the Store**
- `Pouchy_1.1.0.0_x64_arm64.msixbundle`: the same bundle without the upload wrapper
- `Pouchy_1.1.0.0_x64.msix` and `Pouchy_1.1.0.0_arm64.msix`: the individual packages

Useful options:
- **Version:** taken from `src\Pouchy\Pouchy.csproj`. Use `-Version 1.2.0` to choose a different one; the package version becomes `1.2.0.0`.
- **One architecture:** `-Architectures x64`.

The Store signs the package itself, so the upload doesn't need a certificate.

## 3. Try it on your PC (optional, recommended)

```powershell
powershell -ExecutionPolicy Bypass -File packaging\store\build-msix.ps1 -TestSign
# then, in an administrator PowerShell:
powershell -ExecutionPolicy Bypass -File packaging\store\install-test.ps1
```

1. **Start it:** open **Pouchy** from the Start menu.
2. **Quit the other version first.** If the installer or portable version is running, quit it, because only one Pouchy runs at a time and they share settings.
3. **Remove it when done:** `Get-AppxPackage *Pouchy* | Remove-AppxPackage`. To also remove the test certificate, delete it from **Manage computer certificates** → Trusted People.

## 4. Submit

In Partner Center, start a submission:

- **Packages:** upload the `.msixupload`. Device family: **Windows 10/11 Desktop**.
- **Properties:** category **Productivity**, subcategory none. For the privacy policy, use https://github.com/editorrylix/Pouchy/blob/main/PRIVACY.md.
- **Age ratings:** the questionnaire. Pouchy has no user-generated content shared with others, no chat and no purchases.
- **Store listing:** description, screenshots (the images in `docs/media` work well) and the logo (`packaging\store\pouchy-1024.png`, made by `make_assets.py`).

### Restricted capabilities

Partner Center asks why the package uses two restricted capabilities. You can paste this:

> **runFullTrust:** Pouchy is a Win32 desktop app (WPF). It needs full trust for its core features: a system tray icon, global keyboard shortcuts, a low-level mouse hook that detects the "shake while dragging" gesture that opens the shelf, OLE drag-and-drop with other apps, and the Windows share sheet and screen snip.
>
> **unvirtualizedResources:** Pouchy is also distributed as a standalone installer and portable app. It keeps the user's shelves and settings in %AppData%\Pouchy and its optional "Add to Pouchy" File Explorer menu entry in HKCU\Software\Classes. These must be real (not virtualized) so the Explorer menu entry works and users moving between the Store and standalone versions keep their data. Pouchy writes only to the current user's profile and registry, never to system locations.

### Notes for certification

You can paste this under **Submission options → Notes for certification**:

> Pouchy runs in the system tray (next to the clock) and opens a welcome window on first launch. To test: drag any file from File Explorer and shake the mouse left and right while holding the button, and the shelf appears; drop the file in, then drag it out again. Alt+Shift+Z also shows the shelf, and right-clicking the tray icon opens the menu. No account or sign-in is needed.

## How the Store version differs

The app is the same. Only what the Store requires or handles itself differs:

- **Updates come from the Store.** Pouchy's own update check and installer are turned off in the Store version, and Settings → About says so. After a Store update, Pouchy still shows **What's new**.
- **Start with Windows** uses the package's startup task instead of the registry, so it keeps working after updates. You can also see and change it in Task Manager → Startup apps.
- **Add to Pouchy** and **Send to** start Pouchy through its app alias (`pouchy.exe`), which also survives updates. You can type `pouchy` in Run or a terminal to start it.
- **Uninstalling** removes the app. Shelves and settings in `%AppData%\Pouchy` stay, as with the other versions. If you turned on the Explorer menu, turn it off in Settings before uninstalling.

## Updating the logo images

The images are drawn from shapes, so every size is sharp. After changing the icon design in `make_assets.py`, run:

```powershell
pip install pillow
python packaging\store\make_assets.py
```
