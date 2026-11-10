using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;

namespace PadLauncher {
    public class PlayniteGame {
        public string Id {get;set;}
        public string Name {get;set;}
        public string Cover {get;set;}
        public string PluginId {get;set;}
        public string NativeId {get;set;}
        public string ActionDescription {get;set;}
        public bool Installed {get;set;}
        public bool Hidden {get;set;}
    }
    public static class PlayniteImport {
        public class Alternative {
            public Game Existing;
            public CustomGame Incoming;
            public bool Keep;
        }
        public class Conflict {
            public string Name;
            public List<Alternative> Alternatives=new List<Alternative>();
        }
        public static string ActionKey(string target,string arguments,string folder,string details=null) {
            // Windows file paths are case-insensitive; arguments remain case-sensitive.
            return (target ?? "").Trim().ToUpperInvariant()+"\n"+(arguments ?? "").Trim()+"\n"+(folder ?? "").Trim().ToUpperInvariant()+"\n"+(details ?? "");
        }
        public static List<Conflict> Conflicts(Settings settings,Library library,List<CustomGame> incoming) {
            var conflicts=new List<Conflict>();
            foreach(var names in incoming.GroupBy(g => ArtworkService.Normalize(g.Name))) {
                var group=new Conflict {Name=names.First().Name};
                group.Alternatives.AddRange(library.Games.Where(g => ArtworkService.Normalize(g.Name)==names.Key).Select(g => new Alternative {Existing=g,Keep=true}));
                foreach(var game in names) {
                    var previous=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.PlayniteId==game.PlayniteId);
                    var identical=previous==null ? null : group.Alternatives.FirstOrDefault(a => a.Existing!=null && a.Existing.Id==previous.Id && ActionKey(a.Existing.LaunchId,a.Existing.Arguments,a.Existing.WorkingDirectory,a.Existing.ImportedActionInfo)==ActionKey(game.Target,game.Arguments,game.WorkingDirectory,game.PlayniteActionInfo));
                    if(identical!=null) identical.Incoming=game;
                    else group.Alternatives.Add(new Alternative {Incoming=game,Keep=group.Alternatives.Count==0});
                }
                if(group.Alternatives.Any(a => a.Existing==null) && group.Alternatives.Select(a => a.Existing!=null ? ActionKey(a.Existing.LaunchId,a.Existing.Arguments,a.Existing.WorkingDirectory,a.Existing.ImportedActionInfo) : ActionKey(a.Incoming.Target,a.Incoming.Arguments,a.Incoming.WorkingDirectory,a.Incoming.PlayniteActionInfo)).Distinct().Count()>1) conflicts.Add(group);
            }
            return conflicts;
        }
        public static int ApplyChoices(Settings settings,List<CustomGame> prepared,List<Conflict> conflicts) {
            var excluded=new HashSet<CustomGame>();
            foreach(var group in conflicts) {
                if(!group.Alternatives.Any(a => a.Keep)) throw new ArgumentException("Keep at least one entry for "+group.Name+".");
                foreach(var choice in group.Alternatives) {
                    if(choice.Incoming!=null && !choice.Keep) excluded.Add(choice.Incoming);
                    if(choice.Existing==null || choice.Keep) continue;
                    if(choice.Existing.Source=="custom") {if(settings.CustomGames!=null) settings.CustomGames.RemoveAll(g => g.Id==choice.Existing.Id);}
                    else {if(settings.ReplacedGameIds==null) settings.ReplacedGameIds=new List<string>(); if(!settings.ReplacedGameIds.Contains(choice.Existing.Id)) settings.ReplacedGameIds.Add(choice.Existing.Id);}
                }
                foreach(var kept in group.Alternatives.Where(a => a.Keep && a.Existing!=null)) {
                    var custom=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.Id==kept.Existing.Id);
                    if(custom!=null && group.Alternatives.Any(a => a.Keep && a.Existing==null && a.Incoming.PlayniteId==custom.PlayniteId)) custom.PlayniteId=null;
                }
            }
            int count=0; foreach(var game in prepared.Where(g => !excluded.Contains(g))) {Merge(settings,game); count++;} return count;
        }
        public static string LibraryFolder(string folder) {
            string config=Path.Combine(folder,"config.json");
            if(File.Exists(config)) {
                string path=Vdf.Str(Storage.Json().DeserializeObject(File.ReadAllText(config)),"DatabasePath");
                if(!String.IsNullOrWhiteSpace(path)) return Path.GetFullPath(Path.Combine(folder,Environment.ExpandEnvironmentVariables(path.Replace("{PlayniteDir}",folder))));
            }
            string portable=Path.Combine(folder,"library");
            if(File.Exists(Path.Combine(portable,"games.db"))) return portable;
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Playnite","library");
        }
        public static List<PlayniteGame> Read(string folder,string library) {
            if(!File.Exists(Path.Combine(folder,"Playnite.DesktopApp.exe"))) throw new IOException("Choose the folder containing Playnite.DesktopApp.exe.");
            string dll=Path.Combine(folder,"LiteDB.dll"),file=Path.Combine(library,"games.db");
            if(!File.Exists(dll) || !File.Exists(file)) throw new IOException("Playnite's database or LiteDB.dll could not be found. Check both folders.");
            if(AssemblyName.GetAssemblyName(dll).Version.Major!=4) throw new IOException("This Playnite database version is not supported yet (LiteDB 4 is required).");
            // Work on an in-memory snapshot. Never open the original database for writing.
            var before=new FileInfo(file); long length=before.Length,ticks=before.LastWriteTimeUtc.Ticks;
            if(length>512L*1024*1024) throw new IOException("The Playnite database is too large to import safely.");
            using(var snapshot=new MemoryStream()) {
                using(var input=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite)) input.CopyTo(snapshot);
                var after=new FileInfo(file);
                if(after.Length!=length || after.LastWriteTimeUtc.Ticks!=ticks) throw new IOException("Playnite's library changed while reading it. Wait for its update to finish, then try again.");
                snapshot.Position=0;
                var assembly=Assembly.LoadFrom(dll); var type=assembly.GetType("LiteDB.LiteDatabase",true);
                using(var database=(IDisposable)Activator.CreateInstance(type,new object[]{snapshot,null,null,false})) {
                    var names=((IEnumerable)type.GetMethod("GetCollectionNames").Invoke(database,null)).Cast<object>().Select(Convert.ToString).ToArray();
                    string name=names.FirstOrDefault(n => n.Equals("Game",StringComparison.OrdinalIgnoreCase) || n.Equals("Games",StringComparison.OrdinalIgnoreCase));
                    if(name==null) return new List<PlayniteGame>();
                    var collection=type.GetMethods().Single(m => m.Name=="GetCollection" && !m.IsGenericMethod && m.GetParameters().Length==1).Invoke(database,new object[]{name});
                    var documents=(IEnumerable)collection.GetType().GetMethod("FindAll").Invoke(collection,null);
                    var serialize=assembly.GetType("LiteDB.JsonSerializer",true).GetMethods().Single(m => m.Name=="Serialize" && m.ReturnType==typeof(string));
                    var result=new List<PlayniteGame>();
                    foreach(var document in documents) {
                        object value=Storage.Json().DeserializeObject((string)serialize.Invoke(null,new object[]{document,false,false}));
                        var game=Parse(value,library); if(game!=null && !result.Any(g => g.Id==game.Id)) result.Add(game);
                    }
                    return result.OrderBy(g => g.Name,StringComparer.CurrentCultureIgnoreCase).ToList();
                }
            }
        }
        static string GuidValue(object value) {return value is string ? (string)value : Vdf.Str(value,"$guid");}
        public static PlayniteGame Parse(object value,string library) {
            Guid id; string name=Vdf.Str(value,"Name");
            if(!Guid.TryParse(GuidValue(Vdf.Get(value,"_id")),out id) || id==Guid.Empty || String.IsNullOrWhiteSpace(name)) return null;
            string cover=Vdf.Str(value,"CoverImage"),resolved=null;
            if(!String.IsNullOrWhiteSpace(cover)) {
                // Only local media inside Playnite's library is copied.
                string root=Path.GetFullPath(Path.Combine(library,"files"))+Path.DirectorySeparatorChar;
                try {
                    string candidate=Path.GetFullPath(Path.Combine(root,cover));
                    if(candidate.StartsWith(root,StringComparison.OrdinalIgnoreCase) && File.Exists(candidate)) resolved=candidate;
                } catch { /* Invalid or remote artwork is left for artwork fallback. */ }
            }
            var descriptions=new List<string>();
            foreach(var action in EpicLibrary.Entries(Vdf.Get(value,"GameActions"))) {
                descriptions.Add(Vdf.Str(action,"Name")+" [type "+Vdf.Str(action,"Type")+"]\n"+Vdf.Str(action,"Path")+" "+Vdf.Str(action,"Arguments")+(String.IsNullOrWhiteSpace(Vdf.Str(action,"WorkingDir")) ? "" : "\nFolder: "+Vdf.Str(action,"WorkingDir")));
            }
            if(Vdf.Str(value,"IncludeLibraryPluginAction").Equals("true",StringComparison.OrdinalIgnoreCase)) descriptions.Add("Library plugin: "+GuidValue(Vdf.Get(value,"PluginId"))+"\nGame ID: "+Vdf.Str(value,"GameId"));
            return new PlayniteGame {Id=id.ToString(),Name=name.Trim(),Cover=resolved,Installed=Vdf.Str(value,"IsInstalled").Equals("true",StringComparison.OrdinalIgnoreCase),Hidden=Vdf.Str(value,"Hidden").Equals("true",StringComparison.OrdinalIgnoreCase),NativeId=Vdf.Str(value,"GameId"),PluginId=GuidValue(Vdf.Get(value,"PluginId")),ActionDescription=String.Join("\n\n",descriptions)};
        }
        public static bool NativeDuplicate(PlayniteGame game,Library library) {
            if(String.Equals(game.PluginId,"cb91dfc9-b977-43bf-8e70-55f46e410fab",StringComparison.OrdinalIgnoreCase)) return library.Games.Any(g => (String.IsNullOrEmpty(g.Source) || g.Source=="steam") && g.Id==game.NativeId);
            if(String.Equals(game.PluginId,"00000002-dbd1-46c6-b5d0-b1ba559d10e4",StringComparison.OrdinalIgnoreCase)) return library.Games.Any(g => g.Source=="epic" && (g.LaunchId ?? "").EndsWith(":"+game.NativeId,StringComparison.OrdinalIgnoreCase));
            if(String.Equals(game.PluginId,"aebe8b7c-6dc3-4a66-af31-e7375c6b5e9e",StringComparison.OrdinalIgnoreCase)) return library.Games.Any(g => g.Source=="gog" && g.LaunchId==game.NativeId);
            return false;
        }
        public static CustomGame Prepare(PlayniteGame record,string folder) {
            Guid id; if(!Guid.TryParse(record.Id,out id)) throw new ArgumentException("Invalid Playnite game ID.");
            string art=null;
            if(!String.IsNullOrEmpty(record.Cover) && File.Exists(record.Cover)) {
                try {
                    using(var image=System.Drawing.Image.FromFile(record.Cover)) {
                        using(var encoded=new MemoryStream()) {
                            image.Save(encoded,System.Drawing.Imaging.ImageFormat.Png); byte[] bytes=encoded.ToArray(); string hash;
                            using(var sha=System.Security.Cryptography.SHA256.Create()) hash=BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","");
                            art=Path.Combine("artwork","playnite",id.ToString("N")+"-"+hash.Substring(0,16)+".png");
                            string path=Path.Combine(Storage.Root,art); Directory.CreateDirectory(Path.GetDirectoryName(path)); if(!File.Exists(path)) File.WriteAllBytes(path,bytes);
                        }
                    }
                } catch {art=null;}
            }
            return new CustomGame {Name=record.Name,Target=Path.Combine(folder,"Playnite.DesktopApp.exe"),Arguments="--start "+id+" --hidesplashscreen --nolibupdate",WorkingDirectory=folder,Artwork=art,PlayniteId=id.ToString(),PlayniteActionInfo=record.ActionDescription};
        }
        public static void Merge(Settings settings,CustomGame game) {
            Guid id; if(!Guid.TryParse(game.PlayniteId,out id) || id==Guid.Empty) throw new ArgumentException("Invalid Playnite import identity.");
            game.PlayniteId=id.ToString();
            var existing=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => String.Equals(g.PlayniteId,game.PlayniteId,StringComparison.OrdinalIgnoreCase));
            game.Id=existing==null ? null : existing.Id;
            CustomGameLibrary.Save(settings,game);
        }
    }
}
