#!/usr/bin/env pwsh
#
# CHANGELOG.md helper.
#
#   ./scripts/changelog.ps1 -Check                 Verify CHANGELOG.md has an entry for the
#                                                  version currently in plugin.json.
#   ./scripts/changelog.ps1 -ReleaseBody 1.2.0     Print that version's entry body, for use
#                                                  as GitHub release notes.
#
# plugin.json's Version is the single source of truth: the release workflow derives the
# git tag from it, and Flow's plugin manifest bot derives the user-visible version from
# that tag.

param(
    [switch]$Check,
    [string]$ReleaseBody
)

Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$changelogPath = Join-Path $repoRoot 'CHANGELOG.md'
$pluginJsonPath = Join-Path $repoRoot 'src/Flow.Launcher.Plugin.SteamLauncher/plugin.json'

function Write-Failure([string]$message) {
    [Console]::Error.WriteLine($message)
    exit 1
}

function Get-PluginVersion {
    if (-not (Test-Path $pluginJsonPath)) { Write-Failure "Not found: $pluginJsonPath" }
    $version = (Get-Content $pluginJsonPath -Raw -Encoding UTF8 | ConvertFrom-Json).Version
    if ([string]::IsNullOrWhiteSpace($version)) { Write-Failure 'plugin.json has no Version field.' }
    return $version
}

# Returns the release section as an object with its heading line and body, or $null when
# CHANGELOG.md has no section for that version.
function Get-ReleaseSection([string]$version) {
    if (-not (Test-Path $changelogPath)) { Write-Failure "Not found: $changelogPath" }

    $normalized = $version -replace '^v', ''
    $heading = "## [$normalized]"
    # -Encoding UTF8 is required: Windows PowerShell 5.1 otherwise decodes as ANSI and
    # mangles every non-ASCII character on the way into the release notes.
    # Strip any trailing CR so CRLF checkouts compare and render the same as LF ones.
    $lines = @(Get-Content $changelogPath -Encoding UTF8) -replace '\r$', ''

    $start = -1
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith($heading, [StringComparison]::Ordinal)) { $start = $i; break }
    }
    if ($start -lt 0) { return $null }

    $end = $lines.Count
    for ($i = $start + 1; $i -lt $lines.Count; $i++) {
        if ($lines[$i].StartsWith('## [', [StringComparison]::Ordinal)) { $end = $i; break }
    }

    $body = if ($end -gt $start + 1) { ($lines[($start + 1)..($end - 1)] -join "`n").Trim() } else { '' }

    return [pscustomobject]@{
        Heading = $lines[$start]
        Body    = $body
    }
}

if (-not [string]::IsNullOrWhiteSpace($ReleaseBody)) {
    $section = Get-ReleaseSection $ReleaseBody
    if ($null -eq $section) { Write-Failure "No release notes found for version `"$ReleaseBody`"." }
    if ([string]::IsNullOrWhiteSpace($section.Body)) { Write-Failure "Release notes for version `"$ReleaseBody`" are empty." }
    Write-Output $section.Body
    exit 0
}

if ($Check) {
    $version = Get-PluginVersion
    $section = Get-ReleaseSection $version

    if ($null -eq $section) {
        Write-Failure "CHANGELOG.md has no `"## [$version]`" section. Add one for the version in plugin.json before releasing."
    }
    if ([string]::IsNullOrWhiteSpace($section.Body)) {
        Write-Failure "The `"## [$version]`" section in CHANGELOG.md is empty. Describe what changed."
    }
    if ($section.Heading -notmatch '^## \[\d+\.\d+\.\d+\] - \d{4}-\d{2}-\d{2}\s*$') {
        Write-Failure "Malformed heading: `"$($section.Heading)`". Expected `"## [$version] - YYYY-MM-DD`"."
    }

    [Console]::Error.WriteLine("CHANGELOG.md has an entry for $version.")
    exit 0
}

[Console]::Error.WriteLine('Usage: changelog.ps1 -Check | -ReleaseBody <version>')
exit 1
