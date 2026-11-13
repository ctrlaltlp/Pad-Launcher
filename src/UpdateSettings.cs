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
        Button checkUpdateButton,installUpdateButton;
        string lastUpdateStatus="";
        void AddUpdateSettings(StackPanel panel) {
            var heading=Label("Updates",18,white); heading.Margin=new Thickness(0,24,0,8); panel.Children.Add(heading);
            panel.Children.Add(Label("Installed version: "+AppUpdates.DisplayVersion,12,muted));
            var auto=new CheckBox {Content="Automatically download and install updates",IsChecked=settings.AutoUpdate,Margin=new Thickness(0,12,0,8)};
            auto.Click+=(s,e) => {settings.AutoUpdate=auto.IsChecked==true; QueueSave(); if(settings.AutoUpdate) StartAutomaticUpdates();}; panel.Children.Add(auto);
            var note=Label("Checks for newer stable releases after startup. Installing restarts the app, preserves your data and keeps an executable backup.",12,muted);
            note.TextWrapping=TextWrapping.Wrap; panel.Children.Add(note);
            var actions=new StackPanel {Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,8)};
            checkUpdateButton=new Button {Content="Check for updates",Padding=new Thickness(12,7,12,7)};
            installUpdateButton=new Button {Content="Update & restart",Padding=new Thickness(12,7,12,7),Margin=new Thickness(8,0,0,0),Background=mint,Foreground=System.Windows.Media.Brushes.Black};
            checkUpdateButton.Click+=async (s,e) => await CheckForUpdates(false);
            installUpdateButton.Click+=async (s,e) => await InstallAvailableUpdate();
            actions.Children.Add(checkUpdateButton); actions.Children.Add(installUpdateButton); panel.Children.Add(actions);
            updateStatus=Label(lastUpdateStatus,12,muted); updateStatus.TextWrapping=TextWrapping.Wrap; panel.Children.Add(updateStatus);
            UpdateControls(lastUpdateStatus);
        }
        void UpdateControls(string message) {
            lastUpdateStatus=message;
            if(updateStatus!=null) updateStatus.Text=message;
            if(checkUpdateButton!=null) checkUpdateButton.IsEnabled=!updateBusy;
            if(installUpdateButton!=null) {installUpdateButton.IsEnabled=!updateBusy; installUpdateButton.Visibility=availableUpdate==null ? Visibility.Collapsed : Visibility.Visible;}
        }
        async void StartAutomaticUpdates() {
            if(automaticUpdateStarted || demo || preview || !settings.AutoUpdate) return;
            automaticUpdateStarted=true;
            try {
                await Task.Delay(15000,updateCancellation.Token);
                if(settings.AutoUpdate && !exiting) await CheckForUpdates(true);
            } catch(OperationCanceledException) {}
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
                string payload=await Task.Run(() => AppUpdates.Download(release,updateCancellation.Token));
                if(exiting || (automatic && !settings.AutoUpdate)) return;
                navigationSaveTimer.Stop();
                Storage.Write("settings.json",settings);
                AppUpdates.StartInstaller(payload,release.Sha256);
                ExitApp();
            } catch(OperationCanceledException) {}
            catch(Exception error) {UpdateControls("Could not install the update. "+error.GetBaseException().Message);}
            finally {updateBusy=false; UpdateControls(lastUpdateStatus);}
        }
    }
}
