param(
    [string]$ProjectDir = $PSScriptRoot,
    [string]$PluginName = "Jellyfin.Plugin.FileUpload",
    [string]$PluginInstallDir = "C:\Users\Yangb\AppData\Local\jellyfin\plugins\Jellyfin.Plugin.FileUpload"
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== FileUpload Plugin Build & Install Script ===" -ForegroundColor Cyan

# 1. Build backend
Write-Host "`n[1/2] Building backend..." -ForegroundColor Yellow
$projFile = Join-Path $ProjectDir "$PluginName.csproj"
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
$dllPath = Join-Path $ProjectDir "bin\Release\net9.0\$PluginName.dll"
$pdbPath = Join-Path $ProjectDir "bin\Release\net9.0\$PluginName.pdb"
$buildYaml = Join-Path $ProjectDir "build.yaml"

if (-not (Test-Path $dllPath)) {
    Write-Host "ERROR: DLL not found: $dllPath" -ForegroundColor Red
    exit 1
}

try {
    # Stop Jellyfin
    $jellyfinProc = Get-Process -Name jellyfin -ErrorAction SilentlyContinue
    if ($jellyfinProc) {
        Stop-Process -Name jellyfin -Force
        Write-Host "Stopped Jellyfin process" -ForegroundColor Gray
        Start-Sleep -Seconds 2
    } else {
        Write-Host "Jellyfin not running" -ForegroundColor Gray
    }

    # Ensure plugin directory
    if (-not (Test-Path $PluginInstallDir)) {
        New-Item -ItemType Directory -Path $PluginInstallDir -Force | Out-Null
    }

    # Copy DLL + PDB
    Copy-Item $dllPath $PluginInstallDir -Force
    Write-Host "Copied DLL to $PluginInstallDir" -ForegroundColor Gray

    if (Test-Path $pdbPath) {
        Copy-Item $pdbPath $PluginInstallDir -Force
        Write-Host "Copied PDB to $PluginInstallDir" -ForegroundColor Gray
    }

    if (Test-Path $buildYaml) {
        Copy-Item $buildYaml $PluginInstallDir -Force
        Write-Host "Copied build.yaml to $PluginInstallDir" -ForegroundColor Gray
    }

    # Clear meta.json cache
    $metaJson = Join-Path $PluginInstallDir "meta.json"
    if (Test-Path $metaJson) {
        Remove-Item $metaJson -Force
        Write-Host "Removed meta.json cache" -ForegroundColor Gray
    }

    Write-Host "`n=== Installation Complete ===" -ForegroundColor Green
    Write-Host "Plugin directory: $PluginInstallDir" -ForegroundColor Cyan
    Write-Host "Start Jellyfin, then go to: Dashboard -> Plugins -> FileUpload" -ForegroundColor Cyan
    Write-Host "Config page URL: http://localhost:8096/web/index.html#!/configurationpage?name=FileUpload" -ForegroundColor Cyan
} catch {
    Write-Host "ERROR: Installation failed: $_" -ForegroundColor Red
    exit 1
}
