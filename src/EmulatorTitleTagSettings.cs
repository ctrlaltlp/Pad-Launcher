using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace PadLauncher {
    public partial class Launcher {
        void CleanImportGameTitles(Window owner,IEnumerable<KeyValuePair<EmulatorImportGame,CheckBox>> candidates) {
            ReviewImportGameTitles(owner,candidates,false);
        }
        void NormaliseImportGameTitles(Window owner,IEnumerable<KeyValuePair<EmulatorImportGame,CheckBox>> candidates) {
            ReviewImportGameTitles(owner,candidates,true);
        }
        void ReviewImportGameTitles(Window owner,IEnumerable<KeyValuePair<EmulatorImportGame,CheckBox>> candidates,bool normalise) {
            string title=normalise ? "Normalise titles" : "Remove tags from game titles";
            var selected=candidates.Where(pair => pair.Value.IsChecked==true).ToList();
            var changes=selected.GroupBy(pair => pair.Key.Path,StringComparer.OrdinalIgnoreCase)
                .Select(group => new {Games=group.ToList(),Before=group.First().Key.Name,After=normalise ? EmulatorTitleTags.Normalise(group.First().Key.Name) : EmulatorTitleTags.Clean(group.First().Key.Name)})
                .Where(change => change.Before!=change.After).ToList();
            if(changes.Count==0) {ThemedMessage.Show(owner,selected.Count==0 ? "Select games first." : normalise ? "No trailing ‘The’ was found in the selected game titles." : "No recognised tags were found in the selected game titles.",title); return;}
            var dialog=new Window {Title=title,Owner=owner,Width=740,Height=650,MinWidth=520,MinHeight=380,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label(title,22,white));
            var note=Label(normalise ? "Moves trailing ‘The’ to the front, keeping subtitles and tags. Review changes before applying. Names are saved only when you complete the library import. ROMs and emulator playlists stay unchanged." : "Review title changes for selected games. Disc numbers and unrecognised title text are kept. Changes are saved only when you complete the library import. ROMs and emulator playlists stay unchanged.",12,muted); note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,10,0,10); header.Children.Add(note);
            var tags=Label("Tags found: "+String.Join(", ",changes.SelectMany(change => EmulatorTitleTags.TagsIn(change.Before)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(tag => tag,StringComparer.CurrentCultureIgnoreCase)),12,mint); tags.TextWrapping=TextWrapping.Wrap;
            if(!normalise) header.Children.Add(new ScrollViewer {Content=tags,MaxHeight=80,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Margin=new Thickness(0,0,0,10)});
            var toggle=new Button {Name="ToggleTitleTagChanges",Content="Unselect all",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,10)}; header.Children.Add(toggle);
            var footer=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,12,0,0)}; DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var apply=new Button {Name="ApplyTitleTagChanges",Content="Apply to import review",Padding=new Thickness(12,7,12,7)};
            var cancel=new Button {Content="Cancel",IsCancel=true,Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)}; footer.Children.Add(apply); footer.Children.Add(cancel);
            var rows=new StackPanel(); root.Children.Add(new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var boxes=new List<CheckBox>();
            foreach(var change in changes) {
                var text=new StackPanel(); var old=Label(change.Before,12,muted); old.TextWrapping=TextWrapping.Wrap; text.Children.Add(old);
                var next=Label("→ "+change.After,14,white); next.TextWrapping=TextWrapping.Wrap; text.Children.Add(next);
                var box=new CheckBox {Content=text,IsChecked=true,Margin=new Thickness(0,5,0,8)}; boxes.Add(box); rows.Children.Add(box);
            }
            Action update=() => {bool any=boxes.Any(box => box.IsChecked==true); apply.IsEnabled=any; toggle.Content=boxes.All(box => box.IsChecked==true) ? "Unselect all" : "Select all";};
            foreach(var box in boxes) box.Click+=(s,e) => update();
            toggle.Click+=(s,e) => {bool choose=!boxes.All(box => box.IsChecked==true); foreach(var box in boxes) box.IsChecked=choose; update();};
            apply.Click+=(s,e) => {
                for(int i=0;i<changes.Count;i++) if(boxes[i].IsChecked==true) foreach(var pair in changes[i].Games) {
                    pair.Key.Name=changes[i].After; pair.Key.RenameExisting=true;
                    var row=pair.Value.Content as StackPanel;
                    if(row!=null && row.Children.Count>0) {
                        var field=row.Children[0] as TextBox; if(field!=null) field.Text=pair.Key.Name;
                        var label=row.Children[0] as TextBlock; if(label!=null) label.Text=pair.Key.Name+" · already added";
                    }
                }
                dialog.DialogResult=true;
            };
            dialog.ShowDialog();
        }
    }
}
