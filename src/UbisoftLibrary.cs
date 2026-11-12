using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace PadLauncher {
    public class UbisoftInstall {
        public string Id;
        public string Folder;
        public string Name;
    }
    public static class UbisoftLibrary {
        public static string DetectClient() {
            if(TestEnvironment.FirstStartup) return null;
            try {
                using(var key=Registry.ClassesRoot.OpenSubKey(@"uplay\shell\open\command")) {
                    string command=key==null ? "" : Convert.ToString(key.GetValue(""));
                    var match=Regex.Match(command,@"^\s*""?(.+?\.exe)""?(?:\s|$)",RegexOptions.IgnoreCase);
                    if(match.Success && File.Exists(match.Groups[1].Value)) return match.Groups[1].Value;
                }
            } catch {}
            foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))
                using(var key=root.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher")) {
                    string folder=key==null ? null : key.GetValue("InstallDir") as string;
                    if(!String.IsNullOrWhiteSpace(folder)) foreach(string name in new[]{"UbisoftConnect.exe","upc.exe"}) {string path=Path.Combine(folder,name); if(File.Exists(path)) return path;}
                }
            } catch {}
            string standard=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Ubisoft","Ubisoft Game Launcher","UbisoftConnect.exe"); return File.Exists(standard) ? standard : null;
        }
        public static string ValidateClient(string value) {
            if(String.IsNullOrWhiteSpace(value)) return null;
            string path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')));
            if(!File.Exists(path) || !new[]{"UbisoftConnect.exe","upc.exe","UbisoftGameLauncher.exe","UbisoftGameLauncher64.exe"}.Contains(Path.GetFileName(path),StringComparer.OrdinalIgnoreCase)) throw new ArgumentException("Choose UbisoftConnect.exe or upc.exe, or leave empty for automatic detection.");
            return path;
        }
        public static List<UbisoftInstall> InstalledRecords() {
            var result=new List<UbisoftInstall>();
            foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser}) foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(hive,view))
                using(var installs=root.OpenSubKey(@"SOFTWARE\Ubisoft\Launcher\Installs")) if(installs!=null) foreach(string name in installs.GetSubKeyNames()) using(var key=installs.OpenSubKey(name)) {
                    if(key!=null) result.Add(new UbisoftInstall {Id=name,Folder=key.GetValue("InstallDir") as string,Name=key.GetValue("DisplayName") as string ?? key.GetValue("Name") as string});
                }
            } catch {}
            return result;
        }
        public static List<Game> Scan(IEnumerable<UbisoftInstall> records=null) {
            var games=new List<Game>();
            foreach(var record in records ?? InstalledRecords()) try {
                uint id; if(!UInt32.TryParse(record.Id,out id) || id==0 || String.IsNullOrWhiteSpace(record.Folder)) continue;
                string folder=Path.GetFullPath(record.Folder.Replace('/',Path.DirectorySeparatorChar)).TrimEnd('\\');
                if(!Directory.Exists(folder) || games.Any(g => g.Id=="ubisoft:"+id)) continue;
                bool executable=false;
                foreach(string relative in new[]{"","bin","bin64","bin32",@"bin\Win64",@"Binaries\Win64",@"Game\bin64"}) {
                    string bin=Path.Combine(folder,relative);
                    if(Directory.Exists(bin) && Directory.GetFiles(bin,"*.exe",SearchOption.TopDirectoryOnly).Any(path => !Regex.IsMatch(Path.GetFileNameWithoutExtension(path),@"^(unins|uninstall|setup|cleanup|vcredist|directx)",RegexOptions.IgnoreCase))) {executable=true; break;}
                }
                if(!executable) continue;
                string name=String.IsNullOrWhiteSpace(record.Name) ? Path.GetFileName(folder) : record.Name.Trim(); if(String.IsNullOrWhiteSpace(name)) continue;
                games.Add(new Game {Id="ubisoft:"+id,LaunchId=id.ToString(),Name=name,Source="ubisoft",WorkingDirectory=folder});
            } catch { /* Stale installs and in-progress downloads do not hide other games. */ }
            return games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static string UriFor(Game game) {
            uint id; if(game.Source!="ubisoft" || !UInt32.TryParse(game.LaunchId,out id) || id==0) throw new InvalidOperationException("Invalid Ubisoft Connect game ID.");
            return "uplay://launch/"+id;
        }
        public static ProcessStartInfo StartInfo(Game game,string customPath=null) {
            string uri=UriFor(game); if(!Directory.Exists(game.WorkingDirectory)) throw new DirectoryNotFoundException("The Ubisoft game's installation folder is missing.");
            string client=String.IsNullOrWhiteSpace(customPath) ? DetectClient() : ValidateClient(customPath);
            return String.IsNullOrEmpty(client) ? new ProcessStartInfo(uri) {UseShellExecute=true} : new ProcessStartInfo(client,"\""+uri+"\"") {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(client)};
        }
    }
}
