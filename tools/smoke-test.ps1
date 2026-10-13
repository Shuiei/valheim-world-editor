# Starts the packaged Windows editor (ValheimWorldEditor.exe from the release zip) and checks it really
# works, through its test driver, like tools/smoke-test.sh does on Linux: it starts, loads SPIRV-Cross
# (spirv), takes the game's data from the tests' copies beside the world (no Valheim on CI, without
# which no world opens), opens the test world, opens an area in the 3D editor, and draws frames with no OpenGL error, through Direct3D (ANGLE).
# Usage: tools/smoke-test.ps1 <program file> <world folder>
param([Parameter(Mandatory)][string]$Program, [Parameter(Mandatory)][string]$World)
$ErrorActionPreference = 'Stop'
$Program = (Resolve-Path $Program).Path
$World = (Resolve-Path $World).Path
$Data = Split-Path $World

# CI's Windows machines start fresh: no settings, worlds or game look there (plain colours then).
$psi = [Diagnostics.ProcessStartInfo]::new($Program, '--driver')
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true
$p = [Diagnostics.Process]::Start($psi)
$err = $p.StandardError.ReadToEndAsync()
foreach ($c in @('spirv', "gamedata $Data", "world $World", 'area 0 0 1', 'wait 2000', 'state', 'quit')) { $p.StandardInput.WriteLine($c) }
$p.StandardInput.Close()
$out = $p.StandardOutput.ReadToEndAsync()
if (-not $p.WaitForExit(180000)) { $p.Kill(); throw 'smoke test: the editor did not finish within 3 minutes' }
$lines = @($out.Result -split "`r?`n" | Where-Object { $_.StartsWith('@@ ') })
# What drew the map and the 3D view (their "OpenGL ... : <renderer>" lines).
$renderers = @($out.Result -split "`r?`n" | Where-Object { $_ -match '^(map: )?OpenGL ' })
$renderers | ForEach-Object { Write-Host $_ }
$lines | ForEach-Object { if ($_.Length -gt 200) { $_.Substring(0, 200) } else { $_ } }

if ($lines -notcontains '@@ ready') { Write-Host $err.Result; throw 'smoke test: the editor never got ready' }
$errors = @($lines | Where-Object { $_.StartsWith('@@ error') })
if ($errors.Count) { throw "smoke test: $($errors[0])" }
$oks = @($lines | Where-Object { $_.StartsWith('@@ ok ') })
if (-not $oks.Count) { throw 'smoke test: no answer from the editor' }
$s = $oks[-1].Substring(6) | ConvertFrom-Json
$problems = @()
if ($s.page -ne 'editor') { $problems += "page is '$($s.page)', not the 3D editor" }
if (-not $s.frames) { $problems += 'no frame drawn' }
if ($s.glErrors) { $problems += "$($s.glErrors) OpenGL error(s)" }
if (-not ($renderers | Where-Object { $_ -match '^OpenGL ES .*ANGLE' })) { $problems += 'the 3D view was not drawn through Direct3D (ANGLE)' }
if ($problems.Count) { throw ('smoke test: ' + ($problems -join '; ')) }
Write-Host "smoke test: ok ($($s.frames) frames, $($s.objects) objects, no OpenGL error, ANGLE)"
