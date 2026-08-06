# Steam Launcher

A Flow Launcher plugin (C#, `net9.0-windows`) that drives Steam from the `st` keyword.

## Commits

- **Subject line only.** No body, ever — put the reasoning in the code as a comment, or in `CHANGELOG.md` if users would care.
- **No AI attribution.** No `Co-Authored-By`, no "Generated with", no tool footers.
- Conventional-commit prefixes: `feat`, `fix`, `docs`, `refactor`, `perf`, `test`, `chore`, `ci`, `build`.
- Work on a branch; `main` merges via PR.

## Build and test

The repo lives on the Windows filesystem and targets `net9.0-windows`, so builds need
Windows tooling. If `.exe` files fail with `exec format error`, the `WSLInterop` binfmt
handler is missing — see `~/.claude/rules/environment.md`. Without it, invoke WSL's interop
launcher directly, which needs no root:

```bash
winps "cd 'C:\Users\Hyun\Documents\coding\steam-launcher'; dotnet build src\Flow.Launcher.Plugin.SteamLauncher\Flow.Launcher.Plugin.SteamLauncher.csproj -c Release"
winps "cd 'C:\Users\Hyun\Documents\coding\steam-launcher'; dotnet test src\Flow.Launcher.Plugin.SteamLauncher.Tests\Flow.Launcher.Plugin.SteamLauncher.Tests.csproj -c Release"
```

`winps` is `~/.local/bin/winps`, a one-line wrapper around
`/init <powershell.exe> -NoProfile -ExecutionPolicy Bypass -Command`. Routing `dotnet`
through PowerShell rather than calling `dotnet.exe` via `/init` is deliberate: `/init`
does not pass arguments through to `dotnet.exe` correctly and it just prints usage.

With the binfmt handler present, `dotnet.exe build ...` works directly from bash.

`Directory.Build.props` sets `TreatWarningsAsErrors` with `AnalysisMode=All`, so analyzer
warnings break the build. Check its `NoWarn` list before suppressing anything new.

## Trying changes in Flow Launcher

**After any user-visible change, deploy to the local dev install so it can actually be
tried** — don't leave the plugin folder stale:

```bash
winps "cd 'C:\Users\Hyun\Documents\coding\steam-launcher'; ./scripts/deploy-local.ps1"
```

That publishes, stops Flow (it holds the assemblies open), copies, and restarts Flow.

The dev install is a **second copy** of the plugin at
`%APPDATA%\FlowLauncher\Plugins\Flow.Launcher.Plugin.SteamLauncher.Local`, with its own
`plugin.json` — a different plugin ID and the **`stt`** action keyword — so it runs beside
the released `st` plugin. The deploy script never overwrites that file; doing so would
collapse the dev copy onto the released plugin's ID and keyword. Per-plugin settings
(including the encrypted API key) are keyed by plugin ID under
`%APPDATA%\FlowLauncher\Settings\Plugins\<id>`, so they live outside the plugin folder and
survive deploys.

## Writing PowerShell scripts

Keep `.ps1` files **ASCII-only**. Windows PowerShell 5.1 reads BOM-less UTF-8 scripts as
ANSI, so a UTF-8 em dash (`E2 80 94`) decodes to `â€”` — and `0x94` becomes a smart quote,
which PowerShell treats as a string delimiter and the script fails to parse. For the same
reason, always pass `-Encoding UTF8` to `Get-Content` when a script reads a UTF-8 file
such as `CHANGELOG.md`, or non-ASCII characters are silently corrupted.

## Releasing

`Version` in `src/Flow.Launcher.Plugin.SteamLauncher/plugin.json` is the single source of
truth. Bump it, add the matching `## [x.y.z] - YYYY-MM-DD` section to `CHANGELOG.md`, and
merge to `main` — the workflow tags and publishes from there. CI fails if the version has
no changelog entry. Keep the bump as the last commit of a release cycle, since merging it
is what ships. Flow's plugin manifest updates itself from the GitHub release; nothing to
do there.

## Layout

- `Query/` — `QueryParser` maps raw input to a `ParsedQuery`; `QueryDispatcher` turns that
  into rows. Every command lands here, which is why the dispatcher is the largest file.
- `Services/` — one interface + implementation per capability, all injected.
- `Steam/` — registry, path resolution, process control, the Web API client.
- `Vdf/` — parser and writer for Steam's config format.
- `Cache/` — TTL cache with per-domain policies (`CachePolicies`) and disk persistence.
- `Main.cs` — composition root; no logic beyond wiring.

Steam's real files are readable at `/mnt/c/Program Files (x86)/Steam/` — check
`steamapps/appmanifest_*.acf` before guessing at manifest fields or `StateFlags` values.
