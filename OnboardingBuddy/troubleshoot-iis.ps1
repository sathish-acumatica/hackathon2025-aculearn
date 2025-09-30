#!/usr/bin/env pwsh

Write-Host "OnboardingBuddy IIS Virtual Application Troubleshooting Script" -ForegroundColor Green
Write-Host "=============================================================" -ForegroundColor Green
Write-Host ""

$sitePath = "C:\Sites\OnboardingBuddy"

# Check if the application path exists
Write-Host "1. Checking application path..." -ForegroundColor Yellow
if (Test-Path $sitePath) {
    Write-Host "? Application path exists: $sitePath" -ForegroundColor Green
} else {
    Write-Host "? Application path does not exist: $sitePath" -ForegroundColor Red
    Write-Host "Please run the publish script first!" -ForegroundColor Cyan
    exit 1
}

# Check for essential files
Write-Host ""
Write-Host "2. Checking essential files..." -ForegroundColor Yellow

$essentialFiles = @(
    "OnboardingBuddy.dll",
    "OnboardingBuddy.deps.json",
    "OnboardingBuddy.runtimeconfig.json",
    "web.config"
)

foreach ($file in $essentialFiles) {
    $filePath = Join-Path $sitePath $file
    if (Test-Path $filePath) {
        Write-Host "? $file found" -ForegroundColor Green
    } else {
        Write-Host "? $file missing" -ForegroundColor Red
    }
}

# Check .NET Runtime
Write-Host ""
Write-Host "3. Checking .NET Runtime..." -ForegroundColor Yellow
try {
    $dotnetInfo = dotnet --info 2>&1
    if ($LASTEXITCODE -eq 0) {
        Write-Host "? .NET is installed" -ForegroundColor Green
        
        # Check for .NET 9.0
        if ($dotnetInfo -match "9\.0") {
            Write-Host "? .NET 9.0 runtime found" -ForegroundColor Green
        } else {
            Write-Host "? .NET 9.0 runtime may not be installed" -ForegroundColor Yellow
            Write-Host "Please install .NET 9.0 Runtime from: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Cyan
        }
    }
} catch {
    Write-Host "? .NET runtime not found or not in PATH" -ForegroundColor Red
    Write-Host "Please install .NET 9.0 Runtime from: https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Cyan
}

# Check ASP.NET Core Module
Write-Host ""
Write-Host "4. Checking ASP.NET Core Module..." -ForegroundColor Yellow
$aspNetCoreModulePath = "C:\Windows\System32\inetsrv\aspnetcorev2.dll"
if (Test-Path $aspNetCoreModulePath) {
    Write-Host "? ASP.NET Core Module V2 is installed" -ForegroundColor Green
} else {
    Write-Host "? ASP.NET Core Module may not be installed" -ForegroundColor Yellow
    Write-Host "Install the ASP.NET Core Runtime & Hosting Bundle from:" -ForegroundColor Cyan
    Write-Host "https://dotnet.microsoft.com/download/dotnet/9.0" -ForegroundColor Cyan
}

# Check URL Rewrite Module
Write-Host ""
Write-Host "5. Checking URL Rewrite Module..." -ForegroundColor Yellow
$rewriteModulePath = "C:\Windows\System32\inetsrv\rewrite.dll"
if (Test-Path $rewriteModulePath) {
    Write-Host "? URL Rewrite Module is installed" -ForegroundColor Green
} else {
    Write-Host "? URL Rewrite Module is not installed" -ForegroundColor Yellow
    Write-Host "This is optional but recommended for better SPA routing" -ForegroundColor Cyan
    Write-Host "Download from: https://www.iis.net/downloads/microsoft/url-rewrite" -ForegroundColor Cyan
}

