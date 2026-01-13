# LogMyDay

LogMyDay helps you capture the story of your day. Use it to record habits, wellness routines, chores, workouts, and the small highlights that make every day different. A web dashboard and Android companion app stay in sync so you can log entries wherever you are.

> [!WARNING]
> LogMyDay is a personal / experimental project and is not production-ready. Features and behavior may change, and occasional bugs or inconsistencies can occur. Use at your own discretion.

## What You Can Track

- Daily routines, workouts, chores, study blocks, or any recurring activity you want to monitor
- Contextual details such as duration, intensity, location, or personal notes using customizable tags
- Obligations you never want to skip—LogMyDay reminds you when a required item is still empty
- One-off events and quick wins captured in seconds from the mobile app

## Feature Highlights

- **Frictionless logging**: Choose from reminders on the Home page, a versatile add-activity modal, or one-tap quick buttons on Android. Every option keeps the selected day and tag in view so you can stay in the moment.
- **Powerful tagging**: Create the vocabulary that matches your life. Mix text, numbers, booleans, dates, times, ranges, option lists, and measurement units to give each entry the right structure.
- **Calendar intelligence**: Review your history in daily lists, month grids, or scrolling timelines. Jump by day, week, or month, and spot gaps that may need filling.
- **Wellness tools**: Guided breathing, HIIT timers, and other helpers walk you through routines and can write the result straight into your log.
- **Gentle reminders**: Required tags surface as prompts on the dashboard, while Android notifications nudge you when something slips through the cracks.
- **Data you own**: Export an entire backup, move between servers, or self-host in Docker without losing any detail.

## Platforms

- **Web dashboard**: A responsive interface for deep-dive reviews, calendar navigation, tag management, and administration.
- **Android companion app**: Optimised for touch with native pickers, offline-friendly quick buttons, and opt-in system notifications.
- **Shared experience**: Sign in on both platforms to keep your activity stream, tags, and preferences in sync.

## Why LogMyDay?

- Capture both routine tasks and meaningful highlights without juggling multiple tools
- Encourage consistency with reminders, required fields, and smart defaults
- Explore trends visually to see how habits evolve over weeks and months
- Stay in control of your information with private hosting and full data exports

## For Administrators

- Invite and manage multiple users with role-based permissions
- Configure culture, time zone, and localisation preferences per user
- Tune notification schedules, quiet hours, and reminder intervals

## For Self-Hosted Deployments

- Deploy the full stack on Windows, Linux, or in containers
- Use SQL Server for storage with HTTPS enforced throughout the stack

## Migration

Run from the root of your solution (or in LogMyDay.App folder):

```
dotnet ef migrations add InitialCreate --project LogMyDay.Api --startup-project LogMyDay.App --output-dir Infrastructure/Data/Migrations
```

Then apply the migration:

```
dotnet ef database update --project LogMyDay.Api --startup-project LogMyDay.App
```

## Generate migration script

```
dotnet ef migrations script --project LogMyDay.Api --startup-project LogMyDay.App --output InitialCreate.sql
```

## Installation

Follow these steps to run the LogMyDay web stack on a workstation. The commands assume Windows PowerShell, but the tooling works the same on macOS and Linux.

## Prerequisites

- .NET SDK 9.0 (`dotnet --list-sdks` should list 9.x)
- Node.js 20 LTS and npm 10+
- Android SDK + .NET MAUI workloads if you plan to build the mobile client (`dotnet workload install maui`)
- A SQL Server instance (local or remote)

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

### 2. Configure Database Connection

Edit `appsettings.Development.json` to set up the SQL Server connection string for local development.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost,1439;Database=logmyday;User Id=sa;Password=YOUR_DEV_PASSWORD;Encrypt=True;TrustServerCertificate=True;"
  }
}
```

| Parameter | Description | Example |
|-----------|-------------|---------|
| `Server` | SQL Server hostname and port | `localhost,1439` |
| `Database` | Database name | `logmyday` |
| `User Id` | SQL Server username | `sa` |
| `Password` | SQL Server password | `MySecurePassword123!` |
| `TrustServerCertificate` | Skip certificate validation | `True` (required for localhost dev) |

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
    "SenderName": "LogMyDay",
    "PasswordResetUrl": "https://localhost:7064/reset-password"
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
| `PasswordResetUrl` | URL for password reset (e.g., `https://localhost:7064/reset-password`). Must match your app's base URL. |

## Apply Database Migrations

Initialize the database schema by applying the Entity Framework Core migrations. This step creates the necessary tables (like `LogMyDay_Units`) and seeds initial data.

```powershell
# Install the EF Core tool if you haven't already
dotnet tool install --global dotnet-ef

# Apply migrations
dotnet ef database update --project src/LogMyDay.Api --startup-project src/LogMyDay.App
```

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

## Build with Cake

The project uses [Cake (C# Make)](https://cakebuild.net/) for a reliable, cross-platform build process. The build script is located at `build/build.cake`.

To run the default build (Clean + Restore + Build + Test + Publish):

```powershell
./build/build.ps1
```

