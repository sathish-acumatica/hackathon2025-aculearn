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

# Clean publish directory (preserve appsettings and .env files)
Write-Host ""
Write-Host "Cleaning publish directory..." -ForegroundColor Yellow
if (Test-Path $PublishPath) {
    # Backup all appsettings files if they exist
    $appsettingsFiles = Get-ChildItem -Path $PublishPath -Filter "appsettings*" -File
    $appsettingsBackups = @()
    
    foreach ($file in $appsettingsFiles) {
        $backupFile = Join-Path $env:TEMP "$($file.Name).backup"
        try {
            Copy-Item $file.FullName $backupFile -Force
            $appsettingsBackups += @{
                Original = $file.FullName
                Backup = $backupFile
                Name = $file.Name
            }
            Write-Host "Backed up $($file.Name)" -ForegroundColor Green
        } catch {
            Write-Host "Could not backup $($file.Name): $_" -ForegroundColor Yellow
        }
    }
    
    # Backup all .env files if they exist
    $envFiles = Get-ChildItem -Path $PublishPath -Filter ".env*" -File
    $envBackups = @()
    
    foreach ($file in $envFiles) {
        $backupFile = Join-Path $env:TEMP "$($file.Name).backup"
        try {
            Copy-Item $file.FullName $backupFile -Force
            $envBackups += @{
                Original = $file.FullName
                Backup = $backupFile
                Name = $file.Name
            }
            Write-Host "Backed up $($file.Name)" -ForegroundColor Green
        } catch {
            Write-Host "Could not backup $($file.Name): $_" -ForegroundColor Yellow
        }
    }
    
    # Delete all items except appsettings and .env files
    Get-ChildItem -Path $PublishPath -Recurse | ForEach-Object {
        if (-not (($_.Name -like "appsettings*") -or ($_.Name -like ".env*"))) {
            try {
                Remove-Item $_.FullName -Recurse -Force -ErrorAction Stop
            } catch {
                # Ignore errors for locked files
                Write-Host "Could not delete $($_.Name) (file may be locked)" -ForegroundColor Yellow
            }
        }
    }
    
    # Restore all appsettings files if we backed them up
    foreach ($backup in $appsettingsBackups) {
        if (Test-Path $backup.Backup) {
            try {
                Copy-Item $backup.Backup $backup.Original -Force
                Remove-Item $backup.Backup -Force
                Write-Host "Restored $($backup.Name)" -ForegroundColor Green
            } catch {
                Write-Host "Could not restore $($backup.Name): $_" -ForegroundColor Yellow
            }
        }
    }
    
    # Restore all .env files if we backed them up
    foreach ($backup in $envBackups) {
        if (Test-Path $backup.Backup) {
            try {
                Copy-Item $backup.Backup $backup.Original -Force
                Remove-Item $backup.Backup -Force
                Write-Host "Restored $($backup.Name)" -ForegroundColor Green
            } catch {
                Write-Host "Could not restore $($backup.Name): $_" -ForegroundColor Yellow
            }
        }
    }
    
    $appsettingsCount = $appsettingsFiles.Count
    $envCount = $envFiles.Count
    $totalPreserved = $appsettingsCount + $envCount
    
    $preservedText = @()
    if ($appsettingsCount -gt 0) {
        $appsettingsText = if ($appsettingsCount -eq 1) { "1 appsettings file" } else { "$appsettingsCount appsettings files" }
        $preservedText += $appsettingsText
    }
    if ($envCount -gt 0) {
        $envText = if ($envCount -eq 1) { "1 .env file" } else { "$envCount .env files" }
        $preservedText += $envText
    }
    
    $preservedMessage = if ($preservedText.Count -gt 0) { 
        "preserved " + ($preservedText -join " and ") 
    } else { 
        "no special files to preserve" 
    }
    
    Write-Host "Cleaned publish directory ($preservedMessage)" -ForegroundColor Green
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
