# Artwork sources and preferences

Pad Launcher combines existing launcher artwork and local images with downloads from **SteamGridDB (SGDB)** and **Libretro**. SGDB requires your API key; Libretro requires no account or key. Manually chosen images take priority. Provider preferences control automatic downloads and cached-cover selection; they do not remove original launcher or local files.

## Defaults and setup

PC launcher games use available launcher artwork, with SGDB for missing covers. Custom games use chosen images or available Steam shortcut artwork, with SGDB for missing covers.

Emulator defaults are:

| Emulators | Preferred provider | Fallback |
| --- | --- | --- |
| DuckStation, mGBA, melonDS | Libretro | SteamGridDB |
| Other emulators, including RetroArch and xemu | SteamGridDB | Libretro |

Existing saved preferences are preserved. **Setup wizard → Choose artwork (3/5)** lets users choose sources per emulator. Configured emulators appear first; **Other emulators** is collapsed initially. Entering or omitting the SGDB key does not reset source choices. If a choice includes SGDB and no key is supplied, continuing offers a missing-key warning. There is no global SGDB or Libretro checkbox in the wizard and no separate RetroArch detection prompt.

Change emulator defaults later under **Settings → Artwork**. There is no global Libretro enable checkbox: the selected source policy determines whether Libretro is used.

## Per-list preferences

**Artwork → Per-list artwork** offers:

- **Use emulator defaults:** Inherit applicable RetroArch list settings or each game's emulator preference. Importing an emulator game into a custom or mixed list does not discard its emulator default. PC/custom games retain their normal artwork chain.
- **Prefer Libretro with SteamGridDB as fallback.**
- **Prefer SteamGridDB with Libretro fallback.**
- **Only SteamGridDB.**
- **Only Libretro.**

An explicit list preference wins over inherited preferences; a manual image wins over automatic selection. “Only” choices do not download or display the other provider as fallback. SGDB downloads require an enabled, saved API key.

**Lists → RetroArch lists settings** provides shared artwork and layout defaults for automatic RetroArch lists, imported playlists and RetroArch-only custom/combined lists. An explicit per-list artwork choice takes precedence. In mixed lists without an explicit artwork choice, each game resolves its emulator preference; RetroArch games can inherit the shared RetroArch artwork preference. Mixed lists do not inherit the RetroArch group layout.

Artwork preferences are separate from **Lists → Per-list layout**. The old **Libretro artwork lists** checklist and **Prefer Libretro artwork for RetroArch games** checkbox have been replaced by these source settings. Saved choices are retained.

## Switching covers and choosing images

SGDB and Libretro keep separate downloaded caches. Saving a different preference displays the appropriate cached cover when available, or requests artwork using the new policy. It does not delete the other provider's cache. One registered game can show different preferred artwork in different lists.

Under **Artwork → Game artwork**, choose a SteamGridDB image, Libretro image or local file to create a manual override. The Libretro picker lets users select a console, search titles and preview covers. Both provider pickers offer a button to use the other provider instead. Choosing a provider in the picker does not change the list preference; selecting an image creates the manual override.

The **Games without artwork** picker follows the game's effective artwork preference. **Reset artwork** removes the manual choice and returns to automatic selection; a Libretro-preferred game can therefore show Libretro again after reset.

## Clear and redownload

**Artwork → Per-list artwork → Clear and redownload list artwork…** clears downloaded provider images and retry records for the selected games, then downloads using the saved policy. The operation requires confirmation. **Keep manually chosen images** is checked by default; unchecking it removes manual assignments without deleting original chosen files.

Native launcher artwork and game files are preserved. Games shared with other lists share provider caches, so refreshing them can affect those lists too. Each list has independent progress: another list can start refreshing while the first is running. Closing Settings does not stop these jobs; exiting the app cancels them.

## Libretro matching and layout

Libretro matches within the relevant console using names, normalized aliases, region/language/revision tags, disc numbers and article ordering. It avoids broad fuzzy matches that could select a sequel. RetroArch imports preserve the playlist database and original labels; cleaned display names do not erase that matching metadata. Unresolved or ambiguous platforms may have no match. Nintendo Switch has no catalogue in the catalogues checked during implementation.

Covers can be portrait, square or landscape. Loaded images supply their own proportions. Fit artwork follows those proportions; Fixed uses the configured panel height with colour-edge fill. Modern and Retro panel sizes can be adjusted together or independently. **Overlay → Artwork gap** controls spacing; per-list layout settings can override layout, panel sizes, outlines, angled sizing and grid dimensions.

Missing Libretro artwork uses console-specific placeholder proportions, with regional differences where known. Profiles were measured from 573 public covers across 120 platform libraries on 2026-11-17. Unknown platforms use square placeholders. These are estimates, not a guarantee for every edition. The original sample dimensions and filenames are recorded in [libretro-placeholder-samples.json](libretro-placeholder-samples.json).

## Cache and implementation references

Libretro reads public `Named_Boxarts` catalogues over HTTPS. Catalogues are cached for seven days under `PadLauncher.Data/artwork/libretro/catalogs`; images and six-hour missing-match markers live under `artwork/libretro`. Filename indexes are scoped to an artwork batch. Downloaded disk caches are separate from the [bounded decoded-image RAM cache](artwork-performance.md).

The measurement tools are `tools/sample-libretro-covers.py` and `tools/build-libretro-placeholder-profiles.py`. Sample downloads remain under ignored `bin/libretro-cover-samples/`; they are not bundled with the app.

Upstream references checked during implementation on 2026-11-17:

- [Libretro thumbnail documentation](https://docs.libretro.com/guides/roms-playlists-thumbnails/).
- [Thumbnail repositories](https://github.com/libretro-thumbnails/libretro-thumbnails).
- [Public thumbnail server](https://thumbnails.libretro.com/).

Automated checks cover provider order and restrictions, list inheritance, manual overrides, cached switches, matching, cancellation, placeholder proportions, panel geometry and startup artwork restoration. These checks do not establish that every game has a matching cover.
