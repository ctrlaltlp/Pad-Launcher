using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Win32;

namespace PadLauncher {
    public sealed class EmulatorInstallRecord {
        public string Name {get;set;}
        public string Location {get;set;}
        public string Icon {get;set;}
        public string Uninstall {get;set;}
    }
    public sealed class EmulatorExecutableHint {
        public string Path {get;set;}
        public string Source {get;set;}
    }
    public sealed class DetectedEmulator {
        public string Id {get;set;}
        public string Name {get;set;}
        public string Path {get;set;}
        public List<string> Sources {get;set;}
    }
    public sealed class EmulatorDetectionResult {
        public List<DetectedEmulator> Matches {get;set;}
        public List<string> Warnings {get;set;}
        public EmulatorDetectionResult() {Matches=new List<DetectedEmulator>(); Warnings=new List<string>();}
    }
    public static class EmulatorDetection {
        public static readonly Dictionary<string,string[]> Executables=new Dictionary<string,string[]> {
            {"duckstation",new[]{"duckstation-qt-x64-ReleaseLTCG.exe","duckstation-qt-x64-Release.exe","duckstation-qt-x64.exe","duckstation.exe"}},
            {"pcsx2",new[]{"pcsx2-qt.exe","pcsx2.exe"}}, {"rpcs3",new[]{"rpcs3.exe"}},
            {"xemu",new[]{"xemu.exe"}}, {"xenia",new[]{"xenia_canary.exe","xenia.exe"}}, {"dolphin",new[]{"Dolphin.exe"}},
            {"eden",new[]{"eden.exe","eden-qt.exe"}}, {"azahar",new[]{"azahar.exe","azahar-qt.exe"}},
            {"melonds",new[]{"melonDS.exe"}}, {"vita3k",new[]{"Vita3K.exe"}},
            {"retroarch",new[]{"retroarch.exe"}}, {"ppsspp",new[]{"PPSSPPWindows64.exe","PPSSPPWindows.exe","PPSSPP.exe"}},
            {"mgba",new[]{"mGBA.exe","mgba-qt.exe"}}, {"cemu",new[]{"Cemu.exe"}}, {"shadps4",new[]{"shadPS4.exe","shadPS4QtLauncher.exe"}}
        };
        static string LocalPath(string value) {
            if(String.IsNullOrWhiteSpace(value)) return null;
            try {
                string path=Environment.ExpandEnvironmentVariables(value.Trim().Trim('"'));
                if(!System.IO.Path.IsPathRooted(path) || path.StartsWith(@"\\",StringComparison.Ordinal)) return null;
                path=System.IO.Path.GetFullPath(path);
                if(!new DriveInfo(System.IO.Path.GetPathRoot(path)).IsReady) return null;
                return path;
            } catch {return null;}
        }
        public static string ExecutableFromCommand(string value) {
            if(String.IsNullOrWhiteSpace(value)) return null;
            var match=Regex.Match(Environment.ExpandEnvironmentVariables(value),"^\\s*(?:\"([^\"]+\\.exe)\"|(.+?\\.exe))(?=\\s|,|$)",RegexOptions.IgnoreCase);
            return match.Success ? LocalPath(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value) : null;
        }
        static string MatchId(string path) {
            string file=System.IO.Path.GetFileName(path);
            return Executables.FirstOrDefault(pair => pair.Value.Contains(file,StringComparer.OrdinalIgnoreCase)).Key;
        }
        static bool MatchesName(string name,string id) {
            string compact=Regex.Replace(name ?? "","[^a-z0-9]","",RegexOptions.IgnoreCase).ToLowerInvariant();
            string title=Regex.Replace(EmulatorLibrary.Preset(id).Name,"[^a-z0-9]","",RegexOptions.IgnoreCase).ToLowerInvariant();
            // Dolphin is also the name of unrelated software: require an emulator-specific title.
            if(id=="dolphin") return compact.StartsWith("dolphinemulator") || compact=="dolphin" || Regex.IsMatch(compact,"^dolphin[0-9]");
            return compact.StartsWith(title);
        }
        static void Add(EmulatorDetectionResult result,string path,string source) {
            path=LocalPath(path); if(path==null || !File.Exists(path)) return;
            string id=MatchId(path); if(id==null) return;
            var existing=result.Matches.FirstOrDefault(item => item.Id==id && String.Equals(item.Path,path,StringComparison.OrdinalIgnoreCase));
            if(existing==null) {existing=new DetectedEmulator {Id=id,Name=EmulatorLibrary.Preset(id).Name+(EmulatorLibrary.IsShadQtLauncher(path) ? " (Qt Launcher)" : ""),Path=path,Sources=new List<string>()}; result.Matches.Add(existing);}
            if(!existing.Sources.Contains(source)) existing.Sources.Add(source);
        }
        static void ProbeFolder(EmulatorDetectionResult result,string id,string folder,string source) {
            folder=LocalPath(folder); if(folder==null || !Directory.Exists(folder)) return;
            foreach(string sub in new[]{"","bin","x64"}) foreach(string file in Executables[id]) Add(result,System.IO.Path.Combine(folder,sub,file),source);
        }
        public static EmulatorDetectionResult FromSources(IEnumerable<EmulatorExecutableHint> hints,IEnumerable<EmulatorInstallRecord> installs,IEnumerable<string> roots,CancellationToken token) {
            token.ThrowIfCancellationRequested();
            var result=new EmulatorDetectionResult();
            foreach(var hint in hints ?? Enumerable.Empty<EmulatorExecutableHint>()) {token.ThrowIfCancellationRequested(); Add(result,ExecutableFromCommand(hint.Path),hint.Source);}
            foreach(var install in installs ?? Enumerable.Empty<EmulatorInstallRecord>()) {
                token.ThrowIfCancellationRequested();
                if(!Executables.Keys.Any(id => MatchesName(install.Name,id))) continue;
                string icon=ExecutableFromCommand(install.Icon),uninstall=ExecutableFromCommand(install.Uninstall),source="Installed app: "+install.Name;
                Add(result,icon,source);
                foreach(string id in Executables.Keys.Where(id => MatchesName(install.Name,id))) {
                    ProbeFolder(result,id,install.Location,source);
                    if(icon!=null) ProbeFolder(result,id,System.IO.Path.GetDirectoryName(icon),source);
                    if(uninstall!=null) ProbeFolder(result,id,System.IO.Path.GetDirectoryName(uninstall),source);
                }
            }
            foreach(string rootValue in (roots ?? Enumerable.Empty<string>()).Distinct(StringComparer.OrdinalIgnoreCase)) {
                token.ThrowIfCancellationRequested();
                string root=LocalPath(rootValue); if(root==null || !Directory.Exists(root)) continue;
                try {foreach(string folder in Directory.GetDirectories(root)) {
                    token.ThrowIfCancellationRequested();
                    if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0) continue;
                    foreach(string id in Executables.Keys.Where(id => MatchesName(System.IO.Path.GetFileName(folder),id))) ProbeFolder(result,id,folder,"Standard installation folder");
                }} catch(OperationCanceledException) {throw;} catch(Exception error) {result.Warnings.Add("Could not inspect "+root+": "+error.Message);}
            }
            result.Matches=result.Matches.OrderBy(item => item.Name,StringComparer.CurrentCultureIgnoreCase).ThenBy(item => item.Path,StringComparer.OrdinalIgnoreCase).ToList(); return result;
        }
        public static string ShortcutTarget(string file) {
            object shell=null,shortcut=null;
            try {
                var type=Type.GetTypeFromProgID("WScript.Shell"); if(type==null) return null;
                shell=Activator.CreateInstance(type);
                shortcut=type.InvokeMember("CreateShortcut",BindingFlags.InvokeMethod,null,shell,new object[]{file});
                return shortcut.GetType().InvokeMember("TargetPath",BindingFlags.GetProperty,null,shortcut,null) as string;
            } finally {
                if(shortcut!=null && Marshal.IsComObject(shortcut)) Marshal.FinalReleaseComObject(shortcut);
                if(shell!=null && Marshal.IsComObject(shell)) Marshal.FinalReleaseComObject(shell);
            }
        }
        static void ReadShortcuts(string folder,List<EmulatorExecutableHint> hints,List<string> warnings,CancellationToken token,int depth=0) {
            if(depth>10 || !Directory.Exists(folder)) return;
            try {
                foreach(string file in Directory.GetFiles(folder,"*.lnk")) {
                    token.ThrowIfCancellationRequested();
                    // Inspect only relevant shortcut names; never resolve or launch their targets.
                    if(!Executables.Keys.Any(id => MatchesName(System.IO.Path.GetFileNameWithoutExtension(file),id))) continue;
                    try {hints.Add(new EmulatorExecutableHint {Path=ShortcutTarget(file),Source="Start menu shortcut: "+System.IO.Path.GetFileNameWithoutExtension(file)});} catch(Exception error) {warnings.Add("Could not read shortcut "+file+": "+error.Message);}
                }
                foreach(string sub in Directory.GetDirectories(folder)) if((File.GetAttributes(sub)&FileAttributes.ReparsePoint)==0) ReadShortcuts(sub,hints,warnings,token,depth+1);
            } catch(OperationCanceledException) {throw;} catch(Exception error) {warnings.Add("Could not inspect Start menu folder "+folder+": "+error.Message);}
        }
        public static List<EmulatorExecutableHint> SteamHints(string steam,CancellationToken token) {
            var hints=new List<EmulatorExecutableHint>(); steam=LocalPath(steam);
            if(steam==null || !Directory.Exists(steam)) return hints;
            var folders=new HashSet<string>(StringComparer.OrdinalIgnoreCase) {steam};
            string file=System.IO.Path.Combine(steam,"steamapps","libraryfolders.vdf");
            if(File.Exists(file)) foreach(var pair in Vdf.Map(Vdf.Get(Vdf.Read(file),"libraryfolders"))) {
                token.ThrowIfCancellationRequested(); string folder=LocalPath(pair.Value as string ?? Vdf.Str(pair.Value,"path"));
                if(folder!=null) folders.Add(folder);
            }
            foreach(string library in folders) {
                token.ThrowIfCancellationRequested(); string common=System.IO.Path.Combine(library,"steamapps","common");
                if(!Directory.Exists(common)) continue;
                foreach(string folder in Directory.GetDirectories(common)) {
                    token.ThrowIfCancellationRequested(); if((File.GetAttributes(folder)&FileAttributes.ReparsePoint)!=0) continue;
                    foreach(string id in Executables.Keys.Where(id => MatchesName(System.IO.Path.GetFileName(folder),id)))
                        foreach(string sub in new[]{"","bin","x64"}) foreach(string name in Executables[id]) {
                            string candidate=System.IO.Path.Combine(folder,sub,name);
                            if(File.Exists(candidate)) hints.Add(new EmulatorExecutableHint {Path=candidate,Source="Steam library: "+library});
                        }
                }
            }
            return hints;
        }
        public static EmulatorDetectionResult Scan(CancellationToken token) {return Scan(token,null);}
        public static EmulatorDetectionResult Scan(CancellationToken token,string configuredSteam) {
            var hints=new List<EmulatorExecutableHint>(); var installs=new List<EmulatorInstallRecord>(); var warnings=new List<string>();
            foreach(var hive in new[]{RegistryHive.CurrentUser,RegistryHive.LocalMachine}) foreach(var view in new[]{RegistryView.Registry64,RegistryView.Registry32}) {
                token.ThrowIfCancellationRequested();
                try {using(var root=RegistryKey.OpenBaseKey(hive,view)) {
                    using(var paths=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths")) if(paths!=null) foreach(string name in Executables.Values.SelectMany(names => names).Distinct(StringComparer.OrdinalIgnoreCase)) {
                        using(var key=paths.OpenSubKey(name)) if(key!=null) hints.Add(new EmulatorExecutableHint {Path=key.GetValue("") as string,Source="App Paths: "+hive+" / "+view});
                    }
                    using(var programs=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) if(programs!=null) foreach(string name in programs.GetSubKeyNames()) {
                        token.ThrowIfCancellationRequested();
                        try {using(var key=programs.OpenSubKey(name)) if(key!=null) {
                            string title=key.GetValue("DisplayName") as string;
                            if(!Executables.Keys.Any(id => MatchesName(title,id))) continue;
                            installs.Add(new EmulatorInstallRecord {Name=title,Location=key.GetValue("InstallLocation") as string,Icon=key.GetValue("DisplayIcon") as string,Uninstall=key.GetValue("UninstallString") as string});
                        }} catch(System.Security.SecurityException) {} catch(UnauthorizedAccessException) {}
                    }
                }} catch(OperationCanceledException) {throw;} catch(Exception error) {warnings.Add("Could not inspect "+hive+" / "+view+": "+error.Message);}
            }
            foreach(var place in new[]{Environment.SpecialFolder.Programs,Environment.SpecialFolder.CommonPrograms}) ReadShortcuts(Environment.GetFolderPath(place),hints,warnings,token);
            foreach(string steam in new[]{configuredSteam,SteamLibrary.DetectSteam()}.Where(path => !String.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase)) {
                try {hints.AddRange(SteamHints(steam,token));}
                catch(OperationCanceledException) {throw;} catch(Exception error) {warnings.Add("Could not inspect Steam emulator locations: "+error.Message);}
            }
            string local=Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var result=FromSources(hints,installs,new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),System.IO.Path.Combine(local,"Programs"),local},token);
            result.Warnings.AddRange(warnings); return result;
        }
        public static Settings Apply(Settings settings,IDictionary<string,string> chosen) {
            var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            foreach(var pair in chosen) {
                string path=EmulatorLibrary.Executable(pair.Value);
                if(MatchId(path)!=pair.Key) throw new ArgumentException("The detected executable does not match "+EmulatorLibrary.Preset(pair.Key).Name+".");
                EmulatorLibrary.SavePath(next,pair.Key,path);
            }
            return next;
        }
    }
}
