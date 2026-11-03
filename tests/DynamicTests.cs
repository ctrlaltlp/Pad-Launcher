using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using PadLauncher;

public static class DynamicTests {
    static int checks;
    static void Check(bool ok,string name) { if (!ok) throw new Exception("FAILED: "+name); checks++; Console.WriteLine("PASS "+name); }
    static object Spec(int group,int[] options,bool union,string search) {
        object[] groups=Enumerable.Range(0,9).Select(i => (object)new { rgOptions=i==group ? options : new int[0],bAcceptUnion=union }).ToArray();
        return Storage.Json().DeserializeObject(Storage.Json().Serialize(new { nFormatVersion=2,strSearchText=search,filterGroups=groups }));
    }
    static List<string> Match(object spec,List<Game> games) { string reason; var result=DynamicRules.Evaluate(spec,games,out reason); if (reason!=null) throw new Exception(reason); return result; }
    static void CString(BinaryWriter writer,string value) { writer.Write(Encoding.UTF8.GetBytes(value)); writer.Write((byte)0); }
    public static int Main() {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-dynamic-tests-"+Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(root);
            var games=new List<Game> {
                new Game { Id="10",Name="First Ω",HasMetadata=true,HasPlayHistory=true,LastPlayed=42,StoreTags=new List<string>{"100","200"},Categories=new List<string>{"28","22"} },
                new Game { Id="20",Name="Second Game",HasMetadata=true,HasPlayHistory=true,StoreTags=new List<string>{"100"},Categories=new List<string>{"18"} },
                new Game { Id="30",Name="My Shortcut",Shortcut=true,HasPlayHistory=true }
            };
            Check(Match(Spec(1,new[]{1},false,""),games).SequenceEqual(new[]{"10","20"}),"Installed dynamic rule excludes non-Steam shortcuts like Steam");
            Check(Match(Spec(1,new[]{3},false,""),games).SequenceEqual(new[]{"10"}),"Played dynamic rule uses profile history");
            Check(Match(Spec(1,new[]{4},false,""),games).SequenceEqual(new[]{"20","30"}),"Unplayed dynamic rule includes unplayed shortcuts");
            Check(Match(Spec(4,new[]{100,200},false,""),games).SequenceEqual(new[]{"10"}),"All-tag rules require every selected tag");
            Check(Match(Spec(4,new[]{100,200},true,""),games).SequenceEqual(new[]{"10","20"}),"Any-tag rules accept either selected tag");
            Check(Match(Spec(2,new[]{1},false,""),games).SequenceEqual(new[]{"10"}),"Full controller support filter");
            Check(Match(Spec(2,new[]{2},false,""),games).SequenceEqual(new[]{"10","20"}),"Any controller support includes full and partial support");
            Check(Match(Spec(8,new[]{22},false,""),games).SequenceEqual(new[]{"10"}),"Store-category rules evaluate cached categories");
            Check(Match(Spec(4,new[]{100},false,"SECOND"),games).SequenceEqual(new[]{"20"}),"Search and tag predicates are combined");
            Check(Match(Spec(4,new int[0],false,"30"),games).SequenceEqual(new[]{"30"}),"Dynamic search supports app IDs");
            Check(Match(Spec(4,new int[0],false,""),games).Count==0,"Empty dynamic filters do not accidentally select the whole library");
            string config=Path.Combine(root,"config"); Directory.CreateDirectory(Path.Combine(config,"cloudstorage"));
            object[] cloud={ new object[] {"user-collections.installed",new {value=Storage.Json().Serialize(new {name="Installed",filterSpec=Spec(1,new[]{1},false,""),added=new[]{30},removed=new[]{20}})}} };
            File.WriteAllText(Path.Combine(config,"cloudstorage","cloud-storage-namespace-1.json"),Storage.Json().Serialize(cloud));
            var library=new Library {Games=games}; SteamLibrary.ReadCollections(config,library);
            Check(library.Collections.Single().Games.OrderBy(id=>id).SequenceEqual(new[]{"10","30"}),"Manual additions and removals apply after dynamic matching");
            games.Add(new Game {Id="40",Name="Newly installed",HasMetadata=true,HasPlayHistory=true});
            SteamLibrary.ReadCollections(config,library);
            Check(library.Collections.Single().Games.Contains("40"),"Refresh recalculates dynamic membership when the installed library changes");
            games.RemoveAt(games.Count-1);
            string reason;
            DynamicRules.Evaluate(Spec(6,new[]{123},false,""),games,out reason);
            Check(reason!=null,"Friend ownership rules report unsupported instead of guessing");
            games[1].HasMetadata=false;
            Check(DynamicRules.Evaluate(Spec(4,new[]{100},false,""),games,out reason).Count==0 && reason!=null,"Missing metadata cannot silently omit matching games");
            games[1].HasMetadata=true; games[1].HasPlayHistory=false;
            DynamicRules.Evaluate(Spec(1,new[]{4},false,""),games,out reason);
            Check(reason!=null,"Missing play history is distinguished from never played");
            string file=Path.Combine(root,"appinfo.vdf");
            string[] keys={"appinfo","common","store_tags","0","category","category_28","controller_support"};
            using (var w=new BinaryWriter(File.Create(file))) {
                w.Write(0x07564429U); w.Write(1U); long tablePointer=w.BaseStream.Position; w.Write(0L);
                w.Write(10U); long sizePointer=w.BaseStream.Position; w.Write(0U); long recordStart=w.BaseStream.Position; w.Write(new byte[60]);
                w.Write((byte)0); w.Write(0U); // appinfo
                w.Write((byte)0); w.Write(1U); // common
                w.Write((byte)0); w.Write(2U); // store_tags
                w.Write((byte)1); w.Write(3U); CString(w,"100"); w.Write((byte)8);
                w.Write((byte)0); w.Write(4U); // category
                w.Write((byte)1); w.Write(5U); CString(w,"1"); w.Write((byte)8);
                w.Write((byte)1); w.Write(6U); CString(w,"full");
                w.Write((byte)8); w.Write((byte)8); w.Write((byte)8);
                long recordEnd=w.BaseStream.Position; w.Write(0U); long table=w.BaseStream.Position;
                w.Write(keys.Length); foreach (string key in keys) CString(w,key);
                w.BaseStream.Position=tablePointer; w.Write(table);
                w.BaseStream.Position=sizePointer; w.Write((uint)(recordEnd-recordStart));
            }
            var target=new Game {Id="10",Name="Cached game"};
            DynamicRules.ReadAppInfo(file,new List<Game>{target});
            Check(target.HasMetadata && target.StoreTags.Contains("100") && target.Categories.Contains("28"),"Current indexed appinfo format reads tags and controller categories");
            File.WriteAllBytes(file,new byte[]{0,0,0,0,0,0,0,0});
            bool rejected=false; try { DynamicRules.ReadAppInfo(file,new List<Game>{target}); } catch (InvalidDataException) { rejected=true; }
            Check(rejected,"Unknown appinfo versions are rejected");
            Console.WriteLine(checks+" dynamic checks passed."); return 0;
        } catch (Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { if (Directory.Exists(root) && Path.GetFileName(root).StartsWith("PadLauncher-dynamic-tests-")) Directory.Delete(root,true); }
    }
}
