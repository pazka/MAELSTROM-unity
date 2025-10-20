# ======================================================
#  Windows 11 Setup Script for Exhibition Don't forget about start ms-cxh:localonly
# ======================================================

Write-Host "Starting setup..." -ForegroundColor Cyan

# ------------------------------------------------------
# Disable all notifications
# ------------------------------------------------------
Write-Host "Disabling all notifications..." -ForegroundColor Cyan

Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\PushNotifications" -Name "ToastEnabled" -Type DWord -Value 0 -Force
New-Item -Path "HKCU:\Software\Policies\Microsoft\Windows\Explorer" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Policies\Microsoft\Windows\Explorer" -Name "DisableNotificationCenter" -Type DWord -Value 1 -Force
New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" -Name "NOC_GLOBAL_SETTING_TOASTS_ENABLED" -Type DWord -Value 0 -Force
New-Item -Path "HKCU:\Software\Policies\Microsoft\Windows\CloudContent" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Policies\Microsoft\Windows\CloudContent" -Name "DisableSoftLanding" -Type DWord -Value 1 -Force
Set-ItemProperty -Path "HKCU:\Software\Policies\Microsoft\Windows\CloudContent" -Name "DisableWindowsSpotlightFeatures" -Type DWord -Value 1 -Force
Set-ItemProperty -Path "HKCU:\Software\Policies\Microsoft\Windows\CloudContent" -Name "DisableWindowsConsumerFeatures" -Type DWord -Value 1 -Force
New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\ContentDeliveryManager" -Name "SubscribedContent-338393Enabled" -Type DWord -Value 0 -Force
New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\QuietHours" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\QuietHours" -Name "QuietHoursActive" -Type DWord -Value 1 -Force
New-Item -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" -Force | Out-Null
Set-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Notifications\Settings" -Name "NOC_GLOBAL_SETTING_ALLOW_TOASTS_ABOVE_LOCK" -Type DWord -Value 0 -Force

Write-Host "Notifications disabled." -ForegroundColor Green

# ------------------------------------------------------
# Enable SSH Server
# ------------------------------------------------------
Write-Host "Enabling SSH Server..." -ForegroundColor Cyan
Add-WindowsCapability -Online -Name "OpenSSH.Server~~~~0.0.1.0"
Set-Service -Name sshd -StartupType 'Automatic'
Start-Service sshd
Write-Host "SSH Server enabled and started." -ForegroundColor Green

# ------------------------------------------------------
# Allow inbound ping (ICMPv4)
# ------------------------------------------------------
Write-Host "Allowing ICMPv4 Echo Request (ping)..." -ForegroundColor Cyan
New-NetFirewallRule -DisplayName "Allow ICMPv4-In" -Protocol ICMPv4 -Direction Inbound -Action Allow -Profile Any
Write-Host "Ping allowed." -ForegroundColor Green

# ------------------------------------------------------
# Share C: drive on network with full control
# ------------------------------------------------------
Write-Host "Sharing C: drive with full control for authenticated users..." -ForegroundColor Cyan
$shareName = "C"
$sharePath = "C:\"
New-SmbShare -Name $shareName -Path $sharePath -FullAccess "Authenticated Users"
Write-Host "C: drive shared with full control for logged users." -ForegroundColor Green

# ------------------------------------------------------
# Manage users
# ------------------------------------------------------
Write-Host "Creating admin user 'mini' with password 'mini'..." -ForegroundColor Cyan
net user mini mini /add
net localgroup Administrators mini /add
Write-Host "Admin user 'mini' created." -ForegroundColor Green

# Grant full control of C:\Users\user to mini
Write-Host "Granting full control of C:\Users\user to 'mini'..." -ForegroundColor Cyan
$acl = Get-Acl "C:\Users\user"
$permission = "mini","FullControl","Allow"
$accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule $permission
$acl.SetAccessRule($accessRule)
Set-Acl -Path "C:\Users\user" -AclObject $acl
Write-Host "Full control granted to 'mini' for C:\Users\user." -ForegroundColor Green

# ------------------------------------------------------
# Add program to startup (start.cmd from Desktop)
# ------------------------------------------------------
Write-Host "Adding start.cmd to run at login..." -ForegroundColor Cyan
$startupFolder = "$env:APPDATA\Microsoft\Windows\Start Menu\Programs\Startup"
$desktopPath = "$env:USERPROFILE\Desktop\start.cmd"
$shortcutPath = Join-Path $startupFolder "start.lnk"

if (Test-Path $desktopPath) {
    $WScriptShell = New-Object -ComObject WScript.Shell
    $shortcut = $WScriptShell.CreateShortcut($shortcutPath)
    $shortcut.TargetPath = $desktopPath
    $shortcut.WorkingDirectory = Split-Path $desktopPath
    $shortcut.Save()
    Write-Host "Shortcut created for start.cmd in Startup folder." -ForegroundColor Green
} else {
    Write-Host "Warning: start.cmd not found on Desktop. Please place it there before next login." -ForegroundColor Yellow
}

# ------------------------------------------------------
# Allow firewall rules
# ------------------------------------------------------
# Set the port you use in Unity
Write-Host "Allow firewall rules..." -ForegroundColor Cyan
$port = 5000

# Allow inbound UDP for IPv4
New-NetFirewallRule -DisplayName "Unity UDP Broadcast IPv4" `
    -Direction Inbound `
    -Protocol UDP `
    -LocalPort $port `
    -Action Allow `
    -Profile Any

# Allow inbound UDP for IPv6
New-NetFirewallRule -DisplayName "Unity UDP Multicast IPv6" `
    -Direction Inbound `
    -Protocol UDP `
    -LocalPort $port `
    -RemoteAddress "LocalSubnet","FF02::/16" `
    -Action Allow `
    -Profile Any

# Optional: allow outbound UDP (usually allowed by default)
New-NetFirewallRule -DisplayName "Unity UDP Outbound" `
    -Direction Outbound `
    -Protocol UDP `
    -LocalPort $port `
    -Action Allow `
    -Profile Any

Write-Host "Firewall rules added for UDP port $port (IPv4 broadcast + IPv6 multicast)"


# ------------------------------------------------------
# Done
# ------------------------------------------------------
Write-Host "`nSetup complete. Restart recommended." -ForegroundColor Yellow
