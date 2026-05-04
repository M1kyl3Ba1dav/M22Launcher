# M22 Game Launcher

A lightweight, controller-compatible Windows game launcher built with WPF (.NET). Automatically detects installed games across Steam, Epic Games, Riot, Blizzard, and Xbox Game Pass - then pulls cover art, ratings, and metadata from the RAWG API.

---

## Why I Built This

All software should solve a real problem - otherwise it’s just unnecessary complexity.

In my case, the problem was simple: my games were scattered across multiple launchers like Steam, Epic Games, Riot, Blizzard, and Xbox Game Pass. Every time I wanted to play something, I had to open a different launcher, wait for it to load, and deal with multiple apps cluttering my taskbar.

Keeping several launchers open at once felt inefficient and messy, especially for something that should be quick and seamless.

So I built this launcher to:

Centralise all my games in one place
Reduce the need to juggle multiple launchers
Make launching a game fast, simple, and controller-friendly

It’s not trying to replace those platforms - it just sits on top of them and removes the friction of switching between them.

---

## Features

- **Multi-platform detection** - scans Steam, Epic, Riot, Blizzard, and Xbox Game Pass libraries automatically
- **Cover art & metadata** - fetches game covers, genres, release dates, and ratings via the RAWG API
- **Search & sort** - filter by name and sort A–Z, Z–A, by rating, or by release date
- **Controller support** - navigate and launch games with an Xbox controller (XInput)
- **Local caching** - saves game data to `games.json` so subsequent launches are instant
- **Refresh** - clears the cache and re-scans your libraries on demand

---

## Requirements

- Windows 10 or 11
- .NET 6.0 or later (WPF)
- [Newtonsoft.Json](https://www.nuget.org/packages/Newtonsoft.Json/) NuGet package
- A free [RAWG API key](https://rawg.io/apidocs) for cover art and metadata (optional)

---

## Setup

### 1. Clone the repository

```bash
git clone https://github.com/your-username/game-launcher.git
cd game-launcher
```

### 2. Restore NuGet packages

```bash
dotnet restore
```

### 3. Set your RAWG API key (optional)

Cover art and metadata are fetched from the [RAWG API](https://rawg.io/apidocs). Without a key, games will display a placeholder image but will still launch correctly.

Set the key as a Windows environment variable:

```powershell
[System.Environment]::SetEnvironmentVariable("RAWG_API_KEY", "your_key_here", "User")
```

Or set it in System Properties → Advanced → Environment Variables.

### 4. Build and run

```bash
dotnet build
dotnet run
```

---

## Project Structure

```
GameLauncher/
├── MainWindow.xaml          # UI layout and styles
├── MainWindow.xaml.cs       # Main logic — scanning, sorting, searching, launching
├── ControllerManager.cs     # XInput controller polling (D-pad navigation + A to launch)
├── Game.cs                  # Game model (Name, Genre, Rating, ExecutablePath, etc.)
├── games.json               # Auto-generated cache file (deleted on Refresh)
└── Covers/                  # Cover image cache folder (created automatically)
```

---

## How It Works

### Library Detection

On first launch (or after a Refresh), the app scans the following locations:

| Platform     | Location scanned |
|--------------|-----------------|
| Steam        | Parsed from `steamapps/libraryfolders.vdf` — supports multiple Steam library drives |
| Epic Games   | Reads `.item` manifest files from `C:\ProgramData\Epic\EpicGamesLauncher\Data\Manifests` |
| Riot Games   | Scans `C:\Riot Games` |
| Blizzard     | Scans `C:\Program Files (x86)\Battle.net` |
| Xbox Game Pass | Scans `C:\XboxGames` |

Each detected game folder is checked for `.exe` files. Duplicates (by executable path) are removed automatically.

### Metadata & Cover Art

For each detected game, the app queries the RAWG API with the game name and fetches:

- Cover image URL (`background_image`)
- Genre
- Release date
- Rating (0–5 scale)

Results are cached in `games.json` in the working directory. The cache is used on all subsequent launches until you click **Refresh**.

### Controller Navigation

The `ControllerManager` class polls XInput at 200ms intervals:

| Input | Action |
|-------|--------|
| D-Pad Up | Select previous game |
| D-Pad Down | Select next game |
| A Button | Launch selected game |

---

## Usage

| Action | How |
|--------|-----|
| Search | Type in the search bar (top centre) |
| Sort | Use the sort dropdown (A–Z, Z–A, Rating, Release Date) |
| Launch game | Click the **▶ PLAY** button on any card, or press **A** on a controller |
| Refresh library | Click the **↻ Refresh** button — clears the cache and re-scans |

---

## Known Limitations

- **EXE detection is best-effort** - for deeply nested installs, the first `.exe` found is used, which may not always be the correct launcher
- **RAWG name matching** - metadata is matched by game name string comparison; names that differ between your install folder and RAWG's database (e.g. `CS2` vs `Counter-Strike 2`) may not match
- **Epic Games** - only games with standard manifest files are detected; some titles may be missed
- **Controller polling** - the current 200ms poll interval means holding a button will fire repeatedly; rapid navigation is intentional behavior

---

## Roadmap

- [ ] Right-click context menu (properties, open folder, hide game)
- [ ] Manual game entry (add games not detected automatically)
- [ ] Favourite / pinning system
- [ ] Play time tracking
- [ ] Per-launcher colour theming on badges
- [ ] Placeholder image fallback with game-initial avatar

---

## License

MIT — free to use, modify, and distribute.
