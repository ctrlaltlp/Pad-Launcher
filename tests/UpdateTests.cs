using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Threading;
using System.Collections.Generic;
using PadLauncher;
class UpdateTests {
    static int checks;
    static void Check(bool condition,string name) {if(!condition) throw new Exception(name); checks++;}
    static void Reject(Action action,string name) {try {action();} catch {checks++; return;} throw new Exception(name);}
    [STAThread] public static void Main(string[] args) {
        if(AppUpdates.RunInstaller(args)) return;
        if(args.Length==1 && args[0]=="--live-check") {
            var live=AppUpdates.Check(CancellationToken.None);
            string download=AppUpdates.Download(live,CancellationToken.None);
            Console.WriteLine("Verified GitHub release "+live.Version+" and downloaded executable checksum.");
            File.Delete(download); Directory.Delete(Path.GetDirectoryName(download)); return;
        }
        if(args.Length>=2 && args[0]=="--data") {File.WriteAllText(Path.Combine(args[1],"restarted.txt"),args.Length==4 && args[2]=="--resume-update" ? args[3] : "ok"); return;}
        if(args.Length==1 && args[0]=="--wait") {Thread.Sleep(750); return;}
        string root=Path.Combine(Path.GetTempPath(),"UpdateTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            string executable=System.Reflection.Assembly.GetExecutingAssembly().Location;
            string hash=AppUpdates.Hash(executable);
            var asset=new Dictionary<string,object>{{"name","PadLauncher.exe"},{"state","uploaded"},{"size",new FileInfo(executable).Length},{"digest","sha256:"+hash},{"browser_download_url","https://github.com/lpv11/Pad-Launcher/releases/download/v0.5.0/PadLauncher.exe"}};
            var release=new Dictionary<string,object>{{"tag_name","v0.5.0"},{"draft",false},{"prerelease",false},{"assets",new[]{asset}}};
            Check(AppUpdates.Parse(Storage.Json().Serialize(release)).Sha256==hash,"Stable release resolves the verified executable");
            asset["browser_download_url"]="https://github.com/lpsbn/Pad-Launcher/releases/download/v0.5.0/PadLauncher.exe";
            Check(AppUpdates.Parse(Storage.Json().Serialize(release)).Sha256==hash,"Updates accept the renamed GitHub account");
            Check(AppUpdates.IsNewer(new Version("0.5.0"),new Version("0.4.2.0")) && !AppUpdates.IsNewer(new Version("0.4.2"),new Version("0.4.2.0")) && !AppUpdates.IsNewer(new Version("0.4.1"),new Version("0.4.2")),"Version comparison ignores omitted zero components and prevents downgrades");
            release["prerelease"]=true; Reject(() => AppUpdates.Parse(Storage.Json().Serialize(release)),"Prereleases rejected"); release["prerelease"]=false;
            asset["digest"]=""; Reject(() => AppUpdates.Parse(Storage.Json().Serialize(release)),"Missing checksum rejected"); asset["digest"]="sha256:"+hash;
            asset["browser_download_url"]="https://github.com/other/project/releases/download/v1/PadLauncher.exe";
            Reject(() => AppUpdates.Parse(Storage.Json().Serialize(release)),"Foreign release rejected");
            Check(!new Settings().AutoUpdate,"Automatic updates are opt-in");
            var settings=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(new Settings {AutoUpdate=true})); Check(settings.AutoUpdate,"Automatic update choice persists");
            string target=Path.Combine(root,"PadLauncher.exe"),payload=Path.Combine(root,"payload.exe");
            File.Copy(executable,target); File.Copy(executable,payload);
            string data=Path.Combine(root,"PadLauncher.Data"); Directory.CreateDirectory(data); File.WriteAllText(Path.Combine(data,"settings.json"),"preserve me");
            var job=new AppUpdateJob {Target=target,Payload=payload,Sha256=new string('0',64)};
            Reject(() => AppUpdates.ReplaceExecutable(job),"Checksum mismatch rejected before replacement");
            Check(File.Exists(payload) && AppUpdates.Hash(target)==hash,"Failed verification preserves the running executable");
            job.Sha256=hash;
            using(var locked=File.Open(target,FileMode.Open,FileAccess.Read,FileShare.Read)) Reject(() => AppUpdates.ReplaceExecutable(job),"Locked executable fails safely");
            string backup=AppUpdates.ReplaceExecutable(job);
            Check(File.Exists(backup) && AppUpdates.Hash(backup)==hash && AppUpdates.Hash(target)==hash && !File.Exists(payload),"Atomic replacement retains an executable backup");
            Check(File.ReadAllText(Path.Combine(data,"settings.json"))=="preserve me","Replacement leaves portable settings untouched");
            string staging=Path.Combine(root,"PadLauncher.Update","test"); Directory.CreateDirectory(staging);
            payload=Path.Combine(staging,"PadLauncher.new.exe"); File.Copy(executable,payload);
            string helper=Path.Combine(staging,"PadLauncher.Updater.exe"); File.Copy(executable,helper);
            using(var parent=Process.Start(new ProcessStartInfo(executable,"--wait") {UseShellExecute=false,CreateNoWindow=true})) {
                job=new AppUpdateJob {Target=target,Payload=payload,Sha256=hash,DataRoot=data,ResumeFile=Path.Combine(staging,"resume.dat"),ProcessId=parent.Id};
                string jobFile=Path.Combine(staging,"update.json"); File.WriteAllText(jobFile,Storage.Json().Serialize(job));
                using(var updater=Process.Start(new ProcessStartInfo(helper,"--apply-update \""+jobFile+"\"") {UseShellExecute=false,CreateNoWindow=true})) {
                    if(!updater.WaitForExit(15000)) {updater.Kill(); throw new Exception("Update helper did not finish");}
                    Check(updater.ExitCode==0,"Update helper waits for the parent and completes");
                }
            }
            for(int retry=0;retry<100 && !File.Exists(Path.Combine(data,"restarted.txt"));retry++) Thread.Sleep(50);
            Check(File.Exists(Path.Combine(data,"restarted.txt")) && File.ReadAllText(Path.Combine(data,"settings.json"))=="preserve me","Update helper restarts with the original portable data folder");
            Check(File.ReadAllText(Path.Combine(data,"restarted.txt"))==job.ResumeFile,"Update helper forwards the one-time session restore path");
            Console.WriteLine(checks+" update checks passed");
        } finally {try {Directory.Delete(root,true);} catch {}}
    }
}
