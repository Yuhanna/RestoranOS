# Downloads stock photos listed in manifest.json from Unsplash.
param(
    [string]$ManifestPath = "$PSScriptRoot\..\apps\api\src\RestaurantOS.Api\media\stock\manifest.json"
)

$ErrorActionPreference = "Continue"
$base = Split-Path $ManifestPath -Parent
$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$downloads = @{}

foreach ($photo in $manifest.photos) {
    if ($photo.sourceUrl) {
        $downloads[$photo.file] = $photo.sourceUrl
    }
}

$ok = 0
$failed = 0
foreach ($entry in $downloads.GetEnumerator()) {
    $dest = Join-Path $base ($entry.Key -replace '/', '\')
    $dir = Split-Path $dest -Parent
    if (!(Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    try {
        Invoke-WebRequest -Uri $entry.Value -OutFile $dest -UseBasicParsing
        Write-Host "OK $($entry.Key)"
        $ok++
    } catch {
        Write-Host "FAILED $($entry.Key): $($_.Exception.Message)"
        $failed++
    }
}

Write-Host "Done. $ok downloaded, $failed failed."
