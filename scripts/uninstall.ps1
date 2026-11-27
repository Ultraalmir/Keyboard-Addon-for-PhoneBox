[CmdletBinding()]
param(
    [switch]$RemoveSettings,
    [switch]$RemovePackageFiles
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

function Get-SafePackageSourceDirectory {
    $sourceFile = Join-Path $env:APPDATA 'PhoneBoxKeyboardAddon\package-source.txt'
    if (-not (Test-Path -LiteralPath $sourceFile -PathType Leaf)) { return $null }

    try {
        $candidate = [System.IO.Path]::GetFullPath((Get-Content -LiteralPath $sourceFile -Raw).Trim())
        $root = [System.IO.Path]::GetPathRoot($candidate).TrimEnd('\')
        if (-not $candidate -or $candidate.TrimEnd('\') -eq $root) { return $null }

        $protected = @(
            [Environment]::GetFolderPath('UserProfile'),
            [Environment]::GetFolderPath('DesktopDirectory'),
            [Environment]::GetFolderPath('MyDocuments'),
            (Join-Path $env:USERPROFILE 'Downloads')
        ) | Where-Object { $_ }
        foreach ($folder in $protected) {
            if ([string]::Equals($candidate.TrimEnd('\'), [System.IO.Path]::GetFullPath($folder).TrimEnd('\'), [StringComparison]::OrdinalIgnoreCase)) {
                return $null
            }
        }

        if (-not (Test-Path -LiteralPath $candidate -PathType Container)) { return $null }
        if (([System.IO.File]::GetAttributes($candidate) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $null }

        $installDirectory = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\PhoneBoxKeyboardAddon')).TrimEnd('\')
        if ([string]::Equals($candidate.TrimEnd('\'), $installDirectory, [StringComparison]::OrdinalIgnoreCase) -or
            $candidate.StartsWith($installDirectory + '\', [StringComparison]::OrdinalIgnoreCase) -or
            $installDirectory.StartsWith($candidate.TrimEnd('\') + '\', [StringComparison]::OrdinalIgnoreCase)) { return $null }

        $protectedRoots = @(
            [Environment]::GetFolderPath('Windows'),
            [Environment]::GetFolderPath('ProgramFiles'),
            [Environment]::GetFolderPath('ProgramFilesX86'),
            [Environment]::GetFolderPath('CommonApplicationData'),
            [Environment]::GetFolderPath('CommonProgramFiles'),
            [Environment]::GetFolderPath('CommonProgramFilesX86')
        ) | Where-Object { $_ }
        foreach ($protectedRoot in $protectedRoots) {
            $normalizedRoot = [System.IO.Path]::GetFullPath($protectedRoot).TrimEnd('\')
            if ([string]::Equals($candidate.TrimEnd('\'), $normalizedRoot, [StringComparison]::OrdinalIgnoreCase) -or
                $candidate.StartsWith($normalizedRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { return $null }
        }

        foreach ($projectFolder in @('.git', '.github', 'src', 'scripts', 'docs', 'site')) {
            if (Test-Path -LiteralPath (Join-Path $candidate $projectFolder)) { return $null }
        }
        # The official package is flat. Refuse nested directories or links rather than
        # recursively deleting a folder that may contain unrelated user data.
        foreach ($entry in (Get-ChildItem -LiteralPath $candidate -Force -ErrorAction Stop)) {
            $entryAttributes = [System.IO.File]::GetAttributes($entry.FullName)
            if (($entryAttributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                ($entryAttributes -band [System.IO.FileAttributes]::Directory) -ne 0) { return $null }
        }
        $marker = Join-Path $candidate '.phonebox-keyboard-addon-package'
        if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) { return $null }
        if (([System.IO.File]::GetAttributes($marker) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $null }
        if ((Get-Content -LiteralPath $marker -Raw).Trim() -ne 'PhoneBoxKeyboardAddonPackage:v1') { return $null }

        foreach ($required in @('PhoneBoxKeyboardAddon.exe', 'Abrir-Configuracoes.exe', 'install.ps1', 'uninstall.ps1', 'README.md', 'LICENSE', 'PhoneBoxPackage.manifest')) {
            if (-not (Test-Path -LiteralPath (Join-Path $candidate $required) -PathType Leaf)) { return $null }
        }
        if (-not (Test-Path -LiteralPath (Join-Path $candidate 'Abrir-Configuracoes.exe') -PathType Leaf)) { return $null }
        if (-not (Test-PhoneBoxPackageManifest -PackageRoot $candidate)) { return $null }

        return $candidate
    }
    catch { return $null }
}

$packageSourceDirectory = $null
if ($RemovePackageFiles) {
    $packageSourceDirectory = Get-SafePackageSourceDirectory
    if (-not $packageSourceDirectory) {
        throw 'A pasta extraída não foi identificada como um pacote oficial seguro para remoção.'
    }
}

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

if ($RemovePackageFiles -and $packageSourceDirectory) {
    Set-Location -LiteralPath $env:TEMP
    try {
        Remove-Item -LiteralPath $packageSourceDirectory -Recurse -Force
        if (Test-Path -LiteralPath $packageSourceDirectory) {
            throw 'A pasta extraída ainda existe após a tentativa de remoção.'
        }
        Write-Host 'Pasta extraída do pacote removida.' -ForegroundColor Yellow
    }
    catch {
        Write-Warning ("Não foi possível remover a pasta extraída: " + $_.Exception.Message)
    }
}
