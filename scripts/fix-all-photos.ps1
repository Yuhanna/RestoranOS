$ErrorActionPreference = 'Continue'
$base = "D:\projcts\RestPROJ\apps\api\src\RestaurantOS.Api\media\stock"
$ManifestPath = "$base\manifest.json"
$manifest = Get-Content $ManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json

# Mapping of ID -> Wikimedia Search Query or Direct URL
$fixes = @{
    "adana-kebap" = "Adana kebab"
    "adana-kebap-2" = "Adana kebab"
    "kebap" = "Turkish kebab"
    "kebap-2" = "Turkish kebab"
    "kebap-3" = "Urfa kebab"
    "kebap-4" = "Shish kebab"
    "iskender" = "Iskender kebap"
    "iskender-2" = "Iskender kebap"
    "iskender-3" = "Iskender kebap"
    "iskender-4" = "Iskender kebap"
    "lahmacun" = "Lahmacun"
    "lahmacun-2" = "Lahmacun"
    "lahmacun-4" = "Lahmacun"
    "manti" = "Turkish manti"
    "manti-2" = "Turkish manti"
    "manti-4" = "Turkish manti"
    "pide" = "Pide"
    "pide-2" = "Kıymalı pide"
    "kusbasili-pide" = "Kuşbaşılı pide"
    "kofte" = "Izgara köfte"
    "kofte-2" = "Izgara köfte"
    "kofte-4" = "Izgara köfte"
    "pilav" = "Turkish pilav"
    "dolma" = "Yaprak sarma"
    "doner-durum" = "Dürüm"
    "doner-durum-2" = "Dürüm"
    "doner-plate" = "Döner kebab plate"
    "wrap" = "Wrap sandwich"
    "shawarma" = "Shawarma"
    "guvec" = "Güveç"
    "karisik-izgara" = "Mixed grill"
    "kanat-izgara" = "Grilled chicken wings"
    "chicken-wings" = "Chicken wings"
    "chicken-wings-2" = "Chicken wings"
    "chicken-wings-3" = "Chicken wings"
    "fried-chicken" = "Fried chicken"
    "nuggets" = "Chicken nuggets"
    "nuggets-2" = "Chicken nuggets"
    "fish-chips" = "Fish and chips"
    "fish-chips-2" = "Fish and chips"
    "somon" = "Grilled salmon"
    "tavuk-izgara" = "Grilled chicken breast"
    "balik-tava" = "Fried fish"
    "ahtapot" = "Grilled octopus"
    "kumpir" = "Kumpir"
    "kuru-fasulye" = "Kuru fasulye"

    "kunefe" = "Künefe"
    "sutlac" = "Sütlaç"
    "kazandibi" = "Kazandibi"
    "trilece" = "Tres leches cake"
    "kavun" = "Sliced melon"
    "dondurma" = "Ice cream in bowl"
    "dondurma-2" = "Turkish ice cream"
    "baklava" = "Turkish baklava"
    "profiterol" = "Profiterole"
    "panna-cotta" = "Panna cotta"
    "cikolatali-fondan" = "Chocolate fondant"
    "brownie" = "Chocolate brownie"
    "cheesecake" = "Cheesecake"

    "gozleme" = "Gözleme"
    "menemen" = "Menemen"
    "omlet" = "Omelette"
    "simit" = "Simit"
    "sucuklu-yumurta" = "Sucuklu yumurta"
    "kahvalti-tabagi" = "Turkish breakfast"
    "granola" = "Granola bowl"

    "ayran" = "Ayran"
    "ayran-2" = "Ayran"
    "cay" = "Turkish tea"
    "cay-bardak" = "Turkish tea"
    "bitki-cayi" = "Herbal tea"
    "filtre-kahve" = "Filter coffee glass"
    "turk-kahvesi" = "Turkish coffee"
    "cola" = "Glass of cola"
    "greyfurt" = "Grapefruit juice"
    "salgam" = "Şalgam"
    "sarap" = "Glass of red wine"
    "soda-maden-suyu" = "Mineral water glass"
    "viski" = "Glass of whiskey"
    "vodka-tonic" = "Vodka tonic"
    "mojito" = "Mojito"
    "margarita" = "Margarita cocktail"
    "kokteyl" = "Cocktail"
    "buzlu-cay" = "Iced tea"
    "limonata" = "Lemonade"
    "sicak-cikolata" = "Hot chocolate"
    "smoothie" = "Smoothie"

    "hummus" = "Hummus"
    "iskembe-corbasi" = "İşkembe çorbası"
    "kelle-paca-corbasi" = "Kelle paça"
    "mercimek-corbasi" = "Mercimek çorbası"
    "ezogelin-corbasi" = "Ezogelin çorbası"
    "yayla-corbasi" = "Yayla çorbası"
    "domates-corbasi" = "Tomato soup"
    "meze-tabagi" = "Turkish meze"
    "meze-tabagi-2" = "Turkish meze"
    "peynir-tabagi" = "Cheese platter"
    "haydari" = "Haydari"
    "haydari-2" = "Haydari"
    "saksuka" = "Şakşuka"
    "muhammara" = "Muhammara"
    "acili-ezme" = "Ezme"
    "sigara-boregi" = "Sigara böreği"
    "coban-salata" = "Çoban salatası"
    "coban-salata-2" = "Çoban salatası"
    "gavurdagi-salata" = "Gavurdağı salatası"
    "akdeniz-salata" = "Mediterranean salad"
    "caesar-salad" = "Caesar salad"
    "patates-kizartmasi" = "French fries"
    "patates-kizartmasi-2" = "French fries"
    "patates-kizartmasi-menu" = "French fries"
    "kalamar" = "Fried squid"
    "kalamar-2" = "Fried squid"
}

