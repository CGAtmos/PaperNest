using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
namespace PaperNest {
 public sealed class NoteVersion {
  public NoteData Note;public DateTime Time;public bool Current;public string Key;
  public override string ToString(){return Current?L.T("现在的内容"):FriendlyTime(Time)+"  ·  "+(Note.Text??"").Trim().Length+L.T(" 字");}
  public static string FriendlyTime(DateTime time){return time.Date==DateTime.Today?L.T("今天 ")+time.ToString("HH:mm"):time.Date==DateTime.Today.AddDays(-1)?L.T("昨天 ")+time.ToString("HH:mm"):time.ToString(L.T("M月d日 HH:mm"),System.Globalization.CultureInfo.GetCultureInfo(L.English?"en-US":"zh-CN"));}
 }
 public sealed class NoteHistory {
  public string Id;public NoteData Current;public readonly List<NoteVersion> Versions=new List<NoteVersion>();
  public NoteData Display{get{return Current??Versions[0].Note;}}
  public string Status{get{return Current==null?L.T("历史留存"):Current.Deleted?L.T("废纸篓"):Current.Hidden?L.T("已收起"):L.T("在桌面");}}
  public bool Matches(string query){return Versions.Any(v=>((v.Note.Title??"")+"\n"+(v.Note.Text??"")).IndexOf(query,StringComparison.OrdinalIgnoreCase)>=0);}
 }
 public static class HistoryCatalog {
  public static string Key(NoteData note){
   string[] items=note.Kind=="checklist"?(note.Items??(note.Text??"").Replace("\r","").Split('\n').ToList()).ToArray():new string[0];
   string[] values=new[]{note.Title??"",note.Kind??"",(note.Text??"").Replace("\r\n","\n").TrimEnd('\n'),note.Checks??"",note.ImageFile??"",note.DocumentRevision??""}.Concat(items).ToArray();
   using(MemoryStream stream=new MemoryStream()){new DataContractJsonSerializer(typeof(string[])).WriteObject(stream,values);using(SHA256 sha=SHA256.Create())return note.Id+":"+BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-","");}
  }
  public static List<NoteHistory> Build(Store store){
   Dictionary<string,NoteHistory> groups=new Dictionary<string,NoteHistory>();
   Action<NoteData,DateTime,bool> add=(note,time,current)=>{NoteHistory group;if(!groups.TryGetValue(note.Id,out group)){group=new NoteHistory{Id=note.Id};groups.Add(note.Id,group);}if(current)group.Current=note;string key=Key(note);if(!group.Versions.Any(v=>v.Key==key))group.Versions.Add(new NoteVersion{Note=Store.Clone(note),Time=time,Current=current,Key=key});};
   foreach(NoteData note in store.Data.Notes)add(note,DateTime.Now,true);
   foreach(string path in store.HistoryFiles())try{foreach(NoteData note in store.ReadHistory(path).Notes)add(note,File.GetLastWriteTime(path),false);}catch{}
   return groups.Values.OrderBy(g=>g.Current==null||g.Current.Deleted?2:g.Current.Hidden?1:0).ThenByDescending(g=>g.Display.Updated).ToList();
  }
 }
}
