<#
.SYNOPSIS
    Builds Pouchy's Microsoft Store package: an .msixupload (and the .msixbundle and per-architecture
    .msix files inside it) for x64 and ARM64.

.DESCRIPTION
    1. Publishes Pouchy self-contained for each architecture (no .NET install needed on the PC).
    2. Adds the logo images and the manifest (identity from store-identity.json).
    3. Indexes the images with makepri, packs each folder with makeappx, bundles them, and zips
       the bundle into an .msixupload for Partner Center.

    The Windows SDK packaging tools (makeappx, makepri, signtool) are taken from the
    Microsoft.Windows.SDK.BuildTools NuGet package, downloaded once into .tools\ if needed.

    Store uploads don't need signing: the Store signs the package. -TestSign also makes a
    self-signed copy you can install on your own PC to try the package before submitting.

.EXAMPLE
    pwsh packaging/build-msix.ps1
    pwsh packaging/build-msix.ps1 -Version 1.1.0 -TestSign
#>
[CmdletBinding()]
param(
    # Semantic version, e.g. 1.1.0. Defaults to <Version> in Pouchy.csproj.
    [string]$Version,
    [string[]]$Architectures = @('x64', 'arm64'),
    # Defaults: packaging\store-identity.json and dist\msix.
    [string]$IdentityFile,
    [string]$OutDir,
    # Also produce a self-signed bundle for installing on this PC.
    [switch]$TestSign
)

$ErrorActionPreference = 'Stop'
# (Windows PowerShell 5.1 doesn't know $PSScriptRoot yet while reading parameter defaults.)
$root = Split-Path $PSScriptRoot
if (-not $IdentityFile) { $IdentityFile = Join-Path $PSScriptRoot 'store-identity.json' }
if (-not $OutDir) { $OutDir = Join-Path $root 'dist\msix' }
$work = Join-Path $root 'obj\msix'

function Fail($message) { throw "build-msix: $message" }

function Invoke-Tool([string]$exe, [string[]]$arguments) {
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) { Fail "$(Split-Path $exe -Leaf) failed with exit code $LASTEXITCODE." }
}

# ---------------------------------------------------------------------------------------------- Version

if (-not $Version) {
    $Version = ([xml](Get-Content (Join-Path $root 'Pouchy.csproj'))).Project.PropertyGroup.Version | Where-Object { $_ } | Select-Object -First 1
}
if ($Version -notmatch '^(\d+)\.(\d+)\.(\d+)') { Fail "'$Version' isn't a version like 1.2.3." }
# The Store needs four numbers and reserves the last one, so it is always 0.
$packageVersion = "$($Matches[1]).$($Matches[2]).$($Matches[3]).0"
Write-Host "Pouchy $Version -> package version $packageVersion"

# ---------------------------------------------------------------------------------------------- Identity

$identity = Get-Content $IdentityFile -Raw | ConvertFrom-Json
foreach ($field in 'IdentityName', 'Publisher', 'PublisherDisplayName') {
    if (-not $identity.$field) { Fail "$IdentityFile has no $field." }
}
$isPlaceholder = $identity.IdentityName -like 'PLACEHOLDER*' -or $identity.Publisher -like '*00000000-0000-0000-0000-000000000000*'
if ($isPlaceholder) {
    Write-Warning "store-identity.json still has placeholder values. The package will build, but Partner Center will reject it until you paste in your app's Product identity."
}

# ---------------------------------------------------------------------------------------------- Tools

function Get-SdkTools {
    $toolsRoot = Join-Path $root '.tools\sdk-buildtools'
    $makeappx = Get-ChildItem $toolsRoot -Recurse -Filter makeappx.exe -ErrorAction SilentlyContinue |
        Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
    if (-not $makeappx) {
        Write-Host 'Downloading the Windows SDK packaging tools (Microsoft.Windows.SDK.BuildTools)...'
        $versions = (Invoke-RestMethod 'https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/index.json').versions |
            Where-Object { $_ -notmatch '-' }
        $latest = $versions[-1]
        New-Item -ItemType Directory -Force $toolsRoot | Out-Null
        $package = Join-Path $toolsRoot "buildtools.$latest.zip"
        Invoke-WebRequest "https://api.nuget.org/v3-flatcontainer/microsoft.windows.sdk.buildtools/$latest/microsoft.windows.sdk.buildtools.$latest.nupkg" -OutFile $package
        Expand-Archive $package -DestinationPath (Join-Path $toolsRoot $latest) -Force
        Remove-Item $package
        $makeappx = Get-ChildItem $toolsRoot -Recurse -Filter makeappx.exe | Where-Object { $_.FullName -match '\\x64\\' } | Select-Object -First 1
        if (-not $makeappx) { Fail 'makeappx.exe not found in the downloaded build tools.' }
    }
    $bin = $makeappx.DirectoryName
    [pscustomobject]@{
        MakeAppx = $makeappx.FullName
        MakePri = Join-Path $bin 'makepri.exe'
        SignTool = Join-Path $bin 'signtool.exe'
    }
}

$tools = Get-SdkTools
Write-Host "Using $($tools.MakeAppx)"

# ---------------------------------------------------------------------------------------------- Build each architecture

