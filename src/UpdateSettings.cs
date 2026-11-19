using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace PadLauncher {
    public partial class Launcher {
        readonly CancellationTokenSource updateCancellation=new CancellationTokenSource();
        bool updateBusy,automaticUpdateStarted;
        AppRelease availableUpdate;
        TextBlock updateStatus;
        Button checkUpdateButton,installUpdateButton,releaseNotesButton;
        string lastUpdateStatus="";
        DateTime nextAutomaticCheck=DateTime.UtcNow.AddSeconds(15);
        string pendingUpdatePayload;
        AppRelease pendingUpdateRelease;
        void AddUpdateSettings(StackPanel panel) {
            if(TestEnvironment.EmulatorDetection) {panel.Children.Add(Label("App updates are disabled in the emulator detection test.",12,muted)); return;}
            var heading=Label("Updates",18,white); heading.Margin=new Thickness(0,24,0,8); panel.Children.Add(heading);
            panel.Children.Add(Label("Installed version: "+AppUpdates.DisplayVersion,12,muted));
            var auto=new CheckBox {Content="Automatically download and install updates",IsChecked=settings.AutoUpdate,Margin=new Thickness(0,12,0,8)};
            auto.Click+=(s,e) => {settings.AutoUpdate=auto.IsChecked==true; QueueSave(); if(settings.AutoUpdate) {nextAutomaticCheck=DateTime.UtcNow; StartAutomaticUpdates();}}; panel.Children.Add(auto);
            var note=Label("Checks periodically for newer stable releases. Automatic installation waits until Settings is closed and the overlay is dismissed. Updates preserve your data and keep an executable backup.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            var actions=new WrapPanel {Margin=new Thickness(0,12,0,8)};
            checkUpdateButton=new Button {Content="Check for updates",Padding=new Thickness(12,7,12,7)};
            installUpdateButton=new Button {Content="Update & restart",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),Background=mint,Foreground=System.Windows.Media.Brushes.Black};
            releaseNotesButton=new Button {Content="Release notes",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0)};
            releaseNotesButton.Click+=(s,e) => ShowReleaseNotes();
            checkUpdateButton.Click+=async (s,e) => await CheckForUpdates(false);
            installUpdateButton.Click+=async (s,e) => await InstallAvailableUpdate();
            actions.Children.Add(checkUpdateButton); actions.Children.Add(installUpdateButton); actions.Children.Add(releaseNotesButton); panel.Children.Add(actions);
            updateStatus=Label(lastUpdateStatus,12,muted); updateStatus.TextWrapping=TextWrapping.Wrap; panel.Children.Add(updateStatus);
            UpdateControls(lastUpdateStatus);
        }
        void ShowReleaseNotes() {
            var release=availableUpdate;
            if(release==null) return;
            var dialog=new Window {
                Title="Release notes",Owner=helpWindow,Width=680,Height=550,MinWidth=340,MinHeight=260,
                WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false,
                Background=helpWindow.Background,Foreground=white,FontFamily=FontFamily,Icon=Icon
            };
            ApplySettingsTheme(dialog);
            SettingsWindowSizing.Attach(dialog);
            var panel=new DockPanel {Margin=new Thickness(24)};
            dialog.Content=panel;
            var heading=Label("Available updates · "+AppUpdates.DisplayVersion+" → "+release.Version,18,white);
            heading.Margin=new Thickness(0,0,0,12);
            DockPanel.SetDock(heading,Dock.Top);
            panel.Children.Add(heading);
            var close=new Button {Content="Close",IsCancel=true,HorizontalAlignment=HorizontalAlignment.Right,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,12,0,0)};
            DockPanel.SetDock(close,Dock.Bottom);
            panel.Children.Add(close);
            var notes=new TextBox {Text="Loading release notes…",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Padding=new Thickness(10)};
            panel.Children.Add(notes);
            using(var cancellation=CancellationTokenSource.CreateLinkedTokenSource(updateCancellation.Token)) {
                var token=cancellation.Token;
                dialog.Closed+=(s,e) => cancellation.Cancel();
                dialog.Loaded+=async (s,e) => {
                    try {
                        string text=await Task.Run(() => AppUpdates.ReadReleaseNotes(AppUpdates.CurrentVersion,release.Version,token),token);
                        if(!token.IsCancellationRequested) notes.Text=text;
                    } catch(OperationCanceledException) {}
                    catch(Exception error) {
                        if(!token.IsCancellationRequested) notes.Text="Could not load release notes. "+error.GetBaseException().Message;
                    }
                };
                dialog.ShowDialog();
            }
        }
        void UpdateControls(string message) {
            lastUpdateStatus=message;
            if(updateStatus!=null) updateStatus.Text=message;
            if(checkUpdateButton!=null) checkUpdateButton.IsEnabled=!updateBusy;
            if(releaseNotesButton!=null) releaseNotesButton.Visibility=availableUpdate==null ? Visibility.Collapsed : Visibility.Visible;
            if(installUpdateButton!=null) {installUpdateButton.IsEnabled=!updateBusy; installUpdateButton.Visibility=availableUpdate==null ? Visibility.Collapsed : Visibility.Visible;}
        }
        async void StartAutomaticUpdates() {
            if(automaticUpdateStarted || demo || preview || TestEnvironment.EmulatorDetection || !settings.AutoUpdate) return;
            automaticUpdateStarted=true;
            try {
                while(settings.AutoUpdate && !exiting) {
                    await Task.Delay(5000,updateCancellation.Token);
                    if(!settings.AutoUpdate || exiting) break;
                    if(pendingUpdatePayload!=null) {
                        if(!updateBusy && CanInstallAutomatically()) await InstallAvailableUpdate(true);
                    } else if(!updateBusy && DateTime.UtcNow>=nextAutomaticCheck) {
                        nextAutomaticCheck=DateTime.UtcNow.AddHours(6);
                        await CheckForUpdates(true);
                    }
                }
            } catch(OperationCanceledException) {}
            finally {automaticUpdateStarted=false;}
        }
        bool CanInstallAutomatically() {
            return helpWindow==null && !IsVisible && !scanning && !launching &&
                (Application.Current==null || !System.Linq.Enumerable.Any(System.Linq.Enumerable.Cast<Window>(Application.Current.Windows),window => window!=this && window.IsVisible));
        }
        async Task CheckForUpdates(bool automatic) {
            if(updateBusy || exiting) return;
            updateBusy=true; UpdateControls("Checking for updates…");
            bool install=false;
            try {
                var release=await Task.Run(() => AppUpdates.Check(updateCancellation.Token));
                if(exiting) return;
                availableUpdate=AppUpdates.IsNewer(release.Version,AppUpdates.CurrentVersion) ? release : null;
                UpdateControls(availableUpdate==null ? "You're up to date." : "Version "+release.Version+" is available.");
                install=automatic && settings.AutoUpdate && availableUpdate!=null;
            } catch(OperationCanceledException) {}
            catch(Exception error) {UpdateControls("Could not check for updates. "+error.GetBaseException().Message);}
            finally {updateBusy=false; UpdateControls(lastUpdateStatus);}
            if(install) await InstallAvailableUpdate(true);
        }
        async Task InstallAvailableUpdate(bool automatic=false) {
            if(updateBusy || availableUpdate==null || exiting) return;
            var release=availableUpdate;
            updateBusy=true; UpdateControls("Downloading version "+release.Version+"…");
            try {
                string payload=pendingUpdateRelease!=null && pendingUpdateRelease.Sha256==release.Sha256 && System.IO.File.Exists(pendingUpdatePayload) ? pendingUpdatePayload : await Task.Run(() => AppUpdates.Download(release,updateCancellation.Token));
                if(exiting || (automatic && !settings.AutoUpdate)) return;
                if(automatic && !CanInstallAutomatically()) {
                    pendingUpdatePayload=payload; pendingUpdateRelease=release;
                    UpdateControls("Update downloaded. Waiting until Settings is closed and the overlay is dismissed.");
                    return;
                }
                if(helpWindow!=null && helpWindow.OwnedWindows.Count>0) {UpdateControls("Close import and other Settings dialogs before updating."); pendingUpdatePayload=payload; pendingUpdateRelease=release; return;}
                navigationSaveTimer.Stop();
                Storage.Write("settings.json",settings);
                string resume=SaveUpdateSession(System.IO.Path.GetDirectoryName(payload));
                System.IO.File.Copy(System.IO.Path.Combine(Storage.Root,"settings.json"),System.IO.Path.Combine(Storage.Root,"settings.before-update.json"),true);
                AppUpdates.StartInstaller(payload,release.Sha256,resume);
                ExitApp();
            } catch(OperationCanceledException) {}
            catch(Exception error) {pendingUpdatePayload=null; pendingUpdateRelease=null; UpdateControls("Could not install the update. "+error.GetBaseException().Message);}
            finally {updateBusy=false; UpdateControls(lastUpdateStatus);}
        }
    }
}
