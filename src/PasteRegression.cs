using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
namespace PaperNest {
 public static class PasteRegression {
  static IEnumerable<TextBox> Boxes(DependencyObject root){TextBox box=root as TextBox;if(box!=null)yield return box;foreach(object item in LogicalTreeHelper.GetChildren(root)){DependencyObject child=item as DependencyObject;if(child!=null)foreach(TextBox nested in Boxes(child))yield return nested;}}
  static TextBox[] Rows(NoteWindow window){return Boxes((DependencyObject)window.Content).Where(b=>b.AcceptsReturn).ToArray();}
  static string Normalize(string s){return s.Replace("\r\n","\n").Replace('\r','\n');}
  // Invoke WPF's real paste implementation with a supplied data object. This avoids
  // reading, replacing or depending on the user's Windows clipboard during tests.
  static void Paste(TextBox field,string text,bool html){
   Type type=typeof(TextBox).Assembly.GetType("System.Windows.Documents.TextEditorCopyPaste");
   MethodInfo method=type.GetMethod("_DoPaste",BindingFlags.Static|BindingFlags.NonPublic);
   object editor=typeof(TextBoxBase).GetProperty("TextEditor",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(field,null);
   DataObject data=new DataObject(DataFormats.UnicodeText,text);if(html)data.SetData(DataFormats.Html,"<html><body>Clipboard rich text alternative</body></html>");
   method.Invoke(null,new object[]{editor,data,false});
  }
  public static int Run(string root,string report){NotesApp app=null;List<string> checks=new List<string>();try{
   if(File.Exists(Path.Combine(root,"notes.json")))throw new Exception("Paste regression requires a fresh isolated directory");
   app=new NotesApp(root,true);app.Initialize(false);
   string payload="周末旅行准备清单（并且整\r\n理在相册和笔记中）\r\n最后一句也必须完整保留 ✓";
   TextBox old=new TextBox { AcceptsReturn=false,TextWrapping=TextWrapping.Wrap };Paste(old,payload,false);
   if(old.Text.Contains("最后一句"))throw new Exception("The original truncation was not reproduced");checks.Add("old single-line WPF paste truncation reproduced without touching user clipboard");
   NoteWindow window=app.NewNote("checklist");TextBox row=Rows(window).Single();Paste(row,payload,true);
   if(Normalize(row.Text)!=Normalize(payload))throw new Exception("Multiline clipboard text was truncated");checks.Add("real Unicode and HTML clipboard paste preserves every line and final suffix");
   if(window.Data.Items.Count!=1||Normalize(window.Data.Items[0])!=Normalize(payload))throw new Exception("One pasted task split into multiple saved items");checks.Add("multiline text stays inside one checklist task");
   row.Measure(new Size(280,Double.PositiveInfinity));row.Arrange(new Rect(0,0,280,row.DesiredSize.Height));row.UpdateLayout();
   if(row.ActualHeight<=38)throw new Exception("Pasted paragraph did not expand visibly");checks.Add("multiline row grows and wraps instead of hiding its tail");
   row.Text="前缀ABC后缀";row.Select(2,3);Paste(row,"第一段\r\n第二段",false);
   if(Normalize(row.Text)!="前缀第一段\n第二段后缀")throw new Exception("Paste selection replacement lost surrounding content");checks.Add("paste replaces only the selection and preserves surrounding text");
   row.SelectAll();string longText=new String('长',40000)+"尾句完整END";Paste(row,longText,false);if(row.Text!=longText)throw new Exception("Long text was limited or truncated");checks.Add("paste over 32767 characters keeps the complete tail");
   row.SelectAll();Paste(row,payload,false);window.SetChecklistItemCompleted(0,true);if(!app.SaveAll())throw new Exception("Paste could not be saved");
   Store reload=new Store(root);NoteData saved=reload.Data.Notes.Single(n=>n.Id==window.Data.Id);NoteWindow reopened=new NoteWindow(app,saved);
   if(Rows(reopened).Length!=1||Normalize(Rows(reopened)[0].Text)!=Normalize(payload)||saved.Checks!="1")throw new Exception("Restart split or truncated multiline task");checks.Add("restart preserves full multiline task and its checked state");
   row.Text="前半后半";row.CaretIndex=2;window.SplitChecklistItem(row);TextBox[] split=Rows(window);
   if(split.Length!=2||split[0].Text!="前半"||split[1].Text!="后半")throw new Exception("Enter did not preserve both sides of the caret");checks.Add("Enter creates the next task without discarding text after the caret");
   string json="{\"passed\":true,\"checks\":["+String.Join(",",checks.Select(s=>"\""+s+"\""))+" ]}";File.WriteAllText(report,json);app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();reopened.Close();return 0;
  }catch(Exception ex){File.WriteAllText(report,"{\"passed\":false,\"error\":\""+ex.ToString().Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"}");if(app!=null){app.Exiting=true;foreach(NoteWindow w in app.Notes.Values)w.Close();}return 1;}}
 }
}
