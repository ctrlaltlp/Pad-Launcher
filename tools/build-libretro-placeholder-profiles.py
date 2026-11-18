"""Generate offline placeholder profiles from sample-libretro-covers.py's report."""
import json
from pathlib import Path
import re
import statistics

ROOT = Path(__file__).resolve().parents[1]
report = json.loads((ROOT / "bin/libretro-cover-samples/report.json").read_text(encoding="utf-8"))
profiles = {}
regional = {}
for entry in report:
    if not entry.get("median"):
        continue
    system = entry["system"]
    profiles[system] = round(entry["median"], 3)
    for region in ("USA", "Europe", "Japan"):
        ratios = [sample["ratio"] for sample in entry["samples"] if re.search(r"\(" + region + r"(?=[,)])", sample["file"])]
        if len(ratios) < 3:
            continue
        median = statistics.median(ratios)
        deviation = statistics.median(abs(value - median) for value in ratios)
        if abs(median - entry["median"]) > .12 and deviation < .08:
            regional[system + "|" + region] = round(median, 3)


def rows(values):
    return "\n".join("            {" + json.dumps(key, ensure_ascii=True) + "," + str(value) + "}," for key, value in sorted(values.items()))


source = '''// Generated from measured Libretro cover samples. See docs/libretro-placeholder-samples.json.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace PadLauncher {
    public static class LibretroPlaceholderSizing {
        static readonly Dictionary<string,double> profiles=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase) {
DEFAULTS
        };
        static readonly Dictionary<string,double> regional=new Dictionary<string,double>(StringComparer.OrdinalIgnoreCase) {
REGIONS
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
            var match=Regex.Match((game.ArtworkTitle ?? "")+" "+(game.LaunchId ?? ""),@"[\\(\\[](?<region>USA|Europe|Japan)(?=[,\\s\\)\\]])",RegexOptions.IgnoreCase);
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
'''.replace("DEFAULTS", rows(profiles)).replace("REGIONS", rows(regional))
(ROOT / "src/LibretroPlaceholderSizing.cs").write_text(source, encoding="utf-8")

clean = [{"system": entry["system"], "median": entry.get("median"), "samples": entry["samples"]} for entry in report]
document = {"measured": "2026-11-17", "source": "https://thumbnails.libretro.com/", "units": "width / height", "profiles": profiles, "regionalOverrides": regional, "platforms": clean}
(ROOT / "docs/libretro-placeholder-samples.json").write_text(json.dumps(document, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
print(str(len(profiles)) + " measured platform profiles; " + str(sum(len(entry["samples"]) for entry in report)) + " sample covers.")
print("Regional overrides: " + json.dumps(regional))
