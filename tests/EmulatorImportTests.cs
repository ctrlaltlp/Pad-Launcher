using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using PadLauncher;
class EmulatorImportTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    static string Write(string root,string file,string text="") {string path=Path.Combine(root,file); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path,text); return path;}
    static void Sfo(string path,string title,string category) {
        Directory.CreateDirectory(Path.GetDirectoryName(path)); byte[] name=Encoding.UTF8.GetBytes(title+"\0"),cat=Encoding.UTF8.GetBytes(category+"\0"),keys=Encoding.ASCII.GetBytes("TITLE\0CATEGORY\0");
        using(var w=new BinaryWriter(File.Create(path))) {
            w.Write(0x46535000u); w.Write(0x101u); w.Write(52u); w.Write((uint)(52+keys.Length)); w.Write(2u);
            w.Write((ushort)0); w.Write((ushort)0x204); w.Write(name.Length); w.Write(name.Length); w.Write(0u);
            w.Write((ushort)6); w.Write((ushort)0x204); w.Write(cat.Length); w.Write(cat.Length); w.Write((uint)name.Length);
            w.Write(keys); w.Write(name); w.Write(cat);
        }
    }
    static string Quote(string value) {return "\""+value.Replace("\\","\\\\").Replace("\"","\\\"")+"\"";}
    static string AzaharApp(string root,string relative,uint category,bool executable=true) {
        string path=Write(root,relative); byte[] data=new byte[0x1000];
        Encoding.ASCII.GetBytes("NCCH").CopyTo(data,0x100);
        BitConverter.GetBytes(((ulong)category<<32)|0x123400UL).CopyTo(data,0x118);
        data[0x18d]=(byte)(executable ? 2 : 1); BitConverter.GetBytes(1u).CopyTo(data,0x1a0); BitConverter.GetBytes(5u).CopyTo(data,0x1a4);
        Encoding.ASCII.GetBytes("icon").CopyTo(data,0x200); BitConverter.GetBytes(0x408u).CopyTo(data,0x20c);
        Encoding.ASCII.GetBytes("SMDH").CopyTo(data,0x400); Encoding.Unicode.GetBytes("Example 3DS title").CopyTo(data,0x688);
        File.WriteAllBytes(path,data); return path;
    }
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_open_v2(byte[] name,out IntPtr db,int flags,IntPtr vfs);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_exec(IntPtr db,byte[] sql,IntPtr callback,IntPtr arg,IntPtr error);
    [DllImport("winsqlite3.dll",CallingConvention=CallingConvention.Cdecl)] static extern int sqlite3_close(IntPtr db);
    static string Sql(string text) {return "'"+text.Replace("'","''")+"'";}
    static void MgbaFixture(string file,string romRoot,string rom) {
        IntPtr db; int opened=sqlite3_open_v2(Encoding.UTF8.GetBytes(file+"\0"),out db,6,IntPtr.Zero);
        try {
            if(opened!=0) throw new Exception("SQLite fixture could not be opened");
            string schema="CREATE TABLE roots(rootid INTEGER,path TEXT); CREATE TABLE paths(rootid INTEGER,romid INTEGER,path TEXT,customTitle TEXT); CREATE TABLE roms(romid INTEGER,internalTitle TEXT); INSERT INTO roots VALUES(1,"+Sql(romRoot)+"); INSERT INTO roms VALUES(1,'Internal'); INSERT INTO paths VALUES(1,1,"+Sql(rom)+",'Pokémon custom');";
            if(sqlite3_exec(db,Encoding.UTF8.GetBytes(schema+"\0"),IntPtr.Zero,IntPtr.Zero,IntPtr.Zero)!=0) throw new Exception("SQLite fixture could not be populated");
        } finally {if(db!=IntPtr.Zero) sqlite3_close(db);}
    }
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"EmulatorImportTests-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            var token=CancellationToken.None;
            Check(EmulatorTitleTags.Clean("Game (USA, Europe) (En,Fr,De) (Rev 1) [n]")=="Game","Title cleanup removes region, language, revision and dump tags found in RetroArch paths");
            Check(EmulatorTitleTags.Clean("Game (Japan) [T-En by Aeon Genesis v1.00] (SGB Enhanced)")=="Game","Title cleanup removes translation and compatibility metadata");
            Check(EmulatorTitleTags.Clean("Game (PAL) (NTSC-U) (Disc 2) [Legend of the Crystals]")=="Game (Disc 2) [Legend of the Crystals]","Title cleanup preserves disc numbers and unknown subtitle text");
            Check(EmulatorTitleTags.Clean("Game USA") == "Game USA" && EmulatorTitleTags.Clean("(USA)")=="(USA)" && EmulatorTitleTags.Clean("Game - PAL")=="Game","Title cleanup protects untagged title words and never empties a title");
            Check(EmulatorTitleTags.Normalise("Legend of Zelda, The")=="The Legend of Zelda","Normalisation moves a trailing article to the front");
            Check(EmulatorTitleTags.Normalise("Legend of Zelda, The - A Link to the Past (USA)")=="The Legend of Zelda - A Link to the Past (USA)","Normalisation keeps subtitles and region tags");
            Check(EmulatorTitleTags.Normalise("Game, The (Disc 2) [!]")=="The Game (Disc 2) [!]","Normalisation keeps disc and dump metadata");
            Check(EmulatorTitleTags.Normalise("Game, The Movie")=="Game, The Movie" && EmulatorTitleTags.Normalise("The Legend of Zelda")=="The Legend of Zelda" && EmulatorTitleTags.Normalise("Game, Theatre")=="Game, Theatre","Normalisation does not rewrite other title words");
            Check(EmulatorTitleTags.Normalise(EmulatorTitleTags.Normalise("Game, The"))=="The Game","Repeated normalisation is idempotent");
            string azahar=Write(root,"Azahar/azahar.exe"),azRoot=Path.GetDirectoryName(azahar);
            string azRom=Write(azRoot,"roms/Cartridge (USA).cci"),nested=Write(azRoot,"roms/nested/Other.zcci");
            Write(azRoot,"roms/Installer.cia");
            string azApp=AzaharApp(azRoot,"user/sdmc/Nintendo 3DS/00000000000000000000000000000000/00000000000000000000000000000000/title/00040000/00123400/content/00000000.app",0x00040000);
            AzaharApp(azRoot,"roms/update.app",0x0004000e); AzaharApp(azRoot,"roms/dlc.app",0x0004008c); AzaharApp(azRoot,"roms/data.app",0x00040000,false);
            string azConfig=Write(azRoot,"user/config/qt-config.ini","[Paths]\ngamedirs\\1\\path="+Path.Combine(azRoot,"roms").Replace("\\","/")+"\ngamedirs\\1\\deep_scan=true\ngamedirs\\2\\path=INSTALLED\ngamedirs\\size=2\n");
            Check(Path.GetFullPath(EmulatorImport.DetectLocation("azahar",azahar))==Path.GetFullPath(azConfig),"Azahar detects portable config before installed data");
            var pathSettings=new Settings();
            EmulatorLibrary.SavePath(pathSettings,"azahar",azahar);
            var pathConfig=EmulatorLibrary.Config(pathSettings,"azahar");
            Check(Path.GetFullPath(pathConfig.ImportPath)==Path.GetFullPath(azConfig),"Saving an executable detects its library location");
            pathConfig.ImportPath="manual-library"; pathConfig.RomFolder="keep-rom-folder";
            EmulatorLibrary.SavePath(pathSettings,"azahar",azahar);
            Check(pathConfig.ImportPath=="manual-library","Saving the same executable preserves a manual library location");
            string movedExecutable=Write(root,"MovedAzahar/azahar.exe");
            string movedConfig=Write(root,"MovedAzahar/user/config/qt-config.ini","[Paths]");
            EmulatorLibrary.SavePath(pathSettings,"azahar",movedExecutable);
            Check(Path.GetFullPath(pathConfig.ImportPath)==Path.GetFullPath(movedConfig) && pathConfig.RomFolder=="keep-rom-folder","Changing the executable updates library detection without clearing the ROM folder");
            EmulatorLibrary.SavePath(pathSettings,"azahar","");
            Check(pathConfig.Path==null && pathConfig.ImportPath==null,"Clearing the executable clears its stale library location");
            var azSources=EmulatorImport.Sources("azahar",azahar,azConfig);
            Check(azSources.Count==2 && azSources.All(source => source.Recursive),"Azahar discovers configured recursive folder and installed titles");
            var azBatch=EmulatorImport.Read("azahar",azahar,azSources,null,token);
            Check(azBatch.Games.Count==3 && azBatch.Games.Any(g => g.Path==Path.GetFullPath(azRom)) && azBatch.Games.Any(g => g.Path==Path.GetFullPath(nested)) && azBatch.Games.Any(g => g.Path==Path.GetFullPath(azApp)),"Azahar imports base games and excludes CIA installers, updates, DLC and nonexecutables");
            Check(azBatch.Games.Single(g => g.Path==Path.GetFullPath(azApp)).Name=="Example 3DS title","Azahar installed games use embedded English title");
            var azSettings=new Settings(); EmulatorLibrary.SavePath(azSettings,"azahar",azahar);
            Check(EmulatorLibrary.AddGames(azSettings,"azahar",azBatch.Games.Select(g => g.Path))==3 && EmulatorLibrary.AddGames(azSettings,"azahar",azBatch.Games.Select(g => g.Path))==0,"Azahar repeated imports do not duplicate games");
            var azLibrary=new Library(); EmulatorLibrary.Apply(azLibrary,azSettings);
            Check(GameLaunch.StartInfo(azLibrary.Games.First(),azSettings).Arguments.StartsWith("-f \""),"Azahar launches fullscreen with a quoted game path");
            Write(azRoot,"user/config/qt-config.ini","[Paths]\ngamedirs\\1\\path="+Path.Combine(azRoot,"roms").Replace("\\","/")+"\ngamedirs\\1\\deep_scan=false\n[Data Storage]\nuse_custom_storage=true\nsdmc_directory="+Path.Combine(azRoot,"customsd").Replace("\\","/")+"\n");
            var shallow=EmulatorImport.Read("azahar",azahar,EmulatorImport.Sources("azahar",azahar,azConfig),null,token);
            Check(shallow.Games.Count==1 && shallow.Games[0].Path==Path.GetFullPath(azRom),"Azahar respects nonrecursive folder setting and custom SD location");

            string retro=Write(root,"Retro Arch\\retroarch.exe"),retroRoot=Path.GetDirectoryName(retro),core=Write(retroRoot,"cores\\snes9x_libretro.dll"),otherCore=Write(retroRoot,"cores\\mgba_libretro.dll");
            string rom=Write(retroRoot,"roms\\Game #1.sfc"),extra=Write(retroRoot,"roms\\New game.sfc");
            string retroCfg=Write(retroRoot,"retroarch.cfg","playlist_directory = \"playlists\"");
            string playlist=Write(retroRoot,"playlists\\SNES.lpl",Storage.Json().Serialize(new {default_core_path=core,items=new[]{new {path=@"roms\Game #1.sfc",label="Game One",core_path="DETECT",db_name="Nintendo - Super Nintendo Entertainment System.lpl"},new {path=@"roms\missing.sfc",label="Missing",core_path="DETECT",db_name="Nintendo - Super Nintendo Entertainment System.lpl"}}}));
            string legacy=Write(retroRoot,"playlists\\Favourites.lpl",String.Join("\n",new[]{rom,"Alternate label",otherCore,"mGBA","","Favourites.lpl"}));
            var sources=EmulatorImport.Sources("retroarch",retro,retroCfg);
            Check(sources.Count==2 && EmulatorImport.DetectLocation("retroarch",retro)==retroCfg,"RetroArch finds configured playlists beside selected executable");
            var snes=sources.Single(s => s.Path==playlist); var favorites=sources.Single(s => s.Path==legacy);
            var batch=EmulatorImport.Read("retroarch",retro,new[]{snes},null,token);
            Check(batch.Games.Count==1 && batch.Games[0].Path==rom && batch.Games[0].Name=="Game One" && batch.Games[0].CorePath==core && batch.Warnings.Count==1,"JSON playlist preserves labels/core defaults and skips missing files");
            Check(batch.Games[0].ArtworkSystem=="Nintendo - Super Nintendo Entertainment System" && batch.Games[0].ArtworkTitle=="Game One","RetroArch playlist database and original label are retained for box-art matching");
            var oldFormat=EmulatorImport.Read("retroarch",retro,new[]{favorites},null,token);
            Check(oldFormat.Games.Single().CorePath==otherCore,"Legacy six-line playlist preserves assigned core");
            var settings=new Settings {EnabledCollectionIds=new List<string>{"all"}}; var library=new Library {Games=new List<Game>{new Game {Id="steam-one",Name="Steam game"}}};
            string target=EmulatorImport.Import(settings,library,"retroarch",retro,retroCfg,new[]{snes},batch.Games,new[]{snes.Id+"|excluded"},null,"SNES games");
            string other=EmulatorImport.Import(settings,library,"retroarch",retro,retroCfg,new[]{favorites},oldFormat.Games,null,null,"Retro favourites");
            EmulatorLibrary.Apply(library,settings);
            var game=EmulatorLibrary.Config(settings,"retroarch").Games.Single(); string stableId=game.Id; game.Name="My renamed game";
            var renameSettings=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            var renameGame=EmulatorLibrary.Config(renameSettings,"retroarch").Games.Single(); renameGame.Name="My game (USA)";
            var renameDraft=new EmulatorImportGame {Path=rom,Name=EmulatorTitleTags.Clean(renameGame.Name),CorePath=core};
            EmulatorImport.AddEntries(renameSettings,"retroarch",retro,new[]{renameDraft});
            Check(renameGame.Name=="My game (USA)","Ordinary reimport preserves an existing title until cleanup is explicitly applied");
            renameDraft.RenameExisting=true; EmulatorImport.AddEntries(renameSettings,"retroarch",retro,new[]{renameDraft});
            Check(renameGame.Name=="My game" && EmulatorLibrary.Config(renameSettings,"retroarch").Games.Count==1 && game.Name=="My renamed game","Explicit title cleanup updates the saved entry without duplicates or touching the original settings draft");
            settings.ArtworkOverrides=new Dictionary<string,string>{{stableId,"my-art.png"}};
            settings.CustomLists.First(l => l.Id==target).Games.Add("steam-one"); settings.EnabledCollectionIds.Remove(target);
            Check(settings.CustomLists.Single(l => l.Id==other).Games.Single()==stableId && game.CorePath==core,"One ROM shared between playlists keeps existing entry and core");
            Check(game.ArtworkSystem=="Nintendo - Super Nintendo Entertainment System" && game.ArtworkTitle=="Game One","A mixed favourites playlist does not overwrite known artwork platform metadata");
            Write(retroRoot,"playlists\\SNES.lpl",Storage.Json().Serialize(new {default_core_path=core,items=new[]{new {path=extra,label="New game",core_path="DETECT"}}}));
            var update=EmulatorImport.Read("retroarch",retro,new[]{snes},null,token);
            EmulatorImport.Import(settings,library,"retroarch",retro,retroCfg,new[]{snes},update.Games,new[]{snes.Id+"|excluded"},target,"ignored");
            EmulatorImport.Import(settings,library,"retroarch",retro,retroCfg,new[]{snes},update.Games,new[]{snes.Id+"|excluded"},target,"ignored");
            Check(settings.EmulatorImports.Count==2 && EmulatorLibrary.Config(settings,"retroarch").Games.Count==2,"Repeat update adds new entries without duplicating games or import definitions");
            Check(settings.CustomLists.Single(l => l.Id==target).Games.Count==3 && game.Name=="My renamed game" && settings.ArtworkOverrides[stableId]=="my-art.png" && !settings.EnabledCollectionIds.Contains(target),"Update retains absent games, manual additions, names, art and list visibility");
            var restored=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings)); EmulatorLibrary.Apply(library,restored);
            string combined=CombinedCollections.Save(restored,library,null,"Mixed games",new[]{target,other});
            Check(CombinedCollections.Options(library,restored).Single(c => c.Id==combined).Games.Count==3 && restored.EmulatorImports.Single(d => d.ListId==target).ExcludedKeys.Count==1,"Imported lists combine with deduplication and saved exclusions persist");
            CustomLists.Delete(restored,other); Check(restored.EmulatorImports.Count==1,"Deleting destination cleans up saved import definition");
            string archive=Write(retroRoot,"roms\\Handheld games.zip"),sevenZip=Write(retroRoot,"roms\\More games.7z");
            var playlistSettings=new Settings {EnabledCollectionIds=new List<string>()}; var playlistLibrary=new Library();
            var playlistGames=batch.Games.Concat(oldFormat.Games).ToList();
            EmulatorImport.ImportPlaylists(playlistSettings,playlistLibrary,retro,retroCfg,new[]{snes,favorites},playlistGames,null);
            Check(playlistSettings.RetroArchPlaylistLists.Count==2 && playlistSettings.RetroArchPlaylistLists.Any(list => list.Name=="SNES") && playlistSettings.RetroArchPlaylistLists.Any(list => list.Name.StartsWith("Favourites")) && EmulatorLibrary.Config(playlistSettings,"retroarch").Games.Count==1,"Per-playlist import creates named lists and shares games appearing in multiple playlists");
            var namedPlaylist=playlistSettings.RetroArchPlaylistLists.First(list => list.Name=="SNES"); string namedId=namedPlaylist.Id;
            EmulatorLibrary.Apply(playlistLibrary,playlistSettings);
            Check(playlistLibrary.Collections.Any(list => list.Id==namedId && list.Source=="emulator") && !(playlistSettings.CustomLists ?? new List<CustomList>()).Any(),"Playlist lists are automatic emulator collections, not custom lists");
            bool blocked=false; try {CustomLists.Delete(playlistSettings,namedId);} catch(ArgumentException) {blocked=true;}
            Check(blocked && EmulatorLibrary.IsAutomaticList(playlistSettings,namedId),"Automatic playlist lists cannot be deleted");
            EmulatorLibrary.Apply(playlistLibrary,playlistSettings); EmulatorLibrary.Apply(playlistLibrary,playlistSettings);
            Check(playlistLibrary.Collections.Count(list => list.Id==namedId)==1,"Repeated library rebuilds do not duplicate automatic playlist collections");

            namedPlaylist.Name="My SNES"; playlistSettings.EnabledCollectionIds.Remove(namedId);
            EmulatorImport.ImportPlaylists(playlistSettings,playlistLibrary,retro,retroCfg,new[]{snes,favorites},playlistGames,null);
            Check(playlistSettings.RetroArchPlaylistLists.Count==2 && namedPlaylist.Id==namedId && namedPlaylist.Name=="My SNES" && !playlistSettings.EnabledCollectionIds.Contains(namedId) && playlistSettings.EmulatorImports.Count==2,"Repeated playlist import reuses lists and preserves renamed lists and overlay visibility");
            string archivePlaylist=Write(retroRoot,"playlists\\Archived.lpl",Storage.Json().Serialize(new {default_core_path=otherCore,items=new[]{
                new {path=@"roms\Handheld games.zip#folder\Yoshi.gb",label="Yoshi",core_path="DETECT",db_name="Nintendo - Game Boy.lpl"},
                new {path=@"roms\Handheld games.zip#Wario.gb",label="Wario",core_path="DETECT",db_name="Nintendo - Game Boy.lpl"},
                new {path=@"roms\More games.7z#Mario.gb",label="Mario",core_path="DETECT",db_name="Nintendo - Game Boy.lpl"},
                new {path=@"roms\Absent.zip#Missing.gb",label="Absent archive",core_path="DETECT",db_name="Nintendo - Game Boy.lpl"}}}));
            var archiveSource=EmulatorImport.Sources("retroarch",retro,retroCfg).Single(source => source.Path==archivePlaylist);
            var archived=EmulatorImport.Read("retroarch",retro,new[]{archiveSource},null,token);
            Check(archived.Games.Count==3 && archived.Warnings.Count==1 && archived.Games[0].Path==archive+"#folder/Yoshi.gb" && archived.Games[0].CorePath==otherCore && archived.Games[0].ArtworkTitle=="Yoshi","Archive playlist imports ZIP/7z members with relative archive paths, labels, cores and artwork metadata");
            Check(EmulatorImport.RetroArchSourceCoreStatus(archived,archiveSource,new EmulatorGame[0])=="Core: mgba_libretro.dll","Source selection names the usable core found for a playlist");
            var archiveSettings=new Settings(); var archiveLibrary=new Library();
            string archiveList=EmulatorImport.Import(archiveSettings,archiveLibrary,"retroarch",retro,retroCfg,new[]{archiveSource},archived.Games,null,null,"Archived games");
            EmulatorImport.Import(archiveSettings,archiveLibrary,"retroarch",retro,retroCfg,new[]{archiveSource},archived.Games,null,archiveList,"Archived games");
            Check(EmulatorLibrary.Config(archiveSettings,"retroarch").Games.Count==3,"Repeat archive import does not duplicate members and separate ROMs inside one archive stay distinct");
            EmulatorLibrary.Apply(archiveLibrary,archiveSettings);
            var archivedGame=archiveLibrary.Games.Single(entry => entry.Name=="Yoshi");
            Check(EmulatorLibrary.StartInfo(archivedGame,archiveSettings).Arguments.Contains("\""+archive+"#folder/Yoshi.gb\"") && !EmulatorLibrary.Missing(archivedGame,archiveSettings),"Archive member launches as one quoted RetroArch content argument and is not marked missing");
            Check(!EmulatorLibrary.ConfirmMissingRom(archive+"#folder/Yoshi.gb"),"Refresh keeps archived ROMs while their archive exists");
            File.Delete(archive);
            Check(EmulatorLibrary.ConfirmMissingRom(archive+"#folder/Yoshi.gb") && EmulatorLibrary.Missing(archivedGame,archiveSettings),"Removing the archive is detected by missing-game checks");
            var unresolved=EmulatorImport.Read("retroarch",retro,new[]{snes},null,token); File.Delete(core); unresolved=EmulatorImport.Read("retroarch",retro,new[]{snes},null,token);
            Check(unresolved.Games.Single().Problem!=null,"Missing RetroArch cores are flagged before review");
            var coreNotes=EmulatorImport.RetroArchCoreReviewNotes(unresolved,new[]{snes},new EmulatorGame[0]);
            Check(coreNotes.Count==1 && coreNotes[0].StartsWith("SNES: Playlist left out") && coreNotes[0].Contains("core"),"Core review names playlists left out because no installed core is available");
            Check(EmulatorImport.RetroArchSourceCoreStatus(unresolved,snes,new EmulatorGame[0])=="No usable core found — will not be imported","Source selection can show missing core status before reviewing games");
            Check(EmulatorImport.RetroArchCoreReviewNotes(unresolved,new[]{snes},new[]{new EmulatorGame {Path=unresolved.Games[0].Path,CorePath=otherCore}}).Count==0,"Already added games with a valid core are not falsely reported as left out for missing playlist cores");

            string rpcs=Write(root,"RPCS3\\rpcs3.exe"),rpcsRoot=Path.GetDirectoryName(rpcs),hdd=Path.Combine(root,"Custom PS3 HDD");
            string installed=Path.Combine(hdd,"game","NPUB10001"); Write(installed,"USRDIR\\EBOOT.BIN"); Sfo(Path.Combine(installed,"PARAM.SFO"),"Installed PS3 game","HG");
            string patch=Path.Combine(hdd,"game","BLUS10001"); Write(patch,"USRDIR\\EBOOT.BIN"); Sfo(Path.Combine(patch,"PARAM.SFO"),"Patch only","GD");
            string disc=Path.Combine(root,"Disc folder"); Write(disc,"PS3_GAME\\USRDIR\\EBOOT.BIN"); Sfo(Path.Combine(disc,"PS3_GAME","PARAM.SFO"),"Registered disc","DG");
            string ps3Iso=Write(root,"PS3 disc.iso");
            Write(rpcsRoot,"vfs.yml","$(EmulatorDir): "+Quote("")+"\n/dev_hdd0/: "+Quote(hdd));
            string registry=Write(rpcsRoot,"games.yml","NPUB10001: "+Quote(installed)+"\nBLUS20001: "+Quote(disc)+"\nBLUS20002: "+Quote(ps3Iso));
            var ps3Sources=EmulatorImport.Sources("rpcs3",rpcs,registry);
            Check(ps3Sources.Count==2 && ps3Sources.Any(s => s.Kind=="rpcs3-installed" && s.Path==Path.Combine(hdd,"game")),"RPCS3 resolves custom VFS HDD and registered game sources independently");
            var ps3=EmulatorImport.Read("rpcs3",rpcs,ps3Sources,null,token);
            Check(ps3.Games.Count==4 && ps3.Games.Count(g => g.Name=="Installed PS3 game")==2 && ps3.Games.All(g => g.Name!="Patch only"),"RPCS3 reads both sources, including ISO paths, and excludes update-only records: "+String.Join(" | ",ps3.Games.Select(g => g.Name).Concat(ps3.Warnings)));
            foreach(var source in ps3Sources) EmulatorImport.Import(settings,library,"rpcs3",rpcs,registry,new[]{source},ps3.Games.Where(g => g.SourceId==source.Id),null,null,"RPCS3 "+source.Name);
            Check(EmulatorLibrary.Config(settings,"rpcs3").Games.Count==3,"Overlapping registered and installed paths share one game entry");
            Check(File.ReadAllText(registry).Contains("NPUB10001"),"Import leaves emulator registry untouched");

            string vita=Write(root,"Vita3K\\Vita3K.exe"),vitaData=Path.Combine(root,"Vita data"),app=Path.Combine(vitaData,"ux0","app","PCSE00001");
            Write(app,"eboot.bin"); Sfo(Path.Combine(app,"sce_sys","param.sfo"),"Vita game","gd");
            string vitaPatch=Path.Combine(vitaData,"ux0","app","PCSE00002"); Write(vitaPatch,"eboot.bin"); Sfo(Path.Combine(vitaPatch,"sce_sys","param.sfo"),"Vita patch","gp");
            string vitaCfg=Write(Path.GetDirectoryName(vita),"config.yml","pref-path: "+Quote(vitaData));
            var vitaGames=EmulatorImport.Read("vita3k",vita,EmulatorImport.Sources("vita3k",vita,vitaCfg),null,token);
            Check(vitaGames.Games.Count==1 && vitaGames.Games[0].Name=="Vita game" && vitaGames.Games[0].Path==app,"Vita3K uses active data path and skips patch entries");

            string duck=Write(root,"DuckStation\\duckstation.exe"),duckRoot=Path.GetDirectoryName(duck),flat=Path.Combine(root,"Disc's games, part 1"),deep=Path.Combine(root,"Nested PS games");
            string flatGame=Write(flat,"Flat.chd"),deepGame=Write(deep,"Nested\\Deep.chd"); Write(flat,"Nested\\Not scanned.chd"); string excludedPath=Write(deep,"Nested\\Hidden.chd");
            string duckIni=Write(duckRoot,"settings.ini","[GameList]\nPaths = "+flat+"\nPaths = "+Path.Combine(root,"Does not exist")+"\nRecursivePaths = "+deep+"\nExcludedPaths = "+excludedPath);
            var duckSources=EmulatorImport.Sources("duckstation",duck,duckIni); var duckGames=EmulatorImport.Read("duckstation",duck,duckSources,null,token);
            Check(duckSources.Count==2 && duckGames.Games.Select(g => g.Path).OrderBy(x => x).SequenceEqual(new[]{flatGame,deepGame}.OrderBy(x => x)),"Repeated INI paths, punctuation, recursion and exclusions are respected");
            string pcs=Write(root,"PCSX2\\pcsx2-qt.exe"),ps2=Path.Combine(root,"PS2 games"); Write(ps2,"Game.iso");
            string pcsIni=Write(Path.GetDirectoryName(pcs),"inis\\PCSX2.ini","[GameList]\nPaths = "+ps2);
            Check(EmulatorImport.Read("pcsx2",pcs,EmulatorImport.Sources("pcsx2",pcs,pcsIni),null,token).Games.Count==1,"PCSX2 imports configured ROM directories");
            string dolphin=Write(root,"Dolphin\\Dolphin.exe"),gc=Path.Combine(root,"GC games"); Write(gc,"Nested\\Game.rvz");
            string dolphinIni=Write(Path.GetDirectoryName(dolphin),"User\\Config\\Dolphin.ini","[General]\nISOPaths = 1\nISOPath0 = "+gc+"\nRecursiveISOPaths = True");
            Check(EmulatorImport.Read("dolphin",dolphin,EmulatorImport.Sources("dolphin",dolphin,dolphinIni),null,token).Games.Count==1,"Dolphin imports configured recursive scan directories");
            string eden=Write(root,"Eden\\eden.exe"),switchRoot=Path.Combine(root,"Switch games"); Write(switchRoot,"Nested\\Game.xci");
            string edenIni=Write(Path.GetDirectoryName(eden),"config\\qt-config.ini","[UI]\nPaths\\gamedirs\\1\\path="+switchRoot+"\nPaths\\gamedirs\\1\\deep_scan=true\nPaths\\gamedirs\\2\\path=UserNAND\nPaths\\gamedirs\\size=2");
            var edenSources=EmulatorImport.Sources("eden",eden,edenIni);
            Check(edenSources.Count==1 && EmulatorImport.Read("eden",eden,edenSources,null,token).Games.Count==1,"Eden reads Qt arrays and excludes logical NAND sources");
            string psp=Write(root,"PPSSPP\\PPSSPPWindows64.exe"),pspRoot=Path.GetDirectoryName(psp),pspGames=Path.Combine(root,"PSP games"),pspIso=Write(pspGames,"Game.iso");
            string pspPbp=Write(pspRoot,"memstick\\PSP\\GAME\\Game2\\EBOOT.PBP");
            string pspSfo=Path.Combine(root,"pbp-param.sfo"); Sfo(pspSfo,"PSP game","MG");
            byte[] pspMetadata=File.ReadAllBytes(pspSfo);
            using(var writer=new BinaryWriter(File.Create(pspPbp))) {
                writer.Write(0x50425000u); writer.Write(0x10000u);
                writer.Write(40u);
                for(int offset=0;offset<7;offset++) writer.Write((uint)(40+pspMetadata.Length));
                writer.Write(pspMetadata);
            }
            string pspCfg=Write(pspRoot,"memstick\\PSP\\SYSTEM\\ppsspp.ini","[PinnedPaths]\nPath0="+pspGames+"\n[Recent]\nFileName0="+pspIso+"\nFileName1=https://example.invalid/game.iso");
            var pspSources=EmulatorImport.Sources("ppsspp",psp,pspCfg);
            Check(pspSources.Count==3 && EmulatorImport.Read("ppsspp",psp,pspSources,null,token).Games.Count==3,"PPSSPP reads pinned/recent paths and installed PSP/GAME without fetching remote URLs");
            string melon=Write(root,"melonDS\\melonDS.exe"),ds=Write(root,"DS games\\Game.nds");
            string melonCfg=Write(Path.GetDirectoryName(melon),"melonDS.toml","LastROMFolder = "+Quote(Path.GetDirectoryName(ds))+"\nRecentROM = ["+Quote(ds)+"]");
            Check(EmulatorImport.Read("melonds",melon,EmulatorImport.Sources("melonds",melon,melonCfg),null,token).Games.Count==2,"melonDS exposes recent entries and last-folder hints");
            string mgba=Write(root,"mGBA\\mGBA.exe"),gb=Write(root,"GB games\\Game.gba"),db=Path.Combine(Path.GetDirectoryName(mgba),"library.sqlite3"); MgbaFixture(db,Path.GetDirectoryName(gb),Path.GetFileName(gb));
            var dbBefore=File.ReadAllBytes(db); var mgbaGames=EmulatorImport.Read("mgba",mgba,EmulatorImport.Sources("mgba",mgba,db),null,token);
            Check(mgbaGames.Games.Single().Name=="Pokémon custom" && dbBefore.SequenceEqual(File.ReadAllBytes(db)),"mGBA reads SQLite title/path in UTF-8 without modifying database");
            string xenia=Write(root,"Xenia\\xenia.exe"),xeniaRoot=Path.GetDirectoryName(xenia),content=Path.Combine(xeniaRoot,"content"); Directory.CreateDirectory(content);
            byte[] stfs=new byte[0x348]; Array.Copy(Encoding.ASCII.GetBytes("LIVE"),stfs,4); stfs[0x346]=0x70; File.WriteAllBytes(Path.Combine(content,"game"),stfs); stfs[0x346]=0; stfs[0x347]=1; File.WriteAllBytes(Path.Combine(content,"save"),stfs);
            string xeniaCfg=Write(xeniaRoot,"xenia.config.toml","[Storage]\ncontent_root = \"content\"");
            Check(EmulatorImport.Read("xenia",xenia,EmulatorImport.Sources("xenia",xenia,xeniaCfg),null,token).Games.Single().Name=="game","Xenia imports game containers and skips saved-game content");
            File.WriteAllText(xeniaCfg,"[Storage] # storage options\nstorage_root = \"\"# use default\ncontent_root = \"content\" # installed content\nquoted = 'folder #1; games' # comment\nescaped = \"folder\\\"#1\" # comment");
            var commented=EmulatorImport.SettingsFile(xeniaCfg);
            Check(EmulatorImport.Read("xenia",xenia,EmulatorImport.Sources("xenia",xenia,xeniaCfg),null,token).Games.Single().Name=="game","Xenia ignores inline comments after empty and quoted paths");
            Check(EmulatorImport.Scalar(commented["Storage/quoted"])=="folder #1; games" && EmulatorImport.Scalar(commented["Storage/escaped"])=="folder\"#1","Comment markers inside quoted paths and escaped quotes are preserved");
            using(var cancel=new CancellationTokenSource()) {cancel.Cancel(); bool stopped=false; try {EmulatorImport.Read("xenia",xenia,EmulatorImport.Sources("xenia",xenia,xeniaCfg),null,cancel.Token);} catch(OperationCanceledException) {stopped=true;} Check(stopped,"Discovery can be cancelled");}
            string xboxExe=Write(root,"xemu/xemu.exe"),xboxRom=Write(root,"Xbox games/Halo.iso"),xboxCfg=Write(root,"xemu/xemu.toml","[general]\ngames_dir = "+Quote(Path.GetDirectoryName(xboxRom))+" # library\n[sys.files]\ndvd_path = "+Quote(xboxRom)+"\n");
            Check(EmulatorImport.DetectLocation("xemu",xboxExe)==Path.GetFullPath(xboxCfg),"xemu detects its portable configuration");
            var xboxSources=EmulatorImport.Sources("xemu",xboxExe,xboxCfg);
            var xboxBatch=EmulatorImport.Read("xemu",xboxExe,xboxSources,null,CancellationToken.None);
            Check(xboxSources.Any(source => source.Kind=="folder") && xboxSources.Any(source => source.Kind=="xemu-disc") && xboxBatch.Games.Select(xboxGame => xboxGame.Path).Distinct(StringComparer.OrdinalIgnoreCase).Count()==1,"xemu imports configured games and last disc with the same identity: "+String.Join("; ",xboxSources.Select(source => source.Kind+"="+source.Path))+" games="+xboxBatch.Games.Count+" notes="+String.Join("; ",xboxBatch.Warnings));
            var xboxSettings=new Settings(); var xboxLibrary=new Library();
            string xboxList=EmulatorImport.Import(xboxSettings,xboxLibrary,"xemu",xboxExe,xboxCfg,xboxSources,xboxBatch.Games,null,null,"Xbox games");
            EmulatorImport.Import(xboxSettings,xboxLibrary,"xemu",xboxExe,xboxCfg,xboxSources,xboxBatch.Games,null,xboxList,"Xbox games");
            Check(EmulatorLibrary.Config(xboxSettings,"xemu").Games.Count==1,"Repeated xemu import does not duplicate the same game across sources");
            Check(EmulatorLibrary.DefaultArguments("xemu")=="-full-screen -dvd_path {game}","xemu uses documented fullscreen disc arguments");
            Check(LibretroArtwork.Systems(new Game {Source="emulator",EmulatorId="xemu"},new Settings()).Single()=="Microsoft - Xbox","xemu artwork uses the original Xbox system");
            Console.WriteLine(checks+" emulator library import checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
