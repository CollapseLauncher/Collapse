@echo off
call "%~dp0\InstallMediaPack"
call :InstallPackage "%~df1\Microsoft.VCLibs.140.00_14.0.33519.0_x64__8wekyb3d8bbwe.Appx" VCLibs
call :InstallPackage "%~df1\Microsoft.WebMediaExtensions_1.2.17.0_neutral_~_8wekyb3d8bbwe.AppxBundle" WebMediaExtensions
call :InstallPackage "%~df1\Microsoft.VP9VideoExtensions_1.2.12.0_neutral_~_8wekyb3d8bbwe.AppxBundle" VP9VideoExtensions
call :InstallPackage "%~df1\Microsoft.AV1VideoExtension_2.0.6.0_neutral_~_8wekyb3d8bbwe.AppxBundle" AV1VideoExtensions

goto :EOF

:InstallPackage
    echo Installing package: %2
    powershell.exe -ExecutionPolicy Bypass -Command "Add-AppxPackage '%~df1'"
    goto :EOF