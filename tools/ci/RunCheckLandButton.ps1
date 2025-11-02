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
  $candidates += (Join-Path $ProjectRoot 'Godot_v4.4.1-stable_mono_win64\Godot_v4.4.1-stable_mono_win64_console.exe')
  $candidates += 'godot4'
  $candidates += 'godot'
  $godot = $null
  foreach ($bin in $candidates) { try { $null = & $bin --version 2>$null; if ($LASTEXITCODE -eq 0) { $godot = $bin; break } } catch { } }
  if (-not $godot) { Write-Error 'Godot Binary nicht gefunden.'; exit 3 }
  if ($VerboseLog) { Write-Host "[CI] Verwende Godot: $godot" }

  $args = @('--headless','--path','.', '--script','res://tools/ci/CheckLandButton.gd')
  $output = @()
  try {
    $output = & $godot @args 2>&1
    $exitCode = $LASTEXITCODE
  } catch {
    # Fange NativeCommandError (z. B. Warnungen auf stderr) ab, werte Ausgabe trotzdem aus
    $exitCode = $LASTEXITCODE
    if (-not $output) { $output = @('') }
  }
  $output | ForEach-Object { Write-Host $_ }

  $jsonLine = ($output | Where-Object { $_ -match '^JSON_RESULT:' } | Select-Object -First 1)
  $ok = $false
  if ($jsonLine) {
    $json = ($jsonLine -replace '^JSON_RESULT:\s*','')
    try { $obj = $json | ConvertFrom-Json -Depth 6; $ok = [bool]($obj.land_button.found -and (($obj.land_button.visible_in_tree) -or ($obj.land_button.visible_in_tree_after))) } catch { Write-Warning "[CI] JSON_RESULT parse error: $_" }
  }
  if ($exitCode -ne 0) { exit $exitCode }
  if ($ok) { exit 0 } else { exit 1 }
}
finally { Pop-Location }
