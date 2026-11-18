using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace PadLauncher {
    public partial class Launcher {
        CancellationTokenSource listArtworkCancellation;
        string editedArtworkListId;
        readonly Dictionary<string,CancellationTokenSource> artworkRefreshJobs=new Dictionary<string,CancellationTokenSource>();
        readonly Dictionary<string,string> artworkRefreshStatuses=new Dictionary<string,string>();
        readonly Dictionary<string,Action> artworkRefreshViews=new Dictionary<string,Action>();
        string AutomaticArtworkDescription(string listId,bool retroArchDefaults=false) {
            if(retroArchDefaults) return "Currently using: "+ArtworkService.ArtworkSourceName(ArtworkService.EmulatorDefaultSource(settings,"retroarch"))+" (RetroArch emulator default).";
            if(ArtworkService.ValidArtworkSource(settings.RetroArchListArtworkSource) && RetroArchListDefaults.Applies(settings,listId))
                return "Currently using: "+ArtworkService.ArtworkSourceName(settings.RetroArchListArtworkSource)+" (RetroArch lists settings).";
            var list=CollectionOptions().FirstOrDefault(item => item.Id==listId);
            var ids=new HashSet<string>(list==null ? new List<string>() : list.Games ?? new List<string>());
            var games=library.Games.Where(game => listId=="all" || ids.Contains(game.Id)).ToList();
            var emulators=games.Where(game => game.Source=="emulator").Select(game => game.EmulatorId).Where(id => !String.IsNullOrWhiteSpace(id)).Distinct().ToList();
            const string prefix="source:emulator:";
            if(emulators.Count==0 && listId!=null && listId.StartsWith(prefix)) emulators.Add(listId.Substring(prefix.Length));
            var descriptions=new List<string>();
            foreach(string emulator in emulators) {
                string source=ArtworkService.GameListSource(settings,new Game {Source="emulator",EmulatorId=emulator},listId,"auto");
                var preset=EmulatorLibrary.Presets.FirstOrDefault(item => item.Id==emulator);
                descriptions.Add((preset==null ? emulator : preset.Name)+": "+ArtworkService.ArtworkSourceName(source));
            }
            if(games.Any(game => game.Source!="emulator") || descriptions.Count==0) descriptions.Add("PC games: existing launcher/local artwork, then SteamGridDB");
            return "Currently using (inherited):\n"+String.Join("\n",descriptions);
        }
        FrameworkElement CreateEmulatorArtworkSettings(Window owner,Action rebuild) {
            var panel=new StackPanel {Margin=new Thickness(0,10,0,0)};
            var note=Label("Defaults for emulator games when the list uses Automatic. Individual list choices take priority. Switching sources reuses existing downloads.",12,muted); note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            var choices=new Dictionary<string,SettingsComboBox>();
            foreach(var preset in EmulatorLibrary.Presets) {
                panel.Children.Add(Label(preset.Name,13,mint));
                string source; source=settings.EmulatorArtworkSources!=null && settings.EmulatorArtworkSources.TryGetValue(preset.Id,out source) ? source : "auto";
                var picker=new SettingsComboBox {Name="EmulatorArtworkSource_"+preset.Id,ItemsSource=ArtworkService.ArtworkSourceChoices(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=source,Padding=new Thickness(8),Margin=new Thickness(0,6,0,12)};
                choices[preset.Id]=picker; panel.Children.Add(picker);
                var effective=Label("",12,muted); effective.Name="EmulatorArtworkEffective_"+preset.Id; effective.TextWrapping=TextWrapping.Wrap; effective.Margin=new Thickness(0,0,0,12); panel.Children.Add(effective);
                Action updateEffective=() => {effective.Visibility=(string)picker.SelectedValue=="auto" ? Visibility.Visible : Visibility.Collapsed; effective.Text="Currently using: "+ArtworkService.ArtworkSourceName(preset.Id=="retroarch" && settings.RetroArchPreferLibretro==true ? "libretro" : ArtworkService.DefaultEmulatorArtworkSource(preset.Id))+(preset.Id=="retroarch" && settings.RetroArchPreferLibretro==true ? " (existing RetroArch preference)." : " (built-in default).");};
                picker.SelectionChanged+=(s,e) => updateEffective(); updateEffective();
            }
            var save=new Button {Name="SaveEmulatorArtworkSources",Content="Save changes",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7)}; panel.Children.Add(save);
            save.Click+=(s,e) => {
                try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    next.EmulatorArtworkSources=choices.Where(pair => ArtworkService.ValidArtworkSource(pair.Value.SelectedValue as string)).ToDictionary(pair => pair.Key,pair => pair.Value.SelectedValue as string);
                    next.LibretroArtworkEnabled=true;
                    Storage.Write("settings.json",next); settings=next; ApplyLibrary(); rebuild();
                } catch(Exception error) {ThemedMessage.Show(owner,error.Message,"Emulator artwork");}
            };
            return new Expander {Name="EmulatorArtworkDefaultsSection",Header="Emulator artwork defaults",Foreground=white,FontSize=14,Margin=new Thickness(0,16,0,0),Content=panel};
        }
        void NotifyArtworkRefreshViews() {foreach(var update in artworkRefreshViews.Values.ToArray()) update();}
        async Task RedownloadListArtwork(Window owner,string listId,Action<string> progress) {
            CancellationTokenSource cancellation=null;
            try {
                if(listId==null) return;
                if(artworkRefreshJobs.ContainsKey(listId)) {progress("Artwork is already being redownloaded for this list."); return;}
                if(scanning) throw new InvalidOperationException("Please wait for the library refresh to finish.");
                var list=CollectionOptions().FirstOrDefault(item => item.Id==listId); if(list==null) return;
                var ids=new HashSet<string>(list.Games ?? new List<string>());
                if(list.Id=="all") ids=new HashSet<string>(library.Games.Select(game => game.Id));
                var games=library.Games.Where(game => ids.Contains(game.Id)).ToList();
                if(games.Count==0) {ThemedMessage.Show(owner,"This list has no registered games.","Redownload list artwork"); return;}
                var confirm=new Window {Title="Redownload list artwork",Owner=owner,Width=560,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
                confirm.Resources.MergedDictionaries.Add(owner.Resources); var panel=new StackPanel {Margin=new Thickness(24)}; confirm.Content=panel;
                var heading=Label("Redownload artwork for “"+list.Name+"”?",20,white); heading.TextWrapping=TextWrapping.Wrap; panel.Children.Add(heading);
                var note=Label("Clears downloaded artwork and retry records for "+games.Count+" games, then downloads it again using this list's saved artwork preference. Other lists sharing these games may also show the refreshed artwork. Launcher artwork and game files stay untouched.",12,muted); note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,12,0,12); panel.Children.Add(note);
                var keep=new CheckBox {Name="KeepManualListArtwork",Content="Keep manually chosen images",IsChecked=true,Margin=new Thickness(0,0,0,14)}; panel.Children.Add(keep);
                var actions=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right}; panel.Children.Add(actions);
                var start=new Button {Name="ConfirmRedownloadListArtwork",Content="Clear and redownload",Padding=new Thickness(12,7,12,7)}; actions.Children.Add(start);
                var cancel=new Button {Content="Cancel",IsCancel=true,Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; actions.Children.Add(cancel);
                start.Click+=(s,e) => confirm.DialogResult=true;
                if(confirm.ShowDialog()!=true) return;
                cancellation=new CancellationTokenSource(); artworkRefreshJobs.Add(listId,cancellation);
                progress("Clearing and redownloading artwork for "+list.Name+"…");
                var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                if(keep.IsChecked!=true) {
                    foreach(string id in ids) if(next.ArtworkOverrides!=null) next.ArtworkOverrides.Remove(id);
                    foreach(var game in next.CustomGames ?? new List<CustomGame>()) if(ids.Contains(game.Id)) game.Artwork=null;
                }
                Storage.Write("settings.json",next); settings=next;
                if(artworkCancellation!=null) artworkCancellation.Cancel();
                if(listArtworkCancellation!=null) listArtworkCancellation.Cancel();
                var batch=games.Select(game => game.ArtworkCopy()).ToList();
                ArtworkService.ClearDownloadedArtwork(batch);
                foreach(var game in batch) {
                    bool manual=game.ArtworkProvider=="chosen" || game.ArtworkProvider=="libretro-chosen";
                    if(keep.IsChecked!=true && manual) {game.Art=null; game.ArtworkProvider=null;}
                    if(!File.Exists(game.Art)) {game.Art=null; game.ArtworkProvider=null;}
                    ArtworkService.ApplyCached(game,next);
                }
                ShowNotice("Redownloading artwork for "+list.Name+"…",3);
                var warnings=new List<string>();
                var token=cancellation.Token;
                await Task.Run(() => {
                    string source=ArtworkService.ListDefaultSource(next,listId);
                    if(batch.Any(game => ArtworkService.PrimaryArtworkSource(ArtworkService.GameListSource(next,game,listId,source))=="libretro")) LibretroArtwork.ClearCatalogCaches(batch,next,token);
                    if(source=="auto") ArtworkService.Fill(batch,next,token,(game,path) => game.Art=path,warnings.Add);
                    ArtworkService.FillList(batch,next,listId,token,id => {},warnings.Add);
                },token);
                if(exiting) return;
                int refreshed=0,manualKept=0,existingKept=0,missing=0;
                string resolvedSource=ArtworkService.ListDefaultSource(next,listId);
                foreach(var updated in batch) {
                    var display=ArtworkService.ForList(updated,next,listId,resolvedSource);
                    if(!File.Exists(display.Art)) {display.Art=null; display.ArtworkProvider=null; missing++;}
                    else if(ArtworkService.HasManualArtwork(display,next)) manualKept++;
                    else if(display.ArtworkProvider=="libretro" || display.ArtworkProvider=="sgdb" || display.ArtworkProvider=="downloaded") refreshed++;
                    else existingKept++;
                    var current=library.Games.FirstOrDefault(game => game.Id==updated.Id); if(current==null) continue;
                    current.Art=display.Art; current.ArtworkProvider=display.ArtworkProvider; ArtworkService.ApplyCached(current,settings);
                }
                RefreshArtworkPresentation();
                string result="Artwork refresh complete for "+list.Name+": "+refreshed+" refreshed · "+manualKept+" manual kept · "+missing+" without artwork"+(existingKept>0 ? " · "+existingKept+" existing kept" : "")+".";
                if(warnings.Count>0) result+=" "+warnings[0];
                progress(result); ShowNotice(result,4);
            } catch(OperationCanceledException) {if(!exiting) progress("Artwork refresh cancelled.");}
            catch(Exception error) {if(!exiting) {progress("Artwork refresh failed: "+error.Message); if(owner.IsVisible) ThemedMessage.Show(owner,error.Message,"Redownload list artwork");}}
            finally {if(cancellation!=null) {artworkRefreshJobs.Remove(listId); cancellation.Dispose(); if(!exiting) NotifyArtworkRefreshViews();}}
        }
        void RefreshArtworkPresentation() {
            if(listArtworkCancellation!=null) listArtworkCancellation.Cancel();
            if(filters.Count>0) {
                string listId=filters[collectionIndex].Id;
                string source=ArtworkService.ListDefaultSource(settings,listId);
                visible=visible.Select(game => ArtworkService.ForList(library.Games.FirstOrDefault(entry => entry.Id==game.Id) ?? game,settings,listId,source)).ToList();
            }
            cards.Clear(); carousel.Children.Clear(); Render(false); RefreshMissingArtworkList();
        }
        void ResetGameArtwork(Game game) {
            if(artworkCancellation!=null) artworkCancellation.Cancel();
            if(listArtworkCancellation!=null) listArtworkCancellation.Cancel();
            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            if(next.ArtworkOverrides!=null) next.ArtworkOverrides.Remove(game.Id);
            var custom=(next.CustomGames ?? new System.Collections.Generic.List<CustomGame>()).FirstOrDefault(entry => entry.Id==game.Id);
            if(custom!=null) custom.Artwork="";
            Storage.Write("settings.json",next); settings=next;
            foreach(var entry in library.Games.Where(item => item.Id==game.Id)) {entry.Art=null; entry.ArtworkProvider=null; ArtworkService.ApplyCached(entry,settings);}
            RefreshArtworkPresentation();
            DownloadListArtwork();
            RefreshLibrary(true);
            if(filters.Count>0 && ArtworkService.ListSource(settings,filters[collectionIndex].Id)!="auto") ShowNotice("Artwork reset to this list's preferred source.",3);
        }
        async void DownloadListArtwork() {
            if(listArtworkCancellation!=null) listArtworkCancellation.Cancel();
            if(preview || demo || filters.Count==0 || visible.Count==0) return;
            string listId=filters[collectionIndex].Id;
            string source=ArtworkService.ListDefaultSource(settings,listId);
            if(!visible.Any(game => ArtworkService.GameListSource(settings,game,listId,source)!="auto")) return;
            var cancellation=new CancellationTokenSource(); listArtworkCancellation=cancellation;
            var snapshot=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            var games=visible.Select(game => game.ArtworkCopy()).ToArray();
            try {
                await Task.Run(() => ArtworkService.FillList(games,snapshot,listId,cancellation.Token,id => {
                    Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => {
                        if(cancellation.IsCancellationRequested || exiting || filters.Count==0 || filters[collectionIndex].Id!=listId) return;
                        var original=library.Games.FirstOrDefault(game => game.Id==id);
                        int index=visible.FindIndex(game => game.Id==id);
                        if(original==null || index<0) return;
                        var updated=ArtworkService.ForList(original,settings,listId);
                        if(updated.Art==visible[index].Art && updated.ArtworkProvider==visible[index].ArtworkProvider) return;
                        visible[index]=updated;
                        FrameworkElement previous;
                        if(cards.TryGetValue(id,out previous)) {carousel.Children.Remove(previous); cards.Remove(id); Render(true);}
                    }));
                },message => Dispatcher.BeginInvoke(new Action(() => {if(!cancellation.IsCancellationRequested && !exiting) ShowNotice(message);} ))));
            } catch(OperationCanceledException) {} catch(Exception) {}
        }
    }
}
