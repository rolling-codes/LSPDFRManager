# Install .NET 8 Desktop Runtime
# Downloads and installs from Microsoft official source

$ErrorActionPreference = "Stop"

Write-Host ".NET 8 Desktop Runtime Installer" -ForegroundColor Cyan
Write-Host "================================`n"

Write-Host "Checking for .NET 8 Desktop Runtime..."
$dotnetCheck = & dotnet --list-runtimes 2>$null | Select-String "WindowsDesktop.App 8\."

if ($dotnetCheck) {
    Write-Host "✓ .NET 8 Desktop Runtime already installed" -ForegroundColor Green
    exit 0
}

Write-Host "Not found. Downloading installer from Microsoft..."

$installerUrl = "https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe"
$installerPath = "$env:TEMP\dotnet-runtime-installer.exe"

try {
    $webClient = New-Object System.Net.WebClient
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $webClient.DownloadFile($installerUrl, $installerPath)
    Write-Host "✓ Downloaded" -ForegroundColor Green

    Write-Host "Running installer (this may take 1-2 minutes)..."
    & $installerPath /quiet /norestart | Out-Null
    Write-Host "✓ Installation complete" -ForegroundColor Green

    Start-Sleep -Seconds 3

    Write-Host "Verifying installation..."
    $verify = & dotnet --list-runtimes 2>$null | Select-String "WindowsDesktop.App 8\."

    if ($verify) {
        Write-Host "✓ .NET 8 Desktop Runtime verified" -ForegroundColor Green
        exit 0
    } else {
        Write-Host "✗ Verification failed" -ForegroundColor Red
        Write-Host "Download manually from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0"
        exit 1
    }

} catch {
    Write-Host "✗ Error: $_" -ForegroundColor Red
    Write-Host "Download manually from: https://dotnet.microsoft.com/en-us/download/dotnet/8.0"
    exit 1
} finally {
    if (Test-Path $installerPath) {
        Remove-Item $installerPath -Force -ErrorAction Ignore
    }
}
