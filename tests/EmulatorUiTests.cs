using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using PadLauncher;
class EmulatorUiTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    static FrameworkElement Find(DependencyObject root,string name) {
        var element=root as FrameworkElement; if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var child=Find(VisualTreeHelper.GetChild(root,i),name); if(child!=null) return child;} return null;
    }
    static void Layout(FrameworkElement root) {root.Measure(new Size(780,680)); root.Arrange(new Rect(0,0,780,680)); root.UpdateLayout();}
    static System.Collections.Generic.IEnumerable<DependencyObject> ImportChildren(DependencyObject root) {
        yield return root;
        foreach(var item in LogicalTreeHelper.GetChildren(root)) {var child=item as DependencyObject; if(child!=null) foreach(var next in ImportChildren(child)) yield return next;}
    }
    static void CheckAddedFilter(FrameworkElement content,string buttonName,int expectedAdded) {
        var button=(Button)Find(content,buttonName);
        var boxes=ImportChildren(content).OfType<CheckBox>().Where(box => box.Content is StackPanel).ToList();
        var added=boxes.Where(box => ((StackPanel)box.Content).Children.OfType<TextBlock>().Any(text => text.Text.Contains("already added"))).ToList();
        var selected=boxes.Select(box => box.IsChecked).ToArray();
        Check(button!=null && button.Visibility==Visibility.Visible && added.Count==expectedAdded,"Import review shows its filter only after game rows are available");
        var note=(TextBlock)Find(content,"ImportNoDuplicatesNote");
        Check(note!=null && note.Text.Contains("without creating duplicates") && ((SolidColorBrush)note.Foreground).Color==Color.FromRgb(255,218,106),"Import review explains duplicate prevention in yellow");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(button.Visibility==Visibility.Visible,"The filter remains available when all already-added rows are hidden");
        Check(added.All(box => box.Visibility==Visibility.Collapsed) && boxes.Except(added).All(box => box.Visibility==Visibility.Visible),"Hide already added collapses existing rows and retains new games");
        Check(boxes.Select(box => box.IsChecked).SequenceEqual(selected),"Hiding existing games preserves their selection and import state");
        button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(boxes.All(box => box.Visibility==Visibility.Visible) && boxes.Select(box => box.IsChecked).SequenceEqual(selected),"Toggling the filter off restores rows without changing selections");
    }
    static void ConfirmNewGames(Window confirmation,string expectedListId=null,string expectedListName=null) {
        var content=(FrameworkElement)confirmation.Content;
        var boxes=ImportChildren(content).OfType<CheckBox>().Where(box => box.Name.StartsWith("EmulatorImportConfirmationGame_",StringComparison.Ordinal)).ToList();
        var count=Find(content,"EmulatorImportConfirmationCount") as TextBlock;
        var total=Find(content,"EmulatorImportConfirmationTotal") as TextBlock;
        var groups=ImportChildren(content).OfType<Expander>().Where(expander => expander.Name.StartsWith("EmulatorImportConfirmationGroup_",StringComparison.Ordinal)).ToList();
        var destinations=ImportChildren(content).OfType<ComboBox>().Where(combo => combo.Name.StartsWith("EmulatorImportConfirmationDestination_",StringComparison.Ordinal)).ToList();
        var names=ImportChildren(content).OfType<TextBox>().Where(field => field.Name.StartsWith("EmulatorImportConfirmationListName_",StringComparison.Ordinal)).ToList();
        Check(boxes.Count>0 && boxes.All(box => box.IsChecked==true),"Confirmation lists newly found games selected by default");
        Check(total!=null && total.Text.Contains(boxes.Count+" new game") && total.Text.EndsWith(" found"),"Confirmation shows the total number of new games found at the top");
        Check(groups.Count>0 && groups.All(group => group.IsExpanded),"Each console or playlist is an expanded dropdown by default");
        Check(groups.Count>0 && destinations.Count==groups.Count && names.Count==groups.Count,"Confirmation groups games by emulator or RetroArch playlist with one destination per group");
        Check(confirmation.Owner!=null && confirmation.Width==confirmation.Owner.Width,"Confirmation matches the width of the previous import window");
        var groupToggles=ImportChildren(content).OfType<Button>().Where(button => button.Name.StartsWith("EmulatorImportConfirmationSelectAll_",StringComparison.Ordinal)).ToList();
        Check(groupToggles.Count==groups.Count,"Every confirmation group has its own select-all toggle");
        string firstSuffix=groups[0].Name.Substring("EmulatorImportConfirmationGroup_".Length);
        var firstToggle=groupToggles.Single(button => button.Name=="EmulatorImportConfirmationSelectAll_"+firstSuffix);
        var firstHeader=groups[0].Header as DependencyObject;
        var firstContent=groups[0].Content as DependencyObject;
        Check(firstHeader!=null && firstContent!=null && !ImportChildren(firstHeader).Contains(firstToggle) && ImportChildren(firstContent).Contains(firstToggle),"Group select-all toggle sits inside the expanded dropdown");
        var firstGroupBoxes=ImportChildren(firstContent).OfType<CheckBox>().Where(box => box.Name.StartsWith("EmulatorImportConfirmationGame_",StringComparison.Ordinal)).ToList();
        Check((string)firstToggle.Content=="Unselect all","Group select-all starts as unselect-all while every game is selected");
        firstToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(firstGroupBoxes.All(box => box.IsChecked!=true) && boxes.Except(firstGroupBoxes).All(box => box.IsChecked==true),"Group select-all clears only that group's games");
        Check((string)firstToggle.Content=="Select all","Group select-all switches to select-all after clearing its games");
        firstToggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(firstGroupBoxes.All(box => box.IsChecked==true) && (string)firstToggle.Content=="Unselect all","Group select-all restores only that group's games");
        Check(destinations.All(combo => ((IEnumerable<KeyValuePair<string,string>>)combo.ItemsSource).Any(option => option.Key=="")),"Each confirmation destination offers a new-list choice");
        if(expectedListId!=null) Check(destinations.All(combo => String.Equals(combo.SelectedValue as string,expectedListId,StringComparison.OrdinalIgnoreCase)),"Each group defaults to the destination chosen in the previous import window");
        if(expectedListName!=null) Check(names.All(field => field.Text==expectedListName),"Each new-list group keeps the list name from the previous import window");
        if(destinations.Count>0 && destinations[0].ItemsSource.Cast<KeyValuePair<string,string>>().Any(option => option.Key=="per-playlist")) Check(groups.Any(group => (group.Header is DependencyObject && ImportChildren((DependencyObject)group.Header).OfType<TextBlock>().Any(text => text.Text.Contains("RetroArch playlist"))) || (group.Content is DependencyObject && ImportChildren((DependencyObject)group.Content).OfType<TextBlock>().Any(text => text.Text.Contains("RetroArch playlist")))),"RetroArch playlist groups are labelled separately");
        Check(count!=null && count.Text.Contains("new games selected"),"Confirmation reports how many new games are selected");
        ((Button)Find(content,"EmulatorImportConfirmationSelectNone")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(boxes.All(box => box.IsChecked!=true),"Confirmation can clear every newly found game");
        ((Button)Find(content,"EmulatorImportConfirmationSelectAll")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(boxes.All(box => box.IsChecked==true),"Confirmation can restore every newly found game");
        var confirmButton=(Button)Find(content,"ConfirmEmulatorImportGames"); var cancelButton=(Button)Find(content,"CancelEmulatorImportConfirmation");
        var buttonRow=(Panel)confirmButton.Parent;
        Check(buttonRow.Children.IndexOf(confirmButton)<buttonRow.Children.IndexOf(cancelButton),"Confirmation places Import selected before Cancel");
        confirmButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }
    static bool RunImport(Window owner,FrameworkElement content,bool update,bool cancel=false,string emulator="retroarch") {
        int stage=0; bool done=false; var started=DateTime.UtcNow;
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e) => {
            var wizard=owner.OwnedWindows.Cast<Window>().FirstOrDefault(w => w.IsVisible && w.Title=="Import "+EmulatorLibrary.Preset(emulator).Name+" library");
            if(wizard==null) return;
            if(DateTime.UtcNow-started>TimeSpan.FromSeconds(5)) {
                foreach(var owned in wizard.OwnedWindows.Cast<Window>().Where(item => item.IsVisible).ToList()) owned.Close();
                wizard.Close(); return;
            }
            var confirmation=wizard.OwnedWindows.Cast<Window>().FirstOrDefault(w => w.IsVisible && w.Title=="Confirm new games");
            if(confirmation!=null) {
                var previousContent=(FrameworkElement)wizard.Content;
                var previousDestination=(ComboBox)Find(previousContent,"EmulatorImportDestination"); var previousName=(TextBox)Find(previousContent,"EmulatorImportListName");
                ConfirmNewGames(confirmation,previousDestination==null ? null : previousDestination.SelectedValue as string,previousName==null ? null : previousName.Text); return;
            }
            var wizardContent=(FrameworkElement)wizard.Content;
            var review=(Button)Find(wizardContent,"ReviewEmulatorImportGames"); var commit=(Button)Find(wizardContent,"CommitEmulatorImport");
            if(stage==0 && review!=null && review.IsEnabled) {
                var importDestination=(ComboBox)Find(wizardContent,"EmulatorImportDestination");
                var automatic=((IEnumerable<KeyValuePair<string,string>>)importDestination.ItemsSource).First(item => item.Key=="source:emulator:"+emulator);
                Check(automatic.Value.Contains(" · ") && automatic.Value.EndsWith(" (automatic list)"),"Automatic import destination includes emulator and console names");
                Check(importDestination.Margin.Top==8,"Destination dropdown has space below its caption");

                Check(ImportChildren(wizardContent).OfType<Button>().Single(button => button.Name=="HideAddedEmulatorImportGames").Visibility==Visibility.Collapsed,"Individual import hides the game filter before reviewing sources");
                if(update) {
                    var saved=(ComboBox)Find(wizardContent,"SavedEmulatorImport"); if(saved==null || saved.Items.Count<2) {wizard.Close(); return;}
                    stage=1; saved.SelectedIndex=1; return;
                }
                stage=2; review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
            }
            if(stage==1 && review!=null && review.IsEnabled) {stage=2; review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;}
            if(stage==2 && commit!=null && commit.IsEnabled) {
                stage=20; ((Button)Find(wizardContent,"LoadEmulatorImportSources")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
            }
            if(stage==20 && review!=null && review.IsEnabled) {
                var sourceSection=(Expander)Find(wizardContent,"EmulatorImportSourcesSection");
                Check(sourceSection.IsExpanded && ImportChildren(sourceSection).OfType<CheckBox>().Any(),"Reload after review makes loaded sources visible again for "+emulator);
                stage=21; review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
            }
            if(stage==21 && commit!=null && commit.IsEnabled) {
                stage=3;
                if(cancel) {wizard.Close(); done=true; return;}
                CheckAddedFilter(wizardContent,"HideAddedEmulatorImportGames",update ? 1 : 0);
                if(!update) {
                    ((ComboBox)Find(wizardContent,"EmulatorImportDestination")).SelectedValue="";
                    ((TextBox)Find(wizardContent,"EmulatorImportListName")).Text="Imported test list";
                    wizardContent.UpdateLayout(); var preview=new RenderTargetBitmap((int)wizardContent.ActualWidth+48,(int)wizardContent.ActualHeight+48,96,96,PixelFormats.Pbgra32);
                    preview.Render(wizardContent); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(preview)); using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Emulator-import-preview.png"))) png.Save(output);
                }
                wizard.Dispatcher.BeginInvoke(new Action(() => {
                    commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    done=!wizard.IsVisible;
                }));
            }
        };
        timer.Start(); try {((Button)Find(content,"ImportEmulatorLibrary_"+emulator)).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));} finally {timer.Stop();}
        return done;
    }
    static void CheckTitleCleanup(Launcher launcher,Window owner,bool applyChanges,bool normalise=false) {
        string before=normalise ? "Legend of Zelda, The - A Link to the Past (USA)" : "My game (USA) (SGB Enhanced)";
        string after=normalise ? "The Legend of Zelda - A Link to the Past (USA)" : "My game";
        var draft=new EmulatorImportGame {Path="test.gb",Name=before,CorePath="core.dll",ArtworkTitle="Original artwork label"};
        var field=new TextBox {Text=draft.Name}; var row=new StackPanel(); row.Children.Add(field);
        var box=new CheckBox {IsChecked=true,Content=row};
        var candidates=new Dictionary<EmulatorImportGame,CheckBox> {{draft,box}};
        bool visited=false; var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e) => {
            var window=owner.OwnedWindows.Cast<Window>().FirstOrDefault(item => item.IsVisible && item.Title==(normalise ? "Normalise titles" : "Remove tags from game titles")); if(window==null) return;
            timer.Stop(); visited=true;
            var toggle=(Button)Find(window.Content as FrameworkElement,"ToggleTitleTagChanges"); var apply=(Button)Find(window.Content as FrameworkElement,"ApplyTitleTagChanges");
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(!apply.IsEnabled && (string)toggle.Content=="Select all","Cleanup Select all toggle can clear the proposed changes");
            toggle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Check(apply.IsEnabled && (string)toggle.Content=="Unselect all","Cleanup Select all toggle restores proposed changes");
            if(applyChanges) apply.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); else window.Close();
        };
        timer.Start(); try {typeof(Launcher).GetMethod(normalise ? "NormaliseImportGameTitles" : "CleanImportGameTitles",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(launcher,new object[]{owner,candidates});} finally {timer.Stop();}
        Check(visited && draft.Name==(applyChanges ? after : before) && draft.RenameExisting==applyChanges,"Cleanup preview applies or cancels draft title changes without saving settings");
        Check(draft.Path=="test.gb" && draft.CorePath=="core.dll" && draft.ArtworkTitle=="Original artwork label","Cleanup leaves launch paths, core and artwork metadata unchanged");
    }
    static bool RunImportAll(Window owner,FrameworkElement content) {
        int stage=0; bool done=false; var started=DateTime.UtcNow;
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e) => {
            var wizard=owner.OwnedWindows.Cast<Window>().FirstOrDefault(w => w.IsVisible && w.Title=="Import emulator libraries");
            if(wizard==null) return;
            if(DateTime.UtcNow-started>TimeSpan.FromSeconds(5)) {foreach(var owned in wizard.OwnedWindows.Cast<Window>().Where(item => item.IsVisible).ToList()) owned.Close(); wizard.Close(); return;}
            var confirmation=wizard.OwnedWindows.Cast<Window>().FirstOrDefault(w => w.IsVisible && w.Title=="Confirm new games");
            if(confirmation!=null) {
                var previousContent=(FrameworkElement)wizard.Content;
                var previousDestination=(ComboBox)Find(previousContent,"ImportAllDestination_retroarch"); var previousName=(TextBox)Find(previousContent,"ImportAllListName_retroarch");
                ConfirmNewGames(confirmation,previousDestination==null ? null : previousDestination.SelectedValue as string,previousName==null ? null : previousName.Text); return;
            }
            var wizardContent=(FrameworkElement)wizard.Content;
            var review=(Button)Find(wizardContent,"ReviewEmulatorImportAll"); var commit=(Button)Find(wizardContent,"CommitEmulatorImportAll");
            var statusLabel=Find(wizardContent,"EmulatorImportAllStatus") as TextBlock;
            var allLoaded=statusLabel!=null && (statusLabel.Text.StartsWith("Sources loaded") || statusLabel.Text.StartsWith("No import sources"));
            if(stage==0 && review!=null && review.IsEnabled && allLoaded) {
                var reviewArea=(StackPanel)Find(wizardContent,"EmulatorImportAllReviewArea");
                var sequence=(Panel)reviewArea.Parent;
                FrameworkElement retroPanel=(FrameworkElement)Find(wizardContent,"ImportAllLocation_retroarch");
                while(retroPanel.Parent!=sequence) retroPanel=(FrameworkElement)retroPanel.Parent;
                Check(sequence.Children.IndexOf(reviewArea)>sequence.Children.IndexOf(retroPanel),"Bulk import shows every emulator's sources before its reviewed games");
                Check(ImportChildren(wizardContent).OfType<Button>().Single(button => button.Name=="HideAddedEmulatorImportAllGames").Visibility==Visibility.Collapsed,"Bulk import hides the game filter before reviewing sources");
                Check(Find(wizardContent,"ImportAllLocation_retroarch")!=null && Find(wizardContent,"ImportAllLocation_duckstation")!=null,"Import all wizard lists every configured emulator");
                var duckLocation=(TextBox)Find(wizardContent,"ImportAllLocation_duckstation"); var duckLoad=(Button)Find(wizardContent,"ImportAllLoadSources_duckstation");
                if(duckLocation!=null && duckLoad!=null) {
                    var emptyDir=Path.Combine(Path.GetTempPath(),"PadLauncher-ImportAll-Empty-"+Guid.NewGuid().ToString("N"));
                    Directory.CreateDirectory(emptyDir);
                    duckLocation.Text=emptyDir;
                    duckLoad.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
                stage=1; return;
            }
            if(stage==1 && review!=null && review.IsEnabled && ((Button)Find(wizardContent,"SelectAllEmulatorImportSources")).IsEnabled) {
                var selectAll=(Button)Find(wizardContent,"SelectAllEmulatorImportSources");
                var unselectAll=(Button)Find(wizardContent,"UnselectAllEmulatorImportSources");
                var countLabel=(TextBlock)Find(wizardContent,"ImportAllSelectionCount");
                Check(unselectAll==null && countLabel!=null && countLabel.Text.EndsWith("source selected") && countLabel.Text!="0 sources selected","Import all reports selected sources before review");
                selectAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(countLabel.Text=="0 sources selected","Unselect all sources clears the source selection");
                selectAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                Check(countLabel.Text.EndsWith("source selected") && countLabel.Text!="0 sources selected","Select all sources restores the full source selection");
                stage=2; review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
            }
            if(stage==2 && commit!=null && commit.IsEnabled) {
                stage=22; review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
            }
            if(stage==22 && commit!=null && commit.IsEnabled) {
                stage=23; ((Button)Find(wizardContent,"ImportAllLoadSources_retroarch")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
            }
            if(stage==23 && review!=null && review.IsEnabled && ((Button)Find(wizardContent,"SelectAllEmulatorImportSources")).IsEnabled) {
                Check(((TextBlock)Find(wizardContent,"ImportAllSelectionCount")).Text.EndsWith("source selected"),"Bulk library reload restores sources after review");
                stage=24; review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
            }
            if(stage==24 && commit!=null && commit.IsEnabled) {
                var bulkCount=(TextBlock)Find(wizardContent,"ImportAllSelectionCount");
                Check(bulkCount.Text.EndsWith("2 games selected"),"Reviewing bulk sources twice keeps one row per game");
                Check(!bulkCount.Text.Contains(Environment.NewLine) && bulkCount.Text.Contains("source") && bulkCount.Text.Contains("games selected") && bulkCount.Margin.Top>0,"Bulk import puts source and game counts on one line below the action buttons with spacing");
                CheckAddedFilter(wizardContent,"HideAddedEmulatorImportAllGames",2);
                var destination=(ComboBox)Find(wizardContent,"ImportAllDestination_retroarch"); var listName=(TextBox)Find(wizardContent,"ImportAllListName_retroarch");
                if(destination!=null && listName!=null) {destination.SelectedValue=""; listName.Text="Imported all test list";}
                stage=3; return;
            }
            if(stage==3 && commit!=null && commit.IsEnabled) {
                stage=4;
                wizard.Dispatcher.BeginInvoke(new Action(() => {
                    commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); done=!wizard.IsVisible;
                }));
            }
        };
        timer.Start();
        try {
            var emulatorTabs=(TabControl)Find(content,"EmulatorTabs");
            if(emulatorTabs==null) throw new Exception("EmulatorTabs missing before import all test");
            emulatorTabs.SelectedIndex=0; Layout(content);
            var open=(Button)Find(content,"ImportEmulatorLibrariesAll");
            if(open==null) throw new Exception("ImportEmulatorLibrariesAll button missing");
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        } finally {timer.Stop();}
        return done;
    }
    [STAThread] static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EmulatorUiTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher launcher=null;
        var previousFetch=ArtworkService.Fetch; ArtworkService.Fetch=(url,key,token) => System.Text.Encoding.UTF8.GetBytes("");
        try {
            string exe=Path.Combine(root,"duckstation.exe"),nextExe=Path.Combine(root,"duckstation-new.exe"),game=Path.Combine(root,"Test game.chd"); File.WriteAllText(exe,""); File.WriteAllText(nextExe,""); File.WriteAllText(game,"");
            var settings=new Settings(); EmulatorLibrary.SavePath(settings,"duckstation",exe); EmulatorLibrary.AddGames(settings,"duckstation",new[]{game});
            var library=new Library(); EmulatorLibrary.Apply(library,settings); Storage.Write("settings.json",settings); Storage.Write("library.json",library);
            launcher=new Launcher(false,true); var dialog=launcher.CreateHelpWindow(); var content=(FrameworkElement)dialog.Content;
            File.Move(exe,exe+".moved");
            typeof(Launcher).GetMethod("ApplyLibrary",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(launcher,new object[]{false});
            var overlayTitle=(TextBlock)Find((FrameworkElement)launcher.Content,"SelectedGameTitle");
            Check(overlayTitle.Text.Contains("executable missing or invalid") && !overlayTitle.Text.Contains("Game file missing"),"Overlay titles identify a moved emulator without claiming the existing ROM is missing");
            File.Move(exe+".moved",exe);
            typeof(Launcher).GetMethod("ApplyLibrary",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(launcher,new object[]{false});
            Check(!overlayTitle.Text.Contains("executable missing"),"Refreshing clears the emulator diagnostic after its executable is restored");
            var tabs=(TabControl)Find(content,"HelpSettingsTabs"); tabs.SelectedIndex=5; Layout(content);
            Check(tabs.Items.Count>9 && (string)((TabItem)tabs.Items[5]).Header=="Emulators" && (string)((TabItem)tabs.Items[6]).Header=="Artwork" && (string)((TabItem)tabs.Items[7]).Header=="Controls" && (string)((TabItem)tabs.Items[8]).Header=="Activity" && (string)((TabItem)tabs.Items[9]).Header=="System","Controls follows Artwork, then Activity, with System last");
            var gamesSection=(Expander)Find(content,"EmulatorGamesSection_duckstation");
            if(gamesSection!=null) {gamesSection.IsExpanded=true; Layout(content);}
            Check(gamesSection!=null && Find(content,"EmulatorRelocateGames_duckstation")!=null && Find(content,"EmulatorLocateGame_duckstation")!=null && (string)((Button)Find(content,"EmulatorRelocateGames_duckstation")).Content=="Relocate games' directory…","Emulator games offer bulk directory relocation and per-game file relocation");
            tabs.SelectedIndex=6; Layout(content);
            var emulatorArtworkDefaults=(Expander)Find(content,"EmulatorArtworkDefaultsSection");
            var cleanUpCachedArtwork=(Button)Find(content,"CleanUpCachedArtwork");
            var artworkPanel=emulatorArtworkDefaults==null ? null : emulatorArtworkDefaults.Parent as Panel;
            Check(cleanUpCachedArtwork!=null && artworkPanel!=null && artworkPanel.Children.IndexOf(cleanUpCachedArtwork)>artworkPanel.Children.IndexOf(emulatorArtworkDefaults) && (string)cleanUpCachedArtwork.Content=="Clean up cached artwork…","Artwork tab places merged cached-artwork cleanup below emulator artwork defaults");
            Check(Find(content,"RemoveMissingGameArtwork")==null,"Old separate missing-game artwork button is replaced");
            tabs.SelectedIndex=8; Layout(content);
            var gameHeader=(Button)Find(content,"PlayActivitySort_game");
            var lastHeader=(Button)Find(content,"PlayActivitySort_last");
            var timeHeader=(Button)Find(content,"PlayActivitySort_time");
            var launchesHeader=(Button)Find(content,"PlayActivitySort_launches");
            Check(Find(content,"PlayActivityGames")!=null && gameHeader!=null && lastHeader!=null && timeHeader!=null && launchesHeader!=null,"Activity column headers sort the activity table");
            Check(((string)timeHeader.Content).EndsWith("▼"),"Activity defaults to descending play time");
            timeHeader.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((string)timeHeader.Content).EndsWith("▲"),"Clicking Play time reverses the activity sort");
            timeHeader.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(((string)timeHeader.Content).EndsWith("▼"),"Clicking Play time again restores descending order");
            var activityGrid=(DataGrid)Find(content,"PlayActivityGames");
            Func<DependencyObject,System.Windows.Controls.Primitives.DataGridColumnHeader> findHeader=null;
            findHeader=node => {
                var columnHeader=node as System.Windows.Controls.Primitives.DataGridColumnHeader;
                if(columnHeader!=null && columnHeader.Column!=null && columnHeader.Column.Header==timeHeader) return columnHeader;
                for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) {var found=findHeader(VisualTreeHelper.GetChild(node,i)); if(found!=null) return found;}
                return null;
            };
            var playTimeHeader=findHeader(activityGrid);
            Check(playTimeHeader!=null,"Activity exposes the full Play time column header");
            if(playTimeHeader!=null) playTimeHeader.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,System.Windows.Input.MouseButton.Left){RoutedEvent=System.Windows.Input.Mouse.PreviewMouseDownEvent});
            Check(((string)timeHeader.Content).EndsWith("▲"),"Clicking anywhere on a column header sorts it");
            var includeImported=(CheckBox)Find(content,"PlayActivityIncludeImportedPlayTime");
            Check(activityGrid.Columns.Count==4 && ((FrameworkElement)activityGrid.Columns[3].Header).Name=="PlayActivitySort_last","Activity places Last launched in the last column");
            Check(includeImported!=null && includeImported.IsChecked==false,"Activity uses Pad Launcher play time only by default");
            Check(activityGrid!=null && activityGrid.Foreground!=null && activityGrid.CellStyle!=null && activityGrid.RowStyle!=null && activityGrid.ColumnHeaderStyle!=null,"Activity table uses the themed readable row, cell and header styles");
            includeImported.IsChecked=true; includeImported.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(Storage.Read<Settings>("settings.json").IncludeImportedPlayTime,"Activity can include imported play time when enabled");
            tabs.SelectedIndex=5; Layout(content);
            var emulatorTabs=(TabControl)Find(content,"EmulatorTabs");
            emulatorTabs.SelectedIndex=1; Layout(content);
            var sections=((StackPanel)((TabItem)emulatorTabs.Items[1]).Content).Children.OfType<Expander>().ToList();
            Check(sections.Count==EmulatorLibrary.Presets.Length && sections.All(s => !s.IsExpanded),"All emulator sections are initially collapsed");
            var section=(Expander)Find(content,"EmulatorSection_duckstation"); section.IsExpanded=true; Layout(content);
            Check(((TextBox)Find(content,"EmulatorPath_duckstation")).Text==exe,"Emulator path loads from saved settings");
            Check(Find(content,"EmulatorRomFolder_duckstation")==null && Find(content,"ImportEmulatorLibrary_duckstation")==null,"Settings keeps library controls in Games");
            emulatorTabs.SelectedIndex=0; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_duckstation")).IsExpanded=true; Layout(content);
            Check(Find(content,"EmulatorScan_duckstation")!=null && Find(content,"ImportEmulatorLibrary_duckstation")!=null,"Games contains import and folder scanning");
            ((TextBox)Find(content,"EmulatorRomFolder_duckstation")).Text=root;
            emulatorTabs.SelectedIndex=1; Layout(content);
            ((TextBox)Find(content,"EmulatorPath_duckstation")).Text=nextExe;
            ((Button)Find(content,"EmulatorSavePath_duckstation")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(content);
            Check(Storage.Read<Settings>("settings.json").Emulators[0].Path==nextExe,"Saving path uses the settings UI handler");
            Check(Storage.Read<Settings>("settings.json").Emulators[0].RomFolder==root,"Saving executable preserves the pending ROM folder");
            Check(((Expander)Find(content,"EmulatorSection_duckstation")).IsExpanded,"Rebuilding settings keeps the edited emulator expanded");
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=0; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_duckstation")).IsExpanded=true; Layout(content);
            Check(Find(content,"EmulatorAddGames_duckstation")!=null && Find(content,"EmulatorPath_duckstation")==null,"Games tab contains manual add without executable settings");
            ((TextBox)Find(content,"EmulatorGameName_duckstation")).Text="A renamed game";
            ((Button)Find(content,"EmulatorSaveGame_duckstation")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(content);
            Check(Storage.Read<Settings>("settings.json").Emulators[0].Games[0].Name=="A renamed game","Game editor saves a new title");
            var image=new RenderTargetBitmap(780,680,96,96,PixelFormats.Pbgra32); image.Render(content); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Emulator-settings-preview.png"))) encoder.Save(output);
            Check(Find(content,"EmulatorRemoveAllGames_duckstation")!=null,"Emulator games offer confirmed bulk unregistration");
            var removeAllGames=(Button)Find(content,"EmulatorRemoveAllGames_duckstation");
            Check(removeAllGames.Parent==((Button)Find(content,"EmulatorSaveGame_duckstation")).Parent && Grid.GetColumn(removeAllGames)==2 && removeAllGames.VerticalAlignment==VerticalAlignment.Center,"Bulk removal shares the game-action row, centered vertically on the right");

            ((Button)Find(content,"EmulatorRemoveGame_duckstation")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(content);
            Check(Storage.Read<Settings>("settings.json").Emulators[0].Games.Count==0 && File.Exists(game),"Individual game removal keeps the original ROM");
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=1; Layout(content);
            var retroSection=(Expander)Find(content,"EmulatorSection_retroarch"); retroSection.IsExpanded=true; Layout(content);
            string coreFolder=Path.Combine(root,"cores"); Directory.CreateDirectory(coreFolder); string core=Path.Combine(coreFolder,"snes9x_libretro.dll"); File.WriteAllText(core,"");
            ((TextBox)Find(content,"EmulatorPath_retroarch")).Text=nextExe;
            ((Button)Find(content,"EmulatorSavePath_retroarch")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(content);
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=0; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_retroarch")).IsExpanded=true; Layout(content);
            Check(Find(content,"EmulatorGamesCore_retroarch")==null && Find(content,"EmulatorDefaultCore")==null && Find(content,"EmulatorInstalledCores")==null,"RetroArch no longer exposes a persistent scan core in Games or Settings");
            Check(Find(content,"EmulatorRomFolder_retroarch")==null && Find(content,"EmulatorScan_retroarch")==null,"RetroArch omits ROM folder scanning");
            Check(Find(content,"ImportEmulatorLibrary_retroarch")!=null && Find(content,"EmulatorAddGames_retroarch")!=null,"RetroArch keeps library import and manual add");
            Check(Find(content,"RetroArchPreferLibretroSetting")==null && Storage.Read<Settings>("settings.json").RetroArchPreferLibretro==null,"Emulator settings omit the artwork preference and path saves leave it unchanged");
            var raLabel=(string)typeof(Launcher).GetMethod("GameSourceLabel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(launcher,new object[]{new Game {Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Game Boy",LaunchId="game.gb"}});
            Check(raLabel=="RetroArch · Game Boy","RetroArch game source labels include the console");
            var unresolvedSource=new EmulatorImportSource {Id="playlist:test",Name="Unresolved playlist"};
            var sourceBox=new CheckBox {Content=unresolvedSource.Name,IsChecked=false}; int sourceChanged=0;
            var sourceRow=(FrameworkElement)typeof(Launcher).GetMethod("RetroArchSourceRow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(launcher,new object[]{unresolvedSource,sourceBox,"No usable core found — will not be imported",new List<string>{core},(Action)(() => sourceChanged++),0});
            var sourceCorePicker=ImportChildren(sourceRow).OfType<ComboBox>().Single(); sourceCorePicker.SelectedValue=core;
            Check(unresolvedSource.CorePath==core && sourceBox.IsChecked==true && sourceChanged==1,"Unresolved source offers installed cores and stages the user's selected core for review");
            sourceCorePicker.SelectedValue="";
            Check(unresolvedSource.CorePath==null && sourceChanged==2,"Source core selection can be reset without writing settings");
            var noCoreRow=(FrameworkElement)typeof(Launcher).GetMethod("RetroArchSourceRow",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(launcher,new object[]{unresolvedSource,new CheckBox(),"No usable core found — will not be imported",new List<string>(),(Action)(() => {}),1});
            Check(!ImportChildren(noCoreRow).OfType<ComboBox>().Any() && ImportChildren(noCoreRow).OfType<TextBlock>().Any(text => text.Text.Contains("Install cores in RetroArch")),"No installed cores gives an installation instruction rather than a misleading empty picker");
            string playlists=Path.Combine(root,"playlists"); Directory.CreateDirectory(playlists); string playlist=Path.Combine(playlists,"Test playlist.lpl");
            File.WriteAllText(playlist,Storage.Json().Serialize(new {default_core_path=core,items=new[]{new {path=game,label="Imported game",core_path="DETECT"}}}));
            dialog.WindowStartupLocation=WindowStartupLocation.Manual; dialog.Left=-20000; dialog.Top=-20000; dialog.Show(); Layout(content);
            CheckTitleCleanup(launcher,dialog,false); CheckTitleCleanup(launcher,dialog,true);
            CheckTitleCleanup(launcher,dialog,false,true); CheckTitleCleanup(launcher,dialog,true,true);
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=0; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_retroarch")).IsExpanded=true; Layout(content);
            Check(RunImport(dialog,content,false,true) && RunImport(dialog,content,false,true),"Cancelling a reviewed RetroArch import allows reopening and reloading sources repeatedly");
            File.WriteAllText(Path.Combine(root,"settings.ini"),"[GameList]\nPaths = "+root);
            Check(RunImport(dialog,content,false,true,"duckstation") && RunImport(dialog,content,false,true,"duckstation"),"Other emulator imports also reopen, reload and review after cancellation");
            Check(RunImport(dialog,content,false),"Emulator import wizard loads sources, reviews a playlist and commits through its button"); Layout(content);
            var importResult=dialog.OwnedWindows.Cast<Window>().FirstOrDefault(window => window.Title=="Import complete");
            Check(importResult!=null && ((TextBox)Find(importResult.Content as FrameworkElement,"ImportedGamePath")).Text==game,"Successful library import shows the imported game's path");
            importResult.Close();
            var imported=Storage.Read<Settings>("settings.json"); string listId=imported.CustomLists.Single().Id;
            Check(imported.CustomLists.Single().Name=="Imported test list" && imported.CustomLists.Single().Games.Count==1 && imported.EmulatorImports.Single().ListId==listId,"Wizard creates named custom list and saved source mapping");
            string secondGame=Path.Combine(root,"Second game.chd"); File.WriteAllText(secondGame,"");
            File.WriteAllText(playlist,Storage.Json().Serialize(new {default_core_path=core,items=new[]{new {path=game,label="Imported game",core_path="DETECT"},new {path=secondGame,label="Second imported game",core_path="DETECT"}}}));
            Check(RunImport(dialog,content,true),"Saved import reopens its source and destination in the wizard");
            var updated=Storage.Read<Settings>("settings.json");
            Check(updated.CustomLists.Single().Id==listId && updated.CustomLists.Single().Games.Count==2 && updated.EmulatorImports.Count==1,"Wizard update extends the same list without duplicate entries");
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=0; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_retroarch")).IsExpanded=true; Layout(content);
            var retroListPicker=(ComboBox)Find(content,"EmulatorGameList_retroarch");
            var retroGamePicker=(ComboBox)Find(content,"EmulatorGames_retroarch");
            Check(retroListPicker!=null && retroGamePicker!=null && Find(content,"EmulatorGameListLabel_retroarch")!=null,"RetroArch games can be browsed by list");
            var retroListOptions=((IEnumerable<KeyValuePair<string,string>>)retroListPicker.ItemsSource).ToList();
            Check(retroListOptions.Any(option => option.Key==listId && option.Value.StartsWith("Imported test list")),"RetroArch list picker includes imported and custom lists containing RetroArch games");
            Check(!retroListOptions.Any(option => option.Key=="recent" || option.Key=="recently-added" || option.Key=="most-played" || option.Key=="most-played-lately"),"RetroArch list picker omits automatic play-history lists");
            retroListPicker.SelectedValue=listId; Layout(content);
            Check(retroGamePicker.Items.Count==2 && ((TextBlock)Find(content,"EmulatorGamesLabel_retroarch")).Text.Contains("2"),"Choosing a RetroArch list shows only that list's games");
            Check(RunImportAll(dialog,content),"Import all wizard reviews every emulator and commits through its button");
            var afterAll=Storage.Read<Settings>("settings.json");
            var allList=afterAll.CustomLists.SingleOrDefault(l => l.Name=="Imported all test list");
            Check(allList!=null && allList.Games.Count==2,"Import all commits its review selection into a new named list");
            var mappings=afterAll.EmulatorImports ?? new List<EmulatorImportDefinition>();
            Check(afterAll.CustomLists.Count==2 && mappings.Count==2 && mappings.Any(i => i.EmulatorId=="retroarch" && i.ListId==allList.Id),"Import all preserves the existing list and records the new source mapping");
            launcher.SetAvailableCollections(new[]{"all"});
            var destinationSettings=(Settings)typeof(Launcher).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(launcher);
            string importedListId=listId,otherImportedListId=allList.Id;
            listId="list:pc"; allList=new CustomList {Id="list:pc-second",Name="Other PC games",Games=new List<string>()};
            destinationSettings.CustomLists.Add(new CustomList {Id=listId,Name="PC",Games=new List<string>()}); destinationSettings.CustomLists.Add(allList);
            Storage.Write("settings.json",destinationSettings);
            var editorMethod=typeof(Launcher).GetMethod("CreateCustomGameEditor",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var addEditor=(FrameworkElement)editorMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {}),false});
            Layout(addEditor);
            var destinationItems=(IEnumerable<KeyValuePair<string,string>>)((ComboBox)Find(addEditor,"DefaultCustomGameList")).ItemsSource;
            Check(!destinationItems.Any(item => item.Key==importedListId || item.Key==otherImportedListId),"Custom-game destinations exclude imported emulator-only lists");
            ((ComboBox)Find(addEditor,"DefaultCustomGameList")).SelectedValue=listId;
            Check(Storage.Read<Settings>("settings.json").DefaultCustomGameListId==listId && (string)((ComboBox)Find(addEditor,"CustomGameList")).SelectedValue==listId,"New custom games use the saved default list");
            string localCover=Path.Combine(root,"local-cover.png");
            using(var cover=new System.Drawing.Bitmap(300,450)) cover.Save(localCover,System.Drawing.Imaging.ImageFormat.Png);
            ((TextBox)Find(addEditor,"CustomGameName")).Text="Custom UI game";
            ((TextBox)Find(addEditor,"CustomGameTarget")).Text=nextExe;
            Check(Find(addEditor,"CustomGameArtwork")==null,"New custom games contain no artwork field");
            ((Button)Find(addEditor,"SaveCustomGame")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var customSaved=Storage.Read<Settings>("settings.json");
            string customId=customSaved.CustomGames.Single().Id;
            Check(customSaved.CustomLists.First(list => list.Id==listId).Games.Contains(customId) && !customSaved.EnabledCollectionIds.Contains(listId),"Adding a custom game preserves a hidden destination list");
            customSaved.CustomGames.Single().Artwork=localCover;
            Storage.Write("settings.json",customSaved);
            typeof(Launcher).GetField("settings",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).SetValue(launcher,customSaved);
            var editEditor=(FrameworkElement)editorMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {}),true}); Layout(editEditor);
            Check(Find(editEditor,"CustomGameArtwork")==null && Find(editEditor,"ClearCustomGameImage")==null,"Added custom games contain no artwork fields or image buttons");
            ((ComboBox)Find(editEditor,"CustomGameList")).SelectedValue=allList.Id;
            ((Button)Find(editEditor,"SaveCustomGame")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(CustomGameLibrary.ArtworkPath(Storage.Read<Settings>("settings.json").CustomGames.Single().Artwork)==localCover,"Saving game details preserves its existing artwork");
            customSaved=Storage.Read<Settings>("settings.json");
            Check(!customSaved.CustomLists.First(list => list.Id==listId).Games.Contains(customId) && customSaved.CustomLists.First(list => list.Id==allList.Id).Games.Contains(customId),"Changing a custom game's list moves its manual membership");
            var panelMethod=typeof(Launcher).GetMethod("CreateCustomGamesPanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var customPanel=(FrameworkElement)panelMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {})}); Layout(customPanel);
            Check(((TabControl)Find(customPanel,"CustomGamesTabs")).SelectedIndex==1,"Saving an existing custom game keeps Added Games selected");
            var customTabs=(TabControl)Find(customPanel,"CustomGamesTabs");
            Check(customTabs.Items.Count==2 && (string)((TabItem)customTabs.Items[0]).Header=="Add New" && (string)((TabItem)customTabs.Items[1]).Header=="Added Games","Custom games contains only Add New and Added Games, without an Import tab");
            customSaved.CustomLists.First(list => list.Id==allList.Id).Games.Remove(customId);
            customSaved.CustomGames.First(entry => entry.Id==customId).IncludeInCustomGamesList=true;
            customSaved.CombinedCollections=new System.Collections.Generic.List<CombinedCollection> {
                new CombinedCollection {Id="combined:inherited-ui",Name="Inherited custom games",Sources=new System.Collections.Generic.List<string>{"source:custom","source:steam"}}
            };
            typeof(Launcher).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(launcher,customSaved);
            editEditor=(FrameworkElement)editorMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {}),true}); Layout(editEditor);
            Check((string)((ComboBox)Find(editEditor,"CustomGameList")).SelectedValue=="keep-memberships" && ((ComboBox)Find(editEditor,"CustomGameList")).Text.Contains("Custom games"),"Game's list reflects built-in Custom games and inherited combined memberships");
            ((Button)Find(editEditor,"SaveCustomGame")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check((Storage.Read<Settings>("settings.json").CombinedCollections.Single().AddedGames ?? new System.Collections.Generic.List<string>()).Count==0,"Saving unchanged inherited membership keeps the source-based relationship");
            string portableChoice=ArtworkService.SaveLocalChoice(localCover);
            var artworkSettings=new Settings {ArtworkOverrides=new System.Collections.Generic.Dictionary<string,string>{{"emulator-cover",portableChoice}}};
            var artworkGame=new Game {Id="emulator-cover",Source="emulator"};
            ArtworkService.ApplyCached(artworkGame,artworkSettings);
            Check(!Path.IsPathRooted(portableChoice) && artworkGame.Art==Path.Combine(Storage.Root,portableChoice),"Local emulator artwork is copied into portable data and accepted by the loader");
            var membershipsMethod=typeof(Launcher).GetMethod("ShowListMemberships",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            membershipsMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {})});
            var membershipsWindow=dialog.OwnedWindows.Cast<Window>().Single(window => window.Title=="Game list memberships"); Layout((FrameworkElement)membershipsWindow.Content);
            Check(Find((FrameworkElement)membershipsWindow.Content,"MembershipGamePicker")!=null && Find((FrameworkElement)membershipsWindow.Content,"SaveMemberships")!=null,"Game list memberships opens a game picker and save control");
            membershipsWindow.Close();

            var removalSettings=Storage.Read<Settings>("settings.json");
            var importedList=removalSettings.CustomLists.First(entry => entry.Id==importedListId);
            string keptGameId=importedList.Games[1];
            importedList.Games.RemoveAt(1);
            Storage.Write("settings.json",removalSettings);
            typeof(Launcher).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(launcher,removalSettings);
            typeof(Launcher).GetMethod("ApplyLibrary",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).Invoke(launcher,new object[]{false});
            dialog.Close();
            dialog=launcher.CreateHelpWindow(); content=(FrameworkElement)dialog.Content;
            dialog.WindowStartupLocation=WindowStartupLocation.Manual; dialog.Left=-20000; dialog.Top=-20000; dialog.Show(); Layout(content);
            ((TabControl)Find(content,"HelpSettingsTabs")).SelectedIndex=5; Layout(content);
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=0; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_retroarch")).IsExpanded=true; Layout(content);
            var scopedListPicker=(ComboBox)Find(content,"EmulatorGameList_retroarch");
            var scopedOptions=((IEnumerable<KeyValuePair<string,string>>)scopedListPicker.ItemsSource).ToList();
            Check(scopedOptions.Any(option => option.Key==importedListId && option.Value.Contains("(1 game)")),"RetroArch list picker counts only games still in the list");
            scopedListPicker.SelectedValue=importedListId; Layout(content);
            var scopedRemoveAll=(Button)Find(content,"EmulatorRemoveAllGames_retroarch");
            Check(scopedRemoveAll!=null && (string)scopedRemoveAll.Content=="Remove all registered games in list…","RetroArch bulk removal is labelled as list-scoped");
            bool removalConfirmed=false;
            var removalTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
            removalTimer.Tick+=(s,e) => {
                var confirmation=dialog.OwnedWindows.Cast<Window>().FirstOrDefault(window => window.IsVisible && window.Title=="Remove emulator games");
                if(confirmation==null) return;
                var confirmationContent=(FrameworkElement)confirmation.Content;
                var text=ImportChildren(confirmationContent).OfType<TextBlock>().Select(block => block.Text).ToList();
                var action=ImportChildren(confirmationContent).OfType<Button>().FirstOrDefault(button => String.Equals(button.Content as string,"Remove all registered games in list"));
                removalConfirmed=action!=null && text.Any(entry => entry.Contains("Imported test list")) && text.Any(entry => entry.Contains("1 registered game"));
                removalTimer.Stop();
                if(action!=null) action.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            removalTimer.Start();
            scopedRemoveAll.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            removalTimer.Stop();
            Check(removalConfirmed,"Removal confirmation names the selected list and its game count");
            var afterRemoval=Storage.Read<Settings>("settings.json");
            var remainingRetroGames=EmulatorLibrary.Config(afterRemoval,"retroarch").Games;
            Check(remainingRetroGames.Count==1 && remainingRetroGames[0].Id==keptGameId,"RetroArch bulk removal unregisters only games in the selected list");
            Check(File.Exists(game) && File.Exists(secondGame),"Removing games in a list leaves the original ROM files");

            Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_retroarch")).IsExpanded=true; Layout(content);
            string manualGame=Path.Combine(root,"Manual game.sfc"); File.WriteAllText(manualGame,"");
            var chooser=typeof(Launcher).GetMethods(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)
                .Single(method => method.Name=="ChooseEmulatorGames" && method.GetParameters().Length==10);
            bool pickerChecked=false; object selection=null;
            var pickerTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
            pickerTimer.Tick+=(s,e) => {
                var picker=dialog.OwnedWindows.Cast<Window>().FirstOrDefault(window => window.IsVisible && window.Title=="Add RetroArch games");
                if(picker==null) return;
                var pickerContent=(FrameworkElement)picker.Content;
                var corePicker=(ComboBox)Find(pickerContent,"EmulatorAddCore");
                var listPicker=(ComboBox)Find(pickerContent,"EmulatorAddList");
                var addSelected=(Button)Find(pickerContent,"EmulatorAddSelected");
                pickerChecked=corePicker!=null && listPicker!=null && addSelected!=null &&
                    String.Equals(corePicker.SelectedValue as string,core,StringComparison.OrdinalIgnoreCase) &&
                    String.Equals(listPicker.SelectedValue as string,importedListId,StringComparison.OrdinalIgnoreCase);
                pickerTimer.Stop();
                if(addSelected!=null) addSelected.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            };
            pickerTimer.Start();
            selection=chooser.Invoke(launcher,new object[]{dialog,EmulatorLibrary.Preset("retroarch"),new List<string>{manualGame},0,null,false,core,importedListId,true,nextExe});
            pickerTimer.Stop();
            var selectedGames=selection==null ? null : (Dictionary<string,string>)selection.GetType().GetField("Games").GetValue(selection);
            Check(pickerChecked && selection!=null && selectedGames!=null && selectedGames.ContainsKey(manualGame)
                && String.Equals((string)selection.GetType().GetField("CorePath").GetValue(selection),core,StringComparison.OrdinalIgnoreCase)
                && String.Equals((string)selection.GetType().GetField("Destination").GetValue(selection),importedListId,StringComparison.OrdinalIgnoreCase),
                "Manual RetroArch add offers the saved core and current list and returns both choices");
            var manualSettings=Storage.Read<Settings>("settings.json");
            Check(EmulatorLibrary.AddGames(manualSettings,"retroarch",selectedGames.Keys,selectedGames,core)==1
                && EmulatorLibrary.Config(manualSettings,"retroarch").Games.Single(entry => entry.Path==manualGame).CorePath==core,
                "Manual RetroArch adds store the chosen core with the new game");

            dialog.Close(); Console.WriteLine(checks+" emulator UI checks passed");
        } finally {if(launcher!=null) launcher.ExitApp(); ArtworkService.Fetch=previousFetch; Directory.Delete(root,true);}
    }
}
