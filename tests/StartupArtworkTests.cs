using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;
using PadLauncher;
class StartupArtworkTests {
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message) {if(!value) throw new Exception(message);}
    [STAThread] static void Main(string[] args) {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-startup-art-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher window=null;
        try {
            if(args.Length==1) {
                File.Copy(Path.Combine(args[0],"settings.json"),Path.Combine(root,"settings.json"));
                File.Copy(Path.Combine(args[0],"library.json"),Path.Combine(root,"library.json"));
                var config=Storage.Read<Settings>("settings.json");
                var artwork=(config.ArtworkOverrides ?? new Dictionary<string,string>()).Values.Concat((config.CustomGames ?? new List<CustomGame>()).Select(game => game.Artwork)).Where(path => !String.IsNullOrWhiteSpace(path)).Distinct().ToList();
                foreach(string relative in artwork) {
                    if(Path.IsPathRooted(relative)) continue;
                    string source=Path.Combine(args[0],relative),destination=Path.GetFullPath(Path.Combine(root,relative));
                    if(!destination.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || !File.Exists(source)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(destination)); File.Copy(source,destination,true);
                }
                var timer=Stopwatch.StartNew(); window=new Launcher(false,true); timer.Stop();
                var visible=(List<Game>)typeof(Launcher).GetField("visible",Flags).GetValue(window);
                Console.WriteLine("Fresh process cached-library constructor: "+timer.ElapsedMilliseconds+" ms; current list "+visible.Count+" games; missing artwork references "+visible.Count(game => !File.Exists(game.Art)));
                foreach(var game in visible.Where(game => game.Source=="custom")) Console.WriteLine("Custom cover: "+game.Name+" = "+(File.Exists(game.Art) ? game.ArtworkProvider : "missing"));
                timer.Restart(); window.ShowOverlay(); window.UpdateLayout(); timer.Stop(); Console.WriteLine("First overlay show: "+timer.ElapsedMilliseconds+" ms");
                Console.WriteLine("Decoded LRU: "+typeof(Launcher).GetField("imageCacheBytes",Flags).GetValue(window)+" bytes; cached bitmap count "+((Dictionary<string,BitmapSource>)typeof(Launcher).GetField("images",Flags).GetValue(window)).Count);
                var baseline=Storage.Read<Library>("library.json"); CustomGameLibrary.Apply(baseline,config);
                var ids=new HashSet<string>(visible.Select(game => game.Id));
                Console.WriteLine("Previous startup rebuild without restoration: "+baseline.Games.Count(game => ids.Contains(game.Id) && !File.Exists(game.Art))+" missing artwork references in the same list.");
                return;
            }
            string cover=Path.Combine(root,"cached-cover.png"),chosen=Path.Combine(root,"artwork","chosen","manual.png"),exe=Path.Combine(root,"game.exe");
            File.WriteAllText(exe,""); Directory.CreateDirectory(Path.GetDirectoryName(chosen));
            using(var bitmap=new System.Drawing.Bitmap(600,900)) {bitmap.Save(cover,System.Drawing.Imaging.ImageFormat.Png); bitmap.Save(chosen,System.Drawing.Imaging.ImageFormat.Png);}
            var settings=new Settings {CollectionId="pc",EnabledCollectionIds=new List<string>{"pc"},CustomGames=new List<CustomGame>{
                new CustomGame {Id="auto",Name="Automatic cached game",Target=exe},new CustomGame {Id="manual",Name="Manual game",Target=exe},new CustomGame {Id="reset",Name="Reset game",Target=exe},new CustomGame {Id="local",Name="Local game",Target=exe,Artwork=chosen}},
                ArtworkOverrides=new Dictionary<string,string>{{"manual",Path.Combine("artwork","chosen","manual.png")}},
                CustomLists=new List<CustomList>{new CustomList {Id="pc",Name="PC",Games=new List<string>{"auto","manual","reset","local","steam"}}}};
            var library=new Library {Games=new List<Game>{new Game {Id="auto",Name="Automatic cached game",Source="custom",Art=cover,ArtworkProvider="downloaded"},new Game {Id="reset",Name="Reset game",Source="custom",Art=cover,ArtworkProvider="chosen"}}};
            for(int i=0;i<1500;i++) library.Games.Add(new Game {Id="emulator:retroarch:"+i,Name="Retro "+i,Source="emulator",EmulatorId="retroarch"});
            library.Games.Add(new Game {Id="steam",Name="Middle Steam game",Source="steam"});
            Storage.Write("settings.json",settings); Storage.Write("library.json",library);
            window=new Launcher(false,true);
            var firstVisible=(List<Game>)typeof(Launcher).GetField("visible",Flags).GetValue(window);
            Check(firstVisible.Select(game => game.Id).SequenceEqual(new[]{"auto","local","manual","steam","reset"}),"First render interleaves cached Steam and rebuilt custom games alphabetically, matching refresh order");
            Check(firstVisible.Single(game => game.Id=="auto").Art==cover,"Downloaded custom artwork is available on the first render without refresh");
            Check(firstVisible.Single(game => game.Id=="manual").Art==chosen,"Saved artwork overrides are applied before the first render");
            Check(firstVisible.Single(game => game.Id=="local").Art==chosen,"Explicit local artwork remains available on startup");
            Check(firstVisible.Single(game => game.Id=="reset").Art==null,"Cleared manual artwork is not resurrected from stale library metadata");
            for(int i=0;i<3;i++) {window.ShowOverlay(); window.UpdateLayout(); window.Close();}
            long bytes=(long)typeof(Launcher).GetField("imageCacheBytes",Flags).GetValue(window);
            Check(bytes<=32L*1024*1024,"A large registered library does not bypass the 32 MB decoded cache limit");
            Console.WriteLine("Startup artwork regression passed: cached, manual and local covers restored before refresh; cleared choices stay cleared; 1,500 unrelated games retain the 32 MB cache bound.");
        } finally {if(window!=null) window.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
