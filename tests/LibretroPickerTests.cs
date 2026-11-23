using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PadLauncher;
class LibretroPickerTests {
    static int checks;
    static void Check(bool condition,string description) {if(!condition) throw new Exception(description); checks++;}
    static IEnumerable<DependencyObject> Children(DependencyObject node) {
        yield return node;
        var tab=node as TabItem;
        if(tab!=null && tab.Content is DependencyObject) {foreach(var entry in Children((DependencyObject)tab.Content)) yield return entry; yield break;}
        foreach(object item in LogicalTreeHelper.GetChildren(node)) {var child=item as DependencyObject; if(child!=null) foreach(var entry in Children(child)) yield return entry;}
    }
    static T Find<T>(Window window,string name) where T:FrameworkElement {return Children(window).OfType<T>().First(item => item.Name==name);}
    static void Wait(Func<bool> condition) {
        var frame=new DispatcherFrame(); var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)}; DateTime deadline=DateTime.UtcNow.AddSeconds(5);
        timer.Tick+=(s,e) => {if(condition() || DateTime.UtcNow>=deadline) frame.Continue=false;}; timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        Check(condition(),"Artwork picker completes its asynchronous UI operation");
    }
    [STAThread] static void Main() {
        System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        string previous=Storage.Root,root=Path.Combine(Path.GetTempPath(),"LibretroPickerTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        var oldFetch=ArtworkService.Fetch; Launcher launcher=null; Window owner=null;
        try {
            byte[] image;
            using(var bitmap=new System.Drawing.Bitmap(256,256)) using(var output=new MemoryStream()) {bitmap.Save(output,System.Drawing.Imaging.ImageFormat.Png); image=output.ToArray();}
            ArtworkService.Fetch=(url,key,token) => {
                Check(key==null,"Manual cover requests are unauthenticated");
                if(url=="https://thumbnails.libretro.com/") return System.Text.Encoding.UTF8.GetBytes("<a href=\"Sony%20-%20PlayStation/\">PlayStation</a>");
                if(url.EndsWith("/")) return System.Text.Encoding.UTF8.GetBytes("<a href=\"Game%20One%20%28USA%29.png\">Game One</a>");
                return image;
            };
            var game=new Game {Id="1",Name="Game One",Source="steam",LaunchId="1"};
            Storage.Write("settings.json",new Settings {SteamGridDbEnabled=false,LibretroArtworkEnabled=false,CustomGames=new List<CustomGame>{new CustomGame {Id="custom:test",Name="Test game",Target="test.exe"}}});
            Storage.Write("library.json",new Library {Games=new List<Game>{game}});
            launcher=new Launcher(false,true); owner=launcher.CreateHelpWindow(); owner.Show();
            Check(Children(owner).OfType<Button>().Any(button => button.Name=="ArtworkChooseLibretroImage"),"Artwork tab offers manual Libretro selection");
            var settingsTabs=Find<TabControl>(owner,"HelpSettingsTabs");
            var artworkTab=settingsTabs.Items.Cast<TabItem>().Single(tab => Convert.ToString(tab.Header)=="Artwork");
            Check(Children(artworkTab).OfType<ComboBox>().Any(control => control.Name=="ArtworkGamePicker") && !Children(artworkTab).OfType<Expander>().Any(control => control.Name=="LibretroListsSection"),"Artwork tab keeps the game artwork picker without the redundant list checklist");
            var artworkGamePicker=Children(artworkTab).OfType<ComboBox>().First(control => control.Name=="ArtworkGamePicker");
            artworkGamePicker.SelectedIndex=0;
            Check(artworkGamePicker.SelectedValue is string && artworkGamePicker.Text.Contains(" · ") && !artworkGamePicker.Text.Contains("PadLauncher.Game"),"Closed artwork dropdown shows the game and source label rather than its object type");
            Check(!Children(owner).OfType<Button>().Any(button => button.Name=="OpenGameSearch"),"Controls has no redundant Search games button");
            Check(!Children(owner).OfType<Button>().Any(button => button.Name=="NewCustomChooseLibretroImage" || button.Name=="CustomChooseLibretroImage"),"Custom games no longer duplicates artwork controls");
            var gap=Find<Slider>(owner,"ArtworkGapSetting");
            Check(gap.Value==16 && gap.Minimum==0 && gap.Maximum==64,"Overlay exposes a bounded gap slider with the existing default");
            gap.Value=32; Check(Storage.Read<Settings>("settings.json").ArtworkGap==32,"Changing the gap slider saves immediately");
            bool invalidGap=false; try {launcher.SetArtworkGap(65);} catch(ArgumentOutOfRangeException) {invalidGap=true;}
            Check(invalidGap && Storage.Read<Settings>("settings.json").ArtworkGap==32,"Invalid spacing cannot overwrite the saved gap");
            var listPicker=Find<ComboBox>(owner,"ListLayoutPicker"); listPicker.SelectedValue="all"; string listId=listPicker.SelectedValue as string;
            Find<ComboBox>(owner,"ListArtworkPicker").SelectedValue="all";
            Check(!Children(owner).OfType<Button>().Any(button => button.Name=="RedownloadListArtwork"),"Per-list artwork settings no longer duplicate redownload actions");
            var listSource=Find<ComboBox>(owner,"ListArtworkSourceSetting");
            Check(listSource.IsEnabled && listSource.Items.Count==5,"Artwork source remains editable when the list inherits its layout");
            listSource.SelectedValue="libretro"; Find<Button>(owner,"SaveListArtwork").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var sourceSettings=Storage.Read<Settings>("settings.json");
            Check(sourceSettings.ListArtworkSources[listId]=="libretro" && !sourceSettings.ListLayouts.ContainsKey(listId),"A source preference saves independently of inherited layout");
            var modernSizeControl=Find<Slider>(owner,"ModernPanelSizeSetting"); var retroSizeControl=Find<Slider>(owner,"RetroPanelSizeSetting"); var sizeLockControl=Find<CheckBox>(owner,"LockPanelSizesSetting");
            Check(sizeLockControl.IsChecked==true,"Panel sizes start locked");
            modernSizeControl.Value=.9; Check(retroSizeControl.Value==.9,"Locked Modern size updates Retro size");
            retroSizeControl.Value=1.05; Check(Math.Abs(modernSizeControl.Value-1.05)<.001,"Locked Retro size updates Modern size");
            sizeLockControl.IsChecked=false; sizeLockControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); retroSizeControl.Value=.8;
            Check(Math.Abs(modernSizeControl.Value-1.05)<.001 && Storage.Read<Settings>("settings.json").RetroPanelScale==.8,"Unlocked Retro sizing preserves Modern size");
            sizeLockControl.IsChecked=true; sizeLockControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); modernSizeControl.Value=1;
            var inheritControl=Find<CheckBox>(owner,"ListUseGlobalSettings"); inheritControl.IsChecked=false; inheritControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var listModernSize=Find<Slider>(owner,"ListModernPanelSizeSetting"); var listRetroSize=Find<Slider>(owner,"ListRetroPanelSizeSetting"); var listSizeLock=Find<CheckBox>(owner,"ListLockPanelSizesSetting");
            listModernSize.Value=1.1; Check(Math.Abs(listRetroSize.Value-1.1)<.001,"Per-list size controls stay linked when locked");
            listSizeLock.IsChecked=false; listSizeLock.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); listRetroSize.Value=.8;
            Find<Button>(owner,"SaveListLayout").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var profile=Storage.Read<Settings>("settings.json").ListLayouts[listId];
            Check(profile.PanelScale==1.1 && profile.RetroPanelScale==.8 && profile.PanelSizesLocked==false,"Per-list Modern, Retro and lock preferences save independently");
            inheritControl=Find<CheckBox>(owner,"ListUseGlobalSettings"); inheritControl.IsChecked=true; inheritControl.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Find<Button>(owner,"SaveListLayout").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            launcher.SetCarouselLayout("grid");
            var content=(FrameworkElement)launcher.Content; content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            var cardMap=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(launcher);
            var enlarged=cardMap.Values.Single(panel => ((System.Windows.Media.ScaleTransform)((System.Windows.Media.TransformGroup)panel.RenderTransform).Children[0]).ScaleX>1);
            Check(Math.Abs(((System.Windows.Media.ScaleTransform)((System.Windows.Media.TransformGroup)enlarged.RenderTransform).Children[0]).ScaleX-1.12)<.001,"Outline-free Grid enlarges its selected panel");
            Check(cardMap.Values.Where(panel => panel!=enlarged).All(panel => Panel.GetZIndex(panel)<Panel.GetZIndex(enlarged)),"Selected Grid artwork stays above neighbouring panels");
            typeof(Launcher).GetMethod("Move",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(launcher,new object[]{1});
            Check(((System.Windows.Media.ScaleTransform)((System.Windows.Media.TransformGroup)enlarged.RenderTransform).Children[0]).ScaleX==1 && cardMap.Values.Count(panel => ((System.Windows.Media.ScaleTransform)((System.Windows.Media.TransformGroup)panel.RenderTransform).Children[0]).ScaleX>1)==1,"Grid selection enlargement follows navigation and restores the previous panel");
            var outlines=Find<CheckBox>(owner,"ShowPanelOutlinesSetting"); outlines.IsChecked=true; outlines.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(cardMap.Values.All(panel => ((System.Windows.Media.ScaleTransform)((System.Windows.Media.TransformGroup)panel.RenderTransform).Children[0]).ScaleX==1),"Enabling outlines restores normal Grid panel scale");
            var open=typeof(Launcher).GetMethod("ShowLibretroArtworkPicker",BindingFlags.NonPublic|BindingFlags.Instance);
            open.Invoke(launcher,new object[]{owner,game,null});
            var chooser=owner.OwnedWindows.Cast<Window>().Single(window => window.Name=="LibretroArtworkPicker");
            Wait(() => Find<ComboBox>(chooser,"LibretroConsolePicker").IsEnabled);
            Find<ComboBox>(chooser,"LibretroConsolePicker").SelectedItem="Sony - PlayStation";
            Wait(() => Find<Button>(chooser,"LibretroUseImage").IsEnabled);
            Check(Find<Image>(chooser,"LibretroCoverPreview").Source!=null,"Selected cover is previewed before applying");
            var coverPicker=Find<ComboBox>(chooser,"LibretroCoverPicker");
            coverPicker.ApplyTemplate();
            var selectionPresenter=(ContentPresenter)coverPicker.Template.FindName("SelectionPresenter",coverPicker);
            Check(selectionPresenter!=null && selectionPresenter.Content==coverPicker.SelectedItem,"The closed dropdown displays the actually selected cover");
            Find<Button>(chooser,"LibretroUseImage").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Wait(() => !chooser.IsVisible);
            var saved=Storage.Read<Settings>("settings.json");
            Check(!saved.LibretroArtworkEnabled && LibretroArtwork.IsChosenPath(saved.ArtworkOverrides["1"]) && File.Exists(Path.Combine(root,saved.ArtworkOverrides["1"])),"Using a cover saves an explicit override while keeping automatic downloads disabled");
            var stateBinding=BindingFlags.NonPublic|BindingFlags.Instance;
            var baseGame=((Library)typeof(Launcher).GetField("library",stateBinding).GetValue(launcher)).Games.First(entry => entry.Id=="1");
            byte[] sgdbImage;
            using(var portrait=new System.Drawing.Bitmap(300,450)) using(var output=new MemoryStream()) {portrait.Save(output,System.Drawing.Imaging.ImageFormat.Png); sgdbImage=output.ToArray();}
            ArtworkKey.Save("test-key");
            ArtworkService.Fetch=(url,key,token) => url.StartsWith("https://www.steamgriddb.com/") ? System.Text.Encoding.UTF8.GetBytes("{\"success\":true,\"data\":[{\"url\":\"https://cdn2.steamgriddb.com/grid/replacement.png\",\"thumb\":\"https://cdn2.steamgriddb.com/grid/replacement.png\"}]}") : sgdbImage;
            typeof(Launcher).GetMethod("ShowArtworkPicker",stateBinding).Invoke(launcher,new object[]{owner,baseGame,null});
            var sgdbPicker=owner.OwnedWindows.Cast<Window>().Single(window => window.Title.StartsWith("Artwork"));
            Wait(() => Children(sgdbPicker).OfType<Button>().Any(button => button.Content is Image));
            Children(sgdbPicker).OfType<Button>().First(button => button.Content is Image).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Wait(() => !sgdbPicker.IsVisible);
            var displayed=((List<Game>)typeof(Launcher).GetField("visible",stateBinding).GetValue(launcher)).First(entry => entry.Id=="1");
            Check(displayed.ArtworkProvider=="chosen" && !LibretroArtwork.IsChosenPath(displayed.Art) && displayed.Art==baseGame.Art,"Choosing SGDB after Libretro immediately replaces the current list's displayed artwork copy");
            string preferred=LibretroArtwork.CachePath(baseGame); Directory.CreateDirectory(Path.GetDirectoryName(preferred)); File.WriteAllBytes(preferred,image);
            typeof(Launcher).GetField("demo",stateBinding).SetValue(launcher,true);
            typeof(Launcher).GetMethod("ResetGameArtwork",stateBinding).Invoke(launcher,new object[]{baseGame});
            typeof(Launcher).GetField("demo",stateBinding).SetValue(launcher,false);
            displayed=((List<Game>)typeof(Launcher).GetField("visible",stateBinding).GetValue(launcher)).First(entry => entry.Id=="1");
            Check(displayed.Art==preferred && displayed.ArtworkProvider=="libretro" && !Storage.Read<Settings>("settings.json").ArtworkOverrides.ContainsKey("1"),"Reset clears stale manual artwork and immediately restores this list's preferred source");
            string before=File.ReadAllText(Path.Combine(root,"settings.json")); bool chosen=false;
            open.Invoke(launcher,new object[]{owner,new Game {Id="custom:new",Name="Game One",Source="custom"},(Action<string>)(path => chosen=true)});
            chooser=owner.OwnedWindows.Cast<Window>().Single(window => window.Name=="LibretroArtworkPicker");
            chooser.Close(); Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(() => {}));
            Check(!chosen && File.ReadAllText(Path.Combine(root,"settings.json"))==before,"Closing the custom-game picker does not modify forms or stored artwork");
            var currentSettings=(Settings)typeof(Launcher).GetField("settings",stateBinding).GetValue(launcher);
            currentSettings.ListArtworkSources=new Dictionary<string,string>(); currentSettings.EmulatorArtworkSources=new Dictionary<string,string>{{"retroarch","libretro"}}; currentSettings.RetroArchPreferLibretro=true;
            var retroGame=new Game {Id="missing-retro",Name="Game One",Source="emulator",EmulatorId="retroarch",ArtworkSystem="Sony - PlayStation"};
            Check(launcher.PreferredArtworkPickerSource(retroGame)=="libretro","Missing RetroArch artwork inherits the shared Libretro preference");
            currentSettings.ListArtworkSources["all"]="sgdb";
            Check(launcher.PreferredArtworkPickerSource(retroGame)=="sgdb","The active list's explicit SGDB preference takes priority");
            currentSettings.ListArtworkSources["all"]="libretro";
            Check(launcher.PreferredArtworkPickerSource(retroGame)=="libretro","The active list's Libretro preference is honoured");
            currentSettings.ListArtworkSources["all"]="libretro-only";
            typeof(Launcher).GetMethod("ShowPreferredArtworkPicker",stateBinding).Invoke(launcher,new object[]{owner,retroGame});
            chooser=owner.OwnedWindows.Cast<Window>().Single(window => window.Name=="LibretroArtworkPicker");
            Check(chooser.IsVisible,"Only Libretro opens the correct manual artwork picker"); chooser.Close();
            currentSettings.ListArtworkSources["all"]="libretro";
            var request=(Action<string>)(path => chosen=true);
            open.Invoke(launcher,new object[]{owner,retroGame,request});
            chooser=owner.OwnedWindows.Cast<Window>().Single(window => window.Name=="LibretroArtworkPicker");
            Find<Button>(chooser,"ArtworkSwitchToSteamGridDB").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            sgdbPicker=owner.OwnedWindows.Cast<Window>().Single(window => window.Title.StartsWith("Artwork"));
            Check(!chooser.IsVisible && sgdbPicker.IsVisible,"Libretro picker can switch directly to SteamGridDB");
            Find<Button>(sgdbPicker,"ArtworkSwitchToLibretro").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            chooser=owner.OwnedWindows.Cast<Window>().Single(window => window.Name=="LibretroArtworkPicker");
            Check(!sgdbPicker.IsVisible && chooser.IsVisible && !chosen,"Switching back preserves the game without saving an artwork choice");
            chooser.Close();
            ((Library)typeof(Launcher).GetField("library",stateBinding).GetValue(launcher)).Games.Add(retroGame);
            var missingRows=new WrapPanel(); var missingCount=new TextBlock();
            typeof(Launcher).GetMethod("UpdateMissingArtworkList",stateBinding).Invoke(launcher,new object[]{missingCount,missingRows,owner});
            missingRows.Children.OfType<Button>().Single(button => Convert.ToString(button.Content).Contains("RetroArch")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            chooser=owner.OwnedWindows.Cast<Window>().Single(window => window.Name=="LibretroArtworkPicker");
            Check(chooser.IsVisible,"Clicking a missing RetroArch artwork entry opens Libretro when preferred"); chooser.Close();
            Console.WriteLine(checks+" Libretro artwork picker checks passed.");
        } finally {
            if(owner!=null) owner.Close(); if(launcher!=null) launcher.ExitApp();
            Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(() => {}));
            ArtworkService.Fetch=oldFetch; Storage.Root=previous; Directory.Delete(root,true);
        }
    }
}
