#if NAVIGATION_DIAGNOSTICS
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;

namespace PadLauncher {
    // Small, bounded diagnostic history. Disk writes never run on the input thread.
    internal sealed class ControllerNavigationTrace {
        readonly Queue<string> history=new Queue<string>();
        readonly object gate=new object();
        string lastState;
        double lastPoll,lastSample,lastWrite,lastFrame;
        bool wasActive;
        volatile bool writing;
        internal void Poll(double now,bool active,bool found,PadState pad,string source) {
            // Preserve the last navigation session when the user switches to
            // another window to report a problem, rather than overwriting it.
            if(!active && !wasActive) return;
            double gap=!wasActive || lastPoll==0 ? 0 : now-lastPoll;
            wasActive=active; lastPoll=active ? now : 0;
            string state=active+"|"+found+"|"+source+"|"+pad.Buttons+"|"+Axis(pad.LX)+"|"+Axis(pad.LY)+"|"+Axis(pad.RX)+"|"+Axis(pad.RY);
            if(state==lastState && gap<120 && now-lastSample<250) return;
            lastState=state; lastSample=now;
            Add(now,"poll active="+active+" found="+found+" source="+source+" buttons="+pad.Buttons.ToString("X4")+" left="+pad.LX+","+pad.LY+" right="+pad.RX+","+pad.RY+" gapMs="+gap.ToString("0.0",CultureInfo.InvariantCulture));
            Flush(now);
        }
        internal void Frame(double now,int selection,double position,double destination) {
            if(now-lastFrame<250) return;
            lastFrame=now;
            Add(now,"frame selection="+selection+" panelX="+position.ToString("0.0",CultureInfo.InvariantCulture)+" targetX="+destination.ToString("0.0",CultureInfo.InvariantCulture));
            Flush(now);
        }
        static int Axis(short value) {return value < -16000 ? -1 : value > 16000 ? 1 : 0;}
        internal void Move(double now,int direction,bool fast,double duration) {
            // Record slow work and the occasional successful move for context.
            if(duration<40 && now-lastMove<250) return;
            lastMove=now;
            Add(now,"move direction="+direction+" fast="+fast+" workMs="+duration.ToString("0.0",CultureInfo.InvariantCulture));
            Flush(now);
        }
        double lastMove;
        void Add(double now,string message) {
            lock(gate) {history.Enqueue(now.ToString("0.0",CultureInfo.InvariantCulture)+" "+message); while(history.Count>160) history.Dequeue();}
        }
        void Flush(double now) {
            if(writing || now-lastWrite<1000) return;
            string[] snapshot; lock(gate) snapshot=history.ToArray();
            string root=Storage.Root; lastWrite=now; writing=true;
            Task.Run(() => {
                try {Directory.CreateDirectory(root); File.WriteAllLines(Path.Combine(root,"controller-navigation.log"),snapshot);}
                catch {} finally {writing=false;}
            });
        }
    }
}

#endif
