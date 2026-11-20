using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace PadLauncher {
    public partial class Launcher {
        Window gameSearchWindow;
        TextBlock searchConsoleLabel;
        TextBlock searchControlHints;
        string gameSearchText="",lastSearchQuery="";
        Action<int,int> moveSearchCursor;
        Action focusSearchTopRow;
        Action activateSearchKey,backspaceSearch,spaceSearch;
        Action<bool> finishGameSearch;
        Action refreshSearchView;
        ushort searchButtons;
        int searchDirection;
        readonly NavigationRepeat searchStickRepeat=new NavigationRepeat();
        long searchNextMove;
        bool suppressSearchRelease;
        bool searchGamesFocused;
        internal bool SearchGamesFocused {get {return GameSearchOpen && searchGamesFocused;}}
        internal bool GameSearchOpen {get {return gameSearchWindow!=null;}}
        internal string GameSearchText {get {return gameSearchText;}}
        internal void SetGameSearchQuery(string query) {if(GameSearchOpen) {gameSearchText=query ?? ""; refreshSearchView();}}

        internal static List<Game> SearchMatches(IEnumerable<Game> games,string query) {
            string key=ArtworkService.SearchQuery(query).ToUpperInvariant();
            var words=key.Split(new[]{' '},StringSplitOptions.RemoveEmptyEntries);
            return games.Select(game => new {Game=game,Name=ArtworkService.SearchQuery(game.Name).ToUpperInvariant()})
                .Where(entry => words.All(word => entry.Name.Contains(word)))
                .OrderByDescending(entry => key.Length>0 && entry.Name==key)
                .ThenByDescending(entry => key.Length>0 && entry.Name.StartsWith(key,StringComparison.Ordinal))
                .ThenBy(entry => key.Length>0 ? entry.Name.Length : 0)
                .ThenBy(entry => entry.Game.Name,StringComparer.CurrentCultureIgnoreCase).Take(100).Select(entry => entry.Game).ToList();
        }
        internal static string SearchSourceLabel(Game game,Settings settings) {
            if(game.Source=="emulator") {
                if(game.EmulatorId=="eden") return "Nintendo Switch";
                var systems=LibretroArtwork.Systems(game,settings);
                if(systems.Length>0) return String.Join(" / ",systems.Select(system => system.Contains(" - ") ? system.Substring(system.IndexOf(" - ",StringComparison.Ordinal)+3) : system));
                return "Emulator";
            }
            if(game.Source=="custom") return "Custom game";
            if(game.Source=="steam" || String.IsNullOrEmpty(game.Source)) return "Steam";
            if(game.Source=="epic") return "Epic Games Store";
            if(game.Source=="gog") return "GOG Galaxy";
            if(game.Source=="ea") return "EA App";
            return CultureInfo.InvariantCulture.TextInfo.ToTitleCase(game.Source);
        }
        void ConfigureSearchViewport() {
            if(searchGamesFocused) {
                carousel.RenderTransform=new ScaleTransform(SafePanelScale(ActivePanelScale),SafePanelScale(ActivePanelScale));
                collectionText.Visibility=Visibility.Visible; title.Visibility=Visibility.Visible;
                if(ActiveLayout=="grid") title.Margin=new Thickness(0,Math.Max(0,scene.Height-84),0,0);
                if(searchConsoleLabel!=null) searchConsoleLabel.Margin=new Thickness(0,title.Margin.Top+36,0,0);
                return;
            }
            double scale=Math.Min(.78,SafePanelScale(ActivePanelScale)*.66);
            carousel.RenderTransform=new ScaleTransform(scale,scale);
            if(ActiveLayout=="grid") {
                double viewport=Math.Max(100,scene.Height-440);
                gridViewportHeight=viewport/scale;
                carouselHost.Height=viewport; carouselHost.VerticalAlignment=VerticalAlignment.Top; carouselHost.Margin=new Thickness(0,60,0,0);
                carousel.Height=gridViewportHeight; carousel.Width=1600;
                title.Margin=new Thickness(0,Math.Max(170,scene.Height-390),0,0);
            } else {
                carouselHost.Height=Double.NaN; carouselHost.Margin=new Thickness(0,55,0,455);
                title.Margin=new Thickness(0,500,0,0);
            }
            collectionText.Margin=new Thickness(0,28,0,0); collectionText.Visibility=Visibility.Visible;
            collectionText.TextTrimming=TextTrimming.CharacterEllipsis; collectionText.MaxWidth=1400;
            title.Visibility=Visibility.Visible;
            if(searchConsoleLabel!=null) searchConsoleLabel.Margin=new Thickness(0,title.Margin.Top+36,0,0);
        }
        void FitSearchCarouselViewport() {
            if(!GameSearchOpen || ActiveLayout=="grid" || visible.Count<=selected) return;
            FrameworkElement panel; if(!cards.TryGetValue(visible[selected].Id,out panel)) return;
            double width=(double)panel.GetAnimationBaseValue(WidthProperty),height=(double)panel.GetAnimationBaseValue(HeightProperty);
            double top=collectionText.Margin.Top+40,bottom=title.Margin.Top-24;
            double desired=searchGamesFocused ? SafePanelScale(ActivePanelScale) : Math.Min(.78,SafePanelScale(ActivePanelScale)*.66);
            double scale=Math.Min(desired,Math.Min(1520/width,Math.Max(100,bottom-top)/height));
            // A short keyboard viewport used to clip the bottom of tall covers
            // before scaling. Keep the full logical canvas and fit its selected panel.
            carousel.Height=490; carousel.Width=1600; carousel.VerticalAlignment=VerticalAlignment.Top; carousel.ClipToBounds=false;
            carouselHost.Height=490; carouselHost.VerticalAlignment=VerticalAlignment.Top; carouselHost.ClipToBounds=false;
            carouselHost.Margin=new Thickness(0,(top+bottom)/2-245,0,0);
            carousel.RenderTransform=new ScaleTransform(scale,scale);
        }
        internal void OpenGameSearch() {
            if(GameSearchOpen) {ResumeSearchKeyboard(); return;}
            if(!IsVisible) ShowOverlay();
            StopListTransition();
            string originalList=filters[collectionIndex].Id;
            string originalGame=visible.Count>selected ? visible[selected].Id : settings.SelectedId;
            var dialog=new Window {Name="GameSearchWindow",Title="Search games",Owner=this,Left=Left,Top=Top,Width=ActualWidth,Height=ActualHeight,WindowStartupLocation=WindowStartupLocation.Manual,WindowStyle=WindowStyle.None,ResizeMode=ResizeMode.NoResize,AllowsTransparency=true,Background=Brushes.Transparent,Topmost=true,ShowInTaskbar=false};
            gameSearchWindow=dialog; gameSearchText=lastSearchQuery; searchGamesFocused=false; ApplySettingsTheme(dialog);
            var root=new Grid {Focusable=true}; dialog.Content=root;
            double displayScale=Math.Min(ActualWidth/1600,ActualHeight/Math.Max(1,scene.Height));
            var keyboard=new StackPanel {Width=726};
            var keyboardHost=new Viewbox {Child=keyboard,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly};
            var frame=new Border {Name="SearchKeyboard",Child=keyboardHost,Background=new SolidColorBrush(Color.FromArgb(248,13,25,34)),BorderBrush=new SolidColorBrush(Color.FromRgb(58,82,96)),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Padding=new Thickness(16),Width=Math.Min(ActualWidth-24,758*displayScale),HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(0,0,0,16*displayScale)};
            root.Children.Add(frame);
            if(searchConsoleLabel==null) {searchConsoleLabel=Label("",16,muted); searchConsoleLabel.Name="SearchConsoleLabel"; searchConsoleLabel.HorizontalAlignment=HorizontalAlignment.Center; searchConsoleLabel.VerticalAlignment=VerticalAlignment.Top; searchConsoleLabel.TextAlignment=TextAlignment.Center; searchConsoleLabel.IsHitTestVisible=false; scene.Children.Add(searchConsoleLabel);}
            searchConsoleLabel.Visibility=Visibility.Visible;
            string[][] labels={"1234567890".Select(ch => ch.ToString()).ToArray(),"qwertyuiop".Select(ch => ch.ToString()).ToArray(),"asdfghjkl".Select(ch => ch.ToString()).ToArray(),"zxcvbnm".Select(ch => ch.ToString()).Concat(new[]{"Clear"}).ToArray()};
            double[] offsets={0,.5,.75,1.25};
            var keys=new List<List<Button>>(); int row=1,column=0;
            Action paint=() => {for(int y=0;y<keys.Count;y++) for(int x=0;x<keys[y].Count;x++) {bool active=y==row && x==column; keys[y][x].Background=active ? mint : new SolidColorBrush(Color.FromRgb(34,51,63)); keys[y][x].Foreground=active ? new SolidColorBrush(Color.FromRgb(13,25,34)) : white;}};
            Func<int,int,double> keyCenter=(y,x) => (offsets[y]+x+.5)*66;
            refreshSearchView=() => {
                var ids=new HashSet<string>(filters.SelectMany(list => list.Games ?? new List<string>())); bool all=filters.Any(list => list.Id=="all");
                var hidden=new HashSet<string>(settings.HiddenGameIds ?? new List<string>());
                var pool=library.Games.Where(game => !hidden.Contains(game.Id) && (all || ids.Contains(game.Id)));
                visible=SearchMatches(pool,gameSearchText).Select(game => {
                    var list=filters.FirstOrDefault(candidate => candidate.Id==originalList && (candidate.Id=="all" || (candidate.Games ?? new List<string>()).Contains(game.Id))) ?? filters.FirstOrDefault(candidate => candidate.Id=="all" || (candidate.Games ?? new List<string>()).Contains(game.Id));
                    return ArtworkService.ForList(game,settings,list==null ? originalList : list.Id);
                }).ToList();
                selected=String.IsNullOrWhiteSpace(gameSearchText) ? Math.Max(0,visible.FindIndex(game => game.Id==originalGame)) : 0;
                cards.Clear(); carousel.Children.Clear(); gridScrollPage=-1; Render(false);
            };
            finishGameSearch=accept => {
                if(!accept) {dialog.Close(); return;}
                if(visible.Count==0) return;
                searchGamesFocused=true; dialog.Hide(); searchButtons=UInt16.MaxValue; searchDirection=0; searchStickRepeat.Reset();
                cards.Clear(); carousel.Children.Clear(); ApplyCarouselScale(); Render(false); Activate(); Focus();
                if(!preview) {Gamepad.StartNative(new System.Windows.Interop.WindowInteropHelper(this).Handle); controllerTimer.Start();}
            };
            backspaceSearch=() => {int[] starts=StringInfo.ParseCombiningCharacters(gameSearchText); if(starts.Length>0) SetGameSearchQuery(gameSearchText.Substring(0,starts[starts.Length-1]));};
            spaceSearch=() => SetGameSearchQuery(gameSearchText+" ");
            Action<string> choose=key => {
                SetGameSearchQuery(key=="Clear" ? "" : gameSearchText+key);
                root.Focus();
            };
            for(int y=0;y<labels.Length;y++) {
                int columns=labels[y].Length;
                var line=new Grid {Width=columns*66,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(offsets[y]*66,0,0,0)};
                for(int x=0;x<columns;x++) line.ColumnDefinitions.Add(new ColumnDefinition());
                keyboard.Children.Add(line); var buttons=new List<Button>(); keys.Add(buttons);
                for(int x=0;x<labels[y].Length;x++) {
                    int keyRow=y,keyColumn=x; string key=labels[y][x];
                    var button=new Button {Name="SearchKey_"+y+"_"+x,Content=key,Height=37,Padding=new Thickness(0),Margin=new Thickness(3),FontSize=15,Focusable=false};
                    Grid.SetColumn(button,x);
                    button.Click+=(s,e) => {row=keyRow; column=keyColumn; choose(key); paint();}; buttons.Add(button); line.Children.Add(button);
                }
            }
            var names=Prompts.Buttons(settings);
            var hint=Label("",12,muted); searchControlHints=hint; RefreshSearchControlHints(); hint.Margin=new Thickness(3,8,3,0); hint.TextWrapping=TextWrapping.Wrap; hint.TextAlignment=TextAlignment.Center; keyboard.Children.Add(hint);
            var gamesNote=Label("Navigate above keyboard to select games",12,muted);
            gamesNote.TextAlignment=TextAlignment.Center; gamesNote.Margin=new Thickness(3,4,3,0);
            keyboard.Children.Add(gamesNote);
            moveSearchCursor=(dx,dy) => {
                if(dy<0 && row==0) {finishGameSearch(true); return;}
                if(dy!=0) {int next=row+dy; if(next>=0 && next<keys.Count) {double center=keyCenter(row,column); column=Enumerable.Range(0,keys[next].Count).OrderBy(x => Math.Abs(keyCenter(next,x)-center)).ThenBy(x => Math.Abs(x-column)).First(); row=next;}}
                else column=(column+dx+keys[row].Count)%keys[row].Count;
                paint();
            };
            focusSearchTopRow=() => {double center=keyCenter(row,column); row=0; column=Enumerable.Range(0,keys[0].Count).OrderBy(x => Math.Abs(keyCenter(0,x)-center)).First(); paint();};
            activateSearchKey=() => {choose(labels[row][column]); paint();};
            dialog.PreviewTextInput+=(s,e) => HandleSearchText(e);
            dialog.PreviewKeyDown+=(s,e) => HandleSearchKey(e);
            searchButtons=UInt16.MaxValue; searchDirection=0; searchStickRepeat.Reset();
            var poll=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(16)};
            poll.Tick+=(s,e) => {if(!dialog.IsActive || preview) return; PadState pad; if(Gamepad.Read(out pad)) HandleGameSearchController(pad,(long)(System.Diagnostics.Stopwatch.GetTimestamp()*1000.0/System.Diagnostics.Stopwatch.Frequency)); else {searchButtons=UInt16.MaxValue; searchDirection=0; searchStickRepeat.Reset();}};
            dialog.Closed+=(s,e) => {
                searchControlHints=null;
                noticeTimer.Stop(); notice.Visibility=Visibility.Collapsed;
                poll.Stop(); gameSearchWindow=null; refreshSearchView=null; moveSearchCursor=null; focusSearchTopRow=null; activateSearchKey=null; backspaceSearch=null; spaceSearch=null; finishGameSearch=null;
                lastSearchQuery=gameSearchText;
                gameSearchText=""; searchGamesFocused=false; suppressSearchRelease=true; exitButton.Reset(); navigation.Reset(); searchConsoleLabel.Visibility=Visibility.Collapsed;
                collectionText.MaxWidth=Double.PositiveInfinity; collectionText.TextTrimming=TextTrimming.None;
                carousel.ClipToBounds=true;
                if(!exiting) {
                    int previous=filters.FindIndex(list => list.Id==originalList); if(previous>=0) collectionIndex=previous;
                    settings.CollectionId=filters[collectionIndex].Id; FilterGames(false);
                    int index=visible.FindIndex(game => game.Id==originalGame); if(index>=0) selected=index;
                    RestoreTemporaryLabels(); ApplyCarouselScale(); Render(false); Activate(); Focus();
                }
                if(!preview) {if(IsVisible && !exiting) Gamepad.StartNative(new System.Windows.Interop.WindowInteropHelper(this).Handle); else Gamepad.StopNative();}
            };
            dialog.Show(); if(!preview) Gamepad.StartNative(new System.Windows.Interop.WindowInteropHelper(dialog).Handle);
            paint(); refreshSearchView(); root.Focus(); poll.Start();
        }
        void HandleSearchText(TextCompositionEventArgs e) {
            if(GameSearchOpen && !String.IsNullOrEmpty(e.Text) && !e.Text.Any(Char.IsControl)) {if(searchGamesFocused) ResumeSearchKeyboard(); SetGameSearchQuery(gameSearchText+e.Text); e.Handled=true;}
        }
        void ResumeSearchKeyboard() {
            if(!GameSearchOpen) return;
            searchGamesFocused=false; searchButtons=UInt16.MaxValue; searchDirection=0; searchStickRepeat.Reset();
            if(focusSearchTopRow!=null) focusSearchTopRow();
            cards.Clear(); carousel.Children.Clear(); Render(false);
            gameSearchWindow.Show(); gameSearchWindow.Activate(); ((Grid)gameSearchWindow.Content).Focus();
            if(!preview) Gamepad.StartNative(new System.Windows.Interop.WindowInteropHelper(gameSearchWindow).Handle);
        }
        void LaunchSearchSelection() {
            if(visible.Count==0) return;
            string id=visible[selected].Id;
            gameSearchWindow.Close();
            int destination=filters.FindIndex(list => list.Id==settings.CollectionId && (list.Id=="all" || (list.Games ?? new List<string>()).Contains(id)));
            if(destination<0) destination=filters.FindIndex(list => list.Id=="all" || (list.Games ?? new List<string>()).Contains(id));
            if(destination<0) return;
            collectionIndex=destination; settings.CollectionId=filters[destination].Id; FilterGames(false);
            int index=visible.FindIndex(game => game.Id==id); if(index<0) return;
            selected=index; Render(false); LaunchSelected();
        }
        void NavigateSearchResults(int dx,int dy) {
            if(dx!=0) {Move(dx); return;}
            if(dy>0) {
                if(ActiveLayout=="grid" && visible.Count>0 && selected/GridColumns<(visible.Count-1)/GridColumns) MoveGridRow(1);
                else {
                    ResumeSearchKeyboard();
                    searchDirection=2;
                    searchNextMove=Int64.MaxValue;
                }
            } else if(dy<0 && ActiveLayout=="grid" && selected/GridColumns>0) MoveGridRow(-1);
        }
        void HandleSearchKey(KeyEventArgs e) {
            if(!GameSearchOpen) return;
            string action=ControlBindings.KeyAction(settings,searchGamesFocused ? "results" : "typing",e.Key==Key.System ? e.SystemKey : e.Key,Keyboard.Modifiers);
            if(action==null) return;
            e.Handled=true;
            if(new[]{"left","right","up","down"}.Contains(action)) {
                PadState physical;
                if(!preview && Gamepad.Read(out physical)) {
                    var mapped=RemapController(physical,searchGamesFocused ? "results" : "typing");
                    bool controllerMove=action=="left" ? (mapped.Buttons&4)!=0 || mapped.LX< -16000 : action=="right" ? (mapped.Buttons&8)!=0 || mapped.LX>16000 : action=="up" ? (mapped.Buttons&1)!=0 || mapped.LY>16000 : (mapped.Buttons&2)!=0 || mapped.LY< -16000;
                    if(controllerMove) {HandleGameSearchController(physical,(long)(System.Diagnostics.Stopwatch.GetTimestamp()*1000.0/System.Diagnostics.Stopwatch.Frequency)); return;}
                }
                if(!searchGamesFocused && action=="down" && e.IsRepeat && searchNextMove==Int64.MaxValue) return;
                if(searchGamesFocused) NavigateSearchResults(action=="left" ? -1 : action=="right" ? 1 : 0,action=="up" ? -1 : action=="down" ? 1 : 0);
                else moveSearchCursor(action=="left" ? -1 : action=="right" ? 1 : 0,action=="up" ? -1 : action=="down" ? 1 : 0);
                return;
            }
            if(e.IsRepeat) return;
            switch(action) {
                case "type": activateSearchKey(); break;
                case "space": spaceSearch(); break;
                case "backspace": backspaceSearch(); break;
                case "finish": finishGameSearch(true); break;
                case "focus": ResumeSearchKeyboard(); break;
                case "cancel": case "cancel-alt": finishGameSearch(false); break;
                case "play": case "play-start": LaunchSearchSelection(); break;
                case "keyboard": ResumeSearchKeyboard(); break;
                case "favourite": ToggleSearchFavourite(); break;
                case "jump": finishGameSearch(false); JumpToConfiguredList(); break;
                case "layout": ToggleLayoutFromKeyboard(); break;
                case "monitor": CycleMonitor(); break;
                case "settings": OpenSettingsFromSearch(); break;
            }
        }
        internal void HandleGameSearchController(PadState pad,long milliseconds) {
            if(!GameSearchOpen) return;
            if(suppressSearchRelease) {
                if(pad.Buttons!=0 || pad.LT>80 || pad.RT>80) return;
                suppressSearchRelease=false; searchButtons=0;
            }
            pad=RemapController(pad,searchGamesFocused ? "results" : "typing");
            if(HandleLayoutToggle(pad)) return;
            ushort pressed=(ushort)(pad.Buttons & ~searchButtons); searchButtons=pad.Buttons;
            if((pressed&0x0040)!=0) {
                finishGameSearch(false); JumpToConfiguredList(); return;
            }
            if((pressed&0x0080)!=0) {ToggleSearchFavourite(); return;}
            if((pressed&0x0800)!=0) {OpenSettingsFromSearch(); return;}
            if((pressed&0x0010)!=0) {if(searchGamesFocused) ResumeSearchKeyboard(); else LaunchSearchSelection(); return;}
            if(searchGamesFocused) {
                if((pressed&0x4000)!=0) {CycleMonitor(); return;}
                if((pressed&0x8000)!=0) {ResumeSearchKeyboard(); return;}
                if((pressed&0x2000)!=0) {finishGameSearch(false); return;}
                if((pressed&0x1000)!=0) {LaunchSearchSelection(); return;}
            } else {
                if((pressed&0x0100)!=0) {finishGameSearch(false); return;}
                if((pressed&0x8000)!=0) {finishGameSearch(true); return;}
                if((pressed&0x2000)!=0) {spaceSearch(); return;}
                if((pressed&0x4000)!=0) {backspaceSearch(); return;}
                if((pressed&0x1000)!=0) {activateSearchKey(); return;}
            }
            int dpadDirection=(pad.Buttons&1)!=0 ? -2 : (pad.Buttons&2)!=0 ? 2 : (pad.Buttons&4)!=0 ? -1 : (pad.Buttons&8)!=0 ? 1 : 0;
            if(searchGamesFocused && dpadDirection==0) {
                bool grid=ActiveLayout=="grid";
                int rightStick=pad.RX< -16000 ? -1 : pad.RX>16000 ? 1 :
                    grid && pad.RY>16000 ? -2 : grid && pad.RY< -16000 ? 2 : 0;
                int stick=rightStick!=0 ? rightStick :
                    pad.LY>16000 ? -2 : pad.LY< -16000 ? 2 : pad.LX< -16000 ? -1 : pad.LX>16000 ? 1 : 0;
                searchDirection=0;
                if(searchStickRepeat.Update(stick,rightStick!=0,milliseconds,settings.RightStickSpeed,settings.LeftStickSpeed))
                    NavigateSearchResults(Math.Abs(stick)==1 ? Math.Sign(stick) : 0,Math.Abs(stick)==2 ? Math.Sign(stick) : 0);
                return;
            }
            searchStickRepeat.Reset();
            int direction=dpadDirection!=0 ? dpadDirection :
                pad.LY>16000 ? -2 : pad.LY< -16000 ? 2 : pad.LX< -16000 ? -1 : pad.LX>16000 ? 1 : 0;
            if(direction==0) {searchDirection=0; return;}
            if(direction!=searchDirection || milliseconds>=searchNextMove) {
                bool initial=direction!=searchDirection; searchDirection=direction; searchNextMove=milliseconds+(initial ? 330 : 110);
                if(searchGamesFocused) NavigateSearchResults(Math.Abs(direction)==1 ? Math.Sign(direction) : 0,Math.Abs(direction)==2 ? Math.Sign(direction) : 0);
                else moveSearchCursor(Math.Abs(direction)==1 ? Math.Sign(direction) : 0,Math.Abs(direction)==2 ? Math.Sign(direction) : 0);
            }
        }
    }
}
