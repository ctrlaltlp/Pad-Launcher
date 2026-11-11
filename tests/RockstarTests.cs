using System;
using System.IO;
using System.Linq;
using PadLauncher;
class RockstarTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"RockstarTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            Check(RockstarLibrary.TitleFromUninstall("\"C:\\Program Files\\Rockstar Games\\Launcher\\Launcher.exe\" -uninstall=gta5_gen9")=="gta5_gen9","Completed uninstall registration parsed");
            Check(RockstarLibrary.TitleFromUninstall("\"C:\\Games\\uninstall.exe\" -uninstall=RDR2")=="rdr2","Game uninstaller accepted");
            Check(RockstarLibrary.TitleFromUninstall("Launcher.exe -uninstall=unknown")==null,"Unsupported title ignored");
            string folder=Path.Combine(root,"GTA V Enhanced"); Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,"GTA5_Enhanced.exe"),"");
            Check(RockstarLibrary.Scan(new RockstarInstall[0]).Count==0,"Files alone do not establish completed installation");
            var record=new RockstarInstall {TitleId="gta5_gen9",Folder=folder};
            var games=RockstarLibrary.Scan(new[]{record,record,new RockstarInstall {TitleId="rdr2",Folder=folder}});
            Check(games.Count==1 && games[0].Id=="rockstar:gta5_gen9","Known executable verified and duplicate registry records removed");
            string client=Path.Combine(root,"Launcher.exe"); File.WriteAllText(client,"");
            var settings=new Settings {RockstarLauncherPath=client}; var start=GameLaunch.StartInfo(games[0],settings);
            Check(start.FileName==client && start.Arguments=="-launchTitleInFolder \""+folder+"\"" && start.UseShellExecute,"Native launcher routing quotes install directory");
            Check(new Settings().RockstarEnabled && RockstarLibrary.ValidateClient(" ")==null,"Enabled and auto-detected by default");
            Check(Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)).RockstarLauncherPath==client,"Launcher preference persists");
            Check(PlayniteImport.NativeDuplicate(new PlayniteGame {PluginId="88409022-088a-4de8-805a-fdbac291f00a",NativeId="gta5_gen9"},new Library {Games=games}),"Playnite native duplicate identified");
            File.Delete(Path.Combine(folder,"GTA5_Enhanced.exe"));
            Check(RockstarLibrary.Scan(new[]{record}).Count==0,"Stale completed registration excluded");
            Console.WriteLine(checks+" Rockstar checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
