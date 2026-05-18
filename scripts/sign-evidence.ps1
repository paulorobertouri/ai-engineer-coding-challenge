$ErrorActionPreference = "Stop"

if ($PSVersionTable.PSVersion.Major -ge 7) {
    $PSNativeCommandUseErrorActionPreference = $true
}

$Root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$EvidenceDir = Join-Path $Root "evidences\raw"
$OutputFile = Join-Path $Root "evidences\evidence-signature-manifest.json"
$SigningKey = $env:EVIDENCE_SIGNING_KEY

if (-not (Test-Path $EvidenceDir)) {
    throw "Evidence directory not found: $EvidenceDir"
}

$files = Get-ChildItem -Path $EvidenceDir -File -Recurse | Sort-Object FullName

$artifacts = foreach ($file in $files) {
    $hashObj = Get-FileHash -Path $file.FullName -Algorithm SHA256
    $relativePath = $file.FullName.Substring($Root.Length + 1).Replace("\", "/")

    $entry = [ordered]@{
        path = $relativePath
        sha256 = $hashObj.Hash.ToLowerInvariant()
    }

    if (-not [string]::IsNullOrWhiteSpace($SigningKey)) {
        $hmac = New-Object System.Security.Cryptography.HMACSHA256
        $hmac.Key = [System.Text.Encoding]::UTF8.GetBytes($SigningKey)
        $signatureBytes = $hmac.ComputeHash([System.Text.Encoding]::UTF8.GetBytes($entry.sha256))
        $entry.signature = ([Convert]::ToHexString($signatureBytes)).ToLowerInvariant()
        $hmac.Dispose()
    }

    [pscustomobject]$entry
}

$manifest = [ordered]@{
    generatedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
    algorithm = "sha256"
    signing = if ([string]::IsNullOrWhiteSpace($SigningKey)) { "unsigned" } else { "hmac-sha256" }
    artifacts = $artifacts
}

$manifest | ConvertTo-Json -Depth 6 | Set-Content -Path $OutputFile -Encoding UTF8
Write-Host "Evidence manifest generated at $OutputFile"
