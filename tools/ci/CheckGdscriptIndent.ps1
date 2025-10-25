# SPDX-License-Identifier: MIT
Param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Test-GdIndentConsistency {
  Param(
    [string]$Path
  )
  $lines = Get-Content -LiteralPath $Path -ErrorAction Stop
  $hasTabIndent = $false
  $hasSpaceIndent = $false
  $spaceLines = @()
  foreach ($i in 0..($lines.Count-1)) {
    $line = $lines[$i]
    if ($line -match '^[\t]+\S') { $hasTabIndent = $true }
    if ($line -match '^[ ]+\S') { $hasSpaceIndent = $true; $spaceLines += ($i+1) }
  }
  if ($hasTabIndent -and $hasSpaceIndent) {
    return [pscustomobject]@{ File=$Path; Lines=$spaceLines }
  }
  return $null
}

$violations = @()
$files = Get-ChildItem -Path ui -Recurse -Include *.gd -File -ErrorAction SilentlyContinue
foreach ($f in $files) {
  $v = Test-GdIndentConsistency -Path $f.FullName
  if ($v) { $violations += $v }
}

if ($violations.Count -gt 0) {
  Write-Host "Mixed indentation detected in GDScript files (tabs + spaces):" -ForegroundColor Red
  foreach ($v in $violations) {
    $linePreview = ($v.Lines | Select-Object -First 5) -join ', '
    Write-Host (" - {0} (first space-indented lines: {1})" -f $v.File, $linePreview)
  }
  throw "Fix indentation: Use tabs consistently or spaces consistently per file."
}
else {
  Write-Host "GDScript indentation check passed (no mixed tabs/spaces)." -ForegroundColor Green
}

