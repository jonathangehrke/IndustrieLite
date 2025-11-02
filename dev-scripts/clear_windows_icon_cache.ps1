# Clear Windows Icon Cache
# Run this script if the .exe file icon doesn't update in Windows Explorer after export

Write-Host "Clearing Windows Icon Cache..." -ForegroundColor Cyan

# Stop Windows Explorer
Write-Host "`nStopping Windows Explorer..." -ForegroundColor Yellow
Stop-Process -Name explorer -Force

# Wait a moment for Explorer to close
Start-Sleep -Seconds 2

# Delete icon cache files
Write-Host "`nDeleting icon cache files..." -ForegroundColor Yellow

$iconCachePath = "$env:LOCALAPPDATA\Microsoft\Windows\Explorer"
$iconCacheFiles = @(
    "iconcache_*.db",
    "thumbcache_*.db"
)

$deletedCount = 0
foreach ($pattern in $iconCacheFiles) {
    $files = Get-ChildItem -Path $iconCachePath -Filter $pattern -ErrorAction SilentlyContinue
    foreach ($file in $files) {
        try {
            Remove-Item $file.FullName -Force -ErrorAction Stop
            Write-Host "  Deleted: $($file.Name)" -ForegroundColor Green
            $deletedCount++
        }
        catch {
            Write-Host "  Failed to delete: $($file.Name)" -ForegroundColor Red
        }
    }
}

if ($deletedCount -eq 0) {
    Write-Host "  No cache files found or already deleted" -ForegroundColor Gray
}

# Restart Windows Explorer
Write-Host "`nRestarting Windows Explorer..." -ForegroundColor Yellow
Start-Process explorer.exe

Write-Host "`nIcon cache cleared successfully!" -ForegroundColor Green
Write-Host "The new icon should appear in Windows Explorer now.`n" -ForegroundColor Cyan
