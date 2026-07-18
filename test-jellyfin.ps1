# 测试 Jellyfin 是否可访问
try {
    $r = Invoke-WebRequest -Uri 'http://localhost:8096/System/Info/Public' -UseBasicParsing -TimeoutSec 5
    Write-Host "Jellyfin OK: HTTP $($r.StatusCode)"
} catch {
    Write-Host "Jellyfin ERROR: $($_.Exception.Message)"
}
