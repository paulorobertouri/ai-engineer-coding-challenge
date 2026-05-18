$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$BackendUrl = if ($env:BACKEND_URL) { $env:BACKEND_URL } else { "http://localhost:5181" }
$Timestamp = (Get-Date).ToUniversalTime().ToString("yyyyMMddTHHmmssZ")
$OutDir = Join-Path $Root "test-results/diagnostics-$Timestamp"
$SummaryFile = Join-Path $OutDir "summary.txt"

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null

function Add-Section {
    param([string]$Title)

    Add-Content -Path $SummaryFile -Value ""
    Add-Content -Path $SummaryFile -Value "## $Title"
}

function Invoke-Capture {
    param(
        [string]$Title,
        [scriptblock]$Command
    )

    Add-Section -Title $Title
    try {
        & $Command | Out-File -FilePath $SummaryFile -Append -Encoding utf8
    }
    catch {
        Add-Content -Path $SummaryFile -Value "(command failed: $($_.Exception.Message))"
    }
}

function Export-SanitizedEnv {
    param(
        [string]$InputPath,
        [string]$OutputPath
    )

    if (-not (Test-Path $InputPath)) {
        return
    }

    $lines = Get-Content -Path $InputPath
    $sanitized = foreach ($line in $lines) {
        if ($line -match '^\s*#' -or $line -notmatch '=') {
            $line
            continue
        }

        $parts = $line -split '=', 2
        $key = $parts[0]
        $value = $parts[1]

        if ($key -match 'KEY|TOKEN|SECRET|PASSWORD|CONNECTIONSTRING') {
            if ([string]::IsNullOrWhiteSpace($value)) {
                "$key="
            }
            else {
                "$key=***REDACTED***"
            }
        }
        else {
            $line
        }
    }

    $sanitized | Out-File -FilePath $OutputPath -Encoding utf8
}

@(
    "SOP Assistant diagnostics",
    "Generated at (UTC): $Timestamp",
    "Workspace: $Root",
    "Backend URL: $BackendUrl"
) | Out-File -FilePath $SummaryFile -Encoding utf8

Invoke-Capture "Host environment" { uname -a }
Invoke-Capture "Git status" { git -C $Root status --short }
Invoke-Capture "Git branch" { git -C $Root branch --show-current }
Invoke-Capture "Dotnet info" { dotnet --info }
Invoke-Capture "Node version" { node --version }
Invoke-Capture "Npm version" { npm --version }
Invoke-Capture "Docker version" { docker --version }
Invoke-Capture "Docker compose version" { docker compose version }

Add-Section "Health endpoint"
$healthPath = Join-Path $OutDir "health.json"
try {
    Invoke-RestMethod -Uri "$BackendUrl/api/v1/health" -TimeoutSec 8 | ConvertTo-Json -Depth 8 | Out-File -FilePath $healthPath -Encoding utf8
}
catch {
    '{"error":"health endpoint unavailable"}' | Out-File -FilePath $healthPath -Encoding utf8
}
Get-Content -Path $healthPath | Out-File -FilePath $SummaryFile -Append -Encoding utf8

Add-Section "Readiness endpoint"
$readyPath = Join-Path $OutDir "ready.json"
try {
    Invoke-RestMethod -Uri "$BackendUrl/api/v1/ready" -TimeoutSec 8 | ConvertTo-Json -Depth 8 | Out-File -FilePath $readyPath -Encoding utf8
}
catch {
    '{"error":"ready endpoint unavailable"}' | Out-File -FilePath $readyPath -Encoding utf8
}
Get-Content -Path $readyPath | Out-File -FilePath $SummaryFile -Append -Encoding utf8

Invoke-Capture "Docker compose status" { docker compose --file (Join-Path $Root "docker-compose.yml") ps }

$dockerLogsPath = Join-Path $OutDir "docker-logs.txt"
try {
    docker compose --file (Join-Path $Root "docker-compose.yml") logs --tail 200 backend frontend | Out-File -FilePath $dockerLogsPath -Encoding utf8
}
catch {
    "docker logs unavailable" | Out-File -FilePath $dockerLogsPath -Encoding utf8
}

Add-Section "Recent docker logs"
Get-Content -Path $dockerLogsPath | Out-File -FilePath $SummaryFile -Append -Encoding utf8

$backendLogsDir = Join-Path $Root "backend/src/Api/Logs"
if (Test-Path $backendLogsDir) {
    $tailPath = Join-Path $OutDir "backend-log-tail.txt"
    Get-ChildItem -Path $backendLogsDir -File | Sort-Object LastWriteTime -Descending | Select-Object -First 3 | ForEach-Object {
        Add-Content -Path $tailPath -Value ""
        Add-Content -Path $tailPath -Value "### $($_.Name)"
        Get-Content -Path $_.FullName -Tail 200 | Add-Content -Path $tailPath
    }
}

Add-Section "Sanitized config"
$sanitizedEnv = Join-Path $OutDir "env.sanitized"
$sanitizedFrontendEnv = Join-Path $OutDir "frontend-env.sanitized"
Export-SanitizedEnv -InputPath (Join-Path $Root ".env") -OutputPath $sanitizedEnv
Export-SanitizedEnv -InputPath (Join-Path $Root "frontend/.env") -OutputPath $sanitizedFrontendEnv

if (Test-Path $sanitizedEnv) {
    Add-Content -Path $SummaryFile -Value ""
    Add-Content -Path $SummaryFile -Value "### .env"
    Get-Content -Path $sanitizedEnv | Add-Content -Path $SummaryFile
}

if (Test-Path $sanitizedFrontendEnv) {
    Add-Content -Path $SummaryFile -Value ""
    Add-Content -Path $SummaryFile -Value "### frontend/.env"
    Get-Content -Path $sanitizedFrontendEnv | Add-Content -Path $SummaryFile
}

Write-Host "Diagnostics written to:" 
Write-Host "  $OutDir"
Write-Host "Main summary:"
Write-Host "  $SummaryFile"
