# SPDX-License-Identifier: MIT
# tools/ci/CheckManagerNoNodePath.ps1
$ErrorActionPreference = 'Stop'

function Remove-CommentsAndStrings([string]$code) {
  $code = [regex]::Replace($code, '/\*.*?\*/', '', 'Singleline') # /* ... */
  $code = [regex]::Replace($code, '//.*$', '', 'Multiline')      # // ...
  $code = [regex]::Replace($code, '@"([^"]|"")*"', '""')         # @"..."
  $code = [regex]::Replace($code, '"([^"\\]|\\.)*"', '""')       # "..."
  $code = [regex]::Replace($code, '''([^''\\]|\\.)*''', "''")    # '...'
  return $code
}

$root = Join-Path $PSScriptRoot '..\..\code\managers'
$files = Get-ChildItem -Path $root -Filter *.cs -Recurse

# Verbotene Muster (direkte Godot-Kopplung)
$forbidden = @(
  '(?<![A-Za-z0-9_\.])AddChild\s*\(',      # nacktes AddChild(
  '(?:^|\W)this\s*\.\s*AddChild\s*\(',     # this.AddChild(
  '(?:^|\W)base\s*\.\s*AddChild\s*\(',     # base.AddChild(
  'GetNode\s*\(',
  '\$[A-Za-z_]\w*',
  '\bNodePath\b',
  'GetTree\s*\(',
  'GetParent\s*\('
)

# Optional: Whitelist-Datei (Pfadfragmente, ein Eintrag pro Zeile)
$whitelist = @()
$wlPath = Join-Path $PSScriptRoot 'NoNodePathWhitelist.txt'
if (Test-Path $wlPath) {
  $whitelist = Get-Content $wlPath | Where-Object { $_ -and -not $_.Trim().StartsWith('#') }
}

$results = @()

foreach ($f in $files) {
  $full = $f.FullName
  if ($whitelist | Where-Object { $full -like "*$_*" }) { continue }

  $raw  = Get-Content $full -Raw
  $code = Remove-CommentsAndStrings $raw

  # Alle ISceneGraph-Variablen in der Datei sammeln (erlaubte Präfixe)
  $allowedPrefixes = @()
  $decl = [regex]::Matches($raw, '\bISceneGraph\s+([A-Za-z_]\w*)')
  foreach ($m in $decl) { $allowedPrefixes += $m.Groups[1].Value }
  # gängige Namen zusätzlich erlauben
  $allowedPrefixes += @('sceneGraph','pendingSceneGraph')

  # 1) Alle verbotenen Treffer einsammeln
  foreach ($pat in $forbidden) {
    $matches = [regex]::Matches($code, $pat)
    foreach ($m in $matches) {
      $lineNum = ($raw.Substring(0,[Math]::Min($m.Index,$raw.Length))).Split("`n").Count
      $line    = ($raw.Split("`n")[$lineNum-1]).Trim()

      # 2) Falls es ein qualifizierter Aufruf X.AddChild( ist: X prüfen
      $skip = $false
      if ($line -match '([A-Za-z_]\w*)\s*\.\s*AddChild\s*\(') {
        $qual = $Matches[1]
        if ($allowedPrefixes -contains $qual) { $skip = $true }
      }

      if (-not $skip) {
        $results += [pscustomobject]@{
          Path       = $full
          LineNumber = $lineNum
          Match      = $m.Value
          Line       = $line
        }
      }
    }
  }
}

if ($results.Count -gt 0) {
  Write-Error "Verbotene Node/NodePath-Aufrufe in Managern gefunden:`n" -ErrorAction Continue
  $results | Sort-Object Path, LineNumber | Format-Table Path, LineNumber, Match, Line -AutoSize
  exit 1
} else {
  Write-Host "OK: Keine Godot-Kopplung in Managern gefunden."
  exit 0
}
