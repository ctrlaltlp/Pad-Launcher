using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using PadLauncher;
class EmulatorTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    static void Reject(Action action,string name) {bool rejected=false; try {action();} catch(ArgumentException) {rejected=true;} catch(IOException) {rejected=true;} Check(rejected,name);}
    static string FileAt(string root,string relative,string content="") {string path=Path.Combine(root,relative); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,content); return path;}
    static void Sfo(string path,string title) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); var text=Encoding.UTF8.GetBytes(title+"\0");
        using(var writer=new BinaryWriter(File.Create(path))) {writer.Write(0x46535000u); writer.Write(0x101u); writer.Write(36u); writer.Write(42u); writer.Write(1u); writer.Write((ushort)0); writer.Write((ushort)0x204); writer.Write(text.Length); writer.Write(text.Length); writer.Write(0u); writer.Write(Encoding.ASCII.GetBytes("TITLE\0")); writer.Write(text);}
    }
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EmulatorTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            var settings=new Settings();
            Check(settings.Emulators==null && EmulatorLibrary.Presets.Length==13,"Existing settings need no migration and all thirteen emulators are available");
            Check(settings.RemoveMissingEmulatorGames,"Missing-ROM cleanup is enabled by default");
            string missingProbe=FileAt(root,"missing-check\\game.iso");
            Check(!EmulatorLibrary.ConfirmMissingRom(missingProbe),"Existing ROM is preserved");
            File.Delete(missingProbe);
            Check(EmulatorLibrary.ConfirmMissingRom(missingProbe),"Missing ROM in a readable parent is confirmed");
            Check(!EmulatorLibrary.ConfirmMissingRom(Path.Combine(root,"unavailable-folder","game.iso")),"Unavailable parent folder preserves the entry");
            string executable=FileAt(root,"Emulators and games\\test.exe");
            var paths=new Dictionary<string,string>();
            paths["duckstation"]=FileAt(root,"PS1\\Game A.chd");
            paths["pcsx2"]=FileAt(root,"PS2\\Game B.iso");
            paths["dolphin"]=FileAt(root,"Wii\\Game C.rvz");
            paths["eden"]=FileAt(root,"Switch\\Game D.xci");
            paths["xenia"]=FileAt(root,"360\\Game E\\default.xex");
            paths["melonds"]=FileAt(root,"DS\\Game F.nds");
            paths["rpcs3"]=FileAt(root,"PS3\\Game G\\PS3_GAME\\USRDIR\\EBOOT.BIN");
            Sfo(Path.Combine(root,"PS3","Game G","PS3_GAME","PARAM.SFO"),"Game G – PS3");
            paths["vita3k"]=Path.Combine(root,"Vita","ux0","app","PCSE00001");
            FileAt(paths["vita3k"],"eboot.bin"); Sfo(Path.Combine(paths["vita3k"],"sce_sys","param.sfo"),"Game H – Vita");
            paths["ppsspp"]=FileAt(root,"PSP\\Game I.chd");
            paths["mgba"]=FileAt(root,"GBA\\Game J.gba");
            paths["retroarch"]=FileAt(root,"Retro\\Game K.sfc");
            paths["cemu"]=FileAt(root,"Wii U\\Game L\\code\\game.rpx");
            paths["shadps4"]=FileAt(root,"PS4\\CUSA00001\\eboot.bin");
            FileAt(root,"PS4\\CUSA00001\\data.bin");
            Sfo(Path.Combine(root,"PS4","CUSA00001","sce_sys","param.sfo"),"Game M – PS4");
            Check(EmulatorLibrary.Scan("shadps4",Path.Combine(root,"PS4"),System.Threading.CancellationToken.None,null,true).Paths.SequenceEqual(new[]{paths["shadps4"]}),"shadPS4 scans game executables only");
            string core=FileAt(root,"Emulators and games\\cores\\snes9x_libretro.dll"),otherCore=FileAt(root,"Emulators and games\\cores\\mgba_libretro.dll");
            EmulatorLibrary.SaveCore(settings,core);
            foreach(var preset in EmulatorLibrary.Presets) {
                EmulatorLibrary.SavePath(settings,preset.Id,executable);
                Check(EmulatorLibrary.AddGames(settings,preset.Id,new[]{paths[preset.Id],paths[preset.Id]})==1,"Adds one unique game for "+preset.Name);
                Check(EmulatorLibrary.AddGames(settings,preset.Id,new[]{paths[preset.Id]})==0,"Repeat add is idempotent for "+preset.Name);
            }
            var library=new Library(); EmulatorLibrary.Apply(library,settings); EmulatorLibrary.Apply(library,settings);
            Check(library.Games.Count==paths.Count && library.Collections.Count==paths.Count,"Repeated apply does not duplicate games or source lists");
            var cemu=library.Games.Single(g => g.EmulatorId=="cemu");
            var shad=library.Games.Single(g => g.EmulatorId=="shadps4");
            Check(cemu.Name=="Game L" && GameLaunch.StartInfo(cemu,settings).Arguments=="-f -g \""+paths["cemu"]+"\"","Cemu names extracted games and launches their RPX");
            EmulatorLibrary.Config(settings,"cemu").Arguments="-g {game}";
            Check(GameLaunch.StartInfo(cemu,settings).Arguments=="-g \""+paths["cemu"]+"\"","Global argument templates replace defaults and quote game paths");
            cemu.Arguments="--extra";
            Check(GameLaunch.StartInfo(cemu,settings).Arguments=="-g \""+paths["cemu"]+"\" --extra","Per-game extra arguments append to the global template");
            cemu.Arguments="--custom {game}";
            Check(GameLaunch.StartInfo(cemu,settings).Arguments=="--custom \""+paths["cemu"]+"\"","Per-game templates override global arguments");
            cemu.Arguments=null;
            EmulatorLibrary.Config(settings,"cemu").Arguments=null;
            Check(shad.Name=="Game M – PS4" && GameLaunch.StartInfo(shad,settings).Arguments=="\""+paths["shadps4"]+"\"","shadPS4 uses SFO titles and launches eboot.bin");
            var retro=library.Games.First(g => g.EmulatorId=="retroarch");
            EmulatorLibrary.SaveCore(settings,otherCore);
            Check(GameLaunch.StartInfo(retro,settings).Arguments.Contains("\""+core+"\""),"Changing default core leaves existing games assigned to their original core");
            EmulatorLibrary.SaveGame(settings,"retroarch",retro.Id,"Retro game",retro.LaunchId,otherCore);
            Check(GameLaunch.StartInfo(retro,settings).Arguments=="-f -L \""+otherCore+"\" \""+retro.LaunchId+"\"","Per-game core override is quoted and used at launch");
            Check(EmulatorLibrary.Cores(executable).Count==2,"Installed cores discovered beside executable");
            Reject(() => EmulatorLibrary.SaveCore(settings,executable),"Core picker rejects executables");
            var noCore=new Settings(); EmulatorLibrary.SavePath(noCore,"retroarch",executable);
            Reject(() => EmulatorLibrary.AddGames(noCore,"retroarch",new[]{retro.LaunchId}),"Adding RetroArch games requires a core");
            string scanRetro=Path.Combine(root,"Retro scan"); string gba=FileAt(scanRetro,"nested\\GBA.gba"); FileAt(scanRetro,"nested\\NES.nes");
            FileAt(root,"Emulators and games\\info\\mgba_libretro.info","supported_extensions = \"gba|gb|gbc\"");
            Check(EmulatorLibrary.Scan("retroarch",scanRetro,CancellationToken.None,otherCore).Paths.SequenceEqual(new[]{gba}),"RetroArch scan uses selected core's supported extensions");
            Check(EmulatorLibrary.Scan("ppsspp",Path.Combine(root,"PSP"),CancellationToken.None).Paths.SequenceEqual(new[]{paths["ppsspp"]}),"PPSSPP recursive scan finds CHD images");
            Check(EmulatorLibrary.Scan("mgba",Path.Combine(root,"GBA"),CancellationToken.None).Paths.SequenceEqual(new[]{paths["mgba"]}),"mGBA recursive scan finds GBA ROMs");
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="ppsspp"),settings).Arguments.StartsWith("--fullscreen --pause-menu-exit "),"PPSSPP fullscreen and frontend exit options");
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="mgba"),settings).Arguments.StartsWith("-f "),"mGBA fullscreen option");
            File.Delete(otherCore); Check(EmulatorLibrary.Missing(retro,settings),"Missing assigned core identified");
            Reject(() => GameLaunch.StartInfo(retro,settings),"Missing core prevents launch"); File.WriteAllText(otherCore,"");
            var restored=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            foreach(var game in library.Games) {
                var start=GameLaunch.StartInfo(game,restored);
                string expectedPath=game.EmulatorId=="vita3k" ? "PCSE00001" : game.LaunchId;
                Check(start.FileName==executable && start.Arguments.Contains("\""+expectedPath+"\"") && start.UseShellExecute && start.WorkingDirectory==Path.GetDirectoryName(executable),"Launch routes through configured "+game.EmulatorId+" and quotes its game");
            }
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="duckstation"),settings).Arguments.StartsWith("-batch -fullscreen -- "),"DuckStation batch/fullscreen preset");
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="pcsx2"),settings).Arguments.StartsWith("-batch -fullscreen -- "),"PCSX2 batch/fullscreen preset");
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="rpcs3"),settings).Arguments.StartsWith("--no-gui --fullscreen "),"RPCS3 native boot preset");
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="dolphin"),settings).Arguments.StartsWith("-b -e "),"Dolphin native executable option");
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="eden"),settings).Arguments.StartsWith("-f -g "),"Eden native game option");
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="melonds"),settings).Arguments.StartsWith("-f "),"melonDS fullscreen preset");
            Check(GameLaunch.StartInfo(library.Games.First(g => g.EmulatorId=="vita3k"),settings).Arguments=="--fullscreen -r \"PCSE00001\"","Vita3K uses installed title ID rather than treating a VPK as a ROM");
            Check(library.Games.First(g => g.EmulatorId=="rpcs3").Name=="Game G – PS3" && library.Games.First(g => g.EmulatorId=="vita3k").Name=="Game H – Vita","PS3 and Vita display titles read from SFO metadata");
            Check(EmulatorLibrary.GamePath("rpcs3",Path.Combine(root,"PS3","Game G"))==paths["rpcs3"],"RPCS3 folder resolves to canonical boot executable");
            Check(EmulatorLibrary.GamePath("vita3k",Path.Combine(paths["vita3k"],"eboot.bin"))==paths["vita3k"],"Vita executable resolves to canonical installed folder");
            string scanRoot=Path.Combine(root,"Scan");
            FileAt(scanRoot,"nested\\Disc 1.bin"); FileAt(scanRoot,"nested\\Disc 2.bin");
            FileAt(scanRoot,"nested\\Disc 1.cue","FILE \"Disc 1.bin\" BINARY\n TRACK 01 MODE2/2352");
            FileAt(scanRoot,"nested\\Disc 2.cue","FILE \"Disc 2.bin\" BINARY");
            string playlist=FileAt(scanRoot,"nested\\Game.m3u","# discs\nDisc 1.cue\nDisc 2.cue");
            string chd=FileAt(scanRoot,"other\\Single.chd"); FileAt(scanRoot,"other\\ignore.txt");
            var scanned=EmulatorLibrary.Scan("duckstation",scanRoot,CancellationToken.None);
            Check(scanned.Paths.Count==2 && scanned.Paths.Contains(playlist) && scanned.Paths.Contains(chd),"Recursive scan suppresses CUE tracks and playlist discs");
            string standalone=FileAt(scanRoot,"other\\Renamed.bin");
            string brokenCue=FileAt(scanRoot,"other\\Renamed.cue","FILE \"Old name.bin\" BINARY");
            var repairedScan=EmulatorLibrary.Scan("duckstation",scanRoot,CancellationToken.None);
            Check(repairedScan.Paths.Contains(standalone) && !repairedScan.Paths.Contains(brokenCue),"Broken CUE does not hide a standalone BIN");
            Check(EmulatorLibrary.Scan("rpcs3",Path.Combine(root,"PS3"),CancellationToken.None).Paths.SequenceEqual(new[]{paths["rpcs3"]}),"PS3 scanner finds only boot executables");
            Check(EmulatorLibrary.Scan("vita3k",Path.Combine(root,"Vita"),CancellationToken.None).Paths.SequenceEqual(new[]{paths["vita3k"]}),"Vita scanner finds installed app folders");
            FileAt(root,"DolphinMods/base.iso");
            string modPreset=FileAt(root,"DolphinMods/Midnight.json","{\"type\":\"dolphin-game-mod-descriptor\",\"version\":1,\"base-file\":\"base.iso\"}");
            FileAt(root,"DolphinMods/other.json","{}");
            var dolphinScan=EmulatorLibrary.Scan("dolphin",Path.Combine(root,"DolphinMods"),CancellationToken.None);
            Check(dolphinScan.Paths.Count==2 && dolphinScan.Paths.Contains(Path.GetFullPath(modPreset)),"Dolphin scans game mod presets and skips unrelated JSON");
            FileAt(root,"360\\readme");
            string container=FileAt(root,"360\\Content\\00007000\\ABCDEF");
            byte[] package=new byte[0x348];
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("LIVE"),package,4);
            package[0x346]=0x70;
            File.WriteAllBytes(container,package);
            Array.Copy(System.Text.Encoding.ASCII.GetBytes("PIRS"),package,4);
            package[0x346]=0;
            package[0x345]=0x0B;
            string update=FileAt(root,"360\\Game\\$SystemUpdate\\su20076000_00000000");
            File.WriteAllBytes(update,package);
            package[0x345]=0x08;
            string demo=FileAt(root,"360\\Game\\Content\\00080000\\demo");
            File.WriteAllBytes(demo,package);
            string extracted=FileAt(root,"360\\Game\\default.xex");
            var candidates=EmulatorLibrary.Scan("xenia",Path.Combine(root,"360"),CancellationToken.None).Paths;
            Check(candidates.Contains(extracted) && !candidates.Contains(update) && !candidates.Contains(demo),"Xenia scan excludes update and demo packages from extracted games");
            Check(EmulatorLibrary.Scan("xenia",Path.Combine(root,"360"),CancellationToken.None).Paths.Contains(container) && !EmulatorLibrary.Scan("xenia",Path.Combine(root,"360"),CancellationToken.None).Paths.Any(p => Path.GetFileName(p)=="readme"),"Extensionless Xbox content requires a container signature");
            var cancelled=new CancellationTokenSource(); cancelled.Cancel(); bool cancellation=false; try {EmulatorLibrary.Scan("duckstation",scanRoot,cancelled.Token);} catch(OperationCanceledException) {cancellation=true;} Check(cancellation,"Recursive scanning can be cancelled");
            int before=EmulatorLibrary.Config(settings,"duckstation").Games.Count;
            Reject(() => EmulatorLibrary.AddGames(settings,"duckstation",new[]{chd,Path.Combine(root,"missing.chd")}),"Invalid bulk selection rejected");
            Check(EmulatorLibrary.Config(settings,"duckstation").Games.Count==before,"Failed bulk selection adds nothing");
            Reject(() => EmulatorLibrary.SavePath(settings,"eden",paths["eden"]),"Game images cannot be saved as emulator executables");
            var ps1=library.Games.First(g => g.EmulatorId=="duckstation");
            EmulatorLibrary.SaveGame(settings,"duckstation",ps1.Id,"Renamed",ps1.LaunchId);
            Check(EmulatorLibrary.Config(settings,"duckstation").Games[0].Id==ps1.Id && EmulatorLibrary.Config(settings,"duckstation").Games[0].Name=="Renamed","Editing preserves game identity");
            string list=CustomLists.Save(settings,library,null,"Emulated favourites",new[]{ps1.Id});
            string combined=CombinedCollections.Save(settings,library,null,"Emulated together",new[]{list,"source:emulator:pcsx2"});
            Check(CombinedCollections.Options(library,settings).Single(c => c.Id==combined).Games.Count==2,"Emulated titles work in custom and combined lists");
            File.Delete(ps1.LaunchId); Check(EmulatorLibrary.Missing(ps1,settings),"Missing ROM identified");
            Reject(() => GameLaunch.StartInfo(ps1,settings),"Missing ROM launch rejected");
            EmulatorLibrary.RemoveGame(settings,"duckstation",ps1.Id); EmulatorLibrary.Apply(library,settings);
            Check(!library.Games.Any(g => g.Id==ps1.Id) && File.Exists(executable),"Remove only removes saved entry");
            File.Delete(executable); Check(EmulatorLibrary.Missing(library.Games[0],settings),"Missing emulator identified");
            Reject(() => GameLaunch.StartInfo(library.Games[0],settings),"Missing emulator launch rejected");
            Check(EmulatorLibrary.SfoTitle(FileAt(root,"invalid.sfo","broken"))==null,"Invalid metadata ignored safely");
            Console.WriteLine(checks+" emulator checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
