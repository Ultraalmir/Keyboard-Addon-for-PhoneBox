param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [Parameter(Mandatory=$true)][string]$InstallDirectory,
    [Parameter(Mandatory=$true)][int]$ParentPid,
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$ExpectedSha256
)
$ErrorActionPreference = 'Stop'
$appName = 'PhoneBoxKeyboardAddon.exe'
$launcherName = 'Abrir-Configuracoes.exe'
$appTarget = Join-Path $InstallDirectory $appName
$launcherTarget = Join-Path $InstallDirectory $launcherName
$appStage = Join-Path $InstallDirectory ($appName + '.new')
$launcherStage = Join-Path $InstallDirectory ($launcherName + '.new')
$appBackup = Join-Path $InstallDirectory ($appName + '.backup')
$launcherBackup = Join-Path $InstallDirectory ($launcherName + '.backup')
$log = Join-Path $env:TEMP 'PhoneBoxKeyboardAddon-update.log'
$resultDir = Join-Path $env:APPDATA 'PhoneBoxKeyboardAddon'
$resultFile = Join-Path $resultDir 'update-result.txt'
$appReplaced = $false
$launcherReplaced = $false
$launcherWasPresent = $false

function Write-UpdateLog([string]$Message) {
    Add-Content -LiteralPath $log -Value ("[{0:yyyy-MM-dd HH:mm:ss}] {1}" -f (Get-Date), $Message)
}
function Write-UpdateResult([string]$Status, [string]$ResultVersion) {
    New-Item -ItemType Directory -Path $resultDir -Force | Out-Null
    $temporaryResult = $resultFile + '.tmp'
    $content = $Status + [Environment]::NewLine + $ResultVersion
    [System.IO.File]::WriteAllText($temporaryResult, $content, [System.Text.UTF8Encoding]::new($false))
    Move-Item -LiteralPath $temporaryResult -Destination $resultFile -Force
}

try {
    Write-UpdateLog "Aguardando encerramento do processo $ParentPid."
    try { Wait-Process -Id $ParentPid -Timeout 45 -ErrorAction Stop } catch {
        if (Get-Process -Id $ParentPid -ErrorAction SilentlyContinue) { throw 'O complemento não encerrou no prazo esperado.' }
    }
    if (-not (Test-Path -LiteralPath $appTarget -PathType Leaf)) { throw "Executável instalado não encontrado: $appTarget" }
    $actualHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $ExpectedSha256.ToLowerInvariant()) { throw 'O pacote mudou após a verificação SHA-256; atualização cancelada.' }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entries = @($archive.Entries)
        $appEntry = $entries | Where-Object { $_.FullName -ceq $appName } | Select-Object -First 1
        $launcherEntry = $entries | Where-Object { $_.FullName -ceq $launcherName } | Select-Object -First 1
        $manifestEntry = $entries | Where-Object { $_.FullName -ceq 'PhoneBoxPackage.manifest' } | Select-Object -First 1
        if (-not $appEntry -or $appEntry.Length -lt 100000) { throw 'O pacote não contém um executável principal válido.' }
        if (-not $launcherEntry -or $launcherEntry.Length -lt 100000) { throw 'O pacote não contém o launcher de configurações válido.' }
        if (-not $manifestEntry -or $manifestEntry.Length -lt 100) { throw 'O pacote não contém o manifesto de inventário esperado.' }

        $inputStream = $appEntry.Open()
        try {
            $outputStream = [System.IO.File]::Open($appStage, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        } finally { $inputStream.Dispose() }

        $inputStream = $launcherEntry.Open()
        try {
            $outputStream = [System.IO.File]::Open($launcherStage, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        } finally { $inputStream.Dispose() }
    } finally { $archive.Dispose() }

    if (Test-Path -LiteralPath $appBackup) { Remove-Item -LiteralPath $appBackup -Force }
    if (Test-Path -LiteralPath $launcherBackup) { Remove-Item -LiteralPath $launcherBackup -Force }
    $launcherWasPresent = Test-Path -LiteralPath $launcherTarget -PathType Leaf

    [System.IO.File]::Replace($appStage, $appTarget, $appBackup, $true)
    $appReplaced = $true
    if ($launcherWasPresent) {
        [System.IO.File]::Replace($launcherStage, $launcherTarget, $launcherBackup, $true)
    }
    else {
        [System.IO.File]::Move($launcherStage, $launcherTarget)
    }
    $launcherReplaced = $true

    Write-UpdateLog "Executável principal e launcher atualizados para $Version."
    $newProcess = Start-Process -FilePath $appTarget -ArgumentList '--background' -WorkingDirectory $InstallDirectory -PassThru
    Start-Sleep -Seconds 3
    $newProcess.Refresh()
    if ($newProcess.HasExited) { throw 'A nova versão encerrou inesperadamente durante a inicialização.' }

    Write-UpdateLog 'Nova versão iniciada com sucesso.'
    if (Test-Path -LiteralPath $appBackup) { Remove-Item -LiteralPath $appBackup -Force }
    if (Test-Path -LiteralPath $launcherBackup) { Remove-Item -LiteralPath $launcherBackup -Force }
    Remove-Item -LiteralPath (Split-Path -Parent $PackagePath) -Recurse -Force -ErrorAction SilentlyContinue
    try { Write-UpdateResult 'success' $Version } catch { Write-UpdateLog ("Falha ao registrar a conclusão da atualização: " + $_.Exception.Message) }
}
catch {
    Write-UpdateLog ("Falha: " + $_.Exception.Message)

    if ($launcherReplaced) {
        try {
            if ($launcherWasPresent -and (Test-Path -LiteralPath $launcherBackup)) {
                if (Test-Path -LiteralPath $launcherTarget) { Remove-Item -LiteralPath $launcherTarget -Force }
                [System.IO.File]::Move($launcherBackup, $launcherTarget)
                Write-UpdateLog 'Launcher anterior restaurado.'
            }
            elseif (-not $launcherWasPresent -and (Test-Path -LiteralPath $launcherTarget)) {
                Remove-Item -LiteralPath $launcherTarget -Force
            }
        } catch { Write-UpdateLog ("Falha ao restaurar o launcher: " + $_.Exception.Message) }
    }

    $restored = $false
    if ($appReplaced -and (Test-Path -LiteralPath $appBackup)) {
        try {
            if (Test-Path -LiteralPath $appTarget) { Remove-Item -LiteralPath $appTarget -Force }
            [System.IO.File]::Move($appBackup, $appTarget)
            $restored = $true
            Write-UpdateLog 'Versão anterior restaurada.'
        } catch { Write-UpdateLog ("Falha ao restaurar backup do aplicativo: " + $_.Exception.Message) }
    }

    if (Test-Path -LiteralPath $appTarget) {
        try {
            Start-Process -FilePath $appTarget -ArgumentList '--background' -WorkingDirectory $InstallDirectory | Out-Null
            if (-not $restored) { Write-UpdateLog 'Versão instalada anteriormente reiniciada após falha.' }
        } catch { Write-UpdateLog ("Falha ao reiniciar o complemento: " + $_.Exception.Message) }
    }
    try { Write-UpdateResult 'failure' $Version } catch { Write-UpdateLog ("Falha ao registrar o resultado da atualização: " + $_.Exception.Message) }
    foreach ($stageFile in @($appStage, $launcherStage)) {
        if (Test-Path -LiteralPath $stageFile) { Remove-Item -LiteralPath $stageFile -Force -ErrorAction SilentlyContinue }
    }
}
