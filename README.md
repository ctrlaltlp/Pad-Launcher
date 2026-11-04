# Steam Pad Launcher

![Pad Launcher showing the .Installed Steam collection over a Windows desktop with the taskbar visible](assets/preview.png)

A transparent Windows overlay for Steam with an angled artwork carousel. Shows your games, the current collection and optionally a game title. It reads your existing Steam library, non-Steam shortcuts, custom artwork and collections.

Demo: https://www.youtube.com/watch?v=rmBuhIfx8JU

## Usage

**[Download SteamPadLauncher.exe](https://github.com/lpv11/pad-launcher-for-steam/releases/latest/download/SteamPadLauncher.exe)** for Windows 10/11 (64-bit).

The first launch puts the app in your notification area, so it's best if you have it on auto-start on windows, then bind a controller button to launch the .exe; that opens the carousel.

Steam is detected automatically. If detection fails, set Steam installation folder(the folder containing `steam.exe` and `steamapps`) in settings. Set `UserId` to choose a specific local Steam profile, or leave it null for automatic selection.

## Recommendation for non-steam games artwork

Create an account on: https://www.steamgriddb.com/. Download either BoilR https://github.com/PhilipK/BoilR or SGDBoop https://www.steamgriddb.com/boop to add artwork to your games. For BoilR you need to set your steamgriddb API key in BoilR from https://www.steamgriddb.com/profile/preferences/api. For SGDBoop, the easiest way of using artwork after installing is find your game in sgdb, click 'View original steam assets' and click 'Boop 'em all' - or do it manually through either program.

## Tray and overlay

The first launch starts quietly in the tray and prepares the library without displaying the overlay. **Double-click the tray icon to toggle the overlay on/off.** The app keeps the overlay, library and decoded artwork in memory when dismissed with Circle/B or after launching a game. Running the executable again signals the existing process to show the overlay.

## Controls

The normal overlay displays no control prompts. Press **Options** (Xbox: Start) or **F1** to briefly show the controls in your chosen button style. No settings dialog opens.

| Action | Controller | Keyboard |
| --- | --- | --- |
| Browse games | Left stick / D-pad | Left / Right |
| Browse games at 3× speed | Right stick left / right | — |
| Launch selected game | Cross / A | Enter |
| Change collection | L1 / R1, LB / RB | Page Up / Page Down |
| Move to next monitor | Square / Xbox X / Nintendo Y | M |
| Exit app completely (overlay open) | Hold Circle / Xbox B / Nintendo A for 1 second | — |
| Brief control hint | Options / Start | F1 |
| Refresh games, artwork and collection membership | Hold Triangle / Y for 1 second | F5 |

## Manual Configuration

The app creates `%LOCALAPPDATA%\PadLauncher\settings.json`. Exit the app completely before editing it. `settings.example.json` in this project shows the available fields.

## Inspiration

https://github.com/motor-dev/wallpaperCarousel