# Check OnboardingBuddy Application Pool
Write-Host ""
Write-Host "6. Checking OnboardingBuddy Application Pool..." -ForegroundColor Yellow
try {
    Import-Module WebAdministration -ErrorAction SilentlyContinue
    if (Get-Module WebAdministration) {
        $appPool = Get-WebAppPoolState -Name "OnboardingBuddy" -ErrorAction SilentlyContinue
        if ($appPool) {
            Write-Host "? OnboardingBuddy application pool exists" -ForegroundColor Green
            Write-Host "  State: $($appPool.Value)" -ForegroundColor Cyan
            
            # Check managed runtime version
            $poolConfig = Get-IISAppPool -Name "OnboardingBuddy" -ErrorAction SilentlyContinue
            if ($poolConfig -and $poolConfig.ManagedRuntimeVersion -eq "") {
                Write-Host "? Application pool set to 'No Managed Code'" -ForegroundColor Green
            } else {
                Write-Host "? Application pool should be set to 'No Managed Code'" -ForegroundColor Yellow
            }
        } else {
            Write-Host "? OnboardingBuddy application pool not found" -ForegroundColor Red
            Write-Host "Please create the OnboardingBuddy application pool in IIS" -ForegroundColor Cyan
        }
    } else {
        Write-Host "? Could not load WebAdministration module" -ForegroundColor Yellow
    }
} catch {
    Write-Host "? Could not check application pool: $_" -ForegroundColor Yellow
}

# Check permissions
Write-Host ""
Write-Host "7. Checking permissions..." -ForegroundColor Yellow
try {
    $acl = Get-Acl $sitePath
    
    # Check for OnboardingBuddy app pool identity
    $appPoolRule = $acl.Access | Where-Object { $_.IdentityReference -like "*OnboardingBuddy*" }
    if ($appPoolRule) {
        Write-Host "? OnboardingBuddy app pool has permissions on the directory" -ForegroundColor Green
    } else {
        Write-Host "? OnboardingBuddy app pool may not have proper permissions" -ForegroundColor Yellow
        Write-Host "Setting permissions for OnboardingBuddy app pool..." -ForegroundColor Cyan
        
        $accessRule = New-Object System.Security.AccessControl.FileSystemAccessRule("IIS AppPool\OnboardingBuddy", "FullControl", "ContainerInherit,ObjectInherit", "None", "Allow")
        $acl.SetAccessRule($accessRule)
        Set-Acl $sitePath $acl
        Write-Host "? Permissions set for IIS AppPool\OnboardingBuddy" -ForegroundColor Green
    }
    
    # Check for IIS_IUSRS
    $iisUsersRule = $acl.Access | Where-Object { $_.IdentityReference -like "*IIS_IUSRS*" }
    if ($iisUsersRule) {
        Write-Host "? IIS_IUSRS has permissions on the directory" -ForegroundColor Green
    } else {
        Write-Host "? IIS_IUSRS may not have proper permissions" -ForegroundColor Yellow
    }
} catch {
    Write-Host "? Could not check/set permissions: $_" -ForegroundColor Yellow
}

# Create logs directory if it doesn't exist
Write-Host ""
Write-Host "8. Setting up logging..." -ForegroundColor Yellow
$logsPath = Join-Path $sitePath "logs"
if (-not (Test-Path $logsPath)) {
    New-Item -ItemType Directory -Path $logsPath -Force | Out-Null
    Write-Host "? Created logs directory" -ForegroundColor Green
} else {
    Write-Host "? Logs directory exists" -ForegroundColor Green
}

# Check web.config syntax and rewrite rules
Write-Host ""
Write-Host "9. Validating web.config..." -ForegroundColor Yellow
$webConfigPath = Join-Path $sitePath "web.config"
if (Test-Path $webConfigPath) {
    try {
        [xml]$webConfigXml = Get-Content $webConfigPath
        Write-Host "? web.config is valid XML" -ForegroundColor Green
        
        # Check for rewrite rules
        $rewriteNode = $webConfigXml.SelectSingleNode("//rewrite")
        if ($rewriteNode) {
            if (Test-Path $rewriteModulePath) {
                Write-Host "? web.config contains rewrite rules and URL Rewrite Module is installed" -ForegroundColor Green
            } else {
                Write-Host "? web.config contains rewrite rules but URL Rewrite Module is not installed" -ForegroundColor Yellow
                Write-Host "  This will cause HTTP 500.19 errors!" -ForegroundColor Red
                Write-Host "  Solution: Install URL Rewrite Module or use basic web.config" -ForegroundColor Cyan
            }
        } else {
            Write-Host "? web.config is compatible (no rewrite rules)" -ForegroundColor Green
        }
        
        # Check for common virtual app issues
        $aspNetCoreNode = $webConfigXml.SelectSingleNode("//aspNetCore")
        if ($aspNetCoreNode) {
            $arguments = $aspNetCoreNode.GetAttribute("arguments")
            if ($arguments -like "*OnboardingBuddy.dll*") {
                Write-Host "? Correct DLL reference in web.config" -ForegroundColor Green
            } else {
                Write-Host "? Check DLL reference in web.config" -ForegroundColor Yellow
            }
        }
    } catch {
        Write-Host "? web.config has XML syntax errors: $_" -ForegroundColor Red
    }
} else {
    Write-Host "? web.config not found" -ForegroundColor Red
}

