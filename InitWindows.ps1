# ======================================================
#  Windows 11 Setup Script for Exhibition
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
# Done
# ------------------------------------------------------
Write-Host "`nSetup complete. Restart recommended." -ForegroundColor Yellow
