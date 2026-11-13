using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text;
using System.Text.RegularExpressions;

namespace PadLauncher {
    public class EmulatorGame {
        public string Arguments {get;set;}
        public string Id {get;set;}
        public string Name {get;set;}
        public string CorePath {get;set;}
        public string Path {get;set;}
    }
    public class EmulatorScan {
        public List<string> Paths=new List<string>();
        public int UnreadableFolders;
        public List<string> ReadErrors=new List<string>();
    }
    public class OrphanedEmulatorGame {
        public string EmulatorId,Id,Name,Path,Reason;
    }
    public class EmulatorConfig {
        public string ListName {get;set;}
        public string Arguments {get;set;}
        public string Id {get;set;}
        public string Path {get;set;}
        public List<EmulatorGame> Games {get;set;}
        public string RomFolder {get;set;}
        public string ImportPath {get;set;}
        public string CorePath {get;set;}
    }
    public class EmulatorPreset {
        public string Id {get;set;}
        public string Name {get;set;}
        public string System {get;set;}
        public string Extensions {get;set;}
        public string Hint {get;set;}
        public string Filter {get {return Name+" games|"+String.Join(";",Extensions.Split(';').Where(e => e.Length>0).Select(e => "*"+e))+"|All files|*.*";}}
    }
    public static class EmulatorLibrary {
        static EmulatorLibrary() {
            var context=typeof(object).Assembly.GetType("System.AppContext");
            var setSwitch=context==null ? null : context.GetMethod("SetSwitch",new[]{typeof(string),typeof(bool)});
            if(setSwitch!=null) {
                setSwitch.Invoke(null,new object[]{"Switch.System.IO.UseLegacyPathHandling",false});
                setSwitch.Invoke(null,new object[]{"Switch.System.IO.BlockLongPaths",false});
            }
        }
        public static readonly EmulatorPreset[] Presets={
            new EmulatorPreset {Id="duckstation",Name="DuckStation",System="PlayStation",Extensions=".cue;.chd;.iso;.bin;.img;.pbp;.m3u;.ccd;.mds",Hint="For multi-track discs, choose the CUE file; for multi-disc games, choose an M3U playlist."},
            new EmulatorPreset {Id="xenia",Name="Xenia",System="Xbox 360",Extensions=".iso;.xex;.zar;.live;.con;.pirs;",Hint="Choose an ISO, extracted game's default.xex, or an extensionless Xbox 360 content file. Xenia Canary executables are supported."},
            new EmulatorPreset {Id="rpcs3",Name="RPCS3",System="PlayStation 3",Extensions=".bin;.elf;.self;.iso",Hint="Choose a game folder or PS3_GAME/USRDIR/EBOOT.BIN. Install PKG games in RPCS3 first, then select their installed game folder."},
            new EmulatorPreset {Id="pcsx2",Name="PCSX2",System="PlayStation 2",Extensions=".iso;.chd;.cso;.bin;.gz;.elf",Hint="Choose a PS2 disc image or ELF file."},
            new EmulatorPreset {Id="dolphin",Name="Dolphin",System="GameCube / Wii",Extensions=".iso;.gcm;.rvz;.wbfs;.wia;.ciso;.gcz;.dol;.elf;.wad;.json",Hint="Choose a GameCube/Wii image, homebrew executable, or Dolphin mod preset. Display preferences follow Dolphin's settings."},
            new EmulatorPreset {Id="eden",Name="Eden",System="Nintendo Switch",Extensions=".xci;.nsp;.nro;.nso;.nca",Hint="Choose a base game file. Updates and DLC are managed inside Eden."},
            new EmulatorPreset {Id="azahar",Name="Azahar",System="Nintendo 3DS",Extensions=".cci;.3ds;.cxi;.3dsx;.elf;.axf;.zcci;.zcxi;.z3dsx;.bcci;.bcxi;.app",Hint="Choose a compatible decrypted 3DS game. Install CIA packages in Azahar first, then import its library. Updates and DLC are not separate games."},
            new EmulatorPreset {Id="melonds",Name="melonDS",System="Nintendo DS / DSi",Extensions=".nds;.srl;.zip;.7z",Hint="Choose a DS ROM or a supported archive containing a DS ROM."},
            new EmulatorPreset {Id="vita3k",Name="Vita3K",System="PlayStation Vita",Extensions=".bin",Hint="Select an installed title folder inside Vita3K's ux0/app folder, or its eboot.bin. Install games in Vita3K first."},
            new EmulatorPreset {Id="retroarch",Name="RetroArch",System="Multiple systems",Extensions=".nes;.fds;.sfc;.smc;.snes;.gb;.gbc;.gba;.sgb;.n64;.z64;.v64;.nds;.md;.gen;.smd;.32x;.sms;.gg;.pce;.sgx;.ngp;.ngc;.ws;.wsc;.a26;.a52;.a78;.lnx;.j64;.jag;.cue;.chd;.iso;.pbp;.m3u;.ccd;.cso;.zip;.7z;.bin;.rom;.col;.int;.vec;.vb;.uze;.bs;.st;.msa;.adf;.hdf;.d64;.d81;.prg;.tap;.crt;.dsk;.mx1;.mx2;.cas;.tzx;.z80;.sna;.nib",Hint="Select a core before adding games. Scan a folder for that core's system; core metadata filters compatible extensions when available. Existing games keep their assigned core."},
            new EmulatorPreset {Id="ppsspp",Name="PPSSPP",System="PlayStation Portable",Extensions=".iso;.cso;.chd;.pbp;.elf;.prx",Hint="Choose a PSP disc image or EBOOT.PBP. For extracted games, select EBOOT.PBP."},
            new EmulatorPreset {Id="mgba",Name="mGBA",System="Game Boy / Color / Advance",Extensions=".gba;.gb;.gbc;.sgb;.zip;.7z",Hint="Choose a Game Boy, Game Boy Color or Game Boy Advance ROM, or a supported archive."},
            new EmulatorPreset {Id="cemu",Name="Cemu",System="Wii U",Extensions=".wua;.wud;.wux;.rpx",Hint="Choose a disc image or the RPX in an extracted game's code folder."},
            new EmulatorPreset {Id="shadps4",Name="shadPS4",System="PlayStation 4",Extensions=".bin",Hint="Choose the emulator executable and an installed game's eboot.bin. Install PKG files in shadPS4 first."}
        };
        public static EmulatorPreset Preset(string id) {
            var result=Presets.FirstOrDefault(p => p.Id==id);
            if(result==null) throw new ArgumentException("Unknown emulator."); return result;
        }
        public static EmulatorConfig Config(Settings settings,string id) {
            Preset(id); return (settings.Emulators ?? new List<EmulatorConfig>()).FirstOrDefault(c => c.Id==id);
        }
        static EmulatorConfig Ensure(Settings settings,string id) {
            var config=Config(settings,id); if(config!=null) return config;
            if(settings.Emulators==null) settings.Emulators=new List<EmulatorConfig>();
            config=new EmulatorConfig {Id=id,Games=new List<EmulatorGame>()}; settings.Emulators.Add(config); return config;
        }
        public static string Executable(string path) {
            if(String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose the emulator's executable first.");
            path=System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
            if(!File.Exists(path) || !System.IO.Path.GetExtension(path).Equals(".exe",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an existing emulator .exe file."); return path;
        }
        public static void SavePath(Settings settings,string id,string path) {
            Preset(id); string normalized=String.IsNullOrWhiteSpace(path) ? null : Executable(path); Ensure(settings,id).Path=normalized;
        }
        public static string Core(string path) {
            if(String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose a RetroArch core before adding or launching games.");
            path=System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
            if(!File.Exists(path) || !System.IO.Path.GetExtension(path).Equals(".dll",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose an installed RetroArch core .dll file."); return path;
        }
        public static void SaveCore(Settings settings,string path) {Ensure(settings,"retroarch").CorePath=String.IsNullOrWhiteSpace(path) ? null : Core(path);}
        static string GameCore(EmulatorConfig config,string id) {
            var game=(config.Games ?? new List<EmulatorGame>()).FirstOrDefault(g => g.Id==id);
            return Core(game==null ? config.CorePath : game.CorePath ?? config.CorePath);
        }
        public static List<string> Cores(string executable) {
            try {string folder=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Executable(executable)),"cores"); return Directory.Exists(folder) ? Directory.GetFiles(folder,"*_libretro.dll").OrderBy(p => p,StringComparer.CurrentCultureIgnoreCase).ToList() : new List<string>();} catch {return new List<string>();}
        }
        static HashSet<string> ScanExtensions(string id,string corePath) {
            var all=new HashSet<string>(Preset(id).Extensions.Split(';'),StringComparer.OrdinalIgnoreCase);
            if(id!="retroarch" || String.IsNullOrWhiteSpace(corePath)) return all;
            try {
                corePath=Core(corePath); string folder=System.IO.Path.GetDirectoryName(corePath),name=System.IO.Path.GetFileNameWithoutExtension(corePath)+".info";
                var info=new[]{System.IO.Path.Combine(folder,name),System.IO.Path.Combine(folder,"..","info",name)}.FirstOrDefault(File.Exists);
                if(info!=null && new FileInfo(info).Length<1024*1024) {
                    var match=Regex.Match(File.ReadAllText(info),@"(?m)^\s*supported_extensions\s*=\s*""([^""]+)""");
                    if(match.Success) {
                        var extensions=new HashSet<string>(match.Groups[1].Value.Split('|').Select(e => "."+e.Trim().TrimStart('.')),StringComparer.OrdinalIgnoreCase);
                        extensions.Add(".zip"); extensions.Add(".7z"); extensions.Add(".m3u"); extensions.IntersectWith(all); if(extensions.Count>3) return extensions;
                        if(extensions.Any(e => e!=".zip" && e!=".7z" && e!=".m3u")) return extensions;
                    }
                }
            } catch {} return all;
        }
        public static string GamePath(string id,string path) {
            var preset=Preset(id);
            if(String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose a game file.");
            path=System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(path.Trim().Trim('"')));
            if(id=="vita3k") {
                if(File.Exists(path) && System.IO.Path.GetFileName(path).Equals("eboot.bin",StringComparison.OrdinalIgnoreCase)) path=System.IO.Path.GetDirectoryName(path);
                var parent=Directory.GetParent(path);
                if(!Directory.Exists(path) || !File.Exists(System.IO.Path.Combine(path,"eboot.bin")) || !File.Exists(System.IO.Path.Combine(path,"sce_sys","param.sfo")) || parent==null || !parent.Name.Equals("app",StringComparison.OrdinalIgnoreCase) || parent.Parent==null || !parent.Parent.Name.Equals("ux0",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException(preset.Hint);
                return path.TrimEnd(System.IO.Path.DirectorySeparatorChar);
            }
            if(id=="rpcs3" && Directory.Exists(path)) {
                string folder=path;
                path=new[]{System.IO.Path.Combine(folder,"PS3_GAME","USRDIR","EBOOT.BIN"),System.IO.Path.Combine(folder,"USRDIR","EBOOT.BIN"),System.IO.Path.Combine(folder,"EBOOT.BIN")}.FirstOrDefault(File.Exists);
                if(path==null) throw new ArgumentException("This folder has no PS3_GAME/USRDIR/EBOOT.BIN or USRDIR/EBOOT.BIN.");
            }
            if(id=="shadps4" && Directory.Exists(path)) path=Path.Combine(path,"eboot.bin");
            if(!File.Exists(path)) throw new FileNotFoundException("The game file could not be found.",path);
            string extension=System.IO.Path.GetExtension(path).ToLowerInvariant();
            if(id=="azahar" && extension==".app" && !IsAzaharApplication(path)) throw new ArgumentException("Choose an installed executable 3DS base game, not content data, an update or DLC.");
            if(id=="dolphin" && extension==".json") ValidateDolphinPreset(path);
            if(!preset.Extensions.Split(';').Contains(extension) || ((id=="rpcs3" || id=="shadps4") && extension==".bin" && !System.IO.Path.GetFileName(path).Equals("EBOOT.BIN",StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Choose a supported "+preset.Name+" game file. "+preset.Hint);
            return path;
        }
        static void ValidateDolphinPreset(string path) {
            try {
                if(new FileInfo(path).Length>1024*1024) throw new FormatException();
                var preset=Storage.Json().Deserialize<Dictionary<string,object>>(File.ReadAllText(path));
                object type,version,baseFile;
                if(preset!=null && preset.TryGetValue("type",out type) && (type as string)=="dolphin-game-mod-descriptor" &&
                    preset.TryGetValue("version",out version) && (version is int || version is decimal || version is double) && Convert.ToDouble(version)==1 &&
                    preset.TryGetValue("base-file",out baseFile) && !String.IsNullOrWhiteSpace(baseFile as string)) {
                    string disc=System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path),(string)baseFile);
                    if(!File.Exists(disc)) throw new FileNotFoundException("The Dolphin preset's base game file is missing.",disc);
                    return;
                }
            } catch(ArgumentException) {} catch(InvalidOperationException) {} catch(FormatException) {}
            throw new ArgumentException("Choose a Dolphin game mod preset saved from Dolphin.");
        }
        public static string SuggestedName(string id,string path) {
            if(id=="azahar") {string embedded=AzaharTitle(path); if(!String.IsNullOrEmpty(embedded)) return embedded;}
            string sfo=null;
            if(id=="vita3k") sfo=System.IO.Path.Combine(path,"sce_sys","param.sfo");
            if(id=="shadps4") sfo=Path.Combine(Path.GetDirectoryName(path),"sce_sys","param.sfo");
            if(id=="rpcs3") {var parent=Directory.GetParent(path); if(parent!=null && parent.Parent!=null) sfo=System.IO.Path.Combine(parent.Parent.FullName,"PARAM.SFO");}
            string title=SfoTitle(sfo); if(!String.IsNullOrWhiteSpace(title)) return title;
            if(id=="vita3k") return CleanFilenameTitle(new DirectoryInfo(path).Name);
            if(id=="shadps4") return CleanFilenameTitle(Directory.GetParent(path).Name);
            if(id=="cemu" && Path.GetExtension(path).Equals(".rpx",StringComparison.OrdinalIgnoreCase)) {
                var code=Directory.GetParent(path);
                if(code!=null && code.Name.Equals("code",StringComparison.OrdinalIgnoreCase) && code.Parent!=null) return CleanFilenameTitle(code.Parent.Name);
            }
            if(id=="rpcs3" && System.IO.Path.GetFileName(path).Equals("EBOOT.BIN",StringComparison.OrdinalIgnoreCase)) {
                var folder=Directory.GetParent(path);
                if(folder!=null && folder.Name.Equals("USRDIR",StringComparison.OrdinalIgnoreCase)) folder=folder.Parent;
                if(folder!=null && folder.Name.Equals("PS3_GAME",StringComparison.OrdinalIgnoreCase)) folder=folder.Parent;
                if(folder!=null) return CleanFilenameTitle(folder.Name);
            }
            if((id=="xenia" && Path.GetFileName(path).Equals("default.xex",StringComparison.OrdinalIgnoreCase)) ||
                (id=="ppsspp" && Path.GetFileName(path).Equals("EBOOT.PBP",StringComparison.OrdinalIgnoreCase))) {
                var folder=Directory.GetParent(path);
                if(folder!=null) return CleanFilenameTitle(folder.Name);
            }
            return CleanFilenameTitle(Path.GetFileNameWithoutExtension(path));
        }
        private static readonly Regex FilenameTitleSuffix=new Regex(
            @"[\s._-]*(?:[\[(]\s*(?:01[0-9a-f]{14}|(?:[A-Z]{4}[-_ ]?\d{5})|v\d+(?:\.\d+)*|rev(?:ision)?[ ._-]*\w+|\d+(?:\.\d+)?\s*(?:GB|MB|KB)|USA|US|Europe|EUR|Japan|JPN|World|Asia|Korea|China|Australia|Brazil|(?:En|Ja|Fr|De|Es|It|Nl|Pt|Ru|Ko|Zh(?:-Hans|-Hant)?)(?:\s*,\s*(?:En|Ja|Fr|De|Es|It|Nl|Pt|Ru|Ko|Zh(?:-Hans|-Hant)?))*|NSP|XCI|Base Game|nsw2u(?:\.com)?)\s*[\])]|(?:__|_)(?:01[0-9a-f]{14}|v\d+(?:\.\d+)*|switch-xci\.com))[\s_]*$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        private static string CleanFilenameTitle(string name) {
            if(String.IsNullOrWhiteSpace(name)) return name;
            string title=name;
            // Keep disc identifiers so separately launchable discs stay distinguishable.
            title=Regex.Replace(title,@"(\((?:Disc|Disk|CD)\s+\d+[^)]*\))$", " $1",RegexOptions.IgnoreCase);
            var disc=Regex.Match(title,@"\s*(\((?:Disc|Disk|CD)\s+\d+[^)]*\))$",RegexOptions.IgnoreCase);
            if(disc.Success) title=title.Substring(0,disc.Index);
            while(true) {
                string shorter=FilenameTitleSuffix.Replace(title,"");
                if(shorter==title) break;
                title=shorter;
            }
            title=Regex.Replace(title,@"_+"," ").Trim();
            if(String.IsNullOrWhiteSpace(title)) return name;
            return disc.Success ? title+" "+disc.Groups[1].Value : title;
        }
        public static int AddGames(Settings settings,string id,IEnumerable<string> paths,IDictionary<string,string> names=null) {
            var config=Config(settings,id); Executable(config==null ? null : config.Path);
            string core=id=="retroarch" ? Core(config.CorePath) : null;
            // Validate the whole selection before changing saved membership.
            var normalized=(paths ?? Enumerable.Empty<string>()).Select(path => GamePath(id,path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if(config.Games==null) config.Games=new List<EmulatorGame>();
            int added=0;
            foreach(string path in normalized) {
                if(config.Games.Any(g => String.Equals(g.Path,path,StringComparison.OrdinalIgnoreCase))) continue;
                string name;
                if(names==null || !names.TryGetValue(path,out name) || String.IsNullOrWhiteSpace(name)) name=SuggestedName(id,path);
                config.Games.Add(new EmulatorGame {Id="emulator:"+id+":"+Guid.NewGuid().ToString("N"),Name=name.Trim(),Path=path,CorePath=core}); added++;
            }
            return added;
        }
        public static void SaveGame(Settings settings,string id,string gameId,string name,string path,string corePath=null,string arguments=null) {
            var config=Config(settings,id); var game=config==null ? null : (config.Games ?? new List<EmulatorGame>()).FirstOrDefault(g => g.Id==gameId);
            if(game==null) throw new ArgumentException("This emulator game no longer exists.");
            name=(name ?? "").Trim(); if(name.Length==0 || name.Length>120) throw new ArgumentException("Enter a game name of 1–120 characters.");
            string normalized=GamePath(id,path);
            if(config.Games.Any(g => g.Id!=gameId && String.Equals(g.Path,normalized,StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("That game file is already added to this emulator.");
            string core=id=="retroarch" ? Core(corePath ?? game.CorePath ?? config.CorePath) : null;
            game.Name=name; game.Path=normalized; game.CorePath=core; if(arguments!=null) game.Arguments=arguments.Trim();
        }
        public static bool ConfirmMissingRom(string path) {
            try {
                if(String.IsNullOrWhiteSpace(path)) return false;
                path=Path.GetFullPath(path);
                // Most games still exist. Avoid enumerating their parent folders on
                // every refresh; the slower check below handles missing paths safely.
                if(File.Exists(path) || Directory.Exists(path)) return false;
                string candidate=path;
                while(true) {
                    string parent=Path.GetDirectoryName(candidate);
                    if(String.IsNullOrEmpty(parent) || parent==candidate) return false;
                    try {
                        // A readable ancestor can confirm that a whole game folder was deleted.
                        // Existing but unreadable folders and disconnected drives remain protected.
                        return !Directory.GetFileSystemEntries(parent).Any(entry => String.Equals(entry,candidate,StringComparison.OrdinalIgnoreCase));
                    } catch(DirectoryNotFoundException) {candidate=parent;}
                }
            } catch(IOException) {} catch(UnauthorizedAccessException) {} catch(System.Security.SecurityException) {} catch(ArgumentException) {}
            return false;
        }
        public static List<OrphanedEmulatorGame> FindOrphanedGames(Settings settings,bool includeAbsent,CancellationToken token,List<string> warnings) {
            var result=new List<OrphanedEmulatorGame>();
            foreach(var preset in Presets) {
                token.ThrowIfCancellationRequested();
                var config=Config(settings,preset.Id);
                if(config==null || config.Games==null) continue;
                var absentById=new Dictionary<string,bool>();
                if(includeAbsent) foreach(var import in (settings.EmulatorImports ?? new List<EmulatorImportDefinition>()).Where(item => item.EmulatorId==preset.Id)) {
                    token.ThrowIfCancellationRequested();
                    var imported=new HashSet<string>(import.ImportedGames ?? new List<string>());
                    if(imported.Count==0) continue;
                    HashSet<string> detected=null;
                    try {
                        string location=import.Location;
                        if(String.IsNullOrWhiteSpace(location)) throw new IOException("No emulator library location was saved for an import.");
                        var sources=EmulatorImport.Sources(preset.Id,config.Path,location);
                        var savedSources=new HashSet<string>(import.Sources ?? new List<string>(),StringComparer.OrdinalIgnoreCase);
                        if(savedSources.Count>0) {
                            sources=sources.Where(source => savedSources.Contains(source.Id)).ToList();
                            if(sources.Count!=savedSources.Count) throw new IOException("One or more imported library sources are unavailable.");
                        }
                        if(sources.Count==0) throw new IOException("No library sources were found.");
                        var batch=EmulatorImport.Read(preset.Id,config.Path,sources,config.CorePath,token);
                        if(batch.Warnings.Count>0 || batch.Games.Any(game => !String.IsNullOrEmpty(game.Problem)))
                            throw new IOException("Library detection was incomplete.");
                        detected=new HashSet<string>(batch.Games.Select(game => game.Path),StringComparer.OrdinalIgnoreCase);
                    } catch(OperationCanceledException) {throw;}
                    catch(Exception error) {warnings.Add(preset.Name+": "+error.Message+" Entries absent from this library were kept.");}
                    foreach(var game in config.Games.Where(item => imported.Contains(item.Id))) {
                        bool absent=detected!=null && !detected.Contains(game.Path), previous;
                        // A game imported from multiple libraries is safe to flag only
                        // when every originating library was read and no longer has it.
                        absentById[game.Id]=absentById.TryGetValue(game.Id,out previous) ? previous && absent : absent;
                    }
                }
                foreach(var game in config.Games) {
                    token.ThrowIfCancellationRequested();
                    string reason=null;
                    if(ConfirmMissingRom(game.Path)) reason="ROM file is missing";
                    bool absent;
                    if(reason==null && absentById.TryGetValue(game.Id,out absent) && absent) {
                        try {
                            string parent=System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(game.Path));
                            Directory.GetFileSystemEntries(parent);
                            File.GetAttributes(game.Path);
                            reason="No longer detected in the emulator library";
                        } catch(IOException) {} catch(UnauthorizedAccessException) {} catch(System.Security.SecurityException) {} catch(ArgumentException) {}
                    }
                    if(reason!=null) result.Add(new OrphanedEmulatorGame {EmulatorId=preset.Id,Id=game.Id,Name=game.Name,Path=game.Path,Reason=reason});
                }
            }
            return result;
        }
        public static void RemoveGame(Settings settings,string id,string gameId) {
            var config=Config(settings,id);
            if(config==null || config.Games==null || config.Games.RemoveAll(g => g.Id==gameId)==0) return;
            foreach(var list in settings.CustomLists ?? new List<CustomList>())
                if(list.Games!=null) list.Games.Remove(gameId);
            foreach(var list in settings.CombinedCollections ?? new List<CombinedCollection>()) {
                if(list.AddedGames!=null) list.AddedGames.RemoveAll(idValue => idValue==gameId);
                if(list.ExcludedGames!=null) list.ExcludedGames.RemoveAll(idValue => idValue==gameId);
            }
            foreach(var import in settings.EmulatorImports ?? new List<EmulatorImportDefinition>())
                if(import.ImportedGames!=null) import.ImportedGames.Remove(gameId);
        }
        public static void Apply(Library library,Settings settings) {
            library.Games.RemoveAll(g => g.Source=="emulator"); library.Collections.RemoveAll(c => (c.Id ?? "").StartsWith("source:emulator:",StringComparison.Ordinal));
            var existingIds=new HashSet<string>(library.Games.Select(g => g.Id));
            foreach(var preset in Presets) {
                var config=Config(settings,preset.Id); if(config==null) continue;
                foreach(var entry in config.Games ?? new List<EmulatorGame>()) {
                    if(entry==null || String.IsNullOrEmpty(entry.Id) || !existingIds.Add(entry.Id)) continue;
                    library.Games.Add(new Game {Id=entry.Id,Name=entry.Name,Source="emulator",EmulatorId=preset.Id,LaunchId=entry.Path,Arguments=entry.Arguments});
                }
                library.Collections.Add(new Collection {Id="source:emulator:"+preset.Id,Source="emulator",Name=String.IsNullOrWhiteSpace(config.ListName) ? preset.Name : config.ListName,Games=library.Games.Where(g => g.Source=="emulator" && g.EmulatorId==preset.Id).Select(g => g.Id).ToList()});
            }
        }
        public static bool Missing(Game game,Settings settings) {
            if(game.Source!="emulator") return false;
            try {GamePath(game.EmulatorId,game.LaunchId); var config=Config(settings,game.EmulatorId); Executable(config==null ? null : config.Path); if(game.EmulatorId=="retroarch") GameCore(config,game.Id); return false;} catch {return true;}
        }
        public static string SfoTitle(string path) {return SfoValue(path,"TITLE");}
        public static string SfoValue(string path,string field) {
            try {
                if(path==null || !File.Exists(path) || new FileInfo(path).Length>1024*1024) return null;
                return SfoDataValue(File.ReadAllBytes(path),field);
            } catch {} return null;
        }
        static string SfoDataValue(byte[] data,string field) {
            try {
                if(data.Length<20 || BitConverter.ToUInt32(data,0)!=0x46535000) return null;
                uint keys=BitConverter.ToUInt32(data,8),values=BitConverter.ToUInt32(data,12),count=BitConverter.ToUInt32(data,16);
                if(count>(data.Length-20)/16) return null;
                for(int index=0;index<count;index++) {
                    int row=20+index*16; long key=(long)keys+BitConverter.ToUInt16(data,row),value=(long)values+BitConverter.ToUInt32(data,row+12); uint length=BitConverter.ToUInt32(data,row+4);
                    if(key<0 || key+field.Length+1>data.Length || value<0 || value+length>data.Length || length>4096) continue;
                    if(Encoding.ASCII.GetString(data,(int)key,field.Length+1)!=field+"\0") continue;
                    return Encoding.UTF8.GetString(data,(int)value,(int)length).TrimEnd('\0').Replace("\r"," ").Replace("\n"," ").Trim();
                }
            } catch {} return null;
        }
        static string PbpTitle(string path) {
            try {
                using(var stream=File.OpenRead(path))
                using(var reader=new BinaryReader(stream)) {
                    if(stream.Length<40 || reader.ReadUInt32()!=0x50425000) return null;
                    reader.ReadUInt32();
                    uint start=reader.ReadUInt32(),end=reader.ReadUInt32();
                    if(start<40 || end<=start || end>stream.Length || end-start>1024*1024) return null;
                    stream.Position=start;
                    return SfoDataValue(reader.ReadBytes((int)(end-start)),"TITLE");
                }
            } catch(IOException) {} catch(UnauthorizedAccessException) {}
            return null;
        }
        static bool IsPspScanCandidate(string path,string extension) {
            if(extension==".elf" || extension==".prx") return false;
            if(extension!=".pbp") return true;
            if(!Path.GetFileName(path).Equals("EBOOT.PBP",StringComparison.OrdinalIgnoreCase)) return false;
            string folder=Path.GetFileName(Path.GetDirectoryName(path));
            if(folder.Contains("%")) return false;
            string title=PbpTitle(path);
            return !String.IsNullOrEmpty(title) && !Regex.IsMatch(title,@"^(?:DEVHOOK|Fastloader)(?:\b|\d)",RegexOptions.IgnoreCase);
        }
        public static string Folder(string folder) {
            if(String.IsNullOrWhiteSpace(folder)) throw new ArgumentException("Choose a ROM folder first.");
            folder=System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(folder.Trim().Trim('"')));
            if(!Directory.Exists(folder)) throw new DirectoryNotFoundException("The ROM folder could not be found."); return folder;
        }
        public static EmulatorScan Scan(string id,string folder,CancellationToken token,string corePath=null,bool recursive=true) {
            var preset=Preset(id); folder=Folder(folder); var result=new EmulatorScan();
            var extensions=ScanExtensions(id,corePath);
            var pending=new Stack<string>(); pending.Push(folder); var files=new List<string>();
            while(pending.Count>0) {
                token.ThrowIfCancellationRequested(); string current=pending.Pop();
                try {
                    if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) continue;
                    if(recursive) foreach(string child in Directory.GetDirectories(current)) pending.Push(child);
                    foreach(string file in Directory.GetFiles(current)) {
                        token.ThrowIfCancellationRequested();
                        string extension=System.IO.Path.GetExtension(file).ToLowerInvariant();
                        if(!extensions.Contains(extension)) continue;
                        if(id=="ppsspp" && !IsPspScanCandidate(file,extension)) continue;
                        if((id=="rpcs3" || id=="vita3k" || id=="shadps4") && !System.IO.Path.GetFileName(file).Equals("eboot.bin",StringComparison.OrdinalIgnoreCase)) continue;
                        if(id=="eden" && !IsEdenScanCandidate(file,extension)) continue;
                        if(id=="azahar" && extension==".app" && !IsAzaharApplication(file)) continue;
                        if((File.GetAttributes(file)&FileAttributes.ReparsePoint)==0) files.Add(file);
                    }
                } catch(UnauthorizedAccessException error) {result.UnreadableFolders++; result.ReadErrors.Add(current+" — "+error.Message);}
                catch(IOException error) {result.UnreadableFolders++; result.ReadErrors.Add(current+" — "+error.Message);}
            }
            var excluded=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if(id=="duckstation" || id=="retroarch") foreach(string file in files.Where(f => new[]{".cue",".m3u"}.Contains(System.IO.Path.GetExtension(f).ToLowerInvariant()))) {
                token.ThrowIfCancellationRequested();
                bool cue=file.EndsWith(".cue",StringComparison.OrdinalIgnoreCase);
                var references=new List<string>();
                try {
                    if(new FileInfo(file).Length<=1024*1024) {
                        foreach(string line in File.ReadLines(file)) {
                            string reference=null;
                            if(cue) {
                                var match=Regex.Match(line,@"^\s*FILE\s+(?:""([^""]+)""|(\S+))",RegexOptions.IgnoreCase);
                                if(match.Success) reference=match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                            } else if(!String.IsNullOrWhiteSpace(line) && !line.TrimStart().StartsWith("#")) {
                                reference=line.Trim().Trim('"');
                            }
                            if(!String.IsNullOrWhiteSpace(reference))
                                references.Add(System.IO.Path.GetFullPath(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(file),reference)));
                        }
                    }
                } catch(IOException) {references.Clear();} catch(UnauthorizedAccessException) {references.Clear();} catch(ArgumentException) {references.Clear();}
                if(cue && (references.Count==0 || references.Any(path => !File.Exists(path)))) excluded.Add(file);
                else excluded.UnionWith(references);
            }
            foreach(string file in files) {
                token.ThrowIfCancellationRequested(); if(excluded.Contains(file)) continue;
                string extension=System.IO.Path.GetExtension(file).ToLowerInvariant();
                if(!extensions.Contains(extension)) continue;
                if((id=="rpcs3" || id=="vita3k" || id=="shadps4") && !System.IO.Path.GetFileName(file).Equals("eboot.bin",StringComparison.OrdinalIgnoreCase)) continue;
                if(id=="xenia" && (extension=="" || extension==".live" || extension==".con" || extension==".pirs") && !IsXeniaGamePackage(file)) continue;
                try {result.Paths.Add(GamePath(id,file));} catch(ArgumentException) {} catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            result.Paths=result.Paths.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(p => p,StringComparer.CurrentCultureIgnoreCase).ToList(); return result;
        }
        public static bool IsAzaharApplication(string path) {
            try {
                using(var file=File.OpenRead(path)) using(var reader=new BinaryReader(file)) {
                    if(file.Length<0x200) return false;
                    file.Position=0x100; if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="NCCH") return false;
                    file.Position=0x118; ulong title=reader.ReadUInt64(); uint category=(uint)(title>>32);
                    if(category!=0x00040000 && category!=0x00040002) return false;
                    file.Position=0x18d; if((reader.ReadByte()&2)==0) return false;
                    file.Position=0x1a4; return reader.ReadUInt32()>0;
                }
            } catch(IOException) {return false;} catch(UnauthorizedAccessException) {return false;}
        }
        static string AzaharTitle(string path) {
            try {
                using(var file=File.OpenRead(path)) using(var reader=new BinaryReader(file)) {
                    if(file.Length<0x200) return null;
                    file.Position=0x100; if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="NCCH") return null;
                    file.Position=0x1a0; long exefs=(long)reader.ReadUInt32()*0x200;
                    if(exefs<0x200 || exefs+0x200>file.Length) return null;
                    for(int i=0;i<10;i++) {
                        file.Position=exefs+i*16; string name=Encoding.ASCII.GetString(reader.ReadBytes(8)).TrimEnd('\0'); uint offset=reader.ReadUInt32(),size=reader.ReadUInt32();
                        if(name!="icon" || size<0x408) continue;
                        long icon=exefs+0x200+offset; if(icon+0x408>file.Length) return null;
                        file.Position=icon; if(Encoding.ASCII.GetString(reader.ReadBytes(4))!="SMDH") return null;
                        file.Position=icon+8+0x200+0x80; string title=Encoding.Unicode.GetString(reader.ReadBytes(0x100)).Split('\0')[0].Trim();
                        if(title.Length==0) {file.Position=icon+8+0x200; title=Encoding.Unicode.GetString(reader.ReadBytes(0x80)).Split('\0')[0].Trim();}
                        return String.IsNullOrEmpty(title) ? null : title.Replace("\r"," ").Replace("\n"," ");
                    }
                }
            } catch(IOException) {} catch(UnauthorizedAccessException) {}
            return null;
        }
        private static bool IsEdenScanCandidate(string path,string extension) {
            if(extension==".nro") return true;
            if(extension!=".nsp" && extension!=".xci") return false;
            string name=Path.GetFileNameWithoutExtension(path);
            var ids=Regex.Matches(name,@"(?<![0-9a-f])01[0-9a-f]{14}(?![0-9a-f])",RegexOptions.IgnoreCase);
            if(ids.Count>0) return ids.Cast<Match>().Any(match => match.Value.EndsWith("000",StringComparison.Ordinal));
            if(Regex.IsMatch(name,@"[\[(]\s*base(?: game)?\s*[\])]",RegexOptions.IgnoreCase)) return true;
            return !Regex.IsMatch(name,@"[\[(][^\])]*\b(?:DLC|UPD|update)\b[^\])]*[\])]|[._](?:DLC|UPD|update)(?:[._]|$)",RegexOptions.IgnoreCase);
        }
        internal static bool IsXeniaGamePackage(string path) {
            try {
                using(var stream=File.OpenRead(path))
                using(var reader=new BinaryReader(stream)) {
                    if(stream.Length<0x348) return false;
                    string signature=Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if(signature!="CON " && signature!="LIVE" && signature!="PIRS") return false;
                    stream.Position=0x344;
                    uint type=((uint)reader.ReadByte()<<24)|((uint)reader.ReadByte()<<16)|((uint)reader.ReadByte()<<8)|reader.ReadByte();
                    return type==0x00007000 || type==0x000D0000;
                }
            } catch(IOException) {return false;} catch(UnauthorizedAccessException) {return false;}
        }
        public static string DefaultArguments(string id) {
            switch(id) {
                case "duckstation": case "pcsx2": return "-batch -fullscreen -- {game}";
                case "rpcs3": return "--no-gui --fullscreen {game}";
                case "dolphin": return "-b -e {game}";
                case "eden": case "cemu": return "-f -g {game}";
                case "azahar": case "melonds": case "mgba": return "-f {game}";
                case "ppsspp": return "--fullscreen --pause-menu-exit {game}";
                case "retroarch": return "-f -L {core} {game}";
                case "vita3k": return "--fullscreen -r {title}";
                default: return "{game}";
            }
        }
        public static ProcessStartInfo StartInfo(Game game,Settings settings) {
            if(settings==null) throw new InvalidOperationException("Emulator settings are unavailable.");
            var config=Config(settings,game.EmulatorId);
            string executable=Executable(config==null ? null : config.Path),path=GamePath(game.EmulatorId,game.LaunchId),quoted="\""+path+"\"",arguments;
            arguments=config!=null && config.Arguments!=null ? config.Arguments : DefaultArguments(game.EmulatorId);
            arguments=arguments.Replace("{game}",quoted).Replace("{core}",game.EmulatorId=="retroarch" ? "\""+GameCore(config,game.Id)+"\"" : "").Replace("{title}","\""+new DirectoryInfo(path).Name+"\"");
            if(!String.IsNullOrWhiteSpace(game.Arguments)) arguments=game.Arguments.Contains("{game}") ? game.Arguments.Replace("{game}",quoted).Replace("{core}",game.EmulatorId=="retroarch" ? "\""+GameCore(config,game.Id)+"\"" : "") : arguments+" "+game.Arguments;
            return new ProcessStartInfo(executable,arguments) {UseShellExecute=true,WorkingDirectory=System.IO.Path.GetDirectoryName(executable)};
        }
    }
}
