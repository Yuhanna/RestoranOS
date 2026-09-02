# Applies validated photo URL fixes from stock-photo-fixes-ok.json to manifest and re-downloads images.
param(
    [string]$ManifestPath = "$PSScriptRoot\..\apps\api\src\RestaurantOS.Api\media\stock\manifest.json",
    [string]$FixesPath = "$PSScriptRoot\stock-photo-fixes-ok.json"
)

$manifest = Get-Content $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$fixes = Get-Content $FixesPath -Raw -Encoding UTF8 | ConvertFrom-Json
$byId = @{}
foreach ($f in $fixes) { $byId[$f.Id] = $f }

$updated = 0
foreach ($photo in $manifest.photos) {
    if (-not $byId.ContainsKey($photo.id)) { continue }
    $fix = $byId[$photo.id]
    $photo.sourceUrl = "https://images.unsplash.com/$($fix.PhotoId)?w=960&h=640&fit=crop&auto=format&q=85"
    $updated++
}

$manifest.version = [int]$manifest.version + 1
$json = $manifest | ConvertTo-Json -Depth 10
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($ManifestPath, $json, $utf8)

$base = Split-Path $ManifestPath -Parent
$ok = 0; $fail = 0
foreach ($fix in $fixes) {
    $photo = $manifest.photos | Where-Object { $_.id -eq $fix.Id } | Select-Object -First 1
    if (-not $photo) { Write-Host "SKIP (not in manifest): $($fix.Id)"; continue }
    $dest = Join-Path $base ($photo.file -replace '/', '\')
    $dir = Split-Path $dest -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    try {
        Invoke-WebRequest -Uri $photo.sourceUrl -OutFile $dest -UseBasicParsing
        Write-Host "OK $($photo.file)"
        $ok++
    } catch {
        Write-Host "FAIL $($photo.file): $($_.Exception.Message)"
        $fail++
    }
}

Write-Host "Manifest v$($manifest.version): updated $updated entries, downloaded $ok, failed $fail"
