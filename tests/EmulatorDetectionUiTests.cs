using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using PadLauncher;
class EmulatorDetectionUiTests {
    static int checks;
    static void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
    static IEnumerable<DependencyObject> Children(DependencyObject root) {
        yield return root;
        foreach(var item in LogicalTreeHelper.GetChildren(root)) {var child=item as DependencyObject; if(child!=null) foreach(var descendant in Children(child)) yield return descendant;}
    }
    static void CheckWizardPrompt(Launcher launcher,Window owner,EmulatorDetectionResult result,bool prefer,bool apply,bool selectRetro=true) {
        var draft=new Settings {RetroArchListArtworkSource="sgdb"}; Settings applied=null;
        var frame=new DispatcherFrame(); var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
        Window dialog=null; int prompts=0; bool rescanned=false; DateTime started=DateTime.UtcNow;
        string before=File.ReadAllText(Path.Combine(Storage.Root,"settings.json"));
        timer.Tick+=(s,e) => {
            if(DateTime.UtcNow-started>TimeSpan.FromSeconds(5)) throw new Exception("Wizard detection prompt test timed out");
            dialog=owner.OwnedWindows.Cast<Window>().FirstOrDefault(window => window.IsVisible && window.Title=="Detect installed emulators"); if(dialog==null) return;
            var prompt=dialog.OwnedWindows.Cast<Window>().FirstOrDefault(window => window.Name=="RetroArchArtworkPrompt" && window.IsVisible);
            if(prompt!=null) {prompts++; Children(prompt).OfType<Button>().Single(button => button.Name==(prefer ? "RetroArchArtworkPreferLibretro" : "RetroArchArtworkKeepCurrent")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;}
            var commit=Children(dialog).OfType<Button>().Single(button => button.Name=="ApplyDetectedEmulators"); if(!commit.IsEnabled || !dialog.IsEnabled) return;
            Check(!Children(dialog).OfType<CheckBox>().Any(box => box.Name=="DetectedRetroArchPreferLibretro"),"Wizard detection has no duplicate artwork checkbox");
            if(!rescanned) {rescanned=true; Children(dialog).OfType<Button>().Single(button => Convert.ToString(button.Content)=="Scan again").RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); return;}
            if(!selectRetro) Children(dialog).OfType<CheckBox>().Single(box => Convert.ToString(box.Content)=="RetroArch").IsChecked=false;
            if(apply) commit.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); else dialog.Close();
            frame.Continue=false;
        };
        timer.Start();
        try {
            typeof(Launcher).GetMethod("ShowEmulatorDetectionDraftCore",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(launcher,new object[]{owner,(Func<Settings>)(() => draft),(Action<Settings>)(next => applied=next),(Func<CancellationToken,EmulatorDetectionResult>)(token => result),true});
            Dispatcher.PushFrame(frame);
        } finally {timer.Stop(); if(dialog!=null) dialog.Close();}
        bool found=result.Matches.Any(match => match.Id=="retroarch");
        Check(prompts==0,"Emulator detection never opens a RetroArch artwork prompt");
        Check(File.ReadAllText(Path.Combine(Storage.Root,"settings.json"))==before,"Detection preferences stay in the wizard draft without touching saved settings");
        if(!apply) Check(applied==null,"Closing detection discards the prompt choice");
        else Check(applied!=null && applied.RetroArchListArtworkSource=="sgdb","Only an accepted preference applied with RetroArch changes the draft source");
    }
    [STAThread] public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EmulatorDetectionUi-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        Launcher launcher=null;
        try {
            Check(!TestEnvironment.EmulatorDetection,"Detection UI tests cover the regular app");
            string oldExe=Path.Combine(root,"old","pcsx2.exe"),newExe=Path.Combine(root,"new","pcsx2-qt.exe"),psp=Path.Combine(root,"PSP","PPSSPPWindows64.exe"),retro=Path.Combine(root,"RetroArch","retroarch.exe");
            foreach(string file in new[]{oldExe,newExe,psp,retro}) {Directory.CreateDirectory(Path.GetDirectoryName(file)); File.WriteAllText(file,"");}
            Storage.Write("settings.json",new Settings {RetroArchListArtworkSource="sgdb",Emulators=new List<EmulatorConfig>{new EmulatorConfig {Id="pcsx2",Path=oldExe,RomFolder="retain folder",Games=new List<EmulatorGame>()}}});
            launcher=new Launcher(false,true); var owner=launcher.CreateHelpWindow(); owner.Show();
            try {
                Check(Children(owner).OfType<Button>().Any(button => button.Name=="DetectInstalledEmulators"),"Test Settings includes the installed-emulator detection action");
                Check(Children(owner).OfType<CheckBox>().Single(box => box.Name=="StartWithWindowsSetting").IsEnabled,"The normal app retains its Windows startup setting");
                var result=new EmulatorDetectionResult {Matches=new List<DetectedEmulator>{new DetectedEmulator {Id="pcsx2",Name="PCSX2",Path=newExe,Sources=new List<string>{"Installer fixture"}},new DetectedEmulator {Id="ppsspp",Name="PPSSPP",Path=psp,Sources=new List<string>{"App Paths fixture"}},new DetectedEmulator {Id="retroarch",Name="RetroArch",Path=retro,Sources=new List<string>{"RetroArch fixture"}}}};
                bool rebuilt=false; Func<CancellationToken,EmulatorDetectionResult> scan=token => result;
                typeof(Launcher).GetMethod("ShowEmulatorDetection",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(launcher,new object[]{owner,(Action)(() => rebuilt=true),scan});
                var dialog=owner.OwnedWindows.Cast<Window>().Single(window => window.Title=="Detect installed emulators");
                try {
                    var frame=new DispatcherFrame(); var timeout=new DispatcherTimer {Interval=TimeSpan.FromSeconds(5)}; bool timedOut=false;
                    timeout.Tick+=(s,e) => {timedOut=true; frame.Continue=false;}; timeout.Start();
                    var poll=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
                    poll.Tick+=(s,e) => {if(Children(dialog).OfType<Button>().Single(button => button.Name=="ApplyDetectedEmulators").IsEnabled) frame.Continue=false;}; poll.Start();
                    Dispatcher.PushFrame(frame); poll.Stop(); timeout.Stop(); Check(!timedOut,"Detected candidates render without blocking the UI thread");
                    var boxes=Children(dialog).OfType<CheckBox>().ToList();
                    Check(boxes.Single(box => (string)box.Content=="PCSX2").IsChecked==false && boxes.Single(box => (string)box.Content=="PPSSPP").IsChecked==true,"Configured emulators start unchecked while newly found ones start checked");
                    Check(EmulatorLibrary.Config(Storage.Read<Settings>("settings.json"),"pcsx2").Path==oldExe,"Detection alone does not change any saved paths");
                    Children(dialog).OfType<Button>().Single(button => button.Name=="ApplyDetectedEmulators").RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    var applied=Storage.Read<Settings>("settings.json");
                    Check(applied.RetroArchListArtworkSource=="sgdb","Detecting RetroArch preserves the saved artwork preference");
                    Check(rebuilt && EmulatorLibrary.Config(applied,"pcsx2").Path==oldExe && EmulatorLibrary.Config(applied,"pcsx2").RomFolder=="retain folder" && EmulatorLibrary.Config(applied,"ppsspp").Path==psp && EmulatorLibrary.Config(applied,"retroarch").Path==retro,"Applying reviewed choices preserves unchecked emulator paths and existing ROM settings");
                    Check(!dialog.IsVisible && !owner.OwnedWindows.Cast<Window>().Contains(dialog),"Successfully applying selected emulator paths closes the review window");
                } finally {dialog.Close();}
                typeof(Launcher).GetMethod("ShowEmulatorDetection",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(launcher,new object[]{owner,(Action)(() => {}),scan});
                var repeated=owner.OwnedWindows.Cast<Window>().Single(window => window.Title=="Detect installed emulators");
                var repeatedFrame=new DispatcherFrame(); var repeatedPoll=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(20)};
                DateTime deadline=DateTime.UtcNow.AddSeconds(5);
                repeatedPoll.Tick+=(s,e) => {
                    if(DateTime.UtcNow>deadline) throw new Exception("Repeat detection timed out");
                    var scanAgain=Children(repeated).OfType<Button>().Single(button => Convert.ToString(button.Content)=="Scan again");
                    if(!scanAgain.IsEnabled) return;
                    var applyAgain=Children(repeated).OfType<Button>().Single(button => button.Name=="ApplyDetectedEmulators");
                    Check(!applyAgain.IsEnabled,"Detecting configured emulators again disables Apply when none are selected");
                    var row=Children(repeated).OfType<CheckBox>().First(); row.IsChecked=true;
                    Check(applyAgain.IsEnabled,"Selecting a detected emulator immediately enables Apply");
                    row.IsChecked=false;
                    Check(!applyAgain.IsEnabled,"Unselecting the last emulator disables Apply again");
                    repeated.Close(); repeatedFrame.Continue=false;
                };
                repeatedPoll.Start(); try {Dispatcher.PushFrame(repeatedFrame);} finally {repeatedPoll.Stop(); repeated.Close();}
                CheckWizardPrompt(launcher,owner,result,true,true);
                CheckWizardPrompt(launcher,owner,result,false,true);
                CheckWizardPrompt(launcher,owner,result,true,false);
                CheckWizardPrompt(launcher,owner,result,true,true,false);
                CheckWizardPrompt(launcher,owner,new EmulatorDetectionResult {Matches=result.Matches.Where(match => match.Id!="retroarch").ToList()},true,true);
            } finally {owner.Close();}
            Console.WriteLine(checks+" emulator detection UI checks passed");
        } finally {if(launcher!=null) launcher.ExitApp(); if(Directory.Exists(root)) Directory.Delete(root,true);}
    }
}
