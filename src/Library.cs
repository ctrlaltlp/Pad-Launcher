using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace PadLauncher {
    public class Game {
        public string Id { get; set; }
        public string Name { get; set; }
        public string LaunchId { get; set; }
        public bool Shortcut { get; set; }
        public bool ShortcutInstalled { get; set; }
        public string ShortcutTarget { get; set; }
        public string Art { get; set; }
        public string Hero { get; set; }
        public List<string> Tags { get; set; }
        public List<string> StoreTags { get; set; }
        public List<string> Categories { get; set; }
        public bool HasMetadata { get; set; }
        public long LastPlayed { get; set; }
        public bool HasPlayHistory { get; set; }
        public Game() { Tags = new List<string>(); StoreTags = new List<string>(); Categories = new List<string>(); }
    }
    public class Collection {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Dynamic { get; set; }
        public bool UnsupportedDynamic { get; set; }
        public string UnsupportedReason { get; set; }
        public List<string> Games { get; set; }
        public Collection() { Games = new List<string>(); }
        public override string ToString() { return Name + (UnsupportedDynamic ? " · dynamic (unsupported)" : ""); }
    }
    public class Library {
        public string SteamPath { get; set; }
        public string UserId { get; set; }
        public List<Game> Games { get; set; }
        public List<Collection> Collections { get; set; }
        public List<string> Warnings { get; set; }
        public Library() { Games = new List<Game>(); Collections = new List<Collection>(); Warnings = new List<string>(); }
    }
    public class Settings {
        public string ButtonPrompts { get; set; }
        public bool ShowGameTitle { get; set; }
        public string OpenMonitor { get; set; }
        public string SteamPath { get; set; }
        public string UserId { get; set; }
        public string CollectionId { get; set; }
        public string SelectedId { get; set; }
        public string DefaultCollectionId { get; set; }
        public double CarouselScale { get; set; }
        public string CarouselLayout { get; set; }
        public Settings() { ButtonPrompts = "PlayStation"; ShowGameTitle = true; OpenMonitor = "cursor"; DefaultCollectionId="remember"; CarouselScale=1; CarouselLayout="angled"; }
    }
    public static class Prompts {
        public static string Style(Settings settings) {
            if(String.Equals(settings.ButtonPrompts,"Xbox",StringComparison.OrdinalIgnoreCase)) return "Xbox";
            if(String.Equals(settings.ButtonPrompts,"Nintendo",StringComparison.OrdinalIgnoreCase)) return "Nintendo";
            return "PlayStation";
        }
        public static string[] Buttons(Settings settings) {
            switch(Style(settings)) {
                case "Xbox": return new[]{"A","B","LB / RB","Y","Start","X"};
                case "Nintendo": return new[]{"B","A","L / R","X","+","Y"};
                default: return new[]{"×","○","L1 / R1","△","Options","□"};
            }
        }
        public static string Help(Settings settings) {
            var names=Buttons(settings);
            return names[0]+"  Play     Tap "+names[1]+"  Dismiss     Hold "+names[1]+"  Exit app     "+names[2]+"  Collections     Tap "+names[3]+"  Refresh     "+names[5]+"  Next monitor     Right stick  3× speed";
        }
    }
    public enum ButtonGesture { None, Tap, Hold }
    public class HoldButton {
        bool pressed, fired;
        long started;
        public void Reset() { pressed=false; fired=false; }
        public ButtonGesture Update(bool down,long milliseconds) {
            if (down) {
                if (!pressed) { pressed=true; fired=false; started=milliseconds; }
                if (!fired && milliseconds-started>=1000) { fired=true; return ButtonGesture.Hold; }
            } else if (pressed) {
                bool held=fired || milliseconds-started>=1000;
                Reset(); return held ? ButtonGesture.None : ButtonGesture.Tap;
            }
            return ButtonGesture.None;
        }
    }
    public static class Storage {
        public static string Root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PadLauncher");
        public static void MigrateFrom(string legacyRoot) {
            if(!Directory.Exists(legacyRoot)) return;
            Directory.CreateDirectory(Root);
            foreach(string name in new[]{"settings.json","library.json"}) {
                string source=Path.Combine(legacyRoot,name),target=Path.Combine(Root,name);
                if(File.Exists(source) && (!File.Exists(target) || File.GetLastWriteTimeUtc(source)>File.GetLastWriteTimeUtc(target))) File.Copy(source,target,true);
            }
        }
        public static JavaScriptSerializer Json() { return new JavaScriptSerializer { MaxJsonLength = 32 * 1024 * 1024 }; }
        public static T Read<T>(string name) where T : class {
            try { return Json().Deserialize<T>(File.ReadAllText(Path.Combine(Root, name))); } catch { return null; }
        }
        public static void Write(string name, object data) {
            Directory.CreateDirectory(Root);
            string path = Path.Combine(Root, name), temp = path + ".tmp";
            File.WriteAllText(temp, Json().Serialize(data));
            if (File.Exists(path)) File.Replace(temp, path, null); else File.Move(temp, path);
        }
    }
    // Valve KeyValues supports quoted/bare tokens, escapes and line comments.
    public static class Vdf {
        public static Dictionary<string,object> Map(object value) { return value as Dictionary<string,object> ?? new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase); }
        public static object Get(object value, string key) { object result; return Map(value).TryGetValue(key, out result) ? result : null; }
        public static string Str(object value, string key) { return Convert.ToString(Get(value, key)); }
        public static Dictionary<string,object> Read(string path) { return Parse(File.ReadAllText(path)); }
        public static Dictionary<string,object> Parse(string text) { int pos = 0; return Block(text, ref pos); }
        static string Token(string text, ref int pos) {
            while (pos < text.Length) {
                if (Char.IsWhiteSpace(text[pos]) || text[pos] == '\uFEFF') { pos++; continue; }
                if (text[pos] == '/' && pos + 1 < text.Length && text[pos+1] == '/') { while (pos < text.Length && text[pos] != '\n') pos++; continue; }
                break;
            }
            if (pos >= text.Length) return null;
            char c = text[pos++];
            if (c == '{' || c == '}') return c.ToString();
            var b = new StringBuilder();
            if (c == '"') {
                while (pos < text.Length) {
                    c = text[pos++]; if (c == '"') return b.ToString();
                    if (c == '\\' && pos < text.Length && (text[pos] == '\\' || text[pos] == '"')) c = text[pos++];
                    b.Append(c);
                }
                throw new InvalidDataException("Unterminated VDF string.");
            }
            b.Append(c);
            while (pos < text.Length && !Char.IsWhiteSpace(text[pos]) && text[pos] != '{' && text[pos] != '}') b.Append(text[pos++]);
            return b.ToString();
        }
        static Dictionary<string,object> Block(string text, ref int pos) {
            var map = new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
            string key;
            while ((key = Token(text, ref pos)) != null && key != "}") {
                string val = Token(text, ref pos);
                if (val == null || val == "}") throw new InvalidDataException("Incomplete VDF entry.");
                map[key] = val == "{" ? (object)Block(text, ref pos) : val;
            }
            return map;
        }
        public static Dictionary<string,object> Binary(string path) {
            using (var r = new BinaryReader(File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))) return BinaryBlock(r, 0);
        }
        static string CString(BinaryReader r) {
            var bytes = new List<byte>(); byte b;
            while ((b = r.ReadByte()) != 0) { bytes.Add(b); if (bytes.Count > 1024*1024) throw new InvalidDataException("Oversized VDF string."); }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
        public static Dictionary<string,object> BinaryRecord(BinaryReader r, string[] keys) { return BinaryBlock(r,0,keys); }
        static Dictionary<string,object> BinaryBlock(BinaryReader r, int depth, string[] keys = null) {
            if (depth > 32) throw new InvalidDataException("VDF nesting too deep.");
            var map = new Dictionary<string,object>(StringComparer.OrdinalIgnoreCase);
            while (r.BaseStream.Position < r.BaseStream.Length) {
                byte type = r.ReadByte(); if (type == 8 || type == 11) return map;
                string key;
                if (keys == null) key = CString(r);
                else { uint index = r.ReadUInt32(); if (index >= keys.Length) throw new InvalidDataException("Invalid appinfo string index."); key = keys[index]; }
                object value;
                switch (type) {
                    case 0: value = BinaryBlock(r, depth+1,keys); break;
                    case 1: value = CString(r); break;
                    case 2: value = r.ReadUInt32(); break;
                    case 3: value = r.ReadSingle(); break;
                    case 7: value = r.ReadUInt64(); break;
                    default: throw new InvalidDataException("Unsupported binary VDF type: " + type);
                }
                map[key] = value;
            }
            return map;
        }
    }
    public static class SteamLibrary {
        public static string ShortcutTargetPath(string executable,string startDirectory) {
            string target=(executable ?? "").Trim();
            if(target.StartsWith("\"")) {
                int end=target.IndexOf('"',1); if(end<0) return null;
                target=target.Substring(1,end-1);
            } else if(!File.Exists(target)) {
                int end=target.IndexOf(".exe",StringComparison.OrdinalIgnoreCase);
                if(end>=0) target=target.Substring(0,end+4);
            }
            if(String.IsNullOrWhiteSpace(target)) return null;
            target=Environment.ExpandEnvironmentVariables(target);
            try {
                if(!Path.IsPathRooted(target)) {
                    string start=Environment.ExpandEnvironmentVariables((startDirectory ?? "").Trim().Trim('"'));
                    if(String.IsNullOrEmpty(start)) return null;
                    target=Path.Combine(start,target);
                }
                return Path.GetFullPath(target);
            } catch(ArgumentException) { return null; } catch(NotSupportedException) { return null; }
        }
        public static string DetectSteam() {
            foreach (RegistryView view in new [] { RegistryView.Registry32, RegistryView.Registry64 }) {
                foreach (RegistryHive hive in new [] { RegistryHive.CurrentUser, RegistryHive.LocalMachine }) {
                    using (var root = RegistryKey.OpenBaseKey(hive, view))
                    using (var key = root.OpenSubKey(@"Software\Valve\Steam")) {
                        if (key == null) continue;
                        string path = Convert.ToString(key.GetValue("SteamPath") ?? key.GetValue("InstallPath"));
                        if (Directory.Exists(Path.Combine(path, "steamapps"))) return path;
                    }
                }
            }
            string fallback = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam");
            return Directory.Exists(Path.Combine(fallback, "steamapps")) ? fallback : null;
        }
        public static List<string> Users(string path) {
            string dir = Path.Combine(path, "userdata");
            return Directory.Exists(dir) ? Directory.GetDirectories(dir).Where(p => { uint id; return UInt32.TryParse(Path.GetFileName(p), out id) && id != 0; }).OrderByDescending(Directory.GetLastWriteTimeUtc).Select(Path.GetFileName).ToList() : new List<string>();
        }
        static string ActiveUser(string path, List<string> users) {
            string login = Path.Combine(path, "config", "loginusers.vdf");
            try {
                foreach (var pair in Vdf.Map(Vdf.Get(Vdf.Read(login), "users"))) {
                    ulong id;
                    if (Vdf.Str(pair.Value, "MostRecent") == "1" && UInt64.TryParse(pair.Key, out id)) {
                        string account = (id & 0xffffffff).ToString(); if (users.Contains(account)) return account;
                    }
                }
            } catch { }
            return users.FirstOrDefault();
        }
        public static string ShortcutLaunchId(uint id) { return (((ulong)id << 32) | 0x02000000UL).ToString(); }
        static uint ShortcutId(string exe, string name) {
            uint crc = 0xffffffff;
            foreach (byte b in Encoding.UTF8.GetBytes(exe + name)) { crc ^= b; for (int bit=0; bit<8; bit++) crc = (crc >> 1) ^ ((crc & 1) != 0 ? 0xedb88320U : 0); }
            return (crc ^ 0xffffffff) | 0x80000000;
        }
        public static Library Scan(string path, string user) {
            if (String.IsNullOrEmpty(path) || !Directory.Exists(Path.Combine(path, "steamapps"))) throw new DirectoryNotFoundException("Choose the Steam folder containing steam.exe and steamapps.");
            var result = new Library { SteamPath = path };
            var users = Users(path);
            user = users.Contains(user) ? user : ActiveUser(path, users);
            result.UserId = user;
            var folders = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { path };
            string libraries = Path.Combine(path, "steamapps", "libraryfolders.vdf");
            if (File.Exists(libraries)) {
                try {
                    foreach (var pair in Vdf.Map(Vdf.Get(Vdf.Read(libraries), "libraryfolders"))) {
                        string p = pair.Value as string ?? Vdf.Str(pair.Value, "path");
                        if (Directory.Exists(Path.Combine(p, "steamapps"))) folders.Add(p);
                    }
                } catch (Exception e) { result.Warnings.Add("Library folders: " + e.Message); }
            }
            foreach (string folder in folders) {
                string[] manifests;
                try { manifests = Directory.GetFiles(Path.Combine(folder, "steamapps"), "appmanifest_*.acf"); }
                catch (Exception e) { result.Warnings.Add("Cannot read library " + folder + ": " + e.Message); continue; }
                foreach (string manifest in manifests) {
                    try {
                        var app = Vdf.Get(Vdf.Read(manifest), "AppState");
                        string id = Vdf.Str(app, "appid"), name = Vdf.Str(app, "name");
                        uint parsed; uint flags;
                        if (!UInt32.TryParse(id, out parsed) || String.IsNullOrWhiteSpace(name)) continue;
                        if (!UInt32.TryParse(Vdf.Str(app, "StateFlags"), out flags) || (flags & 4) == 0) continue;
                        if (IsRuntime(name)) continue;
                        if (!result.Games.Any(g => g.Id == id)) result.Games.Add(new Game { Id = id, LaunchId = id, Name = name });
                    } catch (Exception e) { result.Warnings.Add(Path.GetFileName(manifest) + ": " + e.Message); }
                }
            }
            if (user != null) {
                string config = Path.Combine(path, "userdata", user, "config");
                string shortcuts = Path.Combine(config, "shortcuts.vdf");
                if (File.Exists(shortcuts)) {
                    try {
                        foreach (object shortcut in Vdf.Map(Vdf.Get(Vdf.Binary(shortcuts), "shortcuts")).Values) {
                            string name = Vdf.Str(shortcut, "AppName"); if (String.IsNullOrWhiteSpace(name)) continue;
                            uint id; if (!UInt32.TryParse(Vdf.Str(shortcut, "appid"), out id)) id = ShortcutId(Vdf.Str(shortcut, "exe"), name);
                            long lastPlayed; Int64.TryParse(Vdf.Str(shortcut,"LastPlayTime"),out lastPlayed);
                            var game = new Game { Id = id.ToString(), LaunchId = ShortcutLaunchId(id), Name = name, Shortcut = true, LastPlayed = lastPlayed, HasPlayHistory = Vdf.Get(shortcut,"LastPlayTime") != null };
                            game.ShortcutTarget=ShortcutTargetPath(Vdf.Str(shortcut,"exe"),Vdf.Str(shortcut,"StartDir"));
                            game.ShortcutInstalled=!String.IsNullOrEmpty(game.ShortcutTarget) && File.Exists(game.ShortcutTarget);
                            game.Tags = Vdf.Map(Vdf.Get(shortcut, "tags")).Values.Select(Convert.ToString).Where(s => !String.IsNullOrWhiteSpace(s)).ToList();
                            result.Games.Add(game);
                        }
                    } catch (Exception e) { result.Warnings.Add("Non-Steam shortcuts: " + e.Message); }
                }
                DynamicRules.LoadMetadata(path,config,result);
                ReadCollections(config, result);
                foreach (var game in result.Games) {
                    game.Hero = Artwork(path, config, game, true);
                    game.Art = Artwork(path, config, game, false) ?? game.Hero;
                }
            } else {
                result.Warnings.Add("No Steam profile found. Sign in to Steam, then refresh to load shortcuts and collections.");
                foreach (var game in result.Games) { game.Hero = Artwork(path, null, game, true); game.Art = Artwork(path, null, game, false) ?? game.Hero; }
            }
            result.Games = result.Games.OrderBy(g => g.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
            return result;
        }
        public static void ReadCollections(string config, Library result) {
            string cloud = Path.Combine(config, "cloudstorage");
            var collections = new Dictionary<string,Collection>();
            var removals = new Dictionary<string,HashSet<string>>();
            if (Directory.Exists(cloud)) foreach (string file in Directory.GetFiles(cloud, "cloud-storage-namespace-*.json")) {
                try {
                    var entries = Storage.Json().DeserializeObject(File.ReadAllText(file)) as object[];
                    if (entries == null) continue;
                    foreach (object entry in entries) {
                        var pair = entry as object[]; if (pair == null || pair.Length < 2) continue;
                        string key = Convert.ToString(pair[0]); if (!key.StartsWith("user-collections.")) continue;
                        if (Convert.ToString(Vdf.Get(pair[1], "is_deleted")).Equals("True", StringComparison.OrdinalIgnoreCase) || Convert.ToString(Vdf.Get(pair[1], "is_deleted")) == "1") continue;
                        string raw = Vdf.Str(pair[1], "value"); if (String.IsNullOrWhiteSpace(raw)) continue;
                        var data = Storage.Json().DeserializeObject(raw);
                        object spec = Vdf.Get(data, "filterSpec");
                        var c = new Collection { Id = key, Name = Vdf.Str(data, "name"), Dynamic = spec != null };
                        string reason;
                        List<string> matches = DynamicRules.Evaluate(spec,result.Games,out reason);
                        c.UnsupportedDynamic = reason != null; c.UnsupportedReason = reason;
                        var removed = new HashSet<string>(Items(Vdf.Get(data, "removed")));
                        removals[key] = removed;
                        c.Games = matches.Concat(Items(Vdf.Get(data, "added"))).Where(id => !removed.Contains(id)).Distinct().ToList();
                        if (!String.IsNullOrWhiteSpace(c.Name)) collections[key] = c;
                    }
                } catch (Exception e) { result.Warnings.Add("Collections " + Path.GetFileName(file) + ": " + e.Message); }
            }
            // Steam stores non-Steam membership separately in this profile's WebStorage.
            string local = Path.Combine(config,"localconfig.vdf");
            if (File.Exists(local)) try {
                var root = Vdf.Get(Vdf.Read(local),"UserLocalConfigStore");
                string raw = Vdf.Str(Vdf.Get(root,"WebStorage"),"user-collections");
                if (!String.IsNullOrEmpty(raw)) foreach (var pair in Vdf.Map(Storage.Json().DeserializeObject(raw))) {
                    Collection c;
                    if (!collections.TryGetValue("user-collections."+pair.Key,out c)) continue;
                    var removed = new HashSet<string>(Items(Vdf.Get(pair.Value,"removed")));
                    HashSet<string> cloudRemoved;
                    if (removals.TryGetValue(c.Id,out cloudRemoved)) removed.UnionWith(cloudRemoved);
                    c.Games = c.Games.Concat(Items(Vdf.Get(pair.Value,"added"))).Where(id => !removed.Contains(id)).Distinct().ToList();
                }
            } catch (Exception e) { result.Warnings.Add("Local collection membership: "+e.Message); }
            // Shortcut tags remain useful when no cloud collection has been cached yet.
            foreach (var game in result.Games.Where(g => g.Shortcut)) foreach (string tag in game.Tags) {
                var collection = collections.Values.FirstOrDefault(c => c.Name.Equals(tag, StringComparison.OrdinalIgnoreCase));
                if (collection != null && !collection.Id.StartsWith("tag:")) continue; // Cloud membership/removals take precedence.
                string key = "tag:" + tag;
                if (!collections.ContainsKey(key)) collections[key] = new Collection { Id = key, Name = tag };
                collections[key].Games.Add(game.Id);
            }
            result.Collections = collections.Values.OrderBy(c => c.Name).ToList();
        }
        static bool IsRuntime(string name) {
            return name.StartsWith("Proton ",StringComparison.OrdinalIgnoreCase) || name.StartsWith("Steam Linux Runtime",StringComparison.OrdinalIgnoreCase) || name.StartsWith("Source SDK",StringComparison.OrdinalIgnoreCase) || name.Equals("Steamworks Common Redistributables",StringComparison.OrdinalIgnoreCase);
        }
        static IEnumerable<string> Items(object value) { var values = value as IEnumerable; if (values == null || value is string) yield break; foreach (object v in values) { long id; string s=Convert.ToString(v); yield return Int64.TryParse(s,out id) && id<0 ? unchecked((uint)id).ToString() : s; } }
        static string Artwork(string root, string config, Game game, bool hero) {
            string[] extensions = { ".png", ".jpg", ".jpeg", ".webp" };
            if (config != null) {
                string grid = Path.Combine(config, "grid");
                foreach (string id in new [] { game.Id, game.LaunchId }) foreach (string suffix in hero ? new [] { "_hero", "", "p" } : new [] { "p", "", "_hero" }) foreach (string ext in extensions) {
                    string file = Path.Combine(grid, id + suffix + ext); if (ext != ".webp" && File.Exists(file)) return file;
                }
            }
            if (!game.Shortcut) {
                string cache = Path.Combine(root, "appcache", "librarycache");
                string sub = Path.Combine(cache, game.Id);
                string[] nested = Directory.Exists(sub) ? Directory.GetFiles(sub,"*",SearchOption.AllDirectories) : new string[0];
                foreach (string kind in hero ? new [] { "library_hero", "library_600x900", "library_header", "header" } : new [] { "library_600x900", "library_hero", "library_header", "header" }) foreach (string ext in extensions) {
                    foreach (string file in new [] { Path.Combine(cache, game.Id + "_" + kind + ext), Path.Combine(cache, game.Id, kind + ext) }) if (ext != ".webp" && File.Exists(file)) return file;
                    if (ext != ".webp") {
                        string file = nested.Where(p => Path.GetExtension(p).Equals(ext,StringComparison.OrdinalIgnoreCase) && Path.GetFileNameWithoutExtension(p).IndexOf(kind,StringComparison.OrdinalIgnoreCase)>=0)
                            .OrderBy(p => Path.GetFileNameWithoutExtension(p).Equals(kind,StringComparison.OrdinalIgnoreCase) ? 0 : Path.GetFileNameWithoutExtension(p).IndexOf("blur",StringComparison.OrdinalIgnoreCase)>=0 ? 2 : 1)
                            .ThenBy(p => p,StringComparer.OrdinalIgnoreCase).FirstOrDefault();
                        if (file != null) return file;
                    }
                }
            }
            return null;
        }
    }
}
