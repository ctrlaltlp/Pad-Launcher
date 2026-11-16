using System;
using System.Linq;
using System.Collections.Generic;

namespace PadLauncher {
    public static class GameImport {
        public class Alternative {
            public Game Existing;
            public CustomGame Incoming;
            public bool Keep;
        }
        public class Conflict {
            public string Name;
            public List<Alternative> Alternatives=new List<Alternative>();
        }
        public static string ImportIdentity(CustomGame game) {
            if(!String.IsNullOrEmpty(game.SteamShortcutKey)) return "steam:"+game.SteamShortcutKey+(game.LaunchViaSteam ? ":steam" : ":direct");
            return null;
        }
        public static string ActionKey(string target,string arguments,string folder,string details=null) {
            // Direct launches default to the executable's directory when no folder was saved.
            if(String.IsNullOrWhiteSpace(folder) && !String.IsNullOrWhiteSpace(target) && System.IO.Path.IsPathRooted(target)) {
                try {folder=System.IO.Path.GetDirectoryName(target);} catch(ArgumentException) { }
            }
            // Windows file paths are case-insensitive; arguments remain case-sensitive.
            return (target ?? "").Trim().ToUpperInvariant()+"\n"+(arguments ?? "").Trim()+"\n"+(folder ?? "").Trim().TrimEnd('\\','/').ToUpperInvariant()+"\n"+(details ?? "");
        }
        static string EmulatorForTarget(Settings settings,string target) {
            var emulator=(settings.Emulators ?? new List<EmulatorConfig>()).FirstOrDefault(config =>
                !String.IsNullOrWhiteSpace(config.Path) && String.Equals((target ?? "").Trim().Trim('"'),config.Path.Trim().Trim('"'),StringComparison.OrdinalIgnoreCase));
            return emulator==null ? "" : emulator.Id;
        }
        static string EmulatorForGame(Settings settings,Game game) {
            if(!String.IsNullOrEmpty(game.EmulatorId)) return game.EmulatorId;
            if(game.Source=="emulator") return "unknown-emulator";
            var custom=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.Id==game.Id);
            return EmulatorForTarget(settings,custom!=null ? custom.Target : game.Shortcut ? game.ShortcutTarget : game.LaunchId);
        }
        public static List<Conflict> Conflicts(Settings settings,Library library,List<CustomGame> incoming) {
            var conflicts=new List<Conflict>();
            foreach(var names in incoming.GroupBy(g => new {Name=ArtworkService.Normalize(g.Name),Emulator=EmulatorForTarget(settings,g.Target)})) {
                var group=new Conflict {Name=names.First().Name};
                bool matchedManual=false;
                group.Alternatives.AddRange(library.Games.Where(g => ArtworkService.Normalize(g.Name)==names.Key.Name &&
                    EmulatorForGame(settings,g)==names.Key.Emulator).Select(g => new Alternative {Existing=g,Keep=true}));
                foreach(var game in names) {
                    string identity=ImportIdentity(game);
                    var previous=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => identity!=null && ImportIdentity(g)==identity);
                    bool manualMatch=false;
                    if(previous==null && identity!=null && !game.LaunchViaSteam) {previous=SteamShortcutImport.FindDirectMatch(settings,game); manualMatch=previous!=null;}
                    var identical=previous==null ? null : group.Alternatives.FirstOrDefault(a => a.Existing!=null && a.Existing.Id==previous.Id && ActionKey(a.Existing.LaunchId,a.Existing.Arguments,a.Existing.WorkingDirectory,a.Existing.ImportedActionInfo)==ActionKey(game.Target,game.Arguments,game.WorkingDirectory,game.PlayniteActionInfo));
                    if(manualMatch) identical=group.Alternatives.FirstOrDefault(a => a.Existing!=null && a.Existing.Id==previous.Id);
                    matchedManual|=manualMatch;
                    if(identical!=null) identical.Incoming=game;
                    else group.Alternatives.Add(new Alternative {Incoming=game,Keep=group.Alternatives.Count==0});
                }
                if((matchedManual || group.Alternatives.Any(a => a.Existing==null)) && group.Alternatives.Select(a => a.Existing!=null ? ActionKey(a.Existing.LaunchId,a.Existing.Arguments,a.Existing.WorkingDirectory,a.Existing.ImportedActionInfo) : ActionKey(a.Incoming.Target,a.Incoming.Arguments,a.Incoming.WorkingDirectory,a.Incoming.PlayniteActionInfo)).Distinct().Count()>1) conflicts.Add(group);
            }
            return conflicts;
        }
        public static int ApplyChoices(Settings settings,List<CustomGame> prepared,List<Conflict> conflicts) {
            var excluded=new HashSet<CustomGame>();
            foreach(var group in conflicts) {

                foreach(var choice in group.Alternatives) {
                    if(choice.Incoming!=null && !choice.Keep) excluded.Add(choice.Incoming);
                    if(choice.Existing==null || choice.Keep) continue;
                    if(choice.Existing.Source=="custom") {if(settings.CustomGames!=null) settings.CustomGames.RemoveAll(g => g.Id==choice.Existing.Id);}
                    else {if(settings.ReplacedGameIds==null) settings.ReplacedGameIds=new List<string>(); if(!settings.ReplacedGameIds.Contains(choice.Existing.Id)) settings.ReplacedGameIds.Add(choice.Existing.Id);}
                }
                foreach(var kept in group.Alternatives.Where(a => a.Keep && a.Existing!=null)) {
                    var custom=(settings.CustomGames ?? new List<CustomGame>()).FirstOrDefault(g => g.Id==kept.Existing.Id);
                    if(custom!=null && ImportIdentity(custom)!=null && group.Alternatives.Any(a => a.Keep && a.Existing==null && ImportIdentity(a.Incoming)==ImportIdentity(custom))) {custom.SteamShortcutKey=null;}
                }
            }
            int count=0; foreach(var game in prepared.Where(g => !excluded.Contains(g))) {SteamShortcutImport.Merge(settings,game); count++;} return count;
        }
    }
}
