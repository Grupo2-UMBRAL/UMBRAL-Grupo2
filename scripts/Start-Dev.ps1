# Get the physical network adapter IP (usually Wi-Fi or Ethernet) to pass to Expo
$ip = (Get-NetIPAddress -AddressFamily IPv4 -PrefixOrigin Dhcp | Where-Object { $_.InterfaceAlias -notmatch 'vEthernet|Loopback|WSL' } | Select-Object -First 1).IPAddress

if (-not $ip) {
    # Fallback to any IPv4 address
    $ip = (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -notmatch 'Loopback' } | Select-Object -First 1).IPAddress
}

if (-not $ip) {
    Write-Warning "Could not determine local IP address. Expo might not connect from physical devices."
} else {
    Write-Host "Detected Host IP: $ip"
    $env:REACT_NATIVE_PACKAGER_HOSTNAME = $ip
}

# Start the full development stack including web and mobile
Write-Host "Starting Docker Compose Dev Stack..."
docker compose -f docker-compose.dev.yml up -d

Write-Host ""
Write-Host "============================================="
Write-Host "          DEV STACK STARTED                  "
Write-Host "============================================="
Write-Host "Web:     http://localhost:3000"
Write-Host "Mobile:  http://localhost:19600"
if ($ip) {
    Write-Host "Mobile QR Code (Expo) IP: $ip"
}
Write-Host "============================================="
