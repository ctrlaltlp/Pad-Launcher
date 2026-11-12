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
    public class ArtworkChoice {
        public string Url {get;set;}
        public string Thumbnail {get;set;}
    }
    public class ArtworkGameMatch {
        public int Id {get;set;}
        public string Name {get;set;}
        public override string ToString() {return Name;}
    }
    public static class ArtworkNetwork {
        public static bool AllowedImageUrl(string value) {
            Uri uri;
            if(!Uri.TryCreate(value,UriKind.Absolute,out uri) || uri.Scheme!="https") return false;
            return uri.Host.EndsWith(".epicgames.com",StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".steamgriddb.com",StringComparison.OrdinalIgnoreCase) || uri.Host.EndsWith(".steamstatic.com",StringComparison.OrdinalIgnoreCase);
        }
        public static byte[] Get(string url,string key,CancellationToken token) {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            var request=(HttpWebRequest)WebRequest.Create(url);
            request.Timeout=12000; request.ReadWriteTimeout=12000; request.AllowAutoRedirect=false;
            request.UserAgent="PadLauncher/0.4.2";
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
            using(var hash=SHA256.Create()) return Path.Combine(Storage.Root,"artwork",BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(game.Id+"|"+game.ArtworkUrl+((game.Source=="custom" || game.Source=="emulator") ? "|"+game.Name : "")))).Replace("-","")+".img");
        }
        public static bool Portrait(string path) {
            try {using(var image=System.Drawing.Image.FromFile(path)) return EpicLibrary.SuitablePortrait(image.Width,image.Height);} catch {return false;}
        }
        public static string SaveLocalChoice(string path) {
            using(var artwork=System.Drawing.Image.FromFile(path)) {
                if(artwork.Width<1 || artwork.Height<1) throw new IOException("Choose a valid artwork image.");
            }
            string name;
            using(var stream=File.OpenRead(path))
            using(var hash=SHA256.Create()) name=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","")+".img";
            string relative=Path.Combine("artwork","chosen",name),destination=Path.Combine(Storage.Root,relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            if(!String.Equals(Path.GetFullPath(path),Path.GetFullPath(destination),StringComparison.OrdinalIgnoreCase))
                File.Copy(path,destination,true);
            return relative;
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
            string custom;
            if(settings.ArtworkOverrides!=null && settings.ArtworkOverrides.TryGetValue(game.Id,out custom)) {
                string root=Path.GetFullPath(Storage.Root)+Path.DirectorySeparatorChar;
                string chosen=Path.GetFullPath(Path.Combine(Storage.Root,custom));
                if(chosen.StartsWith(root,StringComparison.OrdinalIgnoreCase) && File.Exists(chosen)) {game.Art=chosen; game.ArtworkProvider="chosen"; return;}
            }
            if(KeepArtwork(game)) return;
            string path=Cache(game);
            if(Portrait(path)) {game.Art=path; game.ArtworkProvider="cached";}
        }
        static object ApiData(string route,string key,CancellationToken token) {
            object value=Storage.Json().DeserializeObject(Encoding.UTF8.GetString(Fetch("https://www.steamgriddb.com/api/v2/"+route,key,token)));
            if(!String.Equals(Convert.ToString(Vdf.Get(value,"success")),"true",StringComparison.OrdinalIgnoreCase)) throw new IOException("SteamGridDB rejected the request.");
            return Vdf.Get(value,"data");
        }
        static object[] Api(string route,string key,CancellationToken token) {
            return EpicLibrary.Entries(ApiData(route,key,token)).ToArray();
        }
        public static string OriginalGrid(Game game,string key,CancellationToken token,int matchedId=0) {
            if(game.Source=="epic" && ArtworkNetwork.AllowedImageUrl(game.ArtworkUrl)) return game.ArtworkUrl;
            ulong steam;
            if((String.IsNullOrEmpty(game.Source) || game.Source=="steam") && !game.Shortcut && UInt64.TryParse(game.Id,out steam) && steam>0) return OriginalSteamUrl(steam);
            if(String.IsNullOrEmpty(key)) throw new InvalidOperationException("Save a SteamGridDB key in System to match this game to its original Steam artwork.");
            if(matchedId==0) matchedId=ExactMatch(game.Name,SearchGames(game.Name,key,token));
            if(matchedId==0) throw new InvalidOperationException("No unambiguous SteamGridDB match found for this game. Search and select a matching title in the artwork picker.");
            object data=ApiData("games/id/"+matchedId+"?platformdata=steam",key,token);
            var links=EpicLibrary.Entries(Vdf.Get(Vdf.Get(data,"external_platform_data"),"steam")).ToArray();
            if(links.Length!=1 || !UInt64.TryParse(Vdf.Str(links[0],"id"),out steam) || steam==0) throw new InvalidOperationException("No unique original Steam artwork link is available for this game.");
            return OriginalSteamUrl(steam);
        }
        public static string OriginalSteamUrl(ulong id) {
            return "https://shared.fastly.steamstatic.com/store_item_assets/steam/apps/"+id+"/library_600x900.jpg";
        }
        public static void ValidateKey(string key) {Api("search/autocomplete/Amnesia",key,CancellationToken.None);}
        public static string SearchQuery(string name) {
            string decomposed=(name ?? "").Normalize(NormalizationForm.FormD);
            decomposed=Regex.Replace(decomposed,@"\p{Mn}","");
            decomposed=Regex.Replace(decomposed,"[™®©]","");
            return Regex.Replace(Regex.Replace(decomposed,@"[^\p{L}\p{N}\s]"," "),@"\s+"," ").Trim();
        }
        public static string Normalize(string name) {
            string clean=SearchQuery(name);
            string[] words=clean.Split(' ');
            var prefix=Regex.Match(name ?? "",@"^\s*([A-Z]{2,6})\s*[:(\-]");
            if(prefix.Success && words.Length>prefix.Groups[1].Value.Length) {
                string acronym=prefix.Groups[1].Value;
                string initials=String.Concat(words.Skip(1).Take(acronym.Length).Select(word => Char.ToUpperInvariant(word[0])));
                if(initials==acronym) clean=String.Join(" ",words.Skip(1));
            }
            return Regex.Replace(clean.ToLowerInvariant(),@"\s","");
        }
        public static int ExactMatch(string name,IEnumerable<ArtworkGameMatch> candidates) {
            var matches=candidates.Where(v => Normalize(v.Name)==Normalize(name)).GroupBy(v => v.Id).Select(g => g.First()).ToArray();
            return matches.Length==1 ? matches[0].Id : 0;
        }
        static string EmulatorArtworkTitle(string name) {
            string title=Regex.Replace(name ?? "",@"\s*(?:\((?:disc|disk|cd)\s*\d+\)|\b(?:disc|disk|cd)\s*\d+)\s*(?=\(|$)"," ",RegexOptions.IgnoreCase).Trim();
            switch(Normalize(title)) {
                case "crashbandicoot1": return "Crash Bandicoot";
                case "crashbandicoot2": return "Crash Bandicoot 2: Cortex Strikes Back";
                case "crashbandicoot3": return "Crash Bandicoot: Warped";
                case "reddeadredemptiongoty": return "Red Dead Redemption";
                case "reddeadredemptiongotyundeadnightmareandonline": return "Red Dead Redemption: Undead Nightmare";
                case "icoandshadowofthecolossuscolelction": return "Ico and Shadow of the Colossus Collection";
                default: return title;
            }
        }
        static int MatchGame(Game game,string key,CancellationToken token) {
            int match=ExactMatch(game.Name,SearchGames(game.Name,key,token));
            if(match!=0 || game.Source!="emulator") return match;
            string title=EmulatorArtworkTitle(game.Name);
            return Normalize(title)==Normalize(game.Name) ? 0 : ExactMatch(title,SearchGames(title,key,token));
        }
        public static List<ArtworkGameMatch> SearchGames(string name,string key,CancellationToken token) {
            string query=SearchQuery(name); if(query.Length==0) return new List<ArtworkGameMatch>();
            var queries=new List<string> {query};
            string originalQuery=Regex.Replace(Regex.Replace(name ?? "","[™®©]",""),@"\s+"," ").Trim();
            if(originalQuery!=query) queries.Add(originalQuery);
            string[] words=query.Split(' ');
            if(words.Length>3) queries.Add(String.Join(" ",words.Take(3)));
            var candidates=new List<ArtworkGameMatch>();
            foreach(string search in queries.Distinct()) {
                var results=Api("search/autocomplete/"+Uri.EscapeDataString(search),key,token);
                candidates.AddRange(results.Select(v => new ArtworkGameMatch {Id=EpicLibrary.Number(Vdf.Get(v,"id")),Name=Vdf.Str(v,"name")}).Where(v => v.Id>0 && !String.IsNullOrWhiteSpace(v.Name)));
                if(ExactMatch(name,candidates)>0) break;
            }
            return candidates.GroupBy(v => v.Id).Select(g => g.First()).OrderByDescending(v => Normalize(v.Name)==Normalize(name)).ThenBy(v => v.Name).ToList();
        }
        public static List<ArtworkGameMatch> SearchForPicker(Game game,string query,string key,CancellationToken token) {
            var candidates=SearchGames(query,key,token);
            if(game.Source=="emulator") {
                string title=EmulatorArtworkTitle(query);
                if(Normalize(title)!=Normalize(query)) candidates.AddRange(SearchGames(title,key,token));
            }
            string normalized=Normalize(query);
            if(normalized.Length==0) return candidates;
            var known=new List<ArtworkGameMatch>();
            try {
                ulong id;
                if((String.IsNullOrEmpty(game.Source) || game.Source=="steam") && !game.Shortcut && UInt64.TryParse(game.Id,out id)) {
                    var data=ApiData("games/steam/"+id,key,token);
                    known.Add(new ArtworkGameMatch {Id=EpicLibrary.Number(Vdf.Get(data,"id")),Name=Vdf.Str(data,"name")});
                } else if(Normalize(game.Name).Contains(normalized)) {
                    var results=SearchGames(game.Name,key,token);
                    int match=ExactMatch(game.Name,results);
                    known.AddRange(results.Where(g => g.Id==match));
                }
            } catch {token.ThrowIfCancellationRequested(); /* Keep the broader suggestions if the targeted lookup fails. */}
            candidates.AddRange(known.Where(g => g.Id>0 && Normalize(g.Name).Contains(normalized)));
            var preferred=new HashSet<int>(known.Select(g => g.Id));
            return candidates.GroupBy(g => g.Id).Select(g => g.First()).OrderByDescending(g => preferred.Contains(g.Id)).ThenByDescending(g => Normalize(g.Name)==normalized).ThenBy(g => g.Name).ToList();
        }
        public static string FindGrid(Game game,string key,CancellationToken token) {
            return FindGrids(game,key,token).Select(choice => choice.Url).FirstOrDefault();
        }
        public static List<ArtworkChoice> FindGrids(Game game,string key,CancellationToken token,int matchedId=0) {
            string route;
            ulong id;
            if(matchedId>0) route="grids/game/"+matchedId;
            else if((String.IsNullOrEmpty(game.Source) || game.Source=="steam") && !game.Shortcut && UInt64.TryParse(game.Id,out id)) route="grids/steam/"+id;
            else {
                int match=MatchGame(game,key,token);
                if(match==0) return new List<ArtworkChoice>();
                route="grids/game/"+match;
            }
            var grids=Api(route+"?dimensions=600x900&types=static&nsfw=false&humor=false",key,token)
                .Where(v => ArtworkNetwork.AllowedImageUrl(Vdf.Str(v,"url")))
                .ToList();
            if(grids.Count==0 && game.Source=="emulator")
                grids=Api(route+"?types=static&nsfw=false&humor=false",key,token)
                    .Where(v => ArtworkNetwork.AllowedImageUrl(Vdf.Str(v,"url")) &&
                        EpicLibrary.SuitablePortrait(EpicLibrary.Number(Vdf.Get(v,"width")),EpicLibrary.Number(Vdf.Get(v,"height")))).ToList();
            return grids.Take(18).Select(v => new ArtworkChoice {Url=Vdf.Str(v,"url"),Thumbnail=ArtworkNetwork.AllowedImageUrl(Vdf.Str(v,"thumb")) ? Vdf.Str(v,"thumb") : Vdf.Str(v,"url")}).ToList();
        }
        public static string SaveChoice(Game game,string url,CancellationToken token) {
            string name;
            using(var hash=SHA256.Create()) name=BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(game.Id+url))).Replace("-","")+".img";
            string relative=Path.Combine("artwork","chosen",name),path=Path.Combine(Storage.Root,relative);
            if(!Download(url,path,token)) throw new IOException("The selected cover is not a supported portrait image.");
            return relative;
        }
        public static void Fill(IEnumerable<Game> games,Settings settings,CancellationToken token,Action<Game,string> ready,Action<string> status) {
            string key=settings.SteamGridDbEnabled ? ArtworkKey.Load() : null;
            foreach(var game in games) {
                token.ThrowIfCancellationRequested();
                if(KeepArtwork(game)) continue;
                string path=Cache(game),retry=path+(game.Source=="emulator" ? ".emulator-v2.retry" : ".retry");
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
