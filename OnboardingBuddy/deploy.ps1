#!/usr/bin/env pwsh

param(
    [Parameter(Mandatory=$false)]
    [string]$VirtualAppName = "OnboardingBuddy",
    
    [Parameter(Mandatory=$false)]
    [string]$PublishPath = "C:\Sites"
)

Write-Host "OnboardingBuddy - Flexible Virtual Application Deploy" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host "Virtual Application: $VirtualAppName" -ForegroundColor Cyan
Write-Host "Deploy Path: $PublishPath\$VirtualAppName" -ForegroundColor Cyan
Write-Host ""

$fullPublishPath = Join-Path $PublishPath $VirtualAppName

# Ensure we're in the OnboardingBuddy project directory
if (-not (Test-Path "OnboardingBuddy.csproj")) {
    Write-Error "OnboardingBuddy.csproj not found. Please run this script from the OnboardingBuddy project directory."
    exit 1
}

# Stop app pool
Write-Host "Stopping $VirtualAppName app pool..." -ForegroundColor Yellow
try {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    if (Get-Module WebAdministration) {
        Stop-WebAppPool -Name $VirtualAppName -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Write-Host "? App pool stopped" -ForegroundColor Green
    }
} catch {
    Write-Host "? Could not stop app pool (may not be running)" -ForegroundColor Yellow
}

# Build Vue.js frontend with dynamic path detection
Write-Host ""
Write-Host "Building Vue.js frontend with universal path support..." -ForegroundColor Yellow
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

# Build with dynamic configuration
npm run build
if ($LASTEXITCODE -ne 0) {
    Write-Error "? Failed to build Vue.js application"
    exit 1
}

# Verify the build and check for the base tag injection
$indexPath = "dist\index.html"
if (Test-Path $indexPath) {
    $content = Get-Content $indexPath -Raw
    
    # Check for our base tag injection script
    if ($content -match 'window\.location\.pathname') {
        Write-Host "? Vue.js built with dynamic base tag injection!" -ForegroundColor Green
        Write-Host "  Base tag will be set automatically based on deployment location" -ForegroundColor Cyan
    } else {
        Write-Host "? WARNING: Base tag injection script not found" -ForegroundColor Yellow
    }
    
    # Check for relative paths
    if ($content -match 'src="\.?/?assets/' -or $content -match 'href="\.?/?assets/') {
        Write-Host "? Assets use relative paths" -ForegroundColor Green
    } else {
        Write-Host "? WARNING: Assets may not use relative paths" -ForegroundColor Yellow
        $content.Split("`n") | Where-Object { $_ -match '(src="|href=")' } | Select-Object -First 3 | ForEach-Object {
            Write-Host "  $_" -ForegroundColor Yellow
        }
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
    --output $fullPublishPath `
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
    $dirPath = Join-Path $fullPublishPath $dir
    if (-not (Test-Path $dirPath)) {
        New-Item -ItemType Directory -Path $dirPath -Force | Out-Null
    }
}

# Copy Vue.js files to wwwroot
$distPath = "ClientApp\dist"
$wwwrootPath = Join-Path $fullPublishPath "wwwroot"

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
    
    # Verify the deployed HTML has the base tag script
    $deployedIndexPath = Join-Path $wwwrootPath "index.html"
    if (Test-Path $deployedIndexPath) {
        $deployedContent = Get-Content $deployedIndexPath -Raw
        if ($deployedContent -match 'window\.location\.pathname') {
            Write-Host "? Deployed HTML includes dynamic base tag script" -ForegroundColor Green
        } else {
            Write-Host "? WARNING: Deployed HTML missing base tag script" -ForegroundColor Yellow
        }
    }
} else {
    Write-Error "? Vue.js dist directory not found"
    exit 1
}

# Set permissions
Write-Host ""
Write-Host "Setting permissions..." -ForegroundColor Yellow
try {
    $acl = Get-Acl $fullPublishPath
    
    # App pool identity
    $appPoolRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\$VirtualAppName", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($appPoolRule)
    
    # IIS users
    $iisUsersRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS_IUSRS", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($iisUsersRule)
    
    # IUSR
    $iusrRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IUSR", "ReadAndExecute", "ContainerInherit,ObjectInherit", "None", "Allow")
    $acl.SetAccessRule($iusrRule)
    
    Set-Acl $fullPublishPath $acl
    Write-Host "? Permissions set for $VirtualAppName app pool" -ForegroundColor Green
} catch {
    Write-Host "? Could not set permissions: $_" -ForegroundColor Yellow
}

# Start app pool
Write-Host ""
Write-Host "Starting $VirtualAppName app pool..." -ForegroundColor Yellow
try {
    if (Get-Module WebAdministration) {
        Start-WebAppPool -Name $VirtualAppName -ErrorAction SilentlyContinue
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
Write-Host "?? Universal Path Support Applied:" -ForegroundColor Yellow
Write-Host "• Dynamic base tag injection for static files" -ForegroundColor White
Write-Host "• SignalR Hub URLs adapt to deployment location" -ForegroundColor White
Write-Host "• Chat API calls use correct virtual app path" -ForegroundColor White
Write-Host "• Admin Panel API calls now use correct paths" -ForegroundColor White
Write-Host "• Vue Router base path auto-detection" -ForegroundColor White
Write-Host ""
Write-Host "?? URL Examples for '$VirtualAppName' deployment:" -ForegroundColor Yellow
Write-Host "• Application: http://localhost/$VirtualAppName" -ForegroundColor White
Write-Host "• Admin Panel: http://localhost/$VirtualAppName/admin" -ForegroundColor White
Write-Host "• Training API: http://localhost/$VirtualAppName/api/trainingmaterials" -ForegroundColor White
Write-Host "• Chat Hub: http://localhost/$VirtualAppName/chatHub" -ForegroundColor White
Write-Host "• Static Assets: http://localhost/$VirtualAppName/assets/" -ForegroundColor White
Write-Host ""
Write-Host "Deployment Details:" -ForegroundColor Yellow
Write-Host "• Virtual Application: $VirtualAppName" -ForegroundColor White
Write-Host "• Physical Path: $fullPublishPath" -ForegroundColor White
Write-Host "• App Pool: $VirtualAppName (No Managed Code)" -ForegroundColor White
Write-Host ""
Write-Host "?? Testing Instructions:" -ForegroundColor Yellow
Write-Host "1. Navigate to http://localhost/$VirtualAppName" -ForegroundColor White
Write-Host "2. Check browser console for 'Base path set to: /$VirtualAppName/'" -ForegroundColor White
Write-Host "3. Test Chat functionality (SignalR)" -ForegroundColor White
Write-Host "4. Test Admin Panel at http://localhost/$VirtualAppName/admin" -ForegroundColor White
Write-Host "5. Verify training materials load correctly in Admin Panel" -ForegroundColor White
Write-Host ""
Write-Host "?? Flexible Deployment Examples:" -ForegroundColor Yellow
Write-Host "   .\deploy.ps1                                    # Deploy to OnboardingBuddy" -ForegroundColor White
Write-Host "   .\deploy.ps1 -VirtualAppName HR                 # Deploy to HR" -ForegroundColor White
Write-Host "   .\deploy.ps1 -VirtualAppName Training           # Deploy to Training" -ForegroundColor White
Write-Host "   .\deploy.ps1 -VirtualAppName MyApp -PublishPath D:\ # Deploy to D:\MyApp" -ForegroundColor White