# Check wwwroot and static files
Write-Host ""
Write-Host "10. Checking static files..." -ForegroundColor Yellow
$wwwrootPath = Join-Path $sitePath "wwwroot"
if (Test-Path $wwwrootPath) {
    Write-Host "? wwwroot directory exists" -ForegroundColor Green
    
    $indexHtml = Join-Path $wwwrootPath "index.html"
    if (Test-Path $indexHtml) {
        Write-Host "? index.html found in wwwroot" -ForegroundColor Green
    } else {
        Write-Host "? index.html missing from wwwroot" -ForegroundColor Red
    }
    
    $assetsDir = Join-Path $wwwrootPath "assets"
    if (Test-Path $assetsDir) {
        $assetCount = (Get-ChildItem $assetsDir -File -ErrorAction SilentlyContinue).Count
        Write-Host "? Assets directory found with $assetCount files" -ForegroundColor Green
    } else {
        Write-Host "? Assets directory missing" -ForegroundColor Red
    }
} else {
    Write-Host "? wwwroot directory not found" -ForegroundColor Red
    Write-Host "The Vue.js build files may not have been copied correctly" -ForegroundColor Yellow
}

Write-Host ""
Write-Host "11. Virtual Application Configuration Check:" -ForegroundColor Yellow
Write-Host "     For virtual application 'OnboardingBuddy' under Default Web Site:" -ForegroundColor Cyan
Write-Host "     • Application Pool: OnboardingBuddy (No Managed Code)" -ForegroundColor Cyan
Write-Host "     • Physical Path: $sitePath" -ForegroundColor Cyan
Write-Host "     • URL: http://localhost/OnboardingBuddy" -ForegroundColor Cyan
Write-Host ""

Write-Host "12. Common Issues and Solutions:" -ForegroundColor Yellow
Write-Host "     • Error 500.19 (Config Error): Web.config syntax or missing modules" -ForegroundColor Cyan
Write-Host "       - Remove rewrite rules if URL Rewrite Module not installed" -ForegroundColor Gray
Write-Host "     • Error 502.5: Missing .NET Runtime or incorrect application pool" -ForegroundColor Cyan
Write-Host "     • Error 500.30: Startup failure - check application logs" -ForegroundColor Cyan
Write-Host "     • Static files not loading: Check wwwroot content and permissions" -ForegroundColor Cyan
Write-Host ""

Write-Host "13. Next Steps if still having issues:" -ForegroundColor Yellow
Write-Host "      1. Check OnboardingBuddy Application Pool is set to 'No Managed Code'" -ForegroundColor Cyan
Write-Host "      2. Verify virtual application path is '/OnboardingBuddy'" -ForegroundColor Cyan
Write-Host "      3. Check Event Viewer > Windows Logs > Application for errors" -ForegroundColor Cyan
Write-Host "      4. Check application logs at: $logsPath" -ForegroundColor Cyan
Write-Host "      5. Try browsing to: http://localhost/OnboardingBuddy" -ForegroundColor Cyan
Write-Host "      6. Run .\check-url-rewrite.ps1 for URL Rewrite Module guidance" -ForegroundColor Cyan
Write-Host ""

Write-Host "Troubleshooting complete!" -ForegroundColor Green