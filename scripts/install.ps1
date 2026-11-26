[CmdletBinding()]
param(
    [switch]$StartWithWindows,
    [switch]$LaunchSettings
)
$ErrorActionPreference = 'Stop'
$installDir = Join-Path $env:LOCALAPPDATA 'Programs\PhoneBoxKeyboardAddon'
$settingsDir = Join-Path $env:APPDATA 'PhoneBoxKeyboardAddon'
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\PhoneBox Keyboard Add-on'
$startupDir = [Environment]::GetFolderPath('Startup')

$candidates = @(
    (Join-Path $PSScriptRoot 'PhoneBoxKeyboardAddon.exe'),
    (Join-Path $PSScriptRoot '..\artifacts\publish\PhoneBoxKeyboardAddon.exe')
)
$sourceExe = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $sourceExe) {
    $publishScript = Join-Path $PSScriptRoot 'publish-windows.ps1'
    if (Test-Path $publishScript) {
        & $publishScript
        if ($LASTEXITCODE -ne 0) { throw 'A publicação do executável falhou.' }
        $sourceExe = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\publish\PhoneBoxKeyboardAddon.exe'
    }
}
if (-not $sourceExe -or -not (Test-Path $sourceExe)) {
    throw 'PhoneBoxKeyboardAddon.exe não encontrado. Execute scripts\publish-windows.ps1 ou use o pacote de release.'
}

# Stop the existing per-user instance before replacing its executable during an update.
$running = Get-Process -Name 'PhoneBoxKeyboardAddon' -ErrorAction SilentlyContinue
if ($running) {
    $running | Stop-Process -Force
    $running | ForEach-Object { try { Wait-Process -Id $_.Id -Timeout 5 -ErrorAction SilentlyContinue } catch {} }
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null
New-Item -ItemType Directory -Path $startMenuDir -Force | Out-Null
Copy-Item $sourceExe (Join-Path $installDir 'PhoneBoxKeyboardAddon.exe') -Force

function New-AddonShortcut([string]$Path, [string]$Arguments, [string]$Description) {
    $shell = New-Object -ComObject WScript.Shell
    $shortcut = $shell.CreateShortcut($Path)
    $shortcut.TargetPath = Join-Path $installDir 'PhoneBoxKeyboardAddon.exe'
    $shortcut.Arguments = $Arguments
    $shortcut.WorkingDirectory = $installDir
    $shortcut.Description = $Description
    $shortcut.WindowStyle = 7
    $shortcut.Save()
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
}

New-AddonShortcut (Join-Path $startMenuDir 'Configurações.lnk') '--settings' 'Configurar atalhos globais do PhoneBox.'
New-AddonShortcut (Join-Path $startMenuDir 'Iniciar em segundo plano.lnk') '--background' 'Iniciar o PhoneBox Keyboard Add-on sem janelas.'
if ($StartWithWindows) {
    New-AddonShortcut (Join-Path $startupDir 'PhoneBox Keyboard Add-on.lnk') '--background' 'Inicialização silenciosa do PhoneBox Keyboard Add-on.'
    $settingsFile = Join-Path $settingsDir 'settings.json'
    if (Test-Path $settingsFile) {
        $appSettings = Get-Content $settingsFile -Raw | ConvertFrom-Json
    }
    else {
        # Keep the default layout aligned with AddonSettings in the application.
        $appSettings = [pscustomobject]@{
            schemaVersion = 1
            shortcutsEnabled = $true
            startWithWindows = $false
            phoneBoxUrl = 'http://127.0.0.1:8765'
            bindings = @(
                1..7 | ForEach-Object { [pscustomobject]@{ page = $_; enabled = $true; modifiers = @('Ctrl'); key = "Num$_" } }
            )
        }
    }
    $appSettings | Add-Member -NotePropertyName startWithWindows -NotePropertyValue $true -Force
    # Write UTF-8 without a BOM so System.Text.Json can read this file on Windows PowerShell 5.1.
    $settingsJson = $appSettings | ConvertTo-Json -Depth 10
    [System.IO.File]::WriteAllText($settingsFile, $settingsJson, [System.Text.UTF8Encoding]::new($false))
}

Write-Host 'PhoneBox Keyboard Add-on instalado para o usuário atual.' -ForegroundColor Green
Write-Host "Executável: $(Join-Path $installDir 'PhoneBoxKeyboardAddon.exe')"
Write-Host "Configurações: $startMenuDir\Configurações.lnk"
if ($LaunchSettings) { Start-Process (Join-Path $installDir 'PhoneBoxKeyboardAddon.exe') -ArgumentList '--settings' }
