---
layout: default
title: Getting Started
parent: Mobile Guide
nav_order: 1
---

# Getting Started

Use these steps to prepare the MAUI application and connect it to a running LogMyDay server.

## Prerequisites

- .NET SDK 9.0 with MAUI workloads (`dotnet workload install maui`)
- Android SDK (API level 34 or newer) via Visual Studio or `sdkmanager`
- A running LogMyDay server (`dotnet run --project src/LogMyDay.App/LogMyDay.App.csproj` or Docker deployment)
- An Android emulator or physical device with developer mode enabled

## Configure the solution

1. Restore .NET dependencies if you have not already done so:
   ```powershell
   dotnet restore src/LogMyDay.sln
   ```
2. Ensure Tailwind assets are available for the embedded Blazor WebView:
   ```powershell
   cd src/ui
   npm install
   npm run build
   cd ../..
   ```
   The CopyTailwindAssets MSBuild target will push the generated CSS/JS into the mobile project on the next build.

## Build and deploy

```powershell
# Build in Release (default Device Target Framework is net9.0-android)
dotnet build src/LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj -c Debug

# Launch on the default device/emulator
cd src/LogMyDay.App.Mobile
dotnet build -t Run -f net9.0-android
```

Visual Studio users can open `LogMyDay.sln` and pick the `LogMyDay.App.Mobile` startup project with an Android emulator.

## First-run flow

1. The login screen prompts for the server URL, username, and password.
2. Enter the HTTPS endpoint of the server you launched earlier. The mobile client refuses plaintext HTTP endpoints.
3. Tap **Connect & Sign In**. The app will create a new Refit client using `ApiClientProvider`, probe the `/tags` endpoint, and store the session in memory.
4. Successful authentication takes you to the Home page with quick stats and the floating action button for new activities.

> 🔁 The password is never persisted. Username and server URL are stored in `Preferences` for convenience, but a logout clears the in-memory `ApiContext`.
