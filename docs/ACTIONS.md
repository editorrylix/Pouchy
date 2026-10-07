# Drop actions and scripts

When you drag files over the pouch, a row of **action tiles** appears under the mascot. Drop on a tile to run that action instead of adding the files. The same actions are in the command palette (<kbd>Ctrl</kbd>+<kbd>K</kbd>) for items already in the pouch, and your scripts are in the item menu under **Run script**.

## Built-in actions

| Tile | What it does | Shown for |
| --- | --- | --- |
| **Zip** | Puts everything in one archive next to the first file and adds the archive to the pouch | Any files and folders |
| **Recent folder** (for example *Downloads*) | Copies the files to one of your two most recent destinations | Any files and folders |
| **PNG** / **JPG** | Saves converted copies next to the originals and adds them to the pouch | Images |
| **50%** | Saves half-size copies and adds them to the pouch | Images |
| **Text** | Reads the text in the picture (on your PC, with Windows OCR), copies it and adds it as a note | One image |
| **Paths** | Copies the full paths as text | Anything |
| **Share** | Opens the Windows share sheet | Any files |
| **Print** | Sends the files to their default app's print command | Up to 20 files |

You can turn each one off in **Settings → Drop actions**, or hide the tiles completely.

**Recent folders** are the folders you most recently copied, moved or dropped items into from Pouchy. Dropping onto the desktop or an Explorer window counts too.

## Your own scripts

Put a script in the actions folder and it becomes an action. To find the folder, go to **Settings → Drop actions → Open actions folder**. It's `%AppData%\Pouchy\actions`.

| File type | How Pouchy runs it |
| --- | --- |
| `.ps1` | `powershell.exe -NoProfile -ExecutionPolicy Bypass -File script.ps1 <paths>` |
| `.bat`, `.cmd` | `cmd.exe /c script.bat <paths>` |
| `.py` | `py.exe script.py <paths>` (falls back to `python.exe`) |
| `.exe` | `program.exe <paths>` |

The rules:

- **Arguments.** Each file path is passed as a separate argument.
- **Working folder.** The script runs in the folder of the first file.
- **No window.** Nothing appears while it runs.
- **Output.** Any line the script prints that is the full path of an existing file or folder is added to the pouch. Everything else is ignored.
- **Errors.** A non-zero exit code shows an error with the last lines of the script's error output.
- **Time limit.** A script that runs longer than 10 minutes is stopped.

### Optional header

Add any of these lines near the top of the script, in any comment style:

```text
Pouchy-Name: Upload to my server
Pouchy-Extensions: .png .jpg .gif
Pouchy-Icon: CloudArrowUp24
```

| Line | What it does |
| --- | --- |
| `Pouchy-Name` | The name shown on the tile and in menus. Without it, Pouchy uses the file name. |
| `Pouchy-Extensions` | Only offer the action when every file has one of these extensions. |
| `Pouchy-Icon` | Any [Fluent UI System Icon](https://github.com/microsoft/fluentui-system-icons) name with a size suffix. |

## Examples

### Copy file names (PowerShell)

```powershell
# Pouchy-Name: Copy file names
# Pouchy-Icon: TextT24
Set-Clipboard -Value ($args | ForEach-Object { Split-Path $_ -Leaf })
```

### Make a WebP copy (Python and Pillow)

```python
# Pouchy-Name: Convert to WebP
# Pouchy-Extensions: .png .jpg .jpeg
# Pouchy-Icon: Image24
import sys, pathlib
from PIL import Image

for path in map(pathlib.Path, sys.argv[1:]):
    target = path.with_suffix(".webp")
    Image.open(path).save(target, "WEBP", quality=85)
    print(target)  # Printed paths are added to the pouch.
```

### Compress videos (batch and ffmpeg)

```bat
@echo off
rem Pouchy-Name: Shrink video
rem Pouchy-Extensions: .mp4 .mov .mkv
rem Pouchy-Icon: Video24
for %%F in (%*) do (
  ffmpeg -y -loglevel error -i "%%~F" -vcodec libx264 -crf 28 "%%~dpnF (small).mp4"
  echo %%~dpnF (small).mp4
)
```

> [!NOTE]
> Scripts run with your account's permissions, like anything else you start yourself. Only put scripts you trust in the actions folder.
