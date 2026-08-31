# RestaurantOS gelistirme ortami: telefon/LAN erisimi icin 5173 ve 5183 portlarini acar.
# PowerShell'i YONETICI olarak calistirin:
#   Set-ExecutionPolicy -Scope Process Bypass -Force; .\scripts\open-dev-firewall.ps1

$ErrorActionPreference = "Stop"

function Ensure-FirewallRule {
    param(
        [string]$Name,
        [int]$Port
    )

    $existing = netsh advfirewall firewall show rule name="$Name" 2>$null
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Kural zaten var: $Name"
        return
    }

    netsh advfirewall firewall add rule name="$Name" dir=in action=allow protocol=TCP localport=$Port
    Write-Host "Eklendi: $Name (TCP $Port)"
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

Ensure-FirewallRule -Name "RestaurantOS Customer Web 5173" -Port 5173
Ensure-FirewallRule -Name "RestaurantOS API 5183" -Port 5183

$lanIp = Get-LanIPv4
Write-Host ""
Write-Host "Telefon test URL (demo):" -ForegroundColor Green
Write-Host "  http://${lanIp}:5173/?qr=demo-marea-table-7"
Write-Host ""
Write-Host "IP degistiyse start-qr-menu.ps1 tekrar calistirin; yeni QR uretin." -ForegroundColor Yellow
