using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using PadLauncher;
class ListArtworkTests {
    static int checks;
    static void Check(bool value,string message) {if(!value) throw new Exception(message); checks++;}
    static byte[] Image(int width,int height) {using(var image=new System.Drawing.Bitmap(width,height)) using(var stream=new MemoryStream()) {image.Save(stream,System.Drawing.Imaging.ImageFormat.Png); return stream.ToArray();}}
    static void Save(string path,byte[] bytes) {Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllBytes(path,bytes);}
    static void Main() {
        string previous=Storage.Root,root=Path.Combine(Path.GetTempPath(),"ListArtworkTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        var previousFetch=ArtworkService.Fetch;
        try {
            var settings=new Settings {SteamGridDbEnabled=false,LibretroArtworkEnabled=false,ListArtworkSources=new Dictionary<string,string>{{"old","libretro"},{"modern","sgdb"}}};
            var game=new Game {Id="emu:crash",Name="Crash Bandicoot",Source="emulator",EmulatorId="duckstation",LaunchId="Crash.cue",Art=Path.Combine(root,"native.png"),ArtworkProvider="native"};
            Save(game.Art,Image(300,450));
            string retro=ArtworkService.ListCachePath(game,"libretro"),modern=ArtworkService.ListCachePath(game,"sgdb");
            Save(retro,Image(256,256)); Save(modern,Image(300,450));
            var oldDisplay=ArtworkService.ForList(game,settings,"old"); var newDisplay=ArtworkService.ForList(game,settings,"modern");
            Check(oldDisplay.Art==retro && oldDisplay.ArtworkProvider=="libretro","Old lists resolve Libretro artwork");
            Check(newDisplay.Art==modern && newDisplay.ArtworkProvider=="sgdb","Modern lists resolve SteamGridDB artwork");
            Check(oldDisplay.Id==newDisplay.Id && game.Art.EndsWith("native.png") && game.ArtworkProvider=="native","Per-list display choices never change the shared game");
            Check(ArtworkService.ForList(game,settings,"other").Art==retro,"Automatic lists inherit the emulator default artwork source");
            settings.ArtworkOverrides=new Dictionary<string,string>{{game.Id,"native.png"}};
            Check(ArtworkService.ForList(game,settings,"old").Art==game.Art,"Manual artwork overrides win over a list source"); settings.ArtworkOverrides.Clear();
            File.Delete(modern);
            Check(ArtworkService.ForList(game,settings,"modern").Art==retro,"A missing preferred source uses cached fallback artwork");
            settings.ListArtworkSources["only-sgdb"]="sgdb-only";
            Check(ArtworkService.ForList(game,settings,"only-sgdb").Art==null,"Only SteamGridDB never displays the available Libretro fallback");
            int forbiddenRequests=0; ArtworkService.Fetch=(url,key,token) => {forbiddenRequests++; throw new Exception("Forbidden provider request");};
            ArtworkService.FillList(new[]{game},settings,"only-sgdb",CancellationToken.None,id => {},message => {});
            Check(forbiddenRequests==0,"Only SteamGridDB with its API disabled never requests Libretro");
            settings.EmulatorArtworkSources=new Dictionary<string,string>{{"duckstation","libretro-only"}};
            Check(ArtworkService.GameListSource(settings,game,"other")=="libretro-only" && ArtworkService.GameListSource(settings,game,"modern")=="sgdb","Per-list choices override emulator artwork defaults");
            Check(ArtworkService.ForList(game,settings,"other").Art==retro,"Emulator defaults reuse downloaded artwork without redownloading");
            settings.EmulatorArtworkSources=null;
            File.Delete(retro); int network=0,ready=0;
            ArtworkService.Fetch=(url,key,token) => {network++; Check(key==null,"Libretro requests have no API key"); return url.EndsWith("/") ? Encoding.UTF8.GetBytes("<a href=\"Crash%20Bandicoot%20%28USA%29.png\">Crash</a>") : Image(256,256);};
            ArtworkService.FillList(new[]{game},settings,"old",CancellationToken.None,id => {Check(id==game.Id,"Source downloads report the correct game"); ready++;},message => {throw new Exception(message);});
            Check(ready==1 && LibretroArtwork.Usable(retro),"A per-list Libretro choice downloads despite the global fallback being disabled");
            Check(!settings.LibretroArtworkEnabled && game.ArtworkProvider=="native","Source downloads preserve the global option and game artwork");
            int requests=network;
            ArtworkService.FillList(new[]{game},settings,"old",CancellationToken.None,id => ready++,message => {});
            Check(network==requests,"Returning to the list reuses its cached cover");
            ArtworkKey.Save("test-key"); settings.SteamGridDbEnabled=true;
            ArtworkService.Fetch=(url,key,token) => {
                network++;
                if(url.StartsWith("https://www.steamgriddb.com/")) {
                    Check(key=="test-key","SteamGridDB requests use the saved key");
                    return Encoding.UTF8.GetBytes(url.Contains("search/") ? "{\"success\":true,\"data\":[{\"id\":77,\"name\":\"Crash Bandicoot\"}]}" : "{\"success\":true,\"data\":[{\"url\":\"https://cdn2.steamgriddb.com/grid/test.png\"}]}");
                }
                Check(key==null,"Image downloads do not expose the API key"); return Image(300,450);
            };
            ArtworkService.FillList(new[]{game},settings,"modern",CancellationToken.None,id => ready++,message => {throw new Exception(message);});
            Check(ArtworkService.Portrait(modern) && LibretroArtwork.Usable(retro),"Both sources remain cached independently for the same game");
            Check(ArtworkService.ForList(game,settings,"old").Art==retro && ArtworkService.ForList(game,settings,"modern").Art==modern,"Switching lists changes the displayed source without overwriting it");
            var cancelled=new CancellationTokenSource(); cancelled.Cancel(); bool stopped=false;
            try {ArtworkService.FillList(new[]{game},settings,"modern",cancelled.Token,id => {},message => {});} catch(OperationCanceledException) {stopped=true;}
            Check(stopped,"Cancelled list requests stop before using cached or remote artwork");
            settings=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            Check(settings.ListArtworkSources["old"]=="libretro" && settings.ListArtworkSources["modern"]=="sgdb","Source choices survive saving and loading settings");
            CustomLists.Delete(settings,"old"); Check(!settings.ListArtworkSources.ContainsKey("old") && settings.ListArtworkSources.ContainsKey("modern"),"Deleting a list removes only its source preference");
            var ra=new Game {Id="ra:crash",Name=game.Name,Source="emulator",EmulatorId="retroarch",ArtworkSystem="Sony - PlayStation",LaunchId="Crash.cue",Art=game.Art};
            settings.RetroArchPreferLibretro=true;
            string raRetro=ArtworkService.ListCachePath(ra,"libretro"); Save(raRetro,Image(256,256));
            Check(ArtworkService.GameListSource(settings,ra,"mixed")=="libretro" && ArtworkService.GameListSource(settings,game,"mixed")=="libretro","Confirmed RetroArch preference follows its games into automatic and mixed lists without affecting other emulators");
            Check(ArtworkService.GameListSource(settings,ra,"modern")=="sgdb" && ArtworkService.ForList(ra,settings,"mixed").Art==raRetro,"Explicit list sources override RetroArch's default artwork preference");
            settings.ArtworkOverrides[ra.Id]="native.png";
            Check(ArtworkService.ForList(ra,settings,"mixed").Art==game.Art,"Manual images override the RetroArch preference"); settings.ArtworkOverrides.Remove(ra.Id);
            string preferredPath=null; ArtworkService.Fill(new[]{ra},settings,CancellationToken.None,(entry,path) => preferredPath=path,message => {throw new Exception(message);});
            Check(preferredPath==raRetro && !settings.LibretroArtworkEnabled,"RetroArch's confirmed preference uses Libretro first without enabling global fallback");
            settings.RetroArchPreferLibretro=false;
            Check(ArtworkService.GameListSource(settings,ra,"mixed")=="sgdb","RetroArch preference can be explicitly declined");
            settings.RetroArchListArtworkSource="libretro";
            Check(ArtworkService.PreferLibretroForRetroArch(settings) && ArtworkService.GameListSource(settings,ra,"mixed")=="libretro","Shared RetroArch artwork source Libretro always enables the per-game preference despite an older false flag");
            preferredPath=null; ArtworkService.Fill(new[]{ra},settings,CancellationToken.None,(entry,path) => preferredPath=path,message => {throw new Exception(message);});
            Check(preferredPath==raRetro,"Shared Libretro artwork source takes priority in the global download path too");
            File.WriteAllText(retro+".retry",""); File.WriteAllText(modern+".retry","");
            string chosen=Path.Combine(root,"artwork","chosen","manual.img"),unrelated=Path.Combine(root,"artwork","providers","sgdb","unrelated.img"); Save(chosen,Image(256,256)); Save(unrelated,Image(300,450));
            ArtworkService.ClearDownloadedArtwork(new[]{game});
            Check(!File.Exists(retro) && !File.Exists(modern) && !File.Exists(retro+".retry") && !File.Exists(modern+".retry"),"Clean artwork removes selected games' provider images and retry records");
            Check(File.Exists(game.Art) && File.Exists(chosen) && File.Exists(unrelated) && File.Exists(raRetro),"Clean artwork preserves native, manual and unrelated games' files");
            var manualGame=game.ArtworkCopy(); manualGame.Art=chosen; manualGame.ArtworkProvider="chosen"; ArtworkService.ClearDownloadedArtwork(new[]{manualGame});
            Check(File.Exists(chosen),"Artwork cleanup never deletes manually chosen source files");
            Console.WriteLine(checks+" per-list artwork checks passed.");
        } finally {ArtworkService.Fetch=previousFetch; Storage.Root=previous; Directory.Delete(root,true);}
    }
}
