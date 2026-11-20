# LogMyDay Build System

This directory holds the Cake build that tests, publishes and deploys the web app to the hosting with Web Deploy. Deployment runs **locally only**, from a machine that has the production configuration.

## Files

- **build.cake**: the build script, with the test gate and the Web Deploy deployment
- **build.ps1**: PowerShell bootstrapper for Windows
- **build.sh**: Bash bootstrapper for Linux/macOS
- **local.env**: deployment credentials (gitignored; see `local.env.example`)
- **tools/packages.config**: NuGet packages for the Cake build system

## ⚠️ Deploy only from a working copy with the production appsettings

Web Deploy **syncs** the publish folder to the site: files that are missing from the publish output are **deleted on the server**. `src/LogMyDay.App/appsettings.json` is gitignored, so it is in the publish output only on a machine that has it. Deploying without it removes the server's `appsettings.json`, and the app then fails to start (HTTP 500.30).

For this reason there is no GitHub Actions deployment: the former tag-triggered workflow built without the appsettings and took the site down twice (v0.7.0, v0.7.1). Only the `DataProtection-Keys` folder is protected from the sync.

## Targets

### 🛡️ Deployment targets

#### `Default` - full deployment

```powershell
.\build\build.ps1
```

- Pipeline: Clean → Restore → Build → **Test** → Build Tailwind → Publish → Package → Deploy
- Deployment is blocked if any test fails.

#### `FastDeploy` - deployment without packaging (recommended)

```powershell
.\build\build.ps1 -Target FastDeploy
```

- Pipeline: Clean → Restore → Build → **Test** → Publish → Deploy
- Runs the same tests as `Default` and skips the zip package.

### Build-only targets

#### `CI` - build, test and package, no deployment

```powershell
.\build\build.ps1 -Target CI
```

- Pipeline: Clean → Restore → Build → **Test** → Build Tailwind → Publish → Package

### ⚠️ Emergency-only target

#### `DeployUnsafe` - deployment WITHOUT tests

```powershell
.\build\build.ps1 -Target DeployUnsafe
```

- Pipeline: Clean → Restore → Build → Publish → Deploy
- ❌ Bypasses all tests. Use only for a true emergency hotfix and verify the site by hand afterwards.

### Individual tasks

- `Clean`: clean the build output and temporary directories
- `Restore`: restore NuGet packages
- `Build`: build the solution and the integration tests
- `Test`: run the unit tests (`LogMyDay.Api.Tests`) and the integration tests (`LogMyDay.Api.IntegrationTests`)
- `BuildTailwind`: build the Tailwind CSS
- `Publish`: publish `LogMyDay.App` to `build/publish`
- `Package`: zip the publish output
- `ValidateDeploymentConfig`: check that the deployment variables are set
- `Deploy`: sync the publish output to the site with Web Deploy

## Test protection

Every deployment target except `DeployUnsafe` runs the full test suite first. If a test fails, the build stops with `TESTS FAILED - DEPLOYMENT ABORTED` and nothing is deployed.

## Deployment variables

The build reads these from `build/local.env` (gitignored) or from the environment:

```bash
LMD_SERVER=your-server-address.com
LMD_PORT=8172
LMD_SITE=your-site-name
LMD_LOGIN=your-username
LMD_PASSWORD=your-password
```

- `LMD_SERVER`: Web Deploy server hostname
- `LMD_PORT`: Web Deploy port (default 8172)
- `LMD_SITE`: target site name
- `LMD_LOGIN`: Web Deploy username
- `LMD_PASSWORD`: Web Deploy password

## Web Deploy

- Only changed files are transferred.
- The `AppOffline` rule takes the app offline during the deployment.
- The `DataProtection-Keys` folder on the server is skipped, so its keys survive every deployment.

## Troubleshooting

### Deployment fails

Check that:
1. Web Deploy is installed on the target server and its Management Service is running
2. The firewall allows the Web Deploy port (usually 8172)
3. The deployment credentials have sufficient permissions
4. The target site exists and is configured
5. `msdeploy.exe` is installed locally (the script looks in the usual Web Deploy folders and in PATH)

### The site returns HTTP 500.30 after a deployment

The app failed to start. The usual cause is a missing `appsettings.json` on the server (see the warning above). Deploy again from a working copy that has the production `src/LogMyDay.App/appsettings.json`.

### Tests fail

1. Run them locally: `dotnet test src/LogMyDay.Api.Tests` and `dotnet test src/LogMyDay.Api.IntegrationTests`
2. Fix the failing tests
3. Commit and deploy again

Don't use `DeployUnsafe` to get around failing tests unless it is a true emergency.
