using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PadLauncher {
    public partial class Launcher {
        static void EnableGameNameNavigation(TextBox field,List<TextBox> nameFields) {
            field.PreviewKeyDown+=(a,key) => {
                var modifiers=Keyboard.Modifiers;
                bool backward=(key.Key==Key.Tab && modifiers==ModifierKeys.Shift) ||
                    (key.Key==Key.Up && modifiers==ModifierKeys.None);
                bool forward=modifiers==ModifierKeys.None && (key.Key==Key.Tab || key.Key==Key.Down);
                if(!backward && !forward) return;

                int index=nameFields.IndexOf(field);
                var candidates=backward ? nameFields.Take(index).Reverse() : nameFields.Skip(index+1);
                var next=candidates.FirstOrDefault(candidate => candidate.IsEnabled && candidate.IsVisible);
                if(next!=null && next.Focus()) {
                    next.BringIntoView();
                    next.SelectAll();
                    key.Handled=true;
                }
            };
        }
        void ShowEmulatorImport(Window owner,EmulatorPreset preset,string executable,string fallbackCore,Action rebuild) {
            executable=EmulatorLibrary.Executable(executable);
            var dialog=new Window {Title="Import "+preset.Name+" library",Owner=owner,Width=840,Height=790,MinWidth=620,MinHeight=570,Background=owner.Background,Foreground=white,FontFamily=FontFamily,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            if(preview) {dialog.WindowStartupLocation=WindowStartupLocation.Manual; dialog.Left=-20000; dialog.Top=-20000;}
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24),Background=owner.Background}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            var title=Label("Import "+preset.Name+" library",23,white); title.Margin=new Thickness(0,0,0,12); header.Children.Add(title);
            var definitions=(settings.EmulatorImports ?? new List<EmulatorImportDefinition>()).Where(d => d.EmulatorId==preset.Id && (settings.CustomLists ?? new List<CustomList>()).Any(l => l.Id==d.ListId)).ToList();
            var savedOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("","New import")};
            foreach(var definition in definitions) savedOptions.Add(new KeyValuePair<string,string>(definition.Id,settings.CustomLists.First(l => l.Id==definition.ListId).Name+(String.IsNullOrEmpty(definition.SourceLabel) ? "" : " · "+definition.SourceLabel)));
            var savedPicker=new SettingsComboBox {Name="SavedEmulatorImport",ItemsSource=savedOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=0,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)};
            if(definitions.Count>0) {header.Children.Add(Label("Update a saved import",12,mint)); header.Children.Add(savedPicker);}
            var locationCaption=Label("Emulator configuration or data location",12,mint); locationCaption.Margin=new Thickness(0,0,0,6); header.Children.Add(locationCaption);
            var config=EmulatorLibrary.Config(settings,preset.Id);
            var location=new TextBox {Name="EmulatorImportLocation",Text=config!=null && !String.IsNullOrWhiteSpace(config.ImportPath) ? config.ImportPath : EmulatorImport.DetectLocation(preset.Id,executable),Padding=new Thickness(8)}; header.Children.Add(location);
            var locate=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,8)}; header.Children.Add(locate);
            var configButton=new Button {Content="Browse file…",Padding=new Thickness(12,7,12,7)};
            var folderButton=new Button {Content="Browse folder…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            var detect=new Button {Name="LoadEmulatorImportSources",Content="Load sources",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            locate.Children.Add(configButton); locate.Children.Add(folderButton); locate.Children.Add(detect);
            configButton.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="Emulator settings or library|*.ini;*.cfg;*.toml;*.yml;*.yaml;*.lpl;*.sqlite3|All files|*.*",CheckFileExists=true}; if(file.ShowDialog(dialog)==true) {location.Text=file.FileName; detect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}};
            folderButton.Click+=(s,e) => {using(var folder=new System.Windows.Forms.FolderBrowserDialog {Description="Choose the emulator's data folder",SelectedPath=Directory.Exists(location.Text) ? location.Text : ""}) {if(folder.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK) {location.Text=folder.SelectedPath; detect.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}}};
            var sourceSection=new Expander {Header="Sources",Foreground=white,IsExpanded=true,Margin=new Thickness(0,6,0,8)};
            var sourceRows=new StackPanel {Margin=new Thickness(8)}; sourceSection.Content=new ScrollViewer {Content=sourceRows,MaxHeight=132,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}; header.Children.Add(sourceSection);
            var reviewActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,8)}; header.Children.Add(reviewActions);
            var review=new Button {Name="ReviewEmulatorImportGames",Content="Review selected sources",Padding=new Thickness(12,7,12,7),IsEnabled=false}; reviewActions.Children.Add(review);
            var sourceAll=new Button {Content="Select all sources",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; reviewActions.Children.Add(sourceAll);
            var status=Label("Load sources to begin.",12,muted); status.Name="EmulatorImportStatus"; status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,0,0,8); header.Children.Add(status);

            var footer=new StackPanel {Margin=new Thickness(0,12,0,0)}; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var destinations=new List<KeyValuePair<string,string>> {
                new KeyValuePair<string,string>("source:emulator:"+preset.Id,preset.Name+" (automatic list)"),
                new KeyValuePair<string,string>("","Create a new list")
            };
            foreach(var list in settings.CustomLists ?? new List<CustomList>()) destinations.Add(new KeyValuePair<string,string>(list.Id,list.Name));
            var destination=new SettingsComboBox {Name="EmulatorImportDestination",ItemsSource=destinations,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=0,Padding=new Thickness(8)};
            footer.Children.Add(Label("Destination list",12,mint)); footer.Children.Add(destination);
            var listCaption=Label("New list name",12,mint); listCaption.Margin=new Thickness(0,8,0,0); footer.Children.Add(listCaption);
            var listName=new TextBox {Name="EmulatorImportListName",Text=preset.Name+" games",MaxLength=80,Padding=new Thickness(8),Margin=new Thickness(0,8,0,0)}; footer.Children.Add(listName);
            var separate=new CheckBox {Name="EmulatorSeparateImportLists",Content=preset.Id=="retroarch" ? "Create one list per playlist" : "Create one list per source",Margin=new Thickness(0,10,0,0)}; footer.Children.Add(separate);
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,10,0,0)}; footer.Children.Add(buttons);
            var import=new Button {Name="CommitEmulatorImport",Content="Import selected games",Padding=new Thickness(12,7,12,7),IsEnabled=false};
            var cancel=new Button {Content="Cancel",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),IsCancel=true}; buttons.Children.Add(import); buttons.Children.Add(cancel);

            var reviewArea=new DockPanel(); root.Children.Add(reviewArea);
            var selectionBar=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,6),Visibility=Visibility.Collapsed}; DockPanel.SetDock(selectionBar,Dock.Top); reviewArea.Children.Add(selectionBar);
            var all=new Button {Content="Select all games",Padding=new Thickness(12,7,12,7)}; var none=new Button {Content="Unselect all games",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; selectionBar.Children.Add(all); selectionBar.Children.Add(none);
            var count=Label("",12,muted); count.Margin=new Thickness(12,0,0,0); selectionBar.Children.Add(count);
            var gameRows=new StackPanel(); reviewArea.Children.Add(new ScrollViewer {Content=gameRows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var nameFields=new List<TextBox>();
            var sourceBoxes=new Dictionary<EmulatorImportSource,CheckBox>(); var gameBoxes=new Dictionary<EmulatorImportGame,CheckBox>();
            var reviewedSources=new List<EmulatorImportSource>(); string loadedLocation=null;
            var lifetime=new CancellationTokenSource(); bool closed=false,busy=false;
            dialog.Closed+=(s,e) => {closed=true; lifetime.Cancel(); if(!busy) lifetime.Dispose();};
            Func<EmulatorImportDefinition> saved=() => definitions.FirstOrDefault(d => d.Id==(string)savedPicker.SelectedValue);
            if(definitions.Count>0) {
                var deleteList=new Button {Content="Delete imported list…",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,12),Padding=new Thickness(12,7,12,7),IsEnabled=false};
                header.Children.Add(deleteList);
                savedPicker.SelectionChanged+=(sender,args) => deleteList.IsEnabled=saved()!=null;
                deleteList.Click+=(sender,args) => {
                    var definition=saved();
                    if(definition==null) return;
                    var list=settings.CustomLists.FirstOrDefault(item => item.Id==definition.ListId);
                    if(list==null) return;
                    if(!ConfirmEmulatorRemoval(dialog,"Delete imported list","Delete imported list?","“"+list.Name+"” will be removed. Its games will stay in Pad Launcher.","Delete list")) return;
                    try {
                        var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                        CustomLists.Delete(next,list.Id);
                        CommitEmulators(next,rebuild);
                        dialog.Close();
                    } catch(Exception error) {status.Text=error.Message;}
                };
            }
            Action refreshDestination=() => {bool newList=String.IsNullOrEmpty(destination.SelectedValue as string); separate.IsEnabled=newList && reviewedSources.Count>1; separate.Visibility=separate.IsEnabled ? Visibility.Visible : Visibility.Collapsed; listName.Visibility=newList && !(separate.IsEnabled && separate.IsChecked==true) ? Visibility.Visible : Visibility.Collapsed; listCaption.Visibility=listName.Visibility;};
            destination.SelectionChanged+=(s,e) => refreshDestination(); separate.Click+=(s,e) => refreshDestination(); refreshDestination();
            Action updateCount=() => {int selected=gameBoxes.Values.Count(b => b.IsChecked==true); count.Text=selected+" selected"; import.IsEnabled=selected>0 && !busy;};
            Action clearReview=() => {gameRows.Children.Clear(); gameBoxes.Clear(); nameFields.Clear(); reviewedSources.Clear(); selectionBar.Visibility=Visibility.Collapsed; updateCount(); refreshDestination();};
            Action<bool> setBusy=value => {busy=value; header.IsEnabled=!value; reviewArea.IsEnabled=!value; destination.IsEnabled=!value; separate.IsEnabled=!value && String.IsNullOrEmpty(destination.SelectedValue as string) && reviewedSources.Count>1; listName.IsEnabled=!value; updateCount();};
            location.TextChanged+=(s,e) => {loadedLocation=null; sourceBoxes.Clear(); sourceRows.Children.Clear(); review.IsEnabled=false; clearReview();};
            all.Click+=(s,e) => {foreach(var box in gameBoxes.Values.Where(b => b.IsEnabled)) box.IsChecked=true; updateCount();};
            none.Click+=(s,e) => {foreach(var box in gameBoxes.Values) box.IsChecked=false; updateCount();};
            sourceAll.Click+=(s,e) => {foreach(var box in sourceBoxes.Values) box.IsChecked=true; clearReview();};
            Func<Task> loadSources=async () => {
                if(busy) return;
                clearReview(); sourceRows.Children.Clear(); sourceBoxes.Clear(); review.IsEnabled=false; loadedLocation=null;
                try {
                    string selectedLocation=Path.GetFullPath(location.Text.Trim().Trim('"')); var previous=saved(); setBusy(true); status.Text="Finding import sources…";
                    var sources=await Task.Run(() => EmulatorImport.Sources(preset.Id,executable,selectedLocation),lifetime.Token);
                    if(closed) return; loadedLocation=selectedLocation;
                    foreach(var source in sources.OrderBy(x => x.Name,StringComparer.CurrentCultureIgnoreCase)) {
                        var box=new CheckBox {Content=source.Name,IsChecked=previous!=null ? (previous.Sources ?? new List<string>()).Contains(source.Id) : preset.Id!="retroarch" || sources.Count==1,ToolTip=source.Path,Margin=new Thickness(0,4,0,4)};
                        box.Click+=(s,e) => clearReview(); sourceBoxes.Add(source,box); sourceRows.Children.Add(box);
                    }
                    review.IsEnabled=sources.Count>0;
                    status.Text=sources.Count==0 ? "No import sources found. Try the emulator's active data folder or configuration file." : sources.Count+" sources found. Select what to review.";
                } catch(OperationCanceledException) {} catch(Exception error) {if(!closed) status.Text=error.Message;}
                finally {if(closed) lifetime.Dispose(); else setBusy(false);}
            };
            detect.Click+=async (s,e) => { await loadSources(); if(!closed && preset.Id=="rpcs3" && sourceBoxes.Values.Any(b => b.IsChecked==true)) review.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); };
            savedPicker.SelectionChanged+=async (s,e) => {var definition=saved(); if(definition!=null) {location.Text=definition.Location; destination.SelectedValue=definition.ListId; await loadSources();} else {destination.SelectedIndex=0; await loadSources();}};
            dialog.Loaded+=async (s,e) => await loadSources();
            review.Click+=async (s,e) => {
                var chosen=sourceBoxes.Where(x => x.Value.IsChecked==true).Select(x => x.Key).ToList();
                if(chosen.Count==0 || loadedLocation==null) {status.Text="Choose at least one source."; return;}
                clearReview();
                try {
                    setBusy(true); status.Text="Reading selected sources…";
                    var batch=await Task.Run(() => EmulatorImport.Read(preset.Id,executable,chosen,fallbackCore,lifetime.Token),lifetime.Token);
                    if(closed) return; reviewedSources=chosen; var previous=saved();
                    var current=(EmulatorLibrary.Config(settings,preset.Id) ?? new EmulatorConfig()).Games ?? new List<EmulatorGame>();
                    var byPath=current.Where(g => !String.IsNullOrEmpty(g.Path)).GroupBy(g => g.Path,StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key,g => g.First(),StringComparer.OrdinalIgnoreCase);
                    var excluded=new HashSet<string>(previous==null ? Enumerable.Empty<string>() : previous.ExcludedKeys ?? new List<string>());
                    foreach(var source in chosen) {
                        var games=batch.Games.Where(g => g.SourceId==source.Id).GroupBy(g => g.Key).Select(g => g.First()).OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                        var caption=Label(source.Name+" ("+games.Count+")",14,mint); caption.TextWrapping=TextWrapping.Wrap; caption.Margin=new Thickness(0,8,0,6); gameRows.Children.Add(caption);
                        foreach(var game in games) {
                            EmulatorGame existing; byPath.TryGetValue(game.Path,out existing);
                            bool ready=existing!=null || game.Problem==null;
                            var row=new StackPanel(); if(existing!=null) row.Children.Add(Label(existing.Name+" · already added",13,white));
                            else {
                                var gameName=new TextBox {Text=game.Name,MaxLength=120,ToolTip="Game name",MinWidth=240};
                                gameName.TextChanged+=(a,b) => game.Name=gameName.Text;
                                nameFields.Add(gameName);
                                EnableGameNameNavigation(gameName,nameFields);
                                row.Children.Add(gameName);
                            }
                            string core=existing==null ? game.CorePath : existing.CorePath ?? (config==null ? null : config.CorePath);
                            var detail=Label(game.Path+(String.IsNullOrEmpty(core) ? "" : " · Core: "+Path.GetFileName(core))+(ready ? "" : " · "+game.Problem),11,muted); detail.TextWrapping=TextWrapping.Wrap; row.Children.Add(detail);
                            var box=new CheckBox {Content=row,IsEnabled=ready,IsChecked=ready && !excluded.Contains(game.Key),Margin=new Thickness(0,4,0,6)}; box.Checked+=(a,b) => updateCount(); box.Unchecked+=(a,b) => updateCount(); gameBoxes.Add(game,box); gameRows.Children.Add(box);
                        }
                    }
                    var found=new HashSet<string>(batch.Games.Select(g => g.Path),StringComparer.OrdinalIgnoreCase);
                    var missing=previous==null ? new List<EmulatorGame>() : current.Where(g => (previous.ImportedGames ?? new List<string>()).Contains(g.Id) && !found.Contains(g.Path)).ToList();
                    if(batch.Warnings.Count>0 || missing.Count>0) {
                        var details=Label(String.Join("\n",missing.Select(g => "Missing from source, kept: "+g.Name).Concat(batch.Warnings)),12,muted); details.TextWrapping=TextWrapping.Wrap; details.Margin=new Thickness(8);
                        gameRows.Children.Add(new Expander {Foreground=white,Header="Review notes ("+(batch.Warnings.Count+missing.Count)+")",Content=details,Margin=new Thickness(0,10,0,0)});
                    }
                    sourceSection.IsExpanded=false; selectionBar.Visibility=gameBoxes.Count>0 ? Visibility.Visible : Visibility.Collapsed;
                    if(previous==null && String.IsNullOrEmpty(destination.SelectedValue as string)) {
                        string suggested=SuggestedImportName(preset,chosen);
                        var match=destinations.FirstOrDefault(d => !String.IsNullOrEmpty(d.Key) && (String.Equals(d.Value,suggested,StringComparison.OrdinalIgnoreCase) || String.Equals(d.Value,preset.Name+" games",StringComparison.OrdinalIgnoreCase)));
                        if(match.Key!=null) destination.SelectedValue=match.Key;
                        else listName.Text=UniqueImportName(settings,suggested);
                    }
                    status.Text=gameBoxes.Count==0 ? "No launchable games found. See review notes below." : "Choose games and a destination. Existing entries and manual list additions are kept.";
                    if(preset.Id=="eden" && gameBoxes.Count>0) status.Text+=" Deselect updates and DLC.";
                    if(missing.Count>0) status.Text+=" "+missing.Count+" previous games were not found and will be kept.";
                } catch(OperationCanceledException) {} catch(Exception error) {if(!closed) status.Text=error.Message;}
                finally {if(closed) lifetime.Dispose(); else {setBusy(false); refreshDestination();}}
            };
            import.Click+=(s,e) => {
                try {
                    var games=gameBoxes.Where(p => p.Value.IsChecked==true).Select(p => p.Key).ToList(); if(games.Count==0) return;
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    var previous=saved(); var excluded=gameBoxes.Where(p => p.Value.IsEnabled && p.Value.IsChecked!=true).Select(p => p.Key.Key).Concat(previous==null ? Enumerable.Empty<string>() : previous.ExcludedKeys ?? new List<string>()).Except(games.Select(g => g.Key)).ToList();
                    if(separate.IsEnabled && separate.IsChecked==true) {
                        foreach(var source in reviewedSources) {
                            var selected=games.Where(g => g.SourceId==source.Id).ToList(); if(selected.Count==0) continue;
                            var old=(next.EmulatorImports ?? new List<EmulatorImportDefinition>()).FirstOrDefault(d => d.EmulatorId==preset.Id && String.Equals(d.Location,loadedLocation,StringComparison.OrdinalIgnoreCase) && d.Sources!=null && d.Sources.SequenceEqual(new[]{source.Id}));
                            string id=old==null ? null : old.ListId;
                            string name=UniqueImportName(next,SuggestedImportName(preset,new[]{source}));
                            EmulatorImport.Import(next,library,preset.Id,executable,loadedLocation,new[]{source},selected,excluded.Where(k => k.StartsWith(source.Id+"|",StringComparison.Ordinal)),id,name);
                        }
                    } else {
                        string id=destination.SelectedValue as string;
                        EmulatorImport.Import(next,library,preset.Id,executable,loadedLocation,reviewedSources,games,excluded,String.IsNullOrEmpty(id) ? null : id,listName.Text);
                    }
                    CommitEmulators(next,rebuild); dialog.DialogResult=true;
                } catch(Exception error) {status.Text=error.Message;}
            };
            dialog.ShowDialog();
        }
        static string SuggestedImportName(EmulatorPreset preset,IEnumerable<EmulatorImportSource> sources) {
            var selected=sources.ToList(); string name=selected.Count==1 && preset.Id=="retroarch" ? selected[0].Name : selected.Count==1 && selected[0].Kind!="folder" ? preset.Name+" · "+selected[0].Name : preset.Name+" games";
            return name.Substring(0,Math.Min(80,name.Length));
        }
        string UniqueImportName(Settings next,string suggested) {
            var names=new HashSet<string>(CombinedCollections.Options(library,next).Select(l => l.Name),StringComparer.CurrentCultureIgnoreCase);
            string name=suggested;
            for(int i=2;names.Contains(name);i++) {string suffix=" ("+i+")"; name=suggested.Substring(0,Math.Min(suggested.Length,80-suffix.Length))+suffix;}
            return name;
        }
    }
}
