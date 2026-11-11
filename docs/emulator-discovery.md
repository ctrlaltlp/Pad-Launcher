# Emulator game discovery

Research checked against upstream source on 2026-11-10. Pad Launcher supports explicit **Import library** discovery alongside manual add and ROM-folder scanning. Imports create or extend custom lists, and saved imports can be run again without removing previously imported games. Source adapters are verified with fixtures; actual installed-emulator testing remains necessary. The methods below distinguish current support from future extensions.

## Recommended methods

| Emulator | Preferred discovery | Important limits |
| --- | --- | --- |
| DuckStation | Read `GameList` search directories (`Paths`, `RecursivePaths`), then scan respecting recursion and exclusions. | Also saves a versioned binary game-list cache. Prefer configuration plus scanning over depending on that cache format. |
| PCSX2 | Read `GameList/Paths` and `GameList/RecursivePaths`, then scan with exclusions. | Also has a versioned binary game-list cache. Cache changes must not break discovery. |
| Dolphin | Read configured ISO directories and recursion preference, then scan. | Has `gamelist.cache`, but it is versioned internal data. Installed Wii NAND channels require an additional title inventory and title-ID launch path. |
| Eden | Read the `gamedirs` array and each entry's `path` / `deep_scan`. | Special entries such as `UserNAND`, `SysNAND`, and `SDMC` are logical sources, not filesystem paths. Installed content needs a separate title-aware adapter. |
| RPCS3 | Resolve its virtual HDD from VFS configuration; inspect installed games. Also read `games.yml` for registered external/disc games. | Installed PKGs are discovered from installed titles, not installer files. Filter game-data/update entries using metadata, and deduplicate by title identity. |
| Vita3K | Resolve its configured Vita filesystem root, then enumerate installed `ux0/app` titles. | These installed folders are the inventory; do not import every PKG/VPK as a separate game. Launch by installed title ID. |
| RetroArch | Read its configured playlist directory and `.lpl` playlists. | Preserve each entry's core, playlist defaults, labels and portable paths. `DETECT` / unresolved cores require user selection. |
| PPSSPP | Read pinned paths, its current browser directory, recent games, and the active memory-stick `PSP/GAME` location. | These sources are incomplete hints rather than a guaranteed complete library. Scan confirmed folders; ignore remote/network URLs unless separately supported. |
| melonDS | Read `RecentROM` and `LastROMFolder`; offer those files/folder as discovery candidates. | Recently played files are not a complete library. Prefer the existing user-selected scan folder for completeness. NAND-installed DSiWare needs separate research and launching support. |
| mGBA | Read its optional `library.sqlite3` library or use its recorded roots as scan candidates. | Library support depends on the build and whether the user populated it. Archive members need special handling. Retain folder-scan fallback. |
| Xenia / Canary | Resolve storage root and configured `content_root`; inspect launchable content containers there. Keep manual ROM-directory scanning. | Content roots also contain saves, DLC and updates. They are not a complete index of external ISOs/XEX games. No general complete ROM-library source was verified in the inspected entry points. Treat third-party managers separately. |

## Evidence and adapter details

### DuckStation

The directory settings UI reads and writes `Paths` and `RecursivePaths` in its game-list configuration section. Use the actual settings parser, not a comma split: serialized string lists and escaping matter. Its game-list implementation has a cache signature and version, demonstrating that the binary cache is not a stable interchange API.

