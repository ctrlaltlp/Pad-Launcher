using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.Runtime.InteropServices;

// v2 reads physical PS through DS4Windows DSU; never falls back to XInput.
partial class TapLauncher
{
    [StructLayout(LayoutKind.Sequential)]
    struct Message { public IntPtr Window; public uint Id; public UIntPtr WParam; public IntPtr LParam; public uint Time; public int X,Y; public uint Private; }
    [DllImport("user32.dll")] static extern bool PeekMessage(out Message message,IntPtr window,uint min,uint max,uint flags);
    [DllImport("user32.dll")] static extern bool PostThreadMessage(uint thread,uint message,UIntPtr wparam,IntPtr lparam);
    [DllImport("user32.dll")] static extern int GetMessage(out Message message,IntPtr window,uint min,uint max);
    [DllImport("kernel32.dll")] static extern uint GetCurrentThreadId();
    static void ClearStartupFeedback()
    {
        // Windows ends the application-starting cursor on the first GetMessage.
        // Queue our own filtered thread message so GetMessage cannot wait for input.
        Message message;
        PeekMessage(out message,IntPtr.Zero,0,0,0);
        if(PostThreadMessage(GetCurrentThreadId(),0x8001,UIntPtr.Zero,IntPtr.Zero))
            GetMessage(out message,new IntPtr(-1),0x8001,0x8001);
    }
    static readonly string Root=AppDomain.CurrentDomain.BaseDirectory;
    static void Log(string s) { try { File.AppendAllText(Path.Combine(Root,"TapLauncher.log"),DateTime.Now.ToString("s")+" v4 "+s+Environment.NewLine); } catch {} }
    static uint Crc(byte[] data)
    {
        uint crc=0xffffffff;
        for(int i=0;i<data.Length;i++) { crc^=(i>=8 && i<12) ? (byte)0 : data[i]; for(int j=0;j<8;j++) crc=(crc>>1)^((crc&1)!=0 ? 0xedb88320u : 0u); }
        return ~crc;
    }
    static void Put(byte[] d,int o,uint v) { Array.Copy(BitConverter.GetBytes(v),0,d,o,4); }
    static byte[] Packet(bool server,int size)
    {
        byte[] d=new byte[size]; Array.Copy(Encoding.ASCII.GetBytes(server ? "DSUS" : "DSUC"),d,4);
        d[4]=0xe9; d[5]=3; d[6]=(byte)(size-16); Put(d,12,0x4f4d4554); Put(d,16,0x100002); return d;
    }
    static bool Valid(byte[] d)
    {
        return d.Length==100 && Encoding.ASCII.GetString(d,0,4)=="DSUS" && BitConverter.ToUInt16(d,4)==1001 && BitConverter.ToUInt16(d,6)==84 && BitConverter.ToUInt32(d,16)==0x100002 && BitConverter.ToUInt32(d,8)==Crc(d) && d[20]<4;
    }
    static bool Observe(int port,bool diagnostic,out string reason)
    {
        using(UdpClient socket=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
        {
            socket.Connect(IPAddress.Loopback,port); socket.Client.ReceiveTimeout=25;
            byte[] request=Packet(false,28); Put(request,8,Crc(request));
            Stopwatch clock=Stopwatch.StartNew(); long sent=-1000,last=-1,held=-1;
            int slot=-1; string previous=""; byte[] mac=null;
            var connectedPads=new Dictionary<int,byte[]>();
            long firstReport=-1;
            while(clock.ElapsedMilliseconds < (diagnostic ? 15000 : 1400))
            {
                long now=clock.ElapsedMilliseconds;
                if(now-sent>=250) { socket.Send(request,request.Length); sent=now; }
                try
                {
                    IPEndPoint sender=null; byte[] d=socket.Receive(ref sender); now=clock.ElapsedMilliseconds;
                    if(!Valid(d)) continue;
                    // DS4Windows leaves byte 31 zero even for a connected pad.
                    // The slot-state byte (21) is the authoritative connection state.
                    int current=d[20]; bool connected=d[21]==2; bool ps=d[38]!=0;
                    if(diagnostic)
                    {
                        string status="Slot "+current+": "+(!connected ? "disconnected" : ps ? "PS DOWN" : "released");
                        if(status!=previous) { Log(status); previous=status; } continue;
                    }
                    if(connected) connectedPads[current]=d; else connectedPads.Remove(current);
                    if(firstReport<0 && connected) firstReport=now;
                    // Prefer the controller actually holding PS, never the first idle slot.
                    // A completed fast tap can only be attributed safely with one pad.
                    if(slot<0 && connected && ps) { slot=current; mac=new byte[6]; Array.Copy(d,24,mac,0,6); }
                    if(slot<0 && connected && !ps && connectedPads.Count==1 && firstReport>=0 && now-firstReport>=120) {
                        reason="Single controller already released: launch now."; return true;
                    }
                    if(slot<0) continue;
                    if(current!=slot) continue;
                    if(!connected) { reason="Physical controller disconnected."; return false; }
                    for(int i=0;i<6;i++) if(mac[i]!=d[24+i]) { reason="Physical controller changed."; return false; }
                    if(last>=0 && now-last>250) { reason="Physical input feed stalled."; return false; }
                    last=now;
                    // Reject a long hold even if its first over-threshold sample
                    // is the release report. Otherwise launch on that release.
                    if(held>=0 && now-held>=700) { reason="PS held: shutdown gesture."; return false; }
                    if(ps) { if(held<0) held=now; }
                    else
                    {
                        reason=held>=0 ? "Short tap released: launch now." : "PS already released when helper started: launch now.";
                        return true;
                    }
                }
                catch(SocketException ex)
                {
                    if(ex.SocketErrorCode!=SocketError.TimedOut && ex.SocketErrorCode!=SocketError.ConnectionReset) throw;
                }
                now=clock.ElapsedMilliseconds;
                if(!diagnostic && last>=0 && now-last>250) { reason="Physical controller feed stopped (disconnect or shutdown)."; return false; }
            }
            if(diagnostic) { reason="Diagnostic complete."; return false; }
            reason=connectedPads.Count>1 ? "Multiple controllers: no PS press observed, so the source of an already-completed tap is unknown." : "No short tap received. Check DS4Windows UDP server on 127.0.0.1:26760.";
            return false;
        }
    }
    // Integration tests use an actual loopback UDP server and production parser.
    static bool Scenario(string mode)
    {
        using(UdpClient server=new UdpClient(new IPEndPoint(IPAddress.Loopback,0)))
        {
            int port=((IPEndPoint)server.Client.LocalEndPoint).Port; bool stop=false; Exception failure=null;
            Thread thread=new Thread(delegate()
            {
                try
                {
                    IPEndPoint peer=null; byte[] req=server.Receive(ref peer);
                    if(req.Length!=28 || BitConverter.ToUInt32(req,8)!=Crc(req)) throw new Exception("Bad subscription");
                    Stopwatch timer=Stopwatch.StartNew(); uint number=0;
                    while(!Volatile.Read(ref stop))
                    {
                        long ms=timer.ElapsedMilliseconds;
                        if(mode=="silent" || (mode=="drop" && ms>300)) { Thread.Sleep(10); continue; }
                        // Match the live DS4Windows packet: byte 31 stays zero.
                        byte[] d=Packet(true,100); d[21]=2; d[24]=42; Put(d,32,number++);
                        bool multi=mode.StartsWith("multi");
                        if(multi) {
                            for(byte idle=0;idle<3;idle++) { d[20]=idle; d[24]=(byte)(42+idle); Put(d,8,Crc(d)); server.Send(d,d.Length,peer); }
                            d[20]=3; d[24]=45;
                        }
                        d[38]=(byte)((mode=="multihold" || (mode=="multitap" && ms<350) || mode=="hold" || mode=="drop" || mode=="disconnect" || (mode=="tap" && ms<100) || (mode=="slowtap" && ms<350) || (mode=="holdrelease" && ms<1000)) ? 1 : 0);
                        if(mode=="disconnect" && ms>300) { d[21]=0; d[31]=0; }
                        Put(d,8,Crc(d)); if(mode=="invalid") d[40]^=1;
                        server.Send(d,d.Length,peer); Thread.Sleep(5);
                    }
                } catch(Exception ex) { failure=ex; }
            });
            thread.IsBackground=true; thread.Start(); string reason; bool result;
            Stopwatch elapsed=Stopwatch.StartNew();
            try { result=Observe(port,false,out reason); }
            finally { Volatile.Write(ref stop,true); thread.Join(2000); }
            if(failure!=null) throw failure;
            bool expected=mode=="tap" || mode=="released" || mode=="slowtap" || mode=="multitap";
            bool timely=!expected || elapsed.ElapsedMilliseconds<650;
            bool pass=result==expected && timely;
            Log("TEST "+mode+": "+(pass ? "PASS " : "FAIL ")+reason+" Elapsed="+elapsed.ElapsedMilliseconds+"ms"); return pass;
        }
    }
}

