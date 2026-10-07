# Builds the finished trailer files in trailer/out from the editable project.
#   powershell -File trailer\scripts\render.ps1 [-Python C:\PouchyTrailer\venv\Scripts\python.exe] [-SkipVideo] [-Variants]
# -SkipVideo reuses out\video-60.mp4 (when only the audio changed).
# -Variants also writes the no-voice and musicless versions (same picture, different mix).
param(
    [string]$Python = 'C:\PouchyTrailer\venv\Scripts\python.exe',
    [switch]$SkipVideo,
    [switch]$Variants
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location $root
New-Item -ItemType Directory -Force out | Out-Null

& $Python scripts\prepare.py
& $Python audio\vo.py
& $Python audio\mix.py

if (-not $SkipVideo) {
    npx remotion render src/index.ts Master60 out/video-60.mp4 --concurrency=10
    if ($LASTEXITCODE) { throw 'Render failed.' }
}

# Web loudness (-14 LUFS, -1 dBTP) and AAC; the picture is copied as rendered.
function Mux($audio, $out) {
    ffmpeg -v error -y -i out/video-60.mp4 -i $audio -map 0:v -map 1:a -c:v copy `
        -af 'loudnorm=I=-14:TP=-1:LRA=11' -ar 48000 -c:a aac -b:a 320k -shortest -movflags +faststart $out
    if ($LASTEXITCODE) { throw "Mux failed: $out" }
}
Mux public/audio/mix-full.wav out/trailer-master-1080p60.mp4

# 30 fps: every other frame of the 60 fps master, re-encoded.
ffmpeg -v error -y -i out/trailer-master-1080p60.mp4 -vf fps=30 -c:v libx264 -preset slow -crf 14 -pix_fmt yuv420p `
    -c:a copy -movflags +faststart out/trailer-master-1080p30.mp4

if ($Variants) {
    Mux audio/build/mix-novoice.wav out/trailer-no-voice.mp4
    Mux audio/build/mix-musicless.wav out/trailer-musicless.mp4
}
Get-ChildItem out\trailer-*.mp4 | Select-Object Name, @{ n = 'MB'; e = { [math]::Round($_.Length / 1MB, 1) } }
