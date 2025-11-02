# PowerShell Script: Icon-Generator für IndustrieLite
# Generiert .ico (Windows) aus dem Basis-Icon

param(
    [string]$sourceIcon = "assets/app-icon/app-icon.png",
    [string]$outputIco = "export/icon.ico"
)

Write-Host "=== IndustrieLite Icon Generator ===" -ForegroundColor Cyan
Write-Host ""

# Prüfe ob ImageMagick installiert ist
$magickPath = Get-Command magick -ErrorAction SilentlyContinue

if (-not $magickPath) {
    Write-Host "ERROR: ImageMagick nicht gefunden!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Bitte installiere ImageMagick:" -ForegroundColor Yellow
    Write-Host "  1. Download: https://imagemagick.org/script/download.php#windows" -ForegroundColor Yellow
    Write-Host "  2. Installiere mit 'Add to PATH' Option" -ForegroundColor Yellow
    Write-Host "  3. Führe dieses Script erneut aus" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "ALTERNATIVE: Verwende Online-Tool:" -ForegroundColor Yellow
    Write-Host "  → https://convertico.com" -ForegroundColor Yellow
    Write-Host "  → Upload: $sourceIcon" -ForegroundColor Yellow
    Write-Host "  → Download als: $outputIco" -ForegroundColor Yellow
    Write-Host ""
    exit 1
}

# Prüfe ob Quell-Icon existiert
if (-not (Test-Path $sourceIcon)) {
    Write-Host "ERROR: Icon nicht gefunden: $sourceIcon" -ForegroundColor Red
    exit 1
}

Write-Host "Quelle:  $sourceIcon" -ForegroundColor Green
Write-Host "Output:  $outputIco" -ForegroundColor Green
Write-Host ""

# Erstelle export/ Ordner falls nicht vorhanden
$exportDir = Split-Path $outputIco -Parent
if (-not (Test-Path $exportDir)) {
    New-Item -ItemType Directory -Path $exportDir | Out-Null
    Write-Host "✓ Erstellt: $exportDir/" -ForegroundColor Green
}

# Generiere Windows .ico (Multi-Size: 256, 128, 64, 48, 32, 16)
Write-Host "Generiere Windows .ico..." -ForegroundColor Cyan
& magick convert $sourceIcon -define icon:auto-resize=256,128,64,48,32,16 $outputIco

if ($LASTEXITCODE -eq 0) {
    $fileSize = (Get-Item $outputIco).Length / 1KB
    Write-Host "✓ Erfolgreich: $outputIco ($([math]::Round($fileSize, 1)) KB)" -ForegroundColor Green
} else {
    Write-Host "✗ Fehler beim Generieren der .ico Datei" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "=== Fertig! ===" -ForegroundColor Cyan
Write-Host ""
Write-Host "Nächste Schritte:" -ForegroundColor Yellow
Write-Host "  1. Öffne Godot Editor" -ForegroundColor White
Write-Host "  2. Projekt sollte neues Icon im Project Manager zeigen" -ForegroundColor White
Write-Host "  3. Für Export: Project → Export → Windows Desktop" -ForegroundColor White
Write-Host "     → Application → Icon: res://export/icon.ico" -ForegroundColor White
Write-Host ""
