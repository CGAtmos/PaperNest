using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using PaperNest;
class StorageProbe {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint size,uint flags);
 [STAThread]static int Main(string[] args){try{
  string path=Path.Combine(args[0],"notes.json");string physical="",hash="";AppData data=null;
  using(FileStream stream=File.OpenRead(path)){StringBuilder p=new StringBuilder(32768);if(GetFinalPathNameByHandle(stream.SafeFileHandle,p,(uint)p.Capacity,0)>0)physical=p.ToString();using(SHA256 sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(stream));stream.Position=0;data=(AppData)new DataContractJsonSerializer(typeof(AppData)).ReadObject(stream);}
  Store store=new Store(args[0]);string startup="";using(RegistryKey run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))if(run!=null)startup=run.GetValue("PaperNest.DesktopNotes") as string;
  object report=new {pid=System.Diagnostics.Process.GetCurrentProcess().Id,package=WindowsHost.PackageName,root=args[0],physical=physical,hash=hash,primaryCount=data.Notes.Count,loadedCount=store.Data.Notes.Count,notes=data.Notes.Select(n=>new{id=n.Id,title=n.Title,hidden=n.Hidden,deleted=n.Deleted,textLength=n.Text.Length}).ToArray(),startup=startup};
  if(args.Length>2){Directory.CreateDirectory(args[2]);foreach(string file in Directory.GetFiles(args[0],"*",SearchOption.AllDirectories)){if(file.EndsWith(".new",StringComparison.OrdinalIgnoreCase)||file.EndsWith(".lock",StringComparison.OrdinalIgnoreCase))continue;string relative=file.Substring(args[0].TrimEnd('\\').Length+1);string dest=Path.Combine(args[2],relative);Directory.CreateDirectory(Path.GetDirectoryName(dest));File.Copy(file,dest,true);}}
  File.WriteAllText(args[1],new System.Web.Script.Serialization.JavaScriptSerializer().Serialize(report));
  return 0;
 }catch(Exception ex){File.WriteAllText(args[1],ex.ToString());return 1;}}
}
