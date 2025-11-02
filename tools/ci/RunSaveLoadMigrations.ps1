# SPDX-License-Identifier: MIT
Param(
    [switch]$VerboseLog
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot '..\..')
Push-Location $ProjectRoot
try {
    $candidates = @()
    if ($env:GODOT4_BIN) { $candidates += $env:GODOT4_BIN }
    if ($env:GODOT_BIN) { $candidates += $env:GODOT_BIN }
    $candidates += 'godot4'
    $candidates += 'godot'

    $godot = $null
    foreach ($bin in $candidates) {
        try { $null = & $bin --version 2>$null; if ($LASTEXITCODE -eq 0) { $godot = $bin; break } } catch { }
    }
    if (-not $godot) { Write-Error 'Godot Binary nicht gefunden.'; exit 3 }
    if ($VerboseLog) { Write-Host "[CI] Verwende Godot: $godot" }

    $args = @('--headless', '--script', 'res://tools/ci/SaveLoadMigrationRunner.gd')
    $output = & $godot @args 2>&1
    $exitCode = $LASTEXITCODE

    $output | ForEach-Object { Write-Host $_ }

    $jsonLine = ($output | Where-Object { $_ -match '^JSON_RESULT:' } | Select-Object -First 1)
    $allOk = $false
    if ($jsonLine) {
        $json = ($jsonLine -replace '^JSON_RESULT:\s*', '')
        try {
            $obj = $json | ConvertFrom-Json -Depth 6
            $allOk = [bool]$obj.all_ok
            if ($allOk) { Write-Host '[CI] Migration: PASS' } else { Write-Error '[CI] Migration: FAIL' }
        } catch { Write-Warning "[CI] Konnte JSON_RESULT nicht parsen: $_" }
    } else {
        Write-Warning '[CI] JSON_RESULT nicht in Ausgabe gefunden.'
    }

    if ($exitCode -ne 0) { exit $exitCode }
    exit ($allOk ? 0 : 1)
}
finally { Pop-Location }

