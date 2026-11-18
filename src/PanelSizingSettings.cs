using System;
using System.Windows;
using System.Windows.Controls;

namespace PadLauncher {
    public partial class Launcher {
        static double SafePanelScale(double value) {return !Double.IsNaN(value) && !Double.IsInfinity(value) && value>=.6 && value<=1.4 ? value : 1;}
        double RelativeRetroPanelScale {get {
            var profile=ActiveListLayout;
            bool locked=profile!=null && profile.PanelSizesLocked.HasValue ? profile.PanelSizesLocked.Value : settings.PanelSizesLocked;
            double retro=profile!=null && profile.RetroPanelScale.HasValue ? profile.RetroPanelScale.Value : settings.RetroPanelScale;
            return locked ? 1 : SafePanelScale(retro)/SafePanelScale(ActivePanelScale);
        }}
        public void SetRetroPanelScale(double scale) {
            if(Double.IsNaN(scale) || Double.IsInfinity(scale) || scale<.6 || scale>1.4) throw new ArgumentOutOfRangeException("scale","Choose a size between 60% and 140%.");
            if(settings.PanelSizesLocked) {SetCarouselScale(scale); return;}
            double previous=settings.RetroPanelScale;
            try {settings.RetroPanelScale=scale; Storage.Write("settings.json",settings);} catch {settings.RetroPanelScale=previous; throw;}
            Render(false);
        }
        public void SetPanelSizesLocked(bool locked) {
            bool previous=settings.PanelSizesLocked; double previousScale=settings.RetroPanelScale;
            try {
                if(previous || locked) settings.RetroPanelScale=SafePanelScale(settings.CarouselScale);
                settings.PanelSizesLocked=locked; Storage.Write("settings.json",settings);
            } catch {settings.PanelSizesLocked=previous; settings.RetroPanelScale=previousScale; throw;}
            Render(false);
        }
        void AddPanelSizeControls(StackPanel panel,Window owner) {
            var modernCaption=Label("Modern panel size",14,mint); modernCaption.Margin=new Thickness(0,14,0,10); panel.Children.Add(modernCaption);
            var modernValue=Label("",13,white); panel.Children.Add(modernValue);
            var modern=new Slider {Name="ModernPanelSizeSetting",Minimum=.6,Maximum=1.4,Value=SafePanelScale(settings.CarouselScale),TickFrequency=.05,SmallChange=.05,LargeChange=.1,IsSnapToTickEnabled=true,Margin=new Thickness(0,4,0,8)}; panel.Children.Add(modern);
            var locked=new CheckBox {Name="LockPanelSizesSetting",Content="Adjust both panel sizes to the same height",IsChecked=settings.PanelSizesLocked,Margin=new Thickness(0,6,0,8)}; panel.Children.Add(locked);
            var lockNote=Label("When checked, changing either panel size adjusts both, keeping modern and retro panels at matching heights.",12,muted); lockNote.TextWrapping=TextWrapping.Wrap; lockNote.Margin=new Thickness(0,0,0,10); panel.Children.Add(lockNote);
            var retroCaption=Label("Retro panel size",14,mint); retroCaption.Margin=new Thickness(0,8,0,10); panel.Children.Add(retroCaption);
            var retroValue=Label("",13,white); panel.Children.Add(retroValue);
            var retro=new Slider {Name="RetroPanelSizeSetting",Minimum=.6,Maximum=1.4,Value=settings.PanelSizesLocked ? modern.Value : SafePanelScale(settings.RetroPanelScale),TickFrequency=.05,SmallChange=.05,LargeChange=.1,IsSnapToTickEnabled=true,Margin=new Thickness(0,4,0,8)}; panel.Children.Add(retro);
            bool restoring=false;
            Action update=() => {restoring=true; modern.Value=SafePanelScale(settings.CarouselScale); retro.Value=settings.PanelSizesLocked ? modern.Value : SafePanelScale(settings.RetroPanelScale); locked.IsChecked=settings.PanelSizesLocked; modernValue.Text=Math.Round(modern.Value*100)+"%"; retroValue.Text=Math.Round(retro.Value*100)+"%"; restoring=false;};
            modern.ValueChanged+=(s,e) => {if(restoring) return; try {SetCarouselScale(modern.Value);} catch(Exception error) {ThemedMessage.Show(owner,error.Message,"Panel size");} update();};
            retro.ValueChanged+=(s,e) => {if(restoring) return; try {SetRetroPanelScale(retro.Value);} catch(Exception error) {ThemedMessage.Show(owner,error.Message,"Panel size");} update();};
            locked.Click+=(s,e) => {if(restoring) return; try {SetPanelSizesLocked(locked.IsChecked==true);} catch(Exception error) {ThemedMessage.Show(owner,error.Message,"Panel size");} update();};
            update();
        }
    }
}
