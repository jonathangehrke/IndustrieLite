# SPDX-License-Identifier: MIT
# CheckNoRuntimeServiceLocator.ps1
# Phase 7: CI Script to verify NO ServiceContainer.Instance runtime lookups in manager logic
# Ensures explicit DI pattern is enforced - only self-registration in _Ready is allowed

param(
    [string]$RootPath = (Resolve-Path "$PSScriptRoot/../..").Path,
    [switch]$Verbose
)

Write-Host "=== DI Pattern Enforcement Check ===" -ForegroundColor Cyan
Write-Host "Checking for runtime ServiceContainer lookups in managers..." -ForegroundColor Cyan
Write-Host ""

$exitCode = 0

# Patterns to find (runtime lookups that violate explicit DI)
$violationPatterns = @(
    "ServiceContainer\.Instance\?\.(GetNamedService|GetService|WaitForService|WaitForNamedService)",
    "ServiceContainer\.Instance\.(GetNamedService|GetService|WaitForService|WaitForNamedService)"
)

# Files to scan
$scanPaths = @(
    "$RootPath\code\managers\*.cs",
    "$RootPath\code\runtime\*.cs"
)

# Exclusions - these are allowed patterns
# 1. Initialize.cs files (DI setup)
# 2. ServiceContainer.cs itself
# 3. DIContainer.cs (composition root)
# 4. Lines containing RegisterNamedService (self-registration is OK)
# 5. Comments
$excludePatterns = @(
    "Initialize\.cs$",
    "ServiceContainer\.cs$",
    "DIContainer\.cs$",
    "BootSelfTest\.cs$",
    "GameLifecycleManager\.cs$"
)

$violations = @()

foreach ($scanPath in $scanPaths) {
    if ($Verbose) {
        Write-Host "Scanning: $scanPath" -ForegroundColor Gray
    }

    $files = Get-ChildItem -Path $scanPath -File -ErrorAction SilentlyContinue

    foreach ($file in $files) {
        # Check if file should be excluded
        $shouldExclude = $false
        foreach ($excludePattern in $excludePatterns) {
            if ($file.Name -match $excludePattern) {
                $shouldExclude = $true
                if ($Verbose) {
                    Write-Host "  Skipping (excluded): $($file.Name)" -ForegroundColor DarkGray
                }
                break
            }
        }

        if ($shouldExclude) {
            continue
        }

        # Read file content
        $content = Get-Content -Path $file.FullName -Raw

        # Check each violation pattern
        foreach ($pattern in $violationPatterns) {
            # Use Select-String for line-by-line matching
            $matches = Select-String -Path $file.FullName -Pattern $pattern -AllMatches

            foreach ($match in $matches) {
                $line = $match.Line.Trim()

                # Skip if it's self-registration (RegisterNamedService)
                if ($line -match "RegisterNamedService") {
                    continue
                }

                # Skip if it's a comment
                if ($line -match "^\s*//") {
                    continue
                }

                # Skip if it's in a comment block or string
                if ($line -match "/\*" -or $line -match "\*/") {
                    continue
                }

                # This is a violation!
                $violations += [PSCustomObject]@{
                    File = $file.Name
                    Line = $match.LineNumber
                    Code = $line
                }

                if ($Verbose) {
                    Write-Host "  VIOLATION in $($file.Name):$($match.LineNumber)" -ForegroundColor Red
                    Write-Host "    $line" -ForegroundColor Yellow
                }
            }
        }
    }
}

# Report results
Write-Host ""
if ($violations.Count -eq 0) {
    Write-Host "[SUCCESS] No runtime ServiceContainer lookups found in managers!" -ForegroundColor Green
    Write-Host "  All managers follow explicit DI pattern." -ForegroundColor Green
    $exitCode = 0
} else {
    Write-Host "[FAILURE] Found $($violations.Count) runtime ServiceContainer lookup(s)!" -ForegroundColor Red
    Write-Host ""
    Write-Host "Violations: managers should use explicit DI via Initialize method" -ForegroundColor Yellow
    Write-Host ""

    foreach ($violation in $violations) {
        Write-Host "  File: $($violation.File)" -ForegroundColor Cyan
        Write-Host "  Line: $($violation.Line)" -ForegroundColor Gray
        Write-Host "  Code: $($violation.Code)" -ForegroundColor Yellow
        Write-Host ""
    }

    Write-Host "Fix: Replace ServiceContainer runtime lookups with explicit Initialize parameters" -ForegroundColor Magenta
    Write-Host "See: docs/DI-POLICY.md for guidelines" -ForegroundColor Magenta
    $exitCode = 1
}

Write-Host ""
Write-Host "=== Check Complete ===" -ForegroundColor Cyan

exit $exitCode
