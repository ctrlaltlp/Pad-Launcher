# Emulator game discovery

## Current import workflow

**Import library** reads configured folders, playlists, databases or installed titles, depending on the emulator; reading configured folders can include scanning ROM files. **Scan ROM folders** is a separate action for a folder selected in Pad Launcher on emulators that support it; RetroArch uses library/playlist import and manual add instead. Both offer review before import, reuse registered games on repeated imports and leave emulator-owned files unchanged. **Import all emulator libraries** uses each emulator's supported library adapter; it is not restricted to saved database entries. The bulk importer lists emulator source selectors first and reviewed games below them.

In review, already-added games can be hidden or shown, and selected titles can be cleaned using **Remove tags from game titles** or **Normalise titles**. These edits apply on import. Newly found games are presented in a themed confirmation window grouped by emulator, or by playlist for RetroArch. Each group has a destination-list dropdown that starts with the destination selected in the import window; changing it sends only that group's games to the chosen list. Each game can be selected or cleared before import; already-added games are updated without appearing in that list. RetroArch defaults to **One automatic list per playlist**, with an automatic RetroArch list or chosen list available instead. Sources show the resolved core, or **No usable core found**; unresolved sources require choosing an installed core before they can be imported. Cancelling review returns to source selection and allows loading sources again.

RetroArch playlist lists are automatic emulator lists: rename, hide, duplicate and configure them per list; manage registered games under Emulators. Older imported lists stay custom. Import the playlists with the automatic option to create their new lists. Reimporting a playlist reuses its automatic list. Emulator and RetroArch playlist lists are ordered by maker, then TV/handheld, then console release year; the list name and console metadata are both used, so a playlist such as **GameCube · RetroArch** sorts with the Nintendo TV lists even when it is empty.

The sections below preserve dated upstream research and adapter limitations. They are implementation references, not claims of physical testing for every emulator/version.

### xemu (checked 2026-11-18)

Detection recognises `xemu.exe`. Launches use `-full-screen -dvd_path {game}`; global and per-game overrides remain editable. ROM scanning accepts `.iso` and `.xiso` Xbox XISO images; extracted executable files are not imported as standalone titles. Required system files must already be configured in xemu. Pad Launcher never supplies or modifies those files.

Library import reads portable `xemu.toml` beside the executable first, otherwise `%APPDATA%/xemu/xemu/xemu.toml` (and the common single-folder variant). It scans `[general] games_dir` and offers `[sys.files] dvd_path` as the explicitly labelled last-disc partial source. Both paths reuse the same ROM identity, preserving repeat-import idempotence. Manual ROM folders and file addition remain available. Configuration is read-only.

