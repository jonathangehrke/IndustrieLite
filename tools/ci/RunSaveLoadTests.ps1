# SPDX-License-Identifier: MIT
Param(
    [switch]$VerboseLog
)

# PowerShell Runner fuer Save/Load Round-Trip Tests (Phase 1)
# - Startet Godot headless mit SaveLoadTestRunner.gd
# - Parst JSON-Resultat aus der Ausgabe
# - Setzt Exit-Code fuer CI

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Projekt-Root (zwei Ebenen ueber diesem Script)
$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $ProjectRoot
try {
    # Godot-Binary finden
    $candidates = @()
    if ($env:GODOT4_BIN) { $candidates += $env:GODOT4_BIN }
    if ($env:GODOT_BIN) { $candidates += $env:GODOT_BIN }
    $candidates += 'godot4'
    $candidates += 'godot'

    $godot = $null
    foreach ($bin in $candidates) {
        try {
            $null = & $bin --version 2>$null
            if ($LASTEXITCODE -eq 0) { $godot = $bin; break }
        } catch { }
    }
    if (-not $godot) {
        Write-Error "Godot Binary nicht gefunden. Setze ENV GODOT4_BIN oder GODOT_BIN, oder installiere Godot in PATH."
        exit 3
    }

    if ($VerboseLog) { Write-Host "[CI] Verwende Godot: $godot" }

    # Tests ausfuehren (headless)
    $args = @('--headless', '--script', 'res://tools/ci/SaveLoadTestRunner.gd')
    $output = & $godot @args 2>&1
    $exitCode = $LASTEXITCODE

    # Ausgabe spiegeln
    $output | ForEach-Object { Write-Host $_ }

    # JSON_RESULT suchen und parsen
    $jsonLine = ($output | Where-Object { $_ -match '^JSON_RESULT:' } | Select-Object -First 1)
    $success = $false
    if ($jsonLine) {
        $json = ($jsonLine -replace '^JSON_RESULT:\s*', '')
        try {
            $obj = $json | ConvertFrom-Json -Depth 4
            $success = [bool]$obj.test_successful
            if ($success) {
                Write-Host "[CI] Round-trip: PASS"
            } else {
                Write-Error ("[CI] Round-trip: FAIL{0}" -f ($(if ($obj.error_message) {" - " + $obj.error_message} else {''})))
            }
        } catch {
            Write-Warning "[CI] Konnte JSON_RESULT nicht parsen: $_"
        }
    } else {
        Write-Warning "[CI] JSON_RESULT nicht in Ausgabe gefunden."
    }

    if ($exitCode -ne 0) {
        exit $exitCode
    }
    exit ($success ? 0 : 1)
}
finally {
    Pop-Location
}

