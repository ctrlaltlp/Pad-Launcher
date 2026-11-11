using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using PadLauncher;
class SteamImportTests {
    static int checks;
    static void Check(bool ok,string name) {if(!ok) throw new Exception(name); checks++;}
    static void CString(BinaryWriter writer,string value) {writer.Write(Encoding.UTF8.GetBytes(value)); writer.Write((byte)0);}
    static void Text(BinaryWriter writer,string key,string value) {writer.Write((byte)1); CString(writer,key); CString(writer,value);}
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"SteamImportTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=Path.Combine(root,"portable");
        try {
            string folder=Path.Combine(root,"Game with spaces"); Directory.CreateDirectory(folder); string exe=Path.Combine(folder,"Game.exe"); File.WriteAllText(exe,"test");
            var entry=new Dictionary<string,object>{{"appid",(uint)1234},{"AppName","Standalone"},{"exe","\""+exe+"\" --embedded"},{"StartDir","\""+folder+"\""},{"LaunchOptions","--language \"en us\""}};
            var record=SteamShortcutImport.Parse(entry,"42");
            Check(record.Target==exe && record.CanLaunchDirect && record.WorkingDirectory==folder,"Quoted executable and working directory imported correctly");
            Check(record.Arguments=="--embedded --language \"en us\"","Embedded arguments and Steam launch options preserved");
            Check(record.Key=="42:1234" && record.SteamLaunchId==SteamLibrary.ShortcutLaunchId(1234),"Profile and shortcut IDs provide stable import identity");
            entry["LaunchOptions"]="%command% --safe"; var substituted=SteamShortcutImport.Parse(entry,"42"); Check(substituted.CanLaunchDirect && substituted.Arguments=="--embedded --safe","Leading command placeholder translated for direct launch");
            entry["LaunchOptions"]="wrapper.exe %command%"; var wrapped=SteamShortcutImport.Parse(entry,"42"); Check(!wrapped.CanLaunchDirect,"Wrapper launch options require Steam");
            bool rejected=false; try {SteamShortcutImport.Prepare(wrapped,false);} catch(ArgumentException) {rejected=true;} Check(rejected,"Unsupported direct translation cannot be silently imported");
            var steamOnly=SteamShortcutImport.Prepare(wrapped,true); var settings=new Settings(); SteamShortcutImport.Merge(settings,steamOnly);
            var library=new Library(); CustomGameLibrary.Apply(library,settings);
            Check(GameLaunch.StartInfo(library.Games.Single()).FileName=="steam://rungameid/"+record.SteamLaunchId,"Steam mode delegates to the original shortcut");
            Check(PlayniteImport.Conflicts(settings,library,new List<CustomGame>{SteamShortcutImport.Prepare(wrapped,true)}).Count==0,"Unchanged Steam-mode reimport does not prompt for conflicts");
            SteamShortcutImport.Merge(settings,SteamShortcutImport.Prepare(wrapped,true)); Check(settings.CustomGames.Count==1,"Repeated Steam imports update rather than duplicate");
            settings=new Settings(); SteamShortcutImport.Merge(settings,SteamShortcutImport.Prepare(record,false)); CustomGameLibrary.Apply(library,settings);
            var direct=GameLaunch.StartInfo(library.Games.Single()); Check(direct.FileName==exe && direct.Arguments==record.Arguments && direct.WorkingDirectory==folder,"Direct mode bypasses Steam and preserves launch configuration");
            string cover=Path.Combine(root,"steam-cover.png"); using(var image=new System.Drawing.Bitmap(200,300)) image.Save(cover,System.Drawing.Imaging.ImageFormat.Png); record.Artwork=cover;
            var withArt=SteamShortcutImport.Prepare(record,false); Check(!Path.IsPathRooted(withArt.Artwork) && File.Exists(CustomGameLibrary.ArtworkPath(withArt.Artwork)) && File.Exists(cover),"Steam artwork is copied to portable data without changing its source");
            string steam=Path.Combine(root,"steam"); Directory.CreateDirectory(Path.Combine(steam,"steamapps")); string config=Path.Combine(steam,"userdata","42","config"); Directory.CreateDirectory(config); string shortcuts=Path.Combine(config,"shortcuts.vdf");
            using(var writer=new BinaryWriter(File.Create(shortcuts))) {writer.Write((byte)0); CString(writer,"shortcuts"); writer.Write((byte)0); CString(writer,"0"); writer.Write((byte)2); CString(writer,"appid"); writer.Write((uint)1234); Text(writer,"AppName","Standalone"); Text(writer,"exe","\""+exe+"\" --embedded"); Text(writer,"StartDir","\""+folder+"\""); Text(writer,"LaunchOptions","--language \"en us\""); writer.Write((byte)8); writer.Write((byte)8);}
            byte[] before=File.ReadAllBytes(shortcuts); var read=SteamShortcutImport.Read(new Settings {SteamPath=steam,UserId="42"}); Check(read.Count==1 && read[0].Arguments==record.Arguments,"Binary Steam shortcuts file read through configured profile");
            Check(before.SequenceEqual(File.ReadAllBytes(shortcuts)),"Import reads leave Steam shortcut file unchanged");
            var native=new Library {Games=new List<Game>{new Game {Id="1234",Name="Standalone",LaunchId=record.SteamLaunchId,Shortcut=true}}}; var prepared=new List<CustomGame>{SteamShortcutImport.Prepare(record,false)};
            var conflicts=PlayniteImport.Conflicts(new Settings(),native,prepared); Check(conflicts.Count==1,"Direct import versus native Steam launch enters shared batch review");
            foreach(var choice in conflicts[0].Alternatives) choice.Keep=choice.Incoming!=null;
            var replaced=new Settings(); PlayniteImport.ApplyChoices(replaced,prepared,conflicts); Check(replaced.CustomGames.Count==1 && replaced.ReplacedGameIds.Contains("1234"),"Keep imported hides only original launcher entry, without modifying Steam");
            Check(replaced.CustomGames[0].SteamShortcutKey==record.Key,"Shared review preserves Steam repeat-import identity");
            var changed=SteamShortcutImport.Prepare(record,true); var current=new Library(); CustomGameLibrary.Apply(current,replaced);
            Check(PlayniteImport.Conflicts(replaced,current,new List<CustomGame>{changed}).Count==1,"Changing Steam/direct launch mode is reviewed before reimport");
            steamOnly.LaunchViaSteam=false; rejected=false; try {CustomGameLibrary.Save(new Settings(),steamOnly);} catch(ArgumentException) {rejected=true;} Check(rejected,"Steam wrapper cannot be accidentally saved as a direct custom launch");
            record.Artwork=Path.Combine(root,"broken.png"); File.WriteAllText(record.Artwork,"broken image"); Check(SteamShortcutImport.Prepare(record,false).Artwork==null,"Unreadable source artwork does not prevent game import");
            Console.WriteLine(checks+" Steam shortcut import checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
