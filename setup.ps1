# LSPDFRManager Remote Setup Script
# Downloads and installs v1.4.0 from GitHub release

$ErrorActionPreference = "Stop"

Write-Host "LSPDFRManager v1.4.0 Setup" -ForegroundColor Cyan
Write-Host "==========================`n"

$installDir = "$env:ProgramFiles\LSPDFRManager"
$releaseUrl = "https://github.com/rolling-codes/LSPDFRManager/releases/download/v1.4.0/LSPDFRManager-v1.4.0.zip"
$zipPath = "$env:TEMP\LSPDFRManager-v1.4.0.zip"

try {
    Write-Host "Creating installation directory: $installDir"
    New-Item -ItemType Directory -Path $installDir -Force | Out-Null

    Write-Host "Downloading release from GitHub..."
    $webClient = New-Object System.Net.WebClient
    $webClient.DownloadFile($releaseUrl, $zipPath)
    Write-Host "✓ Downloaded" -ForegroundColor Green

    Write-Host "Extracting files..."
    Add-Type -Assembly System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($zipPath, $installDir)
    Write-Host "✓ Extracted" -ForegroundColor Green

    Write-Host "Launching setup..."
    & "$installDir\run.bat"

} catch {
    Write-Host "✗ Error: $_" -ForegroundColor Red
    exit 1
} finally {
    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force -ErrorAction Ignore
    }
}
