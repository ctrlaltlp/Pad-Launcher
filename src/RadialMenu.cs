using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace PadLauncher {
    public partial class Launcher {
        Popup radial;
        Window arcOnlyWindow;
        TextBlock arcOnlyNotice;
        readonly System.Windows.Media.Effects.BlurEffect arcOverlayBlur=new System.Windows.Media.Effects.BlurEffect {Radius=14,RenderingBias=System.Windows.Media.Effects.RenderingBias.Performance};
        bool radialWaitRelease,arcOpenedByShortcut,arcOnly,arcOnlyWasHidden;

        int radialIndex;
        int arcDirection,arcInput;
        long arcNextStep;
        ushort radialButtons;
        bool arcConfirmNeedsRelease;
        List<RadialSlot> radialSlots;
        readonly List<Border> radialCards=new List<Border>();
        readonly Brush arcSelected=new SolidColorBrush(Color.FromRgb(163,240,205));
        readonly Brush arcSelectedText=new SolidColorBrush(Color.FromRgb(13,25,34));
        readonly Brush arcTextBackdrop=new SolidColorBrush(Color.FromArgb(218,21,36,46));
        const double ArcCenterY=270,ArcStep=63;

        bool HandleRadial(PadState pad,long milliseconds) {

            if(radial!=null || arcOnlyWindow!=null) {
                if(!arcOpenedByShortcut && pad.RT<80) {
                    var slot=radialSlots[radialIndex];
                    CloseRadial(); radialWaitRelease=false; suppressSearchRelease=true;
                    if(settings.QuickSelectActivateOnRelease) ActivateRadialSlot(slot);
                    return true;
                }
                ushort pressed=(ushort)(pad.Buttons & ~radialButtons);
                radialButtons=pad.Buttons;
                if(arcOpenedByShortcut && (pressed&0x2000)!=0) {DismissArcShortcut(); suppressSearchRelease=true; return true;}
                int direction=(pad.Buttons&1)!=0 ? -1 : (pad.Buttons&2)!=0 ? 1 : 0;
                int input=direction==0 ? 0 : 1;
                if(input==0 && Math.Abs((int)pad.LY)>15000) {direction=pad.LY>0 ? -1 : 1; input=2;}
                if(direction!=0 && (direction!=arcDirection || input!=arcInput || milliseconds>=arcNextStep)) {
                    bool first=direction!=arcDirection || input!=arcInput;
                    radialIndex=(radialIndex+direction+radialSlots.Count)%radialSlots.Count;
                    LayoutArc(direction);
                    arcNextStep=milliseconds+(input==2 ? 105 : first ? 310 : 225);
                }
                arcDirection=direction; arcInput=input;
                if((pressed&0x1000)!=0) {
                    var slot=radialSlots[radialIndex];
                    if(!arcOnly && (arcOpenedByShortcut || settings.QuickSelectActivateOnRelease)) {
                        CloseRadial(); suppressSearchRelease=true;
                    }
                    suppressSearchRelease=true; ActivateRadialSlot(slot);
                }
                return true;
            }
            if(radialWaitRelease) {if(pad.RT<80) radialWaitRelease=false; return true;}
            if(pad.RT<160) return false;
            radialWaitRelease=true;
            OpenRadial(pad.Buttons);
            return true;
        }
        internal void ToggleArcFromShortcut() {ToggleArcShortcut(false);}
        internal void ToggleArcOnlyFromShortcut() {ToggleArcShortcut(true);}
        void ToggleArcShortcut(bool only) {
            if(exiting || launching) return;
            if(radial!=null || arcOnly) {DismissArcShortcut(); suppressSearchRelease=true; return;}
            if(GameSearchOpen) gameSearchWindow.Close();
            arcOnly=only; arcOnlyWasHidden=only && !IsVisible;
            if(arcOnly) {
                if(IsVisible) Hide();
                CancelArcReveal();
                StopListTransition();
            } else {
                if(!IsVisible) ShowOverlay(); else if(!OverlayHasForeground()) FocusVisibleOverlayFromShortcut();
                if(!IsVisible) {CloseRadial(); return;}
            }
            PadState pad;
            if(arcOnly && !preview) {
                Gamepad.StartNative(new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle());
                controllerTimer.Interval=TimeSpan.FromMilliseconds(16); controllerTimer.Start();
            }
            ushort buttons=!preview && Gamepad.Read(out pad) ? pad.Buttons : (ushort)0;
            arcOpenedByShortcut=true;
            OpenRadial(buttons);
        }
        void PollShortcutArc() {
            PadState pad;
            if(preview || !Gamepad.Read(out pad)) return;
            var target=arcOnlyWindow ?? this;
            var handle=new System.Windows.Interop.WindowInteropHelper(target).Handle;
            bool confirmDown=(pad.Buttons&0x1000)!=0;
            if(!confirmDown) arcConfirmNeedsRelease=false;
            bool foreground=handle!=IntPtr.Zero && GetForegroundWindow()==handle;
            if(!foreground && confirmDown && !arcConfirmNeedsRelease) {
                arcConfirmNeedsRelease=true;
                target.Activate(); SetForegroundWindow(handle);
            }
            // A press that requested focus must be released before it can confirm.
            if(!foreground || arcConfirmNeedsRelease) pad.Buttons=(ushort)(pad.Buttons & ~0x1000);
            HandleRadial(pad,(long)(System.Diagnostics.Stopwatch.GetTimestamp()*1000.0/System.Diagnostics.Stopwatch.Frequency));
        }
        bool HandleArcShortcutKey(System.Windows.Input.KeyEventArgs e) {
            if((radial==null && arcOnlyWindow==null) || !arcOpenedByShortcut) return false;
            e.Handled=true;
            if(e.Key==System.Windows.Input.Key.Escape) {DismissArcShortcut(); return true;}
            if(e.Key==System.Windows.Input.Key.Enter && !e.IsRepeat) {
                var slot=radialSlots[radialIndex]; if(!arcOnly) CloseRadial(); ActivateRadialSlot(slot); return true;
            }
            int direction=e.Key==System.Windows.Input.Key.Up ? -1 : e.Key==System.Windows.Input.Key.Down ? 1 : 0;
            if(direction!=0) {
                radialIndex=(radialIndex+direction+radialSlots.Count)%radialSlots.Count;
                LayoutArc(direction);
            }
            return true;
        }
        void DismissArcShortcut() {
            bool restoreOverlay=arcOnly && !arcOnlyWasHidden;
            CloseRadial();
            if(restoreOverlay) ShowOverlay(true);
        }
        void CloseRadial() {
            arcOnly=arcOnlyWasHidden=false;
            if(arcOnlyWindow!=null) {
                var window=arcOnlyWindow; arcOnlyWindow=null; window.Close();
                if(!IsVisible && !exiting) UpdateTrayHomeListener();
            }
            if(radial!=null) {radial.IsOpen=false; radial=null; scene.Effect=null;}
            radialCards.Clear(); arcOpenedByShortcut=false; arcOnlyNotice=null;
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
        IntPtr arcRevealClippedWindow;
        void CancelArcReveal() {
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
            if(!arcOnly) return;
            CloseRadial();
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
                UpdateLayout();
                controllerWaitForForegroundRelease=true;
            } catch {
                CancelArcReveal();
                throw;
            }
        }
        static double ArcX(double offset) {return 135-5.9*offset*offset;}
        void LayoutArc(int direction) {
            int count=radialCards.Count;
            for(int i=0;i<count;i++) {
                int offset=(i-radialIndex+count)%count;
                if(offset>count/2) offset-=count;
                var card=radialCards[i];
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
        void OpenRadial(ushort buttons) {
            bool useLists=settings.QuickSelectMode=="lists";
            var arcLists=useLists ? filters.Where(list => CollectionEnabled(list.Id) && !list.UnsupportedDynamic &&
                list.Id!="unavailable-collections" && !list.Id.StartsWith("radial-tag:",StringComparison.Ordinal)).ToList() : null;
            radialSlots=useLists ? arcLists.Select(list => new RadialSlot {Kind="list",Target=list.Id}).ToList()
                : (settings.RadialSlots ?? PreviewRadialSlots()).Take(8).ToList();
            if(radialSlots.Count==0) radialSlots.Add(new RadialSlot());
            radialButtons=buttons; arcConfirmNeedsRelease=(buttons&0x1000)!=0; radialIndex=0; arcDirection=arcInput=0; navigation.Reset();
            var canvas=new Canvas {Width=520,Height=540,ClipToBounds=true,IsHitTestVisible=arcOnly};
            bool isLeft=settings.QuickSelectSide=="left";
            var rail=new System.Windows.Shapes.Path {Data=Geometry.Parse(isLeft ? "M 0,18 Q 270,270 0,522" : "M 520,18 Q 250,270 520,522"),
                Stroke=mint,StrokeThickness=2,Opacity=.65,IsHitTestVisible=false};
            canvas.Children.Add(rail);
            for(int i=0;i<radialSlots.Count;i++) {
                var text=Label(useLists && arcLists.Count==0 ? "No available lists" : useLists ? arcLists[i].Name : RadialSlotLabel(radialSlots[i]),23,white);
                text.MaxWidth=324; text.TextAlignment=isLeft ? TextAlignment.Left : TextAlignment.Right; text.TextTrimming=TextTrimming.CharacterEllipsis; text.VerticalAlignment=VerticalAlignment.Center;
                var card=new Border {Height=44,CornerRadius=new CornerRadius(8),Padding=new Thickness(12,0,12,0),
                    RenderTransform=new TranslateTransform(),Child=text};
                canvas.Children.Add(card); radialCards.Add(card);
            }
            if(arcOnly) {
                arcOnlyNotice=Label("",15,mint);
                arcOnlyNotice.Width=420; arcOnlyNotice.TextAlignment=TextAlignment.Center; arcOnlyNotice.TextWrapping=TextWrapping.Wrap;
                Canvas.SetLeft(arcOnlyNotice,50); Canvas.SetTop(arcOnlyNotice,492); canvas.Children.Add(arcOnlyNotice);
            }
            LayoutArc(0);
            if(arcOnly) {ShowArcOnlyWindow(canvas,isLeft); return;}
            double scale=Math.Min(1.5,Math.Min(ActualWidth/canvas.Width,ActualHeight/canvas.Height));
            if(scale<=0) scale=1;
            var scaledArc=new Viewbox {Width=canvas.Width*scale,Height=canvas.Height*scale,Child=canvas};
            radial=new Popup {AllowsTransparency=true,Placement=PlacementMode.Relative,PlacementTarget=this,
                HorizontalOffset=isLeft ? 0 : Math.Max(0,ActualWidth-scaledArc.Width),VerticalOffset=Math.Max(0,(ActualHeight-scaledArc.Height)/2),
                Child=scaledArc,StaysOpen=true,IsHitTestVisible=false};
            radial.IsOpen=true;
            if(!arcOnly) scene.Effect=arcOverlayBlur;
        }
        void ShowArcOnlyWindow(Canvas canvas,bool isLeft) {
            var monitor=ResolveMonitor(settings.OpenMonitor,System.Windows.Forms.Screen.AllScreens,System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position));
            var area=monitor.WorkingArea;
            var view=new Viewbox {Child=canvas};
            var window=new Window {Title="Pad Launcher Arc",Content=view,Width=520,Height=540,ShowInTaskbar=false,
                WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,
                Background=Brushes.Transparent,Topmost=true,WindowStartupLocation=WindowStartupLocation.Manual};
            window.PreviewKeyDown+=(sender,args) => HandleArcShortcutKey(args);
            arcOnlyWindow=window;
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
        }
        List<RadialSlot> radialStarterSlots;
        List<RadialSlot> PreviewRadialSlots() {
            if(radialStarterSlots!=null) return radialStarterSlots;
            var random=new Random();
            Func<IEnumerable<string>,string> pick=values => {
                var choices=values.Where(value => !String.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                return choices.Count==0 ? null : choices[random.Next(choices.Count)];
            };
            var hidden=new HashSet<string>(settings.HiddenGameIds ?? new List<string>());
            var lists=CollectionOptions().Where(list => !list.UnsupportedDynamic && CollectionEnabled(list.Id)).ToList();
            var tags=(settings.ListTags ?? new Dictionary<string,string>());
            var slots=new List<RadialSlot> {
                new RadialSlot {Kind="game",Target=demo ? null : pick(library.Games.Where(game => !hidden.Contains(game.Id)).Select(game => game.Id))},
                new RadialSlot {Kind="list",Target=pick(lists.Select(list => list.Id))},
                new RadialSlot {Kind="tag",Target=pick(lists.Where(list => tags.ContainsKey(list.Id)).SelectMany(list => (tags[list.Id] ?? "").Split(',')).Select(tag => tag.Trim()))}
            };
            while(slots.Count<8) slots.Add(new RadialSlot());
            return radialStarterSlots=slots;
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
                    ThemedMessage.Show(owner,error.Message,"Quick select");
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
                    ThemedMessage.Show(owner,error.Message,"Quick select");
                }
            };
            panel.Children.Add(activateOnRelease);
            return panel;
        }
        FrameworkElement CreateRadialSettings(Window owner) {
            var section=new Expander {Header="Menu setting",Foreground=white,Margin=new Thickness(0,16,0,12)};
            var panel=new StackPanel {Margin=new Thickness(0,8,0,0)};
            section.Content=panel;
            string button=Prompts.RightTrigger(settings);
            var note=Label("Hold "+button+" to open Quick select. Use D-pad Up/Down to step or the left stick to scroll faster. The menu starts at slot 1.",12,muted);
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
                note.Text="Hold "+button+" to open Quick select. Use D-pad Up/Down to step or the left stick to scroll faster."+
                    (custom ? " The menu starts at slot 1." : " The menu follows the order of your overlay lists.");
                if(!custom || !section.IsExpanded || initialized) return;
                initialized=true;
                var options=new Dictionary<string,List<KeyValuePair<string,string>>> {
                    {"game",library.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).Select(g => new KeyValuePair<string,string>(g.Id,g.Name)).ToList()},
                    {"list",CollectionOptions().Select(c => new KeyValuePair<string,string>(c.Id,c.Name)).ToList()},
                    {"tag",(settings.ListTags ?? new Dictionary<string,string>()).Values.SelectMany(value => (value ?? "").Split(',')).Select(t => t.Trim()).Where(t => t.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(t => t,StringComparer.CurrentCultureIgnoreCase).Select(t => new KeyValuePair<string,string>(t,t)).ToList()}
                };
                var slots=settings.RadialSlots ?? PreviewRadialSlots();
                var kinds=new List<ComboBox>(); var targets=new List<ComboBox>();
                for(int i=0;i<8;i++) {
                    var slot=i<slots.Count ? slots[i] ?? new RadialSlot() : new RadialSlot();
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
                var save=new Button {Content="Save quick select",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};
                customPanel.Children.Add(save);
                var status=Label("",12,muted); customPanel.Children.Add(status);
                save.Click+=(s,e) => {
                    var next=new List<RadialSlot>();
                    for(int i=0;i<kinds.Count;i++) {
                        string target=targets[i].SelectedValue as string;
                        if(kinds[i].SelectedIndex>0 && target==null) {
                            status.Text="Choose a target or set slot "+(i+1)+" to Unassigned."; return;
                        }
                        next.Add(target==null ? new RadialSlot() : new RadialSlot {Kind=((string)kinds[i].SelectedItem).ToLowerInvariant(),Target=target});
                    }
                    var previous=settings.RadialSlots;
                    try {settings.RadialSlots=next; Storage.Write("settings.json",settings); status.Text="Quick select saved.";}
                    catch(Exception error) {settings.RadialSlots=previous; ThemedMessage.Show(owner,error.Message,"Quick select");}
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
                    ThemedMessage.Show(owner,error.Message,"Quick select");
                }
            };
            refreshMode();
            return section;
        }
        string RadialSlotLabel(RadialSlot slot) {
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
        void ActivateRadialSlot(RadialSlot slot) {
            if(launching) return;
            if(slot==null || String.IsNullOrWhiteSpace(slot.Target)) {ShowNotice("Assign an item to this slot",1.5); return;}
            if(slot.Kind=="game") {LaunchRadialGame(library.Games.FirstOrDefault(g => g.Id==slot.Target)); return;}
            if(slot.Kind=="list") {
                if(!CollectionEnabled(slot.Target)) {ShowNotice("This list is hidden from the overlay.",1.5); return;}
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
                var collection=new Collection {Id="radial-tag:"+slot.Target,Name="Tag: "+slot.Target,
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
