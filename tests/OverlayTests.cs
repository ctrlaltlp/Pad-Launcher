using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using PadLauncher;

public static class OverlayTests {
    static int checks;
    static void Check(bool ok,string name) { if (!ok) throw new Exception("FAILED: "+name); checks++; Console.WriteLine("PASS "+name); }
    static bool ContainsButton(DependencyObject root) {
        if (root is Button) return true;
        for (int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) if (ContainsButton(VisualTreeHelper.GetChild(root,i))) return true;
        return false;
    }
    static FrameworkElement FindElement(DependencyObject root,string name) {
        var element=root as FrameworkElement;
        if(element!=null && element.Name==name) return element;
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++) {
            var match=FindElement(VisualTreeHelper.GetChild(root,i),name);
            if(match!=null) return match;
        }
        return null;
    }
    [STAThread] public static int Main() {
        Storage.Root=Path.Combine(Path.GetTempPath(),"PadLauncher-overlay-tests-"+Guid.NewGuid().ToString("N"));
        Launcher window=null;
        try {
            var defaults=Storage.Json().Deserialize<Settings>("{\"CollectionId\":\"all\"}");
            Check(defaults.ButtonPrompts=="PlayStation" && defaults.ShowGameTitle,"Older configuration files default to PlayStation prompts and visible title");
            Check(defaults.OpenMonitor=="cursor","Older configuration files default to the cursor's monitor");
            Check(defaults.DefaultCollectionId=="remember" && defaults.CarouselScale==1,"Older settings preserve last-collection behaviour, normal size");
            Check(Prompts.Help(defaults).Contains("×") && !Prompts.Help(defaults).Contains("LB"),"PlayStation hint uses cross, circle and L1/R1");
            defaults.ButtonPrompts="Xbox";
            Storage.Write("settings.json",defaults);
            var restored=Storage.Read<Settings>("settings.json");
            restored.OpenMonitor="\\\\.\\DISPLAY2"; Storage.Write("settings.json",restored);
            Check(Storage.Read<Settings>("settings.json").OpenMonitor=="\\\\.\\DISPLAY2","Selected display preference survives saving and reopening");
            Check(Prompts.Help(restored).Contains("LB / RB") && !Prompts.Help(restored).Contains("△"),"Xbox preference survives configuration save and reload");
            var hold=new HoldButton();
            Check(hold.Update(true,0)==ButtonGesture.None && hold.Update(false,200)==ButtonGesture.Tap,"Quick button press produces a tap on release");
            Check(hold.Update(true,300)==ButtonGesture.None && hold.Update(true,1300)==ButtonGesture.Hold && hold.Update(true,1400)==ButtonGesture.None && hold.Update(false,1500)==ButtonGesture.None,"Hold triggers once and does not also trigger a tap on release");
            hold.Update(true,2000); hold.Reset();
            Check(hold.Update(false,4000)==ButtonGesture.None,"Focus loss or controller disconnect cancels a pending gesture");
            Check(Prompts.Help(defaults).Contains("Tap Y") && Prompts.Help(new Settings()).Contains("Tap △"),"Tap refresh appears in the selected controller hint style");
            var nintendo=new Settings {ButtonPrompts="Nintendo"};
            Check(Prompts.Buttons(nintendo)[0]=="B" && Prompts.Buttons(nintendo)[1]=="A" && Prompts.Buttons(nintendo)[3]=="X" && Prompts.Buttons(nintendo)[4]=="+","Nintendo labels follow the same physical button positions");
            Check(Prompts.Help(nintendo).Contains("B  Play") && Prompts.Help(nintendo).Contains("Tap X"),"Nintendo help uses its face buttons and refresh label");
            window=new Launcher(true,true);
            Check(window.AllowsTransparency && window.Background==Brushes.Transparent && window.WindowStyle==WindowStyle.None,"Native overlay window supports desktop transparency without a frame");
            Check(window.Topmost && !window.ShowInTaskbar,"Overlay stays above desktop without a taskbar entry");
            var content=(FrameworkElement)window.Content;
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            Check(!ContainsButton(content),"Overlay contains no visible or hidden desktop buttons");
            var gameTitle=FindElement(content,"SelectedGameTitle");
            Check(defaults.ShowCollectionName,"Older settings keep the collection name visible");
            window.SetShowCollectionName(false);
            Check(FindElement(content,"SelectedCollection").Visibility==Visibility.Collapsed && !Storage.Read<Settings>("settings.json").ShowCollectionName,"Collection name can be hidden immediately and the preference is saved");
            window.SetShowCollectionName(true);
            Check(FindElement(content,"SelectedCollection").Visibility==Visibility.Visible && Storage.Read<Settings>("settings.json").ShowCollectionName,"Collection name can be restored immediately and the preference is saved");
            window.SetShowGameTitle(false);
            Check(gameTitle.Visibility==Visibility.Collapsed && !Storage.Read<Settings>("settings.json").ShowGameTitle,"Show game title setting hides the title immediately and saves the choice");
            window.SetShowGameTitle(true);
            Check(gameTitle.Visibility==Visibility.Visible && Storage.Read<Settings>("settings.json").ShowGameTitle,"Show game title setting can be restored immediately");
            var bitmap=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32); bitmap.Render(content);
            byte[] pixel=new byte[4]; bitmap.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);
            Check(pixel[3]==0,"Rendered screen corner is fully transparent");
            bitmap.CopyPixels(new Int32Rect(800,450,1,1),pixel,4,0);
            Check(pixel[3]>0,"Carousel artwork and shadow remain visible over the transparent desktop");
            window.SetCarouselScale(.8);
            var transform=FindElement(content,"GameCarousel").RenderTransform as ScaleTransform;
            Check(transform!=null && transform.ScaleX==.8 && Storage.Read<Settings>("settings.json").CarouselScale==.8,"Carousel size is applied immediately and saved");
            bool invalidScale=false; try { window.SetCarouselScale(2); } catch(ArgumentOutOfRangeException) { invalidScale=true; }
            Check(invalidScale && Storage.Read<Settings>("settings.json").CarouselScale==.8,"Out-of-range carousel sizes do not overwrite the last valid size");
            window.SetCarouselScale(1);
            Check(defaults.CarouselLayout=="angled","Older settings preserve the original angled layout");
            window.SetCarouselLayout("rectangular");
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            var panels=(Canvas)FindElement(content,"GameCarousel");
            var panelBody=(Grid)((Grid)panels.Children[0]).Children[0];
            Check(panelBody.Clip.FillContains(new Point(1,1)) && Storage.Read<Settings>("settings.json").CarouselLayout=="rectangular","Rectangular panels have square corners and the choice is saved");
            Check(VisualTreeHelper.HitTest((Grid)panels.Children[0],new Point(1,1))!=null,"Rectangular panel corners remain clickable");
            window.SetCarouselLayout("angled");
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            panelBody=(Grid)((Grid)panels.Children[0]).Children[0];
            Check(!panelBody.Clip.FillContains(new Point(1,1)) && Storage.Read<Settings>("settings.json").CarouselLayout=="angled","Switching back restores the original slanted panels");
            var hitPanel=(Grid)panels.Children[0];
            Check(VisualTreeHelper.HitTest(hitPanel,new Point(1,1))==null && VisualTreeHelper.HitTest(hitPanel,new Point(hitPanel.ActualWidth-1,hitPanel.ActualHeight-1))==null && VisualTreeHelper.HitTest(hitPanel,new Point(hitPanel.ActualWidth/2,hitPanel.ActualHeight/2))!=null,"Angled panel hit testing excludes empty corners and includes the visible panel");
            bool invalidLayout=false; try { window.SetCarouselLayout("unknown"); } catch(ArgumentException) { invalidLayout=true; }
            Check(invalidLayout && Storage.Read<Settings>("settings.json").CarouselLayout=="angled","Invalid layouts preserve the saved choice");
            window.SetCarouselLayout("grid");
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            Check(panels.Children.Count==9 && Canvas.GetTop(panels.Children[0])==10 && Canvas.GetTop(panels.Children[5])==255 && ((FrameworkElement)panels.Children[0]).Width==150,"Grid places game covers in two rows of five");
            Check(Storage.Read<Settings>("settings.json").CarouselLayout=="grid" && ((Grid)((Grid)panels.Children[0]).Children[0]).Clip.FillContains(new Point(1,1)),"Grid layout is saved and uses rectangular artwork panels");
            var gridImage=new RenderTargetBitmap(1600,900,96,96,PixelFormats.Pbgra32); gridImage.Render(content);
            var gridPng=new PngBitmapEncoder(); gridPng.Frames.Add(BitmapFrame.Create(gridImage));
            using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"grid-test-preview.png"))) gridPng.Save(stream);
            var handle=new System.Windows.Interop.WindowInteropHelper(window).EnsureHandle();
            var inputSource=System.Windows.Interop.HwndSource.FromHwnd(handle);
            window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,inputSource,0,System.Windows.Input.Key.Down) {RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            Check(((TextBlock)gameTitle).Text=="Sekiro","Grid down navigation selects the game in the next row");
            window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,inputSource,0,System.Windows.Input.Key.Up) {RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            Check(((TextBlock)gameTitle).Text=="The Witcher 3","Grid up navigation returns to the same column");
            Check(defaults.GridColumns==5,"Older settings keep five grid columns");
            window.SetGridColumns(3);
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            Check(panels.Children.Count==6 && Canvas.GetTop(panels.Children[3])==255 && Storage.Read<Settings>("settings.json").GridColumns==3,"Three columns updates both rows, page size and saved preference");
            window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,inputSource,0,System.Windows.Input.Key.Down) {RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            Check(((TextBlock)gameTitle).Text=="Red Dead Redemption 2" && panels.Children.Count==3,"Row navigation uses the chosen column count and advances to the next page");
            window.SetGridColumns(10);
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            bool gridFits=true;
            foreach(FrameworkElement tile in panels.Children) gridFits &= Canvas.GetLeft(tile)>=0 && Canvas.GetLeft(tile)+tile.Width<=1600;
            Check(gridFits && panels.Children.Count==9 && ((FrameworkElement)panels.Children[0]).Width<150,"Ten columns shrinks covers to keep the grid within the overlay");
            bool invalidColumns=false; try { window.SetGridColumns(11); } catch(ArgumentOutOfRangeException) { invalidColumns=true; }
            Check(invalidColumns && Storage.Read<Settings>("settings.json").GridColumns==10,"Invalid grid column counts preserve the saved setting");
            window.SetGridColumns(5);
            Check(defaults.GridRows==2,"Older settings retain two grid rows");
            window.SetGridRows(1);
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            Check(panels.Children.Count<=5 && Storage.Read<Settings>("settings.json").GridRows==1,"One grid row limits page size and is saved");
            window.SetGridRows(3);
            content.Measure(new Size(1600,900)); content.Arrange(new Rect(0,0,1600,900)); content.UpdateLayout();
            bool rowSizesMatch=true; foreach(FrameworkElement tile in panels.Children) rowSizesMatch &= Math.Abs(tile.Height-225)<1 && Math.Abs(tile.Width-150)<1;
            Check(rowSizesMatch && panels.Children.Count==9,"Changing row count preserves the artwork and panel dimensions");
            bool invalidRows=false; try { window.SetGridRows(6); } catch(ArgumentOutOfRangeException) { invalidRows=true; }
            Check(invalidRows && Storage.Read<Settings>("settings.json").GridRows==3,"Invalid grid row counts preserve the saved setting");
            window.SetGridRows(2);
            window.SetCarouselLayout("angled");
            window.NavigateController(new PadState());
            string beforeMapped=((TextBlock)gameTitle).Text;
            var dpadRight=new PadState {Buttons=0x0008};
            bool mappedHandled=window.TryControllerNavigationKey(System.Windows.Input.Key.Right,dpadRight);
            string afterMapped=((TextBlock)gameTitle).Text;
            window.NavigateController(dpadRight);
            Check(mappedHandled && afterMapped!=beforeMapped && ((TextBlock)gameTitle).Text==afterMapped,"Mapped arrow arriving before controller polling moves only one game");
            window.NavigateController(new PadState());
            window.NavigateController(dpadRight);
            string afterPolling=((TextBlock)gameTitle).Text;
            Check(window.TryControllerNavigationKey(System.Windows.Input.Key.Right,dpadRight) && ((TextBlock)gameTitle).Text==afterPolling,"Mapped arrow arriving after controller polling does not move a second game");
            Check(!window.TryControllerNavigationKey(System.Windows.Input.Key.Left,dpadRight),"An unrelated keyboard direction remains available");
            Check(!window.TryControllerNavigationKey(System.Windows.Input.Key.Right,new PadState()),"An idle controller does not intercept keyboard navigation");
            window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,inputSource,0,System.Windows.Input.Key.Right) {RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            Check(((TextBlock)gameTitle).Text!=afterPolling,"Normal keyboard arrow navigation continues to work");
            Check(defaults.EnabledCollectionIds==null,"Older settings include every collection by default");
            window.SetDefaultCollection("all");
            window.SetAvailableCollections(new[]{"couch"});
            var chosenCollections=Storage.Read<Settings>("settings.json");
            Check(chosenCollections.EnabledCollectionIds.Count==1 && chosenCollections.EnabledCollectionIds[0]=="couch","Chosen available collections are saved");
            Check(chosenCollections.DefaultCollectionId=="remember" && chosenCollections.CollectionId=="couch","Hiding the current or default collection switches safely to an enabled collection");
            window.ShowOverlay();
            window.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,inputSource,0,System.Windows.Input.Key.PageDown) {RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            Check(((TextBlock)FindElement(content,"SelectedCollection")).Text=="Couch games","Collection cycling skips hidden collections, including All games");
            window.Close();
            bool hiddenDefault=false; try { window.SetDefaultCollection("all"); } catch(ArgumentException) { hiddenDefault=true; }
            Check(hiddenDefault,"A hidden collection cannot be selected as the default");
            string beforeEmptyCollections=File.ReadAllText(Path.Combine(Storage.Root,"settings.json"));
            bool emptyCollections=false; try { window.SetAvailableCollections(new string[0]); } catch(ArgumentException) { emptyCollections=true; }
            Check(emptyCollections && File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==beforeEmptyCollections,"Selecting no collections is rejected without changing saved settings");
            var restoredCollections=new Launcher(true,true); restoredCollections.ShowOverlay();
            Check(((TextBlock)FindElement((FrameworkElement)restoredCollections.Content,"SelectedCollection")).Text=="Couch games","Collection visibility survives restarting the launcher");
            restoredCollections.ExitApp();
            var missingCollectionSettings=Storage.Read<Settings>("settings.json"); missingCollectionSettings.EnabledCollectionIds=new System.Collections.Generic.List<string> {"removed-collection"}; Storage.Write("settings.json",missingCollectionSettings);
            var missingCollections=new Launcher(true,true);
            Check(((TextBlock)FindElement((FrameworkElement)missingCollections.Content,"SelectedCollection")).Text=="No available collections" && ((TextBlock)FindElement((FrameworkElement)missingCollections.Content,"SelectedGameTitle")).Text.Contains("Settings"),"Removed Steam collections do not expose hidden collections and provide a recovery instruction");
            missingCollections.ExitApp();
            window.SetAvailableCollections(null);
            Check(Storage.Read<Settings>("settings.json").EnabledCollectionIds==null,"All-collections mode can be restored for current and future collections");
            window.SetDefaultCollection("couch"); window.ShowOverlay();
            Check(((TextBlock)FindElement(content,"SelectedCollection")).Text=="Couch games","Chosen default collection is used when reopening the overlay");
            window.Close(); window.SetDefaultCollection("all"); window.ShowOverlay();
            Check(((TextBlock)FindElement(content,"SelectedCollection")).Text=="All games","All games can be selected as the opening collection");
            window.Close(); window.SetDefaultCollection("remember");
            window.ShowControls(); // Exercises optional hint with no settings dialog.
            var resident=new Launcher(true,true);
            resident.PrepareTray();
            Check(resident.Idle,"Tray startup prepares the overlay without showing it or polling the controller");
            resident.Close();
            Check(resident.Idle,"Dismissing the overlay leaves it idle with controller polling stopped");
            object retainedContent=resident.Content;
            var retainedCarousel=(Canvas)FindElement((FrameworkElement)resident.Content,"GameCarousel");
            object retainedPanel=retainedCarousel.Children[0];
            resident.ToggleOverlay();
            Check(resident.IsVisible && Object.ReferenceEquals(retainedContent,resident.Content),"Reopening the overlay reuses the existing visual tree");
            Check(Object.ReferenceEquals(retainedPanel,retainedCarousel.Children[0]),"Reopening the same collection keeps its artwork panels instead of rebuilding them");
            resident.ApplyControllerState(new PadState {Buttons=0x2000},"test-pad",0);
            resident.ShowOverlay();
            resident.ApplyControllerState(new PadState(),"test-pad",50);
            Check(resident.Idle,"A duplicate open request cannot swallow a pending Circle release");
            var cycleTime=System.Diagnostics.Stopwatch.StartNew();
            bool cyclesWorked=true;
            for(int cycle=0;cycle<20;cycle++) {
                resident.ShowOverlay();
                cyclesWorked &= resident.IsVisible && Object.ReferenceEquals(retainedPanel,retainedCarousel.Children[0]);
                resident.ApplyControllerState(new PadState {Buttons=0x2000},"test-pad",cycle*100);
                resident.ApplyControllerState(new PadState(),"test-pad",cycle*100+30);
                cyclesWorked &= resident.Idle;
            }
            cycleTime.Stop();
            Check(cyclesWorked,"Twenty rapid open/Circle-close cycles retain panels and dismiss every time");
            Console.WriteLine("Warm preview open/close cycles: "+cycleTime.ElapsedMilliseconds+"ms for 20 cycles (hardware input excluded)");
            resident.ShowOverlay();
            resident.ToggleOverlay();
            Check(resident.Idle,"Reopened overlay can be dismissed back to idle again");
            var helpDialog=resident.CreateHelpWindow();
            var helpRoot=(FrameworkElement)helpDialog.Content;
            var helpTabs=(TabControl)FindElement(helpRoot,"HelpSettingsTabs");
            var helpPage=(ScrollViewer)((TabItem)helpTabs.Items[0]).Content;
            var settingsPage=(ScrollViewer)((TabItem)helpTabs.Items[1]).Content;
            Check(helpTabs.Items.Count==4 && (string)((TabItem)helpTabs.Items[0]).Header=="Layout" && (string)((TabItem)helpTabs.Items[1]).Header=="Appearance" && (string)((TabItem)helpTabs.Items[2]).Header=="System","Help and settings have separate named tabs");
            Check(helpPage.VerticalScrollBarVisibility==ScrollBarVisibility.Auto && settingsPage.VerticalScrollBarVisibility==ScrollBarVisibility.Auto,"Both tabs show their slim scrollbar only when scrolling is needed");
            helpTabs.SelectedIndex=1;
            helpRoot.Measure(new Size(540,650)); helpRoot.Arrange(new Rect(0,0,540,650)); helpRoot.UpdateLayout();
            settingsPage.ScrollToVerticalOffset(100); helpRoot.UpdateLayout();
            Check(settingsPage.ScrollableHeight>0 && settingsPage.VerticalOffset>0,"Settings remain scrollable with the slim scrollbar");
            var slimBar=(System.Windows.Controls.Primitives.ScrollBar)settingsPage.Template.FindName("PART_VerticalScrollBar",settingsPage);
            slimBar.ApplyTemplate();
            var mintThumb=(System.Windows.Controls.Primitives.Thumb)slimBar.Template.FindName("MintThumb",slimBar);
            Check(slimBar.ActualWidth<=6.1 && ((SolidColorBrush)mintThumb.Background).Color==Color.FromRgb(163,240,205),"Scrollbar is six pixels wide and matches the mint selection colour");
            var settingsStyleButtons=(StackPanel)FindElement((FrameworkElement)helpPage.Content,"ButtonStyleChoices");
            ((Button)settingsStyleButtons.Children[1]).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(helpTabs.SelectedIndex==1 && ((TextBlock)FindElement((FrameworkElement)helpPage.Content,"LaunchButtonGuide")).Text=="A","Button style lives with Help and updates its controls guide");
            var settingLayoutPicker=(ComboBox)FindElement((FrameworkElement)settingsPage.Content,"CarouselLayoutSetting");
            settingLayoutPicker.SelectedValue="grid";
            Check(FindElement((FrameworkElement)settingsPage.Content,"GridColumnsSettings").Visibility==Visibility.Visible,"Grid column settings appear when Grid is selected");
            var sizingSettings=FindElement((FrameworkElement)settingsPage.Content,"AngledPanelSizingSettings");
            Check(sizingSettings.Visibility==Visibility.Collapsed,"Angled panel sizing is hidden in Grid layout");
            settingLayoutPicker.SelectedValue="rectangular";
            Check(sizingSettings.Visibility==Visibility.Collapsed,"Angled panel sizing is hidden in Rectangular layout");
            settingLayoutPicker.SelectedValue="angled";
            Check(sizingSettings.Visibility==Visibility.Visible,"Angled panel sizing appears in Angled layout");
            Check(FindElement((FrameworkElement)settingsPage.Content,"GridColumnsSettings").Visibility==Visibility.Collapsed,"Grid column settings disappear for other layouts");
            settingsPage.ScrollToTop(); helpRoot.UpdateLayout();
            var wheelItems=new System.Collections.Generic.List<string>(); for(int i=0;i<100;i++) wheelItems.Add("Game "+i);
            var wheelCombo=new SettingsComboBox {ItemsSource=wheelItems,SelectedIndex=0,Style=(Style)helpDialog.Resources[typeof(SettingsComboBox)]};
            var wheelStack=new StackPanel(); wheelStack.Children.Add(wheelCombo); wheelStack.Children.Add(new Border {Height=900});
            var wheelParent=new ScrollViewer {Content=wheelStack};
            var wheelWindow=new Window {Content=wheelParent,Width=300,Height=200,Left=-20000,Top=-20000,ShowInTaskbar=false};
            wheelWindow.Show(); wheelWindow.UpdateLayout();
            wheelCombo.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,-120) {RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent}); wheelWindow.UpdateLayout();
            bool closedWheel=wheelParent.VerticalOffset>0 && wheelCombo.SelectedIndex==0;
            wheelParent.ScrollToTop(); wheelWindow.UpdateLayout(); wheelCombo.IsDropDownOpen=true; wheelWindow.UpdateLayout();
            var dropScroll=(ScrollViewer)wheelCombo.Template.FindName("DropDownScroll",wheelCombo); dropScroll.UpdateLayout();
            wheelCombo.RaiseEvent(new System.Windows.Input.MouseWheelEventArgs(System.Windows.Input.Mouse.PrimaryDevice,0,-120) {RoutedEvent=System.Windows.Input.Mouse.PreviewMouseWheelEvent}); dropScroll.UpdateLayout(); wheelWindow.UpdateLayout();
            bool openWheel=dropScroll.VerticalOffset>0 && wheelParent.VerticalOffset==0 && wheelCombo.SelectedIndex==0;
            wheelCombo.IsDropDownOpen=false; wheelWindow.Close();
            Check(closedWheel,"Closed dropdown wheel scrolls settings without changing selection");
            Check(openWheel,"Open game dropdown wheel scrolls its list without moving settings or changing selection");
            var helpScreenshot=new RenderTargetBitmap(540,650,96,96,PixelFormats.Pbgra32); helpScreenshot.Render(helpRoot);
            var helpEncoder=new PngBitmapEncoder(); helpEncoder.Frames.Add(BitmapFrame.Create(helpScreenshot));
            using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"settings-tabs-preview.png"))) helpEncoder.Save(stream);
            helpDialog.Close();
            resident.ExitApp();
            string artworkPath=Path.Combine(Storage.Root,"artwork.png");
            var artworkDrawing=new DrawingVisual();
            using(var drawing=artworkDrawing.RenderOpen()) {
                drawing.DrawRectangle(Brushes.DarkCyan,null,new Rect(0,0,100,150));
                drawing.DrawRectangle(Brushes.Coral,null,new Rect(0,0,30,150));
            }
            var artworkBitmap=new RenderTargetBitmap(100,150,96,96,PixelFormats.Pbgra32); artworkBitmap.Render(artworkDrawing);
            var artworkPng=new PngBitmapEncoder(); artworkPng.Frames.Add(BitmapFrame.Create(artworkBitmap));
            using(var stream=File.Create(artworkPath)) artworkPng.Save(stream);
            string originalArtwork=Convert.ToBase64String(File.ReadAllBytes(artworkPath));
            var artworkLibrary=new Library(); artworkLibrary.Games.Add(new Game {Id="art-test",Name="Artwork test",Art=artworkPath,LaunchId="1"});
            Storage.Write("settings.json",new Settings()); Storage.Write("library.json",artworkLibrary);
            var artworkWindow=new Launcher(false,true); var artworkContent=(FrameworkElement)artworkWindow.Content;
            artworkContent.Measure(new Size(1600,900)); artworkContent.Arrange(new Rect(0,0,1600,900)); artworkContent.UpdateLayout();
            var artworkImage=(Image)FindElement(artworkContent,"GameArtwork");
            var artworkFrame=(FrameworkElement)FindElement(artworkContent,"ArtworkFrame");
            var artworkMatrix=artworkFrame.RenderTransform.Value;
            double artworkPanelWidth=((FrameworkElement)VisualTreeHelper.GetParent(artworkFrame)).ActualWidth;
            Check(Math.Abs(artworkMatrix.Transform(new Point(0,0)).X-73)<.01 && Math.Abs(artworkMatrix.Transform(new Point(0,artworkFrame.Height)).X)<.01 && Math.Abs(artworkMatrix.Transform(new Point(artworkFrame.Width,0)).X-artworkPanelWidth)<.01,"Angled artwork frame follows the full panel parallelogram");
            var artworkOrigin=artworkImage.TranslatePoint(new Point(0,0),artworkFrame);
            Check(Math.Abs(artworkOrigin.X)<1 && Math.Abs(artworkOrigin.Y)<1 && Math.Abs(artworkImage.ActualWidth-artworkFrame.ActualWidth)<1 && Math.Abs(artworkImage.ActualHeight-artworkFrame.ActualHeight)<1,"Angled panels trim unused space while retaining the full artwork");
            Check(artworkPanelWidth<=356 && ((FrameworkElement)VisualTreeHelper.GetParent(artworkFrame)).ActualHeight<=491,"Trimmed angled panels stay within the original size limits");
            var backdropImage=(Border)FindElement(artworkContent,"ArtworkBackdrop");
            Check(backdropImage!=null && backdropImage.Background is LinearGradientBrush && backdropImage.Effect==null && ((Grid)artworkFrame).Children.IndexOf(backdropImage)<((Grid)artworkFrame).Children.IndexOf(artworkImage) && ((Grid)artworkFrame).ClipToBounds,"Colour-only fill sits behind the artwork without repeating image details");
            var colourDrawing=new DrawingVisual();
            using(var drawing=colourDrawing.RenderOpen()) {
                drawing.DrawRectangle(Brushes.Lime,null,new Rect(0,0,100,100));
                drawing.DrawRectangle(Brushes.Red,null,new Rect(0,0,100,25));
                drawing.DrawRectangle(Brushes.Blue,null,new Rect(0,75,100,25));
                drawing.DrawRectangle(Brushes.White,null,new Rect(10,0,2,25));
            }
            var colourBitmap=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32); colourBitmap.Render(colourDrawing); colourBitmap.Freeze();
            var edgeColours=ArtworkColours.Background(colourBitmap);
            Check(edgeColours.GradientStops[0].Color.R>=240 && edgeColours.GradientStops[0].Color.G<32 && edgeColours.GradientStops[0].Color.B<32 && edgeColours.GradientStops[3].Color.B>=240 && edgeColours.GradientStops[3].Color.R<32 && edgeColours.GradientStops[3].Color.G<32,"Fill samples dominant colours from the top and bottom quarters, ignoring the middle and small lettering");
            Check(Object.ReferenceEquals(edgeColours,ArtworkColours.Background(colourBitmap)) && edgeColours.IsFrozen,"Sampled colour fills are cached for responsive navigation");
            using(var drawing=colourDrawing.RenderOpen()) {
                drawing.DrawRectangle(Brushes.Red,null,new Rect(0,0,100,100));
                drawing.DrawRectangle(Brushes.Blue,null,new Rect(0,75,100,25));
                drawing.DrawRectangle(Brushes.White,null,new Rect(15,5,70,20));
                drawing.DrawRectangle(Brushes.White,null,new Rect(15,75,70,20));
            }
            var logoBitmap=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32); logoBitmap.Render(colourDrawing); logoBitmap.Freeze();
            var logoColours=ArtworkColours.Background(logoBitmap);
            Check(logoColours.GradientStops[0].Color.R>=240 && logoColours.GradientStops[0].Color.G<32 && logoColours.GradientStops[0].Color.B<32 && logoColours.GradientStops[3].Color.B>=240 && logoColours.GradientStops[3].Color.R<32 && logoColours.GradientStops[3].Color.G<32,"Large white titles cannot overpower the background colour at the artwork edges");
            using(var drawing=colourDrawing.RenderOpen()) drawing.DrawRectangle(Brushes.White,null,new Rect(0,0,100,100));
            var whiteBitmap=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32); whiteBitmap.Render(colourDrawing); whiteBitmap.Freeze();
            Check(ArtworkColours.Background(whiteBitmap).GradientStops[0].Color==Colors.White,"Genuinely white artwork backgrounds remain supported");
            var darkPixels=new byte[40*40*4];
            for(int y=0;y<40;y++) for(int x=0;x<40;x++) {
                int darkOffset=(y*40+x)*4; darkPixels[darkOffset+3]=255;
                if(x%3==0) { darkPixels[darkOffset+(y<20 ? 2 : 0)]=(byte)(60+(x%6)*20); darkPixels[darkOffset+1]=20; darkPixels[darkOffset+(y<20 ? 0 : 2)]=12; }
            }
            var darkBitmap=BitmapSource.Create(40,40,96,96,PixelFormats.Bgra32,null,darkPixels,160); darkBitmap.Freeze();
            var darkColours=ArtworkColours.Background(darkBitmap);
            Check(darkColours.GradientStops[0].Color.R>darkColours.GradientStops[0].Color.G*2 && darkColours.GradientStops[3].Color.B>darkColours.GradientStops[3].Color.R*2,"Edge blending retains red and blue tones alongside black shadows");
            var neutralPixels=new byte[40*40*4];
            for(int i=0;i<40*40;i++) { bool accent=i%5==0; neutralPixels[i*4]=neutralPixels[i*4+1]=(byte)(accent ? 0 : 128); neutralPixels[i*4+2]=(byte)(accent ? 200 : 128); neutralPixels[i*4+3]=255; }
            var neutralBitmap=BitmapSource.Create(40,40,96,96,PixelFormats.Bgra32,null,neutralPixels,160); neutralBitmap.Freeze();
            var neutralColour=ArtworkColours.Background(neutralBitmap).GradientStops[0].Color;
            Check(neutralColour.G>80 && neutralColour.B>80,"A small red accent cannot replace a mostly neutral background");
            using(var drawing=colourDrawing.RenderOpen()) {
                drawing.DrawRectangle(new SolidColorBrush(Color.FromRgb(220,220,220)),null,new Rect(0,0,100,100));
                var badge=new StreamGeometry(); using(var shape=badge.Open()) { shape.BeginFigure(new Point(0,0),true,true); shape.LineTo(new Point(45,0),true,false); shape.LineTo(new Point(0,45),true,false); }
                drawing.DrawGeometry(Brushes.Gold,null,badge);
            }
            var badgeBitmap=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32); badgeBitmap.Render(colourDrawing); badgeBitmap.Freeze();
            var badgeColour=ArtworkColours.Background(badgeBitmap).GradientStops[0].Color;
            Check(badgeColour.B>=210 && Math.Abs(badgeColour.R-badgeColour.B)<15,"A yellow corner badge cannot tint a predominantly light grey background");
            using(var drawing=colourDrawing.RenderOpen()) drawing.DrawRectangle(Brushes.Gold,null,new Rect(0,0,100,100));
            var yellowBitmap=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32); yellowBitmap.Render(colourDrawing); yellowBitmap.Freeze();
            Check(ArtworkColours.Background(yellowBitmap).GradientStops[0].Color==Colors.Gold,"Genuinely yellow backgrounds keep their colour");
            var bandGradient=ArtworkColours.Fill(logoBitmap,500,400);
            Check(Math.Abs(bandGradient.GradientStops[1].Offset-.1)<.001 && Math.Abs(bandGradient.GradientStops[2].Offset-.9)<.001 && bandGradient.GradientStops[0].Color.R<bandGradient.GradientStops[1].Color.R && bandGradient.GradientStops[3].Color.B<bandGradient.GradientStops[2].Color.B,"Each visible gap fades smoothly between its image edge and a darker outer edge");
            using(var drawing=colourDrawing.RenderOpen()) drawing.DrawRectangle(Brushes.Black,null,new Rect(0,0,100,100));
            var blackBitmap=new RenderTargetBitmap(100,100,96,96,PixelFormats.Pbgra32); blackBitmap.Render(colourDrawing); blackBitmap.Freeze();
            Check(ArtworkColours.Background(blackBitmap).GradientStops[0].Color==Colors.Black,"Genuinely black artwork remains black instead of receiving an invented tint");
            Check(defaults.AngledPanelSizing=="fit","Older settings keep the current fit-artwork sizing");
            artworkWindow.SetAngledPanelSizing("fixed");
            artworkContent.Measure(new Size(1600,900)); artworkContent.Arrange(new Rect(0,0,1600,900)); artworkContent.UpdateLayout();
            artworkFrame=(FrameworkElement)FindElement(artworkContent,"ArtworkFrame"); artworkImage=(Image)FindElement(artworkContent,"GameArtwork");
            var fixedPanel=(FrameworkElement)VisualTreeHelper.GetParent(artworkFrame);
            Check(Math.Abs(fixedPanel.ActualWidth-355)<1 && Math.Abs(fixedPanel.ActualHeight-490)<1 && Storage.Read<Settings>("settings.json").AngledPanelSizing=="fixed","Fixed sizing restores original panel dimensions and saves the choice");
            artworkOrigin=artworkImage.TranslatePoint(new Point(0,0),artworkFrame);
            Check(Math.Abs(artworkOrigin.Y-(artworkFrame.ActualHeight-artworkImage.ActualHeight)/2)<1 && FindElement(artworkContent,"ArtworkBackdrop")!=null,"Fixed sizing centres the full image over the sampled colour background");
            artworkWindow.SetAngledPanelSizing("fit");
            artworkContent.Measure(new Size(1600,900)); artworkContent.Arrange(new Rect(0,0,1600,900)); artworkContent.UpdateLayout();
            artworkFrame=(FrameworkElement)FindElement(artworkContent,"ArtworkFrame"); artworkImage=(Image)FindElement(artworkContent,"GameArtwork");
            Check(Math.Abs(artworkFrame.ActualHeight-artworkImage.ActualHeight)<1 && Storage.Read<Settings>("settings.json").AngledPanelSizing=="fit","Fit artwork can be restored immediately and is saved");
            bool invalidSizing=false; try { artworkWindow.SetAngledPanelSizing("unknown"); } catch(ArgumentException) { invalidSizing=true; }
            Check(invalidSizing && Storage.Read<Settings>("settings.json").AngledPanelSizing=="fit","Invalid sizing modes preserve the saved choice");
            foreach(string artworkLayout in new[]{"rectangular","grid"}) {
                artworkWindow.SetCarouselLayout(artworkLayout);
                artworkContent.Measure(new Size(1600,900)); artworkContent.Arrange(new Rect(0,0,1600,900)); artworkContent.UpdateLayout();
                artworkImage=(Image)FindElement(artworkContent,"GameArtwork");
                Check(FindElement(artworkContent,"ArtworkFrame").RenderTransform.Value.IsIdentity,"Artwork remains straight in "+artworkLayout+" layout");
                Check(FindElement(artworkContent,"ArtworkBackdrop")==null,"Colour fill does not change "+artworkLayout+" layout");
            }
            Check(Convert.ToBase64String(File.ReadAllBytes(artworkPath))==originalArtwork,"Angling artwork never modifies the source image file");
            artworkWindow.ExitApp();
            var tallGridLibrary=new Library();
            for(int i=0;i<20;i++) tallGridLibrary.Games.Add(new Game {Id="grid-"+i,Name="Grid "+i,LaunchId="1",Art=artworkPath});
            Storage.Write("library.json",tallGridLibrary); Storage.Write("settings.json",new Settings {CarouselLayout="grid",GridRows=4});
            var tallGridWindow=new Launcher(false,true); var tallGridContent=(FrameworkElement)tallGridWindow.Content;
            tallGridContent.Measure(new Size(1600,900)); tallGridContent.Arrange(new Rect(0,0,1600,900)); tallGridContent.UpdateLayout();
            var tallGridPanels=(Canvas)FindElement(tallGridContent,"GameCarousel");
            Check(tallGridPanels.Children.Count==20 && ((FrameworkElement)tallGridPanels.Children[15]).Height==225,"Four grid rows retain their original panel size, including offscreen rows");
            var tallHandle=new System.Windows.Interop.WindowInteropHelper(tallGridWindow).EnsureHandle(); var tallSource=System.Windows.Interop.HwndSource.FromHwnd(tallHandle);
            for(int i=0;i<3;i++) tallGridWindow.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,tallSource,0,System.Windows.Input.Key.Down) {RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            var bottomSelected=(FrameworkElement)tallGridPanels.Children[15];
            Check(Canvas.GetTop(bottomSelected)>=0 && Canvas.GetTop(bottomSelected)+bottomSelected.Height<=tallGridPanels.Height+1 && bottomSelected.Height==225,"Moving down scrolls the selected row fully into view without resizing it");
            for(int i=0;i<3;i++) tallGridWindow.RaiseEvent(new System.Windows.Input.KeyEventArgs(System.Windows.Input.Keyboard.PrimaryDevice,tallSource,0,System.Windows.Input.Key.Up) {RoutedEvent=System.Windows.Input.Keyboard.PreviewKeyDownEvent});
            Check(Canvas.GetTop((FrameworkElement)tallGridPanels.Children[0])==0,"Moving back up restores the first row to view");
            tallGridWindow.SetGridRows(3);
            tallGridContent.Measure(new Size(1600,900)); tallGridContent.Arrange(new Rect(0,0,1600,900)); tallGridContent.UpdateLayout();
            var thirdRow=(FrameworkElement)tallGridPanels.Children[14];
            Check(tallGridPanels.Height>=715 && Canvas.GetTop(thirdRow)+thirdRow.Height<=tallGridPanels.Height+1 && thirdRow.Height==225,"The grid expands vertically to show three full-size rows at once");
            var expandedHost=(FrameworkElement)FindElement(tallGridContent,"CarouselViewport");
            var expandedTitle=(FrameworkElement)FindElement(tallGridContent,"SelectedGameTitle");
            var expandedScene=(FrameworkElement)VisualTreeHelper.GetParent(expandedHost);
            Check(expandedHost.Margin.Top+expandedHost.ActualHeight<=expandedTitle.Margin.Top && expandedTitle.Margin.Top+expandedTitle.ActualHeight<expandedScene.ActualHeight,"Expanded grid leaves a separate title area above the screen working-area bottom");
            tallGridWindow.SetCarouselScale(1.4);
            tallGridContent.Measure(new Size(1600,900)); tallGridContent.Arrange(new Rect(0,0,1600,900)); tallGridContent.UpdateLayout();
            Check(expandedHost.Margin.Top+expandedHost.ActualHeight<=expandedTitle.Margin.Top && expandedTitle.Margin.Top+expandedTitle.ActualHeight<expandedScene.ActualHeight,"Increasing panel size cannot move the title into the taskbar area");
            tallGridWindow.SetCarouselLayout("angled");
            Check(expandedScene.Height==900 && expandedHost.Margin.Top==205,"Switching out of Grid restores the carousel's original layout");
            tallGridWindow.ExitApp();
            string steam=Path.Combine(Storage.Root,"steam"),libraryOnly=Path.Combine(Storage.Root,"library-only");
            Directory.CreateDirectory(Path.Combine(steam,"steamapps")); File.WriteAllBytes(Path.Combine(steam,"steam.exe"),new byte[0]);
            Directory.CreateDirectory(Path.Combine(libraryOnly,"steamapps"));
            string savedBefore=File.ReadAllText(Path.Combine(Storage.Root,"settings.json"));
            bool rejected=false; try { window.SetSteamFolder(libraryOnly); } catch(DirectoryNotFoundException) { rejected=true; }
            Check(rejected && File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==savedBefore,"A library drive without steam.exe is rejected without changing saved settings");
            string selectedSteam=window.SetSteamFolder(steam);
            var steamSettings=Storage.Read<Settings>("settings.json");
            Check(selectedSteam==Path.GetFullPath(steam) && steamSettings.SteamPath==selectedSteam,"Steam folder setting validates and persists the selected installation");
            Check(steamSettings.UserId==null && steamSettings.CollectionId=="all" && steamSettings.SelectedId==null,"Changing Steam installations resets profile and collection selection for rediscovery");
            Directory.CreateDirectory(Path.Combine(steam,"userdata","123")); Directory.CreateDirectory(Path.Combine(steam,"userdata","456"));
            window.SetSteamUserId("456");
            Check(Storage.Read<Settings>("settings.json").UserId=="456","Selected local Steam user ID is saved");
            string userBefore=File.ReadAllText(Path.Combine(Storage.Root,"settings.json"));
            bool invalidUser=false; try { window.SetSteamUserId("999"); } catch(InvalidOperationException) { invalidUser=true; }
            Check(invalidUser && File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==userBefore,"User IDs outside the selected installation are rejected without changing settings");
            window.SetSteamUserId(null);
            Check(Storage.Read<Settings>("settings.json").UserId==null,"Automatic Steam profile selection is saved without a fixed user ID");
            string legacy=Path.Combine(Storage.Root,"legacy"); Directory.CreateDirectory(legacy);
            string oldSettings=Path.Combine(legacy,"settings.json"); File.WriteAllText(oldSettings,"{\"ButtonPrompts\":\"Nintendo\"}");
            File.SetLastWriteTimeUtc(oldSettings,DateTime.UtcNow.AddMinutes(1));
            Storage.MigrateFrom(legacy);
            Check(Storage.Read<Settings>("settings.json").ButtonPrompts=="Nintendo","Renamed app imports the latest existing button preference");
            File.WriteAllText(Path.Combine(Storage.Root,"settings.json"),"{\"ButtonPrompts\":\"PlayStation\"}");
            File.SetLastWriteTimeUtc(Path.Combine(Storage.Root,"settings.json"),DateTime.UtcNow.AddMinutes(2));
            Storage.MigrateFrom(legacy);
            Check(Storage.Read<Settings>("settings.json").ButtonPrompts=="PlayStation","Migration preserves settings already changed in the renamed app");
            Console.WriteLine(checks+" overlay checks passed."); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally {
            if (window!=null) window.ExitApp();
            if (Directory.Exists(Storage.Root) && Path.GetFileName(Storage.Root).StartsWith("PadLauncher-overlay-tests-")) Directory.Delete(Storage.Root,true);
        }
    }
}
