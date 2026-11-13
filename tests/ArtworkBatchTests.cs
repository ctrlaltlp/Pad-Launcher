using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using PadLauncher;

class ArtworkBatchTests {
    public static void Main() {
        string root=Path.Combine(Path.GetTempPath(),"ArtworkBatchTests-"+Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); Storage.Root=root;
        try {
            byte[] coverBytes;
            using(var cover=new System.Drawing.Bitmap(600,900)) using(var stream=new MemoryStream()) {
                cover.Save(stream,System.Drawing.Imaging.ImageFormat.Png); coverBytes=stream.ToArray();
            }
            ArtworkKey.Save("batch-test-key");
            var batch=new[] {
                new Game {Id="batch:fallback",Source="epic",Name="Fallback",ArtworkUrl="https://cdn1.epicgames.com/bad.png"},
                new Game {Id="batch:invalid",Source="epic",Name="Invalid",ArtworkUrl="https://cdn1.epicgames.com/bad.png"},
                new Game {Id="7654321",Source="steam",Name="Invalid grid"},
                new Game {Id="batch:later",Source="epic",Name="Later",ArtworkUrl="https://cdn1.epicgames.com/good.png"}
            };
            ArtworkService.Fetch=(url,bearer,token) => {
                token.ThrowIfCancellationRequested();
                if(url.Contains("autocomplete")) return Encoding.UTF8.GetBytes(url.EndsWith("Fallback") ? "{\"success\":true,\"data\":[{\"id\":42,\"name\":\"Fallback\"}]}" : "{\"success\":true,\"data\":[]}");
                if(url.Contains("/grids/steam/7654321")) return Encoding.UTF8.GetBytes("{\"success\":true,\"data\":[{\"url\":\"https://cdn2.steamgriddb.com/grid/bad.png\"}]}");
                if(url.Contains("/grids/")) return Encoding.UTF8.GetBytes("{\"success\":true,\"data\":[{\"url\":\"https://cdn2.steamgriddb.com/grid/good.png\"}]}");
                return url.Contains("bad.png") ? Encoding.UTF8.GetBytes("not an image") : coverBytes;
            };
            var ready=new List<string>(); var warnings=new List<string>();
            ArtworkService.Fill(batch,new Settings {SteamGridDbEnabled=true},CancellationToken.None,(game,path) => ready.Add(game.Id),warnings.Add);
            if(!ready.SequenceEqual(new[]{"batch:fallback","batch:later"}) || warnings.Count!=0)
                throw new Exception("Invalid artwork must fall back to SGDB and not stop later games.");
            var canceled=new CancellationTokenSource(); canceled.Cancel();
            bool propagated=false;
            try {ArtworkService.Fill(batch,new Settings(),canceled.Token,(game,path) => {},message => {});} catch(OperationCanceledException) {propagated=true;}
            if(!propagated) throw new Exception("Artwork batch cancellation must propagate.");
            Console.WriteLine("Artwork batch regression checks passed");
        } finally {Directory.Delete(root,true);}
    }
}