Sources: [directory settings](https://github.com/stenzek/duckstation/blob/master/src/duckstation-qt/gamelistsettingswidget.cpp), [game-list cache](https://github.com/stenzek/duckstation/blob/master/src/core/game_list.cpp).

### PCSX2

Its settings UI explicitly separates nonrecursive `GameList/Paths` from `GameList/RecursivePaths`, and maintains exclusions. Its game-list implementation also versions its binary cache. Resolve the active settings/data directory before reading either source; confirm installed versus portable layout against the chosen version.

Sources: [directory settings](https://github.com/PCSX2/pcsx2/blob/master/pcsx2-qt/Settings/GameListSettingsWidget.cpp), [game-list cache](https://github.com/PCSX2/pcsx2/blob/master/pcsx2/GameList.cpp).

### Dolphin

Configuration stores `General/ISOPaths` as a count and `ISOPath0`, `ISOPath1`, etc. `GetIsoPaths` reconstructs the list. Game-file discovery takes directories and a recursion flag; the separate game-file cache is stored as `gamelist.cache` and has a cache revision. Reading ROM directories is the preferred initial adapter. Installed Wii titles are a separate case; a WAD installer and an installed NAND title must not become duplicate launch entries.

Sources: [ISO directory settings](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/Core/Config/MainSettings.cpp), [scan/cache implementation](https://github.com/dolphin-emu/dolphin/blob/master/Source/Core/UICommon/GameFileCache.cpp).

### Eden

The Qt configuration reads `gamedirs` entries with `path` and `deep_scan`. It also creates special NAND/SD sources. A reader should handle the Qt array serialization and distinguish normal directories from those logical sources. Reading configured external folders is straightforward; identifying installed Switch base games versus updates/DLC requires content metadata and a verified launch method, not a blanket scan of NCA files.

Source: [Qt path configuration](https://github.com/eden-emulator/mirror/blob/master/src/qt_common/config/qt_config.cpp).

### RPCS3

`vfs.yml` defines `/dev_hdd0/` and `/games/`, with `$(EmulatorDir)` substitutions. Resolve overrides before inspecting the HDD's `game` directory. `games.yml` supplies registered external paths; current source includes handling for disc folders, external HDD games and ISOs. Installed game metadata provides names, title IDs and categories. Upstream rejects category `GD` as game data when registering games. Updates may contain executables, so the existence of EBOOT alone is not enough to classify a base title. Confirm other categories and trial/unlock cases before shipping the filter.

Sources: [VFS fields](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/vfs_config.h), [VFS resolution/file](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/vfs_config.cpp), [registration and games.yml](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/System.cpp), [metadata/trial handling](https://github.com/RPCS3/rpcs3/blob/master/rpcs3/Emu/system_utils.cpp).

### Vita3K

The configuration resolves a Vita filesystem root, including portable-mode overrides. Installed app lookup uses `<root>/ux0/app`; the `-r` argument is validated against installed app directory IDs. Discover installed folders and read `sce_sys/param.sfo` for names and identity. Match the same active filesystem/configuration when launching. Keep patch/add-on storage out of the base-game inventory.

Source: [configuration, filesystem resolution and installed-title validation](https://github.com/Vita3K/Vita3K/blob/master/vita3k/config/src/config.cpp).

### RetroArch

`playlist_directory` points to saved collections. The playlist implementation stores content paths, labels, per-entry core paths and playlist-level default cores. It also implements legacy/compressed formats and portable-path resolution, so do not assume every `.lpl` is plain JSON with absolute paths. Start with a declared supported format, report unsupported files, and keep explicit core assignment when importing. History/favourites can supplement system playlists but should be deduplicated.

Sources: [directory configuration](https://github.com/libretro/RetroArch/blob/master/retroarch.cfg), [playlist implementation](https://github.com/libretro/RetroArch/blob/master/playlist.c).

### PPSSPP

`ppsspp.ini` has a `PinnedPaths` section, `General/CurrentDirectory`, and recent-file data. Its path utility resolves the active memory stick and system directories. Scan relevant local pinned/current directories and the installed PSP game area after resolving that root. A recently opened browser directory does not imply that all games live there; ask for a scan folder when these hints are insufficient. Preserve EBOOT.PBP launch targets and use title metadata when available.

Sources: [saved paths and recent entries](https://github.com/hrydgard/ppsspp/blob/master/Core/Config.cpp), [memory-stick/path resolution](https://github.com/hrydgard/ppsspp/blob/master/Core/Util/PathUtil.cpp).

### melonDS

Current configuration uses `melonDS.toml` with legacy INI migration. The window code reads `RecentROM` and uses `LastROMFolder`. Recent entries can include archive/member syntax joined with `|`; this must be preserved or reported unsupported, never interpreted as an ordinary Windows filename. These are useful suggestions, not evidence of a complete saved library. NAND/DSiWare inventory is outside the verified ROM-file route.

Sources: [configuration format](https://github.com/melonDS-emu/melonDS/blob/master/src/frontend/qt_sdl/Config.cpp), [recent games and folder hints](https://github.com/melonDS-emu/melonDS/blob/master/src/frontend/qt_sdl/Window.cpp).

### mGBA

The Qt frontend opens `library.sqlite3` in its configuration directory when SQLite library support is built in. The schema separates ROM metadata, roots and paths, including custom titles. Join roots and paths using the actual schema, preserving archive members. Read an independent consistent snapshot or use a read-only connection; do not call upstream's library-loading function because it opens read/write and can create or migrate tables. Pad Launcher reads the supported schema using Windows SQLite in read-only mode; no extra DLL is shipped. Archive-member entries are reported for manual setup.

Sources: [frontend database location](https://github.com/mgba-emu/mgba/blob/master/src/platform/qt/Window.cpp), [library frontend](https://github.com/mgba-emu/mgba/blob/master/src/platform/qt/library/LibraryController.cpp), [database schema](https://github.com/mgba-emu/mgba/blob/master/src/core/library.c).

### Xenia / Canary

Entry points resolve `storage_root`, portable mode/`portable.txt`, and `content_root` (defaulting to `content` under storage). Relative content roots are relative to storage, not necessarily the executable. Inspect Xbox content metadata/type before considering files launchable; that root is also used for saves and other content. External ROM directories still need a user-selected scan path unless a separately supported manager supplies a verified index. Master and Canary need independent fixtures.

Sources: [Xenia storage/content resolution](https://github.com/xenia-project/xenia/blob/master/src/xenia/app/xenia_main.cc), [Canary resolution](https://github.com/xenia-canary/xenia-canary/blob/master/src/xenia/app/xenia_main.cc), [launch/container handling](https://github.com/xenia-project/xenia/blob/master/src/xenia/emulator.cc).

## Rules for future emulator integrations

1. Research native discovery before offering only a ROM-folder field: saved playlists/databases, configured scan roots, installed title inventories, then recent-file hints and manual scanning.
2. Verify upstream sources and record links, date, supported versions/formats and uncertainties here. Source inspection establishes feasibility; actual installation tests establish working support.
3. Resolve the selected executable's active config/data roots, portable mode and custom overrides. Do not silently combine multiple installations or assume data is beside the executable. Provide a manual data/config path override where ambiguity remains.
4. Read emulator-owned files only. Respect folder recursion/exclusions, relative paths and serialization. Do not copy or modify ROMs, emulator settings, NAND content, BIOS, firmware or keys.
5. Identify base games using metadata; exclude updates, DLC, saves and firmware. Installed package discovery scans installed titles, not installer archives. Keep launch targets and metadata title IDs consistent.
6. Review imports in one window and make repeated discovery idempotent. Preserve existing names, artwork, list memberships and core assignments. Treat alternate editions/actions as choices rather than collapsing them solely by name.
7. Keep detection separate from launch and artwork. Discovery must not start an emulator or game. Verify launch commands independently, including installed-title identifiers, archive-member paths and cores.
8. Keep imports explicit initially. Any future ongoing synchronization needs clear user controls, retained exclusions and defined behaviour for missing games. Background scans must not delay opening the overlay.
9. Test fixtures for normal/portable/custom roots, recursion/exclusions, malformed or unsupported configuration, inaccessible paths, missing files, duplicates and installed-game versus update/DLC classification. Keep the manual add/scan fallback.

Suggested implementation order: configured folders (DuckStation, PCSX2, Dolphin, Eden); installed inventories (RPCS3, Vita3K); RetroArch playlists; PPSSPP/melonDS hints; mGBA database; Xenia content inspection. This order is a project recommendation, not an upstream requirement.

## Artwork follow-up

Imported titles already use Pad Launcher's SteamGridDB lookup by game name. User artwork overrides remain tied to the same game ID when a list is updated. The import readers do not fetch artwork themselves.

A useful next source is Libretro box art: [thumbnail layout and public server](https://docs.libretro.com/guides/roms-playlists-thumbnails/#thumbnails). It does not require an API key. Prefer reusing installed RetroArch thumbnails when present; a network fallback can use the official thumbnail server. Match by system and playlist/database label, keeping region and edition distinctions. Do not guess from a broad title search across systems. Preserve the chosen cover first, then portrait artwork when available; use console box art as a fallback, keeping its aspect ratio.

Current import limitations: RetroArch supports plain JSON and legacy six-line playlists, with executable-relative paths and explicit/default core assignment. Compressed playlists and archive-member entries are reported for manual handling. Installed Wii NAND, installed Switch content and melonDS DSiWare are separate future adapters. Xenia content import deliberately filters to game containers; it cannot discover arbitrary external ISO folders from content-root settings alone.
