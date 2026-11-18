// Generated from measured Libretro cover samples. See docs/libretro-placeholder-samples.json.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PadLauncher {
    public static class LibretroPlaceholderSizing {
        static readonly Dictionary<string,double> profiles=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase) {
            {"Amstrad - CPC",0.719},
            {"Atari - 2600",0.733},
            {"Atari - 5200",0.733},
            {"Atari - 7800",0.723},
            {"Atari - 8-bit",0.716},
            {"Atari - 8-bit Family",0.716},
            {"Atari - Jaguar",0.724},
            {"Atari - Lynx",0.815},
            {"Atari - ST",0.833},
            {"Atomiswave",0.731},
            {"Bandai - WonderSwan",0.823},
            {"Bandai - WonderSwan Color",0.693},
            {"Cannonball",1.0},
            {"Casio - Loopy",0.635},
            {"Casio - PV-1000",0.658},
            {"Cave Story",0.799},
            {"ChaiLove",1.977},
            {"Coleco - ColecoVision",0.732},
            {"Commodore - 64",0.704},
            {"Commodore - Amiga",0.833},
            {"Commodore - CD32",1.0},
            {"Commodore - CDTV",1.01},
            {"Commodore - Plus-4",0.79},
            {"Commodore - VIC-20",0.702},
            {"DOOM",0.802},
            {"DOS",0.829},
            {"Dinothawr",0.749},
            {"Emerson - Arcadia 2001",0.75},
            {"Entex - Adventure Vision",0.714},
            {"Epoch - Super Cassette Vision",1.293},
            {"FBNeo - Arcade Games",0.707},
            {"Fairchild - Channel F",0.742},
            {"Flashback",0.777},
            {"Funtech - Super Acan",0.571},
            {"GCE - Vectrex",0.743},
            {"GamePark - GP32",1.429},
            {"Handheld Electronic Game",1.503},
            {"Hartung - Game Master",0.728},
            {"LeapFrog - Leapster Learning Game System",0.708},
            {"LowRes NX",1.333},
            {"MAME",0.716},
            {"Magnavox - Odyssey2",0.734},
            {"Mattel - Intellivision",0.722},
            {"Microsoft - MSX",0.712},
            {"Microsoft - MSX2",0.712},
            {"Microsoft - Xbox",0.714},
            {"Microsoft - Xbox 360",0.709},
            {"MrBoom",1.6},
            {"NEC - PC Engine - TurboGrafx 16",1.0},
            {"NEC - PC Engine CD - TurboGrafx-CD",1.008},
            {"NEC - PC Engine SuperGrafx",1.125},
            {"NEC - PC-8001 - PC-8801",0.711},
            {"NEC - PC-88",0.711},
            {"NEC - PC-98",0.87},
            {"NEC - PC-FX",1.014},
            {"Nintendo - Family Computer Disk System",0.987},
            {"Nintendo - Game Boy",0.996},
            {"Nintendo - Game Boy Advance",1.0},
            {"Nintendo - Game Boy Color",1.0},
            {"Nintendo - GameCube",0.715},
            {"Nintendo - Nintendo 3DS",1.123},
            {"Nintendo - Nintendo 3DS (Digital)",1.264},
            {"Nintendo - Nintendo 64",1.365},
            {"Nintendo - Nintendo 64DD",1.158},
            {"Nintendo - Nintendo DS",1.111},
            {"Nintendo - Nintendo DSi",1.111},
            {"Nintendo - Nintendo Entertainment System",0.701},
            {"Nintendo - Pokemon Mini",0.985},
            {"Nintendo - Satellaview",0.551},
            {"Nintendo - Sufami Turbo",0.463},
            {"Nintendo - Super Nintendo Entertainment System",1.384},
            {"Nintendo - Virtual Boy",1.006},
            {"Nintendo - Wii",0.714},
            {"Nintendo - Wii U",0.714},
            {"Philips - CD-i",1.0},
            {"Philips - Videopac+",0.724},
            {"Quake",0.839},
            {"Quake II",0.846},
            {"Quake III",0.782},
            {"RCA - Studio II",1.31},
            {"RPG Maker",1.333},
            {"Rick Dangerous",0.826},
            {"SNK - Neo Geo",0.7},
            {"SNK - Neo Geo CD",1.0},
            {"SNK - Neo Geo Pocket",0.883},
            {"SNK - Neo Geo Pocket Color",0.986},
            {"ScummVM",1.154},
            {"Sega - 32X",0.706},
            {"Sega - Dreamcast",1.008},
            {"Sega - Game Gear",0.711},
            {"Sega - Master System - Mark III",0.706},
            {"Sega - Mega Drive - Genesis",0.71},
            {"Sega - Mega-CD - Sega CD",0.707},
            {"Sega - Naomi",0.712},
            {"Sega - Naomi 2",0.77},
            {"Sega - PICO",1.02},
            {"Sega - SG-1000",0.735},
            {"Sega - Saturn",0.648},
            {"Sharp - X1",0.755},
            {"Sharp - X68000",0.722},
            {"Sinclair - ZX 81",0.638},
            {"Sinclair - ZX Spectrum",0.836},
            {"Sony - PlayStation",1.0},
            {"Sony - PlayStation 2",0.715},
            {"Sony - PlayStation 3",0.862},
            {"Sony - PlayStation 3 (Downloadable)",0.802},
            {"Sony - PlayStation 4",0.799},
            {"Sony - PlayStation Portable",0.579},
            {"Sony - PlayStation Vita",0.786},
            {"Spectravideo - SVI-318 - SVI-328",0.731},
            {"TIC-80",1.765},
            {"The 3DO Company - 3DO",0.981},
            {"Thomson - MOTO",0.712},
            {"Tiger - Game.com",0.7},
            {"Tomb Raider",1.01},
            {"VTech - CreatiVision",0.721},
            {"VTech - V.Smile",0.735},
            {"Vircon32",1.442},
            {"Watara - Supervision",0.764},
            {"Wolfenstein 3D",0.852},
        };
        static readonly Dictionary<string,double> regional=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase) {
            {"Nintendo - Game Boy Advance|Japan",0.553},
            {"Nintendo - Game Boy Color|Japan",0.791},
            {"Nintendo - Game Boy|Japan",0.794},
            {"Nintendo - Nintendo 64|Japan",0.716},
            {"Nintendo - Nintendo Entertainment System|Japan",1.43},
            {"Nintendo - Super Nintendo Entertainment System|Japan",0.55},
            {"Sega - Game Gear|Japan",0.868},
            {"Sega - Mega-CD - Sega CD|Japan",1.0},
            {"Sega - Saturn|Japan",1.0},
        };
        sealed class Resolved {
            public Settings Settings;
            public string System,Title,Path,Emulator;
            public double Ratio;
        }
        static readonly ConditionalWeakTable<Game,Resolved> resolved=new ConditionalWeakTable<Game,Resolved>();
        public static double Ratio(Game game,Settings settings) {
            var cached=resolved.GetValue(game,key => new Resolved());
            if(cached.Settings==settings && cached.System==game.ArtworkSystem && cached.Title==game.ArtworkTitle && cached.Path==game.LaunchId && cached.Emulator==game.EmulatorId) return cached.Ratio;
            string region=null;
            var match=Regex.Match((game.ArtworkTitle ?? "")+" "+(game.LaunchId ?? ""),@"[\(\[](?<region>USA|Europe|Japan)(?=[,\s\)\]])",RegexOptions.IgnoreCase);
            if(match.Success) region=match.Groups["region"].Value;
            var systems=LibretroArtwork.Systems(game,settings);
            var ratios=new List<double>();
            foreach(string system in systems) {
                double ratio;
                if(!profiles.TryGetValue(system,out ratio)) {ratios.Clear(); break;}
                double specific;
                if(region!=null && regional.TryGetValue(system+"|"+region,out specific)) ratio=specific;
                ratios.Add(ratio);
            }
            // Unknown consoles and ambiguous mixed-system cores keep a neutral square.
            // This is an estimate only; actual images always supply their own dimensions.
            double result=ratios.Count>0 && ratios.Max()-ratios.Min()<.12 ? ratios.Average() : 1;
            cached.Settings=settings; cached.System=game.ArtworkSystem; cached.Title=game.ArtworkTitle;
            cached.Path=game.LaunchId; cached.Emulator=game.EmulatorId; cached.Ratio=result;
            return result;
        }
    }
}
