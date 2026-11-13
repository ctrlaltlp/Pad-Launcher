using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace PadLauncher {
    public partial class Launcher {
        readonly Grid listContent=new Grid {Name="ListTransitionContent"};
        readonly TranslateTransform listTranslation=new TranslateTransform();
        Grid outgoingList;
        int listTransitionGeneration;
        internal bool ListTransitionActive {get {return outgoingList!=null;}}
        internal bool ListTransitionVertical {get;private set;}
        void StopListTransition() {
            listTransitionGeneration++;
            listTranslation.BeginAnimation(TranslateTransform.XProperty,null);
            listTranslation.BeginAnimation(TranslateTransform.YProperty,null);
            listTranslation.X=0; listTranslation.Y=0;
            listContent.BeginAnimation(OpacityProperty,null); listContent.Opacity=1;
            collectionText.BeginAnimation(OpacityProperty,null); collectionText.Opacity=1;
            title.BeginAnimation(OpacityProperty,null); title.Opacity=1;
            if(outgoingList!=null) {
                outgoingList.BeginAnimation(OpacityProperty,null);
                var translation=outgoingList.RenderTransform as TranslateTransform;
                if(translation!=null) {translation.BeginAnimation(TranslateTransform.XProperty,null); translation.BeginAnimation(TranslateTransform.YProperty,null);}
                carouselHost.Children.Remove(outgoingList); outgoingList.Children.Clear(); outgoingList=null;
            }
        }
        Grid DetachOutgoingList() {
            double x=listTranslation.X,y=listTranslation.Y,opacity=listContent.Opacity;
            StopListTransition();
            if(!IsVisible || !SystemParameters.ClientAreaAnimation) return null;
            // Reuse the existing panel visuals instead of taking a bitmap or decoding artwork again.
            var oldPanels=new Canvas {Width=carousel.Width,Height=carousel.Height,VerticalAlignment=carousel.VerticalAlignment,HorizontalAlignment=carousel.HorizontalAlignment,ClipToBounds=carousel.ClipToBounds,RenderTransformOrigin=carousel.RenderTransformOrigin,RenderTransform=carousel.RenderTransform.CloneCurrentValue()};
            foreach(UIElement card in carousel.Children.Cast<UIElement>().ToArray()) {carousel.Children.Remove(card); oldPanels.Children.Add(card);}
            var layer=new Grid {Name="OutgoingListTransition",IsHitTestVisible=false,Opacity=opacity,RenderTransform=new TranslateTransform(x,y)};
            layer.Children.Add(oldPanels); Panel.SetZIndex(layer,0); carouselHost.Children.Add(layer); outgoingList=layer; return layer;
        }
        void AnimateListSwitch(Grid oldList,int delta,bool vertical) {
            if(oldList==null) return;
            int generation=listTransitionGeneration; ListTransitionVertical=vertical;
            double distance=(vertical ? 105 : 140)*Math.Sign(delta);
            var axis=vertical ? TranslateTransform.YProperty : TranslateTransform.XProperty;
            var duration=TimeSpan.FromMilliseconds(240);
            Func<double,double,DoubleAnimation> animation=(from,to) => new DoubleAnimation(from,to,duration) {EasingFunction=new SineEase {EasingMode=EasingMode.EaseOut},FillBehavior=FillBehavior.Stop};
            var outgoingTranslation=(TranslateTransform)oldList.RenderTransform;
            double start=vertical ? outgoingTranslation.Y : outgoingTranslation.X;
            outgoingTranslation.BeginAnimation(axis,animation(start,-distance));
            oldList.BeginAnimation(OpacityProperty,animation(oldList.Opacity,0));
            listTranslation.BeginAnimation(axis,animation(distance,0));
            var incoming=animation(.5,1);
            incoming.Completed+=(s,e) => {if(generation==listTransitionGeneration) StopListTransition();};
            listContent.BeginAnimation(OpacityProperty,incoming);
            collectionText.BeginAnimation(OpacityProperty,animation(.5,1));
            title.BeginAnimation(OpacityProperty,animation(.5,1));
        }
    }
}
