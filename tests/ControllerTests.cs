using System;
using System.Collections.Generic;
using System.Windows.Interop;
using PadLauncher;

public static class ControllerTests {
    static int checks;
    static void Check(bool ok,string name) { if(!ok) throw new Exception("FAILED: "+name); checks++; Console.WriteLine("PASS "+name); }
    static byte[] Report(int length,int id,int axes,int buttons) {
        var report=new byte[length]; report[0]=(byte)id;
        for(int i=0;i<4;i++) report[axes+i]=128;
        report[buttons]=8; return report;
    }
    [STAThread] public static int Main() {
        Launcher window=null;
        var reader=new PlayStationInput();
        try {
            var router=new ControllerRouter(); PadState routed; string routedSource;
            var pads=new List<KeyValuePair<string,PadState>> {new KeyValuePair<string,PadState>("pad0",new PadState()),new KeyValuePair<string,PadState>("pad1",new PadState {Buttons=0x1000})};
            Check(router.Select(pads,out routed,out routedSource) && routedSource=="pad1" && routed.Buttons==0x1000,"An idle first controller does not block a second controller");
            pads[0]=new KeyValuePair<string,PadState>("pad0",new PadState {Buttons=0x4000});
            Check(router.Select(pads,out routed,out routedSource) && routedSource=="pad1","Simultaneous input keeps the current controller until it releases");
            pads[1]=new KeyValuePair<string,PadState>("pad1",new PadState());
            Check(router.Select(pads,out routed,out routedSource) && routedSource=="pad0","Another controller takes over when the current controller releases");
            pads.RemoveAt(0);
            Check(router.Select(pads,out routed,out routedSource) && routedSource=="pad1","Disconnecting the current controller leaves other controllers usable");
            pads.Clear();
            Check(!router.Select(pads,out routed,out routedSource) && routedSource==null,"Disconnecting every controller clears input");
            Check(SonyReports.Model(0x054c,0x05c4)==SonyPadModel.DualShock4 && SonyReports.Model(0x054c,0x09cc)==SonyPadModel.DualShock4,"Identify both DualShock 4 revisions");
            Check(SonyReports.Model(0x054c,0x0ce6)==SonyPadModel.DualSense && SonyReports.Model(0x054c,0x0df2)==SonyPadModel.DualSense,"Identify DualSense and DualSense Edge");
            Check(SonyReports.Model(0x1234,0x05c4)==SonyPadModel.Unknown && SonyReports.Model(0x054c,0xffff)==SonyPadModel.Unknown,"Unrecognised HID devices are not interpreted as Sony controllers");
            PadState state;
            var ds4=Report(64,1,1,5);
            Check(SonyReports.Decode(SonyPadModel.DualShock4,ds4,out state) && state.Buttons==0 && state.LX==0 && state.RX==0,"Neutral DualShock 4 USB report");
            byte[] faces={0x20,0x40,0x10,0x80}; ushort[] expected={0x1000,0x2000,0x4000,0x8000};
            string[] labels={"Cross","Circle","Square","Triangle"};
            for(int i=0;i<faces.Length;i++) {
                ds4[5]=(byte)(faces[i]|8); SonyReports.Decode(SonyPadModel.DualShock4,ds4,out state);
                Check(state.Buttons==expected[i],labels[i]+" maps to its existing launcher action");
            }
            ushort[] hats={1,9,8,10,2,6,4,5,0};
            for(int i=0;i<hats.Length;i++) {
                ds4[5]=(byte)i; SonyReports.Decode(SonyPadModel.DualShock4,ds4,out state);
                Check((state.Buttons&15)==hats[i],"D-pad hat position "+i);
            }
            ds4[5]=8; ds4[6]=0x23; ds4[1]=0; ds4[2]=255; ds4[3]=255; ds4[4]=0;
            SonyReports.Decode(SonyPadModel.DualShock4,ds4,out state);
            Check(state.LX==Int16.MinValue && state.RX==Int16.MaxValue && state.LY==-32767 && state.RY==32767,"Both sticks preserve direction and full-scale values");
            Check(state.Buttons==0x0310,"L1, R1 and Options map to collections and hints");
            var ds4bt=Report(78,0x11,3,7); ds4bt[7]=0x28;
            Check(SonyReports.Decode(SonyPadModel.DualShock4,ds4bt,out state) && state.Buttons==0x1000 && state.LX==0,"DualShock 4 Bluetooth report header offset");
            var ds5=Report(64,1,1,8); ds5[8]=0x48; ds5[5]=37; ds5[6]=211;
            Check(SonyReports.Decode(SonyPadModel.DualSense,ds5,out state) && state.Buttons==0x2000 && state.LT==37 && state.RT==211,"DualSense USB buttons and trigger offsets");
            var ds5bt=Report(78,0x31,2,9); ds5bt[9]=0x18; ds5bt[4]=255;
            Check(SonyReports.Decode(SonyPadModel.DualSense,ds5bt,out state) && state.Buttons==0x4000 && state.RX==32767,"DualSense Bluetooth report header offset and right stick");
            var simple=Report(10,1,1,5); simple[5]=0x88;
            Check(SonyReports.Decode(SonyPadModel.DualSense,simple,out state) && state.Buttons==0x8000,"DualSense initial simple Bluetooth report");
            Check(!SonyReports.Decode(SonyPadModel.DualSense,new byte[9],out state) && !SonyReports.Decode(SonyPadModel.DualShock4,null,out state),"Truncated and null reports are rejected safely");
            Check(!SonyReports.Decode(SonyPadModel.DualSense,Report(64,0x31,2,9),out state) && !SonyReports.Decode(SonyPadModel.DualShock4,Report(64,0x11,3,7),out state),"Truncated extended Bluetooth reports are rejected");
            Check(!SonyReports.Decode(SonyPadModel.DualSense,new byte[78],out state),"Unrecognised report IDs cannot produce button actions");
            ds5[8]=8; ds5[10]=1;
            Check(SonyReports.Decode(SonyPadModel.DualSense,ds5,out state) && (state.Buttons&0x0400)!=0,"Native PS button report decodes correctly");
            window=new Launcher(true,true);
            var handle=new WindowInteropHelper(window).EnsureHandle();
            Check(reader.Start(handle),"Register native gamepad input on an invisible launcher window");
            reader.Stop(); string identity;
            Check(!reader.Read(out state,out identity),"Stopping native input clears cached controller state");
            Check(!window.IsVisible,"Native controller registration does not display the overlay");
            Console.WriteLine(checks+" controller checks passed."); return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
        finally { reader.Stop(); if(window!=null) window.ExitApp(); }
    }
}
