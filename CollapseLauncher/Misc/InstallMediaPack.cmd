@echo off
echo Please do not close this console window until it finishes
powershell.exe -ExecutionPolicy Bypass -Command "Get-WindowsCapability -Online | Where-Object -Property Name -Like "*Media.MediaFeaturePack*" | Add-WindowsCapability -Online"