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

[assembly: System.Reflection.AssemblyVersion("0.3.0.0")]
[assembly: System.Reflection.AssemblyFileVersion("0.3.0.0")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.3.0")]
[assembly: System.Reflection.AssemblyTitle("Pad Launcher")]
[assembly: System.Reflection.AssemblyProduct("Pad Launcher")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

namespace PadLauncher {
    public sealed class SettingsComboBox : ComboBox {
        static ScrollViewer FindScroll(DependencyObject node) {
            if(node==null) return null;
            var scroll=node as ScrollViewer; if(scroll!=null) return scroll;
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++) {var found=FindScroll(VisualTreeHelper.GetChild(node,i)); if(found!=null) return found;}
            return null;
        }
        protected override void OnPreviewMouseWheel(System.Windows.Input.MouseWheelEventArgs e) {
            e.Handled=true;
            if(IsDropDownOpen) {
                var popup=Template.FindName("PART_Popup",this) as System.Windows.Controls.Primitives.Popup;
                var list=popup==null ? null : FindScroll(popup.Child);
                if(list!=null) {
                    double distance=list.CanContentScroll ? 3 : 48;
                    list.ScrollToVerticalOffset(list.VerticalOffset-e.Delta*distance/120.0);
                }
                return;
            }
            DependencyObject parent=VisualTreeHelper.GetParent(this);
            while(parent!=null) {
                var scroll=parent as ScrollViewer;
                if(scroll!=null) { scroll.ScrollToVerticalOffset(scroll.VerticalOffset-e.Delta*48.0/120); break; }
                parent=VisualTreeHelper.GetParent(parent);
            }
        }
    }
    public static class Entry {
        static Mutex instance;
#if ARTWORK_TEST
        const string InstanceName=@"Local\PadLauncher.ArtworkTest";
        public const string AppName="Pad Launcher - Artwork test";
#else
        const string InstanceName=@"Local\PadLauncher";
        public const string AppName="Pad Launcher";
#endif
        [STAThread] public static void Main(string[] args) {
            string dataPath = Argument(args,"--data");
            if (!String.IsNullOrEmpty(dataPath)) Storage.Root = System.IO.Path.GetFullPath(dataPath);
            else {
#if ARTWORK_TEST
                string normalRoot=Storage.Root;
                Storage.Root=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PadLauncher.ArtworkTest.Data");
                if(!File.Exists(System.IO.Path.Combine(Storage.Root,"settings.json"))) {
                    Settings testSettings=null;
                    try { testSettings=Storage.Json().Deserialize<Settings>(File.ReadAllText(System.IO.Path.Combine(normalRoot,"settings.json"))); } catch { }
                    testSettings=testSettings ?? new Settings(); testSettings.CollectionId="all"; testSettings.DefaultCollectionId="all"; testSettings.EnabledCollectionIds=null;
                    Storage.Write("settings.json",testSettings);
                }
#else
                try {
                    string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                    Storage.InitializePortable(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"SteamPadLauncher.Data"),System.IO.Path.Combine(local,"PadLauncher"),System.IO.Path.Combine(local,"PadLauncherBackground"));
                } catch(Exception error) {
                    MessageBox.Show("Could not prepare the portable data folder beside PadLauncher.exe. Place the app in a writable folder.\n\n"+error.Message,"Pad Launcher");
                    return;
                }
#endif
            }
            if (args.Contains("--scan")) {
                try {
                    string path = Argument(args, "--steam") ?? SteamLibrary.DetectSteam();
                    var library = LauncherLibrary.Scan(new Settings {SteamPath=path,UserId=Argument(args,"--profile")});
                    Storage.Write("library.json", library);
                    File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scan-result.txt"), String.Format("Games: {0}\r\nCollections: {1}\r\nWarnings:\r\n{2}", library.Games.Count, library.Collections.Count, String.Join("\r\n", library.Warnings)));
                } catch (Exception e) { File.WriteAllText(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scan-result.txt"), e.ToString()); Environment.ExitCode = 1; }
                return;
            }
            bool first;
            var showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,InstanceName+".Show");
            var exitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,InstanceName+".Exit");
            instance = new Mutex(true, InstanceName, out first);
            if (!first) {
                foreach (var process in Process.GetProcessesByName(Process.GetCurrentProcess().ProcessName)) if (process.Id!=Process.GetCurrentProcess().Id) AllowSetForegroundWindow(process.Id);
                if (args.Contains("--quit")) exitSignal.Set(); else showSignal.Set();
                return;
            }
            if (args.Contains("--quit")) { instance.ReleaseMutex(); return; }
            try {StartupSetting.MigrateLegacy();} catch {}
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
                tray=new System.Windows.Forms.NotifyIcon { Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Text=AppName,Visible=true };
                var menu=new System.Windows.Forms.ContextMenuStrip();
                menu.Items.Add("Show launcher",null,(s,e) => window.ShowOverlay());
                menu.Items.Add("Settings",null,(s,e) => window.ShowHelpWindow());
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
#if ARTWORK_TEST
                window.ShowOverlay();
#endif
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
        readonly Grid carouselHost=new Grid {Name="CarouselViewport"};
        readonly TextBlock title = new TextBlock();
        readonly TextBlock collectionText = new TextBlock();
        readonly Border notice = new Border();
        readonly TextBlock noticeText = new TextBlock();
        readonly Dictionary<string,BitmapSource> images = new Dictionary<string,BitmapSource>();
        readonly Dictionary<string,FrameworkElement> cards = new Dictionary<string,FrameworkElement>();
        readonly DispatcherTimer controllerTimer = new DispatcherTimer(DispatcherPriority.Input);
        readonly DispatcherTimer noticeTimer = new DispatcherTimer();
        Settings settings;
        Library library = new Library();
        List<Game> visible = new List<Game>();
        List<Collection> filters = new List<Collection>();
        int selected, collectionIndex;
        int gridScrollPage=-1;
        double gridScrollOffset;
        double gridViewportHeight=490;
        bool scanning, launching, demo, preview;
        ushort previousButtons;
        string previousInputSource;
        readonly HoldButton exitButton = new HoldButton();
        readonly NavigationRepeat navigation = new NavigationRepeat();
        bool exiting;
        bool saveQueued;
        Window helpWindow;
        public bool Idle { get { return !IsVisible && !controllerTimer.IsEnabled; } }

        public Launcher(bool demoMode, bool renderPreview) {
            demo = demoMode; preview = renderPreview;
            settings = Storage.Read<Settings>("settings.json") ?? new Settings();
            Title = Entry.AppName; Foreground = white;
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
            SizeChanged+=(s,e) => { if(settings.CarouselLayout=="grid") Render(false); };
            PreviewKeyDown += OnKey;
            MouseWheel += (s,e) => { Move(e.Delta < 0 ? 1 : -1); };
            Closed += (s,e) => { controllerTimer.Stop(); noticeTimer.Stop(); Gamepad.StopNative(); Save(); };
            Closing += (s,e) => {
                if (!exiting) {
                    e.Cancel=true; controllerTimer.Stop(); noticeTimer.Stop(); previousInputSource=null;
                    notice.Visibility=Visibility.Collapsed; exitButton.Reset(); previousButtons=0; navigation.Reset();
                    launching=false; Hide(); Gamepad.StopNative();
                    QueueSave();
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
            if (IsVisible) { Activate(); Focus(); return; }
            launching=false; exitButton.Reset(); previousButtons=0; navigation.Reset();
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
            if(id!="remember" && !CollectionOptions().Any(c => c.Id==id && CollectionEnabled(id))) throw new ArgumentException("Choose an available collection.");
            string previous=settings.DefaultCollectionId;
            try { settings.DefaultCollectionId=id; Storage.Write("settings.json",settings); }
            catch { settings.DefaultCollectionId=previous; throw; }
        }
        List<Collection> CollectionOptions() {
            return CombinedCollections.Options(library,settings);
        }
        bool CollectionEnabled(string id) { return settings.EnabledCollectionIds==null || settings.EnabledCollectionIds.Contains(id); }
        public void SetAvailableCollections(IEnumerable<string> ids) {
            var chosen=ids==null ? null : ids.Distinct().ToList();
            var options=CollectionOptions();
            if(chosen!=null && (chosen.Count==0 || chosen.Any(id => !options.Any(c => c.Id==id)))) throw new ArgumentException("Select at least one available collection.");
            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            next.EnabledCollectionIds=chosen;
            if(chosen!=null) {
                if(!chosen.Contains(next.CollectionId)) next.CollectionId=options.First(c => chosen.Contains(c.Id)).Id;
                if(next.DefaultCollectionId!="remember" && !chosen.Contains(next.DefaultCollectionId)) next.DefaultCollectionId="remember";
            }
            Storage.Write("settings.json",next); settings=next; ApplyLibrary();
        }
        public void SetCarouselScale(double scale) {
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<0.6 || scale>1.4) throw new ArgumentOutOfRangeException("scale","Choose a size between 60% and 140%.");
            double previous=settings.CarouselScale;
            try { settings.CarouselScale=scale; Storage.Write("settings.json",settings); }
            catch { settings.CarouselScale=previous; throw; }
            ApplyCarouselScale();
            if(settings.CarouselLayout=="grid") Render(false);
        }
        public void SetCarouselLayout(string layout) {
            if(layout!="angled" && layout!="rectangular" && layout!="grid") throw new ArgumentException("Choose an angled, rectangular or grid layout.");
            string previous=settings.CarouselLayout;
            try { settings.CarouselLayout=layout; Storage.Write("settings.json",settings); }
            catch { settings.CarouselLayout=previous; throw; }
            cards.Clear(); carousel.Children.Clear(); Render(false);
        }
        public void SetAngledPanelSizing(string sizing) {
            if(sizing!="fixed" && sizing!="fit") throw new ArgumentException("Choose Fixed or Fit artwork.");
            string previous=settings.AngledPanelSizing;
            try { settings.AngledPanelSizing=sizing; Storage.Write("settings.json",settings); }
            catch { settings.AngledPanelSizing=previous; throw; }
            if(settings.CarouselLayout!="grid" && settings.CarouselLayout!="rectangular") { cards.Clear(); carousel.Children.Clear(); Render(false); }
        }
        int GridColumns { get { return settings.GridColumns>=2 && settings.GridColumns<=10 ? settings.GridColumns : 5; } }
        int GridRows { get { return settings.GridRows>=1 && settings.GridRows<=5 ? settings.GridRows : 2; } }
        public void SetGridRows(int rows) {
            if(rows<1 || rows>5) throw new ArgumentOutOfRangeException("rows","Choose between 1 and 5 rows.");
            int previous=settings.GridRows;
            try { settings.GridRows=rows; Storage.Write("settings.json",settings); }
            catch { settings.GridRows=previous; throw; }
            if(settings.CarouselLayout=="grid") { cards.Clear(); carousel.Children.Clear(); Render(false); }
        }
        public void SetGridColumns(int columns) {
            if(columns<2 || columns>10) throw new ArgumentOutOfRangeException("columns","Choose between 2 and 10 columns.");
            int previous=settings.GridColumns;
            try { settings.GridColumns=columns; Storage.Write("settings.json",settings); }
            catch { settings.GridColumns=previous; throw; }
            if(settings.CarouselLayout=="grid") { cards.Clear(); carousel.Children.Clear(); Render(false); }
        }
        void ApplyCarouselScale() {
            double scale=settings.CarouselScale;
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<0.6 || scale>1.4) scale=1;
            carousel.RenderTransformOrigin=new Point(.5,.5);
            carousel.RenderTransform=new ScaleTransform(scale,scale);
            ConfigureViewport();
        }
        void ConfigureViewport() {
            double scale=settings.CarouselScale;
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<.6 || scale>1.4) scale=1;
            if(settings.CarouselLayout!="grid") {
                scene.Height=900;
                carouselHost.Height=Double.NaN; carouselHost.VerticalAlignment=VerticalAlignment.Stretch; carouselHost.Margin=new Thickness(0,205,0,195); carouselHost.ClipToBounds=false;
                carousel.Height=Double.NaN; carousel.Width=Double.NaN; carousel.VerticalAlignment=VerticalAlignment.Stretch;
                collectionText.Margin=new Thickness(0,Math.Max(40,130+(1-scale)*250),0,0);
                title.Margin=new Thickness(0,726+(scale-1)*240,0,0);
                gridViewportHeight=490; return;
            }
            double windowWidth=ActualWidth>0 ? ActualWidth : Width,windowHeight=ActualHeight>0 ? ActualHeight : Height;
            if(windowWidth>0 && windowHeight>0) scene.Height=1600*windowHeight/windowWidth;
            double top=settings.ShowCollectionName ? 56 : 16,bottom=settings.ShowGameTitle ? 72 : 16;
            double available=Math.Max(100,scene.Height-top-bottom);
            double panelHeight=Math.Min(150,(1440.0-(GridColumns-1)*28)/GridColumns)*1.5;
            double wanted=(panelHeight*GridRows+(GridRows-1)*20)*scale;
            double viewport=Math.Min(available,Math.Max(490*scale,wanted));
            gridViewportHeight=viewport/scale;
            carouselHost.Height=viewport; carouselHost.VerticalAlignment=VerticalAlignment.Top; carouselHost.Margin=new Thickness(0,top+(available-viewport)/2,0,0); carouselHost.ClipToBounds=true;
            carousel.Width=1600; carousel.Height=gridViewportHeight; carousel.VerticalAlignment=VerticalAlignment.Center;
            collectionText.Margin=new Thickness(0,16,0,0);
            title.Margin=new Thickness(0,Math.Max(0,scene.Height-52),0,0);
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
                next.UserId=null; next.CollectionId="all"; next.SelectedId=null; next.EnabledCollectionIds=null; next.DefaultCollectionId="remember";
            }
            Storage.Write("settings.json",next); settings=next; demo=false;
            return folder;
        }
        public void SetShowCollectionName(bool show) {
            bool previous=settings.ShowCollectionName;
            try { settings.ShowCollectionName=show; Storage.Write("settings.json",settings); }
            catch { settings.ShowCollectionName=previous; throw; }
            collectionText.Visibility=show ? Visibility.Visible : Visibility.Collapsed;
            if(settings.CarouselLayout=="grid") Render(false);
        }
        public void SetShowGameTitle(bool show) {
            bool previous=settings.ShowGameTitle;
            try { settings.ShowGameTitle=show; Storage.Write("settings.json",settings); }
            catch { settings.ShowGameTitle=previous; throw; }
            title.Visibility=show ? Visibility.Visible : Visibility.Collapsed;
            if(settings.CarouselLayout=="grid") Render(false);
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
            next.UserId=selected; next.CollectionId="all"; next.SelectedId=null; next.EnabledCollectionIds=null; next.DefaultCollectionId="remember";
            Storage.Write("settings.json",next); settings=next;
        }
        public Window CreateHelpWindow() {
            var dialog=new Window {
                Title="Pad Launcher · Settings",Width=540,Height=720,MinWidth=480,MinHeight=500,
                WindowStartupLocation=WindowStartupLocation.CenterScreen,ResizeMode=ResizeMode.CanResize,
                Background=new SolidColorBrush(Color.FromRgb(13,25,34)),Foreground=white,FontFamily=FontFamily,
                ShowInTaskbar=false,Topmost=true,Icon=Icon
            };
            using(var theme=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SettingsTheme.xaml")) {
                var resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Load(theme);
                dialog.Resources.MergedDictionaries.Add(resources);
                dialog.Resources.Add(typeof(SettingsComboBox),resources[typeof(ComboBox)]);
            }
            string inputStyle=@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type INPUT}'>
                <Setter Property='Background' Value='#1C303D'/><Setter Property='Foreground' Value='#ECF3F2'/>
                <Setter Property='BorderBrush' Value='#3A5260'/><Setter Property='BorderThickness' Value='1'/>
                <Setter Property='CaretBrush' Value='#A3F0CD'/><Setter Property='SelectionBrush' Value='#497D75'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type INPUT}'>
                    <Border x:Name='InputBorder' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='{TemplateBinding BorderThickness}' CornerRadius='6' Padding='{TemplateBinding Padding}'>
                        <ScrollViewer x:Name='PART_ContentHost' Focusable='False' HorizontalScrollBarVisibility='Hidden' VerticalScrollBarVisibility='Hidden'/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='InputBorder' Property='BorderBrush' Value='#64848E'/></Trigger>
                        <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='InputBorder' Property='BorderBrush' Value='#A3F0CD'/></Trigger>
                        <Trigger Property='IsEnabled' Value='False'><Setter TargetName='InputBorder' Property='Opacity' Value='0.5'/></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value></Setter>
            </Style>";
            dialog.Resources.Add(typeof(TextBox),System.Windows.Markup.XamlReader.Parse(inputStyle.Replace("INPUT","TextBox")));
            dialog.Resources.Add(typeof(PasswordBox),System.Windows.Markup.XamlReader.Parse(inputStyle.Replace("INPUT","PasswordBox")));
            dialog.Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type ScrollBar}'>
                <Setter Property='Width' Value='6'/><Setter Property='MinWidth' Value='6'/><Setter Property='Background' Value='Transparent'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type ScrollBar}'>
                    <Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='Vertical' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'>
                        <Track.DecreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageUpCommand}' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton>
                        <Track.Thumb><Thumb x:Name='MintThumb' Background='#A3F0CD' MinHeight='24'><Thumb.Template><ControlTemplate TargetType='{x:Type Thumb}'><Border Background='{TemplateBinding Background}' CornerRadius='3'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                        <Track.IncreaseRepeatButton><RepeatButton Command='{x:Static ScrollBar.PageDownCommand}' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton>
                    </Track></Grid>
                </ControlTemplate></Setter.Value></Setter>
            </Style>"));
            var layout=new StackPanel { Margin=new Thickness(28) };
            var helpLayout=new StackPanel {Margin=new Thickness(28)};
            var systemLayout=new StackPanel {Margin=new Thickness(28)};
            var customLayout=new StackPanel {Margin=new Thickness(28)};
            var customScroll=new ScrollViewer {Content=customLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var systemScroll=new ScrollViewer {Content=systemLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var helpScroll=new ScrollViewer {Content=helpLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var settingsScroll=new ScrollViewer {Content=layout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var tabs=new TabControl {Name="HelpSettingsTabs",Background=dialog.Background,BorderThickness=new Thickness(0),Foreground=white,Margin=new Thickness(12,12,12,0)};
            tabs.ItemContainerStyle=(Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type TabItem}'>
                <Setter Property='Background' Value='#22333F'/><Setter Property='Foreground' Value='#ECF3F2'/><Setter Property='FontSize' Value='15'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabItem}'>
                    <Border x:Name='TabBorder' Background='{TemplateBinding Background}' CornerRadius='6,6,0,0' Padding='12,11' Margin='0,0,6,0'><ContentPresenter ContentSource='Header' HorizontalAlignment='Center'/></Border>
                    <ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='TabBorder' Property='Background' Value='#A3F0CD'/><Setter Property='Foreground' Value='#0D1922'/></Trigger></ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value></Setter>
            </Style>");
            tabs.Items.Add(new TabItem {Header="Layout",Content=helpScroll});
            tabs.Items.Add(new TabItem {Header="Appearance",Content=settingsScroll});
            tabs.Items.Add(new TabItem {Header="System",Content=systemScroll});
            tabs.Items.Add(new TabItem {Header="Custom games",Content=customScroll});
            tabs.SelectedIndex=0;
            Action rebuild=null;
            rebuild=() => {
            layout.Children.Clear(); helpLayout.Children.Clear(); systemLayout.Children.Clear();
            customLayout.Children.Clear(); customLayout.Children.Add(CreateCustomGamesPanel(dialog,rebuild));
            var systemHeading=Label("System",26,white); systemHeading.FontWeight=FontWeights.SemiBold; systemLayout.Children.Add(systemHeading);
            var names=Prompts.Buttons(settings);
            var heading=Label("Appearance",26,white); heading.FontWeight=FontWeights.SemiBold; layout.Children.Add(heading);
            var helpHeading=Label("Layout",26,white); helpHeading.FontWeight=FontWeights.SemiBold; helpHeading.Margin=new Thickness(0,0,0,22); helpLayout.Children.Add(helpHeading);
            var caption=Label("Button style",14,mint); caption.Margin=new Thickness(0,0,0,10); helpLayout.Children.Add(caption);
            var styles=new StackPanel {Name="ButtonStyleChoices",Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,10)};
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
            helpLayout.Children.Add(styles);
            string[][] rows={
                new[]{"Left stick / D-pad","Browse games"},
                new[]{"Right stick","Browse games at "+settings.RightStickSpeed.ToString("0.#")+"× speed"},
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
                if(row[1]=="Launch selected game") key.Name="LaunchButtonGuide";
                var description=Label(row[1],14,white); description.TextWrapping=TextWrapping.Wrap; Grid.SetColumn(description,1); grid.Children.Add(description);
                helpLayout.Children.Add(grid);
            }
            var keyboard=Label("Keyboard: ← / → browse · ↑ / ↓ grid rows\nEnter play · Esc dismiss · Page Up / Down collections\nM next monitor · F5 refresh · F1 hints",12,muted);
            keyboard.TextWrapping=TextWrapping.Wrap; keyboard.Margin=new Thickness(0,8,0,12); helpLayout.Children.Add(keyboard);
            var speedCaption=Label("Right stick speed",14,mint); speedCaption.Margin=new Thickness(0,16,0,8); helpLayout.Children.Add(speedCaption);
            var speedValue=Label(settings.RightStickSpeed.ToString("0.#")+"×",13,white); helpLayout.Children.Add(speedValue);
            var speedPicker=new Slider {Name="RightStickSpeedSetting",Minimum=1,Maximum=10,Value=settings.RightStickSpeed,TickFrequency=.5,IsSnapToTickEnabled=true,SmallChange=.5,LargeChange=1,Margin=new Thickness(0,4,0,10)};
            speedPicker.ValueChanged+=(s,e) => {settings.RightStickSpeed=speedPicker.Value; speedValue.Text=settings.RightStickSpeed.ToString("0.#")+"×"; navigation.Reset(); QueueSave();}; helpLayout.Children.Add(speedPicker);
            var titleToggle=new CheckBox {Content="Show game title",IsChecked=settings.ShowGameTitle,FontSize=14,Foreground=white,Margin=new Thickness(0,18,0,6)};
            titleToggle.Click+=(s,e) => {
                try { SetShowGameTitle(titleToggle.IsChecked==true); }
                catch(Exception error) { titleToggle.IsChecked=settings.ShowGameTitle; MessageBox.Show(dialog,"Could not save title preference: "+error.Message,"Settings"); }
            };
            layout.Children.Add(titleToggle);
            var defaultCaption=Label("Default collection",14,mint); defaultCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(defaultCaption);
            var collectionOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("remember","Remember last collection")};
            foreach(var collection in CollectionOptions().Where(c => CollectionEnabled(c.Id))) collectionOptions.Add(new KeyValuePair<string,string>(collection.Id,collection.Name));
            string defaultId=String.IsNullOrEmpty(settings.DefaultCollectionId) ? "remember" : settings.DefaultCollectionId;
            if(!collectionOptions.Any(p => p.Key==defaultId)) collectionOptions.Add(new KeyValuePair<string,string>(defaultId,"Saved collection (unavailable)"));
            var defaultPicker=new SettingsComboBox {ItemsSource=collectionOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=defaultId,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringDefault=false;
            defaultPicker.SelectionChanged+=(s,e) => {
                if(restoringDefault) return;
                string id=defaultPicker.SelectedValue as string,previous=settings.DefaultCollectionId ?? "remember";
                if(id==null || id==previous) return;
                try { SetDefaultCollection(id); }
                catch(Exception error) { restoringDefault=true; defaultPicker.SelectedValue=previous; restoringDefault=false; MessageBox.Show(dialog,error.Message,"Settings"); }
            };
            layout.Children.Add(defaultPicker);
            var collectionToggle=new CheckBox {Content="Show collection name",IsChecked=settings.ShowCollectionName,FontSize=14,Foreground=white,Margin=new Thickness(0,12,0,8)};
            collectionToggle.Click+=(s,e) => {
                try { SetShowCollectionName(collectionToggle.IsChecked==true); }
                catch(Exception error) { collectionToggle.IsChecked=settings.ShowCollectionName; MessageBox.Show(dialog,"Could not save collection name preference: "+error.Message,"Settings"); }
            };
            layout.Children.Add(collectionToggle);
            var availableSection=new Expander {Header="Available collections",Foreground=white,FontSize=14,Margin=new Thickness(0,12,0,10)};
            var availablePanel=new StackPanel {Margin=new Thickness(0,12,0,0)};
            var allCollections=new CheckBox {Content="Include all collections (including new ones)",Foreground=white,IsChecked=settings.EnabledCollectionIds==null,Margin=new Thickness(0,0,0,10)};
            availablePanel.Children.Add(allCollections);
            var collectionChecks=new StackPanel {IsEnabled=settings.EnabledCollectionIds!=null};
            var collectionBoxes=new Dictionary<string,CheckBox>();
            Action saveAvailability=() => {
                try {
                    SetAvailableCollections(allCollections.IsChecked==true ? null : collectionBoxes.Where(p => p.Value.IsChecked==true).Select(p => p.Key));
                    var defaults=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("remember","Remember last collection")};
                    defaults.AddRange(CollectionOptions().Where(c => CollectionEnabled(c.Id)).Select(c => new KeyValuePair<string,string>(c.Id,c.Name)));
                    restoringDefault=true; defaultPicker.ItemsSource=defaults; defaultPicker.SelectedValue=settings.DefaultCollectionId ?? "remember"; restoringDefault=false;
                } catch(Exception error) {
                    allCollections.IsChecked=settings.EnabledCollectionIds==null;
                    foreach(var item in collectionBoxes) item.Value.IsChecked=CollectionEnabled(item.Key);
                    collectionChecks.IsEnabled=settings.EnabledCollectionIds!=null;
                    MessageBox.Show(dialog,error.Message,"Settings");
                }
            };
            foreach(var collection in CollectionOptions()) {
                var box=new CheckBox {Content=collection.Name,Foreground=white,IsChecked=CollectionEnabled(collection.Id),Margin=new Thickness(0,0,0,8)};
                box.Click+=(s,e) => saveAvailability();
                collectionBoxes[collection.Id]=box; collectionChecks.Children.Add(box);
            }
            allCollections.Click+=(s,e) => {
                if(allCollections.IsChecked!=true) foreach(var box in collectionBoxes.Values) box.IsChecked=true;
                collectionChecks.IsEnabled=allCollections.IsChecked!=true; saveAvailability();
            };
            availablePanel.Children.Add(new ScrollViewer {Content=collectionChecks,MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var availableTip=Label("Changes save immediately. Only selected collections appear when switching collections. Select at least one. Hidden collections are also removed from Default collection.",12,muted); availableTip.TextWrapping=TextWrapping.Wrap; availableTip.Margin=new Thickness(0,8,0,0); availablePanel.Children.Add(availableTip);
            availableSection.Content=availablePanel; layout.Children.Add(availableSection);
            var combineSection=new Expander {Header="Combine collections",Foreground=white,FontSize=14,Margin=new Thickness(0,12,0,10)};
            var combinePanel=new StackPanel {Margin=new Thickness(0,12,0,0)};
            var combinedOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("","Create a new collection")};
            foreach(var combined in settings.CombinedCollections ?? new List<CombinedCollection>()) combinedOptions.Add(new KeyValuePair<string,string>(combined.Id,combined.Name));
            var combinedPicker=new SettingsComboBox {ItemsSource=combinedOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=0,Padding=new Thickness(8)};
            combinePanel.Children.Add(combinedPicker);
            var combinedName=new TextBox {Margin=new Thickness(0,10,0,10),Padding=new Thickness(8),MaxLength=80};
            var combinedNameCaption=Label("Collection name",12,muted); combinedNameCaption.Margin=new Thickness(0,14,0,0);
            combinePanel.Children.Add(combinedNameCaption); combinePanel.Children.Add(combinedName);
            var sourcePanel=new StackPanel {Margin=new Thickness(0,10,0,0)};
            combinePanel.Children.Add(new ScrollViewer {Content=sourcePanel,MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var sourceBoxes=new Dictionary<string,CheckBox>();
            var combinedActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,10,0,0)};
            var saveCombined=new Button {Content="Create collection",Padding=new Thickness(12,7,12,7)};
            var deleteCombined=new Button {Content="Delete collection",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),Visibility=Visibility.Collapsed};
            Action loadCombined=() => {
                string id=combinedPicker.SelectedValue as string;
                var edit=(settings.CombinedCollections ?? new List<CombinedCollection>()).FirstOrDefault(c => c.Id==id);
                combinedName.Text=edit==null ? "" : edit.Name;
                sourceBoxes.Clear(); sourcePanel.Children.Clear();
                foreach(var source in CollectionOptions().Where(c => !(settings.CombinedCollections ?? new List<CombinedCollection>()).Any(x => x.Id==c.Id))) {
                    var box=new CheckBox {Content=source.Name,Foreground=white,IsChecked=edit!=null && edit.Sources.Contains(source.Id),Margin=new Thickness(0,0,0,8)};
                    sourceBoxes[source.Id]=box; sourcePanel.Children.Add(box);
                }
                saveCombined.Content=edit==null ? "Create collection" : "Save changes";
                deleteCombined.Visibility=edit==null ? Visibility.Collapsed : Visibility.Visible;
            };
            combinedPicker.SelectionChanged+=(s,e) => loadCombined(); loadCombined();
            saveCombined.Click+=(s,e) => {
                try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    CombinedCollections.Save(next,library,String.IsNullOrEmpty(combinedPicker.SelectedValue as string) ? null : (string)combinedPicker.SelectedValue,combinedName.Text,sourceBoxes.Where(p => p.Value.IsChecked==true).Select(p => p.Key));
                    Storage.Write("settings.json",next); settings=next; ApplyLibrary(); rebuild();
                } catch(Exception error) {MessageBox.Show(dialog,error.Message,"Combine collections");}
            };
            deleteCombined.Click+=(s,e) => {
                try {
                    string id=combinedPicker.SelectedValue as string;
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    next.CombinedCollections.RemoveAll(c => c.Id==id);
                    if(next.EnabledCollectionIds!=null) {next.EnabledCollectionIds.Remove(id); if(next.EnabledCollectionIds.Count==0) next.EnabledCollectionIds.Add("all");}
                    if(next.CollectionId==id) next.CollectionId="all";
                    if(next.DefaultCollectionId==id) next.DefaultCollectionId="remember";
                    Storage.Write("settings.json",next); settings=next; ApplyLibrary(); rebuild();
                } catch(Exception error) {MessageBox.Show(dialog,error.Message,"Combine collections");}
            };
            combinedActions.Children.Add(saveCombined); combinedActions.Children.Add(deleteCombined); combinePanel.Children.Add(combinedActions);
            combineSection.Content=combinePanel; layout.Children.Add(combineSection);
            var monitorCaption=Label("Open on monitor",14,mint); monitorCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(monitorCaption);
            var monitorOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("cursor","Cursor's monitor")};
            var displays=System.Windows.Forms.Screen.AllScreens.OrderBy(s => s.DeviceName,StringComparer.Ordinal).ToArray();
            for(int i=0;i<displays.Length;i++) {
                var display=displays[i];
                monitorOptions.Add(new KeyValuePair<string,string>(display.DeviceName,"Monitor "+(i+1)+" · "+display.Bounds.Width+" × "+display.Bounds.Height+(display.Primary ? " (primary)" : "")));
            }
            string monitorPreference=String.IsNullOrEmpty(settings.OpenMonitor) ? "cursor" : settings.OpenMonitor;
            if(!monitorOptions.Any(p => p.Key==monitorPreference)) monitorOptions.Add(new KeyValuePair<string,string>(monitorPreference,"Saved monitor (disconnected)"));
            var monitorPicker=new SettingsComboBox {ItemsSource=monitorOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=monitorPreference,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white,Background=new SolidColorBrush(Color.FromRgb(28,48,61))};
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
            var layoutCaption=Label("Carousel layout",14,mint); layoutCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(layoutCaption);
            var columnsControls=new StackPanel {Name="GridColumnsSettings",Visibility=settings.CarouselLayout=="grid" ? Visibility.Visible : Visibility.Collapsed};
            bool angledSizingAvailable=settings.CarouselLayout!="grid" && settings.CarouselLayout!="rectangular";
            var sizingControls=new StackPanel {Name="AngledPanelSizingSettings",Visibility=angledSizingAvailable ? Visibility.Visible : Visibility.Collapsed};
            var layoutPicker=new SettingsComboBox {Name="CarouselLayoutSetting",ItemsSource=new[]{new KeyValuePair<string,string>("angled","Angled"),new KeyValuePair<string,string>("rectangular","Rectangular"),new KeyValuePair<string,string>("grid","Grid")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.CarouselLayout=="rectangular" || settings.CarouselLayout=="grid" ? settings.CarouselLayout : "angled",FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringLayout=false;
            layoutPicker.SelectionChanged+=(s,e) => {
                if(restoringLayout) return;
                string choice=layoutPicker.SelectedValue as string,previous=settings.CarouselLayout=="rectangular" || settings.CarouselLayout=="grid" ? settings.CarouselLayout : "angled";
                if(choice==null || choice==previous) return;
                try { SetCarouselLayout(choice); columnsControls.Visibility=choice=="grid" ? Visibility.Visible : Visibility.Collapsed; sizingControls.Visibility=choice=="angled" ? Visibility.Visible : Visibility.Collapsed; }
                catch(Exception error) { restoringLayout=true; layoutPicker.SelectedValue=previous; restoringLayout=false; MessageBox.Show(dialog,error.Message,"Settings"); }
            };
            layout.Children.Add(layoutPicker);
            var sizingCaption=Label("Angled panel sizing",14,mint); sizingCaption.Margin=new Thickness(0,14,0,10); sizingControls.Children.Add(sizingCaption);
            var sizingPicker=new SettingsComboBox {ItemsSource=new[]{new KeyValuePair<string,string>("fixed","Fixed (blurred edges)"),new KeyValuePair<string,string>("fit","Fit artwork")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.AngledPanelSizing=="fixed" ? "fixed" : "fit",FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringSizing=false;
            sizingPicker.SelectionChanged+=(s,e) => {
                if(restoringSizing) return;
                string choice=sizingPicker.SelectedValue as string,previous=settings.AngledPanelSizing=="fixed" ? "fixed" : "fit";
                if(choice==null || choice==previous) return;
                try { SetAngledPanelSizing(choice); }
                catch(Exception error) { restoringSizing=true; sizingPicker.SelectedValue=previous; restoringSizing=false; MessageBox.Show(dialog,error.Message,"Settings"); }
            };
            sizingControls.Children.Add(sizingPicker); layout.Children.Add(sizingControls);
            var columnsCaption=Label("Grid columns",14,mint); columnsCaption.Margin=new Thickness(0,14,0,10); columnsControls.Children.Add(columnsCaption);
            var columnsPicker=new SettingsComboBox {ItemsSource=Enumerable.Range(2,9).ToArray(),SelectedItem=GridColumns,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringColumns=false;
            columnsPicker.SelectionChanged+=(s,e) => {
                if(restoringColumns || columnsPicker.SelectedItem==null) return;
                int previous=GridColumns,columns=(int)columnsPicker.SelectedItem;
                if(columns==previous) return;
                try { SetGridColumns(columns); }
                catch(Exception error) { restoringColumns=true; columnsPicker.SelectedItem=previous; restoringColumns=false; MessageBox.Show(dialog,error.Message,"Settings"); }
            };
            columnsControls.Children.Add(columnsPicker);
            var rowsCaption=Label("Grid rows",14,mint); rowsCaption.Margin=new Thickness(0,14,0,10); columnsControls.Children.Add(rowsCaption);
            var rowsPicker=new SettingsComboBox {ItemsSource=Enumerable.Range(1,5).ToArray(),SelectedItem=GridRows,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringRows=false;
            rowsPicker.SelectionChanged+=(s,e) => {
                if(restoringRows || rowsPicker.SelectedItem==null) return;
                int previous=GridRows,rowCount=(int)rowsPicker.SelectedItem;
                if(rowCount==previous) return;
                try { SetGridRows(rowCount); }
                catch(Exception error) { restoringRows=true; rowsPicker.SelectedItem=previous; restoringRows=false; MessageBox.Show(dialog,error.Message,"Settings"); }
            };
            columnsControls.Children.Add(rowsPicker);
            var rowsTip=Label("Rows keep their panel size. The grid scrolls to keep the selected row visible.",12,muted); rowsTip.TextWrapping=TextWrapping.Wrap; rowsTip.Margin=new Thickness(0,7,0,0); columnsControls.Children.Add(rowsTip);
            layout.Children.Add(columnsControls);
            var sizeCaption=Label("Panel size",14,mint); sizeCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(sizeCaption);
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
            var artworkCaption=Label("Game artwork",14,mint); artworkCaption.Margin=new Thickness(0,16,0,8); layout.Children.Add(artworkCaption);
            var artworkGames=new SettingsComboBox {ItemsSource=library.Games,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=settings.SelectedId,Padding=new Thickness(8)};
            layout.Children.Add(artworkGames);
            var artworkActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,12)};
            var pickArtwork=new Button {Content="Choose artwork…",Padding=new Thickness(12,7,12,7)};
            pickArtwork.Click+=(s,e) => {var game=artworkGames.SelectedItem as Game; if(game!=null) ShowArtworkPicker(dialog,game);};
            var resetArtwork=new Button {Content="Reset artwork",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            resetArtwork.Click+=(s,e) => {
                var game=artworkGames.SelectedItem as Game; if(game==null) return;
                if(scanning) {MessageBox.Show(dialog,"Please wait for the current refresh to finish.","Artwork"); return;}
                try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); if(next.ArtworkOverrides!=null) next.ArtworkOverrides.Remove(game.Id); Storage.Write("settings.json",next); settings=next; RefreshLibrary(true);}
                catch(Exception error) {MessageBox.Show(dialog,error.Message,"Artwork");}
            };
            artworkActions.Children.Add(pickArtwork); artworkActions.Children.Add(resetArtwork); layout.Children.Add(artworkActions);
            bool startupEnabled=false; try {startupEnabled=StartupSetting.Enabled;} catch {}
            var startupToggle=new CheckBox {Content="Start with Windows",IsChecked=startupEnabled,Margin=new Thickness(0,18,0,12)};
            startupToggle.Click+=(s,e) => {try {StartupSetting.Set(startupToggle.IsChecked==true);} catch(Exception error) {startupToggle.IsChecked=startupEnabled; MessageBox.Show(dialog,error.Message,"Windows startup");}}; systemLayout.Children.Add(startupToggle);
            var steamCaption=Label("Steam path",14,mint); steamCaption.Margin=new Thickness(0,14,0,10); systemLayout.Children.Add(steamCaption);
            var steamPath=new TextBox {Text=settings.SteamPath ?? SteamLibrary.DetectSteam() ?? "",FontSize=14,Padding=new Thickness(9,7,9,7),ToolTip="Steam installation folder containing steam.exe and steamapps"}; systemLayout.Children.Add(steamPath);
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
            steamActions.Children.Add(browseSteam); steamActions.Children.Add(saveSteam); systemLayout.Children.Add(steamActions); systemLayout.Children.Add(steamStatus);
            var userCaption=Label("Steam user ID",14,mint); userCaption.Margin=new Thickness(0,14,0,10); systemLayout.Children.Add(userCaption);
            var userPicker=new SettingsComboBox {DisplayMemberPath="Value",SelectedValuePath="Key",FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white,Background=new SolidColorBrush(Color.FromRgb(28,48,61))};
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
            userPicker.Margin=new Thickness(0,0,0,24);
            systemLayout.Children.Add(userPicker);
            var epicToggle=new CheckBox {Content="Include Epic Games Store games",IsChecked=settings.EpicEnabled,Foreground=white,Margin=new Thickness(0,12,0,12)};
            epicToggle.Click+=(s,e) => {settings.EpicEnabled=epicToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; systemLayout.Children.Add(epicToggle);
            var gogToggle=new CheckBox {Content="Include GOG Galaxy games",IsChecked=settings.GogEnabled,Foreground=white,Margin=new Thickness(0,12,0,12)};
            gogToggle.Click+=(s,e) => {settings.GogEnabled=gogToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; systemLayout.Children.Add(gogToggle);
            var sgdbToggle=new CheckBox {Content="SteamGridDB artwork fallback",IsChecked=settings.SteamGridDbEnabled,Foreground=white,Margin=new Thickness(0,12,0,12)};
            sgdbToggle.Click+=(s,e) => {settings.SteamGridDbEnabled=sgdbToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; systemLayout.Children.Add(sgdbToggle);
            systemLayout.Children.Add(Label("SteamGridDB API key (stored encrypted for your Windows account)",12,muted));
            var apiKey=new PasswordBox {Margin=new Thickness(0,8,0,8),Padding=new Thickness(8),PasswordChar='*',Password=ArtworkKey.Load() ?? ""}; systemLayout.Children.Add(apiKey);
            apiKey.GotKeyboardFocus+=(s,e) => apiKey.SelectAll();
            var apiStatus=Label(String.IsNullOrEmpty(apiKey.Password) ? "No key saved. Epic artwork works without a key." : "An encrypted API key is saved.",12,muted); apiStatus.TextWrapping=TextWrapping.Wrap;
            var apiButtons=new StackPanel {Orientation=Orientation.Horizontal};
            var saveKey=new Button {Content="Save & test key",Padding=new Thickness(10,5,10,5)};
            saveKey.Click+=async (s,e) => {
                string key=apiKey.Password.Trim(); if(String.IsNullOrEmpty(key)) {apiStatus.Text="Enter your API key first."; return;}
                saveKey.IsEnabled=false; apiStatus.Text="Checking key…";
                try {await Task.Run(() => ArtworkService.ValidateKey(key)); ArtworkKey.Save(key); apiKey.Password=key; apiStatus.Text="Key saved. Refreshing artwork…"; RefreshLibrary(true);}
                catch {apiStatus.Text="Could not verify key. Check the key and your connection; previous key was kept.";}
                finally {saveKey.IsEnabled=true;}
            };
            var clearKey=new Button {Content="Remove key",Padding=new Thickness(10,5,10,5),Margin=new Thickness(8,0,0,0)};
            clearKey.Click+=(s,e) => {ArtworkKey.Clear(); apiKey.Clear(); apiStatus.Text="Key removed. Downloaded artwork stays cached.";};
            var getKey=new Button {Content="Get API key",Padding=new Thickness(10,5,10,5),Margin=new Thickness(8,0,0,0)};
            getKey.Click+=(s,e) => Process.Start(new ProcessStartInfo("https://www.steamgriddb.com/profile/preferences/api") {UseShellExecute=true});
            apiButtons.Children.Add(saveKey); apiButtons.Children.Add(clearKey); apiButtons.Children.Add(getKey); systemLayout.Children.Add(apiButtons); systemLayout.Children.Add(apiStatus);
            var note=Label("The overlay opens on your chosen monitor (the cursor's monitor by default).\nThe library refreshes automatically whenever you open the overlay.\nThe app stays ready after you dismiss it. Hold "+names[1]+" for 1 second while the overlay is open, or use the tray menu, to exit completely.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,18,0,20); helpLayout.Children.Add(note);
            };
            rebuild();
            dialog.PreviewKeyDown+=(s,e) => { if(e.Key==System.Windows.Input.Key.Escape) { dialog.Close(); e.Handled=true; } };
            var root=new Grid(); root.RowDefinitions.Add(new RowDefinition()); root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto}); root.Children.Add(tabs);
            var close=new Button {Content="Close",Background=mint,Foreground=new SolidColorBrush(Color.FromRgb(13,25,34)),BorderThickness=new Thickness(0),Padding=new Thickness(22,9,22,9),FontSize=14,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(28,12,28,20),IsCancel=true};
            close.Click+=(s,e) => dialog.Close(); Grid.SetRow(close,1); root.Children.Add(close);
            dialog.Content=root;
            return dialog;
        }
        TextBlock Label(string text, double size, Brush color) { return new TextBlock { Text = text, FontSize = size, Foreground = color, VerticalAlignment = VerticalAlignment.Center }; }
        async void ShowArtworkPicker(Window owner,Game game) {
            string key=ArtworkKey.Load();
            var chooser=new Window {Title="Artwork · "+game.Name,Owner=owner,Width=660,Height=600,MinWidth=400,MinHeight=350,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,Topmost=true};
            chooser.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(20)};
            var status=Label("Finding covers…",14,white); status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,0,0,14); DockPanel.SetDock(status,Dock.Top); root.Children.Add(status);
            var original=new Button {Content="Use original grid artwork",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,14)};
            DockPanel.SetDock(original,Dock.Top); root.Children.Add(original);
            var searchRow=new DockPanel {Margin=new Thickness(0,0,0,8)};
            var searchButton=new Button {Content="Find game",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(searchButton,Dock.Right); searchRow.Children.Add(searchButton);
            var searchText=new TextBox {Text=ArtworkService.SearchQuery(game.Name),Padding=new Thickness(8),ToolTip="Search SteamGridDB by game title"}; searchRow.Children.Add(searchText); DockPanel.SetDock(searchRow,Dock.Top); root.Children.Add(searchRow);
            var searchHint=Label("Broad searches return limited suggestions. Use a more specific title for more accurate results; the current game's match is included when relevant.",12,muted); searchHint.TextWrapping=TextWrapping.Wrap; searchHint.Margin=new Thickness(0,0,0,10); DockPanel.SetDock(searchHint,Dock.Top); root.Children.Add(searchHint);
            var matchRow=new DockPanel {Margin=new Thickness(0,0,0,12),Visibility=Visibility.Collapsed};
            var useMatch=new Button {Content="Show covers",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(useMatch,Dock.Right); matchRow.Children.Add(useMatch);
            var matchPicker=new SettingsComboBox {DisplayMemberPath="Name",Padding=new Thickness(8)}; matchRow.Children.Add(matchPicker); DockPanel.SetDock(matchRow,Dock.Top); root.Children.Add(matchRow);
            var covers=new WrapPanel(); root.Children.Add(new ScrollViewer {Content=covers,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}); chooser.Content=root;
            var cancellation=new CancellationTokenSource(); chooser.Closed+=(s,e) => cancellation.Cancel(); chooser.Show();
            int loadRevision=0,selectedMatch=0;
            original.Click+=async (s,e) => {
                if(scanning) {status.Text="Please wait for the library refresh to finish."; return;}
                loadRevision++; original.IsEnabled=false; covers.IsEnabled=false; status.Text="Getting original cover…";
                try {
                    int match=selectedMatch;
                    string path=await Task.Run(() => ArtworkService.SaveChoice(game,ArtworkService.OriginalGrid(game,key,cancellation.Token,match),cancellation.Token));
                    if(cancellation.IsCancellationRequested) return;
                    if(artworkCancellation!=null) artworkCancellation.Cancel();
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    if(next.ArtworkOverrides==null) next.ArtworkOverrides=new Dictionary<string,string>(); next.ArtworkOverrides[game.Id]=path;
                    Storage.Write("settings.json",next); settings=next;
                    foreach(var entry in library.Games.Where(g => g.Id==game.Id)) ArtworkService.ApplyCached(entry,settings);
                    cards.Clear(); carousel.Children.Clear(); Render(false); chooser.Close();
                } catch(Exception error) {if(!cancellation.IsCancellationRequested) {status.Text=error is InvalidOperationException ? error.Message : "Original cover could not be downloaded. Your artwork was kept."; original.IsEnabled=true; covers.IsEnabled=true;}}
            };
            if(String.IsNullOrEmpty(key)) {searchRow.IsEnabled=false; status.Text="Use the original cover, or save a SteamGridDB API key in System to browse community artwork."; return;}
            Func<int,Task> loadCovers=async match => {
            int revision=++loadRevision; selectedMatch=match; covers.Children.Clear(); covers.IsEnabled=true; status.Text="Loading covers…";
            try {
                var choices=await Task.Run(() => ArtworkService.FindGrids(game,key,cancellation.Token,match));
                if(cancellation.IsCancellationRequested || revision!=loadRevision) return;
                status.Text=choices.Count==0 ? "No automatic match found. Use Find game above, select the correct title, then Show covers." : "Choose a cover, or use Find game to select a different title.";
                foreach(var choice in choices) {
                    cancellation.Token.ThrowIfCancellationRequested();
                    BitmapImage bitmap;
                    try {
                        byte[] data=await Task.Run(() => ArtworkService.Fetch(choice.Thumbnail,null,cancellation.Token));
                        cancellation.Token.ThrowIfCancellationRequested();
                        if(revision!=loadRevision) return;
                        using(var stream=new MemoryStream(data)) {bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth=150; bitmap.StreamSource=stream; bitmap.EndInit(); bitmap.Freeze();}
                    } catch {if(cancellation.IsCancellationRequested) return; continue;}
                    var tile=new Button {Content=new Image {Source=bitmap,Width=140,Height=210,Stretch=Stretch.Uniform},Margin=new Thickness(0,0,12,12),Padding=new Thickness(4),ToolTip="Use this cover"};
                    var picked=choice;
                    tile.Click+=async (s,e) => {
                        if(scanning) {status.Text="Please wait for the library refresh to finish, then choose a cover."; return;}
                        loadRevision++; covers.IsEnabled=false; status.Text="Downloading selected cover…";
                        try {
                            string path=await Task.Run(() => ArtworkService.SaveChoice(game,picked.Url,cancellation.Token));
                            if(cancellation.IsCancellationRequested) return;
                            if(artworkCancellation!=null) artworkCancellation.Cancel();
                            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                            if(next.ArtworkOverrides==null) next.ArtworkOverrides=new Dictionary<string,string>(); next.ArtworkOverrides[game.Id]=path;
                            Storage.Write("settings.json",next); settings=next;
                            foreach(var entry in library.Games.Where(g => g.Id==game.Id)) ArtworkService.ApplyCached(entry,settings);
                            cards.Clear(); carousel.Children.Clear(); Render(false); chooser.Close();
                        } catch(Exception) {if(!cancellation.IsCancellationRequested) {status.Text="Could not save that cover. Your previous artwork was kept."; covers.IsEnabled=true;}}
                    };
                    covers.Children.Add(tile);
                }
                if(covers.Children.Count==0 && choices.Count>0) status.Text="Cover previews could not be loaded. Check your connection and try again.";
            } catch {if(!cancellation.IsCancellationRequested && revision==loadRevision) status.Text="Could not load SteamGridDB covers. Check your key and connection.";}
            };
            searchButton.Click+=async (s,e) => {
                int revision=++loadRevision; searchButton.IsEnabled=false; status.Text="Searching SteamGridDB…";
                try {
                    string query=searchText.Text;
                    var matches=await Task.Run(() => ArtworkService.SearchForPicker(game,query,key,cancellation.Token));
                    if(cancellation.IsCancellationRequested || revision!=loadRevision) return;
                    matchPicker.ItemsSource=matches; matchPicker.SelectedIndex=matches.Count>0 ? 0 : -1; matchRow.Visibility=matches.Count>0 ? Visibility.Visible : Visibility.Collapsed;
                    status.Text=matches.Count>0 ? "Select the correct game, then Show covers." : "No titles found. Try a shorter game name.";
                } catch {if(!cancellation.IsCancellationRequested) status.Text="Search failed. Check your key and connection.";}
                finally {searchButton.IsEnabled=true;}
            };
            useMatch.Click+=async (s,e) => {var match=matchPicker.SelectedItem as ArtworkGameMatch; if(match!=null) await loadCovers(match.Id);};
            searchText.KeyDown+=(s,e) => {if(e.Key==System.Windows.Input.Key.Enter && searchButton.IsEnabled) {searchButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled=true;}};
            await loadCovers(0);
        }
        void ShowPlayniteImport(Window owner,Action rebuild) {
            var dialog=new Window {Title="Import from Playnite",Owner=owner,Width=680,Height=720,MinWidth=500,MinHeight=500,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=owner.Background,Foreground=white,FontFamily=FontFamily,Topmost=true};
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            var title=Label("Import from Playnite",24,white); title.FontWeight=FontWeights.SemiBold; title.Margin=new Thickness(0,0,0,16); header.Children.Add(title);
            string start=settings.PlaynitePath;
            if(String.IsNullOrEmpty(start)) {start=System.IO.Path.GetFullPath(System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"..","Playnite")); if(!Directory.Exists(start)) start=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Playnite");}
            Func<string,string,TextBox> pathField=(caption,initial) => {
                var label=Label(caption,14,mint); label.Margin=new Thickness(0,8,0,8); header.Children.Add(label);
                var row=new DockPanel(); var browse=new Button {Content="Browse…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(browse,Dock.Right); row.Children.Add(browse);
                var field=new TextBox {Text=initial ?? "",Padding=new Thickness(8)}; row.Children.Add(field); header.Children.Add(row);
                browse.Click+=(s,e) => {using(var picker=new System.Windows.Forms.FolderBrowserDialog {Description=caption,SelectedPath=Directory.Exists(field.Text) ? field.Text : ""}) {if(picker.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK) field.Text=picker.SelectedPath;}};
                return field;
            };
            var install=pathField("Playnite application folder",start);
            var database=pathField("Playnite library folder (leave empty for automatic)",settings.PlayniteLibraryPath);
            var controls=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,14,0,12)};
            var load=new Button {Content="Read library",Padding=new Thickness(12,7,12,7)}; controls.Children.Add(load);
            var installed=new CheckBox {Content="Installed only",IsChecked=true,Margin=new Thickness(16,0,0,0),VerticalAlignment=VerticalAlignment.Center}; controls.Children.Add(installed);
            var hidden=new CheckBox {Content="Include hidden",IsChecked=false,Margin=new Thickness(16,0,0,0),VerticalAlignment=VerticalAlignment.Center}; controls.Children.Add(hidden); header.Children.Add(controls);
            var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var status=Label("Read your library, then choose games to import. Reimport updates existing entries without duplicates.",12,muted); status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,12,0,12); footer.Children.Add(status);
            var actions=new StackPanel {Orientation=Orientation.Horizontal};
            var import=new Button {Content="Import selected",IsEnabled=false,Padding=new Thickness(14,8,14,8)}; actions.Children.Add(import);
            var close=new Button {Content="Close",Padding=new Thickness(14,8,14,8),Margin=new Thickness(8,0,0,0)}; close.Click+=(s,e) => dialog.Close(); actions.Children.Add(close); footer.Children.Add(actions);
            var listPanel=new StackPanel(); var listScroll=new ScrollViewer {Content=listPanel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}; root.Children.Add(listScroll);
            var records=new List<PlayniteGame>(); var boxes=new List<CheckBox>(); string loadedInstall=null,loadedLibrary=null; bool closed=false,busy=false;
            var selectAll=new Button {Content="Select all",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,0,8),HorizontalAlignment=HorizontalAlignment.Left};
            selectAll.Click+=(s,e) => {foreach(var box in boxes.Where(b => b.IsEnabled)) box.IsChecked=true;};
            var selectionActions=new StackPanel {Orientation=Orientation.Horizontal}; selectionActions.Children.Add(selectAll);
            var unselectAll=new Button {Content="Unselect all",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,8)};
            unselectAll.Click+=(s,e) => {foreach(var box in boxes.Where(b => b.IsEnabled)) box.IsChecked=false;}; selectionActions.Children.Add(unselectAll); header.Children.Add(selectionActions);
            dialog.Closed+=(s,e) => closed=true;
            Action renderList=() => {
                listPanel.Children.Clear(); boxes.Clear();
                foreach(var game in records.Where(g => (installed.IsChecked!=true || g.Installed) && (hidden.IsChecked==true || !g.Hidden))) {
                    bool duplicate=PlayniteImport.NativeDuplicate(game,library),existing=(settings.CustomGames ?? new List<CustomGame>()).Any(g => g.PlayniteId==game.Id);
                    var box=new CheckBox {Content=game.Name+(duplicate ? " · Already detected" : existing ? " · Update imported entry" : !game.Installed ? " · Not installed" : ""),Tag=game,IsChecked=!duplicate,Margin=new Thickness(0,0,0,10)};
                    boxes.Add(box); listPanel.Children.Add(box);
                }
                import.IsEnabled=boxes.Any(); status.Text=boxes.Count+" listed. Already detected games start unchecked. Conflicting launch actions are reviewed together before import.";
            };
            installed.Click+=(s,e) => renderList(); hidden.Click+=(s,e) => renderList();
            load.Click+=async (s,e) => {
                load.IsEnabled=false; import.IsEnabled=false; status.Text="Reading Playnite library…";
                try {
                    string folder=System.IO.Path.GetFullPath(install.Text.Trim().Trim('"'));
                    string lib=String.IsNullOrWhiteSpace(database.Text) ? PlayniteImport.LibraryFolder(folder) : System.IO.Path.GetFullPath(database.Text.Trim().Trim('"'));
                    var result=await Task.Run(() => PlayniteImport.Read(folder,lib)); if(closed) return;
                    records=result; loadedInstall=folder; loadedLibrary=lib; renderList();
                } catch(Exception error) {if(!closed) status.Text="Could not read Playnite. If it is updating or locking the library, close it and retry. "+error.GetBaseException().Message;}
                finally {if(!closed) load.IsEnabled=true;}
            };
            import.Click+=async (s,e) => {
                if(scanning) {status.Text="Please wait for the current launcher refresh to finish."; return;}
                var selected=boxes.Where(b => b.IsEnabled && b.IsChecked==true).Select(b => (PlayniteGame)b.Tag).ToList();
                if(selected.Count==0) {status.Text="Select at least one game."; return;}
                busy=true; header.IsEnabled=false; listPanel.IsEnabled=false; import.IsEnabled=false; close.IsEnabled=false; status.Text="Copying artwork and preparing entries…";
                try {
                    var prepared=await Task.Run(() => selected.Select(g => PlayniteImport.Prepare(g,loadedInstall)).ToList()); if(closed) return;
                    var conflicts=PlayniteImport.Conflicts(settings,library,prepared);
                    if(conflicts.Count>0 && !ReviewPlayniteConflicts(dialog,conflicts)) {status.Text="Import cancelled. No game entries were changed."; return;}
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    int imported=PlayniteImport.ApplyChoices(next,prepared,conflicts);
                    next.PlaynitePath=loadedInstall; next.PlayniteLibraryPath=loadedLibrary;
                    if(next.EnabledCollectionIds!=null && !next.EnabledCollectionIds.Contains("source:custom")) next.EnabledCollectionIds.Add("source:custom");
                    Storage.Write("settings.json",next); settings=next;
                    if(artworkCancellation!=null) artworkCancellation.Cancel();
                    CustomGameLibrary.Apply(library,settings); if(settings.ReplacedGameIds!=null) library.Games.RemoveAll(g => settings.ReplacedGameIds.Contains(g.Id)); foreach(var game in library.Games) ArtworkService.ApplyCached(game,settings);
                    library.Games=library.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList(); ApplyLibrary(); rebuild(); DownloadArtwork(library);
                    renderList(); status.Text="Imported or updated "+imported+" games. Find them in Custom games.";
                } catch(Exception error) {if(!closed) status.Text="Import could not complete. "+error.Message;}
                finally {busy=false; if(!closed) {header.IsEnabled=true; listPanel.IsEnabled=true; close.IsEnabled=true; import.IsEnabled=boxes.Any(b => b.IsEnabled);}}
            };
            dialog.Closing+=(s,e) => {if(busy) e.Cancel=true;}; dialog.Show();
        }
        bool ReviewPlayniteConflicts(Window owner,List<PlayniteImport.Conflict> conflicts) {
            var dialog=new Window {Title="Review matching game names",Owner=owner,Width=820,Height=680,MinWidth=540,MinHeight=400,Background=owner.Background,Foreground=white,FontFamily=FontFamily,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterOwner}; dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var note=Label("These names have different launch targets or actions. Check the entries to keep; select both if you want both. Game files are never removed.",14,white); note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,0,0,16); DockPanel.SetDock(note,Dock.Top); root.Children.Add(note);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,16,0,0)};
            var cancel=new Button {Content="Cancel import",Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,8,0)}; cancel.Click+=(s,e) => dialog.DialogResult=false; actions.Children.Add(cancel);
            var apply=new Button {Content="Apply choices & import",Padding=new Thickness(14,8,14,8)};
            apply.Click+=(s,e) => {if(conflicts.Any(c => !c.Alternatives.Any(a => a.Keep))) {MessageBox.Show(dialog,"Keep at least one entry for each game name.","Review import"); return;} dialog.DialogResult=true;}; actions.Children.Add(apply); DockPanel.SetDock(actions,Dock.Bottom); root.Children.Add(actions);
            var groups=new StackPanel(); root.Children.Add(new ScrollViewer {Content=groups,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            foreach(var conflict in conflicts) {
                var group=new StackPanel {Margin=new Thickness(14)};
                var heading=Label(conflict.Name,17,mint); heading.TextWrapping=TextWrapping.Wrap; heading.Margin=new Thickness(0,0,0,12); group.Children.Add(heading);
                foreach(var alternative in conflict.Alternatives) {
                    var choice=alternative;
                    string source=choice.Existing!=null ? "Existing entry" : "Import from Playnite";
                    var box=new CheckBox {Content=source,IsChecked=choice.Keep,Margin=new Thickness(0,0,0,6)}; box.Click+=(s,e) => choice.Keep=box.IsChecked==true; group.Children.Add(box);
                    string target=choice.Existing!=null ? choice.Existing.LaunchId : choice.Incoming.Target;
                    if(choice.Existing!=null && choice.Existing.Source!="custom") {try {target=GameLaunch.UriFor(choice.Existing);} catch {}}
                    string arguments=choice.Existing!=null ? choice.Existing.Arguments : choice.Incoming.Arguments;
                    string folder=choice.Existing!=null ? choice.Existing.WorkingDirectory : choice.Incoming.WorkingDirectory;
                    string action=choice.Existing!=null ? choice.Existing.ImportedActionInfo : choice.Incoming.PlayniteActionInfo;
                    var detail=Label(target+(String.IsNullOrWhiteSpace(arguments) ? "" : "\n"+arguments)+(String.IsNullOrWhiteSpace(folder) ? "" : "\nFolder: "+folder)+(String.IsNullOrWhiteSpace(action) ? "" : "\nPlaynite action:\n"+action),12,muted); detail.FontFamily=new FontFamily("Consolas"); detail.TextWrapping=TextWrapping.Wrap; detail.Margin=new Thickness(28,0,0,16); group.Children.Add(detail);
                }
                groups.Children.Add(new Border {Child=group,Background=new SolidColorBrush(Color.FromRgb(23,43,55)),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,0,0,12)});
            }
            return dialog.ShowDialog()==true;
        }
        StackPanel CreateCustomGamesPanel(Window dialog,Action rebuild) {
            var panel=new StackPanel();
            var heading=Label("Custom games",26,white); heading.FontWeight=FontWeights.SemiBold; heading.Margin=new Thickness(0,0,0,20); panel.Children.Add(heading);
            var importPanel=new StackPanel {Margin=new Thickness(0,16,0,0)};
            var importPlaynite=new Button {Content="Import from Playnite…",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,16)};
            importPlaynite.Click+=(s,e) => ShowPlayniteImport(dialog,rebuild); importPanel.Children.Add(importPlaynite);
            if(settings.ReplacedGameIds!=null && settings.ReplacedGameIds.Count>0) {
                var restore=new Button {Content="Restore replaced library entries",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,16)};
                restore.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); next.ReplacedGameIds=null; Storage.Write("settings.json",next); settings=next; RefreshLibrary(true); rebuild();} catch(Exception error) {MessageBox.Show(dialog,error.Message,"Restore entries");}}; importPanel.Children.Add(restore);
            }
            var tabs=new TabControl {Name="CustomGamesTabs",Background=dialog.Background,BorderThickness=new Thickness(0),Foreground=white};
            tabs.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type TabControl}'>
                <Grid><Grid.RowDefinitions><RowDefinition Height='Auto'/><RowDefinition Height='*'/></Grid.RowDefinitions>
                    <Border Background='#15242E' BorderBrush='#30434D' BorderThickness='1' CornerRadius='12' Padding='5' HorizontalAlignment='Left'>
                        <TabPanel IsItemsHost='True' KeyboardNavigation.TabIndex='1'/>
                    </Border>
                    <ContentPresenter Grid.Row='1' ContentSource='SelectedContent' Margin='0,6,0,0'/>
                </Grid>
            </ControlTemplate>");
            tabs.ItemContainerStyle=(Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type TabItem}'>
                <Setter Property='Foreground' Value='#B9C9CC'/><Setter Property='FontSize' Value='14'/><Setter Property='FontWeight' Value='SemiBold'/><Setter Property='Background' Value='Transparent'/><Setter Property='Cursor' Value='Hand'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabItem}'>
                    <Border x:Name='Segment' Background='{TemplateBinding Background}' CornerRadius='8' Padding='15,10' MinWidth='105' BorderThickness='1' BorderBrush='Transparent'>
                        <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Segment' Property='Background' Value='#2B414B'/><Setter Property='Foreground' Value='#ECF3F2'/></Trigger>
                        <Trigger Property='IsSelected' Value='True'><Setter TargetName='Segment' Property='Background' Value='#A3F0CD'/><Setter Property='Foreground' Value='#0D1922'/></Trigger>
                        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Segment' Property='BorderBrush' Value='#ECF3F2'/></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value></Setter>
            </Style>");
            tabs.Items.Add(new TabItem {Header="Added Games",Content=CreateCustomGameEditor(dialog,rebuild,true)});
            tabs.Items.Add(new TabItem {Header="Add New",Content=CreateCustomGameEditor(dialog,rebuild,false)});
            tabs.Items.Add(new TabItem {Header="Import",Content=importPanel});
            tabs.SelectedIndex=0; panel.Children.Add(tabs); return panel;
        }
        StackPanel CreateCustomGameEditor(Window dialog,Action rebuild,bool editing) {
            var panel=new StackPanel();
            panel.Margin=new Thickness(0,16,0,0);
            var options=new List<KeyValuePair<string,string>>();
            if(editing) foreach(var game in (settings.CustomGames ?? new List<CustomGame>()).OrderBy(g => g.Name)) options.Add(new KeyValuePair<string,string>(game.Id,game.Name));
            if(editing && options.Count==0) {panel.Children.Add(Label("No custom games yet. Add one in Add New.",14,muted)); return panel;}
            var picker=new SettingsComboBox {Name="CustomGamePicker",ItemsSource=options,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=0,Padding=new Thickness(8)};
            if(editing) panel.Children.Add(picker);
            Func<string,string,TextBox> field=(title,name) => {
                var caption=Label(title,14,mint); caption.Margin=new Thickness(0,16,0,8); panel.Children.Add(caption);
                var box=new TextBox {Name=name,Padding=new Thickness(8),FontSize=14}; panel.Children.Add(box); return box;
            };
            var gameName=field("Game name","CustomGameName"); gameName.MaxLength=120;
            var target=field("Executable or shortcut","CustomGameTarget");
            var missingFiles=Label("",12,new SolidColorBrush(Color.FromRgb(255,172,153))); missingFiles.TextWrapping=TextWrapping.Wrap; missingFiles.Margin=new Thickness(0,8,0,0); panel.Children.Add(missingFiles);
            var browse=new Button {Content="Browse…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,8,0,0)}; panel.Children.Add(browse);
            browse.Click+=(s,e) => {
                var file=new Microsoft.Win32.OpenFileDialog {Filter="Games and shortcuts|*.exe;*.lnk;*.bat;*.cmd",CheckFileExists=true,DereferenceLinks=false};
                if(file.ShowDialog(dialog)==true) {target.Text=file.FileName; if(String.IsNullOrWhiteSpace(gameName.Text)) gameName.Text=System.IO.Path.GetFileNameWithoutExtension(file.FileName);}
            };
            var arguments=field("Launch arguments (optional)","CustomGameArguments");
            var folder=field("Working folder (optional)","CustomGameFolder");
            var browseFolder=new Button {Content="Browse folder…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,6,12,6),Margin=new Thickness(0,8,0,0)}; panel.Children.Add(browseFolder);
            browseFolder.Click+=(s,e) => {
                using(var file=new System.Windows.Forms.FolderBrowserDialog {Description="Choose the game's working folder"}) {
                    if(file.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).Handle))==System.Windows.Forms.DialogResult.OK) folder.Text=file.SelectedPath;
                }
            };
            var art=field("Artwork (optional)","CustomGameArtwork");
            Action checkFiles=() => {
                var messages=new List<string>();
                if(!String.IsNullOrWhiteSpace(target.Text) && !File.Exists(target.Text.Trim().Trim('"'))) messages.Add("Game file missing — update the path.");
                if(!String.IsNullOrWhiteSpace(art.Text) && !File.Exists(CustomGameLibrary.ArtworkPath(art.Text.Trim().Trim('"')))) messages.Add("Artwork file missing — choose another image or clear it.");
                missingFiles.Text=String.Join("\n",messages);
            };
            target.TextChanged+=(s,e) => checkFiles(); art.TextChanged+=(s,e) => checkFiles();
            art.ToolTip="Leave artwork empty to use SteamGridDB fallback when enabled.";
            panel.Children.Remove(art);
            var artworkField=new Grid(); artworkField.Children.Add(art);
            var artworkHint=Label("Leave empty to use SteamGridDB",12,muted); artworkHint.Margin=new Thickness(9,0,9,0); artworkHint.IsHitTestVisible=false;
            artworkField.Children.Add(artworkHint); panel.Children.Add(artworkField);
            Action updateArtworkHint=() => artworkHint.Visibility=String.IsNullOrEmpty(art.Text) ? Visibility.Visible : Visibility.Collapsed;
            art.TextChanged+=(s,e) => updateArtworkHint(); updateArtworkHint();
            var artActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};
            var browseArt=new Button {Content="Choose image…",Padding=new Thickness(12,6,12,6)};
            browseArt.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="Artwork|*.png;*.jpg;*.jpeg;*.bmp",CheckFileExists=true}; if(file.ShowDialog(dialog)==true) art.Text=file.FileName;};
            var clearArt=new Button {Content="Clear image",Padding=new Thickness(12,6,12,6),Margin=new Thickness(8,0,0,0)}; clearArt.Click+=(s,e) => art.Clear();
            artActions.Children.Add(browseArt); artActions.Children.Add(clearArt); panel.Children.Add(artActions);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,32,0,0)};
            var save=new Button {Content="Add game",Padding=new Thickness(14,7,14,7)};
            var remove=new Button {Content="Remove game",Padding=new Thickness(14,7,14,7),Margin=new Thickness(8,0,0,0),Visibility=Visibility.Collapsed}; actions.Children.Add(save); actions.Children.Add(remove); panel.Children.Add(actions);
            var removalHint=Label("Removing an entry keeps your game files.",12,muted); removalHint.TextWrapping=TextWrapping.Wrap; removalHint.Margin=new Thickness(0,8,0,0); removalHint.Visibility=editing ? Visibility.Visible : Visibility.Collapsed; panel.Children.Add(removalHint);
            Action loadEntry=() => {
                var entry=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.Id==(picker.SelectedValue as string));
                gameName.Text=entry==null ? "" : entry.Name; target.Text=entry==null ? "" : entry.Target; arguments.Text=entry==null ? "" : entry.Arguments; folder.Text=entry==null ? "" : entry.WorkingDirectory; art.Text=entry==null ? "" : CustomGameLibrary.ArtworkPath(entry.Artwork);
                save.Content=entry==null ? "Add game" : "Save changes"; remove.Visibility=entry==null ? Visibility.Collapsed : Visibility.Visible;
            };
            picker.SelectionChanged+=(s,e) => loadEntry(); loadEntry();
            Action<Settings> commit=next => {
                Storage.Write("settings.json",next); settings=next;
                if(artworkCancellation!=null) artworkCancellation.Cancel();
                CustomGameLibrary.Apply(library,settings);
                foreach(var game in library.Games) ArtworkService.ApplyCached(game,settings);
                library.Games=library.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                ApplyLibrary(); rebuild(); DownloadArtwork(library);
            };
            save.Click+=(s,e) => {
                try {
                    if(scanning) throw new InvalidOperationException("Please wait for the current library refresh to finish.");
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    string id=picker.SelectedValue as string;
                    CustomGameLibrary.Save(next,new CustomGame {Id=String.IsNullOrEmpty(id) ? null : id,Name=gameName.Text,Target=target.Text,Arguments=arguments.Text,WorkingDirectory=folder.Text,Artwork=art.Text});
                    if(next.EnabledCollectionIds!=null && !next.EnabledCollectionIds.Contains("source:custom")) next.EnabledCollectionIds.Add("source:custom");
                    commit(next);
                } catch(Exception error) {MessageBox.Show(dialog,error.Message,"Custom games");}
            };
            remove.Click+=(s,e) => {
                try {
                    if(scanning) throw new InvalidOperationException("Please wait for the current library refresh to finish.");
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); next.CustomGames.RemoveAll(g => g.Id==(picker.SelectedValue as string));
                    if(next.CustomGames.Count==0) {
                        if(next.EnabledCollectionIds!=null) {next.EnabledCollectionIds.Remove("source:custom"); if(next.EnabledCollectionIds.Count==0) next.EnabledCollectionIds.Add("all");}
                        if(next.CollectionId=="source:custom") next.CollectionId="all"; if(next.DefaultCollectionId=="source:custom") next.DefaultCollectionId="remember";
                    }
                    commit(next);
                } catch(Exception error) {MessageBox.Show(dialog,error.Message,"Custom games");}
            };
            return panel;
        }
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
            collectionText.Visibility=settings.ShowCollectionName ? Visibility.Visible : Visibility.Collapsed;
            collectionText.HorizontalAlignment = HorizontalAlignment.Center;
            collectionText.VerticalAlignment = VerticalAlignment.Top;
            collectionText.Margin = new Thickness(0,130,0,0);
            collectionText.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius=14,ShadowDepth=1,Opacity=.9 };
            scene.Children.Add(collectionText);
            carouselHost.Margin = new Thickness(0,205,0,195); carouselHost.Children.Add(carousel); scene.Children.Add(carouselHost);
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
            scanning = true;
            if (feedback) ShowNotice("Refreshing games and collections…");
            try {
                string user = settings.UserId;
                var scanSettings=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                var previous=library;
                var refreshed = await Task.Run(() => {
                    var result=LauncherLibrary.Scan(scanSettings);
                    bool changed=Storage.Json().Serialize(previous)!=Storage.Json().Serialize(result);
                    if(changed) Storage.Write("library.json",result);
                    return Tuple.Create(result,changed);
                });
                var next=refreshed.Item1;
                library = next; settings.SteamPath = next.SteamPath;
                if(!String.IsNullOrEmpty(user)) settings.UserId=next.UserId;
                if(refreshed.Item2 || feedback) ApplyLibrary();
                QueueSave();
                if (feedback) ShowNotice("Refreshed · " + filters[collectionIndex].Name + " · " + visible.Count + " games");
                if (library.Warnings.Count > 0) ShowNotice(library.Warnings[0]);
                DownloadArtwork(next);
            } catch (Exception e) { ShowNotice(e.Message); }
            finally { scanning = false; }
        }
        System.Threading.CancellationTokenSource artworkCancellation;
        async void DownloadArtwork(Library batch) {
            if(artworkCancellation!=null) artworkCancellation.Cancel();
            var cancellation=new System.Threading.CancellationTokenSource(); artworkCancellation=cancellation;
            var snapshot=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            try {
                await Task.Run(() => ArtworkService.Fill(batch.Games,snapshot,cancellation.Token,(game,path) => {
                    Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => {
                        if(cancellation.IsCancellationRequested || library!=batch) return;
                        game.Art=path; game.ArtworkProvider="downloaded"; cards.Clear(); carousel.Children.Clear(); Render(false);
                    }));
                },message => Dispatcher.BeginInvoke(new Action(() => {if(!cancellation.IsCancellationRequested) ShowNotice(message);} ))));
            } catch(OperationCanceledException) {} catch(Exception) {}
        }
        void ApplyLibrary(bool opening=false) {
            string previousFilter=filters.Count>0 ? filters[collectionIndex].Id : null;
            SetFilters();
            if(opening && !String.IsNullOrEmpty(settings.DefaultCollectionId) && settings.DefaultCollectionId!="remember") {
                collectionIndex=Math.Max(0,filters.FindIndex(c => c.Id==settings.DefaultCollectionId));
                settings.CollectionId=filters[collectionIndex].Id;
            }
            if(!opening || previousFilter!=filters[collectionIndex].Id || visible.Count==0) FilterGames(true);
        }
        void SetFilters() {
            filters=CollectionOptions().Where(c => CollectionEnabled(c.Id)).ToList();
            if(filters.Count==0) filters.Add(new Collection {Id="unavailable-collections",Name="No available collections"});
            collectionIndex = Math.Max(0, filters.FindIndex(c => c.Id == settings.CollectionId));
        }
        void FilterGames(bool remember) {
            Collection filter = filters[collectionIndex];
            string oldId = remember ? settings.SelectedId : visible.Count > selected ? visible[selected].Id : null;
            visible = library.Games.Where(g => filter.Id == "all" || (!filter.UnsupportedDynamic && filter.Games.Contains(g.Id))).ToList();
            if(filter.Id=="recent") visible=visible.OrderByDescending(g => CombinedCollections.RecentPlayed(settings,g)).ToList();
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
        void MoveGridRow(int delta) {
            if(visible.Count==0 || launching) return;
            int column=selected%GridColumns,next=selected+delta*GridColumns;
            if(next>=visible.Count) next=column;
            if(next<0) next=column+((visible.Count-1-column)/GridColumns)*GridColumns;
            selected=next;
            Render(false);
        }
        void Render(bool animate) {
            ConfigureViewport();
            if (filters.Count == 0) SetFilters();
            collectionText.Text = filters[collectionIndex].Name;
            if (visible.Count == 0) {
                carousel.Children.Clear(); cards.Clear();
                title.Text = filters[collectionIndex].Id=="unavailable-collections" ? "Choose available collections in Settings" : filters[collectionIndex].UnsupportedDynamic ? "This collection needs an unsupported filter" : library.Games.Count > 0 ? "No installed games in this collection" : "Your next game starts here";
                return;
            }
            Game current = visible[selected]; settings.SelectedId = current.Id;
            title.Text = current.Name+(current.Source=="custom" && !File.Exists(current.LaunchId) ? " · Game file missing" : "");
            var keep = new HashSet<string>();
            bool gridLayout=settings.CarouselLayout=="grid";
            int pageSize=GridColumns*GridRows,pageStart=(selected/pageSize)*pageSize;
            int count = gridLayout ? Math.Min(pageSize,visible.Count-pageStart) : Math.Min(9,visible.Count), start = -(count/2);
            double gridHeight=Math.Min(150,(1440.0-(GridColumns-1)*28)/GridColumns)*1.5;
            double gridContentHeight=gridHeight*GridRows+(GridRows-1)*20;
            if(gridLayout) {
                if(gridScrollPage!=pageStart || cards.Count==0) gridScrollOffset=0;
                gridScrollPage=pageStart;
                double rowTop=((selected-pageStart)/GridColumns)*(gridHeight+20);
                if(rowTop<gridScrollOffset) gridScrollOffset=rowTop;
                if(rowTop+gridHeight>gridScrollOffset+gridViewportHeight) gridScrollOffset=rowTop+gridHeight-gridViewportHeight;
                gridScrollOffset=Math.Max(0,Math.Min(Math.Max(0,gridContentHeight-gridViewportHeight),gridScrollOffset));
            }
            for (int slot = 0; slot < count; slot++) {
                int offset=start+slot, i=gridLayout ? pageStart+slot : (selected+offset+visible.Count)%visible.Count;
                bool active=i==selected;
                Game game = visible[i]; keep.Add(game.Id);
                FrameworkElement card;
                double h = offset == 0 ? 490 : 390 - Math.Min(3,Math.Abs(offset))*16;
                double w = offset == 0 ? 355 : 300;
                bool rectangular=settings.CarouselLayout=="rectangular";
                if(rectangular) w=offset==0 ? 300 : 250;
                double x = 800 - w/2 + offset*(rectangular ? 285 : 254);
                double y = offset == 0 ? 0 : (490-h)/2;
                if(!gridLayout && !rectangular && settings.AngledPanelSizing!="fixed") {
                    FrameworkElement retainedCard;
                    var image=cards.TryGetValue(game.Id,out retainedCard) ? retainedCard.Tag as BitmapSource : LoadImage(game.Art,700);
                    if(image!=null && image.PixelWidth>0 && image.PixelHeight>0) {
                        double fit=Math.Min((w-73)/image.PixelWidth,h/image.PixelHeight);
                        w=image.PixelWidth*fit+73; h=image.PixelHeight*fit;
                        x=800-w/2+offset*254; y=(490-h)/2;
                    }
                }
                if(gridLayout) {
                    w=Math.Min(150,(1440.0-(GridColumns-1)*28)/GridColumns); h=w*1.5;
                    double gridWidth=GridColumns*w+(GridColumns-1)*28;
                    x=(1600-gridWidth)/2+(slot%GridColumns)*(w+28);
                    y=Math.Max(0,(gridViewportHeight-gridContentHeight)/2)+(slot/GridColumns)*(h+20)-gridScrollOffset;
                }
                if (!cards.TryGetValue(game.Id,out card)) {
                    card = Card(game, i, w,h,active); cards[game.Id] = card; carousel.Children.Add(card);
                    Canvas.SetLeft(card,x + (animate ? Math.Sign(offset)*45 : 0)); Canvas.SetTop(card,y); card.Width = w; card.Height = h;
                }
                // Geometry and artwork transform follow the animated panel size.
                Panel.SetZIndex(card, 10-Math.Abs(offset));
                SetAnimation(card, Canvas.LeftProperty,x,animate); SetAnimation(card, Canvas.TopProperty,y,animate);
                SetAnimation(card, WidthProperty,w,animate); SetAnimation(card, HeightProperty,h,animate); SetAnimation(card, OpacityProperty,active ? 1 : gridLayout ? .72 : .48,animate);
                var border = card as Grid;
                bool missing=game.Source=="custom" && !File.Exists(game.LaunchId);
                var missingBadge=border.Children.OfType<Border>().FirstOrDefault(b => b.Name=="MissingGameBadge");
                if(missingBadge!=null) missingBadge.Visibility=missing ? Visibility.Visible : Visibility.Collapsed;
                card.ToolTip=missing ? "Game file missing. Update its path in Custom games." : game.Name;
                var outline = border.Children[border.Children.Count-1] as System.Windows.Shapes.Path;
                if (outline != null) { outline.Stroke = missing ? new SolidColorBrush(Color.FromRgb(255,172,153)) : active ? mint : new SolidColorBrush(Color.FromArgb(70,180,206,213)); outline.StrokeThickness = active ? 2 : 1; }
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
            grid.Tag=bitmap;
            Image artwork=null;
            Grid artworkFrame=null;
            Border artworkBackdrop=null;
            if (bitmap != null) {
                artworkFrame=new Grid {Name="ArtworkFrame",HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,ClipToBounds=true};
                if(settings.CarouselLayout!="rectangular" && settings.CarouselLayout!="grid") {
                    artworkBackdrop=new Border {Name="ArtworkBackdrop",Background=ArtworkColours.Background(bitmap),IsHitTestVisible=false};
                    artworkFrame.Children.Add(artworkBackdrop);
                }
                artwork=new Image {Name="GameArtwork",Source=bitmap,Stretch=Stretch.UniformToFill,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
                artworkFrame.Children.Add(artwork); body.Children.Add(artworkFrame);
            }
            else {
                body.Children.Add(new System.Windows.Shapes.Ellipse { Width=330,Height=330,Fill=new SolidColorBrush(Color.FromArgb(22,163,240,205)),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,-120,-160,0) });
                bool gridLayout=settings.CarouselLayout=="grid";
                var words = Label(game.Name,gridLayout ? 18 : 31,white); words.FontWeight=FontWeights.SemiBold; words.TextWrapping=TextWrapping.Wrap; words.Margin=gridLayout ? new Thickness(12) : new Thickness(58,60,40,75); words.VerticalAlignment=VerticalAlignment.Center; body.Children.Add(words);
            }
            body.Children.Add(new System.Windows.Shapes.Rectangle { Fill = new LinearGradientBrush(Color.FromArgb(0,0,0,0),Color.FromArgb(210,5,13,20),90) });
            grid.Children.Add(new Border {Name="MissingGameBadge",Background=new SolidColorBrush(Color.FromRgb(54,30,28)),CornerRadius=new CornerRadius(5),Padding=new Thickness(8,5,8,5),Margin=new Thickness(0,14,0,0),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed,IsHitTestVisible=false,Child=Label("File missing",11,new SolidColorBrush(Color.FromRgb(255,172,153)))});
            var outline = new System.Windows.Shapes.Path { Fill=Brushes.Transparent,IsHitTestVisible=false,Stroke=mint,StrokeThickness=2 }; grid.Children.Add(outline);
            Action geometry = () => {
                double w=grid.ActualWidth,h=grid.ActualHeight;
                double slant=settings.CarouselLayout=="rectangular" || settings.CarouselLayout=="grid" ? 0 : 73;
                if(artwork!=null && h>0 && w>slant) {
                    artworkFrame.Width=w-slant; artworkFrame.Height=h;
                    artwork.Stretch=slant>0 ? Stretch.Uniform : Stretch.UniformToFill;
                    artworkFrame.RenderTransform=new MatrixTransform(1,0,-slant/h,1,slant,0);
                    if(artworkBackdrop!=null) {
                        double fittedHeight=bitmap.PixelHeight*Math.Min((w-slant)/bitmap.PixelWidth,h/bitmap.PixelHeight);
                        artworkBackdrop.Background=ArtworkColours.Fill(bitmap,h,fittedHeight);
                    }
                }
                var shape = new StreamGeometry();
                using (var ctx=shape.Open()) { ctx.BeginFigure(new Point(slant,0),true,true); ctx.LineTo(new Point(w,0),true,false); ctx.LineTo(new Point(w-slant,h),true,false); ctx.LineTo(new Point(0,h),true,false); }
                shape.Freeze(); grid.Clip=shape; body.Clip=shape; outline.Data=shape;
            };
            grid.SizeChanged += (s,e) => geometry();
            bool panelPressed=false;
            grid.MouseLeftButtonDown+=(s,e) => {
                if(grid.Clip==null || !grid.Clip.FillContains(e.GetPosition(grid))) return;
                panelPressed=grid.CaptureMouse(); e.Handled=true;
            };
            grid.LostMouseCapture+=(s,e) => panelPressed=false;
            grid.MouseLeftButtonUp += (s,e) => {
                if(!panelPressed) return;
                bool inside=grid.Clip!=null && grid.Clip.FillContains(e.GetPosition(grid));
                panelPressed=false; grid.ReleaseMouseCapture(); e.Handled=true;
                if(!inside) return;
                if (index==selected) LaunchSelected(); else { selected=index; Render(true); }
            };
            return grid;
        }
        BitmapSource LoadImage(string path, int size) {
            if (String.IsNullOrEmpty(path)) return null;
            string key=path+":"+size; BitmapSource cached;
            try {
                var file=new FileInfo(path);
                key+=":"+file.LastWriteTimeUtc.Ticks+":"+(file.Exists ? file.Length : 0);
                if (images.TryGetValue(key,out cached)) return cached;
                var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.CreateOptions=BitmapCreateOptions.IgnoreImageCache; bitmap.DecodePixelWidth=size; bitmap.UriSource=new Uri(path,UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze();
                if (images.Count>=70) images.Remove(images.Keys.First()); images[key]=bitmap; return bitmap;
            } catch { images[key]=null; return null; }
        }
        void LaunchSelected() {
#if ARTWORK_TEST
            ShowNotice("Artwork test only - game launching is disabled.");
#else
            if (launching || visible.Count==0) return;
            if (demo) { ShowNotice("Preview mode · connect your Steam library to launch games."); return; }
            var game=visible[selected];
            launching=true;
            try {
                Save();
                Process.Start(GameLaunch.StartInfo(game));
                if(settings.LaunchHistory==null) settings.LaunchHistory=new Dictionary<string,long>();
                settings.LaunchHistory[game.Id]=DateTimeOffset.UtcNow.ToUnixTimeSeconds(); Save();
                Close();
            } catch (Exception e) { launching=false; ShowNotice("Game could not be opened: "+e.Message); }
#endif
        }
        void Save() { if (demo) return; try { Storage.Write("settings.json",settings); } catch { } }
        void QueueSave() {
            if(demo || saveQueued) return;
            saveQueued=true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => { saveQueued=false; Save(); }));
        }
        void OnKey(object sender, System.Windows.Input.KeyEventArgs e) {
            var key=e.Key;
            int keyDirection=ControllerNavigation.KeyDirection(key,settings.CarouselLayout=="grid",GridColumns);
            PadState pad;
            if(keyDirection!=0 && !preview && Gamepad.Read(out pad) && TryControllerNavigationKey(key,pad)) { e.Handled=true; return; }
            switch (key) {
                case System.Windows.Input.Key.Left: Move(-1); break;
                case System.Windows.Input.Key.Right: Move(1); break;
                case System.Windows.Input.Key.Up: if(settings.CarouselLayout=="grid") MoveGridRow(-1); break;
                case System.Windows.Input.Key.Down: if(settings.CarouselLayout=="grid") MoveGridRow(1); break;
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
            if (!IsActive || preview) { exitButton.Reset(); previousButtons=0; navigation.Reset(); return; }
            PadState pad; bool found=Gamepad.Read(out pad);
            if (!found) { exitButton.Reset(); previousButtons=0; navigation.Reset(); return; }
            ApplyControllerState(pad,Gamepad.Source,(long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency));
        }
        internal void ApplyControllerState(PadState pad,string source,long milliseconds) {
            if(previousInputSource!=source) {
                previousInputSource=source; previousButtons=0;
                exitButton.Reset();
            }
            NavigateController(pad);
            ushort buttons=pad.Buttons;
            ushort pressed=(ushort)(buttons & ~previousButtons);
            previousButtons=buttons;
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
        internal bool TryControllerNavigationKey(System.Windows.Input.Key key,PadState pad) {
            bool fast,grid=settings.CarouselLayout=="grid";
            int keyDirection=ControllerNavigation.KeyDirection(key,grid,GridColumns);
            if(keyDirection==0 || ControllerNavigation.Direction(pad,grid,GridColumns,out fast)!=keyDirection) return false;
            // Desktop controller mappings may send an arrow key before their
            // per-app profile switches. Use the same repeat gate as polling.
            NavigateController(pad); return true;
        }
        internal void NavigateController(PadState pad) {
            bool gridLayout=settings.CarouselLayout=="grid";
            bool fast;
            int direction=ControllerNavigation.Direction(pad,gridLayout,GridColumns,out fast);
            double now=Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency;
            if (navigation.Update(direction,fast,now,settings.RightStickSpeed)) {
                if(gridLayout && Math.Abs(direction)==GridColumns) MoveGridRow(Math.Sign(direction)); else Move(direction);
                // Artwork decoding/layout can take longer on the first move.
                // Begin the repeat delay after that work, never catch up missed ticks.
                navigation.CompleteMove(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency);
            }
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
    public static class ArtworkColours {
        static readonly System.Runtime.CompilerServices.ConditionalWeakTable<BitmapSource,LinearGradientBrush> cache=new System.Runtime.CompilerServices.ConditionalWeakTable<BitmapSource,LinearGradientBrush>();
        public static LinearGradientBrush Background(BitmapSource image) { return cache.GetValue(image,CreateBackground); }
        public static LinearGradientBrush Fill(BitmapSource image,double height,double fittedHeight) {
            var colours=Background(image); Color top=colours.GradientStops[0].Color,bottom=colours.GradientStops[3].Color;
            double edge=Math.Max(0,Math.Min(.5,(height-fittedHeight)/(2*height)));
            var brush=new LinearGradientBrush(new GradientStopCollection {new GradientStop(Darker(top),0),new GradientStop(top,edge),new GradientStop(bottom,1-edge),new GradientStop(Darker(bottom),1)},new Point(.5,0),new Point(.5,1));
            brush.Freeze(); return brush;
        }
        static Color Darker(Color colour) { return Color.FromRgb((byte)(colour.R*.45),(byte)(colour.G*.45),(byte)(colour.B*.45)); }
        static LinearGradientBrush CreateBackground(BitmapSource image) {
            var small=new TransformedBitmap(image,new ScaleTransform(Math.Min(1,64.0/image.PixelWidth),Math.Min(1,96.0/image.PixelHeight)));
            var sample=new FormatConvertedBitmap(small,PixelFormats.Bgra32,null,0);
            int stride=sample.PixelWidth*4;
            var pixels=new byte[stride*sample.PixelHeight]; sample.CopyPixels(pixels,stride,0);
            int rows=Math.Max(1,(sample.PixelHeight+3)/4);
            Color top=EdgeBlend(pixels,stride,0,rows,false),bottom=EdgeBlend(pixels,stride,sample.PixelHeight-rows,rows,true);
            var brush=new LinearGradientBrush(new GradientStopCollection {new GradientStop(top,0),new GradientStop(top,.25),new GradientStop(bottom,.75),new GradientStop(bottom,1)},new Point(.5,0),new Point(.5,1));
            brush.Freeze(); return brush;
        }
        static Color EdgeBlend(byte[] pixels,int stride,int startRow,int rows,bool bottom) {
            var weights=new int[256]; var red=new int[256]; var green=new int[256]; var blue=new int[256];
            int totalWeight=0,lightNeutralWeight=0,neutralRed=0,neutralGreen=0,neutralBlue=0;
            int width=stride/4,sides=Math.Max(1,width*15/100),rim=Math.Max(1,rows/6);
            for(int y=startRow;y<startRow+rows;y++) for(int x=0;x<stride;x+=4) {
                bool edge=bottom ? y>=startRow+rows-rim : y<startRow+rim;
                // Titles tend to occupy the centre of the upper/lower quarter.
                // Use its outer rim and corners instead of counting those letters.
                if(!edge && x/4>=sides && x/4<width-sides) continue;
                int i=y*stride+x,a=pixels[i+3]; if(a<32) continue;
                int r=pixels[i+2],g=pixels[i+1],b=pixels[i],bin=(54*r+183*g+19*b)>>8;
                weights[bin]+=a; red[bin]+=r*a; green[bin]+=g*a; blue[bin]+=b*a;
                totalWeight+=a;
                if(bin>=145 && Math.Max(r,Math.Max(g,b))-Math.Min(r,Math.Min(g,b))<=20) {
                    lightNeutralWeight+=a; neutralRed+=r*a; neutralGreen+=g*a; neutralBlue+=b*a;
                }
            }
            if(totalWeight==0) return Color.FromRgb(13,25,34);
            // A light paper/wall background should not inherit the hue of a
            // corner badge. Preserve it when it makes up most of the edge sample.
            if(lightNeutralWeight>=totalWeight*.5) return Color.FromRgb((byte)(neutralRed/lightNeutralWeight),(byte)(neutralGreen/lightNeutralWeight),(byte)(neutralBlue/lightNeutralWeight));
            // Blend the actual edge colours, trimming only the brightest and
            // darkest 10%. Do not promote a small saturated accent over its background.
            double low=totalWeight*.1,high=totalWeight*.9,cumulative=0,weight=0,rSum=0,gSum=0,bSum=0;
            for(int i=0;i<weights.Length;i++) {
                double take=Math.Max(0,Math.Min(cumulative+weights[i],high)-Math.Max(cumulative,low));
                cumulative+=weights[i];
                if(take<=0 || weights[i]==0) continue;
                double fraction=take/weights[i];
                weight+=take; rSum+=red[i]*fraction; gSum+=green[i]*fraction; bSum+=blue[i]*fraction;
            }
            return Color.FromRgb((byte)Math.Round(rSum/weight),(byte)Math.Round(gSum/weight),(byte)Math.Round(bSum/weight));
        }
    }
    public static class ControllerNavigation {
        public static int KeyDirection(System.Windows.Input.Key key,bool grid,int columns) {
            if(key==System.Windows.Input.Key.Left) return -1;
            if(key==System.Windows.Input.Key.Right) return 1;
            if(grid && key==System.Windows.Input.Key.Up) return -columns;
            if(grid && key==System.Windows.Input.Key.Down) return columns;
            return 0;
        }
        public static int Direction(PadState pad,bool grid,int columns,out bool fast) {
            int right=pad.RX < -16000 ? -1 : pad.RX > 16000 ? 1 : 0;
            int vertical=pad.RY > 16000 ? -1 : pad.RY < -16000 ? 1 : 0;
            fast=right!=0 || (grid && vertical!=0);
            int direction=fast ? right : (pad.Buttons&0x0004)!=0 || pad.LX < -16000 ? -1 : (pad.Buttons&0x0008)!=0 || pad.LX > 16000 ? 1 : 0;
            if(grid && direction==0) direction=columns*(fast ? vertical : (pad.Buttons&0x0001)!=0 || pad.LY>16000 ? -1 : (pad.Buttons&0x0002)!=0 || pad.LY < -16000 ? 1 : 0);
            return direction;
        }
    }
    public sealed class NavigationRepeat {
        int direction;
        bool fast;
        double next,delay;
        public void Reset() { direction=0; fast=false; next=0; delay=0; }
        public bool Update(int value,bool accelerated,double milliseconds,double speed=3) {
            if(value==0) { Reset(); return false; }
            bool changed=value!=direction || accelerated!=fast;
            direction=value; fast=accelerated;
            if(!changed && milliseconds<next) return false;
            if(Double.IsNaN(speed) || Double.IsInfinity(speed) || speed<1 || speed>10) speed=3;
            delay=(changed ? 330.0 : 125.0)/(accelerated ? speed : 1);
            next=milliseconds+delay;
            return true;
        }
        public void CompleteMove(double milliseconds) { next=milliseconds+delay; }
    }
    public sealed class ControllerRouter {
        string owner;
        public static bool Active(PadState pad) { return pad.Buttons!=0 || Math.Abs((int)pad.LX)>16000 || Math.Abs((int)pad.RX)>16000 || Math.Abs((int)pad.LY)>16000 || Math.Abs((int)pad.RY)>16000; }
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
