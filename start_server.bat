@echo off
setlocal
cd /d "%~dp0"

if exist "D:\VulkanSDK" (
    set "VULKAN_SDK=D:\VulkanSDK"
    set "PATH=D:\VulkanSDK\Bin;%PATH%"
)

set "VULKAN_SERVER=%~dp0llama.cpp-dory\build-vulkan\bin\Release\llama-server.exe"
set "CPU_SERVER=%~dp0llama.cpp-dory\build\bin\Release\llama-server.exe"

if exist "%VULKAN_SERVER%" (
    set "LLAMA_SERVER=%VULKAN_SERVER%"
    set "BACKEND_NAME=Vulkan GPU (AMD Radeon RX 580 - 8GB)"
) else (
    set "LLAMA_SERVER=%CPU_SERVER%"
    set "BACKEND_NAME=CPU (AVX2 fallback)"
)
set "MODEL=%~dp0models\NVIDIA-Nemotron-Labs-Dory1.0-4B-Q5_K_M.gguf"
set "PYTHON_EXE=%~dp0.venv\Scripts\python.exe"

if not exist "%LLAMA_SERVER%" (
    echo Error: llama-server.exe not found at %LLAMA_SERVER%
    pause
    exit /b 1
)

if not exist "%MODEL%" (
    echo Error: Model file not found at %MODEL%
    echo Please run download_model.py first:
    echo   .\.venv\Scripts\python.exe download_model.py
    pause
    exit /b 1
)

if not exist "%PYTHON_EXE%" (
    echo Error: Python venv not found at %PYTHON_EXE%
    pause
    exit /b 1
)

echo ==========================================================
echo   Starting NVIDIA Nemotron Dory 1.0 4B Studio ^& Engine
echo   GUI Studio:   http://127.0.0.1:5000
echo   API Endpoint: http://127.0.0.1:5000/v1 (and direct :8080)
echo   LAN Endpoint: http://192.168.178.24:5000/v1
echo   Hardware:     %BACKEND_NAME%
echo   Context:      32768 tokens (32K Window)
echo   Reasoning:    deepseek format (delta.reasoning_content)
echo ==========================================================

:: Automatically launch browser to the GUI Studio after 1.5 seconds
start "" cmd /c "timeout /t 2 /nobreak >nul && start http://127.0.0.1:5000"

:: Start GUI Studio (which automatically boots llama-server on port 8080 and monitors activity)
"%PYTHON_EXE%" -m uvicorn gui.app:app --host 0.0.0.0 --port 5000 --reload

pause
