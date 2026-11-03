using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;

namespace PaperNest {
    public static class InteractionRegression {
        static IEnumerable<T> Find<T>(DependencyObject root) where T:DependencyObject {
            if(root is T)yield return (T)root;
            foreach(object item in LogicalTreeHelper.GetChildren(root)){DependencyObject child=item as DependencyObject;if(child!=null)foreach(T result in Find<T>(child))yield return result;}
        }
        static void Check(bool value,string name,List<string> checks){if(!value)throw new Exception(name);checks.Add(name);}
        static void Render(Window window,string file,double width,double height){FrameworkElement content=(FrameworkElement)window.Content;Panel panel=content as Panel;if(panel!=null)panel.Background=window.Background;content.Width=width;content.Height=height;content.Measure(new Size(width,height));content.Arrange(new Rect(0,0,width,height));content.UpdateLayout();Look.WritePng(content,file,width,height,1.5);}
        static void Write(string report,bool passed,List<string> checks,Exception error){Directory.CreateDirectory(Path.GetDirectoryName(report));Func<string,string> quote=s=>"\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";File.WriteAllText(report,"{\"passed\":"+(passed?"true":"false")+",\"checks\":["+String.Join(",",checks.Select(quote))+"],\"error\":"+(error==null?"null":quote(error.ToString()))+"}",new UTF8Encoding(false));}
        public static int Run(string root,string report){List<string> checks=new List<string>();NotesApp app=null;try{
            if(File.Exists(Path.Combine(root,"notes.json")))throw new Exception("Use a fresh isolated data directory");L.Set("zh-CN");app=new NotesApp(root,true);app.Initialize(false);
            NoteWindow text=app.NewNote("text"),todo=app.NewNote("checklist"),photo=app.NewNote("photo");
            new System.Windows.Interop.WindowInteropHelper(text).EnsureHandle();
            TextBox title=Find<TextBox>((DependencyObject)text.Content).First();title.Text="我的灵感 / My ideas";
            RichTextBox editor=Find<RichTextBox>((DependencyObject)text.Content).Single();editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(new Run("我的中文内容\nEnglish content stays as written.")));
            BitmapSource sample=BitmapSource.Create(2,2,96,96,PixelFormats.Bgra32,null,new byte[]{20,90,180,255,30,80,200,255,50,100,170,255,20,150,210,255},8);
            text.InsertImageSource(sample);photo.InsertImageSource(sample);todo.SetChecklistItemCompleted(0,true);app.SaveAll();
            NoteData before=Store.Clone(text.Data);string revision=before.DocumentRevision;string photoBefore=photo.Data.ImageFile;
            Check(text.SetLocked(true)&&todo.SetLocked(true)&&photo.SetLocked(true),"all three note types lock after saving pending edits",checks);
            MethodInfo messages=typeof(NoteWindow).GetMethod("WindowMessages",BindingFlags.NonPublic|BindingFlags.Instance);object[] native={IntPtr.Zero,0x112,new IntPtr(0xF010),IntPtr.Zero,false};messages.Invoke(text,native);Check((bool)native[4],"native system Move is blocked for a locked note",checks);native=new object[]{IntPtr.Zero,0x84,new IntPtr(Int64.MaxValue),IntPtr.Zero,false};messages.Invoke(text,native);Check(!(bool)native[4],"unrelated 64-bit native messages pass through without integer overflow",checks);
            Check(title.IsReadOnly&&editor.IsReadOnly&&Find<TextBox>((DependencyObject)todo.Content).All(t=>t.IsReadOnly)&&Find<TextBox>((DependencyObject)photo.Content).All(t=>t.IsReadOnly),"title, rich text, checklist fields and photo caption become read-only",checks);
            Check(Find<CheckBox>((DependencyObject)todo.Content).All(c=>!c.IsEnabled)&&Find<Thumb>((DependencyObject)text.Content).All(t=>!t.IsEnabled&&!t.IsHitTestVisible),"task toggles and resize handle are blocked while locked",checks);
            editor.SelectAll();EditingCommands.Backspace.Execute(null,editor);title.SelectAll();EditingCommands.Backspace.Execute(null,title);
            text.SetZoom(2);text.SetAngle(5);text.InsertImageSource(sample);photo.InsertImageSource(sample);todo.SetChecklistItemCompleted(0,false);todo.SplitChecklistItem(Find<TextBox>((DependencyObject)todo.Content).Last());
            app.SaveAll();Check(text.Data.Title==before.Title&&text.Data.Text==before.Text&&text.Data.Zoom==before.Zoom&&text.Data.Angle==before.Angle&&text.EmbeddedImageCount==1&&photo.Data.ImageFile==photoBefore&&todo.Data.Checks=="1", "editing commands, image insertion, task edits, zoom and tilt cannot change locked notes",checks);
            MenuItem lockedAction=text.Frame.ContextMenu.Items.OfType<MenuItem>().First(m=>Object.Equals(m.Header,L.T("解锁这张便签   Ctrl+L")));Check(lockedAction.IsEnabled&&text.Frame.ContextMenu.Items.OfType<MenuItem>().Where(m=>Object.Equals(m.Header,L.T("放入废纸篓…"))||Object.Equals(m.Header,L.T("添加图片…"))||Object.Equals(m.Header,L.T("纸张颜色"))).All(m=>!m.IsEnabled),"unlock stays reachable while edit and trash actions are disabled",checks);
            Check(!text.ToolbarVisible,"toolbar is hidden on an idle note",checks);
            ContextMenu menu=text.Frame.ContextMenu;menu.RaiseEvent(new RoutedEventArgs(ContextMenu.OpenedEvent));Check(text.ToolbarVisible,"toolbar remains visible while its popup menu is open",checks);menu.RaiseEvent(new RoutedEventArgs(ContextMenu.ClosedEvent));Check(!text.ToolbarVisible,"closing the popup away from the note hides the toolbar",checks);
            FieldInfo keyboardMode=typeof(NoteWindow).GetField("keyboardToolbar",BindingFlags.NonPublic|BindingFlags.Instance);keyboardMode.SetValue(text,true);text.Frame.RaiseEvent(new MouseEventArgs(Mouse.PrimaryDevice,Environment.TickCount){RoutedEvent=Mouse.MouseLeaveEvent});Check(!(bool)keyboardMode.GetValue(text)&&!text.ToolbarVisible,"moving the mouse out clears keyboard toolbar visibility",checks);
            string signature=HistoryCatalog.Key(text.Data);SettingsWindow settings=new SettingsWindow(app);ComboBox language=Find<ComboBox>((DependencyObject)settings.Content).First();language.SelectedIndex=1;Find<Button>((DependencyObject)settings.Content).Single(b=>Object.Equals(b.Content,L.T("保存设置"))).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Check(L.English&&new Store(root).Data.Language=="en"&&text.Title.StartsWith("PaperNest"),"settings switch to English immediately and persist the preference",checks);
            Check(Object.ReferenceEquals(editor,Find<RichTextBox>((DependencyObject)text.Content).Single())&&HistoryCatalog.Key(text.Data)==signature&&text.Data.DocumentRevision==revision&&text.Data.Locked,"live language switch preserves the editor, content, image revision and lock",checks);
            Check(text.Frame.ContextMenu.Items.OfType<MenuItem>().Any(m=>Object.Equals(m.Header,"Unlock this note   Ctrl+L"))&&DesktopMenu.Labels[0]=="New text note", "note actions and Windows context menu labels use the new language",checks);
            SettingsWindow englishSettings=new SettingsWindow(app);AboutWindow about=new AboutWindow(app);ManagerWindow library=new ManagerWindow(app);HistoryWindow history=new HistoryWindow(app);
            Check(Find<TextBlock>((DependencyObject)about.Content).Any(t=>t.Text.Contains("Kaysar"))&&Find<TextBlock>((DependencyObject)about.Content).Any(t=>t.Text.Contains(Assembly.GetExecutingAssembly().GetName().Version.ToString(3)))&&about.Title=="About PaperNest", "About shows Kaysar, the current version, and English copy",checks);
            Render(englishSettings,Path.Combine(root,"settings-en.png"),564,665);Render(about,Path.Combine(root,"about-en.png"),600,650);Render(history,Path.Combine(root,"history-en.png"),930,660);Render(text,Path.Combine(root,"locked-note-idle.png"),text.Width,text.Height);
            Check(library.Title=="PaperNest · Note Library"&&history.Title=="PaperNest · Recover Notes", "library and recovery surfaces use English",checks);
            englishSettings.Close();about.Close();library.Close();history.Close();
            Store reload=new Store(root);NoteData saved=reload.Data.Notes.Single(n=>n.Id==text.Data.Id);NoteWindow reopened=new NoteWindow(app,saved);Check(saved.Locked&&Find<RichTextBox>((DependencyObject)reopened.Content).Single().IsReadOnly&&reopened.EmbeddedImageCount==1,"locked state and full-resolution embedded content survive a store reload",checks);app.Exiting=true;reopened.Close();app.Exiting=false;
            lockedAction=text.Frame.ContextMenu.Items.OfType<MenuItem>().First(m=>Object.Equals(m.Header,"Unlock this note   Ctrl+L"));lockedAction.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Check(!text.Data.Locked&&!editor.IsReadOnly&&!title.IsReadOnly,"unlock from the visible menu restores editing immediately",checks);text.SetAngle(3);text.SetZoom(1.25);Check(text.Data.Angle==3&&text.Data.Zoom==1.25&&todo.Data.Locked,"unlock affects only its own note and restores layout controls",checks);
            Check(app.SaveAll()&&!new Store(root).Data.Notes.Single(n=>n.Id==text.Data.Id).Locked,"unlock persists across reload",checks);
            app.ChangeLanguage("zh-CN");app.SaveAll();Check(text.Title.StartsWith("纸间")&&new Store(root).Data.Language=="zh-CN"&&text.Data.Text==before.Text,"switching back to Chinese preserves user text",checks);
            string legacy=Path.Combine(root,"legacy");Directory.CreateDirectory(legacy);File.WriteAllText(Path.Combine(legacy,"notes.json"),"{\"FormatVersion\":1,\"Notes\":[{\"Id\":\""+Guid.NewGuid().ToString("N")+"\",\"Title\":\"Legacy\",\"Text\":\"Kept\"}]}");Store old=new Store(legacy);L.Set(old.Data.Language);Check(!old.Data.Notes[0].Locked&&!L.English,"old data without language or lock fields keeps Chinese and remains editable",checks);
            app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();Write(report,true,checks,null);return 0;
        }catch(Exception ex){if(app!=null){app.Exiting=true;foreach(Window w in app.Windows.Cast<Window>().ToArray())w.Close();}Write(report,false,checks,ex);return 1;}}
    }
}
