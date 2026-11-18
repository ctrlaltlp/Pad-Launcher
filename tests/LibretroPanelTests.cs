using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PadLauncher;

class LibretroPanelTests {
    static int checks;
    static void Check(bool value,string message) {if(!value) throw new Exception(message); checks++;}
    static FrameworkElement Find(DependencyObject root,string name) {
        var element=root as FrameworkElement; if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {var child=Find(VisualTreeHelper.GetChild(root,i),name); if(child!=null) return child;}
        return null;
    }
    static string Cover(string root,string name,int width,int height,Brush colour) {
        string path=Path.Combine(root,name+".png"); var drawing=new DrawingVisual();
        using(var context=drawing.RenderOpen()) {
            context.DrawRectangle(colour,null,new Rect(0,0,width,height));
            context.DrawRectangle(Brushes.White,null,new Rect(8,8,width-16,8));
            context.DrawRectangle(Brushes.Coral,null,new Rect(8,height-16,width-16,8));
        }
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32); bitmap.Render(drawing);
        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(path)) encoder.Save(stream);
        return path;
    }
    static void Layout(Launcher window) {var content=(FrameworkElement)window.Content; content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();}
    static double VisibleGap(FrameworkElement left,FrameworkElement right,bool angled) {
        double slant=angled ? 73 : 0;
        double top=Math.Max(Canvas.GetTop(left),Canvas.GetTop(right));
        double bottom=Math.Min(Canvas.GetTop(left)+left.Height,Canvas.GetTop(right)+right.Height);
        Func<double,double> gap=y => Canvas.GetLeft(right)+slant*(1-(y-Canvas.GetTop(right))/right.Height)-(Canvas.GetLeft(left)+left.Width-slant*(y-Canvas.GetTop(left))/left.Height);
        return Math.Min(gap(top),gap(bottom));
    }
    [STAThread] static void Main(string[] args) {
        string previous=Storage.Root,root=Path.Combine(Path.GetTempPath(),"LibretroPanelTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        try {
            string portrait=Cover(root,"portrait",120,180,Brushes.SteelBlue),square=Cover(root,"square",180,180,Brushes.DarkCyan),wide=Cover(root,"wide",320,180,Brushes.DarkGoldenrod);
            var library=new Library {Games=new List<Game> {
                new Game {Id="modern",Name="A Modern",Source="steam",Art=portrait,LaunchId="1"},
                new Game {Id="square",Name="B Square",Source="steam",Art=square,ArtworkProvider="libretro",LaunchId="2"},
                new Game {Id="wide",Name="C Landscape",Source="steam",Art=wide,ArtworkProvider="libretro-chosen",LaunchId="3"},
                new Game {Id="modern2",Name="D Second modern",Source="steam",Art=portrait,LaunchId="4"}
            }};
            var flags=BindingFlags.Instance|BindingFlags.NonPublic;
            foreach(string mode in new[]{"fixed","fit"}) foreach(string layout in new[]{"angled","rectangular","grid"}) {
                Storage.Write("library.json",library); Storage.Write("settings.json",new Settings {CarouselLayout=layout,AngledPanelSizing=mode,SelectedId="square",ShowPanelOutlines=true});
                var window=new Launcher(false,true);
                try {
                    Layout(window);
                    var panels=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(window);
                    Check(panels.Count==4,"Every cover is represented");
                    Check(Math.Abs(panels["modern"].Height-panels["wide"].Height)<1,"Libretro and modern panels at the same depth have the same height in "+layout+" / "+mode);
                    foreach(string id in new[]{"square","wide"}) {
                        var panel=panels[id]; var frame=Find(panel,"ArtworkFrame"); var image=(Image)Find(panel,"GameArtwork");
                        Check(Math.Abs(frame.Height-panel.Height)<1,"Libretro artwork frame keeps the panel height in "+layout+" / "+mode);
                        if(mode=="fit") Check(Math.Abs(image.ActualHeight-panel.Height)<1,"Fitted cover fills panel height");
                        else {
                            double referenceHeight=layout=="angled" ? Math.Min(panel.Height,((id=="square" ? 355 : 300)-73)*1.5) : panel.Height;
                            Check(Math.Abs(image.ActualHeight-referenceHeight)<1,"Fixed Libretro artwork matches the modern reference image height");
                            if(id=="wide") {
                                var modernImage=(Image)Find(panels["modern"],"GameArtwork");
                                Check(Math.Abs((panel.Height-image.ActualHeight)-(panels["modern"].Height-modernImage.ActualHeight))<1,"Modern and retro panels at matching depth have equal blurred-edge heights");
                            }
                        }
                        Check(image.Stretch==Stretch.Uniform,"Libretro proportions are preserved");
                        Check(Math.Abs(image.ActualWidth/image.ActualHeight-(id=="square" ? 1 : 320.0/180))<.02,"Libretro cover is not stretched or cropped");
                        var backdrop=(Border)Find(panel,"ArtworkBackdrop");
                        Check(backdrop!=null && backdrop.Effect==null && backdrop.Background is LinearGradientBrush,"Libretro reuses the original colour-edge fill");
                        Check(Math.Abs(((LinearGradientBrush)backdrop.Background).GradientStops[0].Color.R-((LinearGradientBrush)ArtworkColours.Fill((BitmapSource)image.Source,frame.Height,image.ActualHeight)).GradientStops[0].Color.R)<1,"Existing fill colours are reused");
                    }
                    if(layout=="grid") {
                        var ordered=panels.Values.OrderBy(panel => Canvas.GetLeft(panel)).ToList();
                        for(int i=1;i<ordered.Count;i++) Check(Math.Abs(VisibleGap(ordered[i-1],ordered[i],false)-16)<1,"Grid panels have a consistent visible gap");
                        Check(panels.Values.All(panel => Math.Abs(panel.Height-panels["modern"].Height)<1),"Grid panels share one height");
                        Check(ordered.Last().Width+Canvas.GetLeft(ordered.Last())-Canvas.GetLeft(ordered.First())<=1441,"Grid fits the available width");
                    } else {
                        var ordered=panels.Values.OrderBy(panel => Canvas.GetLeft(panel)).ToList();
                        for(int i=1;i<ordered.Count;i++) Check(Math.Abs(VisibleGap(ordered[i-1],ordered[i],layout=="angled")-16)<1,"Modern and retro neighbours keep the same minimum visible gap at different heights");
                        double selectedHeight=panels["square"].Height;
                        typeof(Launcher).GetMethod("Move",flags).Invoke(window,new object[]{-1}); Layout(window);
                        panels=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(window);
                        Check(Math.Abs(panels["modern"].Height-selectedHeight)<1,"Selected Libretro and modern panels use the same reference height");
                    }
                    if(args.Length>0) {
                        Directory.CreateDirectory(args[0]);
                        var bitmap=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32); bitmap.Render((Visual)window.Content);
                        var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using(var stream=File.Create(Path.Combine(args[0],layout+"-"+mode+".png"))) encoder.Save(stream);
                    }
                    if(layout=="grid") {var state=(Settings)typeof(Launcher).GetField("settings",flags).GetValue(window); state.ShowGameTitle=false; state.ShowCollectionName=false;}
                    window.OpenGameSearch(); Layout(window);
                    if(layout!="grid") {
                        var searchCanvas=(FrameworkElement)typeof(Launcher).GetField("carousel",flags).GetValue(window);
                        var searchHost=(FrameworkElement)typeof(Launcher).GetField("carouselHost",flags).GetValue(window);
                        Check(!searchCanvas.ClipToBounds && !searchHost.ClipToBounds && searchCanvas.Height==490,"Search keeps the full carousel canvas so tall covers are not clipped before scaling");
                    }
                    foreach(var panel in ((Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(window)).Values) {
                        var image=(Image)Find(panel,"GameArtwork"); var artFrame=Find(panel,"ArtworkFrame"); var bitmap=(BitmapSource)image.Source;
                        Check(Find(panel,"ArtworkBackdrop")==null && image.Stretch==Stretch.Uniform,"Search artwork is fitted without blur in "+layout+" / "+mode);
                        Check(Math.Abs(artFrame.Width/artFrame.Height-(double)bitmap.PixelWidth/bitmap.PixelHeight)<.02,"Search panels fit the whole artwork without cropping in "+layout+" / "+mode);
                    }
                    for(int step=0;step<6 && !window.SearchGamesFocused;step++) {window.HandleGameSearchController(new PadState(),step*2); window.HandleGameSearchController(new PadState {Buttons=1},step*2+1);} Layout(window);
                    Check(window.SearchGamesFocused,"Finishing search focuses the panels in "+layout);
                    if(layout=="grid") {
                        var gridHost=(FrameworkElement)typeof(Launcher).GetField("carouselHost",flags).GetValue(window);
                        var gameTitle=(TextBlock)typeof(Launcher).GetField("title",flags).GetValue(window);
                        var console=(TextBlock)typeof(Launcher).GetField("searchConsoleLabel",flags).GetValue(window);
                        var stage=(FrameworkElement)typeof(Launcher).GetField("scene",flags).GetValue(window);
                        Check(gridHost.Margin.Top+gridHost.ActualHeight<=gameTitle.Margin.Top && console.Margin.Top+console.ActualHeight<stage.Height,"Focused Grid results reserve space for title and console above the taskbar");
                    }
                    Check(((Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(window)).Values.All(panel => Find(panel,"ArtworkBackdrop")==null),"Focused search results keep blur disabled in "+layout);
                    window.HandleGameSearchController(new PadState(),2); window.HandleGameSearchController(new PadState {Buttons=0x2000},3);
                    Check(!window.GameSearchOpen && Storage.Read<Settings>("settings.json").AngledPanelSizing==mode,"Cancelling restores the configured sizing mode");
                } finally {window.ExitApp();}
            }
            Storage.Write("library.json",library); Storage.Write("settings.json",new Settings {CarouselLayout="rectangular",SelectedId="square"});
            var sizingWindow=new Launcher(false,true);
            try {
                Layout(sizingWindow);
                var sizingCards=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(sizingWindow);
                double originalRetroHeight=sizingCards["square"].Height,originalModernHeight=sizingCards["modern"].Height;
                sizingWindow.SetRetroPanelScale(.8);
                Check(Storage.Read<Settings>("settings.json").CarouselScale==.8 && Storage.Read<Settings>("settings.json").RetroPanelScale==.8,"Locked Retro size updates both saved scales");
                sizingWindow.SetPanelSizesLocked(false); sizingWindow.SetRetroPanelScale(1.2); Layout(sizingWindow);
                Check(Math.Abs(sizingCards["square"].Height/originalRetroHeight-1.5)<.001 && sizingCards["modern"].Height==originalModernHeight,"Unlocked Retro scales only retro panel geometry");
                double retroDisplayHeight=sizingCards["square"].Height*.8;
                sizingWindow.SetCarouselScale(1); Layout(sizingWindow);
                Check(Math.Abs(sizingCards["square"].Height-retroDisplayHeight)<.01,"Unlocked Modern resizing preserves the Retro display height");
                var state=(Settings)typeof(Launcher).GetField("settings",flags).GetValue(sizingWindow);
                state.ListLayouts=new Dictionary<string,ListLayout>{{"all",new ListLayout {Layout="rectangular",PanelScale=1.1,RetroPanelScale=.7,PanelSizesLocked=false,Columns=5,Rows=2,AngledSizing="fixed"}}};
                sizingWindow.SetAvailableCollections(new[]{"all"}); Layout(sizingWindow);
                sizingCards=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(sizingWindow);
                Check(Math.Abs(sizingCards["square"].Height*1.1-originalRetroHeight*.7)<.01,"Per-list Retro scale controls the actual displayed height independently of global sizing");
            } finally {sizingWindow.ExitApp();}
            foreach(string system in new[]{"Nintendo - Nintendo Entertainment System","Nintendo - Super Nintendo Entertainment System","Nintendo - Game Boy Advance","Unknown - Console"})
            foreach(string placeholderLayout in new[]{"angled","rectangular","grid"}) foreach(string placeholderSizing in new[]{"fit","fixed"}) {
                string placeholderId="emulator:retroarch:placeholder";
                Storage.Write("library.json",new Library {Games=new List<Game> {new Game {Id=placeholderId,Name="Missing cover",Source="emulator",EmulatorId="retroarch",LaunchId="missing.rom",ArtworkSystem=system}}});
                var placeholderSettings=new Settings {CollectionId="list:retro",EnabledCollectionIds=new List<string>{"list:retro"},SelectedId=placeholderId,CarouselLayout=placeholderLayout,AngledPanelSizing=placeholderSizing,RetroArchListArtworkSource="libretro",CustomLists=new List<CustomList>{new CustomList {Id="list:retro",Name="Retro",Games=new List<string>{placeholderId}}}};
                Storage.Write("settings.json",placeholderSettings);
                var placeholderWindow=new Launcher(false,true);
                try {
                    Layout(placeholderWindow); var placeholderCards=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(placeholderWindow); var panel=placeholderCards.Values.Single();
                    double ratio=system=="Nintendo - Nintendo Entertainment System" ? .701 : system=="Nintendo - Super Nintendo Entertainment System" ? 1.384 : 1;
                    double contentHeight=panel.Height;
                    Check(panel.Tag==null && Math.Abs(panel.Width-(placeholderLayout=="angled" ? 73 : 0)-contentHeight*ratio)<.02,"Missing Libretro artwork uses measured "+system+" proportions in "+placeholderLayout+" / "+placeholderSizing);
                } finally {placeholderWindow.ExitApp();}
                placeholderSettings.ListArtworkSources=new Dictionary<string,string>{{"list:retro","sgdb"}}; Storage.Write("settings.json",placeholderSettings);
                var modernPlaceholder=new Launcher(false,true);
                try {
                    Layout(modernPlaceholder); var panel=((Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(modernPlaceholder)).Values.Single();
                    Check(Math.Abs(panel.Width-(placeholderLayout=="angled" ? 73 : 0)-panel.Height)>1,"Explicit SGDB list override retains its modern placeholder in "+placeholderLayout+" / "+placeholderSizing);
                } finally {modernPlaceholder.ExitApp();}
            }
            var regionGame=new Game {Source="emulator",EmulatorId="retroarch",ArtworkSystem="Nintendo - Super Nintendo Entertainment System",ArtworkTitle="Game (Japan)"};
            var regionSettings=new Settings();
            Check(Math.Abs(LibretroPlaceholderSizing.Ratio(regionGame,regionSettings)-.55)<.001,"Japanese SNES placeholders use measured portrait packaging");
            regionGame.ArtworkTitle="Game (USA)";
            Check(LibretroPlaceholderSizing.Ratio(regionGame,regionSettings)>1.3,"Changing region metadata invalidates cached placeholder sizing");
            regionGame.ArtworkSystem="Nintendo - Nintendo 64"; regionGame.ArtworkTitle="Game (Japan)";
            Check(LibretroPlaceholderSizing.Ratio(regionGame,regionSettings)<.8,"Japanese N64 packaging is portrait");
            regionGame.ArtworkTitle="Game (Europe)";
            Check(LibretroPlaceholderSizing.Ratio(regionGame,regionSettings)>1.3,"European N64 packaging is landscape");
            regionGame.ArtworkSystem="Sony - PlayStation Portable";
            Check(LibretroPlaceholderSizing.Ratio(regionGame,regionSettings)<.6,"PSP uses narrow portrait placeholders");
            regionGame.ArtworkSystem="Sony - PlayStation";
            Check(LibretroPlaceholderSizing.Ratio(regionGame,regionSettings)==1,"PlayStation uses sampled square jewel-case proportions");
            // Wide covers in a dense grid reduce the common height uniformly, not
            // just the height of the Libretro panels, and never overflow horizontally.
            library.Games.Add(new Game {Id="extra",Name="Extra",Source="steam",Art=wide,ArtworkProvider="libretro",LaunchId="4"});
            Storage.Write("library.json",library); Storage.Write("settings.json",new Settings {CarouselLayout="grid",AngledPanelSizing="fit",GridColumns=2,GridRows=2});
            var varied=new Launcher(false,true);
            try {
                Layout(varied); var panels=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(varied);
                foreach(var row in panels.Values.GroupBy(panel => Math.Round(Canvas.GetTop(panel),2))) {
                    var ordered=row.OrderBy(panel => Canvas.GetLeft(panel)).ToList();
                    for(int i=1;i<ordered.Count;i++) Check(Math.Abs(VisibleGap(ordered[i-1],ordered[i],false)-16)<1,"Different-width Grid rows retain the same visible gap");
                }
            } finally {varied.ExitApp();}
            library.Games=Enumerable.Range(0,10).Select(i => new Game {Id=i.ToString(),Name="Cover "+i,Source="steam",Art=wide,ArtworkProvider="libretro",LaunchId="1"}).ToList();
            Storage.Write("library.json",library); Storage.Write("settings.json",new Settings {CarouselLayout="grid",AngledPanelSizing="fit",GridColumns=5,GridRows=2});
            var dense=new Launcher(false,true);
            try {
                Layout(dense); var cards=(Dictionary<string,FrameworkElement>)typeof(Launcher).GetField("cards",flags).GetValue(dense);
                Check(cards.Count==10 && cards.Values.Select(panel => Math.Round(panel.Height,3)).Distinct().Count()==1,"Dense grids keep all panels at one common height");
                Check(cards.Values.Max(panel => Canvas.GetLeft(panel)+panel.Width)-cards.Values.Min(panel => Canvas.GetLeft(panel))<=1441,"Dense landscape grids fit without clipping");
                Check(cards.Values.All(panel => Math.Abs(((Image)Find(panel,"GameArtwork")).ActualHeight-panel.Height)<1),"Dense grids still fill artwork height");
            } finally {dense.ExitApp();}
            Console.WriteLine(checks+" Libretro panel checks passed.");
        } finally {Storage.Root=previous; Directory.Delete(root,true);}
    }
}
