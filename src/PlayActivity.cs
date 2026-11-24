using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;

namespace PadLauncher {
    public partial class Launcher {
        sealed class PlayActivityItem {
            public string Game {get;private set;}
            public string LastLaunched {get;private set;}
            public string PlayTime {get;private set;}
            public string Launches {get;private set;}
            public long LastValue {get;private set;}
            public long PlaySeconds {get;private set;}
            public long LaunchValue {get;private set;}
            public PlayActivityItem(Game game,Settings settings,bool includeImportedPlayTime) {
                Game=game.Name;
                long launched=0;
                if(settings.LaunchHistory!=null) settings.LaunchHistory.TryGetValue(game.Id,out launched);
                LastValue=Math.Max(game.LastPlayed,launched);
                PlaySeconds=PlayTimeTracker.Local(settings,game);
                if(includeImportedPlayTime) PlaySeconds=Math.Max(PlaySeconds,Math.Max(0,game.ImportedPlayTimeSeconds));
                LaunchValue=CombinedCollections.LaunchCount(settings,game);
                LastLaunched=LastValue>0 ? DateTimeOffset.FromUnixTimeSeconds(LastValue).ToLocalTime().ToString("g") : "Never";
                PlayTime=FormatPlayTime(PlaySeconds);
                Launches=LaunchValue.ToString();
            }
        }
        static string FormatPlayTime(long seconds) {
            if(seconds<=0) return "—";
            long hours=seconds/3600, minutes=(seconds%3600)/60;
            return hours>0 ? hours+"h "+minutes+"m" : Math.Max(1,minutes)+"m";
        }
        FrameworkElement CreatePlayActivityPanel(Window dialog) {
            var root=new StackPanel();
            var heading=Label("Activity",26,white); heading.FontWeight=FontWeights.SemiBold; root.Children.Add(heading);
            var note=Label("Last launched and play time for games with recorded activity. Play time includes Pad Launcher launches and tracked Steam sessions. Enable the checkbox to show Steam's total imported play time. This tab refreshes automatically.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,8,0,14); root.Children.Add(note);
            bool changed=PlayTimeTracker.Consume(settings,Storage.Root);
            if(PlayTimeTracker.ConsumeSteam(settings)) changed=true;
            if(changed) try {Storage.Write("settings.json",settings);} catch(Exception) {}
            var controlRow=new Grid {Margin=new Thickness(0,0,0,10)};
            controlRow.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(1,GridUnitType.Star)});
            controlRow.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});
            var includeImported=new CheckBox {Name="PlayActivityIncludeImportedPlayTime",Content="Include imported play time (Steam)",IsChecked=settings.IncludeImportedPlayTime,Foreground=white,FontSize=14,VerticalAlignment=VerticalAlignment.Center};
            Grid.SetColumn(includeImported,0); controlRow.Children.Add(includeImported);
            var refresh=new Button {Name="PlayActivityRefresh",Content="Refresh",Padding=new Thickness(12,4,12,4),Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};
            Grid.SetColumn(refresh,1); controlRow.Children.Add(refresh); root.Children.Add(controlRow);
            var count=Label("",12,muted); count.Name="PlayActivityCount"; count.Margin=new Thickness(0,0,0,8); root.Children.Add(count);
            var cellStyle=new Style(typeof(DataGridCell));
            cellStyle.Setters.Add(new Setter(DataGridCell.ForegroundProperty,white));
            cellStyle.Setters.Add(new Setter(DataGridCell.BackgroundProperty,Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(DataGridCell.BorderBrushProperty,Brushes.Transparent));
            cellStyle.Setters.Add(new Setter(DataGridCell.BorderThicknessProperty,new Thickness(0)));
            cellStyle.Setters.Add(new Setter(DataGridCell.PaddingProperty,new Thickness(8,6,8,6)));
            cellStyle.Setters.Add(new Setter(DataGridCell.FocusVisualStyleProperty,null));
            var rowStyle=new Style(typeof(DataGridRow));
            rowStyle.Setters.Add(new Setter(DataGridRow.ForegroundProperty,white));
            rowStyle.Setters.Add(new Setter(DataGridRow.BorderThicknessProperty,new Thickness(0)));
            var selectedRow=new Trigger {Property=DataGridRow.IsSelectedProperty,Value=true};
            selectedRow.Setters.Add(new Setter(DataGridRow.BackgroundProperty,new SolidColorBrush(Color.FromRgb(38,90,85))));
            selectedRow.Setters.Add(new Setter(DataGridRow.ForegroundProperty,white));
            rowStyle.Triggers.Add(selectedRow);
            var headerStyle=new Style(typeof(DataGridColumnHeader));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BackgroundProperty,new SolidColorBrush(Color.FromRgb(28,48,60))));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.ForegroundProperty,white));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderBrushProperty,new SolidColorBrush(Color.FromRgb(45,70,82))));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.BorderThicknessProperty,new Thickness(0,0,0,1)));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.PaddingProperty,new Thickness(8,7,8,7)));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.FontWeightProperty,FontWeights.SemiBold));
            headerStyle.Setters.Add(new Setter(DataGridColumnHeader.HorizontalContentAlignmentProperty,HorizontalAlignment.Left));
            var textStyle=new Style(typeof(TextBlock));
            textStyle.Setters.Add(new Setter(TextBlock.ForegroundProperty,white));
            textStyle.Setters.Add(new Setter(TextBlock.VerticalAlignmentProperty,VerticalAlignment.Center));
            textStyle.Setters.Add(new Setter(TextBlock.TextTrimmingProperty,TextTrimming.CharacterEllipsis));
            var grid=new DataGrid {Name="PlayActivityGames",AutoGenerateColumns=false,CanUserAddRows=false,CanUserDeleteRows=false,CanUserReorderColumns=false,CanUserResizeRows=false,CanUserSortColumns=false,IsReadOnly=true,HeadersVisibility=DataGridHeadersVisibility.Column,GridLinesVisibility=DataGridGridLinesVisibility.None,RowHeaderWidth=0,Background=Brushes.Transparent,Foreground=white,BorderBrush=new SolidColorBrush(Color.FromRgb(45,70,82)),BorderThickness=new Thickness(1),MaxHeight=560,MinHeight=220,RowBackground=new SolidColorBrush(Color.FromRgb(18,34,44)),AlternatingRowBackground=new SolidColorBrush(Color.FromRgb(22,40,51)),HorizontalGridLinesBrush=Brushes.Transparent,VerticalGridLinesBrush=Brushes.Transparent,CellStyle=cellStyle,RowStyle=rowStyle,ColumnHeaderStyle=headerStyle,ColumnHeaderHeight=34,RowHeight=32,SelectionUnit=DataGridSelectionUnit.FullRow};
            string sortKey="time";
            bool sortAscending=false;
            var sortButtons=new Dictionary<string,Button>();
            var sortLabels=new Dictionary<string,string>{{"game","Game"},{"last","Last launched"},{"time","Play time"},{"launches","Launches"}};
            Action render=null;
            Action refreshHeaders=() => {
                foreach(var pair in sortButtons) pair.Value.Content=sortLabels[pair.Key]+(pair.Key==sortKey ? (sortAscending ? " ▲" : " ▼") : "");
            };
            Action<string> activateSort=key => {
                if(sortKey==key) sortAscending=!sortAscending;
                else {sortKey=key; sortAscending=key=="game";}
                refreshHeaders(); if(render!=null) render();
            };
            Func<string,string,Button> header=(text,key) => {
                var button=new Button {Name="PlayActivitySort_"+key,Content=text,Tag=key,Background=Brushes.Transparent,Foreground=white,BorderThickness=new Thickness(0),Padding=new Thickness(0),Cursor=System.Windows.Input.Cursors.Hand,FontWeight=FontWeights.SemiBold,HorizontalContentAlignment=HorizontalAlignment.Left};
                button.Click+=(s,e) => activateSort(key);
                sortButtons[key]=button; return button;
            };
            grid.Columns.Add(new DataGridTextColumn {Header=header("Game","game"),Binding=new Binding("Game"),Width=new DataGridLength(1,DataGridLengthUnitType.Star),ElementStyle=textStyle});
            grid.Columns.Add(new DataGridTextColumn {Header=header("Play time","time"),Binding=new Binding("PlayTime"),Width=130,ElementStyle=textStyle});
            grid.Columns.Add(new DataGridTextColumn {Header=header("Launches","launches"),Binding=new Binding("Launches"),Width=90,ElementStyle=textStyle});
            grid.Columns.Add(new DataGridTextColumn {Header=header("Last launched","last"),Binding=new Binding("LastLaunched"),Width=170,ElementStyle=textStyle});
            grid.PreviewMouseDown+=(s,e) => {
                if(e.ChangedButton!=System.Windows.Input.MouseButton.Left) return;
                DependencyObject node=e.OriginalSource as DependencyObject;
                while(node!=null && !(node is DataGridColumnHeader) && !(node is Thumb)) node=VisualTreeHelper.GetParent(node);
                if(node is Thumb) return;
                var columnHeader=node as DataGridColumnHeader;
                if(columnHeader==null || columnHeader.Column==null) return;
                var button=columnHeader.Column.Header as Button;
                string key=button==null ? null : button.Tag as string;
                if(String.IsNullOrEmpty(key)) return;
                activateSort(key); e.Handled=true;
            };
            root.Children.Add(grid);
            render=() => {
                var items=(library==null || library.Games==null ? new List<Game>() : library.Games).Where(game => game!=null).Select(game => new PlayActivityItem(game,settings,settings.IncludeImportedPlayTime))
                    .Where(item => item.LastValue>0 || item.PlaySeconds>0 || item.LaunchValue>0).ToList();
                if(sortKey=="game") items=sortAscending ? items.OrderBy(item => item.Game,StringComparer.CurrentCultureIgnoreCase).ToList() : items.OrderByDescending(item => item.Game,StringComparer.CurrentCultureIgnoreCase).ToList();
                else if(sortKey=="last") items=sortAscending ? items.OrderBy(item => item.LastValue).ThenBy(item => item.Game,StringComparer.CurrentCultureIgnoreCase).ToList() : items.OrderByDescending(item => item.LastValue).ThenBy(item => item.Game,StringComparer.CurrentCultureIgnoreCase).ToList();
                else if(sortKey=="launches") items=sortAscending ? items.OrderBy(item => item.LaunchValue).ThenBy(item => item.Game,StringComparer.CurrentCultureIgnoreCase).ToList() : items.OrderByDescending(item => item.LaunchValue).ThenBy(item => item.Game,StringComparer.CurrentCultureIgnoreCase).ToList();
                else items=sortAscending ? items.OrderBy(item => item.PlaySeconds).ThenBy(item => item.Game,StringComparer.CurrentCultureIgnoreCase).ToList() : items.OrderByDescending(item => item.PlaySeconds).ThenBy(item => item.Game,StringComparer.CurrentCultureIgnoreCase).ToList();
                refreshHeaders();
                grid.ItemsSource=items;
                count.Text=items.Count==1 ? "1 game with activity" : items.Count+" games with activity";
            };
            includeImported.Click+=(s,e) => {
                settings.IncludeImportedPlayTime=includeImported.IsChecked==true;
                try {Storage.Write("settings.json",settings);} catch(Exception) {}
                render();
            };
            refresh.Click+=(s,e) => {bool consumedTime=PlayTimeTracker.Consume(settings,Storage.Root); if(PlayTimeTracker.ConsumeSteam(settings)) consumedTime=true; if(consumedTime) try {Storage.Write("settings.json",settings);} catch(Exception) {} render();};
            render();
            int seenRevision=PlayTimeTracker.Revision;
            var timer=new DispatcherTimer {Interval=TimeSpan.FromSeconds(2)};
            int ticks=0;
            timer.Tick+=(s,e) => {
                PlayTimeTracker.Consume(settings,Storage.Root);
                ticks++;
                if(ticks%15==0) PlayTimeTracker.ConsumeSteam(settings);
                if(PlayTimeTracker.Revision==seenRevision) return;
                seenRevision=PlayTimeTracker.Revision;
                try {Storage.Write("settings.json",settings);} catch(Exception) {}
                render();
            };
            root.Loaded+=(s,e) => {timer.Start(); render();};
            root.IsVisibleChanged+=(s,e) => {if(root.IsVisible) render();};
            root.Unloaded+=(s,e) => timer.Stop();
            dialog.Closed+=(s,e) => timer.Stop();
            timer.Start();
            return root;
        }
    }
}
