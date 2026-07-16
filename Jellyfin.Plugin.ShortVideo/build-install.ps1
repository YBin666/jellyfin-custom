param(
    [string]$ProjectDir = $PSScriptRoot
)

$ErrorActionPreference = "Stop"

Write-Host "`n=== ShortVideo Plugin Build & Install Script ===" -ForegroundColor Cyan

# 1. Build frontend
Write-Host "`n[1/3] Building frontend..." -ForegroundColor Yellow
$frontendDir = Join-Path $ProjectDir "Web\react-app"
if (-not (Test-Path $frontendDir)) {
    Write-Host "ERROR: Frontend directory not found: $frontendDir" -ForegroundColor Red
    exit 1
}

Push-Location $frontendDir
try {
    npx vite build
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Frontend build failed" -ForegroundColor Red
        exit 1
    }
    Write-Host "Frontend build succeeded" -ForegroundColor Green
} finally {
    Pop-Location
}

# 2. Build backend
Write-Host "`n[2/3] Building backend..." -ForegroundColor Yellow
$projFile = Join-Path $ProjectDir "Jellyfin.Plugin.ShortVideo.csproj"
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

# 3. Install plugin
Write-Host "`n[3/3] Installing plugin..." -ForegroundColor Yellow
$dllPath = Join-Path $ProjectDir "bin\Release\net9.0\Jellyfin.Plugin.ShortVideo.dll"
$pluginDir = "C:\Users\Yangb\AppData\Local\jellyfin\plugins\Jellyfin.Plugin.ShortVideo"

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

    # 4. Inject JS into index.html
    Write-Host "`n[4/4] Injecting JS into index.html..." -ForegroundColor Yellow
    $webDirs = @(
        "C:\Program Files\Jellyfin\Server\jellyfin-web",
        "C:\Users\Yangb\AppData\Local\jellyfin\jellyfin-web"
    )
    $injectMarker = '<!-- Jellyfin.ScriptHost injected -->'
    $scriptTag = $injectMarker + "`n<script src=`"/ScriptHost/Inject.js`"></script>`n"
    $injected = $false
    
    foreach ($wd in $webDirs) {
        $idx = Join-Path $wd "index.html"
        if (Test-Path $idx) {
            Write-Host "Found index.html: $idx" -ForegroundColor Gray
            
            $content = Get-Content $idx -Raw -Encoding UTF8
            
            if ($content.Contains($injectMarker)) {
                Write-Host "Removing old injection..." -ForegroundColor Gray
                $content = $content -replace [regex]::Escape($scriptTag), ''
            }
            
            $newContent = $content -replace '</body>', ($scriptTag + '</body>')
            
            try {
                [System.IO.File]::WriteAllText($idx, $newContent, [System.Text.Encoding]::UTF8)
                Write-Host "SUCCESS: Injected JS into $idx" -ForegroundColor Green
                $injected = $true
            } catch {
                Write-Host "WARNING: Cannot write to $idx (needs admin): $_" -ForegroundColor Yellow
            }
            break
        }
    }
    
    if (-not $injected) {
        Write-Host "WARNING: Could not inject JS into any index.html" -ForegroundColor Yellow
        Write-Host "Please run this script as Administrator" -ForegroundColor Yellow
    }

    Write-Host "`n=== Installation Complete ===" -ForegroundColor Green
    Write-Host "Start Jellyfin and navigate to http://localhost:8096" -ForegroundColor Cyan
} catch {
    Write-Host "ERROR: Installation failed: $_" -ForegroundColor Red
    exit 1
}