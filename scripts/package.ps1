[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'artifacts\publish'
$packageDir = Join-Path $root 'artifacts\package'
$zip = Join-Path $root 'artifacts\PhoneBoxKeyboardAddon-win-x64.zip'

if (-not (Test-Path (Join-Path $publishDir 'PhoneBoxKeyboardAddon.exe'))) {
    & (Join-Path $PSScriptRoot 'publish-windows.ps1')
    if ($LASTEXITCODE -ne 0) { throw 'A publicação falhou.' }
}
if (Test-Path $packageDir) { Remove-Item $packageDir -Recurse -Force }
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
Copy-Item (Join-Path $publishDir 'PhoneBoxKeyboardAddon.exe') $packageDir
Copy-Item (Join-Path $root 'README.md') $packageDir
Copy-Item (Join-Path $root 'LICENSE') $packageDir
Copy-Item (Join-Path $PSScriptRoot 'install.ps1') $packageDir
Copy-Item (Join-Path $PSScriptRoot 'uninstall.ps1') $packageDir
if (Test-Path $zip) { Remove-Item $zip -Force }
Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $zip -CompressionLevel Optimal
$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -Path ($zip + '.sha256') -Value "$hash  PhoneBoxKeyboardAddon-win-x64.zip" -Encoding ascii
Write-Host "Pacote: $zip" -ForegroundColor Green
Write-Host "SHA-256: $hash"
