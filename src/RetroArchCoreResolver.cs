using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace PadLauncher {
    public sealed class RetroArchCoreResolver {
        sealed class InstalledCore {
            public string Path,Name,DisplayName;
            public HashSet<string> Databases,Extensions;
        }
        readonly string root;
        readonly List<InstalledCore> installed=new List<InstalledCore>();
        public List<string> Paths {get {return installed.Select(core => core.Path).ToList();}}
        static string Value(Dictionary<string,string> data,string key) {string value; return data.TryGetValue(key,out value) ? EmulatorImport.Scalar(value) : null;}
        static HashSet<string> Parts(string value) {return new HashSet<string>((value ?? "").Split('|').Select(part => part.Trim()).Where(part => part.Length>0),StringComparer.OrdinalIgnoreCase);}
        public RetroArchCoreResolver(string executable) {
            root=System.IO.Path.GetDirectoryName(EmulatorLibrary.Executable(executable));
            string configPath=System.IO.Path.Combine(root,"retroarch.cfg");
            if(!File.Exists(configPath)) {string detected=EmulatorImport.DetectLocation("retroarch",executable); if(File.Exists(detected)) configPath=detected;}
            var config=File.Exists(configPath) ? EmulatorImport.SettingsFile(configPath) : new Dictionary<string,string>();
            var folders=new List<string> {System.IO.Path.Combine(root,"cores")};
            string configured=Value(config,"libretro_directory"),info=Value(config,"libretro_info_path");
            if(!String.IsNullOrWhiteSpace(configured)) {string folder=EmulatorImport.Resolve(configured,root); if(Directory.Exists(folder)) {folders.Clear(); folders.Add(folder);}}
            string infoFolder=String.IsNullOrWhiteSpace(info) ? System.IO.Path.Combine(root,"info") : EmulatorImport.Resolve(info,root);
            foreach(string folder in folders.Distinct(StringComparer.OrdinalIgnoreCase)) {
                if(!Directory.Exists(folder)) continue;
                foreach(string path in Directory.GetFiles(folder,"*_libretro.dll").OrderBy(value => value,StringComparer.OrdinalIgnoreCase)) {
                    if(installed.Any(core => String.Equals(core.Path,path,StringComparison.OrdinalIgnoreCase))) continue;
                    string filename=System.IO.Path.GetFileNameWithoutExtension(path)+".info";
                    string metadata=new[]{System.IO.Path.Combine(infoFolder,filename),System.IO.Path.Combine(folder,filename),System.IO.Path.Combine(folder,"..","info",filename)}.FirstOrDefault(File.Exists);
                    var data=metadata==null ? new Dictionary<string,string>() : EmulatorImport.SettingsFile(metadata);
                    installed.Add(new InstalledCore {Path=path,Name=Value(data,"corename"),DisplayName=Value(data,"display_name"),Databases=Parts(Value(data,"database")),Extensions=Parts(Value(data,"supported_extensions"))});
                }
            }
        }
        public string Relocate(string path) {
            if(String.IsNullOrWhiteSpace(path) || path.Equals("DETECT",StringComparison.OrdinalIgnoreCase)) return null;
            try {
                string resolved=EmulatorImport.Resolve(path,root);
                if(File.Exists(resolved) && System.IO.Path.GetExtension(resolved).Equals(".dll",StringComparison.OrdinalIgnoreCase)) return EmulatorLibrary.Core(resolved);
                string filename=System.IO.Path.GetFileNameWithoutExtension(path.Replace('/',System.IO.Path.DirectorySeparatorChar))+".dll";
                var matches=installed.Where(core => System.IO.Path.GetFileName(core.Path).Equals(filename,StringComparison.OrdinalIgnoreCase)).ToList();
                return matches.Count==1 ? matches[0].Path : null;
            } catch(ArgumentException) {return null;} catch(IOException) {return null;}
        }
        string Named(string name) {
            if(String.IsNullOrWhiteSpace(name) || name.Equals("DETECT",StringComparison.OrdinalIgnoreCase)) return null;
            var matches=installed.Where(core => String.Equals(core.Name,name,StringComparison.OrdinalIgnoreCase) || String.Equals(core.DisplayName,name,StringComparison.OrdinalIgnoreCase) || String.Equals(System.IO.Path.GetFileNameWithoutExtension(core.Path).Replace("_libretro",""),name,StringComparison.OrdinalIgnoreCase)).ToList();
            return matches.Count==1 ? matches[0].Path : null;
        }
        public string Resolve(string path,string coreName,string defaultPath,string defaultName,string fallback,string content,string database,out string problem) {
            problem=null;
            string core=Relocate(path) ?? Named(coreName) ?? Relocate(defaultPath) ?? Named(defaultName) ?? Relocate(fallback);
            if(core!=null) return core;
            string system=LibretroArtwork.NormalizeDatabase(database);
            string extension=System.IO.Path.GetExtension(content ?? "").TrimStart('.');
            bool archive=extension.Equals("zip",StringComparison.OrdinalIgnoreCase) || extension.Equals("7z",StringComparison.OrdinalIgnoreCase);
            var candidates=installed.Where(item => (system==null || item.Databases.Contains(system)) && (archive || (extension.Length>0 && item.Extensions.Contains(extension)))).ToList();
            if(system==null) candidates.Clear();
            if(candidates.Count==1) return candidates[0].Path;
            problem=candidates.Count>1 ? "Multiple compatible cores found. Assign a playlist core in RetroArch or choose a default core in Pad Launcher." : "No usable core found. Install a compatible core and its info file, or select a core explicitly.";
            return null;
        }
    }
}
