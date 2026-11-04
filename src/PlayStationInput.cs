using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace PadLauncher {
    public enum SonyPadModel { Unknown, DualShock4, DualSense }
    // Input-only reader: no exclusive handles, output reports, rumble or device settings.
    public static class SonyReports {
        public static SonyPadModel Model(uint vendor,uint product) {
            if(vendor!=0x054c) return SonyPadModel.Unknown;
            if(product==0x05c4 || product==0x09cc) return SonyPadModel.DualShock4;
            if(product==0x0ce6 || product==0x0df2) return SonyPadModel.DualSense;
            return SonyPadModel.Unknown;
        }
        static short Axis(byte value) { return (short)(value<128 ? (value-128)*256 : (value-128)*32767/127); }
        static short Invert(short value) { return (short)Math.Min(32767,-(int)value); }
        public static bool Decode(SonyPadModel model,byte[] report,out PadState pad) {
            pad=new PadState();
            if(report==null || report.Length<10 || model==SonyPadModel.Unknown) return false;
            int axes,buttons,triggers;
            if(model==SonyPadModel.DualShock4) {
                if(report[0]==1) axes=1;
                else if(report[0]>=0x11 && report[0]<=0x19 && report.Length>=78) axes=3;
                else return false;
                buttons=axes+4; triggers=axes+7;
            } else {
                if(report[0]==0x31 && report.Length>=78) axes=2;
                else if(report[0]==1 && report.Length>=64) axes=1;
                else if(report[0]==1 && report.Length==10) {
                    // Bluetooth's initial simple report uses the DS4-compatible layout.
                    return Decode(SonyPadModel.DualShock4,report,out pad);
                } else return false;
                triggers=axes+4; buttons=axes+7;
            }
            if(buttons+2>=report.Length || triggers+1>=report.Length) return false;
            pad.LX=Axis(report[axes]); pad.LY=Invert(Axis(report[axes+1]));
            pad.RX=Axis(report[axes+2]); pad.RY=Invert(Axis(report[axes+3]));
            pad.LT=report[triggers]; pad.RT=report[triggers+1];
            int face=report[buttons],extra=report[buttons+1],hat=face&15;
            ushort bits=0;
            if(hat==0 || hat==1 || hat==7) bits|=0x0001;
            if(hat==3 || hat==4 || hat==5) bits|=0x0002;
            if(hat==5 || hat==6 || hat==7) bits|=0x0004;
            if(hat==1 || hat==2 || hat==3) bits|=0x0008;
            if((face&0x10)!=0) bits|=0x4000; // Square
            if((face&0x20)!=0) bits|=0x1000; // Cross
            if((face&0x40)!=0) bits|=0x2000; // Circle
            if((face&0x80)!=0) bits|=0x8000; // Triangle
            if((extra&1)!=0) bits|=0x0100;
            if((extra&2)!=0) bits|=0x0200;
            if((extra&0x10)!=0) bits|=0x0020; // Share / Create
            if((extra&0x20)!=0) bits|=0x0010; // Options
            if((extra&0x40)!=0) bits|=0x0040;
            if((extra&0x80)!=0) bits|=0x0080;
            if((report[buttons+2]&1)!=0) bits|=0x0400; // PS button (native reports only)
            if(pad.LT==0 && (extra&4)!=0) pad.LT=255;
            if(pad.RT==0 && (extra&8)!=0) pad.RT=255;
            pad.Buttons=bits;
            return true;
        }
    }
    public sealed class PlayStationInput {
        [StructLayout(LayoutKind.Sequential)] struct RawDevice {
            public ushort Page,Usage;
            public uint Flags;
            public IntPtr Window;
        }
        [StructLayout(LayoutKind.Sequential)] struct RawHeader {
            public uint Type,Size;
            public IntPtr Device,WParam;
        }
        [StructLayout(LayoutKind.Sequential)] struct DeviceInfo {
            public uint Size,Type,Vendor,Product,Version;
            public ushort Page,Usage;
            public uint Padding1,Padding2;
        }
        [DllImport("user32.dll",SetLastError=true)]
        static extern bool RegisterRawInputDevices(RawDevice[] devices,uint count,uint size);
        [DllImport("user32.dll",SetLastError=true)]
        static extern uint GetRawInputData(IntPtr input,uint command,[Out] byte[] data,ref uint size,uint headerSize);
        [DllImport("user32.dll",EntryPoint="GetRawInputDeviceInfoW",SetLastError=true)]
        static extern uint GetRawInputDeviceInfo(IntPtr device,uint command,ref DeviceInfo info,ref uint size);
        readonly Dictionary<IntPtr,SonyPadModel> models=new Dictionary<IntPtr,SonyPadModel>();
        HwndSource source;
        sealed class Sample { public PadState State; public long Received; }
        readonly Dictionary<IntPtr,Sample> pads=new Dictionary<IntPtr,Sample>();
        static long Milliseconds { get { return (long)(Stopwatch.GetTimestamp()*1000.0/Stopwatch.Frequency); } }
        public bool Start(IntPtr window) {
            if(source!=null && !source.IsDisposed && source.Handle==window) return true;
            Stop();
            source=HwndSource.FromHwnd(window);
            if(source==null) return false;
            source.AddHook(OnMessage);
            // Only gamepad/joystick usage pages are registered; no keyboard/mouse hooks.
            var registrations=new[]{new RawDevice {Page=1,Usage=5,Flags=0x2100,Window=window},new RawDevice {Page=1,Usage=4,Flags=0x2100,Window=window}};
            if(RegisterRawInputDevices(registrations,2,(uint)Marshal.SizeOf(typeof(RawDevice)))) return true;
            source.RemoveHook(OnMessage); source=null; return false;
        }
        public void Stop() {
            if(source!=null) {
                var remove=new[]{new RawDevice {Page=1,Usage=5,Flags=1},new RawDevice {Page=1,Usage=4,Flags=1}};
                RegisterRawInputDevices(remove,2,(uint)Marshal.SizeOf(typeof(RawDevice)));
                if(!source.IsDisposed) source.RemoveHook(OnMessage);
                source=null;
            }
            models.Clear(); pads.Clear();
        }
        public bool Read(out PadState state,out string identity) {
            var states=ReadAll();
            state=states.Count>0 ? states[0].Value : new PadState(); identity=states.Count>0 ? states[0].Key : null;
            return states.Count>0;
        }
        public List<KeyValuePair<string,PadState>> ReadAll() {
            var result=new List<KeyValuePair<string,PadState>>();
            foreach(var pair in pads) if(Milliseconds-pair.Value.Received<=1000) result.Add(new KeyValuePair<string,PadState>("sony:"+pair.Key.ToInt64(),pair.Value.State));
            return result;
        }
        IntPtr OnMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam,ref bool handled) {
            if(message==0x00fe && wParam.ToInt64()==2) {
                models.Remove(lParam);
                pads.Remove(lParam);
            }
            if(message!=0x00ff) return IntPtr.Zero;
            uint headerSize=(uint)Marshal.SizeOf(typeof(RawHeader)),size=0;
            if(GetRawInputData(lParam,0x10000003,null,ref size,headerSize)!=0 || size<headerSize+8 || size>65536) return IntPtr.Zero;
            byte[] data=new byte[size];
            if(GetRawInputData(lParam,0x10000003,data,ref size,headerSize)!=size || BitConverter.ToUInt32(data,0)!=2) return IntPtr.Zero;
            IntPtr inputDevice=IntPtr.Size==8 ? new IntPtr(BitConverter.ToInt64(data,8)) : new IntPtr(BitConverter.ToInt32(data,8));
            SonyPadModel model;
            if(!models.TryGetValue(inputDevice,out model)) {
                var info=new DeviceInfo {Size=(uint)Marshal.SizeOf(typeof(DeviceInfo))}; uint infoSize=info.Size;
                model=GetRawInputDeviceInfo(inputDevice,0x2000000b,ref info,ref infoSize)!=UInt32.MaxValue && info.Type==2 ? SonyReports.Model(info.Vendor,info.Product) : SonyPadModel.Unknown;
                models[inputDevice]=model;
            }
            if(model==SonyPadModel.Unknown) return IntPtr.Zero;
            int offset=(int)headerSize;
            uint reportSize=BitConverter.ToUInt32(data,offset),count=BitConverter.ToUInt32(data,offset+4);
            if(reportSize==0 || reportSize>1024 || count==0 || (ulong)reportSize*count>(ulong)(data.Length-offset-8)) return IntPtr.Zero;
            for(uint i=0;i<count;i++) {
                byte[] report=new byte[reportSize];
                Buffer.BlockCopy(data,offset+8+(int)(i*reportSize),report,0,report.Length);
                PadState decoded;
                if(!SonyReports.Decode(model,report,out decoded)) continue;
                long now=Milliseconds;
                Sample previous; decoded.Packet=pads.TryGetValue(inputDevice,out previous) ? previous.State.Packet+1 : 1;
                pads[inputDevice]=new Sample {State=decoded,Received=now};
            }
            return IntPtr.Zero;
        }
    }
}
