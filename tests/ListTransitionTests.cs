using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using PadLauncher;
class ListTransitionTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    static FrameworkElement Find(DependencyObject root,string name) {
        var element=root as FrameworkElement; if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var found=Find(VisualTreeHelper.GetChild(root,i),name); if(found!=null) return found;} return null;
    }
    static void Key(Launcher window,System.Windows.Input.Key key) {
        var source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle);
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,source,0,key) {RoutedEvent=Keyboard.PreviewKeyDownEvent});
    }
    static void Pump(int milliseconds) {
        var frame=new DispatcherFrame(); var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(milliseconds)};
        timer.Tick+=(s,e) => {timer.Stop(); frame.Continue=false;}; timer.Start(); Dispatcher.PushFrame(frame);
    }
    [STAThread] static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"ListTransitionTests-"+Guid.NewGuid().ToString("N")); Storage.Root=root; Directory.CreateDirectory(root);
        Launcher window=null;
        try {
            window=new Launcher(true,true); window.SetAvailableCollections(new[]{"all","couch"}); window.ShowOverlay();
            var content=(FrameworkElement)window.Content; content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            var carousel=(Canvas)Find(content,"GameCarousel"); var viewport=(Grid)Find(content,"CarouselViewport"); var live=(Grid)Find(content,"ListTransitionContent");
            var motion=new Border {Width=100,Height=100}; carousel.Children.Add(motion); Canvas.SetLeft(motion,0);
            content.UpdateLayout();
            Launcher.AnimatePanelProperty(motion,Canvas.LeftProperty,100,true); Pump(70);
            double beforeInterrupt=Canvas.GetLeft(motion);
            Launcher.AnimatePanelProperty(motion,Canvas.LeftProperty,200,true);
            double afterInterrupt=Canvas.GetLeft(motion);
            Check(beforeInterrupt>0 && beforeInterrupt<100 && Math.Abs(afterInterrupt-beforeInterrupt)<1,"Repeated navigation continues from the current animated position: "+beforeInterrupt+" -> "+afterInterrupt);
            Pump(300); Check(Math.Abs(Canvas.GetLeft(motion)-200)<.01,"Panel settles at its real destination after animation completes");
            Launcher.AnimatePanelProperty(motion,Canvas.LeftProperty,25,false);
            Check(Math.Abs(Canvas.GetLeft(motion)-25)<.01,"Disabled animations update panel position immediately"); carousel.Children.Remove(motion);
            var label=(TextBlock)Find(content,"SelectedCollection"); string before=label.Text;
            var original=carousel.Children[0] as FrameworkElement;
            Key(window,System.Windows.Input.Key.PageDown);
            Check(label.Text!=before && carousel.Children.Count==4,"List membership updates immediately while the transition starts");
            Check(window.ListTransitionActive==SystemParameters.ClientAreaAnimation && !window.ListTransitionVertical,"Shoulder/keyboard list changes use horizontal transitions and respect system animation preference");
            Check(!SystemParameters.ClientAreaAnimation || (original.Parent!=carousel && original.Parent!=null),"Outgoing panels are retained as visuals without bitmap snapshots");
            for(int i=0;i<12;i++) Key(window,i%2==0 ? System.Windows.Input.Key.PageUp : System.Windows.Input.Key.PageDown);
            Check(viewport.Children.Count<=2 && label.Text!=before,"Rapid changes replace the current transition without queued or extra layers");
            Pump(260);
            var translation=(TranslateTransform)live.RenderTransform;
            Check(!window.ListTransitionActive && viewport.Children.Count==1 && live.Opacity==1 && translation.X==0 && translation.Y==0,"Completion removes outgoing panels and restores exact opacity and position");
            window.SetVerticalListSwitch("both"); window.NavigateController(new PadState()); window.NavigateController(new PadState {Buttons=1});
            Check(label.Text==before && (!SystemParameters.ClientAreaAnimation || window.ListTransitionVertical),"Up/down list changes use a vertical transition");
            Key(window,System.Windows.Input.Key.Right);
            Check(!window.ListTransitionActive && live.Opacity==1,"Game browsing can interrupt a transition immediately");
            Key(window,System.Windows.Input.Key.PageDown); window.Close();
            Check(!window.ListTransitionActive && viewport.Children.Count==1,"Dismissing the overlay removes the transition layer");
            window.ShowOverlay(); Check(!window.ListTransitionActive && live.Opacity==1 && translation.X==0 && translation.Y==0,"Reopening never resumes a stale transition");
            window.SetAvailableCollections(new[]{"all","source:epic"}); Key(window,System.Windows.Input.Key.PageDown);
            Check(carousel.Children.Count>0 && label.Text!="Epic Games Store","Empty lists are skipped by default");
            Pump(260); Check(viewport.Children.Count==1 && !window.ListTransitionActive,"Skipping an empty list leaves no transition layer");
            var navigationLibrary=new Library();
            navigationLibrary.Games.Add(new Game {Id="one",Name="One",LaunchId="1"});
            navigationLibrary.Games.Add(new Game {Id="two",Name="Two",LaunchId="2"});
            navigationLibrary.Collections.Add(new Collection {Id="first",Name="First",Games=new System.Collections.Generic.List<string>{"one"}});
            navigationLibrary.Collections.Add(new Collection {Id="second",Name="Second",Games=new System.Collections.Generic.List<string>{"two"}});
            Storage.Write("settings.json",new Settings {CollectionId="first"});
            Storage.Write("library.json",navigationLibrary);
            var persisted=new Launcher(false,true);
            try {
                persisted.SetAvailableCollections(new[]{"first","second"}); persisted.ShowOverlay();
                string savedBefore=File.ReadAllText(Path.Combine(Storage.Root,"settings.json"));
                Key(persisted,System.Windows.Input.Key.PageDown);
                Check(File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==savedBefore,"List switching does not synchronously write settings");
                for(int index=0;index<20;index++) Key(persisted,System.Windows.Input.Key.PageDown);
                Pump(500);
                Check(Storage.Read<Settings>("settings.json").CollectionId=="second","Rapid list switches save the final list after browsing pauses");
                Key(persisted,System.Windows.Input.Key.PageDown);
                var binding=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
                var savedSettings=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(persisted);
                navigationLibrary.Collections[0].Games.Add("two");
                typeof(Launcher).GetField("library",binding).SetValue(persisted,navigationLibrary);
                persisted.SetAvailableCollections(new[]{"first","second"});
                Key(persisted,System.Windows.Input.Key.Right);
                string remembered=((TextBlock)Find((FrameworkElement)persisted.Content,"SelectedGameTitle")).Text;
                Key(persisted,System.Windows.Input.Key.PageDown); Key(persisted,System.Windows.Input.Key.PageUp);
                Check(((TextBlock)Find((FrameworkElement)persisted.Content,"SelectedGameTitle")).Text==remembered,"Switching lists restores the selected game for each list");
                savedSettings=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(persisted);
                navigationLibrary.Games[0].LastPlayed=200; navigationLibrary.Games[1].LastPlayed=100;
                savedSettings.SelectedGamesByList["recent"]="two";
                persisted.SetAvailableCollections(new[]{"first","recent"});
                Key(persisted,System.Windows.Input.Key.PageDown);
                Check(((TextBlock)Find((FrameworkElement)persisted.Content,"SelectedGameTitle")).Text.StartsWith("One"),"Recently played ignores saved selection and starts on newest game");
                Key(persisted,System.Windows.Input.Key.Right); Key(persisted,System.Windows.Input.Key.PageUp); Key(persisted,System.Windows.Input.Key.PageDown);
                savedSettings=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(persisted);
                Check(((TextBlock)Find((FrameworkElement)persisted.Content,"SelectedGameTitle")).Text.StartsWith("One") && !savedSettings.SelectedGamesByList.ContainsKey("recent"),"Recently played resets on returning and does not store a selection");
                persisted.SetAvailableCollections(new[]{"first","second"});
                savedSettings=(Settings)typeof(Launcher).GetField("settings",binding).GetValue(persisted);
                savedSettings.ListLayouts=new System.Collections.Generic.Dictionary<string,ListLayout> {{"first",new ListLayout {Layout="grid",PanelScale=1,Columns=2,Rows=1,AngledSizing="fit"}}};
                persisted.SetAvailableCollections(new[]{"first","second"});
                Check((string)typeof(Launcher).GetProperty("ActiveLayout",binding).GetValue(persisted,null)=="grid" && savedSettings.CarouselLayout=="angled","Per-list layout overrides the overlay without changing the global default");
                Key(persisted,System.Windows.Input.Key.PageDown);
                Check((string)typeof(Launcher).GetProperty("ActiveLayout",binding).GetValue(persisted,null)=="angled","Other lists inherit the global layout");
                persisted.ApplyControllerState(new PadState {Buttons=0x0080},"test",0);
                Check(Storage.Read<Settings>("settings.json").FavouriteGameIds.Contains("two"),"R3 always adds the selected game to favourites");
                Check(!Storage.Read<Settings>("settings.json").EnabledCollectionIds.Contains("favourites"),"Adding a favourite preserves a hidden Favourites list");
                persisted.ApplyControllerState(new PadState {Buttons=0x0080},"test",3);
                Check(Storage.Read<Settings>("settings.json").FavouriteGameIds.Count==1,"Holding R3 cannot repeatedly toggle favourites");
                persisted.ApplyControllerState(new PadState(),"test",4);
                persisted.ApplyControllerState(new PadState {Buttons=0x0080},"test",5);
                Check(Storage.Read<Settings>("settings.json").FavouriteGameIds.Count==0,"A second R3 click removes the favourite");
                Key(persisted,System.Windows.Input.Key.PageUp); persisted.ExitApp();
                Check(Storage.Read<Settings>("settings.json").CollectionId=="first","Exiting flushes the final navigation selection");
            } finally {persisted.ExitApp();}
            Console.WriteLine(checks+" list transition checks passed");
        } finally {if(window!=null) window.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
