using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using PaperNest;

class RenderGallery {
    static void Label(Canvas canvas,string text,double x,double y,double size,string color,double width=0){TextBlock label=Look.Label(text,size,Look.Brush(color));if(width>0)label.Width=width;Canvas.SetLeft(label,x);Canvas.SetTop(label,y);canvas.Children.Add(label);}
    static void Render(Window window,string file,double width,double height){FrameworkElement content=(FrameworkElement)window.Content;Panel panel=content as Panel;if(panel!=null)panel.Background=window.Background;content.Width=width;content.Height=height;content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();Look.WritePng(content,file,width,height,1.5);}
    [STAThread] static int Main(string[] args){
        string output=Path.GetFullPath(args[0]),data=Path.GetFullPath(args[1]);
        if(Directory.Exists(data))throw new Exception("Use a fresh demo data directory");
        Directory.CreateDirectory(output);L.Set("en");NotesApp app=new NotesApp(data,true);app.Store.Data.Language="en";
        app.Store.Data.Notes=new List<NoteData>{
            new NoteData{Kind="checklist",Title="My little plans",Text="Make something lovely\nTake a little break\nSave a bright idea",Items=new List<string>{"Make something lovely","Take a little break","Save a bright idea"},Checks="100",Width=480,Height=540,Color="butter",Decoration="tape",Angle=-2.2},
            new NoteData{Kind="photo",Title="A little sunshine",Text="Small moments, kept close.",Width=420,Height=490,Color="rose",Decoration="clip",Angle=2.7,ImageFile=app.Store.ImportImage(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Sunset.png"))},
            new NoteData{Kind="text",Title="A thought to keep",Text="Leave a little room\nfor a new idea.",Width=435,Height=325,Color="sage",Decoration="none",Angle=-1,Locked=true}
        };
        app.Initialize(false);app.SaveAll();
        SettingsWindow settings=new SettingsWindow(app);Render(settings,Path.Combine(output,"settings.png"),564,665);settings.Close();
        AboutWindow about=new AboutWindow(app);Render(about,Path.Combine(output,"about.png"),600,650);about.Close();
        ManagerWindow library=new ManagerWindow(app);Render(library,Path.Combine(output,"library.png"),650,630);library.Close();
        Canvas stage=new Canvas{Width=1600,Height=1000,Background=Look.Gradient((Color)ColorConverter.ConvertFromString("#EDF1EB"),(Color)ColorConverter.ConvertFromString("#C9D8D5"),120)};
        Label(stage,"PAPERNEST  /  WINDOWS",90,110,14,"#536D67");
        Label(stage,"A little paper.\nA little peace.",86,215,58,"#29483F",450);
        Label(stage,"Your words, plans and favorite\nmoments. Right on your desktop.",92,402,20,"#536D67",400);
        Label(stage,"Write. Pin. Keep.",92,540,23,"#29483F");
        Label(stage,"Text · Checklists · Photos\nAuto-save · Per-note locks\nEnglish / 简体中文",92,602,17,"#536D67",390);
        Label(stage,"FREE & OPEN SOURCE",92,825,13,"#536D67");
        Label(stage,"Created by Kaysar",92,860,18,"#29483F");
        double[] xs={520,1030,1010},ys={125,105,615};int i=0;
        foreach(NoteWindow note in app.Notes.Values){FrameworkElement paper=(FrameworkElement)note.Content;note.Content=null;paper.Width=note.Width;paper.Height=note.Height;Canvas.SetLeft(paper,xs[i]);Canvas.SetTop(paper,ys[i]);stage.Children.Add(paper);i++;}
        stage.Measure(new Size(1600,1000));stage.Arrange(new Rect(0,0,1600,1000));stage.UpdateLayout();Look.WritePng(stage,Path.Combine(output,"papernest-hero.png"),1600,1000,1.25);
        app.Exiting=true;foreach(NoteWindow note in app.Notes.Values)note.Close();return 0;
    }
}
