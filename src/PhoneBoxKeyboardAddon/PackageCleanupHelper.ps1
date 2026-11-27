param(
    [Parameter(Mandatory = $true)][string]$PackageRoot
)
$ErrorActionPreference = 'Stop'

function Test-PhoneBoxPackageDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    try {
        $fullPath = [System.IO.Path]::GetFullPath($Path).TrimEnd('\')
        $root = [System.IO.Path]::GetPathRoot($fullPath).TrimEnd('\')
        if (-not $fullPath -or $fullPath -eq $root) { return $false }

        $protectedFolders = @(
            [Environment]::GetFolderPath('UserProfile'),
            [Environment]::GetFolderPath('DesktopDirectory'),
            [Environment]::GetFolderPath('MyDocuments'),
            (Join-Path $env:USERPROFILE 'Downloads')
        ) | Where-Object { $_ }
        foreach ($folder in $protectedFolders) {
            $protectedPath = [System.IO.Path]::GetFullPath($folder).TrimEnd('\')
            if ([string]::Equals($fullPath, $protectedPath, [StringComparison]::OrdinalIgnoreCase)) { return $false }
        }

        $protectedRoots = @(
            [Environment]::GetFolderPath('Windows'),
            [Environment]::GetFolderPath('ProgramFiles'),
            [Environment]::GetFolderPath('ProgramFilesX86'),
            [Environment]::GetFolderPath('CommonApplicationData'),
            [Environment]::GetFolderPath('CommonProgramFiles'),
            [Environment]::GetFolderPath('CommonProgramFilesX86')
        ) | Where-Object { $_ }
        foreach ($folder in $protectedRoots) {
            $protectedPath = [System.IO.Path]::GetFullPath($folder).TrimEnd('\')
            if ([string]::Equals($fullPath, $protectedPath, [StringComparison]::OrdinalIgnoreCase) -or
                $fullPath.StartsWith($protectedPath + '\', [StringComparison]::OrdinalIgnoreCase)) { return $false }
        }

        if (-not (Test-Path -LiteralPath $fullPath -PathType Container)) { return $false }
        if (([System.IO.File]::GetAttributes($fullPath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }

        $installPath = [System.IO.Path]::GetFullPath((Join-Path $env:LOCALAPPDATA 'Programs\PhoneBoxKeyboardAddon')).TrimEnd('\')
        if ([string]::Equals($fullPath, $installPath, [StringComparison]::OrdinalIgnoreCase) -or
            $fullPath.StartsWith($installPath + '\', [StringComparison]::OrdinalIgnoreCase) -or
            $installPath.StartsWith($fullPath + '\', [StringComparison]::OrdinalIgnoreCase)) { return $false }

        foreach ($projectFolder in @('.git', '.github', 'src', 'scripts', 'docs', 'site')) {
            if (Test-Path -LiteralPath (Join-Path $fullPath $projectFolder)) { return $false }
        }

        foreach ($entry in (Get-ChildItem -LiteralPath $fullPath -Force -ErrorAction Stop)) {
            $attributes = [System.IO.File]::GetAttributes($entry.FullName)
            if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0 -or
                ($attributes -band [System.IO.FileAttributes]::Directory) -ne 0) { return $false }
        }

        $markerPath = Join-Path $fullPath '.phonebox-keyboard-addon-package'
        if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) { return $false }
        if (([System.IO.File]::GetAttributes($markerPath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
        if ((Get-Content -LiteralPath $markerPath -Raw).Trim() -ne 'PhoneBoxKeyboardAddonPackage:v1') { return $false }

        $manifestPath = Join-Path $fullPath 'PhoneBoxPackage.manifest'
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
            if ($name -ieq 'PhoneBoxPackage.manifest' -or $name -ieq '.phonebox-keyboard-addon-package') { return $false }
            if ($hash -notmatch '^[A-Fa-f0-9]{64}$' -or -not $expectedNames.Add($name)) { return $false }

            $filePath = Join-Path $fullPath $name
            if (-not (Test-Path -LiteralPath $filePath -PathType Leaf)) { return $false }
            if (([System.IO.File]::GetAttributes($filePath) -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) { return $false }
            $actualHash = (Get-FileHash -LiteralPath $filePath -Algorithm SHA256).Hash
            if (-not [string]::Equals($actualHash, $hash, [StringComparison]::OrdinalIgnoreCase)) { return $false }
            if ($hasRequired.ContainsKey($name)) { $hasRequired[$name] = $true }
        }
        if (@($hasRequired.Values | Where-Object { -not $_ }).Count -gt 0) { return $false }

        $actualNames = @(Get-ChildItem -LiteralPath $fullPath -Force | ForEach-Object Name)

        # Avoid generic-constructor overload binding with object[] in Windows
        # PowerShell. Build the expected and actual sets explicitly.
        $expectedWithMetadata = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($expectedName in $expectedNames) {
            [void]$expectedWithMetadata.Add([string]$expectedName)
        }
        [void]$expectedWithMetadata.Add('PhoneBoxPackage.manifest')
        [void]$expectedWithMetadata.Add('.phonebox-keyboard-addon-package')

        if ($actualNames.Count -ne $expectedWithMetadata.Count) { return $false }
        $actualSet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
        foreach ($actualName in $actualNames) {
            if (-not $actualSet.Add([string]$actualName)) { return $false }
        }
        return $actualSet.SetEquals($expectedWithMetadata)
    }
    catch {
        Write-Warning ("Falha na validação final da pasta extraída: {0}" -f $_.Exception.Message)
        return $false
    }
}

$resolvedPackageRoot = [System.IO.Path]::GetFullPath($PackageRoot)
if (-not (Test-PhoneBoxPackageDirectory -Path $resolvedPackageRoot)) {
    throw 'A pasta do pacote mudou ou não passou na validação final. Nenhum arquivo dessa pasta foi removido.'
}

Set-Location -LiteralPath $env:TEMP
Remove-Item -LiteralPath $resolvedPackageRoot -Recurse -Force
if (Test-Path -LiteralPath $resolvedPackageRoot) {
    throw 'A pasta extraída ainda existe após a tentativa de remoção.'
}
Write-Output 'Pasta extraída do pacote removida após a validação final do manifesto e dos hashes.'
