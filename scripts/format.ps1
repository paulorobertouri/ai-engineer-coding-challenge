$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Fix = $args.Count -gt 0 -and ($args[0] -eq "--fix" -or $args[0] -eq "-Fix" -or $args[0] -eq "-fix")

function Invoke-Checked {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Arguments
    )

    & $FilePath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$FilePath failed with exit code $LASTEXITCODE."
    }
}

Write-Host "==> Backend: dotnet format..."
if ($Fix) {
    Invoke-Checked dotnet format (Join-Path $Root "backend\AIEngineerCodingChallenge.Backend.slnx")
} else {
    Invoke-Checked dotnet format (Join-Path $Root "backend\AIEngineerCodingChallenge.Backend.slnx") --verify-no-changes
}

Write-Host ""
Write-Host "==> Frontend: ESLint..."
Push-Location (Join-Path $Root "frontend")
try {
    if ($Fix) {
        Invoke-Checked npx eslint . --fix
    } else {
        Invoke-Checked npm run lint
    }

    Write-Host ""
    Write-Host "==> Frontend: Prettier..."
    if ($Fix) {
        Invoke-Checked npm run format
    } else {
        Invoke-Checked npx prettier --check .
    }
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "==> E2E: Prettier..."
$Prettier = Join-Path $Root "frontend\node_modules\.bin\prettier.cmd"
if (-not (Test-Path $Prettier)) {
    $Prettier = Join-Path $Root "frontend\node_modules\.bin\prettier"
}

$E2EConfig = Join-Path $Root "e2e\playwright.config.ts"
$E2EEvidenceConfig = Join-Path $Root "e2e\playwright.evidence.config.ts"
$E2ETests = Join-Path $Root "e2e\tests"
if ($Fix) {
    Invoke-Checked $Prettier --write $E2EConfig $E2EEvidenceConfig $E2ETests
} else {
    Invoke-Checked $Prettier --check $E2EConfig $E2EEvidenceConfig $E2ETests
}

Write-Host ""
Write-Host "Format/lint checks passed."
