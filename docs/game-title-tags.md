# Import title cleanup

**Normalise titles** sits beside Remove tags in individual and bulk emulator-library import reviews. It moves a trailing `, The` to the front, including before subtitles or metadata: `Legend of Zelda, The - A Link to the Past (USA)` becomes `The Legend of Zelda - A Link to the Past (USA)`. It previews selected titles with a Select all toggle and changes only checked proposals. Changes remain in the import draft until import completes, and preserve ROM paths, cores, original artwork labels and emulator-owned playlists. Already-normalised titles are unchanged.

**Remove tags from game titles** is available after reviewing games in individual and bulk emulator-library imports. It uses the selected games, previews old/new titles, and lets the user toggle all proposed changes or choose individual changes. Applying the preview changes only the import draft; completing the import saves the names, including explicitly cleaned existing entries. Cancelling the import discards the draft.

The initial catalogue was checked against five configured RetroArch playlists and registered game paths. Frequently observed tags included `(USA)`, `(USA, Europe)`, `(Europe)`, `(Japan)`, `(Japan, USA)`, `(World)`, language combinations such as `(En,Fr,De,Es,It)`, `(Rev 1)`, `(Rev A)`, `(SGB Enhanced)`, `(GB Compatible)`, `(CGB+SGB Enhanced)`, `(Virtual Console)`, `(Unl)`, `(NP)`, `[n]`, `[i]`, translation credits such as `[T-En by Aeon Genesis v1.00]`, and `[Add by ...]` patch credits.

Recognised categories:

- Bracketed regions/countries and combinations, including PAL, NTSC, USA, Europe, Japan, World and other common regional releases.
- Bracketed language codes and comma-separated combinations.
- Revisions, version numbers, supported serial/title IDs, compatibility metadata and the release tags above.
- Known square-bracket dump flags, translation tags and patch credits.
- Explicit terminal `- PAL` / `- NTSC` markers.

Disc numbers, demo/beta identifiers, unknown parenthetical text and subtitles such as `[Legend of the Crystals]` are preserved. Unbracketed country words within a title are preserved. A title is never changed to an empty string.

Paths, files, emulator playlists, launch arguments, cores and original artwork matching metadata are unchanged. Cleanup is explicit, not automatic on import or refresh.
