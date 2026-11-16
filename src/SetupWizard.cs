using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PadLauncher {
    public partial class Launcher {
        Window CreateSetupArtworkKeyWarning(Window owner) {
            var warning=new Window {Name="SetupArtworkKeyWarning",Title="SteamGridDB API key",Owner=owner,Width=600,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,Icon=Icon};
            ApplySettingsTheme(warning);
            var panel=new StackPanel {Margin=new Thickness(24)}; warning.Content=panel;
            var message=Label("You have not provided a SteamGridDB API key, artwork may fail on emulator & custom games and maybe some launcher games.\n\nWhat do you want to do?",14,white); message.TextWrapping=TextWrapping.Wrap; panel.Children.Add(message);
            var buttons=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,22,0,0)}; panel.Children.Add(buttons);
            var provide=new Button {Name="SetupProvideArtworkKey",Content="Provide API key (recommended)",IsDefault=true,IsCancel=true,Padding=new Thickness(14,8,14,8)};
            var proceed=new Button {Name="SetupContinueWithoutArtworkKey",Content="Continue",Padding=new Thickness(14,8,14,8),Margin=new Thickness(10,0,0,0)};
            provide.Click+=(s,e) => {warning.DialogResult=false;}; proceed.Click+=(s,e) => {warning.DialogResult=true;}; buttons.Children.Add(provide); buttons.Children.Add(proceed);
            return warning;
        }
        Window setupWindow;
        readonly bool freshProfile;
        internal bool NeedsInitialSetup {get {return !demo && !preview && !settings.SetupWizardShown && (freshProfile || TestEnvironment.EmulatorDetection);}}
        public void StartInitialSetup() {
            if(NeedsInitialSetup) ShowSetupWizard();
            else if(TestEnvironment.EmulatorDetection && !demo && !preview) OpenSettingsTab(5);
        }
        static Dictionary<string,string> DetectSetupLaunchers() {
            return new Dictionary<string,string> {
                {"steam",SteamLibrary.DetectSteam()},{"gog",GogLibrary.DetectClient()},{"epic",EpicLibrary.DetectLauncher()},
                {"ea",EaLibrary.DetectClient()},{"ubisoft",UbisoftLibrary.DetectClient()},{"battlenet",BattleNetLibrary.DetectClient()},{"rockstar",RockstarLibrary.DetectClient()},{"xbox",XboxLibrary.DetectClient()}
            };
        }
        public void ShowSetupWizard(bool restart=false,IDictionary<string,string> detectedLaunchers=null) {
            if(setupWindow!=null) {setupWindow.WindowState=WindowState.Normal; setupWindow.Activate(); return;}
            if(scanning) {ThemedMessage.Show(helpWindow,"Please wait for the current library refresh to finish, then restart setup.","Setup"); return;}
            int page=restart ? 0 : Math.Max(0,Math.Min(4,settings.SetupWizardPage));
            bool useSavedLauncherChoices=settings.SetupWizardShown;
            var initial=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            initial.SetupWizardShown=true; initial.SetupWizardCompleted=false; initial.SetupWizardPage=page;
            Storage.Write("settings.json",initial); settings=initial;
            var detected=detectedLaunchers ?? DetectSetupLaunchers();
            var dialog=new Window {Title="Pad Launcher · Setup",Width=760,Height=700,MinWidth=620,MinHeight=460,Background=new SolidColorBrush(Color.FromRgb(13,25,34)),Foreground=white,FontFamily=FontFamily,WindowStartupLocation=helpWindow!=null ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen,Icon=Icon};
            if(helpWindow!=null) dialog.Owner=helpWindow;
            ApplySettingsTheme(dialog); setupWindow=dialog;
            var root=new DockPanel {Margin=new Thickness(28),Background=dialog.Background}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            var progress=Label("",12,mint); progress.Name="SetupProgress"; header.Children.Add(progress);
            var title=Label("",27,white); title.Name="SetupPageTitle"; title.Margin=new Thickness(0,8,0,8); header.Children.Add(title);
            var optional=Label("Every step is optional. Next saves this page; Skip keeps your current settings. You can close setup and reopen it from System.",12,muted); optional.TextWrapping=TextWrapping.Wrap; optional.Margin=new Thickness(0,0,0,18); header.Children.Add(optional);
            var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var status=Label("",12,muted); status.Name="SetupStatus"; status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,12,0,12); footer.Children.Add(status);
            var actions=new Grid(); actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto}); footer.Children.Add(actions);
            var back=new Button {Name="SetupBack",Content="Back",Padding=new Thickness(16,8,16,8)};
            var right=new StackPanel {Orientation=Orientation.Horizontal}; Grid.SetColumn(right,1); actions.Children.Add(right);
            var skip=new Button {Name="SetupSkip",Content="Skip",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(16,8,16,8)}; actions.Children.Add(skip);
            var next=new Button {Name="SetupNext",Content="Next",Padding=new Thickness(16,8,16,8),Margin=new Thickness(8,0,0,0),Background=mint,Foreground=Brushes.Black};
            var again=new Button {Name="SetupRestart",Content="Restart setup",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(16,8,16,8)}; actions.Children.Add(again);
            var finish=new Button {Name="SetupFinish",Content="Finish",Padding=new Thickness(16,8,16,8),Margin=new Thickness(8,0,0,0),Background=mint,Foreground=Brushes.Black};
            right.Children.Add(back); right.Children.Add(next); right.Children.Add(finish);
            var scroll=new ScrollViewer {Name="SetupScroll",VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}; root.Children.Add(scroll);
            var pages=new StackPanel[5]; var apply=new Action<Settings>[5];
            Func<StackPanel,string,string,TextBox> textField=(panel,caption,name) => {
                var label=Label(caption,14,mint); label.Margin=new Thickness(0,12,0,8); panel.Children.Add(label);
                var box=new TextBox {Name=name,Padding=new Thickness(9)}; panel.Children.Add(box); return box;
            };
            Func<StackPanel,string,TextBlock> note=(panel,text) => {var label=Label(text,12,muted); label.TextWrapping=TextWrapping.Wrap; label.Margin=new Thickness(0,6,0,10); panel.Children.Add(label); return label;};
            Action buildLaunchers=() => {
                var panel=new StackPanel(); pages[0]=panel;
                note(panel,"Launcher locations are detected automatically where possible. Use Change path to choose another installation.");
                var launcherPaths=new Dictionary<string,TextBlock>();
                Func<string,string,TextBlock> pathRow=(id,path) => {
                    var row=new Grid {Margin=new Thickness(0,6,16,10)};
                    row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
                    var label=Label(DisplayLauncherPath(path),12,muted); label.Name=id=="steam" ? "SetupSteamPath" : "SetupLauncherPath_"+id; label.TextWrapping=TextWrapping.Wrap; label.VerticalAlignment=VerticalAlignment.Center; label.Margin=new Thickness(0,0,12,0);
                    if(String.IsNullOrEmpty(path)) label.Text="Not detected";
                    var change=new Button {Name="SetupChangePath_"+id,Content="Change path…",Padding=new Thickness(12,7,12,7),VerticalAlignment=VerticalAlignment.Center}; Grid.SetColumn(change,1);
                    change.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="Launcher executable|*.exe",CheckFileExists=true}; if(file.ShowDialog(dialog)==true) label.Text=DisplayLauncherPath(id=="steam" ? Path.GetDirectoryName(file.FileName) : file.FileName);};
                    row.Children.Add(label); row.Children.Add(change); panel.Children.Add(row); launcherPaths[id]=label; return label;
                };
                string autoSteam; detected.TryGetValue("steam",out autoSteam);
                var includeSteam=new CheckBox {Name="SetupLauncher_steam",Content="Include Steam games",IsChecked=useSavedLauncherChoices ? settings.SteamEnabled : !String.IsNullOrEmpty(settings.SteamPath ?? autoSteam),Margin=new Thickness(0,10,0,0)}; panel.Children.Add(includeSteam);
                var steam=pathRow("steam",settings.SteamPath ?? autoSteam);
                var shortcutsPanel=new StackPanel {Margin=new Thickness(16,0,0,10)}; panel.Children.Add(shortcutsPanel);
                var includeShortcuts=new CheckBox {Name="SetupIncludeSteamShortcuts",Content="Include non-Steam games",IsChecked=settings.IncludeSteamShortcuts,Margin=new Thickness(0,6,0,8)}; shortcutsPanel.Children.Add(includeShortcuts);
                var modeCaption=Label("Non-Steam launch mode",12,mint); shortcutsPanel.Children.Add(modeCaption);
                var shortcutMode=new SettingsComboBox {Name="SetupSteamShortcutMode",Width=240,HorizontalAlignment=HorizontalAlignment.Left,ItemsSource=new[]{new KeyValuePair<string,string>("steam","Through Steam"),new KeyValuePair<string,string>("direct","Direct launch")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.SteamShortcutLaunchMode=="direct" ? "direct" : "steam",Padding=new Thickness(8),Margin=new Thickness(0,6,0,0)}; shortcutsPanel.Children.Add(shortcutMode);
                Action updateSteamChoices=() => {
                    bool detectedSteam=!String.IsNullOrWhiteSpace(steam.Text) && steam.Text!="Not detected";
                    shortcutsPanel.Visibility=detectedSteam ? Visibility.Visible : Visibility.Collapsed;
                    includeShortcuts.IsEnabled=detectedSteam && includeSteam.IsChecked==true;
                    modeCaption.Visibility=shortcutMode.Visibility=includeShortcuts.IsChecked==true ? Visibility.Visible : Visibility.Collapsed;
                    shortcutMode.IsEnabled=includeShortcuts.IsEnabled && includeShortcuts.IsChecked==true;
                };
                includeSteam.Checked+=(s,e) => updateSteamChoices(); includeSteam.Unchecked+=(s,e) => updateSteamChoices();
                includeShortcuts.Checked+=(s,e) => updateSteamChoices(); includeShortcuts.Unchecked+=(s,e) => updateSteamChoices();
                System.ComponentModel.DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty,typeof(TextBlock)).AddValueChanged(steam,(s,e) => updateSteamChoices()); updateSteamChoices();
                var boxes=new Dictionary<string,CheckBox>();
                foreach(var source in new[]{new[]{"gog","GOG Galaxy"},new[]{"epic","Epic Games Store"},new[]{"ea","EA App"},new[]{"ubisoft","Ubisoft Connect"},new[]{"battlenet","Battle.net"},new[]{"rockstar","Rockstar Games Launcher"}}) {
                    string path; detected.TryGetValue(source[0],out path);
                    path=(source[0]=="gog" ? settings.GogLauncherPath : source[0]=="epic" ? settings.EpicLauncherPath : source[0]=="ea" ? settings.EaLauncherPath : source[0]=="ubisoft" ? settings.UbisoftLauncherPath : source[0]=="battlenet" ? settings.BattleNetLauncherPath : settings.RockstarLauncherPath) ?? path;
                    bool savedEnabled=source[0]=="gog" ? settings.GogEnabled : source[0]=="epic" ? settings.EpicEnabled : source[0]=="ea" ? settings.EaEnabled : source[0]=="ubisoft" ? settings.UbisoftEnabled : source[0]=="battlenet" ? settings.BattleNetEnabled : settings.RockstarEnabled;
                    var box=new CheckBox {Name="SetupLauncher_"+source[0],Content="Include "+source[1]+" games",IsChecked=useSavedLauncherChoices ? savedEnabled : !String.IsNullOrEmpty(path),Margin=new Thickness(0,10,0,0)}; panel.Children.Add(box); boxes[source[0]]=box;
                    pathRow(source[0],path);
                }
                string xboxPath; detected.TryGetValue("xbox",out xboxPath);
                var xbox=new CheckBox {Name="SetupLauncher_xbox",Content="Include Xbox app games",IsChecked=useSavedLauncherChoices ? settings.XboxEnabled : !String.IsNullOrEmpty(xboxPath),Margin=new Thickness(0,10,0,0)}; panel.Children.Add(xbox);
                note(panel,String.IsNullOrEmpty(xboxPath) ? "Not detected." : DisplayLauncherPath(xboxPath));
                note(panel,"Xbox game locations and launching are managed by Windows; no launcher path is needed.");
                apply[0]=draft => {
                    draft.XboxEnabled=xbox.IsChecked==true;
                    string path=steam.Text=="Not detected" ? "" : steam.Text.Trim().Trim('"');
                    if(includeSteam.IsChecked==true && path.Length>0) {
                        path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(path));
                        if(!File.Exists(Path.Combine(path,"steam.exe")) || !Directory.Exists(Path.Combine(path,"steamapps"))) throw new ArgumentException("Choose a valid Steam folder, leave the field empty, or use Skip.");
                        if(!String.Equals(path,draft.SteamPath,StringComparison.OrdinalIgnoreCase)) {draft.SteamPath=path; draft.UserId=null; draft.CollectionId="all"; draft.SelectedId=null; draft.EnabledCollectionIds=null; draft.DefaultCollectionId="remember";}
                    }
                    draft.SteamEnabled=includeSteam.IsChecked==true;
                    draft.IncludeSteamShortcuts=includeShortcuts.IsChecked==true;
                    draft.SteamShortcutLaunchMode=shortcutMode.SelectedValue as string ?? "steam";
                    draft.GogEnabled=boxes["gog"].IsChecked==true; draft.EpicEnabled=boxes["epic"].IsChecked==true;
                    draft.EaEnabled=boxes["ea"].IsChecked==true; draft.UbisoftEnabled=boxes["ubisoft"].IsChecked==true;
                    draft.BattleNetEnabled=boxes["battlenet"].IsChecked==true; draft.RockstarEnabled=boxes["rockstar"].IsChecked==true;
                    Func<string,string> chosen=id => launcherPaths[id].Text=="Not detected" ? null : launcherPaths[id].Text;
                    draft.GogLauncherPath=chosen("gog"); draft.EpicLauncherPath=chosen("epic"); draft.EaLauncherPath=chosen("ea"); draft.UbisoftLauncherPath=chosen("ubisoft"); draft.BattleNetLauncherPath=chosen("battlenet"); draft.RockstarLauncherPath=chosen("rockstar");
                };
            };
            Action buildEmulators=() => {
                var panel=new StackPanel(); pages[1]=panel;
                note(panel,"Find installed emulators or portable copies with Start-menu shortcuts, or choose executable paths below.");
                var importNote=note(panel,"Games can be imported later in Emulator settings; this page does not scan ROM folders.");
                importNote.Foreground=new SolidColorBrush(Color.FromRgb(255,211,105));
                var paths=new Dictionary<string,TextBox>();
                var detect=new Button {Name="SetupDetectEmulators",Content="Detect emulators…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,8,0,12)}; panel.Children.Add(detect);
                foreach(var preset in EmulatorLibrary.Presets) {
                    string id=preset.Id; var config=EmulatorLibrary.Config(settings,id);
                    var section=new Expander {Header=preset.Name+" · "+preset.System,Foreground=white,Margin=new Thickness(0,6,0,0)};
                    var body=new StackPanel {Margin=new Thickness(12,6,12,12)}; section.Content=body; panel.Children.Add(section);
                    var field=textField(body,"Emulator executable (optional)","SetupEmulatorPath_"+id); field.Text=config==null ? "" : config.Path ?? ""; paths[id]=field;
                    var browse=new Button {Content="Browse…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,8,0,0)};
                    browse.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter=preset.Name+" executable|*.exe",CheckFileExists=true}; if(file.ShowDialog(dialog)==true) field.Text=file.FileName;}; body.Children.Add(browse);
                }
                detect.Click+=(s,e) => ShowEmulatorDetection(dialog,() => {
                    foreach(var pair in paths) {var saved=EmulatorLibrary.Config(settings,pair.Key); if(saved!=null) pair.Value.Text=saved.Path ?? "";}
                });
                apply[1]=draft => {foreach(var pair in paths) if(!String.IsNullOrWhiteSpace(pair.Value.Text)) EmulatorLibrary.SavePath(draft,pair.Key,pair.Value.Text);};
            };
            PasswordBox apiKey=null;
            Action buildArtwork=() => {
                var panel=new StackPanel(); pages[2]=panel;
                note(panel,"Existing launcher artwork and local images work without a SteamGridDB key. Add a key only if you want SteamGridDB artwork for custom games, emulator games, or launcher games with missing artwork.");
                var enabled=new CheckBox {Name="SetupArtworkEnabled",Content="Use SteamGridDB artwork",IsChecked=settings.SteamGridDbEnabled && !String.IsNullOrWhiteSpace(ArtworkKey.Load()),Margin=new Thickness(0,12,0,12)}; panel.Children.Add(enabled);
                panel.Children.Add(Label("SteamGridDB API key",14,mint));
                apiKey=new PasswordBox {Name="SetupArtworkKey",Password=ArtworkKey.Load() ?? "",Padding=new Thickness(9),Margin=new Thickness(0,8,0,0)}; panel.Children.Add(apiKey);
                bool hadKey=!String.IsNullOrWhiteSpace(apiKey.Password);
                apiKey.PasswordChanged+=(s,e) => {bool hasKey=!String.IsNullOrWhiteSpace(apiKey.Password); if(!hasKey || !hadKey) enabled.IsChecked=hasKey; hadKey=hasKey;};
                note(panel,"Stored encrypted for your Windows account. Leaving this blank keeps any saved key. You can verify or change it later under System.");
                apply[2]=draft => {draft.SteamGridDbEnabled=enabled.IsChecked==true && (!String.IsNullOrWhiteSpace(apiKey.Password) || !String.IsNullOrWhiteSpace(ArtworkKey.Load()));};
            };
            Action buildAppearance=() => {
                var panel=new StackPanel(); pages[3]=panel;
                note(panel,"Choose how the overlay looks and where it opens. All of these options remain available in Settings.");
                Func<string,string,IEnumerable<KeyValuePair<string,string>>,string,SettingsComboBox> choice=(caption,name,options,value) => {
                    var label=Label(caption,14,mint); label.Margin=new Thickness(0,14,0,8); panel.Children.Add(label);
                    var picker=new SettingsComboBox {Name=name,ItemsSource=options.ToList(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=value,Padding=new Thickness(8)}; panel.Children.Add(picker); if(picker.SelectedIndex<0) picker.SelectedIndex=0; return picker;
                };
                var buttons=choice("Button style","SetupButtonStyle",new[]{"PlayStation","Xbox","Nintendo"}.Select(item => new KeyValuePair<string,string>(item,item)),Prompts.Style(settings));
                var layout=choice("Overlay layout","SetupLayout",new[]{new KeyValuePair<string,string>("angled","Angled"),new KeyValuePair<string,string>("rectangular","Rectangular"),new KeyValuePair<string,string>("grid","Grid")},settings.CarouselLayout);
                AddLayoutPreviews(panel,layout);
                var monitors=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("cursor","Cursor's monitor")};
                int index=1; foreach(var screen in System.Windows.Forms.Screen.AllScreens) monitors.Add(new KeyValuePair<string,string>(screen.DeviceName,"Monitor "+(index++)+" · "+screen.DeviceName));
                var monitor=choice("Open on monitor","SetupMonitor",monitors,settings.OpenMonitor);
                var titleCheck=new CheckBox {Name="SetupShowTitle",Content="Show game title",IsChecked=settings.ShowGameTitle,Margin=new Thickness(0,18,0,10)}; panel.Children.Add(titleCheck);
                var listCheck=new CheckBox {Name="SetupShowList",Content="Show list name",IsChecked=settings.ShowCollectionName,Margin=new Thickness(0,0,0,10)}; panel.Children.Add(listCheck);
                apply[3]=draft => {draft.ButtonPrompts=(string)buttons.SelectedValue; draft.CarouselLayout=(string)layout.SelectedValue; draft.OpenMonitor=(string)monitor.SelectedValue; draft.ShowGameTitle=titleCheck.IsChecked==true; draft.ShowCollectionName=listCheck.IsChecked==true;};
            };
            var finishPanel=new StackPanel(); pages[4]=finishPanel;
            note(finishPanel,"Your saved choices are ready. Nothing else is required. Finish returns the app to the tray; running the executable again or double-clicking its tray icon opens the overlay.");
            note(finishPanel,"Enabled launcher libraries will refresh after Finish. You can import emulator games and create lists later in Settings. Restart setup keeps the settings you have already saved.");
            var openSettings=new CheckBox {Name="SetupOpenSettings",Content="Open Settings when finished",IsChecked=false,Margin=new Thickness(0,18,0,0)}; finishPanel.Children.Add(openSettings);
            buildLaunchers(); buildEmulators(); buildArtwork(); buildAppearance();
            string[] titles={"Connect your launchers","Add your emulators","Choose artwork","Controls & overlay","You're ready"};
            Action render=() => {
                progress.Text="Step "+(page+1)+" of "+pages.Length; title.Text=titles[page]; scroll.Content=pages[page]; scroll.ScrollToTop();
                back.IsEnabled=page>0; skip.Visibility=next.Visibility=page==4 ? Visibility.Collapsed : Visibility.Visible;
                again.Visibility=finish.Visibility=page==4 ? Visibility.Visible : Visibility.Collapsed; status.Text="";
                optional.Text=page==4 ? "All choices remain available in Settings. Restart setup keeps your saved choices." : "Every step is optional. Next saves this page; Skip keeps your current settings. You can close setup and reopen it from System.";
                next.IsDefault=page<4; finish.IsDefault=page==4;
            };
            Action<bool> advance=save => {
                try {
                    if(save && page==2 && String.IsNullOrWhiteSpace(apiKey.Password) && String.IsNullOrWhiteSpace(ArtworkKey.Load())) {
                        if(CreateSetupArtworkKeyWarning(dialog).ShowDialog()!=true) {apiKey.Focus(); return;}
                    }
                    var draft=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    if(save) {apply[page](draft); if(page==2 && !String.IsNullOrWhiteSpace(apiKey.Password) && apiKey.Password.Trim()!=ArtworkKey.Load()) ArtworkKey.Save(apiKey.Password.Trim());}
                    draft.SetupWizardPage=page+1; Storage.Write("settings.json",draft); settings=draft;
                    if(!save) {if(page==0) buildLaunchers(); else if(page==1) buildEmulators(); else if(page==2) buildArtwork(); else buildAppearance();}
                    page++; render();
                } catch(Exception error) {status.Text="Could not save this page. "+error.Message;}
            };
            next.Click+=(s,e) => advance(true); skip.Click+=(s,e) => advance(false);
            back.Click+=(s,e) => {if(page>0) {page--; render();}};
            again.Click+=(s,e) => {
                try {var draft=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); draft.SetupWizardPage=0; Storage.Write("settings.json",draft); settings=draft; useSavedLauncherChoices=true; page=0; buildLaunchers(); buildEmulators(); buildArtwork(); buildAppearance(); render();}
                catch(Exception error) {status.Text=error.Message;}
            };
            finish.Click+=(s,e) => {
                try {
                    var draft=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); draft.SetupWizardShown=true; draft.SetupWizardCompleted=true; draft.SetupWizardPage=4;
                    Storage.Write("settings.json",draft); settings=draft; ApplyLibrary();
                    bool show=openSettings.IsChecked==true; dialog.Close(); RefreshLibrary(); if(show) OpenSettingsTab(5);
                } catch(Exception error) {status.Text="Could not finish setup. "+error.Message;}
            };
            dialog.PreviewKeyDown+=(s,e) => {if(e.Key==System.Windows.Input.Key.Escape) {dialog.Close(); e.Handled=true;}};
            dialog.Closed+=(s,e) => {setupWindow=null; if(freshProfile && !TestEnvironment.EmulatorDetection && !settings.SetupWizardCompleted && !exiting && !demo) RefreshLibrary();}; render(); dialog.Show();
        }
    }
}
