# Script to replace emoji characters with ASCII equivalents
$ErrorActionPreference = "Stop"

$files = @(
    "src\LogMyDay.Manager.Cli\Commands\ServerCommands.cs",
    "src\LogMyDay.Manager.Cli\Commands\ManagerCommands.cs",
    "src\LogMyDay.Manager.Core\Services\PrerequisiteChecker.cs"
)

foreach ($file in $files) {
    Write-Host "Processing $file..."
    
    $fullPath = Join-Path $PSScriptRoot "..\$file"
    $content = Get-Content $fullPath -Raw -Encoding UTF8
    
    # Replace emoji characters
    $content = $content `
        -replace '✓', '[OK]' `
        -replace '✗', '[X]' `
        -replace '⚠', '[WARNING]' `
        -replace '•', '-'
    
    Set-Content $fullPath -Value $content -NoNewline -Encoding UTF8
    Write-Host "  Done!"
}

Write-Host "`nAll emoji characters replaced successfully!"
