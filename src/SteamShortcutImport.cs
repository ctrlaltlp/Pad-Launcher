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
            if(String.IsNullOrEmpty(library.UserId)) throw new IOException("Choose a Steam user in Integrations settings first.");
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
            var existing=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.SteamShortcutKey==game.SteamShortcutKey);
            game.Id=existing==null ? null : existing.Id; CustomGameLibrary.Save(settings,game);
        }
    }
}
