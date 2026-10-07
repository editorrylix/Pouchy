<#
.SYNOPSIS
    Records the trailer's real footage: real File Explorer, Paint, Edge and Pouchy, driven by
    scripted mouse and keyboard input, captured at 1920x1080 60 fps.

.DESCRIPTION
    Takes over the mouse and keyboard while it runs. Don't touch the PC until it says "done".

    - Sample files: C:\PouchyTrailer\Photos and \Project (make_samples.py).
    - Pouchy runs from C:\PouchyTrailer\app with its own "trailer" profile, so your own shelves
      and settings are never touched.
    - A backdrop window hides the desktop, and the taskbar is set to auto-hide while recording
      (restored afterwards).
    - Each shot is a separate file in C:\PouchyTrailer\footage. shot-list.md describes them.

.EXAMPLE
    powershell -ExecutionPolicy Bypass -File trailer\capture\record.ps1
    powershell -ExecutionPolicy Bypass -File trailer\capture\record.ps1 -Shots 02-summon,03-drop-anywhere
#>
param(
    [string[]]$Shots = @(),
    [string]$Root = 'C:\PouchyTrailer',
    # Run the actions without recording, to check them.
    [switch]$DryRun,
    # Only set up the stage, list what's on it, and clean up.
    [switch]$CheckStage
)

$ErrorActionPreference = 'Stop'
Add-Type -Path (Join-Path $PSScriptRoot 'Rec.cs')
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes
[Rec]::Init()

$exe = Join-Path $Root 'app\Pouchy.exe'
$photos = Join-Path $Root 'Photos'
$project = Join-Path $Root 'Project'
$footage = Join-Path $Root 'footage'
$profileDir = Join-Path $env:APPDATA 'Pouchy-trailer'
New-Item -ItemType Directory -Force $footage | Out-Null

$VK = @{ Shift = [byte]0x10; Ctrl = [byte]0x11; Alt = [byte]0x12; Esc = [byte]0x1B; Enter = [byte]0x0D; Del = [byte]0x2E }
function Wait($ms) { Start-Sleep -Milliseconds $ms }
function Log($text) { Write-Host ("[{0:HH:mm:ss}] {1}" -f (Get-Date), $text) }

# ---------------------------------------------------------------------------------------------- UI Automation

$UIA = [System.Windows.Automation.AutomationElement]
function Center($element) {
    $r = $element.Current.BoundingRectangle
    [pscustomobject]@{ X = [int]($r.X + $r.Width / 2); Y = [int]($r.Y + $r.Height / 2); Left = [int]$r.X; Top = [int]$r.Y; Width = [int]$r.Width; Height = [int]$r.Height }
}
function Find-All($hwnd, $controlType) {
    $root = $UIA::FromHandle($hwnd)
    $condition = New-Object System.Windows.Automation.PropertyCondition($UIA::ControlTypeProperty, $controlType)
    $root.FindAll([System.Windows.Automation.TreeScope]::Descendants, $condition)
}
# Where an Explorer item is on screen. Explorer may still be re-laying out its icons, so the position
# is only trusted once a hit-test at its centre lands on that same item.
function Explorer-Item($name) {
    for ($attempt = 0; $attempt -lt 20; $attempt++) {
        foreach ($item in (Find-All $script:explorer ([System.Windows.Automation.ControlType]::ListItem))) {
            $n = $item.Current.Name
            if ($n -ne $name -and $n -notlike "$name.*") { continue }
            $c = Center $item
            $hit = $UIA::FromPoint((New-Object System.Windows.Point($c.X, $c.Y)))
            for ($e = $hit; $e; $e = [System.Windows.Automation.TreeWalker]::ControlViewWalker.GetParent($e)) {
                if ($e.Current.Name -eq $n -and $e.Current.ControlType -eq [System.Windows.Automation.ControlType]::ListItem) { return $c }
                if ($e.Current.ControlType -eq [System.Windows.Automation.ControlType]::Window) { break }
            }
        }
        Wait 250
    }
    foreach ($item in (Find-All $script:explorer ([System.Windows.Automation.ControlType]::ListItem))) {
        if ($item.Current.Name -eq $name -or $item.Current.Name -like "$name.*") { Log "  (using Explorer's position for $name unchecked)"; return Center $item }
    }
    throw "Explorer item '$name' not found."
}

# Picks up an Explorer item the way a person does: select it, then press and pull it away so
# Explorer starts a real drag-and-drop. Leaves the mouse button down.
function Grab($name, [int]$pull = 40) {
    $item = Explorer-Item $name
    [Rec]::Move($item.X, $item.Y, 380); Wait 90
    [Rec]::Click(); Wait 700        # longer than the double-click time, or Windows would open the file
    [Rec]::Down(); Wait 140
    [Rec]::Move($item.X + $pull, $item.Y + 8, 220); Wait 260
    return $item
}
function Reset-Samples {
    Get-ChildItem $photos | Where-Object { $_.Name -match '\(\d+%\)|\.zip$|^Archive' } | Remove-Item -Force -ErrorAction SilentlyContinue
    Wait 600
}

