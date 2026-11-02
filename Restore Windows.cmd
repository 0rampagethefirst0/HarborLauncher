@echo off
if exist "%~dp0App\Harbor.exe" (
    start "" "%~dp0App\Harbor.exe" --restore
) else (
    reg delete "HKCU\Software\Microsoft\Windows NT\CurrentVersion\Winlogon" /v Shell /f >nul 2>&1
    schtasks /delete /tn "Harbor Desktop" /f >nul 2>&1
    start "" explorer.exe
)
