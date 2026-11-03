# Pad Launcher

A transparent Windows overlay with an angled artwork carousel, the current collection and an optional game title. Your desktop stays visible around the cards. The app lives in the tray and keeps library data and artwork ready in memory. It reads your existing Steam library, non-Steam shortcuts, custom artwork and collections.

## Download and run

**[Download PadLauncherBackground.exe](https://github.com/lpv11/pad-launcher-for-steam/releases/latest/download/PadLauncherBackground.exe)** for Windows 10/11 (64-bit), save it in a folder of your choice, and double-click it. No installer or build step is needed. The executable uses the .NET Framework included with Windows; it needs no Node, Electron or .NET SDK installation.

The first launch puts the app in your notification area. Double-click its tray icon to open the game carousel. See [all releases](https://github.com/lpv11/pad-launcher-for-steam/releases) for version details and download checksums.

Steam is detected automatically. If detection fails, set `SteamPath` in the configuration file to the Steam installation folder (the folder containing `steam.exe` and `steamapps`). Set `UserId` to choose a specific local Steam profile, or leave it null for automatic selection. Steam remains responsible for launching games and applying their controller configurations.

## Tray and overlay

The first launch starts quietly in the tray and prepares the library without displaying the overlay. **Double-click the tray icon to toggle the overlay on/off.** The tray's Show launcher command also opens it. The app keeps the overlay, library and decoded artwork in memory when dismissed with Circle/B or after launching a game. Running the executable again signals the existing process to show the overlay. It never reopens itself after a game exits.

The hidden process stops controller polling and has no periodic library scan. On reopening, retained artwork displays first and Steam data refreshes afterwards.

Right-click its notification-area icon and choose **Show help & settings** for controls and **PlayStation**, **Xbox**, or **Nintendo** style buttons. Changes save immediately and update help and overlay hints. Styles change labels while keeping physical button positions: Nintendo B plays, A dismisses, X searches/refreshes, L/R changes collections and + shows hints. The window can be opened while the overlay is hidden. Hold Circle for one second while the overlay is open, or use **Exit background process**, to stop it completely. No Windows startup registration is installed.

## Configuration

The app creates `%LOCALAPPDATA%\PadLauncherBackground\settings.json`. Exit the app completely before editing it. `settings.example.json` in this project shows the available fields. Existing settings in this directory are preserved by rebuilding.

- `"ButtonPrompts": "PlayStation"` is the default. Set it to `"Xbox"` or `"Nintendo"` for those labels, or use the tray settings buttons. This changes optional help hints independently of the controller DS4Windows exposes.
- `"ShowGameTitle": true` shows the selected game's title below the carousel. Set it to `false` to leave only artwork and the collection name.
- `SteamPath` and `UserId` override automatic Steam/profile detection.

The normal overlay displays no control prompts. Press **Options** (Xbox: Start) or **F1** to briefly show the controls in your chosen button style. No settings dialog opens.

## Controls

| Action | Controller | Keyboard |
| --- | --- | --- |
| Browse games | Left stick / D-pad | Left / Right |
| Browse games at 3× speed | Right stick left / right | — |
| Launch selected game | Cross / A | Enter |
| Change collection | L1 / R1, LB / RB | Page Up / Page Down |
| Search | Triangle / Y | F |
| Close search / clear search / exit | Circle / B | Escape |
| Exit app completely (overlay open) | Hold Circle / Xbox B / Nintendo A for 1 second | — |
| Brief control hint | Options / Start | F1 |
| Refresh games, artwork and collection membership | Hold Triangle / Y for 1 second | F5 |

Mouse clicks and the scroll wheel also work. Search appears only when invoked, and text entry uses a keyboard in this version. Controllers use XInput, including the virtual Xbox controller exposed by DS4Windows. The launcher does not change DS4Windows settings or install a PS-button binding.

Tap Circle to dismiss on release; hold it for one second to exit the entire app, including the background process and tray icon. Xbox uses B; Nintendo uses A. Exiting saves your selection and shuts down normally.

After changing a collection in Steam, hold **Triangle** for one second (Xbox: **Y**) to reread it without reopening the overlay. A short tap still opens search. A brief notice confirms the refresh and shows the current collection's game count. Your collection and selected game are kept when they still exist; a removed collection falls back to All games. Steam must save its changes locally before they can be read.

## Library behaviour

- Installed Steam titles are discovered from manifests across library drives. Known Proton, Steam Linux Runtime, Source SDK and Steamworks redistributable entries are excluded. Other installed tools may still appear.
- Existing non-Steam Steam shortcuts are imported and launched through their 64-bit Steam shortcut IDs.
- User-supplied Steam artwork takes priority; the launcher then uses Steam's local artwork cache. Missing or unsupported artwork produces a title card. PNG and JPEG artwork are supported; WebP artwork is not decoded by this version.
- Collections combine Steam's local cloud-storage cache with profile-local non-Steam membership. Explicit removal lists take precedence. Shortcut tags provide a fallback when a matching cloud collection is absent.
- Dynamic collections support Installed, Played/Unplayed, full/partial controller support, store tags, store-category/accessibility IDs, and basic name/app-ID search. Any/all logic within groups is preserved, groups are combined, then manual additions and removals are applied. Installed follows Steam's rule of excluding non-Steam shortcuts unless manually added. Only installed games and imported shortcuts can appear, consistent with the rest of this launcher.
- Dynamic rules use Steam's local appinfo and profile play-history caches, without a web API or API key. Missing required metadata, unknown rule versions, and rules requiring unavailable data (such as friend ownership, remote streaming status, language or device compatibility) produce an explanatory empty state. Name search supports substrings and saved suggestions; it does not reproduce Steam's fuzzy spelling search.
- Collections with saved empty filter objects work from their explicit membership lists, matching Steam's current behaviour. Membership is recalculated on app startup, opening the overlay and manual refresh.
- Library data, artwork paths, the selected collection and the selected game are cached under `%LOCALAPPDATA%\PadLauncherBackground`. Artwork itself is read from Steam rather than copied. Cached data displays first, then a background task refreshes it while the launcher is open.
- The overlay hides once Windows accepts the Steam launch request. The app stays in the tray, does not monitor the game's exit and does not reopen itself. A Steam login, update or third-party launcher can still require attention.

Steam configuration files are read only. Changes made in Steam appear on the next launcher startup or refresh, after Steam has saved them locally. Steam's internal file formats may require adjustments after future client updates.

## Build and verify

For development, run `./build.ps1` from PowerShell. This uses Windows' .NET Framework C# compiler, with no package downloads, and produces `bin/PadLauncherBackground.exe`. `./build.ps1 -Test` also checks library discovery, binary/text VDF, shortcuts, artwork, collections, corrupt-file handling, transparent overlay rendering and button-style configuration. Downloads above are already built.

Useful executable arguments:

- `--demo`: sample title-card library; game launching disabled.
- `--quit-background`: exit the running app completely.
- `--scan --steam "C:\Program Files (x86)\Steam"`: scan without opening the UI; write a diagnostic summary beside the executable and cache the library.
- `--profile ACCOUNT_ID`: select a profile during a command-line scan.
- `--data DIRECTORY`: override the launcher data directory for isolated verification.
- `--render-preview`: render the sample UI to `preview.png` beside the executable.
- `--render-preview --live-preview --data DIRECTORY`: render the cached library from the specified data directory without launching a game.

Actual game launch and physical-controller behaviour should be verified on the gaming PC. The automated suite does not start games or alter Steam/DS4Windows configuration.
