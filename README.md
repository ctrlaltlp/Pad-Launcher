# Pad Launcher

![Pad Launcher angled artwork carousel showing Alice: Madness Returns in the PC list](assets/preview.png)

A transparent, controller-friendly Windows game launcher with angled, rectangular and grid layouts. Browse games from multiple launchers, emulators and custom entries, with optional game titles and list names. Organise your games into individual or combined lists and use existing launcher artwork, local images or SteamGridDB covers.

Launchers supported: **Steam**, **GOG Galaxy**, **Epic Games Store**, **EA App**, **Ubisoft Connect**, **Battle.net**, **Rockstar Games Launcher**, **Xbox app** (installed Xbox / Game Pass titles), and custom games. Optional **SteamGridDB** artwork uses your API key and is cached locally.

Choose **SteamGridDB** or **Libretro** artwork per emulator during setup and per list in Artwork settings, with fallback or single-source choices. Libretro needs no account or API key.

## Usage

**[Download the latest PadLauncher.exe](https://github.com/ctrlaltlp/Pad-Launcher/releases/latest/download/PadLauncher.exe)** for Windows 10/11 (64-bit).

Place `PadLauncher.exe` in its own writable folder before running it. The app creates `PadLauncher.Data` and `PadLauncher.Update` folders beside the executable for settings, artwork and temporary update files.

With a fresh data folder, an optional setup wizard helps configure launchers, emulators, artwork, controls and the overlay layout. Every page can be skipped; reopen it from **Settings → System → Restart setup**. Existing installations keep starting quietly in the notification area. Running the executable again toggles the overlay: opens it when hidden, or hides it when visible.

Launchers are detected automatically. If detection fails, set the installation folder in **Settings → Launchers**. Use **Emulators → Settings → Detect installed emulators** to review emulator paths found through Windows registrations, Start-menu shortcuts and standard installation folders, or browse manually. Emulator games can then be imported from libraries, scanned from ROM folders or added manually.

## Emulators

Supports **DuckStation, PCSX2, RPCS3, xemu, Xenia, Dolphin, Eden, melonDS, Vita3K, RetroArch, PPSSPP, mGBA, Cemu, Azahar and shadPS4**. Configure emulator paths, import libraries (including RetroArch playlists), scan ROM folders or add games manually. Core selection and launch arguments are configurable; imported games can be organised into lists.

There are 2 ways to import emulator games to the app:
- Import library from emulator: Reads the emulator’s configured game folders, playlists, registered games or installed titles, depending on the emulator. Folder-based libraries may involve scanning those folders.
- Scan ROM folders: Separately scans a folder you choose in Pad Launcher, including its subfolders. It does not require a library configured inside the emulator.
Both let you review detected games, rename new entries and choose a destination list before adding them. Repeating an import or scan reuses existing game entries without creating duplicates.

## Controls

Choose PlayStation, Xbox or Nintendo button labels in **Settings → Controls**. Options / Start (or F1) briefly shows control hints and the game title.

Use **Controls → Show/Change controls** to remap controller buttons and keyboard shortcuts independently for the overlay, search keyboard and search results. Conflicting assignments are flagged before saving; **Restore defaults** is available. The table below shows the defaults.

Controller input supports Xbox/XInput, DualShock 4, DualSense, and the original Switch Pro Controller through SDL. Switch Pro USB/Bluetooth support is implemented but still awaiting physical-controller testing; Joy-Cons and Switch 2 controllers are not enabled. See [controller support](docs/controller-support.md) for details.

| Action | Controller | Keyboard / mouse |
| --- | --- | --- |
| Browse games | D-pad / left stick; right stick for faster browsing | Left / Right |
| Move between grid rows | D-pad / left stick up and down | Up / Down |
| Launch selected game | Cross / Xbox A / Nintendo B | Enter, or click a game panel |
| Dismiss overlay | Tap Circle / Xbox B / Nintendo A | Esc |
| Exit app completely | Hold Circle / Xbox B / Nintendo A for 1 second | Tray → Exit app |
| Change list | L1 / R1, Xbox LB / RB, Nintendo L / R | Page Up / Page Down |
| Toggle Grid 5×3 ↔ previous Angled/Rectangular layout | Square / Xbox X / Nintendo Y | — |
| Move to next monitor | L2 / Xbox LT / Nintendo ZL | M |
| Search games with the gamepad keyboard | Triangle / Xbox Y / Nintendo X | Ctrl+F |
| Refresh games, artwork and lists | Select/Create / Xbox Back/View / Nintendo − | F5 |
| Toggle favourite | R3 / right stick click | F |
| Jump to a chosen list (optional) | L3 / left stick click | — |
| Brief control hints and game title | Options / Start / Nintendo + | F1 |
| Open Settings | — | Tray menu → Settings |

Left and right stick speeds are adjustable in Controls (both default to 5×). D-pad and left stick up/down switch lists in Angled and Rectangular layouts. Grid keeps up/down row navigation. R3 toggles favourites; show or hide the Favourites list under **Lists → Overlay lists**.

## Overlay layouts

**Overlay settings:** Choose your layout, artwork sizing, panel sizes, spacing and outlines. Adjust title and list-name visibility, select a monitor, and optionally remember layout switches across restarts. Individual lists can override layout and artwork preferences.

Choose **Angled** for a slanted artwork carousel, **Rectangular** for a straight carousel, or **Grid** for multiple rows and columns. **Fixed (blurred edges)** keeps consistent panel sizing and fills unused edges with artwork colours; **Fit artwork** follows each cover's proportions to show the full image. Layout and sizing can be set globally or per list. Enable **Overlay → Remember layout changes made with controls** to keep layout switches for each list across restarts; this is off by default.

## Artwork

Pad Launcher uses existing launcher artwork and local images, plus **SteamGridDB (SGDB)** and **Libretro** downloads. SGDB requires your API key; Libretro does not. Downloaded images are cached locally, and manually chosen images take priority.

- **PC launcher games:** Use available launcher artwork, with SGDB for missing covers.
- **Custom games:** Use manually chosen or available Steam shortcut artwork, with SGDB for missing covers.
- **Emulator games:** DuckStation, PCSX2, xemu, mGBA and melonDS prefer Libretro by default; other emulators, including RetroArch, prefer SGDB. The other provider is used as fallback.

Change emulator defaults or override individual lists under **Settings → Artwork**. Choose a preferred provider with fallback, or **Only SteamGridDB / Only Libretro**. Lists using **Automatic** follow each game's emulator preference, even when games are imported into a custom list. Existing preferences are preserved. [Artwork details](docs/libretro-artwork.md).

## Lists

Choose which lists appear and arrange their order under **Settings → Lists → Overlay lists**. Each list can have its own layout, panel sizes and outlines under **Per-list layout**, plus its own artwork preference under **Artwork → Per-list artwork**. Create lists from individual games or merge existing lists into an independent new list. Existing combined lists still update with their source lists. You can show or hide any list you want on the overlay including the preconfigured ones.

Preconfigured lists:
- **Recently added:** Newly registered games from all sources, newest first. Tracking starts when this feature is first used; existing games have no historical added date. Refreshing or reimporting a game does not move it to the top.
- **Favourites:** Your hand-picked games. Press R3 / right stick click or F by default to add or remove the selected game.
- **Recently played:** Games ordered by their latest launch in Pad Launcher or available launcher play history, newest first.

Open **Settings → Lists → List editor** to manage your lists.

- **Create** starts a list from individual games. Filter by name or use **Select all** to pick games in bulk.
- **Edit** renames a list or changes its games. **Show added games** filters to current members. **Uncheck added** clears their selections in the draft, and **Restore added** puts them back before you save.
- **Combine** creates or edits a live list that updates as its source lists change.
- **Merge** makes a snapshot of the selected lists. Keep the sources, delete all eligible sources, or choose which eligible sources to delete.
- **Duplicate** copies a list’s current games into a new list you can name and edit independently.
- **Delete** removes a custom or combined list. Choose **Delete list and unregister its games** to also remove those games from Pad Launcher. Game files stay on disk.

## Credits

Controller artwork from **PS5 Button Icons and Controls**, **Xbox Series Button Icons and Controls**, and **Switch Button Icons and Controls** by [Zacksly](https://zacksly.itch.io), licensed under [CC BY 3.0](https://creativecommons.org/licenses/by/3.0/). Pad Launcher crops and scales these images and adds interactive control labels and guide lines.

## Inspiration

https://github.com/motor-dev/wallpaperCarousel
