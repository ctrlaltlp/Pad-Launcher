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

[assembly: System.Reflection.AssemblyVersion("0.6.2")]
[assembly: System.Reflection.AssemblyFileVersion("0.6.2")]
[assembly: System.Reflection.AssemblyInformationalVersion("0.6.2")]
[assembly: System.Reflection.AssemblyTitle("Pad Launcher")]
[assembly: System.Reflection.AssemblyProduct("Pad Launcher")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

namespace PadLauncher {
    static class ThemedMessage {
        public static void Show(string message,string title) {Show(null,message,title);}
        public static void Show(Window owner,string message,string title) {
            var dialog=new Window {
                Title=title,Width=440,SizeToContent=SizeToContent.Height,MaxHeight=600,
                ResizeMode=ResizeMode.NoResize,ShowInTaskbar=false,
                Background=new SolidColorBrush(Color.FromRgb(13,25,34)),
                Foreground=new SolidColorBrush(Color.FromRgb(236,243,242)),
                WindowStartupLocation=owner!=null && owner.IsVisible ? WindowStartupLocation.CenterOwner : WindowStartupLocation.CenterScreen
            };
            if(owner!=null && owner.IsVisible) dialog.Owner=owner;
            using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("SettingsTheme.xaml"))
                dialog.Resources.MergedDictionaries.Add((ResourceDictionary)System.Windows.Markup.XamlReader.Load(stream));
            var panel=new StackPanel {Margin=new Thickness(24)};
            panel.Children.Add(new TextBlock {Text=title,FontSize=20,Foreground=dialog.Foreground,TextWrapping=TextWrapping.Wrap});
            var detail=new TextBlock {Text=message,FontSize=13,Foreground=new SolidColorBrush(Color.FromRgb(155,179,191)),TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,12,0,20)};
            panel.Children.Add(new ScrollViewer {Content=detail,MaxHeight=400,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var close=new Button {Content="Close",IsDefault=true,IsCancel=true,Padding=new Thickness(16,8,16,8),HorizontalAlignment=HorizontalAlignment.Right};
            close.Click+=(s,e) => dialog.Close(); panel.Children.Add(close);
            dialog.Content=panel;
            dialog.ShowDialog();
        }
    }
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
#if EMULATOR_DETECTION_TEST
        const string InstanceName=@"Local\PadLauncher.EmulatorDetectionTest";
        public const string AppName="Pad Launcher - Emulator detection test";
#elif FIRST_START_TEST
        const string InstanceName=@"Local\PadLauncher.FirstStartupTest";
        public const string AppName="Pad Launcher - First startup test";
#elif XBOX_GUIDE_TEST
        const string InstanceName=@"Local\PadLauncher.XboxGuideTest";
        public const string AppName="Pad Launcher - Xbox Guide test";
#elif ARTWORK_TEST
        const string InstanceName=@"Local\PadLauncher.ArtworkTest";
        public const string AppName="Pad Launcher - Artwork test";
#else
        const string InstanceName=@"Local\PadLauncher";
        public const string AppName="Pad Launcher";
#endif
        [STAThread] public static void Main(string[] args) {
#if XBOX_GUIDE_TEST
            if(args.Contains("--probe-guide")) {
                using(var guide=new XboxGuideInput()) guide.Probe();
                return;
            }
#endif
            if(AppUpdates.RunInstaller(args,message => ThemedMessage.Show(message,"Pad Launcher update"))) return;
            string dataPath = Argument(args,"--data");
#if XBOX_GUIDE_TEST
            if(String.IsNullOrEmpty(dataPath)) dataPath=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PadLauncher.XboxGuideTest.Data");
#endif
            if (!String.IsNullOrEmpty(dataPath)) Storage.Root = System.IO.Path.GetFullPath(dataPath);
            else {
#if EMULATOR_DETECTION_TEST
                Storage.Root=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PadLauncher.EmulatorDetectionTest.Data");
                Storage.InitializePortable();
#elif FIRST_START_TEST
                Storage.Root=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PadLauncher.FirstStartupTest.Data");
                Storage.InitializePortable();
#elif ARTWORK_TEST
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
                    Storage.InitializePortable();
                } catch(Exception error) {
                    ThemedMessage.Show("Could not prepare the portable data folder beside PadLauncher.exe. Place the app in a writable folder.\n\n"+error.Message,"Pad Launcher");
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
#if EMULATOR_DETECTION_TEST
            if(args.Contains("--detection-report")) {
                File.WriteAllText(System.IO.Path.Combine(Storage.Root,"emulator-detection-report.json"),Storage.ReadableJson(EmulatorDetection.Scan(CancellationToken.None)));
                return;
            }
#endif
            bool first;
            var showSignal=new EventWaitHandle(false,EventResetMode.AutoReset,InstanceName+".Show");
            var arcOnlySignal=new EventWaitHandle(false,EventResetMode.AutoReset,InstanceName+".ArcOnly");
            var arcSignal=new EventWaitHandle(false,EventResetMode.AutoReset,InstanceName+".Arc");
            var exitSignal=new EventWaitHandle(false,EventResetMode.AutoReset,InstanceName+".Exit");
            instance = new Mutex(true, InstanceName, out first);
            if (!first) {
                foreach (var process in new[]{Process.GetCurrentProcess().ProcessName,"PadLauncher","SteamPadLauncher"}.Distinct().SelectMany(name => Process.GetProcessesByName(name)).GroupBy(p => p.Id).Select(group => group.First())) if (process.Id!=Process.GetCurrentProcess().Id) AllowSetForegroundWindow(process.Id);
                if (args.Contains("--quit")) exitSignal.Set(); else if(args.Contains("--toggle-arc-only")) arcOnlySignal.Set(); else if(args.Contains("--toggle-arc")) arcSignal.Set(); else showSignal.Set();
                return;
            }
            if (args.Contains("--quit")) { instance.ReleaseMutex(); return; }
            var app = new Application();
            bool reportingError=false;
            app.DispatcherUnhandledException += (s,e) => {
                e.Handled=true;
                try { Directory.CreateDirectory(Storage.Root); File.AppendAllText(System.IO.Path.Combine(Storage.Root, "error.log"), DateTime.Now + " " + e.Exception + Environment.NewLine); } catch { }
                if(reportingError) return;
                reportingError=true;
                try {ThemedMessage.Show(e.Exception.Message, "Pad Launcher");} catch { }
                finally {reportingError=false;}
            };
            var window = new Launcher(args.Contains("--demo") || (args.Contains("--render-preview") && !args.Contains("--live-preview")), args.Contains("--render-preview"));
            RegisteredWaitHandle showWait=null,arcWait=null,arcOnlyWait=null,exitWait=null;
            System.Windows.Forms.NotifyIcon tray=null;
            if (!args.Contains("--render-preview")) {
                showWait=ThreadPool.RegisterWaitForSingleObject(showSignal,(s,t) => app.Dispatcher.BeginInvoke(new Action(window.ToggleOverlayFromShortcut)),null,Timeout.Infinite,false);
                arcOnlyWait=ThreadPool.RegisterWaitForSingleObject(arcOnlySignal,(s,t) => app.Dispatcher.BeginInvoke(new Action(window.ToggleArcOnlyFromShortcut)),null,Timeout.Infinite,false);
                arcWait=ThreadPool.RegisterWaitForSingleObject(arcSignal,(s,t) => app.Dispatcher.BeginInvoke(new Action(window.ToggleArcFromShortcut)),null,Timeout.Infinite,false);
                exitWait=ThreadPool.RegisterWaitForSingleObject(exitSignal,(s,t) => app.Dispatcher.BeginInvoke(new Action(window.ExitApp)),null,Timeout.Infinite,false);
                tray=new System.Windows.Forms.NotifyIcon { Icon=System.Drawing.Icon.ExtractAssociatedIcon(System.Reflection.Assembly.GetExecutingAssembly().Location),Text=AppName,Visible=true };
                var menu=new System.Windows.Forms.ContextMenuStrip {
                    Renderer=new TrayMenuRenderer {RoundedEdges=false},
                    BackColor=TrayMenuColors.Surface,ForeColor=System.Drawing.Color.FromArgb(236,243,242),
                    ShowImageMargin=false,Padding=new System.Windows.Forms.Padding(5)
                };
                var toggleLauncher=menu.Items.Add("Show launcher",null,(s,e) => window.ToggleOverlay());
                menu.Opening+=(s,e) => toggleLauncher.Text=window.IsVisible ? "Hide launcher" : "Show launcher";
                menu.Items.Add("Settings",null,(s,e) => window.ShowHelpWindow());
                menu.Items.Add("Exit app",null,(s,e) => window.ExitApp());
                foreach(System.Windows.Forms.ToolStripItem item in menu.Items)
                    item.Padding=new System.Windows.Forms.Padding(10,5,10,5);
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
                string resume=Argument(args,"--resume-update");
                if(!String.IsNullOrEmpty(resume)) window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(() => window.ResumeAfterUpdate(resume)));
                else window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(() => {
                    if(!args.Contains("--toggle-arc-only")) window.StartInitialSetup();
                    if(args.Contains("--toggle-arc-only")) window.ToggleArcOnlyFromShortcut();
                    else if(args.Contains("--toggle-arc")) window.ToggleArcFromShortcut();
                    else if(args.Contains("--toggle-overlay")) window.ShowOverlay();
                }));
#if ARTWORK_TEST || XBOX_GUIDE_TEST
                window.ShowOverlay();
#endif
                app.Run();
            }
            if (showWait!=null) showWait.Unregister(null);
            if (arcOnlyWait!=null) arcOnlyWait.Unregister(null);
            if (arcWait!=null) arcWait.Unregister(null);
            if (exitWait!=null) exitWait.Unregister(null);
            if (tray!=null) { tray.Visible=false; tray.ContextMenuStrip.Dispose(); var icon=tray.Icon; tray.Dispose(); if(icon!=null) icon.Dispose(); }
            if (showSignal!=null) showSignal.Dispose();
            if (arcOnlySignal!=null) arcOnlySignal.Dispose();
            if (arcSignal!=null) arcSignal.Dispose();
            if (exitSignal!=null) exitSignal.Dispose();
            instance.ReleaseMutex();
        }
        static string Argument(string[] args, string key) { int i = Array.IndexOf(args, key); return i >= 0 && i+1 < args.Length ? args[i+1] : null; }
        [DllImport("user32.dll")] static extern bool AllowSetForegroundWindow(int processId);
    }
    sealed class TrayMenuRenderer : System.Windows.Forms.ToolStripProfessionalRenderer {
        public TrayMenuRenderer() : base(new TrayMenuColors()) {}

        protected override void OnRenderMenuItemBackground(System.Windows.Forms.ToolStripItemRenderEventArgs args) {
            var bounds=new System.Drawing.Rectangle(3,1,args.Item.Width-6,args.Item.Height-2);
            using(var fill=new System.Drawing.SolidBrush(args.Item.Selected && args.Item.Enabled ? TrayMenuColors.Hover : TrayMenuColors.Surface))
                args.Graphics.FillRectangle(fill,bounds);
        }
    }

    sealed class TrayMenuColors : System.Windows.Forms.ProfessionalColorTable {
        internal static readonly System.Drawing.Color Surface=System.Drawing.Color.FromArgb(21,36,46);
        internal static readonly System.Drawing.Color Hover=System.Drawing.Color.FromArgb(43,85,81);
        static readonly System.Drawing.Color Border=System.Drawing.Color.FromArgb(58,82,96);
        public override System.Drawing.Color ToolStripDropDownBackground {get {return Surface;}}
        public override System.Drawing.Color MenuBorder {get {return Border;}}
        public override System.Drawing.Color MenuItemBorder {get {return Hover;}}
        public override System.Drawing.Color MenuItemSelected {get {return Hover;}}
        public override System.Drawing.Color MenuItemSelectedGradientBegin {get {return Hover;}}
        public override System.Drawing.Color MenuItemSelectedGradientEnd {get {return Hover;}}
    }

    public partial class Launcher : Window {
        readonly Brush white = new SolidColorBrush(Color.FromRgb(236,243,242));
        readonly Brush muted = new SolidColorBrush(Color.FromRgb(141,161,168));
        readonly Brush mint = new SolidColorBrush(Color.FromRgb(163,240,205));
        readonly Grid scene = new Grid { Width = 1600, Height = 900, Background = Brushes.Transparent };
        readonly Canvas carousel = new Canvas { Name="GameCarousel",ClipToBounds = true };
        readonly Grid carouselHost=new Grid {Name="CarouselViewport"};
        readonly TextBlock title = new TextBlock();
        readonly TextBlock launchCountSubtitle = new TextBlock();
        readonly TextBlock collectionText = new TextBlock();
        readonly Border notice = new Border();
        readonly TextBlock noticeText = new TextBlock();
        readonly Dictionary<string,BitmapSource> images = new Dictionary<string,BitmapSource>();
        readonly LinkedList<string> imageRecency=new LinkedList<string>();
        const long ImageCacheLimit=32L*1024*1024;
        long imageCacheBytes;
        readonly Dictionary<string,string> missingGameCache=new Dictionary<string,string>();
        DateTime missingGameCacheUntil;
        readonly Dictionary<string,FrameworkElement> cards = new Dictionary<string,FrameworkElement>();
        readonly List<FrameworkElement> guideCards = new List<FrameworkElement>();
        StackPanel guideActions;
        readonly DispatcherTimer controllerTimer = new DispatcherTimer(DispatcherPriority.Input);
        readonly DispatcherTimer noticeTimer = new DispatcherTimer();
        readonly DispatcherTimer navigationSaveTimer=new DispatcherTimer(DispatcherPriority.Background);
        bool temporaryLabels;
        Settings settings;
        Library library = new Library();
        List<Game> visible = new List<Game>();
        List<Collection> filters = new List<Collection>();
        int selected, collectionIndex;
        int gridScrollPage=-1;
        double gridScrollOffset;
        double gridViewportHeight=490;
        bool scanning, launching, demo, preview;
        bool pendingLibraryRefresh, pendingLibraryRefreshFeedback, libraryCacheNeedsWrite;
        ushort previousButtons;
        string previousInputSource;
        readonly HoldButton exitButton = new HoldButton();
        readonly NavigationRepeat navigation = new NavigationRepeat();
        bool exiting;
        bool saveQueued;
        Window helpWindow;
        TabControl helpTabs;
        WrapPanel missingArtworkList;
        TextBlock missingArtworkCount;
        public bool Idle { get { return !IsVisible && !controllerTimer.IsEnabled; } }

        public Launcher(bool demoMode, bool renderPreview) {
            demo = demoMode; preview = renderPreview;
            freshProfile=!File.Exists(System.IO.Path.Combine(Storage.Root,"settings.json")) && !File.Exists(System.IO.Path.Combine(Storage.Root,"library.json"));
            settings = Storage.Read<Settings>("settings.json") ?? new Settings();
#if XBOX_GUIDE_TEST
            settings.SetupWizardDeclined=true;
            settings.OpenOverlayWithHome=true;
            settings.AutoUpdate=false;
#endif
            if(settings.StickSpeedsLocked) settings.RightStickSpeed=settings.LeftStickSpeed;
            if(IsSpecialJumpList(settings.DefaultCollectionId)) settings.DefaultCollectionId="remember";
            ControlBindings.UpgradeSearchDefaults(settings);
            ArtworkService.UpgradeArtworkPreferences(settings);
            var beforeShortcutCleanup=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            if(SteamShortcutIdentity.Normalize(settings)) {
                Storage.Write("settings.before-shortcut-unification.json",beforeShortcutCleanup);
                Storage.Write("settings.json",settings);
            }
#if FOCUS_DIAGNOSTICS
            InitializeFocusDiagnostics();
#endif
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
            listContent.RenderTransform=listTranslation;
            BuildScene();
            ApplyCarouselScale();
            SizeChanged+=(s,e) => { if(ActiveLayout=="grid") Render(false); };
            Closed+=(sender,args) => xboxGuideInput.Dispose();
            Deactivated+=(s,e) => controllerWaitForForegroundRelease=true;
            PreviewKeyDown += OnKey;
            PreviewTextInput+=(s,e) => HandleSearchText(e);
            Closed += (s,e) => { updateCancellation.Cancel(); controllerTimer.Stop(); noticeTimer.Stop(); navigationSaveTimer.Stop(); Gamepad.StopNative(); Save(); };
            Closing += (s,e) => {
                CloseRadial();
                CancelArcReveal();
                if(gameSearchWindow!=null) gameSearchWindow.Close();
                if (!exiting) {
                    e.Cancel=true; controllerTimer.Stop(); noticeTimer.Stop(); previousInputSource=null;
                    notice.Visibility=Visibility.Collapsed; exitButton.Reset(); previousButtons=0; navigation.Reset();
                    StopListTransition(); launching=false; Hide(); RestoreTemporaryLabels(); UpdateTrayHomeListener();
                    QueueSave();
                }
            };
            controllerTimer.Interval = TimeSpan.FromMilliseconds(16);
            controllerTimer.Tick += PollController;
#if NAVIGATION_DIAGNOSTICS
            CompositionTarget.Rendering+=TraceControllerFrame;
            Closed+=(s,e) => CompositionTarget.Rendering-=TraceControllerFrame;
#endif
            noticeTimer.Interval = TimeSpan.FromSeconds(5);
            navigationSaveTimer.Interval=TimeSpan.FromMilliseconds(350);
            navigationSaveTimer.Tick+=(s,e) => {navigationSaveTimer.Stop(); Save();};
            noticeTimer.Tick += (s,e) => { notice.Visibility = Visibility.Collapsed; if(arcOnlyNotice!=null) arcOnlyNotice.Text=""; noticeTimer.Stop(); RestoreTemporaryLabels(); };
            if (demo) { library = Demo(); settings.SelectedId="4"; ApplyLibrary(true); }
            else {
                var cached = Storage.Read<Library>("library.json");
                if (cached != null && (String.IsNullOrEmpty(settings.SteamPath) || String.Equals(cached.SteamPath, settings.SteamPath, StringComparison.OrdinalIgnoreCase)) && (String.IsNullOrEmpty(settings.UserId) || cached.UserId == settings.UserId)) {
                    library=cached;
                    var previousArtwork=library.Games.Where(game => game.Source=="custom").GroupBy(game => game.Id).ToDictionary(group => group.Key,group => group.First());
                    CustomGameLibrary.Apply(library,settings);
                    library.Games=library.Games.OrderBy(game => game.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                    foreach(var game in library.Games.Where(entry => entry.Source=="custom")) {Game previous; previousArtwork.TryGetValue(game.Id,out previous); ArtworkService.RestoreStartupArtwork(game,previous,settings);}
                    ApplyLibrary(true);
                }
                else { SetFilters(); Render(false); }
            }
        }
        public void PrepareTray() {
            Task.Run(() => AppUpdates.CleanOldDownloads());
            controllerTimer.Stop();
            UpdateTrayHomeListener();
            var content=Content as FrameworkElement;
            if(content!=null) { content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout(); }
            if(!preview && !TestEnvironment.EmulatorDetection && !NeedsInitialSetup) Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => RefreshLibrary()));
            Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(StartAutomaticUpdates));
        }
        internal void ToggleOverlayFromShortcut() {
            if(arcOnly) {CloseRadial(); ShowOverlay(); return;}
            if(exiting) return;
            if(!IsVisible || OverlayHasForeground()) {ToggleOverlay(); return;}
            FocusVisibleOverlayFromShortcut();
        }
        void FocusVisibleOverlayFromShortcut() {
            if(gameSearchWindow!=null && gameSearchWindow.IsVisible) {
                var search=gameSearchWindow;
                search.Activate();
                SetForegroundWindow(new System.Windows.Interop.WindowInteropHelper(search).Handle);
                return;
            }
            FocusOverlayInput();
        }
        public void ToggleOverlay() {
            if(exiting) return;
            if(IsVisible) Close(); else ShowOverlay();
        }
        public void ShowOverlay() {ShowOverlay(false);}
        void ShowOverlay(bool keepPreparedList) {
            if (exiting) return;
            if (IsVisible) { FocusOverlayInput(); return; }
            launching=false; controllerWaitForForegroundRelease=false; exitButton.Reset(); previousButtons=0; navigation.Reset();
            suppressSearchRelease=false; bindingLeftTriggerDown=bindingRightTriggerDown=false; testLayoutTriggerDown=false;
            if(!keepPreparedList) ApplyLibrary(true);
            var monitor=preview ? null : ResolveMonitor(settings.OpenMonitor,System.Windows.Forms.Screen.AllScreens,System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position));
            if(monitor!=null) PlaceOnMonitor(monitor);
            Show();
            if(monitor!=null) PlaceOnMonitor(monitor);
            if (!preview) { Gamepad.StartNative(new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle()); controllerTimer.Interval=TimeSpan.FromMilliseconds(16); controllerTimer.Start(); }
            FocusOverlayInput();
            // Paint retained artwork first, then reread Steam's saved changes.
            if (!preview && !keepPreparedList) Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => RefreshLibrary()));
        }
        [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr window);
        [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
        void FocusOverlayInput() {
            IntPtr handle=new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
            IntPtr previousForeground=GetForegroundWindow();
            Activate(); SetForegroundWindow(handle); Focus();
            // Tray activation can finish after Show returns. Retry once before
            // background refresh, without reclaiming focus from another app.
            Dispatcher.BeginInvoke(DispatcherPriority.Input,new Action(() => {
                if(exiting || !IsVisible || GameSearchOpen || OwnedWindows.Cast<Window>().Any(window => window.IsVisible)) return;
                IntPtr foreground=GetForegroundWindow();
                if(foreground!=handle && foreground!=previousForeground) return;
                if(foreground!=handle) {Activate(); SetForegroundWindow(handle);}
                Focus();
            }));
        }
        static bool IsSpecialJumpList(string id) {
            return id=="favourites" || id=="recent" || id=="most-played";
        }
        List<KeyValuePair<string,string>> DefaultListChoices() {
            var choices=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("remember","Remember last list")};
            foreach(var list in CollectionOptions()) {
                bool enabled=CollectionEnabled(list.Id);
                if(enabled && !IsSpecialJumpList(list.Id))
                    choices.Add(new KeyValuePair<string,string>(list.Id,ListLabel(list)));
            }
            return choices;
        }
        List<KeyValuePair<string,string>> JumpListChoices() {
            return CollectionOptions().Where(list => CollectionEnabled(list.Id) || IsSpecialJumpList(list.Id))
                .Select(list => new KeyValuePair<string,string>(list.Id,ListLabel(list)+(CollectionEnabled(list.Id) ? "" : " (not in overlay)"))).ToList();
        }
        public void SetDefaultCollection(string id) {
            if(id!="remember" && !CollectionOptions().Any(c => c.Id==id && CollectionEnabled(id) && !IsSpecialJumpList(id))) throw new ArgumentException("Choose an available list.");
            string previous=settings.DefaultCollectionId;
            try { settings.DefaultCollectionId=id; Storage.Write("settings.json",settings); }
            catch { settings.DefaultCollectionId=previous; throw; }
        }
        List<Collection> CollectionOptions() {
            var options=EmulatorLibrary.DefaultListOrder(CombinedCollections.Options(library,settings),library,settings);
            if(settings.OverlayListOrder==null) return options;
            var ranks=settings.OverlayListOrder.Distinct().Select((id,index) => new {id,index}).ToDictionary(item => item.id,item => item.index);
            return options.OrderBy(list => ranks.ContainsKey(list.Id) ? ranks[list.Id] : Int32.MaxValue).ToList();
        }
        string ListLabel(Collection list) {return CombinedCollections.Label(list,library);}
        bool CollectionEnabled(string id) { return settings.EnabledCollectionIds==null || settings.EnabledCollectionIds.Contains(id); }
        public void SetAvailableCollections(IEnumerable<string> ids) {
            var chosen=ids==null ? null : ids.Distinct().ToList();
            var options=CollectionOptions();
            if(chosen!=null && (chosen.Count==0 || chosen.Any(id => !options.Any(c => c.Id==id)))) throw new ArgumentException("Select at least one available list.");
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
            double previous=settings.CarouselScale,previousRetro=settings.RetroPanelScale;
            try { settings.CarouselScale=scale; if(settings.PanelSizesLocked) settings.RetroPanelScale=scale; Storage.Write("settings.json",settings); }
            catch { settings.CarouselScale=previous; settings.RetroPanelScale=previousRetro; throw; }
            ApplyCarouselScale();
            Render(false);
        }
        string DefaultCarouselLayout {
            get {
                string layout=settings.CarouselLayout=="grid" ? settings.PreferredCarouselLayout : settings.CarouselLayout;
                return layout=="rectangular" ? "rectangular" : "angled";
            }
        }
        public void SetCarouselLayout(string layout) {
            StopListTransition();
            if(layout!="angled" && layout!="rectangular" && layout!="grid") throw new ArgumentException("Choose an angled, rectangular or grid layout.");
            string previous=settings.CarouselLayout;
            string previousPreference=settings.PreferredCarouselLayout;
            try {
                settings.PreferredCarouselLayout=layout=="grid" ? DefaultCarouselLayout : layout;
                settings.CarouselLayout=layout; Storage.Write("settings.json",settings);
            }
            catch { settings.CarouselLayout=previous; settings.PreferredCarouselLayout=previousPreference; throw; }
            cards.Clear(); carousel.Children.Clear(); Render(false);
        }
        public void SetAngledPanelSizing(string sizing) {
            if(sizing!="fixed" && sizing!="fit") throw new ArgumentException("Choose Fixed or Fit artwork.");
            string previous=settings.AngledPanelSizing;
            try { settings.AngledPanelSizing=sizing; Storage.Write("settings.json",settings); }
            catch { settings.AngledPanelSizing=previous; throw; }
            if(ActiveLayout!="grid" && ActiveLayout!="rectangular") { cards.Clear(); carousel.Children.Clear(); Render(false); }
        }
        ListLayout activeListLayout;
        ListLayout ActiveListLayout {get {return activeListLayout;}}
        readonly Dictionary<string,string> testLayoutOverrides=new Dictionary<string,string>();
        readonly Dictionary<string,string> testPreviousCarouselLayouts=new Dictionary<string,string>();
        bool testLayoutTriggerDown;
        string ActiveLayout { get {string value; return filters.Count>collectionIndex && testLayoutOverrides.TryGetValue(filters[collectionIndex].Id,out value) ? value : ActiveListLayout==null ? settings.CarouselLayout : ActiveListLayout.Layout;}}
        internal bool HandleTestLayoutTrigger(PadState pad) {
            bool previous=testLayoutTriggerDown;
            if(pad.RT<80) testLayoutTriggerDown=false; else if(pad.RT>=160) testLayoutTriggerDown=true;
            if(previous || !testLayoutTriggerDown || launching || filters.Count==0) return false;
            string id=filters[collectionIndex].Id,layout=ActiveLayout;
            if(layout=="grid") {
                if(!testPreviousCarouselLayouts.TryGetValue(id,out layout) && (settings.PreviousCarouselLayouts==null || !settings.PreviousCarouselLayouts.TryGetValue(id,out layout))) layout=DefaultCarouselLayout;
            } else {testPreviousCarouselLayouts[id]=layout; layout="grid";}
            testLayoutOverrides[id]=layout;
            if(settings.RememberControlLayoutChanges) {
                var profile=ActiveListLayout==null ? new ListLayout {PanelScale=settings.CarouselScale,RetroPanelScale=settings.RetroPanelScale,PanelSizesLocked=settings.PanelSizesLocked,ShowPanelOutlines=settings.ShowPanelOutlines,Columns=settings.GridColumns,Rows=settings.GridRows} : Storage.Json().Deserialize<ListLayout>(Storage.Json().Serialize(ActiveListLayout));
                profile.Layout=layout;
                if(settings.ListLayouts==null) settings.ListLayouts=new Dictionary<string,ListLayout>();
                settings.ListLayouts[id]=profile; activeListLayout=profile;
                string previousCarousel;
                if(testPreviousCarouselLayouts.TryGetValue(id,out previousCarousel)) {
                    if(settings.PreviousCarouselLayouts==null) settings.PreviousCarouselLayouts=new Dictionary<string,string>();
                    settings.PreviousCarouselLayouts[id]=previousCarousel;
                }
                testLayoutOverrides.Remove(id); QueueSave();
            }
            StopListTransition(); navigation.Reset(); exitButton.Reset(); gridScrollPage=-1;
            cards.Clear(); carousel.Children.Clear(); ApplyCarouselScale(); Render(false); return true;
        }
        bool ActivePanelOutlines {get {var layout=ActiveListLayout; return layout!=null && layout.ShowPanelOutlines.HasValue ? layout.ShowPanelOutlines.Value : settings.ShowPanelOutlines;}}
        double ActivePanelScale { get {return ActiveListLayout==null ? settings.CarouselScale : ActiveListLayout.PanelScale;}}
        string ResolveArtworkSizing(string listId,ListLayout profile,IList<Game> games) {
            string sizing=profile==null ? null : profile.AngledSizing;
            if(sizing=="fit" || sizing=="fixed") return sizing;
            string source=ArtworkService.ListDefaultSource(settings,listId);
            if(ArtworkService.PrimaryArtworkSource(source)=="libretro") return "fit";
            if(source=="auto" && games.Count>0 && games.All(game =>
                ArtworkService.PrimaryArtworkSource(ArtworkService.GameListSource(settings,game,listId,source))=="libretro")) return "fit";
            return settings.AngledPanelSizing;
        }
        string ActiveAngledSizing {
            get {
                if(GameSearchOpen) return "fit";
                string id=collectionIndex>=0 && collectionIndex<filters.Count ? filters[collectionIndex].Id : null;
                return ResolveArtworkSizing(id,ActiveListLayout,visible);
            }
        }
        int GridColumns { get {int value=ActiveListLayout==null ? settings.GridColumns : ActiveListLayout.Columns; return value>=2 && value<=10 ? value : 5;} }
        int GridRows { get {int value=ActiveListLayout==null ? settings.GridRows : ActiveListLayout.Rows; return value>=1 && value<=5 ? value : 3;} }
        public void SetGridRows(int rows) {
            if(rows<1 || rows>5) throw new ArgumentOutOfRangeException("rows","Choose between 1 and 5 rows.");
            int previous=settings.GridRows;
            try { settings.GridRows=rows; Storage.Write("settings.json",settings); }
            catch { settings.GridRows=previous; throw; }
            if(ActiveLayout=="grid") { cards.Clear(); carousel.Children.Clear(); Render(false); }
        }
        public void SetGridColumns(int columns) {
            if(columns<2 || columns>10) throw new ArgumentOutOfRangeException("columns","Choose between 2 and 10 columns.");
            int previous=settings.GridColumns;
            try { settings.GridColumns=columns; Storage.Write("settings.json",settings); }
            catch { settings.GridColumns=previous; throw; }
            if(ActiveLayout=="grid") { cards.Clear(); carousel.Children.Clear(); Render(false); }
        }
        void ApplyCarouselScale() {
            double scale=ActivePanelScale;
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<0.6 || scale>1.4) scale=1;
            carousel.RenderTransformOrigin=new Point(.5,.5);
            carousel.RenderTransform=new ScaleTransform(scale,scale);
            ConfigureViewport();
        }
        void ConfigureViewport() {
            double scale=ActivePanelScale;
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<.6 || scale>1.4) scale=1;
            if(ActiveLayout!="grid") {
                scene.Height=900;
                carouselHost.Height=Double.NaN; carouselHost.VerticalAlignment=VerticalAlignment.Stretch; carouselHost.Margin=new Thickness(0,205,0,195); carouselHost.ClipToBounds=false;
                carousel.Height=Double.NaN; carousel.Width=Double.NaN; carousel.VerticalAlignment=VerticalAlignment.Stretch;
                collectionText.Margin=new Thickness(0,Math.Max(40,130+(1-scale)*250),0,0);
                title.Margin=new Thickness(0,726+(scale-1)*240,0,0);
                gridViewportHeight=490; return;
            }
            double windowWidth=ActualWidth>0 ? ActualWidth : Width,windowHeight=ActualHeight>0 ? ActualHeight : Height;
            if(windowWidth>0 && windowHeight>0) scene.Height=1600*windowHeight/windowWidth;
            double top=settings.ShowCollectionName || GameSearchOpen ? 56 : 16,bottom=GameSearchOpen ? 104 : settings.ShowGameTitle || temporaryLabels ? 72 : 16;
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
        }
        public void ExitApp() { StopListTransition(); exiting=true; foreach(var job in artworkRefreshJobs.Values) job.Cancel(); if(listArtworkCancellation!=null) listArtworkCancellation.Cancel(); if(helpWindow!=null) helpWindow.Close(); Close(); }
        public void ShowHelpWindow() {
            if(helpWindow!=null) { if(helpWindow.WindowState==WindowState.Minimized) helpWindow.WindowState=WindowState.Normal; helpWindow.Activate(); return; }
            helpWindow=CreateHelpWindow();
            helpWindow.Closed+=(s,e) => { helpWindow=null; helpTabs=null; missingArtworkList=null; missingArtworkCount=null; };
            helpWindow.Show();
        }
        public Window HelpWindow { get { return helpWindow; } }
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
            if(ActiveLayout=="grid") Render(false);
        }
        string BrowseGuide(bool stick) {
            return (stick ? "Browse games at "+settings.LeftStickSpeed.ToString("0.#")+"× speed" : "Browse games")+" · Up/down switches lists outside Grid";
        }
        public void SetShowGameTitle(bool show) {
            bool previous=settings.ShowGameTitle;
            try { settings.ShowGameTitle=show; Storage.Write("settings.json",settings); }
            catch { settings.ShowGameTitle=previous; throw; }
            title.Visibility=show ? Visibility.Visible : Visibility.Collapsed;
            if(ActiveLayout=="grid") Render(false);
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
        void UpdateMissingArtworkList(TextBlock count,WrapPanel list,Window dialog) {
            list.Children.Clear();
            var missing=library.Games.Where(g => String.IsNullOrEmpty(g.Art) || !File.Exists(g.Art)).OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            count.Text=missing.Count==0 ? "Every game has artwork." : missing.Count+" game"+(missing.Count==1 ? "" : "s")+" need"+(missing.Count==1 ? "s" : "")+" artwork.";
            foreach(var game in missing) {
                var picked=game;
                var button=new Button {Content=picked.EmulatorId=="retroarch" ? picked.Name+" · "+GameSourceLabel(picked) : picked.Name,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,8),ToolTip="Choose artwork for "+picked.Name};
                button.Click+=(s,e) => ShowPreferredArtworkPicker(dialog,picked);
                list.Children.Add(button);
            }
        }
        void RefreshMissingArtworkList() {
            if(helpWindow==null) return;
            var list=missingArtworkList;
            var count=missingArtworkCount;
            if(list==null || count==null) return;
            UpdateMissingArtworkList(count,list,helpWindow);
        }
        public Window CreateHelpWindow() {
            var dialog=new Window {
                Title="Pad Launcher · Settings",Width=700,Height=920,MinWidth=700,MinHeight=500,
                WindowStartupLocation=WindowStartupLocation.CenterScreen,ResizeMode=ResizeMode.CanResize,
                Background=new SolidColorBrush(Color.FromRgb(13,25,34)),Foreground=white,FontFamily=FontFamily,
                ShowInTaskbar=true,Topmost=true,Icon=Icon
            };
            ApplySettingsTheme(dialog);
            SettingsWindowSizing.Attach(dialog);
            dialog.Closed+=(s,e) => CleanupSettingsContent(dialog);
            var layout=new StackPanel { Margin=new Thickness(28) };
            var listsLayout=new StackPanel {Margin=new Thickness(28)};
            var listsScroll=new ScrollViewer {Content=listsLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var helpLayout=new StackPanel {Margin=new Thickness(28)};
            var systemLayout=new StackPanel {Margin=new Thickness(28)};
            var artworkLayout=new StackPanel {Name="ArtworkSettingsPanel",Margin=new Thickness(28)};
            var artworkScroll=new ScrollViewer {Content=artworkLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var integrationsLayout=new StackPanel {Margin=new Thickness(28)};
            var integrationsScroll=new ScrollViewer {Content=integrationsLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var emulatorLayout=new StackPanel {Margin=new Thickness(28)};
            var emulatorScroll=new ScrollViewer {Content=emulatorLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var customLayout=new StackPanel {Margin=new Thickness(28)};
            var customScroll=new ScrollViewer {Content=customLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var systemScroll=new ScrollViewer {Content=systemLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var helpScroll=new ScrollViewer {Content=helpLayout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var settingsScroll=new ScrollViewer {Content=layout,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            var tabs=new TabControl {Name="HelpSettingsTabs",Background=dialog.Background,BorderThickness=new Thickness(0),Foreground=white,Margin=new Thickness(12,12,12,0)};
            tabs.ItemContainerStyle=(Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='{x:Type TabItem}'>
                <Setter Property='Background' Value='#22333F'/><Setter Property='Foreground' Value='#ECF3F2'/><Setter Property='FontSize' Value='14'/>
                <Setter Property='Template'><Setter.Value><ControlTemplate TargetType='{x:Type TabItem}'>
                    <Border x:Name='TabBorder' Background='{TemplateBinding Background}' CornerRadius='6,6,0,0' Padding='7,11' Margin='0,0,4,0'><ContentPresenter ContentSource='Header' HorizontalAlignment='Center'/></Border>
                    <ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='TabBorder' Property='Background' Value='#A3F0CD'/><Setter Property='Foreground' Value='#0D1922'/></Trigger></ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value></Setter>
            </Style>");
            tabs.Items.Add(new TabItem {Header="Overlay",Content=settingsScroll});
            var arcLayout=new StackPanel {Margin=new Thickness(28)};
            var arcHeading=Label("Arc",26,white); arcHeading.FontWeight=FontWeights.SemiBold; arcLayout.Children.Add(arcHeading);
            arcLayout.Children.Add(CreateArcSideSettings(dialog));
            arcLayout.Children.Add(CreateRadialSettings(dialog));
            tabs.Items.Add(new TabItem {Header="Arc",Content=new ScrollViewer {Content=arcLayout,
                VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}});
            tabs.Items.Add(new TabItem {Header="Lists",Content=listsScroll});
            tabs.Items.Add(new TabItem {Header="Launchers",Content=integrationsScroll});
            tabs.Items.Add(new TabItem {Header="Custom games",Content=customScroll});
            tabs.Items.Add(new TabItem {Header="Emulators",Content=emulatorScroll});
            tabs.Items.Add(new TabItem {Header="Controls",Content=helpScroll});
            tabs.Items.Add(new TabItem {Header="Artwork",Content=artworkScroll});
            tabs.Items.Add(new TabItem {Header="System",Content=systemScroll});
            tabs.SelectedIndex=0;
            helpTabs=tabs;
            Action rebuild=null;
            rebuild=() => {
            CleanupSettingsContent(dialog);
            artworkLayout.Children.Clear(); var artworkHeading=Label("Artwork",26,white); artworkHeading.FontWeight=FontWeights.SemiBold; artworkLayout.Children.Add(artworkHeading);
            layout.Children.Clear(); helpLayout.Children.Clear(); systemLayout.Children.Clear(); integrationsLayout.Children.Clear(); listsLayout.Children.Clear();
            customLayout.Children.Clear(); customLayout.Children.Add(CreateCustomGamesPanel(dialog,rebuild));
            emulatorLayout.Children.Clear(); emulatorLayout.Children.Add(CreateEmulatorsPanel(dialog,rebuild));
            var systemHeading=Label("System",26,white); systemHeading.FontWeight=FontWeights.SemiBold; systemLayout.Children.Add(systemHeading);
            var integrationsHeading=Label("Launchers",26,white); integrationsHeading.FontWeight=FontWeights.SemiBold; integrationsLayout.Children.Add(integrationsHeading);
            var listsHeading=Label("Lists",26,white); listsHeading.FontWeight=FontWeights.SemiBold; listsLayout.Children.Add(listsHeading);
            var names=Prompts.Buttons(settings);
            var heading=Label("Overlay",26,white); heading.FontWeight=FontWeights.SemiBold; layout.Children.Add(heading);
            var helpHeading=Label("Controls",26,white); helpHeading.FontWeight=FontWeights.SemiBold; helpHeading.Margin=new Thickness(0,0,0,22); helpLayout.Children.Add(helpHeading);
            var openWithHome=new CheckBox {Content="Open or refocus overlay with PS / Home / Guide",IsChecked=settings.OpenOverlayWithHome,Margin=new Thickness(0,8,0,4)};
            openWithHome.Click+=(sender,args) => {
                bool previous=settings.OpenOverlayWithHome;
                try {settings.OpenOverlayWithHome=openWithHome.IsChecked==true; Storage.Write("settings.json",settings); UpdateTrayHomeListener();}
                catch(Exception error) {settings.OpenOverlayWithHome=previous; openWithHome.IsChecked=previous; ThemedMessage.Show(dialog,error.Message,"Controls");}
            };
            helpLayout.Children.Add(openWithHome);
            var homeNote=Label("Tap PS on a natively connected DualShock 4 or DualSense, Home on a Switch Pro Controller, or Guide on an Xbox controller to open the overlay or request focus if it is already open. Xbox Guide requires Microsoft GameInput v4; regular Xbox controls work without it. Refocus may not work properly in all systems/setups.",12,muted);
                        homeNote.TextWrapping=TextWrapping.Wrap; helpLayout.Children.Add(homeNote);
#if XBOX_GUIDE_TEST
            helpLayout.Children.Add(Label("Xbox Guide test: the same checkbox enables GameInput Guide detection from the tray.",12,muted));
#endif
            var changeControls=new Button {Name="ChangeControls",Content="Show/Change controls",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,17,0,1)};
            changeControls.Click+=(s,e) => ShowControlSettings(dialog,rebuild); helpLayout.Children.Add(changeControls);
            var lockStickSpeeds=new CheckBox {
                Name="LockStickSpeedsSetting",Content="Lock stick speeds together",IsChecked=settings.StickSpeedsLocked,
                Margin=new Thickness(0,16,0,0)
            };
            helpLayout.Children.Add(lockStickSpeeds);
            var leftSpeedCaption=Label("Left stick speed",14,mint); leftSpeedCaption.Margin=new Thickness(0,16,0,8); helpLayout.Children.Add(leftSpeedCaption);
            var leftSpeedValue=Label(settings.LeftStickSpeed.ToString("0.#")+"×",13,white); helpLayout.Children.Add(leftSpeedValue);
            var leftSpeedPicker=new Slider {Name="LeftStickSpeedSetting",Minimum=1,Maximum=10,Value=settings.LeftStickSpeed,TickFrequency=.5,IsSnapToTickEnabled=true,SmallChange=.5,LargeChange=1,Margin=new Thickness(0,4,0,10)};
            helpLayout.Children.Add(leftSpeedPicker);
            var speedCaption=Label("Right stick speed",14,mint); speedCaption.Margin=new Thickness(0,16,0,8); helpLayout.Children.Add(speedCaption);
            var speedValue=Label(settings.RightStickSpeed.ToString("0.#")+"×",13,white); helpLayout.Children.Add(speedValue);
            var speedPicker=new Slider {Name="RightStickSpeedSetting",Minimum=1,Maximum=10,Value=settings.RightStickSpeed,TickFrequency=.5,IsSnapToTickEnabled=true,SmallChange=.5,LargeChange=1,Margin=new Thickness(0,4,0,10)};
            helpLayout.Children.Add(speedPicker);
            bool syncingStickSpeeds=false;
            Action<Slider,Slider> updateStickSpeeds=(changed,other) => {
                if(syncingStickSpeeds) return;
                if(settings.StickSpeedsLocked) {
                    syncingStickSpeeds=true;
                    other.Value=changed.Value;
                    syncingStickSpeeds=false;
                }
                settings.LeftStickSpeed=leftSpeedPicker.Value;
                settings.RightStickSpeed=speedPicker.Value;
                leftSpeedValue.Text=settings.LeftStickSpeed.ToString("0.#")+"×";
                speedValue.Text=settings.RightStickSpeed.ToString("0.#")+"×";
                navigation.Reset();
                QueueSave();
            };
            leftSpeedPicker.ValueChanged+=(s,e) => updateStickSpeeds(leftSpeedPicker,speedPicker);
            speedPicker.ValueChanged+=(s,e) => updateStickSpeeds(speedPicker,leftSpeedPicker);
            lockStickSpeeds.Click+=(s,e) => {
                settings.StickSpeedsLocked=lockStickSpeeds.IsChecked==true;
                updateStickSpeeds(leftSpeedPicker,speedPicker);
            };
            var titleToggle=new CheckBox {Content="Show game title",IsChecked=settings.ShowGameTitle,FontSize=14,Foreground=white,Margin=new Thickness(0,18,0,12)};
            titleToggle.Click+=(s,e) => {
                try { SetShowGameTitle(titleToggle.IsChecked==true); }
                catch(Exception error) { titleToggle.IsChecked=settings.ShowGameTitle; ThemedMessage.Show(dialog,"Could not save title preference: "+error.Message,"Settings"); }
            };
            layout.Children.Add(titleToggle);
            var outlinesToggle=new CheckBox {Name="ShowPanelOutlinesSetting",Content="Show panel outlines",IsChecked=settings.ShowPanelOutlines,FontSize=14,Foreground=white,Margin=new Thickness(0,0,0,12)};
            outlinesToggle.Click+=(s,e) => {settings.ShowPanelOutlines=outlinesToggle.IsChecked==true; QueueSave(); cards.Clear(); carousel.Children.Clear(); Render(false);}; layout.Children.Add(outlinesToggle);

            var skipEmpty=new CheckBox {Content="Skip empty lists in overlay",IsChecked=settings.SkipEmptyLists,FontSize=14,Foreground=white,Margin=new Thickness(0,0,0,12)};
            skipEmpty.Click+=(s,e) => {
                bool previous=settings.SkipEmptyLists;
                try {
                    settings.SkipEmptyLists=skipEmpty.IsChecked==true;
                    Storage.Write("settings.json",settings);
                    ApplyLibrary();
                } catch(Exception error) {
                    settings.SkipEmptyLists=previous; skipEmpty.IsChecked=previous;
                    ThemedMessage.Show(dialog,error.Message,"Lists");
                }
            };
            var collectionToggle=new CheckBox {Name="ShowListNameSetting",Content="Show list name",IsChecked=settings.ShowCollectionName,FontSize=14,Foreground=white,Margin=new Thickness(0,0,0,12)};
            collectionToggle.Click+=(s,e) => {
                try { SetShowCollectionName(collectionToggle.IsChecked==true); }
                catch(Exception error) { collectionToggle.IsChecked=settings.ShowCollectionName; ThemedMessage.Show(dialog,"Could not save list name preference: "+error.Message,"Settings"); }
            };
            layout.Children.Add(collectionToggle);
            var listCountToggle=new CheckBox {Name="ShowListGameCountSetting",Content="Show game count next to list",IsChecked=settings.ShowListGameCount,IsEnabled=settings.ShowCollectionName,FontSize=14,Foreground=white,Margin=new Thickness(24,0,0,12)};
            collectionToggle.Checked+=(s,e) => listCountToggle.IsEnabled=true;
            collectionToggle.Unchecked+=(s,e) => listCountToggle.IsEnabled=false;
            listCountToggle.Click+=(s,e) => {
                bool previous=settings.ShowListGameCount;
                try {settings.ShowListGameCount=listCountToggle.IsChecked==true; Storage.Write("settings.json",settings); Render(false);}
                catch(Exception error) {settings.ShowListGameCount=previous; listCountToggle.IsChecked=previous; ThemedMessage.Show(dialog,error.Message,"Overlay");}
            };
            var listCountRow=new Grid {Margin=new Thickness(0,0,0,12)};
            listCountRow.Children.Add(new System.Windows.Shapes.Path {
                Data=Geometry.Parse("M 10,-5 L 10,11 L 22,11"),Stroke=muted,StrokeThickness=1,
                HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,
                IsHitTestVisible=false,Opacity=0.65
            });
            listCountToggle.Margin=new Thickness(30,0,0,0);
            listCountRow.Children.Add(listCountToggle);
            layout.Children.Add(listCountRow);
            var defaultCaption=Label("Default list",14,mint); defaultCaption.Margin=new Thickness(0,14,0,10);
            var collectionOptions=DefaultListChoices();
            string defaultId=String.IsNullOrEmpty(settings.DefaultCollectionId) ? "remember" : settings.DefaultCollectionId;
            if(!collectionOptions.Any(p => p.Key==defaultId)) collectionOptions.Add(new KeyValuePair<string,string>(defaultId,"Saved list (unavailable)"));
            var defaultPicker=new SettingsComboBox {Name="DefaultListSetting",ItemsSource=collectionOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=defaultId,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringDefault=false;
            defaultPicker.SelectionChanged+=(s,e) => {
                if(restoringDefault) return;
                string id=defaultPicker.SelectedValue as string,previous=settings.DefaultCollectionId ?? "remember";
                if(id==null || id==previous) return;
                try { SetDefaultCollection(id); }
                catch(Exception error) { restoringDefault=true; defaultPicker.SelectedValue=previous; restoringDefault=false; ThemedMessage.Show(dialog,error.Message,"Settings"); }
            };

            string jumpBinding=ControlBindings.Label(settings,"overlay.jump");
            if(jumpBinding=="Unassigned") jumpBinding=ControlBindings.KeyLabel(ControlBindings.Get(settings,"overlay.jump",true));
            var jumpCaption=Label("Jump to list ("+jumpBinding+")",14,mint);
            jumpCaption.Margin=new Thickness(0,14,0,8);
            var jumpDestination=new SettingsComboBox {Name="JumpListSetting",ItemsSource=JumpListChoices(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.JumpListId,Padding=new Thickness(8)};
            bool restoringJump=false;
            jumpDestination.SelectionChanged+=(s,e) => {
                if(restoringJump || jumpDestination.SelectedValue==null) return;
                settings.JumpListId=jumpDestination.SelectedValue as string; QueueSave();
            };
            var jumpNote=Label("Assign Jump to configured list in Show/Change controls, then choose its destination here.",12,muted);
            jumpNote.TextWrapping=TextWrapping.Wrap; jumpNote.Margin=new Thickness(0,8,0,0);
            var availableSection=new Expander {Name="AvailableListsSection",Header="Overlay lists",Foreground=white,FontSize=14,Margin=new Thickness(0,16,0,0)};
            var availablePanel=new StackPanel {Margin=new Thickness(0,12,0,0)};
            var allCollections=new CheckBox {Content="Include all lists (including new ones)",Foreground=white,IsChecked=settings.EnabledCollectionIds==null,Margin=new Thickness(0,0,0,10)};
            availablePanel.Children.Add(allCollections);
            var listSearchCaption=Label("Filter lists by name",12,muted); listSearchCaption.Margin=new Thickness(0,0,0,8); availablePanel.Children.Add(listSearchCaption);
            var listSearch=new TextBox {Name="OverlayListSearch",Padding=new Thickness(8),Margin=new Thickness(0,0,0,10)}; availablePanel.Children.Add(listSearch);
            var collectionChecks=new StackPanel();
            var collectionBoxes=new Dictionary<string,CheckBox>();
            Action saveAvailability=() => {
                try {
                    SetAvailableCollections(allCollections.IsChecked==true ? null : collectionBoxes.Where(p => p.Value.IsChecked==true).Select(p => p.Key));
                    restoringDefault=true; defaultPicker.ItemsSource=DefaultListChoices(); defaultPicker.SelectedValue=settings.DefaultCollectionId ?? "remember"; restoringDefault=false;
                    restoringJump=true; jumpDestination.ItemsSource=JumpListChoices(); jumpDestination.SelectedValue=settings.JumpListId; restoringJump=false;
                } catch(Exception error) {
                    allCollections.IsChecked=settings.EnabledCollectionIds==null;
                    foreach(var item in collectionBoxes) item.Value.IsChecked=CollectionEnabled(item.Key);
                    foreach(var box in collectionBoxes.Values) box.IsEnabled=settings.EnabledCollectionIds!=null;
                    ThemedMessage.Show(dialog,error.Message,"Settings");
                }
            };
            var listOrderRows=new Dictionary<string,FrameworkElement>();
            ScrollViewer orderScroll=null;
            const string listDragFormat="PadLauncher.OverlayList";
            Action<string,int> moveList=(id,delta) => {
                var order=CollectionOptions().Select(list => list.Id).ToList();
                int index=order.IndexOf(id),target=index+delta;
                if(index<0 || target<0 || target>=order.Count) return;
                order.RemoveAt(index); order.Insert(target,id);
                var previous=settings.OverlayListOrder;
                try {
                    settings.OverlayListOrder=order;
                    Storage.Write("settings.json",settings);
                    ApplyLibrary();
                    collectionChecks.Children.Clear();
                    foreach(string orderedId in order) collectionChecks.Children.Add(listOrderRows[orderedId]);
                } catch(Exception error) {settings.OverlayListOrder=previous; ThemedMessage.Show(dialog,error.Message,"List order");}
            };
            foreach(var list in CollectionOptions()) {
                var box=new CheckBox {Content=CombinedCollections.Label(list,library,settings.HiddenGameIds),Foreground=white,IsChecked=CollectionEnabled(list.Id),Margin=new Thickness(0,0,0,8)};
                box.IsEnabled=settings.EnabledCollectionIds!=null;
                box.Click+=(s,e) => saveAvailability();
                collectionBoxes[list.Id]=box;
                string listId=list.Id;
                var row=new DockPanel {LastChildFill=true,Margin=new Thickness(0,0,0,4)};
                var handle=new Border {Padding=new Thickness(10,3,10,3),Margin=new Thickness(8,0,0,0),Background=Brushes.Transparent,Cursor=System.Windows.Input.Cursors.SizeAll,ToolTip="Drag to reorder",Focusable=true,
                    Child=new TextBlock {Text="⋮⋮",FontSize=20,Foreground=muted,VerticalAlignment=VerticalAlignment.Center}};
                DockPanel.SetDock(handle,Dock.Right);
                row.Children.Add(handle); row.Children.Add(box);
                var dropRow=new Border {Child=row,AllowDrop=true,BorderBrush=mint,BorderThickness=new Thickness(0)};
                Point? dragStart=null;
                handle.MouseLeftButtonDown+=(s,e) => {dragStart=e.GetPosition(null); handle.Focus(); e.Handled=true;};
                handle.MouseLeftButtonUp+=(s,e) => dragStart=null;
                handle.MouseMove+=(s,e) => {
                    if(e.LeftButton!=System.Windows.Input.MouseButtonState.Pressed) {dragStart=null; return;}
                    if(!dragStart.HasValue) return;
                    Point position=e.GetPosition(null);
                    if(Math.Abs(position.X-dragStart.Value.X)<SystemParameters.MinimumHorizontalDragDistance &&
                        Math.Abs(position.Y-dragStart.Value.Y)<SystemParameters.MinimumVerticalDragDistance) return;
                    dragStart=null;
                    try {DragDrop.DoDragDrop(handle,new DataObject(listDragFormat,listId),DragDropEffects.Move);}
                    finally {foreach(var entry in listOrderRows.Values) ((Border)entry).BorderThickness=new Thickness(0);}
                };
                handle.KeyDown+=(s,e) => {
                    if(e.Key==System.Windows.Input.Key.Up || e.Key==System.Windows.Input.Key.Down) {
                        moveList(listId,e.Key==System.Windows.Input.Key.Up ? -1 : 1); handle.Focus(); e.Handled=true;
                    }
                };
                dropRow.DragOver+=(s,e) => {
                    if(!e.Data.GetDataPresent(listDragFormat)) {e.Effects=DragDropEffects.None; return;}
                    e.Effects=DragDropEffects.Move; e.Handled=true;
                    bool below=e.GetPosition(dropRow).Y>dropRow.ActualHeight/2;
                    dropRow.BorderThickness=below ? new Thickness(0,0,0,2) : new Thickness(0,2,0,0);
                    if(orderScroll!=null) {
                        double y=e.GetPosition(orderScroll).Y;
                        if(y<24) orderScroll.LineUp();
                        else if(y>orderScroll.ActualHeight-24) orderScroll.LineDown();
                    }
                };
                dropRow.DragLeave+=(s,e) => dropRow.BorderThickness=new Thickness(0);
                dropRow.Drop+=(s,e) => {
                    dropRow.BorderThickness=new Thickness(0);
                    string dragged=e.Data.GetData(listDragFormat) as string;
                    if(dragged==null) return;
                    var order=CollectionOptions().Select(item => item.Id).ToList();
                    int from=order.IndexOf(dragged),to=order.IndexOf(listId);
                    if(from<0 || to<0) return;
                    if(e.GetPosition(dropRow).Y>dropRow.ActualHeight/2) to++;
                    if(from<to) to--;
                    moveList(dragged,to-from); e.Handled=true;
                };
                listOrderRows[list.Id]=dropRow; collectionChecks.Children.Add(dropRow);
            }
            allCollections.Click+=(s,e) => {
                if(allCollections.IsChecked!=true) foreach(var box in collectionBoxes.Values) box.IsChecked=true;
                foreach(var box in collectionBoxes.Values) box.IsEnabled=allCollections.IsChecked!=true; saveAvailability();
            };
            orderScroll=new ScrollViewer {Content=collectionChecks,MaxHeight=220,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};
            availablePanel.Children.Add(orderScroll);
            var noListMatches=Label("No matching lists",12,muted); noListMatches.Visibility=Visibility.Collapsed; availablePanel.Children.Add(noListMatches);
            listSearch.TextChanged+=(s,e) => {
                string query=listSearch.Text.Trim(); int matches=0;
                foreach(var entry in listOrderRows) {
                    bool shown=Convert.ToString(collectionBoxes[entry.Key].Content).IndexOf(query,StringComparison.CurrentCultureIgnoreCase)>=0;
                    entry.Value.Visibility=shown ? Visibility.Visible : Visibility.Collapsed;
                    if(shown) matches++;
                }
                noListMatches.Visibility=matches==0 ? Visibility.Visible : Visibility.Collapsed;
                orderScroll.ScrollToTop();
            };
            var availableTip=Label("Changes save immediately. Only selected lists appear when switching lists. Select at least one. Favourites, Recently played and Most played can still be jump destinations when hidden here.",12,muted); availableTip.TextWrapping=TextWrapping.Wrap; availableTip.Margin=new Thickness(0,8,0,0); availablePanel.Children.Add(availableTip);
            availableSection.Content=availablePanel; listsLayout.Children.Add(availableSection);
            var listLayoutSection=new Expander {Header="Per-list layout",Foreground=white,FontSize=14,Margin=new Thickness(0,16,0,0),IsExpanded=listLayoutExpanded};
            listLayoutSection.Expanded+=(s,e) => listLayoutExpanded=true; listLayoutSection.Collapsed+=(s,e) => listLayoutExpanded=false;
            listLayoutSection.Content=CreateListLayoutSettings(dialog,rebuild); listsLayout.Children.Add(listLayoutSection);
            var retroArchListsSection=new Expander {Name="RetroArchListsSettingsSection",Header="RetroArch list settings",Foreground=white,FontSize=14,Margin=new Thickness(0,16,0,0),IsExpanded=retroArchListSettingsExpanded};
            retroArchListsSection.Expanded+=(s,e) => retroArchListSettingsExpanded=true; retroArchListsSection.Collapsed+=(s,e) => retroArchListSettingsExpanded=false;
            retroArchListsSection.Content=CreateListLayoutSettings(dialog,rebuild,true); listsLayout.Children.Add(retroArchListsSection);

            var customListSection=new Expander {Name="ListEditorSection",Header="List editor",Foreground=white,FontSize=14,Margin=new Thickness(0,16,0,0),IsExpanded=listEditorExpanded};
            customListSection.Expanded+=(s,e) => listEditorExpanded=true; customListSection.Collapsed+=(s,e) => listEditorExpanded=false;
            var customListPanel=new StackPanel {Margin=new Thickness(0,12,0,0)};
            var editorCollections=CollectionOptions();
            var editorCollectionsById=editorCollections.ToDictionary(c => c.Id);
            var customListOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("","Create a new list")};
            customListOptions.Add(new KeyValuePair<string,string>("source:custom","Custom games"));
            foreach(var item in settings.CustomLists ?? new List<CustomList>()) customListOptions.Add(new KeyValuePair<string,string>(item.Id,ListLabel(editorCollectionsById[item.Id])));
            foreach(var item in settings.CombinedCollections ?? new List<CombinedCollection>()) customListOptions.Add(new KeyValuePair<string,string>(item.Id,ListLabel(editorCollectionsById[item.Id])));
            foreach(var item in editorCollections.Where(c => EmulatorLibrary.IsAutomaticList(settings,c.Id))) customListOptions.Add(new KeyValuePair<string,string>(item.Id,ListLabel(item)));
            var customListPicker=new SettingsComboBox {Name="CustomListPicker",ItemsSource=customListOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=0,Padding=new Thickness(8),Margin=new Thickness(0,0,0,14)};
            var individualNote=Label("Choose individual games for a new list, or edit, rename, duplicate or delete an existing list.",12,muted);
            individualNote.TextWrapping=TextWrapping.Wrap; individualNote.Margin=new Thickness(0,0,0,8); customListPanel.Children.Add(individualNote);
            customListPanel.Children.Add(customListPicker);
            var customNameCaption=Label("List name",12,muted); customNameCaption.Margin=new Thickness(0,0,0,8); customListPanel.Children.Add(customNameCaption);
            var customListName=new TextBox {Name="CustomListName",Margin=new Thickness(0,0,0,14),Padding=new Thickness(8),MaxLength=80}; customListPanel.Children.Add(customListName);
            var gameSearch=new TextBox {Name="ListEditorGameSearch",Padding=new Thickness(8),Margin=new Thickness(0,0,0,10)};
            var gameSearchCaption=Label("Filter games by name",12,muted); gameSearchCaption.Margin=new Thickness(0,0,0,8);
            customListPanel.Children.Add(gameSearchCaption); customListPanel.Children.Add(gameSearch);
            var gameChoices=new StackPanel {Name="ListEditorGameChoices"};
            var gameBoxes=new Dictionary<string,CheckBox>();
            var gameChoiceRows=new Dictionary<string,FrameworkElement>();
            bool showAddedGamesOnly=false;
            Action filterEditorGames=() => {
                foreach(var pair in gameBoxes) {
                    bool shown=((string)pair.Value.Tag).IndexOf(gameSearch.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0 && (listEditorMode!="edit" || !showAddedGamesOnly || pair.Value.IsChecked==true);
                    pair.Value.Visibility=gameChoiceRows[pair.Key].Visibility=shown ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            var unavailableGames=new List<string>();
            var selectionCount=Label("",12,muted); customListPanel.Children.Add(selectionCount);
            bool countQueued=false;
            Action updateCount=() => selectionCount.Text=gameBoxes.Values.Count(b => b.IsChecked==true)+" games selected"+(unavailableGames.Count>0 ? " · "+unavailableGames.Count+" unavailable games retained" : "");
            var selectionActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,8)};
            var selectGames=new Button {Name="SelectAllListGames",Content="Select all",Padding=new Thickness(12,7,12,7)};

            BindSelectionToggle(selectGames,() => gameBoxes.Values.Where(b => b.Visibility==Visibility.Visible),updateCount);

            var showAddedGames=new Button {Name="ShowAddedListGames",Content="Show added games",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),Visibility=Visibility.Collapsed};
            showAddedGames.Click+=(s,e) => {
                showAddedGamesOnly=!showAddedGamesOnly;
                if(showAddedGamesOnly) {showAddedGames.Background=mint; showAddedGames.Foreground=Brushes.Black;}
                else {showAddedGames.ClearValue(Control.BackgroundProperty); showAddedGames.ClearValue(Control.ForegroundProperty);}
                filterEditorGames();
            };
            HashSet<string> uncheckedGamesSnapshot=null; List<string> unavailableSnapshot=null;
            var uncheckAddedGames=new Button {Name="UncheckAddedListGames",Content="Uncheck added",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),Visibility=Visibility.Collapsed};
            uncheckAddedGames.Click+=(s,e) => {
                if(uncheckedGamesSnapshot==null) {
                    uncheckedGamesSnapshot=new HashSet<string>(gameBoxes.Where(pair => pair.Value.IsChecked==true).Select(pair => pair.Key));
                    unavailableSnapshot=unavailableGames.ToList();
                    foreach(var box in gameBoxes.Values.Where(box => box.IsChecked==true).ToList()) box.IsChecked=false;
                    unavailableGames.Clear(); uncheckAddedGames.Content="Restore added";
                } else {
                    foreach(var pair in gameBoxes.Where(pair => uncheckedGamesSnapshot.Contains(pair.Key))) pair.Value.IsChecked=true;
                    unavailableGames=unavailableGames.Concat(unavailableSnapshot ?? new List<string>()).Distinct().ToList();
                    uncheckedGamesSnapshot=null; unavailableSnapshot=null; uncheckAddedGames.Content="Uncheck added";
                }
                updateCount(); filterEditorGames();
            };
            selectionActions.Children.Add(selectGames); selectionActions.Children.Add(showAddedGames); selectionActions.Children.Add(uncheckAddedGames); customListPanel.Children.Add(selectionActions);
            var gameChoicesScroll=new ScrollViewer {Content=gameChoices,MaxHeight=260,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled}; customListPanel.Children.Add(gameChoicesScroll);
            var customListActions=new WrapPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0)};
            var saveCustomList=new Button {Name="SaveCustomList",Content="Create list",Padding=new Thickness(12,7,12,7)};
            var deleteCustomList=new Button {Content="Delete list",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            var deleteListGames=new Button {Name="DeleteListAndGames",Content="Delete list and unregister its games",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),Visibility=Visibility.Collapsed};
            var duplicateCustomList=new Button {Name="DuplicateCustomList",Content="Duplicate list",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            var automaticListNote=Label("Automatic emulator lists can be renamed or duplicated. Manage their games under Emulators, or hide their list under Overlay lists.",12,new SolidColorBrush(Color.FromRgb(255,218,106)));
            automaticListNote.TextWrapping=TextWrapping.Wrap; automaticListNote.Margin=new Thickness(0,12,0,0);
            bool refreshingEditor=false;
            List<Game> editorGames=null;
            var editorLabels=new Dictionary<string,string>();
            HashSet<string> registeredEditorIds=null;
            Action loadCustomList=() => {
                if(refreshingEditor || !customListSection.IsExpanded || customListPanel.Visibility==Visibility.Collapsed) return;
                uncheckedGamesSnapshot=null; unavailableSnapshot=null; uncheckAddedGames.Content="Uncheck added";
                var membershipOptions=editorCollections;
                var item=membershipOptions.FirstOrDefault(c => c.Id==customListPicker.SelectedValue as string);
                customListName.Text=item==null ? "" : item.Name; gameSearch.Text="";
                bool duplicating=listEditorMode=="duplicate",deleting=listEditorMode=="delete",showGames=listEditorMode=="create" || listEditorMode=="edit";
                showAddedGames.Visibility=uncheckAddedGames.Visibility=listEditorMode=="edit" ? Visibility.Visible : Visibility.Collapsed;
                selectGames.Visibility=listEditorMode=="create" ? Visibility.Visible : Visibility.Collapsed;
                customListName.IsEnabled=duplicating || item==null || item.Id!="source:custom";
                if(duplicating && item!=null) {
                    string baseName=item.Name.Length>69 ? item.Name.Substring(0,69)+" copy" : item.Name+" copy",copyName=baseName; int suffix=2;
                    while(editorCollections.Any(list => String.Equals(list.Name,copyName,StringComparison.CurrentCultureIgnoreCase))) copyName=baseName+" "+suffix++;
                    customListName.Text=copyName;
                }
                customNameCaption.Text=duplicating ? "New list name" : "List name";
                customNameCaption.Visibility=customListName.Visibility=deleting ? Visibility.Collapsed : Visibility.Visible;
                gameSearchCaption.Visibility=gameSearch.Visibility=selectionCount.Visibility=selectionActions.Visibility=gameChoicesScroll.Visibility=showGames ? Visibility.Visible : Visibility.Collapsed;
                saveCustomList.Visibility=showGames ? Visibility.Visible : Visibility.Collapsed;
                individualNote.Text=duplicating ? "Copy a list’s current games into a new independent list." : deleting ? "Delete a user-created list, keeping its games registered, or delete it and unregister its games. Automatic and launcher lists can be hidden under Overlay lists." : "Choose individual games for a new list, or edit its name and games.";
                bool automatic=item!=null && EmulatorLibrary.IsAutomaticList(settings,item.Id);
                gameChoices.IsEnabled=gameSearch.IsEnabled=selectGames.IsEnabled=uncheckAddedGames.IsEnabled=!automatic;
                automaticListNote.Visibility=automatic && listEditorMode=="edit" ? Visibility.Visible : Visibility.Collapsed;
                duplicateCustomList.Visibility=duplicating && item!=null ? Visibility.Visible : Visibility.Collapsed;
                gameBoxes.Clear(); gameChoiceRows.Clear(); gameChoices.Children.Clear();
                var selected=new HashSet<string>(item==null ? new List<string>() : item.Games ?? new List<string>());
                if(showGames && editorGames==null) {
                    editorGames=library.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).GroupBy(g => g.Id).Select(g => g.First()).ToList();
                    registeredEditorIds=new HashSet<string>(editorGames.Select(g => g.Id));
                }
                unavailableGames=showGames ? selected.Where(id => !registeredEditorIds.Contains(id)).ToList() : new List<string>();
                foreach(var game in (showGames ? (IEnumerable<Game>)editorGames : Enumerable.Empty<Game>())) {
                    string source;
                    if(!editorLabels.TryGetValue(game.Id,out source)) editorLabels[game.Id]=source=GameSourceLabel(game);
                    var box=new CheckBox {Content=game.Name+" · "+source,Tag=game.Name,Foreground=white,IsChecked=selected.Contains(game.Id),Margin=new Thickness(0,4,0,4)};
                    var gameRow=new StackPanel {Margin=new Thickness(0,0,0,6)}; gameRow.Children.Add(box);
                    var membershipHint=Label("",12,muted);
                    membershipHint.TextWrapping=TextWrapping.Wrap; membershipHint.Margin=new Thickness(28,0,0,4); gameRow.Children.Add(membershipHint);
                    Action updateHint=() => {
                        membershipHint.Visibility=box.IsChecked==true ? Visibility.Visible : Visibility.Collapsed;
                        if(box.IsChecked==true) membershipHint.Text=CustomLists.MembershipReason(settings,membershipOptions,item==null ? null : item.Id,game.Id)+(box.IsChecked==true && !selected.Contains(game.Id) ? " · Not saved yet" : "");
                    };
                    RoutedEventHandler selectionChanged=(s,e) => {
                        updateHint();
                        bool shown=game.Name.IndexOf(gameSearch.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)>=0 &&
                            (listEditorMode!="edit" || !showAddedGamesOnly || box.IsChecked==true);
                        box.Visibility=gameRow.Visibility=shown ? Visibility.Visible : Visibility.Collapsed;
                        if(countQueued) return;
                        countQueued=true;
                        Dispatcher.BeginInvoke(new Action(() => {countQueued=false; updateCount();}));
                    };
                    box.Checked+=selectionChanged; box.Unchecked+=selectionChanged;
                    updateHint(); gameBoxes[game.Id]=box; gameChoiceRows[game.Id]=gameRow; gameChoices.Children.Add(gameRow);
                }
                saveCustomList.Content=item==null ? "Create list" : "Save changes"; deleteCustomList.Visibility=!deleting || item==null || item.Id=="source:custom" || automatic ? Visibility.Collapsed : Visibility.Visible; updateCount();
                deleteListGames.Visibility=deleteCustomList.Visibility; filterEditorGames();
            };
            gameSearch.TextChanged+=(s,e) => filterEditorGames();
            customListPicker.SelectionChanged+=(s,e) => loadCustomList();
            customListSection.Expanded+=(s,e) => {if(e.Source==customListSection) loadCustomList();};
            saveCustomList.Click+=(s,e) => {try {
                var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                var chosenGames=gameBoxes.Where(b => b.Value.IsChecked==true).Select(b => b.Key).ToList();
                string id=customListPicker.SelectedValue as string;
                if(id!=null && EmulatorLibrary.IsAutomaticList(settings,id)) {
                    string name=customListName.Text.Trim();
                    if(name.Length==0) throw new ArgumentException("Enter a list name.");
                    if(CollectionOptions().Any(c => c.Id!=id && String.Equals(c.Name,name,StringComparison.CurrentCultureIgnoreCase))) throw new ArgumentException("A list with that name already exists.");
                    var playlist=(next.RetroArchPlaylistLists ?? new List<CustomList>()).FirstOrDefault(list => list.Id==id);
                    if(playlist!=null) playlist.Name=name;
                    else EmulatorLibrary.Config(next,id.Substring("source:emulator:".Length)).ListName=name;
                } else CustomLists.Save(next,library,String.IsNullOrEmpty(id) ? null : id,customListName.Text,chosenGames.Concat(unavailableGames));
                Storage.Write("settings.json",next); settings=next; EmulatorLibrary.Apply(library,settings); ApplyLibrary(); rebuild();
            } catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Custom lists");}};
            deleteCustomList.Click+=(s,e) => {try {
                if(!ConfirmEmulatorRemoval(dialog,"Delete list","Delete this list?","Its games will stay in Pad Launcher.","Delete list")) return;
                var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); CustomLists.Delete(next,customListPicker.SelectedValue as string);
                Storage.Write("settings.json",next); settings=next; ApplyLibrary(); rebuild();
            } catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Custom lists");}};
            duplicateCustomList.Click+=(s,e) => {try {
                var selected=CollectionOptions().First(c => c.Id==customListPicker.SelectedValue as string);
                var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                CustomLists.Save(next,library,null,customListName.Text,selected.Games ?? new List<string>());
                Storage.Write("settings.json",next); settings=next; ApplyLibrary(); rebuild();
            } catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Custom lists");}};
            deleteListGames.Click+=(s,e) => {try {
                if(scanning) throw new InvalidOperationException("Please wait for the library refresh to finish.");
                string id=customListPicker.SelectedValue as string;
                var selectedList=CollectionOptions().First(list => list.Id==id);
                int count=(selectedList.Games ?? new List<string>()).Distinct().Count();
                if(!ConfirmEmulatorRemoval(dialog,"Delete list and games","Delete “"+selectedList.Name+"” and unregister "+count+" games?","These game entries will also disappear from all other Pad Launcher lists. ROMs, installed games and artwork files stay on disk. Launcher games stay removed on refresh; restoring their launcher games can add them again.","Delete list and unregister games")) return;
                var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                var removed=new HashSet<string>(CustomLists.DeleteAndUnregisterGames(next,library,id));
                Storage.Write("settings.json",next); settings=next;
                library.Games.RemoveAll(game => removed.Contains(game.Id));
                foreach(var list in library.Collections) if(list.Games!=null) list.Games.RemoveAll(removed.Contains);
                cards.Clear(); carousel.Children.Clear(); ApplyLibrary(); rebuild();
            } catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Custom lists");}};
            customListActions.Children.Add(saveCustomList); customListActions.Children.Add(duplicateCustomList); customListActions.Children.Add(deleteCustomList); customListActions.Children.Add(deleteListGames); customListPanel.Children.Add(customListActions); customListPanel.Children.Add(automaticListNote);
            var editorPanel=new StackPanel {Margin=new Thickness(12,8,12,12)};
            var modes=new System.Windows.Controls.Primitives.UniformGrid {Rows=1,HorizontalAlignment=HorizontalAlignment.Stretch,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,10,0,2)}; editorPanel.Children.Add(modes);
            editorPanel.Children.Add(customListPanel);
            var combinePanel=CreateCombinedListEditor(dialog,rebuild); editorPanel.Children.Add(combinePanel);
            var mergePanel=new StackPanel(); editorPanel.Children.Add(mergePanel);
            var mergeNote=Label("Merge the current games from two or more lists into a new list. Duplicate game entries are included once. The new list can be edited independently.",12,muted); mergeNote.TextWrapping=TextWrapping.Wrap; mergeNote.Margin=new Thickness(0,12,0,0); mergePanel.Children.Add(mergeNote);
            var mergeNameCaption=Label("New list name",12,mint); mergeNameCaption.Margin=new Thickness(0,12,0,6); mergePanel.Children.Add(mergeNameCaption);
            var mergeName=new TextBox {Name="MergeListName",Padding=new Thickness(8),MaxLength=80}; mergePanel.Children.Add(mergeName);
            var mergeChoices=new StackPanel {Name="MergeSources"}; var mergeBoxes=new Dictionary<string,CheckBox>();
            foreach(var sourceList in CollectionOptions()) {
                var box=new CheckBox {Content=ListLabel(sourceList),Tag=sourceList.Id,Margin=new Thickness(0,6,0,6)};
                mergeBoxes[sourceList.Id]=box; mergeChoices.Children.Add(box);
            }
            var selectMerge=new Button {Content="Select all",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,8)};
            BindSelectionToggle(selectMerge,() => mergeBoxes.Values); mergePanel.Children.Add(selectMerge);
            mergePanel.Children.Add(new ScrollViewer {Content=mergeChoices,MaxHeight=260,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var mergeDisposition=new SettingsComboBox {Name="MergeSourceDisposition",ItemsSource=new[]{new KeyValuePair<string,string>("keep","Keep lists after merging"),new KeyValuePair<string,string>("all","Delete all source lists"),new KeyValuePair<string,string>("choose","Choose what to delete")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue="keep",Padding=new Thickness(8),Margin=new Thickness(0,12,0,8)}; mergePanel.Children.Add(mergeDisposition);
            var deletionChoices=new StackPanel {Name="MergeDeleteChoices",Visibility=Visibility.Collapsed}; mergePanel.Children.Add(deletionChoices);
            var deletionBoxes=new Dictionary<string,CheckBox>();
            var deletableSources=new HashSet<string>((settings.CustomLists ?? new List<CustomList>()).Select(list => list.Id).Concat((settings.CombinedCollections ?? new List<CombinedCollection>()).Select(list => list.Id)));
            Action refreshDeletionChoices=() => {
                var checkedIds=new HashSet<string>(deletionBoxes.Where(pair => pair.Value.IsChecked==true).Select(pair => pair.Key));
                deletionChoices.Children.Clear(); deletionBoxes.Clear();
                deletionChoices.Visibility=(string)mergeDisposition.SelectedValue=="choose" ? Visibility.Visible : Visibility.Collapsed;
                foreach(var source in CollectionOptions().Where(list => mergeBoxes.ContainsKey(list.Id) && mergeBoxes[list.Id].IsChecked==true)) {
                    var box=new CheckBox {Name="MergeDeleteSource",Content=source.Name+(deletableSources.Contains(source.Id) ? "" : " (automatic list — kept)"),Tag=source.Id,IsEnabled=deletableSources.Contains(source.Id),IsChecked=checkedIds.Contains(source.Id),Margin=new Thickness(0,5,0,5)};
                    deletionBoxes[source.Id]=box; deletionChoices.Children.Add(box);
                }
                if(deletionBoxes.Count==0) deletionChoices.Children.Add(Label("Select source lists above, then check which ones to delete.",12,muted));
            };
            mergeDisposition.SelectionChanged+=(s,e) => refreshDeletionChoices();
            foreach(var box in mergeBoxes.Values) {box.Checked+=(s,e) => refreshDeletionChoices(); box.Unchecked+=(s,e) => refreshDeletionChoices();}
            refreshDeletionChoices();
            var mergeDeletionNote=Label("Only user-created lists can be deleted. Automatic and launcher lists are always kept; registered games are never removed by merging.",12,muted); mergeDeletionNote.TextWrapping=TextWrapping.Wrap; mergePanel.Children.Add(mergeDeletionNote);
            var mergeSave=new Button {Name="MergeLists",Content="Merge lists",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,12,0,0)}; mergePanel.Children.Add(mergeSave);
            mergeSave.Click+=(s,e) => {try {
                var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                var chosenSources=mergeBoxes.Where(pair => pair.Value.IsChecked==true).Select(pair => pair.Key).ToList();
                string disposition=mergeDisposition.SelectedValue as string;
                var deletingIds=disposition=="all" ? chosenSources.Where(deletableSources.Contains).ToList() : disposition=="choose" ? deletionBoxes.Where(pair => pair.Value.IsEnabled && pair.Value.IsChecked==true).Select(pair => pair.Key).ToList() : new List<string>();
                CustomLists.Merge(next,library,mergeName.Text,chosenSources,deletingIds);
                if(disposition!="keep") {
                    var deleting=CollectionOptions().Where(list => deletingIds.Contains(list.Id)).Select(list => list.Name).ToList();
                    string question=deleting.Count>0 ? "Merge and delete "+deleting.Count+" source lists?\n"+String.Join("\n",deleting) : "No source lists will be deleted. Merge and keep them?";
                    if(!ConfirmEmulatorRemoval(dialog,"Merge lists",question,"Games stay registered and are kept in the new list. Other combined lists using deleted source lists may change. Automatic and launcher lists stay available.",deleting.Count>0 ? "Merge and delete lists" : "Merge lists")) return;
                }
                Storage.Write("settings.json",next); settings=next; ApplyLibrary(); listEditorMode="edit"; rebuild();
            } catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Merge lists");}};
            var modeButtons=new Dictionary<string,Button>();
            var tagsPanel=CreateListTagsSettings(dialog); editorPanel.Children.Add(tagsPanel);
            Action refreshEditor=() => {
                bool create=listEditorMode=="create",edit=listEditorMode=="edit";
                tagsPanel.Visibility=listEditorMode=="tags" ? Visibility.Visible : Visibility.Collapsed;
                customListPanel.Visibility=listEditorMode!="tags" && listEditorMode!="merge" && listEditorMode!="combine" ? Visibility.Visible : Visibility.Collapsed;
                combinePanel.Visibility=listEditorMode=="combine" ? Visibility.Visible : Visibility.Collapsed;
                mergePanel.Visibility=listEditorMode=="merge" ? Visibility.Visible : Visibility.Collapsed;
                customListPicker.Visibility=create ? Visibility.Collapsed : Visibility.Visible;
                refreshingEditor=true;
                customListPicker.ItemsSource=create ? customListOptions.Take(1).ToList() : listEditorMode=="duplicate" ? editorCollections.Select(list => new KeyValuePair<string,string>(list.Id,ListLabel(list))).ToList() : listEditorMode=="delete" ? customListOptions.Where(list => deletableSources.Contains(list.Key)).ToList() : customListOptions.Skip(1).ToList();
                customListPicker.SelectedIndex=0; refreshingEditor=false; loadCustomList();
                foreach(var pair in modeButtons) {pair.Value.Background=pair.Key==listEditorMode ? mint : new SolidColorBrush(Color.FromRgb(28,48,61)); pair.Value.Foreground=pair.Key==listEditorMode ? Brushes.Black : white;}
                saveCustomList.IsEnabled=create || customListPicker.SelectedIndex>=0;
            };
            foreach(string mode in new[]{"create","edit","combine","merge","duplicate","delete","tags"}) {
                string chosenMode=mode;
                var modeButton=new Button {Name="ListEditorMode_"+mode,Content=Char.ToUpper(mode[0])+mode.Substring(1),Padding=new Thickness(4,8,4,8),Height=38,VerticalAlignment=VerticalAlignment.Center,VerticalContentAlignment=VerticalAlignment.Center,Margin=new Thickness(2,0,2,0)};
                modeButtons[mode]=modeButton; modes.Children.Add(modeButton);
                modeButton.Click+=(s,e) => {listEditorMode=chosenMode; refreshEditor();};
            }
            refreshEditor(); customListSection.Content=editorPanel; listsLayout.Children.Add(customListSection);
            listsLayout.Children.Add(CreateHiddenGamesSettings(rebuild));
            listsLayout.Children.Add(jumpCaption); listsLayout.Children.Add(jumpDestination); listsLayout.Children.Add(jumpNote);
            listsLayout.Children.Add(defaultCaption); listsLayout.Children.Add(defaultPicker);
            layout.Children.Add(skipEmpty);
            var removeMissing=new CheckBox {Content="Remove missing emulator and custom games on refresh",IsChecked=settings.RemoveMissingEmulatorGames,FontSize=14,Foreground=white,Margin=new Thickness(0,0,0,8)};
            removeMissing.Click+=(s,e) => {
                bool previous=settings.RemoveMissingEmulatorGames;
                try {settings.RemoveMissingEmulatorGames=removeMissing.IsChecked==true; Storage.Write("settings.json",settings);}
                catch(Exception error) {settings.RemoveMissingEmulatorGames=previous; removeMissing.IsChecked=previous; ThemedMessage.Show(dialog,error.Message,"Overlay");}
            };
            layout.Children.Add(removeMissing);
            var missingNote=Label("Removes emulator and custom game entries when their game file or folder has been deleted. Disconnected drives and inaccessible folders are skipped.",12,muted);
            missingNote.TextWrapping=TextWrapping.Wrap; layout.Children.Add(missingNote);
            var monitorCaption=Label("Open on monitor",14,mint); monitorCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(monitorCaption);
            var monitorOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("cursor","Cursor's monitor")};
            var displays=System.Windows.Forms.Screen.AllScreens.OrderBy(s => s.DeviceName,StringComparer.Ordinal).ToArray();
            for(int i=0;i<displays.Length;i++) {
                var display=displays[i];
                monitorOptions.Add(new KeyValuePair<string,string>(display.DeviceName,"Monitor "+(i+1)+" · "+display.Bounds.Width+" × "+display.Bounds.Height+(display.Primary ? " (primary)" : "")));
            }
            string monitorPreference=String.IsNullOrEmpty(settings.OpenMonitor) ? "cursor" : settings.OpenMonitor;
            if(!monitorOptions.Any(p => p.Key==monitorPreference)) monitorOptions.Add(new KeyValuePair<string,string>(monitorPreference,"Saved monitor (disconnected)"));
            var monitorPicker=new SettingsComboBox {Name="OpenMonitorSetting",ItemsSource=monitorOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=monitorPreference,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white,Background=new SolidColorBrush(Color.FromRgb(28,48,61))};
            bool restoringMonitor=false;
            monitorPicker.SelectionChanged+=(s,e) => {
                if(restoringMonitor) return;
                string next=monitorPicker.SelectedValue as string,previous=settings.OpenMonitor;
                if(next==null || next==previous) return;
                try {
                    settings.OpenMonitor=next; Storage.Write("settings.json",settings);
                    if(IsVisible && !preview) PlaceOnMonitor(ResolveMonitor(next,System.Windows.Forms.Screen.AllScreens,System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position)));
                }
                catch(Exception error) {
                    settings.OpenMonitor=previous; restoringMonitor=true; monitorPicker.SelectedValue=previous ?? "cursor"; restoringMonitor=false;
                    ThemedMessage.Show(dialog,"Could not save monitor preference: "+error.Message,"Settings");
                }
            };
            layout.Children.Add(monitorPicker);
            var layoutCaption=Label("Layout",14,mint); layoutCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(layoutCaption);
            var columnsControls=new StackPanel {Name="GridColumnsSettings"};
            bool angledSizingAvailable=settings.CarouselLayout!="grid" && settings.CarouselLayout!="rectangular";
            var sizingControls=new StackPanel {Name="AngledPanelSizingSettings",Visibility=angledSizingAvailable ? Visibility.Visible : Visibility.Collapsed};
            var layoutPicker=new SettingsComboBox {Name="CarouselLayoutSetting",ItemsSource=new[]{new KeyValuePair<string,string>("angled","Angled"),new KeyValuePair<string,string>("rectangular","Rectangular")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=DefaultCarouselLayout,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringLayout=false;
            layoutPicker.SelectionChanged+=(s,e) => {
                if(restoringLayout) return;
                string choice=layoutPicker.SelectedValue as string,previous=DefaultCarouselLayout;
                if(choice==null || choice==previous) return;
                try { SetCarouselLayout(choice); sizingControls.Visibility=choice=="angled" ? Visibility.Visible : Visibility.Collapsed; }
                catch(Exception error) { restoringLayout=true; layoutPicker.SelectedValue=previous; restoringLayout=false; ThemedMessage.Show(dialog,error.Message,"Settings"); }
            };
            layout.Children.Add(layoutPicker);
            var rememberLayouts=new CheckBox {Name="RememberControlLayoutChangesSetting",Content="Remember layout changes made with controls (default: "+ControlBindings.ButtonLabel(settings,"west")+")",IsChecked=settings.RememberControlLayoutChanges,Margin=new Thickness(0,14,0,6)};
            layout.Children.Add(rememberLayouts);
            var rememberLayoutsNote=Label("Save Grid ↔ Angled/Rectangular switches for each list, including after restarting the app.",12,muted); rememberLayoutsNote.TextWrapping=TextWrapping.Wrap; rememberLayoutsNote.Margin=new Thickness(0,0,0,16); layout.Children.Add(rememberLayoutsNote);
            rememberLayouts.Click+=(s,e) => {settings.RememberControlLayoutChanges=rememberLayouts.IsChecked==true; QueueSave();};
            var sizingCaption=Label("Angled panel sizing",14,mint); sizingCaption.Margin=new Thickness(0,14,0,10); sizingControls.Children.Add(sizingCaption);
            var sizingPicker=new SettingsComboBox {ItemsSource=new[]{new KeyValuePair<string,string>("fixed","Fixed (blurred edges)"),new KeyValuePair<string,string>("fit","Fit artwork")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.AngledPanelSizing=="fixed" ? "fixed" : "fit",FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringSizing=false;
            sizingPicker.SelectionChanged+=(s,e) => {
                if(restoringSizing) return;
                string choice=sizingPicker.SelectedValue as string,previous=settings.AngledPanelSizing=="fixed" ? "fixed" : "fit";
                if(choice==null || choice==previous) return;
                try { SetAngledPanelSizing(choice); }
                catch(Exception error) { restoringSizing=true; sizingPicker.SelectedValue=previous; restoringSizing=false; ThemedMessage.Show(dialog,error.Message,"Settings"); }
            };
            sizingControls.Children.Add(sizingPicker); layout.Children.Add(sizingControls);

            var gridHeading=Label("Grid settings",16,white); gridHeading.FontWeight=FontWeights.SemiBold; gridHeading.Margin=new Thickness(0,12,0,0); columnsControls.Children.Add(gridHeading);
            var columnsCaption=Label("Grid columns",14,mint); columnsCaption.Margin=new Thickness(0,14,0,10); columnsControls.Children.Add(columnsCaption);
            var columnsPicker=new SettingsComboBox {ItemsSource=Enumerable.Range(2,9).ToArray(),SelectedItem=settings.GridColumns >= 2 && settings.GridColumns <= 10 ? settings.GridColumns : 5,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringColumns=false;
            columnsPicker.SelectionChanged+=(s,e) => {
                if(restoringColumns || columnsPicker.SelectedItem==null) return;
                int previous=(settings.GridColumns >= 2 && settings.GridColumns <= 10 ? settings.GridColumns : 5),columns=(int)columnsPicker.SelectedItem;
                if(columns==previous) return;
                try { SetGridColumns(columns); }
                catch(Exception error) { restoringColumns=true; columnsPicker.SelectedItem=previous; restoringColumns=false; ThemedMessage.Show(dialog,error.Message,"Settings"); }
            };
            columnsControls.Children.Add(columnsPicker);
            var rowsCaption=Label("Grid rows",14,mint); rowsCaption.Margin=new Thickness(0,14,0,10); columnsControls.Children.Add(rowsCaption);
            var rowsPicker=new SettingsComboBox {ItemsSource=Enumerable.Range(1,5).ToArray(),SelectedItem=settings.GridRows >= 1 && settings.GridRows <= 5 ? settings.GridRows : 3,FontSize=14,Padding=new Thickness(10,7,10,7),Foreground=white};
            bool restoringRows=false;
            rowsPicker.SelectionChanged+=(s,e) => {
                if(restoringRows || rowsPicker.SelectedItem==null) return;
                int previous=(settings.GridRows >= 1 && settings.GridRows <= 5 ? settings.GridRows : 3),rowCount=(int)rowsPicker.SelectedItem;
                if(rowCount==previous) return;
                try { SetGridRows(rowCount); }
                catch(Exception error) { restoringRows=true; rowsPicker.SelectedItem=previous; restoringRows=false; ThemedMessage.Show(dialog,error.Message,"Settings"); }
            };
            columnsControls.Children.Add(rowsPicker);
            var rowsTip=Label("These defaults also apply when switching to Grid with the controller. Individual lists can override them. The grid scrolls to keep the selected row visible.",12,muted); rowsTip.TextWrapping=TextWrapping.Wrap; rowsTip.Margin=new Thickness(0,7,0,0); columnsControls.Children.Add(rowsTip);
            layout.Children.Add(columnsControls);
            AddPanelSizeControls(layout,dialog);
            var gapCaption=Label("Artwork gap",14,mint); gapCaption.Margin=new Thickness(0,14,0,10); layout.Children.Add(gapCaption);
            var gapValue=Label(settings.ArtworkGap.ToString("0")+" px",13,white); layout.Children.Add(gapValue);
            var gapPicker=new Slider {Name="ArtworkGapSetting",Minimum=0,Maximum=64,Value=LibretroPanelGap,TickFrequency=2,SmallChange=2,LargeChange=8,IsSnapToTickEnabled=true,Margin=new Thickness(0,4,0,8)};
            bool restoringGap=false;
            gapPicker.ValueChanged+=(s,e) => {
                if(restoringGap) return;
                double previous=LibretroPanelGap;
                try {SetArtworkGap(gapPicker.Value); gapValue.Text=gapPicker.Value.ToString("0")+" px";}
                catch(Exception error) {restoringGap=true; gapPicker.Value=previous; restoringGap=false; ThemedMessage.Show(dialog,error.Message,"Settings");}
            };
            layout.Children.Add(gapPicker);
            var listArtworkSection=new Expander {Name="PerListArtworkSection",Header="Per-list artwork",Foreground=white,FontSize=14,Margin=new Thickness(0,16,0,0),Content=CreateListLayoutSettings(dialog,rebuild,false,true)}; artworkLayout.Children.Add(listArtworkSection);
            artworkLayout.Children.Add(CreateEmulatorArtworkSettings(dialog,rebuild));
            var artworkCaption=Label("Game artwork",14,mint); artworkCaption.Margin=new Thickness(0,16,0,8); artworkLayout.Children.Add(artworkCaption);
            var artworkGames=new SettingsComboBox {Name="ArtworkGamePicker",ItemsSource=library.Games.Select(game => new KeyValuePair<string,string>(game.Id,game.Name+" · "+GameSourceLabel(game))).ToList(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.SelectedId,Padding=new Thickness(8)};
            artworkLayout.Children.Add(artworkGames);
            var artworkActions=new WrapPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,4)};
            var pickArtwork=new Button {Content="Choose SGDB image…",Padding=new Thickness(12,7,12,7)};
            pickArtwork.Click+=(s,e) => {var game=library.Games.FirstOrDefault(entry => entry.Id==artworkGames.SelectedValue as string); if(game!=null) ShowArtworkPicker(dialog,game);};
            var libretroArtworkButton=new Button {Name="ArtworkChooseLibretroImage",Content="Choose Libretro image…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            libretroArtworkButton.Click+=(s,e) => {var game=library.Games.FirstOrDefault(entry => entry.Id==artworkGames.SelectedValue as string); if(game!=null) ShowLibretroArtworkPicker(dialog,game);};
            var localArtwork=new Button {Content="Choose local image…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            localArtwork.Click+=(s,e) => {
                var game=library.Games.FirstOrDefault(entry => entry.Id==artworkGames.SelectedValue as string); if(game==null) return;
                if(scanning) {ThemedMessage.Show(dialog,"Please wait for the current refresh to finish.","Artwork"); return;}
                var file=new Microsoft.Win32.OpenFileDialog {Filter="Artwork|*.png;*.jpg;*.jpeg;*.bmp",CheckFileExists=true};
                if(file.ShowDialog(dialog)!=true) return;
                try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    if(next.ArtworkOverrides==null) next.ArtworkOverrides=new Dictionary<string,string>();
                    next.ArtworkOverrides[game.Id]=ArtworkService.SaveLocalChoice(file.FileName);
                    Storage.Write("settings.json",next); settings=next;
                    if(artworkCancellation!=null) artworkCancellation.Cancel();
                    foreach(var entry in library.Games.Where(entry => entry.Id==game.Id)) ArtworkService.ApplyCached(entry,settings);
                    RefreshArtworkPresentation();
                } catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Artwork");}
            };
            var resetArtwork=new Button {Content="Reset artwork",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            resetArtwork.Click+=(s,e) => {
                var game=library.Games.FirstOrDefault(entry => entry.Id==artworkGames.SelectedValue as string); if(game==null) return;
                if(scanning) {ThemedMessage.Show(dialog,"Please wait for the current refresh to finish.","Artwork"); return;}
                try {ResetGameArtwork(game);}
                catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Artwork");}
            };
            foreach(var button in new[] {pickArtwork,libretroArtworkButton,localArtwork,resetArtwork}) {button.Margin=new Thickness(0,0,8,8); artworkActions.Children.Add(button);} artworkLayout.Children.Add(artworkActions);
            var missingCaption=Label("Games without artwork",14,mint); missingCaption.Margin=new Thickness(0,12,0,8); artworkLayout.Children.Add(missingCaption);
            var missingCount=Label("",12,muted); missingCount.Name="MissingArtworkCount"; artworkLayout.Children.Add(missingCount);
            var missingList=new WrapPanel {Name="MissingArtworkList",Orientation=Orientation.Horizontal,Margin=new Thickness(0,4,0,12)}; artworkLayout.Children.Add(missingList);
            missingArtworkCount=missingCount; missingArtworkList=missingList;
            UpdateMissingArtworkList(missingCount,missingList,dialog);
            bool startupEnabled=false; try {startupEnabled=StartupSetting.Enabled;} catch {}
            var startupToggle=new CheckBox {Name="StartWithWindowsSetting",Content="Start with Windows",IsChecked=startupEnabled,Margin=new Thickness(0,18,0,12)};
            startupToggle.IsEnabled=!TestEnvironment.EmulatorDetection;
            if(TestEnvironment.FirstStartup) startupToggle.IsEnabled=false;
            startupToggle.Click+=(s,e) => {try {StartupSetting.Set(startupToggle.IsChecked==true);} catch(Exception error) {startupToggle.IsChecked=startupEnabled; ThemedMessage.Show(dialog,error.Message,"Windows startup");}}; systemLayout.Children.Add(startupToggle);
            Func<string,string,StackPanel> addIntegration=(title,name) => {
                var body=new StackPanel {Margin=new Thickness(12,0,12,12)};
                integrationsLayout.Children.Add(new Expander {Name=name,Header=title,Content=body,IsExpanded=false,Margin=new Thickness(0,14,0,0),Foreground=white,FontSize=14});
                return body;
            };
            var steamIntegration=addIntegration("Steam","SteamIntegrationSection");
            var steamEnabled=new CheckBox {Name="SteamEnabledSetting",Content="Include Steam games",IsChecked=settings.SteamEnabled,Margin=new Thickness(0,26,0,12)}; steamIntegration.Children.Add(steamEnabled);
            var steamCaption=Label("Steam path",14,mint); steamCaption.Margin=new Thickness(0,14,0,10); steamIntegration.Children.Add(steamCaption);
            var steamPath=new TextBox {Name="SteamPathSetting",Foreground=muted,Text=DisplayLauncherPath(settings.SteamPath ?? SteamLibrary.DetectSteam()),FontSize=14,Padding=new Thickness(8),ToolTip="Steam installation folder containing steam.exe and steamapps"};
            var steamPathRow=new DockPanel(); steamIntegration.Children.Add(steamPathRow);
            Action reloadUsers=null;
            var steamActions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,8,0,0)};
            var browseSteam=new Button {Content="Browse…",FontSize=13,Padding=new Thickness(14,7,14,7),Margin=new Thickness(8,0,0,0)};
            DockPanel.SetDock(browseSteam,Dock.Right); steamPathRow.Children.Add(browseSteam); steamPathRow.Children.Add(steamPath);
            var saveSteam=new Button {Content="Save path",FontSize=13,Padding=new Thickness(14,7,14,7)};
            var steamStatus=Label("Select the Steam installation, not an individual library drive.",12,muted); steamStatus.TextWrapping=TextWrapping.Wrap; steamStatus.Margin=new Thickness(0,7,0,0);
            browseSteam.Click+=(s,e) => {
                using(var picker=new System.Windows.Forms.FolderBrowserDialog {Description="Choose the Steam installation folder",SelectedPath=steamPath.Text}) {
                    if(picker.ShowDialog(new DialogOwner(new System.Windows.Interop.WindowInteropHelper(dialog).EnsureHandle()))==System.Windows.Forms.DialogResult.OK) steamPath.Text=picker.SelectedPath;
                }
            };
            saveSteam.Click+=(s,e) => {
                try { steamPath.Text=DisplayLauncherPath(SetSteamFolder(steamPath.Text)); reloadUsers(); RefreshLibrary(true); ShowLauncherPathCheck(steamStatus,"Steam",steamPath.Text); }
                catch(Exception error) { ShowLauncherPathError(steamStatus,"Steam",error); }
            };
            steamActions.Children.Add(saveSteam); steamIntegration.Children.Add(steamActions); steamIntegration.Children.Add(steamStatus);
            var userCaption=Label("Steam user ID",14,mint); userCaption.Margin=new Thickness(0,14,0,10); steamIntegration.Children.Add(userCaption);
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
            steamIntegration.Children.Add(userPicker);
            var steamShortcuts=new CheckBox {Name="SteamShortcutsSetting",Content="Include non-Steam games",IsChecked=settings.IncludeSteamShortcuts,Margin=new Thickness(0,0,0,8)}; steamIntegration.Children.Add(steamShortcuts);
            steamIntegration.Children.Add(Label("Non-Steam game launch mode",14,mint));
            var steamShortcutMode=new SettingsComboBox {Name="SteamShortcutModeSetting",ItemsSource=new[]{new KeyValuePair<string,string>("steam","Through Steam"),new KeyValuePair<string,string>("direct","Direct launch")},DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.SteamShortcutLaunchMode=="direct" ? "direct" : "steam",Padding=new Thickness(8),Margin=new Thickness(0,8,0,12)}; steamIntegration.Children.Add(steamShortcutMode);
            Action updateSteamChoices=() => {steamShortcuts.IsEnabled=steamEnabled.IsChecked==true; steamShortcutMode.IsEnabled=true; steamShortcutMode.Visibility=Visibility.Visible;};
            bool savingSteamChoices=false;
            Action saveSteamChoices=() => {
                if(savingSteamChoices) return; savingSteamChoices=true;
                try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); next.SteamEnabled=steamEnabled.IsChecked==true; next.IncludeSteamShortcuts=steamShortcuts.IsChecked==true; next.SteamShortcutLaunchMode=steamShortcutMode.SelectedValue as string ?? "steam"; Storage.Write("settings.json",next); settings=next; RefreshLibrary(true);}
                catch(Exception error) {steamEnabled.IsChecked=settings.SteamEnabled; steamShortcuts.IsChecked=settings.IncludeSteamShortcuts; steamShortcutMode.SelectedValue=settings.SteamShortcutLaunchMode ?? "steam"; ThemedMessage.Show(dialog,error.Message,"Steam settings");}
                finally {savingSteamChoices=false; updateSteamChoices();}
            };
            steamEnabled.Click+=(s,e) => saveSteamChoices(); steamShortcuts.Click+=(s,e) => saveSteamChoices(); steamShortcutMode.SelectionChanged+=(s,e) => saveSteamChoices(); updateSteamChoices();
            var gogIntegration=addIntegration("GOG Galaxy","GogIntegrationSection");
            var gogToggle=new CheckBox {Content="Include GOG Galaxy games",IsChecked=settings.GogEnabled,Foreground=white,Margin=new Thickness(0,12,0,12)};
            gogToggle.Click+=(s,e) => {settings.GogEnabled=gogToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; gogIntegration.Children.Add(gogToggle);
            AddLauncherPathField(gogIntegration,dialog,"GOG Galaxy","GogPathSetting",s => s.GogLauncherPath,(s,path) => s.GogLauncherPath=path,GogLibrary.DetectClient,new[]{"GalaxyClient.exe"});
            var epicIntegration=addIntegration("Epic Games Store","EpicIntegrationSection");
            var epicToggle=new CheckBox {Content="Include Epic Games Store games",IsChecked=settings.EpicEnabled,Foreground=white,Margin=new Thickness(0,12,0,12)};
            epicToggle.Click+=(s,e) => {settings.EpicEnabled=epicToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; epicIntegration.Children.Add(epicToggle);
            var epicPathCaption=Label("Epic Games Launcher path",14,mint); epicPathCaption.Margin=new Thickness(0,10,0,8); epicIntegration.Children.Add(epicPathCaption);
            var epicPathRow=new DockPanel();
            var browseEpic=new Button {Content="Browse…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(browseEpic,Dock.Right); epicPathRow.Children.Add(browseEpic);
            var epicPathField=new TextBox {Foreground=muted,Text=DisplayLauncherPath(settings.EpicLauncherPath ?? EpicLibrary.DetectLauncher()),Padding=new Thickness(8)};
            var epicPathBox=new Grid(); epicPathBox.Children.Add(epicPathField);
            string epicDetectedPath=DisplayLauncherPath(EpicLibrary.DetectLauncher());
            var epicPlaceholder=Label(String.IsNullOrEmpty(epicDetectedPath) ? "Automatic detection" : epicDetectedPath,12,muted); epicPlaceholder.Margin=new Thickness(19,0,9,0); epicPlaceholder.IsHitTestVisible=false; epicPathBox.Children.Add(epicPlaceholder);
            epicPathField.TextChanged+=(s,e) => epicPlaceholder.Visibility=String.IsNullOrEmpty(epicPathField.Text) ? Visibility.Visible : Visibility.Collapsed;
            epicPlaceholder.Visibility=String.IsNullOrEmpty(epicPathField.Text) ? Visibility.Visible : Visibility.Collapsed; epicPathRow.Children.Add(epicPathBox); epicIntegration.Children.Add(epicPathRow);
            var epicPathStatus=Label("",12,muted); epicPathStatus.TextWrapping=TextWrapping.Wrap;
            string detectedEpic=EpicLibrary.DetectLauncher(); epicPathStatus.Text=String.IsNullOrEmpty(settings.EpicLauncherPath) ? String.IsNullOrEmpty(detectedEpic) ? "Automatic: uses Epic's registered launch link." : "" : "Custom launcher path saved.";
            var saveEpic=new Button {Content="Save path",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,8)};
            saveEpic.Click+=(s,e) => {
                try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); next.EpicLauncherPath=EpicLibrary.ValidateLauncher(epicPathField.Text); Storage.Write("settings.json",next); settings=next; epicPathField.Text=DisplayLauncherPath(settings.EpicLauncherPath ?? EpicLibrary.DetectLauncher()); epicPathStatus.Text=String.IsNullOrEmpty(settings.EpicLauncherPath) ? "Automatic detection enabled." : "Custom launcher path saved."; RefreshLibrary(true); ShowLauncherPathCheck(epicPathStatus,"Epic Games Store",settings.EpicLauncherPath ?? EpicLibrary.DetectLauncher());}
                catch(Exception error) {ShowLauncherPathError(epicPathStatus,"Epic Games Store",error);}
            };
            browseEpic.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="Epic Games Launcher|EpicGamesLauncher.exe",CheckFileExists=true,FileName="EpicGamesLauncher.exe"}; if(file.ShowDialog(dialog)==true) {epicPathField.Text=file.FileName; saveEpic.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}};
            epicIntegration.Children.Add(saveEpic); epicIntegration.Children.Add(epicPathStatus);
            var eaIntegration=addIntegration("EA App","EaIntegrationSection");
            var eaToggle=new CheckBox {Content="Include EA App games",IsChecked=settings.EaEnabled,Foreground=white,Margin=new Thickness(0,12,0,12)};
            eaToggle.Click+=(s,e) => {settings.EaEnabled=eaToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; eaIntegration.Children.Add(eaToggle);
            var eaCaption=Label("EA App path",14,mint); eaCaption.Margin=new Thickness(0,10,0,8); eaIntegration.Children.Add(eaCaption);
            var eaRow=new DockPanel(); var browseEa=new Button {Content="Browse…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(browseEa,Dock.Right); eaRow.Children.Add(browseEa);
            var eaPath=new TextBox {Foreground=muted,Text=DisplayLauncherPath(settings.EaLauncherPath ?? EaLibrary.DetectClient()),Padding=new Thickness(8),ToolTip="Leave empty for automatic detection; choose EALauncher.exe to override."};
            var eaField=new Grid(); eaField.Children.Add(eaPath); string eaDetectedPath=DisplayLauncherPath(EaLibrary.DetectClient()); var eaHint=Label(String.IsNullOrEmpty(eaDetectedPath) ? "Automatic detection" : eaDetectedPath,12,muted); eaHint.Margin=new Thickness(19,0,9,0); eaHint.IsHitTestVisible=false; eaField.Children.Add(eaHint);
            eaPath.TextChanged+=(s,e) => eaHint.Visibility=String.IsNullOrEmpty(eaPath.Text) ? Visibility.Visible : Visibility.Collapsed; eaHint.Visibility=String.IsNullOrEmpty(eaPath.Text) ? Visibility.Visible : Visibility.Collapsed;
            eaRow.Children.Add(eaField); eaIntegration.Children.Add(eaRow);
            var eaStatus=Label("",12,muted); eaStatus.TextWrapping=TextWrapping.Wrap;
            Action updateEaStatus=() => {
                string detected=EaLibrary.DetectClient();
                eaStatus.Text=String.IsNullOrEmpty(settings.EaLauncherPath) ? String.IsNullOrEmpty(detected) ? "Automatic: uses EA's registered launch link." : "" : "Custom launcher path saved.";
            }; updateEaStatus();
            var saveEa=new Button {Content="Save path",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,8)};
            saveEa.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); next.EaLauncherPath=EaLibrary.ValidateClient(eaPath.Text); Storage.Write("settings.json",next); settings=next; eaPath.Text=DisplayLauncherPath(settings.EaLauncherPath ?? EaLibrary.DetectClient()); updateEaStatus(); RefreshLibrary(true); ShowLauncherPathCheck(eaStatus,"EA App",settings.EaLauncherPath ?? EaLibrary.DetectClient());} catch(Exception error) {ShowLauncherPathError(eaStatus,"EA App",error);}};
            browseEa.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="EA App launcher|EALauncher.exe",CheckFileExists=true,FileName="EALauncher.exe"}; if(file.ShowDialog(dialog)==true) {eaPath.Text=file.FileName; saveEa.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}}; eaIntegration.Children.Add(saveEa); eaIntegration.Children.Add(eaStatus);
            var ubisoftIntegration=addIntegration("Ubisoft Connect","UbisoftIntegrationSection");
            var ubisoftToggle=new CheckBox {Content="Include Ubisoft Connect games",IsChecked=settings.UbisoftEnabled,Margin=new Thickness(0,12,0,12)};
            ubisoftToggle.Click+=(s,e) => {settings.UbisoftEnabled=ubisoftToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; ubisoftIntegration.Children.Add(ubisoftToggle);
            var ubisoftCaption=Label("Ubisoft Connect path",14,mint); ubisoftCaption.Margin=new Thickness(0,10,0,8); ubisoftIntegration.Children.Add(ubisoftCaption);
            var ubisoftRow=new DockPanel(); var browseUbisoft=new Button {Content="Browse…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(browseUbisoft,Dock.Right); ubisoftRow.Children.Add(browseUbisoft);
            var ubisoftPath=new TextBox {Foreground=muted,Text=DisplayLauncherPath(settings.UbisoftLauncherPath ?? UbisoftLibrary.DetectClient()),Padding=new Thickness(8)};
            var ubisoftField=new Grid(); ubisoftField.Children.Add(ubisoftPath); string ubisoftDetectedPath=DisplayLauncherPath(UbisoftLibrary.DetectClient()); var ubisoftHint=Label(String.IsNullOrEmpty(ubisoftDetectedPath) ? "Automatic detection" : ubisoftDetectedPath,12,muted); ubisoftHint.Margin=new Thickness(19,0,9,0); ubisoftHint.IsHitTestVisible=false; ubisoftField.Children.Add(ubisoftHint);
            ubisoftPath.TextChanged+=(s,e) => ubisoftHint.Visibility=String.IsNullOrEmpty(ubisoftPath.Text) ? Visibility.Visible : Visibility.Collapsed; ubisoftHint.Visibility=String.IsNullOrEmpty(ubisoftPath.Text) ? Visibility.Visible : Visibility.Collapsed; ubisoftRow.Children.Add(ubisoftField); ubisoftIntegration.Children.Add(ubisoftRow);
            var ubisoftStatus=Label("",12,muted); ubisoftStatus.TextWrapping=TextWrapping.Wrap;
            Action updateUbisoftStatus=() => {string detected=UbisoftLibrary.DetectClient(); ubisoftStatus.Text=String.IsNullOrEmpty(settings.UbisoftLauncherPath) ? String.IsNullOrEmpty(detected) ? "Automatic: uses Ubisoft's registered launch link." : "" : "Custom launcher path saved.";}; updateUbisoftStatus();
            var saveUbisoft=new Button {Content="Save path",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,8)};
            saveUbisoft.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); next.UbisoftLauncherPath=UbisoftLibrary.ValidateClient(ubisoftPath.Text); Storage.Write("settings.json",next); settings=next; ubisoftPath.Text=DisplayLauncherPath(settings.UbisoftLauncherPath ?? UbisoftLibrary.DetectClient()); updateUbisoftStatus(); RefreshLibrary(true); ShowLauncherPathCheck(ubisoftStatus,"Ubisoft Connect",settings.UbisoftLauncherPath ?? UbisoftLibrary.DetectClient());} catch(Exception error) {ShowLauncherPathError(ubisoftStatus,"Ubisoft Connect",error);}};
            browseUbisoft.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="Ubisoft Connect|UbisoftConnect.exe;upc.exe;UbisoftGameLauncher.exe;UbisoftGameLauncher64.exe",CheckFileExists=true,FileName="UbisoftConnect.exe"}; if(file.ShowDialog(dialog)==true) {ubisoftPath.Text=file.FileName; saveUbisoft.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}}; ubisoftIntegration.Children.Add(saveUbisoft); ubisoftIntegration.Children.Add(ubisoftStatus);
            var battleNetIntegration=addIntegration("Battle.net","BattleNetIntegrationSection");
            var battleNetToggle=new CheckBox {Content="Include Battle.net games",IsChecked=settings.BattleNetEnabled,Margin=new Thickness(0,12,0,12)};
            battleNetToggle.Click+=(s,e) => {settings.BattleNetEnabled=battleNetToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);};
            battleNetIntegration.Children.Add(battleNetToggle);
            AddLauncherPathField(battleNetIntegration,dialog,"Battle.net","BattleNetPathSetting",s => s.BattleNetLauncherPath,(s,path) => s.BattleNetLauncherPath=path,BattleNetLibrary.DetectClient,new[]{"Battle.net.exe","Battle.net Launcher.exe"});
            var rockstarIntegration=addIntegration("Rockstar Games Launcher","RockstarIntegrationSection");
            var rockstarToggle=new CheckBox {Content="Include Rockstar Games Launcher games",IsChecked=settings.RockstarEnabled,Margin=new Thickness(0,12,0,12)};
            rockstarToggle.Click+=(s,e) => {settings.RockstarEnabled=rockstarToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; rockstarIntegration.Children.Add(rockstarToggle);
            var rockstarCaption=Label("Rockstar Games Launcher path",14,mint); rockstarCaption.Margin=new Thickness(0,10,0,8); rockstarIntegration.Children.Add(rockstarCaption);
            var rockstarRow=new DockPanel(); var browseRockstar=new Button {Content="Browse…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; DockPanel.SetDock(browseRockstar,Dock.Right); rockstarRow.Children.Add(browseRockstar);
            var rockstarPath=new TextBox {Foreground=muted,Text=DisplayLauncherPath(settings.RockstarLauncherPath ?? RockstarLibrary.DetectClient()),Padding=new Thickness(8)};
            var rockstarField=new Grid(); rockstarField.Children.Add(rockstarPath); string rockstarDetectedPath=DisplayLauncherPath(RockstarLibrary.DetectClient()); var rockstarHint=Label(String.IsNullOrEmpty(rockstarDetectedPath) ? "Automatic detection" : rockstarDetectedPath,12,muted); rockstarHint.Margin=new Thickness(19,0,9,0); rockstarHint.IsHitTestVisible=false; rockstarField.Children.Add(rockstarHint);
            foreach(var hint in new[]{epicPlaceholder,eaHint,ubisoftHint,rockstarHint}) {
                hint.TextTrimming=TextTrimming.CharacterEllipsis;
                hint.HorizontalAlignment=HorizontalAlignment.Stretch;
            }
            epicPathBox.ToolTip=epicPlaceholder.Text;
            eaField.ToolTip=eaHint.Text;
            ubisoftField.ToolTip=ubisoftHint.Text;
            rockstarField.ToolTip=rockstarHint.Text;
            rockstarPath.TextChanged+=(s,e) => rockstarHint.Visibility=String.IsNullOrEmpty(rockstarPath.Text) ? Visibility.Visible : Visibility.Collapsed; rockstarHint.Visibility=String.IsNullOrEmpty(rockstarPath.Text) ? Visibility.Visible : Visibility.Collapsed; rockstarRow.Children.Add(rockstarField); rockstarIntegration.Children.Add(rockstarRow);
            var rockstarStatus=Label("",12,muted); rockstarStatus.TextWrapping=TextWrapping.Wrap;
            Action updateRockstarStatus=() => {string detected=RockstarLibrary.DetectClient(); rockstarStatus.Text=String.IsNullOrEmpty(settings.RockstarLauncherPath) ? String.IsNullOrEmpty(detected) ? "Rockstar Games Launcher not detected. Choose its path above." : "" : "Custom launcher path saved.";}; updateRockstarStatus();
            var saveRockstar=new Button {Content="Save path",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,8)};
            saveRockstar.Click+=(s,e) => {try {var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); next.RockstarLauncherPath=RockstarLibrary.ValidateClient(rockstarPath.Text); Storage.Write("settings.json",next); settings=next; rockstarPath.Text=DisplayLauncherPath(settings.RockstarLauncherPath ?? RockstarLibrary.DetectClient()); updateRockstarStatus(); RefreshLibrary(true); ShowLauncherPathCheck(rockstarStatus,"Rockstar Games Launcher",settings.RockstarLauncherPath ?? RockstarLibrary.DetectClient());} catch(Exception error) {ShowLauncherPathError(rockstarStatus,"Rockstar Games Launcher",error);}};
            browseRockstar.Click+=(s,e) => {var file=new Microsoft.Win32.OpenFileDialog {Filter="Rockstar Games Launcher|Launcher.exe",CheckFileExists=true,FileName="Launcher.exe"}; if(file.ShowDialog(dialog)==true) {rockstarPath.Text=file.FileName; saveRockstar.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));}}; rockstarIntegration.Children.Add(saveRockstar); rockstarIntegration.Children.Add(rockstarStatus);
            foreach(var launcherToggle in new[]{gogToggle,epicToggle,eaToggle,ubisoftToggle,battleNetToggle,rockstarToggle})
                launcherToggle.Margin=new Thickness(0,26,0,12);
            SetLauncherDetectionNote(steamStatus,"Steam",steamPath.Text);
            SetLauncherDetectionNote(epicPathStatus,"Epic Games Store",settings.EpicLauncherPath ?? EpicLibrary.DetectLauncher());
            SetLauncherDetectionNote(eaStatus,"EA App",settings.EaLauncherPath ?? EaLibrary.DetectClient());
            SetLauncherDetectionNote(ubisoftStatus,"Ubisoft Connect",settings.UbisoftLauncherPath ?? UbisoftLibrary.DetectClient());
            SetLauncherDetectionNote(rockstarStatus,"Rockstar Games Launcher",settings.RockstarLauncherPath ?? RockstarLibrary.DetectClient());
            AddLauncherReimport(steamIntegration,"steam","Steam",() => settings.SteamEnabled);
            var importSteam=new Button {Name="ImportSteamShortcuts",Content="Import Steam shortcuts",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};
            importSteam.Click+=(s,e) => ShowSteamShortcutImport(dialog,rebuild); steamIntegration.Children.Add(importSteam);
            AddLauncherReimport(gogIntegration,"gog","GOG Galaxy",() => settings.GogEnabled);
            AddLauncherReimport(epicIntegration,"epic","Epic Games Store",() => settings.EpicEnabled);
            AddLauncherReimport(eaIntegration,"ea","EA App",() => settings.EaEnabled);
            AddLauncherReimport(ubisoftIntegration,"ubisoft","Ubisoft Connect",() => settings.UbisoftEnabled);
            AddLauncherReimport(battleNetIntegration,"battlenet","Battle.net",() => settings.BattleNetEnabled);
            AddLauncherReimport(rockstarIntegration,"rockstar","Rockstar Games Launcher",() => settings.RockstarEnabled);
            var xboxIntegration=addIntegration("Xbox app","XboxIntegrationSection");
            var xboxPathCaption=Label("Xbox app path",14,mint); xboxPathCaption.Margin=new Thickness(0,10,0,8); xboxIntegration.Children.Add(xboxPathCaption);
            var xboxPath=Label(DisplayLauncherPath(XboxLibrary.DetectClient()),12,muted); xboxPath.Name="XboxPathSetting"; if(String.IsNullOrEmpty(xboxPath.Text)) xboxPath.Text="Not detected"; xboxPath.TextWrapping=TextWrapping.Wrap; xboxIntegration.Children.Add(xboxPath);
            var xboxToggle=new CheckBox {Name="XboxEnabledSetting",Content="Include Xbox app games",IsChecked=settings.XboxEnabled,Margin=new Thickness(0,26,0,12)};
            var xboxDetection=Label("",12,muted); SetLauncherDetectionNote(xboxDetection,"Xbox app",XboxLibrary.DetectClient()); if(xboxPath.Text!="Not detected") xboxIntegration.Children.Add(xboxDetection);
            xboxToggle.Click+=(s,e) => {settings.XboxEnabled=xboxToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; xboxIntegration.Children.Add(xboxToggle);
            var xboxNote=Label("Finds installed Xbox / Game Pass games registered for your Windows account. Launching is managed by Windows; no executable path is needed. EA Play and Battle.net games use their own integrations.",12,muted); xboxNote.TextWrapping=TextWrapping.Wrap; xboxIntegration.Children.Add(xboxNote);
            AddLauncherReimport(xboxIntegration,"xbox","Xbox app",() => settings.XboxEnabled);
            var sgdbToggle=new CheckBox {Name="SteamGridDbSetting",Content="SteamGridDB artwork API key",IsChecked=settings.SteamGridDbEnabled && !String.IsNullOrWhiteSpace(ArtworkKey.Load()),Foreground=white,Margin=new Thickness(0,12,0,12)};
            var repairPaths=new Button {Content="Repair missing game paths…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,12,0,8)};
            repairPaths.Click+=(s,e) => ShowMissingFileRepair(dialog,rebuild);
            sgdbToggle.Click+=(s,e) => {settings.SteamGridDbEnabled=sgdbToggle.IsChecked==true; QueueSave(); RefreshLibrary(true);}; systemLayout.Children.Add(sgdbToggle);
            systemLayout.Children.Add(Label("Stored encrypted for your Windows account.",12,muted));
            var apiKey=new PasswordBox {Margin=new Thickness(0,8,0,8),Padding=new Thickness(8),PasswordChar='*',Password=ArtworkKey.Load() ?? ""}; systemLayout.Children.Add(apiKey);
            apiKey.GotKeyboardFocus+=(s,e) => apiKey.SelectAll();
            var apiStatus=Label(String.IsNullOrEmpty(apiKey.Password) ? "No key saved. Epic artwork works without a key." : "An encrypted API key is saved.",12,muted); apiStatus.TextWrapping=TextWrapping.Wrap;
            var apiButtons=new StackPanel {Orientation=Orientation.Horizontal};
            var saveKey=new Button {Content="Save & test key",Padding=new Thickness(10,5,10,5)};
            saveKey.Click+=async (s,e) => {
                string key=apiKey.Password.Trim(); if(String.IsNullOrEmpty(key)) {apiStatus.Text="Enter your API key first."; return;}
                saveKey.IsEnabled=false; apiStatus.Text="Checking key…";
                try {await Task.Run(() => ArtworkService.ValidateKey(key)); ArtworkKey.Save(key); apiKey.Password=key; settings.SteamGridDbEnabled=true; sgdbToggle.IsChecked=true; QueueSave(); apiStatus.Text="Key saved. Refreshing artwork…"; RefreshLibrary(true);}
                catch {apiStatus.Text="Could not verify key. Check the key and your connection; previous key was kept.";}
                finally {saveKey.IsEnabled=true;}
            };
            var clearKey=new Button {Content="Remove key",Padding=new Thickness(10,5,10,5),Margin=new Thickness(8,0,0,0)};
            clearKey.Click+=(s,e) => {ArtworkKey.Clear(); apiKey.Clear(); settings.SteamGridDbEnabled=false; sgdbToggle.IsChecked=false; QueueSave(); apiStatus.Text="Key removed. Downloaded artwork stays cached.";};
            var getKey=new Button {Content="Get API key",Padding=new Thickness(10,5,10,5),Margin=new Thickness(8,0,0,0)};
            getKey.Click+=(s,e) => Process.Start(new ProcessStartInfo("https://www.steamgriddb.com/profile/preferences/api") {UseShellExecute=true});
            apiButtons.Children.Add(saveKey); apiButtons.Children.Add(clearKey); apiButtons.Children.Add(getKey); systemLayout.Children.Add(apiButtons); systemLayout.Children.Add(apiStatus);
            systemLayout.Children.Add(repairPaths);
            var restartSetup=new Button {Name="RestartSetupWizard",Content="Restart setup…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,20,0,8)};
            restartSetup.Click+=(s,e) => ShowSetupWizard(true); systemLayout.Children.Add(restartSetup);
            var clearUnusedArtwork=new Button {Name="ClearUnusedCachedArtwork",Content="Clear unused cached artwork…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,18,0,0),HorizontalAlignment=HorizontalAlignment.Left};
            systemLayout.Children.Add(clearUnusedArtwork);
            var unusedArtworkNote=Label("Only deletes downloaded artwork for unregistered games. Registered games keep both SteamGridDB and Libretro artwork, regardless of their source preference. Local images and unidentified older cache files are kept.",12,muted);
            unusedArtworkNote.TextWrapping=TextWrapping.Wrap; unusedArtworkNote.Margin=new Thickness(0,8,0,8); systemLayout.Children.Add(unusedArtworkNote);
            clearUnusedArtwork.Click+=(s,e) => {
                if(scanning) {ThemedMessage.Show(dialog,"Please wait for the library refresh to finish.","Cached artwork"); return;}
                if(!ConfirmEmulatorRemoval(dialog,"Clear unused cached artwork","Clear artwork for unregistered games?",unusedArtworkNote.Text,"Clear unused cached artwork")) return;
                try {int removed=ArtworkService.ClearUnusedCachedArtwork(library.Games,settings); ThemedMessage.Show(dialog,removed+" unused cached artwork files deleted. Registered games’ artwork was kept.","Cached artwork");}
                catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Cached artwork");}
            };
            AddUpdateSettings(systemLayout);
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
        internal string PreferredArtworkPickerSource(Game game) {
            var current=filters.Count>collectionIndex ? filters[collectionIndex] : null;
            if(current!=null && (current.Id=="all" || (current.Games ?? new List<string>()).Contains(game.Id))) return ArtworkService.GameListSource(settings,game,current.Id);
            foreach(var list in CollectionOptions().Where(item => (item.Games ?? new List<string>()).Contains(game.Id))) {
                string source=ArtworkService.GameListSource(settings,game,list.Id);
                if(source!="auto") return source;
            }
            return ArtworkService.GameListSource(settings,game,null);
        }
        void ShowPreferredArtworkPicker(Window owner,Game game) {
            if(ArtworkService.PrimaryArtworkSource(PreferredArtworkPickerSource(game))=="libretro") ShowLibretroArtworkPicker(owner,game);
            else ShowArtworkPicker(owner,game);
        }
        async void ShowArtworkPicker(Window owner,Game game,Action<string> onChoose=null) {
            string key=ArtworkKey.Load();
            var chooser=new Window {Title="Artwork · "+game.Name,Owner=owner,Width=660,Height=600,MinWidth=400,MinHeight=350,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,Topmost=true};
            chooser.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(20)};
            var switchSource=new Button {Name="ArtworkSwitchToLibretro",Content="Choose from Libretro instead…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,0,12)};
            switchSource.Click+=(s,e) => {chooser.Close(); ShowLibretroArtworkPicker(owner,game,onChoose);}; DockPanel.SetDock(switchSource,Dock.Top); root.Children.Add(switchSource);
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
            Action<string> commit=path => {
                if(onChoose==null) {
                    if(artworkCancellation!=null) artworkCancellation.Cancel();
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    if(next.ArtworkOverrides==null) next.ArtworkOverrides=new Dictionary<string,string>(); next.ArtworkOverrides[game.Id]=path;
                    Storage.Write("settings.json",next); settings=next;
                    foreach(var entry in library.Games.Where(g => g.Id==game.Id)) ArtworkService.ApplyCached(entry,settings);
                    RefreshArtworkPresentation();
                } else onChoose(path);
                chooser.Close();
            };
            original.Click+=async (s,e) => {
                if(scanning) {status.Text="Please wait for the library refresh to finish."; return;}
                loadRevision++; original.IsEnabled=false; covers.IsEnabled=false; status.Text="Getting original cover…";
                try {
                    int match=selectedMatch;
                    string path=await Task.Run(() => ArtworkService.SaveChoice(game,ArtworkService.OriginalGrid(game,key,cancellation.Token,match),cancellation.Token));
                    if(cancellation.IsCancellationRequested) return;
                    commit(path);
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
                            commit(path);
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
        void AddLauncherReimport(StackPanel panel,string source,string name,Func<bool> enabled) {
            var result=Label("",12,muted); result.TextWrapping=TextWrapping.Wrap;
            var button=new Button {Content=source=="steam" ? "Restore Steam collections" : "Restore "+name+" games",HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,4,0,8),Padding=new Thickness(12,7,12,7)};
            var restoreNote=Label(source=="steam" ? "Reloads Steam collections and restores detected Steam entries removed from Pad Launcher. Games removed in Steam stay removed." : "Restores installed games removed from Pad Launcher to this launcher's list. Uninstalled games stay removed.",12,muted);
            restoreNote.TextWrapping=TextWrapping.Wrap;
            restoreNote.Margin=new Thickness(0,6,0,4);
            panel.Children.Add(restoreNote);
            button.Margin=new Thickness(0,6,0,8);
            if(source=="steam") {
                var row=new DockPanel {Margin=new Thickness(0,6,0,0)};
                button.Margin=new Thickness(0,0,12,0); DockPanel.SetDock(button,Dock.Left); row.Children.Add(button);
                result.VerticalAlignment=VerticalAlignment.Center; row.Children.Add(result); panel.Children.Add(row);
            } else {panel.Children.Add(button); panel.Children.Add(result);}
            button.Click+=async (sender,args) => {
                if(scanning) {result.Text="Please wait for the current refresh to finish."; return;}
                if(!enabled()) {result.Text="Enable "+name+" games above before restoring."; return;}
                scanning=true; button.IsEnabled=false; result.Text="Restoring…";
                try {
                    var snapshot=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    var fresh=await Task.Run(() => LauncherLibrary.ScanSource(snapshot,source));
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    var updated=Storage.Json().Deserialize<Library>(Storage.Json().Serialize(library));
                    LauncherLibrary.RestoreSource(updated,fresh,next,source);
                    foreach(var game in updated.Games) ArtworkService.ApplyCached(game,next);
                    Storage.Write("settings.json",next); Storage.Write("library.json",updated);
                    settings=next; library=updated; ApplyLibrary(); DownloadArtwork(library);
                    result.Text=source=="steam" ? "Steam collections restored." : name+" games restored.";
                } catch(Exception error) {result.Text="Restore failed. "+error.Message;}
                finally {button.IsEnabled=true; FinishLibraryScan();}
            };
        }
        void ShowSteamShortcutImport(Window owner,Action rebuild) {
            var dialog=new Window {Name="SteamShortcutImportWindow",Title="Import Steam shortcuts",Owner=owner,Width=820,Height=680,MinWidth=620,MinHeight=450,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,Topmost=true};
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label("Import Steam shortcuts",24,white));
            var note=Label("Select shortcuts and their destination lists. Reimporting updates the same games without creating duplicates.\nYou can change the launch mode for non-Steam games under Launchers → Steam settings.",13,new SolidColorBrush(Color.FromRgb(255,218,106)));
            note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,12,0,16); header.Children.Add(note);
            var select=new Button {Content="Select all",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,0,12)}; header.Children.Add(select);
            var destinations=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("source:custom","Custom games"),new KeyValuePair<string,string>("","Keep existing lists")};
            destinations.AddRange(CollectionOptions().Where(c => c.Id!="all" && c.Id!="recent" && c.Id!="most-played" && !c.Id.StartsWith("source:")).Select(c => new KeyValuePair<string,string>(c.Id,c.Name)));
            var bulkOptions=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("bulk","Add all to list…")}; bulkOptions.AddRange(destinations);
            bulkOptions.Add(new KeyValuePair<string,string>("create","Create new list…"));
            var bulk=new SettingsComboBox {Name="SteamShortcutBulkList",ItemsSource=bulkOptions,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue="bulk",Width=220,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,0,12)}; header.Children.Add(bulk);
            var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var status=Label("Reading shortcuts…",12,muted); status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,12,0,12); footer.Children.Add(status);
            var actions=new StackPanel {Orientation=Orientation.Horizontal}; footer.Children.Add(actions);
            var import=new Button {Name="ImportSelectedSteamShortcuts",Content="Import selected",Padding=new Thickness(14,8,14,8),IsEnabled=false}; actions.Children.Add(import);
            var close=new Button {Content="Close",Padding=new Thickness(14,8,14,8),Margin=new Thickness(8,0,0,0)}; actions.Children.Add(close);
            var list=new StackPanel(); root.Children.Add(new ScrollViewer {Content=list,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var rows=new List<Tuple<SteamShortcutRecord,CheckBox,SettingsComboBox>>(); bool busy=false,closed=false;
            Action count=() => status.Text=rows.Count+" shortcuts. "+rows.Count(row => row.Item2.IsChecked==true)+" selected";
            BindSelectionToggle(select,() => rows.Select(row => row.Item2),count);
            close.Click+=(s,e) => dialog.Close(); dialog.Closed+=(s,e) => closed=true; dialog.Closing+=(s,e) => {if(busy) e.Cancel=true;};
            bulk.SelectionChanged+=(s,e) => {
                string value=bulk.SelectedValue as string; if(value==null || value=="bulk") return;
                if(value=="create") {
                    var create=new Window {Title="Create new list",Owner=dialog,Width=440,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=dialog.Background,Foreground=white,FontFamily=FontFamily}; create.Resources.MergedDictionaries.Add(dialog.Resources);
                    var body=new StackPanel {Margin=new Thickness(24)}; create.Content=body; body.Children.Add(Label("List name",14,mint));
                    var name=new TextBox {Margin=new Thickness(0,10,0,16),MaxLength=80}; body.Children.Add(name);
                    var error=Label("",12,new SolidColorBrush(Color.FromRgb(255,113,113))); error.TextWrapping=TextWrapping.Wrap; body.Children.Add(error);
                    var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right}; body.Children.Add(buttons);
                    var cancel=new Button {Content="Cancel",IsCancel=true,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,8,0)}; buttons.Children.Add(cancel);
                    var save=new Button {Content="Create list",IsDefault=true,Padding=new Thickness(12,7,12,7)}; buttons.Children.Add(save);
                    string createdId=null;
                    save.Click+=(sender,args) => {try {
                        string title=name.Text.Trim();
                        if(title.Length==0) throw new ArgumentException("Enter a list name.");
                        if(CollectionOptions().Any(c => String.Equals(c.Name,title,StringComparison.CurrentCultureIgnoreCase))) throw new ArgumentException("A list with that name already exists.");
                        var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                        if(next.CustomLists==null) next.CustomLists=new List<CustomList>();
                        createdId="list:"+Guid.NewGuid().ToString("N"); next.CustomLists.Add(new CustomList {Id=createdId,Name=title,Games=new List<string>()});
                        Storage.Write("settings.json",next); settings=next; create.DialogResult=true;
                    } catch(Exception failure) {error.Text=failure.Message;}};
                    if(create.ShowDialog()!=true) {bulk.SelectedValue="bulk"; return;}
                    value=createdId;
                    var option=new KeyValuePair<string,string>(value,name.Text.Trim()); destinations.Add(option); bulkOptions.Insert(bulkOptions.Count-1,option);
                    foreach(var row in rows) {object previous=row.Item3.SelectedValue; row.Item3.ItemsSource=null; row.Item3.ItemsSource=destinations.ToList(); row.Item3.SelectedValue=previous;}
                    bulk.ItemsSource=null; bulk.ItemsSource=bulkOptions; rebuild();
                }
                foreach(var row in rows) row.Item3.SelectedValue=value;
                bulk.SelectedValue="bulk";
            };
            dialog.Loaded+=async (s,e) => {
                try {
                    var snapshot=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    var records=await Task.Run(() => SteamShortcutImport.Read(snapshot)); if(closed) return;
                    foreach(var record in records) {
                        var previous=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(game => game.SteamShortcutKey==record.Key)
                            ?? SteamShortcutImport.FindDirectMatch(settings,new CustomGame {Target=record.Target,Arguments=record.Arguments,WorkingDirectory=record.WorkingDirectory});
                        var line=new Grid {Margin=new Thickness(0,0,0,14)}; line.ColumnDefinitions.Add(new ColumnDefinition()); line.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(220)});
                        var text=new StackPanel {Margin=new Thickness(0,0,12,0)}; line.Children.Add(text);
                        var box=new CheckBox {Content=record.Name,IsChecked=true}; text.Children.Add(box);
                        string availability=previous!=null ? "Already added · updates existing entry" : library.Games.Any(game => game.Shortcut && game.LaunchId==record.SteamLaunchId) ? "Available from Steam · one entry will be kept" : "New shortcut";
                        var detail=Label(availability+"\n"+record.Target+(!record.CanLaunchDirect ? "\n"+record.DirectWarning : ""),12,muted); detail.TextWrapping=TextWrapping.Wrap; detail.Margin=new Thickness(29,5,0,0); text.Children.Add(detail);
                        var destination=new SettingsComboBox {ItemsSource=destinations.ToList(),DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=previous!=null || library.Games.Any(game => game.Shortcut && game.LaunchId==record.SteamLaunchId) ? "" : "source:custom",VerticalAlignment=VerticalAlignment.Top}; Grid.SetColumn(destination,1); line.Children.Add(destination);
                        rows.Add(Tuple.Create(record,box,destination)); box.Checked+=(sender,args) => count(); box.Unchecked+=(sender,args) => count(); list.Children.Add(line);
                    }
                    import.IsEnabled=rows.Count>0; count();
                } catch(Exception error) {if(!closed) status.Text="Could not read shortcuts. "+error.Message;}
            };
            import.Click+=async (s,e) => {
                if(scanning) {status.Text="Please wait for the current library refresh to finish."; return;}
                var selected=rows.Where(row => row.Item2.IsChecked==true).ToList();
                if(selected.Count==0) {status.Text="Select at least one shortcut."; return;}
                busy=true; header.IsEnabled=list.IsEnabled=import.IsEnabled=close.IsEnabled=false; status.Text="Importing shortcuts…";
                try {
                    var prepared=await Task.Run(() => selected.Select(row => SteamShortcutImport.Prepare(row.Item1)).ToList());
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    foreach(var game in prepared) SteamShortcutImport.Merge(next,game);
                    SteamShortcutImport.PreserveLists(next,library,prepared,selected.ToDictionary(row => row.Item1.Key,row => row.Item3.SelectedValue as string));
                    var updated=Storage.Json().Deserialize<Library>(Storage.Json().Serialize(library)); CustomGameLibrary.Apply(updated,next);
                    foreach(var game in updated.Games) ArtworkService.ApplyCached(game,next);
                    Storage.Write("settings.json",next); Storage.Write("library.json",updated);
                    settings=next; library=updated; ApplyLibrary(); rebuild(); DownloadArtwork(library);
                    busy=false; dialog.Close();
                } catch(Exception error) {status.Text="Import failed. "+error.Message;}
                finally {busy=false; if(!closed) header.IsEnabled=list.IsEnabled=import.IsEnabled=close.IsEnabled=true;}
            };
            dialog.Show();
        }

        static void BindSelectionToggle(Button button,Func<IEnumerable<CheckBox>> items,Action changed=null,string suffix="",string selectText=null,string unselectText=null) {
            Action update=() => {
                var boxes=items().Where(b => b.IsEnabled).ToList();
                string caption=boxes.Count>0 && boxes.All(b => b.IsChecked==true) ? (unselectText ?? "Unselect all"+suffix) : (selectText ?? "Select all"+suffix);
                if(!Object.Equals(button.Content,caption)) button.Content=caption;
            };
            button.Click+=(s,e) => {
                var boxes=items().Where(b => b.IsEnabled).ToList();
                bool value=!boxes.All(b => b.IsChecked==true);
                foreach(var box in boxes) box.IsChecked=value;
                if(changed!=null) changed(); update();
            };
            button.LayoutUpdated+=(s,e) => update();
        }
        int customGamesTabIndex;
        string selectedCustomGameId;
        StackPanel CreateCustomGamesPanel(Window dialog,Action rebuild) {
            var panel=new StackPanel();
            var heading=Label("Custom games",26,white); heading.FontWeight=FontWeights.SemiBold; heading.Margin=new Thickness(0,0,0,20); panel.Children.Add(heading);
            var tabs=new TabControl {Name="CustomGamesTabs",Background=dialog.Background,BorderThickness=new Thickness(0),Foreground=white};
            StyleSettingsSubtabs(tabs);
            tabs.Items.Add(new TabItem {Header="Add New",Content=CreateCustomGameEditor(dialog,rebuild,false)});
            tabs.Items.Add(new TabItem {Header="Added Games",Content=CreateCustomGameEditor(dialog,rebuild,true)});
            tabs.SelectedIndex=Math.Max(0,Math.Min(1,customGamesTabIndex));
            tabs.SelectionChanged+=(s,e) => {if(e.Source==tabs && tabs.SelectedIndex>=0) customGamesTabIndex=tabs.SelectedIndex;};
            panel.Children.Add(tabs); return panel;
        }
        void StyleSettingsSubtabs(TabControl tabs,bool compact=false) {
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
                    <Border x:Name='Segment' Background='{TemplateBinding Background}' CornerRadius='8' Padding='"+(compact ? "10,5" : "15,10")+@"' MinWidth='"+(compact ? "78" : "105")+@"' BorderThickness='1' BorderBrush='Transparent'>
                        <ContentPresenter ContentSource='Header' HorizontalAlignment='Center' VerticalAlignment='Center'/>
                    </Border>
                    <ControlTemplate.Triggers>
                        <Trigger Property='IsMouseOver' Value='True'><Setter TargetName='Segment' Property='Background' Value='#2B414B'/><Setter Property='Foreground' Value='#ECF3F2'/></Trigger>
                        <Trigger Property='IsSelected' Value='True'><Setter TargetName='Segment' Property='Background' Value='#A3F0CD'/><Setter Property='Foreground' Value='#0D1922'/></Trigger>
                        <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='Segment' Property='BorderBrush' Value='#ECF3F2'/></Trigger>
                    </ControlTemplate.Triggers>
                </ControlTemplate></Setter.Value></Setter>
            </Style>");
        }
        StackPanel CreateCustomGameEditor(Window dialog,Action rebuild,bool editing) {
            var panel=new StackPanel();
            panel.Margin=new Thickness(0,16,0,0);
            var options=new List<KeyValuePair<string,string>>();
            if(editing) foreach(var game in (settings.CustomGames ?? new List<CustomGame>()).OrderBy(g => g.Name)) options.Add(new KeyValuePair<string,string>(game.Id,game.Name));
            if(editing && options.Count==0) {panel.Children.Add(Label("No custom games yet. Add one in Add New.",14,muted)); return panel;}
            var picker=new SettingsComboBox {Name="CustomGamePicker",ItemsSource=options,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedIndex=0,Padding=new Thickness(8)};
            if(editing && options.Any(option => option.Key==selectedCustomGameId)) picker.SelectedValue=selectedCustomGameId;
            if(editing) panel.Children.Add(picker);
            var destinations=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("","None")};
            var editableIds=new HashSet<string>((settings.CustomLists ?? new List<CustomList>()).Select(list => list.Id)
                .Concat((settings.CombinedCollections ?? new List<CombinedCollection>()).Select(list => list.Id)));
            editableIds.Add("source:custom");
            destinations.AddRange(CollectionOptions().Where(list => editableIds.Contains(list.Id) && (list.Id=="source:custom" || !CustomLists.IsEmulatorOnlyList(settings,library,list.Id))).Select(list => new KeyValuePair<string,string>(list.Id,list.Name)));
            SettingsComboBox defaultListPicker=null;
            if(!editing) {
                var defaultCaption=Label("Automatically add new custom games to this list",14,mint); defaultCaption.Margin=new Thickness(0,16,0,8); panel.Children.Add(defaultCaption);
                var defaultList=new SettingsComboBox {Name="DefaultCustomGameList",ItemsSource=destinations,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=settings.DefaultCustomGameListId ?? "",Padding=new Thickness(8)};
                defaultListPicker=defaultList;
                if(defaultList.SelectedIndex<0) defaultList.SelectedIndex=0;
                panel.Children.Add(defaultList);
                defaultList.SelectionChanged+=(s,e) => {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    next.DefaultCustomGameListId=defaultList.SelectedValue as string;
                    try {Storage.Write("settings.json",next); settings=next;}
                    catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Custom games");}
                };
            }
            var listCaption=Label(editing ? "Game's list" : "Add to list",14,mint); listCaption.Margin=new Thickness(0,16,0,8); panel.Children.Add(listCaption);
            var gameDestinations=destinations.ToList();
            if(editing) gameDestinations.Insert(1,new KeyValuePair<string,string>("keep-memberships","Keep existing list memberships"));
            var gameList=new SettingsComboBox {Name="CustomGameList",ItemsSource=gameDestinations,DisplayMemberPath="Value",SelectedValuePath="Key",Padding=new Thickness(8)};
            panel.Children.Add(gameList);
            string originalListSelection=null;
            if(defaultListPicker!=null) defaultListPicker.SelectionChanged+=(s,e) => gameList.SelectedValue=defaultListPicker.SelectedValue;
            Func<string,string,TextBox> field=(title,name) => {
                var caption=Label(title,14,mint); caption.Margin=new Thickness(0,16,0,8); panel.Children.Add(caption);
                var box=new TextBox {Name=name,Padding=new Thickness(8),FontSize=14}; panel.Children.Add(box); return box;
            };
            var gameName=field("Game name","CustomGameName"); gameName.MaxLength=120;
            var target=field("Executable or shortcut","CustomGameTarget");
            var steamMode=new CheckBox {Content="Launch through Steam",Margin=new Thickness(0,10,0,0),Visibility=Visibility.Collapsed}; panel.Children.Add(steamMode);
            var linkedSteamNote=Label("Launch mode is managed under Launchers → Steam settings.",12,muted); linkedSteamNote.TextWrapping=TextWrapping.Wrap; linkedSteamNote.Margin=new Thickness(0,8,0,0); linkedSteamNote.Visibility=Visibility.Collapsed; panel.Children.Add(linkedSteamNote);
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
            Action checkFiles=() => {
                missingFiles.Text=steamMode.IsChecked!=true && !String.IsNullOrWhiteSpace(target.Text) && !File.Exists(target.Text.Trim().Trim('"')) ? "Game file missing — update the path." : "";
            };
            target.TextChanged+=(s,e) => checkFiles();
            steamMode.Checked+=(s,e) => checkFiles(); steamMode.Unchecked+=(s,e) => checkFiles();
            Action<Settings> commit=next => {
                Storage.Write("settings.json",next); settings=next;
                if(artworkCancellation!=null) artworkCancellation.Cancel();
                CustomGameLibrary.Apply(library,settings);
                foreach(var game in library.Games) ArtworkService.ApplyCached(game,settings);
                library.Games=library.Games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                ApplyLibrary(); rebuild(); DownloadArtwork(library);
            };
            var actions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,32,0,0)};
            var save=new Button {Name="SaveCustomGame",Content="Add game",Padding=new Thickness(14,7,14,7)};
            var remove=new Button {Content="Remove game",Padding=new Thickness(14,7,14,7),Margin=new Thickness(8,0,0,0),Visibility=Visibility.Collapsed}; actions.Children.Add(save); actions.Children.Add(remove); panel.Children.Add(actions);
            var removalHint=Label("Removing an entry keeps your game files.",12,muted); removalHint.TextWrapping=TextWrapping.Wrap; removalHint.Margin=new Thickness(0,8,0,0); removalHint.Visibility=editing ? Visibility.Visible : Visibility.Collapsed; panel.Children.Add(removalHint);
            Action loadEntry=() => {
                var entry=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.Id==(picker.SelectedValue as string));
                bool imported=entry!=null && SteamShortcutIdentity.IsImported(entry);
                steamMode.Visibility=entry!=null && !String.IsNullOrEmpty(entry.SteamLaunchId) && !imported ? Visibility.Visible : Visibility.Collapsed;
                steamMode.IsChecked=entry!=null && SteamShortcutIdentity.ThroughSteam(settings,entry);
                linkedSteamNote.Visibility=imported ? Visibility.Visible : Visibility.Collapsed;
                gameName.Text=entry==null ? "" : entry.Name; target.Text=entry==null ? "" : entry.Target; arguments.Text=entry==null ? "" : entry.Arguments; folder.Text=entry==null ? "" : entry.WorkingDirectory;
                save.Content=entry==null ? "Add game" : "Save changes"; remove.Visibility=entry==null ? Visibility.Collapsed : Visibility.Visible;
                if(entry==null) {
                    gameList.SelectedValue=settings.DefaultCustomGameListId ?? "";
                    if(gameList.SelectedIndex<0) gameList.SelectedIndex=0;
                } else {
                    var memberships=CollectionOptions().Where(list => editableIds.Contains(list.Id) && (list.Games ?? new List<string>()).Contains(entry.Id)).ToList();
                    if(memberships.Count>1) {
                        var updatedDestinations=destinations.ToList();
                        updatedDestinations.Insert(1,new KeyValuePair<string,string>("keep-memberships","Multiple lists: "+String.Join(", ",memberships.Select(list => list.Name))));
                        gameList.ItemsSource=updatedDestinations;
                    } else gameList.ItemsSource=gameDestinations;
                    gameList.SelectedValue=memberships.Count==0 ? "" : memberships.Count==1 ? memberships[0].Id : "keep-memberships";
                    selectedCustomGameId=entry.Id;
                }
                originalListSelection=gameList.SelectedValue as string;
            };
            picker.SelectionChanged+=(s,e) => loadEntry(); loadEntry();
            save.Click+=(s,e) => {
                try {
                    if(scanning) throw new InvalidOperationException("Please wait for the current library refresh to finish.");
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    string id=picker.SelectedValue as string;
                    var old=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.Id==id);
                    var draft=new CustomGame {Id=String.IsNullOrEmpty(id) ? null : id,Name=gameName.Text,Target=target.Text,Arguments=arguments.Text,WorkingDirectory=folder.Text,Artwork=old==null ? null : old.Artwork,SteamShortcutKey=old==null ? null : old.SteamShortcutKey,SteamLaunchId=old==null ? null : old.SteamLaunchId,LaunchViaSteam=steamMode.IsChecked==true};
                    if(!String.IsNullOrEmpty(draft.SteamLaunchId)) draft.PlayniteActionInfo=SteamShortcutImport.Describe(draft);
                    string gameId=CustomGameLibrary.Save(next,draft);
                    string destination=gameList.SelectedValue as string;
                    if(destination!="keep-memberships" && (!editing || destination!=originalListSelection)) {
                        var working=new Library {Games=library.Games.ToList(),Collections=library.Collections.ToList()};
                        CustomGameLibrary.Apply(working,next);
                        CustomLists.SetGameList(next,working,gameId,destination);
                    }
                    if(next.ArtworkOverrides!=null && (old==null || !String.Equals(CustomGameLibrary.ArtworkPath(old.Artwork),CustomGameLibrary.ArtworkPath(draft.Artwork),StringComparison.OrdinalIgnoreCase))) next.ArtworkOverrides.Remove(gameId);
                    if(editing) {customGamesTabIndex=1; selectedCustomGameId=gameId;}
                    commit(next);
                } catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Custom games");}
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
                } catch(Exception error) {ThemedMessage.Show(dialog,error.Message,"Custom games");}
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
            carouselHost.Margin = new Thickness(0,205,0,195); listContent.Children.Add(carousel); Panel.SetZIndex(listContent,1); carouselHost.Children.Add(listContent); scene.Children.Add(carouselHost);
            title.Name="SelectedGameTitle"; title.Foreground = white; title.FontSize = 28; title.FontWeight = FontWeights.Medium;
            title.TextTrimming = TextTrimming.CharacterEllipsis; title.TextAlignment = TextAlignment.Center;
            title.HorizontalAlignment = HorizontalAlignment.Center; title.VerticalAlignment = VerticalAlignment.Top;
            title.MaxWidth = 1000; title.Margin = new Thickness(0,726,0,0);
            title.Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius=16,ShadowDepth=1,Opacity=.95 };
            title.Visibility = settings.ShowGameTitle ? Visibility.Visible : Visibility.Collapsed;
            scene.Children.Add(title);
            launchCountSubtitle.Name="SelectedGameLaunchCount";
            launchCountSubtitle.FontSize=15;
            launchCountSubtitle.Foreground=white;
            launchCountSubtitle.HorizontalAlignment=HorizontalAlignment.Center;
            launchCountSubtitle.VerticalAlignment=VerticalAlignment.Top;
            launchCountSubtitle.IsHitTestVisible=false;
            launchCountSubtitle.SetBinding(UIElement.VisibilityProperty,new System.Windows.Data.Binding("Visibility") {Source=title});
            scene.Children.Add(launchCountSubtitle);
            notice.Background = new SolidColorBrush(Color.FromArgb(235,18,31,40)); notice.CornerRadius = new CornerRadius(8);
            notice.Padding = new Thickness(24,15,24,15); notice.VerticalAlignment = VerticalAlignment.Bottom;
            notice.HorizontalAlignment = HorizontalAlignment.Center; notice.Margin = new Thickness(0,0,0,55);
            notice.MaxWidth = 1050; notice.Visibility = Visibility.Collapsed; noticeText.Foreground = white;
            noticeText.FontSize = 16; noticeText.TextWrapping = TextWrapping.Wrap; notice.Child = noticeText; scene.Children.Add(notice);
        }
        void RestoreTemporaryLabels() {
            if(!temporaryLabels) return;
            temporaryLabels=false;
            collectionText.Visibility=settings.ShowCollectionName ? Visibility.Visible : Visibility.Collapsed;
            title.Visibility=settings.ShowGameTitle ? Visibility.Visible : Visibility.Collapsed;
            if(ActiveLayout=="grid") Render(false);
        }
        public void ShowControls() {
            temporaryLabels=true;
            title.Visibility=Visibility.Visible;
            if(ActiveLayout=="grid") Render(false);
            ShowNotice(Prompts.Help(settings));
        }
        void ShowNotice(string text,double seconds=5) { if(arcOnlyNotice!=null) arcOnlyNotice.Text=text; else {noticeText.Text = text; notice.Visibility = Visibility.Visible;} noticeTimer.Stop(); noticeTimer.Interval=TimeSpan.FromSeconds(seconds); noticeTimer.Start(); }
        void FinishLibraryScan() {
            scanning=false;
            if(!pendingLibraryRefresh) return;
            bool feedback=pendingLibraryRefreshFeedback;
            pendingLibraryRefresh=false;
            pendingLibraryRefreshFeedback=false;
            RefreshLibrary(feedback);
        }
        async void RefreshLibrary(bool feedback = false) {
            if(setupWindow!=null) return;
            if(setupTemplateOnly) {if(!feedback) return; setupTemplateOnly=false;}
            if (demo) { if (feedback) ShowNotice("Preview library · refresh is available with your Steam library."); return; }
            if (scanning) {
                pendingLibraryRefresh=true;
                pendingLibraryRefreshFeedback |= feedback;
                if (feedback) ShowNotice("Refreshing games and lists…");
                return;
            }
            scanning = true;
            if (feedback) ShowNotice("Refreshing games and lists…");
            try {
                string user = settings.UserId;
                var scanSettings=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                var missingCandidates=new List<Tuple<string,string,string,string>>();
                var previous=library;
                var refreshed = await Task.Run(() => {
                    if(scanSettings.RemoveMissingEmulatorGames) {
                        foreach(var preset in EmulatorLibrary.Presets) {
                            var config=EmulatorLibrary.Config(scanSettings,preset.Id);
                            if(config==null) continue;
                            foreach(var game in config.Games ?? new List<EmulatorGame>())
                                if(EmulatorLibrary.ConfirmMissingRom(game.Path)) missingCandidates.Add(Tuple.Create(preset.Id,game.Id,game.Path,game.Name));
                        }
                        foreach(var game in scanSettings.CustomGames ?? new List<CustomGame>())
                            if(CustomGameLibrary.MissingTarget(game)) missingCandidates.Add(Tuple.Create("custom",game.Id,game.Target,game.Name));
                    }
                    var result=LauncherLibrary.Scan(scanSettings);
                    bool changed=Storage.Json().Serialize(previous)!=Storage.Json().Serialize(result);
                    return Tuple.Create(result,changed);
                });
                // A newer request uses newer settings, so do not publish this snapshot.
                if(pendingLibraryRefresh) return;
                if(refreshed.Item2 || libraryCacheNeedsWrite) {
                    libraryCacheNeedsWrite=true;
                    await Task.Run(() => Storage.Write("library.json",refreshed.Item1));
                    // A request can arrive while the cache is being written. The next
                    // scan must rewrite it even if its result matches the live library.
                    if(pendingLibraryRefresh) return;
                    libraryCacheNeedsWrite=false;
                }
                var next=refreshed.Item1;
                var removedNames=new List<string>();
                if(settings.RemoveMissingEmulatorGames && missingCandidates.Count>0) {
                    var cleaned=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    foreach(var candidate in missingCandidates) {
                        if(candidate.Item1=="custom") {
                            var game=(cleaned.CustomGames ?? new List<CustomGame>()).FirstOrDefault(entry => entry.Id==candidate.Item2 && entry.Target==candidate.Item3);
                            if(game==null || !CustomGameLibrary.MissingTarget(game)) continue;
                            cleaned.CustomGames.Remove(game);
                            foreach(var list in cleaned.CustomLists ?? new List<CustomList>()) if(list.Games!=null) list.Games.RemoveAll(id => id==game.Id);
                            foreach(var list in cleaned.CombinedCollections ?? new List<CombinedCollection>()) {
                                if(list.AddedGames!=null) list.AddedGames.RemoveAll(id => id==game.Id);
                                if(list.ExcludedGames!=null) list.ExcludedGames.RemoveAll(id => id==game.Id);
                            }
                            if(cleaned.ArtworkOverrides!=null) cleaned.ArtworkOverrides.Remove(game.Id);
                        } else {
                            var config=EmulatorLibrary.Config(cleaned,candidate.Item1);
                            if(config==null || config.Games==null || !config.Games.Any(game => game.Id==candidate.Item2 && game.Path==candidate.Item3) || !EmulatorLibrary.ConfirmMissingRom(candidate.Item3)) continue;
                            EmulatorLibrary.RemoveGame(cleaned,candidate.Item1,candidate.Item2);
                        }
                        next.Games.RemoveAll(game => game.Id==candidate.Item2);
                        foreach(var list in next.Collections) if(list.Games!=null) list.Games.Remove(candidate.Item2);
                        removedNames.Add(candidate.Item4);
                    }
                    Storage.Write("settings.json",cleaned); settings=cleaned;
                    Storage.Write("library.json",next);
                }
                SteamShortcutIdentity.Reconcile(next,settings);
                library = next; settings.SteamPath = next.SteamPath;
                if(!String.IsNullOrEmpty(user)) settings.UserId=next.UserId;
                if(refreshed.Item2 || feedback || removedNames.Count>0) ApplyLibrary();
                QueueSave();
                if (feedback) ShowNotice("Refreshed · " + filters[collectionIndex].Name + " · " + visible.Count + " games");
                if (library.Warnings.Count > 0) ShowNotice(library.Warnings[0]);
                RefreshMissingArtworkList();
                if(removedNames.Count>0) ShowNotice("Removed missing games: "+String.Join(", ",removedNames));
                DownloadArtwork(next);
            } catch (Exception e) { ShowNotice(e.Message); }
            finally { FinishLibraryScan(); }
        }
        System.Threading.CancellationTokenSource artworkCancellation;
        async void DownloadArtwork(Library batch) {
            if(artworkCancellation!=null) artworkCancellation.Cancel();
            var cancellation=new System.Threading.CancellationTokenSource(); artworkCancellation=cancellation;
            var snapshot=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            var pending=new Dictionary<string,Tuple<Game,string>>();
            bool scheduled=false;
            try {
                await Task.Run(() => ArtworkService.Fill(batch.Games,snapshot,cancellation.Token,(game,path) => {
                    lock(pending) {
                        pending[game.Id]=Tuple.Create(game,path);
                        if(scheduled) return;
                        scheduled=true;
                    }
                    Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => {
                        Tuple<Game,string>[] updates;
                        lock(pending) {updates=pending.Values.ToArray(); pending.Clear(); scheduled=false;}
                        if(cancellation.IsCancellationRequested || library!=batch) return;
                        bool redraw=false;
                        foreach(var update in updates) {
                            var changed=update.Item1;
                            changed.Art=update.Item2;
                            if(!(changed.ArtworkProvider ?? "").StartsWith("libretro",StringComparison.Ordinal)) changed.ArtworkProvider="downloaded";
                            int index=visible.FindIndex(entry => entry.Id==changed.Id);
                            if(index>=0 && filters.Count>0) visible[index]=ArtworkService.ForList(changed,settings,filters[collectionIndex].Id);
                            FrameworkElement card;
                            if(cards.TryGetValue(changed.Id,out card)) {cards.Remove(changed.Id); carousel.Children.Remove(card); redraw=true;}
                        }
                        if(redraw) Render(false);
                        RefreshMissingArtworkList();
                    }));
                },message => Dispatcher.BeginInvoke(new Action(() => {if(!cancellation.IsCancellationRequested) ShowNotice(message);} ))));
            } catch(OperationCanceledException) {} catch(Exception) {}
        }
        void ApplyLibrary(bool opening=false) {
            if(CombinedCollections.TrackAddedGames(library,settings,DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()) && !preview && !demo) QueueSave();
            missingGameCache.Clear();
            StopListTransition();
            string previousFilter=filters.Count>0 ? filters[collectionIndex].Id : null;
            SetFilters();
            if(!opening && previousFilter==settings.JumpListId && settings.CollectionId==previousFilter && IsSpecialJumpList(previousFilter))
                collectionIndex=Math.Max(0,IncludeJumpList());
            if(opening && !String.IsNullOrEmpty(settings.DefaultCollectionId) && settings.DefaultCollectionId!="remember") {
                collectionIndex=Math.Max(0,filters.FindIndex(c => c.Id==settings.DefaultCollectionId));
                settings.CollectionId=filters[collectionIndex].Id;
            }
            if(!opening || previousFilter!=filters[collectionIndex].Id || visible.Count==0) FilterGames(true);
        }
        void SetFilters() {
            var hidden=new HashSet<string>(settings.HiddenGameIds ?? new List<string>());
            var gameIds=new HashSet<string>(library.Games.Where(game => !hidden.Contains(game.Id)).Select(game => game.Id));
            filters=CollectionOptions().Where(c => CollectionEnabled(c.Id) &&
                (!settings.SkipEmptyLists || (c.Id=="all" ? gameIds.Count>0 : !c.UnsupportedDynamic && c.Games!=null && c.Games.Any(gameIds.Contains)))).ToList();
            if(filters.Count==0) filters.Add(new Collection {Id="unavailable-collections",Name="No available lists"});
            collectionIndex = Math.Max(0, filters.FindIndex(c => c.Id == settings.CollectionId));
        }
        void FilterGames(bool remember) {
            if(GameSearchOpen && refreshSearchView!=null) {refreshSearchView(); return;}
            Collection filter = filters[collectionIndex];
            activeListLayout=RetroArchListDefaults.Layout(settings,filter.Id);
            string oldId = remember ? settings.SelectedId : visible.Count > selected ? visible[selected].Id : null;
            string remembered;
            if(settings.SelectedGamesByList!=null && settings.SelectedGamesByList.TryGetValue(filter.Id,out remembered)) oldId=remembered;
            if(filter.Id=="recent" || filter.Id=="recently-added" || filter.Id=="most-played") oldId=null;
            var membership=new HashSet<string>(filter.Games ?? new List<string>());
            var hidden=new HashSet<string>(settings.HiddenGameIds ?? new List<string>());
            visible = library.Games.Where(g => !hidden.Contains(g.Id) && (filter.Id == "all" || (!filter.UnsupportedDynamic && membership.Contains(g.Id)))).ToList();
            if(filter.Id=="recent") visible=visible.OrderByDescending(g => CombinedCollections.RecentPlayed(settings,g)).ToList();
            if(filter.Id=="most-played") visible=visible.OrderByDescending(g => CombinedCollections.LaunchCount(settings,g)).ThenBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            if(filter.Id=="recently-added") visible=visible.OrderByDescending(g => CombinedCollections.AddedAt(settings,g)).ThenBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            string artworkSource=ArtworkService.ListDefaultSource(settings,filter.Id);
            visible=visible.Select(game => ArtworkService.ForList(game,settings,filter.Id,artworkSource)).ToList();
            selected = Math.Max(0, visible.FindIndex(g => g.Id == oldId));
            cards.Clear(); carousel.Children.Clear(); ApplyCarouselScale(); Render(false);
            DownloadListArtwork();
        }
        void ChangeCollection(int delta,bool vertical=false,bool animate=true) {
            if (launching || filters.Count < 2) return;
            int next=(collectionIndex+delta+filters.Count)%filters.Count;
            if(next==collectionIndex) return;
            if(!animate) StopListTransition();
            var oldList=animate ? DetachOutgoingList() : null;
            if(filters[collectionIndex].Id.StartsWith("radial-tag:",StringComparison.Ordinal) || !CollectionEnabled(filters[collectionIndex].Id)) {
                filters.RemoveAt(collectionIndex);
                if(next>collectionIndex) next--;
            }
            collectionIndex=next;
            try {
                settings.CollectionId = filters[collectionIndex].Id; FilterGames(false);
                navigationSaveTimer.Stop(); if(!demo) navigationSaveTimer.Start();
                if(animate) AnimateListSwitch(oldList,delta,vertical);
            }
            catch {StopListTransition(); throw;}
            if (filters[collectionIndex].UnsupportedDynamic) ShowNotice(filters[collectionIndex].UnsupportedReason ?? "This dynamic list uses a rule that is not supported yet.");
        }
        void Move(int delta) {
            if (visible.Count == 0 || launching) return;
            StopListTransition();
            selected = (selected + delta + visible.Count) % visible.Count;
            Render(true);
        }
        void MoveGridRow(int delta) {
            if(visible.Count==0 || launching) return;
            StopListTransition();
            int rowCount=(visible.Count+GridColumns-1)/GridColumns;
            int row=((selected/GridColumns+delta)%rowCount+rowCount)%rowCount;
            int pageSize=GridColumns*GridRows,sourcePage=selected/pageSize*pageSize;
            double ignored;
            var sourcePositions=ArtworkPanelPositions(Math.Min(pageSize,visible.Count-sourcePage),0,sourcePage,true,out ignored);
            Rect source=sourcePositions[selected-sourcePage];
            double center=source.X+source.Width/2;
            int first=row*GridColumns,last=Math.Min(first+GridColumns,visible.Count),targetPage=first/pageSize*pageSize;
            var targets=ArtworkPanelPositions(Math.Min(pageSize,visible.Count-targetPage),0,targetPage,true,out ignored);
            int next=first; double nearest=Double.PositiveInfinity;
            bool fitAngledArtwork=ActiveAngledSizing!="fixed";
            for(int candidate=first;candidate<last;candidate++) {
                Rect target=targets[candidate-targetPage];
                double distance=Math.Abs(target.X+target.Width/2-center);
                if(distance<nearest-.001) {nearest=distance; next=candidate;}
            }
            selected=next;
            Render(false);
        }
        void Render(bool animate) {
            ConfigureViewport();
            if(GameSearchOpen) ConfigureSearchViewport();
            RemoveGuideCards();
            launchCountSubtitle.Text="";
            if (filters.Count == 0) SetFilters();
            collectionText.Text = GameSearchOpen ? (String.IsNullOrEmpty(gameSearchText) ? "Search:" : "Search: "+gameSearchText) : settings.ShowListGameCount ? CombinedCollections.Label(filters[collectionIndex],library,settings.HiddenGameIds,countOnly:true) : filters[collectionIndex].Name;
            if (visible.Count == 0) {
                carousel.Children.Clear(); cards.Clear();
                if(!GameSearchOpen && !demo && library.Games.Count==0) AddGuideCards();
                if(GameSearchOpen && searchConsoleLabel!=null) searchConsoleLabel.Text="";
                title.Text = GameSearchOpen ? "No matching games" : filters[collectionIndex].Id=="unavailable-collections" ? "Choose available lists in Settings" : filters[collectionIndex].UnsupportedDynamic ? "This list needs an unsupported filter" : library.Games.Count > 0 ? "No installed games in this list" : "Your next game starts here";
                return;
            }
            Game current = visible[selected];
            if(!GameSearchOpen) {
                settings.SelectedId = current.Id;
                if(settings.SelectedGamesByList==null) settings.SelectedGamesByList=new Dictionary<string,string>();
                if(filters[collectionIndex].Id=="recent" || filters[collectionIndex].Id=="recently-added" || filters[collectionIndex].Id=="most-played") settings.SelectedGamesByList.Remove(filters[collectionIndex].Id);
                else settings.SelectedGamesByList[filters[collectionIndex].Id]=current.Id;
            } else if(searchConsoleLabel!=null) searchConsoleLabel.Text=SearchSourceLabel(current,settings);
            string availabilityIssue=GameAvailabilityIssue(current);
            if(!GameSearchOpen && filters[collectionIndex].Id=="most-played") {
                long launchCount=CombinedCollections.LaunchCount(settings,current);
                launchCountSubtitle.Text="("+launchCount+(launchCount==1 ? " time launched)" : " times launched)");
                launchCountSubtitle.Margin=new Thickness(0,title.Margin.Top+34,0,0);
            }
            title.Text = current.Name+(availabilityIssue==null ? "" : " · "+availabilityIssue);
            var keep = new HashSet<string>();
            bool gridLayout=ActiveLayout=="grid";
            int pageSize=GridColumns*GridRows,pageStart=(selected/pageSize)*pageSize;
            int count = gridLayout ? Math.Min(pageSize,visible.Count-pageStart) : Math.Min(9,visible.Count), start = -(count/2);
            double gridHeight=Math.Min(150,(1440.0-(GridColumns-1)*28)/GridColumns)*1.5;
            double coverRowHeight;
            var coverPositions=ArtworkPanelPositions(count,start,pageStart,gridLayout,out coverRowHeight);
            if(gridLayout && coverPositions!=null) gridHeight=coverRowHeight;
            double gridContentHeight=gridHeight*GridRows+(GridRows-1)*20;
            if(gridLayout) {
                if(gridScrollPage!=pageStart || cards.Count==0) gridScrollOffset=0;
                gridScrollPage=pageStart;
                double rowTop=((selected-pageStart)/GridColumns)*(gridHeight+20);
                if(rowTop<gridScrollOffset) gridScrollOffset=rowTop;
                if(rowTop+gridHeight>gridScrollOffset+gridViewportHeight) gridScrollOffset=rowTop+gridHeight-gridViewportHeight;
                gridScrollOffset=Math.Max(0,Math.Min(Math.Max(0,gridContentHeight-gridViewportHeight),gridScrollOffset));
            }
            bool fitAngledArtwork=ActiveAngledSizing!="fixed";
            for (int slot = 0; slot < count; slot++) {
                int offset=start+slot, i=gridLayout ? pageStart+slot : (selected+offset+visible.Count)%visible.Count;
                bool active=i==selected;
                Game game = visible[i]; keep.Add(game.Id);
                FrameworkElement card;
                double h = offset == 0 ? 490 : 390 - Math.Min(3,Math.Abs(offset))*16;
                double w = offset == 0 ? 355 : 300;
                bool rectangular=ActiveLayout=="rectangular";
                if(rectangular) w=offset==0 ? 300 : 250;
                double x = 800 - w/2 + offset*(rectangular ? 285 : 254);
                double y = offset == 0 ? 0 : (490-h)/2;
                if(!gridLayout && !rectangular && fitAngledArtwork) {
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
                if(coverPositions!=null) {
                    var position=coverPositions[slot]; w=position.Width; h=position.Height; x=position.X;
                    y=gridLayout ? Math.Max(0,(gridViewportHeight-gridContentHeight)/2)+(slot/GridColumns)*(gridHeight+20)+position.Y-gridScrollOffset : position.Y;
                }
                if (!cards.TryGetValue(game.Id,out card)) {
                    double pixelScale=1;
                    var source=PresentationSource.FromVisual(this);
                    if(source!=null && source.CompositionTarget!=null) pixelScale=source.CompositionTarget.TransformToDevice.M11;
                    double screenWidth=ActualWidth>0 ? ActualWidth*pixelScale : 1600;
                    int decodeWidth=gridLayout ? Math.Min(700,Math.Max(128,(int)Math.Ceiling(w*(ActivePanelOutlines ? 1 : 1.12)*ActivePanelScale*screenWidth/1600/64)*64)) : 700;
                    card = Card(game, i, w,h,active,decodeWidth); cards[game.Id] = card; carousel.Children.Add(card);
                    Canvas.SetLeft(card,x + (animate ? Math.Sign(offset)*45 : 0)); Canvas.SetTop(card,y); card.Width = w; card.Height = h;
                }
                // Geometry and artwork transform follow the animated panel size.
                Panel.SetZIndex(card, gridLayout ? (active ? 100 : 0) : 10-Math.Abs(offset));
                if(gridLayout) UpdateGridSelection(card,active,animate,x,y,w,h);
                SetAnimation(card, Canvas.LeftProperty,x,animate); SetAnimation(card, Canvas.TopProperty,y,animate);
                SetAnimation(card, WidthProperty,w,animate); SetAnimation(card, HeightProperty,h,animate); SetAnimation(card, OpacityProperty,active ? 1 : gridLayout ? .72 : .48,animate);
                var border = card as Grid;
                bool missing=GameFileMissing(game);
                var missingBadge=border.Children.OfType<Border>().FirstOrDefault(b => b.Name=="MissingGameBadge");
                if(missingBadge!=null) missingBadge.Visibility=missing ? Visibility.Visible : Visibility.Collapsed;
                card.ToolTip=missing ? GameAvailabilityIssue(game)+(game.Source=="emulator" ? ". Check this game's emulator, core or game path in Emulators." : ". Update its path in Custom games.") : game.Name;
                var outline = border.Children[border.Children.Count-1] as System.Windows.Shapes.Path;
                if (outline != null) { outline.Stroke = missing ? new SolidColorBrush(Color.FromRgb(255,172,153)) : active ? mint : new SolidColorBrush(Color.FromArgb(70,180,206,213)); outline.StrokeThickness = ActivePanelOutlines ? (active ? 2 : 1) : 0; }
            }
            foreach (string id in cards.Keys.Where(k => !keep.Contains(k)).ToArray()) { carousel.Children.Remove(cards[id]); cards.Remove(id); }
            FitSearchCarouselViewport();
        }
        void RemoveGuideCards() {
            foreach(var card in guideCards) carousel.Children.Remove(card);
            if(guideActions!=null) carousel.Children.Remove(guideActions);
        }
        void AddGuideCards() {
            if(guideCards.Count==0) {
                string[] names={"GuideCardSteam","GuideCardCustomGames","GuideCardEmulators","GuideCardLists"};
                string[] titles={"Connect a launcher","Add custom games","Import emulators","Manage lists"};
                string[] hints={"Configure your game launchers in Settings to load their libraries.","Add any game from an executable or shortcut.","Add ROM folders and playlists for your emulators.","Choose which lists appear in the overlay."};
                int[] tabs={3,4,5,2};
                double[] offsets={-1.5,-0.5,0.5,1.5};
                for(int i=0;i<titles.Length;i++) {
                    int tab=tabs[i];
                    var grid=new Grid {Name=names[i],Width=300,Height=390,Background=Brushes.Transparent,Cursor=System.Windows.Input.Cursors.Hand,ToolTip=titles[i]+" · opens Settings"};
                    grid.Tag=tab;
                    int hash=17; foreach(char ch in titles[i]) hash=unchecked(hash*31+ch);
                    byte hue=(byte)(Math.Abs((long)hash)%90);
                    var body=new Grid(); body.Background=new LinearGradientBrush(Color.FromRgb((byte)(28+hue/2),(byte)(65+hue/3),(byte)(82+hue/2)),Color.FromRgb(11,21,33),60);
                    grid.Children.Add(body);
                    body.Children.Add(new System.Windows.Shapes.Rectangle {Fill=new LinearGradientBrush(Color.FromArgb(0,0,0,0),Color.FromArgb(210,5,13,20),90)});
                    var text=new StackPanel {Margin=new Thickness(30,44,30,0),MaxWidth=240,VerticalAlignment=VerticalAlignment.Top,HorizontalAlignment=HorizontalAlignment.Left};
                    var titleLabel=Label(titles[i],31,white); titleLabel.FontWeight=FontWeights.SemiBold; titleLabel.TextWrapping=TextWrapping.Wrap;
                    var hintLabel=Label(hints[i],14,muted); hintLabel.TextWrapping=TextWrapping.Wrap; hintLabel.Margin=new Thickness(0,12,0,0);
                    text.Children.Add(titleLabel); text.Children.Add(hintLabel);
                    body.Children.Add(text);
                    grid.Children.Add(new Border {BorderBrush=mint,BorderThickness=new Thickness(ActivePanelOutlines ? 2 : 0),Background=Brushes.Transparent,CornerRadius=new CornerRadius(8)});
                    grid.MouseLeftButtonUp+=(s,e) => OpenSettingsTab(tab);
                    guideCards.Add(grid);
                    Canvas.SetLeft(grid,800-150+offsets[i]*320); Canvas.SetTop(grid,(490-390)/2);
                }
            }
            foreach(var card in guideCards) carousel.Children.Add(card);
            if(guideActions==null) {
                guideActions=new StackPanel {Name="TemplateActions",Orientation=Orientation.Horizontal};
                guideActions.SizeChanged+=(s,e) => Canvas.SetLeft(guideActions,800-guideActions.ActualWidth/2);
                var setup=new Button {Name="TemplateRunSetup",Content="Run first-launch wizard",Padding=new Thickness(16,8,16,8),FontSize=14,Background=mint,Foreground=Brushes.Black,Margin=new Thickness(0,0,12,0)};
                setup.Click+=(s,e) => {e.Handled=true; Close(); ShowSetupWizard(true);};
                var quit=new Button {Name="TemplateQuitApp",Content="Quit app",Padding=new Thickness(16,8,16,8),FontSize=14,Background=new SolidColorBrush(Color.FromRgb(28,48,61)),Foreground=white,BorderBrush=mint,BorderThickness=new Thickness(1)};
                quit.Click+=(s,e) => {e.Handled=true; ExitApp();};
                guideActions.Children.Add(setup); guideActions.Children.Add(quit);
                Canvas.SetLeft(guideActions,600); Canvas.SetTop(guideActions,450);
            }
            carousel.Children.Add(guideActions);
        }
        internal static int SettingsTabIndex(int oldIndex) {return oldIndex==0 ? 6 : oldIndex==1 ? 0 : oldIndex>=2 && oldIndex<=5 ? oldIndex : oldIndex+1;}
        public void OpenSettingsTab(int tab) {
            bool created=false;
            if(helpWindow==null) { helpWindow=CreateHelpWindow(); helpWindow.Closed+=(s,e) => { helpWindow=null; helpTabs=null; missingArtworkList=null; missingArtworkCount=null; }; created=true; }
            if(helpWindow.WindowState==WindowState.Minimized) helpWindow.WindowState=WindowState.Normal;
            if(helpTabs!=null) helpTabs.SelectedIndex=SettingsTabIndex(tab);
            if(created) helpWindow.Show();
            helpWindow.Activate();
        }
        void SetAnimation(FrameworkElement element, DependencyProperty property, double value, bool animate) {
            AnimatePanelProperty(element,property,value,animate && !preview && SystemParameters.ClientAreaAnimation);
        }
        internal static void AnimatePanelProperty(FrameworkElement element,DependencyProperty property,double value,bool animate) {
            double current=(double)element.GetValue(property);
            if(Double.IsNaN(current) || Double.IsInfinity(current)) current=value;
            element.SetValue(property,value);
            if(!animate || Math.Abs(current-value)<0.001) {element.BeginAnimation(property,null); return;}
            var animation=new DoubleAnimation(current,value,TimeSpan.FromMilliseconds(230)) {
                EasingFunction=new SineEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop
            };
            element.BeginAnimation(property,animation,HandoffBehavior.SnapshotAndReplace);
        }
        FrameworkElement Card(Game game, int index, double width, double height, bool active,int decodeWidth) {
            var grid = new Grid { Width = width, Height = height, Cursor = System.Windows.Input.Cursors.Hand, Background = Brushes.Transparent };
            var body = new Grid(); grid.Children.Add(body);
            int hash = 17; foreach (char ch in game.Name) hash = unchecked(hash*31+ch);
            byte hue = (byte)(Math.Abs((long)hash)%90);
            body.Background = new LinearGradientBrush(Color.FromRgb((byte)(28+hue/2),(byte)(65+hue/3),(byte)(82+hue/2)), Color.FromRgb(11,21,33),60);
            var bitmap = LoadImage(game.Art,decodeWidth);
            grid.Tag=bitmap;
            Image artwork=null;
            Grid artworkFrame=null;
            Border artworkBackdrop=null;
            if (bitmap != null) {
                artworkFrame=new Grid {Name="ArtworkFrame",HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Top,ClipToBounds=true};
                if(!GameSearchOpen && ((ActiveLayout!="rectangular" && ActiveLayout!="grid") || IsLibretroCover(game))) {
                    artworkBackdrop=new Border {Name="ArtworkBackdrop",Background=ArtworkColours.Background(bitmap),IsHitTestVisible=false};
                    artworkFrame.Children.Add(artworkBackdrop);
                }
                artwork=new Image {Name="GameArtwork",Source=bitmap,Stretch=GameSearchOpen ? Stretch.Uniform : Stretch.UniformToFill,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center};
                artworkFrame.Children.Add(artwork); body.Children.Add(artworkFrame);
            }
            else {
                body.Children.Add(new System.Windows.Shapes.Ellipse { Width=330,Height=330,Fill=new SolidColorBrush(Color.FromArgb(22,163,240,205)),HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,-120,-160,0) });
                bool gridLayout=ActiveLayout=="grid";
                var words = Label(game.Name,gridLayout ? 18 : 31,white); words.FontWeight=FontWeights.SemiBold; words.TextWrapping=TextWrapping.Wrap; words.Margin=gridLayout ? new Thickness(12) : new Thickness(58,60,40,75); words.VerticalAlignment=VerticalAlignment.Center; body.Children.Add(words);
            }
            body.Children.Add(new System.Windows.Shapes.Rectangle { Fill = new LinearGradientBrush(Color.FromArgb(0,0,0,0),Color.FromArgb(210,5,13,20),90) });
            grid.Children.Add(new Border {Name="MissingGameBadge",Background=new SolidColorBrush(Color.FromRgb(54,30,28)),CornerRadius=new CornerRadius(5),Padding=new Thickness(8,5,8,5),Margin=new Thickness(0,14,0,0),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top,Visibility=Visibility.Collapsed,IsHitTestVisible=false,Child=Label("File missing",11,new SolidColorBrush(Color.FromRgb(255,172,153)))});
            var outline = new System.Windows.Shapes.Path { Fill=Brushes.Transparent,IsHitTestVisible=false,Stroke=mint,StrokeThickness=ActivePanelOutlines ? 2 : 0 }; grid.Children.Add(outline);
            Action geometry = () => {
                double w=grid.ActualWidth,h=grid.ActualHeight;
                double slant=ActiveLayout=="rectangular" || ActiveLayout=="grid" ? 0 : 73;
                if(artwork!=null && h>0 && w>slant) {
                    artworkFrame.Width=w-slant; artworkFrame.Height=h;
                    artwork.Stretch=GameSearchOpen || slant>0 || IsLibretroCover(game) ? Stretch.Uniform : Stretch.UniformToFill;
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
                if(GameSearchOpen) {if(SearchGamesFocused && index==selected) LaunchSearchSelection(); else {selected=index; Render(false);} return;}
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
                if (images.TryGetValue(key,out cached)) {
                    imageRecency.Remove(key); imageRecency.AddLast(key);
                    return cached;
                }
                var bitmap=new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption=BitmapCacheOption.OnLoad; bitmap.CreateOptions=BitmapCreateOptions.IgnoreImageCache; bitmap.DecodePixelWidth=size; bitmap.UriSource=new Uri(path,UriKind.Absolute); bitmap.EndInit(); bitmap.Freeze();
                long bytes=ImageBytes(bitmap);
                while(imageRecency.Count>0 && (imageCacheBytes+bytes>ImageCacheLimit || images.Count>=70)) {
                    string oldest=imageRecency.First.Value;
                    imageRecency.RemoveFirst();
                    imageCacheBytes-=ImageBytes(images[oldest]); images.Remove(oldest);
                }
                if(bytes<=ImageCacheLimit) {images[key]=bitmap; imageRecency.AddLast(key); imageCacheBytes+=bytes;}
                return bitmap;
            } catch { return null; }
        }
        static long ImageBytes(BitmapSource image) {
            return ((long)image.PixelWidth*Math.Max(1,image.Format.BitsPerPixel)+7)/8*image.PixelHeight;
        }
        void LaunchSelected() {
#if ARTWORK_TEST
            ShowNotice("Artwork test only - game launching is disabled.");
#else
            if (launching) return;
            if (visible.Count==0) {
                if(!demo && library.Games.Count==0) OpenSettingsTab(filters.Count>0 && filters[collectionIndex].Id=="unavailable-collections" ? 2 : 3);
                return;
            }
            if (demo) { ShowNotice("Preview mode · connect your Steam library to launch games."); return; }
            LaunchRadialGame(visible[selected]);
#endif
        }
        void LaunchRadialGame(Game game) {
#if ARTWORK_TEST
            ShowNotice("Artwork test only - game launching is disabled.");
#else
            if(launching || demo || game==null) return;
            string emulatorIssue=EmulatorLibrary.MissingReason(game,settings);
            if(emulatorIssue!=null) {ShowNotice(emulatorIssue+". Check Settings → Emulators."); return;}
            launching=true;
            try {
                Save();
                Process.Start(GameLaunch.StartInfo(game,settings));
                if(settings.LaunchHistory==null) settings.LaunchHistory=new Dictionary<string,long>();
                settings.LaunchHistory[game.Id]=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if(settings.LaunchCounts==null) settings.LaunchCounts=new Dictionary<string,long>();
                settings.LaunchCounts[game.Id]=CombinedCollections.LaunchCount(settings,game)+1;
                Save();
                Close();
            } catch (Exception e) { launching=false; ShowNotice("Game could not be opened: "+e.Message); }
#endif
        }
        bool GameFileMissing(Game game) {
            return GameAvailabilityIssue(game)!=null;
        }
        string GameAvailabilityIssue(Game game) {
            if(DateTime.UtcNow>=missingGameCacheUntil) {
                missingGameCache.Clear(); missingGameCacheUntil=DateTime.UtcNow.AddSeconds(2);
            }
            string missing;
            if(!missingGameCache.TryGetValue(game.Id,out missing)) {
                missing=game.Source=="custom" && String.IsNullOrEmpty(game.SteamLaunchId) && !File.Exists(game.LaunchId) ? "Game file missing" : EmulatorLibrary.MissingReason(game,settings);
                missingGameCache[game.Id]=missing;
            }
            return missing;
        }
        void Save() { navigationSaveTimer.Stop(); if (demo || setupTemplateOnly) return; try { Storage.Write("settings.json",settings); } catch { } }
        void QueueSave() {
            if(demo || saveQueued) return;
            saveQueued=true;
            Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(() => { saveQueued=false; Save(); }));
        }
        void OnKey(object sender, System.Windows.Input.KeyEventArgs e) {
            if(!preview && (controllerWaitForForegroundRelease || !OverlayHasForeground())) return;
            if(HandleArcShortcutKey(e)) return;
            if(GameSearchOpen) {HandleSearchKey(e); return;}
            string action=ControlBindings.KeyAction(settings,"overlay",e.Key==System.Windows.Input.Key.System ? e.SystemKey : e.Key,System.Windows.Input.Keyboard.Modifiers);
            if(action==null) return;
            var navKey=action=="left" ? System.Windows.Input.Key.Left : action=="right" ? System.Windows.Input.Key.Right : action=="up" ? System.Windows.Input.Key.Up : action=="down" ? System.Windows.Input.Key.Down : System.Windows.Input.Key.None;
            PadState pad;
            if(navKey!=System.Windows.Input.Key.None && !preview && Gamepad.Read(out pad) && TryControllerNavigationKey(navKey,RemapController(pad,"overlay"))) {e.Handled=true; return;}
            e.Handled=true;
            if(action=="left" || action=="right") {Move(action=="left" ? -1 : 1); return;}
            if(action=="up" || action=="down") {if(ActiveLayout=="grid") MoveGridRow(action=="up" ? -1 : 1); return;}
            if(e.IsRepeat) return;
            switch(action) {
                case "play": LaunchSelected(); break;
                case "dismiss": Close(); break;
                case "exit": ExitApp(); break;
                case "previous": ChangeCollection(-1); break;
                case "next": ChangeCollection(1); break;
                case "refresh": RefreshLibrary(true); break;
                case "hints": ShowControls(); break;
                case "search": OpenGameSearch(); break;
                case "favourite": ToggleFavourite(); break;
                case "monitor": CycleMonitor(); break;
                case "jump": JumpToConfiguredList(); break;
                case "layout": ToggleTestLayoutFromKeyboard(); break;
                case "settings": ShowHelpWindow(); break;
            }
        }
