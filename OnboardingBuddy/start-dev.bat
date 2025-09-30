@echo off
echo Starting OnboardingBuddy Development Environment...
echo.

echo Installing npm dependencies...
cd ClientApp
call npm install
if errorlevel 1 (
    echo Failed to install npm dependencies
    pause
    exit /b 1
)

echo.
echo Starting Vite development server...
start "Vite Dev Server" cmd /k "npm run dev"

echo.
echo Waiting for Vite server to start...
timeout /t 5 /nobreak > nul

echo.
echo You can now run the ASP.NET Core application from Visual Studio
echo The frontend will be available at: http://localhost:3000
echo The backend will be available at: http://localhost:5000
echo.
pause