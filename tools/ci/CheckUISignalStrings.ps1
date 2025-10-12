# SPDX-License-Identifier: MIT
Param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Liste der zu verbietenden Signalnamen (EventHub + lokale UI-Signale)
$eventHubSignals = @(
  'MoneyChanged',
  'ProductionCostIncurred',
  'SelectedBuildingChanged',
  'ResourceTotalsChanged',
  'ResourceInfoChanged',
  'FarmStatusChanged',
  'MarketOrdersChanged'
)

$uiSignals = @(
  'build_selected',
  'accept_order',
  'accept'
)

function Find-OffendingUsages {
  Param(
    [string[]]$Names
  )
  $offenses = @()
  $files = Get-ChildItem -Path ui -Recurse -Include *.gd -File -ErrorAction SilentlyContinue
  foreach ($f in $files) {
    foreach ($n in $Names) {
      $patterns = @(
        ('connect("' + $n + '"'),
        ('is_connected("' + $n + '"'),
        ('emit_signal("' + $n + '"'),
        ('has_signal("' + $n + '")')
      )
      foreach ($p in $patterns) {
        $hit = Select-String -Path $f.FullName -SimpleMatch -CaseSensitive -Pattern $p -ErrorAction SilentlyContinue
        if ($hit) {
          $kind = if ($p.StartsWith('connect')) { 'connect' } elseif ($p.StartsWith('is_connected')) { 'is_connected' } elseif ($p.StartsWith('emit_signal')) { 'emit_signal' } else { 'has_signal' }
          $offenses += [pscustomobject]@{ File = $f.FullName; Name = $n; Kind = $kind }
        }
      }
    }
  }
  return $offenses
}

$violations = @()
$violations += Find-OffendingUsages -Names $eventHubSignals
$violations += Find-OffendingUsages -Names $uiSignals

if ($violations.Count -gt 0) {
  Write-Host "Found forbidden string-based signal usages in UI scripts:" -ForegroundColor Red
  $violations | Sort-Object File, Name, Kind | ForEach-Object {
    Write-Host (" - {0} :: {1}('{2}')" -f $_.File, $_.Kind, $_.Name)
  }
  throw "String-basierte Signal-Namen sind in UI verboten. Bitte EventNames.* verwenden."
}
else {
  Write-Host "UI check passed: No forbidden string-based signal names found." -ForegroundColor Green
}
