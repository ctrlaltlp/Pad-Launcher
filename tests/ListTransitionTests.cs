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
            Check(carousel.Children.Count==0 && ((TextBlock)Find(content,"SelectedGameTitle")).Text=="No installed games in this list","Switching to an empty list updates its message immediately");
            Pump(260); Check(viewport.Children.Count==1 && !window.ListTransitionActive,"Empty-list transitions also clean up");
            Console.WriteLine(checks+" list transition checks passed");
        } finally {if(window!=null) window.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
