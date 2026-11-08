# kill_server.ps1 — stop the Dory backend, inference engine, and native client
# so start_server.bat / DoryNative can be launched cleanly.
# Auto-elevates: wedged/orphaned backend processes sometimes run elevated and
# refuse to die from a normal shell.

$principal = [Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()
if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Requesting administrator rights (needed to kill elevated/orphaned processes)..."
    Start-Process powershell -Verb RunAs -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

$killed = @()
$failed = @()

$targets = Get-CimInstance Win32_Process | Where-Object {
    ($_.Name -in @('python.exe','pythonw.exe') -and $_.CommandLine -match 'uvicorn|gui\.app|uvicorn\.app') -or
    ($_.Name -match 'llama-server') -or
    ($_.Name -eq 'DoryNative.exe')
}

foreach ($p in $targets) {
    try {
        Stop-Process -Id $p.ProcessId -Force -ErrorAction Stop
        $killed += "PID $($p.ProcessId)  $($p.Name)  ($($p.CommandLine))"
    } catch {
        $failed += "PID $($p.ProcessId)  $($p.Name)"
    }
}

# Fallback: anything still LISTENING on the backend/inference ports
foreach ($port in 5000, 8080) {
    $owners = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue |
              Select-Object -ExpandProperty OwningProcess -Unique
    foreach ($procId in $owners) {
        if ($procId -and $procId -ne 0 -and ($killed -notmatch "PID $procId\b") -and ($failed -notmatch "PID $procId\b")) {
            try {
                Stop-Process -Id $procId -Force -ErrorAction Stop
                $killed += "PID $procId  (port $port owner)"
            } catch {
                $failed += "PID $procId  (port $port owner)"
            }
        }
    }
}

Start-Sleep -Milliseconds 800

Write-Host ""
if ($killed) {
    Write-Host "Killed:" -ForegroundColor Green
    $killed | ForEach-Object { Write-Host "  $_" }
} else {
    Write-Host "Nothing was running." -ForegroundColor Green
}

$still = Get-NetTCPConnection -LocalPort 5000 -State Listen -ErrorAction SilentlyContinue
if ($still) {
    Write-Host ""
    Write-Warning "Port 5000 is STILL held by PID $(($still.OwningProcess | Select-Object -Unique) -join ', '). Kill it manually in Task Manager."
} else {
    Write-Host "Port 5000 is free." -ForegroundColor Green
}

if ($failed) {
    Write-Host ""
    Write-Warning "Could not kill (access denied even as admin?):"
    $failed | ForEach-Object { Write-Warning "  $_" }
}

Write-Host ""
Write-Host "Done. You can now run start_server.bat (or launch DoryNative.exe)." -ForegroundColor Cyan
