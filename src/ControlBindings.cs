using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Input;

namespace PadLauncher {
    public sealed class ControlAction {
        public string Context,Id,Name,Button,Key;
        public int Mask;
        public string FullId {get {return Context+"."+Id;}}
    }
    public static class ControlBindings {
        public static readonly string[] Contexts={"overlay","typing","results"};
        public static readonly string[] Inputs={"none","south","east","west","north","lb","rb","lt","rt","ls","rs","select","start","up","down","left","right"};
        public static readonly List<ControlAction> Actions=Build();
        static readonly Dictionary<string,ControlAction> byId=Actions.ToDictionary(action => action.FullId);
        static readonly Dictionary<string,ControlAction[]> byContext=Contexts.ToDictionary(context => context,context => Actions.Where(action => action.Context==context).ToArray());
        public static ControlAction[] InContext(string context) {return byContext[context];}
        static List<ControlAction> Build() {
            var list=new List<ControlAction>();
            foreach(string context in Contexts) {
                list.Add(new ControlAction {Context=context,Id="left",Name="Browse / navigate left",Button="left",Key="Left",Mask=4});
                list.Add(new ControlAction {Context=context,Id="right",Name="Browse / navigate right",Button="right",Key="Right",Mask=8});
                list.Add(new ControlAction {Context=context,Id="up",Name="Browse / navigate up",Button="up",Key="Up",Mask=1});
                list.Add(new ControlAction {Context=context,Id="down",Name="Browse / navigate down",Button="down",Key="Down",Mask=2});
                list.Add(new ControlAction {Context=context,Id="favourite",Name="Toggle favourite",Button="rs",Key=context=="overlay" ? "F" : "none",Mask=0x80});
                list.Add(new ControlAction {Context=context,Id="jump",Name="Jump to configured list",Button="ls",Key="none",Mask=0x40});
                list.Add(new ControlAction {Context=context,Id="layout",Name="Toggle Grid 5×3 / carousel",Button=context=="typing" ? "lt" : "west",Key="none"});
                list.Add(new ControlAction {Context=context,Id="settings",Name="Open Settings",Button="none",Key="none",Mask=0x800});
            }
            Action<string,string,string,string,string,int> add=(context,id,name,button,key,mask) => list.Add(new ControlAction {Context=context,Id=id,Name=name,Button=button,Key=key,Mask=mask});
            add("overlay","play","Launch selected game","south","Return",0x1000);
            add("overlay","dismiss","Dismiss overlay (tap)","east","Escape",0x2000);
            add("overlay","exit","Exit app (hold controller button 1 sec)","east","none",0x400);
            add("overlay","previous","Previous list","lb","PageUp",0x100);
            add("overlay","next","Next list","rb","PageDown",0x200);
            add("overlay","search","Search games","north","Ctrl+F",0x8000);
            add("overlay","monitor","Next monitor","lt","M",0x4000);
            add("overlay","refresh","Refresh library","select","F5",0x20);
            add("overlay","hints","Show control hints","start","F1",0x10);
            add("typing","play","Launch selected result","start","none",0x10);
            add("results","focus","Return to keyboard","rt","none",0x10);
            add("results","monitor","Next monitor","lt","M",0x4000);
            add("results","play-start","Launch selected result (Start)","start","none",0x1000);
            add("typing","type","Type selected key","south","none",0x1000);
            add("typing","space","Insert space","north","none",0x2000);
            add("typing","backspace","Backspace","west","Back",0x4000);
            add("typing","finish","Focus found games","rt","Return",0x8000);
            add("typing","cancel","Exit search","east","Escape",0x100);
            add("results","play","Launch selected result","south","Return",0x1000);
            add("results","cancel","Exit search","east","Escape",0x2000);
            add("results","cancel-alt","Exit search (alternate)","none","none",0x2000);
            add("results","keyboard","Return to keyboard","none","Back",0x8000);
            return list;
        }
        public static string ContextName(string context) {return context=="overlay" ? "Overlay" : context=="typing" ? "Search keyboard" : "Search results";}
        public static string Get(Settings settings,string id,bool keyboard=false) {
            var action=byId[id]; string value;
            var saved=keyboard ? settings.ControlKeys : settings.ControlButtons;
            if(saved!=null && saved.TryGetValue(id,out value) && (keyboard ? ValidKey(value) : Inputs.Contains(value))) return value;
            return keyboard ? action.Key : action.Id=="jump" && settings.JumpListButton!="left" ? "none" : action.Button;
        }
        public static string ButtonLabel(Settings settings,string input) {
            var names=Prompts.Buttons(settings);
            switch(input) {
                case "south": return names[0]; case "east": return names[1]; case "west": return names[5]; case "north": return names[3];
                case "select": return Prompts.SelectButton(settings); case "start": return names[4];
                case "lb": return Prompts.Style(settings)=="Xbox" ? "LB" : Prompts.Style(settings)=="Nintendo" ? "L" : "L1";
                case "rb": return Prompts.Style(settings)=="Xbox" ? "RB" : Prompts.Style(settings)=="Nintendo" ? "R" : "R1";
                case "lt": return Prompts.Style(settings)=="Xbox" ? "LT" : Prompts.Style(settings)=="Nintendo" ? "ZL" : "L2";
                case "rt": return Prompts.RightTrigger(settings);
                case "ls": return "L3"; case "rs": return "R3";
                case "up": return "D-pad ↑"; case "down": return "D-pad ↓"; case "left": return "D-pad ←"; case "right": return "D-pad →";
                default: return "Unassigned";
            }
        }
        public static string[] Buttons(Settings settings,string id) {
            List<string> extra;
            return new[]{Get(settings,id)}.Concat(settings.ControlButtonAlternates!=null && settings.ControlButtonAlternates.TryGetValue(id,out extra) && extra!=null ? extra : Enumerable.Empty<string>()).Where(input => input!="none" && Inputs.Contains(input)).Distinct().ToArray();
        }
        public static bool UsesButton(Settings settings,string id,string input) {return Buttons(settings,id).Contains(input);}
        public static void SetButtons(Settings settings,string id,IEnumerable<string> inputs) {
            var buttons=inputs.Where(input => input!="none" && Inputs.Contains(input)).Distinct().ToArray();
            if(settings.ControlButtons==null) settings.ControlButtons=Actions.ToDictionary(action => action.FullId,action => Get(settings,action.FullId));
            if(settings.ControlButtonAlternates==null) settings.ControlButtonAlternates=new Dictionary<string,List<string>>();
            settings.ControlButtons[id]=buttons.Length>0 ? buttons[0] : "none";
            if(buttons.Length>1) settings.ControlButtonAlternates[id]=buttons.Skip(1).ToList(); else settings.ControlButtonAlternates.Remove(id);
        }
        public static void AssignButton(Settings settings,string context,string input,string action) {
            foreach(var item in InContext(context)) SetButtons(settings,item.FullId,Buttons(settings,item.FullId).Where(button => button!=input));
            if(action=="dismiss-exit") {SetButtons(settings,"overlay.dismiss",Buttons(settings,"overlay.dismiss").Concat(new[]{input})); SetButtons(settings,"overlay.exit",Buttons(settings,"overlay.exit").Concat(new[]{input}));}
            else if(action!="none") {string id=context+"."+action; SetButtons(settings,id,Buttons(settings,id).Concat(new[]{input}));}
        }
        public static string Label(Settings settings,string id) {var buttons=Buttons(settings,id); return buttons.Length==0 ? "Unassigned" : String.Join(" / ",buttons.Select(input => ButtonLabel(settings,input)));}
        public static string KeyLabel(string value) {if(value=="none") return "Unassigned"; var parts=value.Split('+'); if(parts[parts.Length-1]=="Return") parts[parts.Length-1]="Enter"; else if(parts[parts.Length-1]=="Back") parts[parts.Length-1]="Backspace"; return String.Join("+",parts);}
        public static string KeyChord(Key key,ModifierKeys modifiers) {
            if((modifiers&ModifierKeys.Windows)!=0 || !Enum.IsDefined(typeof(Key),key) || key==Key.None || key==Key.System || key==Key.ImeProcessed || key==Key.DeadCharProcessed || new[]{Key.LeftCtrl,Key.RightCtrl,Key.LeftAlt,Key.RightAlt,Key.LeftShift,Key.RightShift,Key.LWin,Key.RWin}.Contains(key)) return null;
            string name=key==Key.PageDown ? "PageDown" : key==Key.PageUp ? "PageUp" : key==Key.Return ? "Return" : key==Key.Escape ? "Escape" : key==Key.Back ? "Back" : key.ToString();
            return ((modifiers&ModifierKeys.Control)!=0 ? "Ctrl+" : "")+((modifiers&ModifierKeys.Alt)!=0 ? "Alt+" : "")+((modifiers&ModifierKeys.Shift)!=0 ? "Shift+" : "")+name;
        }
        static bool ValidKey(string value) {
            if(value=="none") return true; if(String.IsNullOrWhiteSpace(value)) return false;
            var parts=value.Split('+'); Key key;
            return parts.Take(parts.Length-1).All(part => new[]{"Ctrl","Alt","Shift"}.Contains(part)) && Enum.TryParse(parts.Last(),out key) && KeyChord(key,ModifierKeys.None)!=null;
        }
        public static string KeyAction(Settings settings,string context,Key key,ModifierKeys modifiers) {
            string chord=KeyChord(key,modifiers); if(chord==null) return null;
            var action=Actions.FirstOrDefault(item => item.Context==context && Get(settings,item.FullId,true)==chord); return action==null ? null : action.Id;
        }
        public static string Conflict(Settings settings) {
            foreach(string context in Contexts) foreach(bool keyboard in new[]{false,true}) {
                foreach(var group in Actions.Where(action => action.Context==context).SelectMany(action => (keyboard ? new[]{Get(settings,action.FullId,true)} : Buttons(settings,action.FullId)).Select(input => new {Action=action,Input=input})).GroupBy(item => item.Input).Where(group => group.Key!="none" && group.Count()>1)) {
                    if(!keyboard && context=="overlay" && group.Count()==2 && group.All(item => item.Action.Id=="dismiss" || item.Action.Id=="exit")) continue;
                    return ContextName(context)+": "+(keyboard ? KeyLabel(group.Key) : ButtonLabel(settings,group.Key))+" is assigned to "+String.Join(" and ",group.Select(item => item.Action.Name))+".";
                }
            }
            return null;
        }
        public static bool HasJump(Settings settings) {return Contexts.Any(context => Buttons(settings,context+".jump").Length>0 || Get(settings,context+".jump",true)!="none");}
        public static void UpgradeSearchDefaults(Settings settings) {
            if(settings.ControlBindingVersion>=7) return;
            var buttons=settings.ControlButtons; string a,b;
            if(buttons!=null) {
                if(settings.ControlBindingVersion<1) {
                    if(buttons.TryGetValue("typing.finish",out a) && a=="north" && buttons.TryGetValue("typing.cancel",out b) && b=="none") {buttons["typing.finish"]="none"; buttons["typing.cancel"]="north";}
                    if(buttons.TryGetValue("results.keyboard",out a) && a=="north" && buttons.TryGetValue("results.cancel",out b) && b=="east") {buttons["results.keyboard"]="none"; buttons["results.cancel"]="north"; buttons["results.cancel-alt"]="east";}
                }
                if(settings.ControlBindingVersion<2 && buttons.TryGetValue("typing.cancel",out a) && a=="north" && (!buttons.TryGetValue("typing.space",out b) || b=="east")) {buttons["typing.cancel"]="east"; buttons["typing.space"]="north";}
            }
            if(settings.ControlBindingVersion<3 && buttons!=null && buttons.TryGetValue("results.cancel",out a) && a=="north" && (!buttons.TryGetValue("results.keyboard",out b) || b=="none")) {
                buttons["results.cancel"]="east"; buttons["results.keyboard"]="north";
                if(buttons.TryGetValue("results.cancel-alt",out b) && b=="east") buttons["results.cancel-alt"]="none";
            }
            if(settings.ControlBindingVersion<4 && buttons!=null && buttons.TryGetValue("overlay.layout",out a) && a=="rt" && buttons.TryGetValue("overlay.monitor",out b) && b=="west") {
                buttons["overlay.layout"]="west"; buttons["overlay.monitor"]="rt";
            }
            if(settings.ControlBindingVersion<5 && buttons!=null) {
                foreach(string context in Contexts) {
                    string old=context=="overlay" ? "west" : "rt";
                    bool occupied=InContext(context).Any(action => action.Id!="layout" && UsesButton(settings,action.FullId,"lt"));
                    if(!occupied && buttons.TryGetValue(context+".layout",out a) && a==old) buttons[context+".layout"]="lt";
                }
                if(!InContext("overlay").Any(action => action.Id!="monitor" && UsesButton(settings,action.FullId,"west")) && buttons.TryGetValue("overlay.monitor",out a) && a=="rt") buttons["overlay.monitor"]="west";
                if(!InContext("typing").Any(action => action.Id!="finish" && UsesButton(settings,action.FullId,"rt")) && buttons.TryGetValue("typing.finish",out a) && a=="none") buttons["typing.finish"]="rt";
                foreach(string id in new[]{"typing.play","results.focus","results.play-start"}) {
                    if(buttons.ContainsKey(id)) continue;
                    var added=byId[id];
                    if(InContext(added.Context).Any(action => action.FullId!=id && UsesButton(settings,action.FullId,added.Button))) buttons[id]="none";
                }
            }
            if(buttons!=null && !buttons.ContainsKey("results.monitor") && InContext("results").Any(action => action.Id!="monitor" && UsesButton(settings,action.FullId,"west"))) buttons["results.monitor"]="none";
            if(buttons!=null) {
                foreach(string context in new[]{"overlay","results"}) {
                    string oldLayout,oldMonitor;
                    bool layoutSaved=buttons.TryGetValue(context+".layout",out oldLayout),monitorSaved=buttons.TryGetValue(context+".monitor",out oldMonitor);
                    bool occupied=InContext(context).Any(action => action.Id!="layout" && action.Id!="monitor" && (UsesButton(settings,action.FullId,"lt") || UsesButton(settings,action.FullId,"west")));
                    if(!occupied && (!layoutSaved || oldLayout=="lt") && (!monitorSaved || oldMonitor=="west")) {buttons[context+".layout"]="west"; buttons[context+".monitor"]="lt";}
                }
                if(buttons.TryGetValue("results.keyboard",out a) && a=="north") buttons["results.keyboard"]="none";
                List<string> extras;
                if(settings.ControlButtonAlternates!=null && settings.ControlButtonAlternates.TryGetValue("results.keyboard",out extras) && extras!=null) extras.RemoveAll(input => input=="north");
            }
            settings.ControlBindingVersion=7;
        }
    }
}
