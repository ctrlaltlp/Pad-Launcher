using System;
using System.IO;
using System.Linq;
using PadLauncher;
class UbisoftTests {
    static int checks;
    static void Check(bool ok,string name) {if(!ok) throw new Exception(name); checks++;}
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"UbisoftTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            string folder=Path.Combine(root,"Test Game"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,"Game.exe"),"test");
            var record=new UbisoftInstall {Id="16382",Folder=folder.Replace('\\','/')+"/"}; var games=UbisoftLibrary.Scan(new[]{record,record,new UbisoftInstall {Id="99",Folder=Path.Combine(root,"missing")}});
            Check(games.Count==1 && games[0].Id=="ubisoft:16382" && games[0].Name=="Test Game","Registry IDs, slash paths, duplicate views and stale installs handled");
            Check(GameLaunch.UriFor(games[0])=="uplay://launch/16382","Connect launch uses registered native game ID");
            string client=Path.Combine(root,"UbisoftConnect.exe"); File.WriteAllText(client,""); var settings=new Settings {UbisoftLauncherPath=client}; var start=GameLaunch.StartInfo(games[0],settings);
            Check(start.FileName==client && start.Arguments=="\"uplay://launch/16382\"" && start.UseShellExecute,"Configured client receives quoted Ubisoft launch URI");
            File.Delete(Path.Combine(folder,"Game.exe")); Directory.CreateDirectory(Path.Combine(folder,"uplay_download")); Check(UbisoftLibrary.Scan(new[]{record}).Count==0,"In-progress download without game executable excluded");
            Directory.CreateDirectory(Path.Combine(folder,"bin64")); File.WriteAllText(Path.Combine(folder,"bin64","Game.exe"),"test"); Check(UbisoftLibrary.Scan(new[]{record}).Count==1,"Common nested game executable folder supported");
            bool rejected=false; try {UbisoftLibrary.UriFor(new Game {Source="ubisoft",LaunchId="16382/other"});} catch(InvalidOperationException) {rejected=true;} Check(rejected,"Malformed Ubisoft game IDs rejected");
            Check(UbisoftLibrary.ValidateClient("")==null && Storage.Json().Deserialize<Settings>("{}").UbisoftEnabled,"Automatic client detection and native integration enabled by default");
            Check(Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)).UbisoftLauncherPath==client,"Custom Ubisoft path persists");
            Check(PlayniteImport.NativeDuplicate(new PlayniteGame {PluginId="c2f038e5-8b92-4877-91f1-da9094155fc5",NativeId="16382"},new Library {Games=games}),"Playnite identifies existing native Ubisoft entries");
            rejected=false; try {UbisoftLibrary.ValidateClient(Path.Combine(root,"Other.exe"));} catch(ArgumentException) {rejected=true;} Check(rejected,"Unrelated launcher executable rejected");
            Console.WriteLine(checks+" Ubisoft integration checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
