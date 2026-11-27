[CmdletBinding()]
param(
    [string]$ExpectedVersion,
    [switch]$RequireDatedChangelog
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Get-ProjectMetadata {
    param([Parameter(Mandatory = $true)][string]$Path)

    [xml]$project = Get-Content -LiteralPath $Path -Raw
    $group = $project.Project.PropertyGroup | Select-Object -First 1

    if (-not $group.Version) {
        throw "A propriedade Version não foi encontrada em $Path."
    }

    $version = [string]$group.Version
    $informationalVersion = if ($group.InformationalVersion) {
        [string]$group.InformationalVersion
    }
    else {
        $version
    }

    [pscustomobject]@{
        Version = $version
        InformationalVersion = $informationalVersion
    }
}

$mainProjectPath = Join-Path $repoRoot 'src\PhoneBoxKeyboardAddon\PhoneBoxKeyboardAddon.csproj'
$launcherProjectPath = Join-Path $repoRoot 'src\SettingsLauncher\SettingsLauncher.csproj'
$addonPathsPath = Join-Path $repoRoot 'src\PhoneBoxKeyboardAddon\AddonPaths.cs'

$main = Get-ProjectMetadata -Path $mainProjectPath
$launcher = Get-ProjectMetadata -Path $launcherProjectPath

if ($main.Version -ne $main.InformationalVersion) {
    throw "Version ($($main.Version)) e InformationalVersion ($($main.InformationalVersion)) do aplicativo principal divergem."
}
if ($launcher.Version -ne $launcher.InformationalVersion) {
    throw "Version ($($launcher.Version)) e InformationalVersion ($($launcher.InformationalVersion)) do launcher divergem."
}
if ($main.Version -ne $launcher.Version) {
    throw "A versão do aplicativo ($($main.Version)) difere da versão do launcher ($($launcher.Version))."
}

$addonPaths = Get-Content -LiteralPath $addonPathsPath -Raw
$versionMatch = [regex]::Match(
    $addonPaths,
    '(?m)^\s*internal\s+const\s+string\s+Version\s*=\s*"(?<version>[^"]+)"\s*;'
)
if (-not $versionMatch.Success) {
    throw 'Não foi possível localizar ProductInfo.Version em AddonPaths.cs.'
}

$productVersion = $versionMatch.Groups['version'].Value
if ($productVersion -ne $main.Version) {
    throw "ProductInfo.Version ($productVersion) difere da versão dos projetos ($($main.Version))."
}

if ($main.Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "A versão de publicação deve seguir o formato MAJOR.MINOR.PATCH; encontrada: $($main.Version)."
}

foreach ($readmeName in @('README.md', 'README.en.md')) {
    $readmePath = Join-Path $repoRoot $readmeName
    $readme = Get-Content -LiteralPath $readmePath -Raw
    $badge = [regex]::Match($readme, 'version-(?<version>\d+\.\d+\.\d+)-')
    if (-not $badge.Success) {
        throw "Badge de versão não encontrado em $readmeName."
    }
    if ($badge.Groups['version'].Value -ne $main.Version) {
        throw "O badge de $readmeName informa $($badge.Groups['version'].Value), mas o projeto está em $($main.Version)."
    }
}

$changelogPath = Join-Path $repoRoot 'CHANGELOG.md'
$changelog = Get-Content -LiteralPath $changelogPath -Raw
$releaseHeading = [regex]::Match(
    $changelog,
    '(?m)^##\s+\[(?<version>\d+\.\d+\.\d+)\]\s+-\s+(?<status>[^\r\n]+)'
)
if (-not $releaseHeading.Success) {
    throw 'Não foi encontrada uma seção de versão no topo do CHANGELOG.md.'
}
if ($releaseHeading.Groups['version'].Value -ne $main.Version) {
    throw "A primeira versão do CHANGELOG.md ($($releaseHeading.Groups['version'].Value)) difere da versão dos projetos ($($main.Version))."
}

if ($ExpectedVersion) {
    $tagVersion = $ExpectedVersion.Trim()
    if ($tagVersion.StartsWith('v', [StringComparison]::OrdinalIgnoreCase)) {
        $tagVersion = $tagVersion.Substring(1)
    }
    if ($tagVersion -ne $main.Version) {
        throw "A tag ($tagVersion) não corresponde à versão dos projetos ($($main.Version))."
    }
}

if ($RequireDatedChangelog) {
    $status = $releaseHeading.Groups['status'].Value.Trim()
    if ($status -match 'prepara|prepar|unreleased|em desenvolvimento') {
        throw "O CHANGELOG.md ainda marca a versão $($main.Version) como '$status'. Atualize o cabeçalho com a data real da Release antes de enviar a tag."
    }
    if ($status -notmatch '^\d{4}-\d{2}-\d{2}$') {
        throw "O cabeçalho da versão no CHANGELOG.md deve conter uma data YYYY-MM-DD; encontrado: '$status'."
    }
    $parsedDate = [datetime]::MinValue
    if (-not [datetime]::TryParseExact(
        $status,
        'yyyy-MM-dd',
        [Globalization.CultureInfo]::InvariantCulture,
        [Globalization.DateTimeStyles]::None,
        [ref]$parsedDate
    )) {
        throw "A data '$status' no CHANGELOG.md não é válida."
    }
}

Write-Host ("Versões consistentes: aplicativo, launcher, ProductInfo e READMEs = {0}." -f $main.Version) -ForegroundColor Green
if ($ExpectedVersion) {
    Write-Host ("Tag validada: {0}." -f $ExpectedVersion) -ForegroundColor Green
}
if ($RequireDatedChangelog) {
    Write-Host ("Data do CHANGELOG validada: {0}." -f $status) -ForegroundColor Green
}
