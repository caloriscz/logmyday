param (
    [string]$CsprojPath = "$PSScriptRoot/../src/LogMyDay.App/LogMyDay.App.csproj",
    [string]$ConfigPath = "$PSScriptRoot/../docs/_config.yml"
)

$ErrorActionPreference = "Stop"

Write-Host "Reading version from $CsprojPath..."
$csproj = [xml](Get-Content $CsprojPath)
$version = $csproj.Project.PropertyGroup.Version

if ([string]::IsNullOrWhiteSpace($version)) {
    Write-Error "Could not find Version in csproj file."
}

Write-Host "Found version: $version"

Write-Host "Updating $ConfigPath..."
$configContent = Get-Content $ConfigPath -Raw

# Regex to replace or add version
if ($configContent -match "project_version:.*") {
    $configContent = $configContent -replace "project_version:.*", "project_version: $version"
} else {
    $configContent += "`nproject_version: $version"
}

Set-Content -Path $ConfigPath -Value $configContent -Encoding UTF8

Write-Host "Successfully updated docs configuration with version $version."
