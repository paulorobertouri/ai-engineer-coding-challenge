$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path

Write-Host "==> Running backend tests with chaos profile enabled"

$env:Chaos__Enabled = "true"
$env:Chaos__FailureRate = "1.0"

try {
    dotnet test (Join-Path $Root "backend\src\Api.Tests\Api.Tests.csproj") --filter "FullyQualifiedName~ChaosInjectionMiddlewareTests|FullyQualifiedName~HealthControllerTests"
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet test failed with exit code $LASTEXITCODE."
    }
}
finally {
    Remove-Item Env:Chaos__Enabled -ErrorAction SilentlyContinue
    Remove-Item Env:Chaos__FailureRate -ErrorAction SilentlyContinue
}

Write-Host ""
Write-Host "Chaos profile tests passed."
