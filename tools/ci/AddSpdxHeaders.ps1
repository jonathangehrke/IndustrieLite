# Adds SPDX-License-Identifier: MIT headers to source files.
# - C#:    // SPDX-License-Identifier: MIT
# - GDScript (.gd): # SPDX-License-Identifier: MIT
# - Godot shader (.gdshader): // SPDX-License-Identifier: MIT
# - PowerShell (.ps1/.psm1): # SPDX-License-Identifier: MIT
# - Shell (.sh): # SPDX-License-Identifier: MIT (after shebang if present)

param(
    [switch]$WhatIf
)

$ErrorActionPreference = 'Stop'

function Add-SpdxHeader {
    param(
        [Parameter(Mandatory)] [string] $Path
    )

    $ext = [System.IO.Path]::GetExtension($Path).ToLowerInvariant()
    $prefix = switch ($ext) {
        '.cs'       { '// ' }
        '.gd'       { '# ' }
        '.gdshader' { '// ' }
        '.ps1'      { '# ' }
        '.psm1'     { '# ' }
        '.sh'       { '# ' }
        default     { $null }
    }
    if (-not $prefix) { return $false }

    $content = Get-Content -Raw -Encoding UTF8 $Path
    if ($content -match 'SPDX-License-Identifier\s*:\s*MIT') {
        return $false
    }

    $spdx = "${prefix}SPDX-License-Identifier: MIT"

    # shebang handling for shell scripts
    if ($ext -eq '.sh' -and $content.StartsWith('#!')) {
        # Insert SPDX after first line
        $idx = $content.IndexOf("`n")
        if ($idx -ge 0) {
            $before = $content.Substring(0, $idx + 1)
            $after  = $content.Substring($idx + 1)
            $newContent = $before + $spdx + "`r`n" + $after
        } else {
            $newContent = $content + "`r`n" + $spdx + "`r`n"
        }
    }
    else {
        $newContent = $spdx + "`r`n" + $content
    }

    if ($WhatIf) {
        Write-Host "[DRY-RUN] Would update: $Path" -ForegroundColor Yellow
    } else {
        Set-Content -Encoding UTF8 -NoNewline -Path $Path -Value $newContent
        Write-Host "[UPDATED] $Path" -ForegroundColor Green
    }
    return $true
}

# Collect files
$all = rg --files -S | ForEach-Object { $_.Trim() }
$all = $all | Where-Object { $_ -notmatch '^\.godot/' -and $_ -notmatch '^Godot_v' }

$targets = @()
$targets += $all | Where-Object { $_ -match '\.(cs)$' }
$targets += $all | Where-Object { $_ -match '\.(gd|gdshader)$' }
$targets += $all | Where-Object { $_ -match '\.(ps1|psm1)$' }
$targets += $all | Where-Object { $_ -match '\.(sh)$' }

$updated = 0
foreach ($p in $targets) {
    $changed = Add-SpdxHeader -Path $p
    if ($changed) { $updated++ }
}

Write-Host "Done. Updated files: $updated / $($targets.Count)" -ForegroundColor Cyan

