using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PadLauncher;
class HiddenGamesTests {
    static int checks;
    static void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
    static FrameworkElement Find(DependencyObject root,string name) {
        var element=root as FrameworkElement; if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var found=Find(VisualTreeHelper.GetChild(root,i),name); if(found!=null) return found;}
        return null;
    }
    [STAThread] public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"HiddenGamesTests-"+Guid.NewGuid().ToString("N"));
        Storage.Root=root; Launcher window=null;
        try {
            Check(Storage.Json().Deserialize<Settings>("{}").HiddenGameIds==null,"Existing settings leave every game visible");
            window=new Launcher(true,true);
            var flags=BindingFlags.NonPublic|BindingFlags.Instance; var type=typeof(Launcher);
            var library=new Library {Games=new List<Game>{new Game {Id="a",Name="Alpha",Source="steam"},new Game {Id="b",Name="Beta",Source="custom"},new Game {Id="c",Name="Gamma",Source="emulator",EmulatorId="pcsx2"}},Collections=new List<Collection>{new Collection {Id="hidden-only",Name="Hidden only",Games=new List<string>{"a","b"}},new Collection {Id="mixed",Name="Mixed",Games=new List<string>{"a","c"}}}};
            var settings=new Settings {CollectionId="all",CustomLists=new List<CustomList>{new CustomList {Id="list:test",Name="My list",Games=new List<string>{"a","b","c"}}},FavouriteGameIds=new List<string>{"a"}};
            type.GetField("library",flags).SetValue(window,library); type.GetField("settings",flags).SetValue(window,settings);
            window.SetHiddenGames(new[]{"a","b","a","unavailable-game"});
            Func<List<Game>> visible=() => (List<Game>)type.GetField("visible",flags).GetValue(window);
            Func<List<Collection>> filters=() => (List<Collection>)type.GetField("filters",flags).GetValue(window);
            Check(visible().Select(g => g.Id).SequenceEqual(new[]{"c"}),"Hidden games are excluded from All games");
            Check(library.Games.Count==3 && settings.CustomLists[0].Games.Count==3,"Hiding preserves registered games and memberships");
            Check(!filters().Any(list => list.Id=="hidden-only" || list.Id=="favourites"),"Lists containing only hidden games are skipped when Skip empty lists is enabled");
            Check(CombinedCollections.Label(library.Collections[1],library,new[]{"a"})=="Mixed (1 game)" && CombinedCollections.Label(library.Collections[1],library)=="Mixed (2 games)","Overlay counts exclude hidden games while membership counts retain them");
            var saved=Storage.Read<Settings>("settings.json");
            Check(saved.HiddenGameIds.Count==3 && saved.HiddenGameIds.Contains("unavailable-game"),"Hidden IDs persist without duplicates, including temporarily unavailable games");
            saved.CollectionId="list:test"; type.GetField("settings",flags).SetValue(window,saved);
            type.GetMethod("ApplyLibrary",flags).Invoke(window,new object[]{false});
            Check(visible().Select(g => g.Id).SequenceEqual(new[]{"c"}),"Hidden games stay excluded after rebuilding a custom list");
            saved.CollectionId="mixed"; type.GetMethod("ApplyLibrary",flags).Invoke(window,new object[]{false});
            Check(visible().Select(g => g.Id).SequenceEqual(new[]{"c"}),"Source lists exclude hidden games too");
            bool rebuilt=false;
            var section=(Expander)type.GetMethod("CreateHiddenGamesSettings",flags).Invoke(window,new object[]{(Action)(() => rebuilt=true)});
            section.IsExpanded=true; section.Measure(new Size(700,800)); section.Arrange(new Rect(0,0,700,800)); section.UpdateLayout();
            var choices=(StackPanel)Find(section,"HiddenGamesChoices");
            var boxes=choices.Children.OfType<CheckBox>().ToList();
            Check(boxes.Count==3 && boxes.Single(b => (string)b.Tag=="b").IsChecked==true,"Hidden games remain available in the settings selector");
            ((TextBox)Find(section,"HiddenGamesSearch")).Text="Beta";
            Check(boxes.Count(b => b.Visibility==Visibility.Visible)==1,"Searching narrows choices without losing hidden selections");
            var beta=boxes.Single(b => (string)b.Tag=="b"); beta.IsChecked=false; beta.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            ((Button)Find(section,"SaveHiddenGames")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(rebuilt && !Storage.Read<Settings>("settings.json").HiddenGameIds.Contains("b") && Storage.Read<Settings>("settings.json").HiddenGameIds.Contains("a"),"Saving unhides only the unchecked game, preserving filtered-out choices");
            window.SetHiddenGames(new[]{"a","b","c"});
            Check(visible().Count==0 && library.Games.Count==3,"Hiding every game preserves the library and handles an empty overlay");
            window.SetHiddenGames(new string[0]);
            Check(visible().Select(g => g.Id).SequenceEqual(new[]{"a","c"}) && filters().Any(list => list.Id=="hidden-only"),"Unhiding restores games in the selected list and previously empty lists");
            Console.WriteLine(checks+" hidden game checks passed");
        } finally {if(window!=null) window.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
