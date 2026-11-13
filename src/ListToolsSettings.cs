using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PadLauncher {
    public partial class Launcher {
        bool hiddenGamesExpanded;
        public void SetHiddenGames(IEnumerable<string> ids) {
            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            next.HiddenGameIds=(ids ?? Enumerable.Empty<string>()).Where(id => !String.IsNullOrWhiteSpace(id)).Distinct().ToList();
            Storage.Write("settings.json",next); settings=next; ApplyLibrary();
        }
        FrameworkElement CreateHiddenGamesSettings(Action rebuild) {
            var section=new Expander {Name="HiddenGamesSection",Header="Hidden games",Foreground=white,FontSize=14,Margin=new Thickness(0,16,0,0),IsExpanded=hiddenGamesExpanded};
            section.Expanded+=(s,e) => hiddenGamesExpanded=true;
            section.Collapsed+=(s,e) => hiddenGamesExpanded=false;
            var panel=new StackPanel {Margin=new Thickness(12,8,12,12)}; section.Content=panel;
            var note=Label("Check games to hide from every overlay list. They stay registered, keep their list memberships and remain editable. Uncheck to show them again.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            var searchCaption=Label("Filter games by name",12,mint); searchCaption.Margin=new Thickness(0,12,0,6); panel.Children.Add(searchCaption);
            var search=new TextBox {Name="HiddenGamesSearch",Padding=new Thickness(8)}; panel.Children.Add(search);
            var hiddenOnly=new CheckBox {Name="HiddenGamesOnly",Content="Show hidden games only",Margin=new Thickness(0,10,0,8)}; panel.Children.Add(hiddenOnly);
            var selected=new HashSet<string>(settings.HiddenGameIds ?? new List<string>());
            var choices=new StackPanel {Name="HiddenGamesChoices"};
            panel.Children.Add(new ScrollViewer {Content=choices,MaxHeight=320,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,4,0,8)});
            var count=Label("",12,muted); panel.Children.Add(count);
            var boxes=new List<CheckBox>();
            Action filter=() => {
                string query=search.Text.Trim();
                foreach(var box in boxes) box.Visibility=(!hiddenOnly.IsChecked.GetValueOrDefault() || selected.Contains((string)box.Tag)) && ((string)box.Content).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0 ? Visibility.Visible : Visibility.Collapsed;
                count.Text=selected.Count+" hidden";
            };
            foreach(var game in library.Games.GroupBy(g => g.Id).Select(group => group.First()).OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase)) {
                string source=game.Source;
                switch(source) {
                    case null: case "": case "steam": source="Steam"; break;
                    case "epic": source="Epic"; break;
                    case "gog": source="GOG"; break;
                    case "ea": source="EA"; break;
                    case "ubisoft": source="Ubisoft Connect"; break;
                    case "rockstar": source="Rockstar Games Launcher"; break;
                    case "battlenet": source="Battle.net"; break;
                    case "custom": source=CustomGameLibrary.EntrySourceLabel(settings,game); break;
                    case "emulator": var preset=EmulatorLibrary.Presets.FirstOrDefault(p => p.Id==game.EmulatorId); source=preset==null ? "Emulator" : preset.Name; break;
                }
                var box=new CheckBox {Content=game.Name+" · "+source,Tag=game.Id,IsChecked=selected.Contains(game.Id),Margin=new Thickness(0,5,0,5)};
                box.Click+=(s,e) => {if(box.IsChecked==true) selected.Add((string)box.Tag); else selected.Remove((string)box.Tag); filter();};
                boxes.Add(box); choices.Children.Add(box);
            }
            search.TextChanged+=(s,e) => filter(); hiddenOnly.Click+=(s,e) => filter(); filter();
            if(boxes.Count==0) choices.Children.Add(Label("No registered games yet.",12,muted));
            var save=new Button {Name="SaveHiddenGames",Content="Save changes",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,10,0,0),Padding=new Thickness(12,7,12,7),Background=mint,Foreground=System.Windows.Media.Brushes.Black};
            save.Click+=(s,e) => {
                try {SetHiddenGames(selected); rebuild();}
                catch(Exception error) {ThemedMessage.Show(error.Message,"Hidden games");}
            };
            panel.Children.Add(save); return section;
        }
        void ShowListMemberships(Window owner,Action rebuild) {
            var dialog=new Window {Title="Game list memberships",Owner=owner,Width=760,Height=720,MinWidth=560,MinHeight=460,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,Topmost=true}; dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label("Game list memberships",24,white));
            var explanation=Label("Each game entry can belong to several lists. Check the memberships to keep. Grey lists follow their source; combined lists can override inherited membership. Changes apply when saved.",13,muted); explanation.TextWrapping=TextWrapping.Wrap; explanation.Margin=new Thickness(0,10,0,14); header.Children.Add(explanation);
            Func<Game,string> sourceLabel=game => game.Source=="custom" ? CustomGameLibrary.EntrySourceLabel(settings,game) : String.IsNullOrEmpty(game.Source) || game.Source=="steam" ? "Steam" : game.Source=="emulator" ? EmulatorLibrary.Preset(game.EmulatorId).Name : game.Source;
            var games=library.Games.GroupBy(game => game.Id).Select(group => group.First()).OrderBy(game => game.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            var picker=new SettingsComboBox {Name="MembershipGamePicker",DisplayMemberPath="Value",SelectedValuePath="Key",ItemsSource=games.Select(game => new KeyValuePair<string,string>(game.Id,game.Name+" · "+sourceLabel(game))).ToList(),SelectedValue=settings.SelectedId,Margin=new Thickness(0,0,0,10)};
            if(picker.SelectedIndex<0 && games.Count>0) picker.SelectedIndex=0; header.Children.Add(picker);
            var target=Label("",12,muted); target.TextWrapping=TextWrapping.Wrap; target.Margin=new Thickness(0,0,0,12); header.Children.Add(target);
            var filter=new TextBox {Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)}; header.Children.Add(Label("Filter lists by name",12,muted)); header.Children.Add(filter);
            var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var status=Label("",12,muted); status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,10,0,8); footer.Children.Add(status);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right}; footer.Children.Add(actions);
            var save=new Button {Name="SaveMemberships",Content="Save changes",Padding=new Thickness(14,8,14,8),Background=mint,Foreground=new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(13,25,34))}; actions.Children.Add(save);
            var close=new Button {Content="Close",Padding=new Thickness(14,8,14,8),Margin=new Thickness(8,0,0,0),IsCancel=true}; close.Click+=(s,e) => dialog.Close(); actions.Children.Add(close);
            var rows=new StackPanel(); root.Children.Add(new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var boxes=new Dictionary<string,CheckBox>(); var original=new Dictionary<string,bool>(); var rowNames=new Dictionary<FrameworkElement,string>();
            Action applyFilter=() => {foreach(var row in rowNames) row.Key.Visibility=row.Value.IndexOf(filter.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0 ? Visibility.Visible : Visibility.Collapsed;};
            Action load=() => {
                boxes.Clear(); original.Clear(); rowNames.Clear(); rows.Children.Clear(); status.Text="";
                var game=games.FirstOrDefault(item => item.Id==(picker.SelectedValue as string)); save.IsEnabled=game!=null;
                if(game==null) {target.Text="No games available."; return;}
                try {target.Text=game.Source=="custom" && !String.IsNullOrEmpty(game.SteamLaunchId) ? "steam://rungameid/"+game.SteamLaunchId : String.IsNullOrEmpty(game.Source) || game.Source=="steam" ? GameLaunch.UriFor(game) : game.LaunchId;} catch {target.Text=game.LaunchId;}
                var options=CollectionOptions();
                foreach(var list in options) {
                    bool selected=(list.Games ?? new List<string>()).Contains(game.Id);
                    var custom=(settings.CustomLists ?? new List<CustomList>()).FirstOrDefault(item => item.Id==list.Id);
                    var combined=(settings.CombinedCollections ?? new List<CombinedCollection>()).FirstOrDefault(item => item.Id==list.Id);
                    bool editable=custom!=null || combined!=null || list.Id=="source:custom" || list.Id=="favourites";
                    var row=new StackPanel {Margin=new Thickness(0,0,0,12)};
                    var box=new CheckBox {Content=ListLabel(list),IsChecked=selected,IsEnabled=editable,Foreground=white}; row.Children.Add(box); boxes[list.Id]=box; original[list.Id]=selected;
                    string detail="";
                    if(combined!=null) {
                        var sources=options.Where(item => (combined.Sources ?? new List<string>()).Contains(item.Id) && (item.Games ?? new List<string>()).Contains(game.Id)).Select(item => item.Name).ToList();
                        if(sources.Count>0) detail="Inherited from: "+String.Join(", ",sources);
                        if((combined.AddedGames ?? new List<string>()).Contains(game.Id)) detail+=(detail.Length>0 ? " · " : "")+"Added individually";
                        if((combined.ExcludedGames ?? new List<string>()).Contains(game.Id)) detail+=(detail.Length>0 ? " · " : "")+"Excluded from this list";
                    } else if(!editable) detail="Automatic membership from this source";
                    if(!String.IsNullOrEmpty(detail)) {var note=Label(detail,12,muted); note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(28,4,0,0); row.Children.Add(note);}
                    rows.Children.Add(row); rowNames[row]=list.Name;
                }
                applyFilter();
            };
            picker.SelectionChanged+=(s,e) => load(); filter.TextChanged+=(s,e) => applyFilter();
            save.Click+=(s,e) => {
                try {
                    if(scanning) throw new InvalidOperationException("Wait for the current library refresh to finish.");
                    string id=picker.SelectedValue as string;
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    foreach(var pair in boxes.Where(pair => pair.Value.IsEnabled && (pair.Value.IsChecked==true)!=original[pair.Key])) CustomLists.SetMembership(next,library,id,pair.Key,pair.Value.IsChecked==true);
                    Storage.Write("settings.json",next); settings=next; CustomGameLibrary.Apply(library,settings); ApplyLibrary(); rebuild(); load(); status.Text="Memberships saved.";
                } catch(Exception error) {status.Text=error.Message;}
            };
            load(); dialog.Show();
        }
        bool listLayoutExpanded;
        string editedListLayoutId;
        void ToggleFavourite() {
            if(visible.Count==0) return;
            var game=visible[selected];
            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            if(next.FavouriteGameIds==null) next.FavouriteGameIds=new List<string>();
            bool removed=next.FavouriteGameIds.Remove(game.Id);
            if(!removed) next.FavouriteGameIds.Add(game.Id);
            try {
                Storage.Write("settings.json",next); settings=next; ApplyLibrary();
                ShowNotice((removed ? "Removed from Favourites: " : "Added to Favourites: ")+game.Name+
                    (!removed && !CollectionEnabled("favourites") ? "\nEnable Favourites under Lists → Overlay lists to browse them." : ""),5.0/3);
            } catch(Exception error) {ShowNotice(error.Message);}
        }
        StackPanel CreateListLayoutSettings(Window owner,Action rebuild) {
            var panel=new StackPanel {Margin=new Thickness(0,12,0,0)};
            var note=Label("Give a list its own layout and panel size, or let it follow the global Overlay settings.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            var choices=CollectionOptions().Select(list => new KeyValuePair<string,string>(list.Id,list.Name)).ToList();
            var picker=new SettingsComboBox {Name="ListLayoutPicker",ItemsSource=choices,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=0,Padding=new Thickness(8),Margin=new Thickness(0,10,0,10)};
            if(choices.Any(choice => choice.Key==editedListLayoutId)) picker.SelectedValue=editedListLayoutId;
            panel.Children.Add(picker);
            var inherit=new CheckBox {Content="Use global Overlay settings",Margin=new Thickness(0,0,0,10)};
            panel.Children.Add(inherit);
            var fields=new StackPanel(); panel.Children.Add(fields);
            fields.Children.Add(Label("Layout",12,mint));
            var layout=new SettingsComboBox {ItemsSource=new[]{new KeyValuePair<string,string>("angled","Angled"),new KeyValuePair<string,string>("rectangular","Rectangular"),new KeyValuePair<string,string>("grid","Grid")},DisplayMemberPath="Value",SelectedValuePath="Key",Padding=new Thickness(8),Margin=new Thickness(0,6,0,10)};
            fields.Children.Add(layout);
            fields.Children.Add(Label("Panel size",12,mint));
            var scaleValue=Label("",12,muted); fields.Children.Add(scaleValue);
            var scale=new Slider {Minimum=.6,Maximum=1.4,TickFrequency=.05,IsSnapToTickEnabled=true,Margin=new Thickness(0,6,0,10)};
            scale.ValueChanged+=(s,e) => scaleValue.Text=scale.Value.ToString("0%"); fields.Children.Add(scale);
            var grid=new StackPanel();
            grid.Children.Add(Label("Grid columns",12,mint));
            var columns=new SettingsComboBox {ItemsSource=Enumerable.Range(2,9).ToList(),Padding=new Thickness(8),Margin=new Thickness(0,6,0,10)}; grid.Children.Add(columns);
            grid.Children.Add(Label("Grid rows",12,mint));
            var rows=new SettingsComboBox {ItemsSource=Enumerable.Range(1,5).ToList(),Padding=new Thickness(8),Margin=new Thickness(0,6,0,10)}; grid.Children.Add(rows); fields.Children.Add(grid);
            var angled=new StackPanel();
            angled.Children.Add(Label("Angled sizing",12,mint));
            var sizing=new SettingsComboBox {ItemsSource=new[]{new KeyValuePair<string,string>("fit","Fit artwork"),new KeyValuePair<string,string>("fixed","Fixed (blurred edges)")},DisplayMemberPath="Value",SelectedValuePath="Key",Padding=new Thickness(8),Margin=new Thickness(0,6,0,10)}; angled.Children.Add(sizing); fields.Children.Add(angled);
            Action update=() => {fields.IsEnabled=inherit.IsChecked!=true; grid.Visibility=(string)layout.SelectedValue=="grid" ? Visibility.Visible : Visibility.Collapsed; angled.Visibility=(string)layout.SelectedValue=="angled" ? Visibility.Visible : Visibility.Collapsed;};
            Action load=() => {
                ListLayout saved=null;
                if(settings.ListLayouts!=null && picker.SelectedValue is string) settings.ListLayouts.TryGetValue((string)picker.SelectedValue,out saved);
                inherit.IsChecked=saved==null;
                layout.SelectedValue=saved==null ? settings.CarouselLayout : saved.Layout;
                scale.Value=saved==null ? settings.CarouselScale : saved.PanelScale;
                columns.SelectedItem=saved==null ? settings.GridColumns : saved.Columns;
                rows.SelectedItem=saved==null ? settings.GridRows : saved.Rows;
                sizing.SelectedValue=saved==null ? settings.AngledPanelSizing : saved.AngledSizing;
                update();
            };
            picker.SelectionChanged+=(s,e) => {editedListLayoutId=picker.SelectedValue as string; load();}; inherit.Click+=(s,e) => update(); layout.SelectionChanged+=(s,e) => update();
            var save=new Button {Name="SaveListLayout",Content="Save changes",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,10,0,0)};
            save.Click+=(s,e) => {try {
                string id=picker.SelectedValue as string; if(id==null) return;
                var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                if(next.ListLayouts==null) next.ListLayouts=new Dictionary<string,ListLayout>();
                if(inherit.IsChecked==true) next.ListLayouts.Remove(id);
                else next.ListLayouts[id]=new ListLayout {Layout=(string)layout.SelectedValue,PanelScale=scale.Value,Columns=(int)columns.SelectedItem,Rows=(int)rows.SelectedItem,AngledSizing=(string)sizing.SelectedValue};
                Storage.Write("settings.json",next); settings=next; ApplyLibrary(); rebuild();
            } catch(Exception error) {ThemedMessage.Show(owner,error.Message,"List layout");}};
            panel.Children.Add(save); load(); return panel;
        }
        void ShowMissingFileRepair(Window owner,Action rebuild) {
            var dialog=new Window {Title="Repair missing game paths",Owner=owner,Width=740,Height=560,MinWidth=500,MinHeight=350,Background=owner.Background,Foreground=white,FontFamily=owner.FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label("Repair missing game paths",22,white));
            var note=Label("Choose replacement files to keep a game's name, artwork, arguments, and list memberships. Entries already removed by automatic cleanup must be added again.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,10,0,14); header.Children.Add(note);
            var status=Label("",12,muted); status.TextWrapping=TextWrapping.Wrap; header.Children.Add(status);
            var close=new Button {Content="Close",IsCancel=true,Padding=new Thickness(14,8,14,8),HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,14,0,0)};
            close.Click+=(s,e) => dialog.Close(); DockPanel.SetDock(close,Dock.Bottom); root.Children.Add(close);
            var entries=new StackPanel(); root.Children.Add(new ScrollViewer {Content=entries,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            Action render=null;
            render=() => {
                entries.Children.Clear(); int count=0;
                foreach(var game in library.Games.Where(entry => entry.Source=="custom" || entry.Source=="emulator")) {
                    string path=game.LaunchId;
                    if(String.IsNullOrEmpty(path) || File.Exists(path) || Directory.Exists(path)) continue;
                    if(game.Source=="custom" && !Path.IsPathRooted(path)) continue;
                    count++;
                    var selectedGame=game;
                    var row=new StackPanel {Margin=new Thickness(0,10,0,12)};
                    var name=Label(game.Name,15,mint); name.TextWrapping=TextWrapping.Wrap; row.Children.Add(name);
                    var oldPath=Label(path,12,muted); oldPath.TextWrapping=TextWrapping.Wrap; row.Children.Add(oldPath);
                    var browse=new Button {Content="Choose replacement…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,8,0,0)};
                    browse.Click+=(s,e) => {
                        if(scanning) {status.Text="Please wait for the library refresh to finish."; return;}
                        var file=new Microsoft.Win32.OpenFileDialog {CheckFileExists=true,Filter=selectedGame.Source=="custom" ? "Games and shortcuts|*.exe;*.lnk;*.bat;*.cmd" : "Game files|*.*"};
                        if(file.ShowDialog(dialog)!=true) return;
                        try {
                            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                            GameLaunch.RepairPath(next,selectedGame,file.FileName);
                            Storage.Write("settings.json",next); settings=next;
                            if(artworkCancellation!=null) artworkCancellation.Cancel();
                            CustomGameLibrary.Apply(library,settings); EmulatorLibrary.Apply(library,settings);
                            foreach(var entry in library.Games) ArtworkService.ApplyCached(entry,settings);
                            ApplyLibrary(); rebuild(); DownloadArtwork(library);
                            status.Text="Updated "+selectedGame.Name+"."; render();
                        } catch(Exception error) {status.Text=error.Message;}
                    };
                    row.Children.Add(browse); entries.Children.Add(row);
                }
                if(count==0) entries.Children.Add(Label("No missing custom or emulator game paths.",14,muted));
            };
            render(); dialog.ShowDialog();
        }
    }
}
