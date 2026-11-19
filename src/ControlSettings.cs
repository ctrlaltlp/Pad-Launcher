using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace PadLauncher {
    public partial class Launcher {
        Window changeControlsWindow;
        void ShowControlSettings(Window owner,Action rebuild) {
            if(changeControlsWindow!=null) {
                if(changeControlsWindow.WindowState==WindowState.Minimized) changeControlsWindow.WindowState=WindowState.Normal;
                changeControlsWindow.Activate(); return;
            }
            var draft=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
            draft.ControlButtons=ControlBindings.Actions.ToDictionary(action => action.FullId,action => ControlBindings.Get(settings,action.FullId));
            draft.ControlKeys=ControlBindings.Actions.ToDictionary(action => action.FullId,action => ControlBindings.Get(settings,action.FullId,true));
            var dialog=new Window {Name="ChangeControlsWindow",Title="Change controls",Owner=owner,Width=830,Height=1040,MinWidth=680,MinHeight=450,Background=owner.Background,Foreground=white,FontFamily=FontFamily,Topmost=true,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=true,Icon=Icon};
            changeControlsWindow=dialog;
            dialog.Closed+=(s,e) => {if(changeControlsWindow==dialog) changeControlsWindow=null;};
            ApplySettingsTheme(dialog); SettingsWindowSizing.Attach(dialog);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var header=new StackPanel(); DockPanel.SetDock(header,Dock.Top); root.Children.Add(header);
            header.Children.Add(Label("Change controls",25,white));
            var styleCaption=Label("Button style",14,mint); styleCaption.Margin=new Thickness(0,12,0,6); header.Children.Add(styleCaption);
            var styles=new StackPanel {Name="ButtonStyleChoices",Orientation=Orientation.Horizontal};
            header.Children.Add(styles);
            var note=Label("Click a controller label to assign an action. Yellow labels share an action with another button. Use Keyboard shortcuts to change keys. Save changes applies all edits; Close discards unsaved edits.",12,muted); note.TextWrapping=TextWrapping.Wrap; note.Margin=new Thickness(0,10,0,12); header.Children.Add(note);
            var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var status=Label("",12,muted); status.Name="ControlBindingsStatus"; status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,12,0,10); footer.Children.Add(status);
            var actions=new Grid(); actions.ColumnDefinitions.Add(new ColumnDefinition()); actions.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto}); footer.Children.Add(actions);
            var restore=new Button {Name="RestoreControlBindings",Content="Restore defaults",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,8,12,8)}; actions.Children.Add(restore);
            var right=new StackPanel {Orientation=Orientation.Horizontal}; Grid.SetColumn(right,1); actions.Children.Add(right);
            var cancel=new Button {Content="Close",IsCancel=true,Padding=new Thickness(12,8,12,8),Margin=new Thickness(8,0,0,0)}; cancel.Click+=(s,e) => dialog.Close();
            var save=new Button {Name="SaveControlBindings",Content="Save changes",Background=mint,Foreground=Brushes.Black,Padding=new Thickness(12,8,12,8)}; right.Children.Add(save); right.Children.Add(cancel);
            var tabs=new TabControl {Name="ControlBindingContexts",Background=dialog.Background}; root.Children.Add(tabs); StyleSettingsSubtabs(tabs);
            var diagramUpdates=new List<Action>();
            var rebuildDiagrams=new List<Action>();
            Action validate=() => {
                foreach(var update in diagramUpdates) update();
                string conflict=ControlBindings.Conflict(draft);
                status.Text=conflict ?? "Changes are saved together. One action can use multiple controller buttons; yellow labels identify those assignments.";
                status.Foreground=conflict==null ? muted : new SolidColorBrush(Color.FromRgb(255,135,135)); save.IsEnabled=conflict==null;
            };
            foreach(string context in ControlBindings.Contexts) {
                var content=new DockPanel {Margin=new Thickness(0,12,0,0)};
                tabs.Items.Add(new TabItem {Header=ControlBindings.ContextName(context),Content=content});
                var keyboard=new Button {Name="EditKeyboard_"+context,Content="Keyboard shortcuts…",HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(12,7,12,7),Margin=new Thickness(0,0,0,6)};
                DockPanel.SetDock(keyboard,Dock.Top); content.Children.Add(keyboard);
                keyboard.Click+=(s,e) => ShowKeyboardShortcuts(dialog,draft,context,validate);
                var diagram=new Viewbox {Name="ControlPreviewFit_"+context,Stretch=Stretch.Uniform,StretchDirection=StretchDirection.DownOnly,HorizontalAlignment=HorizontalAlignment.Center,VerticalAlignment=VerticalAlignment.Top};
                content.Children.Add(diagram);
                int index=diagramUpdates.Count;
                diagramUpdates.Add(null);
                Action rebuildDiagram=() => {
                    Action update;
                    diagram.Child=CreateControlDiagram(draft,context,out update,validate);
                    diagramUpdates[index]=update;
                };
                rebuildDiagram();
                rebuildDiagrams.Add(rebuildDiagram);
            }
            var styleButtons=new Dictionary<string,Button>();
            Action refreshStyleButtons=() => {
                foreach(var pair in styleButtons) {
                    bool selected=Prompts.Style(draft)==pair.Key;
                    pair.Value.Content=pair.Key;
                    pair.Value.Background=selected ? mint : new SolidColorBrush(Color.FromRgb(34,51,63));
                    pair.Value.Foreground=selected ? Brushes.Black : white;
                }
            };
            foreach(string style in new[]{"PlayStation","Xbox","Nintendo"}) {
                string selectedStyle=style;
                var choice=new Button {Padding=new Thickness(13,7,13,7),Margin=new Thickness(0,0,8,0)};
                choice.Click+=(s,e) => {
                    if(Prompts.Style(draft)==selectedStyle) return;
                    draft.ButtonPrompts=selectedStyle;
                    refreshStyleButtons();
                    foreach(var buildDiagram in rebuildDiagrams) buildDiagram();
                    validate();
                };
                styleButtons.Add(style,choice);
                styles.Children.Add(choice);
            }
            refreshStyleButtons();
            restore.Click+=(s,e) => {
                var defaults=new Settings {JumpListButton="off"};
                draft.ControlButtons=ControlBindings.Actions.ToDictionary(action => action.FullId,action => ControlBindings.Get(defaults,action.FullId));
                draft.ControlKeys=ControlBindings.Actions.ToDictionary(action => action.FullId,action => ControlBindings.Get(defaults,action.FullId,true));
                draft.ControlButtonAlternates=null; validate();
            };
            save.Click+=(s,e) => {
                if(ControlBindings.Conflict(draft)!=null) {validate(); return;}
                try {
                    var next=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(settings));
                    next.ButtonPrompts=draft.ButtonPrompts;
                    next.ControlButtons=new Dictionary<string,string>(draft.ControlButtons); next.ControlKeys=new Dictionary<string,string>(draft.ControlKeys);
                    next.ControlButtonAlternates=draft.ControlButtonAlternates==null ? null : draft.ControlButtonAlternates.ToDictionary(pair => pair.Key,pair => new List<string>(pair.Value));
                    next.ControlBindingVersion=8; next.JumpListButton=ControlBindings.HasJump(next) ? "left" : "off";
                    Storage.Write("settings.json",next); settings=next; RefreshSearchControlHints();
                    exitButton.Reset(); navigation.Reset(); previousButtons=0; searchButtons=UInt16.MaxValue; bindingLeftTriggerDown=bindingRightTriggerDown=false; testLayoutTriggerDown=false; suppressSearchRelease=true;
                    dialog.Close(); rebuild();
                } catch(Exception error) {status.Text="Could not save controls: "+error.Message;}
            };
            validate(); dialog.Show();
        }
        void ShowKeyboardShortcuts(Window owner,Settings draft,string context,Action changed) {
            var keys=new Dictionary<string,string>(draft.ControlKeys);
            var dialog=new Window {Name="KeyboardShortcutsWindow",Title="Keyboard shortcuts",Owner=owner,Width=650,Height=720,MinWidth=480,MinHeight=350,Background=owner.Background,Foreground=white,FontFamily=FontFamily,Topmost=true,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.CenterOwner};
            ApplySettingsTheme(dialog); SettingsWindowSizing.Attach(dialog);
            var root=new DockPanel {Margin=new Thickness(24)}; dialog.Content=root;
            var heading=Label(ControlBindings.ContextName(context)+" · keyboard shortcuts",21,mint); heading.TextWrapping=TextWrapping.Wrap; heading.Margin=new Thickness(0,0,0,16); DockPanel.SetDock(heading,Dock.Top); root.Children.Add(heading);
            var footer=new StackPanel(); DockPanel.SetDock(footer,Dock.Bottom); root.Children.Add(footer);
            var status=Label("Click a shortcut, then press a key combination. × clears it.",12,muted); status.Name="KeyboardShortcutStatus"; status.TextWrapping=TextWrapping.Wrap; status.Margin=new Thickness(0,12,0,12); footer.Children.Add(status);
            var buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right}; footer.Children.Add(buttons);
            var cancel=new Button {Content="Cancel",IsCancel=true,Padding=new Thickness(12,8,12,8),Margin=new Thickness(0,0,8,0)}; cancel.Click+=(s,e) => dialog.Close(); buttons.Children.Add(cancel);
            var apply=new Button {Name="ApplyKeyboardShortcuts",Content="Apply",Padding=new Thickness(12,8,12,8),Background=mint,Foreground=Brushes.Black}; buttons.Children.Add(apply);
            var panel=new StackPanel(); root.Children.Add(new ScrollViewer {Content=panel,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled});
            var keyButtons=new Dictionary<string,Button>(); string capturing=null;
            Action validate=() => {
                var check=Storage.Json().Deserialize<Settings>(Storage.Json().Serialize(draft)); check.ControlKeys=keys;
                string conflict=ControlBindings.Conflict(check); apply.IsEnabled=conflict==null && capturing==null;
                status.Text=capturing!=null ? "Press the shortcut. Click the same button to cancel capture." : conflict ?? "Keyboard edits remain pending until Save changes in the main window.";
                status.Foreground=conflict==null ? muted : new SolidColorBrush(Color.FromRgb(255,135,135));
            };
            Action stopCapture=() => {if(capturing!=null) keyButtons[capturing].Content=ControlBindings.KeyLabel(keys[capturing]); capturing=null; validate();};
            foreach(var action in ControlBindings.InContext(context).OrderBy(item => item.Name)) {
                string id=action.FullId; var row=new Grid {Margin=new Thickness(0,0,0,12)};
                row.ColumnDefinitions.Add(new ColumnDefinition()); row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(160)}); row.ColumnDefinitions.Add(new ColumnDefinition {Width=new GridLength(34)});
                var label=Label(action.Name,13,white); label.TextWrapping=TextWrapping.Wrap; label.Margin=new Thickness(0,0,10,0); row.Children.Add(label);
                var capture=new Button {Name="ControlKey_"+id.Replace('.','_').Replace('-','_'),Content=ControlBindings.KeyLabel(keys[id]),Padding=new Thickness(8),Margin=new Thickness(0,0,6,0)}; Grid.SetColumn(capture,1); row.Children.Add(capture); keyButtons[id]=capture;
                capture.Click+=(s,e) => {bool same=capturing==id; stopCapture(); if(!same) {capturing=id; capture.Content="Press shortcut…"; validate();}};
                var clear=new Button {Content="×",Padding=new Thickness(4),ToolTip="Clear shortcut"}; Grid.SetColumn(clear,2); row.Children.Add(clear);
                clear.Click+=(s,e) => {stopCapture(); keys[id]="none"; capture.Content="Unassigned"; validate();}; panel.Children.Add(row);
            }
            dialog.PreviewKeyDown+=(s,e) => {if(capturing==null) return; e.Handled=true; string value=ControlBindings.KeyChord(e.Key==Key.System ? e.SystemKey : e.Key,Keyboard.Modifiers); if(value!=null) {keys[capturing]=value; stopCapture();}};
            apply.Click+=(s,e) => {if(!apply.IsEnabled) return; draft.ControlKeys=keys; changed(); dialog.Close();};
            validate(); dialog.ShowDialog();
        }
    }
}