function Pouch { [Rec]::FindTitle('Pouchy') }
function Pouch-Rect { $h = Pouch; if ($h -eq [IntPtr]::Zero) { throw 'The pouch is not visible.' }; [Rec]::Rect($h) }
function Pouch-Center {
    $r = Pouch-Rect
    [pscustomobject]@{ X = [int](($r.Left + $r.Right) / 2); Y = [int](($r.Top + $r.Bottom) / 2 + 30) }
}
function Pouch-Tiles { @(Find-All (Pouch) ([System.Windows.Automation.ControlType]::ListItem) | ForEach-Object { Center $_ }) }
function Pouch-Text($text) {
    foreach ($t in (Find-All (Pouch) ([System.Windows.Automation.ControlType]::Text))) {
        if ($t.Current.Name -eq $text) { return Center $t }
    }
    return $null
}
function Pouch-Button($help) {
    foreach ($b in (Find-All (Pouch) ([System.Windows.Automation.ControlType]::Button))) {
        if ($b.Current.HelpText -like "$help*") { return Center $b }
    }
    throw "Pouch button '$help' not found."
}
# Line $index (0-based) on the prop page, from its fixed CSS layout (notes.html) and the page area's position.
function Edge-Line([int]$index) {
    $page = [Rec]::ChildRect($script:edge, 'Chrome_RenderWidgetHostHWND')
    if ($page.Width -le 0) { throw 'The browser page area was not found.' }
    $scale = 1.25   # Display scaling of this PC (120 dpi); CSS pixels are scaled by it.
    [pscustomobject]@{ X = [int]($page.Left + (44 + 140) * $scale); Y = [int]($page.Top + (30 + 48 + 20 + $index * (64 + 18) + 32) * $scale) }
}
# An item in an open context menu: a classic Win32 menu (#32768) or one of Pouchy's own (WPF) menus.
function Menu-Item($name) {
    for ($i = 0; $i -lt 30; $i++) {
        $r = [Rec]::MenuItemRect($name)
        if ($r.Width -gt 0) { return [pscustomobject]@{ X = [int](($r.Left + $r.Right) / 2); Y = [int](($r.Top + $r.Bottom) / 2); Left = $r.Left; Top = $r.Top; Width = $r.Width; Height = $r.Height } }
        $windows = @([Rec]::FindClass('#32768'))
        foreach ($p in (Get-Process Pouchy -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe })) { $windows += [Rec]::FindPopups($p.Id) }
        foreach ($h in $windows) {
            if ($h -eq [IntPtr]::Zero) { continue }
            foreach ($item in (Find-All $h ([System.Windows.Automation.ControlType]::MenuItem))) {
                if ($item.Current.Name -eq $name) { return Center $item }
            }
        }
        Wait 150
    }
    throw "Menu item '$name' not found."
}

# ---------------------------------------------------------------------------------------------- Pouchy

$baseSettings = [ordered]@{
    ThemeId = 'midnight'; ViewMode = 'Grid'; TileSize = 'Large'; GridColumns = 3; ShowItemNames = $true
    CheckForUpdates = $false; LastSeenVersion = '1.1.0'; SoundPack = 'Off'; ShowDropActions = $true
    FetchLinkPreviews = $true; SpawnAnimation = 'Pop'; ClipboardHistoryLimit = 50
    SuppressInFullscreen = $false   # Overlays from other tools (e.g. Overwolf) would otherwise count as fullscreen apps
    RecentDestinations = @($project)
}

function Item($name, [string]$kind = 'File') {
    $path = Get-ChildItem $photos | Where-Object { $_.BaseName -eq $name } | Select-Object -First 1
    [ordered]@{ Id = [guid]::NewGuid(); Kind = $kind; FilePath = $path.FullName; DisplayName = $name; AddedAt = (Get-Date).ToString('o') }
}
function Shelf($name, $items, [string]$rule = 'None', [string]$color = '#8B7CFF', [string]$icon = 'Archive') {
    [ordered]@{ Id = [guid]::NewGuid(); Name = $name; Color = $color; Icon = $icon; Rule = $rule; Items = @($items) }
}

function Stop-Pouchy {
    Get-Process Pouchy -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe } | ForEach-Object { $_.Kill(); $_.WaitForExit(3000) | Out-Null }
}

function Start-Pouchy($shelves, [int]$active = 0, $settings = @{}) {
    Stop-Pouchy
    New-Item -ItemType Directory -Force $profileDir | Out-Null
    Get-ChildItem $profileDir -Filter '*.json' -ErrorAction SilentlyContinue | Remove-Item -Force
    Remove-Item (Join-Path $profileDir 'themes') -Recurse -Force -ErrorAction SilentlyContinue   # custom themes from earlier takes
    $s = [ordered]@{}; foreach ($k in $baseSettings.Keys) { $s[$k] = $baseSettings[$k] }; foreach ($k in $settings.Keys) { $s[$k] = $settings[$k] }
    $s | ConvertTo-Json -Depth 5 | Set-Content -Encoding utf8 (Join-Path $profileDir 'settings.json')
    $state = [ordered]@{ Version = 2; ActiveShelfId = $shelves[$active].Id; Shelves = @($shelves) }
    $state | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $profileDir 'shelf_state.json')
    $script:pouchy = Start-Process $exe -ArgumentList '--profile', 'trailer' -PassThru
    Wait 3500
}

function Show-Pouch($x, $y) {
    [Rec]::Move($x, $y, 400)
    [Rec]::Combo(@($VK.Alt, $VK.Shift), [byte]0x5A)   # Alt+Shift+Z
    Wait 700
}

function Screenshot-Keys {
    $hk = (Get-Content (Join-Path $profileDir 'settings.json') -Raw | ConvertFrom-Json).ScreenshotHotkey
    $key = if ($hk.Key) { [byte][char]$hk.Key.ToString().ToUpper()[0] } else { [byte]0x53 }
    $mods = @()
    if ("$($hk.Modifiers)" -match 'Control') { $mods += $VK.Ctrl }
    if ("$($hk.Modifiers)" -match 'Alt' -or -not $hk.Modifiers) { $mods += $VK.Alt }
    if ("$($hk.Modifiers)" -match 'Shift' -or -not $hk.Modifiers) { $mods += $VK.Shift }
    [pscustomobject]@{ Mods = $mods; Key = $key }
}

# ---------------------------------------------------------------------------------------------- Stage

$layout = @{
    Explorer = @(70, 90, 960, 780)
    Paint = @(1010, 130, 860, 760)
    Edge = @(950, 110, 900, 660)
}

$allowedTitles = @('Photos', 'Photos - File Explorer', 'Project - File Explorer', 'Pouchy', 'Pouchy command palette', 'Launch notes',
    'Pouchy Settings', 'Theme editor', 'Quick Look',
    'Untitled - Paint', '* - Paint', 'Pouchy trailer backdrop', 'Program Manager',
    # Transparent overlays from other tools that exist on every desktop and draw nothing on their own.
    # (The footage is still reviewed frame by frame.)
    'Outplayed - Screen Recorder Overlay', 'Cua.AgentCursorOverlay.default',
    'PopupHost')   # tooltips and pop-ups of the stage apps (Paint and Explorer are WinUI)

