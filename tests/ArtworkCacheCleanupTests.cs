using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PadLauncher;
class ArtworkCacheCleanupTests {
    static int checks;
    static void Check(bool value,string message) {if(!value) throw new Exception(message); checks++;}
    static string Cache(Game game,string provider) {string path=ArtworkService.ListCachePath(game,provider); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,"image"); return ArtworkService.ListCachePath(game,provider);}
    static void Main() {
        string previous=Storage.Root,root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cachetest-"+Guid.NewGuid().ToString("N").Substring(0,8)); Storage.Root=root;
        try {
            var live=new Game {Id="live",Name="Live",Source="emulator",EmulatorId="duckstation",PreferredArtworkSource="libretro-only"};
            var removed=new Game {Id="removed",Name="Removed",Source="emulator"};
            string sgdb=Cache(live,"sgdb"),retro=Cache(live,"libretro"),unused=Cache(removed,"sgdb"),unusedRetro=Cache(removed,"libretro");
            string unknown=Path.Combine(root,"artwork",new string('A',64)+".img"); File.WriteAllText(unknown,"legacy");
            string manual=Path.Combine(root,"artwork","chosen",new string('B',64)+".img"); Directory.CreateDirectory(Path.GetDirectoryName(manual)); File.WriteAllText(manual,"manual");
            var settings=new Settings();
            Check(ArtworkService.ClearUnusedCachedArtwork(new[]{live},settings)==2,"Remove only confirmed orphan provider images");
            Check(File.Exists(sgdb) && File.Exists(retro),"Libretro-only registered game retains both providers");
            Check(!File.Exists(unused) && !File.Exists(unusedRetro),"Both orphan provider caches removed");
            Check(File.Exists(manual) && File.Exists(unknown),"Chosen and unidentified legacy images preserved");
            live.PreferredArtworkSource="sgdb-only";
            Check(ArtworkService.ClearUnusedCachedArtwork(new[]{live},settings)==0 && File.Exists(retro),"SGDB-only also retains Libretro and repeat cleanup is harmless");
            string shared=Cache(removed,"sgdb"); live.Art=shared;
            Check(ArtworkService.ClearUnusedCachedArtwork(new[]{live},settings)==0 && File.Exists(shared),"Actual artwork referenced by a registered game is protected");
            live.Art=null; settings.ArtworkOverrides=new Dictionary<string,string>{{"manual",shared}};
            Check(ArtworkService.ClearUnusedCachedArtwork(new[]{live},settings)==0 && File.Exists(shared),"Manual override references protected");
            string outside=Path.Combine(root,"outside.img"); File.WriteAllText(outside,"original");
            File.AppendAllText(Path.Combine(root,"artwork","cache-owners.jsonl"),Storage.Json().Serialize(new ArtworkService.CacheOwner {Path=outside,GameId="gone"})+Environment.NewLine+"bad record"+Environment.NewLine);
            Check(ArtworkService.ClearUnusedCachedArtwork(new[]{live},settings)==0 && File.Exists(outside),"Invalid records and paths outside downloaded cache never deleted");
            var missingLive=new Game {Id="missing-live",Name="Missing Live",Source="emulator",EmulatorId="duckstation"};
            string missingSgdb=Cache(missingLive,"sgdb"),missingRetro=Cache(missingLive,"libretro");
            Check(ArtworkService.ClearMissingCachedArtwork(new[]{live,missingLive},settings,new[]{"missing-live"})==2,"Missing registered games lose their downloaded artwork");
            Check(!File.Exists(missingSgdb) && !File.Exists(missingRetro) && File.Exists(sgdb) && File.Exists(retro),"Missing-game cleanup keeps artwork for live games");
            var config=new EmulatorConfig {Id="duckstation",Games=new List<EmulatorGame>{new EmulatorGame {Id="one"},new EmulatorGame {Id="two"}}}; settings.Emulators=new List<EmulatorConfig>{config};
            settings.CustomLists=new List<CustomList>{new CustomList {Id="list",Games=new List<string>{"one","other"}}};
            settings.FavouriteGameIds=new List<string>{"one","other"}; settings.HiddenGameIds=new List<string>{"one"}; settings.ListGameAdditions=new Dictionary<string,List<string>>{{"pc",new List<string>{"one","other"}}};
            EmulatorLibrary.RemoveGame(settings,"duckstation","one");
            Check(config.Games.Count==1 && config.Games[0].Id=="two","Unregistration targets this emulator game only");
            Check(settings.CustomLists[0].Games.SequenceEqual(new[]{"other"}) && settings.ListGameAdditions["pc"].SequenceEqual(new[]{"other"}) && settings.FavouriteGameIds.SequenceEqual(new[]{"other"}) && settings.HiddenGameIds.Count==0,"Unregistration clears memberships and favourites without unrelated games");
            Check(File.Exists(sgdb) && File.Exists(retro),"Unregistration itself never deletes artwork");
            Console.WriteLine(checks+" artwork cleanup checks passed.");
        } finally {Storage.Root=previous; if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}

