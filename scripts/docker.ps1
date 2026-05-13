$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Command = if ($args.Count -gt 0) { $args[0] } else { "" }
$Service = if ($args.Count -gt 1) { $args[1] } else { "" }
$ComposeFile = Join-Path $Root "docker-compose.yml"
$ServiceArgs = if ([string]::IsNullOrWhiteSpace($Service)) { @() } else { @($Service) }

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

function Show-Usage {
    Write-Error "Usage: .\scripts\docker.ps1 <up|down|restart|logs|status> [backend|frontend]"
    exit 1
}

switch ($Command) {
    "up" {
        if (-not (Test-Path (Join-Path $Root ".env"))) {
            Write-Error "ERROR: $(Join-Path $Root ".env") not found. Run scripts/setup.ps1 first."
            exit 1
        }

        $ServiceLabel = if ($Service) { " ($Service)" } else { "" }
        Write-Host "==> Starting stack$ServiceLabel..."
        Invoke-Checked docker compose --file $ComposeFile up -d --build @ServiceArgs
        Write-Host ""

        $Port = if ($env:PORT) { $env:PORT } else { "5181" }
        Write-Host "Backend  -> http://localhost:$Port"
        Write-Host "Frontend -> http://localhost:5173"
    }
    "down" {
        $ServiceLabel = if ($Service) { " ($Service)" } else { "" }
        Write-Host "==> Stopping stack$ServiceLabel..."
        Invoke-Checked docker compose --file $ComposeFile down @ServiceArgs
    }
    "restart" {
        $ServiceLabel = if ($Service) { " ($Service)" } else { "" }
        Write-Host "==> Restarting stack$ServiceLabel..."
        Invoke-Checked docker compose --file $ComposeFile down @ServiceArgs
        Invoke-Checked docker compose --file $ComposeFile up -d --build @ServiceArgs
    }
    "logs" {
        Invoke-Checked docker compose --file $ComposeFile logs -f @ServiceArgs
    }
    "status" {
        Invoke-Checked docker compose --file $ComposeFile ps @ServiceArgs
    }
    default {
        Show-Usage
    }
}
