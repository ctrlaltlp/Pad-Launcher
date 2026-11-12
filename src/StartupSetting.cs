using System;
using System.IO;
using Microsoft.Win32;
namespace PadLauncher {
    public static class StartupSetting {
        const string Key=@"Software\Microsoft\Windows\CurrentVersion\Run";
        const string Name="PadLauncher";
        public static string Command(string executable) {return "\""+Path.GetFullPath(executable)+"\"";}
        public static bool Enabled {
            get {using(var key=Registry.CurrentUser.OpenSubKey(Key)) return key!=null && String.Equals(key.GetValue(Name) as string,Command(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName),StringComparison.OrdinalIgnoreCase);}
        }
        public static void Set(bool enabled) {
            using(var key=Registry.CurrentUser.CreateSubKey(Key)) {
                if(enabled) key.SetValue(Name,Command(System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName)); else key.DeleteValue(Name,false);
            }
        }
    }
}
