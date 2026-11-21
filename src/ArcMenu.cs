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
        System.Windows.Threading.DispatcherTimer arcBlurTimer;
        bool arcTriggerWaitRelease,arcOnly;

        int arcSelectedIndex;
        int arcDirection,arcInput;
        long arcNextStep;
        ushort arcButtons;
        bool arcConfirmNeedsRelease;
        long arcButtonsNeutralSince=-1;
        bool arcButtonsArmed;
        bool UseDesktopArcSettings { get { return arcOnly && !settings.ShareArcMenuSettings; } }
        List<ArcSlot> arcSlots;
        ArcSlot arcActivatedSlot;
        int arcPendingReleaseInput;
        long arcDpadReleasedAt=-1;
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
                    bool activate=settings.OverlayArcActivateOnButtonRelease && !settings.QuickSelectActivateOnRelease && slot!=arcActivatedSlot;
                    CloseArc(); arcTriggerWaitRelease=false; suppressSearchRelease=true;
                    if(activate) ActivateArcSlot(slot);
                    return true;
                }
                ushort pressed=(ushort)(pad.Buttons & ~arcButtons);
                arcButtons=pad.Buttons;
                if((pressed&0x2000)!=0 && !ControlBindings.UsesButton(settings,"overlay.arc","east")) {
                    DismissArc();
                    suppressSearchRelease=true;
                    return true;
                }
                int direction=(pad.Buttons&1)!=0 && !ControlBindings.UsesButton(settings,"overlay.arc","up") ? -1 :
                    (pad.Buttons&2)!=0 && !ControlBindings.UsesButton(settings,"overlay.arc","down") ? 1 : 0;
                bool dpadHeld=direction!=0;
                if(!dpadHeld && arcInput==1 && arcBlurTimer!=null && arcBlurTimer.IsEnabled) {
                    arcBlurTimer.Stop();
                    arcBlurTimer.Interval=TimeSpan.FromMilliseconds(175);
                    arcBlurTimer.Start();
                }
                if(dpadHeld || arcPendingReleaseInput!=1) arcDpadReleasedAt=-1;
                else if(arcDpadReleasedAt<0) arcDpadReleasedAt=milliseconds;
                int input=direction==0 ? 0 : 1;
                if(input==0 && Math.Abs((int)pad.LY)>15000) {direction=pad.LY>0 ? -1 : 1; input=2;}
                if(direction!=0 && (direction!=arcDirection || input!=arcInput || milliseconds>=arcNextStep)) {
                    bool first=direction!=arcDirection || input!=arcInput;
                    arcSelectedIndex=(arcSelectedIndex+direction+arcSlots.Count)%arcSlots.Count;
                    arcPendingReleaseInput=input;
                    LayoutArc(direction);
                    arcNextStep=milliseconds+(input==2 ? 105 : first ? 310 : 225);
                }
                arcDirection=direction; arcInput=input;
                if((pressed&ArcConfirmMask())!=0) {
                    TraceArc("controller Confirm: "+Gamepad.Source);
                    var slot=arcSlots[arcSelectedIndex];
                    bool showsList=slot!=null && (slot.Kind=="list" || slot.Kind=="tag");
                    arcPendingReleaseInput=0;
                    if(!arcOnly && !showsList && settings.QuickSelectActivateOnRelease) {
                        CloseArc(); suppressSearchRelease=true;
                    }
                    suppressSearchRelease=true; ActivateArcSlot(slot);
                } else if((arcPendingReleaseInput==1 && arcDpadReleasedAt>=0 && milliseconds-arcDpadReleasedAt>=150) ||
                    (arcPendingReleaseInput==2 && Math.Abs((int)pad.LY)<11000)) {
                    arcPendingReleaseInput=0;
                    if(UseDesktopArcSettings ? settings.DesktopArcActivateOnNavigationRelease : settings.QuickSelectActivateOnRelease)
                        ActivateArcSlot(arcSlots[arcSelectedIndex]);
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
                OwnedWindows.Cast<Window>().Any(window => window.IsVisible)) {
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
                try {
                    var slot=arcSlots[arcSelectedIndex];
                    bool activate=UseDesktopArcSettings
                        ? settings.ArcActivateOnButtonRelease && !settings.DesktopArcActivateOnNavigationRelease
                        : settings.OverlayArcActivateOnButtonRelease && !settings.QuickSelectActivateOnRelease;
                    if(activate && slot!=arcActivatedSlot)
                        ActivateArcSlot(slot);
                } finally {
                    DismissArc();
                    arcTriggerWaitRelease=false;
                    suppressSearchRelease=true;
                }
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
            bool canConfirm=handle!=IntPtr.Zero &&
                (GetForegroundWindow()==handle || (IsWindowEnabled(handle) && SettingsHasForeground()));
            if(!canConfirm && confirmDown && !arcConfirmNeedsRelease) {
                arcConfirmNeedsRelease=true;
                target.Activate(); SetForegroundWindow(handle);
            }
            // A press that requested focus must be released before it can confirm.
            if(!canConfirm || arcConfirmNeedsRelease) pad.Buttons=(ushort)(pad.Buttons & ~confirmMask);
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
            arcActivatedSlot=null;
            arcPendingReleaseInput=0;
            arcDpadReleasedAt=-1;
            if(arcOnlyWindow!=null) {
                var window=arcOnlyWindow; arcOnlyWindow=null; window.Close();
                if(!IsVisible && !exiting) UpdateTrayHomeListener();
            }
            if(arcPopup!=null) {arcPopup.IsOpen=false; arcPopup=null;}
            if(arcBlurTimer!=null) arcBlurTimer.Stop();
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
            if(direction!=0 && IsVisible) {
                if(arcBlurTimer==null) {
                    arcBlurTimer=new System.Windows.Threading.DispatcherTimer {Interval=TimeSpan.FromMilliseconds(150)};
                    arcBlurTimer.Tick+=(sender,args) => {
                        if(arcInput==1 && arcDirection!=0) return;
                        if(arcPopup!=null && !arcOnly && !settings.QuickSelectActivateOnRelease) {
                            var slot=arcSlots[arcSelectedIndex];
                            string highlightedList=slot==null ? null : slot.Kind=="list" ? slot.Target :
                                slot.Kind=="tag" && !String.IsNullOrWhiteSpace(slot.Target) ? "arcPopup-tag:"+slot.Target : null;
                            if(highlightedList==null || highlightedList!=settings.CollectionId) return;
                        }
                        arcBlurTimer.Stop();
                        if(scene.Effect==arcOverlayBlur) scene.Effect=null;
                    };
                }
                scene.Effect=arcOverlayBlur;
                arcBlurTimer.Stop();
                arcBlurTimer.Interval=TimeSpan.FromMilliseconds(150);
                arcBlurTimer.Start();
            }
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
                bool isLeft=(UseDesktopArcSettings ? settings.DesktopArcSide : settings.QuickSelectSide)=="left";
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
            bool useLists=(UseDesktopArcSettings ? settings.DesktopArcMode : settings.QuickSelectMode)=="lists";
            var excluded=GetArcExcludedListIds(UseDesktopArcSettings);
            List<Collection> arcLists=null;
            if(useLists) {
                var activeLists=new HashSet<string>(filters.Select(list => list.Id));
                arcLists=CollectionOptions().Where(list => CanInheritArcList(list) &&
                    (activeLists.Contains(list.Id) || IsAlwaysAvailableArcList(list.Id)) &&
                    !excluded.Contains(list.Id)).ToList();
            }
            arcSlots=useLists ? arcLists.Select(list => new ArcSlot {Kind="list",Target=list.Id}).ToList()
                : ((UseDesktopArcSettings ? settings.DesktopArcSlots : settings.RadialSlots) ?? DefaultArcSlots()).ToList();
            if(arcSlots.Count==0) arcSlots.Add(new ArcSlot());
            arcSelectedIndex=Math.Max(0,arcSlots.FindIndex(slot => slot!=null && slot.Kind=="list" && slot.Target==settings.CollectionId));
            arcButtonsArmed=false; arcButtonsNeutralSince=-1;
            arcButtons=buttons; arcConfirmNeedsRelease=(buttons&ArcConfirmMask())!=0; arcDirection=arcInput=0; navigation.Reset();
            var canvas=new Canvas {Width=520,Height=540,ClipToBounds=true,IsHitTestVisible=arcOnly};
            bool isLeft=(UseDesktopArcSettings ? settings.DesktopArcSide : settings.QuickSelectSide)=="left";
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
            return arcStarterSlots=slots;
        }
        FrameworkElement CreateArcSideSetting(Window owner,bool desktop) {
            var panel=new StackPanel();
            var sideLabel=Label("Screen side",14,mint); sideLabel.Margin=new Thickness(0,12,0,6); panel.Children.Add(sideLabel);
            string current=desktop ? settings.DesktopArcSide : settings.QuickSelectSide;
            var side=new SettingsComboBox {Name=desktop ? "DesktopArcSide" : "QuickSelectSide",ItemsSource=new[]{"Left","Right"},
                SelectedIndex=current=="left" ? 0 : 1,Padding=new Thickness(8)};
            bool restoringSide=false;
            side.SelectionChanged+=(sender,args) => {
                if(restoringSide) return;
                string previous=desktop ? settings.DesktopArcSide : settings.QuickSelectSide;
                try {
                    string next=side.SelectedIndex==0 ? "left" : "right";
                    if(desktop) settings.DesktopArcSide=next; else settings.QuickSelectSide=next;
                    Storage.Write("settings.json",settings);
                }
                catch(Exception error) {
                    if(desktop) settings.DesktopArcSide=previous; else settings.QuickSelectSide=previous;
                    restoringSide=true; side.SelectedIndex=previous=="left" ? 0 : 1; restoringSide=false;
                    ThemedMessage.Show(owner,error.Message,"Arc menu");
                }
            };
            panel.Children.Add(side);
            return panel;
        }
        FrameworkElement CreateArcSettings(Window owner) {
            var panel=new StackPanel();
            var shareSettings=new CheckBox {Name="ShareArcMenuSettings",Content="Use the settings for both arc menus",
                IsChecked=settings.ShareArcMenuSettings,Margin=new Thickness(0,12,0,0)};
            panel.Children.Add(shareSettings);
            string arcButton=ControlBindings.Label(settings,"overlay.arc");
            string releaseLabel=arcButton=="Unassigned" ? "Activate on release (Arc shortcut unassigned)" : "Activate on release of "+arcButton;
            var trayArc=new CheckBox {Name="OpenArcFromTrayWithTrigger",
                Content=arcButton=="Unassigned" ? "Open Arc from the tray (Arc shortcut unassigned)" : "Open Arc from the tray with "+arcButton,
                IsChecked=settings.OpenArcFromTrayWithTrigger};
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
            var overlayHeading=Label("Main overlay Arc menu",16,white);
            overlayHeading.FontWeight=FontWeights.SemiBold;
            overlayHeading.Margin=new Thickness(0,16,0,0);
            panel.Children.Add(overlayHeading);
            panel.Children.Add(CreateArcMenuSettings(owner,false));
            panel.Children.Add(CreateArcSideSetting(owner,false));
            AddArcReleaseSettings(panel,owner,releaseLabel,false);
            var arcOnlyHeading=Label("Desktop Arc menu",16,white);
            arcOnlyHeading.FontWeight=FontWeights.SemiBold;
            arcOnlyHeading.Margin=new Thickness(0,28,0,0);
            panel.Children.Add(arcOnlyHeading);
            var desktopMenu=CreateArcMenuSettings(owner,true);
            var desktopSide=CreateArcSideSetting(owner,true);
            panel.Children.Add(desktopMenu);
            panel.Children.Add(desktopSide);
            trayArc.Margin=new Thickness(0,10,0,0);
            panel.Children.Add(trayArc);
            var desktopRelease=new StackPanel();
            var refreshDesktopRelease=AddArcReleaseSettings(desktopRelease,owner,releaseLabel,true);
            panel.Children.Add(desktopRelease);
            Action updateDesktopControls=() => {
                var visibility=settings.ShareArcMenuSettings ? Visibility.Collapsed : Visibility.Visible;
                desktopMenu.Visibility=desktopSide.Visibility=visibility;
                desktopRelease.IsEnabled=!settings.ShareArcMenuSettings;
                refreshDesktopRelease();
            };
            panel.AddHandler(Button.ClickEvent,new RoutedEventHandler((sender,args) => refreshDesktopRelease()));
            updateDesktopControls();
            shareSettings.Click+=(sender,args) => {
                bool previous=settings.ShareArcMenuSettings;
                try {
                    settings.ShareArcMenuSettings=shareSettings.IsChecked==true;
                    Storage.Write("settings.json",settings);
                    updateDesktopControls();
                } catch(Exception error) {
                    settings.ShareArcMenuSettings=previous;
                    shareSettings.IsChecked=previous;
                    ThemedMessage.Show(owner,error.Message,"Arc menu");
                }
            };
            return panel;
        }
        Action AddArcReleaseSettings(StackPanel panel,Window owner,string releaseLabel,bool desktop) {
            Func<bool> readNavigation=() => desktop && !settings.ShareArcMenuSettings ? settings.DesktopArcActivateOnNavigationRelease : settings.QuickSelectActivateOnRelease;
            Func<bool> readButton=() => desktop && !settings.ShareArcMenuSettings ? settings.ArcActivateOnButtonRelease : settings.OverlayArcActivateOnButtonRelease;
            Action<bool,bool> setRelease=(navigation,button) => {
                if(desktop) {
                    settings.DesktopArcActivateOnNavigationRelease=navigation;
                    settings.ArcActivateOnButtonRelease=button;
                } else {
                    settings.QuickSelectActivateOnRelease=navigation;
                    settings.OverlayArcActivateOnButtonRelease=button;
                }
            };
            var buttonRelease=new CheckBox {Name=desktop ? "ArcActivateOnButtonRelease" : "OverlayArcActivateOnButtonRelease",
                Content=releaseLabel,IsChecked=readButton() && !readNavigation(),Margin=new Thickness(0,10,0,0)};
            var navigationRelease=new CheckBox {Name=desktop ? "DesktopArcActivateOnNavigationRelease" : "QuickSelectActivateOnRelease",
                Content="Activate on release of left-stick or D-pad",IsChecked=readNavigation(),Margin=new Thickness(0,10,0,0)};
            RoutedEventHandler saveRelease=(sender,args) => {
                bool previousNavigation=readNavigation();
                bool previousButton=readButton();
                try {
                    if(sender==navigationRelease && navigationRelease.IsChecked==true) buttonRelease.IsChecked=false;
                    if(sender==buttonRelease && buttonRelease.IsChecked==true) navigationRelease.IsChecked=false;
                    setRelease(navigationRelease.IsChecked==true,buttonRelease.IsChecked==true);
                    Storage.Write("settings.json",settings);
                } catch(Exception error) {
                    setRelease(previousNavigation,previousButton);
                    navigationRelease.IsChecked=previousNavigation;
                    buttonRelease.IsChecked=previousButton && !previousNavigation;
                    ThemedMessage.Show(owner,error.Message,"Arc menu");
                }
            };
            buttonRelease.Click+=saveRelease;
            navigationRelease.Click+=saveRelease;
            panel.Children.Add(buttonRelease);
            panel.Children.Add(navigationRelease);
            return () => {
                bool navigation=readNavigation();
                navigationRelease.IsChecked=navigation;
                buttonRelease.IsChecked=readButton() && !navigation;
            };
        }
        static bool IsAlwaysAvailableArcList(string id) {
            return id=="recent" || id=="recently-added" || id=="favourites";
        }
        static readonly string[] DefaultArcExcludedListIds={"recent","recently-added","favourites"};
        IList<string> GetArcExcludedListIds(bool desktop) {
            var saved=desktop ? settings.DesktopArcExcludedListIds : settings.OverlayArcExcludedListIds;
            return saved==null ? (IList<string>)DefaultArcExcludedListIds : saved;
        }
        bool CanInheritArcList(Collection list) {
            return (CollectionEnabled(list.Id) || IsAlwaysAvailableArcList(list.Id)) && !list.UnsupportedDynamic &&
                list.Id!="unavailable-collections" && !list.Id.StartsWith("arcPopup-tag:",StringComparison.Ordinal);
        }
        FrameworkElement CreateArcMenuSettings(Window owner,bool desktop) {
            var section=new Expander {Header="Menu setting",Foreground=white,Margin=new Thickness(0,10,0,0)};
            var panel=new StackPanel {Margin=new Thickness(0,8,0,0)};
            section.Content=panel;
            string button=ControlBindings.Label(settings,"overlay.arc");
            string openHint=button=="Unassigned" ? "Assign an Arc menu button in Show/Change controls. " : "Hold "+button+" to open the Arc menu. ";
            var note=Label("",12,muted);
            note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            Func<string> currentMode=() => desktop ? settings.DesktopArcMode : settings.QuickSelectMode;
            var mode=new SettingsComboBox {Name=desktop ? "DesktopArcMode" : "QuickSelectMode",ItemsSource=new[]{"Use overlay lists for the arc menu","Custom"},
                SelectedIndex=currentMode()=="lists" ? 0 : 1,Padding=new Thickness(8),Margin=new Thickness(0,8,0,0)};
            panel.Children.Add(mode);
            var inheritedPanel=new StackPanel {Margin=new Thickness(0,8,0,0)};
            panel.Children.Add(inheritedPanel);
            var customPanel=new StackPanel();
            panel.Children.Add(customPanel);
            bool initialized=false, restoringMode=false;
            Action refreshMode=() => {
                bool custom=currentMode()!="lists";
                customPanel.Visibility=custom ? Visibility.Visible : Visibility.Collapsed;
                inheritedPanel.Visibility=custom ? Visibility.Collapsed : Visibility.Visible;
                note.Text=openHint+"Use D-pad Up/Down to step or the left stick to scroll faster."+
                    " Arc starts on your last accessed list when available.";
                if(!custom && section.IsExpanded) {
                    inheritedPanel.Children.Clear();
                    inheritedPanel.Children.Add(Label("Choose which overlay lists appear in Arc",14,mint));
                    foreach(var list in CollectionOptions().Where(CanInheritArcList)) {
                        string id=list.Id;
                        var include=new CheckBox {Content=list.Name,IsChecked=!GetArcExcludedListIds(desktop).Contains(id),
                            Margin=new Thickness(0,6,0,0)};
                        include.Click+=(sender,args) => {
                            var previous=desktop ? settings.DesktopArcExcludedListIds : settings.OverlayArcExcludedListIds;
                            var next=new List<string>(GetArcExcludedListIds(desktop));
                            if(include.IsChecked==true) next.RemoveAll(value => value==id); else next.Add(id);
                            try {
                                if(desktop) settings.DesktopArcExcludedListIds=next; else settings.OverlayArcExcludedListIds=next;
                                Storage.Write("settings.json",settings);
                            } catch(Exception error) {
                                if(desktop) settings.DesktopArcExcludedListIds=previous; else settings.OverlayArcExcludedListIds=previous;
                                include.IsChecked=!GetArcExcludedListIds(desktop).Contains(id);
                                ThemedMessage.Show(owner,error.Message,"Arc menu");
                            }
                        };
                        inheritedPanel.Children.Add(include);
                    }
                }
                if(!custom || !section.IsExpanded || initialized) return;
                initialized=true;
                var options=new Dictionary<string,List<KeyValuePair<string,string>>> {
                    {"game",library.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).Select(g => new KeyValuePair<string,string>(g.Id,g.Name)).ToList()},
                    {"list",CollectionOptions().Select(c => new KeyValuePair<string,string>(c.Id,c.Name)).ToList()},
                    {"tag",(settings.ListTags ?? new Dictionary<string,string>()).Values.SelectMany(value => (value ?? "").Split(',')).Select(t => t.Trim()).Where(t => t.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t,StringComparer.CurrentCultureIgnoreCase).Select(t => new KeyValuePair<string,string>(t,t)).ToList()}
                };
                var slots=(desktop ? settings.DesktopArcSlots : settings.RadialSlots) ?? DefaultArcSlots();
                var kinds=new List<ComboBox>(); var targets=new List<ComboBox>();
                var rows=new StackPanel();
                customPanel.Children.Add(rows);
                Action<ArcSlot> addSlot=slot => {
                    var row=new Grid {Margin=new Thickness(0,6,0,0)};
                    row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(28)});
                    row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(120)});
                    row.ColumnDefinitions.Add(new ColumnDefinition());
                    var number=Label((rows.Children.Count+1).ToString(),12,mint); number.VerticalAlignment=VerticalAlignment.Center; row.Children.Add(number);
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
                    rows.Children.Add(row);
                };
                foreach(var slot in slots) addSlot(slot ?? new ArcSlot());
                if(rows.Children.Count==0) addSlot(new ArcSlot());
                var buttons=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};
                customPanel.Children.Add(buttons);
                var add=new Button {Content="+",Padding=new Thickness(12,7,12,7),ToolTip="Add slot"};
                var remove=new Button {Content="−",Padding=new Thickness(12,7,12,7),Margin=new Thickness(6,0,0,0),ToolTip="Remove last slot",IsEnabled=rows.Children.Count>1};
                System.Windows.Automation.AutomationProperties.SetName(add,"Add slot");
                System.Windows.Automation.AutomationProperties.SetName(remove,"Remove last slot");
                var save=new Button {Content="Save Arc menu",Padding=new Thickness(12,7,12,7),Margin=new Thickness(6,0,0,0)};
                buttons.Children.Add(add); buttons.Children.Add(remove); buttons.Children.Add(save);
                var status=Label("",12,muted); customPanel.Children.Add(status);
                add.Click+=(s,e) => {
                    addSlot(new ArcSlot());
                    remove.IsEnabled=true;
                    status.Text="";
                };
                remove.Click+=(s,e) => {
                    int last=rows.Children.Count-1;
                    if(last<1) return;
                    rows.Children.RemoveAt(last);
                    kinds.RemoveAt(last);
                    targets.RemoveAt(last);
                    remove.IsEnabled=rows.Children.Count>1;
                    status.Text="";
                };
                save.Click+=(s,e) => {
                    var next=new List<ArcSlot>();
                    for(int i=0;i<kinds.Count;i++) {
                        string target=targets[i].SelectedValue as string;
                        if(kinds[i].SelectedIndex>0 && target==null) {
                            status.Text="Choose a target or set slot "+(i+1)+" to Unassigned."; return;
                        }
                        next.Add(target==null ? new ArcSlot() : new ArcSlot {Kind=((string)kinds[i].SelectedItem).ToLowerInvariant(),Target=target});
                    }
                    var previous=desktop ? settings.DesktopArcSlots : settings.RadialSlots;
                    try {
                        if(desktop) settings.DesktopArcSlots=next; else settings.RadialSlots=next;
                        Storage.Write("settings.json",settings); status.Text="Arc menu saved.";
                    } catch(Exception error) {
                        if(desktop) settings.DesktopArcSlots=previous; else settings.RadialSlots=previous;
                        ThemedMessage.Show(owner,error.Message,"Arc menu");
                    }
                };
            };
            section.Expanded+=(sender,args) => {if(args.Source==section) refreshMode();};
            mode.SelectionChanged+=(sender,args) => {
                if(restoringMode) return;
                string previous=currentMode();
                try {
                    string next=mode.SelectedIndex==0 ? "lists" : "custom";
                    if(desktop) settings.DesktopArcMode=next; else settings.QuickSelectMode=next;
                    Storage.Write("settings.json",settings);
                    refreshMode();
                } catch(Exception error) {
                    if(desktop) settings.DesktopArcMode=previous; else settings.QuickSelectMode=previous;
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
            if(arcPopup!=null || arcOnlyWindow!=null) arcActivatedSlot=slot;
            TraceArc("activate slot kind="+(slot==null ? "(empty)" : slot.Kind)+"; launching="+launching);
            if(launching) return;
            if(slot==null || String.IsNullOrWhiteSpace(slot.Target)) {ShowNotice("Assign an item to this slot",1.5); return;}
            if(slot.Kind=="game") {LaunchGame(library.Games.FirstOrDefault(g => g.Id==slot.Target)); return;}
            if(slot.Kind=="list") {
                if(!CollectionEnabled(slot.Target) && !IsAlwaysAvailableArcList(slot.Target)) {TraceArc("selected list is hidden"); ShowNotice("This list is hidden from the overlay.",1.5); return;}
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
