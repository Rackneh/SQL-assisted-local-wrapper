@echo off
rem kill_server.bat — stop Dory backend / inference / native client, then exit.
rem Will prompt for administrator rights if needed (UAC).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0kill_server.ps1"
pause
