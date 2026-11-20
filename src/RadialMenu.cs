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
        bool radialWaitRelease;
        int radialIndex=-1;
        ushort radialButtons;
        List<RadialSlot> radialSlots;
        readonly List<Border> radialCards=new List<Border>();
        readonly Brush radialDark=new SolidColorBrush(Color.FromRgb(21,36,46));

        readonly Brush radialSelected=new SolidColorBrush(Color.FromRgb(42,72,73));

        bool HandleRadial(PadState pad) {
            if(radial!=null) {
                if(pad.RT<80) { CloseRadial(); radialWaitRelease=false; suppressSearchRelease=true; return true; }
                int x=pad.LX,y=pad.LY;
                if(x*x+(double)y*y>14000.0*14000) {
                    var angle=Math.Atan2(x,y);
                    radialIndex=((int)Math.Round(angle*radialSlots.Count/(2*Math.PI))+radialSlots.Count)%radialSlots.Count;
                }
                ushort pressed=(ushort)(pad.Buttons & ~radialButtons);
                radialButtons=pad.Buttons;
                for(int i=0;i<radialCards.Count;i++) { radialCards[i].BorderBrush=i==radialIndex ? mint : muted; radialCards[i].Background=i==radialIndex ? radialSelected : radialDark; }
                if((pressed&0x1000)!=0 && radialIndex>=0) ActivateRadialSlot(radialSlots[radialIndex]);
                return true;
            }
            if(radialWaitRelease) {if(pad.RT<80) radialWaitRelease=false; return true;}
            if(pad.RT<160) return false;
            radialWaitRelease=true;
            OpenRadial(pad.Buttons);
            return true;
        }
        void CloseRadial() {
            if(radial!=null) {radial.IsOpen=false; radial=null;}
            radialCards.Clear();
        }
        void OpenRadial(ushort buttons) {
            radialSlots=(settings.RadialSlots ?? PreviewRadialSlots()).Take(8).ToList();
            if(radialSlots.Count==0) radialSlots.Add(new RadialSlot());
            radialButtons=buttons; radialIndex=-1; navigation.Reset();
            var canvas=new Canvas {Width=760,Height=590};
            for(int i=0;i<radialSlots.Count;i++) {
                double angle=i*2*Math.PI/radialSlots.Count;
                var text=Label(RadialSlotLabel(radialSlots[i]),14,white);
                text.TextAlignment=TextAlignment.Center; text.TextTrimming=TextTrimming.CharacterEllipsis;
                var card=new Border {Width=172,Height=58,Background=radialDark,BorderBrush=muted,
                    BorderThickness=new Thickness(1.5),CornerRadius=new CornerRadius(10),Padding=new Thickness(7),Child=text};
                text.VerticalAlignment=VerticalAlignment.Center;
                Canvas.SetLeft(card,294+Math.Sin(angle)*262); Canvas.SetTop(card,266-Math.Cos(angle)*231);
                canvas.Children.Add(card); radialCards.Add(card);
            }
            var hub=new Border {Width=126,Height=126,CornerRadius=new CornerRadius(63),Background=radialDark,BorderBrush=mint,BorderThickness=new Thickness(1),Child=new TextBlock {Text="Quick\nselect",Foreground=mint,TextAlignment=TextAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Center}};
            Canvas.SetLeft(hub,317); Canvas.SetTop(hub,232); canvas.Children.Add(hub);
            radial=new Popup {AllowsTransparency=true,Placement=PlacementMode.Center,PlacementTarget=carousel,Child=canvas,StaysOpen=true,IsHitTestVisible=false};
            radial.IsOpen=true;
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
        FrameworkElement CreateRadialSettings(Window owner) {
            var section=new Expander {Header="Quick select menu",Foreground=white,Margin=new Thickness(0,16,0,12)};
            var panel=new StackPanel {Margin=new Thickness(0,8,0,0)};
            section.Content=panel;
            string button=Prompts.RightTrigger(settings);
            var note=Label("Hold "+button+" to open the wheel. Slot 1 is at the top; the rest go clockwise.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            bool initialized=false;
            section.Expanded+=(sender,args) => {
                if(initialized || args.Source!=section) return;
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
                    panel.Children.Add(row);
                }
                var save=new Button {Content="Save quick select",Padding=new Thickness(12,7,12,7),HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,8,0,0)};
                panel.Children.Add(save);
                var status=Label("",12,muted); panel.Children.Add(status);
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
                int index=filters.FindIndex(c => c.Id==slot.Target);
                if(index<0) {
                    var list=CollectionOptions().FirstOrDefault(c => c.Id==slot.Target);
                    if(list==null) return;
                    filters.Add(list); index=filters.Count-1;
                }
                if(index!=collectionIndex) ChangeCollection(index-collectionIndex);
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
                else ChangeCollection(index-collectionIndex);
            }
        }
    }
}
