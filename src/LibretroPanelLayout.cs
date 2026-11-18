using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace PadLauncher {
    public partial class Launcher {
        double LibretroPanelGap {get {double gap=settings.ArtworkGap; return !Double.IsNaN(gap) && !Double.IsInfinity(gap) && gap>=0 && gap<=64 ? gap : 16;}}
        public void SetArtworkGap(double gap) {
            if(Double.IsNaN(gap) || Double.IsInfinity(gap) || gap<0 || gap>64) throw new ArgumentOutOfRangeException("gap","Choose a gap from 0 to 64.");
            double previous=settings.ArtworkGap;
            try {settings.ArtworkGap=gap; Storage.Write("settings.json",settings);}
            catch {settings.ArtworkGap=previous; throw;}
            Render(false);
        }
        double LibretroNeighbourGap(Rect left,Rect right,bool angled) {
            // The original panels use a fixed slant but different heights. Allow
            // for the changing edge angle so the minimum visible gap stays equal.
            return LibretroPanelGap+(angled ? 73*Math.Abs(left.Height-right.Height)/(2*Math.Max(left.Height,right.Height)) : 0);
        }
        static bool IsLibretroCover(Game game) {
            return (game.ArtworkProvider ?? "").StartsWith("libretro",StringComparison.Ordinal) || LibretroArtwork.IsChosenPath(game.Art);
        }

        internal static double CoverWidthAtHeight(double imageWidth,double imageHeight,double height,double slant) {
            double ratio=imageWidth>0 && imageHeight>0 ? imageWidth/imageHeight : 2.0/3;
            // A malformed or unusually panoramic asset must not make an enormous panel.
            if(Double.IsNaN(ratio) || Double.IsInfinity(ratio)) ratio=2.0/3;
            return height*Math.Max(.2,Math.Min(3,ratio))+slant;
        }

        void UpdateGridSelection(FrameworkElement card,bool active,bool animate,double x,double y,double width,double height) {
            var transforms=card.RenderTransform as TransformGroup;
            if(transforms==null) {
                transforms=new TransformGroup(); transforms.Children.Add(new ScaleTransform(1,1)); transforms.Children.Add(new TranslateTransform());
                card.RenderTransformOrigin=new Point(.5,.5); card.RenderTransform=transforms;
            }
            bool enlarged=active && !ActivePanelOutlines;
            double zoom=1,dx=0,dy=0;
            if(enlarged) {
                double panelScale=ActivePanelScale;
                if(Double.IsNaN(panelScale) || Double.IsInfinity(panelScale) || panelScale<.6 || panelScale>1.4) panelScale=1;
                double left=800-800/panelScale,right=800+800/panelScale;
                zoom=Math.Max(1,Math.Min(1.12,Math.Min((right-left)/width,gridViewportHeight/height)));
                double expandedLeft=x-(zoom-1)*width/2,expandedRight=x+width+(zoom-1)*width/2;
                double expandedTop=y-(zoom-1)*height/2,expandedBottom=y+height+(zoom-1)*height/2;
                dx=Math.Max(0,left-expandedLeft)+Math.Min(0,right-expandedRight);
                dy=Math.Max(0,-expandedTop)+Math.Min(0,gridViewportHeight-expandedBottom);
            }
            bool motion=!preview && SystemParameters.ClientAreaAnimation && (animate || IsVisible);
            var scale=(ScaleTransform)transforms.Children[0]; var translation=(TranslateTransform)transforms.Children[1];
            AnimateGridTransform(scale,ScaleTransform.ScaleXProperty,zoom,motion);
            AnimateGridTransform(scale,ScaleTransform.ScaleYProperty,zoom,motion);
            AnimateGridTransform(translation,TranslateTransform.XProperty,dx,motion);
            AnimateGridTransform(translation,TranslateTransform.YProperty,dy,motion);
        }

        static void AnimateGridTransform(Animatable transform,DependencyProperty property,double value,bool animate) {
            if(Math.Abs((double)transform.GetAnimationBaseValue(property)-value)<.0001) return;
            double current=(double)transform.GetValue(property); transform.SetValue(property,value);
            if(!animate) {transform.BeginAnimation(property,null); return;}
            var animation=new DoubleAnimation(current,value,TimeSpan.FromMilliseconds(230)) {EasingFunction=new SineEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
            var clock=(AnimationClock)animation.CreateClock(true);
            transform.ApplyAnimationClock(property,clock,HandoffBehavior.SnapshotAndReplace);
            clock.Controller.SeekAlignedToLastTick(TimeSpan.Zero,TimeSeekOrigin.BeginTime);
        }

        Rect[] ArtworkPanelPositions(int count,int start,int pageStart,bool grid,out double rowHeight) {
            rowHeight=Math.Min(150,(1440.0-(GridColumns-1)*28)/GridColumns)*1.5;
            var games=Enumerable.Range(0,count).Select(slot => visible[grid ? pageStart+slot : (selected+start+slot+visible.Count)%visible.Count]).ToArray();
            var images=new BitmapSource[count]; var retro=new bool[count]; var placeholders=new bool[count]; var placeholderRatios=new double[count];
            for(int slot=0;slot<count;slot++) {
                FrameworkElement retained;
                if(GameSearchOpen || IsLibretroCover(games[slot]) || games[slot].PreferredArtworkSource=="libretro" || (!grid && ActiveLayout!="rectangular" && ActiveAngledSizing!="fixed"))
                    images[slot]=cards.TryGetValue(games[slot].Id,out retained) ? retained.Tag as BitmapSource : LoadImage(games[slot].Art,700);
                placeholders[slot]=images[slot]==null && games[slot].PreferredArtworkSource=="libretro";
                if(placeholders[slot]) placeholderRatios[slot]=LibretroPlaceholderSizing.Ratio(games[slot],settings);
                retro[slot]=(IsLibretroCover(games[slot]) && images[slot]!=null) || placeholders[slot];
            }
            var positions=new Rect[count];
            double retroScale=RelativeRetroPanelScale;
            if(grid) {
                // Pack each row by the actual panel widths. Shared column bounds
                // would leave gaps beside narrow covers on other rows.
                var widths=new double[count];
                for(int slot=0;slot<count;slot++) {
                    widths[slot]=placeholders[slot] ? placeholderRatios[slot]*retroScale : (retro[slot] || GameSearchOpen) && images[slot]!=null ? CoverWidthAtHeight(images[slot].PixelWidth,images[slot].PixelHeight,retro[slot] ? retroScale : 1,0) : 2.0/3;
                }
                var rowRatios=Enumerable.Range(0,(count+GridColumns-1)/GridColumns).Select(row => widths.Skip(row*GridColumns).Take(GridColumns).Sum()).ToArray();
                rowHeight=Math.Min(rowHeight,(1440-(Math.Min(GridColumns,count)-1)*LibretroPanelGap)/rowRatios.Max());
                double baseHeight=rowHeight;
                if(retro.Any(value => value)) rowHeight*=Math.Max(1,retroScale);
                for(int slot=0;slot<count;slot++) {
                    int column=slot%GridColumns;
                    double height=baseHeight*(retro[slot] ? retroScale : 1);
                    double width=widths[slot]*baseHeight;
                    int row=slot/GridColumns;
                    int rowCount=Math.Min(GridColumns,count-row*GridColumns);
                    double x=(1600-rowRatios[row]*baseHeight-(rowCount-1)*LibretroPanelGap)/2+widths.Skip(row*GridColumns).Take(column).Sum()*baseHeight+column*LibretroPanelGap;
                    positions[slot]=new Rect(x,(rowHeight-height)/2,width,height);
                }
                return positions;
            }
            bool angled=ActiveLayout!="rectangular";
            for(int slot=0;slot<count;slot++) {
                int offset=start+slot;
                double height=offset==0 ? 490 : 390-Math.Min(3,Math.Abs(offset))*16;
                double width=offset==0 ? (angled ? 355 : 300) : (angled ? 300 : 250);
                var image=images[slot];
                if(retro[slot] || (GameSearchOpen && image!=null)) {
                    double artworkHeight=angled ? Math.Min(height,(width-73)*1.5) : height;
                    // Fixed retains panel height, with the same edge-fill height
                    // as a modern 2:3 portrait. Fit trims that edge fill away.
                    if(ActiveAngledSizing!="fixed") height=artworkHeight;
                    width=placeholders[slot] ? CoverWidthAtHeight(placeholderRatios[slot],1,height,angled ? 73 : 0) : CoverWidthAtHeight(image.PixelWidth,image.PixelHeight,artworkHeight,angled ? 73 : 0);
                    if(retro[slot]) {height*=retroScale; width=(width-(angled ? 73 : 0))*retroScale+(angled ? 73 : 0);}
                } else if(angled && ActiveAngledSizing!="fixed" && image!=null) {
                    double fit=Math.Min((width-73)/image.PixelWidth,height/image.PixelHeight);
                    width=image.PixelWidth*fit+73; height=image.PixelHeight*fit;
                }
                positions[slot]=new Rect(0,(490-height)/2,width,height);
            }
            int center=-start;
            positions[center]=new Rect(800-positions[center].Width/2,positions[center].Y,positions[center].Width,positions[center].Height);
            for(int slot=center+1;slot<count;slot++) {
                double x=positions[slot-1].Right-(angled ? 73 : 0)+LibretroNeighbourGap(positions[slot-1],positions[slot],angled);
                positions[slot]=new Rect(x,positions[slot].Y,positions[slot].Width,positions[slot].Height);
            }
            for(int slot=center-1;slot>=0;slot--) {
                double x=positions[slot+1].X+(angled ? 73 : 0)-positions[slot].Width-LibretroNeighbourGap(positions[slot],positions[slot+1],angled);
                positions[slot]=new Rect(x,positions[slot].Y,positions[slot].Width,positions[slot].Height);
            }
            return positions;
        }
    }
}
