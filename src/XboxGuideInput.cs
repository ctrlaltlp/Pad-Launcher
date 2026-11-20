using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;

namespace PadLauncher {
    // GameInput v4 ABI, Microsoft GameInput.h, include/v4.
    internal sealed class XboxGuideInput : IDisposable {
        const uint SystemButtonReading=0x00200000;
        const uint Guide=1;
        static readonly Guid InterfaceId=new Guid("F2DAAE4C-4D21-4250-8953-4F791E6279AB");
        [DllImport("GameInputRedist.dll",ExactSpelling=true)]
        [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
        static extern int GameInputInitialize(ref Guid iid,out IntPtr input);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void EnableBackgroundInput(IntPtr input,[MarshalAs(UnmanagedType.I1)] bool enabled);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        delegate bool GetSystemButtons(IntPtr reading,out uint buttons);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void ReadingCallback(ulong token,IntPtr context,IntPtr reading);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate int RegisterReading(IntPtr input,IntPtr device,uint kind,IntPtr context,ReadingCallback callback,out ulong token);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        [return: MarshalAs(UnmanagedType.I1)]
        delegate bool UnregisterReading(IntPtr input,ulong token);

        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate void GetReadingDevice(IntPtr reading,out IntPtr device);
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
        delegate ulong GetReadingTimestamp(IntPtr reading);

        readonly ReadingCallback systemCallback=OnSystemReading;
        readonly ReadingCallback gamepadCallback=OnGamepadReading;
        int gamepadSeen;
        static void OnSystemReading(ulong token,IntPtr context,IntPtr reading) {
            var self=(XboxGuideInput)GCHandle.FromIntPtr(context).Target;
            uint buttons;
            if(((GetSystemButtons)Method(reading,21,typeof(GetSystemButtons)))(reading,out buttons)) {
                IntPtr device;
                ((GetReadingDevice)Method(reading,5,typeof(GetReadingDevice)))(reading,out device);
                if(device==IntPtr.Zero) return;
                try {
                    ulong now=((GetReadingTimestamp)Method(reading,4,typeof(GetReadingTimestamp)))(reading);
                    bool down=(buttons&Guide)!=0;
                    lock(self.guideState) {
                        ulong downAt;
                        if(down) {
                            if(!self.guideDownAt.ContainsKey(device)) self.guideDownAt[device]=now;
                        } else if(self.guideDownAt.TryGetValue(device,out downAt)) {
                            self.guideDownAt.Remove(device);
                            ulong held=now>=downAt ? now-downAt : UInt64.MaxValue;
                            if(held<350000) Interlocked.Exchange(ref self.pending,1);
                            Log("Guide held for "+held/1000+" ms on device 0x"+device.ToInt64().ToString("X"));
                        }
                    }
                    Log("System button reading: 0x"+buttons.ToString("X8")+" device=0x"+device.ToInt64().ToString("X"));
                } finally {Marshal.Release(device);}
            } else Log("System reading had no button state.");
        }
        static void OnGamepadReading(ulong token,IntPtr context,IntPtr reading) {
            var self=(XboxGuideInput)GCHandle.FromIntPtr(context).Target;
            if(Interlocked.Exchange(ref self.gamepadSeen,1)==0) Log("Received ordinary gamepad input.");
        }
        Thread worker;
        readonly ManualResetEvent stop=new ManualResetEvent(false);
        int pending;
        readonly Dictionary<IntPtr,ulong> guideDownAt=new Dictionary<IntPtr,ulong>();
        readonly object guideState=new object();
        bool attempted;
        public void Start() {
            if(attempted) return;
            attempted=true;
            stop.Reset();
            worker=new Thread(() => Run(false)) {IsBackground=true};
            worker.SetApartmentState(ApartmentState.MTA);
            worker.Start();
        }
        public void Probe() {
            var probe=new Thread(() => Run(true));
            probe.SetApartmentState(ApartmentState.MTA);
            probe.Start(); probe.Join();
        }
        void Run(bool probeOnly) {
            IntPtr input=IntPtr.Zero;
            try {
                var iid=InterfaceId;
                int result=GameInputInitialize(ref iid,out input);
                Log("GameInput v4 initialize: 0x"+result.ToString("X8"));
                if(result<0 || input==IntPtr.Zero) return;
                ((EnableBackgroundInput)Method(input,13,typeof(EnableBackgroundInput)))(input,true);
                var register=(RegisterReading)Method(input,7,typeof(RegisterReading));
                var unregister=(UnregisterReading)Method(input,9,typeof(UnregisterReading));
                var context=GCHandle.Alloc(this);
                ulong systemToken=0,gamepadToken=0;
                try {
                    result=register(input,IntPtr.Zero,SystemButtonReading,GCHandle.ToIntPtr(context),systemCallback,out systemToken);
                    Log("System-button subscription: 0x"+result.ToString("X8"));
                    result=register(input,IntPtr.Zero,0x00040000,GCHandle.ToIntPtr(context),gamepadCallback,out gamepadToken);
                    Log("Gamepad subscription: 0x"+result.ToString("X8"));
                    if(!probeOnly) stop.WaitOne();
                } finally {
                    if(systemToken!=0) unregister(input,systemToken);
                    if(gamepadToken!=0) unregister(input,gamepadToken);
                    context.Free();
                }
            } catch(DllNotFoundException) {Log("GameInput v4 redistributable not found in System32.");}
              catch(EntryPointNotFoundException) {Log("GameInput v4 initialization export unavailable.");}
            finally {if(input!=IntPtr.Zero) Marshal.Release(input);}
        }
        static Delegate Method(IntPtr instance,int slot,Type type) {
            return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(instance),slot*IntPtr.Size),type);
        }
        public bool TakePress() {return Interlocked.Exchange(ref pending,0)!=0;}
        public void Dispose() {
            if(worker!=null) {
                stop.Set();
                if(!worker.Join(500)) return;
                worker=null;
            }
            attempted=false; lock(guideState) guideDownAt.Clear();
            TakePress();
        }
        [System.Diagnostics.Conditional("XBOX_GUIDE_TEST")]
        static void Log(string message) {
            try { File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PadLauncher.XboxGuideTest.log"),DateTime.UtcNow.ToString("O")+" "+message+Environment.NewLine); }
            catch(IOException) {} catch(UnauthorizedAccessException) {}
        }
    }
}
