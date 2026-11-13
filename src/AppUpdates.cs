using System;
using System.IO;
using System.Net;
using System.Linq;
using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Threading;
using System.Collections.Generic;

namespace PadLauncher {
    public sealed class AppRelease {
        public Version Version;
        public string Url;
        public string Sha256;
        public long Size;
    }
    public sealed class AppUpdateJob {
        public string Target {get;set;}
        public string Payload {get;set;}
        public string Sha256 {get;set;}
        public string DataRoot {get;set;}
        public string ResumeFile {get;set;}
        public int ProcessId {get;set;}
    }
    public static class AppUpdates {
        public const string LatestUrl="https://api.github.com/repos/lpv11/Pad-Launcher/releases/latest";
        const long MaxDownload=100*1024*1024;
        public static Version CurrentVersion {get {return Assembly.GetExecutingAssembly().GetName().Version;}}
        public static string DisplayVersion {get {
            var info=(AssemblyInformationalVersionAttribute)Attribute.GetCustomAttribute(Assembly.GetExecutingAssembly(),typeof(AssemblyInformationalVersionAttribute));
            return info==null ? CurrentVersion.ToString() : info.InformationalVersion;
        }}
        public static AppRelease Parse(string json) {
            var release=Vdf.Map(Storage.Json().DeserializeObject(json));
            if(Vdf.Str(release,"draft")=="True" || Vdf.Str(release,"prerelease")=="True") throw new InvalidDataException("Only stable releases are supported.");
            Version version;
            if(!Version.TryParse(Vdf.Str(release,"tag_name").TrimStart('v','V'),out version)) throw new InvalidDataException("The release has an invalid version.");
            var assets=Vdf.Get(release,"assets") as System.Collections.IEnumerable;
            if(assets!=null) foreach(var item in assets) {
                var asset=Vdf.Map(item);
                if(Vdf.Str(asset,"name")!="PadLauncher.exe" || Vdf.Str(asset,"state")!="uploaded") continue;
                string url=Vdf.Str(asset,"browser_download_url"),digest=Vdf.Str(asset,"digest");
                Uri uri; long size;
                if(!Uri.TryCreate(url,UriKind.Absolute,out uri) || uri.Scheme!="https" || uri.Host!="github.com" || !uri.AbsolutePath.StartsWith("/lpv11/Pad-Launcher/releases/download/",StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unexpected update download location.");
                if(!digest.StartsWith("sha256:",StringComparison.OrdinalIgnoreCase) || digest.Length!=71 || !digest.Substring(7).All(Uri.IsHexDigit)) throw new InvalidDataException("The release has no valid SHA-256 checksum.");
                if(!Int64.TryParse(Vdf.Str(asset,"size"),out size) || size<=0 || size>MaxDownload) throw new InvalidDataException("Invalid update size.");
                return new AppRelease {Version=version,Url=url,Sha256=digest.Substring(7),Size=size};
            }
            throw new InvalidDataException("The latest release has no PadLauncher.exe download.");
        }
        public static bool IsNewer(Version candidate,Version current) {
            Func<Version,Version> full=v => new Version(v.Major,v.Minor,Math.Max(0,v.Build),Math.Max(0,v.Revision));
            return full(candidate)>full(current);
        }
        static HttpWebResponse Open(string url,CancellationToken token) {
            ServicePointManager.SecurityProtocol|=SecurityProtocolType.Tls12;
            for(int redirects=0;redirects<6;redirects++) {
                token.ThrowIfCancellationRequested();
                var uri=new Uri(url);
                if(uri.Scheme!="https" || !(uri.Host=="api.github.com" || uri.Host=="github.com" || uri.Host=="release-assets.githubusercontent.com" || uri.Host=="objects.githubusercontent.com")) throw new InvalidDataException("Unexpected update server.");
                var request=(HttpWebRequest)WebRequest.Create(uri);
                request.UserAgent="PadLauncher/"+CurrentVersion; request.Accept="application/vnd.github+json";
                request.AllowAutoRedirect=false; request.Timeout=20000; request.ReadWriteTimeout=20000;
                HttpWebResponse response;
                using(token.Register(request.Abort)) {
                    try {response=(HttpWebResponse)request.GetResponse();}
                    catch(WebException) {token.ThrowIfCancellationRequested(); throw;}
                }
                if((int)response.StatusCode>=300 && (int)response.StatusCode<400) {
                    string next=response.Headers["Location"]; response.Dispose();
                    url=new Uri(uri,next).AbsoluteUri; continue;
                }
                return response;
            }
            throw new IOException("Too many update download redirects.");
        }
        public static AppRelease Check(CancellationToken token) {
            using(var response=Open(LatestUrl,token)) using(var stream=response.GetResponseStream()) using(var output=new MemoryStream()) {
                Copy(stream,output,2*1024*1024,token);
                return Parse(System.Text.Encoding.UTF8.GetString(output.ToArray()));
            }
        }
        static void Copy(Stream input,Stream output,long limit,CancellationToken token) {
            var buffer=new byte[65536]; long total=0; int read;
            while((read=input.Read(buffer,0,buffer.Length))>0) {
                token.ThrowIfCancellationRequested(); total+=read;
                if(total>limit) throw new InvalidDataException("The update exceeds its expected size.");
                output.Write(buffer,0,read);
            }
            token.ThrowIfCancellationRequested();
        }
        public static string Hash(string file) {
            using(var stream=File.OpenRead(file)) using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        public static void Verify(string file,string expected) {
            if(!String.Equals(Hash(file),expected,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("The downloaded update failed checksum verification.");
            AssemblyName.GetAssemblyName(file);
        }
        public static string Download(AppRelease release,CancellationToken token) {
            string folder=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"PadLauncher.Update",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            string payload=Path.Combine(folder,"PadLauncher.new.exe");
            try {
                using(var response=Open(release.Url,token)) using(var input=response.GetResponseStream()) using(var output=File.Create(payload)) Copy(input,output,release.Size,token);
                if(new FileInfo(payload).Length!=release.Size) throw new InvalidDataException("The update download is incomplete.");
                Verify(payload,release.Sha256); return payload;
            } catch {try {File.Delete(payload); Directory.Delete(folder);} catch {} throw;}
        }
        public static void StartInstaller(string payload,string digest,string resumeFile=null) {
            string current=Assembly.GetExecutingAssembly().Location;
            string folder=Path.GetDirectoryName(payload),helper=Path.Combine(folder,"PadLauncher.Updater.exe"),job=Path.Combine(folder,"update.json");
            Verify(payload,digest);
            File.Copy(current,helper,true);
            File.WriteAllText(job,Storage.Json().Serialize(new AppUpdateJob {Target=current,Payload=payload,Sha256=digest,DataRoot=Storage.Root,ResumeFile=resumeFile,ProcessId=Process.GetCurrentProcess().Id}));
            Process.Start(new ProcessStartInfo(helper,"--apply-update \""+job+"\"") {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder});
        }
        public static string ReplaceExecutable(AppUpdateJob job) {
            Verify(job.Payload,job.Sha256);
            string backup=Path.Combine(Path.GetDirectoryName(job.Target),Path.GetFileNameWithoutExtension(job.Target)+".backup-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+"-"+Guid.NewGuid().ToString("N").Substring(0,6)+".exe");
            File.Replace(job.Payload,job.Target,backup);
            return backup;
        }
        public static bool RunInstaller(string[] args,Action<string> reportError=null) {
            if(args.Length==0 || args[0]!="--apply-update") return false;
            AppUpdateJob job=null; string backup=null;
            try {
                if(args.Length!=2) throw new ArgumentException("Invalid update request.");
                job=Storage.Json().Deserialize<AppUpdateJob>(File.ReadAllText(args[1]));
                string helper=Assembly.GetExecutingAssembly().Location,folder=Path.GetDirectoryName(helper);
                if(!String.Equals(Path.GetDirectoryName(Path.GetFullPath(job.Payload)),folder,StringComparison.OrdinalIgnoreCase) ||
                    !String.Equals(Path.GetDirectoryName(folder),Path.Combine(Path.GetDirectoryName(Path.GetFullPath(job.Target)),"PadLauncher.Update"),StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Invalid update paths.");
                try {using(var parent=Process.GetProcessById(job.ProcessId)) {if(!parent.WaitForExit(30000)) throw new IOException("Pad Launcher is still running. Close it and try again.");}}
                catch(ArgumentException) {}
                for(int attempt=0;;attempt++) {
                    try {backup=ReplaceExecutable(job); break;}
                    catch(IOException) {if(attempt>=19) throw; Thread.Sleep(250);}
                }
                try {Restart(job);} catch {File.Copy(backup,job.Target,true); throw;}
                try {File.Delete(args[1]);} catch {}
            } catch(Exception error) {
                string message="Pad Launcher could not finish updating. Your settings have been preserved.\n\n"+error.Message;
                if(reportError!=null) reportError(message);
                else System.Windows.Forms.MessageBox.Show(message,"Pad Launcher update",System.Windows.Forms.MessageBoxButtons.OK,System.Windows.Forms.MessageBoxIcon.Error);
                if(job!=null && File.Exists(job.Target)) try {Restart(job);} catch {}
            }
            return true;
        }
        public static void CleanOldDownloads() {
            try {
                string root=Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),"PadLauncher.Update");
                if(!Directory.Exists(root)) return;
                foreach(string folder in Directory.GetDirectories(root)) {
                    Guid id;
                    if(!Guid.TryParseExact(Path.GetFileName(folder),"N",out id) || Directory.GetLastWriteTimeUtc(folder)>DateTime.UtcNow.AddDays(-1)) continue;
                    try {
                        foreach(string name in new[]{"PadLauncher.new.exe","PadLauncher.Updater.exe","update.json","resume.dat"}) File.Delete(Path.Combine(folder,name));
                        Directory.Delete(folder);
                    } catch {}
                }
            } catch {}
        }
        static void Restart(AppUpdateJob job) {
            string arguments="--data \""+job.DataRoot+"\"";
            if(!String.IsNullOrEmpty(job.ResumeFile)) arguments+=" --resume-update \""+job.ResumeFile+"\"";
            Process.Start(new ProcessStartInfo(job.Target,arguments) {UseShellExecute=true,WorkingDirectory=Path.GetDirectoryName(job.Target)});
        }
    }
}
