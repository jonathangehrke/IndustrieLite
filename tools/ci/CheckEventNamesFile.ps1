# SPDX-License-Identifier: MIT
Param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path (Join-Path $PSScriptRoot '..') '..')).Path
$filePath = Join-Path $root 'ui/event_names.gd'
if (-not (Test-Path -LiteralPath $filePath)) {
  throw "Erwartete Datei fehlt: $filePath"
}

$text = Get-Content -Raw -LiteralPath $filePath

# Muss klassennamenregistriert sein
$classOk = Select-String -Path $filePath -Pattern '^\s*class_name\s+EventNames\b' -CaseSensitive
if (-not $classOk) {
  throw "In ui/event_names.gd fehlt 'class_name EventNames'"
}

# Erwartete Konstanten und Werte
$expected = @(
  @{ Name = 'MONEY_CHANGED';               Value = 'MoneyChanged' },
  @{ Name = 'PRODUCTION_COST_INCURRED';    Value = 'ProductionCostIncurred' },
  @{ Name = 'SELECTED_BUILDING_CHANGED';   Value = 'SelectedBuildingChanged' },
  @{ Name = 'RESOURCE_TOTALS_CHANGED';     Value = 'ResourceTotalsChanged' },
  @{ Name = 'RESOURCE_INFO_CHANGED';       Value = 'ResourceInfoChanged' },
  @{ Name = 'FARM_STATUS_CHANGED';         Value = 'FarmStatusChanged' },
  @{ Name = 'MARKET_ORDERS_CHANGED';       Value = 'MarketOrdersChanged' },
  @{ Name = 'UI_BUILD_SELECTED';           Value = 'build_selected' },
  @{ Name = 'UI_ACCEPT_ORDER';             Value = 'accept_order' },
  @{ Name = 'UI_ACCEPT';                   Value = 'accept' }
)

$missing = @()
foreach ($e in $expected) {
  $pattern = '^\s*const\s+{0}\s*=\s*"{1}"\s*$' -f [regex]::Escape($e.Name), [regex]::Escape($e.Value)
  $hit = Select-String -Path $filePath -Pattern $pattern -CaseSensitive
  if (-not $hit) {
    $missing += $e
  }
}

if ($missing.Count -gt 0) {
  Write-Host "Fehlende oder abweichende Konstanten in ui/event_names.gd:" -ForegroundColor Red
  foreach ($m in $missing) {
    Write-Host (' - {0} = "{1}" erwartet' -f $m.Name, $m.Value)
  }
  throw "EventNames-Konstanten unvollstaendig."
}
else {
  Write-Host "EventNames-Datei vorhanden und vollstaendig." -ForegroundColor Green
}
