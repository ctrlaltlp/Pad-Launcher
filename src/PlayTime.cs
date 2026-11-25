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
            return Local(settings,game)+LiveSeconds(game)+(game==null ? 0 : Math.Max(0,game.ImportedPlayTimeSeconds));
        }
        public const string LivePrefix="playtime-live-";
        static readonly object liveGate=new object();
        static string liveRoot;
        static long liveStamp;
        static Dictionary<string,long> liveMap;
        public static void InvalidateLiveCache() {
            lock(liveGate) {liveRoot=null; liveMap=null; liveStamp=0;}
        }
        static Dictionary<string,long> ReadLive(long now) {
            var map=new Dictionary<string,long>(StringComparer.Ordinal);
            string[] files;
            try {files=Directory.GetFiles(Storage.Root,LivePrefix+"*.log");}
            catch(IOException) {return map;} catch(UnauthorizedAccessException) {return map;}
            foreach(string file in files) {
                try {
                    string[] parts=File.ReadAllText(file).Trim().Split('\t');
                    if(parts.Length<3) continue;
                    long start,last;
                    if(!Int64.TryParse(parts[1],out start) || !Int64.TryParse(parts[2],out last)) continue;
                    string id;
                    try {id=Uri.UnescapeDataString(parts[0]);} catch(UriFormatException) {continue;}
                    if(last<start || now-last>15) {
                        if(now-last>60) {try {File.Delete(file);} catch(IOException) {} catch(UnauthorizedAccessException) {}}
                        continue;
                    }
                    long elapsed=Math.Min(12*3600,last-start);
                    if(elapsed<=0) continue;
                    long existing; map.TryGetValue(id,out existing);
                    map[id]=Math.Min(12*3600,existing+elapsed);
                } catch(IOException) {} catch(UnauthorizedAccessException) {}
            }
            return map;
        }
        public static long LiveSeconds(Game game) {
            if(game==null || String.IsNullOrWhiteSpace(Storage.Root)) return 0;
            long now=DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            Dictionary<string,long> map;
            lock(liveGate) {
                if(liveMap==null || !String.Equals(liveRoot,Storage.Root,StringComparison.OrdinalIgnoreCase) || now-liveStamp>1) {
                    liveMap=ReadLive(now);
                    liveRoot=Storage.Root;
                    liveStamp=now;
                }
                map=liveMap;
            }
            long seconds;
            return map.TryGetValue(game.Id,out seconds) ? seconds : 0;
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
            long seconds=Local(settings,game)+LiveSeconds(game);
            if(seconds<=0) return "0m";
            if(seconds<3600) return Math.Max(1,seconds/60)+"m";
            return (seconds/3600)+"h";
        }
        public static bool Consume(Settings settings,string root) {
            if(settings==null || String.IsNullOrWhiteSpace(root) || !Directory.Exists(root)) return false;
            string pendingPrefix=SessionFile+".processing";
            string pending;
            string[] lines;
            try {
                pending=Array.Find(Directory.GetFiles(root,pendingPrefix+".*"),file => {
                    Guid batchId;
                    return Guid.TryParseExact(Path.GetExtension(file).TrimStart('.'),"N",out batchId);
                });
                if(pending==null) {
                    string legacy=Path.Combine(root,pendingPrefix);
                    string source=File.Exists(legacy) ? legacy : Path.Combine(root,SessionFile);
                    if(!File.Exists(source)) return false;
                    pending=Path.Combine(root,pendingPrefix+"."+Guid.NewGuid().ToString("N"));
                    File.Move(source,pending);
                }
                lines=File.ReadAllLines(pending);
            } catch(IOException) {return false;} catch(UnauthorizedAccessException) {return false;}

            string batch=Path.GetFileName(pending);
            bool changed=false;
            if(batch!=settings.LastAppliedPlayTimeBatch) {
                var previousTotals=settings.PlayTimeSeconds;
                var totals=previousTotals==null ? new Dictionary<string,long>() : new Dictionary<string,long>(previousTotals);
                foreach(string line in lines) {
                    string[] parts=line.Split('\t');
                    long start,end;
                    if(parts.Length<3 || !Int64.TryParse(parts[1],out start) || !Int64.TryParse(parts[2],out end) || end<=start) continue;
                    string id;
                    try {id=Uri.UnescapeDataString(parts[0]);} catch(UriFormatException) {continue;}
                    if(String.IsNullOrWhiteSpace(id)) continue;
                    long current;
                    totals.TryGetValue(id,out current);
                    totals[id]=current+(end-start);
                    changed=true;
                }
                if(changed) {
                    // Commit the batch identity with the totals so a leftover log cannot be counted twice.
                    string previousBatch=settings.LastAppliedPlayTimeBatch;
                    settings.PlayTimeSeconds=totals;
                    settings.LastAppliedPlayTimeBatch=batch;
                    bool saved=false;
                    try {Storage.Write(root,"settings.json",settings); saved=true;}
                    catch(IOException) {} catch(UnauthorizedAccessException) {}
                    if(!saved) {
                        settings.PlayTimeSeconds=previousTotals;
                        settings.LastAppliedPlayTimeBatch=previousBatch;
                        return false;
                    }
                    Revision++;
                }
            }
            try {File.Delete(pending);} catch(IOException) {} catch(UnauthorizedAccessException) {}
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
                using(var process=Process.GetProcessById(pid)) {
                    while(!process.WaitForExit(1000)) WriteLive(root,gameId,start);
                }
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
                if(first>0) WriteLive(root,gameId,first);
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
            ClearLive(root);
            if(end<=start) return;
            try {
                Directory.CreateDirectory(root);
                string line=Uri.EscapeDataString(gameId)+"\t"+start+"\t"+end+Environment.NewLine;
                using(var stream=new FileStream(Path.Combine(root,SessionFile),FileMode.Append,FileAccess.Write,FileShare.ReadWrite))
                using(var writer=new StreamWriter(stream,new UTF8Encoding(false))) writer.Write(line);
            } catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
        static void WriteLive(string root,string gameId,long start) {
            if(String.IsNullOrWhiteSpace(root) || String.IsNullOrWhiteSpace(gameId) || start<=0) return;
            try {
                Directory.CreateDirectory(root);
                string line=Uri.EscapeDataString(gameId)+"\t"+start+"\t"+DateTimeOffset.UtcNow.ToUnixTimeSeconds()+Environment.NewLine;
                File.WriteAllText(Path.Combine(root,LivePrefix+Process.GetCurrentProcess().Id+".log"),line,new UTF8Encoding(false));
            } catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
        static void ClearLive(string root) {
            if(String.IsNullOrWhiteSpace(root)) return;
            try {File.Delete(Path.Combine(root,LivePrefix+Process.GetCurrentProcess().Id+".log"));}
            catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
        static string Argument(string[] args,string name) {
            for(int index=0;index+1<args.Length;index++) if(String.Equals(args[index],name,StringComparison.OrdinalIgnoreCase)) return args[index+1];
            return null;
        }
    }
}
