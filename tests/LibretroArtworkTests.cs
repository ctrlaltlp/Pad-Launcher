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
            var other=Psx("other","Other Game"); new LibretroArtwork().Download(other,enabled,CancellationToken.None);
            Check(requests==before+1,"New artwork batches reuse console catalogs saved on disk");
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
