$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$OpenApiOut = Join-Path $Root "frontend\src\generated\openapi.v1.json"
$TypesOut = Join-Path $Root "frontend\src\generated\api-types.ts"
$ApiUrl = "http://127.0.0.1:5181"
$SwaggerUrl = "$ApiUrl/swagger/v1/swagger.json"
$ApiProject = Join-Path $Root "backend\src\Api\Api.csproj"

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

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Error "ERROR: dotnet is required."
    exit 1
}

if (-not (Get-Command npm -ErrorAction SilentlyContinue)) {
    Write-Error "ERROR: npm is required."
    exit 1
}

New-Item -ItemType Directory -Path (Split-Path $OpenApiOut -Parent) -Force | Out-Null

Write-Host "==> Building backend for OpenAPI generation..."
Invoke-Checked dotnet build $ApiProject -c Debug

Write-Host "==> Starting backend to fetch OpenAPI schema..."
$BackendOutLog = [System.IO.Path]::GetTempFileName()
$BackendErrLog = [System.IO.Path]::GetTempFileName()

$BackendProcess = Start-Process dotnet -ArgumentList @("run", "--project", $ApiProject, "--no-build", "--no-launch-profile", "--urls", $ApiUrl) -RedirectStandardOutput $BackendOutLog -RedirectStandardError $BackendErrLog -PassThru

try {
    Write-Host "==> Waiting for backend startup..."
    $Downloaded = $false

    foreach ($Attempt in 1..40) {
        try {
            Invoke-WebRequest -Uri $SwaggerUrl -OutFile $OpenApiOut | Out-Null
            $Downloaded = $true
            break
        } catch {
            Start-Sleep -Milliseconds 500
        }
    }

    if (-not $Downloaded -or -not (Test-Path $OpenApiOut) -or (Get-Item $OpenApiOut).Length -eq 0) {
        Write-Error "ERROR: Failed to download OpenAPI spec from $SwaggerUrl"
        exit 1
    }

    Write-Host "==> Generating TypeScript types from OpenAPI..."
    Push-Location (Join-Path $Root "frontend")
    try {
        Invoke-Checked npx swagger-typescript-api generate `
            --path "src/generated/openapi.v1.json" `
            --output "src/generated" `
            --name "api-types.ts" `
            --no-client `
            --extract-request-body `
            --extract-response-body `
            --extract-request-params

        Write-Host "==> Formatting generated API artifacts..."
        Invoke-Checked npx prettier --write "src/generated/openapi.v1.json" "src/generated/api-types.ts"
    } finally {
        Pop-Location
    }

    Write-Host "==> API type generation complete."
    Write-Host "OpenAPI: $OpenApiOut"
    Write-Host "Types:   $TypesOut"
} finally {
    if ($BackendProcess -and -not $BackendProcess.HasExited) {
        Stop-Process -Id $BackendProcess.Id -Force -ErrorAction SilentlyContinue
        Wait-Process -Id $BackendProcess.Id -ErrorAction SilentlyContinue
    }

    Remove-Item $BackendOutLog -ErrorAction SilentlyContinue
    Remove-Item $BackendErrLog -ErrorAction SilentlyContinue
}
