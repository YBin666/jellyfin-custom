# Build & Install FileUpload Plugin
# 新版本已移除媒体库列表浮动按钮，无需注入 index.html
param(
    [string]$ProjectDir = "c:\CodeFiles\IdeaProject\jellyfin-custom\Jellyfin.Plugin.FileUpload"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== FileUpload Plugin Build & Install Script ===" -ForegroundColor Cyan

# 1. Build backend
Write-Host "`n[1/2] Building backend..." -ForegroundColor Yellow
$projFile = Join-Path $ProjectDir "Jellyfin.Plugin.FileUpload.csproj"
if (-not (Test-Path $projFile)) {
    Write-Host "ERROR: Project file not found: $projFile" -ForegroundColor Red
    exit 1
}

dotnet build $projFile -c Release
if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Backend build failed" -ForegroundColor Red
    exit 1
}
Write-Host "Backend build succeeded" -ForegroundColor Green

# 2. Install plugin
Write-Host "`n[2/2] Installing plugin..." -ForegroundColor Yellow
$dllPath = Join-Path $ProjectDir "bin\Release\net9.0\Jellyfin.Plugin.FileUpload.dll"
$pluginDir = "C:\Users\Yangb\AppData\Local\jellyfin\plugins\FileUpload_1.0.0.0"

if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: DLL not found: $dllPath" -ForegroundColor Red
    exit 1
}

try {
    Stop-Process -Name jellyfin -Force -ErrorAction SilentlyContinue
    Write-Host "Stopped Jellyfin service" -ForegroundColor Gray

    Start-Sleep -Seconds 2

    if (-not (Test-Path $pluginDir)) {
        New-Item -ItemType Directory -Path $pluginDir -Force | Out-Null
    }

    Copy-Item $dllPath $pluginDir -Force
    Write-Host "Copied DLL to plugin directory" -ForegroundColor Gray

    $metaJson = Join-Path $pluginDir "meta.json"
    if (Test-Path $metaJson) {
        Remove-Item $metaJson -Force
        Write-Host "Removed meta.json cache" -ForegroundColor Gray
    }

    Write-Host "`n=== Installation Complete ===" -ForegroundColor Green
    Write-Host "Start Jellyfin and navigate to http://localhost:8096" -ForegroundColor Cyan
    Write-Host "插件启动时会自动清理旧版本遗留的 index.html 注入（如有）" -ForegroundColor Gray
} catch {
    Write-Host "ERROR: Installation failed: $_" -ForegroundColor Red
    exit 1
}
