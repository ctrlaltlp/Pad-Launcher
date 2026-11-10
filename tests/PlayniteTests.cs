using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PadLauncher;
class PlayniteTests {
    static int checks;
    static void Check(bool ok,string name) {if(!ok) throw new Exception(name); checks++;}
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"PlayniteImportTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=Path.Combine(root,"portable");
        try {
            string app=Path.Combine(root,"playnite"),library=Path.Combine(app,"library"); Directory.CreateDirectory(Path.Combine(library,"files")); File.WriteAllText(Path.Combine(app,"Playnite.DesktopApp.exe"),"");
            string id=Guid.NewGuid().ToString(); string cover=Path.Combine(library,"files","cover.png"); using(var image=new System.Drawing.Bitmap(100,150)) image.Save(cover,System.Drawing.Imaging.ImageFormat.Png);
            object fixture=Storage.Json().DeserializeObject("{\"_id\":{\"$guid\":\""+id+"\"},\"Name\":\"Standalone\",\"IsInstalled\":true,\"Hidden\":false,\"CoverImage\":\"cover.png\",\"GameId\":\"10\",\"PluginId\":{\"$guid\":\"cb91dfc9-b977-43bf-8e70-55f46e410fab\"}}");
            var game=PlayniteImport.Parse(fixture,library);
            Check(game.Id==id && game.Installed && !game.Hidden && game.Cover==cover,"BSON JSON game fields and local cover resolved");
            var native=new Library {Games=new List<Game>{new Game {Id="10"}}}; Check(PlayniteImport.NativeDuplicate(game,native),"Native Steam IDs prevent duplicate imports");
            game.PluginId="other"; Check(!PlayniteImport.NativeDuplicate(game,native),"Same numeric ID from another platform is not mistaken for Steam");
            var prepared=PlayniteImport.Prepare(game,app); var settings=new Settings(); PlayniteImport.Merge(settings,prepared);
            string importedId=settings.CustomGames.Single().Id;
            Check(prepared.Arguments.Contains("--start "+id) && prepared.Target.EndsWith("Playnite.DesktopApp.exe"),"Launch delegates to Playnite's configured game action");
            Check(!Path.IsPathRooted(prepared.Artwork) && File.Exists(CustomGameLibrary.ArtworkPath(prepared.Artwork)),"Imported cover copied into portable data");
            Check(File.Exists(cover),"Playnite original artwork preserved");
            var repeated=PlayniteImport.Prepare(game,app); repeated.Name="Updated name"; PlayniteImport.Merge(settings,repeated);
            Check(settings.CustomGames.Count==1 && settings.CustomGames[0].Id==importedId && settings.CustomGames[0].Name=="Updated name","Repeat import updates existing stable entry without duplicates");
            var edited=new CustomGame {Id=importedId,Name="Edited",Target=prepared.Target}; CustomGameLibrary.Save(settings,edited);
            Check(edited.PlayniteId==id,"Manual edits preserve Playnite identity for future reimports");
            var cloned=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); PlayniteImport.Merge(cloned,PlayniteImport.Prepare(game,app)); Check(cloned.CustomGames.Count==1,"Repeat import remains idempotent after settings reload");
            object unsafeCover=Storage.Json().DeserializeObject("{\"_id\":{\"$guid\":\""+id+"\"},\"Name\":\"Test\",\"CoverImage\":\"../outside.png\"}");
            Check(PlayniteImport.Parse(unsafeCover,library).Cover==null,"Artwork path cannot escape Playnite media folder");
            Check(PlayniteImport.Parse(Storage.Json().DeserializeObject("{\"_id\":\"bad\",\"Name\":\"Test\"}"),library)==null,"Malformed IDs excluded");
            File.WriteAllText(Path.Combine(app,"config.json"),"{\"DatabasePath\":\"{PlayniteDir}/library\"}"); Check(PlayniteImport.LibraryFolder(app)==library,"Portable library path resolved from config");
            var conflictSettings=new Settings(); var conflictLibrary=new Library {Games=new List<Game>{new Game {Id="10",Name="Standalone",LaunchId="10"}}};
            var pending=new List<CustomGame>{PlayniteImport.Prepare(game,app)};
            var conflicts=PlayniteImport.Conflicts(conflictSettings,conflictLibrary,pending);
            Check(conflicts.Count==1 && conflicts[0].Alternatives.Count==2,"Same name with native and Playnite launch actions produces one review group");
            Check(PlayniteImport.ApplyChoices(conflictSettings,pending,conflicts)==0 && (conflictSettings.CustomGames==null || conflictSettings.CustomGames.Count==0),"Keep existing skips imported alternative");
            conflictSettings=new Settings(); conflicts=PlayniteImport.Conflicts(conflictSettings,conflictLibrary,pending); foreach(var choice in conflicts[0].Alternatives) choice.Keep=choice.Incoming!=null;
            Check(PlayniteImport.ApplyChoices(conflictSettings,pending,conflicts)==1 && conflictSettings.ReplacedGameIds.Contains("10"),"Keep imported records native replacement without changing source library");
            conflictSettings=new Settings(); conflicts=PlayniteImport.Conflicts(conflictSettings,conflictLibrary,pending); foreach(var choice in conflicts[0].Alternatives) choice.Keep=true;
            Check(PlayniteImport.ApplyChoices(conflictSettings,pending,conflicts)==1 && conflictSettings.ReplacedGameIds==null,"Keep both imports without suppressing existing entry");
            var importedLibrary=new Library(); CustomGameLibrary.Apply(importedLibrary,conflictSettings);
            var repeatPending=new List<CustomGame>{PlayniteImport.Prepare(game,app)};
            Check(PlayniteImport.Conflicts(conflictSettings,importedLibrary,repeatPending).Count==0,"Unchanged repeat import does not ask for conflict review");
            repeatPending[0].PlayniteActionInfo="Different launch action";
            conflicts=PlayniteImport.Conflicts(conflictSettings,importedLibrary,repeatPending);
            Check(conflicts.Count==1,"Changed Playnite action is reviewed even with unchanged game ID");
            foreach(var choice in conflicts[0].Alternatives) choice.Keep=true;
            PlayniteImport.ApplyChoices(conflictSettings,repeatPending,conflicts);
            Check(conflictSettings.CustomGames.Count==2 && conflictSettings.CustomGames.Count(g => !String.IsNullOrEmpty(g.PlayniteId))==1,"Keeping both action versions preserves one stable reimport identity");
            Console.WriteLine(checks+" Playnite import checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
