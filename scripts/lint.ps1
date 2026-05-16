$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Target = if ($args.Count -gt 0) { $args[0] } else { "all" }

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

function Run-BackendLint {
    Write-Host "==> Backend: no dedicated lint step configured (skipping)."
}

function Run-FrontendLint {
    Write-Host "==> Frontend: ESLint..."
    Push-Location (Join-Path $Root "frontend")
    try {
        Invoke-Checked npm run lint
    } finally {
        Pop-Location
    }
}

switch ($Target) {
    "backend" {
        Run-BackendLint
    }
    "frontend" {
        Run-FrontendLint
    }
    "all" {
        Run-BackendLint
        Write-Host ""
        Run-FrontendLint
    }
    default {
        Write-Error "Usage: .\scripts\lint.ps1 [all|backend|frontend]"
        exit 1
    }
}

Write-Host ""
Write-Host "Lint checks passed."
