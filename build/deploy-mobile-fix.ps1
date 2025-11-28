# Mobile App - Clean Build & Deploy Script
# Run this from the solution root directory

Write-Host "🚀 LogMyDay Mobile - Clean Build & Deploy" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host ""

# Step 1: Clean
Write-Host "🧹 Step 1/4: Cleaning project..." -ForegroundColor Yellow
dotnet clean src/LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Clean failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Clean completed" -ForegroundColor Green
Write-Host ""

# Step 2: Build
Write-Host "🔨 Step 2/4: Building project..." -ForegroundColor Yellow
dotnet build src/LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj -f net9.0-android
if ($LASTEXITCODE -ne 0) {
    Write-Host "❌ Build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "✅ Build completed" -ForegroundColor Green
Write-Host ""

# Step 3: Verify CSS files in build output
Write-Host "🔍 Step 3/4: Verifying CSS files in build output..." -ForegroundColor Yellow
$cssFiles = Get-ChildItem "src\LogMyDay.App.Mobile\obj\Debug\net9.0-android\" -Recurse -Filter "*.css" -ErrorAction SilentlyContinue
if ($cssFiles) {
    Write-Host "✅ Found CSS files in build output:" -ForegroundColor Green
    $cssFiles | ForEach-Object { Write-Host "   - $($_.Name) ($([math]::Round($_.Length/1KB, 2)) KB)" -ForegroundColor Gray }
} else {
    Write-Host "⚠️  Warning: No CSS files found in build output!" -ForegroundColor Yellow
    Write-Host "   This might indicate the MauiAsset configuration isn't working." -ForegroundColor Yellow
}
Write-Host ""

# Step 4: Instructions for manual deployment
Write-Host "📱 Step 4/4: Deploy to device/emulator" -ForegroundColor Yellow
Write-Host ""
Write-Host "BEFORE DEPLOYING:" -ForegroundColor Cyan
Write-Host "1. Uninstall the old app from your emulator/device:" -ForegroundColor White
Write-Host "   Settings → Apps → LogMyDay Mobile → Uninstall" -ForegroundColor Gray
Write-Host "   OR use: adb uninstall com.logmyday.mobile" -ForegroundColor Gray
Write-Host ""
Write-Host "THEN DEPLOY:" -ForegroundColor Cyan
Write-Host "• From Visual Studio: Right-click LogMyDay.App.Mobile → Deploy" -ForegroundColor White
Write-Host "• From command line: dotnet build src/LogMyDay.App.Mobile/LogMyDay.App.Mobile.csproj -t:Run -f net9.0-android" -ForegroundColor Gray
Write-Host ""

Write-Host "✅ Build preparation complete!" -ForegroundColor Green
Write-Host ""
Write-Host "📋 After deployment, verify:" -ForegroundColor Cyan
Write-Host "   ✓ Cards have visible borders and backgrounds" -ForegroundColor White
Write-Host "   ✓ Secondary buttons are gray (not blue)" -ForegroundColor White
Write-Host "   ✓ Danger buttons are red" -ForegroundColor White
Write-Host "   ✓ Alert messages have colored backgrounds" -ForegroundColor White
Write-Host "   ✓ Date picker opens native Android UI" -ForegroundColor White
Write-Host "   ✓ No JavaScript errors in console" -ForegroundColor White
Write-Host "   ✓ Dark theme toggle works" -ForegroundColor White
Write-Host ""
