using System;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;

[assembly: AssemblyTitle("纸间 · 桌面便签")]
[assembly: AssemblyDescription("有纸张温度的 Windows 桌面便签")]
[assembly: AssemblyCompany("Kaysar")]
[assembly: AssemblyProduct("PaperNest Desktop Notes")]
[assembly: AssemblyVersion("1.7.1.0")]
[assembly: AssemblyFileVersion("1.7.1.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

namespace PaperNest {
    public static class Startup {
        public const string ValueName="PaperNest.DesktopNotes";
        public static string ExePath { get { return Assembly.GetExecutingAssembly().Location; } }
        public static bool IsEnabled() {
            using(RegistryKey key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                return key!=null && String.Equals(key.GetValue(ValueName) as string,"\""+ExePath+"\" --background",StringComparison.OrdinalIgnoreCase);
            }
        }
        public static void Set(bool enabled) {
            using(RegistryKey key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")) {
                if(enabled)key.SetValue(ValueName,"\""+ExePath+"\" --background");else key.DeleteValue(ValueName,false);
            }
        }
    }
    public sealed class NotesApp : Application {
        public static NotesApp CurrentApp;
        public Store Store;
        public readonly Dictionary<string,NoteWindow> Notes=new Dictionary<string,NoteWindow>();
        public bool Exiting;
        public bool TestMode;
        Forms.NotifyIcon tray;
        bool trayActivationPending;
        DispatcherTimer saveTimer;
        DispatcherTimer safetyTimer;
        bool savePending;
        public string LastSaveError="";
        DateTime lastSaveWarning=DateTime.MinValue;
        DispatcherTimer ipcTimer;
        Mutex mutex;
        FileStream writerLease;
        string pipeName;
        volatile bool stopPipe;
        SettingsWindow settings;
        ManagerWindow manager;
        HistoryWindow history;
        AboutWindow about;
        readonly Queue<string> commands=new Queue<string>();
        public NotesApp(string dataRoot,bool test) {
            CurrentApp=this;TestMode=test;ShutdownMode=ShutdownMode.OnExplicitShutdown;
            Resources.Add(typeof(System.Windows.Controls.Primitives.ScrollBar),QuietScrollBar());
            Store=new Store(dataRoot);L.Set(Store.Data.Language);Store.Data.Language=L.Code;
            DispatcherUnhandledException+=delegate(object sender,DispatcherUnhandledExceptionEventArgs e) {
                Store.Log(e.Exception);e.Handled=true;
                MessageBox.Show(L.T("这次操作没有完成，已有便签仍保留。\n")+e.Exception.Message,L.T("纸间"),MessageBoxButton.OK,MessageBoxImage.Information);
            };
            SessionEnding+=delegate(object sender,SessionEndingCancelEventArgs e){if(!SaveAll()){e.Cancel=true;return;}Exiting=true;stopPipe=true;if(tray!=null){tray.Visible=false;tray.Dispose();tray=null;}};
        }
        static Style QuietScrollBar() {
            return (Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ScrollBar'><Setter Property='Width' Value='7'/><Setter Property='Background' Value='Transparent'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ScrollBar'><Grid Background='Transparent'><Track x:Name='PART_Track' IsDirectionReversed='True' Orientation='{TemplateBinding Orientation}' Minimum='{TemplateBinding Minimum}' Maximum='{TemplateBinding Maximum}' Value='{TemplateBinding Value}' ViewportSize='{TemplateBinding ViewportSize}'><Track.DecreaseRepeatButton><RepeatButton Command='ScrollBar.PageUpCommand' Opacity='0' Focusable='False'/></Track.DecreaseRepeatButton><Track.Thumb><Thumb MinHeight='20'><Thumb.Template><ControlTemplate TargetType='Thumb'><Border CornerRadius='3' Margin='2,1,1,1' Background='#405D5847'/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb><Track.IncreaseRepeatButton><RepeatButton Command='ScrollBar.PageDownCommand' Opacity='0' Focusable='False'/></Track.IncreaseRepeatButton></Track></Grid></ControlTemplate></Setter.Value></Setter></Style>");
        }
        public void Initialize(bool background) {
            Store.Data.StartWithWindows=TestMode?Store.Data.StartWithWindows:PaperNest.Startup.IsEnabled();
            if(Store.Data.Notes.Count>0)Store.SnapshotNow();
            if(Store.Data.Notes.Count==0)CreateWelcomeNotes();
            if(!TestMode){InitializeTray();try{if(DesktopMenu.IsEnabled(PaperNest.Startup.ExePath))DesktopMenu.SetEnabled(true,PaperNest.Startup.ExePath);}catch(Exception ex){Store.Log(ex);}}
            foreach(NoteData note in Store.Data.Notes.Where(n=>!n.Deleted).ToArray()) {
                NoteWindow window=new NoteWindow(this,note);Notes.Add(note.Id,window);
                if(!TestMode && !note.Hidden && (!background || Store.Data.ShowOnLaunch))window.Show();
            }
            saveTimer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds(250) };saveTimer.Tick+=delegate { saveTimer.Stop();if(savePending)SaveAll(); };
            safetyTimer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromSeconds(5) };safetyTimer.Tick+=delegate { if(savePending)SaveAll(); };safetyTimer.Start();
            if(!String.IsNullOrEmpty(Store.LoadWarning))Dispatcher.BeginInvoke(new Action(delegate { MessageBox.Show(Store.LoadWarning,L.T("纸间")); }));
        }
        void InitializeTray() {
            if(tray!=null){tray.Visible=false;tray.Dispose();}
            tray=new Forms.NotifyIcon();
            string iconFile=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PaperNest.ico");
            tray.Icon=File.Exists(iconFile)?new System.Drawing.Icon(iconFile):System.Drawing.SystemIcons.Information;
            tray.Text=L.T("纸间 · 双击打开收纳盒，右键更多操作");tray.Visible=true;tray.MouseDoubleClick+=OnTrayMouseDoubleClick;
            Forms.ContextMenuStrip menu=new Forms.ContextMenuStrip();
            menu.Items.Add(L.T("新建文字便签"),null,delegate { Dispatcher.BeginInvoke(new Action(delegate { NewNote("text"); })); });
            menu.Items.Add(L.T("新建待办清单"),null,delegate { Dispatcher.BeginInvoke(new Action(delegate { NewNote("checklist"); })); });
            menu.Items.Add(L.T("新建照片便签"),null,delegate { Dispatcher.BeginInvoke(new Action(delegate { NewNote("photo"); })); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(L.T("显示全部便签"),null,delegate { Dispatcher.BeginInvoke(new Action(ShowAll)); });
            menu.Items.Add(L.T("收起全部便签"),null,delegate { Dispatcher.BeginInvoke(new Action(HideAll)); });
            menu.Items.Add(L.T("便签收纳盒"),null,delegate { Dispatcher.BeginInvoke(new Action(OpenManager)); });
            menu.Items.Add(L.T("找回便签"),null,delegate { Dispatcher.BeginInvoke(new Action(OpenHistory)); });
            menu.Items.Add(L.T("设置"),null,delegate { Dispatcher.BeginInvoke(new Action(OpenSettings)); });
            menu.Items.Add(L.T("关于纸间"),null,delegate { Dispatcher.BeginInvoke(new Action(OpenAbout)); });
            menu.Items.Add(new Forms.ToolStripSeparator());
            menu.Items.Add(L.T("彻底退出纸间"),null,delegate { Dispatcher.BeginInvoke(new Action(ExitApp)); });
            tray.ContextMenuStrip=menu;
        }
        void OnTrayMouseDoubleClick(object sender,Forms.MouseEventArgs e) {
            if(e.Button!=Forms.MouseButtons.Left||Exiting||trayActivationPending)return;
            trayActivationPending=true;
            Dispatcher.BeginInvoke(new Action(delegate {
                trayActivationPending=false;
                if(!Exiting)OpenManager();
            }));
        }
        public void ScheduleSave() { savePending=true;if(saveTimer!=null&&!saveTimer.IsEnabled)saveTimer.Start(); }
        public void NoteChanged(NoteWindow window,bool content) {
            window.CaptureDraft();window.Data.Updated=DateTime.UtcNow;if(content)window.Data.ContentUpdated=window.Data.Updated;
            window.SetSaveStatus(L.T("保存中…"),false);
            try{Store.SaveDraft(window.Data);}catch(Exception ex){SaveFailed(ex);}
            ScheduleSave();
        }
        void SaveFailed(Exception ex){LastSaveError=ex.Message;Store.Log(ex);foreach(NoteWindow window in Notes.Values)window.SetSaveStatus(L.T("保存未完成，请勿强制关闭"),true);if((DateTime.Now-lastSaveWarning).TotalSeconds>30){lastSaveWarning=DateTime.Now;Notify(L.T("保存未完成：")+ex.Message+L.T("。当前内容仍在窗口内，请勿强制结束程序。"));}}
        public bool SaveAll() {
            try {
                foreach(NoteWindow window in Notes.Values){window.SaveContent();Store.SaveDraft(window.Data);}
                Store.Save();savePending=false;LastSaveError="";foreach(NoteWindow window in Notes.Values)window.SetSaveStatus(L.T("已自动保存 ")+DateTime.Now.ToString("HH:mm:ss"),false);return true;
            }catch(Exception ex) { SaveFailed(ex);savePending=true;return false; }
        }
        public void Notify(string text) {
            if(tray!=null){tray.BalloonTipTitle=L.T("纸间");tray.BalloonTipText=text;tray.ShowBalloonTip(3500);}
        }
        public NoteWindow NewNote(string kind) {
            Rect area=SystemParameters.WorkArea;
            if(Array.IndexOf(DesktopMenu.Kinds,kind)<0)kind="text";
            NoteData note=new NoteData { Kind=kind,Color=kind=="photo"?"rose":Store.Data.DefaultColor,Decoration=kind=="photo"?"clip":"tape",Title=kind=="photo"?L.T("喜欢的瞬间"):kind=="checklist"?L.T("待办清单"):L.T("新的小纸条"),Zoom=Store.Data.DefaultZoom,Left=Math.Max(area.Left,area.Right-460-(Notes.Count%3)*24),Top=area.Top+65+(Notes.Count%4)*45,Angle=PaperTilt.RandomAngle(0) };
            Store.Data.Notes.Add(note);NoteWindow window=new NoteWindow(this,note);Notes.Add(note.Id,window);NoteChanged(window,false);if(!SaveAll())Notify(L.T("新便签尚未保存成功，请保持程序打开。"));if(!TestMode){window.Show();window.Activate();}return window;
        }
        void CreateWelcomeNotes() {
            Rect area=SystemParameters.WorkArea;double scale=Math.Max(.7,Math.Min(1,(area.Height-35)/870));double mainWidth=Math.Max(340,425*scale),photoWidth=Math.Max(310,365*scale);double x=Math.Max(area.Left+10,area.Right-mainWidth-photoWidth-55),top=area.Top+Math.Max(15,70*scale);
            NoteData main=new NoteData { Title=L.T("今天的小计划"),Kind="checklist",Text=L.T("完成设计稿\n整理参考图\n下班前备份"),Left=x,Top=top,Width=mainWidth,Height=Math.Max(350,464*scale),Zoom=Math.Max(.85,scale),Color="butter",Angle=-2,Decoration="tape" };
            NoteData photo=new NoteData { Title=L.T("把喜欢的瞬间贴起来"),Kind="photo",Text=L.T("把喜欢的瞬间贴起来 ♡"),Left=x+mainWidth+10,Top=top+20*scale,Width=photoWidth,Height=Math.Max(350,432*scale),Zoom=Math.Max(.85,scale),Color="rose",Angle=2.5,Decoration="clip" };
            string sample=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Assets","Sunset.png");
            if(File.Exists(sample))photo.ImageFile=Store.ImportImage(sample);
            double ideaHeight=Math.Max(240,296*scale);NoteData idea=new NoteData { Title=L.T("突然想到"),Text=L.T("好点子，先记下来。"),Left=photo.Left-20,Top=Math.Min(area.Bottom-ideaHeight-12,photo.Top+photo.Height+15),Width=Math.Max(310,360*scale),Height=ideaHeight,Zoom=Math.Max(.85,scale),Color="sage",Decoration="none",Angle=1.2 };
            Store.Data.Notes.Add(main);Store.Data.Notes.Add(photo);Store.Data.Notes.Add(idea);Store.Save();
        }
        public void ShowNote(NoteData note) {
            if(note.Deleted)return;
            note.Hidden=false;NoteWindow window;
            if(!Notes.TryGetValue(note.Id,out window)){window=new NoteWindow(this,note);Notes.Add(note.Id,window);}
            window.Show();window.Activate();ScheduleSave();
        }
        public void ShowAll() { foreach(NoteData note in Store.Data.Notes.Where(n=>!n.Deleted).ToArray())ShowNote(note); }
        public void OpenWorkspace(){NoteData[] visible=Store.Data.Notes.Where(n=>!n.Deleted&&!n.Hidden).ToArray();if(visible.Length==0)OpenManager();else foreach(NoteData note in visible)ShowNote(note);}
        public void HideAll() { foreach(NoteWindow window in Notes.Values)window.Data.Hidden=true;if(SaveAll())foreach(NoteWindow window in Notes.Values)window.Hide(); }
        public void ChangeLanguage(string code) {
            Store.Data.Language=code;L.Set(code);
            foreach(NoteWindow note in Notes.Values)note.RefreshLanguage();
            if(!TestMode)InitializeTray();
            if(manager!=null){manager.Close();OpenManager();}
            if(history!=null){history.Close();OpenHistory();}
            if(about!=null){about.Close();OpenAbout();}
            foreach(ImageViewer viewer in Windows.OfType<ImageViewer>().ToArray())viewer.RefreshLanguage();
        }
        public void OpenAbout(){if(about==null){about=new AboutWindow(this);about.Closed+=delegate{about=null;};about.Show();}else about.Activate();}
        public void OpenSettings() {
            if(settings==null){settings=new SettingsWindow(this);settings.Closed+=delegate{settings=null;};settings.Show();}else{settings.Activate();}
        }
        public void OpenManager() {
            if(manager==null){manager=new ManagerWindow(this);manager.Closed+=delegate{manager=null;};manager.Show();}else{manager.Refresh();if(manager.WindowState==WindowState.Minimized)manager.WindowState=WindowState.Normal;manager.Activate();}
        }
        public void OpenHistory(){if(history==null){history=new HistoryWindow(this);history.Closed+=delegate{history=null;};history.Show();}else{history.RefreshCatalog();history.Activate();}}
        public NoteWindow RestoreHistoryNote(NoteData source){
            string key=HistoryCatalog.Key(source);NoteData existing=Store.Data.Notes.FirstOrDefault(n=>n.RecoverySourceKey==key);
            if(existing!=null){existing.Deleted=false;ShowNote(existing);if(!SaveAll())throw new IOException(LastSaveError);return Notes[existing.Id];}
            NoteData note=PaperNest.Store.Clone(source);note.Id=Guid.NewGuid().ToString("N");note.RecoverySourceKey=key;note.Title=(source.Title??L.T("便签"))+L.T(" · 找回");note.Hidden=false;note.Deleted=false;note.Left=SystemParameters.WorkArea.Left+100;note.Top=SystemParameters.WorkArea.Top+80;note.Updated=DateTime.UtcNow;
            string original=Store.VersionDocumentPath(source);if(File.Exists(original)){File.Copy(original,Store.DocumentPath(note));note.DocumentSavedAt=DateTime.UtcNow;note.ContentUpdated=note.DocumentSavedAt;}else{note.DocumentRevision="";note.ContentUpdated=DateTime.UtcNow;note.DocumentSavedAt=DateTime.SpecifyKind(DateTime.MinValue,DateTimeKind.Utc);}
            Store.Data.Notes.Add(note);NoteWindow window=new NoteWindow(this,note);Notes.Add(note.Id,window);NoteChanged(window,false);if(!SaveAll())throw new IOException(LastSaveError);if(!TestMode){window.Show();window.Activate();}return window;
        }
        public void DeleteNote(NoteWindow window) {
            if(window.Data.Locked)return;
            if(MessageBox.Show(L.T("把这张便签放入废纸篓？\n之后仍可在收纳盒里恢复。"),L.T("纸间"),MessageBoxButton.OKCancel,MessageBoxImage.Question)!=MessageBoxResult.OK)return;
            window.SaveContent();window.Data.Deleted=true;window.Hide();ScheduleSave();
        }
        public void ExitApp() {
            if(Exiting)return;
            if(!SaveAll()){MessageBox.Show(L.T("保存未完成，程序保持打开。请检查磁盘空间后重试。"),L.T("纸间"));return;}
            Exiting=true;stopPipe=true;
            Store.SnapshotNow();
            if(saveTimer!=null)saveTimer.Stop();if(ipcTimer!=null)ipcTimer.Stop();
            if(safetyTimer!=null)safetyTimer.Stop();
            if(tray!=null){tray.Visible=false;tray.Dispose();tray=null;}
            foreach(NoteWindow window in Notes.Values.ToArray())window.Close();
            if(writerLease!=null){writerLease.Dispose();writerLease=null;}
            if(mutex!=null){try{mutex.ReleaseMutex();}catch{}mutex.Dispose();}
            Shutdown(0);
        }
        public bool AcquireSingleInstance(string root,string requestedCommand) {
            root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);
            uint hash=2166136261;foreach(char c in root.ToLowerInvariant())hash=(hash^c)*16777619;
            pipeName="PaperNest_"+hash.ToString("x8");bool created;
            mutex=new Mutex(true,@"Local\"+pipeName,out created);
            if(!created) {
                try { using(NamedPipeClientStream pipe=new NamedPipeClientStream(".",pipeName,PipeDirection.Out)){pipe.Connect(2000);using(StreamWriter writer=new StreamWriter(pipe)){writer.WriteLine(requestedCommand);writer.Flush();}} }
                catch { MessageBox.Show("纸间已经在运行，请从右下角托盘图标打开。","纸间"); }
                mutex.Dispose();mutex=null;return false;
            }
            writerLease=new FileStream(Path.Combine(Store.Root,".writer.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.Read);
            Task.Factory.StartNew(delegate {
                while(!stopPipe) {
                    try { using(NamedPipeServerStream pipe=new NamedPipeServerStream(pipeName,PipeDirection.In,1,PipeTransmissionMode.Byte,PipeOptions.Asynchronous)) {
                        IAsyncResult waiting=pipe.BeginWaitForConnection(null,null);
                        while(!waiting.AsyncWaitHandle.WaitOne(400)){if(stopPipe)return;}
                        pipe.EndWaitForConnection(waiting);using(StreamReader reader=new StreamReader(pipe)){string cmd=reader.ReadLine();if(cmd!=null)lock(commands)commands.Enqueue(cmd);}
                    }}catch(Exception ex){if(!stopPipe)Store.Log(ex);}
                }
            },TaskCreationOptions.LongRunning);
            ipcTimer=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(150) };ipcTimer.Tick+=delegate {
                string cmd=null;lock(commands){if(commands.Count>0)cmd=commands.Dequeue();}
                if(cmd=="exit")ExitApp();else if(cmd=="settings")OpenSettings();else if(cmd=="history")OpenHistory();else if(cmd=="about")OpenAbout();else if(cmd=="hide")HideAll();else if(cmd=="new")NewNote("text");else if(cmd=="new-checklist")NewNote("checklist");else if(cmd=="new-photo")NewNote("photo");else if(cmd=="show")ShowAll();else if(cmd=="open")OpenWorkspace();else if(cmd=="inspect")Inspect();else if(cmd!=null&&cmd.StartsWith("inspect@",StringComparison.Ordinal))Inspect(Encoding.UTF8.GetString(Convert.FromBase64String(cmd.Substring(8))));
            };ipcTimer.Start();return true;
        }
        internal void Inspect(string output=null) {
            bool saved=SaveAll();string text="{\"notes\":"+Notes.Count+",\"dataNotes\":"+Store.Data.Notes.Count+",\"visible\":"+Notes.Values.Count(n=>n.IsVisible)+",\"saved\":"+(saved?"true":"false")+",\"tray\":"+(tray!=null&&tray.Visible?"true":"false")+",\"startup\":"+(PaperNest.Startup.IsEnabled()?"true":"false")+",\"statePath\":\""+Store.StatePath.Replace("\\","\\\\")+"\"}";
            text=text.Substring(0,text.Length-1)+",\"processId\":"+Process.GetCurrentProcess().Id+",\"runtimeVersion\":\""+Assembly.GetExecutingAssembly().GetName().Version+"\",\"physicalStatePath\":\""+WindowsHost.PhysicalFile(Store.StatePath).Replace("\\","\\\\")+"\",\"noteIds\":["+String.Join(",",Store.Data.Notes.Select(n=>"\""+n.Id+"\""))+"]}";
            File.WriteAllText(output??Path.Combine(Store.Root,"inspection.json"),text);
        }
    }
    static class Entry {
        static string Value(string[] args,string key,string fallback) {int i=Array.IndexOf(args,key);return i>=0 && i+1<args.Length?args[i+1]:fallback;}
        [STAThread] public static int Main(string[] args) {
            L.Set(L.PreferredLanguage);
            string root=Value(args,"--data-dir",Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PaperNest","Data"));
            try {
                if(args.Contains("--self-test"))return SelfTest.Run(root,Value(args,"--report",Path.Combine(root,"self-test.json")));
                if(args.Contains("--autosave-test"))return AutosaveRegression.Run(root,Value(args,"--report",Path.Combine(root,"autosave-test.json")));
                if(args.Contains("--crash-write-test"))return AutosaveRegression.CrashWrite(root,Value(args,"--report",Path.Combine(root,"crash-ready.txt")));
                if(args.Contains("--crash-reopen-test"))return AutosaveRegression.CrashReopen(root,Value(args,"--report",Path.Combine(root,"crash-reopen.json")));
                if(args.Contains("--load-existing-test"))return AutosaveRegression.LoadExisting(root,Value(args,"--report",Path.Combine(root,"load-existing.json")));
                if(args.Contains("--paste-test"))return PasteRegression.Run(root,Value(args,"--report",Path.Combine(root,"paste-test.json")));
                if(args.Contains("--interaction-test"))return InteractionRegression.Run(root,Value(args,"--report",Path.Combine(root,"interaction-test.json")));
                if(args.Contains("--tray-test"))return TrayRegression.Run(root,Value(args,"--report",Path.Combine(root,"tray-test.json")));
                if(args.Contains("--history-test"))return HistoryRegression.Run(root,Value(args,"--report",Path.Combine(root,"history-test.json")));
                if(args.Contains("--history-preview"))return HistoryRegression.Render(root,Value(args,"--history-preview","history-preview.png"));
                if(args.Contains("--write-icon")){AppIcon.Write(Value(args,"--write-icon","PaperNest.ico"));return 0;}
                if(args.Contains("--render-preview"))return SelfTest.Render(Value(args,"--render-preview","preview.png"),root);
                if(!args.Contains("--data-dir")&&WindowsHost.HandoffIfRedirected(Startup.ExePath,args,root))return 0;
                NotesApp app=new NotesApp(root,false);
                string cmd=args.Contains("--exit")?"exit":args.Contains("--settings")?"settings":args.Contains("--history")?"history":args.Contains("--about")?"about":args.Contains("--hide")?"hide":args.Contains("--new-photo")?"new-photo":args.Contains("--new-checklist")?"new-checklist":args.Contains("--new")||args.Contains("--new-text")?"new":args.Contains("--inspect-output")?"inspect@"+Convert.ToBase64String(Encoding.UTF8.GetBytes(Path.GetFullPath(Value(args,"--inspect-output","inspection.json")))):args.Contains("--inspect")?"inspect":args.Contains("--show")?"show":"open";
                if(!app.AcquireSingleInstance(root,cmd))return 0;
                if(cmd=="exit"){app.ExitApp();return 0;}
                string recover=Value(args,"--recover-from","");if(recover.Length>0)app.Store.ImportLegacyWorkspace(recover);
                app.Initialize(args.Contains("--background"));
                if(cmd=="show")app.ShowAll();if(cmd=="open"&&!args.Contains("--background"))app.OpenWorkspace();
                if(cmd=="inspect"||cmd.StartsWith("inspect@",StringComparison.Ordinal)){app.Dispatcher.BeginInvoke(new Action(delegate{if(cmd=="inspect")app.Inspect();else app.Inspect(Encoding.UTF8.GetString(Convert.FromBase64String(cmd.Substring(8))));}));}
                if(cmd=="about")app.OpenAbout();if(cmd=="history")app.OpenHistory();if(cmd=="settings")app.OpenSettings();if(cmd=="new")app.NewNote("text");
                if(cmd=="new-checklist")app.NewNote("checklist");if(cmd=="new-photo")app.NewNote("photo");
                app.Run();return 0;
            }catch(Exception ex) {
                try{Directory.CreateDirectory(root);File.AppendAllText(Path.Combine(root,"startup-error.log"),ex.ToString());}catch{}
                MessageBox.Show(L.T("纸间没能启动，原有数据仍保留。\n")+ex.Message,L.T("纸间"),MessageBoxButton.OK,MessageBoxImage.Information);return 1;
            }
        }
    }
}
