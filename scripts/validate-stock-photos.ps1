# Validates stock photo library integrity (manifest, files, encoding, duplicates).
param(
    [string]$StockRoot = "$PSScriptRoot\..\apps\api\src\RestaurantOS.Api\media\stock"
)

$ErrorActionPreference = "Stop"
$manifestPath = Join-Path $StockRoot "manifest.json"
if (-not (Test-Path $manifestPath)) {
    Write-Error "manifest.json not found at $manifestPath"
}

$manifest = Get-Content $manifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$errors = @()

# UTF-8 mojibake / replacement character check
$mojibake = Select-String -Path $manifestPath -Pattern 'Ã|Å|Ä|�' -AllMatches
if ($mojibake) {
    $errors += "manifest.json contains invalid encoding ($($mojibake.Matches.Count) matches)"
}

$ids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($photo in $manifest.photos) {
    if (-not $ids.Add($photo.id)) {
        $errors += "Duplicate photo id: $($photo.id)"
    }

    $filePath = Join-Path $StockRoot ($photo.file -replace '/', '\')
    if (-not (Test-Path $filePath)) {
        $errors += "Missing file for $($photo.id): $($photo.file)"
    }

    if ([string]::IsNullOrWhiteSpace($photo.sourceUrl)) {
        $errors += "Missing sourceUrl for $($photo.id)"
    }
}

$jpgFiles = Get-ChildItem $StockRoot -Recurse -Include *.jpg, *.png -File
$hashGroups = $jpgFiles | ForEach-Object {
    [pscustomobject]@{
        File = $_.FullName.Substring($StockRoot.Length + 1)
        Hash = (Get-FileHash $_.FullName -Algorithm MD5).Hash
    }
} | Group-Object Hash | Where-Object { $_.Count -gt 1 }

$duplicateCount = ($hashGroups | Measure-Object).Count
$uniqueCount = ($jpgFiles | ForEach-Object { (Get-FileHash $_.FullName -Algorithm MD5).Hash } | Select-Object -Unique).Count

Write-Host "Manifest v$($manifest.version): $($manifest.photos.Count) entries, $($jpgFiles.Count) files, $uniqueCount unique hashes, $duplicateCount duplicate groups"

if ($duplicateCount -gt 0) {
    Write-Host "WARN: $duplicateCount duplicate image hash groups (variants may share visuals)"
    foreach ($g in $hashGroups | Select-Object -First 5) {
        Write-Host "  $($g.Name.Substring(0,8))... -> $($g.Group.File -join ', ')"
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
}

Write-Host "Stock photo validation passed."
