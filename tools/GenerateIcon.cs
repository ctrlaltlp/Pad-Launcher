using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

// Reproducible vector-drawn icon. Render each ICO size separately for sharp edges.
public static class GenerateIcon {
    static GraphicsPath Rounded(float x,float y,float width,float height,float radius) {
        var p=new GraphicsPath(); float d=radius*2;
        p.AddArc(x,y,d,d,180,90); p.AddArc(x+width-d,y,d,d,270,90);
        p.AddArc(x+width-d,y+height-d,d,d,0,90); p.AddArc(x,y+height-d,d,d,90,90); p.CloseFigure(); return p;
    }
    static Bitmap Draw(int size) {
        var b=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(b)) {
            g.SmoothingMode=SmoothingMode.AntiAlias; g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            g.ScaleTransform(size/256f,size/256f);
            using(var tile=Rounded(8,8,240,240,53))
            using(var fill=new LinearGradientBrush(new Point(20,8),new Point(230,248),Color.FromArgb(29,51,64),Color.FromArgb(8,18,28))) {
                g.FillPath(fill,tile);
                using(var edge=new Pen(Color.FromArgb(75,135,180,182),1.5f)) g.DrawPath(edge,tile);
            }
            var left=new[]{new PointF(43,73),new PointF(91,73),new PointF(68,192),new PointF(20,192)};
            var right=new[]{new PointF(188,64),new PointF(236,64),new PointF(213,183),new PointF(165,183)};
            using(var fill=new SolidBrush(Color.FromArgb(48,92,105))) g.FillPolygon(fill,left);
            using(var fill=new SolidBrush(Color.FromArgb(65,127,132))) g.FillPolygon(fill,right);
            var center=new[]{new PointF(99,48),new PointF(188,48),new PointF(157,208),new PointF(68,208)};
            using(var fill=new LinearGradientBrush(new Point(95,48),new Point(158,210),Color.FromArgb(190,255,223),Color.FromArgb(89,207,177))) g.FillPolygon(fill,center);
            using(var edge=new Pen(Color.FromArgb(214,255,237),2)) g.DrawPolygon(edge,center);
            // The controls stay upright inside the angled carousel card.
            using(var ink=new Pen(Color.FromArgb(16,51,57),8)) {
                ink.StartCap=LineCap.Round; ink.EndCap=LineCap.Round;
                g.DrawLine(ink,106,116,106,140); g.DrawLine(ink,94,128,118,128);
            }
            using(var ink=new SolidBrush(Color.FromArgb(16,51,57))) {
                g.FillEllipse(ink,145,112,9,9); g.FillEllipse(ink,145,135,9,9);
                g.FillEllipse(ink,134,123,9,9); g.FillEllipse(ink,156,123,9,9);
            }
        }
        return b;
    }
    public static void Main(string[] args) {
        string dir=args[0]; Directory.CreateDirectory(dir);
        int[] sizes={16,20,24,32,40,48,64,128,256};
        var frames=new List<byte[]>();
        foreach(int size in sizes) using(var bitmap=Draw(size)) using(var stream=new MemoryStream()) { bitmap.Save(stream,ImageFormat.Png); frames.Add(stream.ToArray()); }
        using(var w=new BinaryWriter(File.Create(Path.Combine(dir,"PadLauncher.ico")))) {
            w.Write((ushort)0); w.Write((ushort)1); w.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) {
                w.Write((byte)(sizes[i]==256?0:sizes[i])); w.Write((byte)(sizes[i]==256?0:sizes[i])); w.Write((byte)0); w.Write((byte)0);
                w.Write((ushort)1); w.Write((ushort)32); w.Write(frames[i].Length); w.Write(offset); offset+=frames[i].Length;
            }
            foreach(byte[] frame in frames) w.Write(frame);
        }
        using(var preview=Draw(512)) preview.Save(Path.Combine(dir,"PadLauncher.png"),ImageFormat.Png);
    }
}
