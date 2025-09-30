#!/usr/bin/env pwsh

Write-Host "Updating OnboardingBuddy Database with Fixed Content" -ForegroundColor Green
Write-Host "=================================================" -ForegroundColor Green
Write-Host ""

# Ensure we're in the OnboardingBuddy project directory
if (-not (Test-Path "OnboardingBuddy.csproj")) {
    Write-Error "OnboardingBuddy.csproj not found. Please run this script from the OnboardingBuddy project directory."
    exit 1
}

Write-Host "Creating and applying database migration..." -ForegroundColor Yellow

# Create a new migration for the updated content
try {
    dotnet ef migrations add UpdateTrainingContent
    if ($LASTEXITCODE -ne 0) {
        Write-Host "? Migration already exists or no changes detected" -ForegroundColor Yellow
    } else {
        Write-Host "? Migration created" -ForegroundColor Green
    }
} catch {
    Write-Host "? Migration creation had issues, continuing..." -ForegroundColor Yellow
}

# Apply migrations to update the database
Write-Host "Applying database updates..." -ForegroundColor Yellow
dotnet ef database update

if ($LASTEXITCODE -eq 0) {
    Write-Host "? Database updated successfully" -ForegroundColor Green
} else {
    Write-Host "? Database update failed" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "?? Database Update Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Changes Applied:" -ForegroundColor Yellow
Write-Host "• Simplified AI system prompts to prevent content limit errors" -ForegroundColor White
Write-Host "• Updated default welcome message to be more engaging" -ForegroundColor White
Write-Host "• Improved training materials for better user experience" -ForegroundColor White
Write-Host ""
Write-Host "? The 'Too Much Content' issue should now be resolved!" -ForegroundColor Green
Write-Host ""
Write-Host "Next Steps:" -ForegroundColor Yellow
Write-Host "1. Deploy the updated application: .\deploy.ps1" -ForegroundColor White
Write-Host "2. Test the chat functionality" -ForegroundColor White
Write-Host "3. Verify the welcome message appears correctly" -ForegroundColor White