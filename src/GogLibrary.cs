using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace PadLauncher {
    public class GogInstall {
        public string Id;
        public string Name;
        public string Folder;
    }
    // Native installed-game adapter. UI/library wiring is intentionally separate.
    public static class GogLibrary {
        public static string DetectClient() {
            foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))
                using(var key=root.OpenSubKey(@"SOFTWARE\GOG.com\GalaxyClient\paths")) {
                    string folder=key==null ? null : key.GetValue("client") as string;
                    if(!String.IsNullOrWhiteSpace(folder) && File.Exists(Path.Combine(folder,"GalaxyClient.exe"))) return Path.Combine(folder,"GalaxyClient.exe");
                }
            } catch {}
            string standard=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"GOG Galaxy","GalaxyClient.exe");
            return File.Exists(standard) ? standard : null;
        }
        public static List<GogInstall> InstalledRecords() {
            var result=new List<GogInstall>();
            foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser}) foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(hive,view)) {
                    using(var games=root.OpenSubKey(@"SOFTWARE\GOG.com\Games")) if(games!=null) foreach(string name in games.GetSubKeyNames()) using(var key=games.OpenSubKey(name)) {
                        if(key==null) continue;
                        result.Add(new GogInstall {Id=Convert.ToString(key.GetValue("gameID") ?? name),Name=key.GetValue("gameName") as string,Folder=key.GetValue("path") as string});
                    }
                    using(var programs=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) if(programs!=null) foreach(string name in programs.GetSubKeyNames()) {
                        var match=Regex.Match(name,@"^(\d+)_is1$"); if(!match.Success) continue;
                        using(var key=programs.OpenSubKey(name)) {
                            if(key==null || !String.Equals(key.GetValue("Publisher") as string,"GOG.com",StringComparison.OrdinalIgnoreCase)) continue;
                            result.Add(new GogInstall {Id=match.Groups[1].Value,Name=key.GetValue("DisplayName") as string,Folder=key.GetValue("InstallLocation") as string});
                        }
                    }
                }
            } catch {}
            return result;
        }
        public static List<Game> Scan(IEnumerable<GogInstall> records=null) {
            var result=new List<Game>();
            foreach(var record in records ?? InstalledRecords()) try {
                ulong id; if(!UInt64.TryParse(record.Id,out id) || id==0 || String.IsNullOrWhiteSpace(record.Folder) || !Directory.Exists(record.Folder)) continue;
                if(result.Any(g => g.Id=="gog:"+id)) continue;
                string root=Path.GetFullPath(record.Folder).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
                string manifest=Path.Combine(root,"goggame-"+id+".info"); if(!File.Exists(manifest)) continue;
                object info=Storage.Json().DeserializeObject(File.ReadAllText(manifest));
                string rootId=Vdf.Str(info,"rootGameId"),gameId=Vdf.Str(info,"gameId");
                if(gameId!=id.ToString() || (!String.IsNullOrEmpty(rootId) && rootId!=gameId)) continue;
                bool playable=EpicLibrary.Entries(Vdf.Get(info,"playTasks")).Any(task => {
                    if(!Vdf.Str(task,"isPrimary").Equals("true",StringComparison.OrdinalIgnoreCase) || !Vdf.Str(task,"type").Equals("FileTask",StringComparison.OrdinalIgnoreCase)) return false;
                    string path=Vdf.Str(task,"path"); if(String.IsNullOrWhiteSpace(path)) return false;
                    string full=Path.GetFullPath(Path.Combine(root,path)); return full.StartsWith(root,StringComparison.OrdinalIgnoreCase) && File.Exists(full);
                });
                if(!playable) continue;
                string name=Vdf.Str(info,"name"); if(String.IsNullOrWhiteSpace(name)) name=record.Name;
                if(String.IsNullOrWhiteSpace(name)) continue;
                result.Add(new Game {Id="gog:"+id,LaunchId=id.ToString(),Name=name.Trim(),Source="gog",WorkingDirectory=Path.GetFullPath(record.Folder)});
            } catch { /* A stale or malformed install must not hide other games. */ }
            return result.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static ProcessStartInfo StartInfo(Game game,string client=null) {
            ulong id; if(game.Source!="gog" || !UInt64.TryParse(game.LaunchId,out id) || id==0) throw new InvalidOperationException("Invalid GOG game ID.");
            if(!Directory.Exists(game.WorkingDirectory)) throw new DirectoryNotFoundException("The GOG game's installation folder is missing.");
            client=client ?? DetectClient(); if(String.IsNullOrEmpty(client) || !File.Exists(client)) throw new FileNotFoundException("GOG Galaxy could not be found.");
            string folder=Path.GetFullPath(game.WorkingDirectory).TrimEnd('\\');
            if(folder.Contains("\"")) throw new InvalidOperationException("Invalid GOG installation folder.");
            return new ProcessStartInfo(client,"/launchViaAutostart /command=runGame /gameId="+id+" /path=\""+folder+"\"") {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(client)};
        }
    }
}
