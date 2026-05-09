# LSPDFRManager Remote Setup Script
# Downloads executable and dependencies from GitHub, installs to Program Files

$ErrorActionPreference = "Stop"

Write-Host "LSPDFRManager v3.2.1 Setup" -ForegroundColor Cyan
Write-Host "==========================`n"

$packageDirName = "LSPDFRManager-v3.2.1"
$installParent = $env:ProgramFiles
$installDir = Join-Path $installParent $packageDirName
$exeZipUrl = "https://github.com/rolling-codes/LSPDFRManager/releases/download/v3.2.1/LSPDFRManager-v3.2.1-win-x64.zip"
$exeZipPath = "$env:TEMP\LSPDFRManager-v3.2.1-win-x64.zip"

try {
    Write-Host "Preparing installation directory: $installDir"
    if (Test-Path $installDir) {
        Remove-Item $installDir -Recurse -Force
    }
    New-Item -ItemType Directory -Path $installParent -Force | Out-Null

    Write-Host "Downloading executable from release..."
    $webClient = New-Object System.Net.WebClient
    [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
    $webClient.DownloadFile($exeZipUrl, $exeZipPath)
    Write-Host "✓ Downloaded" -ForegroundColor Green

    Write-Host "Extracting files..."
    Add-Type -Assembly System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::ExtractToDirectory($exeZipPath, $installParent)
    Write-Host "✓ Extracted" -ForegroundColor Green

    Write-Host "Release files installed to: $installDir`n"

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
