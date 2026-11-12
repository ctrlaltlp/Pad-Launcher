using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace PadLauncher {
    public static class BattleNetLibrary {
        static readonly Dictionary<string,string> Products=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {
            {"wow","WoW"},{"wow_classic","WoWC"},{"wow_classic_era","WoWC"},
            {"diablo3","D3"},{"fen","Fen"},{"osi","OSI"},{"d1","D1"},
            {"s1","S1"},{"s2","S2"},{"hs_beta","WTCG"},{"heroes","Hero"},{"prometheus","Pro"},
            {"w3","W3"},{"w1r","W1R"},{"w2r","W2R"},{"w1","W1"},{"w2","W2"},
            {"viper","VIPR"},{"odin","ODIN"},{"lazarus","LAZR"},{"zeus","ZEUS"},
            {"fore","FORE"},{"wlby","WLBY"},{"rtro","RTRO"},{"anbs","ANBS"},{"auks","AUKS"},
            {"gryphon","GRY"},{"aris","ARIS"},{"scorpio","SCOR"},{"arkansas","ARK"},
            {"libra","LBRA"},{"pinta","PNTA"},{"aqua","AQUA"}
        };
        public static string DetectClient() {
            if(TestEnvironment.FirstStartup) return null;
            foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,view))
                using(var key=root.OpenSubKey(@"SOFTWARE\Blizzard Entertainment\Battle.net\Capabilities")) {
                    string icon=key==null ? null : key.GetValue("ApplicationIcon") as string;
                    var match=Regex.Match(icon ?? "",@"^\s*""?([^""]*?\\Battle\.net\.exe)""?(?:,|$)",RegexOptions.IgnoreCase);
                    if(match.Success && File.Exists(match.Groups[1].Value)) return match.Groups[1].Value;
                }
            } catch(System.Security.SecurityException) {} catch(UnauthorizedAccessException) {} catch(IOException) {}
            return new[]{Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.ProgramFiles}
                .Select(folder => Path.Combine(Environment.GetFolderPath(folder),"Battle.net","Battle.net.exe")).FirstOrDefault(File.Exists);
        }
        public static Game FromInstall(string name,string folder,string command) {
            if(String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(folder)) return null;
            var match=Regex.Match(command ?? "",@"^\s*(?:""[^""]*\\(?:Battle\.net(?: Launcher)?\.exe|Battle\.net\\Agent\\Blizzard Uninstaller\.exe)""|[^""]*?\\(?:Battle\.net(?: Launcher)?\.exe|Battle\.net\\Agent\\Blizzard Uninstaller\.exe))\s+.*?--uid=(?:""([a-z0-9_]+)""|([a-z0-9_]+))(?=\s|$)",RegexOptions.IgnoreCase);
            string uid=match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            string product;
            if(!match.Success || !Products.TryGetValue(uid,out product)) return null;
            folder=Environment.ExpandEnvironmentVariables(folder.Trim().Trim('"'));
            if(!Directory.Exists(folder)) return null;
            return new Game {Id="battlenet:"+uid.ToLowerInvariant(),Source="battlenet",Name=name.Trim(),LaunchId=product,WorkingDirectory=folder};
        }
        public static List<Game> Scan() {
            var games=new List<Game>();
            foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser})
            foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(hive,view))
                using(var programs=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) {
                    if(programs==null) continue;
                    foreach(string name in programs.GetSubKeyNames()) try {
                        using(var key=programs.OpenSubKey(name)) {
                            if(key==null) continue;
                            var game=FromInstall(key.GetValue("DisplayName") as string,key.GetValue("InstallLocation") as string,key.GetValue("UninstallString") as string);
                            if(game!=null) games.Add(game);
                        }
                    } catch(System.Security.SecurityException) {} catch(UnauthorizedAccessException) {} catch(IOException) {}
                }
            } catch(System.Security.SecurityException) {} catch(UnauthorizedAccessException) {} catch(IOException) {}
            return games.GroupBy(game => game.Id).Select(group => group.First()).OrderBy(game => game.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static ProcessStartInfo StartInfo(Game game) {
            if(game.Source!="battlenet" || !Products.Values.Contains(game.LaunchId)) throw new InvalidOperationException("Unsupported Battle.net game ID.");
            if(!Directory.Exists(game.WorkingDirectory)) throw new DirectoryNotFoundException("The Battle.net game folder is missing.");
            string client=DetectClient();
            if(String.IsNullOrEmpty(client)) throw new FileNotFoundException("Battle.net.exe could not be found.");
            return new ProcessStartInfo(client,"--exec=\"launch "+game.LaunchId+"\"") {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(client)};
        }
    }
}
