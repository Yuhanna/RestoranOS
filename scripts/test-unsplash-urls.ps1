param([string[]]$Urls)
$q = "?w=960&h=640&fit=crop&auto=format&q=85"
foreach ($entry in $Urls) {
    $parts = $entry -split '\|', 2
    $name = $parts[0]
    $id = $parts[1]
    $url = "https://images.unsplash.com/$id$q"
    try {
        $resp = Invoke-WebRequest -Uri $url -Method Head -UseBasicParsing
        Write-Output "OK|$name|$id|$($resp.Headers['Content-Length'])"
    } catch {
        Write-Output "FAIL|$name|$id"
    }
}
