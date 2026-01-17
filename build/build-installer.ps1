# DEPRECATED: This script has been replaced by build-manager.ps1
# LogMyDay.Installer has been renamed to LogMyDay.Manager
# Please use: .\build-manager.ps1

Write-Host "=== DEPRECATED SCRIPT ===" -ForegroundColor Red
Write-Host ""
Write-Host "This script has been replaced. Please use:" -ForegroundColor Yellow
Write-Host "  .\build-manager.ps1" -ForegroundColor Cyan
Write-Host ""
Write-Host "LogMyDay.Installer has been refactored into:" -ForegroundColor Yellow
Write-Host "  - LogMyDay.Manager.Core (business logic)" -ForegroundColor White
Write-Host "  - LogMyDay.Manager.Cli (.NET global tool)" -ForegroundColor White
Write-Host ""
Write-Host "New installation method:" -ForegroundColor Yellow
Write-Host "  dotnet tool install -g LogMyDay.Manager" -ForegroundColor Cyan
Write-Host ""
Write-Host "Running new build script..." -ForegroundColor Green
Write-Host ""

# Forward to new script
& "$PSScriptRoot\build-manager.ps1" @PSBoundParameters
