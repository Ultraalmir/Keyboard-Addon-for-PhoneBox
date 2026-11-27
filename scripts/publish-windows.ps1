[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [switch]$SkipSelfTest
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot 'src\PhoneBoxKeyboardAddon\PhoneBoxKeyboardAddon.csproj'
$launcherProject = Join-Path $repoRoot 'src\SettingsLauncher\SettingsLauncher.csproj'
$appPublishDir = Join-Path $repoRoot 'artifacts\publish'
$launcherPublishDir = Join-Path $repoRoot 'artifacts\publish-launcher'

Write-Host 'PhoneBox Keyboard Add-on - publicação Windows' -ForegroundColor Cyan
Write-Host "Projeto principal: $appProject"
Write-Host "Launcher: $launcherProject"

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 10 SDK não encontrado. Instale em https://dotnet.microsoft.com/download/dotnet/10.0'
}

Push-Location $repoRoot
try {
    $version = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Não foi possível consultar a versão do SDK .NET.' }
    if (-not $version.StartsWith('10.')) {
        throw "Este projeto exige o SDK .NET 10; SDK detectado: $version."
    }

    dotnet restore $appProject
    if ($LASTEXITCODE -ne 0) { throw 'Falha no restore do projeto principal.' }

    dotnet build $appProject --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação do projeto principal.' }

    if (-not $SkipSelfTest) {
        dotnet run --project $appProject --configuration Release --no-build -- --self-test
        if ($LASTEXITCODE -ne 0) { throw 'Os testes internos falharam.' }
    }

    foreach ($directory in @($appPublishDir, $launcherPublishDir)) {
        if (Test-Path $directory) { Remove-Item $directory -Recurse -Force }
        New-Item -ItemType Directory -Path $directory -Force | Out-Null
    }

    dotnet publish $appProject --configuration Release --runtime $Runtime --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $appPublishDir
    if ($LASTEXITCODE -ne 0) { throw 'Falha na publicação do aplicativo.' }

    dotnet publish $launcherProject --configuration Release --runtime $Runtime --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $launcherPublishDir
    if ($LASTEXITCODE -ne 0) { throw 'Falha na publicação do launcher de configurações.' }

    $appExe = Join-Path $appPublishDir 'PhoneBoxKeyboardAddon.exe'
    $launcherExe = Join-Path $launcherPublishDir 'Abrir-Configuracoes.exe'
    if (-not (Test-Path $appExe -PathType Leaf)) { throw 'O executável principal publicado não foi encontrado.' }
    if (-not (Test-Path $launcherExe -PathType Leaf)) { throw 'O launcher Abrir-Configuracoes.exe não foi encontrado.' }

    Write-Host "Aplicativo: $appExe" -ForegroundColor Green
    Write-Host "Launcher:   $launcherExe" -ForegroundColor Green
    Write-Host 'Publicação concluída.' -ForegroundColor Green
}
finally {
    Pop-Location
}
