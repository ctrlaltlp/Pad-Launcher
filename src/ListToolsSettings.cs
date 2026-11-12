using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PadLauncher {
    public partial class Launcher {
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
