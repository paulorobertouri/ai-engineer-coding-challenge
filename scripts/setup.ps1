$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

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

Write-Host "==> Setting up environment files..."

$RootEnv = Join-Path $Root ".env"
$RootEnvExample = Join-Path $Root ".env.example"
if (-not (Test-Path $RootEnv)) {
    Copy-Item $RootEnvExample $RootEnv
    Write-Host "    Created $RootEnv from .env.example"
    Write-Host "    !!! Open .env and fill in OpenAI__ApiKey before running the app."
} else {
    Write-Host "    $RootEnv already exists, skipping."
}

$FrontendEnv = Join-Path $Root "frontend\.env"
$FrontendEnvExample = Join-Path $Root "frontend\.env.example"
if (-not (Test-Path $FrontendEnv)) {
    Copy-Item $FrontendEnvExample $FrontendEnv
    Write-Host "    Created $FrontendEnv from frontend/.env.example"
} else {
    Write-Host "    $FrontendEnv already exists, skipping."
}

Write-Host ""
Write-Host "==> Installing frontend dependencies..."
Push-Location (Join-Path $Root "frontend")
try {
    Invoke-Checked npm ci
} finally {
    Pop-Location
}

Write-Host ""
Write-Host "==> Restoring backend (dotnet)..."
Invoke-Checked dotnet restore (Join-Path $Root "backend\AIEngineerCodingChallenge.Backend.slnx")

Write-Host ""
Write-Host "Setup complete."
