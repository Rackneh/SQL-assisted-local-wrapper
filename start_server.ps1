param(
    [switch]$NoGui = $false,
    [switch]$NoBrowser = $false,
    [int]$Loops = 2,
    [ValidateSet("per_loop", "last_loop")]
    [string]$CacheMode = "last_loop",
    [int]$Port = 8080,
    [int]$GuiPort = 5000,
    [string]$HostAddress = "0.0.0.0",
    [string]$ApiKey = "",
    [int]$CtxSize = 32768,
    [int]$Threads = 4,
    [int]$GpuLayers = 999
)

if (Test-Path "D:\VulkanSDK") {
    $env:VULKAN_SDK = "D:\VulkanSDK"
    $env:PATH = "D:\VulkanSDK\Bin;" + $env:PATH
}

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
Set-Location $PSScriptRoot

$VulkanServer = Join-Path $PSScriptRoot "llama.cpp-dory\build-vulkan\bin\Release\llama-server.exe"
$CpuServer = Join-Path $PSScriptRoot "llama.cpp-dory\build\bin\Release\llama-server.exe"
if (Test-Path $VulkanServer) {
    $LlamaServer = $VulkanServer
    $EngineType = "Vulkan GPU (AMD Radeon RX 580 - 8GB)"
} else {
    $LlamaServer = $CpuServer
    $EngineType = "CPU (AVX2 fallback)"
}
$ModelPath = Join-Path $PSScriptRoot "models\NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf"
$PythonExe = Join-Path $PSScriptRoot ".venv\Scripts\python.exe"

if (-not (Test-Path $LlamaServer)) {
    Write-Error "llama-server.exe not found at: $LlamaServer"
    exit 1
}

if (-not (Test-Path $ModelPath)) {
    Write-Warning "Model file not found at: $ModelPath"
    Write-Host "Please run python download_model.py first!" -ForegroundColor Yellow
    exit 1
}

if (-not $NoGui) {
    if (-not (Test-Path $PythonExe)) {
        Write-Error "Python venv not found at: $PythonExe"
        exit 1
    }

    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "  Starting NVIDIA Nemotron Dory 1.0 4B Studio & Engine" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Cyan
    Write-Host "  GUI Studio:   http://127.0.0.1:${GuiPort} (and http://127.0.0.1:${Port})" -ForegroundColor Yellow
    Write-Host "  API Endpoint: http://127.0.0.1:${Port}/v1 (and http://127.0.0.1:${GuiPort}/v1)" -ForegroundColor Yellow
    Write-Host "  LAN Endpoint: http://192.168.178.24:${Port}/v1" -ForegroundColor Yellow
    Write-Host "  Context:      ${CtxSize} tokens (32K)" -ForegroundColor Yellow
    Write-Host "  Hardware:     $EngineType" -ForegroundColor Green
    Write-Host "  Reasoning:    deepseek format (delta.reasoning_content active)" -ForegroundColor Green
    Write-Host "==========================================================" -ForegroundColor Cyan

    if (-not $NoBrowser) {
        Start-Job -ScriptBlock {
            param($p)
            Start-Sleep -Seconds 2
            Start-Process "http://127.0.0.1:$p"
        } -ArgumentList $GuiPort | Out-Null
    }

    & $PythonExe -m uvicorn gui.app:app --host 0.0.0.0 --port $GuiPort --reload
    exit $LASTEXITCODE
}

# Standalone Console Mode (-NoGui)
$OverrideKV = "dory.num_loops=int:${Loops},dory.recurrent_kv_cache_mode=str:${CacheMode}"

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Starting NVIDIA Dory 1.0 4B llama-server (Console Mode)" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "  Model:           $ModelPath"
Write-Host "  Recurrent Loops: $Loops"
Write-Host "  KV Cache:        $CacheMode"
Write-Host "  Threads:         $Threads"
Write-Host "  Context Size:    $CtxSize"
Write-Host "  Host:            $HostAddress (Port: $Port)"
Write-Host "  API Endpoint:    http://127.0.0.1:${Port}/v1" -ForegroundColor Yellow
Write-Host "  Reasoning:       deepseek format (OpenCode compatible)" -ForegroundColor Green
Write-Host "==========================================================" -ForegroundColor Cyan

$ServerArgs = @(
    "-m", $ModelPath,
    "--alias", "dory",
    "--host", $HostAddress,
    "--port", $Port,
    "-ngl", $GpuLayers,
    "-t", $Threads,
    "--ctx-size", $CtxSize,
    "--parallel", "1",
    "--batch-size", "512",
    "--ubatch-size", "512",
    "--flash-attn", "auto",
    "--context-shift",
    "--sse-ping-interval", "15",
    "--timeout", "3600",
    "--jinja",
    "--reasoning", "on",
    "--reasoning-format", "deepseek",
    "--reasoning-effort", "medium",
    "--temp", "1.0",
    "--top-p", "0.95",
    "--top-k", "0",
    "--min-p", "0",
    "--repeat-penalty", "1",
    "--override-kv", $OverrideKV
)

if ($ApiKey) {
    $ServerArgs += @("--api-key", $ApiKey)
}

& $LlamaServer @ServerArgs
