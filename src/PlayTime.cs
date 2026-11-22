using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace PadLauncher {
    public static class PlayTimeTracker {
        public const string SessionFile="playtime-sessions.log";
        public static long Local(Settings settings,Game game) {
            long seconds=0;
            if(settings!=null && settings.PlayTimeSeconds!=null && game!=null) settings.PlayTimeSeconds.TryGetValue(game.Id,out seconds);
            return Math.Max(0,seconds);
        }
        public static long Total(Settings settings,Game game) {
            return Local(settings,game)+(game==null ? 0 : Math.Max(0,game.ImportedPlayTimeSeconds));
        }
        public static bool Consume(Settings settings,string root) {
            if(settings==null || String.IsNullOrWhiteSpace(root)) return false;
            string path=Path.Combine(root,SessionFile);
            string pending=path+".processing";
            var completed=new List<string>();
            if(File.Exists(pending)) completed.Add(pending);
            if(File.Exists(path)) {
                try {File.Move(path,pending); completed.Add(pending);} catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            bool changed=false;
            foreach(string file in completed) {
                string[] lines;
                try {lines=File.ReadAllLines(file);} catch(IOException) {continue;} catch(UnauthorizedAccessException) {continue;}
                foreach(string line in lines) {
                    string[] parts=line.Split('\t');
                    if(parts.Length<3) continue;
                    long start,end;
                    if(!Int64.TryParse(parts[1],out start) || !Int64.TryParse(parts[2],out end) || end<=start) continue;
                    string id;
                    try {id=Uri.UnescapeDataString(parts[0]);} catch {continue;}
                    if(String.IsNullOrWhiteSpace(id)) continue;
                    if(settings.PlayTimeSeconds==null) settings.PlayTimeSeconds=new Dictionary<string,long>();
                    long current;
                    settings.PlayTimeSeconds.TryGetValue(id,out current);
                    settings.PlayTimeSeconds[id]=current+(end-start);
                    changed=true;
                }
                try {File.Delete(file);} catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            return changed;
        }
        public static void Run(string[] args) {
            string gameId=Argument(args,"--game"),root=Argument(args,"--data");
            int pid;
            long start;
            if(String.IsNullOrWhiteSpace(gameId) || String.IsNullOrWhiteSpace(root) || !Int32.TryParse(Argument(args,"--pid"),out pid) || !Int64.TryParse(Argument(args,"--start"),out start)) return;
            try {
                var process=Process.GetProcessById(pid);
                process.WaitForExit();
            } catch(ArgumentException) {return;} catch(InvalidOperationException) {return;}
            long end=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if(end<=start) return;
            try {
                Directory.CreateDirectory(root);
                string line=Uri.EscapeDataString(gameId)+"\t"+start+"\t"+end+Environment.NewLine;
                using(var stream=new FileStream(Path.Combine(root,SessionFile),FileMode.Append,FileAccess.Write,FileShare.ReadWrite))
                using(var writer=new StreamWriter(stream,new UTF8Encoding(false))) writer.Write(line);
            } catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
        static string Argument(string[] args,string name) {
            for(int index=0;index+1<args.Length;index++) if(String.Equals(args[index],name,StringComparison.OrdinalIgnoreCase)) return args[index+1];
            return null;
        }
    }
}
