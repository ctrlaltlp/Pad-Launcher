using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Reflection;
using PadLauncher;
class ArtworkRefreshUiTests {
    static FrameworkElement Find(DependencyObject root,string name) {
        var element=root as FrameworkElement; if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var found=Find(VisualTreeHelper.GetChild(root,i),name); if(found!=null) return found;} return null;
    }
    static void Check(bool condition,string message) {if(!condition) throw new Exception(message);}
    [STAThread] static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-artwork-jobs-"+Guid.NewGuid().ToString("N")); Storage.Root=root;
        var fetch=ArtworkService.Fetch; var gate=new ManualResetEventSlim(false); Launcher launcher=null; Window owner=null;
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)};
        var confirmations=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(30)}; var frame=new DispatcherFrame();
        try {
            int imageRequests=0,catalogRequests=0,sgdbRequests=0; bool freshCatalog=false;
            byte[] image; using(var bitmap=new System.Drawing.Bitmap(256,256)) using(var stream=new MemoryStream()) {using(var graphics=System.Drawing.Graphics.FromImage(bitmap)) graphics.Clear(System.Drawing.Color.Lime); bitmap.Save(stream,System.Drawing.Imaging.ImageFormat.Png); image=stream.ToArray();}
            ArtworkService.Fetch=(url,key,token) => {
                if(url.Contains("steamgriddb.com")) {Interlocked.Increment(ref sgdbRequests); throw new Exception("Explicit Libretro refresh must not download SGDB fallback art");}
                if(url.EndsWith("/")) {Interlocked.Increment(ref catalogRequests); return Encoding.UTF8.GetBytes("<a href=\"Game%20A.png\">A</a>"+(freshCatalog ? "<a href=\"Game%20B.png\">B</a>" : ""));}
                Interlocked.Increment(ref imageRequests);
                gate.Wait(token); return image;
            };
            var a=new Game {Id="a",Name="Game A",Source="emulator",EmulatorId="duckstation",LaunchId="a.cue"};
            var b=new Game {Id="b",Name="Game B",Source="emulator",EmulatorId="duckstation",LaunchId="b.cue"};
            var missingGame=new Game {Id="missing",Name="Unmatched game",Source="emulator",EmulatorId="duckstation",LaunchId="missing.cue",ArtworkProvider="cached"};
            missingGame.Art=ArtworkService.ListCachePath(missingGame,"sgdb"); SaveColour(missingGame.Art,System.Drawing.Color.Blue,300,450);
            string aCache=LibretroArtwork.CachePath(a),bCache=LibretroArtwork.CachePath(b);
            SaveColour(aCache,System.Drawing.Color.Red,256,256); SaveColour(bCache,System.Drawing.Color.Red,256,256);
            a.Art=aCache; a.ArtworkProvider="libretro";
            b.Art=ArtworkService.ListCachePath(b,"sgdb"); b.ArtworkProvider="cached"; SaveColour(b.Art,System.Drawing.Color.Blue,300,450);
            File.WriteAllText(bCache+".retry","");
            new LibretroArtwork().Search("Sony - PlayStation","Game A",CancellationToken.None); freshCatalog=true;
            ArtworkKey.Save("test-key");
            Storage.Write("settings.json",new Settings {SteamGridDbEnabled=true,LibretroArtworkEnabled=false,CustomLists=new List<CustomList> {
                new CustomList {Id="list:a",Name="List A",Games=new List<string>{"a"}},new CustomList {Id="list:b",Name="List B",Games=new List<string>{"b","missing"}}
            },ListArtworkSources=new Dictionary<string,string>{{"list:a","libretro-only"},{"list:b","libretro-only"}}});
            Storage.Write("library.json",new Library {Games=new List<Game>{a,b,missingGame}});
            launcher=new Launcher(false,true); owner=launcher.CreateHelpWindow(); owner.Left=-20000; owner.Show();
            ((TabControl)Find(owner,"HelpSettingsTabs")).SelectedIndex=6; owner.UpdateLayout();
            foreach(var expander in LogicalExpanders(owner)) if(expander.Header!=null && Convert.ToString(expander.Header).IndexOf("artwork",StringComparison.OrdinalIgnoreCase)>=0) expander.IsExpanded=true;
            owner.UpdateLayout();
            var picker=(ComboBox)Find(owner,"ListArtworkPicker"); var button=(Button)Find(owner,"RedownloadListArtwork");
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            var jobs=(Dictionary<string,CancellationTokenSource>)typeof(Launcher).GetField("artworkRefreshJobs",flags).GetValue(launcher);
            var statuses=(Dictionary<string,string>)typeof(Launcher).GetField("artworkRefreshStatuses",flags).GetValue(launcher);
            int stage=0; DateTime started=DateTime.UtcNow;
            confirmations.Tick+=(s,e) => {
                var confirm=owner.OwnedWindows.Cast<Window>().FirstOrDefault(window => window.IsVisible && window.Title=="Redownload list artwork");
                if(confirm!=null) {confirm.UpdateLayout(); ((Button)Find(confirm,"ConfirmRedownloadListArtwork")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}
            };
            timer.Tick+=(s,e) => {
                if(DateTime.UtcNow-started>TimeSpan.FromSeconds(12)) throw new Exception("Artwork job UI test timed out at stage "+stage);
                if(stage==0) {stage=1; picker.SelectedValue="list:a"; button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;}
                if(stage==1 && jobs.ContainsKey("list:a")) {
                    Check(!button.IsEnabled,"The running list cannot start a duplicate job");
                    picker.SelectedValue="list:b"; Check(button.IsEnabled,"Another list remains available while the first downloads");
                    stage=2; button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;
                }
                if(stage==2 && jobs.Count==2) {
                    Check(statuses["list:a"].Contains("List A") && statuses["list:b"].Contains("List B"),"Each running list retains its own progress");
                    picker.SelectedValue="all"; Check(button.IsEnabled,"Starting two jobs does not block other lists");
                    stage=3; gate.Set(); return;
                }
                if(stage==3 && jobs.Count==0) {
                    Check(File.Exists(LibretroArtwork.CachePath(a)) && File.Exists(LibretroArtwork.CachePath(b)),"Both independently started list jobs finish");
                    Check(imageRequests==2 && catalogRequests>=3 && sgdbRequests==0,"Each old image and the stale catalogue are fetched again without SGDB fallback requests");
                    using(var updatedA=new System.Drawing.Bitmap(aCache)) using(var updatedB=new System.Drawing.Bitmap(bCache)) Check(updatedA.GetPixel(0,0).G==255 && updatedB.GetPixel(0,0).G==255,"Downloaded bytes replace old red covers with new green covers");
                    var live=(Library)typeof(Launcher).GetField("library",flags).GetValue(launcher);
                    Check(live.Games.Where(game => game.Id!="missing").All(game => game.ArtworkProvider=="libretro") && !File.Exists(bCache+".retry"),"The displayed provider updates and old retry markers are cleared");
                    Check(live.Games.Single(game => game.Id=="missing").Art==null,"An unmatched explicit Libretro refresh does not silently reintroduce a modern fallback cover");
                    var cards=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(launcher);
                    byte[] pixel=new byte[4]; ((System.Windows.Media.Imaging.BitmapSource)cards["a"].Tag).CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
                    Check(pixel[1]==255 && pixel[2]==0,"The actual overlay bitmap updates from the old red cached image to the new green image");
                    Check(statuses["list:a"].Contains("1 refreshed") && statuses["list:a"].Contains("0 without artwork") && statuses["list:b"].Contains("1 refreshed") && statuses["list:b"].Contains("1 without artwork"),"Refresh reports actual successful and unmatched cover counts rather than claiming every game refreshed");
                    Console.WriteLine("Artwork refresh verified: old bytes replaced, fresh catalogues requested, provider updated, no SGDB fallback, result counts correct; independent jobs passed."); frame.Continue=false;
                }
            };
            confirmations.Start(); timer.Start(); Dispatcher.PushFrame(frame);
        } finally {timer.Stop(); confirmations.Stop(); gate.Set(); if(owner!=null) owner.Close(); if(launcher!=null) launcher.ExitApp(); ArtworkService.Fetch=fetch; if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
    static void SaveColour(string path,System.Drawing.Color colour,int width,int height) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); using(var bitmap=new System.Drawing.Bitmap(width,height)) using(var graphics=System.Drawing.Graphics.FromImage(bitmap)) {graphics.Clear(colour); bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);}
    }
    static IEnumerable<Expander> LogicalExpanders(DependencyObject root) {
        var expander=root as Expander; if(expander!=null) yield return expander;
        foreach(var child in LogicalTreeHelper.GetChildren(root)) {var node=child as DependencyObject; if(node!=null) foreach(var item in LogicalExpanders(node)) yield return item;}
    }
}
