[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $root 'artifacts\publish'
$packageDir = Join-Path $root 'artifacts\package'
$zip = Join-Path $root 'artifacts\PhoneBoxKeyboardAddon-win-x64.zip'

if (-not (Test-Path (Join-Path $publishDir 'PhoneBoxKeyboardAddon.exe'))) {
    & (Join-Path $PSScriptRoot 'publish-windows.ps1')
    if ($LASTEXITCODE -ne 0) {
        throw 'A publicação falhou.'
    }
}

if (Test-Path $packageDir) {
    Remove-Item $packageDir -Recurse -Force
}

New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

# Copia todos os arquivos necessários da publicação do .NET.
$runtimeFiles = Get-ChildItem $publishDir -File

if (-not ($runtimeFiles | Where-Object Name -eq 'PhoneBoxKeyboardAddon.exe')) {
    throw 'Executável não encontrado na publicação.'
}

foreach ($file in $runtimeFiles) {
    Copy-Item $file.FullName $packageDir
}

# Arquivos adicionais do pacote.
$files = @(
    (Join-Path $root 'README.md'),
    (Join-Path $root 'Abrir-Configuracoes.bat'),
    (Join-Path $root 'LICENSE'),
    (Join-Path $PSScriptRoot 'install.ps1'),
    (Join-Path $PSScriptRoot 'uninstall.ps1')
)

foreach ($file in $files) {
    if (-not (Test-Path $file -PathType Leaf)) {
        throw "Arquivo obrigatório não encontrado: $file"
    }

    Copy-Item $file $packageDir
}

if (Test-Path $zip) {
    Remove-Item $zip -Force
}

Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $zip -CompressionLevel Optimal

$hash = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLowerInvariant()

Set-Content -Path ($zip + '.sha256') `
    -Value "$hash  PhoneBoxKeyboardAddon-win-x64.zip" `
    -Encoding ascii

Write-Host "Pacote: $zip" -ForegroundColor Green
Write-Host "SHA-256: $hash"