param(
    [Parameter(Mandatory=$true)][string]$PackagePath,
    [Parameter(Mandatory=$true)][string]$InstallDirectory,
    [Parameter(Mandatory=$true)][int]$ParentPid,
    [Parameter(Mandatory=$true)][string]$Version,
    [Parameter(Mandatory=$true)][string]$ExpectedSha256
)
$ErrorActionPreference = 'Stop'
$exeName = 'PhoneBoxKeyboardAddon.exe'
$target = Join-Path $InstallDirectory $exeName
$stage = Join-Path $InstallDirectory ($exeName + '.new')
$backup = Join-Path $InstallDirectory ($exeName + '.backup')
$log = Join-Path $env:TEMP 'PhoneBoxKeyboardAddon-update.log'
$resultDir = Join-Path $env:APPDATA 'PhoneBoxKeyboardAddon'
$resultFile = Join-Path $resultDir 'update-result.txt'
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
    if (-not (Test-Path -LiteralPath $target)) { throw "Executável instalado não encontrado: $target" }
    $actualHash = (Get-FileHash -LiteralPath $PackagePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne $ExpectedSha256.ToLowerInvariant()) { throw 'O pacote mudou após a verificação SHA-256; atualização cancelada.' }

    Add-Type -AssemblyName System.IO.Compression
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $entry = $archive.Entries | Where-Object { $_.FullName -ceq $exeName } | Select-Object -First 1
        if (-not $entry -or $entry.Length -lt 100000) { throw 'O pacote não contém um executável válido.' }
        $inputStream = $entry.Open()
        try {
            $outputStream = [System.IO.File]::Open($stage, [System.IO.FileMode]::Create, [System.IO.FileAccess]::Write, [System.IO.FileShare]::None)
            try { $inputStream.CopyTo($outputStream) } finally { $outputStream.Dispose() }
        } finally { $inputStream.Dispose() }
    } finally { $archive.Dispose() }

    if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Force }
    [System.IO.File]::Replace($stage, $target, $backup, $true)
    Write-UpdateLog "Executável atualizado para $Version."
    $newProcess = Start-Process -FilePath $target -ArgumentList '--background' -WorkingDirectory $InstallDirectory -PassThru
    Start-Sleep -Seconds 3
    $newProcess.Refresh()
    if ($newProcess.HasExited) { throw 'A nova versão encerrou inesperadamente durante a inicialização.' }
    Write-UpdateLog 'Nova versão iniciada com sucesso.'
    if (Test-Path -LiteralPath $backup) { Remove-Item -LiteralPath $backup -Force }
    Remove-Item -LiteralPath (Split-Path -Parent $PackagePath) -Recurse -Force -ErrorAction SilentlyContinue
    try { Write-UpdateResult 'success' $Version } catch { Write-UpdateLog ("Falha ao registrar a conclusão da atualização: " + $_.Exception.Message) }
}
catch {
    Write-UpdateLog ("Falha: " + $_.Exception.Message)
    $restored = $false
    if (Test-Path -LiteralPath $backup) {
        try {
            if (Test-Path -LiteralPath $target) { Remove-Item -LiteralPath $target -Force }
            [System.IO.File]::Move($backup, $target)
            $restored = $true
            Write-UpdateLog 'Versão anterior restaurada.'
        } catch { Write-UpdateLog ("Falha ao restaurar backup: " + $_.Exception.Message) }
    }
    if (Test-Path -LiteralPath $target) {
        try {
            Start-Process -FilePath $target -ArgumentList '--background' -WorkingDirectory $InstallDirectory | Out-Null
            if (-not $restored) { Write-UpdateLog 'Versão instalada anteriormente reiniciada após falha.' }
        } catch { Write-UpdateLog ("Falha ao reiniciar o complemento: " + $_.Exception.Message) }
    }
    try { Write-UpdateResult 'failure' $Version } catch { Write-UpdateLog ("Falha ao registrar o resultado da atualização: " + $_.Exception.Message) }
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Force -ErrorAction SilentlyContinue }
}
