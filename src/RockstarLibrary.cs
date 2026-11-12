using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace PadLauncher {
    public class RockstarInstall {
        public string TitleId;
        public string Folder;
    }
    public static class RockstarLibrary {
        static readonly Dictionary<string,string[]> Titles=new Dictionary<string,string[]>(StringComparer.OrdinalIgnoreCase) {
            {"gta5",new[]{"Grand Theft Auto V","PlayGTAV.exe"}},
            {"gta5_gen9",new[]{"Grand Theft Auto V Enhanced","GTA5_Enhanced_BE.exe","GTA5_Enhanced.exe"}},
            {"gta4",new[]{"Grand Theft Auto IV","GTAIV.exe"}},
            {"rdr",new[]{"Red Dead Redemption","RDR.exe"}},
            {"rdr2",new[]{"Red Dead Redemption 2","RDR2.exe"}},
            {"lanoire",new[]{"L.A. Noire","LANoire.exe"}},
            {"lanoirevr",new[]{"L.A. Noire: The VR Case Files","LANoireVR.exe"}},
            {"mp3",new[]{"Max Payne 3","MaxPayne3.exe"}},
            {"bully",new[]{"Bully: Scholarship Edition","Bully.exe"}},
            {"gta3",new[]{"Grand Theft Auto III","gta3.exe"}},
            {"gtavc",new[]{"Grand Theft Auto: Vice City","gta-vc.exe"}},
            {"gtasa",new[]{"Grand Theft Auto: San Andreas","gta_sa.exe"}},
            {"gta3unreal",new[]{"Grand Theft Auto III: The Definitive Edition",@"Gameface\Binaries\Win64\LibertyCity.exe"}},
            {"gtavcunreal",new[]{"Grand Theft Auto: Vice City – The Definitive Edition",@"Gameface\Binaries\Win64\ViceCity.exe"}},
            {"gtasaunreal",new[]{"Grand Theft Auto: San Andreas – The Definitive Edition",@"Gameface\Binaries\Win64\SanAndreas.exe"}}
        };
        public static string DetectClient() {
            if(TestEnvironment.FirstStartup) return null;
            try {using(var key=Registry.ClassesRoot.OpenSubKey(@"rockstar\shell\open\command")) {
                var match=Regex.Match(key==null ? "" : Convert.ToString(key.GetValue("")),@"^\s*""([^""]+\.exe)""",RegexOptions.IgnoreCase);
                if(match.Success && File.Exists(match.Groups[1].Value)) return match.Groups[1].Value;
            }} catch {}
            foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view)) using(var key=root.OpenSubKey(@"SOFTWARE\Rockstar Games\Launcher")) {
                    string folder=key==null ? null : key.GetValue("InstallFolder") as string;
                    if(!String.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder,"Launcher.exe"))) return Path.Combine(folder,"Launcher.exe");
                }
            } catch {}
            string standard=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Rockstar Games","Launcher","Launcher.exe"); return File.Exists(standard) ? standard : null;
        }
        public static string ValidateClient(string value) {
            if(String.IsNullOrWhiteSpace(value)) return null;
            string path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')));
            if(!File.Exists(path) || !Path.GetFileName(path).Equals("Launcher.exe",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose Rockstar Games Launcher's Launcher.exe, or leave empty for automatic detection.");
            return path;
        }
        public static string TitleFromUninstall(string command) {
            var match=Regex.Match(command ?? "",@"(?:Launcher|uninstall)\.exe""?\s+.*?[-/]uninstall\s*[= ]\s*""?([A-Za-z0-9_]+)""?(?=\s|$)",RegexOptions.IgnoreCase);
            return match.Success && Titles.ContainsKey(match.Groups[1].Value) ? match.Groups[1].Value.ToLowerInvariant() : null;
        }
        public static List<RockstarInstall> InstalledRecords() {
            var result=new List<RockstarInstall>();
            foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser}) foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(hive,view)) using(var programs=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) if(programs!=null) foreach(string name in programs.GetSubKeyNames()) using(var key=programs.OpenSubKey(name)) {
                    if(key==null) continue;
                    string title=TitleFromUninstall(key.GetValue("UninstallString") as string); if(title==null) continue;
                    result.Add(new RockstarInstall {TitleId=title,Folder=key.GetValue("InstallLocation") as string});
                }
            } catch {}
            return result;
        }
        public static List<Game> Scan(IEnumerable<RockstarInstall> records=null) {
            var games=new List<Game>();
            foreach(var record in records ?? InstalledRecords()) try {
                string[] title;
                if(String.IsNullOrEmpty(record.TitleId) || !Titles.TryGetValue(record.TitleId,out title) || String.IsNullOrWhiteSpace(record.Folder)) continue;
                string folder=Path.GetFullPath(Environment.ExpandEnvironmentVariables(record.Folder.Trim().Trim('"'))).TrimEnd('\\');
                if(!Directory.Exists(folder) || !title.Skip(1).Any(file => File.Exists(Path.Combine(folder,file))) || games.Any(g => g.LaunchId.Equals(record.TitleId,StringComparison.OrdinalIgnoreCase))) continue;
                games.Add(new Game {Id="rockstar:"+record.TitleId.ToLowerInvariant(),Source="rockstar",LaunchId=record.TitleId.ToLowerInvariant(),Name=title[0],WorkingDirectory=folder});
            } catch {}
            return games.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static ProcessStartInfo StartInfo(Game game,string customPath=null) {
            string[] title;
            if(game.Source!="rockstar" || !Titles.TryGetValue(game.LaunchId ?? "",out title)) throw new InvalidOperationException("Unsupported Rockstar game ID.");
            if(!Directory.Exists(game.WorkingDirectory) || !title.Skip(1).Any(file => File.Exists(Path.Combine(game.WorkingDirectory,file)))) throw new FileNotFoundException("The Rockstar game's installed executable is missing.");
            string folder=Path.GetFullPath(game.WorkingDirectory).TrimEnd('\\');
            string client=String.IsNullOrWhiteSpace(customPath) ? DetectClient() : ValidateClient(customPath);
            if(String.IsNullOrEmpty(client)) throw new FileNotFoundException("Rockstar Games Launcher could not be found. Choose its path in Launchers.");
            return new ProcessStartInfo(client,"-launchTitleInFolder \""+folder+"\"") {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(client)};
        }
    }
}
