using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;

namespace PadLauncher {
    public class EmulatorImportSource {
        public string Id,Name,Kind,Path;
        public bool Recursive;
        public List<string> Exclusions=new List<string>();
    }
    public class EmulatorImportGame {
        public string Key,Name,Path,CorePath,SourceId,Problem;
    }
    public class EmulatorImportBatch {
        public List<EmulatorImportGame> Games=new List<EmulatorImportGame>();
        public List<string> Warnings=new List<string>();
    }
    public class EmulatorImportDefinition {
        public string Id {get;set;}
        public string EmulatorId {get;set;}
        public string Location {get;set;}
        public string SourceLabel {get;set;}
        public string ListId {get;set;}
        public List<string> Sources {get;set;}
        public List<string> ExcludedKeys {get;set;}
        public List<string> ImportedGames {get;set;}
    }
    public static class EmulatorImport {
        public static string[] ConfigNames(string id) {
            switch(id) {
                case "duckstation": return new[]{"settings.ini"};
                case "pcsx2": return new[]{"inis/PCSX2.ini","PCSX2.ini"};
                case "dolphin": return new[]{"User/Config/Dolphin.ini","Config/Dolphin.ini","Dolphin.ini"};
                case "eden": return new[]{"user/config/qt-config.ini","config/qt-config.ini","qt-config.ini"};
                case "retroarch": return new[]{"retroarch.cfg"};
                case "rpcs3": return new[]{"games.yml","vfs.yml"};
                case "vita3k": return new[]{"config.yml"};
                case "ppsspp": return new[]{"memstick/PSP/SYSTEM/ppsspp.ini","PSP/SYSTEM/ppsspp.ini","ppsspp.ini"};
                case "melonds": return new[]{"melonDS.toml","melonDS.ini"};
                case "mgba": return new[]{"library.sqlite3"};
                case "cemu": return new[]{"settings.xml"};
                default: return new[]{"xenia-canary.config.toml","xenia.config.toml"};
            }
        }
        public static string DetectLocation(string id,string executable) {
            string folder=System.IO.Path.GetDirectoryName(EmulatorLibrary.Executable(executable));
            var roots=new List<string> {folder};
            if(id=="rpcs3" || id=="vita3k") roots.Insert(0,System.IO.Path.Combine(folder,"portable"));
            if(id=="rpcs3") roots.Insert(0,System.IO.Path.Combine(folder,"config"));
            string docs=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),roaming=Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string[] names=id=="dolphin" ? new[]{"Dolphin Emulator"} : id=="pcsx2" ? new[]{"PCSX2"} : id=="ppsspp" ? new[]{"PPSSPP"} : id=="vita3k" ? new[]{"Vita3K/Vita3K","Vita3K"} : id=="xenia" ? new[]{"Xenia"} : new[]{EmulatorLibrary.Preset(id).Name,id};
            // An explicit saved override always takes precedence in the UI. Only select one installation here.
            foreach(string root in new[]{roaming,local,docs}) foreach(string name in names) roots.Add(System.IO.Path.Combine(root,name.Replace('/',System.IO.Path.DirectorySeparatorChar)));
            foreach(string root in roots.Distinct(StringComparer.OrdinalIgnoreCase)) foreach(string name in ConfigNames(id)) {string file=System.IO.Path.Combine(root,name.Replace('/',System.IO.Path.DirectorySeparatorChar)); if(File.Exists(file)) return file;}
            if(id=="retroarch") {string cfg=System.IO.Path.Combine(roaming,"retroarch.cfg"); if(File.Exists(cfg)) return cfg; if(Directory.Exists(System.IO.Path.Combine(folder,"playlists"))) return System.IO.Path.Combine(folder,"playlists");}
            if(id=="vita3k") foreach(string root in roots) if(Directory.Exists(System.IO.Path.Combine(root,"ux0","app"))) return root;
            if(id=="rpcs3" && Directory.Exists(System.IO.Path.Combine(folder,"dev_hdd0","game"))) return folder;
            return folder;
        }
        public static string Text(string path) {
            if(new FileInfo(path).Length>16*1024*1024) throw new IOException("Configuration is too large: "+System.IO.Path.GetFileName(path));
            return File.ReadAllText(path);
        }
        public static string Scalar(string value) {
            value=(value ?? "").Trim();
            if(value.Length>=2 && value[0]=='\'' && value[value.Length-1]=='\'') return value.Substring(1,value.Length-2).Replace("''","'");
            if(value.Length>=2 && value[0]=='"' && value[value.Length-1]=='"') {
                var body=value.Substring(1,value.Length-2); var result=new StringBuilder();
                for(int i=0;i<body.Length;i++) {
                    if(body[i]=='\\' && i+1<body.Length && (body[i+1]=='\\' || body[i+1]=='"')) result.Append(body[++i]); else result.Append(body[i]);
                }
                return result.ToString();
            }
            return value;
        }
        public static Dictionary<string,string> SettingsFile(string path) {
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase); string section="";
            foreach(string raw in Text(path).Split('\n')) {
                string line=WithoutComment(raw,System.IO.Path.GetExtension(path).Equals(".toml",StringComparison.OrdinalIgnoreCase)).Trim(); if(line.Length==0) continue;
                if(line.StartsWith("[") && line.EndsWith("]")) {section=line.Substring(1,line.Length-2); continue;}
                var match=Regex.Match(line,@"^([^=]+?)\s*=\s*(.*)$");
                if(match.Success) {
                    string key=(section.Length>0 ? section+"/" : "")+match.Groups[1].Value.Trim(),value=match.Groups[2].Value.Trim();
                    if(section.Equals("GameList",StringComparison.OrdinalIgnoreCase) && new[]{"Paths","RecursivePaths","ExcludedPaths"}.Contains(match.Groups[1].Value.Trim(),StringComparer.OrdinalIgnoreCase)) {
                        string old; result.TryGetValue(key,out old); value="\""+Scalar(value).Replace("\\","\\\\").Replace("\"","\\\"")+"\""; result[key]=String.IsNullOrEmpty(old) ? value : old+","+value;
                    } else result[key]=value;
                }
            }
            return result;
        }
        static string WithoutComment(string line,bool toml) {
            char quote='\0';
            for(int i=0;i<line.Length;i++) {
                char ch=line[i];
                if(quote=='"') {
                    if(ch=='\\' && i+1<line.Length && (line[i+1]=='\\' || line[i+1]=='"')) {i++; continue;}
                    if(ch=='"') quote='\0';
                } else if(quote=='\'') {
                    if(ch=='\'') quote='\0';
                } else {
                    if((ch=='#' && toml) || ((ch=='#' || ch==';') && (i==0 || Char.IsWhiteSpace(line[i-1])))) return line.Substring(0,i);
                    if((ch=='"' || ch=='\'') && (i==0 || Char.IsWhiteSpace(line[i-1]) || line[i-1]=='=' || line[i-1]==',' || line[i-1]=='[')) quote=ch;
                }
            }
            return line;
        }
        public static Dictionary<string,string> YamlScalars(string path) {
            var result=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            if(!File.Exists(path)) return result;
            foreach(string raw in Text(path).Split('\n')) {
                var match=Regex.Match(raw,@"^([^\s#][^:]*):\s*(.*?)\s*$");
                if(match.Success) result[Scalar(match.Groups[1].Value)]=Scalar(match.Groups[2].Value);
            }
            return result;
        }
        static string Get(Dictionary<string,string> values,string key,string fallback="") {string value; return values.TryGetValue(key,out value) ? Scalar(value) : fallback;}
        static bool Yes(string value) {return value.Equals("true",StringComparison.OrdinalIgnoreCase) || value=="1";}
        public static List<string> StringList(string value) {
            value=(value ?? "").Trim(); if(value=="@Invalid()" || value.Length==0) return new List<string>();
            if(value.StartsWith("[") && value.EndsWith("]")) value=value.Substring(1,value.Length-2);
            var parts=new List<string>(); var part=new StringBuilder(); char quote='\0'; bool escaped=false;
            foreach(char c in value) {
                if(escaped) {part.Append(c); escaped=false; continue;}
                if(c=='\\' && quote=='"') {part.Append(c); escaped=true; continue;}
                if((c=='"' || c=='\'') && ((quote=='\0' && part.ToString().Trim().Length==0) || quote==c)) {quote=quote==c ? '\0' : c; part.Append(c); continue;}
                if(c==',' && quote=='\0') {parts.Add(Scalar(part.ToString())); part.Clear();} else part.Append(c);
            }
            if(quote!='\0') throw new FormatException("Unterminated string list in emulator configuration.");
            parts.Add(Scalar(part.ToString())); return parts.Where(p => !String.IsNullOrWhiteSpace(p)).ToList();
        }
        public static string Resolve(string path,string root) {
            path=Scalar(path); if(String.IsNullOrWhiteSpace(path)) return null; path=Environment.ExpandEnvironmentVariables(path);
            if(path.StartsWith(":/") || path.StartsWith(":\\")) path=path.Substring(2);
            if(path.Contains("://") || path.StartsWith("content:",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Remote paths are not supported.");
            return System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(root,path));
        }
        static EmulatorImportSource Source(string name,string kind,string path,bool recursive) {return new EmulatorImportSource {Id=kind+"|"+path.ToUpperInvariant(),Name=name,Kind=kind,Path=path,Recursive=recursive};}
        public static List<EmulatorImportSource> Sources(string id,string executable,string location) {
            string exeRoot=System.IO.Path.GetDirectoryName(EmulatorLibrary.Executable(executable));
            location=System.IO.Path.GetFullPath((location ?? "").Trim().Trim('"'));
            if(!Directory.Exists(location) && !File.Exists(location)) throw new IOException("Choose an existing configuration file or emulator data folder.");
            string root=Directory.Exists(location) ? location : System.IO.Path.GetDirectoryName(location);
            string file=File.Exists(location) ? location : ConfigNames(id).Select(n => System.IO.Path.Combine(root,n.Replace('/',System.IO.Path.DirectorySeparatorChar))).FirstOrDefault(File.Exists);
            var sources=new List<EmulatorImportSource>();
            if(id=="retroarch") {
                string playlists=root;
                if(file!=null && file.EndsWith(".lpl",StringComparison.OrdinalIgnoreCase)) return new List<EmulatorImportSource> {Source(System.IO.Path.GetFileNameWithoutExtension(file),"playlist",file,false)};
                if(file!=null) {var cfg=SettingsFile(file); string setting=Get(cfg,"playlist_directory"); playlists=String.IsNullOrEmpty(setting) || setting=="default" ? System.IO.Path.Combine(exeRoot,"playlists") : Resolve(setting,exeRoot);}
                else if(Directory.Exists(System.IO.Path.Combine(root,"playlists"))) playlists=System.IO.Path.Combine(root,"playlists");
                if(Directory.Exists(playlists)) foreach(string playlist in Directory.GetFiles(playlists,"*.lpl")) sources.Add(Source(System.IO.Path.GetFileNameWithoutExtension(playlist),"playlist",playlist,false));
                return sources;
            }
            if(id=="rpcs3") {
                if(Directory.Exists(location) && file==null) {
                    var nested=new[]{"config","portable"}.Select(n => System.IO.Path.Combine(root,n)).FirstOrDefault(p => File.Exists(System.IO.Path.Combine(p,"games.yml")) || File.Exists(System.IO.Path.Combine(p,"vfs.yml")));
                    if(nested!=null) root=nested;
                }
                if(new DirectoryInfo(root).Name.Equals("game",StringComparison.OrdinalIgnoreCase)) return new List<EmulatorImportSource>{Source("Installed titles","rpcs3-installed",root,false)};
                string registry=System.IO.Path.Combine(root,"games.yml"); if(File.Exists(registry)) sources.Add(Source("Registered games","rpcs3-registered",registry,false));
                var vfs=YamlScalars(System.IO.Path.Combine(root,"vfs.yml")); string emu=Get(vfs,"emulator_dir",Get(vfs,"$(EmulatorDir)",root));
                if(String.IsNullOrWhiteSpace(emu)) emu=exeRoot;
                emu=emu.Replace("$(EmulatorDir)",root+System.IO.Path.DirectorySeparatorChar); emu=Resolve(emu,root);
                string hdd=Get(vfs,"/dev_hdd0/","$(EmulatorDir)dev_hdd0/").Replace("$(EmulatorDir)",emu.TrimEnd('\\','/')+System.IO.Path.DirectorySeparatorChar);
                string gameRoot=System.IO.Path.Combine(Resolve(hdd,root) ?? System.IO.Path.Combine(emu,"dev_hdd0"),"game");
                if(!Directory.Exists(gameRoot) && vfs.Count==0) {var candidates=new[]{System.IO.Path.Combine(exeRoot,"dev_hdd0","game"),System.IO.Path.Combine(root,"game")}; gameRoot=candidates.FirstOrDefault(Directory.Exists) ?? gameRoot;}
                if(Directory.Exists(gameRoot)) sources.Add(Source("Installed titles","rpcs3-installed",gameRoot,false)); return sources;
            }
            if(id=="vita3k") {
                var cfg=YamlScalars(file ?? System.IO.Path.Combine(root,"config.yml")); string storage=Get(cfg,"pref-path",root);
                if(String.IsNullOrWhiteSpace(storage)) storage=root;
                string apps=System.IO.Path.Combine(Resolve(storage,root),"ux0","app");
                if(root.EndsWith(System.IO.Path.Combine("ux0","app"),StringComparison.OrdinalIgnoreCase)) apps=root;
                if(!Directory.Exists(apps)) {string possible=System.IO.Path.Combine(root,"portable","ux0","app"); if(Directory.Exists(possible)) apps=possible;}
                if(Directory.Exists(apps)) sources.Add(Source("Installed titles","vita3k-installed",apps,false)); return sources;
            }
            if(id=="mgba") {if(file!=null) sources.Add(Source("Saved library","mgba-library",file,false)); return sources;}
            if(id=="shadps4") {
                if(!Directory.Exists(location)) throw new IOException("Choose the folder containing your installed PS4 games.");
                sources.Add(Source("Installed games","folder",root,true));
                return sources;
            }
            if(file==null) throw new IOException("No supported configuration found here. Browse to the emulator's configuration file.");
            Action<string,bool> folder=(path,recursive) => {
                if(String.IsNullOrWhiteSpace(path) || new[]{"UserNAND","SysNAND","SDMC","default","."}.Contains(path)) return;
                try {string resolved=Resolve(path,root); if(!Directory.Exists(resolved)) return; var source=Source("Folder: "+resolved,"folder",resolved,recursive); var existing=sources.FirstOrDefault(s => s.Id==source.Id); if(existing==null) sources.Add(source); else existing.Recursive|=recursive;} catch(ArgumentException) {}
            };
            if(id=="cemu") {
                var document=new System.Xml.XmlDocument {XmlResolver=null};
                using(var reader=System.Xml.XmlReader.Create(file,new System.Xml.XmlReaderSettings {DtdProcessing=System.Xml.DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024})) document.Load(reader);
                foreach(System.Xml.XmlNode entry in document.SelectNodes("/content/GamePaths/Entry")) folder(entry.InnerText,true);
                return sources;
            }
            var settings=SettingsFile(file);
            if(id=="duckstation" || id=="pcsx2") {
                foreach(string path in StringList(GetRaw(settings,"GameList/Paths"))) folder(path,false);
                foreach(string path in StringList(GetRaw(settings,"GameList/RecursivePaths"))) folder(path,true);
                var exclusions=StringList(GetRaw(settings,"GameList/ExcludedPaths")).Select(p => Resolve(p,root)).ToList(); foreach(var source in sources) source.Exclusions=exclusions;
            } else if(id=="dolphin") {
                foreach(var setting in settings.Where(p => Regex.IsMatch(p.Key,@"^General/ISOPath\d+$",RegexOptions.IgnoreCase))) folder(Scalar(setting.Value),Yes(Get(settings,"General/RecursiveISOPaths")));
            } else if(id=="eden") {
                foreach(var setting in settings.Where(p => Regex.IsMatch(p.Key,@"(?:^|[/\\])gamedirs[/\\]\d+[/\\]path$",RegexOptions.IgnoreCase))) {string prefix=setting.Key.Substring(0,setting.Key.Length-4); folder(Scalar(setting.Value),Yes(Get(settings,prefix+"deep_scan")));}
                if(sources.Count==0) folder(Get(settings,"UI/Paths/gameListRootDir",Get(settings,"UI/gameListRootDir")),Yes(Get(settings,"UI/Paths/gameListDeepScan")));
            } else if(id=="ppsspp") {
                foreach(var setting in settings.Where(p => p.Key.StartsWith("PinnedPaths/",StringComparison.OrdinalIgnoreCase))) folder(Scalar(setting.Value),true);
                folder(Get(settings,"General/CurrentDirectory"),true);
                var system=Directory.GetParent(root); if(system!=null && system.Name.Equals("PSP",StringComparison.OrdinalIgnoreCase)) folder(System.IO.Path.Combine(system.FullName,"GAME"),true);
                var recent=settings.Where(p => p.Key.StartsWith("Recent/FileName",StringComparison.OrdinalIgnoreCase)).Select(p => Scalar(p.Value)).ToList(); if(recent.Count>0) sources.Add(Source("Recent games (partial library)","recent",file,false));
            } else if(id=="melonds") {
                folder(Get(settings,"LastROMFolder",Get(settings,"UI/LastROMFolder")),true);
                sources.Add(Source("Recent games (partial library)","recent",file,false));
            } else if(id=="xenia") {
                string storage=Get(settings,"storage_root",Get(settings,"Storage/storage_root",root)); if(String.IsNullOrEmpty(storage)) storage=root;
                string content=Get(settings,"content_root",Get(settings,"Storage/content_root","content")); if(String.IsNullOrEmpty(content)) content="content";
                string contentRoot=Resolve(content,Resolve(storage,root)); if(Directory.Exists(contentRoot)) sources.Add(Source("Installed content games","xenia-content",contentRoot,true));
            }
            return sources;
        }
        static string GetRaw(Dictionary<string,string> values,string key) {string value; return values.TryGetValue(key,out value) ? value : "";}
        static void Candidate(EmulatorImportBatch batch,string id,EmulatorImportSource source,string path,string name,string core,string fallbackCore,string basePath=null) {
            try {
                if(path!=null && path.Contains("|")) throw new ArgumentException("Archive-member paths need manual setup; import the archive through the game picker instead.");
                if(basePath!=null) path=Resolve(path,basePath);
                if(path!=null && path.Contains("#") && !File.Exists(path) && !Directory.Exists(path)) throw new ArgumentException("Archive-member paths need manual setup.");
                path=EmulatorLibrary.GamePath(id,path);
                var entry=new EmulatorImportGame {Key=source.Id+"|"+path.ToUpperInvariant(),SourceId=source.Id,Path=path,Name=String.IsNullOrWhiteSpace(name) ? EmulatorLibrary.SuggestedName(id,path) : name.Trim(),CorePath=core};
                if(id=="retroarch") {try {entry.CorePath=EmulatorLibrary.Core(String.IsNullOrWhiteSpace(core) || core=="DETECT" ? fallbackCore : core);} catch(Exception error) {entry.Problem=error.Message;}}
                batch.Games.Add(entry);
            } catch(Exception error) {if(batch.Warnings.Count<30) batch.Warnings.Add((String.IsNullOrWhiteSpace(name) ? path : name)+": "+error.Message);}
        }
        public static EmulatorImportBatch Read(string id,string executable,IEnumerable<EmulatorImportSource> sources,string fallbackCore,CancellationToken token) {
            var batch=new EmulatorImportBatch(); string exeRoot=System.IO.Path.GetDirectoryName(EmulatorLibrary.Executable(executable));
            foreach(var source in sources) {
                token.ThrowIfCancellationRequested();
                try {
                    if(source.Kind=="playlist") {
                        string text=Text(source.Path),root=exeRoot;
                        if(text.TrimStart().StartsWith("{")) {
                            var data=Storage.Json().DeserializeObject(text); string defaultCore=Vdf.Str(data,"default_core_path");
                            foreach(var item in EpicLibrary.Entries(Vdf.Get(data,"items"))) {
                                token.ThrowIfCancellationRequested(); string path=Vdf.Str(item,"path"),core=Vdf.Str(item,"core_path");
                                if(String.IsNullOrEmpty(core) || core=="DETECT") core=defaultCore;
                                if(!String.IsNullOrEmpty(core) && core!="DETECT") core=Resolve(core,root);
                                Candidate(batch,id,source,path,Vdf.Str(item,"label"),core,fallbackCore,root);
                            }
                        } else {
                            if(text.Any(c => c=='\0')) throw new FormatException("Compressed playlists are not supported yet. Save this playlist uncompressed in RetroArch first.");
                            var lines=text.Replace("\r","").Split('\n'); if(lines.Length%6==1 && lines[lines.Length-1]=="") lines=lines.Take(lines.Length-1).ToArray();
                            if(lines.Length%6!=0) throw new FormatException("Unsupported playlist format.");
                            for(int i=0;i<lines.Length;i+=6) {token.ThrowIfCancellationRequested(); string core=lines[i+2]; Candidate(batch,id,source,lines[i],lines[i+1],core=="DETECT" ? core : Resolve(core,root),fallbackCore,root);}
                        }
                    } else if(source.Kind=="rpcs3-registered") {
                        foreach(var pair in YamlScalars(source.Path)) {
                            token.ThrowIfCancellationRequested(); if(!Regex.IsMatch(pair.Key,@"^[A-Za-z0-9_-]+$") || String.IsNullOrWhiteSpace(pair.Value)) continue;
                            string path=Resolve(pair.Value.Replace("$(EmulatorDir)",System.IO.Path.GetDirectoryName(source.Path)+System.IO.Path.DirectorySeparatorChar),System.IO.Path.GetDirectoryName(source.Path));
                            ReadPs3(batch,source,path,pair.Key,fallbackCore);
                        }
                    } else if(source.Kind=="rpcs3-installed") {
                        foreach(string folder in Directory.GetDirectories(source.Path)) {token.ThrowIfCancellationRequested(); ReadPs3(batch,source,folder,null,fallbackCore);}
                    } else if(source.Kind=="vita3k-installed") {
                        foreach(string folder in Directory.GetDirectories(source.Path)) {
                            token.ThrowIfCancellationRequested(); if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0 || System.IO.Path.GetFileName(folder).StartsWith("NPXS",StringComparison.OrdinalIgnoreCase)) continue;
                            string sfo=System.IO.Path.Combine(folder,"sce_sys","param.sfo"),category=EmulatorLibrary.SfoValue(sfo,"CATEGORY"); if(category=="gp" || category=="ac") continue;
                            Candidate(batch,id,source,folder,EmulatorLibrary.SfoTitle(sfo),null,null);
                        }
                    } else if(source.Kind=="recent") {
                        var cfg=SettingsFile(source.Path); var paths=new List<string>();
                        if(id=="ppsspp") paths.AddRange(cfg.Where(p => p.Key.StartsWith("Recent/FileName",StringComparison.OrdinalIgnoreCase)).Select(p => Scalar(p.Value)));
                        else {string raw=GetRaw(cfg,"RecentROM"); if(raw.Length==0) raw=GetRaw(cfg,"UI/RecentROM"); paths.AddRange(StringList(raw)); foreach(var p in cfg.Where(p => Regex.IsMatch(p.Key,@"(?:^|/)RecentROM_?\d+$",RegexOptions.IgnoreCase))) paths.Add(Scalar(p.Value));}
                        foreach(string path in paths) {token.ThrowIfCancellationRequested(); Candidate(batch,id,source,path,null,null,null,System.IO.Path.GetDirectoryName(source.Path));}
                    } else if(source.Kind=="mgba-library") {
                        foreach(var row in EmulatorSqlite.ReadLibrary(source.Path,token)) {
                            string root=row[0],path=row[1];
                            if(File.Exists(root)) {batch.Warnings.Add("Archive entry requires manual setup: "+root+" / "+path); continue;}
                            Candidate(batch,id,source,Resolve(path,Resolve(root,System.IO.Path.GetDirectoryName(source.Path))),row[2],null,null);
                        }
                    } else {
                        var scanned=EmulatorLibrary.Scan(id,source.Path,token,fallbackCore,source.Recursive);
                        foreach(string path in scanned.Paths) {
                            token.ThrowIfCancellationRequested();
                            if(source.Exclusions.Any(p => path.Equals(p,StringComparison.OrdinalIgnoreCase) || path.StartsWith(p.TrimEnd('\\','/')+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))) continue;
                            if(source.Kind=="xenia-content" && !XeniaGame(path)) continue;
                            Candidate(batch,id,source,path,null,null,fallbackCore);
                        }
                        if(scanned.UnreadableFolders>0) batch.Warnings.AddRange(scanned.ReadErrors);
                    }
                } catch(OperationCanceledException) {throw;} catch(Exception error) {batch.Warnings.Add(source.Name+": "+error.Message);}
            }
            return batch;
        }
        static void ReadPs3(EmulatorImportBatch batch,EmulatorImportSource source,string path,string titleId,string fallback) {
            if(Directory.Exists(path) && (File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0) return;
            string sfo=Directory.Exists(System.IO.Path.Combine(path,"PS3_GAME")) ? System.IO.Path.Combine(path,"PS3_GAME","PARAM.SFO") : System.IO.Path.Combine(path,"PARAM.SFO");
            string category=EmulatorLibrary.SfoValue(sfo,"CATEGORY");
            if(source.Kind=="rpcs3-installed" && !new[]{"HG","HM","CB"}.Contains(category)) return;
            if(category=="GD" || category=="AC") return;
            string title=EmulatorLibrary.SfoTitle(sfo);
            Candidate(batch,"rpcs3",source,path,title,null,fallback);
        }
        static bool XeniaGame(string path) {
            if(path.EndsWith("default.xex",StringComparison.OrdinalIgnoreCase) || path.EndsWith(".iso",StringComparison.OrdinalIgnoreCase)) return true;
            return EmulatorLibrary.IsXeniaGamePackage(path);
        }
        internal static List<string> AddEntries(Settings settings,string emulator,string executable,IEnumerable<EmulatorImportGame> chosen) {
            var entries=chosen.ToList(); if(entries.Count==0) throw new ArgumentException("Select at least one game.");
            EmulatorLibrary.SavePath(settings,emulator,executable); var config=EmulatorLibrary.Config(settings,emulator); if(config.Games==null) config.Games=new List<EmulatorGame>();
            var ids=new List<string>();
            var byPath=config.Games.Where(g => !String.IsNullOrEmpty(g.Path)).GroupBy(g => g.Path,StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key,g => g.First(),StringComparer.OrdinalIgnoreCase);
            foreach(var item in entries) {
                string path=EmulatorLibrary.GamePath(emulator,item.Path);
                EmulatorGame existing; byPath.TryGetValue(path,out existing);
                string core=emulator=="retroarch" && existing==null ? EmulatorLibrary.Core(item.CorePath) : null;
                if(existing==null) {existing=new EmulatorGame {Id="emulator:"+emulator+":"+Guid.NewGuid().ToString("N"),Path=path,Name=String.IsNullOrWhiteSpace(item.Name) ? EmulatorLibrary.SuggestedName(emulator,path) : item.Name.Trim().Substring(0,Math.Min(120,item.Name.Trim().Length)),CorePath=core}; config.Games.Add(existing); byPath[path]=existing;}
                ids.Add(existing.Id);
            }
            return ids;
        }
        public static string Import(Settings settings,Library library,string emulator,string executable,string location,IEnumerable<EmulatorImportSource> sources,IEnumerable<EmulatorImportGame> chosen,IEnumerable<string> excluded,string listId,string name) {
            var ids=AddEntries(settings,emulator,executable,chosen);
            var config=EmulatorLibrary.Config(settings,emulator);
            var working=new Library {Games=library.Games.ToList(),Collections=library.Collections.ToList()}; EmulatorLibrary.Apply(working,settings);
            var previous=(settings.CustomLists ?? new List<CustomList>()).FirstOrDefault(l => l.Id==listId);
            bool automatic=listId=="source:emulator:"+emulator;
            if(listId!=null && previous==null && !automatic) throw new ArgumentException("The destination list no longer exists.");
            bool keepHidden=previous!=null && settings.EnabledCollectionIds!=null && !settings.EnabledCollectionIds.Contains(previous.Id);
            string target=automatic ? listId : CustomLists.Save(settings,working,listId,previous==null ? name : previous.Name,(previous==null ? Enumerable.Empty<string>() : previous.Games ?? new List<string>()).Concat(ids));
            if(keepHidden) settings.EnabledCollectionIds.Remove(target);
            if(settings.EmulatorImports==null) settings.EmulatorImports=new List<EmulatorImportDefinition>();
            var sourceIds=sources.Select(s => s.Id).Distinct().OrderBy(s => s,StringComparer.Ordinal).ToList();
            var definition=settings.EmulatorImports.FirstOrDefault(d => d.EmulatorId==emulator && String.Equals(d.Location,location,StringComparison.OrdinalIgnoreCase) && d.ListId==target && (d.Sources ?? new List<string>()).OrderBy(s => s,StringComparer.Ordinal).SequenceEqual(sourceIds));
            if(definition==null) {definition=new EmulatorImportDefinition {Id=Guid.NewGuid().ToString("N"),EmulatorId=emulator,Location=location,ListId=target}; settings.EmulatorImports.Add(definition);}
            definition.SourceLabel=String.Join(", ",sources.Select(s => s.Name)); definition.Sources=sourceIds; definition.ExcludedKeys=(excluded ?? Enumerable.Empty<string>()).Distinct().ToList(); definition.ImportedGames=(definition.ImportedGames ?? new List<string>()).Concat(ids).Distinct().ToList(); config.ImportPath=location;
            return target;
        }
    }
}
