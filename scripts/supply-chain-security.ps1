$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$ReportDir = Join-Path $Root "security-reports"
$TmpDotnetToolsDir = Join-Path $Root ".tmp-tools"
$NodeForbiddenLicenses = "GPL-1.0;GPL-2.0;GPL-3.0;AGPL-1.0;AGPL-3.0;LGPL-2.0;LGPL-2.1;LGPL-3.0;SSPL-1.0"

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

New-Item -ItemType Directory -Path $ReportDir -Force | Out-Null
Get-ChildItem -Path $ReportDir -Filter *.json -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Get-ChildItem -Path $ReportDir -Filter *.spdx.json -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

Write-Host "==> Secret scanning (Trivy filesystem scanner)..."
Invoke-Checked docker run --rm -v "${Root}:/workspace" aquasec/trivy:0.65.0 fs --scanners secret --severity HIGH,CRITICAL --exit-code 1 /workspace

Write-Host "==> Building local images for SBOM and vulnerability scanning..."
Invoke-Checked docker build -t ai-sop-backend:local (Join-Path $Root "backend\src\Api")
Invoke-Checked docker build -t ai-sop-frontend:local (Join-Path $Root "frontend")

Write-Host "==> Image vulnerability scanning (Trivy)..."
Invoke-Checked docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy:0.65.0 image --severity HIGH,CRITICAL --ignore-unfixed --exit-code 1 ai-sop-backend:local
Invoke-Checked docker run --rm -v /var/run/docker.sock:/var/run/docker.sock aquasec/trivy:0.65.0 image --severity HIGH,CRITICAL --ignore-unfixed --exit-code 1 ai-sop-frontend:local

Write-Host "==> Generating SBOMs (Syft)..."
Invoke-Checked docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v "${ReportDir}:/out" anchore/syft:1.38.0 ai-sop-backend:local -o spdx-json=/out/backend-image.spdx.json
Invoke-Checked docker run --rm -v /var/run/docker.sock:/var/run/docker.sock -v "${ReportDir}:/out" anchore/syft:1.38.0 ai-sop-frontend:local -o spdx-json=/out/frontend-image.spdx.json

Write-Host "==> Node dependency license checks..."
Push-Location (Join-Path $Root "frontend")
try {
    Invoke-Checked npx --yes license-checker@25.0.1 --production --failOn $NodeForbiddenLicenses --summary
} finally {
    Pop-Location
}

Push-Location (Join-Path $Root "e2e")
try {
    Invoke-Checked npx --yes license-checker@25.0.1 --production --failOn $NodeForbiddenLicenses --summary
} finally {
    Pop-Location
}

Write-Host "==> .NET dependency license checks..."
New-Item -ItemType Directory -Path $TmpDotnetToolsDir -Force | Out-Null

try {
    Invoke-Checked dotnet tool install dotnet-project-licenses --tool-path $TmpDotnetToolsDir --version 2.7.1
} catch {
    # Ignore install failures when the tool is already installed.
}

$PreviousRollForward = $env:DOTNET_ROLL_FORWARD
$env:DOTNET_ROLL_FORWARD = "Major"
try {
    Invoke-Checked (Join-Path $TmpDotnetToolsDir "dotnet-project-licenses") `
        -i (Join-Path $Root "backend\AIEngineerCodingChallenge.Backend.slnx") `
        --forbidden-license-types (Join-Path $Root "security\licenses\dotnet-forbidden-licenses.json") `
        --output jsonPretty `
        --outfile (Join-Path $ReportDir "dotnet-licenses.json")
} finally {
    $env:DOTNET_ROLL_FORWARD = $PreviousRollForward
}

Write-Host "==> Supply chain checks completed. Reports saved in $ReportDir"
