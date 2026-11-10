using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace PadLauncher {
    public static class LauncherLibrary {
        public static Library Scan(Settings settings,string manifestFolder=null,string catalogFile=null) {
            string steam=settings.SteamPath;
            if(String.IsNullOrWhiteSpace(steam)) steam=SteamLibrary.DetectSteam();
            var result=new Library {SteamPath=steam,UserId=settings.UserId};
            if(!String.IsNullOrWhiteSpace(steam)) {
                try { result=SteamLibrary.Scan(steam,settings.UserId); }
                catch(Exception e) { result.Warnings.Add("Steam library: "+e.Message); }
            }
            if(result.Games.Count>0) result.Collections.Add(new Collection {Id="source:steam",Name="Steam",Games=result.Games.Select(g => g.Id).ToList()});
            if(settings.EpicEnabled) {
                string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"Epic","EpicGamesLauncher","Data");
                var epic=EpicLibrary.Scan(manifestFolder ?? Path.Combine(data,"Manifests"),catalogFile ?? Path.Combine(data,"Catalog","catcache.bin"),result.Warnings);
                result.Games.AddRange(epic);
                if(epic.Count>0) result.Collections.Add(new Collection {Id="source:epic",Name="Epic Games Store",Games=epic.Select(g => g.Id).ToList()});
            }
            CustomGameLibrary.Apply(result,settings);
            foreach(var game in result.Games) ArtworkService.ApplyCached(game,settings);
            result.Games=result.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            if(result.Games.Count==0 && String.IsNullOrWhiteSpace(steam)) result.Warnings.Add("No games found. Choose your Steam folder or install a game through Epic Games Store.");
            return result;
        }
    }
    public class CustomGame {
        public string Id {get;set;}
        public string Name {get;set;}
        public string Target {get;set;}
        public string Arguments {get;set;}
        public string WorkingDirectory {get;set;}
        public string Artwork {get;set;}
    }
    public static class CustomGameLibrary {
        public static string Save(Settings settings,CustomGame game) {
            game.Name=(game.Name ?? "").Trim();
            if(game.Name.Length==0 || game.Name.Length>120) throw new ArgumentException("Enter a game name of 1–120 characters.");
            game.Target=Path.GetFullPath(Environment.ExpandEnvironmentVariables((game.Target ?? "").Trim().Trim('"')));
            string extension=Path.GetExtension(game.Target).ToLowerInvariant();
            if(!new[]{".exe",".lnk",".bat",".cmd"}.Contains(extension) || !File.Exists(game.Target)) throw new ArgumentException("Choose an existing .exe, .lnk, .bat, or .cmd file.");
            game.WorkingDirectory=(game.WorkingDirectory ?? "").Trim().Trim('"');
            if(game.WorkingDirectory.Length>0) {
                game.WorkingDirectory=Path.GetFullPath(Environment.ExpandEnvironmentVariables(game.WorkingDirectory));
                if(!Directory.Exists(game.WorkingDirectory)) throw new ArgumentException("The working folder does not exist.");
            }
            game.Artwork=(game.Artwork ?? "").Trim().Trim('"');
            if(game.Artwork.Length>0) {
                game.Artwork=Path.GetFullPath(game.Artwork);
                try {using(var image=System.Drawing.Image.FromFile(game.Artwork)) {if(image.Width<1) throw new IOException();}} catch {throw new ArgumentException("Choose a valid artwork image, or leave artwork empty for SteamGridDB fallback.");}
            }
            if(settings.CustomGames==null) settings.CustomGames=new List<CustomGame>();
            if(!String.IsNullOrEmpty(game.Id) && !settings.CustomGames.Any(g => g.Id==game.Id)) throw new ArgumentException("This custom game no longer exists.");
            if(String.IsNullOrEmpty(game.Id)) game.Id="custom:"+Guid.NewGuid().ToString("N");
            settings.CustomGames.RemoveAll(g => g.Id==game.Id); settings.CustomGames.Add(game);
            return game.Id;
        }
        public static void Apply(Library library,Settings settings) {
            library.Games.RemoveAll(g => g.Source=="custom"); library.Collections.RemoveAll(c => c.Id=="source:custom");
            foreach(var entry in settings.CustomGames ?? new List<CustomGame>()) {
                library.Games.Add(new Game {Id=entry.Id,Name=entry.Name,Source="custom",LaunchId=entry.Target,Arguments=entry.Arguments,WorkingDirectory=entry.WorkingDirectory,Art=entry.Artwork});
            }
            var games=library.Games.Where(g => g.Source=="custom").Select(g => g.Id).ToList();
            if(games.Count>0) library.Collections.Add(new Collection {Id="source:custom",Name="Custom games",Games=games});
        }
    }
    public static class EpicLibrary {
        public static bool SuitablePortrait(int width,int height) { return width>=250 && height>=350 && (double)width/height>=.5 && (double)width/height<=.85; }
        public static List<Game> Scan(string folder,string catalogFile,List<string> warnings) {
            var games=new List<Game>();
            if(!Directory.Exists(folder)) return games;
            var catalog=new Dictionary<string,object>();
            if(File.Exists(catalogFile)) try {
                string text=File.ReadAllText(catalogFile).Trim();
                if(!text.StartsWith("[") && !text.StartsWith("{")) text=Encoding.UTF8.GetString(Convert.FromBase64String(text));
                foreach(var item in Entries(Storage.Json().DeserializeObject(text))) {
                    string key=Vdf.Str(item,"namespace")+":"+Vdf.Str(item,"id"); catalog[key]=item;
                }
            } catch { warnings.Add("Epic artwork catalog could not be read; installed games are still available."); }
            foreach(string file in Directory.GetFiles(folder,"*.item",SearchOption.TopDirectoryOnly)) try {
                object entry=Storage.Json().DeserializeObject(File.ReadAllText(file));
                string name=Vdf.Str(entry,"DisplayName"),app=Vdf.Str(entry,"AppName"),ns=Vdf.Str(entry,"CatalogNamespace"),itemId=Vdf.Str(entry,"CatalogItemId");
                var categories=Entries(Vdf.Get(entry,"AppCategories")).Select(Convert.ToString).ToArray();
                if(String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(app) || String.IsNullOrWhiteSpace(ns) || String.IsNullOrWhiteSpace(itemId)) continue;
                if(Convert.ToString(Vdf.Get(entry,"bIsIncompleteInstall")).Equals("true",StringComparison.OrdinalIgnoreCase) || Vdf.Str(entry,"bIsIncompleteInstall")=="1") continue;
                if(categories.Any(c => c.StartsWith("engines",StringComparison.OrdinalIgnoreCase)) || app.StartsWith("UE_",StringComparison.OrdinalIgnoreCase)) continue;
                if(categories.Length>0 && !categories.Contains("games",StringComparer.OrdinalIgnoreCase)) continue;
                string install=Vdf.Str(entry,"InstallLocation"),exe=Vdf.Str(entry,"LaunchExecutable");
                if(!Directory.Exists(install) || String.IsNullOrWhiteSpace(exe)) continue;
                string root=Path.GetFullPath(install).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
                string target=Path.GetFullPath(Path.Combine(root,exe.Replace('/',Path.DirectorySeparatorChar)));
                if(!target.StartsWith(root,StringComparison.OrdinalIgnoreCase) || !File.Exists(target)) continue;
                string launch=ns+":"+itemId+":"+app,id="epic:"+launch;
                if(games.Any(g => g.Id==id)) continue;
                var game=new Game {Id=id,LaunchId=launch,Source="epic",Name=Regex.Replace(name,@"\s+"," ").Trim()};
                object metadata;
                if(catalog.TryGetValue(ns+":"+itemId,out metadata)) game.ArtworkUrl=SelectArtwork(Entries(Vdf.Get(metadata,"keyImages")));
                games.Add(game);
            } catch { warnings.Add("One Epic manifest could not be read: "+Path.GetFileName(file)); }
            return games;
        }
        public static string SelectArtwork(IEnumerable<object> images) {
            return images.Select(image => new {Url=Vdf.Str(image,"url"),Width=Number(Vdf.Get(image,"width")),Height=Number(Vdf.Get(image,"height"))})
                .Where(i => ArtworkNetwork.AllowedImageUrl(i.Url))
                .OrderBy(i => SuitablePortrait(i.Width,i.Height) ? 0 : 1)
                .ThenBy(i => i.Height>0 ? Math.Abs((double)i.Width/i.Height-2.0/3) : 10)
                .Select(i => i.Url).FirstOrDefault();
        }
        internal static int Number(object value) { int number; return Int32.TryParse(Convert.ToString(value),out number) ? number : 0; }
        internal static IEnumerable<object> Entries(object value) { var items=value as IEnumerable; if(items==null || value is string) yield break; foreach(var item in items) yield return item; }
    }
    public static class GameLaunch {
        public static System.Diagnostics.ProcessStartInfo StartInfo(Game game) {
            if(game.Source!="custom") return new System.Diagnostics.ProcessStartInfo(UriFor(game)) {UseShellExecute=true};
            if(!File.Exists(game.LaunchId)) throw new FileNotFoundException("The custom game's file could not be found. Edit its path in Custom games.",game.LaunchId);
            string folder=String.IsNullOrWhiteSpace(game.WorkingDirectory) ? Path.GetDirectoryName(game.LaunchId) : game.WorkingDirectory;
            if(!Directory.Exists(folder)) throw new DirectoryNotFoundException("The custom game's working folder could not be found.");
            return new System.Diagnostics.ProcessStartInfo(game.LaunchId,game.Arguments ?? "") {WorkingDirectory=folder,UseShellExecute=true};
        }
        public static string UriFor(Game game) {
            if(game.Source=="epic") {
                string[] ids=(game.LaunchId ?? "").Split(':');
                if(ids.Length!=3 || ids.Any(String.IsNullOrWhiteSpace)) throw new InvalidOperationException("This game's Epic launch IDs are invalid. Refresh the library.");
                return "com.epicgames.launcher://apps/"+Uri.EscapeDataString(game.LaunchId)+"?action=launch&silent=true";
            }
            if(!String.IsNullOrEmpty(game.Source) && game.Source!="steam") throw new InvalidOperationException("This game's launcher is not supported.");
            ulong id;
            if(!UInt64.TryParse(game.LaunchId,out id) || id==0) throw new InvalidOperationException("This game's Steam launch ID is invalid. Refresh the library.");
            return "steam://rungameid/"+game.LaunchId;
        }
    }
}
