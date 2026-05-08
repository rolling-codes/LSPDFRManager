@echo off
setlocal enabledelayedexpansion

cd /d "%~dp0"

echo.
echo Checking for .NET 8 Desktop Runtime...
echo.

dotnet --list-runtimes 2>nul | find "WindowsDesktop.App 8." >nul 2>&1

if %errorlevel% neq 0 goto :install_dotnet

:launch
echo [OK] .NET 8 Desktop Runtime found.
echo.

:: Kill any leftover API process from a previous run
taskkill /IM LSPDFRManager.Api.exe /F >nul 2>&1
timeout /t 1 /nobreak >nul 2>&1

:: Start the API server hidden in the background
if exist "%~dp0LSPDFRManager.Api.exe" (
    echo Starting LSPDFRManager.Api...
    start "" /B "%~dp0LSPDFRManager.Api.exe"
    echo [OK] API server started.
) else (
    echo [WARN] LSPDFRManager.Api.exe not found - Browse tab will be unavailable.
)

echo Launching LSPDFRManager...
echo.
"%~dp0LSPDFRManager.exe"

:: Clean up API when main app exits
taskkill /IM LSPDFRManager.Api.exe /F >nul 2>&1
exit /b 0

:install_dotnet
echo [ERROR] .NET 8 Desktop Runtime not found.
echo.
echo Attempting to download and install .NET 8 Desktop Runtime...
echo This may take 1-2 minutes.
echo.

set "INSTALLER=%TEMP%\dotnet-runtime-installer.exe"
set "DOWNLOAD_URL=https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"

powershell -Command "try { [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12; (New-Object Net.WebClient).DownloadFile('%DOWNLOAD_URL%', '%INSTALLER%'); exit 0 } catch { exit 1 }" >nul 2>&1

if %errorlevel% neq 0 (
    echo [FAILED] Could not download .NET 8 Desktop Runtime.
    echo.
    echo Download and install manually from:
    echo https://dotnet.microsoft.com/en-us/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)

echo Running installer (this may take a minute)...
"%INSTALLER%" /quiet /norestart >nul 2>&1

timeout /t 3 /nobreak >nul 2>&1

dotnet --list-runtimes 2>nul | find "WindowsDesktop.App 8." >nul 2>&1

if %errorlevel% equ 0 (
    goto :launch
) else (
    echo [FAILED] .NET 8 Desktop Runtime still not found.
    echo.
    echo Please install manually from:
    echo https://dotnet.microsoft.com/en-us/download/dotnet/8.0
    echo.
    pause
    exit /b 1
)
