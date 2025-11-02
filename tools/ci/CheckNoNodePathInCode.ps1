# SPDX-License-Identifier: MIT
Param(
  [string]$Root = "."
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Scans all C# files under code/ and fails if NodePath appears in code (comments/strings stripped)

function Remove-CommentsAndStrings {
  Param([string]$Text)
  $t = [regex]::Replace($Text, '/\*.*?\*/', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
  $t = [regex]::Replace($t, '"(?:\\.|[^"\\])*"', '""')
  $t = [regex]::Replace($t, '@"(?:""|[^"])*"', '""')
  $t = $t -replace '//.*',''
  return $t
}

$codeRoot = Join-Path $Root 'code'
if (-not (Test-Path -LiteralPath $codeRoot)) { throw "Code-Ordner nicht gefunden: $codeRoot" }

$files = Get-ChildItem -Path $codeRoot -Recurse -Include *.cs -File
$violations = @()

foreach ($f in $files) {
  $raw = Get-Content -Raw -LiteralPath $f.FullName
  $text = Remove-CommentsAndStrings -Text $raw

  if ($text -match '\bNodePath\b') {
    $violations += [pscustomobject]@{ File=$f.FullName; Kind='NodePath_forbidden' }
  }
}

if ($violations.Count -gt 0) {
  Write-Host '[CI] NodePath-Verbote: VERSTOESSE gefunden' -ForegroundColor Red
  foreach ($v in $violations) {
    Write-Host (" - {0}: {1}" -f $v.Kind, $v.File)
  }
  $json = @{ status='error'; violations=$violations } | ConvertTo-Json -Depth 5 -Compress
  Write-Host ('JSON_RESULT: ' + $json)
  exit 1
}
else {
  Write-Host '[CI] NodePath-Verbote: OK' -ForegroundColor Green
  $json = @{ status='ok'; violations=@() } | ConvertTo-Json -Depth 5 -Compress
  Write-Host ('JSON_RESULT: ' + $json)
}

