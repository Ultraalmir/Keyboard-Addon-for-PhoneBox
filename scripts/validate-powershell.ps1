[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptRoots = @(
    (Join-Path $repoRoot 'scripts'),
    (Join-Path $repoRoot 'src\PhoneBoxKeyboardAddon')
)

$scriptFiles = @(
    foreach ($root in $scriptRoots) {
        if (Test-Path -LiteralPath $root -PathType Container) {
            Get-ChildItem -LiteralPath $root -Filter '*.ps1' -File -Recurse
        }
    }
) | Sort-Object -Property FullName -Unique

if ($scriptFiles.Count -eq 0) {
    throw 'Nenhum script PowerShell foi encontrado para validação.'
}

$errors = [System.Collections.Generic.List[string]]::new()

foreach ($scriptFile in $scriptFiles) {
    $tokens = $null
    $parseErrors = $null

    [System.Management.Automation.Language.Parser]::ParseFile(
        $scriptFile.FullName,
        [ref]$tokens,
        [ref]$parseErrors
    ) | Out-Null

    foreach ($parseError in $parseErrors) {
        $relative = $scriptFile.FullName.Substring($repoRoot.Length).TrimStart('\', '/')
        $errors.Add(("{0}:{1}:{2}: {3}" -f
            $relative,
            $parseError.Extent.StartLineNumber,
            $parseError.Extent.StartColumnNumber,
            $parseError.Message))
    }
}

if ($errors.Count -gt 0) {
    throw ("Falha na validação de sintaxe PowerShell:`n" + ($errors -join [Environment]::NewLine))
}

Write-Host ("Sintaxe PowerShell aprovada: {0} arquivos." -f $scriptFiles.Count) -ForegroundColor Green
