using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PadLauncher {
    public partial class Launcher {
        async void ShowLibretroArtworkPicker(Window owner,Game game,Action<string> onChoose=null) {
            var chooser=new Window {Name="LibretroArtworkPicker",Title="Libretro artwork · "+game.Name,Owner=owner,Width=760,Height=680,MinWidth=580,MinHeight=480,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,Topmost=true};
            chooser.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; chooser.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label("Choose Libretro image",23,white));
            var switchSource=new Button {Name="ArtworkSwitchToSteamGridDB",Content="Choose from SteamGridDB instead…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,10,0,0)};
            switchSource.Click+=(s,e) => {chooser.Close(); ShowArtworkPicker(owner,game,onChoose);}; header.Children.Add(switchSource);
            var note=Label("Choose a console and search for a game. No API key is needed; manual choices work even when automatic Libretro downloads are disabled.",12,muted); note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,8,0,14); header.Children.Add(note);
            header.Children.Add(Label("Console",12,mint));
            var platforms=new SettingsComboBox {Name="LibretroConsolePicker",Padding=new Thickness(8),Margin=new Thickness(0,8,0,12),IsEnabled=false}; header.Children.Add(platforms);
            var searchRow=new DockPanel {Margin=new Thickness(0,0,0,12)}; header.Children.Add(searchRow);
            var find=new Button {Name="LibretroFindCovers",Content="Find covers",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),IsEnabled=false}; DockPanel.SetDock(find,Dock.Right); searchRow.Children.Add(find);
            var query=new TextBox {Name="LibretroSearchText",Text=game.Name,Padding=new Thickness(8)}; searchRow.Children.Add(query);
            var choices=new SettingsComboBox {Name="LibretroCoverPicker",DisplayMemberPath="Value",SelectedValuePath="Key",Padding=new Thickness(8),Margin=new Thickness(0,0,0,12)}; header.Children.Add(choices);
            var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var status=Label("Loading consoles…",12,muted); status.Name="LibretroPickerStatus"; status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,12,0,12); footer.Children.Add(status);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right}; footer.Children.Add(actions);
            var close=new Button {Content="Close",IsCancel=true,Padding=new Thickness(14,8,14,8)}; actions.Children.Add(close);
            var use=new Button {Name="LibretroUseImage",Content="Use image",Padding=new Thickness(14,8,14,8),Margin=new Thickness(8,0,0,0),Background=mint,Foreground=Brushes.Black,IsEnabled=false}; actions.Children.Add(use);
            var preview=new Image {Name="LibretroCoverPreview",Stretch=Stretch.Uniform,Margin=new Thickness(12)};
            root.Children.Add(new Border {Background=new SolidColorBrush(Color.FromRgb(21,36,46)),CornerRadius=new CornerRadius(8),Child=preview});
            var resolver=new LibretroArtwork(); var cancellation=new CancellationTokenSource(); CancellationTokenSource previewCancellation=null;
            int revision=0; bool saving=false;
            chooser.Closed+=(s,e) => {cancellation.Cancel(); if(previewCancellation!=null) previewCancellation.Cancel();};
            close.Click+=(s,e) => chooser.Close();
            Func<Task> search=async () => {
                string system=platforms.SelectedItem as string; if(system==null) {status.Text="Choose a console first."; return;}
                int current=++revision; if(previewCancellation!=null) previewCancellation.Cancel();
                use.IsEnabled=false; preview.Source=null; choices.ItemsSource=null; find.IsEnabled=false; status.Text="Finding covers…";
                try {
                    string text=query.Text;
                    var files=await Task.Run(() => resolver.Search(system,text,cancellation.Token));
                    if(cancellation.IsCancellationRequested || current!=revision) return;
                    status.Text=files.Count==0 ? "No matching covers found. Try a shorter title or another console." : files.Count>=200 ? "Showing the first 200 covers. Refine your search to narrow the results." : files.Count+" covers found. Select one to preview.";
                    choices.ItemsSource=files.Select(file => new KeyValuePair<string,string>(file,Path.GetFileNameWithoutExtension(file))).ToList();
                    if(files.Count>0) choices.SelectedIndex=0;
                } catch(Exception error) {if(!cancellation.IsCancellationRequested && current==revision) status.Text="Could not load covers. "+error.Message;}
                finally {if(!cancellation.IsCancellationRequested) find.IsEnabled=true;}
            };
            choices.SelectionChanged+=async (s,e) => {
                string file=choices.SelectedValue as string,system=platforms.SelectedItem as string;
                if(previewCancellation!=null) previewCancellation.Cancel();
                use.IsEnabled=false; preview.Source=null; if(file==null || system==null) return;
                previewCancellation=CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token); var token=previewCancellation.Token;
                status.Text="Loading cover preview…";
                try {
                    byte[] bytes=await Task.Run(() => LibretroArtwork.FetchImage(system,file,token));
                    token.ThrowIfCancellationRequested();
                    using(var stream=new MemoryStream(bytes)) {
                        var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad; image.DecodePixelWidth=320; image.StreamSource=stream; image.EndInit(); image.Freeze(); preview.Source=image;
                    }
                    use.IsEnabled=true; status.Text=Path.GetFileNameWithoutExtension(file);
                } catch(Exception error) {if(!token.IsCancellationRequested) status.Text="Preview unavailable. "+error.Message;}
            };
            platforms.SelectionChanged+=async (s,e) => {if(platforms.SelectedItem!=null) await search();};
            find.Click+=async (s,e) => await search();
            query.KeyDown+=async (s,e) => {if(e.Key==System.Windows.Input.Key.Enter && find.IsEnabled) {e.Handled=true; await search();}};
            use.Click+=async (s,e) => {
                if(saving) return;
                if(scanning) {status.Text="Please wait for the library refresh to finish."; return;}
                string system=platforms.SelectedItem as string,file=choices.SelectedValue as string; if(system==null || file==null) return;
                saving=true; header.IsEnabled=false; use.IsEnabled=false; status.Text="Saving cover…";
                try {
                    string path=await Task.Run(() => resolver.SaveChoice(game,system,file,cancellation.Token));
                    if(cancellation.IsCancellationRequested) return;
                    if(onChoose!=null) onChoose(path);
                    else {
                        if(!library.Games.Any(entry => entry.Id==game.Id)) throw new InvalidOperationException("This game is no longer available. Select it again before choosing artwork.");
                        var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                        if(next.ArtworkOverrides==null) next.ArtworkOverrides=new Dictionary<string,string>(); next.ArtworkOverrides[game.Id]=path;
                        Storage.Write("settings.json",next); settings=next;
                        if(artworkCancellation!=null) artworkCancellation.Cancel();
                        foreach(var entry in library.Games.Where(entry => entry.Id==game.Id)) ArtworkService.ApplyCached(entry,settings);
                        RefreshArtworkPresentation();
                    }
                    chooser.Close();
                } catch(Exception error) {if(!cancellation.IsCancellationRequested) status.Text="Could not use this cover. "+error.Message;}
                finally {saving=false; if(!cancellation.IsCancellationRequested) {header.IsEnabled=true; use.IsEnabled=preview.Source!=null;}}
            };
            chooser.Show();
            try {
                var systems=await Task.Run(() => resolver.Platforms(cancellation.Token));
                if(cancellation.IsCancellationRequested) return;
                platforms.ItemsSource=systems; platforms.IsEnabled=find.IsEnabled=true;
                string preferred=LibretroArtwork.Systems(game,settings).FirstOrDefault(system => systems.Contains(system));
                if(preferred!=null) platforms.SelectedItem=preferred;
                else status.Text="Choose the game's console, then Find covers.";
            } catch(Exception error) {if(!cancellation.IsCancellationRequested) status.Text="Could not load consoles. "+error.Message;}
        }
    }
}
