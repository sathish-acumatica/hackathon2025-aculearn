#!/usr/bin/env pwsh

Write-Host "Testing Vue.js Build for Virtual Application Support" -ForegroundColor Green
Write-Host "==================================================" -ForegroundColor Green
Write-Host ""

# Ensure we're in the OnboardingBuddy project directory
if (-not (Test-Path "OnboardingBuddy.csproj")) {
    Write-Error "OnboardingBuddy.csproj not found. Please run this script from the OnboardingBuddy project directory."
    exit 1
}

Set-Location "ClientApp"

Write-Host "Building Vue.js application..." -ForegroundColor Yellow
npm run build

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Check the generated index.html
$indexPath = "dist\index.html"
if (Test-Path $indexPath) {
    Write-Host ""
    Write-Host "?? Generated index.html analysis:" -ForegroundColor Yellow
    Write-Host "================================" -ForegroundColor Yellow
    
    $content = Get-Content $indexPath -Raw
    
    # Check for base tag injection script
    if ($content -match 'window\.location\.pathname') {
        Write-Host "? Base tag injection script: FOUND" -ForegroundColor Green
    } else {
        Write-Host "? Base tag injection script: MISSING" -ForegroundColor Red
    }
    
    # Check for relative asset paths
    if ($content -match 'src="\.?/?assets/' -or $content -match 'href="\.?/?assets/') {
        Write-Host "? Relative asset paths: CORRECT" -ForegroundColor Green
    } else {
        Write-Host "? Relative asset paths: INCORRECT" -ForegroundColor Red
    }
    
    # Show the relevant lines
    Write-Host ""
    Write-Host "?? Asset references found:" -ForegroundColor Cyan
    $content.Split("`n") | Where-Object { $_ -match '(src="|href=").*\.(js|css)' } | ForEach-Object {
        $line = $_.Trim()
        if ($line -ne '') {
            if ($line -match '(src="|href=")\.?/?assets/') {
                Write-Host "  ? $line" -ForegroundColor Green
            } else {
                Write-Host "  ? $line" -ForegroundColor Red
            }
        }
    }
    
    # Show base tag script presence
    Write-Host ""
    Write-Host "?? Base tag injection script:" -ForegroundColor Cyan
    if ($content -match 'const path = window\.location\.pathname') {
        Write-Host "  ? Dynamic path detection script found" -ForegroundColor Green
    } else {
        Write-Host "  ? Dynamic path detection script missing" -ForegroundColor Red
    }
    
    Write-Host ""
    Write-Host "?? Summary:" -ForegroundColor Yellow
    if (($content -match 'window\.location\.pathname') -and ($content -match 'src="\.?/?assets/' -or $content -match 'href="\.?/?assets/')) {
        Write-Host "? Build is correctly configured for virtual application deployment!" -ForegroundColor Green
        Write-Host "   • Base tag will be set dynamically" -ForegroundColor White
        Write-Host "   • Assets use relative paths" -ForegroundColor White
        Write-Host "   • Should work in any subdirectory" -ForegroundColor White
    } else {
        Write-Host "? Build has issues that may prevent virtual application deployment" -ForegroundColor Red
    }
} else {
    Write-Host "? index.html not found after build" -ForegroundColor Red
}

Set-Location ".."

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Green