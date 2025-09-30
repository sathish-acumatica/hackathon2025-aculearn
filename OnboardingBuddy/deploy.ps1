#!/usr/bin/env pwsh

Write-Host "OnboardingBuddy - One-Click Publish to IIS" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host ""

$publishPath = "C:\Sites\OnboardingBuddy"

# Ensure we're in the OnboardingBuddy project directory
if (-not (Test-Path "OnboardingBuddy.csproj")) {
    Write-Error "OnboardingBuddy.csproj not found. Please run this script from the OnboardingBuddy project directory."
    exit 1
}

# Stop OnboardingBuddy app pool
Write-Host "Stopping OnboardingBuddy app pool..." -ForegroundColor Yellow
try {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    if (Get-Module WebAdministration) {
        Stop-WebAppPool -Name "OnboardingBuddy" -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Host "? App pool stopped" -ForegroundColor Green
    }
} catch {
    Write-Host "? Could not stop app pool (may not be running)" -ForegroundColor Yellow
}

# Clean and rebuild Vue.js frontend to ensure proper relative paths
Write-Host ""
Write-Host "Building Vue.js frontend with relative paths..." -ForegroundColor Yellow
Set-Location "ClientApp"

# Clean previous build
if (Test-Path "dist") {
    Remove-Item -Path "dist" -Recurse -Force
    Write-Host "? Cleaned previous build" -ForegroundColor Green
}

# Install dependencies
npm install
if ($LASTEXITCODE -ne 0) {
    Write-Error "? Failed to install npm dependencies"
    exit 1
}

# Build with Vite configuration that ensures relative paths
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "? Failed to build Vue.js application"
    exit 1
}

# Verify that relative paths were generated correctly
$indexPath = "dist\index.html"
if (Test-Path $indexPath) {
    $content = Get-Content $indexPath -Raw
    
    # Check for absolute paths (bad)
    if ($content -match 'src="/[^.]' -or $content -match 'href="/[^.]') {
        Write-Host "? ERROR: Still generating absolute paths!" -ForegroundColor Red
        exit 1
    }
    
    # Check for relative paths (good)
    if ($content -match 'src="[^/]' -or $content -match 'href="[^/]') {
        Write-Host "? Vue.js built with relative paths!" -ForegroundColor Green
    } else {
        Write-Host "? No asset paths found in HTML" -ForegroundColor Yellow
    }
} else {
    Write-Error "? Vue.js build failed - index.html not found"
    exit 1
}

Set-Location ".."

# Publish .NET application
Write-Host ""
Write-Host "Publishing .NET application..." -ForegroundColor Yellow
dotnet publish "OnboardingBuddy.csproj" `
    --configuration Release `
    --output $publishPath `
    --framework net9.0 `
    --runtime win-x64 `
    --self-contained false `
    --verbosity minimal

if ($LASTEXITCODE -ne 0) {
    Write-Error "? Failed to publish .NET application"
    exit 1
}

Write-Host "? .NET application published" -ForegroundColor Green

# Create necessary directories
Write-Host ""
Write-Host "Setting up directories..." -ForegroundColor Yellow
$directories = @("logs", "uploads", "wwwroot", "wwwroot\assets")
foreach ($dir in $directories) {
    $dirPath = Join-Path $publishPath $dir
    if (-not (Test-Path $dirPath)) {
        New-Item -ItemType Directory -Path $dirPath -Force | Out-Null
    }
}

# Copy Vue.js files to wwwroot
$distPath = "ClientApp\dist"
$wwwrootPath = Join-Path $publishPath "wwwroot"

Write-Host "Copying frontend files to wwwroot..." -ForegroundColor Yellow
if (Test-Path $distPath) {
    Get-ChildItem -Path $distPath -Recurse -File | ForEach-Object {
        $relativePath = $_.FullName.Substring((Resolve-Path $distPath).Path.Length + 1)
        $destinationPath = Join-Path $wwwrootPath $relativePath
        $destinationDir = Split-Path $destinationPath -Parent
        
        if (-not (Test-Path $destinationDir)) {
            New-Item -ItemType Directory -Path $destinationDir -Force | Out-Null
        }
        
        Copy-Item $_.FullName $destinationPath -Force
    }
    Write-Host "? Frontend files copied" -ForegroundColor Green
} else {
    Write-Error "? Vue.js dist directory not found"
    exit 1
}

# Set permissions
Write-Host ""
Write-Host "Setting permissions..." -ForegroundColor Yellow
try {
    $acl = Get-Acl $publishPath
    
    # OnboardingBuddy app pool
    $appPoolRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\OnboardingBuddy", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($appPoolRule)
    
    # IIS users
    $iisUsersRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($iisUsersRule)
    
    # IUSR
    $iusrRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IUSR", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($iusrRule)
    
    Set-Acl $publishPath $acl
    Write-Host "? Permissions set" -ForegroundColor Green
} catch {
    Write-Host "? Could not set permissions: $_" -ForegroundColor Yellow
}

# Start OnboardingBuddy app pool
Write-Host ""
Write-Host "Starting OnboardingBuddy app pool..." -ForegroundColor Yellow
try {
    if (Get-Module WebAdministration) {
        Start-WebAppPool -Name "OnboardingBuddy" -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Host "? App pool started" -ForegroundColor Green
    }
} catch {
    Write-Host "? Could not start app pool" -ForegroundColor Yellow
}

# Final verification
Write-Host ""
Write-Host "Verifying deployment..." -ForegroundColor Yellow
$indexHtml = Join-Path $wwwrootPath "index.html"
$assetsDir = Join-Path $wwwrootPath "assets"

if (Test-Path $indexHtml) {
    Write-Host "? index.html deployed" -ForegroundColor Green
} else {
    Write-Host "? index.html missing" -ForegroundColor Red
}

if (Test-Path $assetsDir) {
    $assetCount = (Get-ChildItem $assetsDir -File).Count
    Write-Host "? $assetCount asset files deployed" -ForegroundColor Green
} else {
    Write-Host "? Assets directory missing" -ForegroundColor Red
}

Write-Host ""
Write-Host "?? DEPLOYMENT COMPLETE!" -ForegroundColor Green
Write-Host ""
Write-Host "Application URL: http://localhost/OnboardingBuddy" -ForegroundColor Cyan
Write-Host "Files location: $publishPath" -ForegroundColor Cyan
Write-Host ""
Write-Host "? Static files should now load with relative paths (no leading slash)" -ForegroundColor Green
Write-Host ""
Write-Host "If you still get 404 errors, check that:" -ForegroundColor Yellow
Write-Host "• OnboardingBuddy virtual application is configured in IIS" -ForegroundColor White
Write-Host "• Application pool 'OnboardingBuddy' is set to 'No Managed Code'" -ForegroundColor White
Write-Host "• .NET 9.0 Runtime is installed" -ForegroundColor White