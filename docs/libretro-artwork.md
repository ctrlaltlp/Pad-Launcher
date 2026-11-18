# Libretro artwork fallback

Libretro requires no account or API key. The wizard offers per-emulator artwork preferences; individual lists can override them in Artwork settings. There is no global Libretro checkbox; the effective source policy controls provider availability.

Existing launcher/local/chosen artwork and cached covers are preserved. For a missing emulator cover, Pad Launcher follows the effective source policy, including its permitted fallback. Libretro still works when there is no SteamGridDB key or that service is unavailable. Disabling downloads leaves already cached images available.

The service reads public `Named_Boxarts` directory catalogs over HTTPS from `thumbnails.libretro.com`, matches within the relevant console, and downloads only the selected PNG. Catalogs are cached for seven days under `PadLauncher.Data/artwork/libretro/catalogs`. Covers and six-hour missing-match markers are cached under `artwork/libretro`. The in-memory filename indexes last only for an artwork batch, rather than retaining all console catalogs for the app's lifetime.

Matching uses exact names and normalized aliases, region/language/revision suffixes, disc numbers, and database article ordering. It does not use a broad fuzzy match that could substitute a sequel. Current game names take precedence over older imported names. Original ROM names help retain region-specific matches.

Most configured emulators identify one console directly. For Dolphin and mGBA, extensions and console-specific folders are used where possible; if a title matches multiple distinct consoles and the platform cannot be resolved, the fallback skips it. RetroArch imports preserve the playlist item's `db_name` (or a recognized system playlist name) and original label. Existing/manual RetroArch entries can also use the core's `.info` database field or an unambiguous ROM extension. Mixed playlists such as Favourites are not treated as consoles. The currently published Libretro catalog does not include Nintendo Switch, so Eden still relies on its other artwork sources.

Libretro panels keep the reference panel height in all three layouts. Fit artwork follows the cover proportions; Fixed retains the original panel heights and widens Libretro panels so their top/bottom colour-edge fill matches the modern portrait reference. The Overlay artwork-gap slider sets the minimum visible gap for modern and retro artwork (0–64, default 16). Angled accounts for different panel heights and the slant; Grid packs each row by its actual panel widths. Grid retains a common height, reduced uniformly only when a row would exceed screen width. With Grid outlines disabled, the selected panel enlarges by up to 12% within the viewport without changing row sizes or navigation.

`Choose Libretro image…` is also available under Artwork → Game artwork. Choose a console, search its cover titles, preview an image, and select `Use image`. This manual picker works even when automatic Libretro downloads are disabled. A selected image takes priority over automatic artwork; the existing clear/reset artwork action removes the choice.

## Verified references (2026-11-17)