#if NAVIGATION_DIAGNOSTICS
        readonly ControllerNavigationTrace controllerTrace=new ControllerNavigationTrace();
        void TraceControllerFrame(object sender,EventArgs e) {
            if(preview || demo || !IsVisible || !IsActive || selected<0 || selected>=visible.Count) return;
            FrameworkElement panel;
            if(cards.TryGetValue(visible[selected].Id,out panel)) controllerTrace.Frame(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency,selected,Canvas.GetLeft(panel),(double)panel.GetAnimationBaseValue(Canvas.LeftProperty));
        }
#endif
        bool OverlayHasForeground() {
            var foreground=GetForegroundWindow();
            if(foreground==IntPtr.Zero) return false;
            return foreground==new System.Windows.Interop.WindowInteropHelper(this).Handle ||
                (gameSearchWindow!=null && foreground==new System.Windows.Interop.WindowInteropHelper(gameSearchWindow).Handle);
        }
        bool controllerWaitForForegroundRelease;
        readonly XboxGuideInput xboxGuideInput=new XboxGuideInput();
        bool switchHomeWasDown;
        long switchHomeDownAt=-1;
        void UpdateTrayHomeListener() {
            if(IsVisible || preview || exiting) return;
            controllerTimer.Stop(); switchHomeDownAt=-1;
            Gamepad.TakePlayStationHomePress();
            xboxGuideInput.TakePress();
            if(settings.OpenOverlayWithHome) xboxGuideInput.Start(); else xboxGuideInput.Dispose();
            if(!settings.OpenOverlayWithHome) {Gamepad.StopNative(); return;}
            Gamepad.StartNative(new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle());
            switchHomeWasDown=Gamepad.SwitchHomeDown();
            controllerTimer.Interval=TimeSpan.FromMilliseconds(32);
            controllerTimer.Start();
        }
        bool TakeOverlayHomePress() {
            bool pressed=Gamepad.TakePlayStationHomePress();
            pressed=xboxGuideInput.TakePress() || pressed;
            if(!settings.OpenOverlayWithHome || preview || exiting) return false;
            bool down=Gamepad.SwitchHomeDown();
            bool switchTapped=false;
            if(down!=switchHomeWasDown) {
                long now=Stopwatch.GetTimestamp();
                if(down) switchHomeDownAt=now;
                else {
                    switchTapped=Gamepad.IsHomeTap(switchHomeDownAt,now);
                    switchHomeDownAt=-1;
                }
                switchHomeWasDown=down;
            }
            return pressed || switchTapped;
        }
        void PollTrayHome() {
            if(!TakeOverlayHomePress() || NeedsInitialSetup ||
                (helpWindow!=null && helpWindow.IsVisible) || OwnedWindows.Cast<Window>().Any(window => window.IsVisible)) return;
            ShowOverlay();
            controllerWaitForForegroundRelease=true;
        }
        void PollController(object sender,EventArgs e) {
            if(arcOpenedByShortcut && (arcOnlyWindow!=null || radial!=null)) {PollShortcutArc(); return;}
            if(!IsVisible) {PollTrayHome(); return;}
            bool homePressed=TakeOverlayHomePress();
            if (preview || !OverlayHasForeground()) {
                controllerWaitForForegroundRelease=true;
                if(radial!=null && !arcOpenedByShortcut) {CloseRadial(); radialWaitRelease=false;}
                if(homePressed && !launching &&
                    (helpWindow==null || !helpWindow.IsVisible || helpWindow.WindowState==WindowState.Minimized) &&
                    !OwnedWindows.Cast<Window>().Any(window => window!=gameSearchWindow && window.IsVisible && window.WindowState!=WindowState.Minimized)) {
#if FOCUS_DIAGNOSTICS
                    TraceFocus("controller shortcut refocus requested");
#endif
                    FocusVisibleOverlayFromShortcut();
#if FOCUS_DIAGNOSTICS
                    QueueFocusTrace("after controller shortcut refocus");
#endif
                }
#if NAVIGATION_DIAGNOSTICS
                if(!preview) controllerTrace.Poll(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency,false,false,new PadState(),null);
#endif
                exitButton.Reset(); previousButtons=0; navigation.Reset(); return;
            }
            PadState pad; bool found=Gamepad.Read(out pad);
#if NAVIGATION_DIAGNOSTICS
            controllerTrace.Poll(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency,true,found,pad,Gamepad.Source);
#endif
            if (!found) { if(!arcOpenedByShortcut) CloseRadial(); controllerWaitForForegroundRelease=false; radialWaitRelease=false; exitButton.Reset(); previousButtons=0; navigation.Reset(); return; }
            if(controllerWaitForForegroundRelease) {
                if(pad.Buttons!=0 || pad.LT>=80 || pad.RT>=80) return;
                controllerWaitForForegroundRelease=false;
            }
            if(!OverlayHasForeground()) {controllerWaitForForegroundRelease=true; return;}
            ApplyControllerState(pad,Gamepad.Source,(long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency));
        }
        internal void ApplyControllerState(PadState pad,string source,long milliseconds) {
            if(gameSearchWindow!=null) {HandleGameSearchController(pad,milliseconds); return;}
            if(HandleRadial(pad,milliseconds)) return;
            pad=RemapController(pad,"overlay");
            if(suppressSearchRelease) {
                previousButtons=pad.Buttons; previousInputSource=source;
                if(pad.Buttons!=0 || pad.RT>80 || pad.LT>80) return;
                suppressSearchRelease=false;
            }
            if(HandleTestLayoutTrigger(pad)) return;
            if(previousInputSource!=source) {
                previousInputSource=source; previousButtons=0;
                exitButton.Reset();
            }
            NavigateController(pad);
            ushort buttons=pad.Buttons;
            ushort pressed=(ushort)(buttons & ~previousButtons);
            previousButtons=buttons;
            ButtonGesture exitGesture=exitButton.Update((buttons&0x0400)!=0,milliseconds);
            if(exitGesture==ButtonGesture.Hold) { ExitApp(); return; }
            if(exitGesture==ButtonGesture.Tap || ((pressed&0x2000)!=0 && (buttons&0x0400)==0)) {Close(); return;}
            if ((pressed&0x1000)!=0) { LaunchSelected(); return; }
            if ((pressed&0x4000)!=0) CycleMonitor();
            if ((pressed&0x0100)!=0) ChangeCollection(-1);
            if ((pressed&0x0200)!=0) ChangeCollection(1);
            if ((pressed&0x8000)!=0) {OpenGameSearch(); return;}
            if ((pressed&0x0020)!=0 && (buttons&0x0010)==0) RefreshLibrary(true);
            if ((pressed&0x0010)!=0 && (buttons&0x0020)==0) ShowControls();
            if((pressed&0x0080)!=0) {ToggleFavourite(); return;}
            if((pressed&0x0040)!=0) JumpToConfiguredList();
            if((pressed&0x0800)!=0) ShowHelpWindow();
        }
        int IncludeJumpList() {
            int target=filters.FindIndex(list => list.Id==settings.JumpListId);
            if(target>=0 || !IsSpecialJumpList(settings.JumpListId)) return target;
            var destination=CollectionOptions().FirstOrDefault(list => list.Id==settings.JumpListId);
            if(destination==null) return -1;
            filters.Add(destination);
            return filters.Count-1;
        }
        void JumpToConfiguredList() {
            if(launching) return;
            int target=IncludeJumpList();
            if(target<0) ShowNotice("Choose an available jump list in Settings → Lists.");
            else if(target!=collectionIndex) ChangeCollection(target-collectionIndex);
        }
        internal bool TryControllerNavigationKey(System.Windows.Input.Key key,PadState pad) {
            bool fast,grid=ActiveLayout=="grid";
            int keyDirection=ControllerNavigation.KeyDirection(key,grid,GridColumns,ControllerNavigation.ListSwitchEnabled(settings,ActiveLayout));
            if(keyDirection==0 || ControllerNavigation.InputDirection(pad,settings,GridColumns,out fast,ActiveLayout)!=keyDirection) return false;
            // Desktop controller mappings may send an arrow key before their
            // per-app profile switches. Use the same repeat gate as polling.
            NavigateController(pad); return true;
        }
        internal void NavigateController(PadState pad) {
            bool gridLayout=ActiveLayout=="grid";
            bool fast;
            int direction=ControllerNavigation.InputDirection(pad,settings,GridColumns,out fast,ActiveLayout);
            double now=Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency;
            if (navigation.Update(direction,fast,now,settings.RightStickSpeed,Math.Abs(direction)==ControllerNavigation.ListStep ? 1 : ControllerNavigation.NormalSpeed(pad,direction,gridLayout,GridColumns,settings))) {
                if(Math.Abs(direction)==ControllerNavigation.ListStep) ChangeCollection(Math.Sign(direction),true);
                else if(gridLayout && Math.Abs(direction)==GridColumns) MoveGridRow(Math.Sign(direction)); else Move(direction);
                // Artwork decoding/layout can take longer on the first move.
                // Begin the repeat delay after that work, never catch up missed ticks.
                double completed=Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency;
#if NAVIGATION_DIAGNOSTICS
                if(!preview && !demo) controllerTrace.Move(completed,direction,fast,completed-now);
#endif
                navigation.CompleteMove(completed);
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
        public const int ListStep=1000000;
        public static bool ListSwitchEnabled(Settings settings,string layout=null) {return (layout ?? settings.CarouselLayout)!="grid";}
        public static int ListDirection(PadState pad,Settings settings,string layout=null) {
            if(!ListSwitchEnabled(settings,layout)) return 0;

            if((pad.Buttons&0x0003)!=0) return (pad.Buttons&0x0003)==0x0001 ? -1 : (pad.Buttons&0x0003)==0x0002 ? 1 : 0;
            return pad.LY>16000 ? -1 : pad.LY < -16000 ? 1 : 0;
        }
        public static int InputDirection(PadState pad,Settings settings,int columns,out bool fast,string layout=null) {
            int direction=Direction(pad,(layout ?? settings.CarouselLayout)=="grid",columns,out fast);
            return direction!=0 ? direction : ListStep*ListDirection(pad,settings,layout);
        }

        public static double NormalSpeed(PadState pad,int direction,bool grid,int columns,Settings settings) {
            bool dpad=(direction==-1 && (pad.Buttons&0x0004)!=0) || (direction==1 && (pad.Buttons&0x0008)!=0) || (grid && direction==-columns && (pad.Buttons&0x0001)!=0) || (grid && direction==columns && (pad.Buttons&0x0002)!=0);
            double speed=settings.LeftStickSpeed;
            return dpad || Double.IsNaN(speed) || Double.IsInfinity(speed) || speed<1 || speed>10 ? 1 : speed;
        }
        public static int KeyDirection(System.Windows.Input.Key key,bool grid,int columns,bool lists=false) {
            if(key==System.Windows.Input.Key.Left) return -1;
            if(key==System.Windows.Input.Key.Right) return 1;
            if(grid && key==System.Windows.Input.Key.Up) return -columns;
            if(!grid && lists && key==System.Windows.Input.Key.Up) return -ListStep;
            if(grid && key==System.Windows.Input.Key.Down) return columns;
            if(!grid && lists && key==System.Windows.Input.Key.Down) return ListStep;
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
        public bool Update(int value,bool accelerated,double milliseconds,double speed=3,double normalSpeed=1) {
            if(value==0) { Reset(); return false; }
            bool changed=value!=direction || accelerated!=fast;
            direction=value; fast=accelerated;
            if(!changed && milliseconds<next) return false;
            if(Double.IsNaN(speed) || Double.IsInfinity(speed) || speed<1 || speed>10) speed=3;
            if(Double.IsNaN(normalSpeed) || Double.IsInfinity(normalSpeed) || normalSpeed<1 || normalSpeed>10) normalSpeed=1;
            delay=(changed ? 330.0 : 125.0)/(accelerated ? speed : normalSpeed);
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
        internal static bool IsHomeTap(long downAt,long upAt) {return downAt>=0 && (upAt-downAt)*1000.0/Stopwatch.Frequency<350;}
        [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] static extern uint Read14(uint index,out PadState state);
        [DllImport("xinput1_3.dll",EntryPoint="XInputGetState")] static extern uint Read13(uint index,out PadState state);
        [DllImport("xinput9_1_0.dll",EntryPoint="XInputGetState")] static extern uint Read91(uint index,out PadState state);
        static int version;
        static readonly PlayStationInput sony=new PlayStationInput();
        static readonly SwitchProInput nintendo=new SwitchProInput();
        static readonly ControllerRouter router=new ControllerRouter();
        public static string Source { get; private set; }
        public static void StartNative(IntPtr window) {
#if !XBOX_GUIDE_TEST
            sony.Start(window);
#endif
            nintendo.Start();
        }
        public static void StopNative() { sony.Stop(); nintendo.Stop(); router.Reset(); Source=null; }
        public static bool TakePlayStationHomePress() {return sony.TakeHomePress();}
        public static bool SwitchHomeDown() {return nintendo.ReadAll().Any(pair => (pair.Value.Buttons&0x0400)!=0);}
        public static bool Read(out PadState state) {
            var pads=sony.ReadAll();
            pads.AddRange(nintendo.ReadAll());
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
