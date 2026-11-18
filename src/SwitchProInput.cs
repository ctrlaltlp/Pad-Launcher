using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace PadLauncher {
    public interface ISwitchProBackend {
        bool Start();
        List<KeyValuePair<string,PadState>> Read();
        void Stop();
    }
    public sealed class SwitchProInput {
        readonly ISwitchProBackend backend;
        readonly Func<bool> connected;
        readonly Func<long> clock;
        bool active,started;
        long nextAttempt;
        public string LastError {get;private set;}
        static readonly ushort[] buttonMasks={0x1000,0x2000,0x4000,0x8000,0x0020,0x0400,0x0010,0x0040,0x0080,0x0100,0x0200,0x0001,0x0002,0x0004,0x0008};
        public SwitchProInput():this(new SdlSwitchProBackend(),SwitchProDevices.Connected,() => (long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency)) {}
        public SwitchProInput(ISwitchProBackend backend,Func<bool> connected,Func<long> clock) {this.backend=backend; this.connected=connected; this.clock=clock;}
        public void Start() {active=true; nextAttempt=0;}
        public void Stop() {active=false; if(started) backend.Stop(); started=false; nextAttempt=0;}
        public List<KeyValuePair<string,PadState>> ReadAll() {
            if(!active) return new List<KeyValuePair<string,PadState>>();
            try {
                if(!started) {
                    if(clock()<nextAttempt) return new List<KeyValuePair<string,PadState>>();
                    nextAttempt=clock()+1000;
                    // Most users never load SDL: probe for an official Switch Pro first.
                    if(!connected()) return new List<KeyValuePair<string,PadState>>();
                    started=backend.Start();
                    if(!started) {nextAttempt=clock()+5000; return new List<KeyValuePair<string,PadState>>();}
                }
                var states=backend.Read(); LastError=null; return states;
            } catch(Exception error) {
                if(!(error is IOException || error is UnauthorizedAccessException || error is DllNotFoundException || error is EntryPointNotFoundException || error is BadImageFormatException || error is System.ComponentModel.Win32Exception)) throw;
                LastError=error.Message;
                if(started) backend.Stop(); started=false; nextAttempt=clock()+5000;
                return new List<KeyValuePair<string,PadState>>();
            }
        }
        // SDL reports physical face-button positions, so Nintendo B is south/play.
        public static PadState Translate(uint buttons,short lx,short ly,short rx,short ry,short lt,short rt) {
            ushort mapped=0;
            for(int i=0;i<buttonMasks.Length;i++) if((buttons&(1u<<i))!=0) mapped|=buttonMasks[i];
            return new PadState {Buttons=mapped,LX=lx,LY=(short)Math.Min(32767,-(int)ly),RX=rx,RY=(short)Math.Min(32767,-(int)ry),LT=(byte)(Math.Max(0,(int)lt)*255/32767),RT=(byte)(Math.Max(0,(int)rt)*255/32767)};
        }
    }
    static class SwitchProDevices {
        [StructLayout(LayoutKind.Sequential)] struct Device {public IntPtr Handle; public uint Type;}
        [StructLayout(LayoutKind.Sequential)] struct Info {public uint Size,Type,Vendor,Product,Version; public ushort Page,Usage; public uint Padding1,Padding2;}
        [DllImport("user32.dll")] static extern uint GetRawInputDeviceList([Out] Device[] devices,ref uint count,uint size);
        [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW")] static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,ref Info info,ref uint size);
        public static bool Connected() {
            uint count=0,size=(uint)Marshal.SizeOf(typeof(Device));
            if(GetRawInputDeviceList(null,ref count,size)!=0 || count==0 || count>1024) return false;
            var devices=new Device[count]; uint found=GetRawInputDeviceList(devices,ref count,size);
            if(found==UInt32.MaxValue) return false;
            foreach(var device in devices.Take((int)Math.Min(found,(uint)devices.Length))) {
                if(device.Type!=2) continue;
                var info=new Info {Size=(uint)Marshal.SizeOf(typeof(Info))}; uint infoSize=info.Size;
                if(GetRawInputDeviceInfo(device.Handle,0x2000000b,ref info,ref infoSize)!=UInt32.MaxValue && info.Vendor==0x057e && info.Product==0x2009) return true;
            }
            return false;
        }
    }
    public sealed class SdlSwitchProBackend:ISwitchProBackend {
        public const string Version="3.4.20";
        public const string DllHash="66844f10251cc1c10619ed5d8e4e3224f564900ee6d69ba87d63de782addae40";
        static IntPtr module;
        readonly Dictionary<uint,IntPtr> pads=new Dictionary<uint,IntPtr>();
        bool initialized;
        long nextDiscovery;
        uint packet;
        const uint GamepadSubsystem=0x2000;
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr file,uint flags);
        static string Hash(string path) {using(var input=File.OpenRead(path)) using(var sha=System.Security.Cryptography.SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","").ToLowerInvariant();}
        public static string PrepareLibrary() {
            string root=Path.Combine(Storage.Root,"runtime","SDL3",Version+"-x64"); Directory.CreateDirectory(root);
            string path=Path.Combine(root,"SDL3.dll");
            if(!File.Exists(path) || Hash(path)!=DllHash) {
                string temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                try {
                    using(var compressed=Assembly.GetExecutingAssembly().GetManifestResourceStream("SDL3.dll.gz")) {
                        if(compressed==null) throw new IOException("The embedded Switch controller driver is missing.");
                        using(var gzip=new GZipStream(compressed,CompressionMode.Decompress)) using(var output=File.Create(temp)) gzip.CopyTo(output);
                    }
                    if(Hash(temp)!=DllHash) throw new IOException("Switch controller driver checksum verification failed.");
                    if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
                } finally {if(File.Exists(temp)) File.Delete(temp);}
            }
            string license=Path.Combine(root,"LICENSE.txt");
            if(!File.Exists(license)) using(var source=Assembly.GetExecutingAssembly().GetManifestResourceStream("SDL3-LICENSE.txt")) using(var output=File.Create(license)) {if(source!=null) source.CopyTo(output);}
            return path;
        }
        public bool Start() {
            if(initialized) return true;
            if(module==IntPtr.Zero) {
                module=LoadLibraryEx(Path.GetFullPath(PrepareLibrary()),IntPtr.Zero,0x1100);
                if(module==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            }
            Native.SetMainReady();
            // Leave Sony/XInput to their existing readers; never register SDL Raw Input.
            foreach(string hint in new[]{"SDL_JOYSTICK_RAWINPUT","SDL_JOYSTICK_DIRECTINPUT","SDL_JOYSTICK_WGI","SDL_JOYSTICK_GAMEINPUT","SDL_XINPUT_ENABLED","SDL_JOYSTICK_HIDAPI_PS3","SDL_JOYSTICK_HIDAPI_PS4","SDL_JOYSTICK_HIDAPI_PS5","SDL_JOYSTICK_HIDAPI_XBOX","SDL_JOYSTICK_HIDAPI_JOY_CONS","SDL_JOYSTICK_HIDAPI_SWITCH2","SDL_JOYSTICK_HIDAPI_SWITCH_HOME_LED","SDL_JOYSTICK_HIDAPI_SWITCH_PLAYER_LED"}) Native.SetHint(hint,"0");
            Native.SetHint("SDL_JOYSTICK_HIDAPI","1"); Native.SetHint("SDL_JOYSTICK_HIDAPI_SWITCH","1"); Native.SetHint("SDL_JOYSTICK_ALLOW_BACKGROUND_EVENTS","1");
            if(!Native.InitSubSystem(GamepadSubsystem)) throw new IOException("Could not initialize Switch controller input.");
            initialized=true; nextDiscovery=0; return true;
        }
        public List<KeyValuePair<string,PadState>> Read() {
            var result=new List<KeyValuePair<string,PadState>>(); if(!initialized) return result;
            Native.PumpEvents(); Native.FlushEvents(0,0xffff);
            foreach(uint id in pads.Keys.ToList()) if(!Native.GamepadConnected(pads[id])) {Native.CloseGamepad(pads[id]); pads.Remove(id);}
            long now=(long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency);
            if(now>=nextDiscovery) {
                nextDiscovery=now+1000;
                int count; IntPtr ids=Native.GetGamepads(out count);
                try {
                    for(int i=0;ids!=IntPtr.Zero && i<count && i<64;i++) {
                        uint id=unchecked((uint)Marshal.ReadInt32(ids,i*4));
                        if(pads.ContainsKey(id) || Native.GetGamepadTypeForID(id)!=7 || Native.GetGamepadVendorForID(id)!=0x057e || Native.GetGamepadProductForID(id)!=0x2009) continue;
                        IntPtr pad=Native.OpenGamepad(id); if(pad!=IntPtr.Zero) pads[id]=pad;
                    }
                } finally {if(ids!=IntPtr.Zero) Native.Free(ids);}
            }
            foreach(var pair in pads) {
                uint buttons=0; for(int i=0;i<15;i++) if(Native.GetGamepadButton(pair.Value,i)) buttons|=1u<<i;
                var state=SwitchProInput.Translate(buttons,Native.GetGamepadAxis(pair.Value,0),Native.GetGamepadAxis(pair.Value,1),Native.GetGamepadAxis(pair.Value,2),Native.GetGamepadAxis(pair.Value,3),Native.GetGamepadAxis(pair.Value,4),Native.GetGamepadAxis(pair.Value,5));
                state.Packet=++packet; result.Add(new KeyValuePair<string,PadState>("switch:"+pair.Key,state));
            }
            return result;
        }
        public void Stop() {
            if(!initialized) return;
            foreach(var pad in pads.Values) Native.CloseGamepad(pad); pads.Clear();
            Native.QuitSubSystem(GamepadSubsystem); initialized=false;
        }
        static class Native {
            const string Dll="SDL3.dll";
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_SetMainReady")] internal static extern void SetMainReady();
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_SetHint")] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool SetHint([MarshalAs(UnmanagedType.LPStr)] string name,[MarshalAs(UnmanagedType.LPStr)] string value);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_InitSubSystem")] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool InitSubSystem(uint flags);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_QuitSubSystem")] internal static extern void QuitSubSystem(uint flags);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_PumpEvents")] internal static extern void PumpEvents();
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_FlushEvents")] internal static extern void FlushEvents(uint min,uint max);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetGamepads")] internal static extern IntPtr GetGamepads(out int count);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_free")] internal static extern void Free(IntPtr memory);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetGamepadTypeForID")] internal static extern int GetGamepadTypeForID(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetGamepadVendorForID")] internal static extern ushort GetGamepadVendorForID(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetGamepadProductForID")] internal static extern ushort GetGamepadProductForID(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_OpenGamepad")] internal static extern IntPtr OpenGamepad(uint id);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_CloseGamepad")] internal static extern void CloseGamepad(IntPtr pad);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GamepadConnected")] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool GamepadConnected(IntPtr pad);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetGamepadButton")] [return:MarshalAs(UnmanagedType.I1)] internal static extern bool GetGamepadButton(IntPtr pad,int button);
            [DllImport(Dll,CallingConvention=CallingConvention.Cdecl,EntryPoint="SDL_GetGamepadAxis")] internal static extern short GetGamepadAxis(IntPtr pad,int axis);
        }
    }
}