- [Libretro thumbnail documentation](https://docs.libretro.com/guides/roms-playlists-thumbnails/): folder layout, database/playlist names and character substitutions.
- [Thumbnail repository](https://github.com/libretro-thumbnails/libretro-thumbnails): box-art categories and naming conventions.
- [Public thumbnail server](https://thumbnails.libretro.com/): actual console folder names and direct HTTPS image access.
- [PlayStation box-art catalog](https://thumbnails.libretro.com/Sony%20-%20PlayStation/Named_Boxarts/): region/disc naming and live cover-download verification.

Checks cover disabled defaults, platform isolation, RetroArch metadata, title aliases, multiple discs, sequel rejection, malformed catalog links, image proportions, cache reloads, SteamGridDB precedence, operation without keys, provider failures and cancellation. A real PlayStation cover was downloaded and validated from the public server without authentication.

## Per-list source

Under Artwork → Per-list artwork, select a list and choose Automatic, SteamGridDB, or Libretro, then Save changes. This choice is independent of whether the list inherits its layout. Preferred covers are cached separately without changing the shared game, so one game can use different artwork in different lists. Manually chosen images take priority, and an unavailable match retains existing artwork. SteamGridDB downloads require its API key to be enabled; an explicit Libretro list choice works independently of the global fallback checkbox.

## Panel sizes and manual artwork

Overlay has Modern panel size and Retro panel size controls. Their lock is enabled by default: changing either changes both while preserving the matched reference heights. Unlocking allows independent display scales. Per-list layout settings offer the same two sizes and lock when global layout inheritance is disabled.

Choosing an image from SteamGridDB, Libretro, or a local file immediately rebuilds the active list's artwork display copies. Reset artwork clears the manual choice and returns to the list's preferred automatic source; a Libretro-preferred list can therefore legitimately show Libretro again after resetting.

## Selecting list artwork sources

Choose each list's source in Artwork → Per-list artwork, or use RetroArch lists settings for shared defaults. The redundant Artwork → Libretro artwork lists checklist has been removed. Its saved choices remain in the same per-list source settings; no migration or reset is needed. Use emulator defaults shows a Currently using note identifying shared RetroArch settings or the emulator defaults it inherits. Mixed lists show the applicable emulator sources separately.

The dropdown offers **Use emulator defaults**, two provider preferences with the other provider as fallback, and **Only SteamGridDB / Only Libretro**. Preferences actively try the permitted fallback; Only choices exclude it. Manual images always take priority.

Clicking a game in **Games without artwork** opens the picker for its effective preference. The active overlay list supplies context when it contains that game; otherwise its list preferences and the RetroArch default are consulted. Automatic with no Libretro preference starts in SteamGridDB. Both pickers offer **Choose from … instead…**, closing the old picker and retaining the same game and selection callback. Switching providers does not change list preferences or save an image; choosing an image still creates an explicit manual override.
# RetroArch default and list artwork refresh

Selecting Libretro as the shared RetroArch artwork source also enables the RetroArch-wide preference, including games in mixed Automatic lists; an older false preference flag cannot negate that source choice. Explicit individual list choices and manual images still win.

Forced list artwork refresh clears the relevant Libretro catalogue caches as well as images/retry records, and explicit source refresh downloads from that provider without first fetching SteamGridDB fallback art. Completion reports refreshed, manually kept and missing images rather than claiming that every game obtained a match. Automatic-source refresh retains the normal fallback behaviour. Libretro itself includes portrait, square and landscape box art; a portrait-shaped cover does not necessarily indicate SteamGridDB artwork.

Missing artwork uses console-specific template proportions when the effective source is Libretro, including an Automatic per-list choice inheriting Libretro from RetroArch lists settings. The offline profiles were measured from 573 public Libretro covers across 120 platform libraries on 2026-11-17. NES uses portrait packaging, GBA square, and western SNES/N64 landscape; known Japanese packaging differences use preserved region tags from the imported title or ROM path. Unknown consoles, unavailable catalogues and ambiguous mixed-system cores use a neutral square fallback. Explicit per-list SteamGridDB choices keep modern placeholders. Loaded images always supply their own dimensions; their layout and artwork are unchanged.

Measurements and original sample filenames are recorded in [libretro-placeholder-samples.json](libretro-placeholder-samples.json). Samples cover representative images, not every edition: unusual boxes and untagged regional variants may differ. Six of the 126 inspected catalogues had no usable covers; eleven had fewer than three usable samples. The measurement tools are `tools/sample-libretro-covers.py` and `tools/build-libretro-placeholder-profiles.py`; downloaded images stay under ignored `bin/libretro-cover-samples/` and are not bundled with the app. Placeholder sizing uses small built-in tables and a weak per-game cache, with no extra downloads or recurring catalogue scans during rendering.

**Lists → RetroArch lists settings** provides shared artwork source, layout, outlines, modern/retro panel sizes and size locking, angled sizing, and grid columns/rows. **Artwork source** controls the shared list preference. The wizard artwork page selects per-emulator source defaults; detection does not change artwork preferences. Automatic RetroArch lists, imported playlists and RetroArch-only custom/combined lists inherit these defaults; mixed lists do not inherit the group layout. Individual per-list layout/artwork overrides take priority. Lists created later inherit the same defaults without copying settings into each list. Select **Use global Overlay settings** to make the shared layout follow global settings.

List artwork refreshes have independent running states and progress messages. Selecting another list allows starting its refresh while existing jobs continue; only an already-running list's own refresh button is disabled. Closing Settings does not stop those jobs, and exiting the app cancels them.


RetroArch game selectors show **Game title · RetroArch · Console**, using imported database metadata and existing console detection. Unknown consoles are labelled explicitly rather than guessed.

Artwork → Per-list artwork offers **Clear and redownload list artwork…**. It clears the selected games' downloaded provider images and retry records, then downloads again using the saved source preference. **Keep manually chosen images** is checked by default. Unchecking removes manual assignments without deleting the original chosen files. Native launcher artwork, game files and unrelated games' caches are preserved. Games shared with other lists share refreshed caches too.



## Artwork source policies (2026-11-18)

Wizard artwork setup now offers one source choice per emulator. DuckStation, mGBA and melonDS prefer Libretro by default; other emulators, including xemu, prefer SteamGridDB. The wizard has no global SteamGridDB checkbox. Editing or omitting the API key preserves source choices. A missing-key warning appears only if a selected emulator policy includes SteamGridDB. The old RetroArch detection prompt and wizard-wide Libretro checkbox are removed. Emulator defaults can be edited later under Artwork.

Per-list artwork preferences live under Artwork → Per-list artwork, separate from Lists → Per-list layout. The four explicit choices are Prefer Libretro with SteamGridDB fallback, Prefer SteamGridDB with Libretro fallback, Only SteamGridDB and Only Libretro. Use emulator defaults inherits shared RetroArch list defaults or emulator defaults where applicable. Manual images remain authoritative. Both providers keep independent cache files: saving another preference switches to a cached cover immediately when available, otherwise downloads missing artwork. Only choices do not request or display the other provider as fallback.

Clear and redownload remains an explicit, confirmed operation, separate from changing preference. Switching sources does not delete either cached provider’s covers. SteamGridDB downloads still need its enabled API key.
