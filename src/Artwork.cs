using System;
using System.IO;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace PaperNest {
    public static class Look {
        public static readonly Brush Ink = Brush("#3C3934");
        public static readonly Brush Muted = Brush("#8B887D");
        public static readonly Brush Accent = Brush("#657B65");
        static FontFamily handwriting;
        public static FontFamily Handwriting {
            get {
                if (handwriting == null) {
                    string folder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Fonts");
                    try { if(Directory.Exists(folder))handwriting=new FontFamily(new Uri(AppDomain.CurrentDomain.BaseDirectory,UriKind.Absolute),"./Fonts/#LXGW WenKai Lite"); } catch { }
                    if (handwriting == null) handwriting = new FontFamily("KaiTi, Microsoft YaHei UI");
                }
                return handwriting;
            }
        }
        public static SolidColorBrush Brush(string hex) { SolidColorBrush b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }
        public static Color PaperColor(string name) {
            if (name == "sage") return (Color)ColorConverter.ConvertFromString("#E6EDC9");
            if (name == "rose") return (Color)ColorConverter.ConvertFromString("#F5D6CB");
            if (name == "sky") return (Color)ColorConverter.ConvertFromString("#D5E5EF");
            if (name == "lilac") return (Color)ColorConverter.ConvertFromString("#E9DDF0");
            if (name == "cream") return (Color)ColorConverter.ConvertFromString("#F5ECDC");
            return (Color)ColorConverter.ConvertFromString("#F9E7A5");
        }
        public static Color Shade(Color c, double n) { return Color.FromRgb((byte)Math.Max(0,Math.Min(255,c.R*n)),(byte)Math.Max(0,Math.Min(255,c.G*n)),(byte)Math.Max(0,Math.Min(255,c.B*n))); }
        public static LinearGradientBrush Gradient(Color a, Color b, double angle) { LinearGradientBrush g = new LinearGradientBrush(a,b,angle); g.Freeze(); return g; }
        public static Geometry Shape(string path) { Geometry g = Geometry.Parse(path); g.Freeze(); return g; }
        public static string F(double n) { return n.ToString("0.###",System.Globalization.CultureInfo.InvariantCulture); }
        public static Button IconButton(string icon, string hint, Action click) {
            Button b = new Button { Width=29, Height=29, ToolTip=hint, Background=Brushes.Transparent, BorderThickness=new Thickness(0), Foreground=Ink, Cursor=System.Windows.Input.Cursors.Hand, Padding=new Thickness(6), Focusable=false };
            Grid grid = new Grid();
            string path = icon == "lock" ? "M4,7 L4,4 C4,0 12,0 12,4 L12,7 M2,7 L14,7 L14,16 L2,16 Z M8,10 L8,13" : icon == "unlock" ? "M4,7 L4,4 C4,0 12,0 12,4 M2,7 L14,7 L14,16 L2,16 Z M8,10 L8,13" : icon == "plus" ? "M8,1 L8,15 M1,8 L15,8" : icon == "close" ? "M2,2 L14,14 M14,2 L2,14" : icon == "more" ? "M8,2 L8,2.2 M8,8 L8,8.2 M8,14 L8,14.2" : icon == "photo" ? "M1,2 L15,2 L15,14 L1,14 Z M2,12 L6,7 L10,11 L12,8 L15,12 M11,5 L11.1,5.1" : icon == "pin" ? "M5,1 L12,1 L11,5 L14,9 L9,9 L8,16 L7,9 L2,9 L6,5 Z" : "M2,2 L14,14 M7,2 L14,9";
            System.Windows.Shapes.Path p = new System.Windows.Shapes.Path { Data=Shape(path), Stroke=Ink, StrokeThickness=1.35, StrokeStartLineCap=PenLineCap.Round, StrokeEndLineCap=PenLineCap.Round, Stretch=Stretch.Uniform };
            grid.Children.Add(p); b.Content=grid;
            ControlTemplate template = new ControlTemplate(typeof(Button));
            FrameworkElementFactory border = new FrameworkElementFactory(typeof(Border));
            border.Name="surface"; border.SetValue(Border.CornerRadiusProperty,new CornerRadius(7)); border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty));
            FrameworkElementFactory cp=new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(FrameworkElement.MarginProperty,new Thickness(7)); border.AppendChild(cp); template.VisualTree=border;
            Trigger hover=new Trigger { Property=UIElement.IsMouseOverProperty,Value=true }; hover.Setters.Add(new Setter(Border.BackgroundProperty,Brush("#24FFFFFF"),"surface")); template.Triggers.Add(hover);
            Trigger down=new Trigger { Property=Button.IsPressedProperty,Value=true }; down.Setters.Add(new Setter(Border.BackgroundProperty,Brush("#28000000"),"surface")); template.Triggers.Add(down);
            b.Template=template; b.Click+=delegate { click(); }; return b;
        }
        public static TextBlock Label(string text, double size, Brush color) { return new TextBlock { Text=text, FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=size, Foreground=color, TextWrapping=TextWrapping.Wrap }; }
        public static Button ActionButton(string text, Action click, bool primary) {
            Button button=new Button { Content=text, FontSize=14, Padding=new Thickness(19,10,19,10), Margin=new Thickness(0,0,8,0), Cursor=System.Windows.Input.Cursors.Hand, Background=primary?Accent:Brush("#F0EDE5"),Foreground=primary?Brushes.White:Ink,BorderThickness=new Thickness(0),MinHeight=39 };
            FrameworkElementFactory border=new FrameworkElementFactory(typeof(Border)); border.SetValue(Border.CornerRadiusProperty,new CornerRadius(8)); border.SetValue(Border.BackgroundProperty,new TemplateBindingExtension(Control.BackgroundProperty)); border.SetValue(Border.PaddingProperty,new TemplateBindingExtension(Control.PaddingProperty));
            FrameworkElementFactory cp=new FrameworkElementFactory(typeof(ContentPresenter)); cp.SetValue(FrameworkElement.HorizontalAlignmentProperty,HorizontalAlignment.Center);cp.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);border.AppendChild(cp);
            button.Template=new ControlTemplate(typeof(Button)) { VisualTree=border };button.Click+=delegate { click(); };return button;
        }
        public static BitmapSource LoadImage(string file) {
            using (FileStream stream=File.OpenRead(file)) { BitmapImage image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.StreamSource=stream;image.EndInit();image.Freeze();return image; }
        }
        public static void WritePng(Visual visual,string file,double width,double height,double scale) {
            RenderTargetBitmap bitmap=new RenderTargetBitmap((int)Math.Ceiling(width*scale),(int)Math.Ceiling(height*scale),96*scale,96*scale,PixelFormats.Pbgra32);bitmap.Render(visual);
            PngBitmapEncoder encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(FileStream stream=File.Create(file)) encoder.Save(stream);
        }
    }
    public sealed class PaperSurface : FrameworkElement {
        public string ColorName="butter";
        public string Decoration="tape";
        public bool Texture=true;
        static DrawingBrush grain;
        static DrawingBrush Grain {
            get {
                if(grain!=null)return grain;
                DrawingGroup g=new DrawingGroup();Random random=new Random(1549);
                using(DrawingContext d=g.Open()) {
                    d.DrawRectangle(Brushes.Transparent,null,new Rect(0,0,160,160));
                    for(int i=0;i<400;i++) {
                        double x=random.NextDouble()*160,y=random.NextDouble()*160;
                        Pen pen=new Pen(i%3==0?Look.Brush("#14FFFFFF"):Look.Brush("#0C7B704E"),0.3+random.NextDouble()*0.5);
                        d.DrawLine(pen,new Point(x,y),new Point(x+1+random.NextDouble()*4,y-1+random.NextDouble()*2));
                    }
                }
                grain=new DrawingBrush(g) { ViewportUnits=BrushMappingMode.Absolute,Viewport=new Rect(0,0,160,160),TileMode=TileMode.Tile,Stretch=Stretch.None };grain.Freeze();return grain;
            }
        }
        protected override void OnRender(DrawingContext d) {
            double w=ActualWidth,h=ActualHeight;if(w<40||h<80)return;
            Color c=Look.PaperColor(ColorName); double curl=Math.Min(43,Math.Min(w,h)*0.13);
            Geometry body=Look.Shape("M1,1 L"+Look.F(w-2)+",0 Q"+Look.F(w)+",1 "+Look.F(w-1)+",4 L"+Look.F(w-1)+","+Look.F(h-curl)+" Q"+Look.F(w-2)+","+Look.F(h-3)+" "+Look.F(w-curl)+","+Look.F(h-1)+" L2,"+Look.F(h)+" Z");
            d.DrawGeometry(Look.Gradient(Look.Shade(c,1.02),Look.Shade(c,0.97),82),new Pen(new SolidColorBrush(Look.Shade(c,0.96)),0.5),body);
            d.PushClip(body);
            d.DrawRectangle(Look.Gradient(Color.FromArgb(20,255,255,255),Color.FromArgb(0,255,255,255),90),null,new Rect(0,0,w,38));
            if(Texture)d.DrawRectangle(Grain,null,new Rect(0,0,w,h));
            d.Pop();
            Geometry shade=Look.Shape("M"+Look.F(w-curl-5)+","+Look.F(h-1)+" Q"+Look.F(w-curl+12)+","+Look.F(h-curl+7)+" "+Look.F(w-1)+","+Look.F(h-curl-2)+" Q"+Look.F(w-3)+","+Look.F(h-4)+" "+Look.F(w-curl)+","+Look.F(h-1)+" Z");
            d.DrawGeometry(Look.Gradient(Color.FromArgb(0,86,72,27),Color.FromArgb(60,86,72,27),45),null,shade);
            Geometry fold=Look.Shape("M"+Look.F(w-curl-2)+","+Look.F(h-1)+" C"+Look.F(w-curl+12)+","+Look.F(h-11)+" "+Look.F(w-curl+4)+","+Look.F(h-curl+8)+" "+Look.F(w-1)+","+Look.F(h-curl-2)+" C"+Look.F(w-3)+","+Look.F(h-curl+15)+" "+Look.F(w-curl+16)+","+Look.F(h-2)+" "+Look.F(w-curl-2)+","+Look.F(h-1)+" Z");
            LinearGradientBrush foldBrush=new LinearGradientBrush { StartPoint=new Point(0.1,0),EndPoint=new Point(1,0.9) };
            foldBrush.GradientStops.Add(new GradientStop(Look.Shade(c,1.09),0));foldBrush.GradientStops.Add(new GradientStop(Look.Shade(c,1.15),0.45));foldBrush.GradientStops.Add(new GradientStop(Look.Shade(c,0.82),1));
            d.DrawGeometry(foldBrush,new Pen(new SolidColorBrush(Color.FromArgb(40,255,255,255)),0.6),fold);
        }
    }
    public sealed class DecorationSurface : FrameworkElement {
        public string Kind="tape";
        protected override void OnRender(DrawingContext d) {
            double w=ActualWidth;if(w<80)return;
            if(Kind=="none")return;
            if(Kind=="clip") {
                d.PushTransform(new RotateTransform(10,w*0.22,29));
                Geometry clip=Look.Shape("M"+Look.F(w*.22+13)+",12 C"+Look.F(w*.22+13)+",-7 "+Look.F(w*.22-8)+",-8 "+Look.F(w*.22-8)+",11 L"+Look.F(w*.22-8)+",61 C"+Look.F(w*.22-8)+",75 "+Look.F(w*.22+8)+",74 "+Look.F(w*.22+8)+",61 L"+Look.F(w*.22+8)+",16 C"+Look.F(w*.22+8)+",7 "+Look.F(w*.22-2)+",7 "+Look.F(w*.22-2)+",17 L"+Look.F(w*.22-2)+",56");
                d.DrawGeometry(null,new Pen(Look.Brush("#407C5039"),5),clip);d.DrawGeometry(null,new Pen(Look.Brush("#C39278"),2.6),clip);d.DrawGeometry(null,new Pen(Look.Brush("#F4D8C2"),0.85),clip);d.Pop();return;
            }
            d.PushTransform(new RotateTransform(-6,w*.48,27));
            double left=w*.48-47;
            Geometry tape=Look.Shape("M"+Look.F(left)+",10 L"+Look.F(left+92)+",7 L"+Look.F(left+94)+",37 L"+Look.F(left+2)+",40 L"+Look.F(left+3)+",33 L"+Look.F(left)+",29 Z");
            d.DrawGeometry(Look.Gradient(Color.FromArgb(128,244,231,195),Color.FromArgb(143,227,211,171),90),new Pen(Look.Brush("#35FFF9E9"),0.6),tape);
            Random r=new Random(17);for(int i=0;i<90;i++){double x=left+r.NextDouble()*92,y=11+r.NextDouble()*24;d.DrawLine(new Pen(Look.Brush("#25FFFFFF"),0.6),new Point(x,y),new Point(x+2,y+1));}d.Pop();
        }
    }
    public sealed class HeadingDoodle : FrameworkElement {
        public string ColorName="butter";
        protected override void OnRender(DrawingContext d) {
            double w=Math.Min(240,Math.Max(40,ActualWidth-28));
            Brush ink=Look.Brush(ColorName=="sage"?"#A0829B70":ColorName=="sky"?"#A071A2B8":"#A0C99A62");
            Geometry line=Look.Shape("M2,7 C"+Look.F(w*.26)+",5 "+Look.F(w*.55)+",4 "+Look.F(w)+",5");
            d.DrawGeometry(null,new Pen(ink,2.4){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},line);
            if(ActualWidth>w+26) {
                d.PushTransform(new TranslateTransform(w+13,-3));d.DrawGeometry(null,new Pen(ink,1.7),Look.Shape("M6,5 C1,-2 -4,3 1,9 L6,14 L11,8 C16,0 9,-1 6,5"));d.Pop();
            }
        }
    }
}
