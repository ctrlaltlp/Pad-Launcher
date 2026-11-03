using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using PadLauncher;

// Tests real monitor coordinates using an invisible native window. No cursor moves.
public static class MonitorTests {
    [StructLayout(LayoutKind.Sequential)] struct NativeRect { public int Left,Top,Right,Bottom; }
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect rect);
    static int checks;
    static void Check(bool ok,string name) { if(!ok) throw new Exception("FAILED: "+name); checks++; Console.WriteLine("PASS "+name); }
    [STAThread] public static int Main() {
        Launcher window=null;
        try {
            window=new Launcher(true,false);
            var handle=new WindowInteropHelper(window).EnsureHandle();
            var place=typeof(Launcher).GetMethod("PlaceOnMonitor",BindingFlags.Instance|BindingFlags.NonPublic);
            var monitors=System.Windows.Forms.Screen.AllScreens.OrderBy(s => s.DeviceName,StringComparer.Ordinal).ToArray();
            foreach(var monitor in monitors) {
                place.Invoke(window,new object[]{monitor});
                NativeRect actual; if(!GetWindowRect(handle,out actual)) throw new Exception("Could not read window bounds.");
                var expected=monitor.WorkingArea;
                Check(actual.Left==expected.Left && actual.Top==expected.Top && actual.Right==expected.Right && actual.Bottom==expected.Bottom,"Native overlay bounds match working area on "+monitor.DeviceName+" (actual "+actual.Left+","+actual.Top+","+actual.Right+","+actual.Bottom+"; expected "+expected.Left+","+expected.Top+","+expected.Right+","+expected.Bottom+")");
            }
            place.Invoke(window,new object[]{monitors[monitors.Length-1]});
            window.CycleMonitor();
            Check(System.Windows.Forms.Screen.FromHandle(handle).DeviceName==monitors[0].DeviceName,"Monitor cycle wraps from the last display to the first");
            Check(!window.IsVisible,"Monitor positioning tests never display the overlay");
            var cursorMonitor=System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
            Check(Launcher.ResolveMonitor(new Settings().OpenMonitor,monitors,cursorMonitor).DeviceName==cursorMonitor.DeviceName,"Default monitor preference uses the cursor's display");
            var fixedMonitor=monitors[monitors.Length-1];
            Check(Launcher.ResolveMonitor(fixedMonitor.DeviceName,monitors,cursorMonitor).DeviceName==fixedMonitor.DeviceName,"Fixed monitor preference overrides cursor selection");
            Check(Launcher.ResolveMonitor("disconnected-display",monitors,cursorMonitor).DeviceName==cursorMonitor.DeviceName,"Missing saved monitor falls back to the cursor's display");
            place.Invoke(window,new object[]{cursorMonitor});
            Check(System.Windows.Forms.Screen.FromHandle(handle).DeviceName==cursorMonitor.DeviceName,"Overlay can be placed on the monitor containing the cursor");
            Check(Prompts.Buttons(new Settings())[5]=="□" && Prompts.Buttons(new Settings{ButtonPrompts="Xbox"})[5]=="X" && Prompts.Buttons(new Settings{ButtonPrompts="Nintendo"})[5]=="Y","Monitor button labels match PlayStation, Xbox and Nintendo positions");
            Console.WriteLine(checks+" monitor checks passed."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { if(window!=null) window.ExitApp(); }
    }
}
