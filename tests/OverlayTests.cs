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
    [STAThread] public static int Main() {
        Storage.Root=Path.Combine(Path.GetTempPath(),"PadLauncher-overlay-tests-"+Guid.NewGuid().ToString("N"));
        Launcher window=null;
        try {
            var defaults=Storage.Json().Deserialize<Settings>("{\"CollectionId\":\"all\"}");
            Check(defaults.ButtonPrompts=="PlayStation" && defaults.ShowGameTitle,"Older configuration files default to PlayStation prompts and visible title");
            Check(Prompts.Help(defaults).Contains("×") && !Prompts.Help(defaults).Contains("LB"),"PlayStation hint uses cross, circle and L1/R1");
            defaults.ButtonPrompts="Xbox";
            Storage.Write("settings.json",defaults);
            var restored=Storage.Read<Settings>("settings.json");
            Check(Prompts.Help(restored).Contains("LB / RB") && !Prompts.Help(restored).Contains("△"),"Xbox preference survives configuration save and reload");
            var hold=new HoldButton();
            Check(hold.Update(true,0)==ButtonGesture.None && hold.Update(false,200)==ButtonGesture.Tap,"Quick triangle press searches on release");
            Check(hold.Update(true,300)==ButtonGesture.None && hold.Update(true,1300)==ButtonGesture.Hold && hold.Update(true,1400)==ButtonGesture.None && hold.Update(false,1500)==ButtonGesture.None,"Hold refreshes once without opening search on release");
            hold.Update(true,2000); hold.Reset();
            Check(hold.Update(false,4000)==ButtonGesture.None,"Focus loss or controller disconnect cancels a pending gesture");
            Check(Prompts.Help(defaults).Contains("Hold Y") && Prompts.Help(new Settings()).Contains("Hold △"),"Refresh gesture appears in the selected controller hint style");
            var nintendo=new Settings {ButtonPrompts="Nintendo"};
            Check(Prompts.Buttons(nintendo)[0]=="B" && Prompts.Buttons(nintendo)[1]=="A" && Prompts.Buttons(nintendo)[3]=="X" && Prompts.Buttons(nintendo)[4]=="+","Nintendo labels follow the same physical button positions");
            Check(Prompts.Help(nintendo).Contains("B  Play") && Prompts.Help(nintendo).Contains("Hold X"),"Nintendo help uses its face buttons and refresh label");
            window=new Launcher(true,true);
            Check(window.AllowsTransparency && window.Background==Brushes.Transparent && window.WindowStyle==WindowStyle.None,"Native overlay window supports desktop transparency without a frame");
            Check(window.Topmost && !window.ShowInTaskbar,"Overlay stays above desktop without a taskbar entry");
            var content=(FrameworkElement)window.Content;
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            Check(!ContainsButton(content),"Overlay contains no visible or hidden desktop buttons");
            var bitmap=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
            byte[] pixel=new byte[4]; bitmap.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
            Check(pixel[3]==0,"Rendered screen corner is fully transparent");
            bitmap.CopyPixels(new Int32Rect(800,450,1,1),pixel,4,0);
            Check(pixel[3]>0,"Carousel artwork and shadow remain visible over the transparent desktop");
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
