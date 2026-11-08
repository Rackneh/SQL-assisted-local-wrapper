param(
    [int]$Loops = 2,
    [ValidateSet("per_loop", "last_loop")]
    [string]$CacheMode = "per_loop",
    [int]$Threads = 6,
    [string]$Prompt = ""
)

$PSScriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Definition
$LlamaCli = Join-Path $PSScriptRoot "llama.cpp-dory\build\bin\Release\llama-cli.exe"
$ModelPath = Join-Path $PSScriptRoot "models\NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf"

if (-not (Test-Path $LlamaCli)) {
    Write-Error "llama-cli.exe not found at: $LlamaCli"
    exit 1
}

if (-not (Test-Path $ModelPath)) {
    Write-Warning "Model file not found at: $ModelPath"
    Write-Host "Please run python download_model.py first!" -ForegroundColor Yellow
    exit 1
}

$OverrideKV = "dory.num_loops=int:${Loops},dory.recurrent_kv_cache_mode=str:${CacheMode}"

$CliArgs = @(
    "-m", $ModelPath,
    "-t", $Threads,
    "--ctx-size", "4096",
    "--flash-attn", "off",
    "--temp", "1.0",
    "--top-p", "0.95",
    "--top-k", "0",
    "--min-p", "0",
    "--repeat-penalty", "1",
    "--override-kv", $OverrideKV
)

if ($Prompt) {
    $CliArgs += @("-p", $Prompt)
} else {
    $CliArgs += @("-co", "--interactive-first")
}

& $LlamaCli @CliArgs
