using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using PadLauncher;
class XboxTests {
    static int checks;
    static void Check(bool value,string name) {if(!value) throw new Exception(name); checks++;}
    static int Main(string[] args) {
        string root=Path.Combine(Path.GetTempPath(),"PadLauncher-Xbox-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            if(args.Contains("--scan")) {
                var warnings=new List<string>(); var installed=XboxLibrary.Scan(warnings);
                Console.WriteLine("Xbox detection: "+installed.Count+" installed games; "+warnings.Count+" warnings.");
                foreach(var game in installed) Console.WriteLine(game.Name+" | "+game.LaunchId);
                foreach(var warning in warnings) Console.WriteLine(warning);
                return 0;
            }
            var package=new XboxPackage {Name="Publisher.Game",PackageFamilyName="Publisher.Game_123abc",InstallLocation=root};
            var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase) {{"Publisher.Game_123abc!Game","Game title"},{"Publisher.Game_123abc!Tool","Game editor"}};
            string manifest=Path.Combine(root,"AppxManifest.xml"),config=Path.Combine(root,"MicrosoftGame.config");
            File.WriteAllText(manifest,"<Package xmlns='urn:appx'><Applications><Application Id='Game'><VisualElements DisplayName='ms-resource:GameName'/></Application><Application Id='Tool'><VisualElements AppListEntry='none'/></Application></Applications></Package>");
            Check(XboxLibrary.FromPackage(package,names).Count==0,"Ordinary Windows apps are not imported as games");
            string product="{\"data\":{\"productKind\":\"GAME\",\"alternateIds\":[{\"idType\":\"PACKAGEFAMILYNAME\",\"id\":\"Publisher.Game_123abc\"}]}}";
            var catalog=XboxLibrary.CatalogGames(new[]{product,"{broken",product.Replace("GAME","APPLICATION"),"{\"data\":null}"});
            Check(catalog.Count==1 && XboxLibrary.FromPackage(package,names,catalog).Count==1,"Xbox catalog identifies games without GDK or Xbox Live manifest markers");
            Check(XboxLibrary.CatalogGames(new[]{product.Replace("GAME","APPLICATION"),product.Replace("GAME","DURABLE")}).Count==0,"Catalog applications and add-ons cannot be classified as games");
            Check(XboxLibrary.FromPackage(package,new Dictionary<string,string>(),catalog).Count==0,"Catalog entries alone cannot import uninstalled or non-launchable games");
            File.WriteAllText(config,"<Game><Identity Name='Publisher.Game'/><ExecutableList><Executable Id='Game' Name='game.exe'/></ExecutableList><ShellVisuals DefaultDisplayName='Game title'/></Game>");
            var games=XboxLibrary.FromPackage(package,names);
            Check(games.Count==1 && games[0].Name=="Game title" && games[0].Source=="xbox","GDK registration imports the named game and excludes hidden applications");
            Check(games[0].LaunchId=="Publisher.Game_123abc!Game" && games[0].Id=="xbox:publisher.game_123abc!game","Package identity produces stable IDs without package version numbers");
            Check(XboxLibrary.FromPackage(package,names).Single().Id==games[0].Id,"Repeat detection retains the same game identity");
            var start=GameLaunch.StartInfo(games[0]);
            Check(start.FileName.EndsWith("explorer.exe",StringComparison.OrdinalIgnoreCase) && start.Arguments=="shell:AppsFolder\\Publisher.Game_123abc!Game","Xbox games launch through Windows app activation");
            Check(!XboxLibrary.ValidAumid("Publisher.Game_123abc!Game /other") && !XboxLibrary.ValidAumid("https://example.com"),"Malformed activation targets are rejected");
            bool rejected=false; try {XboxLibrary.StartInfo(new Game {Source="xbox",LaunchId="invalid"});} catch(InvalidOperationException) {rejected=true;}
            Check(rejected,"Invalid launch registrations cannot become Explorer arguments");
            Check(XboxLibrary.FromPackage(package,new Dictionary<string,string>()).Count==0,"Entries without a current-user launch registration are omitted");
            File.Delete(config);
            File.WriteAllText(manifest,"<Package><Extensions><Protocol Name='ms-xbl-12345678'/></Extensions><Applications><Application Id='Game'/></Applications></Package>");
            Check(XboxLibrary.FromPackage(package,names).Count==1,"Legacy Xbox Live Windows games are detected without GDK config");
            File.WriteAllText(manifest,"<Package><Dependencies><PackageDependency Name='Microsoft.GamingServices'/></Dependencies><Applications><Application Id='Game'/></Applications></Package>");
            Check(XboxLibrary.FromPackage(package,names).Count==1,"Gaming Services package dependencies identify Xbox games");
            File.WriteAllText(manifest,"<Package><Dependencies><PackageDependency Name='Microsoft.GamingServices'/></Dependencies></Package>");
            Check(XboxLibrary.FromPackage(package,names).Count==0,"DLC and framework packages without launchable apps are excluded");
            var library=new Library(); library.Games.Add(games[0]); var settings=new Settings();
            Check(CombinedCollections.Options(library,settings).Any(list => list.Id=="source:xbox" && list.Games.Contains(games[0].Id)),"Xbox games have a source list for overlay and combined lists");
            Console.WriteLine(checks+" Xbox integration checks passed"); return 0;
        } catch(Exception error) {Console.Error.WriteLine(error); return 1;}
        finally {Directory.Delete(root,true);}
    }
}
