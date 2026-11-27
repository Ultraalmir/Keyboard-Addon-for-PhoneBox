[CmdletBinding()]
param(
    [switch]$RemoveSettings
)

$ErrorActionPreference = 'Stop'

$processes = Get-Process -Name 'PhoneBoxKeyboardAddon' -ErrorAction SilentlyContinue
if ($processes) {
    $processes | Stop-Process -Force
    $processes | ForEach-Object {
        try { Wait-Process -Id $_.Id -Timeout 10 -ErrorAction SilentlyContinue } catch {}
    }
}

$installDir = Join-Path $env:LOCALAPPDATA 'Programs\PhoneBoxKeyboardAddon'
$startMenuDir = Join-Path $env:APPDATA 'Microsoft\Windows\Start Menu\Programs\PhoneBox Keyboard Add-on'
$startupShortcut = Join-Path ([Environment]::GetFolderPath('Startup')) 'PhoneBox Keyboard Add-on.lnk'
$settingsDir = Join-Path $env:APPDATA 'PhoneBoxKeyboardAddon'
$logsDir = Join-Path $env:LOCALAPPDATA 'PhoneBoxKeyboardAddon'

# Remove a instalação, o atalho de inicialização e os atalhos antigos do Menu Iniciar.
foreach ($item in @($startupShortcut, $startMenuDir, $installDir)) {
    if (Test-Path $item) {
        Remove-Item $item -Recurse -Force
    }
}

if ($RemoveSettings) {
    if (Test-Path $settingsDir) {
        Remove-Item $settingsDir -Recurse -Force
    }
    if (Test-Path $logsDir) {
        Remove-Item $logsDir -Recurse -Force
    }

    Write-Host 'Programa, configurações e logs removidos.' -ForegroundColor Yellow
}
else {
    Write-Host 'Programa removido. Configurações e logs preservados.' -ForegroundColor Green
}
