# SPDX-License-Identifier: MIT
param(
    [switch]$FailOnViolation = $true,
    [string[]]$Allowlist = @()
)

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$repo = Resolve-Path (Join-Path $root "..\..")
$files = @()
if (Test-Path (Join-Path $repo "code\managers")) {
  $files += Get-ChildItem -Path (Join-Path $repo "code\managers") -Filter *.cs -Recurse | Where-Object { $_.Name -notlike "*.uid" }
}
if (Test-Path (Join-Path $repo "code\buildings")) {
  $files += Get-ChildItem -Path (Join-Path $repo "code\buildings") -Filter *.cs -Recurse | Where-Object { $_.Name -notlike "*.uid" }
}
if (Test-Path (Join-Path $repo "code\input")) {
  $files += Get-ChildItem -Path (Join-Path $repo "code\input") -Filter *.cs -Recurse | Where-Object { $_.Name -notlike "*.uid" }
}
if (Test-Path (Join-Path $repo "code\runtime\ui")) {
  $files += Get-ChildItem -Path (Join-Path $repo "code\runtime\ui") -Filter *.cs -Recurse | Where-Object { $_.Name -notlike "*.uid" }
}

$patterns = @(
    'ServiceContainer\.Instance',
    '\.GetNamedService<',
    '\.GetService<'
)

$violations = @()

foreach ($file in $files) {
    if ($Allowlist -contains $file.Name) { continue }
    $text = Get-Content -Raw -Path $file.FullName
    foreach ($pat in $patterns) {
        if ($text -match $pat) {
            $violations += [pscustomobject]@{ File = $file.FullName; Pattern = $pat }
        }
    }
}

if ($violations.Count -gt 0) {
    Write-Warning "Service Locator usage detected in manager files:"
    $violations | ForEach-Object { Write-Host " - $($_.File) matches '$($_.Pattern)'" }
    if ($FailOnViolation) { exit 1 } else { exit 0 }
}
else {
    Write-Host "No Service Locator usage found in manager files."; exit 0
}
