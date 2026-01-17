# Build LogMyDay Manager as a .NET Global Tool

param(
    [string]$Configuration = "Release",
    [string]$OutputPath = ".\publish\manager"
)

Write-Host "Building LogMyDay Manager..." -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration"
Write-Host "  Output: $OutputPath"
Write-Host ""

$projectPath = Join-Path $PSScriptRoot "..\src\LogMyDay.Manager.Cli\LogMyDay.Manager.Cli.csproj"

# Clean previous build
if (Test-Path $OutputPath) {
    Write-Host "Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Path $OutputPath -Recurse -Force
}

# Create output directory
New-Item -ItemType Directory -Path $OutputPath -Force | Out-Null

# Pack as NuGet package (global tool)
Write-Host "Packing as .NET Global Tool..." -ForegroundColor Cyan
dotnet pack $projectPath `
    --configuration $Configuration `
    --output $OutputPath

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "  Package: $OutputPath\LogMyDay.Manager.*.nupkg"
    Write-Host ""
    Write-Host "Installation Instructions:" -ForegroundColor Cyan
    Write-Host "  1. Install globally:" -ForegroundColor White
    Write-Host "     dotnet tool install -g LogMyDay.Manager --add-source $OutputPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  2. Update existing installation:" -ForegroundColor White
    Write-Host "     dotnet tool update -g LogMyDay.Manager --add-source $OutputPath" -ForegroundColor Gray
    Write-Host ""
    Write-Host "  3. Run the tool:" -ForegroundColor White
    Write-Host "     logmyday install" -ForegroundColor Gray
    Write-Host "     logmyday status" -ForegroundColor Gray
    Write-Host "     logmyday backup -s <server-url>" -ForegroundColor Gray
    Write-Host ""
    
    $packageFile = Get-ChildItem "$OutputPath\LogMyDay.Manager.*.nupkg" | Select-Object -First 1
    if ($packageFile) {
        $fileSizeKB = [math]::Round($packageFile.Length / 1KB, 2)
        Write-Host "  Package Size: $fileSizeKB KB" -ForegroundColor White
    }
} else {
    Write-Host ""
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
