"""Measure public Libretro box art; samples stay in ignored bin/, never app data.

Run: python tools/sample-libretro-covers.py
The report records source filenames and dimensions for reproducible placeholder ratios.
"""
import concurrent.futures
import hashlib
import html
import io
import json
from pathlib import Path
import re
import statistics
import sys
import urllib.parse
import urllib.request
from PIL import Image

ROOT = "https://thumbnails.libretro.com/"
OUT = Path(__file__).resolve().parents[1] / "bin" / "libretro-cover-samples"
OUT.mkdir(parents=True, exist_ok=True)


def get(url):
    with urllib.request.urlopen(urllib.request.Request(url, headers={"User-Agent": "PadLauncher-cover-survey/1.0"}), timeout=25) as response:
        return response.read()


def links(url):
    return [urllib.parse.unquote(html.unescape(value)) for value in re.findall(r'href="([^"]+)"', get(url).decode("utf-8"))]


def sample(system):
    folder = OUT / hashlib.sha256(system.encode()).hexdigest()[:12]
    folder.mkdir(exist_ok=True)
    result = {"system": system, "samples": []}
    try:
        base = ROOT + urllib.parse.quote(system) + "/Named_Boxarts/"
        files = sorted(value for value in links(base) if value.lower().endswith(".png") and "/" not in value)
        ordinary = [value for value in files if not re.search(r"\((?:Proto|Beta|Sample|Unl|Aftermarket|Demo|Virtual Console|GameCube|eShop|Preview)[^)]*\)|\[[hbt]", value, re.I)]
        if len(ordinary) >= 5:
            files = ordinary
        chosen = []
        for region in ("USA", "Europe", "Japan"):
            candidates = [value for value in files if "(" + region in value]
            if candidates:
                for fraction in ((.25, .5, .75) if len(sys.argv) > 1 else (.5,)):
                    value = candidates[min(len(candidates) - 1, int(len(candidates) * fraction))]
                    if value not in chosen:
                        chosen.append(value)
        for fraction in (.15, .4, .65, .9):
            if files and len(chosen) < 5:
                value = files[min(len(files) - 1, int(len(files) * fraction))]
                if value not in chosen:
                    chosen.append(value)
        for filename in chosen:
            path = folder / (hashlib.sha256(filename.encode()).hexdigest()[:16] + ".png")
            if not path.exists():
                path.write_bytes(get(base + urllib.parse.quote(filename)))
            try:
                with Image.open(path) as image:
                    width, height = image.size
            except Exception as error:
                result.setdefault("skipped", []).append({"file": filename, "error": str(error)})
                continue
            result["samples"].append({"file": filename, "width": width, "height": height, "ratio": round(width / height, 4)})
        ratios = [entry["ratio"] for entry in result["samples"]]
        result["median"] = round(statistics.median(ratios), 4) if ratios else None
    except Exception as error:
        result["error"] = str(error)
    print(system + ": " + str(result.get("median", result.get("error", "no samples"))), flush=True)
    return result


systems = sys.argv[1:] or sorted(value.rstrip("/") for value in links(ROOT) if value.endswith("/") and not value.startswith(("/", ".", "http")) and value != "stats/")
with concurrent.futures.ThreadPoolExecutor(max_workers=6) as pool:
    results = list(pool.map(sample, systems))
if len(sys.argv) > 1 and (OUT / "report.json").exists():
    previous = json.loads((OUT / "report.json").read_text(encoding="utf-8"))
    results += [entry for entry in previous if entry["system"] not in systems and entry["system"] != "stats"]
results.sort(key=lambda entry: entry["system"])
(OUT / "report.json").write_text(json.dumps(results, ensure_ascii=False, indent=2), encoding="utf-8")
print("Report: " + str(OUT / "report.json"), flush=True)
