using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Security.Cryptography;
using System.Threading;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PadLauncher {
    public static class ArtworkNetwork {
        public static bool AllowedImageUrl(string value) {
            Uri uri;
            if(!Uri.TryCreate(value,UriKind.Absolute,out uri) || uri.Scheme!="https") return false;
            return uri.Host.EndsWith(".epicgames.com",StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".steamgriddb.com",StringComparison.OrdinalIgnoreCase);
        }
        public static byte[] Get(string url,string key,CancellationToken token) {
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.Timeout=12000; request.ReadWriteTimeout=12000; request.AllowAutoRedirect=false;
            request.UserAgent="SteamPadLauncher/0.2";
            if(!String.IsNullOrEmpty(key)) {
                if(new Uri(url).Host!="www.steamgriddb.com") throw new InvalidOperationException("Invalid API host.");
                request.Headers["Authorization"]="Bearer "+key;
            }
            using(token.Register(() => request.Abort()))
            using(var response=(HttpWebResponse)request.GetResponse())
            using(var stream=response.GetResponseStream())
            using(var output=new MemoryStream()) {
                if((int)response.StatusCode!=200) throw new IOException("Artwork server returned "+(int)response.StatusCode);
                var buffer=new byte[16384]; int count;
                while((count=stream.Read(buffer,0,buffer.Length))>0) {
                    token.ThrowIfCancellationRequested();
                    if(output.Length+count>16000000) throw new IOException("Artwork exceeds download limit.");
                    output.Write(buffer,0,count);
                }
                return output.ToArray();
            }
        }
    }
    public static class ArtworkKey {
        static string PathName {get {return Path.Combine(Storage.Root,"steamgriddb.key");}}
        public static string Load() { try {return Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(PathName),null,DataProtectionScope.CurrentUser));} catch {return null;} }
        public static void Save(string key) {
            Directory.CreateDirectory(Storage.Root); File.WriteAllBytes(PathName,ProtectedData.Protect(Encoding.UTF8.GetBytes(key.Trim()),null,DataProtectionScope.CurrentUser));
            string cache=Path.Combine(Storage.Root,"artwork");
            if(Directory.Exists(cache)) foreach(string retry in Directory.GetFiles(cache,"*.retry")) File.Delete(retry);
        }
        public static void Clear() {if(File.Exists(PathName)) File.Delete(PathName);}
    }
    public static class ArtworkService {
        public static Func<string,string,CancellationToken,byte[]> Fetch=ArtworkNetwork.Get;
        static string Cache(Game game) {
            using(var hash=SHA256.Create()) return Path.Combine(Storage.Root,"artwork",BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(game.Id+"|"+game.ArtworkUrl+(game.Source=="custom" ? "|"+game.Name : "")))).Replace("-","")+".img");
        }
        public static bool Portrait(string path) {
            try {using(var image=System.Drawing.Image.FromFile(path)) return EpicLibrary.SuitablePortrait(image.Width,image.Height);} catch {return false;}
        }
        public static bool KeepArtwork(Game game) {
            // Steam's own artwork and user-selected shortcut art take priority,
            // regardless of their dimensions. Provider cache files remain replaceable.
            bool steam=String.IsNullOrEmpty(game.Source) || game.Source=="steam" || game.Source=="custom";
            if(steam && !String.IsNullOrEmpty(game.Art) && File.Exists(game.Art)) {
                string cacheRoot=Path.GetFullPath(Path.Combine(Storage.Root,"artwork"))+Path.DirectorySeparatorChar;
                if(!Path.GetFullPath(game.Art).StartsWith(cacheRoot,StringComparison.OrdinalIgnoreCase)) return true;
            }
            return Portrait(game.Art);
        }
        public static void ApplyCached(Game game,Settings settings) {
            if(KeepArtwork(game)) return;
            string path=Cache(game);
            if(Portrait(path)) {game.Art=path; game.ArtworkProvider="cached";}
        }
        static object[] Api(string route,string key,CancellationToken token) {
            object value=Storage.Json().DeserializeObject(Encoding.UTF8.GetString(Fetch("https://www.steamgriddb.com/api/v2/"+route,key,token)));
            if(!String.Equals(Convert.ToString(Vdf.Get(value,"success")),"true",StringComparison.OrdinalIgnoreCase)) throw new IOException("SteamGridDB rejected the request.");
            return EpicLibrary.Entries(Vdf.Get(value,"data")).ToArray();
        }
        public static void ValidateKey(string key) {Api("search/autocomplete/Amnesia",key,CancellationToken.None);}
        public static string Normalize(string name) {return Regex.Replace((name ?? "").ToLowerInvariant(),@"[^\p{L}\p{N}]","");}
        public static string FindGrid(Game game,string key,CancellationToken token) {
            string route;
            ulong id;
            if(String.IsNullOrEmpty(game.Source) && !game.Shortcut && UInt64.TryParse(game.Id,out id)) route="grids/steam/"+id;
            else {
                var matches=Api("search/autocomplete/"+Uri.EscapeDataString(game.Name),key,token).Where(v => Normalize(Vdf.Str(v,"name"))==Normalize(game.Name)).ToArray();
                if(matches.Length!=1) return null;
                route="grids/game/"+EpicLibrary.Number(Vdf.Get(matches[0],"id"));
            }
            return Api(route+"?dimensions=600x900&types=static&nsfw=false&humor=false",key,token)
                .Select(v => Vdf.Str(v,"url")).FirstOrDefault(ArtworkNetwork.AllowedImageUrl);
        }
        public static void Fill(IEnumerable<Game> games,Settings settings,CancellationToken token,Action<Game,string> ready,Action<string> status) {
            string key=settings.SteamGridDbEnabled ? ArtworkKey.Load() : null;
            foreach(var game in games) {
                token.ThrowIfCancellationRequested();
                if(KeepArtwork(game)) continue;
                string path=Cache(game),retry=path+".retry";
                if(File.Exists(retry) && File.GetLastWriteTimeUtc(retry)>DateTime.UtcNow.AddHours(-6)) continue;
                try {
                    string url=game.ArtworkUrl;
                    bool saved=false;
                    try {saved=Download(url,path,token);} catch(WebException) {token.ThrowIfCancellationRequested();}
                    if(!saved && !String.IsNullOrEmpty(key)) saved=Download(FindGrid(game,key,token),path,token);
                    if(saved) ready(game,path);
                    else {Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(retry,"");}
                } catch(Exception) {
                    token.ThrowIfCancellationRequested();
                    status("Artwork download paused. Check your connection or SteamGridDB key, then refresh.");
                    break;
                }
            }
        }
        static bool Download(string url,string path,CancellationToken token) {
            if(!ArtworkNetwork.AllowedImageUrl(url)) return false;
            byte[] data=Fetch(url,null,token);
            using(var stream=new MemoryStream(data)) using(var image=System.Drawing.Image.FromStream(stream)) {
                if(!EpicLibrary.SuitablePortrait(image.Width,image.Height)) return false;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path,data); return true;
        }
    }
}
