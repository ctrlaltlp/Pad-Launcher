using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PadLauncher {
    public partial class Launcher {
        static readonly Dictionary<string,BitmapSource> controllerDiagramImages=new Dictionary<string,BitmapSource>();
        static BitmapSource ControllerDiagramImage(string style) {
            BitmapSource cached; if(controllerDiagramImages.TryGetValue(style,out cached)) return cached;
            using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream(style+"Controls.png")) {
                var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad;
                image.DecodePixelWidth=1024; image.StreamSource=stream; image.EndInit(); image.Freeze();
                // Remove transparent margins at display time; keep the supplied asset intact.
                var cropped=new CroppedBitmap(image,style=="PlayStation" ? new Int32Rect(267,110,490,325) : new Int32Rect(295,119,434,302)); cropped.Freeze();
                controllerDiagramImages[style]=cropped; return cropped;
            }
        }
        FrameworkElement CreateControlDiagram(Settings draft,string context,out Action update,Action changed=null) {
            string style=Prompts.Style(draft);
            var section=new StackPanel {Name="ControllerDiagram_"+context,Cursor=Cursors.Arrow,Margin=new Thickness(0,24,0,16)};
            section.Children.Add(Label(style+" · "+ControlBindings.ContextName(context),17,mint));
            var canvas=new Canvas {Width=1000,Height=850,Background=new SolidColorBrush(Color.FromRgb(16,30,40))};
            var image=new Image {Source=ControllerDiagramImage(style),Width=570,Height=style=="PlayStation" ? 378 : 400,Stretch=Stretch.Uniform,IsHitTestVisible=false};
            Canvas.SetLeft(image,215); Canvas.SetTop(image,200); canvas.Children.Add(image);
            var actionColour=new SolidColorBrush(Color.FromRgb(255,214,102));
            var labels=new Dictionary<string,TextBlock>(); var wires=new Dictionary<string,FrameworkElement[]>();
            // Label positions follow the physical controls. Inner controls leave
            // below the controller rather than weaving through side callouts.
            var placements=new Dictionary<string,Rect> {
                {"lt",new Rect(8,60,196,60)},{"lb",new Rect(8,140,196,60)},
                {"select",new Rect(345,65,150,60)},{"start",new Rect(620,65,150,60)},
                {"up",new Rect(8,240,196,60)},{"left",new Rect(8,320,196,60)},
                {"down",new Rect(8,400,196,60)},{"right",new Rect(8,480,196,60)},
                {"ls",new Rect(340,690,180,60)},
                {"rt",new Rect(796,60,196,60)},{"rb",new Rect(796,140,196,60)},
                {"north",new Rect(796,228,196,60)},{"east",new Rect(796,320,196,60)},
                {"south",new Rect(796,400,196,60)},{"west",new Rect(796,480,196,60)},
                {"rs",new Rect(540,690,180,60)}
            };
            var routes=new Dictionary<string,Point[]> {
                {"lt",new[]{new Point(212,79),new Point(280,79),new Point(320,209)}},
                {"lb",new[]{new Point(212,159),new Point(280,159),new Point(320,225)}},
                {"select",new[]{new Point(362,125),new Point(362,235)}},
                {"up",new[]{new Point(212,259),new Point(260,259),new Point(300,260),new Point(317,260)}},
                {"left",new[]{new Point(212,339),new Point(250,339),new Point(269,309)}},
                {"down",new[]{new Point(212,419),new Point(265,419),new Point(317,360)}},
                {"right",new[]{new Point(212,499),new Point(230,499),new Point(368,430),new Point(368,309)}},
                {"ls",new[]{new Point(404,680),new Point(404,389)}},
                {"rt",new[]{new Point(788,79),new Point(780,79),new Point(705,125),new Point(678,209)}},
                {"rb",new[]{new Point(788,159),new Point(720,159),new Point(678,225)}},
                {"start",new[]{new Point(634,125),new Point(634,235)}},
                {"north",new[]{new Point(788,247),new Point(680.1,247)}},
                {"east",new[]{new Point(788,339),new Point(750,339),new Point(744,310.1)}},
                {"south",new[]{new Point(788,419),new Point(735,419),new Point(680.1,373)}},
                {"west",new[]{new Point(788,499),new Point(770,499),new Point(637.4,440),new Point(637.4,331)}},
                {"rs",new[]{new Point(593,680),new Point(593,389)}}
            };
            if(style!="PlayStation") ConfigureAsymmetricDiagram(style,placements,routes);
            Action refreshDiagram=null;
            foreach(var placement in placements) {
                string input=placement.Key; Rect bounds=placement.Value;
                bool alignRight=bounds.X<300;
                var text=new TextBlock {Name="DiagramLabel_"+context+"_"+input,FontSize=15,Foreground=mint,TextWrapping=TextWrapping.Wrap,Width=bounds.Width,TextAlignment=style!="PlayStation" && (input=="select" || input=="start") ? TextAlignment.Center : alignRight ? TextAlignment.Right : TextAlignment.Left};
                var edit=new Button {Name="DiagramAssign_"+context+"_"+input,Content=text,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),MinHeight=28,Cursor=Cursors.Hand,ToolTip="Assign action to "+ControlBindings.ButtonLabel(draft,input),HorizontalContentAlignment=HorizontalAlignment.Stretch};
                Canvas.SetLeft(edit,bounds.X); Canvas.SetTop(edit,bounds.Y); canvas.Children.Add(edit); labels[input]=text;
                edit.Click+=(s,e) => ShowDiagramAssignment(Window.GetWindow(section),draft,context,input,() => {refreshDiagram(); if(changed!=null) changed();});
                Point anchor=routes[input].Last();
                var line=new System.Windows.Shapes.Polyline {Name="DiagramLine_"+context+"_"+input,Stroke=new SolidColorBrush(Color.FromArgb(220,163,240,205)),StrokeThickness=3,IsHitTestVisible=false,StrokeLineJoin=PenLineJoin.Round,Points=new PointCollection(routes[input])};
                var dot=new System.Windows.Shapes.Ellipse {Width=6,Height=6,Fill=mint,IsHitTestVisible=false}; Canvas.SetLeft(dot,anchor.X-3); Canvas.SetTop(dot,anchor.Y-3);
                canvas.Children.Add(line); canvas.Children.Add(dot); wires[input]=new FrameworkElement[]{line,dot};
            }
            var sticks=Label("Left stick: navigate games, lists and keys\nRight stick: cycle games",15,muted); sticks.TextAlignment=TextAlignment.Center;
            sticks.Width=520; Canvas.SetLeft(sticks,240); Canvas.SetTop(sticks,780); canvas.Children.Add(sticks);
            section.Children.Add(new Viewbox {Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,Child=canvas,HorizontalAlignment=HorizontalAlignment.Center});
            update=refreshDiagram=() => {
                foreach(var entry in labels) {
                    var assigned=ControlBindings.InContext(context).Where(action => ControlBindings.UsesButton(draft,action.FullId,entry.Key)).Select(action => DiagramActionName(action)).ToArray();
                    bool showJumpHint=entry.Key=="ls" && assigned.Length==0 && ControlBindings.Get(draft,context+".jump")=="none";
                    if(showJumpHint) assigned=new[]{"Jump to list (not assigned)"};
                    bool active=assigned.Length>0;
                    bool shoulder=entry.Key=="lb" || entry.Key=="lt" || entry.Key=="rb" || entry.Key=="rt";
                    entry.Value.Text=(shoulder ? ControlBindings.ButtonLabel(draft,entry.Key)+(active ? "\n" : "") : "")+(active ? String.Join(" / ",assigned) : shoulder ? "" : "Unassigned");
                    bool duplicate=ControlBindings.InContext(context).Any(action => ControlBindings.UsesButton(draft,action.FullId,entry.Key) && ControlBindings.Buttons(draft,action.FullId).Length>1);
                    entry.Value.Foreground=duplicate ? actionColour : active && !showJumpHint ? mint : muted;
                    foreach(var wire in wires[entry.Key]) wire.Visibility=active ? Visibility.Visible : Visibility.Collapsed;
                }
            };
            update(); return section;
        }
        void ShowDiagramAssignment(Window owner,Settings draft,string context,string input,Action changed) {
            var dialog=new Window {Name="DiagramAssignmentWindow",Title="Assign controller action",Owner=owner,Width=460,SizeToContent=SizeToContent.Height,ResizeMode=ResizeMode.NoResize,Background=owner.Background,Foreground=white,FontFamily=FontFamily,Topmost=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            ApplySettingsTheme(dialog); SettingsWindowSizing.Attach(dialog);
            var panel=new StackPanel {Margin=new Thickness(24)}; dialog.Content=panel;
            panel.Children.Add(Label(ControlBindings.ButtonLabel(draft,input)+" · "+ControlBindings.ContextName(context),20,mint));
            var options=new List<KeyValuePair<string,string>> {new KeyValuePair<string,string>("none","Unassigned")};
            if(context=="overlay") options.Add(new KeyValuePair<string,string>("dismiss-exit","Dismiss (tap) / Exit app (hold)"));
            options.AddRange(ControlBindings.InContext(context).OrderBy(action => action.Name).Select(action => new KeyValuePair<string,string>(action.Id,action.Name)));
            var assigned=ControlBindings.InContext(context).Where(action => ControlBindings.UsesButton(draft,action.FullId,input)).Select(action => action.Id).ToArray();
            string current=assigned.Length==1 ? assigned[0] : context=="overlay" && assigned.Length==2 && assigned.Contains("dismiss") && assigned.Contains("exit") ? "dismiss-exit" : "none";
            var picker=new SettingsComboBox {Name="DiagramActionPicker",ItemsSource=options,DisplayMemberPath="Value",SelectedValuePath="Key",SelectedValue=current,Margin=new Thickness(0,16,0,12),Padding=new Thickness(10)}; panel.Children.Add(picker);
            var note=Label("An action can use more than one button. Matching actions are highlighted yellow. Save changes in the main window to apply.",12,muted); note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,20,0,0)}; panel.Children.Add(buttons);
            var cancel=new Button {Content="Cancel",IsCancel=true,Padding=new Thickness(14,8,14,8),Margin=new Thickness(0,0,8,0)}; cancel.Click+=(s,e) => dialog.Close(); buttons.Children.Add(cancel);
            var apply=new Button {Name="ApplyDiagramAction",Content="Assign",IsDefault=true,Background=mint,Foreground=Brushes.Black,Padding=new Thickness(14,8,14,8)}; buttons.Children.Add(apply);
            apply.Click+=(s,e) => {
                string action=picker.SelectedValue as string; if(action==null) return;
                ControlBindings.AssignButton(draft,context,input,action);
                changed(); dialog.Close();
            };
            dialog.ShowDialog();
        }
        static void ConfigureAsymmetricDiagram(string style,Dictionary<string,Rect> placements,Dictionary<string,Point[]> routes) {
            bool nintendo=style=="Nintendo";
            double dpadX=nintendo ? 412 : 423,stickX=nintendo ? 345 : 353,stickY=nintendo ? 322 : 313;
            placements["ls"]=new Rect(8,stickY-19,196,60);
            placements["up"]=new Rect(8,360,196,60); placements["left"]=new Rect(8,440,196,60);
            placements["down"]=new Rect(8,520,196,60); placements["right"]=new Rect(8,600,196,60);
            placements["select"]=new Rect(nintendo ? 410 : 440,65,150,60);
            placements["start"]=new Rect(530,20,180,60);
            routes["lt"]=new[]{new Point(212,79),new Point(280,79),new Point(350,207)};
            routes["lb"]=new[]{new Point(212,159),new Point(280,159),new Point(350,224)};
            routes["rt"]=new[]{new Point(788,79),new Point(780,79),new Point(705,125),new Point(648,207)};
            routes["rb"]=new[]{new Point(788,159),new Point(720,159),new Point(648,224)};
            double selectX=nintendo ? 429 : 460,startX=nintendo ? 570 : 542;
            routes["select"]=new[]{new Point(selectX,125),new Point(selectX,nintendo ? 264 : 293)};
            routes["start"]=new[]{new Point(startX,80),new Point(startX,nintendo ? 264 : 293)};
            if(nintendo) {
                placements["select"]=new Rect(300,65,160,60);
                placements["start"]=new Rect(540,65,180,60);
                routes["select"]=new[]{new Point(380,105),new Point(429,185),new Point(429,264)};
                routes["start"]=new[]{new Point(630,105),new Point(570,185),new Point(570,264)};
                routes["lb"]=new[]{new Point(212,159),new Point(280,159),new Point(350,216)};
                routes["rb"]=new[]{new Point(788,159),new Point(720,159),new Point(648,216)};
            } else {
                placements["select"]=new Rect(330,65,160,60);
                placements["start"]=new Rect(510,65,180,60);
                routes["select"]=new[]{new Point(410,105),new Point(460,185),new Point(460,293)};
                routes["start"]=new[]{new Point(600,105),new Point(542,185),new Point(542,293)};
            }
            routes["ls"]=new[]{new Point(212,stickY),new Point(stickX,stickY)};
            routes["up"]=new[]{new Point(212,379),new Point(300,379),new Point(dpadX,nintendo ? 354 : 357)};
            routes["left"]=new[]{new Point(212,459),new Point(300,459),new Point(nintendo ? 367 : 379,403)};
            routes["down"]=new[]{new Point(212,539),new Point(330,539),new Point(dpadX,nintendo ? 443 : 453)};
            routes["right"]=new[]{new Point(212,619),new Point(340,619),new Point(nintendo ? 459 : 470,nintendo ? 525 : 516),new Point(nintendo ? 459 : 470,403)};
            routes["north"]=new[]{new Point(788,247),new Point(710,247),new Point(648,nintendo ? 258 : 251)};
            routes["east"]=new[]{new Point(788,339),new Point(740,339),new Point(nintendo ? 716 : 706,nintendo ? 320 : 311)};
            routes["south"]=new[]{new Point(788,419),new Point(730,419),new Point(648,nintendo ? 383 : 374)};
            routes["west"]=new[]{new Point(788,499),new Point(770,499),new Point(620,430),new Point(620,nintendo ? 370 : 379),new Point(606,nintendo ? 343 : 333)};
            routes["rs"]=new[]{new Point(574,680),new Point(574,399)};
        }
        static string DiagramActionName(ControlAction action) {
            switch(action.Id) {
                case "left": return "Navigate left"; case "right": return "Navigate right";
                case "up": return "Navigate up"; case "down": return "Navigate down";
                case "dismiss": return "Dismiss (tap)"; case "exit": return "Exit app (hold)";
                case "layout": return "Grid / carousel"; case "favourite": return "Favourite";
                case "jump": return "Jump to list"; case "finish": return "Focus games";
                case "cancel-alt": return "Exit search";
                default: return action.Name;
            }
        }
    }
}
