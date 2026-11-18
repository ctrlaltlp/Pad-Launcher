using System;
using System.Linq;
using System.Collections.Generic;

namespace PadLauncher {
    public static class RetroArchListDefaults {
        public static bool Applies(Settings settings,string id) {return Applies(settings,id,new HashSet<string>());}
        static bool Applies(Settings settings,string id,HashSet<string> visiting) {
            if(String.IsNullOrEmpty(id) || !visiting.Add(id)) return false;
            var config=EmulatorLibrary.Config(settings,"retroarch");
            var known=new HashSet<string>((config==null ? new List<EmulatorGame>() : config.Games ?? new List<EmulatorGame>()).Select(game => game.Id));
            Func<string,bool> isRetro=game => known.Contains(game) || (game ?? "").StartsWith("emulator:retroarch:",StringComparison.Ordinal);
            List<string> extra;
            if(settings.ListGameAdditions!=null && settings.ListGameAdditions.TryGetValue(id,out extra) && extra!=null && !extra.All(isRetro)) return false;
            if(id=="source:emulator:retroarch") return true;
            var custom=(settings.CustomLists ?? new List<CustomList>()).FirstOrDefault(list => list.Id==id);
            if(custom!=null) {
                var ids=custom.Games ?? new List<string>();
                return ids.Count>0 ? ids.All(isRetro) : (settings.EmulatorImports ?? new List<EmulatorImportDefinition>()).Any(import => import.ListId==id && import.EmulatorId=="retroarch");
            }
            var combined=(settings.CombinedCollections ?? new List<CombinedCollection>()).FirstOrDefault(list => list.Id==id);
            if(combined==null) return false;
            var sources=combined.Sources ?? new List<string>(); var additions=combined.AddedGames ?? new List<string>();
            return sources.Count+additions.Count>0 && additions.All(isRetro) && sources.All(source => Applies(settings,source,new HashSet<string>(visiting)));
        }
        public static ListLayout Layout(Settings settings,string id) {
            ListLayout saved;
            if(settings.ListLayouts!=null && id!=null && settings.ListLayouts.TryGetValue(id,out saved)) return saved;
            return settings.RetroArchListLayout!=null && Applies(settings,id) ? settings.RetroArchListLayout : null;
        }
    }
}
