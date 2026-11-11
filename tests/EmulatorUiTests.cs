using System;
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
            var imported=Storage.Read<Settings>("settings.json"); string listId=imported.CustomLists.Single().Id;
            Check(imported.CustomLists.Single().Name=="Imported test list" && imported.CustomLists.Single().Games.Count==1 && imported.EmulatorImports.Single().ListId==listId,"Wizard creates named custom list and saved source mapping");
            string secondGame=Path.Combine(root,"Second game.chd"); File.WriteAllText(secondGame,"");
            File.WriteAllText(playlist,Storage.Json().Serialize(new {default_core_path=core,items=new[]{new {path=game,label="Imported game",core_path="DETECT"},new {path=secondGame,label="Second imported game",core_path="DETECT"}}}));
            Check(RunImport(dialog,content,true),"Saved import reopens its source and destination in the wizard");
            var updated=Storage.Read<Settings>("settings.json");
            Check(updated.CustomLists.Single().Id==listId && updated.CustomLists.Single().Games.Count==2 && updated.EmulatorImports.Count==1,"Wizard update extends the same list without duplicate entries");
            dialog.Close(); Console.WriteLine(checks+" emulator UI checks passed");
        } finally {if(launcher!=null) launcher.ExitApp(); Directory.Delete(root,true);}
    }
}
