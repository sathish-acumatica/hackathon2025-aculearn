# OnboardingBuddy Virtual Application Deployment Guide

## Overview
This guide helps you deploy OnboardingBuddy as a virtual application under IIS Default Web Site using the OnboardingBuddy application pool.

## Prerequisites
1. **IIS with ASP.NET Core Module V2** - Install the ASP.NET Core Runtime & Hosting Bundle
2. **.NET 9.0 Runtime** - Download from Microsoft
3. **Node.js** - Required for building the Vue.js frontend
4. **IIS URL Rewrite Module** - Optional but recommended for better SPA routing

## Quick Start

### Step 1: One-Click Deployment
```powershell
# Run from the OnboardingBuddy project directory
.\deploy.ps1
```

This single script will:
- ? Build Vue.js frontend with relative paths
- ? Publish .NET application to IIS
- ? Set up all necessary directories and permissions
- ? Validate the deployment
- ? Start/stop application pools automatically

### Step 2: Configure IIS Application Pool (if not done automatically)
1. Open **IIS Manager**
2. Go to **Application Pools**
3. Create/configure **OnboardingBuddy** application pool:
   - **.NET CLR Version**: **No Managed Code**
   - **Managed Pipeline Mode**: **Integrated**
   - **Identity**: **ApplicationPoolIdentity**

### Step 3: Configure IIS Virtual Application (if not exists)
1. In IIS Manager, expand **Default Web Site**
2. Right-click and select **Add Application**
3. Set:
   - **Alias**: `OnboardingBuddy`
   - **Physical Path**: `C:\Sites\OnboardingBuddy`
   - **Application Pool**: `OnboardingBuddy`

### Step 4: Test the Application
Navigate to: `http://localhost/OnboardingBuddy`

## Troubleshooting

### If you get any deployment errors
Run the troubleshooting script:
```powershell
.\troubleshoot-iis.ps1
```

### Common Issues

#### HTTP Error 500.19
- **Cause**: Missing ASP.NET Core Module or invalid web.config
- **Solution**: Install ASP.NET Core Runtime & Hosting Bundle

#### HTTP Error 502.5
- **Cause**: Missing .NET 9.0 Runtime or incorrect application pool
- **Solution**: Install .NET 9.0 Runtime and ensure pool is set to "No Managed Code"

#### Static files (CSS/JS) return 404 errors
- **Status**: ? **FIXED** - The deploy script now automatically generates relative paths
- **Solution**: Re-run `.\deploy.ps1` to rebuild with correct paths

## File Structure
After deployment, your directory will contain:
```
C:\Sites\OnboardingBuddy\
??? OnboardingBuddy.dll
??? OnboardingBuddy.deps.json
??? OnboardingBuddy.runtimeconfig.json
??? web.config
??? wwwroot\
?   ??? index.html (with relative paths)
?   ??? onboarding-buddy-icon.svg
?   ??? assets\
?       ??? index-*.js
?       ??? index-*.css
??? logs\
??? uploads\
??? [other application files]
```

## Project Scripts

### Primary Script (Use This One)
- **`deploy.ps1`** - Complete one-click deployment script

### Utility Scripts
- **`troubleshoot-iis.ps1`** - Diagnose deployment issues

### Essential Files
- **`web.config`** - IIS configuration
- **`DEPLOYMENT-GUIDE.md`** - This documentation
- **`ClientApp/vite.config.js`** - Frontend build configuration

## URLs
- **Application**: `http://localhost/OnboardingBuddy`
- **API**: `http://localhost/OnboardingBuddy/api/`
- **Chat Hub**: `http://localhost/OnboardingBuddy/chatHub`

## Security Notes
- Application runs under OnboardingBuddy application pool identity
- `IIS AppPool\OnboardingBuddy` has necessary permissions
- Static files use relative paths (no URL rewrite dependency)
- Logs written to `logs` directory
- File uploads stored in `uploads` directory

## Relative Path Solution
The application now uses **relative paths** for all static assets:
- ? **Before**: `/assets/index-3dClTRSZ.js` (broken in virtual apps)
- ? **After**: `assets/index-3dClTRSZ.js` (works in virtual apps)

This is achieved through:
1. **Vite configuration** with `base: './'`
2. **Automatic build process** in `deploy.ps1`
3. **Path validation** during deployment

## Updating the Application
```powershell
# Simply re-run the deployment script
.\deploy.ps1
```

## Support
If you encounter issues:
1. Run `.\troubleshoot-iis.ps1`
2. Check Windows Event Viewer > Application logs
3. Review application logs in the `logs` directory
4. Verify OnboardingBuddy application pool configuration

## Recent Improvements
- ? **Simplified deployment** - One script does everything
- ? **Automatic path fixing** - No manual intervention needed
- ? **Comprehensive validation** - Catches issues during deployment
- ? **Clean file structure** - Removed duplicate/unnecessary files