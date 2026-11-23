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
        public string ArtworkSystem {get;set;}
        public string ArtworkTitle {get;set;}
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
            new EmulatorPreset {Id="xemu",Name="xemu",System="Xbox",Extensions=".iso;.xiso",Hint="Choose an Xbox XISO disc image. Configure the required system files in xemu first."},
            new EmulatorPreset {Id="xenia",Name="Xenia",System="Xbox 360",Extensions=".iso;.xex;.zar;.live;.con;.pirs;",Hint="Choose an ISO, extracted game's default.xex, or an extensionless Xbox 360 content file. Xenia Canary executables are supported."},
            new EmulatorPreset {Id="dolphin",Name="Dolphin",System="GameCube / Wii",Extensions=".iso;.gcm;.rvz;.wbfs;.wia;.ciso;.gcz;.dol;.elf;.wad;.json",Hint="Choose a GameCube/Wii image, homebrew executable, or Dolphin mod preset. Display preferences follow Dolphin's settings."},
            new EmulatorPreset {Id="cemu",Name="Cemu",System="Wii U",Extensions=".wua;.wud;.wux;.rpx",Hint="Choose a disc image or the RPX in an extracted game's code folder."},
            new EmulatorPreset {Id="eden",Name="Eden",System="Nintendo Switch",Extensions=".xci;.nsp;.nro;.nso;.nca",Hint="Choose a base game file. Updates and DLC are managed inside Eden."},
            new EmulatorPreset {Id="mgba",Name="mGBA",System="Game Boy / Color / Advance",Extensions=".gba;.gb;.gbc;.sgb;.zip;.7z",Hint="Choose a Game Boy, Game Boy Color or Game Boy Advance ROM, or a supported archive."},
            new EmulatorPreset {Id="melonds",Name="melonDS",System="Nintendo DS / DSi",Extensions=".nds;.srl;.zip;.7z",Hint="Choose a DS ROM or a supported archive containing a DS ROM."},
            new EmulatorPreset {Id="azahar",Name="Azahar",System="Nintendo 3DS",Extensions=".cci;.3ds;.cxi;.3dsx;.elf;.axf;.zcci;.zcxi;.z3dsx;.bcci;.bcxi;.app",Hint="Choose a compatible decrypted 3DS game. Install CIA packages in Azahar first, then import its library. Updates and DLC are not separate games."},
            new EmulatorPreset {Id="duckstation",Name="DuckStation",System="PlayStation",Extensions=".cue;.chd;.iso;.bin;.img;.pbp;.m3u;.ccd;.mds",Hint="For multi-track discs, choose the CUE file; for multi-disc games, choose an M3U playlist."},
            new EmulatorPreset {Id="pcsx2",Name="PCSX2",System="PlayStation 2",Extensions=".iso;.chd;.cso;.bin;.gz;.elf",Hint="Choose a PS2 disc image or ELF file."},
            new EmulatorPreset {Id="rpcs3",Name="RPCS3",System="PlayStation 3",Extensions=".bin;.elf;.self;.iso",Hint="Choose a game folder or PS3_GAME/USRDIR/EBOOT.BIN. Install PKG games in RPCS3 first, then select their installed game folder."},
            new EmulatorPreset {Id="shadps4",Name="shadPS4",System="PlayStation 4",Extensions=".bin",Hint="Choose the emulator executable and an installed game's eboot.bin. Install PKG files in shadPS4 first."},
            new EmulatorPreset {Id="ppsspp",Name="PPSSPP",System="PlayStation Portable",Extensions=".iso;.cso;.chd;.pbp;.elf;.prx",Hint="Choose a PSP disc image or EBOOT.PBP. For extracted games, select EBOOT.PBP."},
            new EmulatorPreset {Id="vita3k",Name="Vita3K",System="PlayStation Vita",Extensions=".bin",Hint="Select an installed title folder inside Vita3K's ux0/app folder, or its eboot.bin. Install games in Vita3K first."},
            new EmulatorPreset {Id="retroarch",Name="RetroArch",System="Multiple systems",Extensions=".nes;.fds;.sfc;.smc;.snes;.gb;.gbc;.gba;.sgb;.n64;.z64;.v64;.nds;.md;.gen;.smd;.32x;.sms;.gg;.pce;.sgx;.ngp;.ngc;.ws;.wsc;.a26;.a52;.a78;.lnx;.j64;.jag;.cue;.chd;.iso;.pbp;.m3u;.ccd;.cso;.zip;.7z;.bin;.rom;.col;.int;.vec;.vb;.uze;.bs;.st;.msa;.adf;.hdf;.d64;.d81;.prg;.tap;.crt;.dsk;.mx1;.mx2;.cas;.tzx;.z80;.sna;.nib;.rvz;.gcm;.wbfs;.wia;.ciso;.gcz;.dol;.elf;.wad;.xci;.nsp;.nro;.3ds;.3dsx;.cci;.cxi;.wua;.wud;.wux;.rpx",Hint="Select a core before adding games. Scan a folder for that core's system; core metadata filters compatible extensions when available. Existing games keep their assigned core."}
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
            Preset(id); string normalized=String.IsNullOrWhiteSpace(path) ? null : Executable(path);
            var config=Ensure(settings,id);
            if(!String.Equals(config.Path,normalized,StringComparison.OrdinalIgnoreCase)) {
                string location=normalized==null ? null : EmulatorImport.DetectLocation(id,normalized);
                config.Path=normalized;
                config.ImportPath=location;
            }
            if(id=="retroarch" && normalized!=null) {
                var resolver=new RetroArchCoreResolver(normalized);
                config.CorePath=resolver.Relocate(config.CorePath) ?? config.CorePath;
                foreach(var game in config.Games ?? new List<EmulatorGame>()) game.CorePath=resolver.Relocate(game.CorePath) ?? game.CorePath;
            }
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
            try {return new RetroArchCoreResolver(executable).Paths.OrderBy(p => p,StringComparer.CurrentCultureIgnoreCase).ToList();} catch {return new List<string>();}
        }
        static HashSet<string> RetroArchExtensions(string corePath,string executable) {
            var extensions=new HashSet<string>(RetroArchCoreResolver.CoreExtensions(corePath),StringComparer.OrdinalIgnoreCase);
            if(extensions.Count==0) {
                extensions.UnionWith(Preset("retroarch").Extensions.Split(';'));
                if(!String.IsNullOrWhiteSpace(executable)) {
                    try {extensions.UnionWith(new RetroArchCoreResolver(executable).InstalledExtensions);} catch {}
                }
            }
            extensions.Add(".zip"); extensions.Add(".7z"); extensions.Add(".m3u");
            return extensions;
        }
        static HashSet<string> ScanExtensions(string id,string corePath) {
            if(id!="retroarch") return new HashSet<string>(Preset(id).Extensions.Split(';'),StringComparer.OrdinalIgnoreCase);
            return RetroArchExtensions(corePath,null);
        }
        internal static bool SplitRetroArchArchive(string path,out string archive,out string member) {
            archive=null; member=null; if(String.IsNullOrWhiteSpace(path)) return false;
            string value=path.Trim().Trim('"');
            int split=-1;
            foreach(string extension in new[]{".zip#",".7z#"}) {
                int found=value.IndexOf(extension,StringComparison.OrdinalIgnoreCase);
                if(found>=0 && (split<0 || found+extension.Length-1<split)) split=found+extension.Length-1;
            }
            if(split<0) return false;
            archive=value.Substring(0,split); member=value.Substring(split+1).Replace('\\','/'); return true;
        }
        public static string GamePath(string id,string path,string corePath=null,string executable=null) {
            var preset=Preset(id);
            var allowed=id=="retroarch" ? RetroArchExtensions(corePath,executable) : null;
            if(String.IsNullOrWhiteSpace(path)) throw new ArgumentException("Choose a game file.");
            string archive,member;
            if(id=="retroarch" && SplitRetroArchArchive(path,out archive,out member)) {
                archive=System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(archive));
                if(!File.Exists(archive)) throw new FileNotFoundException("The game archive could not be found.",archive);
                if(String.IsNullOrWhiteSpace(member) || member.StartsWith("/") || member.Contains(":") || member.Contains("\"") || member.Any(Char.IsControl) || member.Split('/').Any(part => part=="..")) throw new ArgumentException("The ROM path inside the archive is invalid.");
                if(!allowed.Contains(System.IO.Path.GetExtension(member).ToLowerInvariant())) throw new ArgumentException("The archive entry is not a supported RetroArch game file.");
                return archive+"#"+member;
            }
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
            bool supported=id=="retroarch" ? allowed.Contains(extension) : preset.Extensions.Split(';').Contains(extension);
            if(!supported || ((id=="rpcs3" || id=="shadps4") && extension==".bin" && !System.IO.Path.GetFileName(path).Equals("EBOOT.BIN",StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Choose a supported "+preset.Name+" game file. "+preset.Hint);
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
        public static int AddGames(Settings settings,string id,IEnumerable<string> paths,IDictionary<string,string> names=null,string corePath=null) {
            var config=Config(settings,id); Executable(config==null ? null : config.Path);
            string core=id=="retroarch" ? Core(String.IsNullOrWhiteSpace(corePath) ? config.CorePath : corePath) : null;
            // Validate the whole selection before changing saved membership.
            var normalized=(paths ?? Enumerable.Empty<string>()).Select(path => GamePath(id,path,core,config.Path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
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
            string core=id=="retroarch" ? Core(corePath ?? game.CorePath ?? config.CorePath) : null;
            string normalized=GamePath(id,path,core,config.Path);
            if(config.Games.Any(g => g.Id!=gameId && String.Equals(g.Path,normalized,StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("That game file is already added to this emulator.");
            game.Name=name; game.Path=normalized; game.CorePath=core; if(arguments!=null) game.Arguments=arguments.Trim();
        }
        public static bool ConfirmMissingRom(string path) {
            try {
                if(String.IsNullOrWhiteSpace(path)) return false;
                string archive,member;
                if(!File.Exists(path) && SplitRetroArchArchive(path,out archive,out member)) path=archive;
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
            foreach(var list in settings.RetroArchPlaylistLists ?? new List<CustomList>()) if(list.Games!=null) list.Games.RemoveAll(value => value==gameId);
            foreach(var list in settings.CombinedCollections ?? new List<CombinedCollection>()) {
                if(list.AddedGames!=null) list.AddedGames.RemoveAll(idValue => idValue==gameId);
                if(list.ExcludedGames!=null) list.ExcludedGames.RemoveAll(idValue => idValue==gameId);
            }
            foreach(var import in settings.EmulatorImports ?? new List<EmulatorImportDefinition>())
                if(import.ImportedGames!=null) import.ImportedGames.Remove(gameId);
            foreach(var additions in (settings.ListGameAdditions ?? new Dictionary<string,List<string>>()).Values) if(additions!=null) additions.RemoveAll(value => value==gameId);
            if(settings.FavouriteGameIds!=null) settings.FavouriteGameIds.RemoveAll(value => value==gameId);
            if(settings.HiddenGameIds!=null) settings.HiddenGameIds.RemoveAll(value => value==gameId);
            if(settings.ArtworkOverrides!=null) settings.ArtworkOverrides.Remove(gameId);
            if(settings.LaunchHistory!=null) settings.LaunchHistory.Remove(gameId);
            if(settings.LaunchCounts!=null) settings.LaunchCounts.Remove(gameId);
            if(settings.SelectedGamesByList!=null) foreach(string listId in settings.SelectedGamesByList.Where(pair => pair.Value==gameId).Select(pair => pair.Key).ToArray()) settings.SelectedGamesByList.Remove(listId);
            if(settings.SelectedId==gameId) settings.SelectedId=null;
        }
        // Lists are ordered by maker, then TV/handheld, then console release year.
        sealed class ConsoleInfo {
            public string Maker;
            public bool Handheld;
            public int Year;
        }
        static readonly string[][] ConsoleMetadataRows={
            new[]{"3DO","TV","1993","3do","the3docompany3do"},
            new[]{"Amstrad","TV","1984","cpc","amstradcpc","cpc464","cpc6128"},
            new[]{"Arcade","TV","1971","arcade","arcadegames","fbneo","mame"},
            new[]{"Atari","TV","1977","atari2600","2600","vcs","atarivideocomputersystem"},
            new[]{"Atari","TV","1979","atari8bit","atari800","atarixegs","xegs"},
            new[]{"Atari","TV","1982","atari5200","5200"},
            new[]{"Atari","TV","1985","atarist"},
            new[]{"Atari","TV","1986","atari7800","7800"},
            new[]{"Atari","Handheld","1989","atarilynx","lynx"},
            new[]{"Atari","TV","1993","atarijaguar","jaguar"},
            new[]{"Atari","TV","1995","atarijaguarcd","jaguarcd"},
            new[]{"Bandai","Handheld","1999","wonderswan","bandaiwonderswan"},
            new[]{"Bandai","Handheld","2000","wonderswancolor","wsc","bandaiwonderswancolor"},
            new[]{"Casio","TV","1983","pv1000","casiopv1000"},
            new[]{"Coleco","TV","1982","colecovision","coleco"},
            new[]{"Commodore","TV","1982","commodore64","c64"},
            new[]{"Commodore","TV","1985","amiga","commodoreamiga"},
            new[]{"Emerson","TV","1982","arcadia2001","emersonarcadia2001"},
            new[]{"Entex","Handheld","1982","adventurevision","entexadventurevision"},
            new[]{"Epoch","TV","1984","supercassettevision","epochsupercassettevision"},
            new[]{"Fairchild","TV","1976","channelf","fairchildchannelf"},
            new[]{"Funtech","TV","1995","superacan","funtechsuperacan"},
            new[]{"GamePark","Handheld","2001","gp32","gameparkgp32"},
            new[]{"GCE","TV","1982","vectrex","gcevectrex"},
            new[]{"Hartung","Handheld","1990","gamemaster","hartunggamemaster"},
            new[]{"LeapFrog","Handheld","2003","leapster","leapsterlearninggamesystem"},
            new[]{"Magnavox","TV","1978","odyssey2","magnavoxodyssey2","videopac"},
            new[]{"Mattel","TV","1979","intellivision","mattelintellivision"},
            new[]{"Microsoft","TV","1983","msx","microsoftmsx"},
            new[]{"Microsoft","TV","1985","msx2","microsoftmsx2"},
            new[]{"Microsoft","TV","2001","xbox","microsoftxbox"},
            new[]{"Microsoft","TV","2005","xbox360","microsoftxbox360"},
            new[]{"Microsoft","TV","2013","xboxone","microsoftxboxone"},
            new[]{"Microsoft","TV","2020","xboxseries","xboxseriesx","xboxseriess","microsoftxboxseries"},
            new[]{"NEC","TV","1979","pc8001","necpc8001"},
            new[]{"NEC","TV","1981","pc88","necpc88"},
            new[]{"NEC","TV","1982","pc98","necpc98"},
            new[]{"NEC","TV","1987","pcengine","turbografx16","turbografx","pce","necpcengine"},
            new[]{"NEC","TV","1988","pcenginecd","turbografxcd","pcenginecdturbografxcd"},
            new[]{"NEC","TV","1989","supergrafx","pcenginesupergrafx"},
            new[]{"NEC","TV","1994","pcfx","necpcfx"},
            new[]{"Nintendo","Handheld","1980","gameandwatch","gamewatch","nintendogameandwatch"},
            new[]{"Nintendo","TV","1983","nes","famicom","nintendoentertainmentsystem","familycomputer","nintendones"},
            new[]{"Nintendo","TV","1986","fds","famicomdisksystem","familycomputerdisksystem","nintendofamicomdisksystem"},
            new[]{"Nintendo","Handheld","1989","gameboy","gb","nintendogameboy"},
            new[]{"Nintendo","TV","1990","snes","supernintendo","superfamicom","supernintendoentertainmentsystem","supernes"},
            new[]{"Nintendo","Handheld","1995","virtualboy","nintendovirtualboy"},
            new[]{"Nintendo","TV","1995","satellaview","nintendosatellaview"},
            new[]{"Nintendo","TV","1996","nintendo64","n64"},
            new[]{"Nintendo","TV","1996","sufamiturbo","nintendosufamiturbo"},
            new[]{"Nintendo","Handheld","1998","gameboycolor","gbc","nintendogameboycolor"},
            new[]{"Nintendo","TV","1999","nintendo64dd","n64dd"},
            new[]{"Nintendo","Handheld","2001","gameboyadvance","gba","nintendogameboyadvance"},
            new[]{"Nintendo","TV","2001","gamecube","gcn","nintendogamecube"},
            new[]{"Nintendo","Handheld","2001","pokemonmini","nintendopokemonmini"},
            new[]{"Nintendo","Handheld","2004","nintendods","nds","ds"},
            new[]{"Nintendo","TV","2006","wii","nintendowii"},
            new[]{"Nintendo","Handheld","2008","nintendodsi","dsi"},
            new[]{"Nintendo","Handheld","2011","nintendo3ds","3ds","nintendonintendo3ds","2ds"},
            new[]{"Nintendo","TV","2012","wiiu","nintendowiiu"},
            new[]{"Nintendo","TV","2017","switch","nintendoswitch"},
            new[]{"Philips","TV","1983","videopacplus","philipsvideopac"},
            new[]{"Philips","TV","1991","cdi","philipscdi"},
            new[]{"RCA","TV","1977","studioii","rcastudioii"},
            new[]{"Sega","TV","1983","sg1000","segasg1000"},
            new[]{"Sega","TV","1985","mastersystem","segamastersystem","markiii","segamegamastersystem"},
            new[]{"Sega","TV","1988","megadrive","genesis","segagenesis","segamegadrive","megadrivegenesis"},
            new[]{"Sega","Handheld","1990","gamegear","segagamegear"},
            new[]{"Sega","TV","1991","megacd","segacd","segamegacd","megacdsegacd"},
            new[]{"Sega","TV","1993","pico","segapico"},
            new[]{"Sega","TV","1994","32x","sega32x"},
            new[]{"Sega","TV","1994","saturn","segasaturn"},
            new[]{"Sega","TV","1998","dreamcast","segadreamcast"},
            new[]{"Sega","TV","1998","naomi","seganaomi"},
            new[]{"Sega","TV","2001","naomi2","seganaomi2"},
            new[]{"Sharp","TV","1982","x1","sharpx1"},
            new[]{"Sharp","TV","1987","x68000","sharpx68000"},
            new[]{"Sinclair","TV","1981","zx81","sinclairzx81"},
            new[]{"Sinclair","TV","1982","zxspectrum","spectrum","sinclairzxspectrum"},
            new[]{"SNK","TV","1990","neogeo","neogeoaes","neogeomvs","snkneogeo"},
            new[]{"SNK","TV","1994","neogeocd","snkneogeocd"},
            new[]{"SNK","Handheld","1998","neogeopocket","ngp","snkneogeopocket"},
            new[]{"SNK","Handheld","1999","neogeopocketcolor","ngpc","snkneogeopocketcolor"},
            new[]{"Sony","TV","1994","playstation","ps1","psx","sonyplaystation"},
            new[]{"Sony","TV","2000","playstation2","ps2","sonyplaystation2"},
            new[]{"Sony","Handheld","2004","playstationportable","psp","sonyplaystationportable"},
            new[]{"Sony","TV","2006","playstation3","ps3","sonyplaystation3"},
            new[]{"Sony","Handheld","2011","playstationvita","psvita","vita","sonyplaystationvita"},
            new[]{"Sony","TV","2013","playstation4","ps4","sonyplaystation4"},
            new[]{"Sony","TV","2020","playstation5","ps5","sonyplaystation5"},
            new[]{"Spectravideo","TV","1983","svi318","svi328","svi318svi328","spectravideo"},
            new[]{"Tiger","Handheld","1997","gamecom","tigergamecom"},
            new[]{"VTech","TV","1981","creativision","vtechcreativision"},
            new[]{"VTech","TV","2004","vsmile","vtechvsmile"},
            new[]{"Watara","Handheld","1992","supervision","watarasupervision"},
            new[]{"PC","TV","1981","pc","dos","windows","scummvm","doom","quake","rpgmaker","tic80","lowresnx","vircon32","dinothawr","mrboom","flashback","rickdangerous","tombraider","wolfenstein3d"},
            new[]{"Other","Handheld","1976","handheldelectronicgame","electronicgame"}
        };
        static readonly Dictionary<string,ConsoleInfo> ConsoleMetadata=BuildConsoleMetadata();
        static Dictionary<string,ConsoleInfo> BuildConsoleMetadata() {
            var result=new Dictionary<string,ConsoleInfo>(StringComparer.OrdinalIgnoreCase);
            foreach(var row in ConsoleMetadataRows) {
                int year; if(row.Length<4 || !Int32.TryParse(row[2],out year)) continue;
                var info=new ConsoleInfo {Maker=row[0],Handheld=String.Equals(row[1],"Handheld",StringComparison.OrdinalIgnoreCase),Year=year};
                for(int index=3;index<row.Length;index++) if(!String.IsNullOrWhiteSpace(row[index]) && !result.ContainsKey(row[index])) result[row[index]]=info;
            }
            return result;
        }
        static string ConsoleKey(string value) {
            string normalized=(value ?? "").Normalize(System.Text.NormalizationForm.FormD);
            return new string(normalized.Where(character => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(character)!=System.Globalization.UnicodeCategory.NonSpacingMark && Char.IsLetterOrDigit(character)).ToArray()).ToLowerInvariant();
        }
        static bool LooksHandheld(string value) {
            string key=ConsoleKey(value);
            return new[]{"handheld","gameboy","gbc","gba","lynx","gamegear","neogeopocket","wonderswan","playstationportable","psp","playstationvita","psvita","nintendods","nds","nintendodsi","dsi","nintendo3ds","3ds","2ds","pokemonmini","virtualboy","supervision","gamecom","gp32","leapster","adventurevision","gameandwatch"}.Any(part => key.Contains(part));
        }
        static bool TryConsoleInfo(string value,out ConsoleInfo info) {
            info=null;
            string name=(value ?? "").Trim();
            foreach(string suffix in new[]{" (automatic list)"," · RetroArch"," - RetroArch"}) if(name.EndsWith(suffix,StringComparison.OrdinalIgnoreCase)) name=name.Substring(0,name.Length-suffix.Length).Trim();
            name=System.Text.RegularExpressions.Regex.Replace(name,@"\s*\(\d+\)$","").Trim();
            if(name.Length==0) return false;
            string maker=null,console=name;
            int separator=name.IndexOf(" - ",StringComparison.Ordinal);
            if(separator>0) {maker=name.Substring(0,separator).Trim(); console=name.Substring(separator+3).Trim();}
            int dot=console.IndexOf(" · ",StringComparison.Ordinal); if(dot>0) console=console.Substring(0,dot).Trim();
            foreach(string candidate in new[]{name,console}) {
                string key=ConsoleKey(candidate);
                if(ConsoleMetadata.TryGetValue(key,out info)) return true;
            }
            string normalized=ConsoleKey(console);
            string match=ConsoleMetadata.Keys.OrderByDescending(key => key.Length).FirstOrDefault(key => key.Length>=3 && normalized.Contains(key));
            if(match!=null) {info=ConsoleMetadata[match]; return true;}
            if(!String.IsNullOrWhiteSpace(maker)) {
                info=new ConsoleInfo {Maker=maker,Handheld=LooksHandheld(console),Year=9999};
                return true;
            }
            return false;
        }
        static int MakerRank(string maker) {
            switch((maker ?? "").Trim().ToLowerInvariant()) {
                case "sony": return 0;
                case "microsoft": return 1;
                case "nintendo": return 2;
                case "sega": return 3;
                default: return 4;
            }
        }
        static string ConsoleSortKey(ConsoleInfo info) {
            if(info==null) return "~|2|9999";
            int rank=MakerRank(info.Maker);
            string maker=rank==4 ? info.Maker.ToLowerInvariant() : "";
            return rank.ToString("D2")+"|"+maker+"|"+(info.Handheld ? "1" : "0")+"|"+info.Year.ToString("D4");
        }
        static string ConsoleSortKey(string value) {
            ConsoleInfo info; return TryConsoleInfo(value,out info) ? ConsoleSortKey(info) : "~|2|9999";
        }
        static string ListConsoleKey(Collection list,Dictionary<string,string> systems) {
            const string prefix="source:emulator:";
            string system;
            if((list.Id ?? "").StartsWith(prefix,StringComparison.Ordinal) && PresetConsole.TryGetValue(list.Id.Substring(prefix.Length),out system)) return ConsoleSortKey(system);
            ConsoleInfo named;
            if(TryConsoleInfo(list.Name,out named)) return ConsoleSortKey(named);
            ConsoleInfo best=null;
            foreach(string value in (list.Games ?? new List<string>()).Where(systems.ContainsKey).Select(id => systems[id]).Distinct(StringComparer.OrdinalIgnoreCase)) {
                ConsoleInfo info; if(!TryConsoleInfo(value,out info)) continue;
                if(best==null || String.Compare(ConsoleSortKey(info),ConsoleSortKey(best),StringComparison.OrdinalIgnoreCase)<0) best=info;
            }
            return ConsoleSortKey(best);
        }
        static readonly Dictionary<string,string> PresetConsole=new Dictionary<string,string> {
            {"xemu","Microsoft - Xbox"},{"xenia","Microsoft - Xbox 360"},
            {"mgba","Nintendo - Game Boy"},{"dolphin","Nintendo - GameCube"},{"melonds","Nintendo - Nintendo DS"},
            {"azahar","Nintendo - Nintendo 3DS"},{"cemu","Nintendo - Wii U"},{"eden","Nintendo - Switch"},
            {"duckstation","Sony - PlayStation"},{"pcsx2","Sony - PlayStation 2"},{"ppsspp","Sony - PlayStation Portable"},
            {"rpcs3","Sony - PlayStation 3"},{"vita3k","Sony - PlayStation Vita"},{"shadps4","Sony - PlayStation 4"}
        };
        internal static List<Collection> DefaultListOrder(List<Collection> options,Library library,Settings settings) {
            var emulatorLists=options.Where(list => list.Source=="emulator").ToList();
            if(emulatorLists.Count==0) return options;
            var systems=library.Games.Where(game => !String.IsNullOrWhiteSpace(game.ArtworkSystem))
                .GroupBy(game => game.Id).ToDictionary(group => group.Key,group => group.First().ArtworkSystem);
            var customIds=new HashSet<string>((settings.CustomLists ?? new List<CustomList>()).Select(list => list.Id)
                .Concat((settings.CombinedCollections ?? new List<CombinedCollection>()).Select(list => list.Id)));
            customIds.Add("source:custom");
            var ordered=emulatorLists.OrderBy(list => ListConsoleKey(list,systems),StringComparer.OrdinalIgnoreCase)
                .ThenBy(list => list.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
            var before=options.TakeWhile(list => list.Source!="emulator").Where(list => !customIds.Contains(list.Id));
            var custom=options.Where(list => customIds.Contains(list.Id));
            var after=options.SkipWhile(list => list.Source!="emulator").Where(list => list.Source!="emulator" && !customIds.Contains(list.Id));
            return before.Concat(custom).Concat(ordered).Concat(after).ToList();
        }
        internal static string DefaultAutomaticListName(EmulatorPreset preset) {
            string console=preset.System;
            switch(preset.Id) {
                case "dolphin": console="Wii / GameCube"; break;
                case "melonds": console="DS / DSi"; break;
                case "eden": console="Switch"; break;
                case "duckstation": console="PS1"; break;
                case "pcsx2": console="PS2"; break;
                case "rpcs3": console="PS3"; break;
                case "shadps4": console="PS4"; break;
                case "ppsspp": console="PSP"; break;
                case "vita3k": console="PS Vita"; break;
            }
            return console+" · "+preset.Name;
        }
        public static bool IsAutomaticList(Settings settings,string id) {
            return (id ?? "").StartsWith("source:emulator:",StringComparison.Ordinal) || (settings.RetroArchPlaylistLists ?? new List<CustomList>()).Any(list => list.Id==id);
        }
        public static void Apply(Library library,Settings settings) {
            library.Games.RemoveAll(g => g.Source=="emulator"); library.Collections.RemoveAll(c => c.Source=="emulator" || (c.Id ?? "").StartsWith("source:emulator:",StringComparison.Ordinal));
            var existingIds=new HashSet<string>(library.Games.Select(g => g.Id));
            foreach(var preset in Presets) {
                var config=Config(settings,preset.Id); if(config==null) continue;
                foreach(var entry in config.Games ?? new List<EmulatorGame>()) {
                    if(entry==null || String.IsNullOrEmpty(entry.Id) || !existingIds.Add(entry.Id)) continue;
                    library.Games.Add(new Game {Id=entry.Id,Name=entry.Name,Source="emulator",EmulatorId=preset.Id,LaunchId=entry.Path,Arguments=entry.Arguments,ArtworkSystem=entry.ArtworkSystem,ArtworkTitle=entry.ArtworkTitle});
                }
                library.Collections.Add(new Collection {Id="source:emulator:"+preset.Id,Source="emulator",Name=String.IsNullOrWhiteSpace(config.ListName) ? DefaultAutomaticListName(preset) : config.ListName,Games=library.Games.Where(g => g.Source=="emulator" && g.EmulatorId==preset.Id).Select(g => g.Id).ToList()});
            }
            var registered=new HashSet<string>(library.Games.Where(game => game.Source=="emulator" && game.EmulatorId=="retroarch").Select(game => game.Id));
            foreach(var playlist in settings.RetroArchPlaylistLists ?? new List<CustomList>()) {
                library.Collections.RemoveAll(list => list.Id==playlist.Id);
                library.Collections.Add(new Collection {Id=playlist.Id,Name=playlist.Name,Source="emulator",Games=(playlist.Games ?? new List<string>()).Where(registered.Contains).Distinct().ToList()});
            }
        }
        public static bool Missing(Game game,Settings settings) {
            return MissingReason(game,settings)!=null;
        }
        public static string MissingReason(Game game,Settings settings) {
            if(game.Source!="emulator") return null;
            var preset=Presets.FirstOrDefault(item => item.Id==game.EmulatorId); string name=preset==null ? "Emulator" : preset.Name;
            var config=Config(settings,game.EmulatorId);
            if(config==null || String.IsNullOrWhiteSpace(config.Path)) return name+" not configured";
            try {Executable(config.Path);} catch {return name+" executable missing or invalid";}
            var saved=game.EmulatorId=="retroarch" ? (config.Games ?? new List<EmulatorGame>()).FirstOrDefault(item => item.Id==game.Id) : null;
            string core=saved==null ? config.CorePath : saved.CorePath ?? config.CorePath;
            try {GamePath(game.EmulatorId,game.LaunchId,core,config.Path);}
            catch(FileNotFoundException) {return "Game file missing";}
            catch(DirectoryNotFoundException) {return "Game folder missing";}
            catch {return "Game file missing or unsupported";}
            if(game.EmulatorId=="retroarch") {
                if(String.IsNullOrWhiteSpace(core)) return "RetroArch core not selected";
                try {GameCore(config,game.Id);} catch {return "RetroArch core missing or invalid";}
            }
            return null;
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
                try {result.Paths.Add(GamePath(id,file,corePath));} catch(ArgumentException) {} catch(IOException) {} catch(UnauthorizedAccessException) {}
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
                case "xemu": return "-full-screen -dvd_path {game}";
                case "dolphin": return "-b -e {game}";
                case "eden": case "cemu": return "-f -g {game}";
                case "azahar": case "melonds": case "mgba": return "-f {game}";
                case "ppsspp": return "--fullscreen --pause-menu-exit {game}";
                case "retroarch": return "-f -L {core} {game}";
                case "vita3k": return "--fullscreen -r {title}";
                default: return "{game}";
            }
        }
        public static bool IsShadQtLauncher(string executable) {
            try {return String.Equals(System.IO.Path.GetFileName((executable ?? "").Trim().Trim('"')),"shadPS4QtLauncher.exe",StringComparison.OrdinalIgnoreCase);} catch(ArgumentException) {return false;}
        }
        public static string DefaultArguments(string id,string executable) {
            return id=="shadps4" && IsShadQtLauncher(executable) ? "-d -g {game}" : DefaultArguments(id);
        }
        public static ProcessStartInfo StartInfo(Game game,Settings settings) {
            if(settings==null) throw new InvalidOperationException("Emulator settings are unavailable.");
            var config=Config(settings,game.EmulatorId);
            string core=game.EmulatorId=="retroarch" && config!=null ? GameCore(config,game.Id) : null;
            string executable=Executable(config==null ? null : config.Path),path=GamePath(game.EmulatorId,game.LaunchId,core,config==null ? null : config.Path),quoted="\""+path+"\"",arguments;
            arguments=config!=null && config.Arguments!=null ? config.Arguments : DefaultArguments(game.EmulatorId,executable);
            arguments=arguments.Replace("{game}",quoted).Replace("{core}",core==null ? "" : "\""+core+"\"").Replace("{title}","\""+new DirectoryInfo(path).Name+"\"");
            if(!String.IsNullOrWhiteSpace(game.Arguments)) arguments=game.Arguments.Contains("{game}") ? game.Arguments.Replace("{game}",quoted).Replace("{core}",core==null ? "" : "\""+core+"\"") : arguments+" "+game.Arguments;
            return new ProcessStartInfo(executable,arguments) {UseShellExecute=true,WorkingDirectory=System.IO.Path.GetDirectoryName(executable)};
        }
    }
}
