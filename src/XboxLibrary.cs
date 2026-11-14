using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Microsoft.Win32;

namespace PadLauncher {
    public class XboxPackage {
        public string Name {get;set;}
        public string PackageFamilyName {get;set;}
        public string InstallLocation {get;set;}
    }
    public class XboxStartApp {
        public string Name {get;set;}
        public string AppID {get;set;}
    }
    public class XboxInventory {
        public List<XboxPackage> Packages {get;set;}
        public List<XboxStartApp> Apps {get;set;}
    }
    public static class XboxLibrary {
        const string PackageRegistry=@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages";
        public static string DetectClient() {
            if(TestEnvironment.FirstStartup) return null;
            try {using(var packages=Registry.CurrentUser.OpenSubKey(PackageRegistry)) {
                if(packages==null) return null;
                foreach(string name in packages.GetSubKeyNames().Where(name => name.StartsWith("Microsoft.GamingApp_",StringComparison.OrdinalIgnoreCase)))
                    using(var key=packages.OpenSubKey(name)) {
                        string path=key==null ? null : key.GetValue("PackageRootFolder") as string;
                        if(!String.IsNullOrEmpty(path) && Directory.Exists(path)) return path;
                    }
            }} catch(UnauthorizedAccessException) {} catch(System.Security.SecurityException) {} catch(IOException) {}
            return null;
        }
        static XmlDocument ReadXml(string path) {
            var document=new XmlDocument {XmlResolver=null};
            using(var reader=XmlReader.Create(path,new XmlReaderSettings {DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=4000000})) document.Load(reader);
            return document;
        }
        static string Attribute(XmlNode node,string name) {return node==null || node.Attributes==null || node.Attributes[name]==null ? "" : node.Attributes[name].Value;}
        static string Asset(string root,string relative) {
            if(String.IsNullOrWhiteSpace(relative) || relative.StartsWith("ms-resource:",StringComparison.OrdinalIgnoreCase)) return null;
            string path=Path.GetFullPath(Path.Combine(root,relative));
            if(!path.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) return null;
            return File.Exists(path) && ArtworkService.Portrait(path) ? path : null;
        }
        public static HashSet<string> CatalogGames(IEnumerable<string> products) {
            var families=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string value in products) try {
                var wrapper=Storage.Json().DeserializeObject(value) as Dictionary<string,object>;
                object dataValue,kindValue,idsValue;
                var data=wrapper!=null && wrapper.TryGetValue("data",out dataValue) ? dataValue as Dictionary<string,object> : null;
                if(data==null || !data.TryGetValue("productKind",out kindValue) || !String.Equals(kindValue as string,"GAME",StringComparison.OrdinalIgnoreCase) || !data.TryGetValue("alternateIds",out idsValue)) continue;
                var ids=idsValue as System.Collections.IEnumerable; if(ids==null) continue;
                foreach(object item in ids) {
                    var id=item as Dictionary<string,object>; object typeValue,familyValue;
                    if(id!=null && id.TryGetValue("idType",out typeValue) && String.Equals(typeValue as string,"PACKAGEFAMILYNAME",StringComparison.OrdinalIgnoreCase) && id.TryGetValue("id",out familyValue) && familyValue is string) families.Add((string)familyValue);
                }
            } catch(ArgumentException) {} catch(InvalidOperationException) {}
            return families;
        }
        public static List<Game> FromPackage(XboxPackage package,IDictionary<string,string> appNames,ISet<string> catalogGames=null) {
            var games=new List<Game>();
            if(package==null || !Regex.IsMatch(package.PackageFamilyName ?? "",@"^[A-Za-z0-9.\-]+_[A-Za-z0-9]+$") || !Directory.Exists(package.InstallLocation)) return games;
            if(new[]{"Microsoft.GamingApp_","Microsoft.GamingServices_","Microsoft.XboxIdentityProvider_","Microsoft.XboxGamingOverlay_","Microsoft.XboxApp_","Microsoft.Xbox.TCUI_"}.Any(prefix => package.PackageFamilyName.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))) return games;
            string manifest=Path.Combine(package.InstallLocation,"AppxManifest.xml");
            if(!File.Exists(manifest)) return games;
            var document=ReadXml(manifest);
            string configFile=Path.Combine(package.InstallLocation,"MicrosoftGame.config");
            var config=File.Exists(configFile) ? ReadXml(configFile) : null;
            bool gamingDependency=document.SelectNodes("//*[local-name()='PackageDependency']").Cast<XmlNode>().Any(node => Attribute(node,"Name").IndexOf("GamingServices",StringComparison.OrdinalIgnoreCase)>=0);
            bool xboxProtocol=document.SelectNodes("//*[local-name()='Protocol']").Cast<XmlNode>().Any(node => Attribute(node,"Name").StartsWith("ms-xbl-",StringComparison.OrdinalIgnoreCase));
            if(config==null && !gamingDependency && !xboxProtocol && (catalogGames==null || !catalogGames.Contains(package.PackageFamilyName))) return games;
            foreach(XmlNode app in document.SelectNodes("/*[local-name()='Package']/*[local-name()='Applications']/*[local-name()='Application']")) {
                string id=Attribute(app,"Id"),aumid=package.PackageFamilyName+"!"+id,name;
                if(!ValidAumid(aumid) || !appNames.TryGetValue(aumid,out name) || String.IsNullOrWhiteSpace(name)) continue;
                XmlNode visuals=app.SelectSingleNode("*[local-name()='VisualElements']");
                if(Attribute(visuals,"AppListEntry").Equals("none",StringComparison.OrdinalIgnoreCase)) continue;
                XmlNode executable=config==null ? null : config.SelectNodes("//*[local-name()='Executable']").Cast<XmlNode>().FirstOrDefault(node => Attribute(node,"Id")==id);
                if(executable!=null && Attribute(executable,"IsDevOnly").Equals("true",StringComparison.OrdinalIgnoreCase)) continue;
                string art=null;
                XmlNode shell=config==null ? null : config.SelectSingleNode("//*[local-name()='ShellVisuals']");
                if(shell!=null) art=Asset(package.InstallLocation,Attribute(shell,"StoreLogo"));
                if(art==null && visuals!=null) {
                    XmlNode tile=visuals.SelectSingleNode("*[local-name()='DefaultTile']");
                    art=Asset(package.InstallLocation,Attribute(tile,"Wide310x150Logo"));
                }
                games.Add(new Game {Id="xbox:"+aumid.ToLowerInvariant(),Source="xbox",Name=name.Trim(),LaunchId=aumid,WorkingDirectory=package.InstallLocation,Art=art});
            }
            return games;
        }
        public static bool ValidAumid(string value) {return Regex.IsMatch(value ?? "",@"^[A-Za-z0-9.\-]+_[A-Za-z0-9]+![A-Za-z0-9.\-]+$");}
        public static List<Game> Scan(List<string> warnings=null) {
            // Current-user registrations only: no administrator rights, drive scans, or writes to Xbox files.
            const string script="$ErrorActionPreference='Stop'; [Console]::OutputEncoding=[Text.Encoding]::UTF8; $p=@(Get-AppxPackage -PackageTypeFilter Main | Where-Object { -not $_.IsFramework -and -not $_.IsResourcePackage } | Select-Object Name,PackageFamilyName,InstallLocation); $a=@(Get-StartApps | Select-Object Name,AppID); ConvertTo-Json -Compress -Depth 4 -InputObject @{Packages=$p; Apps=$a}";
            string powershell=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe");
            var info=new ProcessStartInfo(powershell,"-NoLogo -NoProfile -NonInteractive -EncodedCommand "+Convert.ToBase64String(Encoding.Unicode.GetBytes(script))) {UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,StandardOutputEncoding=Encoding.UTF8};
            XboxInventory inventory;
            using(var process=Process.Start(info)) {
                var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
                if(!process.WaitForExit(15000)) {process.Kill(); throw new IOException("Windows game detection timed out. Try refreshing again.");}
                if(process.ExitCode!=0) throw new IOException("Windows could not read installed Xbox games. "+error.Result.Trim());
                inventory=Storage.Json().Deserialize<XboxInventory>(output.Result.TrimStart('\uFEFF'));
            }
            if(inventory==null) throw new IOException("Windows returned no game inventory.");
            var names=(inventory.Apps ?? new List<XboxStartApp>()).Where(app => !String.IsNullOrEmpty(app.AppID)).GroupBy(app => app.AppID,StringComparer.OrdinalIgnoreCase).ToDictionary(group => group.Key,group => group.First().Name,StringComparer.OrdinalIgnoreCase);
            var catalogGames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string catalog=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),@"Packages\Microsoft.GamingApp_8wekyb3d8bbwe\LocalState\AsyncCache.db");
            if(File.Exists(catalog)) try {catalogGames=CatalogGames(EmulatorSqlite.ReadXboxProducts(catalog));}
                catch(IOException error) {if(warnings!=null) warnings.Add("Xbox catalog: "+error.Message);}
                catch(UnauthorizedAccessException error) {if(warnings!=null) warnings.Add("Xbox catalog: "+error.Message);}
            var games=new List<Game>();
            foreach(var package in inventory.Packages ?? new List<XboxPackage>()) try {games.AddRange(FromPackage(package,names,catalogGames));}
                catch(IOException error) {if(warnings!=null) warnings.Add("Xbox: "+package.Name+": "+error.Message);}
                catch(UnauthorizedAccessException error) {if(warnings!=null) warnings.Add("Xbox: "+package.Name+": "+error.Message);}
                catch(XmlException error) {if(warnings!=null) warnings.Add("Xbox: "+package.Name+": "+error.Message);}
                catch(ArgumentException error) {if(warnings!=null) warnings.Add("Xbox: "+package.Name+": "+error.Message);}
            return games.GroupBy(game => game.Id).Select(group => group.First()).OrderBy(game => game.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static ProcessStartInfo StartInfo(Game game) {
            if(game.Source!="xbox" || !ValidAumid(game.LaunchId)) throw new InvalidOperationException("Invalid Xbox game registration. Refresh the library.");
            return new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"explorer.exe"),"shell:AppsFolder\\"+game.LaunchId) {UseShellExecute=true};
        }
    }
}
