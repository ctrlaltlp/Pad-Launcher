using System;
using System.IO;
using System.Linq;
using PadLauncher;
class GogTests {
    static int checks;
    static void Check(bool ok,string name) {if(!ok) throw new Exception(name); checks++;}
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"GogTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            string game=Path.Combine(root,"Game folder"); Directory.CreateDirectory(Path.Combine(game,"ScummVM")); File.WriteAllText(Path.Combine(game,"ScummVM","game.exe"),"");
            string manifest=Path.Combine(game,"goggame-123.info");
            File.WriteAllText(manifest,"{\"gameId\":\"123\",\"rootGameId\":\"123\",\"name\":\"Base game\",\"playTasks\":[{\"type\":\"FileTask\",\"isPrimary\":true,\"path\":\"ScummVM/game.exe\"}]}");
            var record=new GogInstall {Id="123",Name="Registry title",Folder=game}; var scanned=GogLibrary.Scan(new[]{record,record,new GogInstall {Id="999",Folder=Path.Combine(root,"missing")}});
            Check(scanned.Count==1 && scanned[0].Name=="Base game" && scanned[0].Id=="gog:123","Manifest validates registry game, deduplicates views, and ignores stale installs");
            string client=Path.Combine(root,"GalaxyClient.exe"); File.WriteAllText(client,""); var start=GogLibrary.StartInfo(scanned[0],client);
            Check(start.Arguments.Contains("/gameId=123") && start.Arguments.Contains("/path=\""+game+"\"") && start.UseShellExecute,"Galaxy command has native ID and quoted installation path");
            Check(Storage.Json().Deserialize<Settings>("{}").GogEnabled,"Older settings enable native GOG integration by default");
            Check(!Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(new Settings {GogEnabled=false})).GogEnabled,"GOG integration can be disabled persistently");
            var playniteDuplicate=new PlayniteGame {PluginId="aebe8b7c-6dc3-4a66-af31-e7375c6b5e9e",NativeId="123"};
            Check(PlayniteImport.NativeDuplicate(playniteDuplicate,new Library {Games=scanned}),"Playnite import recognises GOG games already detected natively");
            File.WriteAllText(manifest,"{\"gameId\":\"123\",\"rootGameId\":\"999\",\"name\":\"DLC\",\"playTasks\":[]}"); Check(GogLibrary.Scan(new[]{record}).Count==0,"DLC entries excluded");
            File.WriteAllText(manifest,"{\"gameId\":\"123\",\"name\":\"Bad path\",\"playTasks\":[{\"type\":\"FileTask\",\"isPrimary\":true,\"path\":\"../GalaxyClient.exe\"}]}"); Check(GogLibrary.Scan(new[]{record}).Count==0,"Primary launch target cannot escape install folder");
            File.WriteAllText(manifest,"not json"); Check(GogLibrary.Scan(new[]{record}).Count==0,"Malformed manifest isolated");
            bool invalid=false; try {GogLibrary.StartInfo(new Game {Source="gog",LaunchId="123 /command=other",WorkingDirectory=game},client);} catch(InvalidOperationException) {invalid=true;} Check(invalid,"Non-numeric product ID rejected before constructing launch command");
            Console.WriteLine(checks+" GOG adapter checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
