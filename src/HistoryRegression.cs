using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace PaperNest {
 public static class HistoryRegression {
  static T Find<T>(DependencyObject root) where T:DependencyObject{if(root is T)return (T)root;foreach(object item in LogicalTreeHelper.GetChildren(root)){DependencyObject child=item as DependencyObject;if(child!=null){T match=Find<T>(child);if(match!=null)return match;}}return null;}
  static BitmapSource Sample(byte r){return BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{40,80,r,255},4);}
  static void Assert(bool good,string name,List<string> checks){if(!good)throw new Exception(name);checks.Add(name);}
  static void RenderWindow(HistoryWindow window,string path){FrameworkElement view=(FrameworkElement)window.Content;((Grid)view).Background=window.Background;view.Width=930;view.Height=660;view.Measure(new Size(930,660));view.Arrange(new Rect(0,0,930,660));view.UpdateLayout();Look.WritePng(view,path,930,660,1.5);}
  public static int Render(string root,string path){NotesApp app=new NotesApp(root,true);app.Initialize(false);HistoryWindow window=new HistoryWindow(app);window.SearchBox.Text="";RenderWindow(window,path);app.Exiting=true;window.Close();foreach(NoteWindow w in app.Notes.Values)w.Close();return 0;}
  public static int Run(string root,string report){NotesApp app=null;HistoryWindow history=null;List<string> checks=new List<string>();try{
   if(File.Exists(Path.Combine(root,"notes.json")))throw new Exception("Use an isolated empty directory");app=new NotesApp(root,true);app.Initialize(false);
   NoteWindow note=app.NewNote("text");note.Data.Title="周末旅行";RichTextBox editor=Find<RichTextBox>((DependencyObject)note.Content);editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(new Run("带上相机，去看日落。")));note.InsertImageSource(Sample(220));app.SaveAll();NoteData old=Store.Clone(note.Data);app.Store.SnapshotNow();note.Data.Left+=10;note.Data.Updated=DateTime.UtcNow;app.SaveAll();app.Store.SnapshotNow();
   Assert(HistoryCatalog.Build(app.Store).Single(g=>g.Id==note.Data.Id).Versions.Count==1,"identical content snapshots and layout-only changes collapse into one version",checks);
   editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(new Run("新的行程：周日去图书馆。")));note.InsertImageSource(Sample(40));app.SaveAll();app.Store.SnapshotNow();
   NoteHistory group=HistoryCatalog.Build(app.Store).Single(g=>g.Id==note.Data.Id);Assert(group.Versions.Count==2,"a real text/image edit creates a distinct selectable content version",checks);
   history=new HistoryWindow(app);history.SearchBox.Text="带上相机";Assert(history.PaperList.Items.Count==1,"search finds a note through its older content",checks);
   history.VersionList.SelectedItem=history.VersionList.Items.OfType<NoteVersion>().Single(v=>v.Key==HistoryCatalog.Key(old));int before=app.Store.Data.Notes.Count;history.PrimaryAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
   NoteData restored=app.Store.Data.Notes.Single(n=>n.RecoverySourceKey==HistoryCatalog.Key(old));NoteWindow restoredWindow=app.Notes[restored.Id];RichTextBox restoredEditor=Find<RichTextBox>((DependencyObject)restoredWindow.Content);Image oldImage=(Image)restoredEditor.Document.Blocks.OfType<BlockUIContainer>().Single().Child;byte[] pixel=new byte[4];((BitmapSource)oldImage.Source).CopyPixels(pixel,4,0);
   Assert(restored.Text.Contains("带上相机")&&!restored.Text.Contains("图书馆")&&pixel[2]==220,"restoring old content uses its own archived rich document and image",checks);
   Assert(note.Data.Text.Contains("图书馆")&&app.Store.Data.Notes.Count==before+1,"restore leaves the current note intact and creates one copy",checks);
   history.PrimaryAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(app.Store.Data.Notes.Count==before+1&&history.ResultText.Text==L.T("已放回桌面，可以继续编辑。"),"repeated restore opens the existing recovered copy and reports success",checks);
   Store disk=new Store(root);Assert(disk.Data.Notes.Count(n=>n.RecoverySourceKey==HistoryCatalog.Key(old))==1,"restored-version identity is persisted for later sessions",checks);
   NoteData legacy=Store.Clone(old);legacy.DocumentRevision="";legacy.Text="更早的纯文字版本";NoteWindow legacyWindow=app.RestoreHistoryNote(legacy);Assert(legacyWindow.EmbeddedImageCount==0&&legacyWindow.Data.Text.Contains("更早的"),"legacy text-only history never substitutes images from the current document",checks);
   NoteData draft=Store.Clone(old);draft.Text="草稿里较新的文字";draft.ContentUpdated=draft.DocumentSavedAt.AddSeconds(1);NoteWindow draftWindow=app.RestoreHistoryNote(draft);Assert(draftWindow.Data.Text.Contains("草稿里较新")&&draftWindow.EmbeddedImageCount==0,"a newer draft never gets replaced by an older archived rich document",checks);
   note.HideNote();history.RefreshCatalog();history.SearchBox.Text="新的行程";int total=app.Store.Data.Notes.Count;history.PrimaryAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(!note.Data.Hidden&&app.Store.Data.Notes.Count==total,"opening a hidden note reuses its original identity",checks);
   note.Data.Deleted=true;note.Hide();app.SaveAll();history.RefreshCatalog();history.PrimaryAction.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Assert(!note.Data.Deleted&&app.Store.Data.Notes.Count==total,"recovering a trashed note reuses its original identity",checks);
   history.SearchBox.Text="不存在的关键词";Assert(history.PaperList.Items.Count==0&&!history.PrimaryAction.IsEnabled,"empty search disables recovery and shows an actionable empty state",checks);
   history.SearchBox.Text="周末旅行";RenderWindow(history,Path.Combine(root,"history-preview.png"));
   File.WriteAllText(report,"{\"passed\":true,\"checks\":["+String.Join(",",checks.Select(s=>"\""+s+"\""))+"]}");return 0;
  }catch(Exception ex){File.WriteAllText(report,ex.ToString());return 1;}finally{if(app!=null){app.Exiting=true;if(history!=null)history.Close();foreach(NoteWindow window in app.Notes.Values)window.Close();}}}
 }
}
