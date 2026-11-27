
[CmdletBinding()]
param(
    [switch]$StartWithWindows,
    [switch]$LaunchSettings
)

$ErrorActionPreference = 'Stop'

function Test-PhoneBoxPackageManifest {
    param([Parameter(Mandatory = $true)][string]$PackageRoot)

    try {
        $manifestName = 'PhoneBoxPackage.manifest'
        $markerName = '.phonebox-keyboard-addon-package'
        $manifestPath = Join-Path $PackageRoot $manifestName
        if (-not (Test-Path -LiteralPath $manifestPath -PathType Leaf)) { return $false }
        if (([System.IO.File]::GetAttributes($manifestPath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }

        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json -ErrorAction Stop
        if ($manifest.format -ne 'PhoneBoxKeyboardAddonPackageManifest:v1' -or -not $manifest.files) { return $false }

        $expectedNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        $hasRequired = @{
            'PhoneBoxKeyboardAddon.exe' = $false
            'Abrir-Configuracoes.exe' = $false
            'install.ps1' = $false
            'uninstall.ps1' = $false
            'README.md' = $false
            'LICENSE' = $false
        }

        foreach ($entry in $manifest.files) {
            $name = [string]$entry.name
            $hash = [string]$entry.sha256
            if (-not $name -or $name -in @('.', '..') -or $name.Contains('/') -or $name.Contains('\') -or $name.Contains(':')) { return $false }
            if ($name -ieq $manifestName -or $name -ieq $markerName) { return $false }
            if ($hash -notmatch '^[A-Fa-f0-9]{64}$') { return $false }
            if (-not $expectedNames.Add($name)) { return $false }

            $filePath = Join-Path $PackageRoot $name
            if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { return $false }
            if (([System.IO.File]::GetAttributes($filePath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
            $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
            if (-not [string]::Equals($actualHash, $hash, [StringComparison]::OrdinalIgnoreCase)) { return $false }
            if ($hasRequired.ContainsKey($name)) { $hasRequired[$name] = $true }
        }

        if (@($hasRequired.Values | Where-Object { -not $_ }).Count -gt 0) { return $false }

        $actualNames = @(Get-ChildItem -LiteralPath $PackageRoot -Force | ForEach-Object Name)

        # Build both sets item-by-item. Passing a PowerShell object[] directly to
        # the generic HashSet(IEnumerable<T>, comparer) constructor can fail
        # overload binding on Windows PowerShell; the previous catch silently
        # converted that exception into a false validation result.
        $expectedWithMetadata = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($expectedName in $expectedNames) {
            [void]$expectedWithMetadata.Add([string]$expectedName)
        }
        [void]$expectedWithMetadata.Add($manifestName)

        $markerPath = Join-Path $PackageRoot $markerName
        if (Test-Path -LiteralPath $markerPath) {
            if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) { return $false }
            if (([System.IO.File]::GetAttributes($markerPath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
            if ((Get-Content -LiteralPath $markerPath -Raw).Trim() -ne 'PhoneBoxKeyboardAddonPackage:v1') { return $false }
            [void]$expectedWithMetadata.Add($markerName)
        }

        if ($actualNames.Count -ne $expectedWithMetadata.Count) { return $false }
        $actualSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($actualName in $actualNames) {
            if (-not $actualSet.Add([string]$actualName)) { return $false }
        }
        return $actualSet.SetEquals($expectedWithMetadata)
    }
    catch {
        Write-Warning ("Falha ao validar o manifesto do pacote: {0}" -f $_.Exception.Message)
        return $false
    }
}

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

# O launcher é fornecido no ZIP oficial ou gerado pelo script de publicação.
$launcherCandidates = @(
    (Join-Path $PSScriptRoot 'Abrir-Configuracoes.exe'),
    (Join-Path (Split-Path -Parent $PSScriptRoot) 'Abrir-Configuracoes.exe'),
    (Join-Path (Split-Path -Parent $PSScriptRoot) 'artifacts\publish-launcher\Abrir-Configuracoes.exe')
)
$launcherSource = $launcherCandidates |
    Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } |
    Select-Object -First 1

if (-not $launcherSource) {
    throw 'Abrir-Configuracoes.exe não encontrado. Use o ZIP oficial ou execute scripts\publish-windows.ps1.'
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
    'Abrir-Configuracoes.bat',
    'Abrir-Configuracoes.exe',
    'PhoneBoxPackage.manifest',
    '.phonebox-keyboard-addon-package'
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

# Keep a copy beside the installed application. The extracted package can then
# be removed independently without taking away the user's settings launcher.
$installedLauncher = Join-Path $installDir 'Abrir-Configuracoes.exe'
Copy-Item -LiteralPath $launcherSource -Destination $installedLauncher -Force

# Register the extracted folder only when this is the official flat release package.
# Repository checkouts and development folders are deliberately never registered.
$packageRoot = (Resolve-Path -LiteralPath $PSScriptRoot).Path
$requiredPackageFiles = @('PhoneBoxKeyboardAddon.exe', 'Abrir-Configuracoes.exe', 'install.ps1', 'uninstall.ps1', 'README.md', 'LICENSE', 'PhoneBoxPackage.manifest')
$hasLauncher = Test-Path (Join-Path $packageRoot 'Abrir-Configuracoes.exe') -PathType Leaf
$canonicalPackageRoot = [System.IO.Path]::GetFullPath($packageRoot).TrimEnd('\')
$canonicalInstallDir = [System.IO.Path]::GetFullPath($installDir).TrimEnd('\')
$pathsOverlap = [string]::Equals($canonicalPackageRoot, $canonicalInstallDir, [StringComparison]::OrdinalIgnoreCase) -or
    $canonicalInstallDir.StartsWith($canonicalPackageRoot + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $canonicalPackageRoot.StartsWith($canonicalInstallDir + '\', [StringComparison]::OrdinalIgnoreCase)
$isReleasePackage = [string]::Equals(
    (Resolve-Path -LiteralPath $sourceDir).Path,
    $packageRoot,
    [StringComparison]::OrdinalIgnoreCase
) -and $hasLauncher -and -not $pathsOverlap

foreach ($requiredFile in $requiredPackageFiles) {
    if (-not (Test-Path (Join-Path $packageRoot $requiredFile) -PathType Leaf)) {
        $isReleasePackage = $false
        break
    }
}

foreach ($projectFolder in @('.git', '.github', 'src', 'scripts', 'docs', 'site')) {
    if (Test-Path (Join-Path $packageRoot $projectFolder)) {
        $isReleasePackage = $false
        break
    }
}

# Official release packages are flat. Do not mark folders that contain
# nested directories or reparse points, which could include unrelated data.
if ($isReleasePackage) {
    foreach ($entry in (Get-ChildItem -LiteralPath $packageRoot -Force)) {
        $attributes = [System.IO.File]::GetAttributes($entry.FullName)
        if (($attributes -band [System.IO.FileAttributes]::Directory) -ne 0 -or
            ($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            $isReleasePackage = $false
            break
        }
    }
}

if ($isReleasePackage) {
    # A 1.5.2 package included this exact legacy BAT launcher. When upgrading
    # in-place, remove only the known original file; modified/user-created files
    # remain untouched and will make the manifest check fail safely.
    $legacyLauncher = Join-Path $packageRoot 'Abrir-Configuracoes.bat'
    $legacyLauncherHash = 'fed79cb18f419b68c834dfe06e679e74f500a353562c905c712f1fe37ca702e2'
    if (Test-Path -LiteralPath $legacyLauncher -PathType Leaf) {
        $currentLegacyHash = (Get-FileHash -LiteralPath $legacyLauncher -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($currentLegacyHash -eq $legacyLauncherHash) {
            Remove-Item -LiteralPath $legacyLauncher -Force
        }
        else {
            Write-Warning 'O antigo Abrir-Configuracoes.bat foi modificado e será preservado; a remoção da pasta extraída ficará indisponível.'
        }
    }
}

if ($isReleasePackage -and -not (Test-PhoneBoxPackageManifest -PackageRoot $packageRoot)) {
    $isReleasePackage = $false
    Write-Warning 'A pasta do pacote contém arquivos ausentes, extras ou modificados. A remoção automática dessa pasta ficará indisponível.'
}

$packageSourceFile = Join-Path $settingsDir 'package-source.txt'
if ($isReleasePackage) {
    $markerPath = Join-Path $packageRoot '.phonebox-keyboard-addon-package'
    [System.IO.File]::WriteAllText(
        $markerPath,
        'PhoneBoxKeyboardAddonPackage:v1',
        [System.Text.UTF8Encoding]::new($false)
    )
    try { & attrib.exe +h $markerPath 2>$null } catch { }
    [System.IO.File]::WriteAllText(
        $packageSourceFile,
        $packageRoot,
        [System.Text.UTF8Encoding]::new($false)
    )
}
elseif (Test-Path $packageSourceFile) {
    # Do not retain an old package location after a development/source install.
    Remove-Item -LiteralPath $packageSourceFile -Force
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
Write-Host "Acesso às configurações: $installedLauncher" -ForegroundColor Cyan

if ($LaunchSettings) {
    Start-Process (Join-Path $installDir 'PhoneBoxKeyboardAddon.exe') -ArgumentList '--settings'
}
