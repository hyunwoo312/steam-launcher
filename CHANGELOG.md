# Changelog

All notable changes to Steam Launcher are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.2.0] - 2026-08-06

### Added

- `st <name>` now searches your whole Steam library, not just what's installed. Games you own but haven't installed appear with their playtime and when you last played, and pressing Enter installs them. This comes from your cached library, so it is instant and works offline.
- `st friends` now starts with an "Open Steam friends list" row, so the friends window is one keystroke away — and it tells you how many friends are online and in game.
- `st bigpicture` opens Big Picture mode, and offers to exit it when it is already open. `st screenshots` opens the screenshot manager, and `st redeem` opens the product-key dialog.

### Fixed

- Steam data cached on disk — store results, review scores, release details, your owned games and friends list — now survives a restart instead of being silently discarded and refetched.
- Large libraries no longer fire hundreds of simultaneous Steam requests on a single query, which could trip Steam's rate limit and leave rows without review scores until it lapsed.
- Games being uninstalled, or whose files Steam reports as missing, are now labelled as such instead of appearing as ordinary launchable games. Fully uninstalled games no longer appear at all.
- A game installed while Flow is running now picks up its Steam icon, rather than falling back to a generic store image until Flow restarts.
- Filtering the library shows a consistent number of results, instead of briefly showing ten and then expanding to every match.
- Searching while offline says so, instead of returning nothing.

## [1.1.1] - 2026-06-19

### Added

- Store search shows prices in the currency of your configured region.
- An offline indicator appears when the network is unavailable, instead of empty results.

### Changed

- The library auto-refreshes when a game is installed or uninstalled — no Flow restart needed.
- Steam API failures log a clearer reason, with the API key redacted.

## [1.1.0] - 2026-06-07

### Added

- `st uninstall [name]` opens Steam's uninstall prompt for an installed game.
- Uninstall is available on the right-click menu of any installed game.
- Game, friend, profile, multiplayer, and account rows carry preview-panel details.

### Changed

- Installed games render immediately while Steam metadata, friends-playing info, and store results load in the background.
- `st switch` lists the current account first instead of hiding it.
- The right-click menu adapts to whether a row is an installed game or a store game.

## [1.0.0] - 2026-04-29

### Added

- Initial release: launch installed games, search the Steam store, manage friends, switch accounts, set status, and find shared multiplayer games.
- `st` lists installed games sorted by most recently played, with playtime, review summary, install size, and update status on each row.
- `st me`, `st new`, `st friends`, `st multi`, `st status`, `st switch`, `st verify`, `st settings`, and `st downloads`.
- `st api` guided wizard for the Steam Web API key, stored encrypted with Windows DPAPI.
