using System;
using System.IO;
using System.Linq;
using PadLauncher;
class EaTests {
    static int checks;
    static void Check(bool ok,string name) {if(!ok) throw new Exception(name); checks++;}
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EaTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            string folder=Path.Combine(root,"Test Game"); Directory.CreateDirectory(Path.Combine(folder,"__Installer")); File.WriteAllText(Path.Combine(folder,"Game.exe"),"test");
            string manifest=Path.Combine(folder,"__Installer","installerdata.xml");
            File.WriteAllText(manifest,"<game><contentIDs><contentID>1007718</contentID><contentID>1020191</contentID></contentIDs><metadata><localeInfo locale='en_US'><title>Fixture game</title></localeInfo></metadata></game>");
            var record=new EaInstall {Folder=folder}; var games=EaLibrary.Scan(new[]{record,record,new EaInstall {Folder=Path.Combine(root,"missing")}});
            Check(games.Count==1 && games[0].Source=="ea" && games[0].Id=="ea:1007718","Installed manifest creates one native EA game and deduplicates repeated records");
            Check(games[0].Name=="Fixture game","Manifest locale supplies game title");
            Check(GameLaunch.UriFor(games[0])=="origin2://game/launch?offerIds=1007718%2C1020191","All manifest content IDs passed in encoded EA launch URI");
            string client=Path.Combine(root,"EALauncher.exe"); File.WriteAllText(client,"");
            var start=GameLaunch.StartInfo(games[0],new Settings {EaLauncherPath=client}); Check(start.FileName==client && start.Arguments.Contains("origin2://game/launch") && start.UseShellExecute,"Game launcher routes EA game to configured EA App handler");
            File.Delete(Path.Combine(folder,"Game.exe")); Check(EaLibrary.Scan(new[]{record}).Count==0,"Partial download without game executable excluded");
            Directory.CreateDirectory(Path.Combine(folder,"Core")); string nested=Path.Combine(folder,"Core","Game.exe"); File.WriteAllText(nested,""); record.Executable=nested; Check(EaLibrary.Scan(new[]{record}).Count==1,"Registry target supports nested game executables");
            record.Executable=client; Check(EaLibrary.Scan(new[]{record}).Count==0,"Registry executable outside install folder rejected"); record.Executable=nested;
            File.WriteAllText(manifest,"<game><contentIDs><contentID>bad&amp;other=1</contentID></contentIDs></game>"); Check(EaLibrary.Scan(new[]{record}).Count==0,"Unsafe content IDs excluded");
            bool rejected=false; try {EaLibrary.UriFor(new Game {Source="ea",LaunchId="id&other=1"});} catch(InvalidOperationException) {rejected=true;} Check(rejected,"Unsafe launch parameters rejected");
            File.WriteAllText(manifest,"<!DOCTYPE game [<!ENTITY external SYSTEM 'file:///missing'>]><game><contentIDs><contentID>&external;</contentID></contentIDs></game>"); Check(EaLibrary.Scan(new[]{record}).Count==0,"External entity XML is not resolved");
            Check(Storage.Json().Deserialize<Settings>("{}").EaEnabled && EaLibrary.ValidateClient("")==null,"EA enabled by default with automatic launcher detection");
            Console.WriteLine(checks+" EA integration checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
