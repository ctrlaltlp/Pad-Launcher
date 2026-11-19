using System;
using System.Linq;

namespace PadLauncher {
    public partial class Launcher {
        bool bindingLeftTriggerDown,bindingRightTriggerDown;
        void RefreshSearchControlHints() {
            if(searchControlHints==null) return;
            string gamesHint=ControlBindings.Buttons(settings,"typing.finish").Length==0 ? "" : " · "+ControlBindings.Label(settings,"typing.finish")+": games";
            searchControlHints.Text=ControlBindings.Label(settings,"typing.type")+": type · "+ControlBindings.Label(settings,"typing.space")+": space · "+ControlBindings.Label(settings,"typing.backspace")+": backspace · "+ControlBindings.Label(settings,"typing.cancel")+": exit"+gamesHint+" · "+ControlBindings.Label(settings,"typing.play")+": play";
        }
        PadState RemapController(PadState raw,string context) {
            if(raw.LT<80) bindingLeftTriggerDown=false; else if(raw.LT>=160) bindingLeftTriggerDown=true;
            if(raw.RT<80) bindingRightTriggerDown=false; else if(raw.RT>=160) bindingRightTriggerDown=true;
            var mapped=raw; mapped.Buttons=0; mapped.LT=0; mapped.RT=0;
            foreach(var action in ControlBindings.InContext(context)) if(BindingActionDown(raw,action.FullId)) {
                if(action.Id=="layout") mapped.RT=255; else mapped.Buttons|=(ushort)action.Mask;
            }
            return mapped;
        }
        bool BindingActionDown(PadState raw,string id) {
            if(BindingButtonDown(raw,ControlBindings.Get(settings,id))) return true;
            System.Collections.Generic.List<string> extras;
            if(settings.ControlButtonAlternates!=null && settings.ControlButtonAlternates.TryGetValue(id,out extras) && extras!=null) foreach(string input in extras) if(BindingButtonDown(raw,input)) return true;
            return false;
        }
        bool BindingButtonDown(PadState raw,string input) {
            switch(input) {
                    case "south": return (raw.Buttons&0x1000)!=0; case "east": return (raw.Buttons&0x2000)!=0;
                    case "west": return (raw.Buttons&0x4000)!=0; case "north": return (raw.Buttons&0x8000)!=0;
                    case "lb": return (raw.Buttons&0x100)!=0; case "rb": return (raw.Buttons&0x200)!=0;
                    case "ls": return (raw.Buttons&0x40)!=0; case "rs": return (raw.Buttons&0x80)!=0;
                    case "select": return (raw.Buttons&0x20)!=0; case "start": return (raw.Buttons&0x10)!=0;
                    case "up": return (raw.Buttons&1)!=0; case "down": return (raw.Buttons&2)!=0;
                    case "left": return (raw.Buttons&4)!=0; case "right": return (raw.Buttons&8)!=0;
                    case "lt": return bindingLeftTriggerDown; case "rt": return bindingRightTriggerDown;
                    default: return false;
            }
        }
        void ToggleTestLayoutFromKeyboard() {HandleTestLayoutTrigger(new PadState()); HandleTestLayoutTrigger(new PadState {RT=255}); HandleTestLayoutTrigger(new PadState());}
        void OpenSettingsFromSearch() {if(GameSearchOpen) gameSearchWindow.Close(); ShowHelpWindow();}
        void ToggleSearchFavourite() {
            if(visible.Count<=selected) return;
            string id=visible[selected].Id; ToggleFavourite();
            int index=visible.FindIndex(game => game.Id==id); if(index>=0) {selected=index; Render(false);}
        }
    }
}
