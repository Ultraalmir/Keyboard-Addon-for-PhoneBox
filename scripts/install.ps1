
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

$publishDir = Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\publish'

# Identifica a pasta que contém a publicação completa.
$sourceDir = $null

if (Test-Path (Join-Path $PSScriptRoot 'PhoneBoxKeyboardAddon.exe') -PathType Leaf) {
    $sourceDir = $PSScriptRoot
}
elseif (Test-Path (Join-Path $publishDir 'PhoneBoxKeyboardAddon.exe') -PathType Leaf) {
    $sourceDir = $publishDir
}
else {
    $publishScript = Join-Path $PSScriptRoot 'publish-windows.ps1'

    if (Test-Path $publishScript) {
        & $publishScript

        if ($LASTEXITCODE -ne 0) {
            throw 'A publicação do executável falhou.'
        }

        if (Test-Path (Join-Path $publishDir 'PhoneBoxKeyboardAddon.exe') -PathType Leaf) {
            $sourceDir = $publishDir
        }
    }
}

if (-not $sourceDir) {
    throw 'PhoneBoxKeyboardAddon.exe não encontrado. Execute scripts\publish-windows.ps1 ou use o pacote de release.'
}

# Encerra a instância anterior antes de substituir os arquivos.
$running = Get-Process -Name 'PhoneBoxKeyboardAddon' -ErrorAction SilentlyContinue

if ($running) {
    $running | Stop-Process -Force

    $running | ForEach-Object {
        try {
            Wait-Process -Id $_.Id -Timeout 5 -ErrorAction SilentlyContinue
        }
        catch {}
    }
}

New-Item -ItemType Directory -Path $installDir -Force | Out-Null
New-Item -ItemType Directory -Path $settingsDir -Force | Out-Null

# Arquivos do pacote que não pertencem à publicação do aplicativo.
$excludedNames = @(
    'install.ps1',
    'uninstall.ps1',
    'package.ps1',
    'publish-windows.ps1',
    'README.md',
    'LICENSE',
    'Abrir-Configuracoes.bat'
)

# Copia todos os arquivos da publicação, incluindo DLLs e arquivos do .NET.
$runtimeFiles = Get-ChildItem -LiteralPath $sourceDir -File -Recurse |
    Where-Object {
        $relativePath = $_.FullName.Substring($sourceDir.Length).TrimStart([char[]]@('\', '/'))
        $firstSegment = $relativePath.Split([char[]]@('\', '/'))[0]

        $firstSegment -notin $excludedNames -and
        $_.Extension -notin @('.ps1', '.md', '.bat')
    }

if (-not ($runtimeFiles | Where-Object Name -eq 'PhoneBoxKeyboardAddon.exe')) {
    throw 'A publicação não contém PhoneBoxKeyboardAddon.exe.'
}

foreach ($file in $runtimeFiles) {
    $relativePath = $file.FullName.Substring($sourceDir.Length).TrimStart([char[]]@('\', '/'))
    $destination = Join-Path $installDir $relativePath
    $destinationDirectory = Split-Path -Parent $destination

    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}

# Remove atalhos antigos do Menu Iniciar criados por versões anteriores.
if (Test-Path $startMenuDir) {
    Remove-Item $startMenuDir -Recurse -Force
}

# A inicialização automática continua opcional.
# Não são criados atalhos no Menu Iniciar.
if ($StartWithWindows) {
    $shell = New-Object -ComObject WScript.Shell

    try {
        $shortcut = $shell.CreateShortcut(
            (Join-Path $startupDir 'PhoneBox Keyboard Add-on.lnk')
        )

        $shortcut.TargetPath = Join-Path $installDir 'PhoneBoxKeyboardAddon.exe'
        $shortcut.Arguments = '--background'
        $shortcut.WorkingDirectory = $installDir
        $shortcut.Description = 'Iniciar PhoneBox Keyboard Add-on com o Windows.'
        $shortcut.WindowStyle = 7
        $shortcut.Save()
    }
    finally {
        if ($shortcut) {
            [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shortcut)
        }

        [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell)
    }

    $settingsFile = Join-Path $settingsDir 'settings.json'

    if (Test-Path $settingsFile) {
        $appSettings = Get-Content $settingsFile -Raw | ConvertFrom-Json
    }
    else {
        $appSettings = [pscustomobject]@{
            schemaVersion = 1
            shortcutsEnabled = $true
            startWithWindows = $false
            phoneBoxUrl = 'http://127.0.0.1:8765'
            bindings = @(
                1..7 | ForEach-Object {
                    [pscustomobject]@{
                        page = $_
                        enabled = $true
                        modifiers = @('Ctrl')
                        key = "Num$_"
                    }
                }
            )
        }
    }

    $appSettings | Add-Member -NotePropertyName startWithWindows -NotePropertyValue $true -Force

    $settingsJson = $appSettings | ConvertTo-Json -Depth 10

    [System.IO.File]::WriteAllText(
        $settingsFile,
        $settingsJson,
        [System.Text.UTF8Encoding]::new($false)
    )
}

Write-Host 'PhoneBox Keyboard Add-on instalado para o usuário atual.' -ForegroundColor Green
Write-Host "Executável: $(Join-Path $installDir 'PhoneBoxKeyboardAddon.exe')"
Write-Host 'Acesso às configurações: Abrir-Configuracoes.bat (na pasta extraída do pacote).'

if ($LaunchSettings) {
    Start-Process (Join-Path $installDir 'PhoneBoxKeyboardAddon.exe') -ArgumentList '--settings'
}
