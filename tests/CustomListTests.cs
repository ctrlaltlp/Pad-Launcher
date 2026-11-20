using System;
using System.Linq;
using System.Collections.Generic;
using PadLauncher;
class CustomListTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    static void Reject(Action action,string name) {bool rejected=false; try {action();} catch(ArgumentException) {rejected=true;} Check(rejected,name);}
    public static void Main() {
        var library=new Library {Games=new List<Game> {new Game {Id="1",Source="steam"},new Game {Id="epic:2",Source="epic"}}};
        var settings=new Settings {EnabledCollectionIds=new List<string>{"all"}};
        string id=CustomLists.Save(settings,library,null,"Mixed favourites",new[]{"1","epic:2","1"});
        Check(settings.CustomLists.Single().Games.Count==2,"Cross-launcher membership deduplicated");
        Check(settings.EnabledCollectionIds.Contains(id),"New list immediately available");
        var restored=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
        Check(CombinedCollections.Options(library,restored).Single(c => c.Id==id).Games.Count==2,"Membership persists");
        CustomLists.Save(settings,library,id,"My games",new[]{"epic:2"});
        Check(settings.CustomLists.Count==1 && settings.CustomLists[0].Name=="My games" && settings.CustomLists[0].Id==id,"Edit preserves identity");
        Reject(() => CustomLists.Save(settings,library,null,"my games",new[]{"1"}),"Duplicate custom names rejected");
        Reject(() => CustomLists.Save(settings,library,null,"All games",new[]{"1"}),"Built-in name collision rejected");
        Reject(() => CustomLists.Save(settings,library,null,"Empty",new string[0]),"Empty selection rejected");
        Reject(() => CustomLists.Save(settings,library,null,"Invalid",new[]{"unknown"}),"Unknown game rejected");
        string combined=CombinedCollections.Save(settings,library,null,"Together",new[]{id,"source:steam"});
        Check(CombinedCollections.Options(library,settings).Single(c => c.Id==combined).Games.Count==2,"Custom list can be combined");
        library.Games.RemoveAll(g => g.Id=="epic:2");
        CustomLists.Save(settings,library,id,"My games",new[]{"epic:2","1"});
        Check(settings.CustomLists[0].Games.Contains("epic:2"),"Unavailable saved selection retained on edit");
        Check(CombinedCollections.Label(CombinedCollections.Options(library,settings).Single(c => c.Id==id),library)=="My games (1 game)","Count excludes unavailable selections");
        library.Games.Add(new Game {Id="epic:2",Source="epic"});
        Check(CombinedCollections.Label(CombinedCollections.Options(library,settings).Single(c => c.Id==id),library)=="My games (2 games)","Returning game reappears automatically");
        settings.CollectionId=id; settings.DefaultCollectionId=id; settings.EnabledCollectionIds=new List<string>{id};
        CustomLists.Delete(settings,id);
        Check(settings.CollectionId=="all" && settings.DefaultCollectionId=="remember" && settings.EnabledCollectionIds.SequenceEqual(new[]{"all"}),"Delete repairs selection and default");
        Check(CombinedCollections.Options(library,settings).Single(c => c.Id==combined).Games.SequenceEqual(new[]{"1"}),"Combined list handles deleted source");
        CustomLists.Save(settings,library,combined,"Together",new[]{"epic:2"});
        Check(CombinedCollections.Options(library,settings).Single(c => c.Id==combined).Games.SequenceEqual(new[]{"epic:2"}),"Combined list supports individual additions and removals");
        Check(settings.CombinedCollections.Single().Sources.Contains("source:steam"),"Individual editing preserves combined source links");
        library.Games.Add(new Game {Id="new-steam",Source="steam"});
        Check(CombinedCollections.Options(library,settings).Single(c => c.Id==combined).Games.Contains("new-steam"),"Combined sources continue updating after individual editing");
        var provenanceOptions=CombinedCollections.Options(library,settings);
        Check(CustomLists.MembershipReason(settings,provenanceOptions,combined,"new-steam").Contains("From Steam"),"Combined game explanation identifies inherited source");
        Check(CustomLists.MembershipReason(settings,provenanceOptions,combined,"epic:2").Contains("Added individually"),"Combined game explanation identifies individual additions");
        var removalLibrary=new Library {Games=new List<Game> {new Game {Id="pc",Source="custom"},new Game {Id="rom",Source="emulator",EmulatorId="duckstation"},new Game {Id="steam",Source="steam"},new Game {Id="keep",Source="custom"}}};
        var removalSettings=new Settings {
            CustomGames=new List<CustomGame> {new CustomGame {Id="pc",Target="untouched.exe"},new CustomGame {Id="keep"}},
            Emulators=new List<EmulatorConfig> {new EmulatorConfig {Id="duckstation",Games=new List<EmulatorGame> {new EmulatorGame {Id="rom",Path="untouched.cue"}}}},
            FavouriteGameIds=new List<string> {"pc","keep"},HiddenGameIds=new List<string> {"rom"},
            ArtworkOverrides=new Dictionary<string,string> {{"pc","untouched.png"}},
            LaunchHistory=new Dictionary<string,long> {{"rom",123}},
            SelectedGamesByList=new Dictionary<string,string> {{"shared","rom"}},
            ListGameAdditions=new Dictionary<string,List<string>> {{"source:steam",new List<string> {"pc","keep"}}}
        };
        string doomed=CustomLists.Save(removalSettings,removalLibrary,null,"Delete me",new[]{"pc","rom","steam"});
        string shared=CustomLists.Save(removalSettings,removalLibrary,null,"Shared",new[]{"pc","rom","keep"});
        string combination=CombinedCollections.Save(removalSettings,removalLibrary,null,"Combined",new[]{doomed,shared});
        var removed=CustomLists.DeleteAndUnregisterGames(removalSettings,removalLibrary,doomed);
        Check(removed.Count==3 && removalSettings.CustomGames.Single().Id=="keep" && removalSettings.Emulators.Single().Games.Count==0,"Delete with games unregisters emulator and custom entries while preserving unrelated games");
        Check(removalSettings.CustomLists.Single().Id==shared && removalSettings.CustomLists.Single().Games.SequenceEqual(new[]{"keep"}) && !removalSettings.CombinedCollections.Single().Sources.Contains(doomed),"Unregistered games are removed from other lists and deleted list links are cleaned");
        Check(removalSettings.ReplacedGameIds.Contains("steam") && removalSettings.FavouriteGameIds.SequenceEqual(new[]{"keep"}) && removalSettings.HiddenGameIds.Count==0 && removalSettings.ArtworkOverrides.Count==0 && removalSettings.LaunchHistory.Count==0 && removalSettings.SelectedGamesByList.Count==0,"Unregistration suppresses rediscovery and clears stale game metadata");
        Check(removalSettings.ListGameAdditions["source:steam"].SequenceEqual(new[]{"keep"}) && removalLibrary.Games.Count==4,"Unregistration prepares settings without mutating the live library before settings are committed");
        Reject(() => CustomLists.DeleteAndUnregisterGames(removalSettings,removalLibrary,"source:steam"),"Cannot delete a built-in launcher list with unregister");
        var addedSettings=new Settings(); var addedLibrary=new Library {Games=new List<Game>{new Game {Id="old",Name="Old"}}};
        Check(CombinedCollections.TrackAddedGames(addedLibrary,addedSettings,100) && addedSettings.GameAddedAt["old"]==0,"First tracking run baselines existing games without inventing dates");
        Check(CombinedCollections.Options(addedLibrary,addedSettings).Single(list => list.Id=="recently-added").Games.Count==0,"Legacy games are not presented as newly added");
        addedLibrary.Games.Add(new Game {Id="new1",Name="New one"}); CombinedCollections.TrackAddedGames(addedLibrary,addedSettings,200);
        addedLibrary.Games.Add(new Game {Id="new2",Name="New two"}); CombinedCollections.TrackAddedGames(addedLibrary,addedSettings,300);
        Check(CombinedCollections.Options(addedLibrary,addedSettings).Single(list => list.Id=="recently-added").Games.SequenceEqual(new[]{"new2","new1"}),"Recently added sorts new registrations newest first");
        Check(!CombinedCollections.TrackAddedGames(addedLibrary,addedSettings,400) && addedSettings.GameAddedAt["new1"]==200,"Reimporting and refreshing do not replace first-added timestamps");
        addedSettings=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(addedSettings));
        Check(addedSettings.GameAddedAt["new2"]==300,"Added dates survive settings serialization");
        addedLibrary.Games.RemoveAll(game => game.Id=="new2");
        Check(CombinedCollections.Options(addedLibrary,addedSettings).Single(list => list.Id=="recently-added").Games.SequenceEqual(new[]{"new1"}),"Unregistered games disappear from Recently added");
        addedLibrary.Games.Add(new Game {Id="new2",Name="New two"}); CombinedCollections.TrackAddedGames(addedLibrary,addedSettings,500);
        Check(addedSettings.GameAddedAt["new2"]==300,"Rediscovering a known ID retains its original registration time");
        addedSettings.HiddenGameIds=new List<string>{"new2"};
        Check(CombinedCollections.Label(CombinedCollections.Options(addedLibrary,addedSettings).Single(list => list.Id=="recently-added"),addedLibrary,addedSettings.HiddenGameIds).EndsWith(" (1 game)"),"Recently added counts respect hidden games");
        var mergeSettings=new Settings {CustomLists=new List<CustomList> {new CustomList {Id="left",Name="Left",Games=new List<string>{"a","b"}},new CustomList {Id="right",Name="Right",Games=new List<string>{"b","c"}}}};
        var mergeLibrary=new Library {Games=new List<Game> {new Game {Id="a"},new Game {Id="b"},new Game {Id="c"}}};
        string merged=CustomLists.Merge(mergeSettings,mergeLibrary,"Merged",new[]{"left","right"});
        Check(mergeSettings.CustomLists.Single(list => list.Id==merged).Games.SequenceEqual(new[]{"a","b","c"}),"Merge unions game identities without duplicates");
        mergeSettings.CustomLists.Single(list => list.Id=="left").Games.Clear();
        Check(mergeSettings.CustomLists.Count==3 && mergeSettings.CustomLists.Single(list => list.Id==merged).Games.Count==3,"Merge keeps source lists and creates an independent snapshot");
        bool oneRejected=false; try {CustomLists.Merge(mergeSettings,mergeLibrary,"Bad",new[]{"right"});} catch(ArgumentException) {oneRejected=true;}
        Check(oneRejected,"Merge requires at least two source lists");
        var deleteMerge=new Settings {CustomLists=new List<CustomList> {new CustomList {Id="left",Name="Left",Games=new List<string>{"a","b"}},new CustomList {Id="right",Name="Right",Games=new List<string>{"b","c"}}}};
        string replaced=CustomLists.Merge(deleteMerge,mergeLibrary,"Replacement",new[]{"left","right"},false);
        Check(deleteMerge.CustomLists.Count==1 && deleteMerge.CustomLists[0].Id==replaced && deleteMerge.CustomLists[0].Games.Count==3 && mergeLibrary.Games.Count==3,"Optional source deletion keeps merged games registered");
        var builtInMerge=new Settings();
        CustomLists.Merge(builtInMerge,mergeLibrary,"Built-in copy",new[]{"all","source:steam"},false);
        Check(CombinedCollections.Options(mergeLibrary,builtInMerge).Any(list => list.Id=="all"),"Merging never deletes automatic source lists");
        var emulatorLists=new Settings {CustomLists=new List<CustomList> {
            new CustomList {Id="retro",Name="Retro",Games=new List<string>{"rom"}},new CustomList {Id="mixed",Name="PC",Games=new List<string>{"rom","pc"}},new CustomList {Id="empty-import",Name="Empty import",Games=new List<string>()}},
            EmulatorImports=new List<EmulatorImportDefinition> {new EmulatorImportDefinition {EmulatorId="retroarch",ListId="empty-import"}}};
        var emulatorPool=new Library {Games=new List<Game>{new Game {Id="rom",Source="emulator",EmulatorId="retroarch"},new Game {Id="pc",Source="steam"}}};
        Check(CustomLists.IsEmulatorOnlyList(emulatorLists,emulatorPool,"retro") && CustomLists.IsEmulatorOnlyList(emulatorLists,emulatorPool,"empty-import"),"Emulator-only lists are identified by content or import provenance when empty");
        Check(!CustomLists.IsEmulatorOnlyList(emulatorLists,emulatorPool,"mixed"),"Mixed PC lists remain custom-game destinations");
        var partialDelete=new Settings {CustomLists=new List<CustomList>{new CustomList {Id="left",Name="Left",Games=new List<string>{"a"}},new CustomList {Id="right",Name="Right",Games=new List<string>{"b"}}}};
        CustomLists.Merge(partialDelete,mergeLibrary,"Partial merge",new[]{"left","right"},new[]{"left"});
        Check(!partialDelete.CustomLists.Any(list => list.Id=="left") && partialDelete.CustomLists.Any(list => list.Id=="right") && partialDelete.CustomLists.Single(list => list.Name=="Partial merge").Games.Count==2,"Selective merge deletion removes only chosen sources");
        bool unrelatedRejected=false; try {CustomLists.Merge(partialDelete,mergeLibrary,"Invalid merge",new[]{"all","source:steam"},new[]{"right"});} catch(ArgumentException) {unrelatedRejected=true;}
        Check(unrelatedRejected && partialDelete.CustomLists.Count==2,"Merge rejects deleting a list that is not a selected source before mutation");
        var cleared=new Settings {CustomLists=new List<CustomList>{new CustomList {Id="clear",Name="Clear me",Games=new List<string>{"a"}}}};
        CustomLists.Save(cleared,mergeLibrary,"clear","Clear me",new string[0]);
        Check(cleared.CustomLists.Single().Games.Count==0,"Existing lists can be saved empty after Uncheck added");
        Reject(() => CustomLists.Save(cleared,mergeLibrary,null,"New empty list",new string[0]),"New lists still require a game");
        Console.WriteLine(checks+" custom list checks passed");
    }
}
