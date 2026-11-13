using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;

namespace PadLauncher {
    public sealed class UpdateFormValue {
        public string Key {get;set;}
        public string Kind {get;set;}
        public string Value {get;set;}
    }
    public sealed class UpdateSession {
        public bool SettingsOpen {get;set;}
        public bool OverlayOpen {get;set;}
        public int SettingsTab {get;set;}
        public int CustomTab {get;set;}
        public int EmulatorTab {get;set;}
        public string CustomGame {get;set;}
        public string CollectionId {get;set;}
        public string SelectedId {get;set;}
        public string Monitor {get;set;}
        public double Left {get;set;}
        public double Top {get;set;}
        public double Width {get;set;}
        public double Height {get;set;}
        public int WindowState {get;set;}
        public double ScrollOffset {get;set;}
        public List<UpdateFormValue> Form {get;set;}
        static readonly byte[] entropy=Encoding.UTF8.GetBytes("PadLauncher.UpdateSession.v1");
        public static void Write(string path,UpdateSession state) {
            File.WriteAllBytes(path,ProtectedData.Protect(Encoding.UTF8.GetBytes(Storage.Json().Serialize(state)),entropy,DataProtectionScope.CurrentUser));
        }
        public static UpdateSession Read(string path) {
            return Storage.Json().Deserialize<UpdateSession>(Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path),entropy,DataProtectionScope.CurrentUser)));
        }
    }
    public partial class Launcher {
        static IEnumerable<KeyValuePair<string,DependencyObject>> SessionControls(DependencyObject root,string key="root") {
            yield return new KeyValuePair<string,DependencyObject>(key,root);
            var counters=new Dictionary<string,int>();
            string caption="";
            foreach(var item in LogicalTreeHelper.GetChildren(root)) {
                var child=item as DependencyObject; if(child==null) continue;
                var label=child as TextBlock; if(label!=null) caption=label.Text ?? "";
                string kind=child.GetType().Name; int index; counters.TryGetValue(kind,out index); counters[kind]=index+1;
                var element=child as FrameworkElement; var tab=child as TabItem; var expander=child as Expander;
                string part=element!=null && !String.IsNullOrEmpty(element.Name) ? kind+"#"+element.Name : tab!=null ? "tab:"+tab.Header : expander!=null ? "section:"+expander.Header : (child is TextBox || child is PasswordBox) && !String.IsNullOrEmpty(caption) ? kind+":"+caption : kind+"["+index+"]";
                foreach(var descendant in SessionControls(child,key+"/"+part)) yield return descendant;
            }
        }
        string SaveUpdateSession(string folder) {
            var state=new UpdateSession {SettingsOpen=helpWindow!=null,OverlayOpen=IsVisible,SettingsTab=helpTabs==null ? 0 : helpTabs.SelectedIndex,CustomTab=customGamesTabIndex,EmulatorTab=emulatorTabIndex,CustomGame=selectedCustomGameId,CollectionId=settings.CollectionId,SelectedId=settings.SelectedId,Form=new List<UpdateFormValue>()};
            if(filters.Count>collectionIndex) state.CollectionId=filters[collectionIndex].Id;
            if(visible.Count>selected) state.SelectedId=visible[selected].Id;
            if(IsVisible) state.Monitor=System.Windows.Forms.Screen.FromHandle(new System.Windows.Interop.WindowInteropHelper(this).Handle).DeviceName;
            if(helpWindow!=null) {
                Rect bounds=helpWindow.WindowState==WindowState.Normal ? new Rect(helpWindow.Left,helpWindow.Top,helpWindow.ActualWidth,helpWindow.ActualHeight) : helpWindow.RestoreBounds;
                state.Left=bounds.Left; state.Top=bounds.Top; state.Width=bounds.Width; state.Height=bounds.Height; state.WindowState=(int)helpWindow.WindowState;
                var active=helpTabs==null ? null : helpTabs.SelectedItem as TabItem;
                var scroll=active==null ? null : active.Content as ScrollViewer;
                state.ScrollOffset=scroll==null ? 0 : scroll.VerticalOffset;
                foreach(var pair in SessionControls(helpWindow)) {
                    var text=pair.Value as TextBox; var password=pair.Value as PasswordBox; var combo=pair.Value as ComboBox; var check=pair.Value as CheckBox; var expander=pair.Value as Expander;
                    string kind=null,value=null;
                    if(text!=null) {kind="text"; value=text.Text;}
                    else if(password!=null) {kind="password"; value=password.Password;}
                    else if(combo!=null && combo.SelectedValue is string) {kind="combo"; value=(string)combo.SelectedValue;}
                    else if(check!=null) {kind="check"; value=check.IsChecked.HasValue ? check.IsChecked.Value.ToString() : "null";}
                    else if(expander!=null) {kind="expand"; value=expander.IsExpanded.ToString();}
                    if(kind!=null) state.Form.Add(new UpdateFormValue {Key=pair.Key,Kind=kind,Value=value});
                }
            }
            string path=Path.Combine(folder,"resume.dat"); UpdateSession.Write(path,state); return path;
        }
        public void ResumeAfterUpdate(string path) {
            UpdateSession state;
            try {state=UpdateSession.Read(path);} catch(Exception error) {ShowNotice("Could not restore the update session. "+error.Message); return;}
            if(state==null) return;
            bool complete=true;
            if(state.OverlayOpen) {
                ShowOverlay();
                var monitor=System.Windows.Forms.Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName==state.Monitor);
                if(monitor!=null) PlaceOnMonitor(monitor);
                settings.CollectionId=state.CollectionId; settings.SelectedId=state.SelectedId; ApplyLibrary();
                int restoredSelection=visible.FindIndex(game => game.Id==state.SelectedId);
                if(restoredSelection>=0) {selected=restoredSelection; Render(false);}
            }
            if(state.SettingsOpen) {
                customGamesTabIndex=Math.Max(0,Math.Min(2,state.CustomTab)); emulatorTabIndex=Math.Max(0,Math.Min(1,state.EmulatorTab)); selectedCustomGameId=state.CustomGame;
                ShowHelpWindow();
                helpTabs.SelectedIndex=Math.Max(0,Math.Min(helpTabs.Items.Count-1,state.SettingsTab));
                if(state.Width>0 && state.Height>0 && !Double.IsInfinity(state.Width) && !Double.IsInfinity(state.Height)) {
                    var area=new Rect(SystemParameters.VirtualScreenLeft,SystemParameters.VirtualScreenTop,SystemParameters.VirtualScreenWidth,SystemParameters.VirtualScreenHeight);
                    var bounds=new Rect(state.Left,state.Top,state.Width,state.Height);
                    if(area.IntersectsWith(bounds)) {helpWindow.WindowStartupLocation=WindowStartupLocation.Manual; helpWindow.Left=state.Left; helpWindow.Top=state.Top; helpWindow.Width=Math.Max(helpWindow.MinWidth,state.Width); helpWindow.Height=Math.Max(helpWindow.MinHeight,state.Height);}
                }
                // Restore selections first: selection handlers can reload the text fields.
                foreach(string kind in new[]{"expand","combo","text","password","check"}) foreach(var value in (state.Form ?? new List<UpdateFormValue>()).Where(item => item.Kind==kind)) {
                    var control=SessionControls(helpWindow).FirstOrDefault(pair => pair.Key==value.Key).Value;
                    if(control==null) {complete=false; continue;}
                    try {
                        if(kind=="expand" && control is Expander) ((Expander)control).IsExpanded=value.Value=="True";
                        else if(kind=="combo" && control is ComboBox) {
                            var combo=(ComboBox)control;
                            bool found=combo.Items.Cast<object>().Any(item => {
                                if(String.IsNullOrEmpty(combo.SelectedValuePath)) return item as string==value.Value;
                                var property=System.ComponentModel.TypeDescriptor.GetProperties(item)[combo.SelectedValuePath];
                                return property!=null && property.GetValue(item) as string==value.Value;
                            });
                            if(found) combo.SelectedValue=value.Value; else complete=false;
                        } else if(kind=="text" && control is TextBox) ((TextBox)control).Text=value.Value;
                        else if(kind=="password" && control is PasswordBox) ((PasswordBox)control).Password=value.Value;
                        else if(kind=="check" && control is CheckBox) {
                            var box=(CheckBox)control; bool? selected=value.Value=="null" ? (bool?)null : value.Value=="True";
                            if(box.IsChecked!=selected) {box.IsChecked=selected; box.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));}
                        }
                    } catch {complete=false;} // Retain a recovery copy if a newer layout cannot restore a field.
                }
                helpWindow.UpdateLayout();
                var selectedTab=helpTabs.SelectedItem as TabItem; var scroll=selectedTab==null ? null : selectedTab.Content as ScrollViewer;
                if(scroll!=null) scroll.ScrollToVerticalOffset(state.ScrollOffset);
                if(state.WindowState==(int)WindowState.Maximized) helpWindow.WindowState=WindowState.Maximized;
                else if(state.WindowState==(int)WindowState.Minimized) helpWindow.WindowState=WindowState.Minimized;
            }
            try {
                if(!complete) File.Copy(path,Path.Combine(Storage.Root,"update-session-recovery.dat"),true);
                File.Delete(path);
            } catch {}
            if(!complete && helpWindow!=null) ThemedMessage.Show(helpWindow,"Some unsaved fields could not be restored in this version. Your saved settings are intact, and an encrypted recovery copy of the previous session has been kept.","Update session");
        }
    }
}
