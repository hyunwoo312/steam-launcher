# Steam Launcher

<p align="center">
  <img src="src/Flow.Launcher.Plugin.SteamLauncher/Images/steam.png" width="128" alt="Steam Launcher">
</p>

<p align="center">
  <img src="https://img.shields.io/github/downloads/hyunwoo312/steam-launcher/total?style=flat-square&color=black&labelColor=blue">
  <img src="https://img.shields.io/github/stars/hyunwoo312/steam-launcher?style=flat-square&color=black&labelColor=blue">
  <img src="https://img.shields.io/github/last-commit/hyunwoo312/steam-launcher?style=flat-square&color=black&labelColor=blue">
  <img src="https://img.shields.io/github/v/release/hyunwoo312/steam-launcher?style=flat-square&color=black&labelColor=blue">
</p>

### A native C# Steam plugin for [Flow Launcher](https://www.flowlauncher.com/)

Launch games, search the store, manage friends, switch accounts, set status, and find shared multiplayer games — all from one keystroke.

<img src=".github/screenshots/library.png" width="600">

## Commands

```
st                       List installed games (sorted by recently played)
st <name>                Filter library, falls through to store search
st api                   Configure Steam Web API key + Steam ID (guided wizard)
st me                    Profile summary (level, owned, playtime)
st new                   Owned games never played
st friends               Online friends, favorites pinned
st friends <name>        Filter friends by name
st status                Set status: Online / Invisible / Offline
st switch                Switch the active Steam account
st multi <friend>        Shared multiplayer games with a friend
st verify [name]         Verify integrity of a game's files
st settings              Open the Steam settings window
st downloads             Open the Steam download manager
```

## Features

### Local library

- Launches installed games directly via Steam (handles update-then-launch automatically)
- Shows playtime · last 2 weeks playtime · last-played · review summary · install size · update-status badge
- Surfaces "(N friends playing)" when any of your friends are currently in that game
- Sorted by most recently played

### Steam Store search

- Falls in seamlessly when you type a game name that isn't installed (up to 10 combined results)
- Shows review summary · price · discount · release date · developer
- Owned-but-not-installed rows route directly to your library page

### Friends

- Online friends sorted in-game / online / offline, with status emoji + current game
- Favorites are pinned to the top with a ⭐ marker
- `st friends <name>` fuzzy-filters by persona name
- Enter on a friend opens DM (Steam comes to the foreground via the AttachThreadInput trick)
- Right-click for the full context menu

### Adaptive context menu

- **On a game:** Launch · Open store · Community guides · Community discussions · Game properties · Open game folder
- **On a friend:** DM · Invite to game · Join game · Open profile · Favorite/Unfavorite (refreshes the list in place)

### Multiplayer planning

- `st multi <friend>` lists games BOTH of you own that have multiplayer/co-op category tags (Multi-player, Co-op, MMO, Online PvP, Online Co-op, LAN, Shared/Split Screen, Cross-Platform)
- Row 0 is the friend's overview (avatar, status, current game)
- If the friend is currently in one of the shared games, that game gets pinned to the top
- Game rows ordered by `min(your-last-played, friend-last-played)` descending

### Steam status

- Switch to Online, Invisible, or Offline with one click via Steam's URL scheme — instant, no Steam restart

### Account switcher

- Lists every Steam account saved on this PC (current account hidden)
- Per-account avatars from Steam's local cache
- Selecting an account → confirmation row → switch (kills Steam, rewrites `loginusers.vdf`, sets `AutoLoginUser` registry, relaunches Steam)
- Refuses to switch if a game is currently running

### Personal stats

- `st me` shows Steam level, owned-game count, total playtime, and recent activity
- Enter on the profile row opens your Steam profile; Enter on the recent row opens the library

### Steam Web API

> API key encrypted with Windows DPAPI, bound to the current user.

- Drives `st me`, `st new`, `st friends`, `st multi`, friends-playing detection, and the store's owned-detection cross-reference
- `st api` walks you through getting a key and (auto-)setting your Steam ID

## Installation

Grab the latest zip from [Releases](https://github.com/hyunwoo312/steam-launcher/releases/latest), unzip into `%appdata%\FlowLauncher\Plugins\`, and restart Flow Launcher.

## License

MIT
