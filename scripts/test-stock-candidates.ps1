# Tests Unsplash candidate URLs for stock photo enrichment.
param(
    [string[]]$ExistingHashes = @(),
    [string]$CandidateFile = "$PSScriptRoot\stock-candidates.json"
)

$q = "?w=960&h=640&fit=crop&auto=format&q=85"
$tmpdir = Join-Path $env:TEMP "unsplash-candidates-$(Get-Random)"
New-Item -ItemType Directory -Force -Path $tmpdir | Out-Null

if (-not (Test-Path $CandidateFile)) {
    Write-Error "Candidate file not found: $CandidateFile"
    exit 1
}

$candidates = Get-Content $CandidateFile -Raw | ConvertFrom-Json
$seen = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($h in $ExistingHashes) { [void]$seen.Add($h) }

$results = @()
foreach ($item in $candidates) {
    $url = "https://images.unsplash.com/$($item.photoId)$q"
    try {
        $dest = Join-Path $tmpdir "$($item.id).jpg"
        Invoke-WebRequest -Uri $url -OutFile $dest -UseBasicParsing
        $hash = (Get-FileHash $dest -Algorithm MD5).Hash.Substring(0, 8)
        if ($seen.Contains($hash)) {
            $results += [pscustomobject]@{ Status = "DUP"; Id = $item.id; PhotoId = $item.photoId; Hash = $hash }
        } else {
            [void]$seen.Add($hash)
            $results += [pscustomobject]@{ Status = "OK"; Id = $item.id; PhotoId = $item.photoId; Hash = $hash; Category = $item.category; File = $item.file; Title = $item.title; Alt = $item.alt; Tags = ($item.tags -join ", ") }
        }
    } catch {
        $results += [pscustomobject]@{ Status = "FAIL"; Id = $item.id; PhotoId = $item.photoId; Hash = "" }
    }
}

$results | Format-Table -AutoSize
Write-Host "`nOK: $(($results | Where-Object Status -eq 'OK').Count) | DUP: $(($results | Where-Object Status -eq 'DUP').Count) | FAIL: $(($results | Where-Object Status -eq 'FAIL').Count)"
$okPath = Join-Path $PSScriptRoot "stock-candidates-ok.json"
$results | Where-Object Status -eq "OK" | ConvertTo-Json -Depth 5 | Set-Content $okPath -Encoding UTF8
Write-Host "Saved OK list to $okPath"
