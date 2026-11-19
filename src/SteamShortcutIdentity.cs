using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PadLauncher {
    // A shortcut's launch mode is a preference, never a separate game identity.
    public static class SteamShortcutIdentity {
        public static bool IsImported(CustomGame game) {return !String.IsNullOrEmpty(game.SteamShortcutKey);}
        public static bool DirectSupported(CustomGame game) {
            return game.SteamDirectSupported ?? (!String.IsNullOrWhiteSpace(game.Target) &&
                new[]{".exe",".lnk",".bat",".cmd"}.Any(extension => game.Target.EndsWith(extension,StringComparison.OrdinalIgnoreCase)) &&
                (game.Arguments ?? "").IndexOf("%command%",StringComparison.OrdinalIgnoreCase)<0);
        }
        public static bool ThroughSteam(Settings settings,CustomGame game) {
            return IsImported(game) ? settings.SteamShortcutLaunchMode!="direct" || !DirectSupported(game) : game.LaunchViaSteam;
        }
        static string Action(CustomGame game) {return GameImport.ActionKey(game.Target,game.Arguments,game.WorkingDirectory);}
        public static bool Normalize(Settings settings) {
            var entries=settings.CustomGames;
            if(entries==null) return false;
            var aliases=new Dictionary<string,string>(); bool changed=false;
            foreach(var group in entries.Where(IsImported).GroupBy(game => game.SteamShortcutKey).Select(group => group.ToList()).ToList()) {
                var members=group.Where(entries.Contains).ToList(); if(members.Count==0) continue;
                var preferred=members.FirstOrDefault(game => !game.LaunchViaSteam) ?? members[0];
                var matching=entries.Where(game => !members.Contains(game) && Action(game)==Action(preferred) &&
                    (IsImported(game) || !game.LaunchViaSteam)).ToList();
                var keep=matching.FirstOrDefault(game => !IsImported(game)) ?? preferred;
                members=members.Concat(matching).Distinct().ToList();
                if(!IsImported(keep)) {keep.SteamShortcutKey=preferred.SteamShortcutKey; keep.SteamLaunchId=preferred.SteamLaunchId; changed=true;}
                if(String.IsNullOrEmpty(keep.SteamLaunchId)) {keep.SteamLaunchId=preferred.SteamLaunchId; changed=true;}
                foreach(var old in members.Where(game => game!=keep)) {
                    aliases[old.Id]=keep.Id;
                    if(String.IsNullOrEmpty(keep.Artwork)) keep.Artwork=old.Artwork;
                    if(old.IncludeInCustomGamesList==true) keep.IncludeInCustomGamesList=true;
                    entries.Remove(old); changed=true;
                }
                foreach(string key in members.Select(game => game.SteamShortcutKey).Where(key => !String.IsNullOrEmpty(key))) {
                    string[] parts=key.Split(':');
                    if(parts.Length==2) aliases[parts[1]]=keep.Id;
                }
            }
            Remap(settings,null,aliases);
            return changed;
        }
        public static void Reconcile(Library library,Settings settings) {
            Normalize(settings);
            var aliases=new Dictionary<string,string>();
            var entries=settings.CustomGames ?? new List<CustomGame>();
            foreach(var native in library.Games.Where(game => game.Shortcut).ToList()) {
                var match=entries.FirstOrDefault(game => IsImported(game) && game.SteamLaunchId==native.LaunchId)
                    ?? entries.FirstOrDefault(game => (!game.LaunchViaSteam || IsImported(game)) && !String.IsNullOrEmpty(native.ShortcutTarget) &&
                        Action(game)==GameImport.ActionKey(native.ShortcutTarget,native.Arguments,native.WorkingDirectory));
                if(match==null) continue;
                aliases[native.Id]=match.Id;
                var retained=library.Games.FirstOrDefault(game => game.Id==match.Id);
                if(retained!=null) {
                    retained.LastPlayed=Math.Max(retained.LastPlayed,native.LastPlayed);
                    if(String.IsNullOrEmpty(retained.Art)) retained.Art=native.Art;
                }
                library.Games.Remove(native);
            }
            Remap(settings,library,aliases);
            foreach(var entry in entries.Where(IsImported)) {
                var game=library.Games.FirstOrDefault(item => item.Id==entry.Id);
                if(game!=null) game.SteamLaunchId=ThroughSteam(settings,entry) ? entry.SteamLaunchId : null;
            }
        }
        static void Remap(Settings settings,Library library,Dictionary<string,string> aliases) {
            if(aliases.Count==0) return;
            Func<string,string> map=id => {
                string result=id,next; int remaining=aliases.Count;
                while(result!=null && remaining-->0 && aliases.TryGetValue(result,out next) && next!=result) result=next;
                return result;
            };
            Func<List<string>,List<string>> list=values => values==null ? null : values.Select(map).Distinct().ToList();
            foreach(var item in settings.CustomLists ?? new List<CustomList>()) item.Games=list(item.Games);
            foreach(var item in settings.CombinedCollections ?? new List<CombinedCollection>()) {item.AddedGames=list(item.AddedGames); item.ExcludedGames=list(item.ExcludedGames);}
            if(settings.ListGameAdditions!=null) foreach(string key in settings.ListGameAdditions.Keys.ToList()) settings.ListGameAdditions[key]=list(settings.ListGameAdditions[key]);
            settings.FavouriteGameIds=list(settings.FavouriteGameIds); settings.HiddenGameIds=list(settings.HiddenGameIds);
            settings.SelectedId=map(settings.SelectedId);
            if(settings.SelectedGamesByList!=null) foreach(string key in settings.SelectedGamesByList.Keys.ToList()) settings.SelectedGamesByList[key]=map(settings.SelectedGamesByList[key]);
            if(settings.ArtworkOverrides!=null) foreach(string key in settings.ArtworkOverrides.Keys.ToList()) {
                string id=map(key); if(id==key) continue;
                if(!settings.ArtworkOverrides.ContainsKey(id)) settings.ArtworkOverrides[id]=settings.ArtworkOverrides[key];
                settings.ArtworkOverrides.Remove(key);
            }
            if(settings.LaunchHistory!=null) foreach(string key in settings.LaunchHistory.Keys.ToList()) {
                string id=map(key); if(id==key) continue;
                long previous; settings.LaunchHistory.TryGetValue(id,out previous);
                settings.LaunchHistory[id]=Math.Max(previous,settings.LaunchHistory[key]); settings.LaunchHistory.Remove(key);
            }
            if(settings.LaunchCounts!=null) foreach(string key in settings.LaunchCounts.Keys.ToList()) {
                string id=map(key); if(id==key) continue;
                long previous; settings.LaunchCounts.TryGetValue(id,out previous);
                settings.LaunchCounts[id]=previous+settings.LaunchCounts[key]; settings.LaunchCounts.Remove(key);
            }
            if(library!=null) foreach(var item in library.Collections) item.Games=list(item.Games);
        }
    }
}
