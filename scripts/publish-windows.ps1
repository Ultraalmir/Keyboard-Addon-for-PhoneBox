[CmdletBinding()]
param(
    [ValidateSet('win-x64')]
    [string]$Runtime = 'win-x64',
    [switch]$SkipSelfTest
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\PhoneBoxKeyboardAddon\PhoneBoxKeyboardAddon.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\publish'

Write-Host 'PhoneBox Keyboard Add-on - publicação Windows' -ForegroundColor Cyan
Write-Host "Projeto: $project"

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

    if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
    dotnet restore $project
    if ($LASTEXITCODE -ne 0) { throw 'Falha no restore do projeto.' }

    dotnet build $project --configuration Release --no-restore
    if ($LASTEXITCODE -ne 0) { throw 'Falha na compilação.' }

    if (-not $SkipSelfTest) {
        dotnet run --project $project --configuration Release --no-build -- --self-test
        if ($LASTEXITCODE -ne 0) { throw 'Os testes internos falharam.' }
    }

    dotnet publish $project --configuration Release --runtime $Runtime --self-contained true `
        -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true `
        --output $publishDir
    if ($LASTEXITCODE -ne 0) { throw 'Falha na publicação do executável.' }

    $exe = Join-Path $publishDir 'PhoneBoxKeyboardAddon.exe'
    if (-not (Test-Path $exe)) { throw 'O executável publicado não foi encontrado.' }
    Write-Host "Publicação concluída: $exe" -ForegroundColor Green
}
finally {
    Pop-Location
}
