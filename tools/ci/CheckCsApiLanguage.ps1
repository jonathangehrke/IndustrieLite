# SPDX-License-Identifier: MIT
Param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$root = (Resolve-Path (Join-Path (Join-Path $PSScriptRoot '..') '..')).Path
$codeRoot = Join-Path $root 'code'
if (-not (Test-Path -LiteralPath $codeRoot)) {
  throw "Code-Ordner nicht gefunden: $codeRoot"
}

function Remove-CommentsAndStrings {
  Param([string]$Text)
  # Blockkommentare entfernen
  $t = [regex]::Replace($Text, '/\*.*?\*/', '', [System.Text.RegularExpressions.RegexOptions]::Singleline)
  # String-Literale entfernen (normale)
  $t = [regex]::Replace($t, '"(?:\\.|[^"\\])*"', '""')
  # Verbatim-Strings entfernen (@"...")
  $t = [regex]::Replace($t, '@"(?:""|[^"])*"', '""')
  # Zeilenkommentare entfernen
  $t = $t -replace '//.*',''
  return $t
}

$files = Get-ChildItem -Path $codeRoot -Recurse -Include *.cs -File
$violations = @()

foreach ($f in $files) {
  $raw = Get-Content -Raw -LiteralPath $f.FullName
  $text = Remove-CommentsAndStrings -Text $raw

  # 1) ResourceType-Enum (falls vorhanden): Nur ASCII-Identifier zulassen
  $rtMatches = [regex]::Matches($text, 'enum\s+ResourceType\s*\{(?<body>[^}]*)\}', [System.Text.RegularExpressions.RegexOptions]::Singleline)
  foreach ($em in $rtMatches) {
    $body = $em.Groups['body'].Value
    foreach ($part in ($body -split ',')) {
      $name = ($part -split '=')[0].Trim()
      if ([string]::IsNullOrWhiteSpace($name)) { continue }
      if ($name -notmatch '^[A-Za-z_][A-Za-z0-9_]*$') {
        $violations += [pscustomobject]@{ File=$f.FullName; Kind='resource_type_member_invalid'; Name=$name; Token='non-ascii or invalid identifier' }
      }
    }
  }

  # 2) IDs in Code-Konstruktoren pruefen (Lowercase+Underscore)
  $ctorPatterns = @(
    'new\s+BuildingDef\s*\(\s*"(?<id>[^"]+)"',
    'new\s+RecipeDef\s*\(\s*"(?<id>[^"]+)"'
  )
  foreach ($p in $ctorPatterns) {
    $idMatches = [regex]::Matches($raw, $p)
    foreach ($im in $idMatches) {
      $id = $im.Groups['id'].Value
      if ($id -notmatch '^[a-z0-9_]+$') {
        $violations += [pscustomobject]@{ File=$f.FullName; Kind='id_style_invalid'; Name=$id; Token='must match ^[a-z0-9_]+$' }
      }
    }
  }
}

if ($violations.Count -gt 0) {
  Write-Host '[CI] API/ID-Konsistenz: VERSTOESSE gefunden' -ForegroundColor Red
  foreach ($v in $violations) {
    Write-Host (" - {0}: {1} (Token: {2}) in {3}" -f $v.Kind, $v.Name, $v.Token, $v.File)
  }
  $json = @{ status='error'; violations=$violations } | ConvertTo-Json -Depth 5 -Compress
  Write-Host ('JSON_RESULT: ' + $json)
  throw "API/ID-Konsistenz verletzt"
}
else {
  Write-Host '[CI] API/ID-Konsistenz: OK' -ForegroundColor Green
  $json = @{ status='ok'; violations=@() } | ConvertTo-Json -Depth 5 -Compress
  Write-Host ('JSON_RESULT: ' + $json)
}
