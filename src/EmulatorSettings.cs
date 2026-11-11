using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PadLauncher {
    public partial class Launcher {
        readonly HashSet<string> openEmulatorSections=new HashSet<string>();
        void CommitEmulators(Settings next,Action rebuild) {
            if(scanning) throw new InvalidOperationException("Please wait for the current library refresh to finish.");
            Storage.Write("settings.json",next); settings=next;
            if(artworkCancellation!=null) artworkCancellation.Cancel();
            EmulatorLibrary.Apply(library,settings);
            foreach(var game in library.Games) ArtworkService.ApplyCached(game,settings);
            library.Games=library.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            cards.Clear(); carousel.Children.Clear(); ApplyLibrary(); rebuild(); DownloadArtwork(library);
        }
        int emulatorTabIndex;
        readonly HashSet<string> openEmulatorGameSections=new HashSet<string>();
        bool ConfirmEmulatorRemoval(Window owner,string title,string heading,string message,string actionLabel) {
            return ShowEmulatorMessage(owner,title,heading,message,actionLabel,true);
        }
        bool ShowEmulatorMessage(Window owner,string title,string heading,string message,string actionLabel=null,bool destructive=false) {
            var confirmation=new Window {
                Title=title, Owner=owner, Width=420,
                SizeToContent=SizeToContent.Height, ResizeMode=ResizeMode.NoResize,
                WindowStartupLocation=WindowStartupLocation.CenterOwner, ShowInTaskbar=false,
                Background=owner.Background, Foreground=white, FontFamily=owner.FontFamily
            };
            confirmation.Resources.MergedDictionaries.Add(owner.Resources);
            var panel=new StackPanel {Margin=new Thickness(24)};
            confirmation.Content=panel;
            panel.Children.Add(Label(heading,20,white));
            var detail=Label(message,13,muted);
            detail.TextWrapping=TextWrapping.Wrap;
            detail.Margin=new Thickness(0,10,0,20);
            panel.Children.Add(new ScrollViewer {Content=detail,MaxHeight=300,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            var cancel=new Button {Content=actionLabel==null ? "Close" : "Cancel",IsCancel=true,IsDefault=true,Padding=new Thickness(14,7,14,7)};
            actions.Children.Add(cancel);
            if(actionLabel!=null) {
                var action=new Button {Content=actionLabel,Padding=new Thickness(14,7,14,7),Margin=new Thickness(8,0,0,0)};
                if(destructive) {action.Background=new SolidColorBrush(Color.FromRgb(118,47,53)); action.Foreground=Brushes.White;}
                action.Click+=(sender,args) => confirmation.DialogResult=true;
                actions.Children.Add(action);
            }
            panel.Children.Add(actions);
            confirmation.Loaded+=(sender,args) => cancel.Focus();
            return confirmation.ShowDialog()==true;
        }
        StackPanel CreateEmulatorsPanel(Window dialog,Action rebuild) {
            var root=new StackPanel();
            var heading=Label("Emulators",26,white); heading.FontWeight=FontWeights.SemiBold; root.Children.Add(heading);
            var settingsPanel=new StackPanel();
            var gamesPanel=new StackPanel();
            var tabs=new TabControl {Name="EmulatorTabs",Background=dialog.Background,Foreground=white,Margin=new Thickness(0,12,0,0)};
            StyleSettingsSubtabs(tabs);
            tabs.Items.Add(new TabItem {Header="Settings",Content=settingsPanel});
            tabs.Items.Add(new TabItem {Header="Games",Content=gamesPanel});
            tabs.SelectedIndex=emulatorTabIndex;
            tabs.SelectionChanged+=(sender,args) => {
                if(args.Source==tabs && tabs.SelectedIndex>=0) emulatorTabIndex=tabs.SelectedIndex;
            };
            root.Children.Add(tabs);
            var importAll=new Button {Content="Import all emulator libraries…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,12,0,0)};
            gamesPanel.Children.Add(importAll);
            var importAllNote=Label("Configure your emulators and their game libraries first so Pad Launcher can import them.",12,muted);
            importAllNote.TextWrapping=TextWrapping.Wrap;
            importAllNote.Margin=new Thickness(0,8,0,0);
            gamesPanel.Children.Add(importAllNote);
            importAll.Click+=async (s,e) => {
                if(scanning || !ShowEmulatorMessage(dialog,"Import emulator libraries","Import all emulator libraries?",
                    "Import games from saved or detected emulator libraries. Use Scan subfolders to import from a ROM folder separately.","Import all")) return;
                var snapshot=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                var warnings=new List<string>();
                dialog.IsEnabled=false;
                try {
                    var batches=await Task.Run(() => EmulatorImport.ReadAllEmulatorGames(snapshot,warnings));
                    if(!dialog.IsLoaded) return;
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    int added=0;
                    foreach(var batch in batches) {
                        var config=EmulatorLibrary.Config(next,batch.Key);
                        int before=config.Games==null ? 0 : config.Games.Count;
                        EmulatorImport.AddEntries(next,batch.Key,config.Path,batch.Value);
                        added+=config.Games.Count-before;
                        EnableEmulatorList(next,batch.Key);
                    }
                    if(batches.Count>0) CommitEmulators(next,rebuild);
                    dialog.IsEnabled=true;
                    string summary=added==0 ? "No new games were found in the emulator libraries." :
                        added==1 ? "1 new game was imported." : added+" new games were imported.";
                    ShowEmulatorMessage(dialog,"Import emulator libraries","Import complete",summary+
                        (warnings.Count==0 ? "" : "\n\n"+String.Join("\n",warnings)));
                } catch(Exception error) {
                    if(dialog.IsLoaded) {dialog.IsEnabled=true; ShowEmulatorMessage(dialog,"Import emulator libraries","Import failed",error.Message);}
                } finally {dialog.IsEnabled=true;}
            };
            foreach(var preset in EmulatorLibrary.Presets) {
                string id=preset.Id; var config=EmulatorLibrary.Config(settings,id);
                var section=new Expander {Name="EmulatorSection_"+id,Header=preset.Name+" · "+preset.System,Foreground=white,FontSize=14,Margin=new Thickness(0,14,0,0),IsExpanded=openEmulatorSections.Contains(id)};
                section.Expanded+=(s,e) => openEmulatorSections.Add(id); section.Collapsed+=(s,e) => openEmulatorSections.Remove(id);
                var panel=new StackPanel {Margin=new Thickness(12,8,12,12)}; section.Content=panel; settingsPanel.Children.Add(section);
                Func<string,string,TextBox> field=(caption,fieldName) => {
                    var label=Label(caption,12,mint); label.Margin=new Thickness(0,12,0,8); panel.Children.Add(label);
                    var box=new TextBox {Name=fieldName,Padding=new Thickness(8)}; panel.Children.Add(box); return box;
                };
                var path=field("Emulator executable","EmulatorPath_"+id); path.Text=config==null ? "" : config.Path ?? "";
                TextBox defaultCore=null, romFolder=null;
                var pathActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)}; panel.Children.Add(pathActions);
                var browse=new Button {Content="Browse…",Padding=new Thickness(12,7,12,7)};
                var savePath=new Button {Name="EmulatorSavePath_"+id,Content="Save path",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
                pathActions.Children.Add(browse); pathActions.Children.Add(savePath);
                var status=Label("",12,muted); status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,8,0,0); panel.Children.Add(status);
                savePath.Click+=(s,e) => {try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SavePath(next,id,path.Text); EmulatorLibrary.Config(next,id).RomFolder=romFolder.Text.Trim(); CommitEmulators(next,rebuild);
                } catch(Exception error) {status.Text=error.Message;}};
                browse.Click+=(s,e) => {
                    var file=new Microsoft.Win32.OpenFileDialog {Filter=preset.Name+" executable|*.exe",CheckFileExists=true};
                    if(file.ShowDialog(dialog)==true) {path.Text=file.FileName; savePath.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                };
                if(id=="retroarch") {
                    defaultCore=field("Default core for new games","EmulatorDefaultCore"); defaultCore.Text=config==null ? "" : config.CorePath ?? "";
                    var coreChoices=new SettingsComboBox {Name="EmulatorInstalledCores",DisplayMemberPath="Value",SelectedValuePath="Key",Padding=new Thickness(8),Margin=new Thickness(0,8,0,0)}; panel.Children.Add(coreChoices);
                    Action findCores=() => {coreChoices.ItemsSource=EmulatorLibrary.Cores(path.Text).Select(p => new KeyValuePair<string,string>(p,System.IO.Path.GetFileNameWithoutExtension(p))).ToList();}; findCores();
                    coreChoices.SelectionChanged+=(s,e) => {if(coreChoices.SelectedValue is string) defaultCore.Text=(string)coreChoices.SelectedValue;};
                    var coreActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)}; panel.Children.Add(coreActions);
                    var browseCore=new Button {Content="Browse core…",Padding=new Thickness(12,7,12,7)};
                    var findCore=new Button {Content="Find installed cores",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
                    var saveCore=new Button {Name="EmulatorSaveCore",Content="Save core",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
                    coreActions.Children.Add(browseCore); coreActions.Children.Add(findCore); coreActions.Children.Add(saveCore);
                    browseCore.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="RetroArch core|*.dll",CheckFileExists=true}; if(file.ShowDialog(dialog)==true) defaultCore.Text=file.FileName;};
                    findCore.Click+=(s,e) => {findCores(); status.Text=coreChoices.Items.Count==0 ? "No cores found next to this executable. Browse to a core installed in RetroArch." : "Choose an installed core above.";};
                    saveCore.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SavePath(next,id,path.Text); EmulatorLibrary.Config(next,id).RomFolder=romFolder.Text.Trim(); EmulatorLibrary.SaveCore(next,defaultCore.Text); CommitEmulators(next,rebuild);} catch(Exception error) {status.Text=error.Message;}};
                }
                var globalArguments=field("Launch arguments (all games)","EmulatorArguments_"+id);
                globalArguments.Text=config==null || config.Arguments==null ? EmulatorLibrary.DefaultArguments(id) : config.Arguments;
                string argumentHelp="Use {game} for the quoted game path.";
                if(id=="retroarch") argumentHelp+=" Use {core} for the RetroArch core.";
                if(id=="vita3k") argumentHelp+=" Use {title} for the Vita3K title ID.";
                var argumentHint=Label(argumentHelp+" Per-game arguments containing {game} override this field. Other per-game arguments are appended.",12,muted);
                argumentHint.TextWrapping=TextWrapping.Wrap; argumentHint.Margin=new Thickness(0,8,0,8); panel.Children.Add(argumentHint);
                var argumentActions=new StackPanel {Orientation=Orientation.Horizontal};
                var saveArguments=new Button {Content="Save arguments",Padding=new Thickness(12,7,12,7)};
                var resetArguments=new Button {Content="Restore defaults",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
                saveArguments.Click+=(s,e) => {try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    EmulatorLibrary.SavePath(next,id,path.Text);
                    EmulatorLibrary.Config(next,id).Arguments=globalArguments.Text.Trim();
                    CommitEmulators(next,rebuild);
                } catch(Exception error) {status.Text=error.Message;}};
                resetArguments.Click+=(s,e) => globalArguments.Text=EmulatorLibrary.DefaultArguments(id);
                argumentActions.Children.Add(saveArguments); argumentActions.Children.Add(resetArguments); panel.Children.Add(argumentActions);
                var gamesSection=new Expander {Name="EmulatorGamesSection_"+id,Header=preset.Name+" · "+preset.System,Foreground=white,FontSize=14,Margin=new Thickness(0,14,0,0),IsExpanded=openEmulatorGameSections.Contains(id)};
                gamesSection.Expanded+=(sender,args) => openEmulatorGameSections.Add(id);
                gamesSection.Collapsed+=(sender,args) => openEmulatorGameSections.Remove(id);
                panel=new StackPanel {Margin=new Thickness(12,8,12,12)};
                gamesSection.Content=panel;
                gamesPanel.Children.Add(gamesSection);
                var gameStatus=Label("",12,muted);
                gameStatus.TextWrapping=TextWrapping.Wrap;
                var importLibrary=new Button {Name="ImportEmulatorLibrary_"+id,Content="Import library from emulator…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,10,0,0),HorizontalAlignment=HorizontalAlignment.Left};
                importLibrary.Click+=(s,e) => {try {ShowEmulatorImport(dialog,preset,path.Text,defaultCore==null ? null : defaultCore.Text,rebuild);} catch(Exception error) {gameStatus.Text=error.Message;}};
                romFolder=field(id=="vita3k" ? "Installed games folder (ux0/app)" : "ROM folder (includes subfolders)","EmulatorRomFolder_"+id); romFolder.Text=config==null ? "" : config.RomFolder ?? "";
                var folderActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)}; panel.Children.Add(folderActions);
                var browseFolder=new Button {Content="Browse folder…",Padding=new Thickness(12,7,12,7)};
                var scan=new Button {Name="EmulatorScan_"+id,Content="Scan subfolders",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
                var cancelScan=new Button {Content="Cancel",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),Visibility=Visibility.Collapsed};
                folderActions.Children.Add(browseFolder); folderActions.Children.Add(scan); folderActions.Children.Add(cancelScan);
                browseFolder.Click+=(s,e) => {using(var folder=new System.Windows.Forms.FolderBrowserDialog {Description="Choose the "+preset.Name+" games folder",SelectedPath=romFolder.Text}) {if(folder.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK) romFolder.Text=folder.SelectedPath;}};
                var scanStatus=Label("",12,muted); scanStatus.TextWrapping=TextWrapping.Wrap; scanStatus.Margin=new Thickness(0,14,0,0); panel.Children.Add(scanStatus);
                var scanDetails=new Expander {Header="Folders that could not be read",Foreground=muted,Margin=new Thickness(0,8,0,0),Visibility=Visibility.Collapsed};
                var scanErrors=Label("",12,muted); scanErrors.TextWrapping=TextWrapping.Wrap;
                scanDetails.Content=new ScrollViewer {Content=scanErrors,MaxHeight=180,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
                panel.Children.Add(scanDetails);
                CancellationTokenSource scanCancellation=null; cancelScan.Click+=(s,e) => {if(scanCancellation!=null) scanCancellation.Cancel();};
                EventHandler onClosed=(s,e) => {if(scanCancellation!=null) scanCancellation.Cancel();}; dialog.Closed+=onClosed;
                // Rebuilt sections release their close handler along with their cancellation source.
                section.Unloaded+=(s,e) => {if(scanCancellation!=null) scanCancellation.Cancel(); dialog.Closed-=onClosed;};
                scan.Click+=async (s,e) => {
                    try {
                        if(scanning) throw new InvalidOperationException("Please wait for the current library refresh to finish.");
                        string executable=EmulatorLibrary.Executable(path.Text),folder=EmulatorLibrary.Folder(romFolder.Text);
                        var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SavePath(next,id,executable); EmulatorLibrary.Config(next,id).RomFolder=folder;
                        if(id=="retroarch") {EmulatorLibrary.SaveCore(next,defaultCore.Text); EmulatorLibrary.Core(defaultCore.Text); }
                        Storage.Write("settings.json",next); settings=next;
                        scanCancellation=new CancellationTokenSource(); var token=scanCancellation.Token;
                        scan.IsEnabled=false; cancelScan.Visibility=Visibility.Visible; scanStatus.Text="Scanning subfolders…";
                        scanDetails.Visibility=Visibility.Collapsed; scanErrors.Text="";
                        string scanCore=id=="retroarch" ? defaultCore.Text : null;
                        var result=await Task.Run(() => EmulatorLibrary.Scan(id,folder,token,scanCore)); token.ThrowIfCancellationRequested();
                        var existing=new HashSet<string>((EmulatorLibrary.Config(settings,id).Games ?? new List<EmulatorGame>()).Select(g => g.Path),StringComparer.OrdinalIgnoreCase);
                        var found=result.Paths.Where(p => !existing.Contains(p)).ToList();
                        scanErrors.Text=String.Join("\n",result.ReadErrors);
                        scanDetails.Visibility=result.ReadErrors.Count>0 ? Visibility.Visible : Visibility.Collapsed;
                        if(found.Count==0) {scanStatus.Text="No new games found."+(result.UnreadableFolders>0 ? " Some folders could not be read." : ""); return;}
                        string destination,newListName;
                        var chosen=ChooseEmulatorGames(dialog,preset,found,result.UnreadableFolders,out destination,out newListName,result.ReadErrors);
                        if(chosen!=null && chosen.Count>0) {
                            next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.AddGames(next,id,chosen.Keys,chosen);
                            if(!String.IsNullOrEmpty(destination)) {
                                var working=new Library {Games=library.Games.ToList(),Collections=library.Collections.ToList()};
                                EmulatorLibrary.Apply(working,next);
                                var selectedPaths=new HashSet<string>(chosen.Keys,StringComparer.OrdinalIgnoreCase);
                                var gameIds=EmulatorLibrary.Config(next,id).Games.Where(game => selectedPaths.Contains(game.Path)).Select(game => game.Id);
                                var destinationList=(next.CustomLists ?? new List<CustomList>()).FirstOrDefault(list => list.Id==destination);
                                if(destination!="new" && destinationList==null) throw new InvalidOperationException("The selected list no longer exists.");
                                CustomLists.Save(next,working,destinationList==null ? null : destinationList.Id,destinationList==null ? newListName : destinationList.Name,
                                    (destinationList==null ? Enumerable.Empty<string>() : destinationList.Games ?? new List<string>()).Concat(gameIds));
                            }
                            EnableEmulatorList(next,id); CommitEmulators(next,rebuild);
                        } else scanStatus.Text="No games added.";
                    } catch(OperationCanceledException) {scanStatus.Text="Scan cancelled.";} catch(Exception error) {scanStatus.Text=error.Message;}
                    finally {scan.IsEnabled=true; cancelScan.Visibility=Visibility.Collapsed; if(scanCancellation!=null) {scanCancellation.Dispose(); scanCancellation=null;}}
                };
                panel.Children.Add(importLibrary);
                panel.Children.Add(gameStatus);
                var hint=Label(preset.Hint,12,muted); hint.TextWrapping=TextWrapping.Wrap; hint.Margin=new Thickness(0,14,0,8); panel.Children.Add(hint);
                var addActions=new StackPanel {Orientation=Orientation.Horizontal}; panel.Children.Add(addActions);
                Action<IEnumerable<string>> add=paths => {try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SavePath(next,id,path.Text); EmulatorLibrary.Config(next,id).RomFolder=romFolder.Text.Trim();
                    if(id=="retroarch") EmulatorLibrary.SaveCore(next,defaultCore.Text);
                    int added=EmulatorLibrary.AddGames(next,id,paths); if(added==0) {gameStatus.Text="These games are already added."; return;} EnableEmulatorList(next,id); CommitEmulators(next,rebuild);
                } catch(Exception error) {gameStatus.Text=error.Message;}};
                var addFiles=new Button {Name="EmulatorAddGames_"+id,Content="Add game manually…",Padding=new Thickness(12,7,12,7)}; addActions.Children.Add(addFiles);
                addFiles.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter=preset.Filter,Multiselect=true,CheckFileExists=true}; if(file.ShowDialog(dialog)==true) add(file.FileNames);};
                if(id=="rpcs3" || id=="vita3k") {
                    var addFolder=new Button {Content="Add game folder…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; addActions.Children.Add(addFolder);
                    addFolder.Click+=(s,e) => {using(var folder=new System.Windows.Forms.FolderBrowserDialog {Description="Choose one installed "+preset.Name+" game folder"}) {if(folder.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK) add(new[]{folder.SelectedPath});}};
                }
                var games=(config==null ? new List<EmulatorGame>() : config.Games ?? new List<EmulatorGame>()).OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                var gameLabel=Label("Added games ("+games.Count+")",14,mint); gameLabel.Margin=new Thickness(0,20,0,8); panel.Children.Add(gameLabel);
                if(games.Count==0) {panel.Children.Add(Label("No games added yet.",12,muted)); continue;}
                var gamePicker=new SettingsComboBox {Name="EmulatorGames_"+id,ItemsSource=games,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedIndex=0,Padding=new Thickness(8)}; panel.Children.Add(gamePicker);
                var name=field("Game name","EmulatorGameName_"+id); name.MaxLength=120;
                var target=field(id=="vita3k" ? "Installed title folder" : "Game path","EmulatorGamePath_"+id);
                var arguments=field("Launch arguments (optional)", "EmulatorGameArguments_"+id);
                arguments.ToolTip="Appended to the defaults. Include {game} to supply the full command arguments. RetroArch also supports {core}.";
                TextBox gameCore=null;
                if(id=="retroarch") {
                    gameCore=field("Core for this game","EmulatorGameCore");
                    var browseGameCore=new Button {Content="Browse core…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,8,0,0),HorizontalAlignment=HorizontalAlignment.Left}; panel.Children.Add(browseGameCore);
                    browseGameCore.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="RetroArch core|*.dll",CheckFileExists=true}; if(file.ShowDialog(dialog)==true) gameCore.Text=file.FileName;};
                }
                var missing=Label("",12,muted); missing.TextWrapping=TextWrapping.Wrap; missing.Margin=new Thickness(0,8,0,0); panel.Children.Add(missing);
                Action load=() => {var game=games.FirstOrDefault(g => g.Id==(string)gamePicker.SelectedValue); name.Text=game==null ? "" : game.Name; target.Text=game==null ? "" : game.Path; arguments.Text=game==null ? "" : game.Arguments ?? ""; if(gameCore!=null) gameCore.Text=game==null ? "" : game.CorePath ?? config.CorePath ?? ""; try {EmulatorLibrary.GamePath(id,target.Text); missing.Text="";} catch {missing.Text="Game file missing or unsupported — update its path.";}};
                gamePicker.SelectionChanged+=(s,e) => load(); load();
                var editActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)}; panel.Children.Add(editActions);
                var saveGame=new Button {Name="EmulatorSaveGame_"+id,Content="Save changes",Padding=new Thickness(12,7,12,7)};
                var remove=new Button {Name="EmulatorRemoveGame_"+id,Content="Remove game",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; editActions.Children.Add(saveGame); editActions.Children.Add(remove);
                var removeAll=new Button {Content="Remove all games",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
                editActions.Children.Add(removeAll);
                var orphaned=new Button {Content="Add orphaned games to list…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,12,0,0)};
                panel.Children.Add(orphaned);
                orphaned.Click+=(s,e) => {try {
                    var listed=new HashSet<string>((settings.CustomLists ?? new List<CustomList>()).SelectMany(list => list.Games ?? new List<string>()));
                    var current=EmulatorLibrary.Config(settings,id);
                    var unlisted=(current==null ? new List<EmulatorGame>() : current.Games ?? new List<EmulatorGame>()).Where(game => !listed.Contains(game.Id)).ToList();
                    if(unlisted.Count==0) {ShowEmulatorMessage(dialog,"Orphaned games","No orphaned games", "All "+preset.Name+" games already belong to a custom list."); return;}
                    string destination,newListName;
                    var chosen=ChooseEmulatorGames(dialog,preset,unlisted.Select(game => game.Path).ToList(),0,out destination,out newListName,null,true);
                    if(chosen==null || chosen.Count==0) return;
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    var selectedPaths=new HashSet<string>(chosen.Keys,StringComparer.OrdinalIgnoreCase);
                    var gameIds=unlisted.Where(game => selectedPaths.Contains(game.Path)).Select(game => game.Id);
                    var targetList=(next.CustomLists ?? new List<CustomList>()).FirstOrDefault(list => list.Id==destination);
                    if(destination!="new" && targetList==null) throw new InvalidOperationException("The selected list no longer exists.");
                    CustomLists.Save(next,library,targetList==null ? null : targetList.Id,targetList==null ? newListName : targetList.Name,
                        (targetList==null ? Enumerable.Empty<string>() : targetList.Games ?? new List<string>()).Concat(gameIds));
                    CommitEmulators(next,rebuild);
                } catch(Exception error) {missing.Text=error.Message;}};
                removeAll.Click+=(s,e) => {
                    if(!ConfirmEmulatorRemoval(dialog,"Remove "+preset.Name+" games","Remove all "+preset.Name+" games?",games.Count+" "+(games.Count==1 ? "game will" : "games will")+" be removed from Pad Launcher. Your game files stay on disk.","Remove all")) return;
                    try {
                        var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                        foreach(var game in games) EmulatorLibrary.RemoveGame(next,id,game.Id);
                        CommitEmulators(next,rebuild);
                    } catch(Exception error) {missing.Text=error.Message;}
                };
                saveGame.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SaveGame(next,id,(string)gamePicker.SelectedValue,name.Text,target.Text,gameCore==null ? null : gameCore.Text,arguments.Text); CommitEmulators(next,rebuild);} catch(Exception error) {missing.Text=error.Message;}};
                remove.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.RemoveGame(next,id,(string)gamePicker.SelectedValue); CommitEmulators(next,rebuild);} catch(Exception error) {missing.Text=error.Message;}};
            }
            return root;
        }
        static void EnableEmulatorList(Settings settings,string id) {
            string list="source:emulator:"+id; if(settings.EnabledCollectionIds!=null && !settings.EnabledCollectionIds.Contains(list)) settings.EnabledCollectionIds.Add(list);
        }
        Dictionary<string,string> ChooseEmulatorGames(Window owner,EmulatorPreset preset,List<string> paths,int unreadableFolders,out string destination,out string newListName,List<string> readErrors=null,bool orphaned=false) {
            var dialog=new Window {Title="Add "+preset.Name+" games",Owner=owner,Width=760,Height=650,MinWidth=500,MinHeight=400,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,Topmost=true}; dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var heading=Label(paths.Count+(orphaned ? " orphaned games" : " new games found"),22,white); heading.Margin=new Thickness(0,0,0,12); DockPanel.SetDock(heading,Dock.Top); root.Children.Add(heading);
            var note=Label("Review the detected files before adding."+(preset.Id=="eden" ? " Deselect updates and DLC; only add base games." : "")+(unreadableFolders>0 ? " "+unreadableFolders+" folders could not be read." : ""),12,muted); note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,0,0,12); DockPanel.SetDock(note,Dock.Top); root.Children.Add(note);
            var topActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)}; DockPanel.SetDock(topActions,Dock.Top); root.Children.Add(topActions);
            if(readErrors!=null && readErrors.Count>0) {
                var details=new Button {Content="View unreadable folders…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,0)};
                details.Click+=(s,e) => ShowEmulatorMessage(dialog,"Scan details","Folders that could not be read",String.Join("\n",readErrors));
                topActions.Children.Add(details);
            }
            var all=new Button {Content="Select all",Padding=new Thickness(12,7,12,7)}; var none=new Button {Content="Unselect all",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; topActions.Children.Add(all); topActions.Children.Add(none);
            var bottom=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)}; DockPanel.SetDock(bottom,Dock.Bottom); root.Children.Add(bottom);
            var add=new Button {Content="Add selected",Padding=new Thickness(12,7,12,7)}; var cancel=new Button {Content="Cancel",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; bottom.Children.Add(add); bottom.Children.Add(cancel);
            var listPanel=new StackPanel {Margin=new Thickness(0,12,0,0)};
            DockPanel.SetDock(listPanel,Dock.Bottom); root.Children.Add(listPanel);
            listPanel.Children.Add(Label("Add to list",12,mint));
            var listOptions=new List<KeyValuePair<string,string>> {
                new KeyValuePair<string,string>("","Only the "+preset.Name+" emulator list"),
                new KeyValuePair<string,string>("new","Create a new list")
            };
            if(orphaned) listOptions.RemoveAt(0);
            foreach(var list in settings.CustomLists ?? new List<CustomList>()) listOptions.Add(new KeyValuePair<string,string>(list.Id,list.Name));
            var listPicker=new SettingsComboBox {ItemsSource=listOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=0,Padding=new Thickness(8),Margin=new Thickness(0,8,0,0)};
            listPanel.Children.Add(listPicker);
            var listName=new TextBox {Text=preset.Name+" games",MaxLength=80,Padding=new Thickness(8),Margin=new Thickness(0,8,0,0),Visibility=Visibility.Collapsed,ToolTip="New list name"};
            if(orphaned) listName.Visibility=Visibility.Visible;
            listPanel.Children.Add(listName);
            listPicker.SelectionChanged+=(s,e) => listName.Visibility=(string)listPicker.SelectedValue=="new" ? Visibility.Visible : Visibility.Collapsed;
            var entries=new StackPanel(); var boxes=new Dictionary<string,CheckBox>();
            var names=new Dictionary<string,TextBox>();
            var nameFields=new List<TextBox>();
            foreach(string path in paths) {
                var row=new StackPanel();
                var saved=orphaned ? (EmulatorLibrary.Config(settings,preset.Id).Games ?? new List<EmulatorGame>()).FirstOrDefault(game => String.Equals(game.Path,path,StringComparison.OrdinalIgnoreCase)) : null;
                var name=new TextBox {Text=saved==null ? EmulatorLibrary.SuggestedName(preset.Id,path) : saved.Name,MaxLength=120,ToolTip="Game name",MinWidth=240,IsReadOnly=orphaned};
                names[path]=name;
                nameFields.Add(name);
                EnableGameNameNavigation(name,nameFields);
                row.Children.Add(name);
                var caption=Label(path,11,muted);
                caption.TextWrapping=TextWrapping.Wrap;
                row.Children.Add(caption);
                var box=new CheckBox {Content=row,IsChecked=true,Margin=new Thickness(0,7,0,7),HorizontalContentAlignment=HorizontalAlignment.Stretch};
                boxes[path]=box;
                entries.Children.Add(box);
            }
            all.Click+=(s,e) => {foreach(var box in boxes.Values) box.IsChecked=true;}; none.Click+=(s,e) => {foreach(var box in boxes.Values) box.IsChecked=false;};
            Dictionary<string,string> result=null;
            add.Click+=(s,e) => {
                if((string)listPicker.SelectedValue=="new" && String.IsNullOrWhiteSpace(listName.Text)) {listName.Focus(); return;}
                result=boxes.Where(p => p.Value.IsChecked==true).ToDictionary(p => p.Key,p => names[p.Key].Text,StringComparer.OrdinalIgnoreCase); dialog.DialogResult=true;
            }; cancel.Click+=(s,e) => dialog.Close();
            root.Children.Add(new ScrollViewer {Content=entries,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            dialog.ShowDialog(); destination=listPicker.SelectedValue as string; newListName=listName.Text.Trim(); return result;
        }
    }
}
