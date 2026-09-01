# Customer-web dev sunucusunu (5173) durdurur.
$ErrorActionPreference = "Stop"

function Stop-ListenersOnPort {
    param([int]$Port)

    $connections = Get-NetTCPConnection -LocalPort $Port -State Listen -ErrorAction SilentlyContinue
    if (-not $connections) {
        Write-Host "Port $Port bos — calisan dev sunucusu yok." -ForegroundColor Yellow
        return $false
    }

    $pids = $connections | Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($pid in $pids) {
        $proc = Get-Process -Id $pid -ErrorAction SilentlyContinue
        $label = if ($proc) { "$($proc.ProcessName) (PID $pid)" } else { "PID $pid" }
        Write-Host "Durduruluyor: $label"
        Stop-Process -Id $pid -Force -ErrorAction SilentlyContinue
    }
    return $true
}

$stopped = Stop-ListenersOnPort -Port 5173
if ($stopped) {
    Write-Host "Port 5173 serbest birakildi. Simdi .\scripts\start-qr-menu.ps1 calistirabilirsiniz." -ForegroundColor Green
}
