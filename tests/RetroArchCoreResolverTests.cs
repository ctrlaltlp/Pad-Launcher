using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using PadLauncher;
class RetroArchCoreResolverTests {
    static int checks;
    static void Check(bool condition,string message) {if(!condition) throw new Exception(message); checks++;}
    static string Write(string root,string relative,string text="") {string path=Path.GetFullPath(Path.Combine(root,relative)); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,text); return path;}
    public static void Main(string[] args) {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-core-resolver-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            string exe=Write(root,"new install/retroarch.exe"),folder=Path.GetDirectoryName(exe);
            string config=Write(folder,"retroarch.cfg","libretro_directory = \":\\downloaded_cores\"\nlibretro_info_path = \":\\coreinfo\"\nplaylist_directory = \":\\playlists\"");
            string mgba=Write(folder,"downloaded_cores/mgba_libretro.dll"),snes=Write(folder,"downloaded_cores/snes9x_libretro.dll");
            Write(folder,"coreinfo/mgba_libretro.info","corename = \"mGBA\"\ndatabase = \"Nintendo - Game Boy|Nintendo - Game Boy Color|Nintendo - Game Boy Advance\"\nsupported_extensions = \"gb|gbc|gba\"");
            Write(folder,"coreinfo/snes9x_libretro.info","corename = \"Snes9x\"\ndatabase = \"Nintendo - Super Nintendo Entertainment System\"\nsupported_extensions = \"sfc|smc\"");
            Write(folder,"cores/unused_libretro.dll");
            var resolver=new RetroArchCoreResolver(exe); string problem;
            Check(resolver.Paths.Count==2 && EmulatorLibrary.Cores(exe).Contains(mgba),"Core discovery honours configured core and info directories");
            Check(resolver.Resolve("DETECT","DETECT",null,null,null,"Game.zip#Game.gb","Nintendo - Game Boy.lpl",out problem)==mgba && problem==null,"Standard DETECT playlist resolves a unique compatible installed core from its metadata");
            string oldCore=Path.Combine(root,"old install","cores","mgba_libretro.dll");
            Check(resolver.Relocate(oldCore)==mgba && resolver.Relocate(@":\downloaded_cores\mgba_libretro.dll")==mgba,"Missing old installation paths and portable paths resolve to the same installed DLL");
            Check(resolver.Resolve(null,"mGBA",null,null,null,"Game.gb",null,out problem)==mgba,"Explicit core names are matched to installed metadata");
            Check(resolver.Resolve(null,null,null,"Snes9x",null,"Game.sfc",null,out problem)==snes,"Playlist default core names are honoured");
            Check(resolver.Resolve(null,null,null,null,null,"Game.cue","Sony - PlayStation",out problem)==null && problem.Contains("No usable core"),"Incompatible installed cores are never guessed");
            Check(resolver.Resolve(null,null,null,null,null,"Game.gb",null,out problem)==null,"An unidentified playlist platform requires user selection rather than guessing from the file extension alone");
            string rom=Write(folder,"roms/Game.gb");
            string playlist=Write(folder,"playlists/Nintendo - Game Boy.lpl",Storage.Json().Serialize(new {items=new[]{new {path=rom,label="Game",core_path="DETECT",core_name="DETECT",db_name="Nintendo - Game Boy.lpl"}}}));
            var source=EmulatorImport.Sources("retroarch",exe,config).Single();
            var batch=EmulatorImport.Read("retroarch",exe,new[]{source},null,CancellationToken.None);
            Check(batch.Games.Single().CorePath==mgba && batch.Games.Single().Problem==null,"Library import uses metadata core resolution without requiring a global default");
            Write(folder,"downloaded_cores/gambatte_libretro.dll");
            Write(folder,"coreinfo/gambatte_libretro.info","corename = \"Gambatte\"\ndatabase = \"Nintendo - Game Boy\"\nsupported_extensions = \"gb|gbc\"");
            batch=EmulatorImport.Read("retroarch",exe,new[]{source},null,CancellationToken.None);
            Check(batch.Games.Single().Problem.StartsWith("Multiple compatible cores") && !EmulatorImport.CanImport("retroarch",batch.Games[0],null,null),"Ambiguous matches remain unselected until the user chooses a core");
            source.CorePath=mgba; batch=EmulatorImport.Read("retroarch",exe,new[]{source},null,CancellationToken.None);
            Check(batch.Games.Single().CorePath==mgba && batch.Games.Single().Problem==null,"A per-source user choice resolves ambiguous installed cores");
            var settings=new Settings(); var library=new Library();
            string destination=EmulatorImport.Import(settings,library,"retroarch",exe,config,new[]{source},batch.Games,null,null,"Game Boy");
            var newSource=EmulatorImport.Sources("retroarch",exe,config).Single(); EmulatorImport.RestoreSourceCores(settings,"retroarch",config,new[]{newSource});
            Check(newSource.CorePath==mgba,"Per-playlist user core choices are restored on later imports");
            EmulatorImport.Import(settings,library,"retroarch",exe,config,new[]{newSource},EmulatorImport.Read("retroarch",exe,new[]{newSource},null,CancellationToken.None).Games,null,destination,"ignored");
            Check(settings.Emulators.Single().Games.Count==1 && settings.CustomLists.Count==1,"Repeated import with saved core choice preserves game and list identity");
            var moved=new Settings {Emulators=new List<EmulatorConfig> {new EmulatorConfig {Id="retroarch",Path=exe,CorePath=oldCore,RomFolder="keep ROM folder",Games=new List<EmulatorGame> {new EmulatorGame {Id="keep",Name="Keep name",Path=rom,CorePath=oldCore}}}}};
            EmulatorLibrary.SavePath(moved,"retroarch",exe);
            Check(moved.Emulators.Single().CorePath==mgba && moved.Emulators.Single().Games.Single().CorePath==mgba && moved.Emulators.Single().RomFolder=="keep ROM folder","Saving an already updated executable also repairs stale default and per-game paths to the same DLL");
            string external=Write(root,"external/custom_libretro.dll");
            Check(resolver.Relocate(external)==external,"Valid explicit external cores are preserved");
            var existing=moved.Emulators.Single().Games.Single(); existing.CorePath=oldCore;
            Check(!EmulatorImport.CanImport("retroarch",new EmulatorImportGame {Problem="Missing core"},existing,null),"An existing entry with a stale core does not falsely count as ready");
            if(args.Length>0) {
                string actualExe=args[0],location=EmulatorImport.DetectLocation("retroarch",actualExe); var sources=EmulatorImport.Sources("retroarch",actualExe,location);
                var actual=EmulatorImport.Read("retroarch",actualExe,sources,null,CancellationToken.None);
                foreach(var item in sources) Console.WriteLine(item.Name+": "+EmulatorImport.RetroArchSourceCoreStatus(actual,item,new EmulatorGame[0])+"; "+actual.Games.Count(game => game.SourceId==item.Id && game.Problem==null)+" ready");
            }
            Console.WriteLine(checks+" RetroArch core resolution checks passed.");
        } finally {Directory.Delete(root,true);}
    }
}
