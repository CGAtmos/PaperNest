using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace PaperNest {
    public static class TrayRegression {
        static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
        static void Check(bool value,string message,List<string> checks){if(!value)throw new Exception(message);checks.Add(message);}
        static void Pump(){DispatcherFrame frame=new DispatcherFrame();Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(delegate{frame.Continue=false;}));Dispatcher.PushFrame(frame);}
        static Forms.NotifyIcon Icon(NotesApp app){return (Forms.NotifyIcon)typeof(NotesApp).GetField("tray",Private).GetValue(app);}
        static void Mouse(NotesApp app,string name,Forms.MouseButtons button,int clicks){typeof(Forms.NotifyIcon).GetMethod(name,Private).Invoke(Icon(app),new object[]{new Forms.MouseEventArgs(button,clicks,0,0,0)});}
        static ManagerWindow Library(NotesApp app){return app.Windows.OfType<ManagerWindow>().SingleOrDefault();}
        static string Contents(NotesApp app){return String.Join("|",app.Store.Data.Notes.Select(n=>n.Id+":"+HistoryCatalog.Key(n)+":"+n.Locked+":"+n.Deleted+":"+n.Left+":"+n.Top+":"+n.Width+":"+n.Height));}
        public static int Run(string root,string report){var checks=new List<string>();NotesApp app=null;Exception error=null;
            try {
                if(File.Exists(Path.Combine(root,"notes.json")))throw new Exception("Use a fresh isolated data directory");
                app=new NotesApp(root,true);app.Initialize(false);
                NoteWindow visible=app.Notes.Values.First();visible.SetLocked(true);app.ShowNote(visible.Data);
                NoteData hidden=app.Store.Data.Notes[1],deleted=app.Store.Data.Notes[2];hidden.Hidden=true;deleted.Deleted=true;deleted.Hidden=true;app.SaveAll();
                int count=app.Store.Data.Notes.Count;string contents=Contents(app);string contentKeys=String.Join("|",app.Store.Data.Notes.Select(HistoryCatalog.Key));
                typeof(NotesApp).GetMethod("InitializeTray",Private).Invoke(app,null);
                for(int i=0;i<20;i++)Mouse(app,"OnMouseDoubleClick",Forms.MouseButtons.Left,2);Pump();
                ManagerWindow first=Library(app);
                Check(first!=null&&app.Windows.OfType<ManagerWindow>().Count()==1,"twenty queued tray double-clicks open exactly one library",checks);
                Check(app.Store.Data.Notes.Count==count&&app.Notes.Count==count&&hidden.Hidden&&deleted.Deleted&&app.Notes.Values.Count(n=>n.IsVisible)==1,"double-click leaves note count and hidden/deleted visibility unchanged",checks);
                for(int i=0;i<10;i++){Mouse(app,"OnMouseDoubleClick",Forms.MouseButtons.Left,2);Pump();}
                Check(Object.ReferenceEquals(first,Library(app))&&Contents(app)==contents,"repeated double-clicks reuse the library and preserve contents, locks and layout",checks);
                first.WindowState=WindowState.Minimized;Mouse(app,"OnMouseDoubleClick",Forms.MouseButtons.Left,2);Pump();
                Check(first.WindowState==WindowState.Normal,"double-click restores a minimized library",checks);
                first.Close();Mouse(app,"OnMouseClick",Forms.MouseButtons.Left,1);Pump();
                Check(Library(app)==null&&app.Store.Data.Notes.Count==count,"single-click does not create a note or open another window",checks);
                Mouse(app,"OnMouseDoubleClick",Forms.MouseButtons.Right,2);Pump();
                Check(Library(app)==null&&hidden.Hidden,"right-button double-click does not open the library or reveal hidden notes",checks);
                Mouse(app,"OnMouseDoubleClick",Forms.MouseButtons.Left,2);Pump();
                Check(Library(app)!=null&&!Object.ReferenceEquals(first,Library(app)),"closing and reopening the library creates one fresh library only",checks);
                app.ChangeLanguage("en");typeof(NotesApp).GetMethod("InitializeTray",Private).Invoke(app,null);Mouse(app,"OnMouseDoubleClick",Forms.MouseButtons.Left,2);Pump();
                Check(Library(app).Title=="PaperNest · Note Library"&&Icon(app).Text.Contains("Double-click")&&app.Store.Data.Notes.Count==count,"English tray recreation preserves the safe double-click behavior",checks);
                app.HideAll();Library(app).Close();app.OpenWorkspace();Pump();
                Check(Library(app)!=null&&app.Notes.Values.All(n=>!n.IsVisible)&&app.Store.Data.Notes.Where(n=>!n.Deleted).All(n=>n.Hidden),"reopening a fully hidden workspace opens the library without unfolding notes",checks);
                app.ShowAll();app.ShowAll();Pump();
                Check(app.Notes.Values.Count(n=>n.IsVisible)==2&&!hidden.Hidden&&deleted.Deleted&&!app.Notes[deleted.Id].IsVisible&&app.Store.Data.Notes.Count==count,"explicit Show all reveals existing notes only and never restores trash or duplicates notes",checks);
                app.HideAll();app.SaveAll();Store reload=new Store(root);
                Check(reload.Data.Notes.Count==count&&reload.Data.Notes.All(n=>n.Hidden)&&reload.Data.Notes.First().Locked&&String.Join("|",reload.Data.Notes.Select(HistoryCatalog.Key))==contentKeys,"saved hidden state, content and locks survive a reload",checks);
                Library(app).Close();app.Exiting=true;Mouse(app,"OnMouseDoubleClick",Forms.MouseButtons.Left,2);Pump();
                Check(Library(app)==null,"tray double-click during shutdown cannot reopen a window",checks);
            }catch(Exception ex){error=ex;}
            finally {if(app!=null){app.Exiting=true;if(Icon(app)!=null){Icon(app).Visible=false;Icon(app).Dispose();}foreach(Window window in app.Windows.Cast<Window>().ToArray())window.Close();}}
            Func<string,string> quote=s=>"\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";
            Directory.CreateDirectory(Path.GetDirectoryName(report));File.WriteAllText(report,"{\"passed\":"+(error==null?"true":"false")+",\"checks\":["+String.Join(",",checks.Select(quote))+"],\"error\":"+(error==null?"null":quote(error.ToString()))+"}");return error==null?0:1;
        }
    }
}
