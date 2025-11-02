# SPDX-License-Identifier: MIT
Param(
  [string]$Root = "."
)

# Prueft, dass in .tscn Dateien keine hardcodierten NodePaths auf /root vorhanden sind
# Rueckgabe: Exit 0 wenn OK, sonst 1 mit Fehlerliste

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$files = Get-ChildItem -Path $Root -Recurse -Filter *.tscn -File
$pattern = 'NodePath\("/root/'
$violations = @()

foreach ($f in $files) {
  $matches = Select-String -Path $f.FullName -Pattern $pattern
  foreach ($m in $matches) {
    $violations += [pscustomobject]@{
      File = $m.Path
      Line = $m.LineNumber
      Text = $m.Line.Trim()
    }
  }
}

if ($violations.Count -gt 0) {
  Write-Error "Hardcodierte NodePaths auf /root in .tscn gefunden:"
  foreach ($v in $violations) {
    Write-Host (" - {0}:{1}: {2}" -f $v.File, $v.Line, $v.Text)
  }
  exit 1
}

Write-Host "OK: Keine hardcodierten /root/ NodePaths in .tscn Dateien gefunden."
exit 0

