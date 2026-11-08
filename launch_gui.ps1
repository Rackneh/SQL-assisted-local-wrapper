$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $PSScriptRoot

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Launching NVIDIA Dory 1.0 4B GUI Studio" -ForegroundColor Green
Write-Host "  URL: http://127.0.0.1:5000" -ForegroundColor Cyan
Write-Host "==========================================================" -ForegroundColor Cyan

Start-Job -ScriptBlock {
    Start-Sleep -Seconds 2
    Start-Process "http://127.0.0.1:5000"
} | Out-Null

& "$PSScriptRoot\.venv\Scripts\python.exe" -m uvicorn gui.app:app --host 0.0.0.0 --port 5000 --reload
