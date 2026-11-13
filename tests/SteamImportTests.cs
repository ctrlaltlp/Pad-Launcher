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
            Check(SteamShortcutImport.ExistingStatus(new Settings(),native,record,true)=="already available through Steam","Steam mode recognises native shortcut");
            Check(SteamShortcutImport.ExistingStatus(new Settings(),native,record,false)=="available through Steam; direct launch not added","Direct mode does not claim native Steam entry is a direct import");
            Check(SteamShortcutImport.ExistingStatus(settings,native,record,false)=="already added for direct launch","Direct mode recognises imported direct target");
            var conflicts=PlayniteImport.Conflicts(new Settings(),native,prepared); Check(conflicts.Count==1,"Direct import versus native Steam launch enters shared batch review");
            SteamShortcutImport.SelectLaunchModes(new Settings(),conflicts);
            Check(conflicts[0].Alternatives.Single(c => c.Incoming!=null).Keep && !conflicts[0].Alternatives.Single(c => c.Existing!=null).Keep,"Direct choice selects import and unselects native Steam entry");
            var steamConflicts=PlayniteImport.Conflicts(new Settings(),native,new List<CustomGame>{SteamShortcutImport.Prepare(record,true)});
            SteamShortcutImport.SelectLaunchModes(new Settings(),steamConflicts);
            Check(steamConflicts[0].Alternatives.Single(c => c.Existing!=null).Keep && !steamConflicts[0].Alternatives.Single(c => c.Incoming!=null).Keep,"Steam choice keeps native entry without adding a duplicate");
            foreach(var choice in conflicts[0].Alternatives) choice.Keep=choice.Incoming!=null;
            var replaced=new Settings(); PlayniteImport.ApplyChoices(replaced,prepared,conflicts); Check(replaced.CustomGames.Count==1 && replaced.ReplacedGameIds.Contains("1234"),"Keep imported hides only original launcher entry, without modifying Steam");
            Check(replaced.CustomGames[0].SteamShortcutKey==record.Key,"Shared review preserves Steam repeat-import identity");
            var changed=SteamShortcutImport.Prepare(record,true); var current=new Library(); CustomGameLibrary.Apply(current,replaced);
            Check(PlayniteImport.Conflicts(replaced,current,new List<CustomGame>{changed}).Count==1,"Changing Steam/direct launch mode is reviewed before reimport");
            steamOnly.LaunchViaSteam=false; rejected=false; try {CustomGameLibrary.Save(new Settings(),steamOnly);} catch(ArgumentException) {rejected=true;} Check(rejected,"Steam wrapper cannot be accidentally saved as a direct custom launch");
            var bothSettings=new Settings(); var both=SteamShortcutImport.PrepareModes(record,"both");
            Check(both.Count==2 && both.Any(g => g.LaunchViaSteam) && both.Any(g => !g.LaunchViaSteam),"Both mode prepares separate Steam and direct entries");
            foreach(var game in both) SteamShortcutImport.Merge(bothSettings,game);
            foreach(var game in SteamShortcutImport.PrepareModes(record,"both")) SteamShortcutImport.Merge(bothSettings,game);
            Check(bothSettings.CustomGames.Count==2,"Repeat Both import does not duplicate either launch mode");
            var bothLibrary=new Library(); CustomGameLibrary.Apply(bothLibrary,bothSettings);
            Check(bothLibrary.Games.Any(g => GameLaunch.StartInfo(g).FileName==exe) && bothLibrary.Games.Any(g => GameLaunch.StartInfo(g).FileName.StartsWith("steam://")),"Both entries preserve different actual launch actions");
            var bothConflicts=PlayniteImport.Conflicts(new Settings(),native,SteamShortcutImport.PrepareModes(record,"both"));
            SteamShortcutImport.SelectLaunchModes(new Settings(),bothConflicts);
            Check(bothConflicts[0].Alternatives.Single(c => c.Existing!=null).Keep && bothConflicts[0].Alternatives.Single(c => c.Incoming!=null && !c.Incoming.LaunchViaSteam).Keep && !bothConflicts[0].Alternatives.Single(c => c.Incoming!=null && c.Incoming.LaunchViaSteam).Keep,"Both mode keeps native Steam and direct import without third entry");
            var repeatBoth=SteamShortcutImport.PrepareModes(record,"both");
            Check(PlayniteImport.Conflicts(bothSettings,bothLibrary,repeatBoth).Count==0,"Unchanged Both reimport has no conflicts");
            var membership=new Settings {CustomLists=new List<CustomList>{new CustomList {Id="list:test",Name="My games",Games=new List<string>{"1234"}}}};
            var oldLibrary=new Library {Games=native.Games,Collections=new List<Collection>{new Collection {Id="tag:test",Name="Steam collection",Games=new List<string>{"1234"}}}};
            var replacement=SteamShortcutImport.Prepare(record,false); SteamShortcutImport.Merge(membership,replacement);
            SteamShortcutImport.PreserveLists(membership,oldLibrary,new List<CustomGame>{replacement},new Dictionary<string,string>());
            Check(membership.CustomLists[0].Games.Contains(replacement.Id) && membership.ListGameAdditions["tag:test"].Contains(replacement.Id),"Replacement inherits custom and Steam list membership");
            var after=new Library {Games=new List<Game>(native.Games),Collections=oldLibrary.Collections}; CustomGameLibrary.Apply(after,membership);
            Check(CombinedCollections.Options(after,membership).Single(l => l.Id=="tag:test").Games.Contains(replacement.Id),"Preserved Steam list includes direct replacement after rebuilding lists");
            var destinationSettings=new Settings {CustomLists=new List<CustomList>{new CustomList {Id="list:chosen",Name="Chosen",Games=new List<string>()}}};
            var chosen=SteamShortcutImport.Prepare(record,false); SteamShortcutImport.Merge(destinationSettings,chosen);
            SteamShortcutImport.PreserveLists(destinationSettings,oldLibrary,new List<CustomGame>{chosen},new Dictionary<string,string>{{record.Key,"list:chosen"}});
            Check(destinationSettings.CustomLists[0].Games.Contains(chosen.Id),"Explicit destination adds imported game to chosen list");
            destinationSettings.CombinedCollections=new List<CombinedCollection>{new CombinedCollection {Id="combined:pc",Name="PC",Sources=new List<string>{"source:custom"}}};
            var assignedLibrary=new Library(); CustomGameLibrary.Apply(assignedLibrary,destinationSettings);
            var assignedLists=CombinedCollections.Options(assignedLibrary,destinationSettings);
            Check(assignedLibrary.Games.Any(g => g.Id==chosen.Id) && assignedLists.Single(l => l.Id=="list:chosen").Games.Contains(chosen.Id) && !assignedLists.Single(l => l.Id=="source:custom").Games.Contains(chosen.Id) && !assignedLists.Single(l => l.Id=="combined:pc").Games.Contains(chosen.Id),"Explicit Steam import destination does not feed Custom games or its combined lists");
            chosen.IncludeInCustomGamesList=null;
            Check(!CustomGameLibrary.InCustomGamesList(destinationSettings,chosen),"Previously imported Steam entries already assigned to a list are excluded from automatic Custom games");
            SteamShortcutImport.PreserveLists(destinationSettings,assignedLibrary,new List<CustomGame>{chosen},new Dictionary<string,string>{{record.Key,"source:custom"}});
            CustomGameLibrary.Apply(assignedLibrary,destinationSettings);
            Check(CombinedCollections.Options(assignedLibrary,destinationSettings).Single(l => l.Id=="source:custom").Games.Contains(chosen.Id),"Explicit Custom games destination still includes the import");
            SteamShortcutImport.PreserveLists(destinationSettings,assignedLibrary,new List<CustomGame>{chosen},new Dictionary<string,string>{{record.Key,"list:chosen"}});
            Check(!destinationSettings.ListGameAdditions["source:custom"].Contains(chosen.Id) && !CustomGameLibrary.InCustomGamesList(destinationSettings,chosen),"Choosing another destination removes stale Custom games links");
            CustomLists.SetGameList(destinationSettings,assignedLibrary,chosen.Id,"source:custom");
            CustomGameLibrary.Apply(assignedLibrary,destinationSettings);
            Check(CustomGameLibrary.InCustomGamesList(destinationSettings,chosen) && CombinedCollections.Options(assignedLibrary,destinationSettings).Single(l => l.Id=="source:custom").Games.Contains(chosen.Id) && !destinationSettings.CustomLists[0].Games.Contains(chosen.Id),"Game editor can move an imported entry to the built-in Custom games list");
            CustomLists.Save(destinationSettings,assignedLibrary,"source:custom","Custom games",new string[0]);
            Check(!CustomGameLibrary.InCustomGamesList(destinationSettings,chosen),"Built-in Custom games membership can be edited and emptied");
            CustomLists.Save(destinationSettings,assignedLibrary,"source:custom","Custom games",new[]{chosen.Id});
            Check(CustomGameLibrary.InCustomGamesList(destinationSettings,chosen),"Built-in Custom games editor can restore a selected custom record");
            CustomLists.SetMembership(destinationSettings,assignedLibrary,chosen.Id,"combined:pc",false);
            Check(!CombinedCollections.Options(assignedLibrary,destinationSettings).Single(list => list.Id=="combined:pc").Games.Contains(chosen.Id) && destinationSettings.CustomGames.Any(game => game.Id==chosen.Id),"Membership editor can exclude an inherited entry from PC without deleting its game record");
            CustomLists.SetMembership(destinationSettings,assignedLibrary,chosen.Id,"combined:pc",true);
            Check(CombinedCollections.Options(assignedLibrary,destinationSettings).Single(list => list.Id=="combined:pc").Games.Contains(chosen.Id),"Membership editor restores an excluded combined-list entry");
            CustomLists.SetMembership(destinationSettings,assignedLibrary,chosen.Id,"source:custom",false);
            Check(!CustomGameLibrary.InCustomGamesList(destinationSettings,chosen) && destinationSettings.CustomLists[0].Games.Count==0,"Editing Custom games membership does not change other list definitions");
            record.Artwork=Path.Combine(root,"broken.png"); File.WriteAllText(record.Artwork,"broken image"); Check(SteamShortcutImport.Prepare(record,false).Artwork==null,"Unreadable source artwork does not prevent game import");
            Console.WriteLine(checks+" Steam shortcut import checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
