using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace PadLauncher {
    public static partial class ArtworkService {
        static readonly object ownershipGate=new object();
        static string ownershipRoot;
        static readonly HashSet<string> recordedOwners=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        public sealed class CacheOwner {public string Path {get;set;} public string GameId {get;set;}}
        static string OwnershipFile {get {return System.IO.Path.Combine(Storage.Root,"artwork","cache-owners.jsonl");}}
        // Only these app-generated image locations are eligible. Never traverse chosen images or catalogues.
        static bool DownloadedCachePath(string path) {
            try {
                string root=System.IO.Path.GetFullPath(System.IO.Path.Combine(Storage.Root,"artwork"));
                string full=System.IO.Path.GetFullPath(path),directory=System.IO.Path.GetDirectoryName(full);
                if(!new[]{root,System.IO.Path.Combine(root,"libretro"),System.IO.Path.Combine(root,"providers","sgdb")}.Contains(directory,StringComparer.OrdinalIgnoreCase)) return false;
                string name=System.IO.Path.GetFileNameWithoutExtension(full);
                if(!full.EndsWith(".img",StringComparison.OrdinalIgnoreCase) || name.Length!=64 || name.Any(c => !Uri.IsHexDigit(c))) return false;
                for(string current=directory;current!=null;current=System.IO.Path.GetDirectoryName(current))
                    if(Directory.Exists(current) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) return false;
                return !File.Exists(full) || (File.GetAttributes(full)&FileAttributes.ReparsePoint)==0;
            } catch {return false;}
        }
        internal static string TrackCachedArtwork(Game game,string path) {
            if(game==null || String.IsNullOrEmpty(game.Id)) return path;
            lock(ownershipGate) {
                try {
                    string root=System.IO.Path.GetFullPath(Storage.Root);
                    if(ownershipRoot!=root) {
                        ownershipRoot=root; recordedOwners.Clear();
                        if(File.Exists(OwnershipFile)) foreach(string line in File.ReadLines(OwnershipFile)) {
                            try {var owner=Storage.Json().Deserialize<CacheOwner>(line); if(owner!=null) recordedOwners.Add(owner.Path+"|"+owner.GameId);} catch(ArgumentException) {} catch(InvalidOperationException) {}
                        }
                    }
                    string key=path+"|"+game.Id;
                    if(!recordedOwners.Contains(key) && File.Exists(path) && DownloadedCachePath(path)) {
                        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(OwnershipFile));
                        File.AppendAllText(OwnershipFile,Storage.Json().Serialize(new CacheOwner {Path=System.IO.Path.GetFullPath(path),GameId=game.Id})+Environment.NewLine);
                        recordedOwners.Add(key);
                    }
                } catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            return path;
        }
        public static int ClearUnusedCachedArtwork(IEnumerable<Game> registeredGames,Settings settings) {
            var games=registeredGames.ToList();
            var ids=new HashSet<string>(games.Select(g => g.Id),StringComparer.Ordinal);
            foreach(var game in settings.CustomGames ?? new List<CustomGame>()) ids.Add(game.Id);
            foreach(var emulator in settings.Emulators ?? new List<EmulatorConfig>()) foreach(var game in emulator.Games ?? new List<EmulatorGame>()) ids.Add(game.Id);
            var protectedPaths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            Action<string> protect=path => {if(!String.IsNullOrWhiteSpace(path)) {try {protectedPaths.Add(System.IO.Path.GetFullPath(System.IO.Path.IsPathRooted(path) ? path : System.IO.Path.Combine(Storage.Root,path)));} catch(ArgumentException) {}}};
            foreach(var game in games) {protect(game.Art); protect(Cache(game)); protect(ListCachePath(game,"sgdb")); protect(LibretroArtwork.CachePath(game));}
            foreach(var path in (settings.ArtworkOverrides ?? new Dictionary<string,string>()).Values) protect(path);
            lock(CacheWriteGate) lock(ownershipGate) {
                if(!File.Exists(OwnershipFile)) return 0;
                var owners=new List<CacheOwner>();
                foreach(string line in File.ReadAllLines(OwnershipFile)) {
                    try {var owner=Storage.Json().Deserialize<CacheOwner>(line); if(owner!=null && !String.IsNullOrEmpty(owner.GameId) && owner.Path!=null) owners.Add(owner);} catch(ArgumentException) {} catch(InvalidOperationException) {}
                }
                int removed=0;
                foreach(var group in owners.GroupBy(owner => owner.Path,StringComparer.OrdinalIgnoreCase)) {
                    string path=group.Key;
                    if(group.Any(owner => ids.Contains(owner.GameId)) || protectedPaths.Contains(path) || !DownloadedCachePath(path)) continue;
                    try {if(File.Exists(path)) {File.Delete(path); removed++;}} catch(IOException) {} catch(UnauthorizedAccessException) {}
                }
                return removed;
            }
        }
    }
}