if (Test-Path $work) { Remove-Item -Recurse -Force $work }
New-Item -ItemType Directory -Force $work, $OutDir | Out-Null
$template = Get-Content (Join-Path $PSScriptRoot 'Package.appxmanifest') -Raw
$bundleInput = Join-Path $work 'bundle'
New-Item -ItemType Directory -Force $bundleInput | Out-Null

foreach ($arch in $Architectures) {
    Write-Host "`n=== $arch ===" -ForegroundColor Cyan
    $layout = Join-Path $work "layout-$arch"

    # Self-contained, but not single-file: a package is already one file, and loose assemblies
    # load straight from disk.
    dotnet publish (Join-Path $root 'Pouchy.csproj') -c Release -r "win-$arch" --self-contained true `
        -p:PublishSingleFile=false -p:DebugType=none -p:Version=$Version -o $layout --nologo
    if ($LASTEXITCODE -ne 0) { Fail "dotnet publish failed for $arch." }

    Copy-Item (Join-Path $PSScriptRoot 'Assets') (Join-Path $layout 'Assets') -Recurse -Force
    $manifest = $template
    $values = [ordered]@{
        '$(IdentityName)'         = [Security.SecurityElement]::Escape($identity.IdentityName)
        '$(PublisherDisplayName)' = [Security.SecurityElement]::Escape($identity.PublisherDisplayName)
        '$(Publisher)'            = [Security.SecurityElement]::Escape($identity.Publisher)
        '$(Version)'              = $packageVersion
        '$(Architecture)'         = $arch
    }
    foreach ($key in $values.Keys) { $manifest = $manifest.Replace($key, $values[$key]) }
    Set-Content -Path (Join-Path $layout 'AppxManifest.xml') -Value $manifest -Encoding utf8

    # resources.pri lets Windows pick the right logo size for each display scale.
    $priConfig = Join-Path $work "priconfig-$arch.xml"
    Invoke-Tool $tools.MakePri @('createconfig', '/cf', $priConfig, '/dq', 'en-US', '/o')
    # One index with every logo size. The default config splits sizes into separate resource
    # packages, which this package doesn't have, so Windows would only find the 100% images.
    $config = [xml](Get-Content $priConfig)
    $packaging = $config.SelectSingleNode('/resources/packaging')
    if ($packaging) { [void]$packaging.ParentNode.RemoveChild($packaging) }
    $config.Save($priConfig)
    Invoke-Tool $tools.MakePri @('new', '/pr', $layout, '/cf', $priConfig, '/mn', (Join-Path $layout 'AppxManifest.xml'), '/of', (Join-Path $layout 'resources.pri'), '/o')

    $msix = Join-Path $bundleInput "Pouchy_${packageVersion}_$arch.msix"
    Invoke-Tool $tools.MakeAppx @('pack', '/o', '/h', 'SHA256', '/d', $layout, '/p', $msix)
}

# ---------------------------------------------------------------------------------------------- Bundle and upload file

$archNames = ($Architectures -join '_')
$bundle = Join-Path $OutDir "Pouchy_${packageVersion}_$archNames.msixbundle"
Write-Host "`n=== bundle ===" -ForegroundColor Cyan
Invoke-Tool $tools.MakeAppx @('bundle', '/o', '/bv', $packageVersion, '/d', $bundleInput, '/p', $bundle)
Copy-Item (Join-Path $bundleInput '*.msix') $OutDir -Force

# An .msixupload is a zip holding the bundle (and optionally symbols); Partner Center prefers it.
$upload = Join-Path $OutDir "Pouchy_${packageVersion}_$archNames.msixupload"
$zip = "$upload.zip"
if (Test-Path $zip) { Remove-Item $zip }
Compress-Archive -Path $bundle -DestinationPath $zip -CompressionLevel NoCompression
Move-Item $zip $upload -Force

# ---------------------------------------------------------------------------------------------- Optional test signing

if ($TestSign) {
    Write-Host "`n=== test signing ===" -ForegroundColor Cyan
    $subject = $identity.Publisher
    $cert = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $subject -and $_.FriendlyName -eq 'Pouchy MSIX test' } | Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $subject -FriendlyName 'Pouchy MSIX test' `
            -KeyUsage DigitalSignature -CertStoreLocation Cert:\CurrentUser\My `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
    }
    $cerFile = Join-Path $OutDir 'Pouchy-test.cer'
    Export-Certificate -Cert $cert -FilePath $cerFile | Out-Null

    $signed = Join-Path $OutDir "Pouchy_${packageVersion}_$archNames-testsigned.msixbundle"
    Copy-Item $bundle $signed -Force
    Invoke-Tool $tools.SignTool @('sign', '/fd', 'SHA256', '/sha1', $cert.Thumbprint, '/s', 'My', $signed)
    Write-Host "Test-signed bundle: $signed"
    Write-Host "To install it, run packaging\install-test.ps1 as administrator (it trusts $cerFile, then installs)."
}

Write-Host "`nDone:" -ForegroundColor Green
Get-ChildItem $OutDir | Sort-Object Name | ForEach-Object { '  {0,-60} {1,8:N1} MB' -f $_.Name, ($_.Length / 1MB) }
if ($isPlaceholder) { Write-Warning 'Remember: fill in store-identity.json before uploading.' }
