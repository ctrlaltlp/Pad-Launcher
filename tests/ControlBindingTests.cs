using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using PadLauncher;
class ControlBindingTests {
    static int checks;
    static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message) {if(!value) throw new Exception(message); checks++;}
    static IEnumerable<DependencyObject> Children(DependencyObject node) {
        yield return node; foreach(var item in LogicalTreeHelper.GetChildren(node)) {var child=item as DependencyObject; if(child!=null) foreach(var nested in Children(child)) yield return nested;}
    }
    static T Find<T>(DependencyObject root,string name) where T:FrameworkElement {return Children(root).OfType<T>().Single(item => item.Name==name);}
    static void KeyPress(Window window,Key key) {
        var handle=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
        window.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,System.Windows.Interop.HwndSource.FromHwnd(handle),0,key) {RoutedEvent=Keyboard.PreviewKeyDownEvent});
    }
    static Settings State(Launcher window) {return (Settings)typeof(Launcher).GetField("settings",Flags).GetValue(window);}
    static void AssignFromDiagram(Window editor,string input,string action,bool apply) {
        bool visited=false; var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(s,e) => {
            var picker=editor.OwnedWindows.Cast<Window>().FirstOrDefault(window => window.Name=="DiagramAssignmentWindow"); if(picker==null) return;
            timer.Stop(); visited=true; Find<ComboBox>(picker,"DiagramActionPicker").SelectedValue=action;
            if(apply) Find<Button>(picker,"ApplyDiagramAction").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); else picker.Close();
        };
        timer.Start(); try {Find<Button>(editor,"DiagramAssign_overlay_"+input).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));} finally {timer.Stop();}
        Check(visited,"Clickable diagram opens the themed action picker");
    }
    static void EditKeys(Window editor) {
        bool visited=false; var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
        timer.Tick+=(s,e) => {
            var dialog=editor.OwnedWindows.Cast<Window>().FirstOrDefault(item => item.Name=="KeyboardShortcutsWindow"); if(dialog==null) return;
            timer.Stop(); visited=true; var capture=Find<Button>(dialog,"ControlKey_overlay_favourite"); capture.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(!Find<Button>(dialog,"ApplyKeyboardShortcuts").IsEnabled,"Keyboard Apply waits for shortcut capture");
            KeyPress(dialog,Key.K); Check(Convert.ToString(capture.Content)=="K","Keyboard editor captures the new shortcut");
            Find<Button>(dialog,"ApplyKeyboardShortcuts").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        };
        timer.Start(); try {Find<Button>(editor,"EditKeyboard_overlay").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));} finally {timer.Stop();}
        Check(visited,"Keyboard shortcuts remains accessible without the mapping subtab");
    }
    static void Editor(Launcher launcher,Window owner,bool restore,bool save) {
        string before=File.ReadAllText(Path.Combine(Storage.Root,"settings.json")); bool visited=false;
        var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(25)};
        timer.Tick+=(s,e) => {
            var dialog=owner.OwnedWindows.Cast<Window>().FirstOrDefault(window => window.Name=="ChangeControlsWindow" && window.IsVisible); if(dialog==null) return;
            timer.Stop(); visited=true;
            Check(dialog.ShowInTaskbar,"Change controls has a taskbar entry");
            Check(owner.IsEnabled,"Main Settings remains enabled while Change controls is open");
            Find<Button>(owner,"ChangeControls").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(owner.OwnedWindows.Cast<Window>().Count(item => item.Name=="ChangeControlsWindow")==1,"Reopening focuses the existing editor without discarding its draft");
            if(restore) {
                dialog.WindowState=WindowState.Minimized;
                Check(dialog.WindowState==WindowState.Minimized && dialog.ShowInTaskbar,"Minimized Change controls retains its taskbar entry");
                dialog.WindowState=WindowState.Normal; dialog.Activate();
            }
            var contexts=Find<TabControl>(dialog,"ControlBindingContexts");
            Check(contexts.Items.Count==3 && Children(dialog).OfType<TabControl>().Count()==1,"Only the three context tabs remain, with no nested tabs");
            foreach(string context in ControlBindings.Contexts) {
                var diagram=Find<StackPanel>(dialog,"ControllerDiagram_"+context);
                Check(diagram!=null && diagram.Cursor==Cursors.Arrow,"Diagram background uses the normal arrow cursor");
                Check(Find<Button>(dialog,"DiagramAssign_"+context+"_rt").Cursor==Cursors.Hand,"Clickable labels use the hand cursor");
                Check(Find<Button>(dialog,"EditKeyboard_"+context)!=null,"Each diagram retains keyboard shortcut access");
            }
            contexts.SelectedIndex=0; dialog.UpdateLayout();
            var styles=Find<StackPanel>(dialog,"ButtonStyleChoices");
            var originalStyle=styles.Children.OfType<Button>().First(button => (string)button.Content==Prompts.Style(State(launcher)));
            var alternateStyle=styles.Children.OfType<Button>().First(button => button!=originalStyle);
            string alternateName=(string)alternateStyle.Content;
            alternateStyle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var styledDiagram=Find<StackPanel>(dialog,"ControllerDiagram_overlay");
            Check(((TextBlock)styledDiagram.Children[0]).Text.StartsWith(alternateName+" ·") && File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==before,"Button style previews immediately and stays staged until Save changes");
            originalStyle.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            dialog.UpdateLayout();

            var fitted=Find<Viewbox>(dialog,"ControlPreviewFit_overlay"); var host=(FrameworkElement)fitted.Parent;
            Check(fitted.ActualHeight<=host.ActualHeight+1 && fitted.ActualWidth<=host.ActualWidth+1,"Full controller diagram fits without scrolling");
            if(restore) Find<Button>(dialog,"RestoreControlBindings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            else {
                AssignFromDiagram(dialog,"rt","search",false);
                Check(Find<TextBlock>(dialog,"DiagramLabel_overlay_rt").Text.Contains("Next monitor"),"Cancelling the action picker preserves its mapping");
                AssignFromDiagram(dialog,"rt","favourite",true);
                var yellow=Color.FromRgb(255,214,102);
                Check(Find<TextBlock>(dialog,"DiagramLabel_overlay_rt").Text.Contains("Favourite") && Find<TextBlock>(dialog,"DiagramLabel_overlay_rs").Text.Contains("Favourite"),"Same action remains assigned to both buttons");
                Check(((SolidColorBrush)Find<TextBlock>(dialog,"DiagramLabel_overlay_rt").Foreground).Color==yellow && ((SolidColorBrush)Find<TextBlock>(dialog,"DiagramLabel_overlay_rs").Foreground).Color==yellow,"Only duplicate action labels become yellow");
                Check(((SolidColorBrush)Find<TextBlock>(dialog,"DiagramLabel_overlay_north").Foreground).Color!=yellow && Find<Button>(dialog,"SaveControlBindings").IsEnabled,"Single actions stay mint and duplicate actions remain saveable");
                AssignFromDiagram(dialog,"rt","monitor",true);
                Check(((SolidColorBrush)Find<TextBlock>(dialog,"DiagramLabel_overlay_rs").Foreground).Color!=yellow,"Removing a duplicate returns the remaining label to mint");
                AssignFromDiagram(dialog,"south","favourite",true); AssignFromDiagram(dialog,"rs","none",true);
                EditKeys(dialog);
                Check(File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==before,"Controller and keyboard edits stay staged until Save changes");
            }
            dialog.UpdateLayout(); var content=(FrameworkElement)dialog.Content;
            var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth)+48,(int)Math.Ceiling(content.ActualHeight)+48,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
            var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using(var output=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Change-controls-preview.png"))) png.Save(output);
            if(save) Find<Button>(dialog,"SaveControlBindings").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); else dialog.Close();
        };
        timer.Start(); try {
            Find<Button>(owner,"ChangeControls").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            var editor=owner.OwnedWindows.Cast<Window>().Single(item => item.Name=="ChangeControlsWindow");
            var frame=new DispatcherFrame(); editor.Closed+=(s,e) => frame.Continue=false; Dispatcher.PushFrame(frame);
        } finally {timer.Stop();}
        Check(visited,"Show/Change controls opens the diagram editor");
        if(!save) Check(File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==before,"Cancelling leaves saved settings unchanged");
    }
    [STAThread] static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-bindings-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher window=null; Window owner=null;
        try {
            var legacy=new Settings {ControlButtons=new Dictionary<string,string>{{"typing.finish","north"},{"typing.cancel","none"},{"results.keyboard","north"},{"results.cancel","east"}}};
            ControlBindings.UpgradeSearchDefaults(legacy);
            Check(ControlBindings.Get(legacy,"typing.cancel")=="east" && ControlBindings.Get(legacy,"typing.finish")=="none" && ControlBindings.Get(legacy,"results.cancel")=="east","Saved old search defaults upgrade to Circle exit");
            legacy.ControlButtons["typing.finish"]="north"; ControlBindings.UpgradeSearchDefaults(legacy);
            Check(ControlBindings.Get(legacy,"typing.finish")=="north","Later custom search mappings are preserved");
            var previousDefaults=new Settings {ControlBindingVersion=3,ControlButtons=new Dictionary<string,string>{{"overlay.layout","rt"},{"overlay.monitor","west"}}};
            ControlBindings.UpgradeSearchDefaults(previousDefaults);
            Check(previousDefaults.ControlButtons["overlay.layout"]=="west" && previousDefaults.ControlButtons["overlay.monitor"]=="rt","Saved overlay defaults upgrade to Square layout and R2 monitor");
            var customTrigger=new Settings {ControlBindingVersion=4,ControlButtons=new Dictionary<string,string>{{"overlay.layout","west"},{"overlay.monitor","rt"},{"overlay.favourite","lt"}}};
            ControlBindings.UpgradeSearchDefaults(customTrigger);
            Check(ControlBindings.Get(customTrigger,"overlay.favourite")=="lt" && ControlBindings.Get(customTrigger,"overlay.layout")=="west" && ControlBindings.Conflict(customTrigger)==null,"Upgrading defaults preserves an occupied custom trigger without creating conflicts");
            var settings=new Settings();
            Check(ControlBindings.Conflict(settings)==null,"Default controller and keyboard bindings are conflict-free");
            Check(ControlBindings.Get(settings,"results.monitor")=="rt" && ControlBindings.Get(settings,"results.focus")=="lt","Search results assign R2 to monitor and L2 to keyboard");
            Check(ControlBindings.Get(settings,"overlay.layout")=="west" && ControlBindings.Get(settings,"overlay.monitor")=="rt","Overlay defaults use Square for layout and R2 for monitor");
            Check(ControlBindings.Get(settings,"typing.backspace")=="west" && ControlBindings.Get(settings,"typing.layout")=="rt","Search keyboard keeps Square for Backspace and uses R2 for layout");
            Check(!Prompts.Help(settings).Contains("(test)"),"Layout toggle hints no longer carry a test label");
            Check(ControlBindings.Get(settings,"overlay.dismiss")=="east" && ControlBindings.Get(settings,"overlay.exit")=="east","Tap and hold exit retain their shared default button");
            Check(ControlBindings.KeyAction(settings,"overlay",Key.PageDown,ModifierKeys.None)=="next" && ControlBindings.KeyAction(settings,"overlay",Key.PageUp,ModifierKeys.None)=="previous","PageUp/PageDown aliases retain navigation shortcuts");
            Check(ControlBindings.KeyAction(settings,"overlay",Key.F,ModifierKeys.Control)=="search" && ControlBindings.KeyAction(settings,"overlay",Key.F,ModifierKeys.None)=="favourite","Ctrl+F and F remain distinct shortcuts");
            Check(ControlBindings.KeyAction(settings,"typing",Key.F,ModifierKeys.None)==null,"Ordinary typing is not intercepted by an overlay-only shortcut");
            settings.ControlKeys=new Dictionary<string,string>{{"overlay.favourite","Ctrl+F"}};
            Check(ControlBindings.Conflict(settings)!=null,"Conflicting keyboard shortcuts are rejected"); settings.ControlKeys=null;
            settings.ControlButtons=new Dictionary<string,string>{{"typing.cancel","north"}};
            Check(ControlBindings.Conflict(settings)!=null,"Search keyboard conflicts are checked independently");
            settings.ControlButtons=new Dictionary<string,string>{{"overlay.play","invalid"}};
            Check(ControlBindings.Get(settings,"overlay.play")=="south","Unknown saved controller codes fall back safely");
            settings.ButtonPrompts="Nintendo";
            Check(ControlBindings.Label(settings,"overlay.play")=="B","Prompt styles change labels without changing the physical binding"); settings.ButtonPrompts="PlayStation";
            settings.ControlButtons=null;
            Storage.Write("settings.json",settings); window=new Launcher(true,true); owner=window.CreateHelpWindow(); owner.Left=-20000; owner.Show();
            Editor(window,owner,false,false);
            Editor(window,owner,false,true);
            var saved=Storage.Read<Settings>("settings.json");
            Check(saved.ControlButtons["overlay.play"]=="none" && saved.ControlButtons["overlay.favourite"]=="south" && saved.ControlKeys["overlay.favourite"]=="K","Controller and keyboard mappings persist together");
            Check(ControlBindings.KeyAction(saved,"overlay",Key.K,ModifierKeys.None)=="favourite" && ControlBindings.KeyAction(saved,"overlay",Key.F,ModifierKeys.None)==null,"Remapped keyboard shortcut replaces the previous key");
            window.ApplyControllerState(new PadState(),"mapping-test",0); window.ApplyControllerState(new PadState {Buttons=0x1000},"mapping-test",1);
            Check(State(window).FavouriteGameIds.Count==1,"The remapped controller button performs its new action");
            window.ApplyControllerState(new PadState {Buttons=0x1000},"mapping-test",2);
            Check(State(window).FavouriteGameIds.Count==1,"A held remapped button does not repeat toggle actions");
            window.ApplyControllerState(new PadState(),"mapping-test",3); window.ApplyControllerState(new PadState {Buttons=0x80},"mapping-test",4);
            Check(State(window).FavouriteGameIds.Count==1,"The former favourites button is no longer active in this context");
            KeyPress(window,Key.K); Check(State(window).FavouriteGameIds.Count==0,"The remapped keyboard key dispatches the real favourite action");
            Check(Prompts.Help(State(window)).Contains("✕  Toggle favourite"),"On-screen hints use the remapped controller label");
            Editor(window,owner,true,false); Check(Storage.Read<Settings>("settings.json").ControlKeys["overlay.favourite"]=="K","Cancelling Restore defaults preserves saved mappings");
            Editor(window,owner,true,true);
            Check(ControlBindings.Get(State(window),"overlay.play")=="south" && ControlBindings.Get(State(window),"overlay.favourite")=="rs" && ControlBindings.Get(State(window),"overlay.favourite",true)=="F","Restore defaults returns both input types to the current scheme");
            // Context-specific remapping must affect search without changing the query.
            State(window).ControlButtons["typing.cancel"]="none"; State(window).ControlButtons["typing.finish"]="east";
            window.OpenGameSearch(); window.SetGameSearchQuery("");
            window.HandleGameSearchController(new PadState(),10); window.HandleGameSearchController(new PadState {Buttons=0x2000},11);
            Check(window.SearchGamesFocused && window.GameSearchText=="","Search uses its own remapped finish action");
            typeof(Launcher).GetMethod("ResumeSearchKeyboard",Flags).Invoke(window,null);
            State(window).ControlKeys["typing.space"]="K";
            KeyPress((Window)typeof(Launcher).GetField("gameSearchWindow",Flags).GetValue(window),Key.K);
            Check(window.GameSearchText==" ","A search keyboard shortcut dispatches its context-specific action");
            ((Window)typeof(Launcher).GetField("gameSearchWindow",Flags).GetValue(window)).Close();
            State(window).ControlButtons["overlay.play"]="lt";
            var remap=typeof(Launcher).GetMethod("RemapController",Flags);
            var high=(PadState)remap.Invoke(window,new object[]{new PadState {LT=200},"overlay"});
            var middle=(PadState)remap.Invoke(window,new object[]{new PadState {LT=110},"overlay"});
            var low=(PadState)remap.Invoke(window,new object[]{new PadState {LT=20},"overlay"});
            Check((high.Buttons&0x1000)!=0 && (middle.Buttons&0x1000)!=0 && (low.Buttons&0x1000)==0,"Trigger mappings use press/release hysteresis");
            ControlBindings.AssignButton(State(window),"overlay","rt","favourite");
            var alternate=(PadState)remap.Invoke(window,new object[]{new PadState {RT=200},"overlay"});
            var original=(PadState)remap.Invoke(window,new object[]{new PadState {Buttons=0x80},"overlay"});
            Check((alternate.Buttons&0x80)!=0 && (original.Buttons&0x80)!=0,"Both controller inputs perform their shared action at runtime");
            Storage.Write("duplicate-bindings.json",State(window));
            Check(ControlBindings.Buttons(Storage.Read<Settings>("duplicate-bindings.json"),"overlay.favourite").Length==2,"Multiple controller inputs persist across reloads");
            foreach(string style in new[]{"PlayStation","Xbox","Nintendo"}) foreach(string context in ControlBindings.Contexts) {
                var arguments=new object[]{new Settings {ButtonPrompts=style},context,null,null};
                var diagram=(FrameworkElement)typeof(Launcher).GetMethod("CreateControlDiagram",Flags).Invoke(window,arguments);
                diagram.Measure(new Size(760,Double.PositiveInfinity)); diagram.Arrange(new Rect(0,0,760,diagram.DesiredSize.Height)); diagram.UpdateLayout();
                var bitmap=new System.Windows.Media.Imaging.RenderTargetBitmap(760,(int)Math.Ceiling(diagram.ActualHeight),96,96,PixelFormats.Pbgra32); bitmap.Render(diagram);
                var png=new System.Windows.Media.Imaging.PngBitmapEncoder(); png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
                using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Controls-diagram-"+style+"-"+context+".png"))) png.Save(stream);
                Check(Children(diagram).OfType<TextBlock>().Where(label => label.Name.StartsWith("DiagramLabel_")).All(label => label.ActualHeight<=70),"Diagram labels fit their callout rows in "+context);
            }
            Console.WriteLine(checks+" control binding checks passed.");
        } finally {if(owner!=null) owner.Close(); if(window!=null) window.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
