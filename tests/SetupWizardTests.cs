using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PadLauncher;
class SetupWizardTests {
    static int checks;
    static void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
    static IEnumerable<DependencyObject> Descendants(DependencyObject node) {
        yield return node;
        foreach(var item in LogicalTreeHelper.GetChildren(node)) {var child=item as DependencyObject; if(child!=null) foreach(var next in Descendants(child)) yield return next;}
    }
    static T Find<T>(Window wizard,string name) where T:FrameworkElement {return Descendants(wizard).OfType<T>().First(item => item.Name==name);}
    static void Click(Window wizard,string name) {Find<Button>(wizard,name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
    static Window Wizard(Launcher launcher) {return (Window)typeof(Launcher).GetField("setupWindow",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(launcher);}
    static void SkipToEnd(Window wizard) {for(int i=0;i<4;i++) Click(wizard,"SetupSkip");}
    static void WaitUntil(Func<bool> condition) {
        var frame=new DispatcherFrame(); var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
        var deadline=DateTime.UtcNow.AddSeconds(10);
        timer.Tick+=(s,e) => {if(condition() || DateTime.UtcNow>=deadline) frame.Continue=false;};
        timer.Start(); Dispatcher.PushFrame(frame); timer.Stop();
        Check(condition(),"Setup work completes without blocking the UI");
    }
    static void WriteString(BinaryWriter writer,string value) {writer.Write(System.Text.Encoding.UTF8.GetBytes(value)); writer.Write((byte)0);}
    static void WriteField(BinaryWriter writer,string key,string value) {writer.Write((byte)1); WriteString(writer,key); WriteString(writer,value);}
    static void CheckTransactionalSetup(string root,string emulatorExe) {
        string previous=Storage.Root; Launcher instance=null;
        try {
            string steam=Path.Combine(root,"wizard-steam"),config=Path.Combine(steam,"userdata","42","config"),game=Path.Combine(root,"WizardGame.exe");
            Directory.CreateDirectory(config); Directory.CreateDirectory(Path.Combine(steam,"steamapps")); File.WriteAllText(Path.Combine(steam,"steam.exe"),""); File.WriteAllText(game,"");
            using(var writer=new BinaryWriter(File.Create(Path.Combine(config,"shortcuts.vdf")))) {
                writer.Write((byte)0); WriteString(writer,"shortcuts"); writer.Write((byte)0); WriteString(writer,"0");
                writer.Write((byte)2); WriteString(writer,"appid"); writer.Write((uint)1234);
                WriteField(writer,"AppName","Wizard game"); WriteField(writer,"exe","\""+game+"\""); WriteField(writer,"StartDir","\""+root+"\"");
                writer.Write((byte)8); writer.Write((byte)8);
            }
            var detected=new Dictionary<string,string>{{"steam",steam}};
            Storage.Root=Path.Combine(root,"cancel-fresh"); Directory.CreateDirectory(Storage.Root);
            instance=new Launcher(false,true); instance.ShowSetupWizard(true,detected); var wizard=Wizard(instance);
            Click(wizard,"SetupNext");
            Check(!Descendants(wizard).OfType<CheckBox>().Any(box => box.Name=="SetupRetroArchPreferLibretro"),"RetroArch setup uses a detection prompt instead of an artwork checkbox");
            Find<TextBox>(wizard,"SetupEmulatorPath_retroarch").Text=emulatorExe;
            Find<TextBox>(wizard,"SetupEmulatorPath_pcsx2").Text=emulatorExe; Click(wizard,"SetupNext");
            Find<PasswordBox>(wizard,"SetupArtworkKey").Password="unsaved-key"; Click(wizard,"SetupNext");
            Find<ComboBox>(wizard,"SetupLayout").SelectedValue="rectangular"; Click(wizard,"SetupNext");
            Check(!File.Exists(Path.Combine(Storage.Root,"settings.json")) && ArtworkKey.Load()==null,"Fresh setup writes neither settings nor API key before Finish");
            wizard.Close();
            var library=(Library)typeof(Launcher).GetField("library",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(instance);
            Check(!instance.IsVisible && library.Games.Count==0,"Closing first-run setup returns to the tray without showing template panels");
            typeof(Launcher).GetMethod("Save",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(instance,null);
            Check(!File.Exists(Path.Combine(Storage.Root,"settings.json")) && ArtworkKey.Load()==null,"Closing first-run setup leaves the data untouched even after an overlay save");
            instance.ExitApp(); Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(() => {})); instance=null;
            Storage.Root=Path.Combine(root,"finish-fresh"); Directory.CreateDirectory(Storage.Root);
            instance=new Launcher(false,true); instance.ShowSetupWizard(true,detected); wizard=Wizard(instance);
            Click(wizard,"SetupNext"); Click(wizard,"SetupSkip"); Click(wizard,"SetupSkip"); Click(wizard,"SetupSkip"); Click(wizard,"SetupFinish");
            var active=instance;
            WaitUntil(() => Wizard(active)==null && !(bool)typeof(Launcher).GetField("scanning",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(active));
            var saved=Storage.Read<Settings>("settings.json");
            library=(Library)typeof(Launcher).GetField("library",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(instance);
            Check(saved.SetupWizardCompleted && saved.CustomGames.Count==1 && CustomGameLibrary.InCustomGamesList(saved,saved.CustomGames[0]),"Finish automatically imports included Steam shortcuts into Custom games");
            Check(library.Games.Count==1 && CombinedCollections.Options(library,saved).Single(list => list.Id=="source:custom").Games.Count==1,"Wizard import and Steam detection produce a single entry");
            string id=saved.CustomGames[0].Id;
            instance.ShowSetupWizard(true,detected); wizard=Wizard(instance); Click(wizard,"SetupNext"); Click(wizard,"SetupSkip"); Click(wizard,"SetupSkip"); Click(wizard,"SetupSkip"); Click(wizard,"SetupFinish");
            WaitUntil(() => Wizard(active)==null && !(bool)typeof(Launcher).GetField("scanning",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(active));
            saved=Storage.Read<Settings>("settings.json");
            Check(saved.CustomGames.Count==1 && saved.CustomGames[0].Id==id,"Repeating setup keeps the same imported game without duplicates");
            instance.ExitApp(); Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(() => {})); instance=null;
            Storage.Root=Path.Combine(root,"finish-without-shortcuts"); Directory.CreateDirectory(Storage.Root);
            instance=new Launcher(false,true); instance.ShowSetupWizard(true,detected); wizard=Wizard(instance);
            Find<CheckBox>(wizard,"SetupIncludeSteamShortcuts").IsChecked=false;
            Click(wizard,"SetupNext"); Click(wizard,"SetupSkip"); Click(wizard,"SetupSkip"); Click(wizard,"SetupSkip"); Click(wizard,"SetupFinish");
            active=instance;
            WaitUntil(() => Wizard(active)==null && !(bool)typeof(Launcher).GetField("scanning",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(active));
            saved=Storage.Read<Settings>("settings.json");
            library=(Library)typeof(Launcher).GetField("library",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(instance);
            Check(!saved.IncludeSteamShortcuts && (saved.CustomGames==null || saved.CustomGames.Count==0) && library.Games.Count==0,"Unchecking non-Steam games prevents both custom imports and native shortcut detection");
        } finally {if(instance!=null) {instance.ExitApp(); Dispatcher.CurrentDispatcher.Invoke(DispatcherPriority.ApplicationIdle,new Action(() => {}));} Storage.Root=previous;}
    }
    static void CheckStartupProfiles(string root) {
        string previous=Storage.Root;
        try {
            foreach(string profile in new[]{"fresh","decline","existing","cached"}) {
                Storage.Root=Path.Combine(root,profile); Directory.CreateDirectory(Storage.Root);
                if(profile=="existing") Storage.Write("settings.json",new Settings {LeftStickSpeed=2});
                if(profile=="cached") Storage.Write("library.json",new Library());
                var instance=new Launcher(false,false);
                try {
                    Check(instance.NeedsInitialSetup==(profile=="fresh" || profile=="decline"),"First-run setup is only automatic for a fresh profile: "+profile);
                    int offers=0;
                    instance.StartInitialSetup(() => {offers++; return profile=="fresh";});
                    Check(offers==(profile=="fresh" || profile=="decline" ? 1 : 0),"Only fresh profiles receive the setup offer");
                    if(profile=="decline") {
                        var declined=Storage.Read<Settings>("settings.json");
                        Check(declined.SetupWizardDeclined && !declined.SetupWizardCompleted,"Declining is saved without completing setup");
                        instance.StartInitialSetup(() => {throw new Exception("Setup was offered again after declining");});
                    }
                    Check((Wizard(instance)!=null)==(profile=="fresh"),"Startup does not force setup onto existing data: "+profile);
                    if(profile=="existing") Check(Storage.Read<Settings>("settings.json").LeftStickSpeed==2 && !Storage.Read<Settings>("settings.json").SetupWizardShown,"Existing settings are not rewritten by a setup prompt");
                } finally {
                    // Prevent the real launcher scan when closing this fresh-profile fixture.
                    typeof(Launcher).GetField("demo",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(instance,true);
                    if(Wizard(instance)!=null) Wizard(instance).Close(); instance.ExitApp();
                }
            }
        } finally {Storage.Root=previous;}
    }
    [STAThread] public static void Main(string[] args) {
        System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext());
        string root=Path.Combine(Path.GetTempPath(),"SetupWizard-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher launcher=null;
        try {
            CheckStartupProfiles(root);
            string mixedCase=Path.Combine(root,"MixedCase","Launcher.EXE"); Directory.CreateDirectory(Path.GetDirectoryName(mixedCase)); File.WriteAllText(mixedCase,"");
            string displayedPath=Launcher.DisplayLauncherPath(mixedCase.ToLowerInvariant().Replace('\\','/'));
            Check(displayedPath.EndsWith(@"\MixedCase\Launcher.EXE",StringComparison.Ordinal) && !displayedPath.Contains('/') && Char.IsUpper(displayedPath[0]),"Launcher paths use actual filesystem casing and Windows separators");
            string oldExe=Path.Combine(root,"old","pcsx2.exe"),newExe=Path.Combine(root,"new","pcsx2-qt.exe");
            foreach(string file in new[]{oldExe,newExe}) {Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file,"");}
            var initial=new Settings {RetroArchListArtworkSource="sgdb",GogEnabled=false,LeftStickSpeed=2,HiddenGameIds=new List<string>{"hidden-sentinel"},CustomLists=new List<CustomList>{new CustomList {Id="list:sentinel",Name="Keep",Games=new List<string>{"keep-game"}}},Emulators=new List<EmulatorConfig>{new EmulatorConfig {Id="pcsx2",Path=oldExe,RomFolder="keep-rom-folder",Arguments="--existing {game}",Games=new List<EmulatorGame>()}}};
            Storage.Write("settings.json",initial); ArtworkKey.Save("existing-key"); launcher=new Launcher(true,true);
            var pathStatus=new TextBlock();
            typeof(Launcher).GetMethod("SetLauncherDetectionNote",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(launcher,new object[]{pathStatus,"Steam",oldExe});
            var pathCheck=typeof(Launcher).GetMethod("ShowLauncherPathCheck",BindingFlags.NonPublic|BindingFlags.Instance);
            pathCheck.Invoke(launcher,new object[]{pathStatus,"Steam",oldExe.ToLowerInvariant().Replace('\\','/')});
            Check(pathStatus.Text=="Steam already detected.","Saving the same detected path recognizes casing and separator differences");
            pathCheck.Invoke(launcher,new object[]{pathStatus,"Steam",newExe});
            Check(pathStatus.Text=="Steam successfully detected","A new valid launcher path shows successful detection");
            pathCheck.Invoke(launcher,new object[]{pathStatus,"Steam",Path.Combine(root,"missing.exe")});
            Check(pathStatus.Text=="Steam not found at the given path.","A missing launcher path reports the requested error");
            pathCheck.Invoke(launcher,new object[]{pathStatus,"Steam",newExe});
            Check(pathStatus.Text=="Steam already detected.","A failed path check retains the last successfully detected path");
            launcher.ShowSetupWizard(true,new Dictionary<string,string>()); var wizard=Wizard(launcher);
            if(args.Length==2 && (args[0]=="--render" || args[0]=="--render-emulators" || args[0]=="--render-layouts")) {
                if(args[0]=="--render-emulators") Click(wizard,"SetupSkip");
                if(args[0]=="--render-layouts") for(int step=0;step<3;step++) Click(wizard,"SetupSkip");
                wizard.UpdateLayout();
                var content=(FrameworkElement)wizard.Content;
                var image=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,System.Windows.Media.PixelFormats.Pbgra32);
                var visual=new System.Windows.Media.DrawingVisual();
                using(var drawing=visual.RenderOpen()) drawing.DrawRectangle(new System.Windows.Media.VisualBrush(content),null,new Rect(0,0,content.ActualWidth,content.ActualHeight));
                image.Render(visual);
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
                using(var output=File.Create(args[1])) png.Save(output);
                return;
            }
            Check(wizard!=null && Find<TextBlock>(wizard,"SetupProgress").Text=="Step 1 of 5" && !Find<Button>(wizard,"SetupBack").IsEnabled,"Setup starts on its first optional page");
            Check(!Descendants(wizard).OfType<TextBox>().Any(),"Launcher paths are displayed as plain text");
            Check(Descendants(wizard).OfType<Button>().Count(button => button.Name.StartsWith("SetupChangePath_"))==7,"Every launcher has a Change path action");
            Check((string)Find<ComboBox>(wizard,"SetupSteamShortcutMode").SelectedValue=="direct","New setups default non-Steam games to direct launch");
            Check(Find<CheckBox>(wizard,"SetupIncludeSteamShortcuts").IsChecked==true,"Non-Steam games are included by default");
            Find<TextBlock>(wizard,"SetupSteamPath").Text="invalid Steam path";
            Find<CheckBox>(wizard,"SetupLauncher_steam").IsChecked=false;
            Check(!Find<CheckBox>(wizard,"SetupIncludeSteamShortcuts").IsEnabled && !Find<ComboBox>(wizard,"SetupSteamShortcutMode").IsEnabled,"Disabling Steam disables its shortcut choices");
            Find<CheckBox>(wizard,"SetupLauncher_steam").IsChecked=true;
            Find<CheckBox>(wizard,"SetupIncludeSteamShortcuts").IsChecked=false;
            Check(Find<ComboBox>(wizard,"SetupSteamShortcutMode").Visibility==Visibility.Collapsed,"Launch mode is hidden when non-Steam games are excluded");
            Find<CheckBox>(wizard,"SetupIncludeSteamShortcuts").IsChecked=true;
            Find<ComboBox>(wizard,"SetupSteamShortcutMode").SelectedValue="direct";
            Click(wizard,"SetupNext");
            Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 1 of 5" && Find<TextBlock>(wizard,"SetupStatus").Text.Contains("Could not save"),"Invalid optional input is reported without changing the saved page");
            Click(wizard,"SetupSkip"); Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 2 of 5","Skip bypasses invalid optional input");
            Find<TextBox>(wizard,"SetupEmulatorPath_pcsx2").Text="invalid emulator path"; Click(wizard,"SetupSkip");
            Find<PasswordBox>(wizard,"SetupArtworkKey").Password="skip-this-key"; Click(wizard,"SetupSkip");
            Find<ComboBox>(wizard,"SetupButtonStyle").SelectedValue="Nintendo"; Find<ComboBox>(wizard,"SetupLayout").SelectedValue="rectangular"; Click(wizard,"SetupSkip");
            Check(Find<Button>(wizard,"SetupSkip").Visibility==Visibility.Collapsed && Find<Button>(wizard,"SetupNext").Visibility==Visibility.Collapsed && Find<Button>(wizard,"SetupRestart").Visibility==Visibility.Visible && Find<Button>(wizard,"SetupFinish").Visibility==Visibility.Visible,"Last page replaces Skip and Next with Restart setup and Finish");
            var skipped=Storage.Read<Settings>("settings.json");
            Check(skipped.GogEnabled==false && skipped.ButtonPrompts=="PlayStation" && skipped.CarouselLayout=="angled" && EmulatorLibrary.Config(skipped,"pcsx2").Path==oldExe && ArtworkKey.Load()=="existing-key","Skipped pages preserve launchers, emulator paths, artwork keys and appearance");
            Click(wizard,"SetupFinish");
            Check(Wizard(launcher)==null && Storage.Read<Settings>("settings.json").SetupWizardCompleted,"All pages can be skipped and setup still finishes");
            Check(Storage.Read<Settings>("settings.json").LeftStickSpeed==2 && Storage.Read<Settings>("settings.json").HiddenGameIds.Single()=="hidden-sentinel" && Storage.Read<Settings>("settings.json").CustomLists.Single().Id=="list:sentinel","Setup preserves unrelated preferences and existing lists");
            launcher.StartInitialSetup();
            Check(Wizard(launcher)==null,"Later launches do not force the wizard again");
            launcher.OpenSettingsTab(5);
            Check(Descendants(launcher.HelpWindow).OfType<Button>().Any(button => button.Name=="RestartSetupWizard"),"System provides a Restart setup action in the regular app");
            launcher.ShowSetupWizard(true,new Dictionary<string,string>()); wizard=Wizard(launcher);
            Check(!Find<CheckBox>(wizard,"SetupLauncher_gog").IsChecked.GetValueOrDefault(),"Rerunning setup reflects saved launcher choices");
            Find<TextBlock>(wizard,"SetupSteamPath").Text=""; Find<CheckBox>(wizard,"SetupLauncher_gog").IsChecked=true;
            Find<CheckBox>(wizard,"SetupLauncher_steam").IsChecked=false;
            Find<ComboBox>(wizard,"SetupSteamShortcutMode").SelectedValue="direct";
            Find<TextBlock>(wizard,"SetupLauncherPath_gog").Text=newExe;
            Find<TextBlock>(wizard,"SetupLauncherPath_battlenet").Text=newExe;
            Click(wizard,"SetupNext");
            Check(Storage.Read<Settings>("settings.json").SteamEnabled,"Next leaves saved Steam inclusion unchanged until Finish");
            Check(Storage.Read<Settings>("settings.json").GogLauncherPath==null && Storage.Read<Settings>("settings.json").BattleNetLauncherPath==null,"Changed launcher paths remain staged");
            Check(!Storage.Read<Settings>("settings.json").GogEnabled && Storage.Read<Settings>("settings.json").SetupWizardPage==4,"Next does not write launcher choices or progress");
            Check(Find<Button>(wizard,"SetupDetectEmulators")!=null,"Emulator detection is accessible directly from the wizard");
            Find<TextBox>(wizard,"SetupEmulatorPath_retroarch").Text=newExe;
            Find<TextBox>(wizard,"SetupEmulatorPath_pcsx2").Text=newExe;
            Click(wizard,"SetupBack"); Click(wizard,"SetupNext");
            Check(Find<TextBox>(wizard,"SetupEmulatorPath_pcsx2").Text==newExe,"Back and Next preserve an emulator-page draft");
            Click(wizard,"SetupNext"); var applied=Storage.Read<Settings>("settings.json");
            Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 3 of 5","Emulator setup advances successfully: "+Find<TextBlock>(wizard,"SetupStatus").Text);
            Check(EmulatorLibrary.Config(applied,"pcsx2").Path==oldExe && EmulatorLibrary.Config(applied,"pcsx2").RomFolder=="keep-rom-folder" && EmulatorLibrary.Config(applied,"pcsx2").Arguments=="--existing {game}","Wizard keeps staged emulator paths out of saved settings");
            Find<PasswordBox>(wizard,"SetupArtworkKey").Password="new-key"; Click(wizard,"SetupNext");
            Check(ArtworkKey.Load()=="existing-key","Next does not replace the saved artwork key");
            Check(Descendants(wizard).OfType<Image>().Count(image => image.Source is System.Windows.Media.Imaging.BitmapSource)==2,"Both carousel layout options have template pictures");
            Click(wizard,"SetupLayoutPreview_angled");
            Check((string)Find<ComboBox>(wizard,"SetupLayout").SelectedValue=="angled","Clicking a layout picture selects that layout");
            Click(wizard,"SetupLayoutPreview_rectangular");
            Check((string)Find<ComboBox>(wizard,"SetupLayout").SelectedValue=="rectangular","The rectangular preview selects the same option as the dropdown");
            Find<ComboBox>(wizard,"SetupButtonStyle").SelectedValue="Nintendo"; Find<ComboBox>(wizard,"SetupLayout").SelectedValue="rectangular"; Find<CheckBox>(wizard,"SetupShowTitle").IsChecked=false; Click(wizard,"SetupNext");
            Check(Storage.Read<Settings>("settings.json").ButtonPrompts=="PlayStation" && Storage.Read<Settings>("settings.json").CarouselLayout=="angled" && Storage.Read<Settings>("settings.json").ShowGameTitle,"Appearance choices are not saved before Finish");
            Click(wizard,"SetupRestart");
            Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 1 of 5" && Find<CheckBox>(wizard,"SetupLauncher_gog").IsChecked==true && !Storage.Read<Settings>("settings.json").GogEnabled && ArtworkKey.Load()=="existing-key","Restart preserves staged choices without committing them");
            SkipToEnd(wizard); Click(wizard,"SetupFinish");
            Check(Storage.Read<Settings>("settings.json").SetupWizardCompleted,"Setup can finish after restarting and skipping every page");
            Check(!Storage.Read<Settings>("settings.json").SteamEnabled && Storage.Read<Settings>("settings.json").GogEnabled && EmulatorLibrary.Config(Storage.Read<Settings>("settings.json"),"pcsx2").Path==newExe && Storage.Read<Settings>("settings.json").CarouselLayout=="rectangular" && ArtworkKey.Load()=="new-key","Finish commits launcher, emulator, artwork and appearance drafts together");
            Check(Storage.Read<Settings>("settings.json").RetroArchListArtworkSource=="sgdb","Manually entering a RetroArch path does not silently change the existing artwork preference");
            string beforeClose=File.ReadAllText(Path.Combine(root,"settings.json"));
            launcher.ShowSetupWizard(true,new Dictionary<string,string>()); wizard=Wizard(launcher); Click(wizard,"SetupSkip"); wizard.Close();
            Check(Wizard(launcher)==null && File.ReadAllText(Path.Combine(root,"settings.json"))==beforeClose,"Closing restarted setup leaves the saved settings byte-for-byte unchanged");
            ArtworkKey.Save(""); launcher.ShowSetupWizard(true,new Dictionary<string,string>()); wizard=Wizard(launcher);
            Click(wizard,"SetupSkip"); Click(wizard,"SetupSkip");
            Check(!Descendants(wizard).OfType<CheckBox>().Any(box => box.Name=="SetupArtworkEnabled"),"Wizard uses source choices without a global SteamGridDB checkbox");
            Check(Find<TextBlock>(wizard,"SetupEmulatorArtworkHeading").Text=="Choose emulator artwork sources","Wizard labels the emulator artwork source section");
            wizard.UpdateLayout();
            var previewRoot=(FrameworkElement)wizard.Content;
            var previewBitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(previewRoot.ActualWidth)+60,(int)Math.Ceiling(previewRoot.ActualHeight)+60,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            previewBitmap.Render(previewRoot); var previewPng=new System.Windows.Media.Imaging.PngBitmapEncoder(); previewPng.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(previewBitmap));
            using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Setup-artwork-preview.png"))) previewPng.Save(output);

            Check(!Descendants(wizard).OfType<CheckBox>().Any(box => box.Name=="SetupLibretroArtwork"),"Wizard artwork uses per-emulator sources instead of a global Libretro checkbox");
            foreach(string emulator in new[]{"duckstation","mgba","melonds"}) Check((string)Find<ComboBox>(wizard,"SetupArtworkSource_"+emulator).SelectedValue=="libretro","Older emulator defaults prefer Libretro: "+emulator);
            Check((string)Find<ComboBox>(wizard,"SetupArtworkSource_pcsx2").SelectedValue=="libretro","PCSX2 defaults to Libretro");
            Check((string)Find<ComboBox>(wizard,"SetupArtworkSource_xemu").SelectedValue=="libretro","xemu defaults to Prefer Libretro");
            Find<ComboBox>(wizard,"SetupArtworkSource_dolphin").SelectedValue="libretro-only";
            Find<PasswordBox>(wizard,"SetupArtworkKey").Password="temporary-key";
            Find<PasswordBox>(wizard,"SetupArtworkKey").Password="";
            Check((string)Find<ComboBox>(wizard,"SetupArtworkSource_dolphin").SelectedValue=="libretro-only","Editing or clearing the key preserves source choices");
            foreach(var preset in EmulatorLibrary.Presets) Find<ComboBox>(wizard,"SetupArtworkSource_"+preset.Id).SelectedValue="libretro-only";
            Click(wizard,"SetupNext");
            Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 4 of 5","Only Libretro choices advance without a missing-SGDB-key warning");
            Click(wizard,"SetupBack");
            Find<ComboBox>(wizard,"SetupArtworkSource_xemu").SelectedValue="sgdb";
            wizard.Dispatcher.BeginInvoke(new Action(() => {
                var warning=wizard.OwnedWindows.Cast<Window>().Single(window => window.Name=="SetupArtworkKeyWarning");
                Check((string)Find<Button>(warning,"SetupProvideArtworkKey").Content=="Provide API key (recommended)","Missing-key warning offers the recommended key action");
                Click(warning,"SetupProvideArtworkKey");
            }));
            Click(wizard,"SetupNext");
            Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 3 of 5" && Storage.Read<Settings>("settings.json").SetupWizardPage==4,"Provide API key keeps the artwork page without changing saved progress");
            wizard.Dispatcher.BeginInvoke(new Action(() => {
                var warning=wizard.OwnedWindows.Cast<Window>().Single(window => window.Name=="SetupArtworkKeyWarning"); Click(warning,"SetupContinueWithoutArtworkKey");
            }));
            Click(wizard,"SetupNext");
            Check(Find<TextBlock>(wizard,"SetupProgress").Text=="Step 4 of 5","Continue allows setup without an API key");
            Click(wizard,"SetupSkip");
            Check(Find<Button>(wizard,"SetupBack").Parent==Find<Button>(wizard,"SetupFinish").Parent && Find<Button>(wizard,"SetupRestart").Parent is Grid && Grid.GetColumn((UIElement)Find<Button>(wizard,"SetupBack").Parent)==1,"Last page places Back and Finish on the right and Restart setup on the left");
            Find<CheckBox>(wizard,"SetupOpenSettings").IsChecked=true;
            Click(wizard,"SetupFinish");
            Check((string)((TabItem)Find<TabControl>(launcher.HelpWindow,"HelpSettingsTabs").SelectedItem).Header=="Overlay","Open Settings after Finish selects Overlay");
            CheckTransactionalSetup(root,newExe);
            Console.WriteLine(checks+" setup wizard checks passed");
        } finally {if(launcher!=null) {var wizard=Wizard(launcher); if(wizard!=null) wizard.Close(); launcher.ExitApp();} if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