# Refuses to record if any window that isn't part of the shoot is visible on this virtual desktop.
function Assert-CleanStage {
    $foreign = [Rec]::ForeignWindows($allowedTitles)
    if ($foreign.Count -gt 0) { $foreign | ForEach-Object { Log "  on stage: $_" }; throw "Not recording: $($foreign.Count) other window(s) visible on the stage desktop." }
}

$desktopsKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\VirtualDesktops'
function Current-Desktop { [guid]::new([byte[]](Get-ItemProperty $desktopsKey).CurrentVirtualDesktop) }

function Setup-Stage {
    Log 'Setting up the stage'
    # Pouchy rewrites the shared startup entry when its path differs; keep the user's own value.
    $script:runKey = (Get-ItemProperty -LiteralPath 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run' -Name Pouchy -ErrorAction SilentlyContinue).Pouchy
    $script:hadRunKey = $null -ne $script:runKey

    # A brand-new virtual desktop (Win+Ctrl+D): only the shoot's windows exist there. Checked, not assumed.
    $script:mainDesktop = Current-Desktop
    for ($attempt = 1; $attempt -le 3 -and -not $script:stageDesktop; $attempt++) {
        [Rec]::KeyDown(0x5B); [Rec]::KeyDown($VK.Ctrl); [Rec]::Key([byte]0x44); [Rec]::KeyUp($VK.Ctrl); [Rec]::KeyUp(0x5B)
        Wait 1800
        $now = Current-Desktop
        if ($now -ne $script:mainDesktop -and ([Rec]::ForeignWindows($allowedTitles)).Count -eq 0) { $script:stageDesktop = $now }
        elseif ($now -eq $script:mainDesktop) { Log "  the new desktop didn't open (attempt $attempt)" }
        else { throw 'The new desktop is not empty; not recording.' }
    }
    if (-not $script:stageDesktop) { throw 'Could not open a clean virtual desktop; not recording.' }
    $script:taskbarWasAutoHide = [Rec]::TaskbarAutoHide()
    if (-not $script:taskbarWasAutoHide) { [Rec]::SetTaskbarAutoHide($true) }
    $script:backdrop = Start-Process powershell -ArgumentList '-NoProfile', '-STA', '-WindowStyle', 'Hidden', '-ExecutionPolicy', 'Bypass',
        '-File', (Join-Path $PSScriptRoot 'backdrop.ps1'), '-Image', (Join-Path $Root 'backdrop.png') -PassThru -WindowStyle Hidden
    Wait 1500

    # Edge: a clean app window with the prop page (its own profile, so nothing of yours shows).
    $edgeExe = @("${env:ProgramFiles(x86)}\Microsoft\Edge\Application\msedge.exe", "$env:ProgramFiles\Microsoft\Edge\Application\msedge.exe") | Where-Object { Test-Path $_ } | Select-Object -First 1
    Copy-Item (Join-Path $PSScriptRoot 'notes.html') (Join-Path $Root 'notes.html') -Force
    $script:edgeProc = Start-Process $edgeExe -ArgumentList "--user-data-dir=$Root\edge-profile", '--no-first-run', '--no-default-browser-check',
        '--disable-features=msEdgeSidebarV2,msUndersideButton', "--app=file:///$($Root.Replace('\','/'))/notes.html" -PassThru
    for ($i = 0; $i -lt 60; $i++) { $script:edge = [Rec]::Find($script:edgeProc.Id, 'Launch notes'); if ($script:edge -ne [IntPtr]::Zero) { break }; Wait 200 }
    if ($script:edge -eq [IntPtr]::Zero) { throw 'The Edge window did not open.' }

    Start-Process mspaint
    for ($i = 0; $i -lt 50; $i++) { $script:paint = [Rec]::FindTitle('Untitled - Paint'); if ($script:paint -ne [IntPtr]::Zero) { break }; Wait 200 }

    Start-Process explorer.exe $photos
    $shell = New-Object -ComObject Shell.Application
    for ($i = 0; $i -lt 50 -and -not $script:explorer; $i++) {
        foreach ($w in $shell.Windows()) {
            try { if ($w.Document.Folder.Self.Path -eq $photos) { $script:explorer = [IntPtr][long]$w.HWND; $script:explorerCom = $w } } catch { }
        }
        Wait 200
    }
    if (-not $script:explorerCom) { throw 'The Explorer window did not open.' }
    try { $script:explorerCom.Document.CurrentViewMode = 1; $script:explorerCom.Document.IconSize = 128 } catch { Log "  could not set the icon view: $_" }
    Wait 1200

    Place-Windows
    $er = [Rec]::Rect($script:edge)
    Log ("  Edge window {0}: {1},{2} {3}x{4}, on this desktop: {5}" -f $script:edge, $er.Left, $er.Top, $er.Width, $er.Height, [Rec]::OnThisDesktop($script:edge))
    Assert-CleanStage
    Log 'Stage ready'
}

function Place-Windows {
    [Rec]::Place($script:paint, $layout.Paint[0], $layout.Paint[1], $layout.Paint[2], $layout.Paint[3])
    [Rec]::Place($script:edge, $layout.Edge[0], $layout.Edge[1], $layout.Edge[2], $layout.Edge[3])
    [Rec]::Place($script:explorer, $layout.Explorer[0], $layout.Explorer[1], $layout.Explorer[2], $layout.Explorer[3])
    Wait 300
}

function Teardown-Stage {
    Log 'Cleaning up'
    Stop-Pouchy
    try { if ($script:explorerCom) { $script:explorerCom.Quit() } } catch { }
    # Any viewer that opened a sample file by accident (e.g. Photos after a stray double-click).
    $samples = Get-ChildItem $photos | ForEach-Object { $_.Name }
    Get-Process -ErrorAction SilentlyContinue | Where-Object { $_.MainWindowTitle -and ($samples -contains $_.MainWindowTitle -or $_.MainWindowTitle -like '*PouchyTrailer*') -and $_.ProcessName -ne 'explorer' } | Stop-Process -Force -ErrorAction SilentlyContinue
    Get-Process mspaint -ErrorAction SilentlyContinue | ForEach-Object { $_.CloseMainWindow() | Out-Null }
    Wait 1500
    Get-Process mspaint -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue   # e.g. stuck on "Save changes?"
    if ($script:edgeProc) { Get-Process msedge -ErrorAction SilentlyContinue | Where-Object { $_.Path -and (Get-CimInstance Win32_Process -Filter "ProcessId=$($_.Id)").CommandLine -like '*PouchyTrailer*' } | Stop-Process -Force -ErrorAction SilentlyContinue }
    if ($script:backdrop) { Stop-Process -Id $script:backdrop.Id -Force -ErrorAction SilentlyContinue }
    Wait 800
    # Close the stage desktop (Win+Ctrl+F4), but only if it's the one this script made and is still showing.
    if ($script:stageDesktop -and (Current-Desktop) -eq $script:stageDesktop) {
        [Rec]::KeyDown(0x5B); [Rec]::KeyDown($VK.Ctrl); [Rec]::Key([byte]0x73); [Rec]::KeyUp($VK.Ctrl); [Rec]::KeyUp(0x5B)
        Wait 1500
        if ((Current-Desktop) -ne $script:mainDesktop) { Log '  WARNING: not back on the original desktop; press Win+Ctrl+Left.' }
    }
    elseif ($script:stageDesktop) { Log '  WARNING: the stage desktop was not showing, so it was left open (close it with Win+Ctrl+F4).' }
    if ($null -ne $script:taskbarWasAutoHide -and -not $script:taskbarWasAutoHide) { [Rec]::SetTaskbarAutoHide($false) }
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree('Software\Classes\*\shell\PouchyTrailer', $false)
    $run = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Run'
    if ($script:hadRunKey) { Set-ItemProperty -LiteralPath $run -Name Pouchy -Value $script:runKey }
    elseif ($null -ne $script:hadRunKey) { Remove-ItemProperty -LiteralPath $run -Name Pouchy -ErrorAction SilentlyContinue }
}

# ---------------------------------------------------------------------------------------------- Recording

function Record($name, [double]$seconds, [scriptblock]$action) {
    Log "Shot $name"
    $file = Join-Path $footage "$name.mp4"
    $ff = $null
    Assert-CleanStage
    if (-not $DryRun) {
        $arguments = "-hide_banner -loglevel error -y -f lavfi -i ddagrab=output_idx=0:framerate=60:draw_mouse=1 -t $seconds " +
            "-vf hwdownload,format=bgra,format=yuv420p -c:v libx264 -preset veryfast -crf 12 -r 60 -movflags +faststart `"$file`""
        $ff = Start-Process ffmpeg -ArgumentList $arguments -PassThru -WindowStyle Hidden
        Wait 900
    }
    try { & $action }
    finally {
        if ($ff) { $ff.WaitForExit() | Out-Null; Log "  saved $file" }
    }
}

# ---------------------------------------------------------------------------------------------- Shots

$all = [ordered]@{}

# The problem: dragging a file toward an app that's buried behind another window.
$all['01-friction'] = {
    Stop-Pouchy
    Place-Windows
    # Paint raises itself once it has loaded; set the order explicitly: Paint, Edge over it, Explorer in front.
    [Rec]::Front($script:paint); Wait 250; [Rec]::Front($script:edge); Wait 250; [Rec]::Front($script:explorer); Wait 400
    $file = Explorer-Item 'Beach'
    [Rec]::Move($file.X - 60, $file.Y + 40, 300)
    Record '01-friction' 6.5 {
        Wait 300
        Grab 'Beach' | Out-Null
        [Rec]::Move($file.X + 260, $file.Y - 20, 450)
        [Rec]::Move(1350, 420, 700)          # over the browser window that hides Paint
        [Rec]::Move(1560, 560, 650)
        [Rec]::Move(1250, 690, 700)
        [Rec]::Move(1500, 300, 650)
        [Rec]::Move(820, 420, 750)           # give up, back toward Explorer
        [Rec]::Key($VK.Esc); Wait 120
        [Rec]::Up(); Wait 600
    }
}

# The magic moment: grab, shake, Pouchy appears, drop. Then a second file.
$all['02-summon'] = {
    # Drop actions off for this shot, so the pouch shows its plain "drop to add" state (04 shows the actions).
    Start-Pouchy @((Shelf 'Pouch' @())) 0 @{ ShowDropActions = $false }
    Place-Windows
    [Rec]::Front($script:explorer); Wait 300
    $file = Explorer-Item 'Sunset'
    [Rec]::Move($file.X - 80, $file.Y + 60, 300)
    Record '02-summon' 14 {
        Wait 300
        Grab 'Sunset' | Out-Null
        [Rec]::Move(900, 470, 700); Wait 450
        [Rec]::Shake(8, 80, 75); Wait 500
        if ((Pouch) -eq [IntPtr]::Zero) { Log '  second shake'; [Rec]::Shake(6, 80, 80); Wait 600 }   # a second try, like a person would
        $c = Pouch-Center
        [Rec]::Move($c.X, $c.Y, 700); Wait 350
        [Rec]::Up(); Wait 1300
        Grab 'Beach' | Out-Null
        $c = Pouch-Center
        [Rec]::Move($c.X + 30, $c.Y, 800); Wait 300
        [Rec]::Up(); Wait 1800
    }
}

# Drop anywhere: switch to Paint and drag the picture out of the pouch into it.
$all['03-drop-anywhere'] = {
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Beach'))))
    Get-Process mspaint -ErrorAction SilentlyContinue | Stop-Process -Force; Wait 1000
    Start-Process mspaint
    for ($i = 0; $i -lt 50; $i++) { $script:paint = [Rec]::FindTitle('Untitled - Paint'); if ($script:paint -ne [IntPtr]::Zero) { break }; Wait 200 }
    [Rec]::Place($script:edge, -2000, 100, 900, 660)   # out of the way for this shot
    [Rec]::Place($script:paint, $layout.Paint[0], $layout.Paint[1], $layout.Paint[2], $layout.Paint[3])
    [Rec]::Place($script:explorer, $layout.Explorer[0], $layout.Explorer[1], $layout.Explorer[2], $layout.Explorer[3])
    [Rec]::Front($script:explorer); Wait 400
    Show-Pouch 560 470
    [Rec]::Move(700, 760, 400)
    Record '03-drop-anywhere' 9.5 {
        Wait 400
        $paintRect = [Rec]::Rect($script:paint)
        [Rec]::Move($paintRect.Left + 260, $paintRect.Top + 18, 650); Wait 150
        [Rec]::Click(); Wait 600                         # bring Paint forward (the pouch stays on top)
        $tile = (Pouch-Tiles)[0]
        [Rec]::Move($tile.X, $tile.Y, 700); Wait 200
        [Rec]::Down(); Wait 150
        [Rec]::Move(($paintRect.Left + $paintRect.Right) / 2 + 80, ($paintRect.Top + $paintRect.Bottom) / 2 + 40, 1100); Wait 350
        [Rec]::Up(); Wait 3200
    }
}

function Drag-To-Tile($files, $tileText, [string[]]$hover = @()) {
    $first = Explorer-Item $files[0]
    [Rec]::Move($first.X, $first.Y, 450); Wait 120
    [Rec]::Click(); Wait 150
    foreach ($f in ($files | Select-Object -Skip 1)) {
        $p = Explorer-Item $f
        [Rec]::Move($p.X, $p.Y, 380); Wait 100
        [Rec]::KeyDown($VK.Ctrl); [Rec]::Click(); [Rec]::KeyUp($VK.Ctrl); Wait 120
    }
    $grab = Explorer-Item $files[-1]
    [Rec]::Move($grab.X, $grab.Y, 250); Wait 700   # not a double-click
    [Rec]::Down(); Wait 140
    [Rec]::Move($grab.X + 40, $grab.Y + 8, 220); Wait 260
    $c = Pouch-Center
    [Rec]::Move($c.X, $c.Y - 40, 800); Wait 700
    foreach ($h in $hover) {
        $t = Pouch-Text $h
        if ($t) { [Rec]::Move($t.X, $t.Y - 12, 420); Wait 300 }
    }
    $target = Pouch-Text $tileText
    if (-not $target) { throw "Tile '$tileText' not found." }
    [Rec]::Move($target.X, $target.Y - 12, 450); Wait 350
    [Rec]::Up()
}

# Drop actions: drag two pictures over the pouch, the action tiles appear, drop on 50%.
$all['04-drop-actions'] = {
    Reset-Samples
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Beach'))))
    Place-Windows
    [Rec]::Place($script:edge, -2000, 100, 900, 660)
    [Rec]::Front($script:explorer); Wait 300
    Show-Pouch 1010 430
    [Rec]::Front($script:explorer); Wait 300
    [Rec]::Move(600, 760, 300)
    Record '04-drop-actions' 17 {
        Wait 300
        Drag-To-Tile @('Forest', 'City lights') '50%' @('Zip', 'Project')
        Wait 2600
    }
}

# Extract text: drop a picture of a note on the Text tile.
$all['05-ocr'] = {
    Reset-Samples
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Beach'))))
    Place-Windows
    [Rec]::Place($script:edge, -2000, 100, 900, 660)
    [Rec]::Front($script:explorer); Wait 300
    Show-Pouch 1010 430
    [Rec]::Front($script:explorer); Wait 300
    [Rec]::Move(600, 760, 300)
    Record '05-ocr' 12 {
        Wait 300
        Drag-To-Tile @('Meeting note') 'Text'
        Wait 3000
    }
}

# Screenshot to Pouchy: the hotkey opens the Windows snip; the capture lands in the pouch.
$all['06-screenshot'] = {
    Reset-Samples
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'))))
    Place-Windows
    [Rec]::Place($script:edge, -2000, 100, 900, 660)
    [Rec]::Front($script:explorer); Wait 300
    $keys = Screenshot-Keys
    $a = Explorer-Item 'Mountains'
    $b = Explorer-Item 'City lights'
    [Rec]::Move(1300, 600, 300)
    Record '06-screenshot' 11.5 {
        Wait 400
        [Rec]::Combo($keys.Mods, $keys.Key); Wait 1300
        [Rec]::Move($a.Left - 30, $a.Top - 20, 600); Wait 200
        [Rec]::Down(); Wait 120
        [Rec]::Move($b.Left + $b.Width + 30, $b.Top + $b.Height + 20, 900); Wait 150
        [Rec]::Up(); Wait 3200
    }
}

# Clipboard history: copy a sentence, a link and a colour; each lands on the Clipboard shelf.
$all['07-clipboard'] = {
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'))), (Shelf 'Clipboard' @() 'Clipboard' '#14B8A6' 'Bookmark')) 1
    [Rec]::Place($script:explorer, -2600, 90, 960, 780)
    [Rec]::Place($script:edge, 90, 150, 860, 620)
    [Rec]::Front($script:edge); Wait 400
    Show-Pouch 1010 430
    $quote = Edge-Line 0
    $link = Edge-Line 1
    $colour = Edge-Line 2
    [Rec]::Move(500, 760, 300)
    Record '07-clipboard' 9.5 {
        Wait 300
        foreach ($p in @($quote, $link, $colour)) {
            [Rec]::Move($p.X - 80, $p.Y, 600); Wait 150
            [Rec]::Click(); Wait 250
            [Rec]::Combo(@($VK.Ctrl), [byte]0x43); Wait 1200
        }
        Wait 1200
    }
}

# Explorer integration: right-click a file, "Add to Pouchy".
$all['08-explorer-menu'] = {
    Reset-Samples
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'))))
    # The same entry Pouchy's "Add to Pouchy from Explorer" setting writes (Pouchy's normal profile would
    # label it the same way; the trailer profile is only used so your own data isn't touched).
    $classes = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey('Software\Classes\*\shell\PouchyTrailer')
    $classes.SetValue('', 'Add to Pouchy'); $classes.SetValue('Icon', "`"$exe`",0")
    $classes.CreateSubKey('command').SetValue('', "`"$exe`" --profile trailer --add `"%1`""); $classes.Close()
    Place-Windows
    [Rec]::Place($script:edge, -2000, 100, 900, 660)
    [Rec]::Front($script:explorer); Wait 300
    $file = Explorer-Item 'Mountains'
    [Rec]::Move($file.X + 120, $file.Y + 120, 300)
    Record '08-explorer-menu' 11 {
        Wait 300
        $file = Explorer-Item 'Mountains'    # measured now: the folder may have just changed
        [Rec]::Move($file.X, $file.Y, 500); Wait 150
        [Rec]::Click(); Wait 700
        [Rec]::KeyDown($VK.Shift); Wait 60; [Rec]::RightClick(); Wait 60; [Rec]::KeyUp($VK.Shift)
        # Explorer takes a moment (busy cursor) to build the full menu.
        for ($i = 0; $i -lt 20 -and [Rec]::FindClass('#32768') -eq [IntPtr]::Zero; $i++) { Wait 150 }
        Wait 500
        if ([Rec]::FindClass('#32768') -eq [IntPtr]::Zero) {
            Log '  no menu after Shift+right-click; using Shift+F10'
            [Rec]::Combo(@($VK.Shift), [byte]0x79); Wait 900
        }
        $entry = Menu-Item 'Add to Pouchy'
        [Rec]::Move($entry.X - 40, $entry.Y, 700); Wait 300
        [Rec]::Click(); Wait 1300
        # Rest on the new tile, away from Explorer's info tips.
        $tiles = Pouch-Tiles
        if ($tiles.Count -gt 0) { [Rec]::Move($tiles[-1].X, $tiles[-1].Y - 20, 700) }
        Wait 2000
    }
    [Microsoft.Win32.Registry]::CurrentUser.DeleteSubKeyTree('Software\Classes\*\shell\PouchyTrailer', $false)
}

# Smart shelves and search: a picture sorts itself onto the Pictures shelf, then Ctrl+F.
$all['09-smart-shelves'] = {
    Reset-Samples
    Start-Pouchy @(
        (Shelf 'Pouch' @((Item 'Sunset'), (Item 'Ideas'))),
        (Shelf 'Pictures' @((Item 'Beach')) 'Images' '#EC4899' 'Camera'),
        (Shelf 'Links' @() 'Links' '#3B82F6' 'Bookmark'))
    Place-Windows
    [Rec]::Place($script:edge, -2000, 100, 900, 660)
    [Rec]::Front($script:explorer); Wait 300
    Show-Pouch 1010 430
    [Rec]::Front($script:explorer); Wait 300
    $file = Explorer-Item 'Forest'
    [Rec]::Move($file.X - 60, $file.Y + 70, 300)
    Record '09-smart-shelves' 14 {
        Wait 300
        Grab 'Forest' | Out-Null
        $c = Pouch-Center
        [Rec]::Move($c.X, $c.Y + 40, 800); Wait 900
        [Rec]::Up(); Wait 1400
        $r = Pouch-Rect
        [Rec]::Move($r.Left + 200, $r.Top + 60, 500); [Rec]::Click(); Wait 200   # focus the pouch (header)
        [Rec]::Combo(@($VK.Ctrl), [byte]0x32); Wait 1100                       # Ctrl+2: Pictures
        [Rec]::Combo(@($VK.Ctrl), [byte]0x46); Wait 400                        # Ctrl+F
        [Rec]::Type('sun', 170); Wait 1600
        [Rec]::Key($VK.Esc); Wait 300
    }
}

# Command palette and themes: Ctrl+K, type a theme, Enter. A few times.
$all['10-palette-themes'] = {
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Beach'), (Item 'Mountains'), (Item 'Ideas'), (Item 'City lights'))))
    Place-Windows
    [Rec]::Place($script:edge, -2000, 100, 900, 660)
    [Rec]::Front($script:explorer); Wait 300
    Show-Pouch 1010 430
    $r = Pouch-Rect
    [Rec]::Move($r.Left + 200, $r.Top + 60, 300); [Rec]::Click(); Wait 200
    [Rec]::Move(780, 760, 300)
    Record '10-palette-themes' 13 {
        Wait 300
        foreach ($theme in @('sunset', 'terminal', 'paper', 'midnight')) {
            [Rec]::Combo(@($VK.Ctrl), [byte]0x4B); Wait 450                    # Ctrl+K
            [Rec]::Type($theme, 110); Wait 350
            [Rec]::Key($VK.Enter); Wait 1300
        }
    }
}

# Organise: label, pin, remove and undo, and switch views.
$all['11-organise'] = {
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Beach'), (Item 'Mountains'), (Item 'Ideas'), (Item 'City lights'), (Item 'Forest'))))
    Place-Windows
    [Rec]::Place($script:edge, -2000, 100, 900, 660)
    [Rec]::Front($script:explorer); Wait 300
    Show-Pouch 1010 400
    [Rec]::Move(780, 760, 300)
    Record '11-organise' 17 {
        Wait 300
        $tiles = Pouch-Tiles
        [Rec]::Move($tiles[0].X, $tiles[0].Y, 600); Wait 150
        [Rec]::RightClick(); Wait 500
        $label = Menu-Item 'Label'
        [Rec]::Move($label.X, $label.Y, 450); Wait 500
        $red = Menu-Item 'Red'
        [Rec]::Move($red.X, $red.Y, 400); Wait 200
        [Rec]::Click(); Wait 700
        [Rec]::Move($tiles[1].X, $tiles[1].Y, 450); [Rec]::Click(); Wait 200
        [Rec]::Combo(@($VK.Ctrl), [byte]0x50); Wait 800                         # Ctrl+P: pin
        [Rec]::Move($tiles[3].X, $tiles[3].Y, 450); [Rec]::Click(); Wait 200
        [Rec]::Key($VK.Del); Wait 1100                                          # remove
        [Rec]::Combo(@($VK.Ctrl), [byte]0x5A); Wait 900                         # Ctrl+Z: undo
        # The pouch changes width with the view and its header buttons are right-aligned, so the
        # button is found from the pouch's right edge each time (UI Automation is too slow here).
        $view = Pouch-Button 'Change view'
        $r0 = [Rec]::Rect((Pouch)); $dx = $r0.Right - $view.X; $dy = $view.Y - $r0.Top
        foreach ($i in 1..3) {
            $r = [Rec]::Rect((Pouch))
            [Rec]::Move($r.Right - $dx, $r.Top + $dy, $(if ($i -eq 1) { 600 } else { 300 })); Wait 150
            [Rec]::Click(); Wait 150
            [Rec]::Move($r.Right - $dx - 90, $r.Top + $dy + 70, 250); Wait 900   # off the header, no tooltips
        }
    }
}

# A text label inside one of Pouchy's windows (Settings, theme editor).
function Window-Text($hwnd, $text) {
    foreach ($t in (Find-All $hwnd ([System.Windows.Automation.ControlType]::Text))) {
        if ($t.Current.Name -eq $text) { return Center $t }
    }
    return $null
}
# The control of a type (Edit, ComboBox) on the same row as a label.
function Row-Control($hwnd, $label, $controlType) {
    $l = Window-Text $hwnd $label
    if (-not $l) { throw "'$label' not found." }
    $best = $null
    foreach ($c in (Find-All $hwnd $controlType)) {
        $p = Center $c
        if ($p.X -gt $l.X -and [Math]::Abs($p.Y - $l.Y) -lt 40 -and (-not $best -or [Math]::Abs($p.Y - $l.Y) -lt [Math]::Abs($best.Y - $l.Y))) { $best = $p }
    }
    if (-not $best) { throw "No control next to '$label'." }
    $best
}
# Scrolls the window under the mouse until a label sits in the given band of the screen.
# Uses the mouse wheel; if the window ignores it, scrolls the same view through UI Automation.
function Scroll-To($hwnd, $text, [int]$top = 300, [int]$bottom = 760, [int]$notches = 1) {
    $scroller = $null; $lastY = $null; $stuck = 0
    for ($i = 0; $i -lt 60; $i++) {
        $t = Window-Text $hwnd $text
        if ($t -and $t.Y -ge $top -and $t.Y -le $bottom -and $t.Height -gt 0) { return $t }
        if ($t -and $lastY -ne $null -and $t.Y -eq $lastY) { $stuck++ } else { $stuck = 0 }
        $lastY = if ($t) { $t.Y } else { $null }
        if ($stuck -ge 3 -and -not $scroller) {
            Log '  the wheel did not scroll; using UI Automation scrolling'
            $cond = New-Object System.Windows.Automation.PropertyCondition($UIA::IsScrollPatternAvailableProperty, $true)
            foreach ($e in $UIA::FromHandle($hwnd).FindAll([System.Windows.Automation.TreeScope]::Descendants, $cond)) {
                $sp = $e.GetCurrentPattern([System.Windows.Automation.ScrollPattern]::Pattern)
                if ($sp.Current.VerticallyScrollable) { $scroller = $sp; break }
            }
        }
        if ($scroller) { $scroller.Scroll([System.Windows.Automation.ScrollAmount]::NoAmount, [System.Windows.Automation.ScrollAmount]::SmallIncrement); Wait 90 }
        else { [Rec]::Wheel(-$notches); Wait 110 }
    }
    throw "Could not scroll to '$text'."
}
function Clear-Stage {
    [Rec]::Place($script:edge, -3000, 100, 900, 660)
    [Rec]::Place($script:paint, -3000, 130, 860, 760)
    [Rec]::Place($script:explorer, -3000, 90, 960, 780)
}

# Settings: a quick scroll through the options, down to the sound packs.
$all['12-settings'] = {
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Beach'), (Item 'Mountains'), (Item 'Ideas'))))
    Clear-Stage
    Show-Pouch 330 420
    $p = [Rec]::Rect((Pouch))
    [Rec]::Place((Pouch), 60, 170, $p.Width, $p.Height); Wait 300
    $r = Pouch-Rect
    [Rec]::Move($r.Left + 200, $r.Top + 60, 300); [Rec]::Click(); Wait 200
    Record '12-settings' 17 {
        Wait 300
        [Rec]::Combo(@($VK.Ctrl), [byte]0x4B); Wait 450                          # Ctrl+K
        [Rec]::Type('settings', 100); Wait 350
        [Rec]::Key($VK.Enter); Wait 1300
        $settings = [Rec]::FindTitle('Pouchy Settings')
        if ($settings -eq [IntPtr]::Zero) { throw 'Settings did not open.' }
        $s = [Rec]::Rect($settings)
        [Rec]::Move($s.Left + 220, $s.Top + 22, 500); [Rec]::Click(); Wait 200
        [Rec]::Move($s.Left + $s.Width / 2, $s.Top + $s.Height / 2, 500); Wait 200
        Scroll-To $settings 'Sound pack' 380 700 3 | Out-Null
        Wait 400
        $combo = Row-Control $settings 'Sound pack' ([System.Windows.Automation.ControlType]::ComboBox)
        [Rec]::Move($combo.X, $combo.Y, 600); Wait 200
        [Rec]::Click(); Wait 1400                                                 # the list of packs
        [Rec]::Key($VK.Esc); Wait 400
        [Rec]::Move($s.Left + $s.Width / 2, $s.Top + $s.Height / 2 - 60, 400)
        Scroll-To $settings 'Shake to open' 300 600 2 | Out-Null
        Wait 1000
    }
}

# Sound packs: Settings opened at the Sounds section, then the list of packs.
$all['14-sounds'] = {
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Beach'), (Item 'Mountains'), (Item 'Ideas')))) 0 @{ SoundPack = 'Soft' }
    Clear-Stage
    Show-Pouch 330 420
    $p = [Rec]::Rect((Pouch))
    [Rec]::Place((Pouch), 60, 170, $p.Width, $p.Height); Wait 300
    $r = Pouch-Rect
    [Rec]::Move($r.Left + 200, $r.Top + 60, 300); [Rec]::Click(); Wait 200
    [Rec]::Combo(@($VK.Ctrl), [byte]0x4B); Wait 450
    [Rec]::Type('settings', 50); Wait 300
    [Rec]::Key($VK.Enter); Wait 1500
    $settings = [Rec]::FindTitle('Pouchy Settings')
    if ($settings -eq [IntPtr]::Zero) { throw 'Settings did not open.' }
    $s = [Rec]::Rect($settings)
    [Rec]::Move($s.Left + 220, $s.Top + 22, 300); [Rec]::Click(); Wait 200
    [Rec]::Move($s.Left + $s.Width / 2, $s.Top + $s.Height / 2, 300)
    Scroll-To $settings 'Sound pack' 420 620 4 | Out-Null
    Wait 800
    [Rec]::Move($s.Left + $s.Width / 2 - 120, $s.Top + $s.Height - 160, 300)
    Record '14-sounds' 8 {
        Wait 400
        $combo = Row-Control $settings 'Sound pack' ([System.Windows.Automation.ControlType]::ComboBox)
        [Rec]::Move($combo.X, $combo.Y, 650); Wait 200
        [Rec]::Click(); Wait 1200                                                 # Off, Soft, Bubbly, Clicky, Custom
        $bubbly = $null
        foreach ($i in (Find-All $settings ([System.Windows.Automation.ControlType]::ListItem))) { if ($i.Current.Name -match 'Bubbly') { $bubbly = Center $i } }
        if (-not $bubbly) { foreach ($h in [Rec]::FindPopups($script:pouchy.Id)) { foreach ($i in (Find-All $h ([System.Windows.Automation.ControlType]::ListItem))) { if ($i.Current.Name -match 'Bubbly') { $bubbly = Center $i } } } }
        if ($bubbly) { [Rec]::Move($bubbly.X, $bubbly.Y, 450); Wait 300; [Rec]::Click() } else { Log '  Bubbly not found'; [Rec]::Key($VK.Esc) }
        Wait 1500
    }
}

# Quick Look: Space previews the selected item, like in Finder.
$all['15-quicklook'] = {
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Meeting note'), (Item 'Mountains'), (Item 'Ideas'), (Item 'Beach'), (Item 'Forest'))))
    Clear-Stage
    Show-Pouch 330 420
    $p = [Rec]::Rect((Pouch))
    [Rec]::Place((Pouch), 12, 200, $p.Width, $p.Height); Wait 300
    [Rec]::Move(760, 700, 300)
    Record '15-quicklook' 12 {
        Wait 300
        $tiles = Pouch-Tiles
        foreach ($n in 0, 1) {
            [Rec]::Move($tiles[$n].X, $tiles[$n].Y, 600); Wait 150
            [Rec]::Click(); Wait 300
            [Rec]::Key([byte]0x20); Wait 2300                                    # Space: Quick Look
            [Rec]::Key($VK.Esc); Wait 700
        }
    }
}

# The theme editor: colours change in the pouch while you type them.
$all['13-theme-editor'] = {
    Start-Pouchy @((Shelf 'Pouch' @((Item 'Sunset'), (Item 'Beach'), (Item 'Mountains'), (Item 'Ideas'), (Item 'City lights'), (Item 'Forest'))))
    Clear-Stage
    Show-Pouch 330 420
    $p = [Rec]::Rect((Pouch))
    [Rec]::Place((Pouch), 60, 170, $p.Width, $p.Height); Wait 300
    $r = Pouch-Rect
    [Rec]::Move($r.Left + 200, $r.Top + 60, 300); [Rec]::Click(); Wait 200
    [Rec]::Combo(@($VK.Ctrl), [byte]0x4B); Wait 450
    [Rec]::Type('customize', 60); Wait 300
    [Rec]::Key($VK.Enter); Wait 1500
    $settings = [Rec]::FindTitle('Pouchy Settings')
    if ($settings -eq [IntPtr]::Zero) { throw 'Settings did not open.' }
    [Rec]::Place($settings, 1010, 40, 700, 900); Wait 600
    $button = $null
    foreach ($b in (Find-All $settings ([System.Windows.Automation.ControlType]::Button))) { if ($b.Current.Name -eq 'Customize this theme') { $button = Center $b } }
    if (-not $button) { throw "'Customize this theme' not found." }
    [Rec]::Move($button.X, $button.Y, 300); [Rec]::Click(); Wait 1500
    $editor = [Rec]::FindTitle('Theme editor')
    if ($editor -eq [IntPtr]::Zero) { throw 'The theme editor did not open.' }
    [Rec]::Place($settings, -3000, 60, 700, 900)
    [Rec]::Place($editor, 1010, 40, 700, 980)
    $pouch = Pouch
    $p = [Rec]::Rect($pouch)
    [Rec]::Place($pouch, 330, 170, $p.Width, $p.Height)
    Wait 600
    $editorRect = [Rec]::Rect($editor)
    [Rec]::Move($editorRect.Left + 300, $editorRect.Top + 200, 300)
    Scroll-To $editor 'Background, bottom' 300 900 | Out-Null
    Wait 400
    [Rec]::Move($editorRect.Left + 200, $editorRect.Top + 120, 300)
    Record '13-theme-editor' 14 {
        Wait 400
        foreach ($change in @(@('Accent', '#FF5C8A'), @('Background', '#4A1648'), @('Background, bottom', '#12071F'))) {
            $box = Row-Control $editor $change[0] ([System.Windows.Automation.ControlType]::Edit)
            [Rec]::Move($box.X, $box.Y, 650); Wait 120
            [Rec]::Click(); Wait 150
            [Rec]::Combo(@($VK.Ctrl), [byte]0x41); Wait 200                     # Ctrl+A
            [Rec]::Type($change[1], 95); Wait 900
        }
        $pr = [Rec]::Rect($pouch)
        [Rec]::Move($pr.Left + $pr.Width / 2, $pr.Top + $pr.Height / 2, 900); Wait 1500
    }
}

# ---------------------------------------------------------------------------------------------- Run

$Shots = @($Shots | ForEach-Object { $_ -split ',' } | Where-Object { $_ })   # "-Shots a,b" from cmd or bash arrives as one string
$selected = if ($Shots.Count -gt 0) { $Shots } else { @($all.Keys) }
try {
    Setup-Stage
    if ($CheckStage) { $selected = @() }
    foreach ($name in $selected) {
        if (-not $all.Contains($name)) { throw "Unknown shot '$name'. Shots: $($all.Keys -join ', ')" }
        try { & $all[$name] } catch { Log "  FAILED $name : $($_.Exception.Message)"; Log "  $($_.InvocationInfo.PositionMessage)" }
        Wait 600
    }
}
finally {
    Teardown-Stage
    Log 'done'
}
