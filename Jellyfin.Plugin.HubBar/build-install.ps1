$ErrorActionPreference = "Stop"

$pluginName = "Jellyfin.Plugin.HubBar"
$projectPath = "$PSScriptRoot\$pluginName.csproj"
$publishDir = "$PSScriptRoot\bin\Release\net9.0\publish"

Write-Host "=== Building $pluginName ===" -ForegroundColor Cyan

Write-Host "`n1. Building React app..." -ForegroundColor Yellow
$reactDir = "$PSScriptRoot\Web\react-app"
if (-not (Test-Path "$reactDir\node_modules")) {
    Write-Host "   Installing dependencies..." -ForegroundColor Gray
    Push-Location $reactDir
    pnpm install
    Pop-Location
}
Push-Location $reactDir
pnpm run build
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nERROR: React build failed!" -ForegroundColor Red
    exit 1
}
Pop-Location
Write-Host "   React app built successfully" -ForegroundColor Green

Write-Host "`n2. Building .NET plugin..." -ForegroundColor Yellow
dotnet publish $projectPath -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "`nERROR: .NET build failed!" -ForegroundColor Red
    exit 1
}
Write-Host "   .NET plugin built successfully" -ForegroundColor Green

Write-Host "`n3. Packing plugin..." -ForegroundColor Yellow
$zipPath = "$PSScriptRoot\$pluginName.zip"
if (Test-Path $zipPath) {
    Remove-Item $zipPath -Force
}
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipPath
Write-Host "   Plugin packed to: $zipPath" -ForegroundColor Green

Write-Host "`n=== Build completed successfully ===" -ForegroundColor Cyan
Write-Host "`nPlugin path: $zipPath" -ForegroundColor Gray