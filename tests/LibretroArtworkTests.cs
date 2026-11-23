using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using PadLauncher;
class LibretroArtworkTests {
    static int checks;
    static void Check(bool condition,string message) {if(!condition) throw new Exception(message); checks++;}
    static byte[] Image(int width,int height) {using(var image=new System.Drawing.Bitmap(width,height)) using(var output=new MemoryStream()) {image.Save(output,System.Drawing.Imaging.ImageFormat.Png); return output.ToArray();}}
    static byte[] Catalog(params string[] files) {return Encoding.UTF8.GetBytes(String.Join("\n",files.Select(file => "<a href=\""+Uri.EscapeDataString(file)+"\">cover</a>")));}
    static Game Psx(string id,string title) {return new Game {Id=id,Name=title,Source="emulator",EmulatorId="duckstation",LaunchId=title+".cue"};}
    static void Main(string[] args) {
        string oldRoot=Storage.Root,root=Path.Combine(Path.GetTempPath(),"LibretroArtworkTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        var oldFetch=ArtworkService.Fetch;
        try {
            var enabled=new Settings {SteamGridDbEnabled=false,LibretroArtworkEnabled=true};
            if(args.Contains("--live")) {
                var live=Psx("live-crash","Crash Bandicoot");
                Check(new LibretroArtwork().Download(live,enabled,CancellationToken.None),"Live Libretro cover found");
                Check(LibretroArtwork.Usable(LibretroArtwork.CachePath(live)),"Live downloaded cover validates");
                Console.WriteLine("Verified live PlayStation box-cover download without an API key."); return;
            }
            Check(!new Settings().LibretroArtworkEnabled,"Libretro downloads default to disabled");
            Check(LibretroArtwork.Systems(Psx("x","Game"),enabled).Single()=="Sony - PlayStation","PlayStation games only search their console");
            Check(LibretroArtwork.Systems(new Game {Source="custom",Name="Game"},enabled).Length==0,"PC games never search retro platforms");
            Check(LibretroArtwork.Systems(new Game {Source="emulator",EmulatorId="mgba",LaunchId="game.gbc"},enabled).Single()=="Nintendo - Game Boy Color","Multi-system emulator resolves unambiguous ROM extensions");
            Check(LibretroArtwork.Systems(new Game {Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Super Nintendo Entertainment System.lpl"},enabled).Single()=="Nintendo - Super Nintendo Entertainment System","RetroArch database names determine the artwork platform");
            Check(LibretroArtwork.NormalizeDatabase("Favourites.lpl")==null && LibretroArtwork.NormalizeDatabase("../../other")==null,"Mixed playlist names and path traversal are not platform names");
            Check(LibretroArtwork.Match(Psx("mgs","Metal Gear Solid (Disc 2)"),new[]{"Metal Gear Solid (USA) (Disc 2).png"})!=null,"Disc suffixes match the original game");
            Check(LibretroArtwork.Match(Psx("crash3","Crash Bandicoot 3"),new[]{"Crash Bandicoot - Warped (USA).png"})!=null,"Known Crash 3 alias matches Libretro's US title");
            Check(LibretroArtwork.Match(Psx("crash1","Crash Bandicoot"),new[]{"Crash Bandicoot 2 - Cortex Strikes Back (USA).png"})==null,"Missing covers do not fall through to a similar sequel");
            Check(LibretroArtwork.Match(Psx("unsafe","Game"),new[]{"../../Game.png","https://other.test/Game.png"})==null,"Catalog links cannot escape the selected box-cover folder");
            Check(LibretroArtwork.Match(Psx("article","The Legend of Zelda"),new[]{"Legend of Zelda, The (USA).png"})!=null,"Database article ordering matches user-friendly titles");
            int requests=0; byte[] square=Image(256,256);
            ArtworkService.Fetch=(url,key,token) => {requests++; Check(key==null,"Libretro never receives an API key"); return url.EndsWith("/") ? Catalog("Crash Bandicoot (USA).png","Other Game (USA).png") : square;};
            var crash=Psx("crash","Crash Bandicoot"); int ready=0;
            ArtworkService.Fill(new[]{crash},new Settings {SteamGridDbEnabled=false,EmulatorArtworkSources=new Dictionary<string,string>{{"duckstation","sgdb-only"}}},CancellationToken.None,(game,path) => ready++,message => {});
            Check(requests==0 && ready==0,"Only SteamGridDB makes no Libretro requests");
            ArtworkService.Fill(new[]{crash},enabled,CancellationToken.None,(game,path) => {ready++; game.Art=path;},message => {});
            Check(ready==1 && requests==2 && crash.ArtworkProvider=="libretro" && !ArtworkService.Portrait(crash.Art),"Enabling bypasses old provider misses and accepts square retro covers");
            var cached=Psx("crash","Crash Bandicoot"); ArtworkService.ApplyCached(cached,enabled);
            Check(cached.ArtworkProvider=="libretro" && ArtworkService.KeepArtwork(cached),"Square covers remain valid across cache reloads");
            int before=requests;
            ArtworkService.Fill(new[]{cached},enabled,CancellationToken.None,(game,path) => {},message => {});
            Check(requests==before,"Cached covers do not trigger another download");
            var oldTagged=new Game {Id="old-tagged-id",Name="Tagged Game (USA) [T+Eng]",Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Game Boy",ArtworkTitle="Tagged Game (USA) [T+Eng]"};
            string oldCachedPath=LibretroArtwork.CachePath(oldTagged);
            Directory.CreateDirectory(Path.GetDirectoryName(oldCachedPath));
            File.WriteAllBytes(oldCachedPath,Image(16,16));
            ArtworkService.TrackCachedArtwork(oldTagged,oldCachedPath);
            var importedTagged=new Game {Id="new-imported-id",Name="Tagged Game",Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Game Boy",ArtworkTitle="Tagged Game (USA) [T+Eng]"};
            var cacheNames=new HashSet<string>(Directory.GetFiles(Path.Combine(Storage.Root,"artwork","libretro"),"*.img").Select(Path.GetFileNameWithoutExtension),StringComparer.OrdinalIgnoreCase);
            var oldIds=ArtworkService.CachedArtworkGameIds();
            Check(oldIds.Contains("old-tagged-id"),"Cached artwork ownership records the original game ID");
            Check(LibretroArtwork.FindExistingCache(importedTagged,oldIds,cacheNames)==oldCachedPath,"Existing artwork is found by matching names and systems across changed game IDs, with or without title tags");
            var oldManual=new Game {Id="old-manual-id",Name="Odd Japanese Title [T-En]",LaunchId=@"C:\roms\Game.chd",Source="emulator",EmulatorId="duckstation",ArtworkSystem="Sony - PlayStation"};
            var newManual=new Game {Id="new-manual-id",Name="Corrected Game",LaunchId=@"C:\roms\Game.chd",Source="emulator",EmulatorId="duckstation",ArtworkSystem="Sony - PlayStation"};
            string chosenRelative=Path.Combine("artwork","chosen","libretro-manual-test.img"),chosenPath=Path.Combine(Storage.Root,chosenRelative);
            Directory.CreateDirectory(Path.GetDirectoryName(chosenPath)); File.WriteAllBytes(chosenPath,Image(16,16));
            var reuseSettings=new Settings {ArtworkOverrides=new Dictionary<string,string>{{oldManual.Id,chosenRelative}}};
            Check(ArtworkService.ExistingLibraryArtwork(newManual,new[]{oldManual},reuseSettings)==Path.GetFullPath(chosenPath),"Manually chosen artwork is reused by launch path even when names differ");
            newManual.LaunchId=@"D:\moved\Game.chd";
            Check(ArtworkService.ExistingLibraryArtwork(newManual,new[]{oldManual},reuseSettings)==Path.GetFullPath(chosenPath),"Manually chosen artwork follows a moved game with the same file name and system");
            var oldArchive=new Game {Id="old-archive",Name="Advance Wars",LaunchId=@"C:\old\Advance Wars (USA) Rev 1).zip#Advance Wars (USA) (Rev 1).gba",Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Game Boy Advance"};
            var newArchive=new Game {Id="new-archive",Name="Advance Wars (USA) (Rev 1)",LaunchId=@"D:\new\Advance Wars.zip#Advance Wars (USA) (Rev 1).gba",Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Game Boy Advance"};
            var archiveSettings=new Settings {ArtworkOverrides=new Dictionary<string,string>{{oldArchive.Id,chosenRelative}}};
            Check(ArtworkService.ExistingLibraryArtwork(newArchive,new[]{oldArchive},archiveSettings)==Path.GetFullPath(chosenPath),"Manually chosen artwork matches the ROM member name inside archives even when the archive path changed");
            var other=Psx("other","Other Game"); new LibretroArtwork().Download(other,enabled,CancellationToken.None);
            Check(requests==before+1,"New artwork batches reuse console catalogs saved on disk");
            var catalogFetch=ArtworkService.Fetch; int emptyRequests=0;
            ArtworkService.Fetch=(url,key,token) => {emptyRequests++; return Encoding.UTF8.GetBytes("<html></html>");};
            var emptyGame=new Game {Id="empty-catalog",Name="Empty",Source="emulator",EmulatorId="pcsx2",LaunchId="empty.iso"};
            Check(!new LibretroArtwork().Download(emptyGame,enabled,CancellationToken.None) && emptyRequests>0,"Empty Libretro catalogs are rejected instead of cached");
            int firstEmpty=emptyRequests;
            Check(!new LibretroArtwork().Download(emptyGame,enabled,CancellationToken.None) && emptyRequests>firstEmpty,"Rejected empty catalogs are retried rather than reused");
            ArtworkService.Fetch=(url,key,token) => {
                if(url.EndsWith("/git/trees/master")) return Encoding.UTF8.GetBytes("{\"tree\":[{\"path\":\"Named_Boxarts\",\"type\":\"tree\",\"sha\":\"catalog\"}]}");
                if(url.Contains("/git/trees/catalog")) return Encoding.UTF8.GetBytes("{\"tree\":[{\"path\":\"Game (USA).png\",\"type\":\"blob\"}]}");
                if(url.StartsWith("https://raw.githubusercontent.com/")) return square;
                if(url.EndsWith("/")) return Encoding.UTF8.GetBytes("<html></html>");
                throw new WebException("Not found");
            };
            var githubGame=new Game {Id="github-fallback",Name="Game",Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - GameCube",LaunchId="Game.rvz"};
            Check(new LibretroArtwork().Download(githubGame,enabled,CancellationToken.None) && githubGame.ArtworkProvider=="libretro","GitHub catalog and cover fallback works when the thumbnail host is down");
            var many=new StringBuilder("{\"tree\":[");
            for(int i=0;i<1000;i++) many.Append("{\"path\":\"Filler ").Append(i).Append(".png\",\"type\":\"blob\"},");
            many.Append("{\"path\":\"Zelda Game (USA).png\",\"type\":\"blob\"}]}");
            ArtworkService.Fetch=(url,key,token) => {
                if(url.EndsWith("/git/trees/master")) return Encoding.UTF8.GetBytes("{\"tree\":[{\"path\":\"Named_Boxarts\",\"type\":\"tree\",\"sha\":\"catalog\"}]}");
                if(url.Contains("/git/trees/catalog")) return Encoding.UTF8.GetBytes(many.ToString());
                if(url.StartsWith("https://raw.githubusercontent.com/")) return square;
                if(url.EndsWith("/")) return Encoding.UTF8.GetBytes("<html></html>");
                throw new WebException("Not found");
            };
            var zelda=new Game {Id="github-full",Name="Zelda Game",Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Nintendo 64",LaunchId="Zelda Game.z64"};
            Check(new LibretroArtwork().Download(zelda,enabled,CancellationToken.None),"GitHub catalogs include entries beyond the first 1000 files");
            ArtworkService.Fetch=(url,key,token) => {
                if(url.EndsWith("/git/trees/master")) return Encoding.UTF8.GetBytes("{\"tree\":[{\"path\":\"Named_Boxarts\",\"type\":\"tree\",\"sha\":\"catalog\"}]}");
                if(url.Contains("/git/trees/catalog")) return Encoding.UTF8.GetBytes("{\"tree\":[{\"path\":\"Linked Game (USA) (Disc 1).png\",\"type\":\"blob\"}]}");
                if(url.EndsWith("Linked%20Game%20(USA)%20(Disc%201).png")) return Encoding.UTF8.GetBytes("Linked Game (USA).png");
                if(url.EndsWith("Linked%20Game%20(USA).png")) return square;
                if(url.StartsWith("https://thumbnails.libretro.com/")) throw new WebException("Not found");
                throw new WebException("Not found");
            };
            var linked=new Game {Id="github-link",Name="Linked Game (Disc 1)",Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Game Boy Advance",LaunchId="Linked Game (Disc 1).rvz"};
            Check(new LibretroArtwork().Download(linked,enabled,CancellationToken.None),"GitHub symlink covers resolve to their real image");
            ArtworkService.Fetch=catalogFetch;
            var chosen=Psx("chosen","Crash Bandicoot"); chosen.Art=crash.Art; chosen.ArtworkProvider="chosen";
            before=requests; ArtworkService.Fill(new[]{chosen},enabled,CancellationToken.None,(game,path) => {},message => {});
            Check(requests==before,"Explicit local artwork is preserved even when square");
            var dolphin=new Game {Id="dolphin",Name="Game",Source="emulator",EmulatorId="dolphin",LaunchId="unknown.rvz"};
            ArtworkService.Fetch=(url,key,token) => url.EndsWith("/") ? Catalog("Game (USA).png") : square;
            Check(!new LibretroArtwork().Download(dolphin,enabled,CancellationToken.None),"Same-name matches on GameCube and Wii need unambiguous platform information");
            dolphin.Id="wii"; dolphin.LaunchId=@"C:\ROMS\Wii\Game.rvz";
            Check(new LibretroArtwork().Download(dolphin,enabled,CancellationToken.None),"Known Wii folder resolves an otherwise ambiguous compressed disc");
            var cancelled=new CancellationTokenSource(); cancelled.Cancel(); bool rejected=false;
            try {new LibretroArtwork().Download(Psx("cancel","Missing"),enabled,cancelled.Token);} catch(OperationCanceledException) {rejected=true;}
            Check(rejected,"Artwork cancellation interrupts matching");
            ArtworkKey.Save("test-key"); int sgdb=0,libretro=0; byte[] portrait=Image(600,900);
            ArtworkService.Fetch=(url,key,token) => {
                if(url.Contains("www.steamgriddb.com")) {sgdb++; return Encoding.UTF8.GetBytes(url.Contains("autocomplete") ? "{\"success\":true,\"data\":[{\"id\":9,\"name\":\"Priority\"}]}" : "{\"success\":true,\"data\":[{\"url\":\"https://cdn2.steamgriddb.com/grid/test.png\"}]}");}
                if(url.Contains("libretro.com")) {libretro++; return square;} return portrait;
            };
            ready=0; ArtworkService.Fill(new[]{Psx("priority","Priority")},new Settings {LibretroArtworkEnabled=true,EmulatorArtworkSources=new Dictionary<string,string>{{"duckstation","sgdb"}}},CancellationToken.None,(game,path) => ready++,message => {});
            Check(ready==1 && sgdb>0 && libretro==0,"SteamGridDB artwork is preferred when available");
            ArtworkService.Fetch=(url,key,token) => {if(url.Contains("www.steamgriddb.com")) throw new WebException("Unavailable"); Check(key==null,"Fallback does not forward the saved SteamGridDB key"); return url.EndsWith("/") ? Catalog("Crash Bandicoot (USA).png") : square;};
            ready=0; ArtworkService.Fill(new[]{Psx("offline-sgdb","Crash Bandicoot")},new Settings {LibretroArtworkEnabled=true,EmulatorArtworkSources=new Dictionary<string,string>{{"duckstation","sgdb"}}},CancellationToken.None,(game,path) => ready++,message => {});
            Check(ready==1,"Libretro still works when SteamGridDB is unavailable");
            Check(ArtworkNetwork.AllowedImageUrl("https://thumbnails.libretro.com/system/Named_Boxarts/game.png") && !ArtworkNetwork.AllowedImageUrl("https://thumbnails.libretro.com.evil.test/game.png") && !ArtworkNetwork.AllowedImageUrl("http://thumbnails.libretro.com/game.png"),"Libretro image host is restricted to its exact HTTPS domain");
            ArtworkService.Fetch=(url,key,token) => {
                Check(key==null,"Manual Libretro selection never sends saved credentials");
                if(url=="https://thumbnails.libretro.com/") return Encoding.UTF8.GetBytes("<a href=\"Sony%20-%20PlayStation/\">PSX</a><a href=\"../\">Parent</a><a href=\"stats/\">Stats</a>");
                return url.EndsWith("/") ? Catalog("Crash Bandicoot (USA).png","Other Game (USA).png") : square;
            };
            var manual=new LibretroArtwork();
            Check(manual.Platforms(CancellationToken.None).SequenceEqual(new[]{"Sony - PlayStation"}),"Manual picker loads safe console names from the public index");
            Check(manual.Search("Sony - PlayStation","Crash",CancellationToken.None).Contains("Crash Bandicoot (USA).png"),"Manual picker supports broad searches within one console");
            var customGame=new Game {Id="custom:manual",Name="Crash Bandicoot",Source="custom"};
            string choice=manual.SaveChoice(customGame,"Sony - PlayStation","Crash Bandicoot (USA).png",CancellationToken.None);
            var manualSettings=new Settings {ArtworkOverrides=new Dictionary<string,string>{{customGame.Id,choice}}};
            ArtworkService.ApplyCached(customGame,manualSettings);
            Check(!manualSettings.LibretroArtworkEnabled && customGame.ArtworkProvider=="libretro-chosen" && ArtworkService.KeepArtwork(customGame),"Manual custom-game covers override automatic art without enabling fallback downloads");
            rejected=false; try {manual.SaveChoice(customGame,"Sony - PlayStation","../Game.png",CancellationToken.None);} catch(ArgumentException) {rejected=true;}
            Check(rejected,"Manual choices reject unsafe image paths");
            Console.WriteLine(checks+" Libretro artwork checks passed.");
        } finally {ArtworkService.Fetch=oldFetch; Storage.Root=oldRoot; Directory.Delete(root,true);}
    }
}
