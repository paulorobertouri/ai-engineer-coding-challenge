$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$Mode = if ($args.Count -gt 0) { $args[0] } else { "" }
$BackupPath = if ($args.Count -gt 1) { $args[1] } else { "" }
$ComposeFile = Join-Path $Root "docker-compose.yml"
$VectorStorePath = Join-Path $Root "backend/src/Api/Data/vector-store.json"

function Show-Usage {
    Write-Error "Usage: .\scripts\rollback.ps1 <app|ingest> [backup-file]"
    exit 1
}

switch ($Mode) {
    "app" {
        Write-Host "==> Rolling back app stack (restart known-good local compose state)..."
        docker compose --file $ComposeFile down
        docker compose --file $ComposeFile up -d --build
        Write-Host "Rollback complete. Verify with /api/v1/health and /api/v1/ready."
    }
    "ingest" {
        if ([string]::IsNullOrWhiteSpace($BackupPath)) {
            Write-Error "Backup path is required for ingest rollback."
            Show-Usage
        }

        if (-not (Test-Path $BackupPath)) {
            Write-Error "Backup file not found: $BackupPath"
            exit 1
        }

        $targetDir = Split-Path -Parent $VectorStorePath
        New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
        Copy-Item -Path $BackupPath -Destination $VectorStorePath -Force

        Write-Host "Ingest rollback complete. Restored vector store from: $BackupPath"
        Write-Host "Target path: $VectorStorePath"
    }
    default {
        Show-Usage
    }
}
