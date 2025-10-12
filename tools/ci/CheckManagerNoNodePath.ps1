# SPDX-License-Identifier: MIT
Param(
  [string]$Root = "."
)

# Prueft, dass BuildingManager und EconomyManager keine NodePath-Felder/-Verkabelungen nutzen
# Rueckgabe: Exit 0 wenn OK, sonst 1 mit Fehlerliste

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$targets = @(
  "code/managers/BuildingManager.cs",
  "code/managers/EconomyManager.cs",
  "code/input/InputManager.cs",
  "code/managers/RoadManager.cs",
  "code/managers/TransportManager.cs",
  "code/managers/ProductionManager.cs",
  "code/runtime/Map.cs",
  "code/runtime/CameraController.cs",
  "code/transport/TransportCoordinator.cs"
)

$violations = @()

foreach ($rel in $targets) {
  $path = Join-Path $Root $rel
  if (-not (Test-Path -LiteralPath $path)) { continue }
  $text = Get-Content -Raw -LiteralPath $path

  # Keine NodePath-Properties oder -Verwendungen erlaubt
  if ($text -match '\bNodePath\b') {
    $violations += [pscustomobject]@{ File=$path; Kind='NodePath usage forbidden'; Line='n/a'; Text='NodePath found' }
  }
  # Keine direkten GetNode*/NodePath-Aufloesungen in diesen Managern
  if ($text -match '\bGetNode\s*<') { $violations += [pscustomobject]@{ File=$path; Kind='GetNode<>() forbidden'; Line='n/a'; Text='GetNode<T>() found' } }
  if ($text -match '\bGetNodeOrNull\s*<') { $violations += [pscustomobject]@{ File=$path; Kind='GetNodeOrNull<>() forbidden'; Line='n/a'; Text='GetNodeOrNull<T>() found' } }
}

if ($violations.Count -gt 0) {
  Write-Error "Verbotene NodePath/Node-Aufrufe in Managern gefunden:" 
  foreach ($v in $violations) {
    Write-Host (" - {0}: {1} ({2})" -f $v.File, $v.Kind, $v.Text)
  }
  exit 1
}

Write-Host "OK: BuildingManager/EconomyManager nutzen keine NodePath-Verkabelung."
exit 0
