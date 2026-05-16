$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ApiDir = Join-Path $Root "backend\src\Api"
$DataDir = Join-Path $ApiDir "Data"
$LogsDir = Join-Path $ApiDir "Logs"

$LogDays = if ($env:RETENTION_LOG_DAYS) { [int]$env:RETENTION_LOG_DAYS } else { 30 }
$AuditDays = if ($env:RETENTION_AUDIT_DAYS) { [int]$env:RETENTION_AUDIT_DAYS } else { 30 }
$FeedbackDays = if ($env:RETENTION_FEEDBACK_DAYS) { [int]$env:RETENTION_FEEDBACK_DAYS } else { 30 }
$VectorStoreDays = if ($env:RETENTION_VECTOR_STORE_DAYS) { [int]$env:RETENTION_VECTOR_STORE_DAYS } else { 90 }
$UploadArtifactDays = if ($env:RETENTION_UPLOAD_ARTIFACT_DAYS) { [int]$env:RETENTION_UPLOAD_ARTIFACT_DAYS } else { 7 }
$DryRun = if ($env:DRY_RUN) { $env:DRY_RUN } else { "1" }
$IncludeVectorStore = if ($env:INCLUDE_VECTOR_STORE) { $env:INCLUDE_VECTOR_STORE } else { "0" }

function Get-TargetFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Target
    )

    if (-not (Test-Path $Target)) {
        return @()
    }

    $Item = Get-Item $Target
    if ($Item.PSIsContainer) {
        return Get-ChildItem -Path $Target -File -Recurse
    }

    return @($Item)
}

function Invoke-RetentionCleanup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Target,
        [Parameter(Mandatory = $true)]
        [int]$AgeDays,
        [Parameter(Mandatory = $true)]
        [string]$Label
    )

    if (-not (Test-Path $Target)) {
        Write-Host "[skip] $Label`: $Target (not found)"
        return
    }

    $Threshold = (Get-Date).AddDays(-$AgeDays)
    $OldFiles = Get-TargetFiles -Target $Target | Where-Object { $_.LastWriteTime -lt $Threshold }

    if ($DryRun -eq "1") {
        Write-Host "[dry-run] $Label files older than $AgeDays day(s):"
        foreach ($File in $OldFiles) {
            Write-Host $File.FullName
        }
        return
    }

    Write-Host "[delete] $Label files older than $AgeDays day(s)"
    foreach ($File in $OldFiles) {
        Write-Host $File.FullName
        Remove-Item -Force $File.FullName
    }
}

Write-Host "==> Retention cleanup started (DRY_RUN=$DryRun)"
Invoke-RetentionCleanup -Target $LogsDir -AgeDays $LogDays -Label "API logs"
Invoke-RetentionCleanup -Target (Join-Path $DataDir "ingestion-audit.json") -AgeDays $AuditDays -Label "Ingestion audit"
Invoke-RetentionCleanup -Target (Join-Path $DataDir "conversation-feedback.json") -AgeDays $FeedbackDays -Label "Conversation feedback"
Invoke-RetentionCleanup -Target (Join-Path $Root "evidences\raw") -AgeDays $UploadArtifactDays -Label "Evidence raw artifacts"
Invoke-RetentionCleanup -Target (Join-Path $Root "e2e\test-results") -AgeDays $UploadArtifactDays -Label "E2E test results"
Invoke-RetentionCleanup -Target (Join-Path $Root "e2e\playwright-report") -AgeDays $UploadArtifactDays -Label "Playwright reports"

if ($IncludeVectorStore -eq "1") {
    Invoke-RetentionCleanup -Target (Join-Path $DataDir "vector-store.json") -AgeDays $VectorStoreDays -Label "Vector store"
} else {
    Write-Host "[skip] Vector store cleanup disabled (set INCLUDE_VECTOR_STORE=1 to enable)."
}

Write-Host "==> Retention cleanup finished"
