using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PadLauncher;
class RetroArchListDefaultsTests {
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern int GetGuiResources(IntPtr process,int flags);
    static void CollectUi() {
        System.Windows.Threading.Dispatcher.CurrentDispatcher.Invoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,new Action(() => {}));
        GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
    }
    static int checks;
    static void Check(bool condition,string message) {if(!condition) throw new Exception(message); checks++;}
    static FrameworkElement Find(DependencyObject root,string name) {
        var element=root as FrameworkElement; if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var found=Find(VisualTreeHelper.GetChild(root,i),name); if(found!=null) return found;} return null;
    }
    [STAThread] static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-ra-defaults-"+Guid.NewGuid().ToString("N")); Storage.Root=root;
        Launcher launcher=null; Window owner=null;
        try {
            var settings=new Settings {Emulators=new List<EmulatorConfig> {new EmulatorConfig {Id="retroarch",Games=new List<EmulatorGame> {new EmulatorGame {Id="ra"}}}},
                CustomLists=new List<CustomList> {new CustomList {Id="pure",Name="GB",Games=new List<string>{"ra"}},new CustomList {Id="mixed",Name="Mixed",Games=new List<string>{"ra","pc"}},new CustomList {Id="empty",Name="Empty imported",Games=new List<string>()}},
                EmulatorImports=new List<EmulatorImportDefinition> {new EmulatorImportDefinition {EmulatorId="retroarch",ListId="empty"}},
                CombinedCollections=new List<CombinedCollection> {new CombinedCollection {Id="pure-combined",Name="Pure combined",Sources=new List<string>{"pure","source:emulator:retroarch"}},new CombinedCollection {Id="mixed-combined",Name="Mixed combined",Sources=new List<string>{"pure","source:steam"}}}
            };
            Check(RetroArchListDefaults.Applies(settings,"pure") && RetroArchListDefaults.Applies(settings,"empty") && RetroArchListDefaults.Applies(settings,"source:emulator:retroarch"),"RetroArch defaults identify imported, custom and automatic RetroArch lists");
            Check(!RetroArchListDefaults.Applies(settings,"mixed") && !RetroArchListDefaults.Applies(settings,"all") && !RetroArchListDefaults.Applies(settings,"mixed-combined") && RetroArchListDefaults.Applies(settings,"pure-combined"),"RetroArch-only combined lists inherit defaults while mixed and built-in general lists do not");
            settings.ListGameAdditions=new Dictionary<string,List<string>> {{"pure",new List<string>{"pc"}}};
            Check(!RetroArchListDefaults.Applies(settings,"pure"),"Individually added non-RetroArch games prevent group layout inheritance"); settings.ListGameAdditions.Clear();
            var shared=new ListLayout {Layout="grid",Columns=3,Rows=4,PanelScale=.8,RetroPanelScale=.8,PanelSizesLocked=true,ShowPanelOutlines=true,AngledSizing="fixed"}; settings.RetroArchListLayout=shared;
            Check(Object.ReferenceEquals(RetroArchListDefaults.Layout(settings,"pure"),shared) && RetroArchListDefaults.Layout(settings,"mixed")==null,"Shared layouts apply only to eligible lists");
            var specific=new ListLayout {Layout="rectangular",PanelScale=1.2}; settings.ListLayouts=new Dictionary<string,ListLayout>{{"pure",specific}};
            Check(Object.ReferenceEquals(RetroArchListDefaults.Layout(settings,"pure"),specific),"Individual list layout overrides win over RetroArch defaults");
            settings.RetroArchListArtworkSource="sgdb"; settings.RetroArchPreferLibretro=true;
            ArtworkService.UpgradeArtworkPreferences(settings);
            var game=new Game {Id="ra",Name="Retro game",Source="emulator",EmulatorId="retroarch",LaunchId="game.gb",ArtworkSystem="Nintendo - Game Boy"};
            Check(ArtworkService.GameListSource(settings,game,"pure")=="sgdb" && ArtworkService.GameListSource(settings,game,"mixed")=="sgdb","Shared artwork source applies to RetroArch games without a stale legacy preference overriding it");
            settings.ListArtworkSources=new Dictionary<string,string>{{"pure","libretro"}};
            Check(ArtworkService.GameListSource(settings,game,"pure")=="libretro","Individual list artwork choices win over group artwork defaults");
            settings.ListLayouts.Clear(); settings.ListArtworkSources.Clear(); settings.RetroArchListLayout=null; settings.RetroArchListArtworkSource=null; settings.RetroArchPreferLibretro=null;
            Storage.Write("settings.json",settings); Storage.Write("library.json",new Library {Games=new List<Game>{game,new Game {Id="pc",Name="PC game",Source="steam"}}});
            launcher=new Launcher(false,true); owner=launcher.CreateHelpWindow(); owner.Left=-20000; owner.Show();
            ((TabControl)Find(owner,"HelpSettingsTabs")).SelectedIndex=1; owner.UpdateLayout();
            var section=(Expander)Find(owner,"RetroArchListsSettingsSection"); section.IsExpanded=true; owner.UpdateLayout();
            var inherit=(CheckBox)Find(owner,"RetroArchListUseGlobalSettings");
            Check(inherit.IsChecked==true && Find(owner,"RetroArchListsPreferLibretro")==null,"RetroArch list settings offer layout inheritance and no duplicate artwork preference checkbox");
            inherit.IsChecked=false; inherit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((ComboBox)Find(owner,"RetroArchListLayoutSetting")).SelectedValue="grid";
            ((CheckBox)Find(owner,"RetroArchListShowPanelOutlinesSetting")).IsChecked=true;
            ((Slider)Find(owner,"RetroArchListModernPanelSizeSetting")).Value=.8;
            Check(Find(owner,"RetroArchListArtworkSourceSetting")==null && Find(owner,"RedownloadRetroArchArtwork")==null,"RetroArch layout settings have no duplicate artwork controls");
            ((Button)Find(owner,"SaveRetroArchListDefaults")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); owner.UpdateLayout();
            var saved=Storage.Read<Settings>("settings.json");
            Check(saved.RetroArchListLayout.Layout=="grid" && saved.RetroArchListLayout.ShowPanelOutlines==true && Math.Abs(saved.RetroArchListLayout.PanelScale-.8)<.01 && saved.RetroArchListArtworkSource==null && saved.RetroArchPreferLibretro==null,"Shared layout and artwork settings save without changing the emulator-level artwork preference");
            Check(saved.ListLayouts.Count==0 && saved.ListArtworkSources.Count==0 && ((Expander)Find(owner,"RetroArchListsSettingsSection")).IsExpanded,"Saving shared defaults does not manufacture per-list overrides and preserves the expanded section");
            CollectUi();
            int cleanupCount=((List<Action>)owner.Resources["PadLauncher.SettingsContentCleanup"]).Count;
            int handles=GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,1);
            for(int iteration=0;iteration<16;iteration++) {
                ((Button)Find(owner,"SaveRetroArchListDefaults")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                owner.UpdateLayout(); CollectUi();
                Check(((List<Action>)owner.Resources["PadLauncher.SettingsContentCleanup"]).Count==cleanupCount,"Rebuilding Settings must not retain cleanup callbacks for old controls");
            }
            int after=GetGuiResources(System.Diagnostics.Process.GetCurrentProcess().Handle,1);
            Check(after-handles<80,"Repeated Settings rebuilds must not accumulate Windows UI handles: "+handles+" -> "+after);
            Console.WriteLine("Settings rebuild stress: USER handles "+handles+" -> "+after+" across 16 rebuilds; cleanup callbacks bounded at "+cleanupCount+".");
            owner.Close(); owner=null;
            var large=new Library();
            var retro=new EmulatorConfig {Id="retroarch",Games=new List<EmulatorGame>()};
            for(int i=0;i<1500;i++) {
                string id="emulator:retroarch:"+i;
                retro.Games.Add(new EmulatorGame {Id=id,Name="Retro "+i,Path="game"+i+".gb"});
                large.Games.Add(new Game {Id=id,Name="Retro "+i,Source="emulator",EmulatorId="retroarch",LaunchId="game"+i+".gb"});
            }
            for(int i=0;i<80;i++) large.Games.Add(new Game {Id="pc"+i,Name="PC "+i,Source="steam"});
            var stressSettings=new Settings {CollectionId="retro",Emulators=new List<EmulatorConfig>{retro},RetroArchListLayout=shared,
                CustomLists=new List<CustomList>{new CustomList {Id="retro",Name="Retro",Games=retro.Games.Select(entry => entry.Id).ToList()},new CustomList {Id="pc",Name="PC",Games=large.Games.Where(entry => entry.Source=="steam").Select(entry => entry.Id).ToList()}}};
            var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
            typeof(Launcher).GetField("settings",flags).SetValue(launcher,stressSettings);
            typeof(Launcher).GetField("library",flags).SetValue(launcher,large);
            typeof(Launcher).GetMethod("ApplyLibrary",flags).Invoke(launcher,new object[]{false});
            launcher.Left=-20000; launcher.Show(); launcher.UpdateLayout();
            var move=typeof(Launcher).GetMethod("Move",flags);
            var change=typeof(Launcher).GetMethod("ChangeCollection",flags);
            var timer=System.Diagnostics.Stopwatch.StartNew();
            for(int cycle=0;cycle<20;cycle++) {
                for(int step=0;step<10;step++) move.Invoke(launcher,new object[]{1});
                change.Invoke(launcher,new object[]{1,false}); launcher.UpdateLayout();
            }
            timer.Stop(); CollectUi();
            Check(timer.ElapsedMilliseconds<30000,"Large-library navigation must complete without stalling the UI");
            Console.WriteLine("Large-library stress: 1,500 RetroArch + 80 PC games, 200 game moves and 20 list switches in "+timer.ElapsedMilliseconds+" ms.");
            Console.WriteLine(checks+" RetroArch list defaults checks passed.");
        } finally {if(owner!=null) owner.Close(); if(launcher!=null) launcher.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
