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
            if(settings.GogEnabled) {
                try {
                    var gog=GogLibrary.Scan(); result.Games.AddRange(gog);
                    if(gog.Count>0) result.Collections.Add(new Collection {Id="source:gog",Name="GOG Galaxy",Games=gog.Select(g => g.Id).ToList()});
                } catch(Exception error) {result.Warnings.Add("GOG library: "+error.Message);}
            }
            if(settings.EaEnabled) {
                try {var ea=EaLibrary.Scan(); result.Games.AddRange(ea); if(ea.Count>0) result.Collections.Add(new Collection {Id="source:ea",Name="EA App",Games=ea.Select(g => g.Id).ToList()});}
                catch(Exception error) {result.Warnings.Add("EA library: "+error.Message);}
            }
            if(settings.UbisoftEnabled) {
                try {var ubisoft=UbisoftLibrary.Scan(); result.Games.AddRange(ubisoft); if(ubisoft.Count>0) result.Collections.Add(new Collection {Id="source:ubisoft",Name="Ubisoft Connect",Games=ubisoft.Select(g => g.Id).ToList()});}
                catch(Exception error) {result.Warnings.Add("Ubisoft library: "+error.Message);}
            }
            if(settings.RockstarEnabled) {
                try {var games=RockstarLibrary.Scan(); result.Games.AddRange(games); if(games.Count>0) result.Collections.Add(new Collection {Id="source:rockstar",Name="Rockstar Games Launcher",Games=games.Select(g => g.Id).ToList()});}
                catch(Exception error) {result.Warnings.Add("Rockstar library: "+error.Message);}
            }
            if(settings.BattleNetEnabled) {
                try {var games=BattleNetLibrary.Scan(); result.Games.AddRange(games); if(games.Count>0) result.Collections.Add(new Collection {Id="source:battlenet",Name="Battle.net",Games=games.Select(g => g.Id).ToList()});}
                catch(Exception error) {result.Warnings.Add("Battle.net library: "+error.Message);}
            }
            CustomGameLibrary.Apply(result,settings);
            EmulatorLibrary.Apply(result,settings);
            if(settings.ReplacedGameIds!=null) result.Games.RemoveAll(g => settings.ReplacedGameIds.Contains(g.Id));
            foreach(var game in result.Games) ArtworkService.ApplyCached(game,settings);
            result.Games=result.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            if(result.Games.Count==0 && String.IsNullOrWhiteSpace(steam)) result.Warnings.Add("No games found. Choose your Steam folder or install a game through Epic Games Store.");
            return result;
        }
    }
    public class CustomGame {
        public string SteamShortcutKey {get;set;}
        public string SteamLaunchId {get;set;}
        public bool LaunchViaSteam {get;set;}
        public string PlayniteId {get;set;}
        public string PlayniteActionInfo {get;set;}
        public string Id {get;set;}
        public string Name {get;set;}
        public string Target {get;set;}
        public string Arguments {get;set;}
        public string WorkingDirectory {get;set;}
        public string Artwork {get;set;}
    }
    public static class CustomGameLibrary {
        public static string ArtworkPath(string path) {return String.IsNullOrWhiteSpace(path) ? null : Path.IsPathRooted(path) ? path : Path.Combine(Storage.Root,path);}
        public static string Save(Settings settings,CustomGame game) {
            game.Name=(game.Name ?? "").Trim();
            if(game.Name.Length==0 || game.Name.Length>120) throw new ArgumentException("Enter a game name of 1–120 characters.");
            if(game.LaunchViaSteam) {
                ulong steamId; if(!UInt64.TryParse(game.SteamLaunchId,out steamId) || steamId==0) throw new ArgumentException("This entry has no valid Steam launch ID.");
            } else {
                if(!String.IsNullOrEmpty(game.SteamLaunchId) && (game.Arguments ?? "").IndexOf("%command%",StringComparison.OrdinalIgnoreCase)>=0) throw new ArgumentException("These Steam launch options use a wrapper. Keep launch through Steam enabled, or edit the arguments for direct launch.");
                game.Target=Path.GetFullPath(Environment.ExpandEnvironmentVariables((game.Target ?? "").Trim().Trim('"')));
                string extension=Path.GetExtension(game.Target).ToLowerInvariant();
                if(!new[]{".exe",".lnk",".bat",".cmd"}.Contains(extension) || !File.Exists(game.Target)) throw new ArgumentException("Choose an existing .exe, .lnk, .bat, or .cmd file.");
            }
            game.WorkingDirectory=(game.WorkingDirectory ?? "").Trim().Trim('"');
            if(game.WorkingDirectory.Length>0 && !game.LaunchViaSteam) {
                game.WorkingDirectory=Path.GetFullPath(Environment.ExpandEnvironmentVariables(game.WorkingDirectory));
                if(!Directory.Exists(game.WorkingDirectory)) throw new ArgumentException("The working folder does not exist.");
            }
            game.Artwork=(game.Artwork ?? "").Trim().Trim('"');
            if(game.Artwork.Length>0) {
                game.Artwork=Path.GetFullPath(ArtworkPath(game.Artwork));
                try {using(var image=System.Drawing.Image.FromFile(game.Artwork)) {if(image.Width<1) throw new IOException();}} catch {throw new ArgumentException("Choose a valid artwork image, or leave artwork empty for SteamGridDB fallback.");}
                string root=Path.GetFullPath(Storage.Root)+Path.DirectorySeparatorChar;
                if(game.Artwork.StartsWith(root,StringComparison.OrdinalIgnoreCase)) game.Artwork=game.Artwork.Substring(root.Length);
            }
            if(settings.CustomGames==null) settings.CustomGames=new List<CustomGame>();
            var previous=settings.CustomGames.FirstOrDefault(g => g.Id==game.Id);
            if(previous!=null && String.IsNullOrEmpty(game.SteamShortcutKey)) game.SteamShortcutKey=previous.SteamShortcutKey;
            if(previous!=null && String.IsNullOrEmpty(game.SteamLaunchId)) game.SteamLaunchId=previous.SteamLaunchId;
            if(previous!=null && String.IsNullOrEmpty(game.PlayniteId)) game.PlayniteId=previous.PlayniteId;
            if(previous!=null && String.IsNullOrEmpty(game.PlayniteActionInfo)) game.PlayniteActionInfo=previous.PlayniteActionInfo;
            if(!String.IsNullOrEmpty(game.Id) && !settings.CustomGames.Any(g => g.Id==game.Id)) throw new ArgumentException("This custom game no longer exists.");
            if(String.IsNullOrEmpty(game.Id)) game.Id="custom:"+Guid.NewGuid().ToString("N");
            settings.CustomGames.RemoveAll(g => g.Id==game.Id); settings.CustomGames.Add(game);
            return game.Id;
        }
        public static void Apply(Library library,Settings settings) {
            library.Games.RemoveAll(g => g.Source=="custom"); library.Collections.RemoveAll(c => c.Id=="source:custom");
            foreach(var entry in settings.CustomGames ?? new List<CustomGame>()) {
                library.Games.Add(new Game {Id=entry.Id,Name=entry.Name,Source="custom",LaunchId=entry.Target,SteamLaunchId=entry.LaunchViaSteam ? entry.SteamLaunchId : null,Arguments=entry.Arguments,WorkingDirectory=entry.WorkingDirectory,ImportedActionInfo=entry.PlayniteActionInfo,Art=ArtworkPath(entry.Artwork)});
            }
            var games=library.Games.Where(g => g.Source=="custom").Select(g => g.Id).ToList();
            if(games.Count>0) library.Collections.Add(new Collection {Id="source:custom",Name="Custom games",Games=games});
        }
    }
    public static class EpicLibrary {
        public static string DetectLauncher() {
            try {
                using(var key=Microsoft.Win32.Registry.ClassesRoot.OpenSubKey(@"com.epicgames.launcher\shell\open\command")) {
                    string command=key==null ? "" : Convert.ToString(key.GetValue(""));
                    var match=Regex.Match(command,@"^\s*""([^""]+\.exe)""",RegexOptions.IgnoreCase);
                    if(match.Success && File.Exists(match.Groups[1].Value)) return match.Groups[1].Value;
                }
            } catch {}
            foreach(string root in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}) foreach(string architecture in new[]{"Win64","Win32"}) {
                string path=Path.Combine(root,"Epic Games","Launcher","Portal","Binaries",architecture,"EpicGamesLauncher.exe"); if(File.Exists(path)) return path;
            }
            return null;
        }
        public static string ValidateLauncher(string value) {
            if(String.IsNullOrWhiteSpace(value)) return null;
            string path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')));
            if(!File.Exists(path) || !Path.GetFileName(path).Equals("EpicGamesLauncher.exe",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose EpicGamesLauncher.exe, or leave the field empty for automatic detection.");
            return path;
        }
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
        public static System.Diagnostics.ProcessStartInfo StartInfo(Game game,Settings settings=null) {
            if(game.Source=="emulator") return EmulatorLibrary.StartInfo(game,settings);
            if(game.Source=="rockstar") return RockstarLibrary.StartInfo(game,settings==null ? null : settings.RockstarLauncherPath);
            if(game.Source=="battlenet") return BattleNetLibrary.StartInfo(game);
            if(game.Source=="ubisoft") return UbisoftLibrary.StartInfo(game,settings==null ? null : settings.UbisoftLauncherPath);
            if(game.Source=="ea") return EaLibrary.StartInfo(game,settings==null ? null : settings.EaLauncherPath);
            if(game.Source=="epic") {
                string uri=UriFor(game),path=settings==null || String.IsNullOrWhiteSpace(settings.EpicLauncherPath) ? EpicLibrary.DetectLauncher() : EpicLibrary.ValidateLauncher(settings.EpicLauncherPath);
                if(!String.IsNullOrEmpty(path)) return new System.Diagnostics.ProcessStartInfo(path,"\""+uri+"\"") {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(path)};
                return new System.Diagnostics.ProcessStartInfo(uri) {UseShellExecute=true};
            }
            if(game.Source=="gog") return GogLibrary.StartInfo(game);
            if(game.Source!="custom") return new System.Diagnostics.ProcessStartInfo(UriFor(game)) {UseShellExecute=true};
            if(!String.IsNullOrEmpty(game.SteamLaunchId)) {
                ulong steamId; if(!UInt64.TryParse(game.SteamLaunchId,out steamId) || steamId==0) throw new InvalidOperationException("Invalid Steam launch ID.");
                return new System.Diagnostics.ProcessStartInfo("steam://rungameid/"+game.SteamLaunchId) {UseShellExecute=true};
            }
            if(!File.Exists(game.LaunchId)) throw new FileNotFoundException("The custom game's file could not be found. Edit its path in Custom games.",game.LaunchId);
            string folder=String.IsNullOrWhiteSpace(game.WorkingDirectory) ? Path.GetDirectoryName(game.LaunchId) : game.WorkingDirectory;
            if(!Directory.Exists(folder)) throw new DirectoryNotFoundException("The custom game's working folder could not be found.");
            return new System.Diagnostics.ProcessStartInfo(game.LaunchId,game.Arguments ?? "") {WorkingDirectory=folder,UseShellExecute=true};
        }
        public static string UriFor(Game game) {
            if(game.Source=="ubisoft") return UbisoftLibrary.UriFor(game);
            if(game.Source=="ea") return EaLibrary.UriFor(game);
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
