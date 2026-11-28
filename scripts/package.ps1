[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$root = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $root 'src\PhoneBoxKeyboardAddon\PhoneBoxKeyboardAddon.csproj'
$launcherProject = Join-Path $root 'src\SettingsLauncher\SettingsLauncher.csproj'
$appPublishDir = Join-Path $root 'artifacts\publish'
$launcherPublishDir = Join-Path $root 'artifacts\publish-launcher'
$packageDir = Join-Path $root 'artifacts\package'
$artifactDir = Join-Path $root 'artifacts'
$zip = Join-Path $artifactDir 'PhoneBoxKeyboardAddon-win-x64.zip'
$checksumFile = Join-Path $artifactDir 'SHA256SUMS.txt'
$legacyChecksumFile = Join-Path $artifactDir 'PhoneBoxKeyboardAddon-win-x64.zip.sha256'
$packageManifestName = 'PhoneBoxPackage.manifest'
$packageManifestFormat = 'PhoneBoxKeyboardAddonPackageManifest:v1'

$appExe = Join-Path $appPublishDir 'PhoneBoxKeyboardAddon.exe'
$launcherExe = Join-Path $launcherPublishDir 'Abrir-Configuracoes.exe'

function Get-ExpectedPublishVersion {
    param([Parameter(Mandatory = $true)][string]$ProjectPath)

    [xml]$project = Get-Content -LiteralPath $ProjectPath -Raw
    $group = $project.Project.PropertyGroup | Select-Object -First 1
    if (-not $group.Version) {
        throw "A propriedade Version não foi encontrada em $ProjectPath."
    }
    if ($group.InformationalVersion) {
        return [string]$group.InformationalVersion
    }
    return [string]$group.Version
}

function Test-PublishedExecutable {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$ExpectedVersion
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) { return $false }
    $actualVersion = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($Path).ProductVersion
    if ([string]::IsNullOrWhiteSpace([string]$actualVersion)) { return $false }

    $expectedPattern = '^{0}(?:\+[0-9A-Za-z.-]+)?$' -f [regex]::Escape($ExpectedVersion)
    return [regex]::IsMatch(
        [string]$actualVersion,
        $expectedPattern,
        [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
    )
}

$appExpectedVersion = Get-ExpectedPublishVersion -ProjectPath $appProject
$launcherExpectedVersion = Get-ExpectedPublishVersion -ProjectPath $launcherProject

if ($appExpectedVersion -ne $launcherExpectedVersion) {
    throw "Os projetos informam versões diferentes: aplicativo '$appExpectedVersion' e launcher '$launcherExpectedVersion'."
}

# Do not silently package old binaries left by a previous build. Re-publish
# whenever either artifact is missing or its embedded ProductVersion is stale.
$appIsCurrent = Test-PublishedExecutable -Path $appExe -ExpectedVersion $appExpectedVersion
$launcherIsCurrent = Test-PublishedExecutable -Path $launcherExe -ExpectedVersion $launcherExpectedVersion
$sourceIsNewer = $false

if ($appIsCurrent -and $launcherIsCurrent) {
    $sourceFiles = @(
        Get-ChildItem -LiteralPath (Join-Path $root 'src') -File -Recurse |
            Where-Object { $_.FullName -notmatch '[\\/](?:bin|obj)[\\/]' }
    )
    if ($sourceFiles.Count -eq 0) {
        throw 'Nenhum arquivo-fonte foi encontrado em src/.'
    }

    $newestSourceUtc = ($sourceFiles | Sort-Object -Property LastWriteTimeUtc -Descending | Select-Object -First 1).LastWriteTimeUtc
    $appArtifactUtc = (Get-Item -LiteralPath $appExe).LastWriteTimeUtc
    $launcherArtifactUtc = (Get-Item -LiteralPath $launcherExe).LastWriteTimeUtc
    $oldestArtifactUtc = $appArtifactUtc
    if ($launcherArtifactUtc -lt $oldestArtifactUtc) {
        $oldestArtifactUtc = $launcherArtifactUtc
    }

    $sourceIsNewer = $newestSourceUtc -gt $oldestArtifactUtc
}

if (-not $appIsCurrent -or -not $launcherIsCurrent -or $sourceIsNewer) {
    Write-Host 'Artefatos ausentes, desatualizados ou anteriores ao código-fonte; executando nova publicação.' -ForegroundColor Yellow
    & (Join-Path $PSScriptRoot 'publish-windows.ps1')
    $appIsCurrent = Test-PublishedExecutable -Path $appExe -ExpectedVersion $appExpectedVersion
    $launcherIsCurrent = Test-PublishedExecutable -Path $launcherExe -ExpectedVersion $launcherExpectedVersion
    if (-not $appIsCurrent -or -not $launcherIsCurrent) {
        throw 'A publicação terminou, mas as versões incorporadas dos executáveis não correspondem aos projetos.'
    }
}

if (Test-Path $packageDir) { Remove-Item $packageDir -Recurse -Force }
New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

# Copy the complete main-app publish output while preserving any runtime subfolders.
$runtimeFiles = Get-ChildItem -LiteralPath $appPublishDir -File -Recurse
if (-not ($runtimeFiles | Where-Object Name -eq 'PhoneBoxKeyboardAddon.exe')) {
    throw 'Executável principal não encontrado na publicação.'
}
foreach ($file in $runtimeFiles) {
    $relativePath = $file.FullName.Substring($appPublishDir.Length).TrimStart([char[]]@('\', '/'))
    if ($relativePath.Contains('\') -or $relativePath.Contains('/')) {
        throw "A publicação contém subpastas ('$relativePath'). O ZIP oficial precisa ser plano para permitir remoção segura da pasta extraída."
    }
    $destination = Join-Path $packageDir $relativePath
    $destinationDirectory = Split-Path -Parent $destination
    New-Item -ItemType Directory -Path $destinationDirectory -Force | Out-Null
    Copy-Item -LiteralPath $file.FullName -Destination $destination -Force
}

# The launcher is a separate self-contained EXE carrying the project's ICO resource.
Copy-Item -LiteralPath $launcherExe -Destination (Join-Path $packageDir 'Abrir-Configuracoes.exe') -Force

$files = @(
    (Join-Path $root 'README.md'),
    (Join-Path $root 'LICENSE'),
    (Join-Path $PSScriptRoot 'install.ps1'),
    (Join-Path $PSScriptRoot 'uninstall.ps1')
)
foreach ($file in $files) {
    if (-not (Test-Path $file -PathType Leaf)) { throw "Arquivo obrigatório não encontrado: $file" }
    Copy-Item -LiteralPath $file -Destination $packageDir -Force
}

# The in-package manifest is used by the installer and uninstaller to verify the
# exact file inventory and hashes before the extracted folder can be deleted.
$manifestFiles = @(
    Get-ChildItem -LiteralPath $packageDir -File |
        Sort-Object -Property Name |
        ForEach-Object {
            [pscustomobject]@{
                name = $_.Name
                sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
            }
        }
)
$manifest = [ordered]@{
    format = $packageManifestFormat
    files = $manifestFiles
}
$manifestPath = Join-Path $packageDir $packageManifestName
[System.IO.File]::WriteAllText(
    $manifestPath,
    ($manifest | ConvertTo-Json -Depth 5),
    [System.Text.UTF8Encoding]::new($false)
)

New-Item -ItemType Directory -Path $artifactDir -Force | Out-Null
if (Test-Path $zip) { Remove-Item $zip -Force }
if (Test-Path $checksumFile) { Remove-Item $checksumFile -Force }
if (Test-Path $legacyChecksumFile) { Remove-Item $legacyChecksumFile -Force }
Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $zip -CompressionLevel Optimal

# Validate the finished ZIP itself: every entry must be at the package root,
# the internal manifest must match all files, and the manifest must not omit
# or silently allow additional entries.
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($zip)
try {
    $entryMap = @{}
    foreach ($entry in $archive.Entries) {
        if ($entry.FullName.Contains('/') -or $entry.FullName.Contains('\') -or
            [string]::IsNullOrWhiteSpace($entry.Name)) {
            throw "O ZIP contém uma pasta ou caminho não permitido: $($entry.FullName)"
        }
        if ($entryMap.ContainsKey($entry.FullName)) {
            throw "O ZIP contém uma entrada duplicada: $($entry.FullName)"
        }
        $entryMap[$entry.FullName] = $entry
    }

    $manifestEntry = $entryMap[$packageManifestName]
    if (-not $manifestEntry) { throw 'O ZIP final não contém PhoneBoxPackage.manifest.' }
    $reader = [System.IO.StreamReader]::new($manifestEntry.Open())
    try { $manifestInZip = $reader.ReadToEnd() | ConvertFrom-Json -ErrorAction Stop }
    finally { $reader.Dispose() }

    if ($manifestInZip.format -ne $packageManifestFormat -or -not $manifestInZip.files) {
        throw 'O manifesto interno do ZIP possui formato inválido.'
    }

    $expectedNames = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    $requiredNames = @(
        'PhoneBoxKeyboardAddon.exe',
        'Abrir-Configuracoes.exe',
        'install.ps1',
        'uninstall.ps1',
        'README.md',
        'LICENSE'
    )

    foreach ($requiredName in $requiredNames) {
        if (-not $entryMap.ContainsKey($requiredName)) {
            throw "O ZIP final não contém o arquivo obrigatório $requiredName."
        }
    }

    foreach ($item in $manifestInZip.files) {
        $name = [string]$item.name
        $expectedHash = [string]$item.sha256
        if (-not $name -or $name.Contains('/') -or $name.Contains('\') -or $name.Contains(':')) {
            throw "Nome de arquivo inválido no manifesto interno: '$name'."
        }
        if ($name -ieq $packageManifestName -or -not $expectedNames.Add($name)) {
            throw "Entrada inválida ou duplicada no manifesto interno: '$name'."
        }
        if ($expectedHash -notmatch '^[A-Fa-f0-9]{64}$') {
            throw "SHA-256 inválido no manifesto interno para '$name'."
        }
        if (-not $entryMap.ContainsKey($name)) {
            throw "O manifesto interno lista um arquivo ausente no ZIP: '$name'."
        }

        $entryStream = $entryMap[$name].Open()
        $sha = [System.Security.Cryptography.SHA256]::Create()
        try {
            $actualHash = [BitConverter]::ToString($sha.ComputeHash($entryStream)).Replace('-', '').ToLowerInvariant()
        }
        finally {
            $sha.Dispose()
            $entryStream.Dispose()
        }
        if (-not [string]::Equals($actualHash, $expectedHash, [StringComparison]::OrdinalIgnoreCase)) {
            throw "O hash interno do ZIP não confere para '$name'."
        }
    }

    [void]$expectedNames.Add($packageManifestName)
    if ($entryMap.Count -ne $expectedNames.Count) {
        throw 'O ZIP final contém arquivos extras ou não listados no manifesto interno.'
    }
    foreach ($entryName in $entryMap.Keys) {
        if (-not $expectedNames.Contains([string]$entryName)) {
            throw "O ZIP final contém uma entrada não prevista: '$entryName'."
        }
    }
}
finally {
    $archive.Dispose()
}

$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
Set-Content -LiteralPath $checksumFile -Value "$hash  PhoneBoxKeyboardAddon-win-x64.zip" -Encoding ascii

Write-Host "Pacote: $zip" -ForegroundColor Green
Write-Host "Manifesto SHA-256: $checksumFile" -ForegroundColor Green
Write-Host "SHA-256: $hash"
Write-Host 'Estrutura do ZIP e hashes internos validados.' -ForegroundColor Green
