[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallDirectory
)

$ErrorActionPreference = 'Stop'

try {
    # The helper is launched from %TEMP% after PhoneBoxKeyboardAddon.exe exits.
    # Accept only the exact per-user installation path; never a caller-selected folder.
    $expectedDirectory = Join-Path $env:LOCALAPPDATA 'Programs\PhoneBoxKeyboardAddon'
    $expectedFullPath = [System.IO.Path]::GetFullPath($expectedDirectory).TrimEnd('\')
    $targetFullPath = [System.IO.Path]::GetFullPath($InstallDirectory).TrimEnd('\')

    if (-not [string]::Equals(
        $targetFullPath,
        $expectedFullPath,
        [System.StringComparison]::OrdinalIgnoreCase
    )) {
        throw "Caminho de instalação recusado. Esperado '$expectedFullPath'; recebido '$targetFullPath'."
    }

    if ([System.IO.File]::Exists($targetFullPath)) {
        throw "O caminho de instalação é um arquivo, não uma pasta: '$targetFullPath'."
    }

    if (-not [System.IO.Directory]::Exists($targetFullPath)) {
        Write-Output 'Application folder was already absent.'
        exit 0
    }

    $root = [System.IO.Path]::GetPathRoot($targetFullPath).TrimEnd('\')
    if (-not $targetFullPath -or $targetFullPath -eq $root) {
        throw "Caminho de instalação inválido: '$targetFullPath'."
    }

    $rootAttributes = [System.IO.File]::GetAttributes($targetFullPath)
    if (($rootAttributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
        throw 'A pasta de instalação é um ponto de nova análise (junction/symlink); remoção recusada.'
    }

    function Assert-NoReparsePoints {
        param([Parameter(Mandatory = $true)][string]$DirectoryPath)

        $pending = [System.Collections.Generic.Stack[string]]::new()
        $pending.Push($DirectoryPath)
        while ($pending.Count -gt 0) {
            $current = $pending.Pop()
            foreach ($entry in [System.IO.Directory]::EnumerateFileSystemEntries($current)) {
                $attributes = [System.IO.File]::GetAttributes($entry)
                if (($attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                    throw "Ponto de nova análise inesperado na instalação: '$entry'. Remoção recusada."
                }
                if (($attributes -band [System.IO.FileAttributes]::Directory) -ne 0) {
                    $pending.Push($entry)
                }
            }
        }
    }

    $maximumAttempts = 6
    $lastError = $null

    for ($attempt = 1; $attempt -le $maximumAttempts; $attempt++) {
        try {
            if ([System.IO.Directory]::Exists($targetFullPath)) {
                # Refuse junctions/symlinks inside the application tree. The helper
                # runs from %TEMP%, not from the directory it is removing.
                Assert-NoReparsePoints -DirectoryPath $targetFullPath
                # Directory.Delete removes the files and the root directory, including
                # the empty-directory case that the previous batch command left behind.
                [System.IO.Directory]::Delete($targetFullPath, $true)
            }

            if (-not [System.IO.Directory]::Exists($targetFullPath)) {
                Write-Output "Removed application folder (attempt $attempt/$maximumAttempts)."
                exit 0
            }

            $lastError = 'A pasta ainda existe após Directory.Delete.'
        }
        catch {
            $lastError = '{0}: {1}' -f $_.Exception.GetType().FullName, $_.Exception.Message
            Write-Output "Application folder cleanup attempt $attempt/$maximumAttempts failed: $lastError"
        }

        if ($attempt -lt $maximumAttempts) {
            Start-Sleep -Milliseconds 750
        }
    }

    throw "Não foi possível remover '$targetFullPath' após $maximumAttempts tentativas. Último erro: $lastError"
}
catch {
    [Console]::Error.WriteLine('FAILED: application folder cleanup: {0}' -f $_.Exception.Message)
    exit 1
}
