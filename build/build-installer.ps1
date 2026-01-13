# Build LogMyDay Installer as a self-contained single-file executable

param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputPath = ".\publish\installer"
)

Write-Host "Building LogMyDay Installer..." -ForegroundColor Cyan
Write-Host "  Configuration: $Configuration"
Write-Host "  Runtime: $Runtime"
Write-Host "  Output: $OutputPath"
Write-Host ""

$projectPath = Join-Path $PSScriptRoot "..\src\LogMyDay.Installer\LogMyDay.Installer.csproj"

# Clean previous build
if (Test-Path $OutputPath) {
    Write-Host "Cleaning previous build..." -ForegroundColor Yellow
    Remove-Item -Path $OutputPath -Recurse -Force
}

# Publish as self-contained single-file
Write-Host "Publishing installer..." -ForegroundColor Cyan
dotnet publish $projectPath `
    --configuration $Configuration `
    --runtime $Runtime `
    --self-contained true `
    --output $OutputPath `
    /p:PublishSingleFile=true `
    /p:PublishTrimmed=false `
    /p:IncludeNativeLibrariesForSelfExtract=true

if ($LASTEXITCODE -eq 0) {
    Write-Host ""
    Write-Host "Build successful!" -ForegroundColor Green
    Write-Host "  Executable: $OutputPath\logmyday.exe"
    
    $fileInfo = Get-Item "$OutputPath\logmyday.exe"
    $fileSizeMB = [math]::Round($fileInfo.Length / 1MB, 2)
    Write-Host "  Size: $fileSizeMB MB"
} else {
    Write-Host ""
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}
