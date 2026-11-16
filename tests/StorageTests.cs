using System;
using System.IO;
using System.Collections.Generic;
using PadLauncher;

class StorageTests {
    static void Check(bool value,string message) {if(!value) throw new Exception(message);}
    static void Main() {
        string original=Storage.Root;
        Storage.Root=Path.Combine(Path.GetTempPath(),"PadStorageTests-"+Guid.NewGuid().ToString("N"));
        try {
            var data=new Dictionary<string,object> {
                {"path","C:\\Games\\\"quoted\"\\"}, {"text","{brackets} [array], colon: newline\n雪"},
                {"nested",new object[]{new Dictionary<string,object>{{"empty",new object[0]}},null,true,1.25}}
            };
            string readable=Storage.ReadableJson(data);
            Check(readable.Contains(Environment.NewLine+"  \"path\": "),"Indentation is readable");
            Check(Storage.Json().Serialize(Storage.Json().DeserializeObject(readable))==Storage.Json().Serialize(data),"Formatting preserves escapes, Unicode, values and nesting");
            Storage.Write("settings.json",data);
            Check(Storage.Read<Dictionary<string,object>>("settings.json")["path"].Equals(data["path"]),"Formatted files round-trip through storage");
            Storage.Write("settings.json",new Settings());
            Check(Storage.Read<Settings>("settings.json")!=null,"Atomic replacement writes readable settings");
            File.WriteAllText(Path.Combine(Storage.Root,"legacy.json"),Storage.Json().Serialize(data));
            Check(Storage.Read<Dictionary<string,object>>("legacy.json")!=null,"Existing compact JSON stays compatible");
            Console.WriteLine("5 storage checks passed.");
        } finally {Directory.Delete(Storage.Root,true); Storage.Root=original;}
    }
}
