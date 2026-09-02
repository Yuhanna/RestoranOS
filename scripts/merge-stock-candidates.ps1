# Merges validated stock-candidates-ok.json into manifest.json (version bump).
param(
    [string]$ManifestPath = "$PSScriptRoot\..\apps\api\src\RestaurantOS.Api\media\stock\manifest.json",
    [string]$OkPath = "$PSScriptRoot\stock-candidates-ok.json"
)

$manifest = Get-Content $ManifestPath -Raw | ConvertFrom-Json
$ok = Get-Content $OkPath -Raw | ConvertFrom-Json
$existingIds = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($p in $manifest.photos) { [void]$existingIds.Add($p.id) }

$added = 0
foreach ($item in $ok) {
    if ($existingIds.Contains($item.Id)) { continue }
    $tags = ($item.Tags -split ",\s*") | Where-Object { $_ }
    $photo = [ordered]@{
        id = $item.Id
        category = $item.Category
        file = $item.File
        title = $item.Title
        alt = $item.Alt
        tags = @($tags)
        sourceUrl = "https://images.unsplash.com/$($item.PhotoId)?w=960&h=640&fit=crop&auto=format&q=85"
    }
    $manifest.photos += [pscustomobject]$photo
    [void]$existingIds.Add($item.Id)
    $added++
}

$manifest.version = [int]$manifest.version + 1
$manifest | ConvertTo-Json -Depth 10 | Set-Content $ManifestPath -Encoding UTF8
Write-Host "Added $added photos. Manifest version: $($manifest.version). Total: $($manifest.photos.Count)"
