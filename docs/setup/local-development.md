---
layout: default
title: Local Development
parent: Setup and Installation
nav_order: 1
---

# Local Development

Follow these steps to run the LogMyDay web stack on a workstation. The commands assume Windows PowerShell, but the tooling works the same on macOS and Linux.

## Prerequisites

- .NET SDK 9.0 (`dotnet --list-sdks` should list 9.x)
- Node.js 20 LTS and npm 10+
- Android SDK + .NET MAUI workloads if you plan to build the mobile client (`dotnet workload install maui`)

## Clone the repository

```powershell
git clone https://github.com/caloriscz/logmyday.git
cd logmyday
```

## Restore .NET dependencies

```powershell
dotnet restore src/LogMyDay.sln
```

The solution filter `LogMyDay.Web.slnf` is available when you only want the web-focused projects.

## Configure application settings

LogMyDay requires configuration files to be set up before running the application. The project uses `.dist` (distribution) template files that must be copied and populated with your actual configuration values.

### 1. Create Configuration Files

Navigate to the App project directory and copy the template files:

```powershell
cd src/LogMyDay.App
Copy-Item appsettings.json.dist appsettings.json
Copy-Item appsettings.Development.json.dist appsettings.Development.json
```

> ⚠️ **Security Note**: Never commit files containing real passwords. The `.gitignore` file already excludes `appsettings.json` and `appsettings.Development.json`.

### 2. Configure Database

LogMyDay stores its data in a single SQLite file; no database server is required. Point the connection string at the file in `appsettings.Development.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=logmyday.db"
  }
}
```

That's it — the application creates the database file and applies the migrations on startup.

Where the file lives:

- A relative path counts from the application's folder (here `src/LogMyDay.App`), on every host. An absolute path is used as it is.
- Without a connection string the file is `data/logmyday.db` in the application's folder.
- The startup log names the file in use: `Database: SQLite, <full path>`.
- On a hosted installation, prefer an absolute path to a folder outside the site, so that a deployment that syncs the site's folder cannot delete the database.

> SQL Server is no longer supported. To move an existing SQL Server database, export it with the SQLite export of a version that still supports SQL Server (Backup page, **Export Whole Database**) and point `DefaultConnection` at the exported file.

### 3. Configure API Base Address

The Blazor Server application needs to know where to find the API endpoints. For local development with the combined host:

```json
{
  "Api": {
    "BaseAddress": "https://localhost:7064"
  }
}
```

> 📌 **Important**: The URL should **not** end with `/api/` as it is automatically appended by the client. The default port for local development is usually `7064` (check your `launchSettings.json` if unsure).

### 4. Configure Email Settings

LogMyDay sends password reset emails. Configure your SMTP server settings in `appsettings.json` (or `appsettings.Development.json`).

Example for Gmail:

```json
{
  "Email": {
    "SmtpServer": "smtp.gmail.com",
    "SmtpPort": 587,
    "UseSsl": true,
    "UserName": "your-email@gmail.com",
    "Password": "your-app-password",
    "SenderEmail": "noreply@yourdomain.com",
    "SenderName": "LogMyDay"
  }
}
```

| Parameter | Description |
|-----------|-------------|
| `SmtpServer` | SMTP server hostname (e.g., `smtp.gmail.com`) |
| `SmtpPort` | SMTP server port (usually `587` for TLS) |
| `UseSsl` | Enable SSL/TLS encryption (`true`) |
| `UserName` | SMTP authentication username |
| `Password` | SMTP authentication password (use App Passwords for Gmail) |
| `SenderEmail` | Email address shown as sender |
| `PasswordResetUrl` | *(Optional)* Override the password reset link base URL. Defaults to the incoming request's scheme and host. Useful when running behind a reverse proxy that does not forward headers. |

## Install UI dependencies

The Tailwind + Vite workspace lives in `src/ui/`.

```powershell
cd src/ui
npm install
npm run build
cd ../..
```

- `npm run build` produces static assets in `src/ui/dist/`.
- Use `npm run dev` for watch mode while you work on Tailwind styles.

## Run the Blazor Server + API host

The `LogMyDay.App` project hosts both the UI and the ASP.NET Core API.

```powershell
dotnet run --project src/LogMyDay.App/LogMyDay.App.csproj
```

Open `https://localhost:7064` after the server starts. The API endpoints are served from the same host; use `/swagger` for the OpenAPI surface in development.

> ⚠️ **Security Warning**: The default administrator account is `admin` with the password `secret123`. You **must** change this password immediately after your first login.

Remember to stop the process (`Ctrl+C` or `Stop-Process -Name dotnet`) at the end of every session to avoid file locking.

## Run the standalone API (optional)

If you need to debug the API separately, run the dedicated project.

```powershell
dotnet run --project src/LogMyDay.Api/LogMyDay.Api.csproj
```

Set the Blazor Server app to point at the API base address via `appsettings.Development.json` when you split them.

## Build and Deploy with Cake

LogMyDay uses [Cake (C# Make)](https://cakebuild.net/) for automated builds and deployments. The build script is located at `build/build.cake`.

### Configure Deployment Credentials

Before running deployment tasks, create a local environment file with your deployment credentials:

```powershell
cd build
Copy-Item local.env.example local.env
```

Edit `build/local.env` with your actual values:

```
LMD_SERVER=your-server-address.com
LMD_PORT=8172
LMD_SITE=your-site-name
LMD_LOGIN=your-deployment-username
LMD_PASSWORD=your-deployment-password
```

> ⚠️ **Security**: The `local.env` file is gitignored. Never commit credentials to version control.

### Common Build Tasks

```powershell
# Build only (no deployment)
dotnet cake build/build.cake --target=Build

# Build and run tests
dotnet cake build/build.cake --target=Test

# Full deployment (Clean, Build, Test, Publish, Package, Deploy)
dotnet cake build/build.cake

# Fast deployment (runs tests, skips packaging)
dotnet cake build/build.cake --target=FastDeploy

# Unsafe deployment (skips tests - use with caution)
dotnet cake build/build.cake --target=DeployUnsafe
```

### CI/CD Environments

For GitHub Actions or other CI/CD pipelines, configure the deployment variables as repository secrets instead of using `local.env`:

- `LMD_SERVER`
- `LMD_PORT`
- `LMD_SITE`
- `LMD_LOGIN`
- `LMD_PASSWORD`