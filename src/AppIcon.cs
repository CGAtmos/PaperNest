using System;
using System.IO;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace PaperNest {
    public static class AppIcon {
        public static void Write(string file) {
            int[] sizes={16,24,32,48,64,128,256};List<byte[]> images=new List<byte[]>();
            foreach(int size in sizes) {
                using(Bitmap b=new Bitmap(size,size,PixelFormat.Format32bppArgb))using(Graphics g=Graphics.FromImage(b)) {
                    g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(size/256f,size/256f);
                    using(GraphicsPath shadow=Paper(29,31,201,202))using(SolidBrush sb=new SolidBrush(Color.FromArgb(35,55,44,27)))g.FillPath(sb,shadow);
                    using(GraphicsPath paper=Paper(22,22,201,202))using(LinearGradientBrush brush=new LinearGradientBrush(new Point(0,20),new Point(240,230),Color.FromArgb(255,239,175),Color.FromArgb(246,211,117)))g.FillPath(brush,paper);
                    using(SolidBrush fold=new SolidBrush(Color.FromArgb(255,246,208)))g.FillPolygon(fold,new[]{new Point(174,224),new Point(223,175),new Point(179,179)});
                    using(Pen line=new Pen(Color.FromArgb(112,105,78),9)){line.StartCap=LineCap.Round;line.EndCap=LineCap.Round;g.DrawLine(line,66,90,171,90);g.DrawLine(line,66,125,161,125);g.DrawLine(line,66,160,123,160);}
                    using(SolidBrush tape=new SolidBrush(Color.FromArgb(200,222,223,185)))g.FillPolygon(tape,new[]{new Point(84,10),new Point(164,6),new Point(169,42),new Point(88,46)});
                    using(MemoryStream stream=new MemoryStream()){b.Save(stream,ImageFormat.Png);images.Add(stream.ToArray());}
                }
            }
            using(BinaryWriter w=new BinaryWriter(File.Create(file))) {
                w.Write((ushort)0);w.Write((ushort)1);w.Write((ushort)sizes.Length);int offset=6+sizes.Length*16;
                for(int i=0;i<sizes.Length;i++){w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)(sizes[i]==256?0:sizes[i]));w.Write((byte)0);w.Write((byte)0);w.Write((ushort)1);w.Write((ushort)32);w.Write(images[i].Length);w.Write(offset);offset+=images[i].Length;}
                foreach(byte[] png in images)w.Write(png);
            }
        }
        static GraphicsPath Paper(int x,int y,int w,int h){GraphicsPath p=new GraphicsPath();p.AddLines(new[]{new Point(x+12,y),new Point(x+w-12,y),new Point(x+w,y+12),new Point(x+w,y+h-49),new Point(x+w-49,y+h),new Point(x+12,y+h),new Point(x,y+h-12),new Point(x,y+12)});p.CloseFigure();return p;}
    }
}
