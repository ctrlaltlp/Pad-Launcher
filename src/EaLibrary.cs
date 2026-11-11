using System;
using System.IO;
using System.Linq;
using System.Xml;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using Microsoft.Win32;

namespace PadLauncher {
    public class EaInstall {
        public string Folder;
        public string Name;
        public string Executable;
    }
    public static class EaLibrary {
        public static string DetectClient() {
            try {
                using(var key=Registry.ClassesRoot.OpenSubKey(@"origin2\shell\open\command")) {
                    string command=key==null ? "" : Convert.ToString(key.GetValue(""));
                    var match=Regex.Match(command,@"^\s*""?(.+?\.exe)""?(?:\s|$)",RegexOptions.IgnoreCase);
                    if(match.Success && File.Exists(match.Groups[1].Value)) return match.Groups[1].Value;
                }
            } catch {}
            foreach(string folder in new[]{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86)}) {
                string path=Path.Combine(folder,"Electronic Arts","EA Desktop","EA Desktop","EALauncher.exe"); if(File.Exists(path)) return path;
            }
            return null;
        }
        public static string ValidateClient(string value) {
            if(String.IsNullOrWhiteSpace(value)) return null;
            string path=Path.GetFullPath(Environment.ExpandEnvironmentVariables(value.Trim().Trim('"')));
            if(!File.Exists(path) || !Path.GetFileName(path).Equals("EALauncher.exe",StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Choose EALauncher.exe, or leave empty for automatic detection.");
            return path;
        }
        public static List<EaInstall> InstalledRecords() {
            var records=new List<EaInstall>();
            foreach(var hive in new[]{RegistryHive.LocalMachine,RegistryHive.CurrentUser}) foreach(var view in new[]{RegistryView.Registry32,RegistryView.Registry64}) try {
                using(var root=RegistryKey.OpenBaseKey(hive,view))
                using(var programs=root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall")) if(programs!=null) foreach(string name in programs.GetSubKeyNames()) try {
                    using(var key=programs.OpenSubKey(name)) {
                    if(key==null) continue;
                    string folder=key.GetValue("InstallLocation") as string;
                    if(String.IsNullOrWhiteSpace(folder) || !File.Exists(Path.Combine(folder,"__Installer","installerdata.xml"))) continue;
                    string icon=Convert.ToString(key.GetValue("DisplayIcon")); var match=Regex.Match(icon,@"^\s*""?(.+?\.exe)(?:""|,|$)",RegexOptions.IgnoreCase);
                    records.Add(new EaInstall {Folder=folder,Name=key.GetValue("DisplayName") as string,Executable=match.Success ? match.Groups[1].Value : null});
                    }
                } catch(UnauthorizedAccessException) {} catch(System.Security.SecurityException) {} catch(IOException) {}
            } catch {}
            var folders=new List<string> {Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"EA Games"),Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),"Origin Games")};
            string ini=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"EA Desktop","machine.ini");
            try {
                if(File.Exists(ini)) foreach(string line in File.ReadLines(ini)) {var match=Regex.Match(line,@"^\s*machine\.downloadinplacedir\s*=\s*(.+?)\s*$",RegexOptions.IgnoreCase); if(match.Success) folders.Add(Environment.ExpandEnvironmentVariables(match.Groups[1].Value.Trim('"')));}
            } catch {}
            foreach(string root in folders.Distinct(StringComparer.OrdinalIgnoreCase)) try {
                if(Directory.Exists(root)) foreach(string folder in Directory.GetDirectories(root)) if(File.Exists(Path.Combine(folder,"__Installer","installerdata.xml"))) records.Add(new EaInstall {Folder=folder});
            } catch {}
            return records;
        }
        public static bool ValidId(string id) {return !String.IsNullOrWhiteSpace(id) && Regex.IsMatch(id,@"^[A-Za-z0-9_.:-]+$");}
        public static List<Game> Scan(IEnumerable<EaInstall> records=null) {
            var result=new List<Game>();
            foreach(var record in records ?? InstalledRecords()) try {
                if(String.IsNullOrWhiteSpace(record.Folder) || !Directory.Exists(record.Folder)) continue;
                string folder=Path.GetFullPath(record.Folder).TrimEnd('\\','/'),prefix=folder+Path.DirectorySeparatorChar;
                string manifest=Path.Combine(folder,"__Installer","installerdata.xml"); if(!File.Exists(manifest)) continue;
                var document=new XmlDocument {XmlResolver=null};
                using(var reader=XmlReader.Create(manifest,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=8*1024*1024})) document.Load(reader);
                var ids=document.SelectNodes("/*[local-name()='game' or local-name()='DiPManifest']/*[local-name()='contentIDs']/*[local-name()='contentID']").Cast<XmlNode>().Select(node => node.InnerText.Trim()).Where(ValidId).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if(ids.Length==0 || result.Any(g => g.Source=="ea" && (g.LaunchId ?? "").Split(',').Intersect(ids,StringComparer.OrdinalIgnoreCase).Any())) continue;
                bool playable=false;
                if(!String.IsNullOrWhiteSpace(record.Executable)) {string path=Path.GetFullPath(record.Executable); playable=path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase) && File.Exists(path);}
                if(!playable) playable=Directory.GetFiles(folder,"*.exe",SearchOption.TopDirectoryOnly).Any(path => !Regex.IsMatch(Path.GetFileNameWithoutExtension(path),@"^(unins|uninstall|setup|touchup|cleanup|activation)",RegexOptions.IgnoreCase));
                if(!playable) {
                    foreach(string subfolder in new[]{ "bin", "Bin64", "Binaries/Win64", "Game/Bin", "Game/Bin64" }) {
                        string candidate=Path.Combine(folder,subfolder);
                        if(Directory.Exists(candidate) && Directory.GetFiles(candidate,"*.exe").Any(path => !Regex.IsMatch(Path.GetFileNameWithoutExtension(path),@"^(unins|setup|touchup|cleanup|activation|crash|eac|easyanticheat)",RegexOptions.IgnoreCase))) {playable=true; break;}
                    }
                }
                if(!playable) continue;
                string name=record.Name;
                if(String.IsNullOrWhiteSpace(name)) {
                    const string titles="//*[local-name()='localeInfo']/*[local-name()='title'] | /*[local-name()='DiPManifest']/*[local-name()='gameTitles']/*[local-name()='gameTitle']";
                    var localized=document.SelectNodes(titles).Cast<XmlNode>().ToList();
                    var title=localized.FirstOrDefault(node => node.SelectSingleNode("@locale[.='en_US'] | ../@locale[.='en_US']")!=null) ?? localized.FirstOrDefault();
                    name=title==null ? Path.GetFileName(folder) : title.InnerText;
                }
                if(String.IsNullOrWhiteSpace(name)) continue;
                result.Add(new Game {Id="ea:"+ids[0],Name=name.Trim(),Source="ea",LaunchId=String.Join(",",ids),WorkingDirectory=folder});
            } catch { /* Partial downloads and malformed manifests cannot hide other games. */ }
            return result.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static string UriFor(Game game) {
            string[] ids=(game.LaunchId ?? "").Split(',');
            if(game.Source!="ea" || ids.Length==0 || ids.Any(id => !ValidId(id))) throw new InvalidOperationException("This game's EA launch IDs are invalid. Refresh the library.");
            return "origin2://game/launch?offerIds="+Uri.EscapeDataString(String.Join(",",ids));
        }
        public static ProcessStartInfo StartInfo(Game game,string customPath=null) {
            string uri=UriFor(game);
            if(!Directory.Exists(game.WorkingDirectory)) throw new DirectoryNotFoundException("The EA game's installation folder is missing.");
            string client=String.IsNullOrWhiteSpace(customPath) ? DetectClient() : ValidateClient(customPath);
            return String.IsNullOrEmpty(client) ? new ProcessStartInfo(uri) {UseShellExecute=true} : new ProcessStartInfo(client,"\""+uri+"\"") {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(client)};
        }
    }
}
