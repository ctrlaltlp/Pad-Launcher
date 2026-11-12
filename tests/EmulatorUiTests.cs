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
    static bool RunImport(Window owner,FrameworkElement content,bool update) {
        int stage=0; bool done=false; var started=DateTime.UtcNow;
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e) => {
            var wizard=owner.OwnedWindows.Cast<Window>().FirstOrDefault(w => w.IsVisible && w.Title=="Import RetroArch library");
            if(wizard==null) return;
            if(DateTime.UtcNow-started>TimeSpan.FromSeconds(5)) {wizard.Close(); return;}
            var wizardContent=(FrameworkElement)wizard.Content;
            var review=(Button)Find(wizardContent,"ReviewEmulatorImportGames"); var commit=(Button)Find(wizardContent,"CommitEmulatorImport");
            if(stage==0 && review!=null && review.IsEnabled) {
                if(update) {
                    var saved=(ComboBox)Find(wizardContent,"SavedEmulatorImport"); if(saved==null || saved.Items.Count<2) {wizard.Close(); return;}
                    stage=1; saved.SelectedIndex=1; return;
                }
                stage=2; review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
            }
            if(stage==1 && review!=null && review.IsEnabled) {stage=2; review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;}
            if(stage==2 && commit!=null && commit.IsEnabled) {
                stage=3;
                if(!update) {
                    ((ComboBox)Find(wizardContent,"EmulatorImportDestination")).SelectedValue="";
                    ((TextBox)Find(wizardContent,"EmulatorImportListName")).Text="Imported test list";
                    wizardContent.UpdateLayout(); var preview=new RenderTargetBitmap((int)wizardContent.ActualWidth+48,(int)wizardContent.ActualHeight+48,96,96,PixelFormats.Pbgra32);
                    preview.Render(wizardContent); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(preview)); using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Emulator-import-preview.png"))) png.Save(output);
                }
                commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); done=!wizard.IsVisible;
            }
        };
        timer.Start(); try {((Button)Find(content,"ImportEmulatorLibrary_retroarch")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));} finally {timer.Stop();}
        return done;
    }
    static bool RunImportAll(Window owner,FrameworkElement content) {
        int stage=0; bool done=false; var started=DateTime.UtcNow;
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
        timer.Tick+=(s,e) => {
            var wizard=owner.OwnedWindows.Cast<Window>().FirstOrDefault(w => w.IsVisible && w.Title=="Import emulator libraries");
            if(wizard==null) return;
            if(DateTime.UtcNow-started>TimeSpan.FromSeconds(5)) {wizard.Close(); return;}
            var wizardContent=(FrameworkElement)wizard.Content;
            var review=(Button)Find(wizardContent,"ReviewEmulatorImportAll"); var commit=(Button)Find(wizardContent,"CommitEmulatorImportAll");
            var statusLabel=Find(wizardContent,"EmulatorImportAllStatus") as TextBlock;
            var allLoaded=statusLabel!=null && (statusLabel.Text.StartsWith("Sources loaded") || statusLabel.Text.StartsWith("No import sources"));
            if(stage==0 && review!=null && review.IsEnabled && allLoaded) {
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
                Check(((TextBlock)Find(wizardContent,"ImportAllSelectionCount")).Text.EndsWith("2 games selected"),"Reviewing bulk sources twice keeps one row per game");
                var destination=(ComboBox)Find(wizardContent,"ImportAllDestination_retroarch"); var listName=(TextBox)Find(wizardContent,"ImportAllListName_retroarch");
                if(destination!=null && listName!=null) {destination.SelectedValue=""; listName.Text="Imported all test list";}
                stage=3; return;
            }
            if(stage==3 && commit!=null && commit.IsEnabled) {
                commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); done=!wizard.IsVisible;
            }
        };
        timer.Start();
        try {
            var emulatorTabs=(TabControl)Find(content,"EmulatorTabs");
            if(emulatorTabs==null) throw new Exception("EmulatorTabs missing before import all test");
            emulatorTabs.SelectedIndex=1; Layout(content);
            var open=(Button)Find(content,"ImportEmulatorLibrariesAll");
            if(open==null) throw new Exception("ImportEmulatorLibrariesAll button missing");
            open.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        } finally {timer.Stop();}
        return done;
    }
    [STAThread] static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EmulatorUiTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher launcher=null;
        try {
            string exe=Path.Combine(root,"duckstation.exe"),nextExe=Path.Combine(root,"duckstation-new.exe"),game=Path.Combine(root,"Test game.chd"); File.WriteAllText(exe,""); File.WriteAllText(nextExe,""); File.WriteAllText(game,"");
            var settings=new Settings(); EmulatorLibrary.SavePath(settings,"duckstation",exe); EmulatorLibrary.AddGames(settings,"duckstation",new[]{game});
            var library=new Library(); EmulatorLibrary.Apply(library,settings); Storage.Write("settings.json",settings); Storage.Write("library.json",library);
            launcher=new Launcher(false,true); var dialog=launcher.CreateHelpWindow(); var content=(FrameworkElement)dialog.Content;
            var tabs=(TabControl)Find(content,"HelpSettingsTabs"); tabs.SelectedIndex=5; Layout(content);
            Check((string)((TabItem)tabs.Items[5]).Header=="Emulators" && (string)((TabItem)tabs.Items[6]).Header=="System","Emulators tab precedes System");
            var emulatorTabs=(TabControl)Find(content,"EmulatorTabs");
            var sections=((StackPanel)((TabItem)emulatorTabs.Items[0]).Content).Children.OfType<Expander>().ToList();
            Check(sections.Count==EmulatorLibrary.Presets.Length && sections.All(s => !s.IsExpanded),"All emulator sections are initially collapsed");
            var section=(Expander)Find(content,"EmulatorSection_duckstation"); section.IsExpanded=true; Layout(content);
            Check(((TextBox)Find(content,"EmulatorPath_duckstation")).Text==exe,"Emulator path loads from saved settings");
            Check(Find(content,"EmulatorRomFolder_duckstation")==null && Find(content,"ImportEmulatorLibrary_duckstation")==null,"Settings keeps library controls in Games");
            emulatorTabs.SelectedIndex=1; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_duckstation")).IsExpanded=true; Layout(content);
            Check(Find(content,"EmulatorScan_duckstation")!=null && Find(content,"ImportEmulatorLibrary_duckstation")!=null,"Games contains import and folder scanning");
            ((TextBox)Find(content,"EmulatorRomFolder_duckstation")).Text=root;
            emulatorTabs.SelectedIndex=0; Layout(content);
            ((TextBox)Find(content,"EmulatorPath_duckstation")).Text=nextExe;
            ((Button)Find(content,"EmulatorSavePath_duckstation")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(content);
            Check(Storage.Read<Settings>("settings.json").Emulators[0].Path==nextExe,"Saving path uses the settings UI handler");
            Check(Storage.Read<Settings>("settings.json").Emulators[0].RomFolder==root,"Saving executable preserves the pending ROM folder");
            Check(((Expander)Find(content,"EmulatorSection_duckstation")).IsExpanded,"Rebuilding settings keeps the edited emulator expanded");
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=1; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_duckstation")).IsExpanded=true; Layout(content);
            Check(Find(content,"EmulatorAddGames_duckstation")!=null && Find(content,"EmulatorPath_duckstation")==null,"Games tab contains manual add without executable settings");
            ((TextBox)Find(content,"EmulatorGameName_duckstation")).Text="A renamed game";
            ((Button)Find(content,"EmulatorSaveGame_duckstation")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(content);
            Check(Storage.Read<Settings>("settings.json").Emulators[0].Games[0].Name=="A renamed game","Game editor saves a new title");
            var image=new RenderTargetBitmap(780,680,96,96,PixelFormats.Pbgra32); image.Render(content); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(image)); using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Emulator-settings-preview.png"))) encoder.Save(output);
            ((Button)Find(content,"EmulatorRemoveGame_duckstation")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(content);
            Check(Storage.Read<Settings>("settings.json").Emulators[0].Games.Count==0 && File.Exists(game),"Removing from UI keeps the original ROM");
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=0; Layout(content);
            var retroSection=(Expander)Find(content,"EmulatorSection_retroarch"); retroSection.IsExpanded=true; Layout(content);
            Check(Find(content,"EmulatorDefaultCore")!=null && Find(content,"EmulatorInstalledCores")!=null,"RetroArch has a default core field and installed core dropdown");
            string coreFolder=Path.Combine(root,"cores"); Directory.CreateDirectory(coreFolder); string core=Path.Combine(coreFolder,"snes9x_libretro.dll"); File.WriteAllText(core,"");
            ((TextBox)Find(content,"EmulatorPath_retroarch")).Text=nextExe;
            ((TextBox)Find(content,"EmulatorDefaultCore")).Text=core;
            ((Button)Find(content,"EmulatorSaveCore")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); Layout(content);
            Check(EmulatorLibrary.Config(Storage.Read<Settings>("settings.json"),"retroarch").CorePath==core,"Core picker save handler persists executable and default core");
            Check(((ComboBox)Find(content,"EmulatorInstalledCores")).Items.Count==1,"Installed core dropdown populated after saving path");
            string playlists=Path.Combine(root,"playlists"); Directory.CreateDirectory(playlists); string playlist=Path.Combine(playlists,"Test playlist.lpl");
            File.WriteAllText(playlist,Storage.Json().Serialize(new {default_core_path=core,items=new[]{new {path=game,label="Imported game",core_path="DETECT"}}}));
            dialog.WindowStartupLocation=WindowStartupLocation.Manual; dialog.Left=-20000; dialog.Top=-20000; dialog.Show(); Layout(content);
            ((TabControl)Find(content,"EmulatorTabs")).SelectedIndex=1; Layout(content);
            ((Expander)Find(content,"EmulatorGamesSection_retroarch")).IsExpanded=true; Layout(content);
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
            Check(RunImportAll(dialog,content),"Import all wizard reviews every emulator and commits through its button");
            var afterAll=Storage.Read<Settings>("settings.json");
            var allList=afterAll.CustomLists.SingleOrDefault(l => l.Name=="Imported all test list");
            Check(allList!=null && allList.Games.Count==2,"Import all commits its review selection into a new named list");
            var mappings=afterAll.EmulatorImports ?? new List<EmulatorImportDefinition>();
            Check(afterAll.CustomLists.Count==2 && mappings.Count==2 && mappings.Any(i => i.EmulatorId=="retroarch" && i.ListId==allList.Id),"Import all preserves the existing list and records the new source mapping");
            launcher.SetAvailableCollections(new[]{"all"});
            var editorMethod=typeof(Launcher).GetMethod("CreateCustomGameEditor",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var addEditor=(FrameworkElement)editorMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {}),false});
            Layout(addEditor);
            ((ComboBox)Find(addEditor,"DefaultCustomGameList")).SelectedValue=listId;
            Check(Storage.Read<Settings>("settings.json").DefaultCustomGameListId==listId && (string)((ComboBox)Find(addEditor,"CustomGameList")).SelectedValue==listId,"New custom games use the saved default list");
            string localCover=Path.Combine(root,"local-cover.png");
            using(var cover=new System.Drawing.Bitmap(300,450)) cover.Save(localCover,System.Drawing.Imaging.ImageFormat.Png);
            ((TextBox)Find(addEditor,"CustomGameName")).Text="Custom UI game";
            ((TextBox)Find(addEditor,"CustomGameTarget")).Text=nextExe;
            ((TextBox)Find(addEditor,"CustomGameArtwork")).Text=localCover;
            ((Button)Find(addEditor,"SaveCustomGame")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var customSaved=Storage.Read<Settings>("settings.json");
            string customId=customSaved.CustomGames.Single().Id;
            Check(customSaved.CustomLists.First(list => list.Id==listId).Games.Contains(customId) && !customSaved.EnabledCollectionIds.Contains(listId),"Adding a custom game preserves a hidden destination list");
            var editEditor=(FrameworkElement)editorMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {}),true}); Layout(editEditor);
            ((Button)Find(editEditor,"ClearCustomGameImage")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(String.IsNullOrEmpty(Storage.Read<Settings>("settings.json").CustomGames.Single().Artwork),"Clear image removes the saved local cover without requiring Save changes");
            ((ComboBox)Find(editEditor,"CustomGameList")).SelectedValue=allList.Id;
            ((Button)Find(editEditor,"SaveCustomGame")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            customSaved=Storage.Read<Settings>("settings.json");
            Check(!customSaved.CustomLists.First(list => list.Id==listId).Games.Contains(customId) && customSaved.CustomLists.First(list => list.Id==allList.Id).Games.Contains(customId),"Changing a custom game's list moves its manual membership");
            var panelMethod=typeof(Launcher).GetMethod("CreateCustomGamesPanel",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            var customPanel=(FrameworkElement)panelMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {})}); Layout(customPanel);
            Check(((TabControl)Find(customPanel,"CustomGamesTabs")).SelectedIndex==1,"Saving an existing custom game keeps Added Games selected");
            customSaved.CustomLists.First(list => list.Id==allList.Id).Games.Remove(customId);
            customSaved.CombinedCollections=new System.Collections.Generic.List<CombinedCollection> {
                new CombinedCollection {Id="combined:inherited-ui",Name="Inherited custom games",Sources=new System.Collections.Generic.List<string>{"source:custom","source:steam"}}
            };
            typeof(Launcher).GetField("settings",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(launcher,customSaved);
            editEditor=(FrameworkElement)editorMethod.Invoke(launcher,new object[]{dialog,(Action)(() => {}),true}); Layout(editEditor);
            Check((string)((ComboBox)Find(editEditor,"CustomGameList")).SelectedValue=="combined:inherited-ui","Game's list reflects membership inherited through a combined source");
            ((Button)Find(editEditor,"SaveCustomGame")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check((Storage.Read<Settings>("settings.json").CombinedCollections.Single().AddedGames ?? new System.Collections.Generic.List<string>()).Count==0,"Saving unchanged inherited membership keeps the source-based relationship");
            string portableChoice=ArtworkService.SaveLocalChoice(localCover);
            var artworkSettings=new Settings {ArtworkOverrides=new System.Collections.Generic.Dictionary<string,string>{{"emulator-cover",portableChoice}}};
            var artworkGame=new Game {Id="emulator-cover",Source="emulator"};
            ArtworkService.ApplyCached(artworkGame,artworkSettings);
            Check(!Path.IsPathRooted(portableChoice) && artworkGame.Art==Path.Combine(Storage.Root,portableChoice),"Local emulator artwork is copied into portable data and accepted by the loader");
            dialog.Close(); Console.WriteLine(checks+" emulator UI checks passed");
        } finally {if(launcher!=null) launcher.ExitApp(); Directory.Delete(root,true);}
    }
}
