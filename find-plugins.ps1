$paths = @(
    "$env:LOCALAPPDATA\jellyfin\plugins",
    "$env:APPDATA\jellyfin\plugins",
    "$env:LOCALAPPDATA\jellyfin\root\default\plugins",
    "$env:ProgramData\jellyfin\plugins",
    "C:\Program Files\Jellyfin\Server\plugins"
)

foreach ($p in $paths) {
    if (Test-Path $p) {
        Write-Host "FOUND: $p"
        Get-ChildItem $p -Directory -ErrorAction SilentlyContinue | ForEach-Object { Write-Host "  - $($_.Name)" }
    } else {
        Write-Host "NOT FOUND: $p"
    }
}

Write-Host "`n--- Searching for config files ---"
$configPaths = @(
    "$env:LOCALAPPDATA\jellyfin",
    "$env:APPDATA\jellyfin",
    "C:\Program Files\Jellyfin\Server"
)
foreach ($c in $configPaths) {
    if (Test-Path $c) {
        Write-Host "EXISTS: $c"
    }
}
