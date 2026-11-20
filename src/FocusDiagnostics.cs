using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PadLauncher {
    public partial class Launcher {
        readonly string focusTracePath=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PadLauncher.FocusDiagnostics.log");
        [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
        void InitializeFocusDiagnostics() {
            SourceInitialized+=(sender,args) => {
                var source=PresentationSource.FromVisual(this) as HwndSource;
                if(source!=null) source.AddHook(FocusTraceHook);
                TraceFocus("overlay window initialized");
            };
            Activated+=(sender,args) => TraceFocus("overlay activated");
            Deactivated+=(sender,args) => TraceFocus("overlay deactivated");
            AddHandler(Mouse.PreviewMouseDownEvent,new MouseButtonEventHandler((sender,args) => {
                TraceFocus("overlay mouse down"); QueueFocusTrace("after overlay mouse down");
            }),true);
        }
        IntPtr FocusTraceHook(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
            switch(message) {
                case 0x21: TraceFocus("WM_MOUSEACTIVATE"); QueueFocusTrace("after WM_MOUSEACTIVATE"); break;
                case 0x07: TraceFocus("WM_SETFOCUS"); break;
                case 0x08: TraceFocus("WM_KILLFOCUS"); break;
            }
            return IntPtr.Zero;
        }
        void QueueFocusTrace(string reason) {
            var timer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(250)};
            timer.Tick+=(sender,args) => {timer.Stop(); TraceFocus(reason);};
            timer.Start();
        }
        void TraceFocus(string reason) {
            try {
                var foreground=GetForegroundWindow();
                uint pid=0;
                if(foreground!=IntPtr.Zero) GetWindowThreadProcessId(foreground,out pid);
                var own=new WindowInteropHelper(this).Handle;
                File.AppendAllText(focusTracePath,String.Format("{0:O} {1} foreground=0x{2:X} pid={3} overlay=0x{4:X} active={5} keyboardWithin={6}{7}",
                    DateTime.UtcNow,reason,foreground.ToInt64(),pid,own.ToInt64(),IsActive,IsKeyboardFocusWithin,Environment.NewLine));
            } catch(IOException) { } catch(UnauthorizedAccessException) { }
        }
    }
}
