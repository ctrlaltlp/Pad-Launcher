using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PadLauncher {
    public static class LayoutPreviews {
        static readonly Dictionary<string,BitmapSource> cache=new Dictionary<string,BitmapSource>();
        public static BitmapSource Get(string layout) {
            lock(cache) {
                BitmapSource image; if(cache.TryGetValue(layout,out image)) return image;
                if(layout!="angled" && layout!="rectangular" && layout!="grid") throw new ArgumentException("Unknown overlay layout.");
                var visual=new DrawingVisual();
                using(var drawing=visual.RenderOpen()) {
                    drawing.DrawRectangle(new LinearGradientBrush(Color.FromRgb(17,32,43),Color.FromRgb(8,16,24),90),null,new Rect(0,0,300,180));
                    if(layout=="grid") {
                        for(int row=0;row<2;row++) for(int column=0;column<3;column++) DrawCover(drawing,new Rect(67+column*57,15+row*78,51,72),false,row*3+column,row==0 && column==1);
                    } else {
                        for(int index=0;index<5;index++) {
                            bool selected=index==2; double height=selected ? 142 : 116,width=selected ? 61 : 50;
                            DrawCover(drawing,new Rect(20+index*54,(180-height)/2,width,height),layout=="angled",index,selected);
                        }
                    }
                }
                var bitmap=new RenderTargetBitmap(300,180,96,96,PixelFormats.Pbgra32); bitmap.Render(visual); bitmap.Freeze(); cache[layout]=bitmap; return bitmap;
            }
        }
        static void DrawCover(DrawingContext drawing,Rect rect,bool angled,int index,bool selected) {
            var colors=new[]{Color.FromRgb(51,139,174),Color.FromRgb(171,87,121),Color.FromRgb(54,157,132),Color.FromRgb(155,109,60),Color.FromRgb(102,91,179),Color.FromRgb(66,126,161)};
            Color color=colors[index%colors.Length]; double shift=angled ? 11 : 0;
            var shape=new StreamGeometry();
            using(var context=shape.Open()) {
                context.BeginFigure(new Point(rect.Left+shift,rect.Top),true,true);
                context.LineTo(new Point(rect.Right+shift,rect.Top),true,false); context.LineTo(new Point(rect.Right-shift,rect.Bottom),true,false); context.LineTo(new Point(rect.Left-shift,rect.Bottom),true,false);
            }
            shape.Freeze(); drawing.PushOpacity(selected ? 1 : .7); drawing.PushClip(shape);
            drawing.DrawRectangle(new LinearGradientBrush(color,Color.FromRgb(12,21,33),90),null,new Rect(rect.Left-shift,rect.Top,rect.Width+shift*2,rect.Height));
            drawing.DrawEllipse(new SolidColorBrush(Color.FromArgb(160,230,238,223)),null,new Point(rect.Left+rect.Width*.72,rect.Top+rect.Height*.25),rect.Width*.1,rect.Width*.1);
            var mountains=new StreamGeometry();
            using(var context=mountains.Open()) {
                context.BeginFigure(new Point(rect.Left-shift,rect.Bottom),true,true);
                context.LineTo(new Point(rect.Left+rect.Width*.23,rect.Top+rect.Height*.47),true,false);
                context.LineTo(new Point(rect.Left+rect.Width*.48,rect.Top+rect.Height*.73),true,false);
                context.LineTo(new Point(rect.Left+rect.Width*.76,rect.Top+rect.Height*.57),true,false);
                context.LineTo(new Point(rect.Right+shift,rect.Bottom),true,false);
            }
            drawing.DrawGeometry(new SolidColorBrush(Color.FromArgb(145,9,19,27)),null,mountains);
            var title=new SolidColorBrush(Color.FromArgb(205,237,245,243));
            drawing.DrawRoundedRectangle(title,null,new Rect(rect.Left+rect.Width*.2,rect.Top+rect.Height*.8,rect.Width*.6,3),1,1);
            drawing.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(120,237,245,243)),null,new Rect(rect.Left+rect.Width*.3,rect.Top+rect.Height*.86,rect.Width*.4,2),1,1);
            drawing.Pop(); drawing.DrawGeometry(null,new Pen(new SolidColorBrush(selected ? Color.FromRgb(163,240,205) : Color.FromArgb(65,160,191,197)),selected ? 2 : 1),shape); drawing.Pop();
        }
    }
    public partial class Launcher {
        void AddLayoutPreviews(StackPanel panel,ComboBox selection) {
            var grid=new Grid {Margin=new Thickness(0,10,0,0)};
            var buttons=new Dictionary<string,Button>();
            foreach(var option in new[]{new[]{"angled","Angled"},new[]{"rectangular","Rectangular"}}) {
                string id=option[0]; int column=grid.ColumnDefinitions.Count; grid.ColumnDefinitions.Add(new ColumnDefinition());
                var content=new StackPanel(); content.Children.Add(new Image {Source=LayoutPreviews.Get(id),Width=150,Height=100,Stretch=Stretch.Uniform});
                var label=Label(option[1],13,white); label.HorizontalAlignment=HorizontalAlignment.Center; label.Margin=new Thickness(0,6,0,0); content.Children.Add(label);
                var button=new Button {Name="SetupLayoutPreview_"+id,Content=content,Padding=new Thickness(8),Margin=new Thickness(column==0 ? 0 : 8,0,0,0),ToolTip="Choose "+option[1]+" layout"};
                button.Click+=(s,e) => selection.SelectedValue=id; Grid.SetColumn(button,column); grid.Children.Add(button); buttons[id]=button;
            }
            Action update=() => {foreach(var pair in buttons) {bool selected=pair.Key==selection.SelectedValue as string; pair.Value.BorderBrush=selected ? mint : new SolidColorBrush(Color.FromRgb(58,82,96)); pair.Value.BorderThickness=new Thickness(selected ? 2 : 1);}};
            selection.SelectionChanged+=(s,e) => update(); update(); panel.Children.Add(grid);
        }
    }
}
