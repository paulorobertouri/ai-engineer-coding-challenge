$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$BuildDir = Join-Path $Root ".build"
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

function Run-BackendTests {
    Write-Host "==> Backend: dotnet test..."
    $BackendCoverageDir = Join-Path $BuildDir "coverage\backend"
    $BackendResultsDir = Join-Path $BuildDir "test-results\backend"
    New-Item -ItemType Directory -Force -Path $BackendCoverageDir | Out-Null
    New-Item -ItemType Directory -Force -Path $BackendResultsDir | Out-Null

    Invoke-Checked dotnet test (Join-Path $Root "backend\src\Api.Tests\Api.Tests.csproj") -c Release `
        "--collect:XPlat Code Coverage" `
        --results-directory $BackendResultsDir `
        "/p:CollectCoverage=true" `
        "/p:CoverletOutputFormat=cobertura" `
        "/p:CoverletOutput=$(Join-Path $BackendCoverageDir "")" `
        "/p:ReportTypes=Html" `
        "/p:Exclude=[GroceryStore.Chatbot.Api]Api.Services.OpenAIRetrievalChatService%2C[GroceryStore.Chatbot.Api]Api.Services.OpenAIEmbeddingService"
}

function Run-FrontendTests {
    Write-Host "==> Frontend: vitest..."
    Push-Location (Join-Path $Root "frontend")
    try {
        Invoke-Checked npm test -- --coverage
    } finally {
        Pop-Location
    }
}

switch ($Target) {
    "backend" {
        Run-BackendTests
    }
    "frontend" {
        Run-FrontendTests
    }
    "all" {
        Run-BackendTests
        Write-Host ""
        Run-FrontendTests
    }
    default {
        Write-Error "Usage: .\scripts\test.ps1 [all|backend|frontend]"
        exit 1
    }
}

Write-Host ""
Write-Host "All tests passed."
