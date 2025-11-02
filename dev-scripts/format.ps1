# SPDX-License-Identifier: MIT
param(
    [switch]$Verify
)

Write-Host "Running dotnet format..." -ForegroundColor Cyan

$argsList = @("format")
if ($Verify) { $argsList += "--verify-no-changes" }

dotnet @argsList

if ($LASTEXITCODE -ne 0) {
    Write-Host "dotnet format reported changes or errors." -ForegroundColor Red
    exit $LASTEXITCODE
} else {
    Write-Host "dotnet format completed successfully." -ForegroundColor Green
}

