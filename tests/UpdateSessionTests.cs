using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using PadLauncher;
class UpdateSessionTests {
    static int checks;
    static void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
    static readonly BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
    static IEnumerable<KeyValuePair<string,DependencyObject>> Controls(Window window) {return (IEnumerable<KeyValuePair<string,DependencyObject>>)typeof(Launcher).GetMethod("SessionControls",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{window,"root"});}
    static T Find<T>(Window window,string name) where T:FrameworkElement {return Controls(window).Select(pair => pair.Value).OfType<T>().First(item => item.Name==name);}
    [STAThread] public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"UpdateSessionTests-"+Guid.NewGuid().ToString("N")); Storage.Root=root;
        Launcher original=null,resumed=null,overlayResume=null;
        try {
            Storage.Write("settings.json",new Settings {AutoUpdate=true});
            original=new Launcher(true,true);
            var canInstall=typeof(Launcher).GetMethod("CanInstallAutomatically",flags);
            Check((bool)canInstall.Invoke(original,null),"An idle tray app can install automatic updates");
            original.ShowHelpWindow();
            Check(!(bool)canInstall.Invoke(original,null),"Settings blocks automatic installation");
            original.HelpWindow.Hide();
            Check(!(bool)canInstall.Invoke(original,null),"A hidden Settings window still protects unsaved work"); original.HelpWindow.Show();
            var tabs=Find<TabControl>(original.HelpWindow,"HelpSettingsTabs"); tabs.SelectedIndex=4;
            Find<TabControl>(original.HelpWindow,"CustomGamesTabs").SelectedIndex=0;
            Find<TextBox>(original.HelpWindow,"CustomGameName").Text="Unsaved game draft";
            var secret=Controls(original.HelpWindow).Select(pair => pair.Value).OfType<PasswordBox>().First(); secret.Password="unsaved-secret-api-key";
            Find<Expander>(original.HelpWindow,"HiddenGamesSection").IsExpanded=true;
            var hiddenChoices=Find<StackPanel>(original.HelpWindow,"HiddenGamesChoices");
            var hiddenBox=hiddenChoices.Children.OfType<CheckBox>().First(); string hiddenId=(string)hiddenBox.Tag;
            hiddenBox.IsChecked=true; hiddenBox.RaiseEvent(new RoutedEventArgs(CheckBox.ClickEvent));
            string payload=Path.Combine(root,"pending.exe"); File.Copy(Assembly.GetExecutingAssembly().Location,payload);
            var release=new AppRelease {Version=new Version("9.0.0"),Sha256=AppUpdates.Hash(payload)};
            typeof(Launcher).GetField("availableUpdate",flags).SetValue(original,release);
            typeof(Launcher).GetField("pendingUpdateRelease",flags).SetValue(original,release);
            typeof(Launcher).GetField("pendingUpdatePayload",flags).SetValue(original,payload);
            ((System.Threading.Tasks.Task)typeof(Launcher).GetMethod("InstallAvailableUpdate",flags).Invoke(original,new object[]{true})).GetAwaiter().GetResult();
            Check(original.HelpWindow!=null && File.Exists(payload) && !File.Exists(Path.Combine(root,"PadLauncher.Updater.exe")),"A downloaded automatic update defers without closing Settings or launching an installer");
            original.HelpWindow.Left=100; original.HelpWindow.Top=90; original.HelpWindow.Height=700; original.HelpWindow.UpdateLayout();
            var oldScroll=(ScrollViewer)((TabItem)tabs.SelectedItem).Content; oldScroll.ScrollToVerticalOffset(120); original.HelpWindow.UpdateLayout();
            string resume=(string)typeof(Launcher).GetMethod("SaveUpdateSession",flags).Invoke(original,new object[]{root});
            var saved=UpdateSession.Read(resume);
            Check(saved.SettingsOpen && saved.SettingsTab==4 && saved.CustomTab==0,"Update captures Settings and selected nested tab");
            Check(saved.Form.Any(value => value.Value=="Unsaved game draft") && saved.Form.Any(value => value.Value=="unsaved-secret-api-key"),"Session includes unsaved form values");
            Check(!Encoding.UTF8.GetString(File.ReadAllBytes(resume)).Contains("unsaved-secret-api-key"),"Session drafts and passwords are encrypted for the Windows user");
            Check((Storage.Read<Settings>("settings.json").CustomGames ?? new List<CustomGame>()).Count==0,"Capturing a draft does not register an unsaved game");
            original.HelpWindow.Close(); original.ExitApp(); original=null;
            resumed=new Launcher(true,true); resumed.ResumeAfterUpdate(resume);
            Check(resumed.HelpWindow!=null && Find<TabControl>(resumed.HelpWindow,"HelpSettingsTabs").SelectedIndex==4 && Find<TabControl>(resumed.HelpWindow,"CustomGamesTabs").SelectedIndex==0,"Restart reopens Settings on its previous tabs");
            Check(Find<TextBox>(resumed.HelpWindow,"CustomGameName").Text=="Unsaved game draft","Restart restores an unsaved custom game name");
            Check(Controls(resumed.HelpWindow).Select(pair => pair.Value).OfType<PasswordBox>().First().Password=="unsaved-secret-api-key","Restart restores an unsaved API key without saving it as plaintext");
            Check(Find<Expander>(resumed.HelpWindow,"HiddenGamesSection").IsExpanded,"Restart restores expanded sections");
            var restoredBox=Find<StackPanel>(resumed.HelpWindow,"HiddenGamesChoices").Children.OfType<CheckBox>().Single(box => (string)box.Tag==hiddenId);
            Check(restoredBox.IsChecked==true && (Storage.Read<Settings>("settings.json").HiddenGameIds ?? new List<string>()).Count==0,"Unsaved checkbox drafts are restored without prematurely saving them");
            Check(Math.Abs(resumed.HelpWindow.Left-100)<1 && Math.Abs(resumed.HelpWindow.Top-90)<1,"Restart restores the Settings window position");
            resumed.HelpWindow.UpdateLayout(); var newScroll=(ScrollViewer)((TabItem)Find<TabControl>(resumed.HelpWindow,"HelpSettingsTabs").SelectedItem).Content;
            Check(saved.ScrollOffset>0 && Math.Abs(newScroll.VerticalOffset-saved.ScrollOffset)<1,"Restart restores the selected Settings tab's scroll position");
            Check(!File.Exists(resume) && Storage.Read<Settings>("settings.json").AutoUpdate,"Session is consumed once while saved settings are preserved");
            resumed.HelpWindow.Close(); Check((bool)canInstall.Invoke(resumed,null),"Closing Settings releases the automatic-install guard");
            typeof(Launcher).GetField("availableUpdate",flags).SetValue(resumed,release);
            typeof(Launcher).GetField("pendingUpdateRelease",flags).SetValue(resumed,release);
            typeof(Launcher).GetField("pendingUpdatePayload",flags).SetValue(resumed,payload);
            string blocked=Path.Combine(root,"blocked-data-folder"); File.WriteAllText(blocked,"sentinel"); Storage.Root=blocked;
            try {
                ((System.Threading.Tasks.Task)typeof(Launcher).GetMethod("InstallAvailableUpdate",flags).Invoke(resumed,new object[]{true})).GetAwaiter().GetResult();
                Check(!File.Exists(Path.Combine(root,"PadLauncher.Updater.exe")) && File.Exists(payload) && ((string)typeof(Launcher).GetField("lastUpdateStatus",flags).GetValue(resumed)).StartsWith("Could not install") && !(bool)typeof(Launcher).GetField("exiting",flags).GetValue(resumed),"A settings-save failure stops installation before starting the helper or exiting");
            } finally {Storage.Root=root;}
            resumed.ShowOverlay(); Check(!(bool)canInstall.Invoke(resumed,null),"A visible overlay also blocks automatic installation");
            typeof(Launcher).GetMethod("Move",flags).Invoke(resumed,new object[]{-2});
            string overlayState=(string)typeof(Launcher).GetMethod("SaveUpdateSession",flags).Invoke(resumed,new object[]{root});
            resumed.ExitApp(); resumed=null;
            overlayResume=new Launcher(true,true); overlayResume.ResumeAfterUpdate(overlayState);
            var games=(List<Game>)typeof(Launcher).GetField("visible",flags).GetValue(overlayResume);
            int selected=(int)typeof(Launcher).GetField("selected",flags).GetValue(overlayResume);
            Check(overlayResume.IsVisible && overlayResume.HelpWindow==null && games[selected].Id=="2","Manual update restarts with the overlay open and its selected game restored");
            Console.WriteLine(checks+" update session checks passed");
        } finally {if(original!=null) original.ExitApp(); if(resumed!=null) resumed.ExitApp(); if(overlayResume!=null) overlayResume.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
