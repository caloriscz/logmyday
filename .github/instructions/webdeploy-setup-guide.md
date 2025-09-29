# Web Deploy Setup and Configuration Guide

This guide explains how to set up the new Web Deploy system for faster deployments.

## Overview

The new build system replaces FTP deployment with Web Deploy (MSDeploy), which provides:
- ⚡ **Much faster deployments** (only changed files are transferred)
- 🔄 **Atomic deployments** (all-or-nothing, no partial states)
- 🛡️ **Built-in retry logic** and error handling
- 📊 **Better progress reporting** and logging

## Setup Steps

### 1. Configure GitHub Secrets

You need to add the following secrets to your GitHub repository:

1. Go to your repository on GitHub
2. Click **Settings** → **Secrets and variables** → **Actions**
3. Click **New repository secret** and add each of these:

| Secret Name | Description | Example Value |
|-------------|-------------|---------------|
| `LMD_SERVER` | Web Deploy server hostname/IP | `your-server` |
| `LMD_PORT` | Web Deploy port (usually 8172) | `port` |
| `LMD_SITE` | Target site name | `your-site` |
| `LMD_LOGIN` | Web Deploy username | `sour-login` |
| `LMD_PASSWORD` | Web Deploy password | `your-password` |

### 2. Verify Server Configuration

Ensure your hosting provider supports Web Deploy:
- Web Deploy (MSDeploy) service is installed
- Management Service is running
- Firewall allows connections on the Web Deploy port (usually 8172)
- Your account has deployment permissions

### 3. Test the Connection

You can test Web Deploy connectivity manually:
```powershell
# Test connection (replace with your actual values)
msdeploy -verb:dump -source:contentPath=C:\temp -dest:contentPath=your-site,wmsvc=your-server:8172,userName=your-username,password=your-password
```

## Deployment Workflow

### Automatic Deployment
- **Trigger**: Push to `main` branch (excluding markdown files and mobile app changes)
- **Target**: Full deployment with tests (`Default` target)

### Manual Deployment
You can trigger deployments manually with different targets:

1. Go to **Actions** tab in GitHub
2. Select **Deploy Web Application** workflow
3. Click **Run workflow**
4. Choose target:
   - **Default**: Full pipeline with tests (recommended for production)
   - **FastDeploy**: Quick deployment without tests (for urgent fixes)
   - **CI**: Build and test only, no deployment

## Build Targets

### Available Targets

| Target | Description | Steps | Use Case |
|--------|-------------|-------|----------|
| `Default` | Full production deployment | Clean → Restore → Build → Test → Publish → Package → Deploy | Production releases |
| `FastDeploy` | Quick deployment | Clean → Restore → Build → Publish → Package → Deploy | Urgent hotfixes |
| `CI` | Continuous integration | Clean → Restore → Build → Test → Package | Testing changes |

### Local Testing

You can test the build locally (requires secrets in environment):

```powershell
cd .build
.\build.ps1 -Target CI  # Test without deployment
.\build.ps1 -Target Default  # Full deployment
```

## Troubleshooting

### Common Issues

#### 1. "Could not connect to remote server"
- **Cause**: Server unreachable or Web Deploy service not running
- **Solution**: 
  - Verify server hostname/IP is correct
  - Check if port is accessible: `Test-NetConnection your-server -Port 8172`
  - Contact hosting provider to ensure Web Deploy service is running

#### 2. "Authentication failed"
- **Cause**: Incorrect username/password or insufficient permissions
- **Solution**:
  - Verify `LMD_LOGIN` and `LMD_PASSWORD` secrets are correct
  - Ensure account has Web Deploy permissions
  - Try logging into your hosting control panel with same credentials

#### 3. "Site not found"
- **Cause**: Incorrect site name
- **Solution**:
  - Verify `LMD_SITE` matches exactly what your hosting provider configured
  - Check hosting control panel for correct site name

#### 4. "Build failed"
- **Cause**: Code compilation errors or test failures
- **Solution**:
  - Check the GitHub Actions logs for specific errors
  - Run `dotnet build` locally to identify issues
  - Use `FastDeploy` target to skip tests if needed (not recommended for production)

### Debug Information

When deployment fails, the build script provides:
- ✅ **Detailed error messages** with specific failure reasons
- 📋 **Troubleshooting tips** based on the error type
- 📦 **Build artifacts** uploaded for review
- 🔍 **Verbose logging** for diagnostic purposes

### Getting Help

If you encounter issues:
1. Check the **Actions** tab for detailed logs
2. Download and review the **build-logs** artifact
3. Verify all GitHub secrets are correctly set
4. Contact your hosting provider for Web Deploy support

## Migration Benefits

### Before (FTP)
- ⏱️ Long deployment times (all files transferred)
- 🚨 Risk of partial deployments during failures
- 🔧 Limited error handling and retry logic
- 📊 Poor progress visibility

### After (Web Deploy)
- ⚡ Fast deployments (incremental, only changed files)
- 🛡️ Atomic deployments (all-or-nothing)
- 🔄 Built-in retry logic and error handling
- 📊 Detailed progress reporting and logging
- 🎯 Better integration with IIS and hosting providers

## Performance Comparison

Typical deployment time improvements:
- **Initial deployment**: Similar time (all files need to be transferred)
- **Incremental deployments**: **70-90% faster** (only changed files)
- **Large applications**: Even greater time savings
- **Network reliability**: Better handling of connection issues

This new system should significantly reduce deployment times and provide a much more reliable deployment experience.