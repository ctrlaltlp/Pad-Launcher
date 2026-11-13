# Pad Launcher

![Pad Launcher showing the .Installed Steam collection over a Windows desktop with the taskbar visible](assets/preview.png)

A transparent Windows overlay for Steam with an angled artwork carousel. Shows your games, the current collection and optionally a game title. It reads your existing Steam library, non-Steam shortcuts, custom artwork and collections.

Launchers supported: **Steam**, **GOG Galaxy**, **Epic Games Store**, **EA App**, **Ubisoft Connect**, **Battle.net**, **Rockstar Games Launcher**, manual **Playnite** imports, and custom games. Optional **SteamGridDB** artwork uses your API key and is cached locally.

## Emulators

Supports **DuckStation, PCSX2, RPCS3, Xenia, Dolphin, Eden, melonDS, Vita3K, RetroArch, PPSSPP, mGBA, Cemu, Azahar and shadPS4**. Configure emulator paths, import libraries (including RetroArch playlists), scan ROM folders or add games manually. Core selection and launch arguments are configurable; imported games can be organised into lists.

## Usage

**[Download the latest PadLauncher.exe](https://github.com/lpv11/Pad-Launcher/releases/latest/download/PadLauncher.exe)** for Windows 10/11 (64-bit).

Place `PadLauncher.exe` in its own writable folder before running it. The app creates `PadLauncher.Data` and `PadLauncher.Update` folders beside the executable for settings, artwork and temporary update files.

The first launch puts the app in your notification area, so it's best if you have it on auto-start on windows, then bind a controller button to launch the .exe; that opens the carousel.

Steam is detected automatically. If detection fails, set Steam installation folder(the folder containing `steam.exe` and `steamapps`) in settings. Set `UserId` to choose a specific local Steam profile, or leave it null for automatic selection.

## Tray and overlay

The first launch starts quietly in the tray and prepares the library without displaying the overlay. **Double-click the tray icon to toggle the overlay on/off.** The app keeps the overlay, library and decoded artwork in memory when dismissed with Circle/B or after launching a game. Running the executable again signals the existing process to show the overlay.

## Controls

Choose PlayStation, Xbox or Nintendo button labels in **Settings → Controls**. Options / Start (or F1) briefly shows control hints and the game title.

| Action | Controller | Keyboard / mouse |
| --- | --- | --- |
| Browse games | D-pad / left stick; right stick for faster browsing | Left / Right |
| Move between grid rows | D-pad / left stick up and down | Up / Down |
| Launch selected game | Cross / Xbox A / Nintendo B | Enter, or click a game panel |
| Dismiss overlay | Tap Circle / Xbox B / Nintendo A | Esc |
| Exit app completely | Hold Circle / Xbox B / Nintendo A for 1 second | Tray → Exit app |
| Change list | L1 / R1, Xbox LB / RB, Nintendo L / R | Page Up / Page Down |
| Move to next monitor | Square / Xbox X / Nintendo Y | M |
| Refresh games, artwork and lists | Tap Triangle / Xbox Y / Nintendo X | F5 |
| Toggle favourite | R3 / right stick click | F |
| Jump to a chosen list (optional) | L3 / left stick click | — |
| Brief control hints and game title | Options / Start / Nintendo + | F1 |
| Open Settings | — | Right-click the overlay or use the tray menu |

Left and right stick speeds are adjustable in Controls (defaults: 1× and 3×). You can also enable up/down list switching for Angled and Rectangular layouts. Grid keeps up/down row navigation. R3 always toggles favourites; show or hide the Favourites list under **Lists → Overlay lists**.

## Inspiration

https://github.com/motor-dev/wallpaperCarousel
