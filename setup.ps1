# LSPDFRManager Remote Setup Script
# Downloads executable and dependencies from GitHub, installs to Program Files

$ErrorActionPreference = "Stop"

Write-Host "LSPDFRManager v1.4.0 Setup" -ForegroundColor Cyan
Write-Host "==========================`n"

$installDir = "$env:ProgramFiles\LSPDFRManager"
$exeZipUrl = "https://github.com/rolling-codes/LSPDFRManager/releases/download/v1.4.0/LSPDFRManager-v1.4.0.zip"
$exeZipPath = "$env:TEMP\LSPDFRManager-v1.4.0.zip"

try {
    Write-Host "Creating installation directory: $installDir"
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null

    Write-Host "Downloading executable from release..."
    $webClient = New-Object System.Net.WebClient
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $webClient.DownloadFile($exeZipUrl, $exeZipPath)
    Write-Host "✓ Downloaded" -ForegroundColor Green

    Write-Host "Extracting files..."
    Add-Type -Assembly System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($exeZipPath, $installDir)
    Write-Host "✓ Extracted" -ForegroundColor Green

    Write-Host "Downloading runtime dependencies..."
    $depsUrl = "https://raw.githubusercontent.com/rolling-codes/LSPDFRManager/master/publish_v1.4.0"
    # Note: Full runtime DLLs are in the publish folder. Users can:
    # 1. Clone repo and copy publish_v1.4.0 contents, or
    # 2. Download built artifact from repo releases
    Write-Host "ℹ Runtime files available at: https://github.com/rolling-codes/LSPDFRManager"
    Write-Host "  Clone repo and copy publish_v1.4.0/ contents to: $installDir`n"

    Write-Host "Launching setup..."
    & "$installDir\run.bat"

} catch {
    Write-Host "✗ Error: $_" -ForegroundColor Red
    exit 1
} finally {
    if (Test-Path $exeZipPath) {
        Remove-Item $exeZipPath -Force -ErrorAction Ignore
    }
}
