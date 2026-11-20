using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PadLauncher {
    public partial class Launcher {
        Popup arcPopup;
        Window arcOnlyWindow;
        TextBlock arcOnlyNotice;
        readonly System.Windows.Media.Effects.BlurEffect arcOverlayBlur=new System.Windows.Media.Effects.BlurEffect {Radius=14,RenderingBias=System.Windows.Media.Effects.RenderingBias.Performance};
        bool arcTriggerWaitRelease,arcOnly;

        int arcSelectedIndex;
        int arcDirection,arcInput;
        long arcNextStep;
        ushort arcButtons;
        bool arcConfirmNeedsRelease;
        long arcButtonsNeutralSince=-1;
        bool arcButtonsArmed;
        List<ArcSlot> arcSlots;
        readonly List<Border> arcCards=new List<Border>();
        readonly Brush arcSelected=new SolidColorBrush(Color.FromRgb(163,240,205));
        readonly Brush arcSelectedText=new SolidColorBrush(Color.FromRgb(13,25,34));
        readonly Brush arcTextBackdrop=new SolidColorBrush(Color.FromArgb(218,21,36,46));
        const double ArcCenterY=270,ArcStep=63;

        [System.Diagnostics.Conditional("FOCUS_DIAGNOSTICS")]
        void TraceArc(string reason) {
#if FOCUS_DIAGNOSTICS
            TraceFocus("Arc: "+reason+"; overlayVisible="+IsVisible+"; tray="+arcOnly+
                "; standalone="+(arcOnlyWindow!=null)+"; popup="+(arcPopup!=null)+"; buttonsArmed="+arcButtonsArmed);
#endif
        }
        ushort ArcConfirmMask() {return ControlBindings.UsesButton(settings,"overlay.arc","south") ? (ushort)0x0010 : (ushort)0x1000;}
        bool HandleArcInput(PadState pad,long milliseconds) {

            if(arcPopup!=null || arcOnlyWindow!=null) {
                if(!arcOnly && !ArcBindingDown(pad)) {
                    var slot=arcSlots[arcSelectedIndex];
                    CloseArc(); arcTriggerWaitRelease=false; suppressSearchRelease=true;
                    if(settings.QuickSelectActivateOnRelease) ActivateArcSlot(slot);
                    return true;
                }
                ushort pressed=(ushort)(pad.Buttons & ~arcButtons);
                arcButtons=pad.Buttons;
                if(arcOnly && (pressed&0x2000)!=0 && !ControlBindings.UsesButton(settings,"overlay.arc","east")) {DismissArc(); return true;}
                int direction=(pad.Buttons&1)!=0 && !ControlBindings.UsesButton(settings,"overlay.arc","up") ? -1 :
                    (pad.Buttons&2)!=0 && !ControlBindings.UsesButton(settings,"overlay.arc","down") ? 1 : 0;
                int input=direction==0 ? 0 : 1;
                if(input==0 && Math.Abs((int)pad.LY)>15000) {direction=pad.LY>0 ? -1 : 1; input=2;}
                if(direction!=0 && (direction!=arcDirection || input!=arcInput || milliseconds>=arcNextStep)) {
                    bool first=direction!=arcDirection || input!=arcInput;
                    arcSelectedIndex=(arcSelectedIndex+direction+arcSlots.Count)%arcSlots.Count;
                    LayoutArc(direction);
                    arcNextStep=milliseconds+(input==2 ? 105 : first ? 310 : 225);
                }
                arcDirection=direction; arcInput=input;
                if((pressed&ArcConfirmMask())!=0) {
                    TraceArc("controller Confirm: "+Gamepad.Source);
                    var slot=arcSlots[arcSelectedIndex];
                    if(!arcOnly && settings.QuickSelectActivateOnRelease) {
                        CloseArc(); suppressSearchRelease=true;
                    }
                    suppressSearchRelease=true; ActivateArcSlot(slot);
                }
                return true;
            }
            if(arcTriggerWaitRelease) {if(!ArcBindingDown(pad)) arcTriggerWaitRelease=false; return true;}
            if(!ArcBindingDown(pad)) return false;
            arcTriggerWaitRelease=true;
            OpenArc(pad.Buttons);
            return true;
        }
        bool trayArcTriggerReady;
        bool PollNativeTrayArc() {
            if(!settings.OpenArcFromTrayWithTrigger || exiting || preview || NeedsInitialSetup ||
                (helpWindow!=null && helpWindow.IsVisible) || OwnedWindows.Cast<Window>().Any(window => window.IsVisible)) {
                trayArcTriggerReady=false;
                return false;
            }
            PadState pad;
            if(!Gamepad.Read(out pad)) {trayArcTriggerReady=false; return false;}
            bool down=ArcBindingDown(pad);
            if(!down) trayArcTriggerReady=true;
            if(!trayArcTriggerReady || !down) return false;
            trayArcTriggerReady=false;
            if(launching || arcOnly) return false;
            arcOnly=true;
            arcTriggerWaitRelease=true;
            controllerTimer.Interval=TimeSpan.FromMilliseconds(16);
            OpenArc(pad.Buttons);
            return true;
        }
        static long ArcMilliseconds() {return (long)(System.Diagnostics.Stopwatch.GetTimestamp()*1000.0/System.Diagnostics.Stopwatch.Frequency);}
        void PollTrayArc() {
            PadState pad;
            if(preview || !Gamepad.Read(out pad)) {
                DismissArc(); arcTriggerWaitRelease=false; suppressSearchRelease=true; return;
            }
            if(!ArcBindingDown(pad)) {
                DismissArc();
                arcTriggerWaitRelease=false;
                suppressSearchRelease=true;
                return;
            }
            long now=ArcMilliseconds();
            ushort confirmMask=ArcConfirmMask();
            int actionMask=confirmMask | (ControlBindings.UsesButton(settings,"overlay.arc","east") ? 0 : 0x2000);
            if(!arcButtonsArmed) {
                // The first report can be neutral, then a held button arrives
                // after a window or input source switch. Wait for stable release.
                if((pad.Buttons&actionMask)!=0) arcButtonsNeutralSince=-1;
                else if(arcButtonsNeutralSince<0) arcButtonsNeutralSince=now;
                else if(now-arcButtonsNeutralSince>=100) arcButtonsArmed=true;
                if(!arcButtonsArmed) pad.Buttons=(ushort)(pad.Buttons & ~actionMask);
            }
            var target=arcOnlyWindow ?? this;
            var handle=new System.Windows.Interop.WindowInteropHelper(target).Handle;
            bool confirmDown=(pad.Buttons&confirmMask)!=0;
            if(!confirmDown) arcConfirmNeedsRelease=false;
            bool foreground=handle!=IntPtr.Zero && GetForegroundWindow()==handle;
            if(!foreground && confirmDown && !arcConfirmNeedsRelease) {
                arcConfirmNeedsRelease=true;
                target.Activate(); SetForegroundWindow(handle);
            }
            // A press that requested focus must be released before it can confirm.
            if(!foreground || arcConfirmNeedsRelease) pad.Buttons=(ushort)(pad.Buttons & ~confirmMask);
            HandleArcInput(pad,now);
        }
        bool HandleTrayArcKey(System.Windows.Input.KeyEventArgs e) {
            if((arcPopup==null && arcOnlyWindow==null) || !arcOnly) return false;
            e.Handled=true;
            if(e.Key==System.Windows.Input.Key.Escape) {DismissArc(); return true;}
            if(e.Key==System.Windows.Input.Key.Enter && !e.IsRepeat) {
                ActivateArcSlot(arcSlots[arcSelectedIndex]); return true;
            }
            int direction=e.Key==System.Windows.Input.Key.Up ? -1 : e.Key==System.Windows.Input.Key.Down ? 1 : 0;
            if(direction!=0) {
                arcSelectedIndex=(arcSelectedIndex+direction+arcSlots.Count)%arcSlots.Count;
                LayoutArc(direction);
            }
            return true;
        }
        void DismissArc() {
            bool returnFocus=arcOnly && IsVisible;
            CloseArc();
            if(returnFocus) FocusVisibleOverlayFromShortcut();
        }
        void CloseArc([System.Runtime.CompilerServices.CallerMemberName] string caller=null) {
            if(arcPopup!=null || arcOnlyWindow!=null || arcOnly) TraceArc("close requested by "+caller);
            arcOnly=false;
            if(arcOnlyWindow!=null) {
                var window=arcOnlyWindow; arcOnlyWindow=null; window.Close();
                if(!IsVisible && !exiting) UpdateTrayHomeListener();
            }
            if(arcPopup!=null) {arcPopup.IsOpen=false; arcPopup=null;}
            if(scene.Effect==arcOverlayBlur) scene.Effect=null;
            arcCards.Clear(); arcOnlyNotice=null;
        }
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern IntPtr CreateRectRgn(int left,int top,int right,int bottom);
        [System.Runtime.InteropServices.DllImport("gdi32.dll")]
        static extern bool DeleteObject(IntPtr region);
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        static extern int SetWindowRgn(IntPtr window,IntPtr region,bool redraw);
        [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
        static extern int DwmFlush();

        EventHandler arcRevealFrame;
        System.Windows.Threading.DispatcherTimer arcRevealDeadline;
        IntPtr arcRevealClippedWindow;
        void CancelArcReveal() {
            if(arcRevealDeadline!=null) {
                arcRevealDeadline.Stop();
                arcRevealDeadline=null;
            }
            if(arcRevealFrame!=null) {
                CompositionTarget.Rendering-=arcRevealFrame;
                arcRevealFrame=null;
            }
            if(arcRevealClippedWindow!=IntPtr.Zero) {
                SetWindowRgn(arcRevealClippedWindow,IntPtr.Zero,true);
                arcRevealClippedWindow=IntPtr.Zero;
            }
            Opacity=1;
        }
        void RevealArcOnlyList() {
            TraceArc("reveal selected list");
            if(!arcOnly || IsVisible) return;
            var handle=new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
            var region=CreateRectRgn(0,0,0,0);
            if(region!=IntPtr.Zero && SetWindowRgn(handle,region,true)!=0) arcRevealClippedWindow=handle;
            else {
                if(region!=IntPtr.Zero) DeleteObject(region);
                Opacity=0;
            }
            int frames=0;
            arcRevealFrame=(sender,args) => {
                if(!IsVisible || exiting) {CancelArcReveal(); return;}
                if(++frames<2) return;
                DwmFlush();
                CancelArcReveal();
            };
            CompositionTarget.Rendering+=arcRevealFrame;
            try {
                ShowOverlay(true);
                TraceArc("show returned for selected list");
                UpdateLayout();
                var arcHandle=new System.Windows.Interop.WindowInteropHelper(arcOnlyWindow).Handle;
                SetWindowPos(arcHandle,new IntPtr(-1),0,0,0,0,0x0013); // Keep Arc above the overlay without moving or activating it.
                arcOnlyWindow.Activate();
                SetForegroundWindow(arcHandle);
                // A clipped window may stop producing rendering callbacks.
                arcRevealDeadline=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(100)};
                arcRevealDeadline.Tick+=(sender,args) => CancelArcReveal();
                arcRevealDeadline.Start();
                controllerWaitForForegroundRelease=true;
            } catch {
                CancelArcReveal();
                throw;
            }
        }
        static double ArcX(double offset) {return 135-5.9*offset*offset;}
        void LayoutArc(int direction) {
            int count=arcCards.Count;
            for(int i=0;i<count;i++) {
                int offset=(i-arcSelectedIndex+count)%count;
                if(offset>count/2) offset-=count;
                var card=arcCards[i];
                var text=(TextBlock)card.Child;
                card.Background=offset==0 ? arcSelected : arcTextBackdrop;
                text.Foreground=offset==0 ? arcSelectedText : white;
                text.FontWeight=offset==0 ? FontWeights.SemiBold : FontWeights.Normal;
                card.Opacity=Math.Abs(offset)>3 ? 0 : 1-Math.Abs(offset)*.19;
                Panel.SetZIndex(card,4-Math.Abs(offset));
                double x=ArcX(offset)+12,y=ArcCenterY+offset*ArcStep-22;
                bool isLeft=settings.QuickSelectSide=="left";
                if(isLeft) Canvas.SetLeft(card,x); else Canvas.SetRight(card,x);
                Canvas.SetTop(card,y);
                var transform=(TranslateTransform)card.RenderTransform;
                var duration=TimeSpan.FromMilliseconds(95);
                double previous=offset+direction;
                bool animate=direction!=0 && Math.Abs(previous)<=4 && Math.Abs(offset)<=3;
                transform.BeginAnimation(TranslateTransform.XProperty,animate ? new System.Windows.Media.Animation.DoubleAnimation((ArcX(offset)-ArcX(previous))*(isLeft ? -1 : 1),0,duration) : null);
                transform.BeginAnimation(TranslateTransform.YProperty,animate ? new System.Windows.Media.Animation.DoubleAnimation(direction*ArcStep,0,duration) : null);
            }
        }
        void OpenArc(ushort buttons) {
            bool useLists=settings.QuickSelectMode=="lists";
            var arcLists=useLists ? filters.Where(list => CollectionEnabled(list.Id) && !list.UnsupportedDynamic &&
                list.Id!="unavailable-collections" && !list.Id.StartsWith("arcPopup-tag:",StringComparison.Ordinal)).ToList() : null;
            arcSlots=useLists ? arcLists.Select(list => new ArcSlot {Kind="list",Target=list.Id}).ToList()
                : (settings.RadialSlots ?? DefaultArcSlots()).Take(8).ToList();
            if(arcSlots.Count==0) arcSlots.Add(new ArcSlot());
            arcButtonsArmed=false; arcButtonsNeutralSince=-1;
            arcButtons=buttons; arcConfirmNeedsRelease=(buttons&ArcConfirmMask())!=0; arcSelectedIndex=0; arcDirection=arcInput=0; navigation.Reset();
            var canvas=new Canvas {Width=520,Height=540,ClipToBounds=true,IsHitTestVisible=arcOnly};
            bool isLeft=settings.QuickSelectSide=="left";
            var rail=new System.Windows.Shapes.Path {Data=Geometry.Parse(isLeft ? "M 0,18 Q 270,270 0,522" : "M 520,18 Q 250,270 520,522"),
                Stroke=mint,StrokeThickness=2,Opacity=.65,IsHitTestVisible=false};
            canvas.Children.Add(rail);
            for(int i=0;i<arcSlots.Count;i++) {
                var text=Label(useLists && arcLists.Count==0 ? "No available lists" : useLists ? arcLists[i].Name : ArcSlotLabel(arcSlots[i]),23,white);
                text.MaxWidth=324; text.TextAlignment=isLeft ? TextAlignment.Left : TextAlignment.Right; text.TextTrimming=TextTrimming.CharacterEllipsis; text.VerticalAlignment=VerticalAlignment.Center;
                var card=new Border {Height=44,CornerRadius=new CornerRadius(8),Padding=new Thickness(12,0,12,0),
                    RenderTransform=new TranslateTransform(),Child=text};
                if(arcOnly) {
                    int index=i;
                    card.PreviewMouseLeftButtonUp+=(sender,args) => {
                        if(card.Opacity<=0) return;
                        args.Handled=true;
                        arcSelectedIndex=index;
                        LayoutArc(0);
                        ActivateArcSlot(arcSlots[index]);
                    };
                }
                canvas.Children.Add(card); arcCards.Add(card);
            }
            if(arcOnly) {
                arcOnlyNotice=Label("",15,mint);
                arcOnlyNotice.Width=420; arcOnlyNotice.TextAlignment=TextAlignment.Center; arcOnlyNotice.TextWrapping=TextWrapping.Wrap;
                Canvas.SetLeft(arcOnlyNotice,50); Canvas.SetTop(arcOnlyNotice,492); canvas.Children.Add(arcOnlyNotice);
            }
            LayoutArc(0);
            // A separate window is only needed when there is no visible overlay.
            // Keeping the existing overlay active avoids a second composition
            // pass as the standalone window takes foreground focus.
            if(arcOnly && !IsVisible) {ShowArcOnlyWindow(canvas,isLeft); return;}
            scene.Effect=arcOverlayBlur;
            double scale=Math.Min(1.5,Math.Min(ActualWidth/canvas.Width,ActualHeight/canvas.Height));
            if(scale<=0) scale=1;
            var scaledArc=new Viewbox {Width=canvas.Width*scale,Height=canvas.Height*scale,Child=canvas};
            arcPopup=new Popup {AllowsTransparency=true,Placement=PlacementMode.Relative,PlacementTarget=this,
                HorizontalOffset=isLeft ? 0 : Math.Max(0,ActualWidth-scaledArc.Width),VerticalOffset=Math.Max(0,(ActualHeight-scaledArc.Height)/2),
                Child=scaledArc,StaysOpen=true,IsHitTestVisible=false};
            arcPopup.IsOpen=true;
        }
        void ShowArcOnlyWindow(Canvas canvas,bool isLeft) {
            var monitor=ResolveMonitor(settings.OpenMonitor,System.Windows.Forms.Screen.AllScreens,System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position));
            var area=monitor.WorkingArea;
            var view=new Viewbox {Child=canvas};
            var window=new Window {Title="Pad Launcher Arc",Content=view,Width=520,Height=540,ShowInTaskbar=false,
                WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,
                Background=Brushes.Transparent,Topmost=true,WindowStartupLocation=WindowStartupLocation.Manual};
            window.PreviewKeyDown+=(sender,args) => HandleTrayArcKey(args);
            arcOnlyWindow=window;
            window.Closing+=(sender,args) => TraceArc("standalone window Closing");
            window.Closed+=(sender,args) => TraceArc("standalone window Closed");
            var handle=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
            SetWindowPos(handle,new IntPtr(-1),area.Left,area.Top,1,1,0x0010);
            var source=System.Windows.Interop.HwndSource.FromHwnd(handle);
            double dpi=source!=null && source.CompositionTarget!=null ? source.CompositionTarget.TransformToDevice.M11 : 1;
            double scale=Math.Min(1.5*dpi,Math.Min(area.Width/canvas.Width,area.Height/canvas.Height));
            int width=(int)(canvas.Width*scale),height=(int)(canvas.Height*scale);
            int left=isLeft ? area.Left : area.Right-width, top=area.Top+(area.Height-height)/2;
            SetWindowPos(handle,new IntPtr(-1),left,top,width,height,0x0010);
            window.Show();
            SetWindowPos(handle,new IntPtr(-1),left,top,width,height,0x0010);
            window.Activate(); SetForegroundWindow(handle);
            TraceArc("standalone window shown");
        }
        List<ArcSlot> arcStarterSlots;
        List<ArcSlot> DefaultArcSlots() {
            if(arcStarterSlots!=null) return arcStarterSlots;
            var random=new Random();
            Func<IEnumerable<string>,string> pick=values => {
                var choices=values.Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return choices.Count==0 ? null : choices[random.Next(choices.Count)];
            };
            var hidden=new HashSet<string>(settings.HiddenGameIds ?? new List<string>());
            var lists=CollectionOptions().Where(list => !list.UnsupportedDynamic && CollectionEnabled(list.Id)).ToList();
            var tags=(settings.ListTags ?? new Dictionary<string,string>());
            var slots=new List<ArcSlot> {
                new ArcSlot {Kind="game",Target=demo ? null : pick(library.Games.Where(game => !hidden.Contains(game.Id)).Select(game => game.Id))},
                new ArcSlot {Kind="list",Target=pick(lists.Select(list => list.Id))},
                new ArcSlot {Kind="tag",Target=pick(lists.Where(list => tags.ContainsKey(list.Id)).SelectMany(list => (tags[list.Id] ?? "").Split(',')).Select(tag => tag.Trim()))}
            };
            while(slots.Count<8) slots.Add(new ArcSlot());
            return arcStarterSlots=slots;
        }
        FrameworkElement CreateArcSideSettings(Window owner) {
            var panel=new StackPanel();
            var sideLabel=Label("Screen side",14,mint); sideLabel.Margin=new Thickness(0,12,0,6); panel.Children.Add(sideLabel);
            var side=new SettingsComboBox {Name="QuickSelectSide",ItemsSource=new[]{"Left","Right"},
                SelectedIndex=settings.QuickSelectSide=="left" ? 0 : 1,Padding=new Thickness(8)};
            bool restoringSide=false;
            side.SelectionChanged+=(sender,args) => {
                if(restoringSide) return;
                string previous=settings.QuickSelectSide;
                try {settings.QuickSelectSide=side.SelectedIndex==0 ? "left" : "right"; Storage.Write("settings.json",settings);}
                catch(Exception error) {
                    settings.QuickSelectSide=previous;
                    restoringSide=true; side.SelectedIndex=previous=="left" ? 0 : 1; restoringSide=false;
                    ThemedMessage.Show(owner,error.Message,"Arc menu");
                }
            };
            panel.Children.Add(side);
            var activateOnRelease=new CheckBox {Name="QuickSelectActivateOnRelease",Content="Activate on release",
                IsChecked=settings.QuickSelectActivateOnRelease,Margin=new Thickness(0,12,0,0)};
            activateOnRelease.Click+=(sender,args) => {
                bool previous=settings.QuickSelectActivateOnRelease;
                try {
                    settings.QuickSelectActivateOnRelease=activateOnRelease.IsChecked==true;
                    Storage.Write("settings.json",settings);
                } catch(Exception error) {
                    settings.QuickSelectActivateOnRelease=previous;
                    activateOnRelease.IsChecked=previous;
                    ThemedMessage.Show(owner,error.Message,"Arc menu");
                }
            };
            panel.Children.Add(activateOnRelease);
            var trayArc=new CheckBox {Name="OpenArcFromTrayWithTrigger",Content="Open Arc from the tray with the assigned button",
                IsChecked=settings.OpenArcFromTrayWithTrigger,Margin=new Thickness(0,10,0,0)};
            trayArc.Click+=(sender,args) => {
                bool previous=settings.OpenArcFromTrayWithTrigger;
                try {
                    settings.OpenArcFromTrayWithTrigger=trayArc.IsChecked==true;
                    Storage.Write("settings.json",settings);
                    UpdateTrayHomeListener();
                } catch(Exception error) {
                    settings.OpenArcFromTrayWithTrigger=previous; trayArc.IsChecked=previous;
                    ThemedMessage.Show(owner,error.Message,"Arc menu");
                }
            };
            panel.Children.Add(trayArc);
            return panel;
        }
        FrameworkElement CreateArcMenuSettings(Window owner) {
            var section=new Expander {Header="Menu setting",Foreground=white,Margin=new Thickness(0,16,0,12)};
            var panel=new StackPanel {Margin=new Thickness(0,8,0,0)};
            section.Content=panel;
            string button=ControlBindings.Label(settings,"overlay.arc");
            string openHint=button=="Unassigned" ? "Assign an Arc menu button in Show/Change controls. " : "Hold "+button+" to open the Arc menu. ";
            var note=Label(openHint+"Use D-pad Up/Down to step or the left stick to scroll faster. The menu starts at slot 1.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            var mode=new SettingsComboBox {Name="QuickSelectMode",ItemsSource=new[]{"Use lists for the arc menu","Custom"},
                SelectedIndex=settings.QuickSelectMode=="lists" ? 0 : 1,Padding=new Thickness(8),Margin=new Thickness(0,8,0,0)};
            panel.Children.Add(mode);
            var customPanel=new StackPanel();
            panel.Children.Add(customPanel);
            bool initialized=false, restoringMode=false;
            Action refreshMode=() => {
                bool custom=settings.QuickSelectMode!="lists";
                customPanel.Visibility=custom ? Visibility.Visible : Visibility.Collapsed;
                note.Text=openHint+"Use D-pad Up/Down to step or the left stick to scroll faster."+
                    (custom ? " The menu starts at slot 1." : " The menu follows the order of your overlay lists.");
                if(!custom || !section.IsExpanded || initialized) return;
                initialized=true;
                var options=new Dictionary<string,List<KeyValuePair<string,string>>> {
                    {"game",library.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).Select(g => new KeyValuePair<string,string>(g.Id,g.Name)).ToList()},
                    {"list",CollectionOptions().Select(c => new KeyValuePair<string,string>(c.Id,c.Name)).ToList()},
                    {"tag",(settings.ListTags ?? new Dictionary<string,string>()).Values.SelectMany(value => (value ?? "").Split(',')).Select(t => t.Trim()).Where(t => t.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t,StringComparer.CurrentCultureIgnoreCase).Select(t => new KeyValuePair<string,string>(t,t)).ToList()}
                };
                var slots=settings.RadialSlots ?? DefaultArcSlots();
                var kinds=new List<ComboBox>(); var targets=new List<ComboBox>();
                for(int i=0;i<8;i++) {
                    var slot=i<slots.Count ? slots[i] ?? new ArcSlot() : new ArcSlot();
                    var row=new Grid {Margin=new Thickness(0,6,0,0)};
                    row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});
                    row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(120)});
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    var number=Label((i+1).ToString(),12,mint); number.VerticalAlignment=VerticalAlignment.Center; row.Children.Add(number);
                    var kind=new SettingsComboBox {ItemsSource=new[]{"Unassigned","Game","List","Tag"},Padding=new Thickness(8)};
                    Grid.SetColumn(kind,1); row.Children.Add(kind); kinds.Add(kind);
                    var target=new SettingsComboBox {DisplayMemberPath="Value",SelectedValuePath="Key",Padding=new Thickness(8),Margin=new Thickness(8,0,0,0)};
                    Grid.SetColumn(target,2); row.Children.Add(target); targets.Add(target);
                    kind.SelectionChanged+=(s,e) => {
                        string key=(kind.SelectedItem as string ?? "").ToLowerInvariant();
                        List<KeyValuePair<string,string>> choices;
                        target.ItemsSource=options.TryGetValue(key,out choices) ? choices : null;
                        target.IsEnabled=choices!=null;
                        target.SelectedIndex=choices!=null && choices.Count>0 ? 0 : -1;
                    };
                    kind.SelectedItem=String.IsNullOrEmpty(slot.Target) ? "Unassigned" : slot.Kind=="game" ? "Game" : slot.Kind=="list" ? "List" : slot.Kind=="tag" ? "Tag" : "Unassigned";
                    if(!String.IsNullOrEmpty(slot.Target)) target.SelectedValue=slot.Target;
                    customPanel.Children.Add(row);
                }
                var save=new Button {Content="Save Arc menu",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};
                customPanel.Children.Add(save);
                var status=Label("",12,muted); customPanel.Children.Add(status);
                save.Click+=(s,e) => {
                    var next=new List<ArcSlot>();
                    for(int i=0;i<kinds.Count;i++) {
                        string target=targets[i].SelectedValue as string;
                        if(kinds[i].SelectedIndex>0 && target==null) {
                            status.Text="Choose a target or set slot "+(i+1)+" to Unassigned."; return;
                        }
                        next.Add(target==null ? new ArcSlot() : new ArcSlot {Kind=((string)kinds[i].SelectedItem).ToLowerInvariant(),Target=target});
                    }
                    var previous=settings.RadialSlots;
                    try {settings.RadialSlots=next; Storage.Write("settings.json",settings); status.Text="Arc menu saved.";}
                    catch(Exception error) {settings.RadialSlots=previous; ThemedMessage.Show(owner,error.Message,"Arc menu");}
                };
            };
            section.Expanded+=(sender,args) => {if(args.Source==section) refreshMode();};
            mode.SelectionChanged+=(sender,args) => {
                if(restoringMode) return;
                string previous=settings.QuickSelectMode;
                try {
                    settings.QuickSelectMode=mode.SelectedIndex==0 ? "lists" : "custom";
                    Storage.Write("settings.json",settings);
                    refreshMode();
                } catch(Exception error) {
                    settings.QuickSelectMode=previous;
                    restoringMode=true; mode.SelectedIndex=previous=="lists" ? 0 : 1; restoringMode=false;
                    ThemedMessage.Show(owner,error.Message,"Arc menu");
                }
            };
            refreshMode();
            return section;
        }
        string ArcSlotLabel(ArcSlot slot) {
            if(slot==null) return "Unassigned";
            if(String.IsNullOrWhiteSpace(slot.Target)) return "Unassigned";
            if(slot.Kind=="tag") return slot.Target ?? "Unassigned";
            if(slot.Kind=="game") {
                var game=library.Games.FirstOrDefault(g => g.Id==slot.Target);
                return game==null ? "Unavailable game" : game.Name;
            }
            if(slot.Kind=="list") {
                var list=CollectionOptions().FirstOrDefault(c => c.Id==slot.Target);
                return list==null ? "Unavailable list" : list.Name;
            }
            return "Unassigned";
        }
        void ActivateArcSlot(ArcSlot slot) {
            TraceArc("activate slot kind="+(slot==null ? "(empty)" : slot.Kind)+"; launching="+launching);
            if(launching) return;
            if(slot==null || String.IsNullOrWhiteSpace(slot.Target)) {ShowNotice("Assign an item to this slot",1.5); return;}
            if(slot.Kind=="game") {LaunchGame(library.Games.FirstOrDefault(g => g.Id==slot.Target)); return;}
            if(slot.Kind=="list") {
                if(!CollectionEnabled(slot.Target)) {TraceArc("selected list is hidden"); ShowNotice("This list is hidden from the overlay.",1.5); return;}
                int index=filters.FindIndex(c => c.Id==slot.Target);
                if(index<0) {
                    var list=CollectionOptions().FirstOrDefault(c => c.Id==slot.Target);
                    if(list==null) return;
                    filters.Add(list); index=filters.Count-1;
                }
                if(index!=collectionIndex) ChangeCollection(index-collectionIndex,animate:!arcOnly);
                RevealArcOnlyList();
            }
            if(slot.Kind=="tag" && settings.ListTags!=null) {
                var options=CollectionOptions();
                var matched=options.Where(c => {
                    string tags;
                    return settings.ListTags.TryGetValue(c.Id,out tags) && (tags ?? "").Split(',')
                        .Any(t => String.Equals(t.Trim(),slot.Target,StringComparison.OrdinalIgnoreCase));
                }).ToList();
                if(matched.Count==0) {ShowNotice("No lists have this tag."); return;}
                var collection=new Collection {Id="arcPopup-tag:"+slot.Target,Name="Tag: "+slot.Target,
                    Games=matched.SelectMany(c => c.Id=="all" ? library.Games.Select(g => g.Id) : (c.Games ?? new List<string>())).Distinct().ToList()};
                int index=filters.FindIndex(c => c.Id==collection.Id);
                if(index<0) {filters.Add(collection); index=filters.Count-1;} else filters[index]=collection;
                if(index==collectionIndex) FilterGames(false);
                else ChangeCollection(index-collectionIndex,animate:!arcOnly);
                RevealArcOnlyList();
            }
        }
    }
}
