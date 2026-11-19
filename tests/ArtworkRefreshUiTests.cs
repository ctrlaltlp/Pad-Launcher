using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using PadLauncher;
class ArtworkRefreshUiTests {
    static IEnumerable<DependencyObject> Elements(DependencyObject root) {
        yield return root;
        foreach(var value in LogicalTreeHelper.GetChildren(root)) {var child=value as DependencyObject; if(child!=null) foreach(var item in Elements(child)) yield return item;}
    }
    [STAThread] static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"ArtworkUi-"+Guid.NewGuid().ToString("N")); Storage.Root=root;
        Launcher launcher=null; Window owner=null;
        try {
            Storage.Write("settings.json",new Settings());
            launcher=new Launcher(true,true); owner=launcher.CreateHelpWindow(); owner.Show();
            var tabs=Elements(owner).OfType<TabControl>().Single(item => item.Name=="HelpSettingsTabs"); tabs.SelectedIndex=6;
            foreach(var section in Elements(owner).OfType<Expander>().ToList()) section.IsExpanded=true;
            owner.UpdateLayout();
            if(Elements(owner).OfType<Button>().Any(button => button.Name=="RedownloadListArtwork" || button.Name=="RedownloadRetroArchArtwork")) throw new Exception("Duplicate redownload actions remain");
            var picker=Elements(owner).OfType<ComboBox>().Single(item => item.Name=="ListArtworkSourceSetting");
            var choices=(IEnumerable<KeyValuePair<string,string>>)picker.ItemsSource;
            if(choices.Single(item => item.Key=="auto").Value!="Automatic") throw new Exception("Automatic artwork choice missing");
            if(!Elements(owner).OfType<TextBlock>().Any(item => item.Text.Contains("Source priority:"))) throw new Exception("Artwork hierarchy explanation missing");
            Console.WriteLine("Artwork UI checks passed: Automatic source, hierarchy note and no duplicate redownload buttons.");
        } finally {if(owner!=null) owner.Close(); if(launcher!=null) launcher.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
