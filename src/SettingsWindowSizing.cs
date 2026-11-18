using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;

namespace PadLauncher {
    static class SettingsWindowSizing {
        [StructLayout(LayoutKind.Sequential)] struct NativeRect {public int Left,Top,Right,Bottom;}
        [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr window,out NativeRect bounds);
        [DllImport("user32.dll")] static extern uint GetDpiForWindow(IntPtr window);
        [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr window,IntPtr after,int x,int y,int width,int height,uint flags);

        // Work area and bounds are native pixels. WPF constraints are DIPs.
        internal static Rect FitBounds(Rect bounds,Rect work) {
            double width=Math.Min(bounds.Width,work.Width),height=Math.Min(bounds.Height,work.Height);
            return new Rect(Math.Max(work.Left,Math.Min(bounds.Left,work.Right-width)),Math.Max(work.Top,Math.Min(bounds.Top,work.Bottom-height)),width,height);
        }
        internal static Size AvailableSize(Rect work,double scale) {return new Size(work.Width/scale,work.Height/scale);}

        internal static void Attach(Window window) {
            double originalMinWidth=window.MinWidth,originalMinHeight=window.MinHeight;
            bool pending=false,applying=false,closed=false;
            HwndSource source=null;
            Action fit=() => {
                if(closed || applying) return;
                var handle=new WindowInteropHelper(window).Handle; if(handle==IntPtr.Zero) return;
                NativeRect bounds; if(!GetWindowRect(handle,out bounds)) return;
                applying=true;
                try {
                    double scale=1;
                    try {uint dpi=GetDpiForWindow(handle); if(dpi>0) scale=dpi/96.0;}
                    catch(EntryPointNotFoundException) {if(source!=null && source.CompositionTarget!=null) scale=source.CompositionTarget.TransformToDevice.M22;}
                    var area=System.Windows.Forms.Screen.FromHandle(handle).WorkingArea;
                    double margin=Math.Ceiling(8*scale);
                    var work=new Rect(area.Left+margin,area.Top+margin,Math.Max(1,area.Width-2*margin),Math.Max(1,area.Height-2*margin));
                    Size available=AvailableSize(work,scale);
                    window.MinWidth=Math.Min(originalMinWidth,available.Width); window.MinHeight=Math.Min(originalMinHeight,available.Height);
                    window.MaxWidth=available.Width; window.MaxHeight=available.Height;
                    if(window.WindowState!=WindowState.Normal) return;
                    // Re-read after WPF applies its updated DPI-aware constraints.
                    if(!GetWindowRect(handle,out bounds)) return;
                    var current=new Rect(bounds.Left,bounds.Top,bounds.Right-bounds.Left,bounds.Bottom-bounds.Top);
                    var fitted=FitBounds(current,work);
                    if(current!=fitted) SetWindowPos(handle,IntPtr.Zero,(int)fitted.Left,(int)fitted.Top,(int)fitted.Width,(int)fitted.Height,0x0014);
                } finally {applying=false;}
            };
            Action schedule=() => {
                if(closed || applying || pending) return;
                pending=true; window.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(() => {pending=false; fit();}));
            };
            HwndSourceHook hook=delegate(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
                if(message==0x02e0 || message==0x007e || message==0x001a || message==0x0232) schedule();
                return IntPtr.Zero;
            };
            window.SourceInitialized+=(s,e) => {source=HwndSource.FromHwnd(new WindowInteropHelper(window).Handle); if(source!=null) source.AddHook(hook); fit();};
            window.Loaded+=(s,e) => schedule();
            window.SizeChanged+=(s,e) => schedule();
            window.Closed+=(s,e) => {closed=true; if(source!=null && !source.IsDisposed) source.RemoveHook(hook);};
        }
    }
}
