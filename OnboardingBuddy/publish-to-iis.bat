@echo off
echo Publishing OnboardingBuddy to IIS...
echo.

powershell -ExecutionPolicy Bypass -File "publish-to-iis.ps1"

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================
    echo Publish completed successfully!
    echo ========================================
    echo.
    echo The application has been published to:
    echo C:\Sites\OnboardingBuddy
    echo.
    echo You can now access it through your IIS virtual application.
) else (
    echo.
    echo ========================================
    echo Publish failed!
    echo ========================================
    echo Please check the error messages above.
)

pause