using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;

namespace PadLauncher {
    public class SteamShortcutRecord {
        public string Key;
        public string Name;
        public string Target;
        public string Arguments;
        public string WorkingDirectory;
        public string SteamLaunchId;
        public string Artwork;
        public bool CanLaunchDirect;
        public string DirectWarning;
    }
    public static class SteamShortcutImport {
        public static List<SteamShortcutRecord> Read(Settings settings) {
            string root=String.IsNullOrWhiteSpace(settings.SteamPath) ? SteamLibrary.DetectSteam() : settings.SteamPath;
            var library=SteamLibrary.Scan(root,settings.UserId);
            if(String.IsNullOrEmpty(library.UserId)) throw new IOException("Choose a Steam user in Launchers settings first.");
            string config=Path.Combine(root,"userdata",library.UserId,"config"),file=Path.Combine(config,"shortcuts.vdf");
            if(!File.Exists(file)) return new List<SteamShortcutRecord>();
            var result=new List<SteamShortcutRecord>();
            foreach(var value in Vdf.Map(Vdf.Get(Vdf.Binary(file),"shortcuts")).Values) {
                var record=Parse(value,library.UserId);
                if(record==null || result.Any(r => r.Key==record.Key)) continue;
                string id=record.Key.Split(':')[1]; var existing=library.Games.FirstOrDefault(g => g.Shortcut && g.Id==id);
                record.Artwork=existing==null ? null : existing.Art;
                if(String.IsNullOrEmpty(record.Artwork)) foreach(string imageId in new[]{id,record.SteamLaunchId}) {
                    foreach(string extension in new[]{".png",".jpg",".jpeg"}) {
                        string path=Path.Combine(config,"grid",imageId+"p"+extension); if(File.Exists(path)) {record.Artwork=path; break;}
                    }
                    if(record.Artwork!=null) break;
                }
                result.Add(record);
            }
            return result.OrderBy(r => r.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
        }
        public static SteamShortcutRecord Parse(object value,string user) {
            string name=Vdf.Str(value,"AppName"); ulong userId;
            if(String.IsNullOrWhiteSpace(name) || !UInt64.TryParse(user,out userId)) return null;
            string raw=Vdf.Str(value,"exe").Trim(),start=Environment.ExpandEnvironmentVariables(Vdf.Str(value,"StartDir").Trim().Trim('"')),tail="",target=raw;
            if(raw.StartsWith("\"")) {int end=raw.IndexOf('"',1); if(end>0) {target=raw.Substring(1,end-1); tail=raw.Substring(end+1).Trim();}}
            else if(!File.Exists(raw)) {
                var match=Regex.Match(raw,@"^(.+?\.(?:exe|lnk|bat|cmd))(?=\s|$)(.*)$",RegexOptions.IgnoreCase);
                if(match.Success) {target=match.Groups[1].Value; tail=match.Groups[2].Value.Trim();}
            }
            string resolved=SteamLibrary.ShortcutTargetPath("\""+target+"\"",start);
            string options=Vdf.Str(value,"LaunchOptions").Trim(),warning=null;
            if(options.StartsWith("%command%",StringComparison.OrdinalIgnoreCase)) options=options.Substring(9).TrimStart();
            if(options.IndexOf("%command%",StringComparison.OrdinalIgnoreCase)>=0) warning="Launch options use a wrapper. Launch through Steam to preserve them.";
            string directory=start;
            if(String.IsNullOrEmpty(directory) && !String.IsNullOrEmpty(resolved)) directory=Path.GetDirectoryName(resolved);
            string extension=String.IsNullOrEmpty(resolved) ? "" : Path.GetExtension(resolved).ToLowerInvariant();
            if(String.IsNullOrEmpty(resolved) || !File.Exists(resolved) || !new[]{".exe",".lnk",".bat",".cmd"}.Contains(extension)) warning=warning ?? "Direct target is unavailable or unsupported. Use Steam.";
            if(!String.IsNullOrEmpty(directory) && !Directory.Exists(directory)) warning=warning ?? "Working folder is missing. Use Steam or fix the shortcut.";
            uint id; if(!UInt32.TryParse(Vdf.Str(value,"appid"),out id)) id=SteamLibrary.ShortcutId(raw,name);
            return new SteamShortcutRecord {Key=user+":"+id,Name=name.Trim(),Target=resolved ?? target,Arguments=String.Join(" ",new[]{tail,options}.Where(s => !String.IsNullOrWhiteSpace(s))),WorkingDirectory=directory,SteamLaunchId=SteamLibrary.ShortcutLaunchId(id),CanLaunchDirect=warning==null,DirectWarning=warning};
        }
        public static List<CustomGame> PrepareModes(SteamShortcutRecord record,string mode) {
            if(mode!="direct" && mode!="steam" && mode!="both") throw new ArgumentException("Choose a launch mode.");
            var result=new List<CustomGame>();
            if(mode!="steam") result.Add(Prepare(record,false));
            if(mode!="direct") result.Add(Prepare(record,true));
            return result;
        }
        public static void SelectLaunchModes(Settings settings,List<PlayniteImport.Conflict> conflicts) {
            foreach(var conflict in conflicts) foreach(var shortcuts in conflict.Alternatives.Where(a => a.Incoming!=null).GroupBy(a => a.Incoming.SteamShortcutKey)) {
                bool keepSteam=shortcuts.Any(a => a.Incoming.LaunchViaSteam);
                string launchId=shortcuts.First().Incoming.SteamLaunchId;
                var native=conflict.Alternatives.Where(a => a.Existing!=null && a.Existing.Shortcut && a.Existing.LaunchId==launchId).ToList();
                foreach(var incoming in shortcuts) incoming.Keep=!incoming.Incoming.LaunchViaSteam || native.Count==0;
                foreach(var existing in native) existing.Keep=keepSteam;
                foreach(var existing in conflict.Alternatives.Where(a => a.Existing!=null && a.Existing.Source=="custom")) {
                    var custom=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(c => c.Id==existing.Existing.Id);
                    if(custom!=null && custom.SteamShortcutKey==shortcuts.Key)
                        existing.Keep=shortcuts.Any(a => a==existing && (!custom.LaunchViaSteam || native.Count==0));
                }
            }
        }
        public static void PreserveLists(Settings settings,Library before,List<CustomGame> prepared,Dictionary<string,string> destinations) {
            var lists=CombinedCollections.Options(before,settings);
            foreach(var group in prepared.GroupBy(g => g.SteamShortcutKey)) {
                string destination; destinations.TryGetValue(group.Key,out destination);
                var sourceIds=new HashSet<string>(before.Games.Where(g => (g.Shortcut && g.LaunchId==group.First().SteamLaunchId)
                    || (g.Source=="custom" && (g.SteamLaunchId==group.First().SteamLaunchId || (g.LaunchId==group.First().Target && g.Arguments==group.First().Arguments)))
                    || (settings.CustomGames ?? new List<CustomGame>()).Any(c => c.Id==g.Id && c.SteamShortcutKey==group.Key)).Select(g => g.Id));
                var retained=group.Where(g => !String.IsNullOrEmpty(g.Id) && (settings.CustomGames ?? new List<CustomGame>()).Any(c => c.Id==g.Id)).Select(g => g.Id).ToList();
                if(retained.Count==0 && !String.IsNullOrEmpty(destination)) retained.AddRange(before.Games.Where(g => sourceIds.Contains(g.Id) && !(settings.ReplacedGameIds ?? new List<string>()).Contains(g.Id)).Select(g => g.Id));
                var targets=String.IsNullOrEmpty(destination) ? lists.Where(l => l.Id!="all" && l.Id!="recent" && !l.Id.StartsWith("source:") && (l.Games ?? new List<string>()).Any(sourceIds.Contains)).Select(l => l.Id) : new[]{destination};
                foreach(string id in targets) {
                    var custom=(settings.CustomLists ?? new List<CustomList>()).FirstOrDefault(l => l.Id==id);
                    if(custom!=null) {custom.Games=(custom.Games ?? new List<string>()).Concat(retained).Distinct().ToList(); continue;}
                    var combined=(settings.CombinedCollections ?? new List<CombinedCollection>()).FirstOrDefault(l => l.Id==id);
                    if(combined!=null) {combined.AddedGames=(combined.AddedGames ?? new List<string>()).Concat(retained).Distinct().ToList(); combined.ExcludedGames=(combined.ExcludedGames ?? new List<string>()).Except(retained).ToList(); continue;}
                    if(settings.ListGameAdditions==null) settings.ListGameAdditions=new Dictionary<string,List<string>>();
                    List<string> additions;
                    if(!settings.ListGameAdditions.TryGetValue(id,out additions)) settings.ListGameAdditions[id]=additions=new List<string>();
                    foreach(string game in retained) if(!additions.Contains(game)) additions.Add(game);
                }
            }
        }
        public static string ExistingStatus(Settings settings,Library library,SteamShortcutRecord record,bool throughSteam) {
            var custom=settings.CustomGames ?? new List<CustomGame>();
            bool imported=custom.Any(g => g.LaunchViaSteam==throughSteam && (throughSteam
                ? g.SteamLaunchId==record.SteamLaunchId
                : PlayniteImport.ActionKey(g.Target,g.Arguments,g.WorkingDirectory)==PlayniteImport.ActionKey(record.Target,record.Arguments,record.WorkingDirectory)));
            if(imported) return throughSteam ? "already added through Steam" : "already added for direct launch";
            bool steam=library.Games.Any(g => g.Shortcut && g.LaunchId==record.SteamLaunchId);
            if(throughSteam && steam) return "already available through Steam";
            return !throughSteam && (steam || custom.Any(g => g.SteamLaunchId==record.SteamLaunchId && g.LaunchViaSteam))
                ? "available through Steam; direct launch not added" : "";
        }
        public static CustomGame Prepare(SteamShortcutRecord record,bool throughSteam) {
            if(!Regex.IsMatch(record.Key ?? "",@"^\d+:\d+$")) throw new ArgumentException("Invalid Steam shortcut import identity.");
            if(!throughSteam && !record.CanLaunchDirect) throw new ArgumentException(record.Name+": "+record.DirectWarning);
            string art=null;
            if(!String.IsNullOrEmpty(record.Artwork) && File.Exists(record.Artwork)) {
                try {
                using(var image=System.Drawing.Image.FromFile(record.Artwork)) using(var encoded=new MemoryStream()) {
                    image.Save(encoded,System.Drawing.Imaging.ImageFormat.Png); byte[] bytes=encoded.ToArray(); string hash;
                    using(var sha=System.Security.Cryptography.SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
                    art=Path.Combine("artwork","steam-import",record.Key.Replace(':','-')+"-"+hash.Substring(0,16)+".png");
                    string path=Path.Combine(Storage.Root,art); Directory.CreateDirectory(Path.GetDirectoryName(path)); if(!File.Exists(path)) File.WriteAllBytes(path,bytes);
                }
                } catch {art=null;}
            }
            var game=new CustomGame {Name=record.Name,Target=record.Target,Arguments=record.Arguments,WorkingDirectory=record.WorkingDirectory,Artwork=art,SteamShortcutKey=record.Key,SteamLaunchId=record.SteamLaunchId,LaunchViaSteam=throughSteam};
            game.PlayniteActionInfo=Describe(game); return game;
        }
        public static string Describe(CustomGame game) {return game.LaunchViaSteam ? "Launch through Steam: steam://rungameid/"+game.SteamLaunchId : "Direct launch (without Steam)";}
        public static void Merge(Settings settings,CustomGame game) {
            if(!Regex.IsMatch(game.SteamShortcutKey ?? "",@"^\d+:\d+$")) throw new ArgumentException("Invalid Steam shortcut import identity.");
            var existing=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.SteamShortcutKey==game.SteamShortcutKey && g.LaunchViaSteam==game.LaunchViaSteam);
            game.Id=existing==null ? null : existing.Id; CustomGameLibrary.Save(settings,game);
        }
    }
}
