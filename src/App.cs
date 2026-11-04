using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

[assembly: System.Reflection.AssemblyVersion("0.1.7.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.1.7.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.1.7")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

namespace PadLauncher {
    public static class Entry {
        static Mutex instance;
        [STAThread] public static void Main(string[] args) {
            string dataPath = Argument(args,"--data");
            if (!String.IsNullOrEmpty(dataPath)) Storage.Root = System.IO.Path.GetFullPath(dataPath);
            else Storage.MigrateFrom(System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PadLauncherBackground"));
            if (args.Contains("--scan")) {
                try {
                    string path = Argument(args, "--steam") ?? SteamLibrary.DetectSteam();
                    var library = SteamLibrary.Scan(path, Argument(args, "--profile"));
                    Storage.Write("library.json", library);
                    File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scan-result.txt"), String.Format("Games: {0}\r\nCollections: {1}\r\nWarnings:\r\n{2}", library.Games.Count, library.Collections.Count, String.Join("\r\n", library.Warnings)));
                } catch (Exception e) { File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scan-result.txt"), e.ToString()); Environment.ExitCode = 1; }
                return;
            }
            bool first;
            var showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\PadLauncher.Show");
            var exitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,@"Local\PadLauncher.Exit");
            instance = new Mutex(true, @"Local\PadLauncher", out first);
            if (!first) {
                foreach (var process in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)) if (process.Id!=Process.GetCurrentProcess().Id) AllowSetForegroundWindow(process.Id);
                if (args.Contains("--quit")) exitSignal.Set(); else showSignal.Set();
                return;
            }
            if (args.Contains("--quit")) { instance.ReleaseMutex(); return; }
            var app = new Application();
            app.DispatcherUnhandledException += (s,e) => {
                try { Directory.CreateDirectory(Storage.Root); File.AppendAllText(System.IO.Path.Combine(Storage.Root, "error.log"), DateTime.Now + " " + e.Exception + Environment.NewLine); } catch { }
                MessageBox.Show(e.Exception.Message, "Pad Launcher"); e.Handled = true;
            };
            var window = new Launcher(args.Contains("--demo") || (args.Contains("--render-preview") && !args.Contains("--live-preview")), args.Contains("--render-preview"));
            RegisteredWaitHandle showWait=null,exitWait=null;
            System.Windows.Forms.NotifyIcon tray=null;
            if (!args.Contains("--render-preview")) {
                showWait=ThreadPool.RegisterWaitForSingleObject(showSignal,(s,t) => app.Dispatcher.BeginInvoke(new Action(window.ShowOverlay)),null,Timeout.Infinite,false);
                exitWait=ThreadPool.RegisterWaitForSingleObject(exitSignal,(s,t) => app.Dispatcher.BeginInvoke(new Action(window.ExitApp)),null,Timeout.Infinite,false);
                tray=new System.Windows.Forms.NotifyIcon { Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Text="Pad Launcher",Visible=true };
                var menu=new System.Windows.Forms.ContextMenuStrip();
                menu.Items.Add("Show launcher",null,(s,e) => window.ShowOverlay());
                menu.Items.Add("Show help && settings",null,(s,e) => window.ShowHelpWindow());
                menu.Items.Add("Exit app",null,(s,e) => window.ExitApp());
                tray.ContextMenuStrip=menu; tray.DoubleClick+=(s,e) => window.ToggleOverlay();
            }
            if (args.Contains("--render-preview")) {
                window.Show();
                window.Dispatcher.Invoke(DispatcherPriority.Render, new Action(() => {}));
                var image = new RenderTargetBitmap(1600, 900, 96, 96, PixelFormats.Pbgra32);
                image.Render(window.Content as Visual);
                var png = new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(image));
                using (var stream = File.Create(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview.png"))) png.Save(stream);
                window.ExitApp();
            } else {
                app.MainWindow=window; app.ShutdownMode=ShutdownMode.OnMainWindowClose;
                window.PrepareTray();
                app.Run();
            }
            if (showWait!=null) showWait.Unregister(null);
            if (exitWait!=null) exitWait.Unregister(null);
            if (tray!=null) { tray.Visible=false; tray.ContextMenuStrip.Dispose(); var icon=tray.Icon; tray.Dispose(); if(icon!=null) icon.Dispose(); }
            if (showSignal!=null) showSignal.Dispose();
            if (exitSignal!=null) exitSignal.Dispose();
            instance.ReleaseMutex();
        }
        static string Argument(string[] args, string key) { int i = Array.IndexOf(args, key); return i >= 0 && i+1 < args.Length ? args[i+1] : null; }
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int processId);
    }
    public class Launcher : Window {
        readonly Brush white = new SolidColorBrush(Color.FromRgb(236,243,242));
        readonly Brush muted = new SolidColorBrush(Color.FromRgb(141,161,168));
        readonly Brush mint = new SolidColorBrush(Color.FromRgb(163,240,205));
        readonly Grid scene = new Grid { Width = 1600, Height = 900, Background = Brushes.Transparent };
        readonly Canvas carousel = new Canvas { Name="GameCarousel",ClipToBounds = true };
        readonly TextBlock title = new TextBlock();
        readonly TextBlock collectionText = new TextBlock();
        readonly Border notice = new Border();
        readonly TextBlock noticeText = new TextBlock();
        readonly Dictionary<string,BitmapSource> images = new Dictionary<string,BitmapSource>();
        readonly Dictionary<string,FrameworkElement> cards = new Dictionary<string,FrameworkElement>();
        readonly DispatcherTimer controllerTimer = new DispatcherTimer();
        readonly DispatcherTimer noticeTimer = new DispatcherTimer();
        Settings settings;
        Library library = new Library();
        List<Game> visible = new List<Game>();
        List<Collection> filters = new List<Collection>();
        int selected, collectionIndex;
        bool scanning, launching, demo, preview;
        ushort previousButtons;
        string previousInputSource;
        readonly HoldButton exitButton = new HoldButton();
        int previousDirection;
        bool previousFast;
        DateTime nextMove = DateTime.MinValue;
        bool exiting;
        Window helpWindow;
        public bool Idle { get { return !IsVisible && !controllerTimer.IsEnabled; } }

        public Launcher(bool demoMode, bool renderPreview) {
            demo = demoMode; preview = renderPreview;
            settings = Storage.Read<Settings>("settings.json") ?? new Settings();
            Title = "Pad Launcher"; Foreground = white;
            using(var iconStream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("PadLauncher.ico")) {
                if(iconStream!=null) { var decoder=new IconBitmapDecoder(iconStream,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad); Icon=decoder.Frames[decoder.Frames.Count-1]; }
            }
            Width = 1280; Height = 760;
            WindowStartupLocation = WindowStartupLocation.Manual;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true; Background = Brushes.Transparent;
            Topmost = true; ShowInTaskbar = false; ResizeMode = ResizeMode.NoResize;
            UseLayoutRounding = true;
            if (preview) { Width = 1600; Height = 900; Left = -20000; Top = -20000; }
            FontFamily = new FontFamily("Segoe UI");
            Content = new Viewbox { Stretch = Stretch.Uniform, Child = scene };
            BuildScene();
            ApplyCarouselScale();
            PreviewKeyDown += OnKey;
            MouseWheel += (s,e) => { Move(e.Delta < 0 ? 1 : -1); };
            Closed += (s,e) => { controllerTimer.Stop(); noticeTimer.Stop(); Gamepad.StopNative(); Save(); };
            Closing += (s,e) => {
                if (!exiting) {
                    e.Cancel=true; Save(); controllerTimer.Stop(); noticeTimer.Stop(); previousInputSource=null;
                    notice.Visibility=Visibility.Collapsed; exitButton.Reset(); previousButtons=0; previousDirection=0;
                    launching=false; Hide(); Gamepad.StopNative();
                }
            };
            controllerTimer.Interval = TimeSpan.FromMilliseconds(16);
            controllerTimer.Tick += PollController;
            noticeTimer.Interval = TimeSpan.FromSeconds(5);
            noticeTimer.Tick += (s,e) => { notice.Visibility = Visibility.Collapsed; noticeTimer.Stop(); };
            if (demo) { library = Demo(); settings.SelectedId="4"; ApplyLibrary(true); }
            else {
                var cached = Storage.Read<Library>("library.json");
                if (cached != null && (String.IsNullOrEmpty(settings.SteamPath) || String.Equals(cached.SteamPath, settings.SteamPath, StringComparison.OrdinalIgnoreCase)) && (String.IsNullOrEmpty(settings.UserId) || cached.UserId == settings.UserId)) { library = cached; ApplyLibrary(true); }
                else { SetFilters(); Render(false); }
            }
        }
        public void PrepareTray() {
            controllerTimer.Stop();
            Gamepad.StopNative();
            var content=Content as FrameworkElement;
            if(content!=null) { content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout(); }
            if(!preview) Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => RefreshLibrary()));
        }
        public void ToggleOverlay() {
            if(exiting) return;
            if(IsVisible) Close(); else ShowOverlay();
        }
        public void ShowOverlay() {
            if (exiting) return;
            launching=false; exitButton.Reset(); previousButtons=0; previousDirection=0;
            ApplyLibrary(true);
            var monitor=preview ? null : ResolveMonitor(settings.OpenMonitor,System.Windows.Forms.Screen.AllScreens,System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position));
            if(monitor!=null) PlaceOnMonitor(monitor);
            Show();
            if(monitor!=null) PlaceOnMonitor(monitor);
            Activate(); Focus();
            if (!preview) { Gamepad.StartNative(new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle()); controllerTimer.Start(); }
            // Paint retained artwork first, then reread Steam's saved changes.
            if (!preview) Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => RefreshLibrary()));
        }
        public void SetDefaultCollection(string id) {
            if(id!="remember" && id!="all" && !library.Collections.Any(c => c.Id==id)) throw new ArgumentException("Choose an available collection.");
            string previous=settings.DefaultCollectionId;
            try { settings.DefaultCollectionId=id; Storage.Write("settings.json",settings); }
            catch { settings.DefaultCollectionId=previous; throw; }
        }
        public void SetCarouselScale(double scale) {
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<0.6 || scale>1.4) throw new ArgumentOutOfRangeException("scale","Choose a size between 60% and 140%.");
            double previous=settings.CarouselScale;
            try { settings.CarouselScale=scale; Storage.Write("settings.json",settings); }
            catch { settings.CarouselScale=previous; throw; }
            ApplyCarouselScale();
        }
        public void SetCarouselLayout(string layout) {
            if(layout!="angled" && layout!="rectangular") throw new ArgumentException("Choose an angled or rectangular layout.");
            string previous=settings.CarouselLayout;
            try { settings.CarouselLayout=layout; Storage.Write("settings.json",settings); }
            catch { settings.CarouselLayout=previous; throw; }
            cards.Clear(); carousel.Children.Clear(); Render(false);
        }
        void ApplyCarouselScale() {
            double scale=settings.CarouselScale;
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<0.6 || scale>1.4) scale=1;
            carousel.RenderTransformOrigin=new Point(.5,.5);
            carousel.RenderTransform=new ScaleTransform(scale,scale);
            collectionText.Margin=new Thickness(0,Math.Max(40,130+(1-scale)*250),0,0);
            title.Margin=new Thickness(0,726+(scale-1)*240,0,0);
        }
        [DllImport("user32.dll",SetLastError=true)]
        static extern bool SetWindowPos(IntPtr window,IntPtr insertAfter,int x,int y,int width,int height,uint flags);
        public static System.Windows.Forms.Screen ResolveMonitor(string preference,System.Windows.Forms.Screen[] monitors,System.Windows.Forms.Screen cursorMonitor) {
            if(!String.IsNullOrEmpty(preference) && !preference.Equals("cursor",StringComparison.OrdinalIgnoreCase)) {
                var selected=monitors.FirstOrDefault(s => s.DeviceName.Equals(preference,StringComparison.OrdinalIgnoreCase));
                if(selected!=null) return selected;
            }
            return cursorMonitor;
        }
        void PlaceOnMonitor(System.Windows.Forms.Screen monitor) {
            // Screen working areas and SetWindowPos share native pixel coordinates.
            // Avoid mixing them with WPF's device-independent Left/Top values.
            var handle=new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
            var area=monitor.WorkingArea;
            WindowState=WindowState.Normal;
            if(!SetWindowPos(handle,new IntPtr(-1),area.Left,area.Top,area.Width,area.Height,0x0010))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            // Moving between scaling factors can synchronously make WPF apply a
            // DPI-change suggested rectangle. Reapply the target working area
            // after that transition has settled, now using the destination DPI.
            if(!SetWindowPos(handle,new IntPtr(-1),area.Left,area.Top,area.Width,area.Height,0x0010))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }
        public void CycleMonitor() {
            if(preview || exiting) return;
            var monitors=System.Windows.Forms.Screen.AllScreens.OrderBy(s => s.DeviceName,StringComparer.Ordinal).ToArray();
            if(monitors.Length<2) { ShowNotice("Only one monitor is connected."); return; }
            var handle=new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
            string current=System.Windows.Forms.Screen.FromHandle(handle).DeviceName;
            int index=Array.FindIndex(monitors,s => s.DeviceName==current);
            var next=monitors[(index+1)%monitors.Length];
            PlaceOnMonitor(next); if(IsVisible) { Activate(); Focus(); }
            ShowNotice("Monitor "+(Array.IndexOf(monitors,next)+1)+" of "+monitors.Length);
        }
        public void ExitApp() { exiting=true; if(helpWindow!=null) helpWindow.Close(); Close(); }
        public void ShowHelpWindow() {
            if(helpWindow!=null) { helpWindow.Activate(); return; }
            helpWindow=CreateHelpWindow();
            helpWindow.Closed+=(s,e) => helpWindow=null;
            helpWindow.Show();
        }
        sealed class DialogOwner : System.Windows.Forms.IWin32Window {
            public IntPtr Handle { get; private set; }
            public DialogOwner(IntPtr handle) { Handle=handle; }
        }
        public string SetSteamFolder(string path) {
            if(scanning) throw new InvalidOperationException("Please wait for the current library refresh to finish.");
            if(String.IsNullOrWhiteSpace(path)) throw new System.IO.DirectoryNotFoundException("Choose the Steam installation folder containing steam.exe and steamapps.");
            string folder=System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables((path ?? "").Trim().Trim('"')));
            if(!Directory.Exists(System.IO.Path.Combine(folder,"steamapps")) || !File.Exists(System.IO.Path.Combine(folder,"steam.exe")))
                throw new System.IO.DirectoryNotFoundException("Choose the Steam installation folder containing steam.exe and steamapps.");
            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            string previous=String.IsNullOrEmpty(settings.SteamPath) ? "" : settings.SteamPath.Replace('/','\\').TrimEnd('\\');
            next.SteamPath=folder;
            if(!String.Equals(previous,folder.TrimEnd('\\'),StringComparison.OrdinalIgnoreCase)) {
                next.UserId=null; next.CollectionId="all"; next.SelectedId=null;
            }
            Storage.Write("settings.json",next); settings=next; demo=false;
            return folder;
        }
        public void SetShowGameTitle(bool show) {
            bool previous=settings.ShowGameTitle;
            try { settings.ShowGameTitle=show; Storage.Write("settings.json",settings); }
            catch { settings.ShowGameTitle=previous; throw; }
            title.Visibility=show ? Visibility.Visible : Visibility.Collapsed;
        }
        public void SetSteamUserId(string userId) {
            if(scanning) throw new InvalidOperationException("Please wait for the current library refresh to finish.");
            string selected=String.IsNullOrWhiteSpace(userId) ? null : userId.Trim();
            if(selected!=null) {
                string steam=settings.SteamPath ?? SteamLibrary.DetectSteam();
                if(String.IsNullOrEmpty(steam) || !SteamLibrary.Users(steam).Contains(selected))
                    throw new InvalidOperationException("Choose a user ID from the selected Steam installation.");
            }
            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            next.UserId=selected; next.CollectionId="all"; next.SelectedId=null;
            Storage.Write("settings.json",next); settings=next;
        }
        public Window CreateHelpWindow() {
            var dialog=new Window {
                Title="Pad Launcher · Help & Settings",Width=540,Height=720,MinWidth=480,MinHeight=500,
                WindowStartupLocation=WindowStartupLocation.CenterScreen,ResizeMode=ResizeMode.CanResize,
                Background=new SolidColorBrush(Color.FromRgb(13,25,34)),Foreground=white,FontFamily=FontFamily,
                ShowInTaskbar=false,Topmost=true,Icon=Icon
            };
            var layout=new StackPanel { Margin=new Thickness(28) };
            Action rebuild=null;
            rebuild=() => {
            layout.Children.Clear();
            var names=Prompts.Buttons(settings);
            var heading=Label("Help & Settings",26,white); heading.FontWeight=FontWeights.SemiBold; layout.Children.Add(heading);
            var monitorCaption=Label("Open on monitor",14,mint); monitorCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(monitorCaption);
            var monitorOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("cursor","Cursor's monitor")};
            var displays=System.Windows.Forms.Screen.AllScreens.OrderBy(s => s.DeviceName,StringComparer.Ordinal).ToArray();
            for(int i=0;i<displays.Length;i++) {
                var display=displays[i];
                monitorOptions.Add(new KeyValuePair<string,string>(display.DeviceName,"Monitor "+(i+1)+" · "+display.Bounds.Width+" × "+display.Bounds.Height+(display.Primary ? " (primary)" : "")));
            }
            string monitorPreference=String.IsNullOrEmpty(settings.OpenMonitor) ? "cursor" : settings.OpenMonitor;
            if(!monitorOptions.Any(p => p.Key==monitorPreference)) monitorOptions.Add(new KeyValuePair<string,string>(monitorPreference,"Saved monitor (disconnected)"));
            var monitorPicker=new ComboBox {ItemsSource=monitorOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=monitorPreference,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=new SolidColorBrush(Color.FromRgb(13,25,34)),Background=new SolidColorBrush(Color.FromRgb(225,236,240))};
            bool restoringMonitor=false;
            monitorPicker.SelectionChanged+=(s,e) => {
                if(restoringMonitor) return;
                string next=monitorPicker.SelectedValue as string,previous=settings.OpenMonitor;
                if(next==null || next==previous) return;
                try { settings.OpenMonitor=next; Storage.Write("settings.json",settings); }
                catch(Exception error) {
                    settings.OpenMonitor=previous; restoringMonitor=true; monitorPicker.SelectedValue=previous ?? "cursor"; restoringMonitor=false;
                    MessageBox.Show(dialog,"Could not save monitor preference: "+error.Message,"Settings");
                }
            };
            layout.Children.Add(monitorPicker);
            var monitorTip=Label("Applies the next time you open the overlay. If the chosen display is disconnected, the cursor's monitor is used.",12,muted); monitorTip.TextWrapping=TextWrapping.Wrap; monitorTip.Margin=new Thickness(0,7,0,4); layout.Children.Add(monitorTip);
            var defaultCaption=Label("Default collection",14,mint); defaultCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(defaultCaption);
            var collectionOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("remember","Remember last collection"),new KeyValuePair<string,string>("all","All games")};
            foreach(var collection in library.Collections) collectionOptions.Add(new KeyValuePair<string,string>(collection.Id,collection.Name));
            string defaultId=String.IsNullOrEmpty(settings.DefaultCollectionId) ? "remember" : settings.DefaultCollectionId;
            if(!collectionOptions.Any(p => p.Key==defaultId)) collectionOptions.Add(new KeyValuePair<string,string>(defaultId,"Saved collection (unavailable)"));
            var defaultPicker=new ComboBox {ItemsSource=collectionOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=defaultId,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=new SolidColorBrush(Color.FromRgb(13,25,34))};
            bool restoringDefault=false;
            defaultPicker.SelectionChanged+=(s,e) => {
                if(restoringDefault) return;
                string id=defaultPicker.SelectedValue as string,previous=settings.DefaultCollectionId ?? "remember";
                if(id==null || id==previous) return;
                try { SetDefaultCollection(id); }
                catch(Exception error) { restoringDefault=true; defaultPicker.SelectedValue=previous; restoringDefault=false; MessageBox.Show(dialog,error.Message,"Settings"); }
            };
            layout.Children.Add(defaultPicker);
            var layoutCaption=Label("Carousel layout",14,mint); layoutCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(layoutCaption);
            var layoutPicker=new ComboBox {ItemsSource=new[]{new KeyValuePair<string,string>("angled","Angled (original)"),new KeyValuePair<string,string>("rectangular","Rectangular")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.CarouselLayout=="rectangular" ? "rectangular" : "angled",FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=new SolidColorBrush(Color.FromRgb(13,25,34))};
            bool restoringLayout=false;
            layoutPicker.SelectionChanged+=(s,e) => {
                if(restoringLayout) return;
                string choice=layoutPicker.SelectedValue as string,previous=settings.CarouselLayout=="rectangular" ? "rectangular" : "angled";
                if(choice==null || choice==previous) return;
                try { SetCarouselLayout(choice); }
                catch(Exception error) { restoringLayout=true; layoutPicker.SelectedValue=previous; restoringLayout=false; MessageBox.Show(dialog,error.Message,"Settings"); }
            };
            layout.Children.Add(layoutPicker);
            var sizeCaption=Label("Carousel size",14,mint); sizeCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(sizeCaption);
            double scale=settings.CarouselScale>=.6 && settings.CarouselScale<=1.4 ? settings.CarouselScale : 1;
            var sizeLabel=Label(Math.Round(scale*100)+"%",13,white);
            var sizePicker=new Slider {Minimum=.6,Maximum=1.4,Value=scale,TickFrequency=.05,SmallChange=.05,LargeChange=.1,IsSnapToTickEnabled=true,Margin=new Thickness(0,4,0,8)};
            bool restoringSize=false;
            sizePicker.ValueChanged+=(s,e) => {
                if(restoringSize) return;
                double previous=settings.CarouselScale;
                try { SetCarouselScale(sizePicker.Value); sizeLabel.Text=Math.Round(sizePicker.Value*100)+"%"; }
                catch(Exception error) { restoringSize=true; sizePicker.Value=previous; restoringSize=false; MessageBox.Show(dialog,error.Message,"Settings"); }
            };
            layout.Children.Add(sizeLabel); layout.Children.Add(sizePicker);
            var caption=Label("Button style",14,mint); caption.Margin=new Thickness(0,14,0,10); layout.Children.Add(caption);
            var styles=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,10)};
            foreach(string style in new[]{"PlayStation","Xbox","Nintendo"}) {
                string selectedStyle=style; bool selected=Prompts.Style(settings)==style;
                var choice=new Button {Content=(selected ? "✓ " : "")+style,FontSize=14,Padding=new Thickness(13,9,13,9),Margin=new Thickness(0,0,8,0),BorderThickness=new Thickness(0),Background=selected ? mint : new SolidColorBrush(Color.FromRgb(34,51,63)),Foreground=selected ? new SolidColorBrush(Color.FromRgb(13,25,34)) : white};
                choice.Click+=(s,e) => {
                    string previous=settings.ButtonPrompts;
                    try { settings.ButtonPrompts=selectedStyle; Storage.Write("settings.json",settings); rebuild(); }
                    catch(Exception error) { settings.ButtonPrompts=previous; MessageBox.Show(dialog,"Could not save button style: "+error.Message,"Settings"); }
                };
                styles.Children.Add(choice);
            }
            layout.Children.Add(styles);
            var explanation=Label("Labels change; button positions stay the same.\nNintendo: bottom B plays, right A dismisses, tap top X to refresh.",12,muted); explanation.TextWrapping=TextWrapping.Wrap; explanation.Margin=new Thickness(0,0,0,22); layout.Children.Add(explanation);
            string[][] rows={
                new[]{"Left stick / D-pad","Browse games"},
                new[]{"Right stick","Browse games at 3× speed"},
                new[]{names[0],"Launch selected game"},
                new[]{"Tap "+names[1],"Dismiss overlay"},
                new[]{"Hold "+names[1]+" · 1 sec","Exit app completely"},
                new[]{names[2],"Switch collections"},
                new[]{names[5],"Move overlay to the next monitor"},
                new[]{"Tap "+names[3],"Refresh games and collections"},
                new[]{names[4],"Briefly show control hints"}
            };
            foreach(var row in rows) {
                var grid=new Grid { Margin=new Thickness(0,0,0,15) };
                grid.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(145)});
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                var key=Label(row[0],15,mint); key.FontWeight=FontWeights.SemiBold; grid.Children.Add(key);
                var description=Label(row[1],14,white); description.TextWrapping=TextWrapping.Wrap; Grid.SetColumn(description,1); grid.Children.Add(description);
                layout.Children.Add(grid);
            }
            var keyboard=Label("Keyboard: ← / → browse · Enter play · Esc dismiss\nPage Up / Down collections · M next monitor\nF5 refresh · F1 hints",12,muted);
            keyboard.TextWrapping=TextWrapping.Wrap; keyboard.Margin=new Thickness(0,8,0,12); layout.Children.Add(keyboard);
            var steamCaption=Label("Steam path",14,mint); steamCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(steamCaption);
            var steamPath=new TextBox {Text=settings.SteamPath ?? SteamLibrary.DetectSteam() ?? "",FontSize=14,Padding=new Thickness(9,7,9,7),ToolTip="Steam installation folder containing steam.exe and steamapps"}; layout.Children.Add(steamPath);
            Action reloadUsers=null;
            var steamActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};
            var browseSteam=new Button {Content="Browse…",FontSize=13,Padding=new Thickness(14,7,14,7),Margin=new Thickness(0,0,8,0)};
            var saveSteam=new Button {Content="Save & refresh",FontSize=13,Padding=new Thickness(14,7,14,7)};
            var steamStatus=Label("Select the Steam installation, not an individual library drive.",12,muted); steamStatus.TextWrapping=TextWrapping.Wrap; steamStatus.Margin=new Thickness(0,7,0,0);
            browseSteam.Click+=(s,e) => {
                using(var picker=new System.Windows.Forms.FolderBrowserDialog {Description="Choose the Steam installation folder",SelectedPath=steamPath.Text}) {
                    if(picker.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).EnsureHandle()))==System.Windows.Forms.DialogResult.OK) steamPath.Text=picker.SelectedPath;
                }
            };
            saveSteam.Click+=(s,e) => {
                try { steamPath.Text=SetSteamFolder(steamPath.Text); reloadUsers(); steamStatus.Text="Steam folder saved."; steamStatus.Foreground=mint; RefreshLibrary(true); }
                catch(Exception error) { steamStatus.Text=error.Message; steamStatus.Foreground=new SolidColorBrush(Color.FromRgb(255,172,153)); }
            };
            steamActions.Children.Add(browseSteam); steamActions.Children.Add(saveSteam); layout.Children.Add(steamActions); layout.Children.Add(steamStatus);
            var userCaption=Label("Steam user ID",14,mint); userCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(userCaption);
            var userPicker=new ComboBox {DisplayMemberPath="Value",SelectedValuePath="Key",FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=new SolidColorBrush(Color.FromRgb(13,25,34)),Background=new SolidColorBrush(Color.FromRgb(225,236,240))};
            bool restoringUser=false;
            reloadUsers=() => {
                restoringUser=true;
                try {
                    var options=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("","Automatic (active Steam profile)")};
                    string steam=settings.SteamPath ?? SteamLibrary.DetectSteam();
                    if(!String.IsNullOrEmpty(steam)) foreach(string id in SteamLibrary.Users(steam)) options.Add(new KeyValuePair<string,string>(id,id));
                    string selected=settings.UserId ?? "";
                    if(!options.Any(p => p.Key==selected)) options.Add(new KeyValuePair<string,string>(selected,selected+" (unavailable)"));
                    userPicker.ItemsSource=options; userPicker.SelectedValue=selected;
                } finally { restoringUser=false; }
            };
            reloadUsers();
            userPicker.SelectionChanged+=(s,e) => {
                if(restoringUser) return;
                string selected=userPicker.SelectedValue as string,previous=settings.UserId ?? "";
                if(selected==null || selected==previous) return;
                try { SetSteamUserId(selected); steamStatus.Text="Steam user saved."; steamStatus.Foreground=mint; RefreshLibrary(true); }
                catch(Exception error) {
                    restoringUser=true; userPicker.SelectedValue=previous; restoringUser=false;
                    steamStatus.Text=error.Message; steamStatus.Foreground=new SolidColorBrush(Color.FromRgb(255,172,153));
                }
            };
            layout.Children.Add(userPicker);
            var titleToggle=new CheckBox {Content="Show game title",IsChecked=settings.ShowGameTitle,FontSize=14,Foreground=white,Margin=new Thickness(0,18,0,24)};
            titleToggle.Click+=(s,e) => {
                try { SetShowGameTitle(titleToggle.IsChecked==true); }
                catch(Exception error) { titleToggle.IsChecked=settings.ShowGameTitle; MessageBox.Show(dialog,"Could not save title preference: "+error.Message,"Settings"); }
            };
            layout.Children.Add(titleToggle);
            var note=Label("The overlay opens on your chosen monitor (the cursor's monitor by default).\nThe library refreshes automatically whenever you open the overlay.\nThe app stays ready after you dismiss it. Hold "+names[1]+" for 1 second while the overlay is open, or use the tray menu, to exit completely.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,0,0,20); layout.Children.Add(note);
            var close=new Button {Content="Got it",Background=mint,Foreground=new SolidColorBrush(Color.FromRgb(13,25,34)),BorderThickness=new Thickness(0),Padding=new Thickness(22,9,22,9),FontSize=14,HorizontalAlignment=HorizontalAlignment.Right,IsCancel=true};
            close.Click+=(s,e) => dialog.Close(); layout.Children.Add(close);
            };
            rebuild();
            dialog.PreviewKeyDown+=(s,e) => { if(e.Key==System.Windows.Input.Key.Escape) { dialog.Close(); e.Handled=true; } };
            dialog.Content=new ScrollViewer {Content=layout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};
            return dialog;
        }
        TextBlock Label(string text, double size, Brush color) { return new TextBlock { Text = text, FontSize = size, Foreground = color, VerticalAlignment = VerticalAlignment.Center }; }
        void BuildScene() {
            // Local shadow fades to fully transparent before reaching the screen edges.
            scene.Children.Add(new System.Windows.Shapes.Ellipse {
                Width = 1580, Height = 740, VerticalAlignment = VerticalAlignment.Center,
                Fill = new RadialGradientBrush(new GradientStopCollection {
                    new GradientStop(Color.FromArgb(170,4,10,16),0),
                    new GradientStop(Color.FromArgb(100,4,10,16),.6),
                    new GradientStop(Color.FromArgb(0,4,10,16),1)
                }), IsHitTestVisible = false
            });
            collectionText.Name="SelectedCollection"; collectionText.FontSize = 20; collectionText.Foreground = white;
            collectionText.HorizontalAlignment = HorizontalAlignment.Center;
            collectionText.VerticalAlignment = VerticalAlignment.Top;
            collectionText.Margin = new Thickness(0,130,0,0);
            collectionText.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius=14,ShadowDepth=1,Opacity=.9 };
            scene.Children.Add(collectionText);
            carousel.Margin = new Thickness(0,205,0,195); scene.Children.Add(carousel);
            title.Name="SelectedGameTitle"; title.Foreground = white; title.FontSize = 28; title.FontWeight = FontWeights.Medium;
            title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextAlignment = TextAlignment.Center;
            title.HorizontalAlignment = HorizontalAlignment.Center; title.VerticalAlignment = VerticalAlignment.Top;
            title.MaxWidth = 1000; title.Margin = new Thickness(0,726,0,0);
            title.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius=16,ShadowDepth=1,Opacity=.95 };
            title.Visibility = settings.ShowGameTitle ? Visibility.Visible : Visibility.Collapsed;
            scene.Children.Add(title);
            notice.Background = new SolidColorBrush(Color.FromArgb(235,18,31,40)); notice.CornerRadius = new CornerRadius(8);
            notice.Padding = new Thickness(24,15,24,15); notice.VerticalAlignment = VerticalAlignment.Bottom;
            notice.HorizontalAlignment = HorizontalAlignment.Center; notice.Margin = new Thickness(0,0,0,55);
            notice.MaxWidth = 1050; notice.Visibility = Visibility.Collapsed; noticeText.Foreground = white;
            noticeText.FontSize = 16; noticeText.TextWrapping = TextWrapping.Wrap; notice.Child = noticeText; scene.Children.Add(notice);
        }
        public void ShowControls() { ShowNotice(Prompts.Help(settings) + "\nPad Launcher"); }
        void ShowNotice(string text) { noticeText.Text = text; notice.Visibility = Visibility.Visible; noticeTimer.Stop(); noticeTimer.Start(); }
        async void RefreshLibrary(bool feedback = false) {
            if (demo) { if (feedback) ShowNotice("Preview library · refresh is available with your Steam library."); return; }
            if (scanning) { if (feedback) ShowNotice("Refreshing games and collections…"); return; }
            string path = settings.SteamPath;
            if (String.IsNullOrWhiteSpace(path)) path = SteamLibrary.DetectSteam();
            if (String.IsNullOrWhiteSpace(path)) { ShowNotice("Steam not found. Choose its folder in Help & Settings."); Render(false); return; }
            scanning = true;
            if (feedback) ShowNotice("Refreshing games and collections…");
            try {
                string user = settings.UserId;
                var next = await Task.Run(() => SteamLibrary.Scan(path,user));
                library = next; settings.SteamPath = next.SteamPath;
                if(!String.IsNullOrEmpty(user)) settings.UserId=next.UserId;
                Storage.Write("library.json", library); images.Clear(); ApplyLibrary(); Save();
                if (feedback) ShowNotice("Refreshed · " + filters[collectionIndex].Name + " · " + visible.Count + " games");
                if (library.Warnings.Count > 0) ShowNotice(library.Warnings[0]);
            } catch (Exception e) { ShowNotice(e.Message); }
            finally { scanning = false; }
        }
        void ApplyLibrary(bool opening=false) {
            SetFilters();
            if(opening && !String.IsNullOrEmpty(settings.DefaultCollectionId) && settings.DefaultCollectionId!="remember") {
                collectionIndex=Math.Max(0,filters.FindIndex(c => c.Id==settings.DefaultCollectionId));
                settings.CollectionId=filters[collectionIndex].Id;
            }
            FilterGames(true);
        }
        void SetFilters() {
            filters = new List<Collection> { new Collection { Id = "all", Name = "All games" } };
            filters.AddRange(library.Collections ?? new List<Collection>());
            collectionIndex = Math.Max(0, filters.FindIndex(c => c.Id == settings.CollectionId));
        }
        void FilterGames(bool remember) {
            Collection filter = filters[collectionIndex];
            string oldId = remember ? settings.SelectedId : visible.Count > selected ? visible[selected].Id : null;
            visible = library.Games.Where(g => filter.Id == "all" || (!filter.UnsupportedDynamic && filter.Games.Contains(g.Id))).ToList();
            selected = Math.Max(0, visible.FindIndex(g => g.Id == oldId));
            cards.Clear(); carousel.Children.Clear(); Render(false);
        }
        void ChangeCollection(int delta) {
            if (launching || filters.Count == 0) return;
            collectionIndex = (collectionIndex + delta + filters.Count) % filters.Count;
            settings.CollectionId = filters[collectionIndex].Id; FilterGames(false); Save();
            if (filters[collectionIndex].UnsupportedDynamic) ShowNotice(filters[collectionIndex].UnsupportedReason ?? "This dynamic collection uses a rule that is not supported yet.");
        }
        void Move(int delta) {
            if (visible.Count == 0 || launching) return;
            selected = (selected + delta + visible.Count) % visible.Count;
            Render(true);
        }
        void Render(bool animate) {
            if (filters.Count == 0) SetFilters();
            collectionText.Text = filters[collectionIndex].Name;
            if (visible.Count == 0) {
                carousel.Children.Clear(); cards.Clear();
                title.Text = filters[collectionIndex].UnsupportedDynamic ? "This collection needs an unsupported filter" : library.Games.Count > 0 ? "No installed games in this collection" : "Your next game starts here";
                return;
            }
            Game current = visible[selected]; settings.SelectedId = current.Id;
            title.Text = current.Name;
            var keep = new HashSet<string>();
            int count = Math.Min(9,visible.Count), start = -(count/2);
            for (int slot = 0; slot < count; slot++) {
                int offset=start+slot, i=(selected+offset+visible.Count)%visible.Count;
                Game game = visible[i]; keep.Add(game.Id);
                FrameworkElement card;
                double h = offset == 0 ? 490 : 390 - Math.Min(3,Math.Abs(offset))*16;
                double w = offset == 0 ? 355 : 300;
                bool rectangular=settings.CarouselLayout=="rectangular";
                if(rectangular) w=offset==0 ? 300 : 250;
                double x = 800 - w/2 + offset*(rectangular ? 285 : 254);
                double y = offset == 0 ? 0 : (490-h)/2;
                if (!cards.TryGetValue(game.Id,out card)) {
                    card = Card(game, i, w,h,offset==0); cards[game.Id] = card; carousel.Children.Add(card);
                    Canvas.SetLeft(card,x + (animate ? Math.Sign(offset)*45 : 0)); Canvas.SetTop(card,y); card.Width = w; card.Height = h;
                }
                // Geometry changes with the card's layout, so artwork stays upright.
                Panel.SetZIndex(card, 10-Math.Abs(offset));
                SetAnimation(card, Canvas.LeftProperty,x,animate); SetAnimation(card, Canvas.TopProperty,y,animate);
                SetAnimation(card, WidthProperty,w,animate); SetAnimation(card, HeightProperty,h,animate); SetAnimation(card, OpacityProperty,offset==0 ? 1 : .48,animate);
                var border = card as Grid;
                var outline = border.Children[border.Children.Count-1] as System.Windows.Shapes.Path;
                if (outline != null) { outline.Stroke = offset==0 ? mint : new SolidColorBrush(Color.FromArgb(70,180,206,213)); outline.StrokeThickness = offset==0 ? 2 : 1; }
            }
            foreach (string id in cards.Keys.Where(k => !keep.Contains(k)).ToArray()) { carousel.Children.Remove(cards[id]); cards.Remove(id); }
        }
        void SetAnimation(FrameworkElement element, DependencyProperty property, double value, bool animate) {
            if (!animate || preview) { element.BeginAnimation(property,null); element.SetValue(property,value); return; }
            var animation = new DoubleAnimation(value,TimeSpan.FromMilliseconds(230)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            element.BeginAnimation(property,animation);
        }
        FrameworkElement Card(Game game, int index, double width, double height, bool active) {
            var grid = new Grid { Width = width, Height = height, Cursor = System.Windows.Input.Cursors.Hand, Background = Brushes.Transparent };
            var body = new Grid(); grid.Children.Add(body);
            int hash = 17; foreach (char ch in game.Name) hash = unchecked(hash*31+ch);
            byte hue = (byte)(Math.Abs((long)hash)%90);
            body.Background = new LinearGradientBrush(Color.FromRgb((byte)(28+hue/2),(byte)(65+hue/3),(byte)(82+hue/2)), Color.FromRgb(11,21,33),60);
            var bitmap = LoadImage(game.Art,700);
            if (bitmap != null) body.Children.Add(new Image { Source = bitmap, Stretch = Stretch.UniformToFill, HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch });
            else {
                body.Children.Add(new System.Windows.Shapes.Ellipse { Width=330,Height=330,Fill=new SolidColorBrush(Color.FromArgb(22,163,240,205)),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,-120,-160,0) });
                var words = Label(game.Name,31,white); words.FontWeight=FontWeights.SemiBold; words.TextWrapping=TextWrapping.Wrap; words.Margin=new Thickness(58,60,40,75); words.VerticalAlignment=VerticalAlignment.Center; body.Children.Add(words);
            }
            body.Children.Add(new System.Windows.Shapes.Rectangle { Fill = new LinearGradientBrush(Color.FromArgb(0,0,0,0),Color.FromArgb(210,5,13,20),90) });
            var outline = new System.Windows.Shapes.Path { Fill=Brushes.Transparent,IsHitTestVisible=false,Stroke=mint,StrokeThickness=2 }; grid.Children.Add(outline);
            Action geometry = () => {
                double w=grid.ActualWidth,h=grid.ActualHeight;
                double slant=settings.CarouselLayout=="rectangular" ? 0 : 73;
                var shape = new StreamGeometry();
                using (var ctx=shape.Open()) { ctx.BeginFigure(new Point(slant,0),true,true); ctx.LineTo(new Point(w,0),true,false); ctx.LineTo(new Point(w-slant,h),true,false); ctx.LineTo(new Point(0,h),true,false); }
                shape.Freeze(); body.Clip=shape; outline.Data=shape;
            };
            grid.SizeChanged += (s,e) => geometry();
            grid.MouseLeftButtonUp += (s,e) => { if (index==selected) LaunchSelected(); else { selected=index; Render(true); } e.Handled=true; };
            return grid;
        }
        BitmapSource LoadImage(string path, int size) {
            if (String.IsNullOrEmpty(path)) return null;
            string key=path+":"+size; BitmapSource cached;
            if (images.TryGetValue(key,out cached)) return cached;
            try {
                var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth=size; bitmap.UriSource=new Uri(path,UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze();
                if (images.Count>70) images.Clear(); images[key]=bitmap; return bitmap;
            } catch { images[key]=null; return null; }
        }
        void LaunchSelected() {
            if (launching || visible.Count==0) return;
            if (demo) { ShowNotice("Preview mode · connect your Steam library to launch games."); return; }
            var game=visible[selected]; ulong id;
            if (!UInt64.TryParse(game.LaunchId,out id) || id==0) { ShowNotice("This game's Steam launch ID is invalid. Refresh the library."); return; }
            launching=true;
            try {
                Save();
                Process.Start(new ProcessStartInfo("steam://rungameid/"+game.LaunchId) { UseShellExecute=true });
                Close();
            } catch (Exception e) { launching=false; ShowNotice("Steam could not be opened: "+e.Message); }
        }
        void Save() { if (demo) return; try { Storage.Write("settings.json",settings); } catch { } }
        void OnKey(object sender, System.Windows.Input.KeyEventArgs e) {
            var key=e.Key;
            switch (key) {
                case System.Windows.Input.Key.Left: Move(-1); break;
                case System.Windows.Input.Key.Right: Move(1); break;
                case System.Windows.Input.Key.Enter: if (!e.IsRepeat) LaunchSelected(); break;
                case System.Windows.Input.Key.Escape: Close(); break;
                case System.Windows.Input.Key.PageUp: ChangeCollection(-1); break;
                case System.Windows.Input.Key.PageDown: ChangeCollection(1); break;
                case System.Windows.Input.Key.F5: if (!e.IsRepeat) RefreshLibrary(true); break;
                case System.Windows.Input.Key.F1: ShowControls(); break;
                case System.Windows.Input.Key.M: if(!e.IsRepeat) CycleMonitor(); break;
                default: return;
            }
            e.Handled=true;
        }
        void PollController(object sender,EventArgs e) {
            if (!IsActive || preview) { exitButton.Reset(); previousButtons=0; previousDirection=0; nextMove=DateTime.UtcNow.AddMilliseconds(180); return; }
            PadState pad; bool found=Gamepad.Read(out pad);
            if (!found) { exitButton.Reset(); previousButtons=0; previousDirection=0; return; }
            if(previousInputSource!=Gamepad.Source) {
                previousInputSource=Gamepad.Source; previousButtons=0;
                previousDirection=0; exitButton.Reset();
            }
            ushort buttons=pad.Buttons;
            int rightDirection=pad.RX < -16000 ? -1 : pad.RX > 16000 ? 1 : 0;
            bool fast=rightDirection!=0;
            int direction=fast ? rightDirection : (buttons&0x0004)!=0 || pad.LX < -16000 ? -1 : (buttons&0x0008)!=0 || pad.LX > 16000 ? 1 : 0;
            var now=DateTime.UtcNow;
            bool changed=direction!=previousDirection || fast!=previousFast;
            if (direction!=0 && (changed || now>=nextMove)) {
                Move(direction);
                double repeat=125.0/(fast ? 3 : 1);
                nextMove=changed ? now.AddMilliseconds(330.0/(fast ? 3 : 1)) : nextMove.AddMilliseconds(repeat);
                if(nextMove<now.AddMilliseconds(-repeat*3)) nextMove=now.AddMilliseconds(repeat);
            }
            ushort pressed=(ushort)(buttons & ~previousButtons);
            previousDirection=direction; previousFast=fast; previousButtons=buttons;
            long milliseconds=(long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency);
            ButtonGesture exitGesture=exitButton.Update((buttons&0x2000)!=0,milliseconds);
            if(exitGesture==ButtonGesture.Hold) { ExitApp(); return; }
            if(exitGesture==ButtonGesture.Tap) {
                Close();
                return;
            }
            if ((pressed&0x1000)!=0) { LaunchSelected(); return; }
            if ((pressed&0x4000)!=0) CycleMonitor();
            if ((pressed&0x0100)!=0) ChangeCollection(-1);
            if ((pressed&0x0200)!=0) ChangeCollection(1);
            if ((pressed&0x8000)!=0) RefreshLibrary(true);
            if ((pressed&0x0010)!=0 && (buttons&0x0020)==0) ShowControls();
        }
        static Library Demo() {
            var library=new Library();
            string[] names={ "Hollow Knight", "Elden Ring", "Ghost of Tsushima", "The Witcher 3", "Cyberpunk 2077", "Hades II", "Red Dead Redemption 2", "Death Stranding", "Sekiro" };
            for (int i=0;i<names.Length;i++) library.Games.Add(new Game { Id=(i+1).ToString(),Name=names[i],LaunchId=(i+1).ToString(),Shortcut=i==3 });
            library.Collections.Add(new Collection { Id="couch",Name="Couch games",Games=new List<string> { "1","2","3","4" } });
            return library;
        }
    }
    [StructLayout(LayoutKind.Sequential)] public struct PadState {
        public uint Packet;
        public ushort Buttons;
        public byte LT,RT;
        public short LX,LY,RX,RY;
    }
    public sealed class ControllerRouter {
        string owner;
        public static bool Active(PadState pad) { return pad.Buttons!=0 || Math.Abs((int)pad.LX)>16000 || Math.Abs((int)pad.RX)>16000; }
        public bool Select(List<KeyValuePair<string,PadState>> pads,out PadState state,out string identity) {
            int chosen=pads.FindIndex(p => p.Key==owner && Active(p.Value));
            if(chosen<0) chosen=pads.FindIndex(p => Active(p.Value));
            if(chosen<0) chosen=pads.FindIndex(p => p.Key==owner);
            if(chosen<0 && pads.Count>0) chosen=0;
            identity=chosen>=0 ? pads[chosen].Key : null; state=chosen>=0 ? pads[chosen].Value : new PadState(); owner=identity;
            return chosen>=0;
        }
        public void Reset() { owner=null; }
    }
    public static class Gamepad {
        [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] static extern uint Read14(uint index,out PadState state);
        [DllImport("xinput1_3.dll",EntryPoint="XInputGetState")] static extern uint Read13(uint index,out PadState state);
        [DllImport("xinput9_1_0.dll",EntryPoint="XInputGetState")] static extern uint Read91(uint index,out PadState state);
        static int version;
        static readonly PlayStationInput sony=new PlayStationInput();
        static readonly ControllerRouter router=new ControllerRouter();
        public static string Source { get; private set; }
        public static void StartNative(IntPtr window) { sony.Start(window); }
        public static void StopNative() { sony.Stop(); router.Reset(); Source=null; }
        public static bool Read(out PadState state) {
            var pads=sony.ReadAll();
            for(uint i=0;i<4;i++) { PadState pad; if(ReadXbox(i,out pad)) pads.Add(new KeyValuePair<string,PadState>("xinput:"+i,pad)); }
            string identity; bool found=router.Select(pads,out state,out identity); Source=identity; return found;
        }
        static bool ReadXbox(uint i,out PadState state) {
            state=new PadState();
                try { if (version==0 || version==14) { uint r=Read14(i,out state); version=14; return r==0; } }
                catch (DllNotFoundException) { version=13; } catch (EntryPointNotFoundException) { version=13; }
                try { if (version==13) return Read13(i,out state)==0; }
                catch (DllNotFoundException) { version=91; } catch (EntryPointNotFoundException) { version=91; }
                try { return Read91(i,out state)==0; }
                catch (DllNotFoundException) { return false; } catch (EntryPointNotFoundException) { return false; }
        }
    }
}
