# Allow inbound TCP 5173 for customer-web LAN phone testing (Private profile).
# Run once in elevated PowerShell if phones hang or get "Bu siteye ulaşılamıyor".

$ruleName = "RestaurantOS Customer Web (5173)"
$existing = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
if ($existing) {
    Write-Host "Firewall rule already exists: $ruleName"
    exit 0
}

New-NetFirewallRule `
    -DisplayName $ruleName `
    -Direction Inbound `
    -Action Allow `
    -Protocol TCP `
    -LocalPort 5173 `
    -Profile Private `
    -Description "Dev Vite customer-web for QR menu phones on the same Wi-Fi"

Write-Host "Created firewall rule: $ruleName"
