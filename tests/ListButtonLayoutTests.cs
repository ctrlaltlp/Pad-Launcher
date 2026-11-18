using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PadLauncher;
class ListButtonLayoutTests {
    static FrameworkElement Find(DependencyObject root,string name) {
        var element=root as FrameworkElement; if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var found=Find(VisualTreeHelper.GetChild(root,i),name); if(found!=null) return found;} return null;
    }
    [STAThread] static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-list-layout-"+Guid.NewGuid().ToString("N")); Storage.Root=root;
        Launcher launcher=null; Window window=null;
        try {
            Storage.Write("settings.json",new Settings {CustomLists=new List<CustomList> {new CustomList {Id="list:test",Name="Test list",Games=new List<string>{"1"}}}});
            Storage.Write("library.json",new Library {Games=new List<Game>{new Game {Id="1",Name="Game",Source="steam"}}});
            launcher=new Launcher(false,true); window=launcher.CreateHelpWindow(); window.Left=-20000; window.Show();
            if(!window.Topmost || !launcher.Topmost) throw new Exception("Settings and the overlay must stay topmost.");
            ((TabControl)Find(window,"HelpSettingsTabs")).SelectedIndex=1; window.UpdateLayout();
            ((Expander)Find(window,"EditListGamesSection")).IsExpanded=true; window.UpdateLayout();
            ((ComboBox)Find(window,"CustomListPicker")).SelectedValue="list:test"; window.UpdateLayout();
            var unregister=(Button)Find(window,"DeleteListAndGames"); var row=(WrapPanel)unregister.Parent;
            var delete=row.Children.OfType<Button>().Single(button => Convert.ToString(button.Content)=="Delete list");
            Point a=delete.TranslatePoint(new Point(),row),b=unregister.TranslatePoint(new Point(),row);
            if(unregister.Visibility!=Visibility.Visible || Math.Abs(a.Y-b.Y)>.5 || b.X<a.X+delete.ActualWidth || b.X+unregister.ActualWidth>row.ActualWidth+.5) throw new Exception("List deletion buttons must fit beside one another at the default Settings width. Width="+window.Width+", row="+row.ActualWidth+", delete="+a+", unregister="+b);
            Console.WriteLine("List deletion buttons fit on the same row at Settings width "+window.Width+".");
        } finally {if(window!=null) window.Close(); if(launcher!=null) launcher.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
