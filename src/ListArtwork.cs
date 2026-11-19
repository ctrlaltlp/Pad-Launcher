using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Linq;

namespace PadLauncher {
    public static partial class ArtworkService {
        public static List<KeyValuePair<string,string>> ArtworkSourceChoices(bool automatic=true) {
            var choices=new List<KeyValuePair<string,string>>(); if(automatic) choices.Add(new KeyValuePair<string,string>("auto","Automatic"));
            choices.Add(new KeyValuePair<string,string>("libretro","Prefer Libretro with SteamGridDB as fallback"));
            choices.Add(new KeyValuePair<string,string>("sgdb","Prefer SteamGridDB with Libretro fallback"));
            choices.Add(new KeyValuePair<string,string>("sgdb-only","Only SteamGridDB"));
            choices.Add(new KeyValuePair<string,string>("libretro-only","Only Libretro")); return choices;
        }
        public static bool ValidArtworkSource(string source) {return source=="sgdb" || source=="libretro" || source=="sgdb-only" || source=="libretro-only";}
        public static string PrimaryArtworkSource(string source) {return (source ?? "auto").Replace("-only","");}
        public static string[] ArtworkProviders(string source) {string primary=PrimaryArtworkSource(source); return !ValidArtworkSource(source) ? new string[0] : source.EndsWith("-only") ? new[]{primary} : new[]{primary,primary=="sgdb" ? "libretro" : "sgdb"};}
        public static string DefaultEmulatorArtworkSource(string id) {return new[]{"duckstation","pcsx2","xemu","mgba","melonds"}.Contains(id) ? "libretro" : "sgdb";}
        public static string EmulatorDefaultSource(Settings settings,string id) {
            string source;
            if(settings.EmulatorArtworkSources!=null && settings.EmulatorArtworkSources.TryGetValue(id ?? "",out source) && ValidArtworkSource(source)) return source;
            if(id=="retroarch" && settings.RetroArchPreferLibretro==true) return "libretro";
            return DefaultEmulatorArtworkSource(id);
        }
        public static string ArtworkSourceName(string source) {return ArtworkSourceChoices().Where(choice => choice.Key==source).Select(choice => choice.Value).FirstOrDefault() ?? "Automatic";}
        public static bool PreferLibretroForRetroArch(Settings settings) {return PrimaryArtworkSource(EmulatorDefaultSource(settings,"retroarch"))=="libretro";}
        public static void ClearDownloadedArtwork(IEnumerable<Game> games) {
            string root=Path.GetFullPath(Path.Combine(Storage.Root,"artwork"))+Path.DirectorySeparatorChar;
            lock(CacheWriteGate) foreach(var game in games) {
                var paths=new List<string> {Cache(game),ListCachePath(game,"sgdb"),LibretroArtwork.CachePath(game)};
                if(!String.IsNullOrWhiteSpace(game.Art) && game.ArtworkProvider!="chosen" && game.ArtworkProvider!="libretro-chosen") {
                    string current=Path.GetFullPath(game.Art);
                    if(current.StartsWith(root,StringComparison.OrdinalIgnoreCase) && !current.StartsWith(Path.Combine(root,"chosen")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) && !current.Contains(Path.DirectorySeparatorChar+"catalogs"+Path.DirectorySeparatorChar)) paths.Add(current);
                }
                foreach(string path in paths.Distinct(StringComparer.OrdinalIgnoreCase)) {
                    string full=Path.GetFullPath(path); if(!full.StartsWith(root,StringComparison.OrdinalIgnoreCase)) throw new IOException("Artwork cache path is outside the data folder.");
                    if(File.Exists(full)) File.Delete(full);
                    string folder=Path.GetDirectoryName(full);
                    if(Directory.Exists(folder)) foreach(string retry in Directory.GetFiles(folder,Path.GetFileName(full)+"*.retry")) File.Delete(retry);
                }
            }
        }
        public static string ListSource(Settings settings,string listId) {
            string source;
            return listId!=null && settings.ListArtworkSources!=null && settings.ListArtworkSources.TryGetValue(listId,out source) && ValidArtworkSource(source) ? source : "auto";
        }
        public static string ListDefaultSource(Settings settings,string listId) {return ListSource(settings,listId);}
        public static string GameListSource(Settings settings,Game game,string listId,string resolvedListSource=null) {
            string source=resolvedListSource ?? ListDefaultSource(settings,listId);
            return source=="auto" && game.Source=="emulator" ? EmulatorDefaultSource(settings,game.EmulatorId) : source;
        }
        public static void UpgradeArtworkPreferences(Settings settings) {
            if(settings.ArtworkPreferenceVersion>=1) return;
            if(settings.EmulatorArtworkSources==null) settings.EmulatorArtworkSources=new Dictionary<string,string>();
            string current;
            if((!settings.EmulatorArtworkSources.TryGetValue("retroarch",out current) || !ValidArtworkSource(current)) && ValidArtworkSource(settings.RetroArchListArtworkSource)) settings.EmulatorArtworkSources["retroarch"]=settings.RetroArchListArtworkSource;
            settings.ArtworkPreferenceVersion=1;
        }
        public static bool HasManualArtwork(Game game,Settings settings) {
            if((game.ArtworkProvider=="chosen" || game.ArtworkProvider=="libretro-chosen") && File.Exists(game.Art)) return true;
            string value;
            if(settings.ArtworkOverrides==null || !settings.ArtworkOverrides.TryGetValue(game.Id,out value) || String.IsNullOrWhiteSpace(value)) return false;
            string root=Path.GetFullPath(Storage.Root)+Path.DirectorySeparatorChar;
            string path=Path.GetFullPath(Path.Combine(Storage.Root,value));
            return path.StartsWith(root,StringComparison.OrdinalIgnoreCase) && File.Exists(path);
        }
        public static string ListCachePath(Game game,string source) {
            if(source=="libretro") return LibretroArtwork.CachePath(game);
            using(var hash=SHA256.Create()) {
                string name=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(game.Id+"|"+game.Name+"|"+game.Source))).Replace("-","");
                return TrackCachedArtwork(game,Path.Combine(Storage.Root,"artwork","providers","sgdb",name+".img"));
            }
        }
        public static Game ForList(Game game,Settings settings,string listId,string resolvedListSource=null) {
            string source=GameListSource(settings,game,listId,resolvedListSource);
            if(source=="auto") return game;
            var copy=game.ArtworkCopy();
            copy.PreferredArtworkSource=PrimaryArtworkSource(source);
            if(HasManualArtwork(game,settings)) {ApplyCached(copy,settings); return copy;}
            foreach(string provider in ArtworkProviders(source)) {
                string path=ListCachePath(game,provider);
                if(!File.Exists(path) && provider=="sgdb" && (game.ArtworkProvider=="downloaded" || game.ArtworkProvider=="sgdb")) path=game.Art;
                if(File.Exists(path)) {copy.Art=path; copy.ArtworkProvider=provider; return copy;}
            }
            if(source.EndsWith("-only")) {copy.Art=null; copy.ArtworkProvider=null;}
            return copy;
        }
        public static void FillList(IEnumerable<Game> games,Settings settings,string listId,CancellationToken token,Action<string> ready,Action<string> status) {
            string listSource=ListDefaultSource(settings,listId);
            string key=settings.SteamGridDbEnabled ? ArtworkKey.Load() : null;
            var libretro=new LibretroArtwork(); bool warned=false;
            var libretroSettings=settings;
            if(!settings.LibretroArtworkEnabled) {libretroSettings=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); libretroSettings.LibretroArtworkEnabled=true;}
            foreach(var original in games) {
                token.ThrowIfCancellationRequested();
                string source=GameListSource(settings,original,listId,listSource); if(source=="auto") continue;
                if(HasManualArtwork(original,settings)) continue;
                var game=original.ArtworkCopy();
                foreach(string provider in ArtworkProviders(source)) {
                    token.ThrowIfCancellationRequested();
                    string path=ListCachePath(game,provider);
                    if(File.Exists(path)) {ready(game.Id); break;}
                    try {
                        bool found=false;
                        if(provider=="libretro") found=libretro.Download(game,libretroSettings,token);
                        else if(!String.IsNullOrWhiteSpace(key)) {
                            string retry=path+".retry";
                            if(File.Exists(retry) && File.GetLastWriteTimeUtc(retry)>DateTime.UtcNow.AddHours(-6)) continue;
                            found=Download(FindGrid(game,key,token),path,token);
                            if(!found) {Directory.CreateDirectory(Path.GetDirectoryName(path)); lock(CacheWriteGate) {token.ThrowIfCancellationRequested(); File.WriteAllText(retry,"");}}
                        }
                        if(found) {TrackCachedArtwork(game,path); ready(game.Id); break;}
                    } catch(Exception) {
                        token.ThrowIfCancellationRequested();
                        if(!warned) {status("Some artwork could not be loaded. Trying the permitted fallback where available."); warned=true;}
                    }
                }
            }
        }
    }
}
