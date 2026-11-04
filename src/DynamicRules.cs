using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

namespace PadLauncher {
    public static class DynamicRules {
        // Steam's current local filter format: type, state, features, suggestions,
        // store tags, special source, friend ownership, language, accessibility.
        static object[] Array(object value) { return value as object[] ?? new object[0]; }
        static string[] Options(object group) { return Array(Vdf.Get(group,"rgOptions")).Select(Convert.ToString).ToArray(); }
        public static List<string> Evaluate(object spec,List<Game> games,out string reason) {
            reason=null;
            var empty=new List<string>();
            if (spec==null) return empty;
            object[] groups=Vdf.Get(spec,"filterGroups") as object[];
            if (groups==null) { reason="Unknown Steam filter schema"; return empty; }
            object version=Vdf.Get(spec,"nFormatVersion");
            if (version!=null && Convert.ToString(version)!="2") { reason="Unsupported Steam filter version"; return empty; }
            string search=Vdf.Str(spec,"strSearchText");
            bool active=search.Length>0 || groups.Any(g => Options(g).Length>0);
            if (!active) return empty; // Steam empty filters use only explicit membership.
            for (int i=0;i<groups.Length;i++) foreach (string option in Options(groups[i])) {
                if (i==1 && option=="1") continue;
                if (i==1 && (option=="3" || option=="4")) {
                    if (games.Any(g => !g.HasPlayHistory)) { reason="Steam play history is not cached locally"; return empty; }
                    continue;
                }
                if (i==2 && (option=="1" || option=="2")) {
                    if (games.Any(g => !g.Shortcut && !g.HasMetadata)) { reason="Steam metadata is missing for controller filtering"; return empty; }
                    continue;
                }
                if (i==4 || i==8) {
                    if (games.Any(g => !g.Shortcut && !g.HasMetadata)) { reason="Steam metadata is missing for tag/category filtering"; return empty; }
                    continue;
                }
                reason="Unsupported dynamic rule (group "+i+", option "+option+")";
                return empty;
            }
            var suggestions=new HashSet<string>(Array(Vdf.Get(spec,"setSuggestions")).Select(Convert.ToString));
            return games.Where(game => {
                for (int i=0;i<groups.Length;i++) {
                    string[] options=Options(groups[i]); if (options.Length==0) continue;
                    int group=i;
                    Func<string,bool> match=option => Matches(game,group,option);
                    bool union=String.Equals(Convert.ToString(Vdf.Get(groups[i],"bAcceptUnion")),"True",StringComparison.OrdinalIgnoreCase);
                    if (!(union ? options.Any(match) : options.All(match))) return false;
                }
                return search.Length==0 || game.Id==search || suggestions.Contains(game.Id) || game.Name.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0;
            }).Select(g => g.Id).ToList();
        }
        static bool Matches(Game game,int group,string option) {
            if (group==1) {
                if (option=="1") return !game.Shortcut || game.ShortcutInstalled;
                if (option=="3") return game.LastPlayed>0;
                if (option=="4") return game.LastPlayed==0;
            }
            if (group==2) {
                if (option=="1") return game.Categories.Contains("28");
                if (option=="2") return game.Categories.Contains("28") || game.Categories.Contains("18");
            }
            if (group==4) return game.StoreTags.Contains(option);
            if (group==8) return game.Categories.Contains(option);
            return false;
        }
        public static void LoadMetadata(string steam,string config,Library library) {
            string local=Path.Combine(config,"localconfig.vdf");
            if (File.Exists(local)) try {
                object root=Vdf.Get(Vdf.Read(local),"UserLocalConfigStore");
                object software=Vdf.Get(Vdf.Get(Vdf.Get(root,"Software"),"Valve"),"Steam");
                object apps=Vdf.Get(software,"apps");
                foreach (Game game in library.Games) {
                    if (!game.Shortcut && apps!=null) game.HasPlayHistory=true;
                    long last; if (Int64.TryParse(Vdf.Str(Vdf.Get(apps,game.Id),"LastPlayed"),out last)) game.LastPlayed=Math.Max(game.LastPlayed,last);
                }
            } catch (Exception e) { library.Warnings.Add("Play history: "+e.Message); }
            string path=Path.Combine(steam,"appcache","appinfo.vdf");
            if (!File.Exists(path)) return;
            try { ReadAppInfo(path,library.Games); }
            catch (Exception e) { library.Warnings.Add("Dynamic collection metadata: "+e.Message); }
        }
        static string CString(BinaryReader r) {
            var bytes=new List<byte>(); byte b;
            while ((b=r.ReadByte())!=0) { if (bytes.Count>1024*1024) throw new InvalidDataException("Oversized appinfo string."); bytes.Add(b); }
            return Encoding.UTF8.GetString(bytes.ToArray());
        }
        public static void ReadAppInfo(string path,List<Game> games) {
            var wanted=games.Where(g => !g.Shortcut).ToDictionary(g => g.Id);
            using (var reader=new BinaryReader(File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite))) {
                uint magic=reader.ReadUInt32(); reader.ReadUInt32();
                if (magic!=0x07564427 && magic!=0x07564428 && magic!=0x07564429) throw new InvalidDataException("Unrecognised appinfo format.");
                string[] keys=null;
                long limit=reader.BaseStream.Length;
                if (magic==0x07564429) {
                    long offset=reader.ReadInt64(),start=reader.BaseStream.Position;
                    if (offset<start || offset>reader.BaseStream.Length-4) throw new InvalidDataException("Invalid appinfo string table.");
                    limit=offset; reader.BaseStream.Position=offset;
                    uint count=reader.ReadUInt32(); if (count>1000000) throw new InvalidDataException("Oversized appinfo string table.");
                    keys=new string[count]; for (int i=0;i<count;i++) keys[i]=CString(reader);
                    reader.BaseStream.Position=start;
                }
                while (reader.BaseStream.Position+4<=limit) {
                    uint id=reader.ReadUInt32(); if (id==0) break;
                    uint size=reader.ReadUInt32(); long start=reader.BaseStream.Position,end=start+size;
                    int header=magic==0x07564427 ? 40 : 60;
                    if (size<header || end>limit) throw new InvalidDataException("Truncated appinfo record.");
                    Game game;
                    if (wanted.TryGetValue(id.ToString(),out game)) {
                        reader.BaseStream.Position=start+header;
                        object data=Vdf.BinaryRecord(reader,keys);
                        if (reader.BaseStream.Position>end) throw new InvalidDataException("Appinfo data exceeds its record.");
                        object common=Vdf.Get(Vdf.Get(data,"appinfo"),"common");
                        if (common!=null) {
                            game.StoreTags=Vdf.Map(Vdf.Get(common,"store_tags")).Values.Select(Convert.ToString).Distinct().ToList();
                            game.Categories=Vdf.Map(Vdf.Get(common,"category")).Where(p => Convert.ToString(p.Value)=="1").Select(p => p.Key.Replace("category_","")).ToList();
                            string controller=Vdf.Str(common,"controller_support");
                            if (controller=="full" && !game.Categories.Contains("28")) game.Categories.Add("28");
                            if (controller=="partial" && !game.Categories.Contains("18")) game.Categories.Add("18");
                            game.HasMetadata=true;
                        }
                    }
                    reader.BaseStream.Position=end;
                }
            }
        }
    }
}
