# QR mobil menüyü başlatır (customer-web + gerekirse bağımlılıklar).
$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

function Find-Node {
    $candidates = @()
    $nodeCmd = Get-Command node -ErrorAction SilentlyContinue
    if ($nodeCmd) {
        $candidates += $nodeCmd.Source
    }
    $candidates += "$env:ProgramFiles\nodejs\node.exe"
    $candidates += "${env:ProgramFiles(x86)}\nodejs\node.exe"
    return $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

function Get-LanIPv4 {
    $preferred = Get-NetIPAddress -AddressFamily IPv4 -ErrorAction SilentlyContinue |
        Where-Object {
            $_.IPAddress -notlike "127.*" -and
            $_.PrefixOrigin -ne "WellKnown" -and
            $_.IPAddress -match "^(10\.|192\.168\.|172\.(1[6-9]|2[0-9]|3[0-1])\.)"
        } |
        Sort-Object {
            if ($_.InterfaceAlias -match "Wi-?Fi|Wireless|Ethernet") { 0 } else { 1 }
        }, InterfaceMetric |
        Select-Object -First 1 -ExpandProperty IPAddress

    if ($preferred) { return $preferred }
    return "10.0.20.68"
}

function Test-PortListening {
    param([int]$Port)
    return [bool](Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1)
}

function Get-ProcessOnPort {
    param([int]$Port)
    $conn = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $conn) { return $null }
    return Get-Process -Id $conn.OwningProcess -ErrorAction SilentlyContinue
}

function Sync-CustomerWebBaseUrl {
    param([string]$LanIp)

    $settingsPath = Join-Path $repoRoot "apps\api\src\RestaurantOS.Api\appsettings.Development.json"
    if (-not (Test-Path $settingsPath)) {
        return
    }

    $targetUrl = "http://${LanIp}:5173"
    $json = Get-Content $settingsPath -Raw | ConvertFrom-Json
    if (-not $json.CustomerWeb) {
        $json | Add-Member -NotePropertyName CustomerWeb -NotePropertyValue ([pscustomobject]@{})
    }
    $json.CustomerWeb.PublicBaseUrl = $targetUrl
    ($json | ConvertTo-Json -Depth 6) + [Environment]::NewLine | Set-Content $settingsPath -Encoding utf8
    Write-Host "CustomerWeb:PublicBaseUrl -> $targetUrl" -ForegroundColor Cyan
}

$node = Find-Node
if (-not $node) {
    Write-Host ""
    Write-Host "Node.js bulunamadi. QR menu icin Node.js LTS kurmaniz gerekiyor." -ForegroundColor Red
    Write-Host ""
    Write-Host "  winget install OpenJS.NodeJS.LTS" -ForegroundColor Yellow
    Write-Host "  veya https://nodejs.org adresinden LTS indirin." -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Kurulumdan sonra PowerShell'i kapatip acin ve tekrar calistirin:" -ForegroundColor Cyan
    Write-Host "  .\scripts\start-qr-menu.ps1" -ForegroundColor Cyan
    exit 1
}

$env:Path = "$(Split-Path $node);$env:Path"
Write-Host "Node: $(& $node -v)"

if (-not (Get-Command pnpm -ErrorAction SilentlyContinue)) {
    Write-Host "pnpm kuruluyor..."
    npm install -g pnpm
}

Set-Location $repoRoot
if (-not (Test-Path "node_modules")) {
    Write-Host "Bagimliliklar yukleniyor (pnpm install)..."
    pnpm install
}

$lanIp = Get-LanIPv4
Sync-CustomerWebBaseUrl -LanIp $lanIp

try {
    $apiHealth = Invoke-WebRequest -Uri "http://127.0.0.1:5183/health/live" -UseBasicParsing -TimeoutSec 3
    if ($apiHealth.StatusCode -eq 200) {
        Write-Host "API (5183) calisiyor." -ForegroundColor Green
    }
} catch {
    Write-Host "API (5183) henuz calismiyor. Visual Studio'dan RestaurantOS.Api'yi F5 ile baslatin." -ForegroundColor Yellow
    Write-Host "  veya baska bir terminalde: cd apps\api\src\RestaurantOS.Api; dotnet run" -ForegroundColor Yellow
}

$demoUrl = "http://${lanIp}:5173/?qr=demo-marea-table-7"

if (Test-PortListening -Port 5173) {
    $proc = Get-ProcessOnPort -Port 5173
    $procLabel = if ($proc) { "$($proc.ProcessName) PID $($proc.Id)" } else { "bilinmeyen surec" }
    Write-Host ""
    Write-Host "Port 5173 zaten kullaniliyor ($procLabel)." -ForegroundColor Yellow
    Write-Host "Customer-web muhtemelen ZATEN calisiyor — yeniden baslatmaniza gerek yok." -ForegroundColor Green
    Write-Host ""
    Write-Host "Telefon tarayicisinda (PowerShell'de DEGIL) su adresi acin:" -ForegroundColor Cyan
    Write-Host "  $demoUrl" -ForegroundColor Green
    Write-Host ""
    Write-Host "Yeniden baslatmak isterseniz once:" -ForegroundColor Yellow
    Write-Host "  .\scripts\stop-qr-menu.ps1" -ForegroundColor Yellow
    exit 0
}

Write-Host ""
Write-Host "Customer-web baslatiliyor..." -ForegroundColor Green
Write-Host "Telefondan (demo): $demoUrl" -ForegroundColor Green
Write-Host "Gercek masa QR icin yonetim panelinden YENI QR uretin (eski baskilar calismayabilir)." -ForegroundColor Yellow
Write-Host "Telefon erisemezse YONETICI PowerShell:" -ForegroundColor Yellow
Write-Host "  .\scripts\open-dev-firewall.ps1" -ForegroundColor Yellow
Write-Host ""
pnpm web:dev
