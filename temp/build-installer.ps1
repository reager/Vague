# Build and Create Installer for Privacy Filter
# This script builds the application and creates an MSI installer

param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$InstallerDir = Join-Path $ProjectRoot "Installer"
$OutputDir = Join-Path $InstallerDir "bin\$Configuration"

Write-Host "=====================================" -ForegroundColor Cyan
Write-Host "Privacy Filter Installer Build Script" -ForegroundColor Cyan
Write-Host "=====================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Check if WiX Toolset is installed
Write-Host "[1/5] Checking WiX Toolset..." -ForegroundColor Yellow
try {
    $wixVersion = dotnet tool list -g | Select-String "wix"
    if (-not $wixVersion) {
        Write-Host "WiX Toolset not found. Installing..." -ForegroundColor Yellow
        dotnet tool install --global wix --version 5.0.2
        Write-Host "WiX Toolset installed successfully!" -ForegroundColor Green
    } else {
        Write-Host "WiX Toolset is already installed." -ForegroundColor Green
    }
} catch {
    Write-Host "Error checking/installing WiX Toolset: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Build the main application
Write-Host ""
Write-Host "[2/5] Building PrivacyFilter application..." -ForegroundColor Yellow
Set-Location $ProjectRoot
try {
    dotnet publish PrivacyFilter.csproj -c $Configuration -r win-x64 --self-contained false -p:PublishSingleFile=false
    Write-Host "Application built successfully!" -ForegroundColor Green
} catch {
    Write-Host "Error building application: $_" -ForegroundColor Red
    exit 1
}

# Step 3: Verify publish output
Write-Host ""
Write-Host "[3/5] Verifying build output..." -ForegroundColor Yellow
$PublishDir = Join-Path $ProjectRoot "bin\$Configuration\net10.0-windows\win-x64\publish"
if (-not (Test-Path $PublishDir)) {
    Write-Host "Error: Publish directory not found at $PublishDir" -ForegroundColor Red
    exit 1
}
$ExePath = Join-Path $PublishDir "PrivacyFilter.exe"
if (-not (Test-Path $ExePath)) {
    Write-Host "Error: PrivacyFilter.exe not found in publish directory" -ForegroundColor Red
    exit 1
}
Write-Host "Build output verified!" -ForegroundColor Green

# Step 4: Build the installer
Write-Host ""
Write-Host "[4/5] Building MSI installer..." -ForegroundColor Yellow
Set-Location $InstallerDir
try {
    # Use dotnet build with WiX v5 SDK
    dotnet build PrivacyFilter.wixproj -c $Configuration
    Write-Host "Installer built successfully!" -ForegroundColor Green
} catch {
    Write-Host "Error building installer: $_" -ForegroundColor Red
    exit 1
}

# Step 5: Locate and display the installer
Write-Host ""
Write-Host "[5/5] Locating installer..." -ForegroundColor Yellow
$MsiPath = Join-Path $OutputDir "PrivacyFilterInstaller.msi"
if (Test-Path $MsiPath) {
    Write-Host ""
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host "SUCCESS! Installer created:" -ForegroundColor Green
    Write-Host $MsiPath -ForegroundColor Cyan
    Write-Host "=====================================" -ForegroundColor Green
    Write-Host ""
    Write-Host "To install: Double-click the MSI file or run:" -ForegroundColor Yellow
    Write-Host "  msiexec /i `"$MsiPath`"" -ForegroundColor Cyan
    Write-Host ""
} else {
    Write-Host "Warning: MSI file not found at expected location: $MsiPath" -ForegroundColor Yellow
    Write-Host "Searching for MSI files..." -ForegroundColor Yellow
    $FoundMsi = Get-ChildItem -Path $InstallerDir -Recurse -Filter "*.msi" | Select-Object -First 1
    if ($FoundMsi) {
        Write-Host "Found MSI at: $($FoundMsi.FullName)" -ForegroundColor Green
    } else {
        Write-Host "No MSI file found. Check build output for errors." -ForegroundColor Red
        exit 1
    }
}

Set-Location $ProjectRoot
