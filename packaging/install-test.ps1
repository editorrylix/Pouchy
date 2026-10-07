<#
.SYNOPSIS
    Installs the test-signed Pouchy package on this PC, to try the Store version before submitting.
    Run as administrator after:  packaging\build-msix.ps1 -TestSign

    It trusts the self-signed test certificate (dist\msix\Pouchy-test.cer) for this PC, then
    installs the bundle. Remove it later with: Get-AppxPackage *Pouchy* | Remove-AppxPackage
#>
$ErrorActionPreference = 'Stop'
$out = Join-Path (Split-Path $PSScriptRoot) 'dist\msix'
$cer = Join-Path $out 'Pouchy-test.cer'
$bundle = Get-ChildItem $out -Filter '*-testsigned.msixbundle' | Select-Object -First 1
if (-not (Test-Path $cer) -or -not $bundle) { throw 'Run packaging\build-msix.ps1 -TestSign first.' }

Import-Certificate -FilePath $cer -CertStoreLocation Cert:\LocalMachine\TrustedPeople | Out-Null
Add-AppxPackage -Path $bundle.FullName
Write-Host 'Installed. Find Pouchy in the Start menu.'
