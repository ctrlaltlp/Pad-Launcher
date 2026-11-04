using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PadLauncher;

public static class OverlayTests {
    static int checks;
    static void Check(bool ok,string name) { if (!ok) throw new Exception("FAILED: "+name); checks++; Console.WriteLine("PASS "+name); }
    static bool ContainsButton(DependencyObject root) {
        if (root is Button) return true;
        for (int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) if (ContainsButton(VisualTreeHelper.GetChild(root,i))) return true;
        return false;
    }
    static FrameworkElement FindElement(DependencyObject root,string name) {
        var element=root as FrameworkElement;
        if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {
            var match=FindElement(VisualTreeHelper.GetChild(root,i),name);
            if(match!=null) return match;
        }
        return null;
    }
    [STAThread] public static int Main() {
        Storage.Root=Path.Combine(Path.GetTempPath(),"PadLauncher-overlay-tests-"+Guid.NewGuid().ToString("N"));
        Launcher window=null;
        try {
            var defaults=Storage.Json().Deserialize<Settings>("{\"CollectionId\":\"all\"}");
            Check(defaults.ButtonPrompts=="PlayStation" && defaults.ShowGameTitle,"Older configuration files default to PlayStation prompts and visible title");
            Check(defaults.OpenMonitor=="cursor","Older configuration files default to the cursor's monitor");
            Check(defaults.DefaultCollectionId=="remember" && defaults.CarouselScale==1,"Older settings preserve last-collection behaviour, normal size");
            Check(Prompts.Help(defaults).Contains("×") && !Prompts.Help(defaults).Contains("LB"),"PlayStation hint uses cross, circle and L1/R1");
            defaults.ButtonPrompts="Xbox";
            Storage.Write("settings.json",defaults);
            var restored=Storage.Read<Settings>("settings.json");
            restored.OpenMonitor="\\\\.\\DISPLAY2"; Storage.Write("settings.json",restored);
            Check(Storage.Read<Settings>("settings.json").OpenMonitor=="\\\\.\\DISPLAY2","Selected display preference survives saving and reopening");
            Check(Prompts.Help(restored).Contains("LB / RB") && !Prompts.Help(restored).Contains("△"),"Xbox preference survives configuration save and reload");
            var hold=new HoldButton();
            Check(hold.Update(true,0)==ButtonGesture.None && hold.Update(false,200)==ButtonGesture.Tap,"Quick button press produces a tap on release");
            Check(hold.Update(true,300)==ButtonGesture.None && hold.Update(true,1300)==ButtonGesture.Hold && hold.Update(true,1400)==ButtonGesture.None && hold.Update(false,1500)==ButtonGesture.None,"Hold triggers once and does not also trigger a tap on release");
            hold.Update(true,2000); hold.Reset();
            Check(hold.Update(false,4000)==ButtonGesture.None,"Focus loss or controller disconnect cancels a pending gesture");
            Check(Prompts.Help(defaults).Contains("Tap Y") && Prompts.Help(new Settings()).Contains("Tap △"),"Tap refresh appears in the selected controller hint style");
            var nintendo=new Settings {ButtonPrompts="Nintendo"};
            Check(Prompts.Buttons(nintendo)[0]=="B" && Prompts.Buttons(nintendo)[1]=="A" && Prompts.Buttons(nintendo)[3]=="X" && Prompts.Buttons(nintendo)[4]=="+","Nintendo labels follow the same physical button positions");
            Check(Prompts.Help(nintendo).Contains("B  Play") && Prompts.Help(nintendo).Contains("Tap X"),"Nintendo help uses its face buttons and refresh label");
            window=new Launcher(true,true);
            Check(window.AllowsTransparency && window.Background==Brushes.Transparent && window.WindowStyle==WindowStyle.None,"Native overlay window supports desktop transparency without a frame");
            Check(window.Topmost && !window.ShowInTaskbar,"Overlay stays above desktop without a taskbar entry");
            var content=(FrameworkElement)window.Content;
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            Check(!ContainsButton(content),"Overlay contains no visible or hidden desktop buttons");
            var gameTitle=FindElement(content,"SelectedGameTitle");
            Check(defaults.ShowCollectionName,"Older settings keep the collection name visible");
            window.SetShowCollectionName(false);
            Check(FindElement(content,"SelectedCollection").Visibility==Visibility.Collapsed && !Storage.Read<Settings>("settings.json").ShowCollectionName,"Collection name can be hidden immediately and the preference is saved");
            window.SetShowCollectionName(true);
            Check(FindElement(content,"SelectedCollection").Visibility==Visibility.Visible && Storage.Read<Settings>("settings.json").ShowCollectionName,"Collection name can be restored immediately and the preference is saved");
            window.SetShowGameTitle(false);
            Check(gameTitle.Visibility==Visibility.Collapsed && !Storage.Read<Settings>("settings.json").ShowGameTitle,"Show game title setting hides the title immediately and saves the choice");
            window.SetShowGameTitle(true);
            Check(gameTitle.Visibility==Visibility.Visible && Storage.Read<Settings>("settings.json").ShowGameTitle,"Show game title setting can be restored immediately");
            var bitmap=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
            byte[] pixel=new byte[4]; bitmap.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
            Check(pixel[3]==0,"Rendered screen corner is fully transparent");
            bitmap.CopyPixels(new Int32Rect(800,450,1,1),pixel,4,0);
            Check(pixel[3]>0,"Carousel artwork and shadow remain visible over the transparent desktop");
            window.SetCarouselScale(.8);
            var transform=FindElement(content,"GameCarousel").RenderTransform as ScaleTransform;
            Check(transform!=null && transform.ScaleX==.8 && Storage.Read<Settings>("settings.json").CarouselScale==.8,"Carousel size is applied immediately and saved");
            bool invalidScale=false; try { window.SetCarouselScale(2); } catch(ArgumentOutOfRangeException) { invalidScale=true; }
            Check(invalidScale && Storage.Read<Settings>("settings.json").CarouselScale==.8,"Out-of-range carousel sizes do not overwrite the last valid size");
            window.SetCarouselScale(1);
            Check(defaults.CarouselLayout=="angled","Older settings preserve the original angled layout");
            window.SetCarouselLayout("rectangular");
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            var panels=(Canvas)FindElement(content,"GameCarousel");
            var panelBody=(Grid)((Grid)panels.Children[0]).Children[0];
            Check(panelBody.Clip.FillContains(new Point(1,1)) && Storage.Read<Settings>("settings.json").CarouselLayout=="rectangular","Rectangular panels have square corners and the choice is saved");
            window.SetCarouselLayout("angled");
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            panelBody=(Grid)((Grid)panels.Children[0]).Children[0];
            Check(!panelBody.Clip.FillContains(new Point(1,1)) && Storage.Read<Settings>("settings.json").CarouselLayout=="angled","Switching back restores the original slanted panels");
            bool invalidLayout=false; try { window.SetCarouselLayout("unknown"); } catch(ArgumentException) { invalidLayout=true; }
            Check(invalidLayout && Storage.Read<Settings>("settings.json").CarouselLayout=="angled","Invalid layouts preserve the saved choice");
            window.SetDefaultCollection("couch"); window.ShowOverlay();
            Check(((TextBlock)FindElement(content,"SelectedCollection")).Text=="Couch games","Chosen default collection is used when reopening the overlay");
            window.Close(); window.SetDefaultCollection("all"); window.ShowOverlay();
            Check(((TextBlock)FindElement(content,"SelectedCollection")).Text=="All games","All games can be selected as the opening collection");
            window.Close(); window.SetDefaultCollection("remember");
            window.ShowControls(); // Exercises optional hint with no settings dialog.
            var resident=new Launcher(true,true);
            resident.PrepareTray();
            Check(resident.Idle,"Tray startup prepares the overlay without showing it or polling the controller");
            resident.Close();
            Check(resident.Idle,"Dismissing the overlay leaves it idle with controller polling stopped");
            object retainedContent=resident.Content;
            resident.ToggleOverlay();
            Check(resident.IsVisible && Object.ReferenceEquals(retainedContent,resident.Content),"Reopening the overlay reuses the existing visual tree");
            resident.ToggleOverlay();
            Check(resident.Idle,"Reopened overlay can be dismissed back to idle again");
            resident.ExitApp();
            string steam=Path.Combine(Storage.Root,"steam"),libraryOnly=Path.Combine(Storage.Root,"library-only");
            Directory.CreateDirectory(Path.Combine(steam,"steamapps")); File.WriteAllBytes(Path.Combine(steam,"steam.exe"),new byte[0]);
            Directory.CreateDirectory(Path.Combine(libraryOnly,"steamapps"));
            string savedBefore=File.ReadAllText(Path.Combine(Storage.Root,"settings.json"));
            bool rejected=false; try { window.SetSteamFolder(libraryOnly); } catch(DirectoryNotFoundException) { rejected=true; }
            Check(rejected && File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==savedBefore,"A library drive without steam.exe is rejected without changing saved settings");
            string selectedSteam=window.SetSteamFolder(steam);
            var steamSettings=Storage.Read<Settings>("settings.json");
            Check(selectedSteam==Path.GetFullPath(steam) && steamSettings.SteamPath==selectedSteam,"Steam folder setting validates and persists the selected installation");
            Check(steamSettings.UserId==null && steamSettings.CollectionId=="all" && steamSettings.SelectedId==null,"Changing Steam installations resets profile and collection selection for rediscovery");
            Directory.CreateDirectory(Path.Combine(steam,"userdata","123")); Directory.CreateDirectory(Path.Combine(steam,"userdata","456"));
            window.SetSteamUserId("456");
            Check(Storage.Read<Settings>("settings.json").UserId=="456","Selected local Steam user ID is saved");
            string userBefore=File.ReadAllText(Path.Combine(Storage.Root,"settings.json"));
            bool invalidUser=false; try { window.SetSteamUserId("999"); } catch(InvalidOperationException) { invalidUser=true; }
            Check(invalidUser && File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==userBefore,"User IDs outside the selected installation are rejected without changing settings");
            window.SetSteamUserId(null);
            Check(Storage.Read<Settings>("settings.json").UserId==null,"Automatic Steam profile selection is saved without a fixed user ID");
            string legacy=Path.Combine(Storage.Root,"legacy"); Directory.CreateDirectory(legacy);
            string oldSettings=Path.Combine(legacy,"settings.json"); File.WriteAllText(oldSettings,"{\"ButtonPrompts\":\"Nintendo\"}");
            File.SetLastWriteTimeUtc(oldSettings,DateTime.UtcNow.AddMinutes(1));
            Storage.MigrateFrom(legacy);
            Check(Storage.Read<Settings>("settings.json").ButtonPrompts=="Nintendo","Renamed app imports the latest existing button preference");
            File.WriteAllText(Path.Combine(Storage.Root,"settings.json"),"{\"ButtonPrompts\":\"PlayStation\"}");
            File.SetLastWriteTimeUtc(Path.Combine(Storage.Root,"settings.json"),DateTime.UtcNow.AddMinutes(2));
            Storage.MigrateFrom(legacy);
            Check(Storage.Read<Settings>("settings.json").ButtonPrompts=="PlayStation","Migration preserves settings already changed in the renamed app");
            Console.WriteLine(checks+" overlay checks passed."); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally {
            if (window!=null) window.ExitApp();
            if (Directory.Exists(Storage.Root) && Path.GetFileName(Storage.Root).StartsWith("PadLauncher-overlay-tests-")) Directory.Delete(Storage.Root,true);
        }
    }
}
