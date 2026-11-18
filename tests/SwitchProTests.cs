using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using PadLauncher;

class SwitchProTests {
    static int checks;
    static void Check(bool condition,string description) {if(!condition) throw new Exception(description); checks++;}
    sealed class Backend:ISwitchProBackend {
        public int Starts,Stops,Reads;
        public bool Fail;
        public List<KeyValuePair<string,PadState>> States=new List<KeyValuePair<string,PadState>>();
        public bool Start() {Starts++; return true;}
        public void Stop() {Stops++;}
        public List<KeyValuePair<string,PadState>> Read() {Reads++; if(Fail) throw new IOException("Disconnected"); return States;}
    }
    [STAThread] static void Main(string[] args) {
        if(args.Length==2 && args[0]=="--native-smoke") {
            Storage.Root=args[1]; var backend=new SdlSwitchProBackend();
            try {if(!backend.Start()) throw new Exception("SDL initialization failed"); backend.Read(); backend.Stop(); if(!backend.Start()) throw new Exception("SDL restart failed"); backend.Read();}
            finally {backend.Stop();}
            return;
        }
        string previous=Storage.Root,root=Path.Combine(Path.GetTempPath(),"SwitchProTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root); Storage.Root=root;
        try {
            var neutral=SwitchProInput.Translate(0,0,0,0,0,0,0);
            Check(neutral.Buttons==0 && neutral.LX==0 && neutral.LY==0 && neutral.LT==0,"Neutral input does not cause actions");
            ushort[] masks={0x1000,0x2000,0x4000,0x8000,0x20,0x400,0x10,0x40,0x80,0x100,0x200,1,2,4,8};
            for(int i=0;i<masks.Length;i++) Check(SwitchProInput.Translate(1u<<i,0,0,0,0,0,0).Buttons==masks[i],"Physical Switch button mapping "+i);
            var extremes=SwitchProInput.Translate(0,Int16.MinValue,Int16.MinValue,Int16.MaxValue,Int16.MaxValue,Int16.MinValue,Int16.MaxValue);
            Check(extremes.LX==Int16.MinValue && extremes.LY==Int16.MaxValue && extremes.RX==Int16.MaxValue && extremes.RY==-Int16.MaxValue && extremes.LT==0 && extremes.RT==255,"Sticks and triggers use the existing input ranges without overflow");
            long time=0; bool attached=false; var fake=new Backend();
            var reader=new SwitchProInput(fake,() => attached,() => time);
            Check(reader.ReadAll().Count==0 && fake.Starts==0,"Inactive reader never loads a controller backend");
            reader.Start(); reader.ReadAll(); Check(fake.Starts==0 && !Directory.Exists(Path.Combine(root,"runtime")),"No connected Switch Pro means no SDL extraction or initialization");
            attached=true; time=1000; fake.States.Add(new KeyValuePair<string,PadState>("switch:1",SwitchProInput.Translate(1,0,0,0,0,0,0)));
            Check(reader.ReadAll().Count==1 && fake.Starts==1,"Hot plugging initializes the reader and produces input");
            fake.States.Add(new KeyValuePair<string,PadState>("switch:2",neutral));
            Check(reader.ReadAll().Count==2,"Multiple Switch pads keep separate identities");
            var router=new ControllerRouter(); PadState selected; string identity;
            var pads=new List<KeyValuePair<string,PadState>> {new KeyValuePair<string,PadState>("xinput:0",neutral)}; pads.AddRange(reader.ReadAll());
            Check(router.Select(pads,out selected,out identity) && identity=="switch:1","Active Switch input is not blocked by an idle Xbox controller");
            fake.Fail=true; Check(reader.ReadAll().Count==0 && reader.LastError=="Disconnected" && fake.Stops==1,"Backend failure releases native input without breaking other controllers");
            fake.Fail=false; time=6000; Check(reader.ReadAll().Count==2 && fake.Starts==2,"Reader can recover after a backend failure");
            reader.Stop(); Check(reader.ReadAll().Count==0 && fake.Stops==2,"Dismissal releases Switch devices and clears input");
            reader.Start(); Check(reader.ReadAll().Count==2 && fake.Starts==3,"Reopening the overlay reconnects the backend"); reader.Stop();
            string runtime=SdlSwitchProBackend.PrepareLibrary();
            Check(File.Exists(runtime) && File.Exists(Path.Combine(Path.GetDirectoryName(runtime),"LICENSE.txt")) && AppUpdates.Hash(runtime)==SdlSwitchProBackend.DllHash,"Embedded SDL extracts with its license and verified checksum");
            File.WriteAllText(runtime,"damaged");
            Check(AppUpdates.Hash(SdlSwitchProBackend.PrepareLibrary())==SdlSwitchProBackend.DllHash,"Damaged runtime is replaced from the verified embedded copy");
            using(var process=Process.Start(new ProcessStartInfo(System.Reflection.Assembly.GetExecutingAssembly().Location,"--native-smoke \""+root+"\"") {UseShellExecute=false,CreateNoWindow=true})) {
                if(!process.WaitForExit(15000)) {process.Kill(); throw new Exception("Native SDL smoke test did not finish");}
                Check(process.ExitCode==0,"Official SDL x64 runtime loads, initializes, polls and restarts on Windows");
            }
            Console.WriteLine(checks+" Switch Pro input checks passed.");
        } finally {Storage.Root=previous; Directory.Delete(root,true);}
    }
}
