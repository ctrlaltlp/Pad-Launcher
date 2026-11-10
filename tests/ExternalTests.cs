using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Threading;
using System.Collections.Generic;
using PadLauncher;
class ExternalTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EpicTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        Storage.Root=root;
        try {
            string install=Path.Combine(root,"game"); Directory.CreateDirectory(install); File.WriteAllText(Path.Combine(install,"game.exe"),"");
            var manifest=new Dictionary<string,object>{{"DisplayName","Amnesia:  The Dark Descent"},{"AppName","app"},{"CatalogNamespace","ns"},{"CatalogItemId","item"},{"InstallLocation",install},{"LaunchExecutable","game.exe"},{"AppCategories",new[]{"games"}}};
            string file=Path.Combine(root,"game.item"); File.WriteAllText(file,Storage.Json().Serialize(manifest));
            string catalog=Path.Combine(root,"catalog"); File.WriteAllText(catalog,Convert.ToBase64String(Encoding.UTF8.GetBytes("[{\"namespace\":\"ns\",\"id\":\"item\",\"keyImages\":[{\"url\":\"https://cdn1.epicgames.com/cover.jpg\",\"width\":860,\"height\":1148}]}]")));
            var warnings=new List<string>(); var games=EpicLibrary.Scan(root,catalog,warnings);
            Check(games.Count==1 && games[0].Name=="Amnesia: The Dark Descent","Installed Epic game and normalized name");
            Check(games[0].ArtworkUrl.EndsWith("cover.jpg"),"Base64 catalog portrait");
            Check(GameLaunch.UriFor(games[0]).Contains("ns%3Aitem%3Aapp"),"Epic launch identifiers escaped");
            Check(GameLaunch.UriFor(new Game {LaunchId="18446744073709551615"}).EndsWith("18446744073709551615"),"64 bit Steam shortcut preserved");
            manifest["bIsIncompleteInstall"]=true; File.WriteAllText(file,Storage.Json().Serialize(manifest)); Check(EpicLibrary.Scan(root,catalog,warnings).Count==0,"Incomplete excluded");
            manifest["bIsIncompleteInstall"]=false; manifest["LaunchExecutable"]="../outside.exe"; File.WriteAllText(Path.Combine(root,"outside.exe"),""); File.WriteAllText(file,Storage.Json().Serialize(manifest)); Check(EpicLibrary.Scan(root,catalog,warnings).Count==0,"Traversal rejected");
            Check(!ArtworkNetwork.AllowedImageUrl("https://cdn1.epicgames.com.evil.test/x") && !ArtworkNetwork.AllowedImageUrl("http://cdn1.epicgames.com/x"),"Image host and HTTPS boundaries");
            ArtworkKey.Save("secret-test-key"); Check(ArtworkKey.Load()=="secret-test-key","DPAPI roundtrip"); Check(!Encoding.UTF8.GetString(File.ReadAllBytes(Path.Combine(root,"steamgriddb.key"))).Contains("secret-test-key"),"Key encrypted at rest"); ArtworkKey.Clear(); Check(ArtworkKey.Load()==null,"Key removal");
            string steamArt=Path.Combine(root,"custom-wide.png");
            using(var image=new System.Drawing.Bitmap(100,50)) image.Save(steamArt,System.Drawing.Imaging.ImageFormat.Png);
            var shortcutArt=new Game {Id="custom",Shortcut=true,Art=steamArt};
            Check(ArtworkService.KeepArtwork(shortcutArt),"Custom Steam shortcut artwork preserved regardless of dimensions");
            ArtworkService.ApplyCached(shortcutArt,new Settings());
            Check(shortcutArt.Art==steamArt,"Artwork cache never replaces existing Steam custom art");
            Check(!ArtworkService.KeepArtwork(new Game {Source="epic",Art=steamArt}),"Unsuitable Epic artwork can still use portrait fallback");
            Check(!ArtworkService.KeepArtwork(new Game {Id="missing",Art=Path.Combine(root,"missing.png")}),"Missing Steam artwork still eligible for fallback");
            int calls=0;
            ArtworkService.Fetch=(url,key,token) => {calls++; Check(key=="test","API bearer passed"); return Encoding.UTF8.GetBytes(url.Contains("autocomplete") ? "{\"success\":true,\"data\":[{\"id\":42,\"name\":\"Amnesia: The Dark Descent\"}]}" : "{\"success\":true,\"data\":[{\"url\":\"https://cdn2.steamgriddb.com/grid/test.png\"}]}");};
            Check(ArtworkService.FindGrid(games[0],"test",CancellationToken.None).Contains("test.png") && calls==2,"Exact SGDB match and portrait lookup");
            ArtworkService.Fetch=(url,key,token) => Encoding.UTF8.GetBytes("{\"success\":true,\"data\":[{\"id\":42,\"name\":\"Amnesia Collection\"}]}");
            Check(ArtworkService.FindGrid(games[0],"test",CancellationToken.None)==null,"Different edition never guessed");
            Console.WriteLine(checks+" external integration checks passed");
            var combinedSettings=new Settings {EnabledCollectionIds=new List<string>{"a","b"}};
            var collectionLibrary=new Library {Games=new List<Game>{new Game {Id="1"},new Game {Id="2"},new Game {Id="3"}},Collections=new List<Collection>{new Collection {Id="a",Name="First",Games=new List<string>{"1","2"}},new Collection {Id="b",Name="Second",Games=new List<string>{"2","3"}}}};
            string combinedId=CombinedCollections.Save(combinedSettings,collectionLibrary,null,"Together",new[]{"a","b"});
            Check(CombinedCollections.Options(collectionLibrary,combinedSettings).Last().Games.SequenceEqual(new[]{"1","2","3"}),"Union deduplicates shared games");
            Check(combinedSettings.EnabledCollectionIds.Contains(combinedId),"New combined collection automatically enabled");
            collectionLibrary.Collections[0].Games=new List<string>{"3"};
            Check(!CombinedCollections.Options(collectionLibrary,combinedSettings).Last().Games.Contains("1"),"Combined membership follows refreshed sources");
            CombinedCollections.Save(combinedSettings,collectionLibrary,combinedId,"Renamed",new[]{"a","all"});
            Check(combinedSettings.CombinedCollections.Count==1 && combinedSettings.CombinedCollections[0].Name=="Renamed","Editing keeps identity and replaces definition");
            var restored=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(combinedSettings));
            Check(CombinedCollections.Options(collectionLibrary,restored).Last().Games.Count==3,"Definition persists and supports All games");
            bool rejected=false; try {CombinedCollections.Save(combinedSettings,collectionLibrary,null,"Bad",new[]{"a"});} catch(ArgumentException) {rejected=true;} Check(rejected,"At least two sources required");
            rejected=false; try {CombinedCollections.Save(combinedSettings,collectionLibrary,null,"Renamed",new[]{"a","b"});} catch(ArgumentException) {rejected=true;} Check(rejected,"Duplicate names rejected");
            rejected=false; try {CombinedCollections.Save(combinedSettings,collectionLibrary,combinedId,"Cycle",new[]{combinedId,"a"});} catch(ArgumentException) {rejected=true;} Check(rejected,"Combined sources cannot introduce cycles");
            collectionLibrary.Collections.Clear(); Check(CombinedCollections.Options(collectionLibrary,restored).Last().Games.Count==3,"Missing sources handled safely");
            Console.WriteLine("9 combined collection checks passed");
            var customSettings=new Settings();
            var custom=new CustomGame {Name="Standalone",Target=Path.Combine(install,"game.exe"),Arguments="--test \"two words\"",Artwork=steamArt};
            string customId=CustomGameLibrary.Save(customSettings,custom);
            var customLibrary=new Library(); CustomGameLibrary.Apply(customLibrary,customSettings);
            Check(customLibrary.Collections.Single().Id=="source:custom" && customLibrary.Games.Single().Source=="custom","Custom games get their own collection");
            var start=GameLaunch.StartInfo(customLibrary.Games.Single());
            Check(start.FileName==custom.Target && start.Arguments==custom.Arguments && start.WorkingDirectory==install,"Custom launch retains arguments and defaults working directory");
            Check(ArtworkService.KeepArtwork(customLibrary.Games.Single()),"Custom local artwork always preserved");
            custom=new CustomGame {Id=customId,Name="Renamed standalone",Target=custom.Target,WorkingDirectory=root}; CustomGameLibrary.Save(customSettings,custom); CustomGameLibrary.Apply(customLibrary,customSettings);
            Check(customLibrary.Games.Count==1 && customLibrary.Games[0].Id==customId && GameLaunch.StartInfo(customLibrary.Games[0]).WorkingDirectory==root,"Editing retains identity and replaces previous entry");
            var customRestored=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(customSettings)); Check(customRestored.CustomGames.Single().Name=="Renamed standalone","Custom games persist in settings");
            bool invalid=false; try {CustomGameLibrary.Save(customSettings,new CustomGame {Name="Invalid",Target=catalog});} catch(ArgumentException) {invalid=true;} Check(invalid,"Nonlaunchable target rejected");
            invalid=false; try {CustomGameLibrary.Save(customSettings,new CustomGame {Name="Invalid",Target=custom.Target,WorkingDirectory=Path.Combine(root,"absent")});} catch(ArgumentException) {invalid=true;} Check(invalid,"Missing working directory rejected");
            customSettings.CustomGames.Clear(); CustomGameLibrary.Apply(customLibrary,customSettings);
            Check(customLibrary.Games.Count==0 && customLibrary.Collections.Count==0 && File.Exists(custom.Target),"Removing entry keeps game files");
            Console.WriteLine("8 custom game checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