Verified upstream: [launch options](https://xemu.app/docs/cli/), [games directory](https://xemu.app/docs/games-directory/), [configuration fields](https://github.com/xemu-project/xemu/blob/master/config_spec.yml), and [portable/SDL preference paths](https://github.com/xemu-project/xemu/blob/master/ui/xemu-settings.cc). Fixture coverage verifies configuration discovery, folder and last-disc sources, launch flags and deduplication; physical game launching remains untested.


### shadPS4 Qt Launcher (checked 2026-11-18)

Executable discovery recognises both `shadPS4.exe` (CLI) and `shadPS4QtLauncher.exe` (frontend), including Start-menu targets. Qt matches are labelled explicitly. Qt defaults to `-d -g {game}`: the launcher selects its configured default emulator version and receives the quoted game path. CLI keeps its positional `{game}` default; user-specified global/per-game arguments remain authoritative. Qt needs an installed, selected default version in its own Version Manager; detecting the frontend alone does not prove an emulator engine has been installed. Pad Launcher does not install versions, launch the frontend during detection, or modify its files.

Native import finds portable `user/config.json` first, otherwise standard `%APPDATA%/shadPS4/config.json`, and reads enabled `General.install_dirs` entries. Disabled folders are excluded, paths are deduplicated, and the existing folder reader selects game `eboot.bin` entries. Explicit installed-games folders and manual scanning remain available; repeated imports retain path-based identity. Fixture-tested detection, quoted launch arguments, custom arguments and configured-folder selection. The user's actual Qt Start-menu shortcut is also checked read-only; no game is launched.

Upstream references: [Qt command-line parser](https://github.com/shadps4-emu/shadps4-qtlauncher/blob/main/src/main.cpp), [portable/installed paths](https://github.com/shadps4-emu/shadps4-qtlauncher/blob/main/src/common/path_util.cpp), [configured game directories](https://github.com/shadps4-emu/shadps4-qtlauncher/blob/main/src/core/emulator_settings.cpp), [version registry](https://github.com/shadps4-emu/shadps4-qtlauncher/blob/main/src/common/versions.cpp).

Research checked against upstream source on 2026-11-10. Pad Launcher supports explicit **Import library** discovery alongside manual add and ROM-folder scanning on emulators that support it. Imports create or extend custom lists, and saved imports can be run again without removing previously imported games. Source adapters are verified with fixtures; actual installed-emulator testing remains necessary. The methods below distinguish current support from future extensions.

## Recommended methods

Library import review offers separate tag removal and title normalisation previews; moving trailing `, The` changes only the chosen display names when the import is committed. Original ROM paths, artwork metadata, cores and emulator-owned library files are preserved. Repeated imports retain path-based identity and do not duplicate renamed entries. RetroArch's Games tab does not offer a ROM-folder scan or a persistent scan core. Import playlists/libraries, or add games manually; the manual-add chooser selects the core and a destination-list dropdown that starts at the list currently being browsed. The chosen core is stored on the new game and the chosen list is updated without changing emulator files.

Artwork metadata from RetroArch playlists is retained alongside imported games: `db_name` identifies the thumbnail system and the original label helps match region/disc names. Mixed playlist names do not replace known platform metadata on repeated imports. The configurable [artwork source system](libretro-artwork.md) uses this metadata without changing launch commands or emulator-owned files.

Missing Libretro-preferred artwork also uses this console/region metadata for offline placeholder sizing. Profiles were measured from public [Libretro box-art catalogues](https://thumbnails.libretro.com/) on 2026-11-17; sample filenames, dimensions and regional exceptions are recorded in [libretro-placeholder-samples.json](libretro-placeholder-samples.json). Unknown consoles retain a square placeholder. These estimates affect no imports, ROM files, emulator settings or loaded image dimensions.

### Installed-executable detection (2026-11-14)

Steam installations are also checked during explicit emulator detection. The detector reads `steamapps/libraryfolders.vdf` from configured and registered Steam roots (modern and legacy VDF entries), then checks emulator-named folders immediately under each `steamapps/common`, including additional library drives. It probes known executable names at the folder root and `bin`/`x64`, skips reparse-point directories and does not recursively scan game folders or launch executables. Matches retain their Steam-library evidence and still require user review. This covers Steam RetroArch and other supported emulators installed in appropriately named Steam folders.

Executable detection is available in regular Settings and the optional setup wizard. It checks both user/machine and 32/64-bit **App Paths** and **Uninstall** registrations, relevant Start-menu shortcut targets, and emulator-named folders immediately under Program Files and local application locations. It checks known executable filenames, including common alternate builds, without launching them, writing registrations, scanning ROMs or searching entire drives. This is candidate discovery, not proof that an emulator was installed by an installer: portable applications with shortcuts can also appear. Paths outside these sources, renamed binaries, Microsoft Store packages and arbitrary portable folders can be missed; manual Browse remains available.

The review screen retains distinct installations, combines duplicate evidence, and leaves already-configured emulators unchecked. Applying checked choices uses normal executable validation and library-location detection; games, ROM folders, arguments and unchecked paths are retained. Apply is disabled when nothing is checked. Configured entries show their current path in yellow and explain that checking them applies a changed path. No games are imported automatically. Development-only detection builds remain supported by the build script; normal use does not require a separate test application. Fresh profiles are asked whether to run the setup wizard. Choosing **No** saves the decision, and **Settings → System** can still open the wizard manually.

Microsoft documents per-user/per-machine [App Paths](https://learn.microsoft.com/en-us/windows/win32/shell/app-registration), [installer registration values](https://learn.microsoft.com/en-us/windows/win32/msi/uninstall-registry-key), and [Shell Link targets](https://learn.microsoft.com/en-us/windows/win32/shell/links). PPSSPP's upstream [Windows installer](https://github.com/hrydgard/ppsspp/blob/master/ppsspp.iss) demonstrates installer-created shortcuts and a DisplayIcon that can name the 32-bit executable even when the 64-bit executable is installed; discovery therefore also probes known sibling filenames. Fixtures cover all supported aliases and this PPSSPP case; real installed-emulator coverage remains unverified on the current portable-only setup.

### Game-library sources

Changing the saved emulator executable re-detects its library/configuration location using the adapters below. Saving an unchanged executable preserves a manually selected library location. ROM folders, imported games and saved import provenance are preserved; clearing the executable clears its library-location override.

| Emulator | Preferred discovery | Important limits |
| --- | --- | --- |
| DuckStation | Read `GameList` search directories (`Paths`, `RecursivePaths`), then scan respecting recursion and exclusions. | Also saves a versioned binary game-list cache. Prefer configuration plus scanning over depending on that cache format. |
| PCSX2 | Read `GameList/Paths` and `GameList/RecursivePaths`, then scan with exclusions. | Also has a versioned binary game-list cache. Cache changes must not break discovery. |
| Dolphin | Read configured ISO directories and recursion preference, then scan. | Has `gamelist.cache`, but it is versioned internal data. Installed Wii NAND channels require an additional title inventory and title-ID launch path. |
| Eden | Read the `gamedirs` array and each entry's `path` / `deep_scan`. | Special entries such as `UserNAND`, `SysNAND`, and `SDMC` are logical sources, not filesystem paths. Installed content needs a separate title-aware adapter. |
| RPCS3 | Resolve its virtual HDD from VFS configuration; inspect installed games. Also read `games.yml` for registered external/disc games. | Installed PKGs are discovered from installed titles, not installer files. Filter game-data/update entries using metadata, and deduplicate by title identity. |
| Vita3K | Resolve its configured Vita filesystem root, then enumerate installed `ux0/app` titles. | These installed folders are the inventory; do not import every PKG/VPK as a separate game. Launch by installed title ID. |
| RetroArch | Read its configured playlist directory and `.lpl` playlists. | Preserve each entry's core, playlist defaults, labels and portable paths. `DETECT` entries use installed core metadata when unambiguous; unresolved or ambiguous matches offer user selection. |
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

Core resolution supports standard playlists whose entries use `DETECT`: preserve valid explicit entry/default core paths or names first, relocate missing paths to the same DLL in the current installation, then use the source's explicitly selected core. Otherwise, select an installed core only when its `.info` database and supported-extension metadata yield exactly one compatible match. Unknown platforms and multiple matches require an explicit choice. The source selector offers installed cores for unresolved playlists; choices are staged until import and saved with the import definition for later reuse. Existing valid per-game core assignments are preserved; missing assignments may be repaired during a confirmed import. Saving a changed (or already-updated) executable also repairs stale paths to the same installed core DLL, without rewriting ROM paths or RetroArch files.

The inventory honours `libretro_directory` and `libretro_info_path` in the active configuration, with standard `cores`/`info` folders as fallback. Evidence: [playlist default-core behaviour](https://docs.libretro.com/guides/roms-playlists-thumbnails/), [core info metadata](https://github.com/libretro/libretro-core-info/blob/master/00_example_libretro.info), and verified local standard RetroArch configuration/playlist files.

Both individual and bulk library imports default to **One list per playlist** in the destination selector. Each non-empty selected playlist gets a list named after it; games shared across playlists retain one game entry with multiple list memberships. Reimport reuses saved per-playlist destinations, preserving renamed lists and their overlay visibility. The automatic RetroArch list remains available as an alternative destination.

Archive members in playlists use `archive.zip#folder/ROM.ext` (also `.7z#...`). The importer resolves the archive against the RetroArch installation, preserves the member, label, database and core, and passes the full content path as one quoted launch argument. No extraction or emulator-file changes are performed. Different members remain distinct; repeat imports reuse their entries. Missing-game checks use the containing archive rather than treating the virtual member path as a missing Windows file. Member contents are left for RetroArch to validate; Pad Launcher does not enumerate archives or detect individual members deleted inside an existing archive. Other emulators' archive syntax remains unsupported unless separately implemented.

RetroArch's accepted content extensions are layered: the resolved or explicitly selected core's `.info` `supported_extensions` first, the union of installed core metadata when no core is selected, then a broad built-in RetroArch fallback that includes disc-image and modern formats such as `.rvz`, `.gcm`, `.wbfs`, `.wia`, `.ciso`, `.gcz`, `.dol`, `.elf` and `.wad`. `.zip`, `.7z` and `.m3u` remain accepted containers. Core metadata is no longer intersected with a narrow hardcoded list, so a core that advertises `.rvz` can import `.rvz` files from playlists, archives and manual selection. Arbitrary extensions are never accepted when no core advertises them, and Pad Launcher still reads all emulator files without modifying them.

Format reference: [Libretro playlist documentation](https://docs.libretro.com/guides/roms-playlists-thumbnails/).

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

The supported adapter categories are configured folders, installed-title inventories, playlists, recent-file hints and read-only databases. New adapters should follow the validation rules above rather than assuming all emulators expose the same kind of library.

## Artwork and remaining import limits

Import readers preserve artwork metadata but do not fetch images themselves. The [artwork source system](libretro-artwork.md) applies each list's preference or emulator defaults, using SGDB, Libretro or a permitted fallback. Manually chosen images retain priority, and repeated imports keep the same game identity.

RetroArch supports plain JSON and legacy six-line playlists, executable-relative paths, explicit/default or manually selected cores, and archive-member paths such as `archive.zip#ROM.ext`. Compressed playlists still require manual handling. Archive members are not extracted or enumerated by Pad Launcher; RetroArch validates the member when launching. Installed Wii NAND, installed Switch content and melonDS DSiWare remain separate future adapters. Xenia content import filters to game containers and cannot discover arbitrary external ISO folders from content-root settings alone.

### Azahar (Nintendo 3DS)

Verified against upstream on 2026-11-13. Import reads `Paths/gamedirs` (`path`, `deep_scan`) in `qt-config.ini`, with legacy `Paths/gameListRootDir` fallback. Portable `user/config/qt-config.ini` takes precedence over `%APPDATA%/Azahar/config/qt-config.ini`. An explicit location override remains available. Reads emulator data only; never installs CIA packages or changes Azahar configuration.

Installed discovery follows `sdmc/Nintendo 3DS/<zero ID0>/<zero ID1>/title/00040000` (games) and `00040002` (demos), respecting `Data Storage/use_custom_storage` and `sdmc_directory`. Ignores SYSTEM entries, update/DLC categories, CIA installers and non-executable NCCH content. Installed `.app` candidates require an NCCH base-game/demo ID and executable ExeFS. Reads an English SMDH title when available; encrypted or compressed icon metadata falls back to the file name and can be renamed in review. No general Azahar binary cache dependency. Does not decrypt or validate full ROM contents; launch compatibility depends on Azahar. Custom user-directory launch flags require matching the import-location override manually.

Manual add and recursive folder scans accept cartridge/homebrew formats, including current upstream `.3ds`, `.cci`, compressed/bundled formats, and executable installed `.app` files. Default launch: `-f {game}`, with global/per-game argument editing. Repeat imports retain the existing path-based identity. Fixture-tested configuration discovery, recursion, installed-title filtering, title extraction, launching and duplicate prevention; real Azahar/game testing remains necessary.

Sources: [configuration paths](https://github.com/azahar-emu/azahar/blob/master/src/citra_qt/configuration/config.cpp), [installed titles and game directories](https://github.com/azahar-emu/azahar/blob/master/src/citra_qt/game_list_worker.cpp), [portable and installed data roots](https://github.com/azahar-emu/azahar/blob/master/src/common/file_util.cpp), [command-line launch and formats](https://github.com/azahar-emu/azahar/blob/master/src/citra_qt/citra_qt.cpp).

Imported RetroArch playlists are editable user lists, not Custom games memberships. Their registered games retain `Source=emulator` and `EmulatorId=retroarch`. The List editor can edit or merge these lists without changing game launch sources. Emulator subtabs now show Games before Settings.

Automatic import destinations show both emulator and console names, for example **DuckStation · PS1 (automatic list)** and **Dolphin · GameCube / Wii (automatic list)**. This is a display label for the existing emulator list, not a second list or a console-based import destination. The same labels are used in individual and bulk library imports.
