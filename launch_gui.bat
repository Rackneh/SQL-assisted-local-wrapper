@echo off
setlocal
cd /d "%~dp0"

echo ==========================================================
echo   Launching NVIDIA Dory 1.0 4B GUI Studio
echo   URL: http://127.0.0.1:5000
echo ==========================================================

:: Start browser after 1.5 seconds in background
start "" cmd /c "timeout /t 2 /nobreak >nul && start http://127.0.0.1:5000"

:: Start GUI server
.\.venv\Scripts\python.exe -m uvicorn gui.app:app --host 0.0.0.0 --port 5000 --reload

pause
