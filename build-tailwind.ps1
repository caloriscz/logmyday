# Quick Build Script for LogMyDay Tailwind Migration
# Run this from the repository root

param(
    [switch]$Watch,
    [switch]$BuildWeb,
    [switch]$BuildMobile,
    [switch]$RunWeb,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"

Write-Host "🎨 LogMyDay Tailwind CSS Build Script" -ForegroundColor Cyan
Write-Host "======================================" -ForegroundColor Cyan
Write-Host ""

# Change to repository root if needed
$repoRoot = Split-Path -Parent $PSScriptRoot
if (Test-Path (Join-Path $repoRoot "ui")) {
    Set-Location $repoRoot
}

# Clean build artifacts
if ($Clean) {
    Write-Host "🧹 Cleaning build artifacts..." -ForegroundColor Yellow
    
    if (Test-Path "ui\dist") {
        Remove-Item "ui\dist" -Recurse -Force
        Write-Host "  ✓ Removed ui/dist" -ForegroundColor Green
    }
    
    if (Test-Path "ui\node_modules") {
        Write-Host "  Removing node_modules (this may take a moment)..." -ForegroundColor Gray
        Remove-Item "ui\node_modules" -Recurse -Force
        Write-Host "  ✓ Removed ui/node_modules" -ForegroundColor Green
    }
    
    Write-Host "  ✓ Clean complete" -ForegroundColor Green
    Write-Host ""
}

# Step 1: Check Node.js
Write-Host "1️⃣  Checking Node.js installation..." -ForegroundColor Cyan
try {
    $nodeVersion = node --version
    Write-Host "  ✓ Node.js $nodeVersion found" -ForegroundColor Green
} catch {
    Write-Host "  ✗ Node.js not found!" -ForegroundColor Red
    Write-Host "  Please install Node.js from https://nodejs.org/" -ForegroundColor Yellow
    exit 1
}
Write-Host ""

# Step 2: Install npm dependencies
Write-Host "2️⃣  Checking npm dependencies..." -ForegroundColor Cyan
if (-not (Test-Path "ui\node_modules")) {
    Write-Host "  Installing npm packages (this may take a moment)..." -ForegroundColor Yellow
    Push-Location ui
    npm install
    Pop-Location
    Write-Host "  ✓ npm packages installed" -ForegroundColor Green
} else {
    Write-Host "  ✓ npm packages already installed" -ForegroundColor Green
}
Write-Host ""

# Step 3: Build Tailwind CSS
if ($Watch) {
    Write-Host "3️⃣  Starting Tailwind CSS watch mode..." -ForegroundColor Cyan
    Write-Host "  Watching for changes... (Press Ctrl+C to stop)" -ForegroundColor Yellow
    Push-Location ui
    npm run dev
    Pop-Location
    exit 0
}

Write-Host "3️⃣  Building Tailwind CSS..." -ForegroundColor Cyan
Push-Location ui
npm run build
Pop-Location

if ($LASTEXITCODE -eq 0) {
    Write-Host "  ✓ Tailwind CSS built successfully" -ForegroundColor Green
    
    # Show output file sizes
    if (Test-Path "ui\dist\css\tailwind.css") {
        $cssSize = (Get-Item "ui\dist\css\tailwind.css").Length / 1KB
        Write-Host "    - tailwind.css: $([math]::Round($cssSize, 2)) KB" -ForegroundColor Gray
    }
    if (Test-Path "ui\dist\js\app.js") {
        $jsSize = (Get-Item "ui\dist\js\app.js").Length / 1KB
        Write-Host "    - app.js: $([math]::Round($jsSize, 2)) KB" -ForegroundColor Gray
    }
} else {
    Write-Host "  ✗ Tailwind CSS build failed" -ForegroundColor Red
    exit 1
}
Write-Host ""

# Step 4: Build Web App (optional)
if ($BuildWeb) {
    Write-Host "4️⃣  Building Web App..." -ForegroundColor Cyan
    dotnet build LogMyDay.App/LogMyDay.App.csproj
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ Web app built successfully" -ForegroundColor Green
    } else {
        Write-Host "  ✗ Web app build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Step 5: Build MAUI App (optional)
if ($BuildMobile) {
    Write-Host "5️⃣  Building MAUI Mobile App..." -ForegroundColor Cyan
    dotnet build LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj
    
    if ($LASTEXITCODE -eq 0) {
        Write-Host "  ✓ MAUI app built successfully" -ForegroundColor Green
    } else {
        Write-Host "  ✗ MAUI app build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host ""
}

# Step 6: Run Web App (optional)
if ($RunWeb) {
    Write-Host "6️⃣  Starting Web App..." -ForegroundColor Cyan
    Write-Host "  Running at https://localhost:5001 (or check console output)" -ForegroundColor Yellow
    dotnet run --project LogMyDay.App/LogMyDay.App.csproj
    exit 0
}

Write-Host "✅ Build Complete!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Cyan
Write-Host "  • Run web app:       .\build-tailwind.ps1 -RunWeb" -ForegroundColor Gray
Write-Host "  • Build web app:     .\build-tailwind.ps1 -BuildWeb" -ForegroundColor Gray
Write-Host "  • Build mobile:      .\build-tailwind.ps1 -BuildMobile" -ForegroundColor Gray
Write-Host "  • Watch CSS changes: .\build-tailwind.ps1 -Watch" -ForegroundColor Gray
Write-Host "  • Clean build:       .\build-tailwind.ps1 -Clean" -ForegroundColor Gray
Write-Host ""
