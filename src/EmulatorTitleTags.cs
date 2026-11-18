using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PadLauncher {
    public static class EmulatorTitleTags {
        static readonly Regex Groups=new Regex(@"\(([^()]*)\)|\[([^\[\]]*)\]",RegexOptions.CultureInvariant);
        static readonly HashSet<string> Regions=new HashSet<string>(new[]{"PAL","NTSC","NTSC-U","NTSC-J","NTSC-U/C","USA","US","Europe","EUR","Japan","JPN","World","Asia","Korea","China","Taiwan","Hong Kong","Australia","Brazil","Canada","France","Germany","Spain","Italy","Netherlands","Sweden","Norway","Denmark","Finland","Russia","UK","United Kingdom"},StringComparer.OrdinalIgnoreCase);
        static readonly HashSet<string> Languages=new HashSet<string>(new[]{"En","Ja","Fr","De","Es","It","Nl","Pt","Ru","Ko","Zh","Zh-Hans","Zh-Hant","Sv","No","Da","Fi","Pl","Cs","Hu","Tr","Ar","He","El","Sk","Ca"},StringComparer.OrdinalIgnoreCase);
        public static bool IsTag(string value,bool square) {
            string tag=value.Trim();
            var parts=tag.Split(',').Select(part => part.Trim()).ToArray();
            if(parts.All(part => Regions.Contains(part)) || parts.All(part => Languages.Contains(part))) return true;
            if(Regex.IsMatch(tag,@"^(?:Rev(?:ision)?[ ._-]*[A-Z0-9]+|v\d+(?:\.\d+)*[a-z]?|(?:CGB\+)?SGB Enhanced|GB Compatible|Unl|NP|Virtual Console|[A-Z]{4}[-_ ]?\d{5}|01[0-9a-f]{14})$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant)) return true;
            return square && Regex.IsMatch(tag,@"^(?:[!nibafhpt]\d*|T-[A-Z]{2}(?:\b.*)?|tr\s+[a-z]{2}(?:\b.*)?|Add by\s+.+)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
        }
        public static IEnumerable<string> TagsIn(string name) {
            return Groups.Matches(name ?? "").Cast<Match>().Where(match => IsTag(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value,match.Value[0]=='[')).Select(match => match.Value).Distinct(StringComparer.OrdinalIgnoreCase);
        }
        public static string Clean(string name) {
            if(String.IsNullOrWhiteSpace(name)) return name;
            string clean=Groups.Replace(name,match => IsTag(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value,match.Value[0]=='[') ? " " : match.Value);
            clean=Regex.Replace(clean,@"\s+[-_]\s*(?:PAL|NTSC(?:-[UJ](?:/C)?)?)\s*$","",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            clean=Regex.Replace(clean,@"\s{2,}"," ").Trim();
            return String.IsNullOrWhiteSpace(clean) ? name : clean;
        }
        public static string Normalise(string name) {
            if(String.IsNullOrWhiteSpace(name)) return name;
            var match=Regex.Match(name,@"^(?<title>.+?),\s*The(?<suffix>\s*(?:(?:[-–—:]|\(|\[).*)?)$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant);
            return match.Success ? "The "+match.Groups["title"].Value.Trim()+match.Groups["suffix"].Value.TrimEnd() : name;
        }
    }
}
