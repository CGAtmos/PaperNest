using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PaperNest {
    public static class SelfTest {
        static void Assert(bool condition,string name,List<string> results){if(!condition)throw new Exception("FAIL: "+name);results.Add(name);}
        static BitmapSource Sample() {
            DrawingVisual visual=new DrawingVisual();using(DrawingContext d=visual.RenderOpen()){d.DrawRectangle(Look.Brush("#91AD91"),null,new Rect(0,0,128,96));d.DrawEllipse(Look.Brush("#FFE4A0"),null,new Point(90,25),15,15);d.DrawGeometry(Look.Brush("#426B67"),null,Look.Shape("M0,96 L34,30 L75,96 Z M45,96 L90,42 L128,96 Z"));}
            RenderTargetBitmap image=new RenderTargetBitmap(128,96,96,96,PixelFormats.Pbgra32);image.Render(visual);image.Freeze();return image;
        }
        public static int Run(string root,string report) {
            List<string> results=new List<string>();NotesApp app=null;
            try {
                if(File.Exists(Path.Combine(root,"notes.json")))throw new Exception("Self-test requires a fresh isolated data directory");
                app=new NotesApp(root,true);app.Initialize(false);
                GlyphTypeface glyph;Assert(new Typeface(Look.Handwriting,FontStyles.Normal,FontWeights.Normal,FontStretches.Normal).TryGetGlyphTypeface(out glyph)&&glyph.FontUri.LocalPath.IndexOf("LXGW",StringComparison.OrdinalIgnoreCase)>=0,"bundled handwriting font is actually used",results);
                Assert(app.Store.Data.Notes.Count==3,"first-run creates three editable notes",results);
                NoteData data=new NoteData { Title="测试：中文与图片",Text="第一行\n第二行 ✓",Width=412,Height=456,Left=71,Top=93,Color="sage",Zoom=1.5 };
                app.Store.Data.Notes.Add(data);NoteWindow window=new NoteWindow(app,data);app.Notes.Add(data.Id,window);
                window.InsertImageSource(Sample());window.SaveContent();Assert(window.EmbeddedImageCount==1,"paste pipeline inserts an embedded full-resolution image",results);
                double untouchedAngle=app.Store.Data.Notes[0].Angle;
                MenuItem tiltMenu=window.Frame.ContextMenu.Items.OfType<MenuItem>().First(m=>m.Header is string&&((string)m.Header).StartsWith(L.T("纸片斜度 · ")));
                Slider tiltSlider=((StackPanel)((MenuItem)tiltMenu.Items[0]).Header).Children.OfType<Slider>().Single();tiltSlider.Value=-2.5;
                Assert(data.Angle==-2.5&&((RotateTransform)window.Frame.RenderTransform).Angle==-2.5,"tilt slider rotates only its own editable paper",results);
                window.SetAngle(100);Assert(data.Angle==5,"manual rotation stays within the supported paper bounds",results);window.SetAngle(-2.5);
                Assert(app.SaveAll(),"atomic save succeeds",results);
                string collisionRoot=Path.Combine(root,"equal-timestamps");Store collision=new Store(collisionRoot);
                DateTime stamp=new DateTime(2026,1,2,3,4,5,DateTimeKind.Utc);
                NoteData savedNote=new NoteData{Title="Saved title",Text="Saved body",Updated=stamp,ContentUpdated=stamp};collision.Data.Notes.Add(savedNote);collision.Save();
                NoteData latestDraft=Store.Clone(savedNote);latestDraft.Title="Latest draft";latestDraft.Text="Latest unsaved body";collision.SaveDraft(latestDraft);
                Store collisionReload=new Store(collisionRoot);
                Assert(collisionReload.Data.Notes.Single().Title==latestDraft.Title&&collisionReload.Data.Notes.Single().Text==latestDraft.Text,"a newer draft survives equal serialized timestamps after an abrupt exit",results);
                collisionReload.Save();Assert(new Store(collisionRoot).LoadWarning=="","identical committed drafts do not report a false recovery on every launch",results);
                DateTime future=DateTime.UtcNow.AddSeconds(2);DateTime firstStamp=Store.NextUpdate(future),secondStamp=Store.NextUpdate(firstStamp);
                Assert(firstStamp>future&&secondStamp>firstStamp,"edit stamps remain ordered when the system clock repeats or moves backward",results);
                Assert(Store.Clone(new NoteData{Updated=secondStamp}).Updated>Store.Clone(new NoteData{Updated=firstStamp}).Updated,"rapid edit ordering survives JSON millisecond serialization",results);
                string priorSnapshot=File.ReadAllText(app.Store.StatePath);
                using(FileStream reader=Store.OpenSnapshot(app.Store.StatePath))
                using(FileStream draftReader=Store.OpenSnapshot(Path.Combine(app.Store.DraftRoot,data.Id+".json"))){
                    app.Store.Data.DefaultZoom=1.2;
                    Assert(app.SaveAll(),"saving succeeds while another reader holds the note index and a draft",results);
                    using(StreamReader oldReader=new StreamReader(reader))Assert(oldReader.ReadToEnd()==priorSnapshot,"an in-flight reader sees a complete old snapshot during atomic replacement",results);
                    Assert(new Store(root).Data.DefaultZoom==1.2,"a new reader sees the complete updated snapshot",results);
                }
                Store reload=new Store(root);NoteData restored=reload.Data.Notes.Single(n=>n.Id==data.Id);
                Assert(restored.Title==data.Title&&restored.Text.Contains("第一行"),"Unicode title and multiline text survive restart",results);
                NoteWindow reopened=new NoteWindow(app,restored);Assert(reopened.EmbeddedImageCount==1,"embedded image survives document reload",results);
                Assert(restored.Angle==-2.5&&((RotateTransform)reopened.Frame.RenderTransform).Angle==-2.5,"manual paper angle survives document and state reload",results);
                MenuItem randomMenu=tiltMenu.Items.OfType<MenuItem>().First(m=>Object.Equals(m.Header,L.T("随机倾斜一次")));randomMenu.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
                Assert(Math.Abs(data.Angle+2.5)>=.75&&Math.Abs(data.Angle)>=1&&Math.Abs(data.Angle)<=3.5&&app.Store.Data.Notes[0].Angle==untouchedAngle,"random tilt visibly changes one note without changing other notes",results);
                app.SaveAll();Assert(new Store(root).Data.Notes.Single(n=>n.Id==data.Id).Angle==data.Angle,"random angle is saved instead of changing again at restart",results);
                Assert(restored.Width==412&&restored.Height==456&&restored.Left==71&&restored.Top==93&&restored.Zoom==1.5,"size position color and zoom persist",results);
                window.HideNote();Assert(data.Hidden,"close-to-tray preserves note content",results);data.Hidden=false;
                NoteData todo=app.Store.Data.Notes.First(n=>n.Kind=="checklist");app.Notes[todo.Id].SetChecklistItemCompleted(0,true);app.Notes[todo.Id].SetChecklistItemCompleted(2,true);app.SaveAll();Assert(new Store(root).Data.Notes.Single(n=>n.Id==todo.Id).Checks=="101","checklist completion survives restart",results);
                string original=Path.Combine(root,"test-original.png");PngBitmapEncoder encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(Sample()));using(FileStream stream=File.Create(original))encoder.Save(stream);
                string imported=app.Store.ImportImage(original);Assert(File.ReadAllBytes(original).SequenceEqual(File.ReadAllBytes(app.Store.ImagePath(imported))),"photo import preserves original image bytes",results);
                Assert(app.Store.ImagePath("..\\outside.png")=="","image paths cannot escape the data directory",results);
                app.SaveAll();app.Store.Save();string expected=File.ReadAllText(app.Store.StatePath);File.WriteAllText(app.Store.StatePath,"{broken");Store recovered=new Store(root);
                Assert(recovered.LoadWarning.Length>0&&recovered.Data.Notes.Count==4,"corrupt latest state recovers previous atomic backup",results);Store.AtomicWrite(app.Store.StatePath,Encoding.UTF8.GetBytes(expected),false);
                Grid probe=new Grid { Width=412,Height=456,Background=Look.Brush("#7D96AA") };UIElement visual=(UIElement)reopened.Content;reopened.Content=null;probe.Children.Add(visual);probe.Measure(new Size(412,456));probe.Arrange(new Rect(0,0,412,456));probe.UpdateLayout();
                Look.WritePng(probe,Path.Combine(root,"scale-100.png"),412,456,1);Look.WritePng(probe,Path.Combine(root,"scale-300.png"),412,456,3);
                Assert(Look.LoadImage(Path.Combine(root,"scale-300.png")).PixelWidth==1236,"vector interface renders natively at 300 percent",results);
                SettingsWindow settings=new SettingsWindow(app);settings.Measure(new Size(564,705));Assert(settings.Content!=null,"settings surface constructs without errors",results);settings.Close();
                ManagerWindow manager=new ManagerWindow(app);manager.Refresh();Assert(manager.Content!=null,"note library and recoverable trash construct",results);manager.Close();
                app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();reopened.Close();
                WriteReport(report,true,results,null);return 0;
            }catch(Exception ex){WriteReport(report,false,results,ex.ToString());return 1;}
        }
        static void WriteReport(string report,bool passed,List<string> results,string error) {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(report)));
            Func<string,string> quote=s=>"\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";
            File.WriteAllText(report,"{\"passed\":"+(passed?"true":"false")+",\"checks\":["+String.Join(",",results.Select(quote))+"],\"error\":"+(error==null?"null":quote(error))+"}",new UTF8Encoding(false));
        }
        public static int Render(string path,string root) {
            NotesApp app=new NotesApp(root,true);app.Initialize(false);
            Canvas stage=new Canvas { Width=1540,Height=990,Background=Look.Gradient((Color)ColorConverter.ConvertFromString("#96ADC0"),(Color)ColorConverter.ConvertFromString("#627E98"),95) };
            TextBlock name=Look.Label("纸间",42,Look.Brush("#F7F3E8"));name.FontFamily=Look.Handwriting;Canvas.SetLeft(name,66);Canvas.SetTop(name,77);stage.Children.Add(name);
            TextBlock tagline=Look.Label("把小事，轻轻贴在桌面。",19,Look.Brush("#E9EEE9"));Canvas.SetLeft(tagline,69);Canvas.SetTop(tagline,146);stage.Children.Add(tagline);
            int i=0;foreach(NoteWindow window in app.Notes.Values.Take(3)) {
                Grid rootGrid=(Grid)window.Content;window.Content=null;rootGrid.Width=window.Width;rootGrid.Height=window.Height;
                Canvas.SetLeft(rootGrid,i==0?640:i==1?1061:1000);Canvas.SetTop(rootGrid,i==0?65:i==1?92:557);stage.Children.Add(rootGrid);i++;
            }
            stage.Measure(new Size(1540,990));stage.Arrange(new Rect(0,0,1540,990));stage.UpdateLayout();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));Look.WritePng(stage,path,1540,990,1.5);
            app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();return 0;
        }
    }
}
