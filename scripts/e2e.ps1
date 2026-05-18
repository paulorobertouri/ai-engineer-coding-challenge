$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$E2EDir = Join-Path $Root "e2e"
$Mode = if ($args.Count -gt 0) { $args[0] } else { "test" }

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

if (-not (Test-Path (Join-Path $E2EDir "node_modules"))) {
    Write-Host "==> Installing e2e dependencies..."
    Invoke-Checked npm ci --prefix $E2EDir
}

switch ($Mode) {
    "test" {
        Write-Host "==> Running e2e tests..."
        Push-Location $E2EDir
        try {
            Invoke-Checked npx playwright test
        } finally {
            Pop-Location
        }
    }
    "evidence" {
        Write-Host "==> Generating evidence (screenshots)..."
        Push-Location $E2EDir
        try {
            Invoke-Checked npx playwright test --config=playwright.evidence.config.ts tests/evidence.spec.ts
        } finally {
            Pop-Location
        }
        Write-Host ""
        Write-Host "Artifacts saved in evidences/raw/ and .build/reports/playwright/"
    }
    default {
        Write-Error "Usage: .\scripts\e2e.ps1 [test|evidence]"
        exit 1
    }
}
