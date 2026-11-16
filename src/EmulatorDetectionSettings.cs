using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PadLauncher {
    public partial class Launcher {
        void AddEmulatorDetectionSettings(StackPanel panel,Window owner,Action rebuild) {
            var button=new Button {Name="DetectInstalledEmulators",Content="Detect installed emulators…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,12,0,0)};
            button.Click+=(s,e) => ShowEmulatorDetection(owner,rebuild); panel.Children.Add(button);
            var note=Label("Checks Windows registrations, Start-menu shortcuts and standard install folders. Review matches before applying; existing paths are not replaced automatically.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,8,0,0); panel.Children.Add(note);
        }
        void ShowEmulatorDetection(Window owner,Action rebuild,Func<CancellationToken,EmulatorDetectionResult> detector=null) {
            if(detector==null) detector=EmulatorDetection.Scan;
            var dialog=new Window {Title="Detect installed emulators",Owner=owner,Width=820,Height=680,MinWidth=620,MinHeight=400,Background=owner.Background,Foreground=white,FontFamily=FontFamily,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            dialog.Resources.MergedDictionaries.Add(owner.Resources);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label("Detect installed emulators",24,white));
            var explanation=Label("Only checked matches are applied. Portable emulators may also appear if they have a Start-menu shortcut. No game imports or disk-wide scans are performed.",12,muted); explanation.TextWrapping=TextWrapping.Wrap; explanation.Margin=new Thickness(0,10,0,12); header.Children.Add(explanation);
            var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var status=Label("",12,muted); status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,12,0,8); footer.Children.Add(status);
            var actions=new WrapPanel {HorizontalAlignment=HorizontalAlignment.Right}; footer.Children.Add(actions);
            var scan=new Button {Content="Scan again",Padding=new Thickness(12,7,12,7)};
            var report=new Button {Content="Save report…",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),IsEnabled=false};
            var apply=new Button {Name="ApplyDetectedEmulators",Content="Apply selected",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),Background=mint,Foreground=System.Windows.Media.Brushes.Black,IsEnabled=false};
            var close=new Button {Content="Close",IsCancel=true,Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            actions.Children.Add(scan); actions.Children.Add(report); actions.Children.Add(apply); actions.Children.Add(close);
            var rows=new StackPanel(); root.Children.Add(new ScrollViewer {Content=rows,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var selected=new Dictionary<string,CheckBox>(); var paths=new Dictionary<string,ComboBox>();
            var cancellation=new CancellationTokenSource(); bool closed=false,busy=false; EmulatorDetectionResult detected=null;
            Func<Task> load=async () => {
                if(busy) return; busy=true; scan.IsEnabled=false; apply.IsEnabled=false; report.IsEnabled=false; status.Text="Checking installed emulator locations…";
                try {
                    var result=await Task.Run(() => detector(cancellation.Token)); if(closed) return;
                    detected=result; selected.Clear(); paths.Clear(); rows.Children.Clear();
                    foreach(var group in result.Matches.GroupBy(match => match.Id)) {
                        string id=group.Key; var config=EmulatorLibrary.Config(settings,id);
                        var row=new StackPanel {Margin=new Thickness(0,10,0,12)};
                        var check=new CheckBox {Content=group.First().Name,IsChecked=config==null || String.IsNullOrWhiteSpace(config.Path),FontSize=14,Margin=new Thickness(0,0,0,8)}; row.Children.Add(check); selected[id]=check;
                        var picker=new SettingsComboBox {ItemsSource=group.ToList(),DisplayMemberPath="Path",SelectedValuePath="Path",SelectedIndex=0,Padding=new Thickness(8)}; row.Children.Add(picker); paths[id]=picker;
                        if(config!=null && group.Any(item => String.Equals(item.Path,config.Path,StringComparison.OrdinalIgnoreCase))) picker.SelectedValue=config.Path;
                        var source=Label("",12,muted); source.TextWrapping=TextWrapping.Wrap; source.Margin=new Thickness(0,6,0,0); row.Children.Add(source);
                        Action updateSource=() => {var item=picker.SelectedItem as DetectedEmulator; source.Text=item==null ? "" : "Found through: "+String.Join("; ",item.Sources);}; picker.SelectionChanged+=(s,e) => updateSource(); updateSource();
                        if(config!=null && !String.IsNullOrWhiteSpace(config.Path)) {var existing=Label("Current path: "+config.Path+"\nCheck this emulator only if you want to use the selected path instead.",12,muted); existing.TextWrapping=TextWrapping.Wrap; existing.Margin=new Thickness(0,6,0,0); row.Children.Add(existing);}
                        rows.Children.Add(row);
                    }
                    if(result.Matches.Count==0) {var empty=Label("No supported installed emulators found. Portable setups without registered shortcuts usually need Browse… instead.",14,white); empty.TextWrapping=TextWrapping.Wrap; rows.Children.Add(empty);}
                    if(result.Warnings.Count>0) {var warnings=Label(String.Join("\n",result.Warnings),12,muted); warnings.TextWrapping=TextWrapping.Wrap; rows.Children.Add(new Expander {Header="Detection notes ("+result.Warnings.Count+")",Content=warnings,Foreground=white,Margin=new Thickness(0,12,0,0)});}
                    status.Text=result.Matches.Select(item => item.Id).Distinct().Count()+" emulators found · "+result.Matches.Count+" executable paths";
                    apply.IsEnabled=selected.Count>0; report.IsEnabled=true;
                } catch(OperationCanceledException) {} catch(Exception error) {if(!closed) status.Text="Detection failed. "+error.Message;}
                finally {busy=false; if(!closed) scan.IsEnabled=true;}
            };
            scan.Click+=async (s,e) => await load(); dialog.Loaded+=async (s,e) => await load();
            apply.Click+=(s,e) => {
                try {
                    var chosen=selected.Where(pair => pair.Value.IsChecked==true).ToDictionary(pair => pair.Key,pair => paths[pair.Key].SelectedValue as string);
                    if(chosen.Count==0) {status.Text="Check at least one emulator to apply."; return;}
                    var next=EmulatorDetection.Apply(settings,chosen); CommitEmulators(next,rebuild);
                    dialog.Close();
                } catch(Exception error) {status.Text="Could not apply paths. "+error.Message;}
            };
            report.Click+=(s,e) => {
                var file=new Microsoft.Win32.SaveFileDialog {Filter="JSON report|*.json",FileName="emulator-detection-report.json"};
                if(file.ShowDialog(dialog)==true) try {File.WriteAllText(file.FileName,Storage.ReadableJson(detected)); status.Text="Report saved. It contains detected paths and detection notes.";} catch(Exception error) {status.Text=error.Message;}
            };
            close.Click+=(s,e) => dialog.Close(); dialog.Closed+=(s,e) => {closed=true; cancellation.Cancel();}; dialog.Show();
        }
    }
}
