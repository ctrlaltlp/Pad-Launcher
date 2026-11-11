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
        string id=CustomLists.Save(settings,library,null,"Favourites",new[]{"1","epic:2","1"});
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
        Console.WriteLine(checks+" custom list checks passed");
    }
}