function Get-WikiImage ($query) {
    $url = "https://commons.wikimedia.org/w/api.php?action=query&generator=search&gsrsearch=$([uri]::EscapeDataString($query))&gsrnamespace=6&gsrlimit=3&prop=imageinfo&iiprop=url&format=json"
    try {
        $response = Invoke-WebRequest -Uri $url -UseBasicParsing | ConvertFrom-Json
        if ($response.query -and $response.query.pages) {
            foreach ($prop in $response.query.pages.psobject.properties) {
                $imgUrl = $prop.Value.imageinfo[0].url
                if ($imgUrl -match '\.(jpg|jpeg|png)$') { return $imgUrl }
            }
            # Fallback if first doesn't match jpg/png
            foreach ($prop in $response.query.pages.psobject.properties) {
                return $prop.Value.imageinfo[0].url
            }
        }
    } catch {}
    return $null
}

$updated = 0
$failed = 0

foreach ($fix in $fixes.GetEnumerator()) {
    $photo = $manifest.photos | Where-Object id -eq $fix.Key | Select-Object -First 1
    if (-not $photo) { continue }
    
    $wikiUrl = Get-WikiImage $fix.Value
    if ($wikiUrl) {
        $dest = Join-Path $base ($photo.file -replace '/','\')
        try {
            Invoke-WebRequest -Uri $wikiUrl -OutFile $dest -UseBasicParsing
            $photo.sourceUrl = $wikiUrl
            Write-Host "OK $($fix.Key) -> $($fix.Value)"
            $updated++
        } catch {
            Write-Host "FAIL DL $($fix.Key)"
            $failed++
        }
    } else {
        Write-Host "NOT FOUND $($fix.Key) -> $($fix.Value)"
        $failed++
    }
}

$manifest.version = [int]$manifest.version + 1
$json = $manifest | ConvertTo-Json -Depth 10
$utf8 = New-Object System.Text.UTF8Encoding $false
[System.IO.File]::WriteAllText($ManifestPath, $json, $utf8)

$files = Get-ChildItem $base -Recurse -Filter *.jpg
$unique = ($files | ForEach-Object { (Get-FileHash $_.FullName -Algorithm MD5).Hash } | Select-Object -Unique).Count
$lines = @(
  '# Stok urun fotograflari','',
  'Bu klasordeki gorseller [Wikimedia Commons](https://commons.wikimedia.org) ve [Unsplash](https://unsplash.com) uzerinden indirilmistir.',
  'Ucretsiz lisanslar kapsaminda kullanima uygundur.',
  '',"Manifest surumu: $($manifest.version) -- $($files.Count) dosya, $unique benzersiz gorsel.",'',
  '| Dosya | Kaynak URL |','| --- | --- |'
)
foreach ($photo in $manifest.photos) {
    $lines += "| ``$($photo.file)`` | $($photo.sourceUrl) |"
}
$lines -join "`n" | Set-Content "$base\ATTRIBUTION.md" -Encoding UTF8

Write-Host "Done. Updated $updated, Failed $failed. Total unique hashes: $unique / $($files.Count)"
