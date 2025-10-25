# SPDX-License-Identifier: MIT
# tools/ci/RunBootSelfTest.ps1
param([switch]$VerboseLog)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-GodotLauncher {
  param([string[]]$Args)

  # Kandidatenquellen: Env + Get-Command
  $candidates = @(
    $env:GODOT_BIN, $env:GODOT4_BIN, $env:GODOT4, $env:GODOT,
    (Get-Command godot4 -ErrorAction SilentlyContinue)?.Source,
    (Get-Command godot  -ErrorAction SilentlyContinue)?.Source
  ) | Where-Object { $_ } | Select-Object -Unique

  # akzeptierte Endungen
  $okExt = @('.exe','.cmd','.bat','.lnk')

  # 1) Direkter Datei-Pfad?
  foreach ($c in $candidates) {
    if (-not (Test-Path $c)) { continue }
    $it = Get-Item $c
    if ($it.PSIsContainer) {
      # rekursiv passende Datei suchen (headless bevorzugt)
      $files = Get-ChildItem -Path $it.FullName -Recurse -File -ErrorAction SilentlyContinue |
               Where-Object { $_.Name -match 'godot' -and $okExt -contains $_.Extension.ToLower() }
      $pref = $files | Where-Object { $_.Name -match 'headless' } | Select-Object -First 1
      if ($pref) { return @{ FilePath = $pref.FullName; UseCmd = ($pref.Extension -in @('.cmd','.bat')); Bare = $false } }
      if ($files) { $f = $files[0]; return @{ FilePath = $f.FullName; UseCmd = ($f.Extension -in @('.cmd','.bat')); Bare = $false } }
    } elseif ($it -is [System.IO.FileInfo]) {
      # Wenn es eine Datei ohne Extension ist (z.B. "godot"), verwende sie direkt
      # statt nach Wrapper-Dateien wie "godot.cmd" zu suchen
      if ([string]::IsNullOrEmpty($it.Extension)) {
        return @{ FilePath = $it.FullName; UseCmd = $false; Bare = $false }
      }

      if ($okExt -contains $it.Extension.ToLower()) {
        return @{ FilePath = $it.FullName; UseCmd = ($it.Extension -in @('.cmd','.bat')); Bare = $false }
      }
      # Nachbar-Varianten probieren (nur als letztes Resort)
      foreach ($name in @('godot.windows.headless.x86_64.exe','godot.exe','Godot.exe','godot.cmd','godot.bat')) {
        $p = Join-Path $it.DirectoryName $name
        if (Test-Path $p) { $fi = Get-Item $p; return @{ FilePath = $fi.FullName; UseCmd = ($fi.Extension -in @('.cmd','.bat')); Bare = $false } }
      }
    }
  }

  # 2) Fallback: über PATH als reines Kommando starten
  # -> wir geben cmd.exe + '/c godot …' zurück, damit auch .cmd/.bat sicher laufen
  $comspec = $env:ComSpec; if (-not $comspec) { $comspec = "$env:WINDIR\System32\cmd.exe" }
  return @{ FilePath = $comspec; UseCmd = $true; Bare = $true }
}

# Godot-Args
$godotArgs = @('--headless','--path','.', '--script','res://tools/ci/CheckBootSelfTest.gd')
if ($VerboseLog) { $godotArgs += '--verbose' }

# Launcher bestimmen
$launcher = Get-GodotLauncher -Args $godotArgs

# Start vorbereiten
if ($launcher.Bare) {
  # PATH-Fallback: cmd /c godot <args>
  $filePath = $launcher.FilePath
  $argList  = @('/c','godot') + $godotArgs
  Write-Host "[CI] Starte über PATH: cmd /c godot $($godotArgs -join ' ')"
} elseif ($launcher.UseCmd) {
  # .cmd/.bat direkt: ebenfalls über cmd.exe starten (robuster ExitCode/Quoting)
  $filePath = $env:ComSpec; if (-not $filePath) { $filePath = "$env:WINDIR\System32\cmd.exe" }
  $argList  = @('/c', ('"{0}"' -f $launcher.FilePath)) + $godotArgs
  Write-Host "[CI] Verwende Godot (cmd): $($launcher.FilePath)"
} else {
  # .exe/.lnk direkt
  $filePath = $launcher.FilePath
  $argList  = $godotArgs
  Write-Host "[CI] Verwende Godot: $filePath"
}

# Starten & Exitcode ermitteln
$proc = Start-Process -FilePath $filePath -ArgumentList $argList -NoNewWindow -PassThru -Wait
$exitCode = $proc.ExitCode
Write-Host "[CI] Godot ExitCode: $exitCode"

if ($exitCode -ne 0) { throw "BootSelfTest fehlgeschlagen (ExitCode=$exitCode)" }
exit $exitCode
