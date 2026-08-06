#!/usr/bin/env pwsh
#
# Publishes the plugin over the local dev install so changes can be tried in Flow Launcher.
#
#   ./scripts/deploy-local.ps1              build, stop Flow, copy, restart Flow
#   ./scripts/deploy-local.ps1 -NoRestart   leave Flow stopped
#
# The dev install is a second copy of the plugin with its own plugin.json -- different
# plugin ID, and the `stt` action keyword so it can run alongside the released `st` one.
# That file is NEVER overwritten: copying the repo's plugin.json would collapse the dev
# copy onto the released plugin's ID and keyword.
#
# Per-plugin settings (including the DPAPI-encrypted API key) live under
# %APPDATA%\FlowLauncher\Settings\Plugins\<plugin id>, keyed by that ID, so they are
# outside the plugin folder and survive a deploy untouched.

param(
    [string]$PluginDir = (Join-Path $env:APPDATA 'FlowLauncher\Plugins\Flow.Launcher.Plugin.SteamLauncher.Local'),
    [switch]$NoRestart
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src\Flow.Launcher.Plugin.SteamLauncher\Flow.Launcher.Plugin.SteamLauncher.csproj'
$publishDir = Join-Path $repoRoot 'artifacts\deploy-local'

if (-not (Test-Path $PluginDir)) {
    Write-Error "Dev plugin folder not found: $PluginDir. Create it (with its own plugin.json) or pass -PluginDir."
}
if (-not (Test-Path (Join-Path $PluginDir 'plugin.json'))) {
    Write-Error "No plugin.json in $PluginDir. Refusing to deploy: the dev copy needs its own ID and action keyword."
}

Write-Host '==> Publishing' -ForegroundColor Cyan
if (Test-Path $publishDir) { Remove-Item $publishDir -Recurse -Force }
dotnet publish $project -c Release -o $publishDir
if ($LASTEXITCODE -ne 0) { Write-Error 'Publish failed.' }

# Flow holds the plugin assemblies open, so they cannot be replaced while it runs.
$flow = Get-Process -Name 'Flow.Launcher' -ErrorAction SilentlyContinue
$wasRunning = $null -ne $flow
if ($wasRunning) {
    Write-Host '==> Stopping Flow Launcher' -ForegroundColor Cyan
    $flow | Stop-Process -Force
    $flow | Wait-Process -Timeout 15 -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 500
}

Write-Host "==> Copying to $PluginDir" -ForegroundColor Cyan
Get-ChildItem -Path $publishDir -Force |
    Where-Object { $_.Name -ne 'plugin.json' } |
    ForEach-Object { Copy-Item $_.FullName -Destination $PluginDir -Recurse -Force }

$devManifest = Get-Content (Join-Path $PluginDir 'plugin.json') -Raw -Encoding UTF8 | ConvertFrom-Json
Write-Host "    kept dev manifest: '$($devManifest.Name)' keyword '$($devManifest.ActionKeyword)'" -ForegroundColor DarkGray

if ($wasRunning -and -not $NoRestart) {
    # The un-versioned stub, so this keeps working across Flow updates.
    $launcher = Join-Path $env:LOCALAPPDATA 'FlowLauncher\Flow.Launcher.exe'
    if (Test-Path $launcher) {
        Write-Host '==> Restarting Flow Launcher' -ForegroundColor Cyan
        Start-Process $launcher
    } else {
        Write-Warning "Flow launcher not found at $launcher -- start it manually."
    }
}

Write-Host "==> Done. Try it with '$($devManifest.ActionKeyword)'." -ForegroundColor Green
