# Builds the finished trailer files in trailer/out from the editable project.
#   powershell -File trailer\scripts\render.ps1 [-Music phonk|classic|path\to\song.wav] [-Vertical] [-Variants] [-Readme]
#
# -Music     the soundtrack (see audio/mix.py): the phonk track (default), the original synth-pop one,
#            or any WAV you have the rights to.
# -SkipVideo reuses the last picture renders (when only the audio changed).
# -Vertical  also renders the 9:16 cut for Reels, Shorts and TikTok.
# -Variants  also writes the no-voice, musicless and effects-only versions (same picture, other mixes).
#            Post the effects-only one when you want to add a sound from the app's own music library.
# -Readme    writes the README's trailer.mp4, trailer-vertical.mp4 and preview.webp into docs/media.
# -Concurrency frames rendered at once; lower it if Chrome runs out of memory.
param(
    [string]$Python = 'C:\PouchyTrailer\venv\Scripts\python.exe',
    [string]$Music = 'phonk',
    [switch]$SkipVideo,
    [switch]$Vertical,
    [switch]$Variants,
    [switch]$Readme,
    [int]$Concurrency = 6
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
New-Item -ItemType Directory -Force out | Out-Null

& $Python scripts\prepare.py
& $Python audio\vo.py
& $Python audio\mix.py --music $Music

function Render($composition, $out) {
    npx remotion render src/index.ts $composition $out --concurrency=$Concurrency --offthreadvideo-cache-size-in-bytes=536870912
    if ($LASTEXITCODE) { throw "Render failed: $composition" }
}
if (-not $SkipVideo) {
    Render Master60 out/video-60.mp4
    if ($Vertical -or $Readme) { Render Vertical out/video-vertical.mp4 }
}

# Web loudness (-14 LUFS, -1 dBTP) and AAC; the picture is copied as rendered.
function Mux($video, $audio, $out) {
    ffmpeg -v error -y -i $video -i $audio -map 0:v -map 1:a -c:v copy `
        -af 'loudnorm=I=-14:TP=-1:LRA=11' -ar 48000 -c:a aac -b:a 320k -shortest -movflags +faststart $out
    if ($LASTEXITCODE) { throw "Mux failed: $out" }
}
Mux out/video-60.mp4 public/audio/mix-full.wav out/trailer-master-1080p60.mp4

# 30 fps: every other frame of the 60 fps master, re-encoded.
ffmpeg -v error -y -i out/trailer-master-1080p60.mp4 -vf fps=30 -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p `
    -c:a copy -movflags +faststart out/trailer-master-1080p30.mp4

if ($Vertical -or $Readme) { Mux out/video-vertical.mp4 public/audio/mix-full.wav out/trailer-vertical-1080x1920.mp4 }

if ($Variants) {
    Mux out/video-60.mp4 audio/build/mix-novoice.wav out/trailer-no-voice.mp4
    Mux out/video-60.mp4 audio/build/mix-musicless.wav out/trailer-musicless.mp4
    Mux out/video-60.mp4 audio/build/mix-sfx.wav out/trailer-sfx-only.mp4
    if ($Vertical) { Mux out/video-vertical.mp4 audio/build/mix-sfx.wav out/trailer-vertical-sfx-only.mp4 }
}

if ($Readme) {
    $media = Join-Path (Split-Path $root -Parent) 'docs\media'
    # Small enough to keep in the repository, still sharp: 1080p30 and 720x1280.
    ffmpeg -v error -y -i out/trailer-master-1080p30.mp4 -c:v libx264 -preset slow -crf 26 -pix_fmt yuv420p `
        -c:a aac -b:a 160k -movflags +faststart "$media\trailer.mp4"
    ffmpeg -v error -y -i out/trailer-vertical-1080x1920.mp4 -vf scale=720:1280 -c:v libx264 -preset slow -crf 26 -pix_fmt yuv420p `
        -c:a aac -b:a 160k -movflags +faststart "$media\trailer-vertical.mp4"
    # The silent loop at the top of the README: the shake, the pop and the drop. Animated WebP: a third
    # of the size of a GIF, and sharper.
    ffmpeg -v error -y -ss 3.6 -t 8.9 -i out/video-60.mp4 -vf 'fps=20,scale=860:-1:flags=lanczos' `
        -c:v libwebp -quality 72 -compression_level 6 -loop 0 -an "$media\preview.webp"
    Get-ChildItem "$media\trailer*.mp4", "$media\preview.webp" | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
}
Get-ChildItem out\trailer-*.mp4 | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
