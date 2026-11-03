using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using PadLauncher;

public static class LibraryTests {
    static int checks;
    static void Check(bool ok,string name) { if (!ok) throw new Exception("FAILED: "+name); checks++; Console.WriteLine("PASS "+name); }
    static void CString(BinaryWriter writer,string value) { writer.Write(Encoding.UTF8.GetBytes(value)); writer.Write((byte)0); }
    static void String(BinaryWriter writer,string key,string value) { writer.Write((byte)1); CString(writer,key); CString(writer,value); }
    public static int Main() {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-tests-"+Guid.NewGuid().ToString("N"));
        try {
            var text=Vdf.Parse("// comment\n\"root\" { \"title\" \"A \\\"quoted\\\" title\" \"path\" \"D:\\\\Games\" bare value }");
            Check(Vdf.Str(Vdf.Get(text,"ROOT"),"title")=="A \"quoted\" title","VDF comments, case-insensitive keys and quoted escapes");
            Check(Vdf.Str(Vdf.Get(text,"root"),"path")==@"D:\Games","VDF Windows paths");
            Directory.CreateDirectory(Path.Combine(root,"steamapps"));
            string second=Path.Combine(root,"other-library"); Directory.CreateDirectory(Path.Combine(second,"steamapps"));
            File.WriteAllText(Path.Combine(root,"steamapps","libraryfolders.vdf"),"\"libraryfolders\" { \"0\" { \"path\" \""+root.Replace("\\","\\\\")+"\" } \"1\" { \"path\" \""+second.Replace("\\","\\\\")+"\" } }");
            File.WriteAllText(Path.Combine(root,"steamapps","appmanifest_10.acf"),"\"AppState\" { \"appid\" \"10\" \"name\" \"First game\" \"StateFlags\" \"4\" }");
            File.WriteAllText(Path.Combine(second,"steamapps","appmanifest_20.acf"),"\"AppState\" { \"appid\" \"20\" \"name\" \"Second game\" \"StateFlags\" \"6\" }");
            File.WriteAllText(Path.Combine(root,"steamapps","appmanifest_30.acf"),"\"AppState\" { \"appid\" \"30\" \"name\" \"Not installed\" \"StateFlags\" \"1\" }");
            File.WriteAllText(Path.Combine(root,"steamapps","appmanifest_40.acf"),"\"AppState\" { \"appid\" \"40\" \"name\" \"Proton 10.0\" \"StateFlags\" \"4\" }");
            string config=Path.Combine(root,"userdata","123","config"); Directory.CreateDirectory(config);
            using (var w=new BinaryWriter(File.Create(Path.Combine(config,"shortcuts.vdf")))) {
                w.Write((byte)0); CString(w,"shortcuts");
                for (int i=0;i<2;i++) {
                    w.Write((byte)0); CString(w,i.ToString());
                    w.Write((byte)2); CString(w,"appid"); w.Write(3000000000U+(uint)i);
                    String(w,"AppName",i==0 ? "Shortcut Ω" : "Other shortcut"); String(w,"exe","\"D:\\My Game\\game.exe\"");
                    w.Write((byte)0); CString(w,"tags"); String(w,"0","Local tag"); String(w,"1","Couch"); w.Write((byte)8);
                    w.Write((byte)8);
                }
                w.Write((byte)8); w.Write((byte)8);
            }
            var c=new { name="Couch",added=new object[] {10,20,3000000000U,3000000001U},removed=new object[] {20,3000000001U} };
            var dynamic=new { name="Dynamic",added=new int[] {10},filterSpec=new { tags=new int[] {1} } };
            object[] entries={
                new object[] {"user-collections.couch",new { value=Storage.Json().Serialize(c) }},
                new object[] {"user-collections.dynamic",new { value=Storage.Json().Serialize(dynamic) }},
                new object[] {"user-collections.emptyfilter",new { value=Storage.Json().Serialize(new { name="Empty filter",added=new int[] {10},filterSpec=new { strSearchText="",filterGroups=new object[] { new { rgOptions=new int[0] } } } }) }},
                new object[] {"user-collections.deleted",new { value=Storage.Json().Serialize(new { name="Deleted",added=new int[] {10} }),is_deleted=true }}
            };
            Directory.CreateDirectory(Path.Combine(config,"cloudstorage"));
            File.WriteAllText(Path.Combine(config,"cloudstorage","cloud-storage-namespace-1.json"),Storage.Json().Serialize(entries));
            Directory.CreateDirectory(Path.Combine(config,"grid"));
            string custom=Path.Combine(config,"grid","3000000000p.png"); File.WriteAllBytes(custom,new byte[] {1});
            Directory.CreateDirectory(Path.Combine(root,"appcache","librarycache","10"));
            string hero=Path.Combine(root,"appcache","librarycache","10","abc_library_hero_2x.jpg"); File.WriteAllBytes(hero,new byte[] {1});
            string hashed=Path.Combine(root,"appcache","librarycache","20","asset-hash"); Directory.CreateDirectory(hashed);
            string nestedCover=Path.Combine(hashed,"library_600x900.jpg"),nestedHero=Path.Combine(hashed,"library_hero.jpg");
            File.WriteAllBytes(nestedCover,new byte[]{1}); File.WriteAllBytes(nestedHero,new byte[]{1});
            File.WriteAllBytes(Path.Combine(hashed,"library_hero_blur.jpg"),new byte[]{1});
            Library library=SteamLibrary.Scan(root,"123");
            Check(library.Games.Count==4,"Installed games from multiple libraries plus non-Steam shortcuts");
            Check(!library.Games.Any(g => g.Id=="30"),"Uninstalled manifests excluded");
            Check(!library.Games.Any(g => g.Id=="40"),"Compatibility runtimes excluded");
            Check(library.Games.Single(g => g.Id=="3000000000").Name=="Shortcut Ω","Binary VDF Unicode and unsigned shortcut IDs");
            Check(library.Games.Single(g => g.Id=="3000000000").LaunchId=="12884901888033554432","64-bit non-Steam Steam launch ID");
            Check(library.Games.Single(g => g.Id=="3000000000").Art==custom,"Existing custom shortcut artwork preferred");
            Check(library.Games.Single(g => g.Id=="10").Hero==hero,"Nested hashed Steam library artwork");
            Check(library.Games.Single(g => g.Id=="20").Art==nestedCover,"Cover artwork inside Steam asset-hash subfolders");
            Check(library.Games.Single(g => g.Id=="20").Hero==nestedHero,"Full hero artwork preferred over blurred variant in asset-hash subfolders");
            var couch=library.Collections.Single(x => x.Name=="Couch");
            Check(couch.Games.Contains("10") && couch.Games.Contains("3000000000") && !couch.Games.Contains("20") && !couch.Games.Contains("3000000001"),"Collection membership and explicit removals override shortcut tags");
            Check(library.Collections.Single(x => x.Name=="Local tag").Games.Count==2,"Shortcut tag fallback contains every matching shortcut");
            Check(library.Collections.Single(x => x.Name=="Dynamic").Dynamic,"Dynamic collections explicitly identified");
            Check(library.Collections.Single(x => x.Name=="Dynamic").UnsupportedDynamic,"Unknown dynamic schemas cannot silently return incomplete membership");
            Check(!library.Collections.Single(x => x.Name=="Empty filter").UnsupportedDynamic,"Empty filter objects retain usable explicit collection membership");
            Check(!library.Collections.Any(x => x.Name=="Deleted"),"Deleted collections ignored");
            string localJson=Storage.Json().Serialize(new Dictionary<string,object> { {"couch",new {added=new uint[] {3000000001U},removed=new uint[] {3000000000U}}} });
            File.WriteAllText(Path.Combine(config,"localconfig.vdf"),"\"UserLocalConfigStore\" { \"WebStorage\" { \"user-collections\" \""+localJson.Replace("\\","\\\\").Replace("\"","\\\"")+"\" } }");
            var merged=SteamLibrary.Scan(root,"123").Collections.Single(x => x.Name=="Couch");
            Check(!merged.Games.Contains("3000000000") && !merged.Games.Contains("3000000001") && merged.Games.Contains("10"),"Local shortcut membership merges with cloud removals taking precedence");
            File.WriteAllBytes(Path.Combine(config,"shortcuts.vdf"),new byte[] {0,1});
            var damaged=SteamLibrary.Scan(root,"123");
            Check(damaged.Games.Count==2 && damaged.Warnings.Any(x => x.StartsWith("Non-Steam")),"Corrupt shortcuts preserve usable Steam library and report a warning");
            string cache=Storage.Json().Serialize(library);
            var roundtrip=Storage.Json().Deserialize<Library>(cache);
            Check(roundtrip.Games.Count==4 && roundtrip.Collections.Count==4,"Library cache roundtrip");
            Console.WriteLine(checks+" checks passed."); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("PadLauncher-tests-")) Directory.Delete(root,true); }
    }
}
