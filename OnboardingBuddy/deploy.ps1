#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory=$false)]
    [string]$PublishPath = ".\publish"
)

Write-Host "OnboardingBuddy - Simple Deployment" -ForegroundColor Green
Write-Host "===================================" -ForegroundColor Green
Write-Host "Publish Path: $PublishPath" -ForegroundColor Cyan
Write-Host ""

# Ensure we are in the OnboardingBuddy project directory
if (-not (Test-Path "OnboardingBuddy.csproj")) {
    Write-Error "OnboardingBuddy.csproj not found. Please run this script from the OnboardingBuddy project directory."
    exit 1
}

# Clean publish directory (preserve appsettings.json)
Write-Host ""
Write-Host "Cleaning publish directory..." -ForegroundColor Yellow
if (Test-Path $PublishPath) {
    # Backup appsettings.json if it exists
    $appsettingsFile = Join-Path $PublishPath "appsettings.json"
    $appsettingsBackup = $null
    if (Test-Path $appsettingsFile) {
        $appsettingsBackup = Join-Path $env:TEMP "appsettings.json.backup"
        try {
            Copy-Item $appsettingsFile $appsettingsBackup -Force
            Write-Host "Backed up appsettings.json" -ForegroundColor Green
        } catch {
            Write-Host "Could not backup appsettings.json: $_" -ForegroundColor Yellow
        }
    }
    
    # Delete all items except appsettings.json
    Get-ChildItem -Path $PublishPath -Recurse | ForEach-Object {
        if ($_.Name -ne "appsettings.json") {
            try {
                Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop
            } catch {
                # Ignore errors for locked files
                Write-Host "Could not delete $($_.Name) (file may be locked)" -ForegroundColor Yellow
            }
        }
    }
    
    # Restore appsettings.json if we backed it up
    if ($appsettingsBackup -and (Test-Path $appsettingsBackup)) {
        try {
            Copy-Item $appsettingsBackup $appsettingsFile -Force
            Remove-Item $appsettingsBackup -Force
            Write-Host "Restored appsettings.json" -ForegroundColor Green
        } catch {
            Write-Host "Could not restore appsettings.json: $_" -ForegroundColor Yellow
        }
    }
    
    Write-Host "Cleaned publish directory (preserved appsettings.json)" -ForegroundColor Green
} else {
    Write-Host "Publish directory does not exist, will be created" -ForegroundColor Green
}

# Build Vue.js frontend
Write-Host ""
Write-Host "Building Vue.js frontend..." -ForegroundColor Yellow
Set-Location "ClientApp"

# Clean previous build
if (Test-Path "../wwwroot") {
    # Keep the onboarding-buddy-icon.svg but remove other files
    Get-ChildItem "../wwwroot" -Exclude "onboarding-buddy-icon.svg" | Remove-Item -Recurse -Force
    Write-Host "Cleaned previous build" -ForegroundColor Green
}

# Install dependencies
Write-Host "Installing npm dependencies..." -ForegroundColor Yellow
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to install npm dependencies"
    exit 1
}
Write-Host "npm dependencies installed" -ForegroundColor Green

# Build application
Write-Host "Building Vue.js application..." -ForegroundColor Yellow
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to build Vue.js application"
    exit 1
}
Write-Host "Vue.js application built" -ForegroundColor Green

Set-Location ".."

# Publish .NET application
Write-Host ""
Write-Host "Publishing .NET application..." -ForegroundColor Yellow
dotnet publish "OnboardingBuddy.csproj" --configuration Release --output $PublishPath --framework net9.0 --runtime win-x64 --self-contained false --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to publish .NET application"
    exit 1
}

Write-Host ".NET application published" -ForegroundColor Green

# Create necessary directories
Write-Host ""
Write-Host "Setting up directories..." -ForegroundColor Yellow
$directories = @("logs", "uploads")
foreach ($dir in $directories) {
    $dirPath = Join-Path $PublishPath $dir
    if (-not (Test-Path $dirPath)) {
        New-Item -ItemType Directory -Path $dirPath -Force | Out-Null
        Write-Host "Created directory: $dir" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "DEPLOYMENT COMPLETE!" -ForegroundColor Green
Write-Host ""
Write-Host "Published to: $PublishPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Copy files to your web server" -ForegroundColor White
Write-Host "2. Ensure appsettings.json has correct database connection string" -ForegroundColor White
Write-Host "3. Configure IIS or hosting environment" -ForegroundColor White
Write-Host "4. Test the application" -ForegroundColor White
