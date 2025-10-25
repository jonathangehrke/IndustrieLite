# SPDX-License-Identifier: MIT
Param()
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expected = @('ServiceContainer','SceneGraphAdapter','DevFlags','EventHub','Database','UIService','BootSelfTest','DataIndex')
$expectedMap = @{
  'ServiceContainer'='*res://code/runtime/ServiceContainer.cs'
  'SceneGraphAdapter'='*res://code/runtime/SceneGraphAdapter.cs'
  'DevFlags'='*res://code/runtime/DevFlags.gd'
  'EventHub'='*res://code/runtime/EventHub.cs'
  'Database'='*res://code/runtime/Database.cs'
  'UIService'='*res://code/runtime/UIService.cs'
  'BootSelfTest'='*res://code/runtime/BootSelfTest.cs'
  'DataIndex'='*res://scenes/DataIndex.gd'
}

$autoloadNames = @()
$autoloadPairs = @{}
$inSection = $false
Get-Content project.godot | ForEach-Object {
  if ($_ -match '^\[autoload\]') { $inSection=$true; return }
  if ($inSection -and $_ -match '^\[') { $inSection=$false }
  if ($inSection) {
    if ($_ -match '^(\w+)=(.+)$') {
      $name = $matches[1]
      $val = $matches[2].Trim().Trim('"')
      $autoloadNames += $name
      $autoloadPairs[$name] = $val
    }
  }
}

Write-Host '[CI] Autoload entries found in project.godot:'
$autoloadNames | ForEach-Object { Write-Host " - $_" }

$exit = 0

# Presence
$missing = @($expected | Where-Object { $_ -notin $autoloadNames })
if ($missing.Count -gt 0) {
  Write-Error "Missing autoload entries: $($missing -join ', ')"
  $exit = 101
}

# Order
$okOrder = (@($expected) -join '||') -eq (@($autoloadNames) -join '||')
if (-not $okOrder) {
  Write-Error "Order mismatch. Expected: $($expected -join ' -> '); Got: $($autoloadNames -join ' -> ')"
  if ($exit -eq 0) { $exit = 103 }
}

# Absolute position for ServiceContainer
if ($autoloadNames.Count -gt 0 -and $autoloadNames[0] -ne 'ServiceContainer') {
  Write-Error 'ServiceContainer must be at index 0'
  if ($exit -eq 0) { $exit = 105 }
}

# Values
$mismatches = New-Object System.Collections.ArrayList
foreach ($k in $expected) {
  if ($expectedMap[$k] -ne $autoloadPairs[$k]) { [void]$mismatches.Add("$k (expected: $($expectedMap[$k]); got: $($autoloadPairs[$k]))") }
}
if ($mismatches.Count -gt 0) {
  Write-Error ("Value mismatches: `n - " + ($mismatches -join "`n - "))
  if ($exit -eq 0) { $exit = 104 }
}

# JSON result
$status = if ($exit -eq 0) { 'ok' } else { 'error' }
$result = [ordered]@{
  expected = $expected
  found = $autoloadNames
  values_ok = ($mismatches.Count -eq 0)
  order_ok = $okOrder
  missing = $missing
  mismatches = $mismatches
  exit_code = $exit
  status = $status
}
Write-Host ('JSON_RESULT: ' + ($result | ConvertTo-Json -Depth 5 -Compress))

exit $exit
