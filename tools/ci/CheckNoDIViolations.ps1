# SPDX-License-Identifier: MIT
# CheckNoDIViolations.ps1
# Prüft DI-Richtlinien für Manager-Code

param(
    [switch]$FailOnViolation = $false
)

$ErrorActionPreference = 'Continue'
$violations = @()

Write-Host "=== Checking DI Violations in code/managers/ ===" -ForegroundColor Cyan

# Pattern 1: ServiceContainer.Instance Aufrufe in Manager-Code (sollten nicht mehr da sein nach Migration)
Write-Host "`nChecking for ServiceContainer.Instance usage in managers..." -ForegroundColor Yellow
$managerFiles = Get-ChildItem -Path "code/managers" -Filter "*.cs" -Recurse -ErrorAction SilentlyContinue

foreach ($file in $managerFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content -match 'ServiceContainer\.Instance') {
        # Exclude comments
        $lines = Get-Content $file.FullName
        $lineNum = 0
        foreach ($line in $lines) {
            $lineNum++
            if ($line -match 'ServiceContainer\.Instance' -and $line -notmatch '^\s*//') {
                $violations += "  $($file.Name):$lineNum - ServiceContainer.Instance call found (should use DI instead)"
            }
        }
    }
}

# Pattern 2: GetService<T>() Aufrufe (typed resolution - deprecated)
Write-Host "`nChecking for GetService<T>() calls in managers..." -ForegroundColor Yellow
foreach ($file in $managerFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content -match '\.GetService<') {
        $lines = Get-Content $file.FullName
        $lineNum = 0
        foreach ($line in $lines) {
            $lineNum++
            if ($line -match '\.GetService<' -and $line -notmatch '^\s*//' -and $line -notmatch 'Obsolete') {
                $violations += "  $($file.Name):$lineNum - GetService<T>() call found (use Initialize() DI instead)"
            }
        }
    }
}

# Pattern 3: Self-Registration in _Ready() (Manager sollen sich nicht selbst registrieren)
Write-Host "`nChecking for self-registration in _Ready()..." -ForegroundColor Yellow
foreach ($file in $managerFiles) {
    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    if ($content -match 'public override void _Ready\(\)') {
        $lines = Get-Content $file.FullName
        $inReady = $false
        $braceCount = 0
        $lineNum = 0
        foreach ($line in $lines) {
            $lineNum++
            if ($line -match 'public override void _Ready\(\)') {
                $inReady = $true
                $braceCount = 0
            }
            if ($inReady) {
                $braceCount += ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
                $braceCount -= ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count

                if ($line -match 'RegisterService|RegisterNamedService' -and $line -notmatch '^\s*//') {
                    $violations += "  $($file.Name):$lineNum - Self-registration in _Ready() found (should be done by DIContainer)"
                }

                if ($braceCount -le 0 -and $line -match '}') {
                    $inReady = $false
                }
            }
        }
    }
}

# Pattern 4: Fehlende Initialize()-Methode (alle Manager sollten eine haben)
Write-Host "`nChecking for missing Initialize() methods..." -ForegroundColor Yellow
$managersWithoutInit = @()
foreach ($file in $managerFiles) {
    # Skip partials that are specifically for Initialize
    if ($file.Name -match '\.Initialize\.cs$') {
        continue
    }

    $content = Get-Content $file.FullName -Raw -ErrorAction SilentlyContinue
    $isManager = $content -match 'public partial class \w+Manager\s*:\s*Node'

    if ($isManager) {
        # Check if there's an Initialize method in this file or a companion .Initialize.cs file
        $baseName = $file.BaseName
        $initFile = Join-Path $file.DirectoryName "$baseName.Initialize.cs"
        $hasInitInFile = $content -match 'public void Initialize\('
        $hasInitFile = Test-Path $initFile

        if (-not $hasInitInFile -and -not $hasInitFile) {
            $managersWithoutInit += $file.Name
        }
    }
}

if ($managersWithoutInit.Count -gt 0) {
    foreach ($managerName in $managersWithoutInit) {
        $violations += "  $managerName - Missing Initialize() method (all managers should have explicit DI)"
    }
}

# Pattern 5: GetNode() calls in Manager constructors/fields (sollte nur in Initialize sein)
Write-Host "`nChecking for GetNode() in constructors/fields..." -ForegroundColor Yellow
foreach ($file in $managerFiles) {
    $lines = Get-Content $file.FullName
    $lineNum = 0
    $inConstructor = $false
    $braceCount = 0
    foreach ($line in $lines) {
        $lineNum++

        # Check for field initialization with GetNode
        if ($line -match '^\s*private\s+\w+.*=.*GetNode' -and $line -notmatch '^\s*//') {
            $violations += "  $($file.Name):$lineNum - GetNode in field initializer (should be in Initialize())"
        }

        # Track if we're in a constructor
        if ($line -match 'public \w+Manager\(\)') {
            $inConstructor = $true
            $braceCount = 0
        }
        if ($inConstructor) {
            $braceCount += ($line.ToCharArray() | Where-Object { $_ -eq '{' }).Count
            $braceCount -= ($line.ToCharArray() | Where-Object { $_ -eq '}' }).Count

            if ($line -match 'GetNode' -and $line -notmatch '^\s*//') {
                $violations += "  $($file.Name):$lineNum - GetNode in constructor (should be in Initialize())"
            }

            if ($braceCount -le 0 -and $line -match '}') {
                $inConstructor = $false
            }
        }
    }
}

# Ergebnisse ausgeben
Write-Host "`n=== Results ===" -ForegroundColor Cyan
if ($violations.Count -eq 0) {
    Write-Host "No DI violations found!" -ForegroundColor Green
    exit 0
} else {
    Write-Host "Found $($violations.Count) DI violation(s):" -ForegroundColor Red
    foreach ($v in $violations) {
        Write-Host $v -ForegroundColor Yellow
    }

    if ($FailOnViolation) {
        Write-Host "`nBuild failed due to DI violations" -ForegroundColor Red
        exit 1
    } else {
        Write-Host "`nViolations found but not failing build (use -FailOnViolation to enforce)" -ForegroundColor Yellow
        exit 0
    }
}
