using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PadLauncher;
class GameSearchTests {
    static int checks;
    static void FocusResults(Launcher launcher) {
        for(int i=0;i<6 && !launcher.SearchGamesFocused;i++) {launcher.HandleGameSearchController(new PadState(),1000+i*2); launcher.HandleGameSearchController(new PadState {Buttons=1},1001+i*2);}
        Check(launcher.SearchGamesFocused,"Up above the first keyboard row focuses games");
    }
    static void Check(bool value,string message) {if(!value) throw new Exception(message); checks++;}
    static IEnumerable<DependencyObject> Elements(DependencyObject node) {
        yield return node;
        var tab=node as TabItem;
        if(tab!=null && tab.Content is DependencyObject) {foreach(var child in Elements((DependencyObject)tab.Content)) yield return child; yield break;}
        foreach(var item in LogicalTreeHelper.GetChildren(node)) {var child=item as DependencyObject; if(child!=null) foreach(var element in Elements(child)) yield return element;}
    }
    static T Find<T>(DependencyObject root,string name) where T:FrameworkElement {return Elements(root).OfType<T>().First(item => item.Name==name);}
    [STAThread] static void Main(string[] args) {
        string previous=Storage.Root,root=Path.Combine(Path.GetTempPath(),"GameSearchTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher launcher=null; Window settingsWindow=null;
        try {
            var library=new Library {Games=new List<Game> {
                new Game {Id="pc1",Name="Crash™ Team Racing Nitro-Fueled",Source="steam",LaunchId="1"},
                new Game {Id="ps1",Name="ISS Pro Evolution",Source="emulator",EmulatorId="duckstation",LaunchId="missing.cue"},
                new Game {Id="hidden",Name="Hidden game",Source="steam",LaunchId="3"},
                new Game {Id="off",Name="Disabled-list game",Source="steam",LaunchId="4"}
            },Collections=new List<Collection> {
                new Collection {Id="pc",Name="PC",Games=new List<string>{"pc1"}},
                new Collection {Id="psx",Name="PSX",Games=new List<string>{"ps1","hidden"}},
                new Collection {Id="off-list",Name="Disabled",Games=new List<string>{"off"}}
            }};
            var settings=new Settings {EnabledCollectionIds=new List<string>{"pc","psx"},HiddenGameIds=new List<string>{"hidden"},CollectionId="pc",JumpListButton="left",JumpListId="pc",ListArtworkSources=new Dictionary<string,string>{{"pc","sgdb"}}};
            Storage.Write("settings.json",settings); Storage.Write("library.json",library);
            Check(Launcher.SearchMatches(library.Games,"team racing").Single().Id=="pc1","Search normalises trademark signs and punctuation");
            Check(Launcher.SearchMatches(library.Games,"evolution ISS").Single().Id=="ps1","Search matches multiple words in either order");
            Check(Launcher.SearchMatches(library.Games,"does not exist").Count==0,"Search reports no matches safely");
            launcher=new Launcher(false,true); launcher.ShowOverlay();
            launcher.ApplyControllerState(new PadState(),"test",0); launcher.ApplyControllerState(new PadState {Buttons=0x8000},"test",10);
            Check(launcher.GameSearchOpen,"Triangle opens game search without replacing L3");
            var binding=BindingFlags.Instance|BindingFlags.NonPublic;
            var search=(Window)typeof(Launcher).GetField("gameSearchWindow",binding).GetValue(launcher);
            Func<List<Game>> shown=() => (List<Game>)typeof(Launcher).GetField("visible",binding).GetValue(launcher);
            Check(!Elements(search).OfType<TextBox>().Any() && !Elements(search).OfType<StackPanel>().Any(panel => panel.Name=="GameSearchResults"),"Keyboard overlay contains no text field or separate result list");
            Check(shown().Count==2,"Search excludes hidden games and games outside enabled lists");
            search.UpdateLayout(); var keyboardFrame=Find<Border>(search,"SearchKeyboard");
            Check(keyboardFrame.TranslatePoint(new Point(),(UIElement)search.Content).Y>launcher.ActualHeight/2,"Keyboard stays at the bottom and leaves the game panels visible");
            Check(Elements(search).OfType<Button>().Where(button => button.Name.StartsWith("SearchKey_") && Convert.ToString(button.Content).Length==1).All(button => !Convert.ToString(button.Content).Any(Char.IsUpper)),"Keyboard keys use lowercase text");
            if(args.Length>0) {
                search.UpdateLayout(); var size=new Size(launcher.ActualWidth,launcher.ActualHeight);
                var drawing=new System.Windows.Media.DrawingVisual(); using(var context=drawing.RenderOpen()) {
                    context.DrawRectangle(new System.Windows.Media.VisualBrush((System.Windows.Media.Visual)launcher.Content) {ViewboxUnits=System.Windows.Media.BrushMappingMode.Absolute,Viewbox=new Rect(size)},null,new Rect(size));
                    context.DrawRectangle(new System.Windows.Media.VisualBrush((System.Windows.Media.Visual)search.Content) {ViewboxUnits=System.Windows.Media.BrushMappingMode.Absolute,Viewbox=new Rect(size)},null,new Rect(size));
                }
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)size.Width,(int)size.Height,96,96,System.Windows.Media.PixelFormats.Pbgra32); bitmap.Render(drawing);
                var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap)); using(var stream=File.Create(args[0])) encoder.Save(stream);
            }
            launcher.HandleGameSearchController(new PadState(),20);
            launcher.HandleGameSearchController(new PadState {Buttons=0x1000},30);
            launcher.HandleGameSearchController(new PadState {Buttons=0x1000},40);
            Check(launcher.GameSearchText=="q","The gamepad keyboard types once per confirmation press");
            launcher.HandleGameSearchController(new PadState(),50); launcher.HandleGameSearchController(new PadState {Buttons=8},60);
            launcher.HandleGameSearchController(new PadState(),70); launcher.HandleGameSearchController(new PadState {Buttons=0x1000},80);
            Check(launcher.GameSearchText=="qw","D-pad navigation selects the next keyboard key");
            launcher.HandleGameSearchController(new PadState(),90); launcher.HandleGameSearchController(new PadState {Buttons=0x4000},100);
            Check(launcher.GameSearchText=="q","Square backspaces the query");
            launcher.HandleGameSearchController(new PadState(),101); launcher.HandleGameSearchController(new PadState {Buttons=0x8000},102); launcher.HandleGameSearchController(new PadState {Buttons=0x8000},103);
            Check(launcher.GameSearchText=="q " && launcher.GameSearchOpen,"Triangle inserts one space without closing the keyboard");
            Check(((TextBlock)typeof(Launcher).GetField("collectionText",binding).GetValue(launcher)).Text=="Search: q ","Search heading preserves normal spaces");
            launcher.SetGameSearchQuery("  ");
            Check(((TextBlock)typeof(Launcher).GetField("collectionText",binding).GetValue(launcher)).Text=="Search:   " && launcher.GameSearchText=="  ","Whitespace-only queries keep normal spaces");
            launcher.SetGameSearchQuery("q ");
            launcher.SetGameSearchQuery(""); launcher.HandleGameSearchController(new PadState(),104); launcher.HandleGameSearchController(new PadState {Buttons=2},105);
            launcher.HandleGameSearchController(new PadState(),106); launcher.HandleGameSearchController(new PadState {Buttons=0x1000},107);
            Check(launcher.GameSearchText=="s","Down chooses the nearest key on the home row");
            launcher.HandleGameSearchController(new PadState(),108); launcher.HandleGameSearchController(new PadState {Buttons=2},109);
            launcher.HandleGameSearchController(new PadState(),110); launcher.HandleGameSearchController(new PadState {Buttons=0x1000},111);
            Check(launcher.GameSearchText=="sx","Down chooses the nearest key on the staggered bottom row");
            search.UpdateLayout(); var qKey=Find<Button>(search,"SearchKey_1_0"); var wKey=Find<Button>(search,"SearchKey_1_1"); var aKey=Find<Button>(search,"SearchKey_2_0"); var zKey=Find<Button>(search,"SearchKey_3_0");
            Func<Button,double> center=key => key.TranslatePoint(new Point(key.ActualWidth/2,0),(UIElement)search.Content).X;
            double pitch=center(wKey)-center(qKey);
            Check(Math.Abs((center(aKey)-center(qKey))/pitch-.25)<.01 && Math.Abs((center(zKey)-center(qKey))/pitch-.75)<.01,"Compact keyboard uses standard QWERTY row offsets");
            launcher.SetGameSearchQuery("");
            var typed=new System.Windows.Input.TextComposition(System.Windows.Input.InputManager.Current,search,"iss");
            search.RaiseEvent(new System.Windows.Input.TextCompositionEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,typed) {RoutedEvent=System.Windows.Input.TextCompositionManager.PreviewTextInputEvent});
            Check(launcher.GameSearchText=="iss","Physical typing works without a visible text field");
            var beforeAccept=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(launcher);
            Check(beforeAccept.CollectionId=="pc" && beforeAccept.SelectedId=="pc1","Live search does not overwrite the original list selection");
            launcher.SetGameSearchQuery("Hidden"); Check(shown().Count==0,"Hidden games cannot be found by typing their names");
            launcher.SetGameSearchQuery("ISS");
            var caption=Elements((DependencyObject)launcher.Content).OfType<TextBlock>().FirstOrDefault(text => text.Name=="SelectedCollection");
            // The carousel uses visual children, so inspect its named fields directly.
            caption=(TextBlock)typeof(Launcher).GetField("collectionText",binding).GetValue(launcher);
            var console=(TextBlock)typeof(Launcher).GetField("searchConsoleLabel",binding).GetValue(launcher);
            Check(caption.Text=="Search: ISS" && console.Text=="PlayStation" && console.TextAlignment==TextAlignment.Center,"Search query appears as the list heading and console is centred below the game title");
            Check(shown().Single().Id=="ps1","Matches are displayed directly in the overlay");
            Check(!Elements(search).OfType<Button>().Any(button => button.Name.StartsWith("SearchKey_4_")) && Elements(search).OfType<Button>().Count(button => button.Name.StartsWith("SearchKey_"))==37,"Search keyboard adds Clear on the last letter row");
            string queryBeforeClear=launcher.GameSearchText;
            var clearKey=Find<Button>(search,"SearchKey_3_7");
            Check((string)clearKey.Content=="Clear","Clear is immediately after m");
            clearKey.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(launcher.GameSearchText=="" && launcher.GameSearchOpen,"Clear resets the query and leaves the keyboard open");
            launcher.SetGameSearchQuery(queryBeforeClear);
            search.Close();
            var restored=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(launcher);
            Check(!launcher.GameSearchOpen && restored.CollectionId=="pc" && restored.SelectedId=="pc1","Closing without accepting restores the original list and selected game");
            Check((string)typeof(Launcher).GetField("lastSearchQuery",binding).GetValue(launcher)=="ISS","Closing search remembers its latest query in memory");
            launcher.OpenGameSearch();
            Check(launcher.GameSearchText=="ISS" && shown().Single().Id=="ps1" && !launcher.SearchGamesFocused,"Reopening search restores the latest query and matching games with keyboard focus");
            launcher.HandleGameSearchController(new PadState(),140); launcher.HandleGameSearchController(new PadState {Buttons=0x2000},141);
            Check(!launcher.GameSearchOpen,"Circle exits directly from the search keyboard");
            launcher.OpenGameSearch();
            launcher.HandleGameSearchController(new PadState(),144); launcher.HandleGameSearchController(new PadState {RT=255},145);
            Check(!launcher.SearchGamesFocused,"R2 does not focus games from the keyboard");
            FocusResults(launcher);
            launcher.HandleGameSearchController(new PadState(),146); launcher.HandleGameSearchController(new PadState {RT=255},147);
            Check(!launcher.SearchGamesFocused && launcher.GameSearchOpen,"R2 returns from results to the keyboard");
            launcher.SetGameSearchQuery(""); ((Window)typeof(Launcher).GetField("gameSearchWindow",binding).GetValue(launcher)).Close();
            launcher.OpenGameSearch(); Check(launcher.GameSearchText=="" && shown().Count==2,"Clearing the latest query is remembered too");
            launcher.SetGameSearchQuery("ISS"); ((Window)typeof(Launcher).GetField("gameSearchWindow",binding).GetValue(launcher)).Close();
            typeof(Launcher).GetMethod("Save",binding).Invoke(launcher,null);
            Check(!File.ReadAllText(Path.Combine(root,"settings.json")).Contains("LastSearchQuery"),"Latest query is not persisted in settings");
            var restarted=new Launcher(false,true);
            try {restarted.OpenGameSearch(); Check(restarted.GameSearchText=="","A new app session starts with an empty search");}
            finally {restarted.ExitApp();}
            launcher.ApplyControllerState(new PadState(),"test",120); launcher.ApplyControllerState(new PadState {Buttons=0x8000},"test",130);
            launcher.SetGameSearchQuery("ISS"); FocusResults(launcher);
            Check(launcher.SearchGamesFocused && !((Window)typeof(Launcher).GetField("gameSearchWindow",binding).GetValue(launcher)).IsVisible,"Up hides the keyboard and focuses filtered overlay games");
            Check(shown().Single().Id=="ps1","Finished search keeps the matching game panels available for navigation");
            launcher.HandleGameSearchController(new PadState(),151); launcher.HandleGameSearchController(new PadState {Buttons=2},152);
            Check(!launcher.SearchGamesFocused && launcher.GameSearchText=="ISS" && ((Window)typeof(Launcher).GetField("gameSearchWindow",binding).GetValue(launcher)).IsVisible,"Down returns focus to the keyboard without losing the query");
            var returnedKeyboard=(Window)typeof(Launcher).GetField("gameSearchWindow",binding).GetValue(launcher);
            var topKey=Elements(returnedKeyboard).OfType<Button>().First(button => button.Name.StartsWith("SearchKey_0_") && ((System.Windows.Media.SolidColorBrush)button.Background).Color==System.Windows.Media.Color.FromRgb(163,240,205));
            launcher.HandleGameSearchController(new PadState {Buttons=2},600);
            Check(((System.Windows.Media.SolidColorBrush)topKey.Background).Color==System.Windows.Media.Color.FromRgb(163,240,205),"Holding Down across the results boundary keeps focus on the first keyboard row");
            FocusResults(launcher);
            launcher.HandleGameSearchController(new PadState(),155); launcher.HandleGameSearchController(new PadState {Buttons=0x2000},156);
            var current=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(launcher);
            Check(!launcher.GameSearchOpen && current.CollectionId=="pc" && current.SelectedId=="pc1","Circle cancels focused results and restores the normal overlay");
            Check(((FrameworkElement)typeof(Launcher).GetField("notice",binding).GetValue(launcher)).Visibility==Visibility.Collapsed && !((System.Windows.Threading.DispatcherTimer)typeof(Launcher).GetField("noticeTimer",binding).GetValue(launcher)).IsEnabled,"Closing search immediately clears its control notice and timeout");
            launcher.ApplyControllerState(new PadState(),"test",157); launcher.ApplyControllerState(new PadState {Buttons=0x8000},"test",158);
            launcher.SetGameSearchQuery("ISS"); FocusResults(launcher);
            typeof(Launcher).GetField("demo",binding).SetValue(launcher,true);
            launcher.HandleGameSearchController(new PadState(),161); launcher.HandleGameSearchController(new PadState {Buttons=0x1000},162);
            Check(!launcher.GameSearchOpen && current.CollectionId=="psx" && current.SelectedId=="ps1" && ((TextBlock)typeof(Launcher).GetField("noticeText",binding).GetValue(launcher)).Text.Contains("Preview mode"),"Cross plays the selected search result through the normal launch action");
            typeof(Launcher).GetField("demo",binding).SetValue(launcher,false);
            launcher.ApplyControllerState(new PadState {Buttons=0x1000},"test",150);
            Check(!(bool)typeof(Launcher).GetField("launching",binding).GetValue(launcher),"Holding confirm after closing search cannot accidentally launch a game");
            launcher.ApplyControllerState(new PadState(),"test",160); launcher.ApplyControllerState(new PadState {Buttons=0x0040},"test",170);
            Check(current.CollectionId=="pc" && !launcher.GameSearchOpen,"L3 retains its configured list jump");
            launcher.ApplyControllerState(new PadState(),"test",180);
            typeof(Launcher).GetField("demo",binding).SetValue(launcher,true);
            launcher.ApplyControllerState(new PadState {Buttons=0x0020},"test",190);
            Check(((TextBlock)typeof(Launcher).GetField("noticeText",binding).GetValue(launcher)).Text.Contains("Preview library"),"Select/Create triggers refresh");
            typeof(Launcher).GetField("demo",binding).SetValue(launcher,false);
            Check(Prompts.Help(settings).Contains("Search") && Prompts.Help(settings).Contains("Select / Create"),"Control hints describe the new search and refresh mapping");
            foreach(bool focusedResults in new[]{false,true}) {
                launcher.OpenGameSearch(); launcher.SetGameSearchQuery("ISS");
                Check(((TextBlock)typeof(Launcher).GetField("collectionText",binding).GetValue(launcher)).Text=="Search: ISS","Search heading includes its label and query");
                string originalLayout=(string)typeof(Launcher).GetProperty("ActiveLayout",binding).GetValue(launcher,null);
                launcher.HandleGameSearchController(new PadState(),197);
                launcher.HandleGameSearchController(new PadState {LT=255},198);
                Check(launcher.GameSearchOpen && launcher.GameSearchText=="ISS" && (string)typeof(Launcher).GetProperty("ActiveLayout",binding).GetValue(launcher,null)!=originalLayout,"L2 changes search layout without closing search or changing its query");
                launcher.HandleGameSearchController(new PadState(),198); launcher.HandleGameSearchController(new PadState {LT=255},199); launcher.HandleGameSearchController(new PadState(),199);
                if(focusedResults) FocusResults(launcher);
                launcher.HandleGameSearchController(new PadState(),202); launcher.HandleGameSearchController(new PadState {Buttons=0x0080},203);
                current=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(launcher);
                Check(current.FavouriteGameIds.Contains("ps1") && launcher.GameSearchOpen && launcher.SearchGamesFocused==focusedResults && launcher.GameSearchText=="ISS","R3 favourites the selected search result without changing keyboard/results focus or query");
                launcher.HandleGameSearchController(new PadState {Buttons=0x0080},204);
                Check(current.FavouriteGameIds.Contains("ps1"),"Holding R3 does not repeatedly toggle favourites");
                launcher.HandleGameSearchController(new PadState(),205); launcher.HandleGameSearchController(new PadState {Buttons=0x0080},206);
                launcher.HandleGameSearchController(new PadState(),206);
                current=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(launcher);
                Check(!current.FavouriteGameIds.Contains("ps1"),"Pressing R3 again removes the search result from favourites");
                string destination=focusedResults ? "pc" : "psx"; current.JumpListId=destination;
                launcher.HandleGameSearchController(new PadState(),207); launcher.HandleGameSearchController(new PadState {Buttons=0x0040},208);
                Check(!launcher.GameSearchOpen && current.CollectionId==destination,"L3 cancels search and jumps to its assigned list from both focus modes");
            }
            launcher.OpenGameSearch(); launcher.SetGameSearchQuery("zzzznoresults");
            launcher.HandleGameSearchController(new PadState(),209); launcher.HandleGameSearchController(new PadState {Buttons=0x0080},210);
            Check(launcher.GameSearchOpen && launcher.GameSearchText=="zzzznoresults" && current.FavouriteGameIds.Count==0,"R3 safely ignores searches with no results");
            ((Window)typeof(Launcher).GetField("gameSearchWindow",binding).GetValue(launcher)).Close();
            launcher.OpenGameSearch(); launcher.SetGameSearchQuery("zzzznoresults");
            for(int i=0;i<4;i++) {launcher.HandleGameSearchController(new PadState(),220+i*2); launcher.HandleGameSearchController(new PadState {Buttons=1},221+i*2);}
            Check(launcher.GameSearchOpen && !launcher.SearchGamesFocused,"No matches keeps keyboard reachable when navigating Up");
            launcher.HandleGameSearchController(new PadState(),230); launcher.HandleGameSearchController(new PadState {Buttons=0x2000},231);
            Check(!launcher.GameSearchOpen,"Circle exits from the keyboard");
            launcher.OpenGameSearch(); launcher.SetGameSearchQuery("ISS"); FocusResults(launcher);
            launcher.HandleGameSearchController(new PadState(),232); launcher.HandleGameSearchController(new PadState {Buttons=0x8000},233);
            Check(launcher.GameSearchOpen && launcher.SearchGamesFocused && launcher.GameSearchText=="ISS","Triangle is unassigned in search results");
            launcher.HandleGameSearchController(new PadState(),234); launcher.HandleGameSearchController(new PadState {RT=255},235);
            Check(!launcher.SearchGamesFocused && launcher.GameSearchText=="ISS","R2 returns to the keyboard with the query intact");
            ((Window)typeof(Launcher).GetField("gameSearchWindow",binding).GetValue(launcher)).Close();
            settingsWindow=launcher.CreateHelpWindow(); settingsWindow.Show();
            Check(!Elements(settingsWindow).OfType<Expander>().Any(item => item.Name=="LibretroListsSection"),"The redundant Libretro lists checklist is removed");
            Find<TabControl>(settingsWindow,"HelpSettingsTabs").SelectedIndex=6;
            Find<ComboBox>(settingsWindow,"ListArtworkPicker").SelectedValue="psx";
            Check(!Elements(settingsWindow).OfType<CheckBox>().Any(box => box.Name=="LibretroArtworkSetting"),"System no longer exposes a global Libretro checkbox");
            var inheritedArtwork=Find<TextBlock>(settingsWindow,"ListArtworkEffectiveSource");
            Check(inheritedArtwork.Visibility==Visibility.Visible && inheritedArtwork.Text.Contains("DuckStation") && inheritedArtwork.Text.Contains("Prefer Libretro"),"Automatic artwork explains the inherited emulator source");
            Find<ComboBox>(settingsWindow,"ListArtworkSourceSetting").SelectedValue="libretro";
            Check(inheritedArtwork.Visibility==Visibility.Collapsed,"Explicit artwork source hides the inherited-source note");
            Find<Expander>(settingsWindow,"PerListArtworkSection").IsExpanded=true;
            settingsWindow.UpdateLayout();
            var previewRoot=(FrameworkElement)settingsWindow.Content;
            var previewBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(previewRoot.ActualWidth)+60,(int)Math.Ceiling(previewRoot.ActualHeight)+60,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            previewBitmap.Render(previewRoot); var previewPng=new System.Windows.Media.Imaging.PngBitmapEncoder(); previewPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(previewBitmap));
            using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Settings-artwork-preview.png"))) previewPng.Save(output);

            Find<Button>(settingsWindow,"SaveListArtwork").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var saved=Storage.Read<Settings>("settings.json");
            Check(saved.ListArtworkSources["psx"]=="libretro" && saved.ListArtworkSources["pc"]=="sgdb","Per-list artwork controls set Libretro without changing other explicit sources");
            Check(saved.EnabledCollectionIds.SequenceEqual(settings.EnabledCollectionIds),"Artwork list selection does not enable or disable overlay lists");
            Find<ComboBox>(settingsWindow,"ListArtworkSourceSetting").SelectedValue="auto";
            Check(Find<TextBlock>(settingsWindow,"ListArtworkEffectiveSource").Text.Contains("DuckStation"),"Returning to Automatic refreshes its effective source description");
            Find<Button>(settingsWindow,"SaveListArtwork").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            saved=Storage.Read<Settings>("settings.json");
            Check(!saved.ListArtworkSources.ContainsKey("psx") && saved.ListArtworkSources["pc"]=="sgdb","Unchecking restores Automatic while keeping other source preferences");
            settingsWindow.Close(); settingsWindow=null;
            launcher.OpenGameSearch(); launcher.SetGameSearchQuery("ISS");
            launcher.Close();
            Check((bool)typeof(Launcher).GetField("suppressSearchRelease",binding).GetValue(launcher),"Closing search guards against held actions");
            launcher.ShowOverlay();
            Check(!(bool)typeof(Launcher).GetField("suppressSearchRelease",binding).GetValue(launcher),"Reopening the overlay clears stale search release suppression");
            current=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(launcher);
            string firstId=shown()[(int)typeof(Launcher).GetField("selected",binding).GetValue(launcher)].Id; bool wasFavourite=(current.FavouriteGameIds ?? new List<string>()).Contains(firstId);
            launcher.ApplyControllerState(new PadState {Buttons=0x80},"reopen-test",5000);
            Check(((Settings)typeof(Launcher).GetField("settings",binding).GetValue(launcher)).FavouriteGameIds.Contains(firstId)!=wasFavourite,"First button press works immediately after reopening without a neutral frame");
            Storage.Write("settings.json",new Settings());
            var demoSearch=new Launcher(true,true);
            try {
                demoSearch.OpenGameSearch();
                demoSearch.HandleGameSearchController(new PadState(),6000); demoSearch.HandleGameSearchController(new PadState {Buttons=0x10},6001);
                Check(!demoSearch.GameSearchOpen,"Start dispatches the selected match directly from the keyboard (demo never launches games)");
                demoSearch.OpenGameSearch(); FocusResults(demoSearch);
                demoSearch.HandleGameSearchController(new PadState(),6010); demoSearch.HandleGameSearchController(new PadState {Buttons=0x10},6011);
                Check(!demoSearch.GameSearchOpen,"Start dispatches the selected match from results (demo never launches games)");
            } finally {demoSearch.ExitApp();}
            Console.WriteLine(checks+" search and artwork-list checks passed.");
        } finally {
            if(settingsWindow!=null) settingsWindow.Close(); if(launcher!=null) launcher.ExitApp();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(() => {}));
            Storage.Root=previous; Directory.Delete(root,true);
        }
    }
}
