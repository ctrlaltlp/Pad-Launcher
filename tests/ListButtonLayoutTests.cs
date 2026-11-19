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
            Storage.Write("settings.json",new Settings {CustomLists=new List<CustomList> {new CustomList {Id="list:test",Name="Test list",Games=new List<string>{"1"}},new CustomList {Id="list:other",Name="Other list",Games=new List<string>{"2"}}}});
            Storage.Write("library.json",new Library {Games=new List<Game>{new Game {Id="1",Name="Game One",Source="steam"},new Game {Id="2",Name="Game Two",Source="steam"}}});
            launcher=new Launcher(false,true); window=launcher.CreateHelpWindow(); window.Left=-20000; window.Show();
            if(!window.Topmost || !launcher.Topmost) throw new Exception("Settings and the overlay must stay topmost.");
            ((TabControl)Find(window,"HelpSettingsTabs")).SelectedIndex=1; window.UpdateLayout();
            ((Expander)Find(window,"ListEditorSection")).IsExpanded=true; window.UpdateLayout();
            ((Button)Find(window,"ListEditorMode_edit")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
            ((ComboBox)Find(window,"CustomListPicker")).SelectedValue="list:test"; window.UpdateLayout();
            var games=(StackPanel)Find(window,"ListEditorGameChoices");
            Func<int> visibleGames=() => games.Children.OfType<StackPanel>().Count(gameRow => gameRow.Visibility==Visibility.Visible);
            var added=(Button)Find(window,"ShowAddedListGames");
            if(visibleGames()!=2) throw new Exception("Edit must initially show all games");
            added.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(visibleGames()!=1) throw new Exception("Added games toggle must filter to checked members");
            ((TextBox)Find(window,"ListEditorGameSearch")).Text="Two";
            if(visibleGames()!=0) throw new Exception("Added games and name filter must work together");
            added.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(visibleGames()!=1) throw new Exception("Turning added filter off must restore name-filtered games");
            ((TextBox)Find(window,"ListEditorGameSearch")).Clear();
            if(((Button)Find(window,"SelectAllListGames")).Visibility!=Visibility.Collapsed) throw new Exception("Select all must be removed from Edit");
            added.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((Button)Find(window,"UncheckAddedListGames")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(visibleGames()!=0 || !Storage.Read<Settings>("settings.json").CustomLists.Single(list => list.Id=="list:test").Games.Contains("1")) throw new Exception("Uncheck added must affect only the unsaved draft");
            ((Button)Find(window,"UncheckAddedListGames")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            if(visibleGames()!=1 || ((Button)Find(window,"UncheckAddedListGames")).Content.ToString()!="Uncheck added") throw new Exception("Second toggle must restore exactly the previous checked games");
            ((Button)Find(window,"UncheckAddedListGames")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            ((ComboBox)Find(window,"CustomListPicker")).SelectedValue="list:other"; window.UpdateLayout();
            if(((Button)Find(window,"UncheckAddedListGames")).Content.ToString()!="Uncheck added") throw new Exception("Changing lists must reset the undo snapshot");


            ((Button)Find(window,"ListEditorMode_duplicate")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
            ((ComboBox)Find(window,"CustomListPicker")).SelectedValue="list:test"; window.UpdateLayout();
            if(((Button)Find(window,"DuplicateCustomList")).Visibility!=Visibility.Visible || ((TextBox)Find(window,"CustomListName")).Text!="Test list copy") throw new Exception("Duplicate tab must provide an independent copy name and action");
            ((Button)Find(window,"ListEditorMode_delete")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
            ((ComboBox)Find(window,"CustomListPicker")).SelectedValue="list:test"; window.UpdateLayout();
            var unregister=(Button)Find(window,"DeleteListAndGames"); var row=(WrapPanel)unregister.Parent;
            var delete=row.Children.OfType<Button>().Single(button => Convert.ToString(button.Content)=="Delete list");
            Point a=delete.TranslatePoint(new Point(),row),b=unregister.TranslatePoint(new Point(),row);
            if(unregister.Visibility!=Visibility.Visible || Math.Abs(a.Y-b.Y)>.5 || b.X<a.X+delete.ActualWidth || b.X+unregister.ActualWidth>row.ActualWidth+.5) throw new Exception("List deletion buttons must fit beside one another at the default Settings width. Width="+window.Width+", row="+row.ActualWidth+", delete="+a+", unregister="+b);
            ((Button)Find(window,"ListEditorMode_combine")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
            if(Find(window,"CombinedListName")==null || Find(window,"SaveCombinedList")==null) throw new Exception("Combine button must reveal live source-list settings inside the editor");
            ((Button)Find(window,"ListEditorMode_merge")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
            var disposition=(ComboBox)Find(window,"MergeSourceDisposition");
            if((string)disposition.SelectedValue!="keep") throw new Exception("Merge must keep source lists by default");
            var sources=(StackPanel)Find(window,"MergeSources");
            foreach(var box in sources.Children.OfType<CheckBox>().Where(box => (string)box.Tag=="list:test" || (string)box.Tag=="list:other")) box.IsChecked=true;
            disposition.SelectedValue="choose"; window.UpdateLayout();
            var deleteChoices=(StackPanel)Find(window,"MergeDeleteChoices");
            if(deleteChoices.Visibility!=Visibility.Visible || deleteChoices.Children.OfType<CheckBox>().Count()!=2) throw new Exception("Selective deletion must show selected source lists");
            deleteChoices.Children.OfType<CheckBox>().Single(box => (string)box.Tag=="list:test").IsChecked=true;
            ((TextBox)Find(window,"MergeListName")).Text="Merged test";
            bool confirmationSeen=false;
            var cancelMerge=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
            cancelMerge.Tick+=(s,e) => {
                var prompt=window.OwnedWindows.Cast<Window>().FirstOrDefault(dialog => dialog.IsVisible && dialog.Title=="Merge lists"); if(prompt==null) return;
                confirmationSeen=true; cancelMerge.Stop(); prompt.Close();
            };
            cancelMerge.Start(); try {((Button)Find(window,"MergeLists")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));} finally {cancelMerge.Stop();}
            if(!confirmationSeen || Storage.Read<Settings>("settings.json").CustomLists.Count!=2) throw new Exception("Cancelling merge deletion confirmation must not change lists");
            if(Find(window,"MergeListName")==null || Find(window,"MergeLists")==null) throw new Exception("Merge mode does not show its own fields");
            ((Button)Find(window,"ListEditorMode_create")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); window.UpdateLayout();
            if(((TextBox)Find(window,"CustomListName")).Text!="" || ((Button)Find(window,"SaveCustomList")).Content.ToString()!="Create list") throw new Exception("Create mode retained an edited list");
            var content=(FrameworkElement)window.Content;
            var preview=new System.Windows.Media.Imaging.RenderTargetBitmap((int)window.ActualWidth,(int)window.ActualHeight,96,96,PixelFormats.Pbgra32); preview.Render(content);
            var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder(); encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(preview)); using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"List-editor-preview.png"))) encoder.Save(stream);
            Console.WriteLine("List deletion buttons fit on the same row at Settings width "+window.Width+".");
        } finally {if(window!=null) window.Close(); if(launcher!=null) launcher.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
