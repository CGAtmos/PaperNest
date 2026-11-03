using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Threading;
using System.Runtime.Serialization.Json;
using System.Collections.Generic;
namespace PaperNest {
 public static class AutosaveRegression {
  static T Find<T>(DependencyObject root) where T:DependencyObject {if(root is T)return (T)root;foreach(object child in LogicalTreeHelper.GetChildren(root)){DependencyObject d=child as DependencyObject;if(d!=null){T found=Find<T>(d);if(found!=null)return found;}}return null;}
  static void Pump(int ms){DispatcherFrame frame=new DispatcherFrame();DispatcherTimer deadline=new DispatcherTimer(DispatcherPriority.Send){Interval=TimeSpan.FromMilliseconds(ms)};deadline.Tick+=delegate{deadline.Stop();frame.Continue=false;};deadline.Start();Dispatcher.PushFrame(frame);}
  public static int Run(string root,string report){
   NotesApp app=null;
   try {
    if(File.Exists(Path.Combine(root,"notes.json")))throw new Exception("Use a fresh isolated test directory");
    app=new NotesApp(root,true);app.Initialize(false);NoteWindow window=app.NewNote("text");
    string token="自动保存回归：不点保存也必须保留。";
    Find<TextBox>((DependencyObject)window.Content).Text="未手动保存的测试便签";
    RichTextBox editor=Find<RichTextBox>((DependencyObject)window.Content);editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(new Run(token)));
    Pump(2200);
    Store disk=new Store(root);NoteData note=disk.Data.Notes.FirstOrDefault(n=>n.Id==window.Data.Id);
    if(note==null||!note.Text.Contains(token))throw new Exception("Autosave did not persist editing without Ctrl+S");
    Run typing=new Run("连续输入：");editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(typing));bool checkpointDuringTyping=false;
    for(int i=0;i<45;i++){typing.Text+="字";Pump(20);if(i==30){using(FileStream stream=File.OpenRead(app.Store.StatePath)){AppData primary=(AppData)new DataContractJsonSerializer(typeof(AppData)).ReadObject(stream);checkpointDuringTyping=primary.Notes.Single(n=>n.Id==window.Data.Id).Text.Contains("连续输入");}}}
    if(!checkpointDuringTyping)throw new Exception("Continuous editing postponed checkpoint indefinitely");
    editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(new Run(token)));Pump(400);
    window.Close();Pump(1400);disk=new Store(root);note=disk.Data.Notes.Single(n=>n.Id==window.Data.Id);
    if(!note.Hidden||!note.Text.Contains(token))throw new Exception("Close did not retain hidden note and contents");
    if(!Directory.GetFiles(app.Store.HistoryRoot,"*.json").Any())throw new Exception("No history snapshot was written");
    File.SetAttributes(app.Store.StatePath,FileAttributes.ReadOnly);bool blocked=!app.SaveAll();File.SetAttributes(app.Store.StatePath,FileAttributes.Normal);if(!blocked)throw new Exception("Read-only save failure was reported as success");if(!app.SaveAll())throw new Exception("Retry after save failure did not succeed");
    File.WriteAllText(report,"{\"passed\":true,\"checks\":[\"editing persists without manual save\",\"checkpoint runs during continuous input\",\"close immediately preserves text and hidden state\",\"history snapshots exist\",\"save failure is visible and retry succeeds\"]}");app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();return 0;
   }catch(Exception ex){File.WriteAllText(report,"{\"passed\":false,\"error\":\""+ex.ToString().Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"}");if(app!=null){app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();}return 1;}
  }
  public static int CrashWrite(string root,string report){NotesApp app=new NotesApp(root,true);app.Initialize(false);NoteWindow window=app.NewNote("text");Find<TextBox>((DependencyObject)window.Content).Text="强制结束前的草稿";RichTextBox editor=Find<RichTextBox>((DependencyObject)window.Content);editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(new Run("尚未按保存，也没有正常退出的文字：完整找回 ✓")));File.WriteAllText(report,window.Data.Id);Environment.Exit(23);return 23;}
  public static int CrashReopen(string root,string report){NotesApp app=null;try{
   app=new NotesApp(root,true);app.Initialize(false);NoteData note=app.Store.Data.Notes.Single(n=>n.Title=="强制结束前的草稿");NoteWindow window=app.Notes[note.Id];RichTextBox editor=Find<RichTextBox>((DependencyObject)window.Content);
   string text=new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Text;if(!text.Contains("完整找回 ✓"))throw new Exception("Draft text did not reach the reopened editor");
   window.Close();string lost=app.Store.StatePath+".test-original";File.Move(app.Store.StatePath,lost);Store missing=new Store(root);if(!missing.Data.Notes.Any(n=>n.Id==note.Id&&n.Text.Contains("完整找回")))throw new Exception("Missing index recovery failed");
   File.WriteAllText(app.Store.StatePath,"{bad");File.WriteAllText(app.Store.StatePath+".bak","{bad backup");Store corrupt=new Store(root);if(!corrupt.Data.Notes.Any(n=>n.Id==note.Id&&n.Text.Contains("完整找回")))throw new Exception("Corrupt indexes erased durable drafts");
   Store.AtomicWrite(app.Store.StatePath,File.ReadAllBytes(lost),false);File.WriteAllText(report,"{\"passed\":true,\"checks\":[\"forced process termination preserves unsaved text\",\"restart renders the newest draft instead of stale document\",\"missing main index recovers from backup and drafts\",\"corrupt main and backup indexes recover from history and drafts\"]}");app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();return 0;
  }catch(Exception ex){File.WriteAllText(report,ex.ToString());if(app!=null){app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();}return 1;}}
  public static int LoadExisting(string root,string report){NotesApp app=null;try{
   app=new NotesApp(root,true);var expected=app.Store.Data.Notes.Where(n=>!n.Deleted).ToDictionary(n=>n.Id,n=>(n.Title??"")+"\n"+(n.Text??"").Trim());app.Initialize(false);
   if(app.Notes.Count!=expected.Count)throw new Exception("Existing notes were not all loaded");
   if(!app.SaveAll())throw new Exception("Legacy notes could not be saved");
   foreach(NoteData n in app.Store.Data.Notes.Where(n=>!n.Deleted))if(expected[n.Id]!=(n.Title??"")+"\n"+(n.Text??"").Trim())throw new Exception("Legacy text changed: "+n.Id);
   HistoryWindow history=new HistoryWindow(app);history.Close();
   File.WriteAllText(report,"{\"passed\":true,\"legacyNotes\":"+expected.Count+",\"checks\":[\"all existing notes load\",\"legacy text and titles remain unchanged\",\"history recovery window opens\"]}");app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();return 0;
  }catch(Exception ex){File.WriteAllText(report,ex.ToString());if(app!=null){app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();}return 1;}}
 }
}
