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

function Build-Service {
    param([Parameter(Mandatory = $true)][string]$Service)

    Write-Host "==> Building Docker image: $Service..."
    Invoke-Checked docker compose --file (Join-Path $Root "docker-compose.yml") build $Service
}

if (-not (Test-Path (Join-Path $Root ".env"))) {
    Write-Error "ERROR: $(Join-Path $Root ".env") not found. Run scripts/setup.ps1 first."
    exit 1
}

switch ($Target) {
    "backend" {
        Build-Service "backend"
    }
    "frontend" {
        Build-Service "frontend"
    }
    "all" {
        Write-Host "==> Building all Docker images..."
        Invoke-Checked docker compose --file (Join-Path $Root "docker-compose.yml") build
    }
    default {
        Write-Error "Usage: .\scripts\build.ps1 [all|backend|frontend]"
        exit 1
    }
}

Write-Host ""
Write-Host "Build complete. Run 'docker compose up -d' to start the stack."
