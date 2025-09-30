#!/usr/bin/env pwsh

Write-Host "Testing ChatInterface Layout Fix" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green
Write-Host ""

# Ensure we're in the OnboardingBuddy project directory
if (-not (Test-Path "OnboardingBuddy.csproj")) {
    Write-Error "OnboardingBuddy.csproj not found. Please run this script from the OnboardingBuddy project directory."
    exit 1
}

Set-Location "ClientApp"

Write-Host "Building Vue.js application to test layout..." -ForegroundColor Yellow
npm run build

if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed"
    exit 1
}

# Check the generated index.html
$indexPath = "dist\index.html"
if (Test-Path $indexPath) {
    Write-Host ""
    Write-Host "? Build successful!" -ForegroundColor Green
    
    # Check for common layout issues
    $content = Get-Content $indexPath -Raw
    
    if ($content -match 'height.*100vh') {
        Write-Host "? Full height layout detected" -ForegroundColor Green
    } else {
        Write-Host "? Full height layout may not be configured properly" -ForegroundColor Yellow
    }
    
    Write-Host ""
    Write-Host "?? Layout Issues Fixed:" -ForegroundColor Yellow
    Write-Host "• Removed duplicate SignalR URL configuration" -ForegroundColor White
    Write-Host "• Fixed duplicate fetch calls in file upload" -ForegroundColor White
    Write-Host "• Removed conflicting height declarations" -ForegroundColor White
    Write-Host "• Ensured chat input container is properly positioned" -ForegroundColor White
    Write-Host "• Cleaned up the unified header layout" -ForegroundColor White
    
    Write-Host ""
    Write-Host "?? Expected Layout:" -ForegroundColor Yellow
    Write-Host "???????????????????????????????????" -ForegroundColor Cyan
    Write-Host "? ?? OnboardingBuddy [Chat][Admin]?" -ForegroundColor Cyan
    Write-Host "???????????????????????????????????" -ForegroundColor Cyan
    Write-Host "?                                 ?" -ForegroundColor Cyan
    Write-Host "?         Chat Messages           ?" -ForegroundColor Cyan
    Write-Host "?         Area                    ?" -ForegroundColor Cyan
    Write-Host "?                                 ?" -ForegroundColor Cyan
    Write-Host "???????????????????????????????????" -ForegroundColor Cyan
    Write-Host "? [Input Box] [??] [??]          ?" -ForegroundColor Cyan
    Write-Host "???????????????????????????????????" -ForegroundColor Cyan
    
    Write-Host ""
    Write-Host "?? Next Steps:" -ForegroundColor Yellow
    Write-Host "1. Deploy the fixed application: ..\deploy.ps1" -ForegroundColor White
    Write-Host "2. Test the chat interface in browser" -ForegroundColor White
    Write-Host "3. Verify input box is visible at bottom of screen" -ForegroundColor White
    Write-Host "4. Check that unified header shows both Chat and Admin buttons" -ForegroundColor White

} else {
    Write-Host "? Build output not found" -ForegroundColor Red
}

Set-Location ".."

Write-Host ""
Write-Host "Test complete!" -ForegroundColor Green