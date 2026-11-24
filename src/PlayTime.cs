using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;

namespace PadLauncher {
    public static class PlayTimeTracker {
        public const string SessionFile="playtime-sessions.log";
        public static int Revision {get;private set;}
        public static long Local(Settings settings,Game game) {
            long seconds=0;
            if(settings!=null && settings.PlayTimeSeconds!=null && game!=null) settings.PlayTimeSeconds.TryGetValue(game.Id,out seconds);
            return Math.Max(0,seconds);
        }
        public static long Total(Settings settings,Game game) {
            return Local(settings,game)+(game==null ? 0 : Math.Max(0,game.ImportedPlayTimeSeconds));
        }
        public static string SteamAppId(Game game) {
            if(game==null) return null;
            string id=null;
            if(!game.Shortcut && (String.IsNullOrEmpty(game.Source) || game.Source=="steam")) id=game.Id;
            else if(game.Source=="custom") id=game.SteamLaunchId;
            if(String.IsNullOrWhiteSpace(id)) return null;
            foreach(char c in id) if(c<'0' || c>'9') return null;
            return id;
        }
        static string SteamConfig(Settings settings) {
            string root=String.IsNullOrWhiteSpace(settings.SteamPath) ? SteamLibrary.DetectSteam() : settings.SteamPath;
            if(String.IsNullOrWhiteSpace(root)) return null;
            string userdata=Path.Combine(root,"userdata");
            if(!Directory.Exists(userdata)) return null;
            string user=settings.UserId;
            if(String.IsNullOrWhiteSpace(user)) {
                DateTime newest=DateTime.MinValue;
                try {
                    foreach(string folder in Directory.GetDirectories(userdata)) {
                        string candidate=Path.Combine(folder,"config","localconfig.vdf");
                        if(!File.Exists(candidate)) continue;
                        DateTime written=File.GetLastWriteTimeUtc(candidate);
                        if(user==null || written>newest) {user=Path.GetFileName(folder); newest=written;}
                    }
                } catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            if(String.IsNullOrWhiteSpace(user)) return null;
            string path=Path.Combine(userdata,user,"config","localconfig.vdf");
            return File.Exists(path) ? path : null;
        }
        public static long SteamPlayTimeSeconds(Settings settings,string appId) {
            if(settings==null || String.IsNullOrWhiteSpace(appId)) return 0;
            string config=SteamConfig(settings);
            if(config==null) return 0;
            try {
                object root=Vdf.Get(Vdf.Read(config),"UserLocalConfigStore");
                object apps=Vdf.Get(Vdf.Get(Vdf.Get(root,"Software"),"Valve"),"Steam");
                apps=Vdf.Get(apps,"apps");
                long minutes;
                if(Int64.TryParse(Vdf.Str(Vdf.Get(apps,appId),"Playtime"),out minutes) && minutes>0) return minutes*60;
            } catch(Exception) {}
            return 0;
        }
        public static bool CaptureSteamPlayTime(Settings settings,Game game) {
            string appId=SteamAppId(game);
            if(settings==null || appId==null) return false;
            long seconds=SteamPlayTimeSeconds(settings,appId);
            if(seconds<=0) return false;
            if(settings.SteamPlayTimeAtLaunch==null) settings.SteamPlayTimeAtLaunch=new Dictionary<string,string>();
            settings.SteamPlayTimeAtLaunch[game.Id]=appId+"|"+seconds;
            return true;
        }
        public static bool ConsumeSteam(Settings settings) {
            if(settings==null || settings.SteamPlayTimeAtLaunch==null || settings.SteamPlayTimeAtLaunch.Count==0) return false;
            bool changed=false;
            foreach(string gameId in new List<string>(settings.SteamPlayTimeAtLaunch.Keys)) {
                string value=settings.SteamPlayTimeAtLaunch[gameId];
                int split=value==null ? -1 : value.IndexOf('|');
                long before;
                if(split<=0 || !Int64.TryParse(value.Substring(split+1),out before)) {settings.SteamPlayTimeAtLaunch.Remove(gameId); changed=true; continue;}
                string appId=value.Substring(0,split);
                long now=SteamPlayTimeSeconds(settings,appId);
                if(now<=0) continue;
                if(now<before) {settings.SteamPlayTimeAtLaunch.Remove(gameId); changed=true; continue;}
                if(now==before) continue;
                if(settings.PlayTimeSeconds==null) settings.PlayTimeSeconds=new Dictionary<string,long>();
                long current;
                settings.PlayTimeSeconds.TryGetValue(gameId,out current);
                settings.PlayTimeSeconds[gameId]=current+(now-before);
                settings.SteamPlayTimeAtLaunch.Remove(gameId);
                changed=true;
            }
            if(changed) Revision++;
            return changed;
        }
        public static string Hours(Settings settings,Game game) {
            long seconds=Local(settings,game);
            if(seconds<=0) return "0m";
            if(seconds<3600) return Math.Max(1,seconds/60)+"m";
            return Math.Round(seconds/3600.0,1).ToString("0.#")+"h";
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
            if(changed) Revision++;
            return changed;
        }
        public static string DirectoryToWatch(Game game) {
            if(game==null || String.IsNullOrWhiteSpace(game.WorkingDirectory)) return null;
            string source=game.Source ?? "";
            if(source!="ea" && source!="epic" && source!="gog" && source!="ubisoft" && source!="rockstar" && source!="battlenet" && source!="xbox") return null;
            try {
                string directory=Path.GetFullPath(game.WorkingDirectory);
                return Directory.Exists(directory) ? directory : null;
            } catch(ArgumentException) {return null;} catch(NotSupportedException) {return null;} catch(PathTooLongException) {return null;}
        }
        public static void Run(string[] args) {
            string gameId=Argument(args,"--game"),root=Argument(args,"--data"),directory=Argument(args,"--dir");
            long start;
            if(String.IsNullOrWhiteSpace(gameId) || String.IsNullOrWhiteSpace(root) || !Int64.TryParse(Argument(args,"--start"),out start)) return;
            if(!String.IsNullOrWhiteSpace(directory)) {
                int grace=4; long wait=300;
                int parsed;
                long parsedLong;
                if(Int32.TryParse(Argument(args,"--grace"),out parsed) && parsed>=1) grace=parsed;
                if(Int64.TryParse(Argument(args,"--wait"),out parsedLong) && parsedLong>=1) wait=parsedLong;
                WatchDirectory(gameId,root,start,directory,grace,wait);
                return;
            }
            int pid;
            if(!Int32.TryParse(Argument(args,"--pid"),out pid)) return;
            try {
                var process=Process.GetProcessById(pid);
                process.WaitForExit();
            } catch(ArgumentException) {return;} catch(InvalidOperationException) {return;}
            long end=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            WriteSession(root,gameId,start,end);
        }
        static void WatchDirectory(string gameId,string root,long start,string directory,int graceSeconds,long waitSeconds) {
            string prefix;
            try {
                prefix=Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar)+Path.DirectorySeparatorChar;
            } catch(ArgumentException) {return;} catch(NotSupportedException) {return;} catch(PathTooLongException) {return;}
            if(!Directory.Exists(prefix)) return;
            long first=0,last=0,giveUp=start+waitSeconds,graceUntil=0;
            while(true) {
                long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                int running=MatchingProcesses(prefix,start);
                if(running>0) {
                    if(first==0) first=now;
                    last=now;
                    graceUntil=0;
                } else if(first>0) {
                    if(graceUntil==0) graceUntil=now+graceSeconds;
                    else if(now>=graceUntil) break;
                } else if(now>=giveUp) return;
                if(first>0 && now-first>12*3600) break;
                Thread.Sleep(1000);
            }
            if(first>0) WriteSession(root,gameId,first,last);
        }
        static int MatchingProcesses(string directoryPrefix,long start) {
            Process[] processes;
            try {processes=Process.GetProcesses();} catch(InvalidOperationException) {return 0;}
            int count=0;
            DateTime earliest=DateTimeOffset.FromUnixTimeSeconds(Math.Max(0,start-10)).UtcDateTime;
            foreach(Process process in processes) {
                try {
                    string path;
                    try {path=process.MainModule.FileName;} catch {continue;}
                    if(String.IsNullOrEmpty(path) || !path.StartsWith(directoryPrefix,StringComparison.OrdinalIgnoreCase)) continue;
                    bool after=true;
                    try {after=process.StartTime.ToUniversalTime()>=earliest;} catch {}
                    if(after) count++;
                } finally {process.Dispose();}
            }
            return count;
        }
        static void WriteSession(string root,string gameId,long start,long end) {
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
