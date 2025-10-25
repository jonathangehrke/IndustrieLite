# SPDX-License-Identifier: MIT
Param(
  [string]$RepoRoot = $(Resolve-Path "..").Path
)

# Simple guard to discourage direct legacy API usage in app code
# Allowed files: TransportManager.cs (wrapper), TransportCoordinator.cs (wrapper), TransportOrderManager.cs (owner)
$patterns = @(
  'AcceptOrder\(',
  'StartPeriodicSupplyRoute\(',
  'StopPeriodicSupplyRoute\('
)

$allowed = @(
  'code/managers/TransportManager.cs',
  'code/transport/TransportCoordinator.cs',
  'code/transport/managers/TransportOrderManager.cs'
)

$fail = $false
foreach ($pat in $patterns) {
  $hits = rg -n --no-heading -S $pat "$RepoRoot" | Where-Object { $_ -notmatch 'tests/|\.backup' }
  foreach ($line in $hits) {
    $rel = $line.Split(':')[0]
    if ($allowed -notcontains $rel) {
      Write-Host "Forbidden API usage: $line" -ForegroundColor Red
      $fail = $true
    }
  }
}

if ($fail) {
  Write-Host "Forbidden API usage detected. Use Try*-APIs with Result." -ForegroundColor Red
  exit 1
} else {
  Write-Host "No forbidden transport API usages found." -ForegroundColor Green
}

