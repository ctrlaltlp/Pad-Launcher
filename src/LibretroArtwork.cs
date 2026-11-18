using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace PadLauncher {
    public sealed class LibretroArtwork {
        static readonly object downloadGate=new object();
        const string Root="https://thumbnails.libretro.com/";
        readonly Dictionary<string,List<string>> catalogs=new Dictionary<string,List<string>>(StringComparer.OrdinalIgnoreCase);
        readonly Dictionary<string,BoxartIndex> indexes=new Dictionary<string,BoxartIndex>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> failedCatalogs=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        readonly object catalogGate=new object();
        static string Hash(string value) {using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? ""))).Replace("-","");}
        public static string CachePath(Game game) {return Path.Combine(Storage.Root,"artwork","libretro",Hash(game.Id+"|"+game.Name+"|"+game.ArtworkSystem+"|"+game.ArtworkTitle)+".img");}
        public static void ClearCatalogCaches(IEnumerable<Game> games,Settings settings,CancellationToken token) {
            var systems=games.SelectMany(game => Systems(game,settings)).Distinct().ToList();
            lock(downloadGate) lock(ArtworkService.CacheWriteGate) foreach(string system in systems) {
                token.ThrowIfCancellationRequested(); string path=Path.Combine(Storage.Root,"artwork","libretro","catalogs",Hash(system)+".json");
                if(File.Exists(path)) File.Delete(path);
            }
        }
        public static bool Usable(string path) {
            try {using(var image=System.Drawing.Image.FromFile(path)) return image.Width>=64 && image.Height>=64 && image.Width<=8192 && image.Height<=8192;} catch {return false;}
        }
        public static bool IsChosenPath(string path) {return Path.GetFileName(path ?? "").StartsWith("libretro-",StringComparison.OrdinalIgnoreCase);}
        public static string NormalizeDatabase(string value) {
            value=(value ?? "").Trim();
            if(value.EndsWith(".lpl",StringComparison.OrdinalIgnoreCase) || value.EndsWith(".rdb",StringComparison.OrdinalIgnoreCase)) value=value.Substring(0,value.Length-4);
            if(!value.Contains(" - ") && !new[]{"MAME","FBNeo - Arcade Games","DOS","ScummVM","DOOM","Quake","Quake II","Quake III","Cave Story","Wolfenstein 3D","Atomiswave"}.Contains(value,StringComparer.OrdinalIgnoreCase)) return null;
            return value.Length>0 && value.Length<150 && !value.Contains("/") && !value.Contains("\\") && value!=".." ? value : null;
        }
        public static string[] Systems(Game game,Settings settings) {
            if(game.Source!="emulator") return new string[0];
            string extension=Path.GetExtension(game.LaunchId ?? "").ToLowerInvariant();
            switch(game.EmulatorId) {
                case "duckstation": return new[]{"Sony - PlayStation"};
                case "pcsx2": return new[]{"Sony - PlayStation 2"};
                case "rpcs3": return new[]{"Sony - PlayStation 3","Sony - PlayStation 3 (Downloadable)"};
                case "xenia": return new[]{"Microsoft - Xbox 360"};
                case "xemu": return new[]{"Microsoft - Xbox"};
                case "ppsspp": return new[]{"Sony - PlayStation Portable"};
                case "vita3k": return new[]{"Sony - PlayStation Vita"};
                case "shadps4": return new[]{"Sony - PlayStation 4"};
                case "cemu": return new[]{"Nintendo - Wii U"};
                case "azahar": return new[]{"Nintendo - Nintendo 3DS","Nintendo - Nintendo 3DS (Digital)"};
                case "melonds": return new[]{"Nintendo - Nintendo DS","Nintendo - Nintendo DSi"};
                case "dolphin":
                    if(extension==".gcm" || Regex.IsMatch(game.LaunchId ?? "",@"(?:^|[\\/])(?:Nintendo - )?(?:GameCube|GCN)(?:[\\/]|$)",RegexOptions.IgnoreCase)) return new[]{"Nintendo - GameCube"};
                    if(extension==".wbfs" || extension==".wad" || Regex.IsMatch(game.LaunchId ?? "",@"(?:^|[\\/])(?:Nintendo - )?Wii(?:[\\/]|$)",RegexOptions.IgnoreCase)) return new[]{"Nintendo - Wii"};
                    return new[]{"Nintendo - GameCube","Nintendo - Wii"};
                case "mgba":
                    if(extension==".gba") return new[]{"Nintendo - Game Boy Advance"};
                    if(extension==".gbc") return new[]{"Nintendo - Game Boy Color"};
                    if(extension==".gb" || extension==".sgb") return new[]{"Nintendo - Game Boy"};
                    return new[]{"Nintendo - Game Boy Advance","Nintendo - Game Boy Color","Nintendo - Game Boy"};
                case "retroarch":
                    string database=NormalizeDatabase(game.ArtworkSystem); if(database!=null) return new[]{database};
                    var config=EmulatorLibrary.Config(settings,"retroarch");
                    var entry=config==null ? null : (config.Games ?? new List<EmulatorGame>()).FirstOrDefault(item => item.Id==game.Id);
                    string core=entry==null ? (config==null ? null : config.CorePath) : entry.CorePath ?? config.CorePath;
                    if(!String.IsNullOrWhiteSpace(core)) {
                        string file=Path.GetFileNameWithoutExtension(core)+".info",folder=Path.GetDirectoryName(core);
                        foreach(string info in new[]{Path.Combine(folder,file),Path.Combine(folder,"..","info",file)}) if(File.Exists(info)) {
                            var match=Regex.Match(File.ReadAllText(info),@"(?m)^\s*database\s*=\s*""([^""]+)""");
                            if(match.Success) return match.Groups[1].Value.Split('|').Select(NormalizeDatabase).Where(value => value!=null).Distinct().ToArray();
                        }
                    }
                    switch(extension) {
                        case ".nes": return new[]{"Nintendo - Nintendo Entertainment System"};
                        case ".fds": return new[]{"Nintendo - Family Computer Disk System"};
                        case ".sfc": case ".smc": case ".snes": return new[]{"Nintendo - Super Nintendo Entertainment System"};
                        case ".gba": return new[]{"Nintendo - Game Boy Advance"};
                        case ".gb": return new[]{"Nintendo - Game Boy"};
                        case ".gbc": return new[]{"Nintendo - Game Boy Color"};
                        case ".nds": return new[]{"Nintendo - Nintendo DS"};
                        case ".n64": case ".z64": case ".v64": return new[]{"Nintendo - Nintendo 64"};
                        case ".md": case ".gen": case ".smd": return new[]{"Sega - Mega Drive - Genesis"};
                        case ".sms": return new[]{"Sega - Master System - Mark III"};
                        case ".gg": return new[]{"Sega - Game Gear"};
                    }
                    break;
            }
            return new string[0];
        }
        public static string TitleKey(string title) {
            title=Regex.Replace(title ?? "",@"\s*[\(\[](?:(?:USA|US|Europe|EUR|Japan|JPN|World|Asia|Korea|Australia|Canada|Brazil|China)(?:[^)\]]*)|(?:En|Ja|Fr|De|Es|It|Nl|Pt|Ru|Ko|Zh)(?:[, -](?:En|Ja|Fr|De|Es|It|Nl|Pt|Ru|Ko|Zh))*|(?:Disc|Disk|CD)\s*\d+[^)\]]*|Rev(?:ision)?\s*[^)\]]*|v\d+(?:\.\d+)*)[\)\]]","",RegexOptions.IgnoreCase);
            title=Regex.Replace(title,@"^(.*?),\s*(The|A|An)(?=\s*[-(:]|$)(.*)$","$2 $1$3",RegexOptions.IgnoreCase);
            title=ArtworkService.EmulatorArtworkTitle(title);
            return ArtworkService.Normalize(title);
        }
        static string SafeTitle(string value) {return Regex.Replace(value ?? "",@"[&*/:`<>?""\\|]","_");}
        static bool SafeFile(string file) {return !String.IsNullOrEmpty(file) && file.Length<512 && file.EndsWith(".png",StringComparison.OrdinalIgnoreCase) && !file.Contains("/") && !file.Contains("\\");}
        sealed class BoxartIndex {
            readonly Dictionary<string,string> exact=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            readonly Dictionary<string,List<string>> normalized=new Dictionary<string,List<string>>();
            public BoxartIndex(IEnumerable<string> files) {
                foreach(string file in files.Where(SafeFile).Distinct()) {
                    exact[file]=file; string key=TitleKey(Path.GetFileNameWithoutExtension(file)); List<string> values;
                    if(!normalized.TryGetValue(key,out values)) normalized[key]=values=new List<string>(); values.Add(file);
                }
            }
            public string Find(Game game) {
            var titles=new[]{game.Name,game.ArtworkTitle,Path.GetFileNameWithoutExtension(game.LaunchId ?? "")}.Where(value => !String.IsNullOrWhiteSpace(value)).ToList();
            foreach(string title in titles) {
                string key=TitleKey(title); if(key.Length==0) continue;
                foreach(string equivalent in titles.Where(value => TitleKey(value)==key).Reverse()) {
                    string file; if(exact.TryGetValue(SafeTitle(equivalent)+".png",out file)) return file;
                }
                List<string> values;
                if(normalized.TryGetValue(key,out values)) return values.OrderBy(file => file.IndexOf("(USA",StringComparison.OrdinalIgnoreCase)>=0 ? 0 : file.IndexOf("(Europe",StringComparison.OrdinalIgnoreCase)>=0 ? 1 : 2).ThenBy(file => file,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            }
            // Only exact normalized titles qualify. Never select a merely similar sequel.
            return null;
            }
            public List<string> Search(string query) {
                string key=TitleKey(query);
                return normalized.Where(pair => pair.Key.Contains(key)).SelectMany(pair => pair.Value).Distinct().OrderBy(file => file,StringComparer.CurrentCultureIgnoreCase).Take(200).ToList();
            }
        }
        public static string Match(Game game,IEnumerable<string> files) {return new BoxartIndex(files).Find(game);}
        public List<string> Platforms(CancellationToken token) {
            string relative=Path.Combine("artwork","libretro","catalogs","systems.json"),path=Path.Combine(Storage.Root,relative);
            if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>DateTime.UtcNow.AddDays(-7)) {
                var cached=Storage.Read<List<string>>(relative); if(cached!=null) return cached.Select(NormalizeDatabase).Where(value => value!=null).ToList();
            }
            string html=Encoding.UTF8.GetString(ArtworkService.Fetch(Root,null,token));
            var systems=new List<string>();
            foreach(Match match in Regex.Matches(html,@"href=""([^""?]+)/""",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2))) {
                string system=NormalizeDatabase(Uri.UnescapeDataString(WebUtility.HtmlDecode(match.Groups[1].Value)));
                if(system!=null) systems.Add(system);
            }
            systems=systems.Distinct().OrderBy(system => system,StringComparer.CurrentCultureIgnoreCase).ToList();
            if(systems.Count==0) throw new IOException("Libretro returned no console catalog.");
            Directory.CreateDirectory(Path.GetDirectoryName(path)); Storage.Write(relative,systems); return systems;
        }
        public List<string> Search(string system,string query,CancellationToken token) {
            if(NormalizeDatabase(system)==null) throw new ArgumentException("Choose a console first.");
            lock(catalogGate) {
                BoxartIndex index;
                if(!indexes.TryGetValue(system,out index)) indexes[system]=index=new BoxartIndex(Catalog(system,token));
                token.ThrowIfCancellationRequested(); return index.Search(query);
            }
        }
        public static string ImageUrl(string system,string file) {
            if(NormalizeDatabase(system)==null || !SafeFile(file)) throw new ArgumentException("Choose a valid Libretro cover.");
            return Root+Uri.EscapeDataString(system)+"/Named_Boxarts/"+Uri.EscapeDataString(file);
        }
        public string SaveChoice(Game game,string system,string file,CancellationToken token) {
            string url=ImageUrl(system,file);
            if(!Catalog(system,token).Contains(file)) throw new ArgumentException("This cover is no longer listed in the selected console catalog.");
            byte[] bytes=ArtworkService.Fetch(url,null,token);
            using(var stream=new MemoryStream(bytes)) using(var image=System.Drawing.Image.FromStream(stream)) {
                if(image.Width<64 || image.Height<64 || image.Width>8192 || image.Height>8192) throw new IOException("This cover is not a supported image.");
            }
            token.ThrowIfCancellationRequested();
            string relative=Path.Combine("artwork","chosen","libretro-"+Hash(game.Id+"|"+system+"|"+file)+".img"),path=Path.Combine(Storage.Root,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,bytes); return relative;
        }
        List<string> Catalog(string system,CancellationToken token) {
            lock(catalogGate) return ReadCatalog(system,token);
        }
        List<string> ReadCatalog(string system,CancellationToken token) {
            if(failedCatalogs.Contains(system)) throw new IOException("Libretro catalog is temporarily unavailable.");
            List<string> files; if(catalogs.TryGetValue(system,out files)) return files;
            string relative=Path.Combine("artwork","libretro","catalogs",Hash(system)+".json"),path=Path.Combine(Storage.Root,relative);
            if(File.Exists(path) && File.GetLastWriteTimeUtc(path)>DateTime.UtcNow.AddDays(-7)) {
                files=Storage.Read<List<string>>(relative); if(files!=null) {catalogs[system]=files; return files;}
            }
            catalogs[system]=new List<string>();
            try {
                string html=Encoding.UTF8.GetString(ArtworkService.Fetch(Root+Uri.EscapeDataString(system)+"/Named_Boxarts/",null,token));
                files=new List<string>();
                foreach(Match match in Regex.Matches(html,@"href=""([^""]+\.png)""",RegexOptions.IgnoreCase,TimeSpan.FromSeconds(2))) {
                    string name=Uri.UnescapeDataString(WebUtility.HtmlDecode(match.Groups[1].Value));
                    if(name.Length<512 && !name.Contains("/") && !name.Contains("\\")) files.Add(name);
                }
            } catch(WebException error) {
                var response=error.Response as HttpWebResponse;
                bool missing=response!=null && response.StatusCode==HttpStatusCode.NotFound;
                if(response!=null) response.Dispose(); if(!missing) {failedCatalogs.Add(system); throw;}
                files=new List<string>();
            } catch(Exception) {failedCatalogs.Add(system); throw;}
            Directory.CreateDirectory(Path.GetDirectoryName(path)); Storage.Write(relative,files);
            catalogs[system]=files; return files;
        }
        public bool Download(Game game,Settings settings,CancellationToken token) {
            lock(downloadGate) {token.ThrowIfCancellationRequested(); return DownloadCore(game,settings,token);}
        }
        bool DownloadCore(Game game,Settings settings,CancellationToken token) {
            if(!settings.LibretroArtworkEnabled || game.Source!="emulator") return false;
            string path=CachePath(game),retry=path+".retry";
            if(Usable(path)) {game.ArtworkProvider="libretro"; return true;}
            if(File.Exists(retry) && File.GetLastWriteTimeUtc(retry)>DateTime.UtcNow.AddHours(-6)) return false;
            var matches=new List<KeyValuePair<string,string>>();
            foreach(string system in Systems(game,settings).Distinct()) {
                token.ThrowIfCancellationRequested(); BoxartIndex index;
                if(!indexes.TryGetValue(system,out index)) indexes[system]=index=new BoxartIndex(Catalog(system,token));
                string file=index.Find(game);
                if(file!=null) matches.Add(new KeyValuePair<string,string>(system,file));
            }
            // A title present on two different systems needs better platform metadata.
            if(matches.Count==1 || (matches.Count>1 && new[]{"rpcs3","azahar","melonds"}.Contains(game.EmulatorId))) {
                string url=Root+Uri.EscapeDataString(matches[0].Key)+"/Named_Boxarts/"+Uri.EscapeDataString(matches[0].Value);
                byte[] bytes=ArtworkService.Fetch(url,null,token);
                using(var stream=new MemoryStream(bytes)) using(var image=System.Drawing.Image.FromStream(stream)) {
                    if(image.Width<64 || image.Height<64 || image.Width>8192 || image.Height>8192) return false;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(path)); lock(ArtworkService.CacheWriteGate) {token.ThrowIfCancellationRequested(); File.WriteAllBytes(path,bytes);} game.ArtworkProvider="libretro"; return true;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path)); lock(ArtworkService.CacheWriteGate) {token.ThrowIfCancellationRequested(); File.WriteAllText(retry,"");} return false;
        }
    }
}
