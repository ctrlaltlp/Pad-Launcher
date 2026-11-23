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
        void ShowRelocateGames(Window owner,EmulatorPreset preset,string id,Action rebuild) {
            var config=EmulatorLibrary.Config(settings,id);
            var games=config==null || config.Games==null ? new List<EmulatorGame>() : config.Games.Where(game => game!=null).ToList();
            if(games.Count==0) {ThemedMessage.Show(owner,"There are no saved games to relocate.","Relocate games"); return;}
            var dialog=new Window {Title="Relocate "+preset.Name+" games",Owner=owner,Width=920,Height=700,MinWidth=680,MinHeight=520,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label("Relocate "+preset.Name+" games",22,white));
            var explanation=Label("Update saved game paths after moving or renaming a folder. Game names, per-game cores, lists, favourites, artwork and play history stay attached.",12,muted);
            explanation.TextWrapping=TextWrapping.Wrap; explanation.Margin=new Thickness(0,10,0,16); header.Children.Add(explanation);
            var oldCaption=Label("Old games folder",12,mint); oldCaption.Margin=new Thickness(0,0,0,6); header.Children.Add(oldCaption);
            var oldRoot=new TextBox {Name="RelocateGamesOldRoot",Padding=new Thickness(8)};
            var broken=games.Where(game => EmulatorLibrary.ConfirmMissingRom(game.Path)).ToList();
            oldRoot.Text=EmulatorLibrary.SuggestedRelocationRoot(broken.Count>0 ? broken : games);
            header.Children.Add(oldRoot);
            var newCaption=Label("New games folder",12,mint); newCaption.Margin=new Thickness(0,12,0,6); header.Children.Add(newCaption);
            var newRow=new Grid(); newRow.ColumnDefinitions.Add(new ColumnDefinition()); newRow.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto}); header.Children.Add(newRow);
            var newRoot=new TextBox {Name="RelocateGamesNewRoot",Padding=new Thickness(8)}; newRow.Children.Add(newRoot);
            var browse=new Button {Name="RelocateGamesBrowse",Content="Browse…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; Grid.SetColumn(browse,1); newRow.Children.Add(browse);
            browse.Click+=(s,e) => {using(var folder=new System.Windows.Forms.FolderBrowserDialog {Description="Choose the new "+preset.Name+" games folder",SelectedPath=Directory.Exists(newRoot.Text) ? newRoot.Text : ""}) {if(folder.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK) newRoot.Text=folder.SelectedPath;}};
            var previewRow=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,14,0,8)};
            var preview=new Button {Name="RelocateGamesPreview",Content="Preview paths",Padding=new Thickness(12,7,12,7)}; previewRow.Children.Add(preview);
            var selectAll=new Button {Name="RelocateGamesSelectAll",Content="Select all",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; previewRow.Children.Add(selectAll);
            var selectNone=new Button {Name="RelocateGamesSelectNone",Content="Select none",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; previewRow.Children.Add(selectNone);
            var count=Label("",12,muted); count.Margin=new Thickness(12,7,0,0); previewRow.Children.Add(count);
            header.Children.Add(previewRow);
            var status=Label("Choose the old and new folders, then preview the paths.",12,muted); status.Name="RelocateGamesStatus"; status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,0,0,8); header.Children.Add(status);
            var rowsPanel=new StackPanel(); var scroll=new ScrollViewer {Content=rowsPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,4,0,0)};
            var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
            DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            root.Children.Add(scroll);
            var boxes=new List<KeyValuePair<CheckBox,EmulatorRelocation>>();
            Action refreshCount=() => {
                int selected=boxes.Count(pair => pair.Key.IsChecked==true && pair.Key.IsEnabled);
                int found=boxes.Count(pair => pair.Value.Status=="Found");
                count.Text=selected+" selected · "+found+" ready";
            };
            Action<IEnumerable<EmulatorRelocation>> showRows=rows => {
                rowsPanel.Children.Clear(); boxes.Clear();
                foreach(EmulatorRelocation row in rows) {
                    var box=new CheckBox {Name="RelocateGame_"+boxes.Count,IsChecked=row.Status=="Found",IsEnabled=row.Status=="Found",Margin=new Thickness(0,0,0,10)};
                    var text=new StackPanel();
                    var title=Label(row.Game.Name+"  ·  "+row.Status,13,row.Status=="Found" ? mint : row.Status=="Collision" ? new SolidColorBrush(Color.FromRgb(255,110,110)) : muted);
                    title.TextWrapping=TextWrapping.Wrap; text.Children.Add(title);
                    var detail=Label((String.IsNullOrWhiteSpace(row.RelativePath) ? row.OldPath : row.RelativePath)+"  →  "+(row.NewPath ?? "not found"),11,muted);
                    detail.TextWrapping=TextWrapping.Wrap; detail.Margin=new Thickness(0,3,0,0); detail.IsHitTestVisible=false; text.Children.Add(detail);
                    box.Content=text; box.Checked+=(s,e) => refreshCount(); box.Unchecked+=(s,e) => refreshCount();
                    rowsPanel.Children.Add(box); boxes.Add(new KeyValuePair<CheckBox,EmulatorRelocation>(box,row));
                }
                refreshCount();
            };
            preview.Click+=(s,e) => {
                try {
                    if(String.IsNullOrWhiteSpace(oldRoot.Text) || String.IsNullOrWhiteSpace(newRoot.Text)) throw new ArgumentException("Choose both the old and new game folders.");
                    var rows=EmulatorLibrary.RelocationPreview(settings,id,oldRoot.Text,newRoot.Text);
                    showRows(rows);
                    int found=rows.Count(row => row.Status=="Found");
                    status.Text=rows.Count==0 ? "No saved games were found for this emulator." : found==0 ? "No games match a valid file in the new folder. Check both folder paths." : "Review the new paths, then apply the selected games.";
                } catch(Exception error) {status.Text=error.Message;}
            };
            selectAll.Click+=(s,e) => {foreach(var pair in boxes) if(pair.Key.IsEnabled) pair.Key.IsChecked=true; refreshCount();};
            selectNone.Click+=(s,e) => {foreach(var pair in boxes) pair.Key.IsChecked=false; refreshCount();};
            var cancel=new Button {Content="Cancel",IsCancel=true,Padding=new Thickness(14,7,14,7)}; footer.Children.Add(cancel);
            var apply=new Button {Name="RelocateGamesApply",Content="Apply relocation",IsDefault=true,Padding=new Thickness(14,7,14,7),Margin=new Thickness(8,0,0,0)}; footer.Children.Add(apply);
            apply.Click+=(s,e) => {
                try {
                    var selected=boxes.Where(pair => pair.Key.IsChecked==true && pair.Key.IsEnabled).Select(pair => pair.Value).ToList();
                    if(selected.Count==0) throw new ArgumentException("Select at least one game with a valid new path.");
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    EmulatorLibrary.ApplyRelocations(next,id,selected,oldRoot.Text,newRoot.Text);
                    CommitEmulators(next,rebuild);
                    dialog.Close();
                } catch(Exception error) {status.Text=error.Message;}
            };
            dialog.Loaded+=(s,e) => {if(!String.IsNullOrWhiteSpace(oldRoot.Text) && !String.IsNullOrWhiteSpace(newRoot.Text)) preview.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));};
            dialog.ShowDialog();
        }
        StackPanel CreateEmulatorsPanel(Window dialog,Action rebuild) {
            var root=new StackPanel();
            var heading=Label("Emulators",26,white); heading.FontWeight=FontWeights.SemiBold; root.Children.Add(heading);
            var settingsPanel=new StackPanel();
            var gamesPanel=new StackPanel();
            var tabs=new TabControl {Name="EmulatorTabs",Background=dialog.Background,Foreground=white,Margin=new Thickness(0,12,0,0)};
            StyleSettingsSubtabs(tabs);
            tabs.Items.Add(new TabItem {Header="Games",Content=gamesPanel});
            tabs.Items.Add(new TabItem {Header="Settings",Content=settingsPanel});
            tabs.SelectedIndex=emulatorTabIndex;
            tabs.SelectionChanged+=(sender,args) => {
                if(args.Source==tabs && tabs.SelectedIndex>=0) emulatorTabIndex=tabs.SelectedIndex;
            };
            root.Children.Add(tabs);
            AddEmulatorDetectionSettings(settingsPanel,dialog,rebuild);
            var importAll=new Button {Name="ImportEmulatorLibrariesAll",Content="Import all emulator libraries…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,12,0,0)};
            gamesPanel.Children.Add(importAll);
            var importAllNote=Label("Configure your emulators and their game libraries first. Imports may scan game folders configured in the emulators; Pad Launcher's ROM folder setting is not used.",12,muted);
            importAllNote.TextWrapping=TextWrapping.Wrap;
            importAllNote.Margin=new Thickness(0,8,0,0);
            gamesPanel.Children.Add(importAllNote);
            var removeOrphaned=new Button {Content="Remove orphaned games",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,12,0,0)};
            removeOrphaned.Click+=(s,e) => ShowOrphanedEmulatorGames(dialog,rebuild);
            gamesPanel.Children.Add(removeOrphaned);
            var orphanNote=Label("Removes game entries no longer detected on your system. For library comparison, entries not added through libraries are not scanned.",12,muted);
            orphanNote.TextWrapping=TextWrapping.Wrap;
            orphanNote.Margin=new Thickness(0,8,0,0);
            gamesPanel.Children.Add(orphanNote);
            importAll.Click+=(s,e) => {try {
                ShowEmulatorImportAll(dialog,rebuild);
            } catch(Exception error) {ShowEmulatorMessage(dialog,"Import emulator libraries","Import failed",error.Message);}};
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
                TextBox romFolder=null;
                var pathActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)}; panel.Children.Add(pathActions);
                var browse=new Button {Content="Browse…",Padding=new Thickness(12,7,12,7)};
                var savePath=new Button {Name="EmulatorSavePath_"+id,Content="Save path",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
                pathActions.Children.Add(browse); pathActions.Children.Add(savePath);
                var status=Label("",12,muted); status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,8,0,0); panel.Children.Add(status);
                savePath.Click+=(s,e) => {try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SavePath(next,id,path.Text); if(romFolder!=null) EmulatorLibrary.Config(next,id).RomFolder=romFolder.Text.Trim(); CommitEmulators(next,rebuild);
                } catch(Exception error) {status.Text=error.Message;}};
                browse.Click+=(s,e) => {
                    var file=new Microsoft.Win32.OpenFileDialog {Filter=preset.Name+" executable|*.exe",CheckFileExists=true};
                    if(file.ShowDialog(dialog)==true) {path.Text=file.FileName; savePath.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
                };
                var globalArguments=field("Launch arguments (all games)","EmulatorArguments_"+id);
                globalArguments.Text=config==null || config.Arguments==null ? EmulatorLibrary.DefaultArguments(id,path.Text) : config.Arguments;
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
                resetArguments.Click+=(s,e) => globalArguments.Text=EmulatorLibrary.DefaultArguments(id,path.Text);
                if(id=="shadps4") {var qtNote=Label("Qt Launcher uses its selected default emulator version. Install and select a version in its Version Manager before launching games.",12,muted); qtNote.TextWrapping=TextWrapping.Wrap; qtNote.Margin=new Thickness(0,8,0,0); panel.Children.Add(qtNote);}
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
                importLibrary.Click+=(s,e) => {try {string fallbackCore=id=="retroarch" ? null : config==null ? null : config.CorePath; ShowEmulatorImport(dialog,preset,path.Text,fallbackCore,rebuild);} catch(Exception error) {gameStatus.Text=error.Message;}};
                if(id!="retroarch") {
                    var hint=Label(preset.Hint,12,muted); hint.TextWrapping=TextWrapping.Wrap; hint.Margin=new Thickness(0,12,0,8); hint.Cursor=System.Windows.Input.Cursors.Arrow; hint.IsHitTestVisible=false; panel.Children.Add(hint);
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
                RegisterSettingsCleanup(dialog,() => {if(scanCancellation!=null) scanCancellation.Cancel();});
                section.Unloaded+=(s,e) => {if(scanCancellation!=null) scanCancellation.Cancel();};
                scan.Click+=async (s,e) => {
                    try {
                        if(scanning) throw new InvalidOperationException("Please wait for the current library refresh to finish.");
                        string executable=EmulatorLibrary.Executable(path.Text),folder=EmulatorLibrary.Folder(romFolder.Text);
                        var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SavePath(next,id,executable); EmulatorLibrary.Config(next,id).RomFolder=folder;
                        Storage.Write("settings.json",next); settings=next;
                        scanCancellation=new CancellationTokenSource(); var token=scanCancellation.Token;
                        scan.IsEnabled=false; cancelScan.Visibility=Visibility.Visible; scanStatus.Text="Scanning subfolders…";
                        scanDetails.Visibility=Visibility.Collapsed; scanErrors.Text="";
                        var result=await Task.Run(() => EmulatorLibrary.Scan(id,folder,token,null)); token.ThrowIfCancellationRequested();
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
                            CommitEmulators(next,rebuild);
                        } else scanStatus.Text="No games added.";
                    } catch(OperationCanceledException) {scanStatus.Text="Scan cancelled.";} catch(Exception error) {scanStatus.Text=error.Message;}
                    finally {scan.IsEnabled=true; cancelScan.Visibility=Visibility.Collapsed; if(scanCancellation!=null) {scanCancellation.Dispose(); scanCancellation=null;}}
                };
                }
                panel.Children.Add(importLibrary);
                panel.Children.Add(gameStatus);
                var addActions=new StackPanel {Orientation=Orientation.Horizontal}; panel.Children.Add(addActions);
                string manualListId=null;
                Action<IEnumerable<string>> add=paths => {try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SavePath(next,id,path.Text); if(romFolder!=null) EmulatorLibrary.Config(next,id).RomFolder=romFolder.Text.Trim();
                    int added=EmulatorLibrary.AddGames(next,id,paths); if(added==0) {gameStatus.Text="These games are already added."; return;} CommitEmulators(next,rebuild);
                } catch(Exception error) {gameStatus.Text=error.Message;}};
                var addFiles=new Button {Name="EmulatorAddGames_"+id,Content="Add game manually…",Padding=new Thickness(12,7,12,7)}; addActions.Children.Add(addFiles);
                var relocateGames=new Button {Name="EmulatorRelocateGames_"+id,Content="Relocate games' directory…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; addActions.Children.Add(relocateGames);
                relocateGames.Click+=(s,e) => {try {ShowRelocateGames(dialog,preset,id,rebuild);} catch(Exception error) {gameStatus.Text=error.Message;}};
                addFiles.Click+=(s,e) => {
                    var file=new Microsoft.Win32.OpenFileDialog {Filter=preset.Filter,Multiselect=true,CheckFileExists=true};
                    if(file.ShowDialog(dialog)!=true) return;
                    if(id!="retroarch") {add(file.FileNames); return;}
                    var selection=ChooseEmulatorGames(dialog,preset,file.FileNames.ToList(),0,null,false,config==null ? null : config.CorePath,manualListId,true,EmulatorLibrary.Executable(path.Text));
                    if(selection==null) return;
                    try {
                        var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                        EmulatorLibrary.SavePath(next,id,path.Text); if(romFolder!=null) EmulatorLibrary.Config(next,id).RomFolder=romFolder.Text.Trim();
                        int added=EmulatorLibrary.AddGames(next,id,selection.Games.Keys,selection.Games,selection.CorePath);
                        if(added==0) {gameStatus.Text="These games are already added."; return;}
                        if(!String.IsNullOrEmpty(selection.Destination)) {
                            var working=new Library {Games=library.Games.ToList(),Collections=library.Collections.ToList()};
                            EmulatorLibrary.Apply(working,next);
                            var selectedPaths=new HashSet<string>(selection.Games.Keys,StringComparer.OrdinalIgnoreCase);
                            var gameIds=EmulatorLibrary.Config(next,id).Games.Where(game => selectedPaths.Contains(game.Path)).Select(game => game.Id).ToList();
                            var destinationList=(next.CustomLists ?? new List<CustomList>()).FirstOrDefault(list => list.Id==selection.Destination);
                            var playlistList=(next.RetroArchPlaylistLists ?? new List<CustomList>()).FirstOrDefault(list => list.Id==selection.Destination);
                            if(selection.Destination!="new" && destinationList==null && playlistList==null) throw new InvalidOperationException("The selected list no longer exists.");
                            if(playlistList!=null) playlistList.Games=(playlistList.Games ?? new List<string>()).Concat(gameIds).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                            else CustomLists.Save(next,working,destinationList==null ? null : destinationList.Id,destinationList==null ? selection.NewListName : destinationList.Name,
                                (destinationList==null ? Enumerable.Empty<string>() : destinationList.Games ?? new List<string>()).Concat(gameIds));
                        }
                        CommitEmulators(next,rebuild);
                    } catch(Exception error) {gameStatus.Text=error.Message;}
                };
                if(id=="rpcs3" || id=="vita3k") {
                    var addFolder=new Button {Content="Add game folder…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; addActions.Children.Add(addFolder);
                    addFolder.Click+=(s,e) => {using(var folder=new System.Windows.Forms.FolderBrowserDialog {Description="Choose one installed "+preset.Name+" game folder"}) {if(folder.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK) add(new[]{folder.SelectedPath});}};
                }
                var games=(config==null ? new List<EmulatorGame>() : config.Games ?? new List<EmulatorGame>()).OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                if(games.Count==0) {var emptyLabel=Label("Added games (0)",14,mint); emptyLabel.Margin=new Thickness(0,20,0,8); panel.Children.Add(emptyLabel); panel.Children.Add(Label("No games added yet.",12,muted)); continue;}
                SettingsComboBox listPicker=null;
                var listGames=new Dictionary<string,List<EmulatorGame>>(StringComparer.OrdinalIgnoreCase);
                var listNames=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
                List<EmulatorGame> visibleGames=games;
                if(id=="retroarch") {
                    var registered=new HashSet<string>(games.Select(game => game.Id),StringComparer.OrdinalIgnoreCase);
                    var gamesById=games.GroupBy(game => game.Id).ToDictionary(group => group.Key,group => group.First(),StringComparer.OrdinalIgnoreCase);
                    var listChoices=new List<KeyValuePair<string,string>>();
                    foreach(var collection in EmulatorLibrary.DefaultListOrder(CombinedCollections.Options(library,settings),library,settings)) {
                        if(collection==null || String.IsNullOrEmpty(collection.Id) || collection.Id=="all" || collection.Id=="unavailable-collections" ||
                            collection.Id=="recent" || collection.Id=="recently-added" || collection.Id=="most-played" || collection.Id=="most-played-lately") continue;
                        var matching=(collection.Games ?? new List<string>()).Where(registered.Contains).Distinct(StringComparer.OrdinalIgnoreCase)
                            .Select(gameId => gamesById[gameId]).OrderBy(game => game.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                        if(matching.Count==0) continue;
                        string count=matching.Count==1 ? "1 game" : matching.Count+" games";
                        listChoices.Add(new KeyValuePair<string,string>(collection.Id,collection.Name+" ("+count+")"));
                        listGames[collection.Id]=matching;
                        listNames[collection.Id]=collection.Name;
                    }
                    if(listChoices.Count==0) {
                        listChoices.Add(new KeyValuePair<string,string>("","All RetroArch games ("+games.Count+" games)"));
                        listGames[""]=games;
                        listNames[""]="All RetroArch games";
                    }
                    var listLabel=Label("List",14,mint); listLabel.Name="EmulatorGameListLabel_"+id; listLabel.Margin=new Thickness(0,20,0,8); panel.Children.Add(listLabel);
                    int selectedList=listChoices.FindIndex(choice => String.Equals(choice.Key,settings.CollectionId,StringComparison.OrdinalIgnoreCase));
                    if(selectedList<0) selectedList=0;
                    listPicker=new SettingsComboBox {Name="EmulatorGameList_"+id,ItemsSource=listChoices,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=selectedList,Padding=new Thickness(8)};
                    panel.Children.Add(listPicker);
                    manualListId=listPicker.SelectedValue as string;
                    List<EmulatorGame> selected;
                    if(listPicker.SelectedValue is string && listGames.TryGetValue((string)listPicker.SelectedValue,out selected)) visibleGames=selected;
                }
                var gameLabel=Label((id=="retroarch" ? "Games in list ("+visibleGames.Count+")" : "Added games ("+games.Count+")"),14,mint); gameLabel.Name="EmulatorGamesLabel_"+id; gameLabel.Margin=new Thickness(0,id=="retroarch" ? 12 : 20,0,8); panel.Children.Add(gameLabel);
                var gamePicker=new SettingsComboBox {Name="EmulatorGames_"+id,ItemsSource=id=="retroarch" ? (System.Collections.IEnumerable)visibleGames.Select(game => new KeyValuePair<string,string>(game.Id,game.Name+" · "+GameSourceLabel(new Game {Id=game.Id,Source="emulator",EmulatorId=id,LaunchId=game.Path,ArtworkSystem=game.ArtworkSystem}))).ToList() : visibleGames,DisplayMemberPath=id=="retroarch" ? "Value" : "Name",SelectedValuePath=id=="retroarch" ? "Key" : "Id",SelectedIndex=0,Padding=new Thickness(8)}; panel.Children.Add(gamePicker);
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
                var locateGame=new Button {Name="EmulatorLocateGame_"+id,Content="Locate game file…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,8,0,0)}; panel.Children.Add(locateGame);
                locateGame.Click+=(s,e) => {
                    string chosen=null;
                    if(id=="vita3k" || id=="rpcs3" || id=="shadps4") {
                        using(var folder=new System.Windows.Forms.FolderBrowserDialog {Description="Choose the new "+preset.Name+" game folder",SelectedPath=Directory.Exists(target.Text) ? target.Text : ""})
                            if(folder.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK) chosen=folder.SelectedPath;
                    } else {
                        var file=new Microsoft.Win32.OpenFileDialog {Filter=preset.Filter,CheckFileExists=true};
                        if(file.ShowDialog(dialog)==true) chosen=file.FileName;
                    }
                    if(String.IsNullOrWhiteSpace(chosen)) return;
                    target.Text=chosen;
                    try {target.Text=EmulatorLibrary.GamePath(id,chosen,gameCore==null ? null : gameCore.Text,EmulatorLibrary.Executable(path.Text)); missing.Text="Selected a new path. Click Save changes to apply.";}
                    catch(Exception error) {missing.Text=error.Message;}
                };
                Action load=() => {var game=visibleGames.FirstOrDefault(g => g.Id==(string)gamePicker.SelectedValue); name.Text=game==null ? "" : game.Name; target.Text=game==null ? "" : game.Path; arguments.Text=game==null ? "" : game.Arguments ?? ""; if(gameCore!=null) gameCore.Text=game==null ? "" : game.CorePath ?? config.CorePath ?? ""; missing.Text=EmulatorLibrary.MissingReason(new Game {Id=game==null ? null : game.Id,Source="emulator",EmulatorId=id,LaunchId=target.Text},settings) ?? "";};
                gamePicker.SelectionChanged+=(s,e) => load(); load();
                if(listPicker!=null) listPicker.SelectionChanged+=(s,e) => {
                    if(!(listPicker.SelectedValue is string)) return;
                    List<EmulatorGame> selected;
                    if(!listGames.TryGetValue((string)listPicker.SelectedValue,out selected)) selected=games;
                    visibleGames=selected;
                    manualListId=listPicker.SelectedValue as string;
                    gameLabel.Text="Games in list ("+visibleGames.Count+")";
                    gamePicker.ItemsSource=visibleGames.Select(game => new KeyValuePair<string,string>(game.Id,game.Name+" · "+GameSourceLabel(new Game {Id=game.Id,Source="emulator",EmulatorId=id,LaunchId=game.Path,ArtworkSystem=game.ArtworkSystem}))).ToList();
                    gamePicker.SelectedIndex=visibleGames.Count==0 ? -1 : 0;
                    load();
                };
                var editActions=new Grid {Margin=new Thickness(0,12,0,0)}; panel.Children.Add(editActions);
                editActions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
                editActions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
                var saveGame=new Button {Name="EmulatorSaveGame_"+id,Content="Save changes",Padding=new Thickness(12,7,12,7)};
                var remove=new Button {Name="EmulatorRemoveGame_"+id,Content="Remove game",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; editActions.Children.Add(saveGame); editActions.Children.Add(remove);
                Grid.SetColumn(remove,1);
                var removeAll=new Button {Name="EmulatorRemoveAllGames_"+id,Content=id=="retroarch" ? "Remove all registered games in list…" : "Remove all registered games…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(16,0,0,0),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Foreground=new SolidColorBrush(Color.FromRgb(255,110,110))}; editActions.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)}); Grid.SetColumn(removeAll,2); editActions.Children.Add(removeAll);
                removeAll.Click+=(s,e) => {
                    var removeGames=id=="retroarch" ? visibleGames : games;
                    string listName=null;
                    if(id=="retroarch" && listPicker!=null && listPicker.SelectedValue is string) listNames.TryGetValue((string)listPicker.SelectedValue,out listName);
                    string listPhrase=String.IsNullOrWhiteSpace(listName) ? "this list" : listName;
                    string headingText=id=="retroarch" ? "Remove all "+preset.Name+" games in "+listPhrase+"?" : "Remove all "+preset.Name+" games?";
                    string countText=removeGames.Count==1 ? "1 registered game" : removeGames.Count+" registered games";
                    string message=id=="retroarch" ? "All "+countText+" in "+listPhrase+" will be removed from every Pad Launcher list. Game files, emulator files and artwork stay on disk. You can import the games again later." : "All "+countText+" for this emulator will be removed from every Pad Launcher list. Game files, emulator files and artwork stay on disk. You can import the games again later.";
                    string action=id=="retroarch" ? "Remove all registered games in list" : "Remove all registered games";
                    if(!ConfirmEmulatorRemoval(dialog,"Remove emulator games",headingText,message,action)) return;
                    try {
                        var removedIds=new HashSet<string>(removeGames.Select(entry => entry.Id),StringComparer.OrdinalIgnoreCase);
                        foreach(var cachedGame in library.Games.Where(g => g.Source=="emulator" && g.EmulatorId==id && removedIds.Contains(g.Id))) {ArtworkService.ListCachePath(cachedGame,"sgdb"); LibretroArtwork.CachePath(cachedGame);}
                        var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                        foreach(var entry in removeGames) EmulatorLibrary.RemoveGame(next,id,entry.Id);
                        CommitEmulators(next,rebuild);
                    } catch(Exception error) {missing.Text=error.Message;}
                };

                saveGame.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.SaveGame(next,id,(string)gamePicker.SelectedValue,name.Text,target.Text,gameCore==null ? null : gameCore.Text,arguments.Text); CommitEmulators(next,rebuild);} catch(Exception error) {missing.Text=error.Message;}};
                remove.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.RemoveGame(next,id,(string)gamePicker.SelectedValue); CommitEmulators(next,rebuild);} catch(Exception error) {missing.Text=error.Message;}};
            }
            return root;
        }
        void ShowOrphanedEmulatorGames(Window owner,Action rebuild) {
            var dialog=new Window {Title="Remove orphaned games",Owner=owner,Width=720,Height=600,MinWidth=480,MinHeight=400,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label("Remove orphaned games",22,white));
            var explanation=Label("Review entries to remove from Pad Launcher. Game files stay on disk. Unavailable drives and folders are skipped.",12,muted);
            explanation.TextWrapping=TextWrapping.Wrap; explanation.Margin=new Thickness(0,10,0,12); header.Children.Add(explanation);
            var includeAbsent=new CheckBox {Content="Also include games no longer detected in emulator libraries",Margin=new Thickness(0,0,0,8)};
            header.Children.Add(includeAbsent);
            var modeNote=Label("Unchecked: find missing ROM files only. Checked: also check games previously imported from emulator libraries. Manually added games are kept when their files exist.",12,muted);
            modeNote.TextWrapping=TextWrapping.Wrap; header.Children.Add(modeNote);
            var scan=new Button {Content="Find orphaned games",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,12,0,8)};
            header.Children.Add(scan);
            var status=Label("",12,muted); status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,0,0,8); header.Children.Add(status);
            var footer=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)};
            DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var remove=new Button {Content="Remove selected",IsEnabled=false,Padding=new Thickness(12,7,12,7)};
            var close=new Button {Content="Close",IsCancel=true,Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            close.Click+=(s,e) => dialog.Close(); footer.Children.Add(remove); footer.Children.Add(close);
            var rows=new StackPanel(); root.Children.Add(new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var boxes=new Dictionary<OrphanedEmulatorGame,CheckBox>();
            var lifetime=new CancellationTokenSource(); bool closed=false,busy=false;
            dialog.Closed+=(s,e) => {closed=true; lifetime.Cancel(); if(!busy) lifetime.Dispose();};
            includeAbsent.Click+=(s,e) => {rows.Children.Clear(); boxes.Clear(); remove.IsEnabled=false; status.Text="Find orphaned games again to use this choice.";};
            scan.Click+=async (s,e) => {
                if(scanning) {status.Text="Please wait for the library refresh to finish."; return;}
                busy=true; scan.IsEnabled=false; includeAbsent.IsEnabled=false; remove.IsEnabled=false; rows.Children.Clear(); boxes.Clear(); status.Text="Checking saved emulator games…";
                try {
                    var snapshot=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    var warnings=new List<string>(); bool checkLibraries=includeAbsent.IsChecked==true; var token=lifetime.Token;
                    var found=await Task.Run(() => EmulatorLibrary.FindOrphanedGames(snapshot,checkLibraries,token,warnings),token);
                    if(closed) return;
                    foreach(var game in found) {
                        var details=new StackPanel();
                        var name=Label(game.Name+" · "+EmulatorLibrary.Preset(game.EmulatorId).Name,14,mint); name.TextWrapping=TextWrapping.Wrap; details.Children.Add(name);
                        var path=Label(game.Path+"\n"+game.Reason,12,muted); path.TextWrapping=TextWrapping.Wrap; details.Children.Add(path);
                        var box=new CheckBox {Content=details,IsChecked=true,Margin=new Thickness(0,7,0,7)};
                        box.Checked+=(a,b) => remove.IsEnabled=boxes.Values.Any(value => value.IsChecked==true);
                        box.Unchecked+=(a,b) => remove.IsEnabled=boxes.Values.Any(value => value.IsChecked==true);
                        boxes.Add(game,box); rows.Children.Add(box);
                    }
                    if(warnings.Count>0) {
                        var notes=Label(String.Join("\n",warnings),12,muted); notes.TextWrapping=TextWrapping.Wrap;
                        rows.Children.Add(new Expander {Header="Detection notes",Content=notes,Foreground=muted,Margin=new Thickness(0,10,0,0)});
                    }
                    status.Text=found.Count==0 ? "No orphaned games found." : found.Count+" entries found. Review the selection before removing.";
                    remove.IsEnabled=found.Count>0;
                } catch(OperationCanceledException) {}
                catch(Exception error) {if(!closed) status.Text=error.Message;}
                finally {busy=false; if(closed) lifetime.Dispose(); else {scan.IsEnabled=true; includeAbsent.IsEnabled=true;}}
            };
            remove.Click+=(s,e) => {
                try {
                    if(scanning) throw new InvalidOperationException("Please wait for the library refresh to finish.");
                    var selected=boxes.Where(pair => pair.Value.IsChecked==true).Select(pair => pair.Key).ToList();
                    if(selected.Count==0 || !ConfirmEmulatorRemoval(dialog,"Remove orphaned games","Remove selected entries?",selected.Count+" entries will be removed from Pad Launcher. Game files stay on disk.","Remove")) return;
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    int removed=0;
                    foreach(var game in selected) {
                        var config=EmulatorLibrary.Config(next,game.EmulatorId);
                        if(config==null || !(config.Games ?? new List<EmulatorGame>()).Any(entry => entry.Id==game.Id && entry.Path==game.Path)) continue;
                        if(game.Reason=="ROM file is missing" && !EmulatorLibrary.ConfirmMissingRom(game.Path)) continue;
                        EmulatorLibrary.RemoveGame(next,game.EmulatorId,game.Id); removed++;
                    }
                    CommitEmulators(next,rebuild); boxes.Clear(); rows.Children.Clear(); remove.IsEnabled=false; status.Text=removed+" entries removed.";
                } catch(Exception error) {status.Text=error.Message;}
            };
            dialog.ShowDialog();
        }
        sealed class EmulatorGameSelection {
            public Dictionary<string,string> Games;
            public string Destination;
            public string NewListName;
            public string CorePath;
        }
        Dictionary<string,string> ChooseEmulatorGames(Window owner,EmulatorPreset preset,List<string> paths,int unreadableFolders,out string destination,out string newListName,List<string> readErrors=null,bool orphaned=false) {
            var choice=ChooseEmulatorGames(owner,preset,paths,unreadableFolders,readErrors,orphaned,null,null,false,null);
            destination=choice==null ? null : choice.Destination;
            newListName=choice==null ? "" : choice.NewListName;
            return choice==null ? null : choice.Games;
        }
        EmulatorGameSelection ChooseEmulatorGames(Window owner,EmulatorPreset preset,List<string> paths,int unreadableFolders,List<string> readErrors,bool orphaned,string defaultCore,string defaultDestination,bool chooseCore,string executable) {
            var dialog=new Window {Title="Add "+preset.Name+" games",Owner=owner,Width=760,Height=650,MinWidth=500,MinHeight=400,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,Topmost=true}; dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var heading=Label(paths.Count+(orphaned ? " orphaned " : " new ")+(paths.Count==1 ? "game" : "games")+(orphaned ? "" : " found"),22,white); heading.Margin=new Thickness(0,0,0,12); DockPanel.SetDock(heading,Dock.Top); root.Children.Add(heading);
            var note=Label((chooseCore ? "Choose a core and a destination list before adding." : "Review the detected files before adding.")+(preset.Id=="eden" ? " Deselect updates and DLC; only add base games." : "")+(unreadableFolders>0 ? " "+unreadableFolders+" folders could not be read." : ""),12,muted); note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,0,0,12); DockPanel.SetDock(note,Dock.Top); root.Children.Add(note);
            var topActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,12)}; DockPanel.SetDock(topActions,Dock.Top); root.Children.Add(topActions);
            if(readErrors!=null && readErrors.Count>0) {
                var details=new Button {Content="View unreadable folders…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,0)};
                details.Click+=(s,e) => ShowEmulatorMessage(dialog,"Scan details","Folders that could not be read",String.Join("\n",readErrors));
                topActions.Children.Add(details);
            }
            var all=new Button {Content="Select all",Padding=new Thickness(12,7,12,7)}; topActions.Children.Add(all);
            var bottom=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)}; DockPanel.SetDock(bottom,Dock.Bottom); root.Children.Add(bottom);
            var add=new Button {Name="EmulatorAddSelected",Content="Add selected",Padding=new Thickness(12,7,12,7)}; var cancel=new Button {Content="Cancel",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; bottom.Children.Add(add); bottom.Children.Add(cancel);
            var listPanel=new StackPanel {Margin=new Thickness(0,12,0,0)};
            DockPanel.SetDock(listPanel,Dock.Bottom); root.Children.Add(listPanel);
            SettingsComboBox corePicker=null;
            if(chooseCore) {
                listPanel.Children.Add(Label("Core",12,mint));
                var coreOptions=new List<KeyValuePair<string,string>>();
                var seenCores=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(string core in EmulatorLibrary.Cores(executable)) {
                    if(!seenCores.Add(core)) continue;
                    coreOptions.Add(new KeyValuePair<string,string>(core,Path.GetFileNameWithoutExtension(core)));
                }
                if(!String.IsNullOrWhiteSpace(defaultCore) && !seenCores.Contains(defaultCore)) {
                    try {EmulatorLibrary.Core(defaultCore); coreOptions.Insert(0,new KeyValuePair<string,string>(defaultCore,Path.GetFileNameWithoutExtension(defaultCore)+" (current)")); seenCores.Add(defaultCore);} catch(ArgumentException) {}
                }
                if(coreOptions.Count==0) coreOptions.Add(new KeyValuePair<string,string>("","No installed cores found"));
                int selectedCore=coreOptions.FindIndex(option => String.Equals(option.Key,defaultCore,StringComparison.OrdinalIgnoreCase));
                if(selectedCore<0) selectedCore=0;
                corePicker=new SettingsComboBox {Name="EmulatorAddCore",ItemsSource=coreOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=selectedCore,Padding=new Thickness(8),Margin=new Thickness(0,8,0,12)};
                listPanel.Children.Add(corePicker);
            }
            listPanel.Children.Add(Label("Add to list",12,mint));
            var listOptions=new List<KeyValuePair<string,string>> {
                new KeyValuePair<string,string>("","Only the "+preset.Name+" emulator list"),
                new KeyValuePair<string,string>("new","Create a new list")
            };
            if(orphaned) listOptions.RemoveAt(0);
            foreach(var list in settings.CustomLists ?? new List<CustomList>()) listOptions.Add(new KeyValuePair<string,string>(list.Id,list.Name));
            if(chooseCore) foreach(var list in settings.RetroArchPlaylistLists ?? new List<CustomList>())
                if(!listOptions.Any(option => String.Equals(option.Key,list.Id,StringComparison.OrdinalIgnoreCase))) listOptions.Add(new KeyValuePair<string,string>(list.Id,list.Name));
            int selectedList=0;
            if(!String.IsNullOrWhiteSpace(defaultDestination)) {
                int match=listOptions.FindIndex(option => String.Equals(option.Key,defaultDestination,StringComparison.OrdinalIgnoreCase));
                if(match>=0) selectedList=match;
            }
            var listPicker=new SettingsComboBox {Name="EmulatorAddList",ItemsSource=listOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=selectedList,Padding=new Thickness(8),Margin=new Thickness(0,8,0,0)};
            listPanel.Children.Add(listPicker);
            var listName=new TextBox {Name="EmulatorAddListName",Text=preset.Name+" games",MaxLength=80,Padding=new Thickness(8),Margin=new Thickness(0,8,0,0),Visibility=(listPicker.SelectedValue as string)=="new" ? Visibility.Visible : Visibility.Collapsed,ToolTip="New list name"};
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
            BindSelectionToggle(all,() => boxes.Values);
            EmulatorGameSelection result=null;
            add.Click+=(s,e) => {
                string corePath=corePicker==null ? null : corePicker.SelectedValue as string;
                if(corePicker!=null && String.IsNullOrWhiteSpace(corePath)) {note.Text="Choose an installed core before adding."; corePicker.Focus(); return;}
                if((string)listPicker.SelectedValue=="new" && String.IsNullOrWhiteSpace(listName.Text)) {listName.Focus(); return;}
                result=new EmulatorGameSelection {
                    Games=boxes.Where(p => p.Value.IsChecked==true).ToDictionary(p => p.Key,p => names[p.Key].Text,StringComparer.OrdinalIgnoreCase),
                    Destination=listPicker.SelectedValue as string,
                    NewListName=listName.Text.Trim(),
                    CorePath=corePath
                };
                dialog.DialogResult=true;
            }; cancel.Click+=(s,e) => dialog.Close();
            root.Children.Add(new ScrollViewer {Content=entries,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            dialog.ShowDialog(); return result;
        }
    }
}
