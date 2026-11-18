using System;

namespace PadLauncher {
    public static class GameImport {
        public static string ActionKey(string target,string arguments,string folder,string details=null) {
            // Direct launches default to the executable's directory when no folder was saved.
            if(String.IsNullOrWhiteSpace(folder) && !String.IsNullOrWhiteSpace(target) && System.IO.Path.IsPathRooted(target)) {
                try {folder=System.IO.Path.GetDirectoryName(target);} catch(ArgumentException) { }
            }
            // Windows file paths are case-insensitive; arguments remain case-sensitive.
            return (target ?? "").Trim().ToUpperInvariant()+"\n"+(arguments ?? "").Trim()+"\n"+(folder ?? "").Trim().TrimEnd('\\','/').ToUpperInvariant()+"\n"+(details ?? "");
        }
    }
}
