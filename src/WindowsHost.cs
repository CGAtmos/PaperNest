using System;
using System.Text;
using System.Linq;
using System.Runtime.InteropServices;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace PaperNest {
 public static class WindowsHost {
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern int GetCurrentPackageFullName(ref int length,StringBuilder name);
  public static string PackageName {
   get {int length=0;int result=GetCurrentPackageFullName(ref length,null);if(result==15700)return "";if(result!=122)throw new InvalidOperationException("Unable to determine Windows package context: "+result);StringBuilder name=new StringBuilder(length);result=GetCurrentPackageFullName(ref length,name);if(result!=0)throw new InvalidOperationException("Unable to read Windows package context: "+result);return name.ToString();}
  }
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern uint GetFinalPathNameByHandle(SafeFileHandle file,StringBuilder path,uint size,uint flags);
  public static string PhysicalFile(string file){using(FileStream stream=new FileStream(file,FileMode.Open,FileAccess.Read,FileShare.ReadWrite|FileShare.Delete)){StringBuilder path=new StringBuilder(32768);if(GetFinalPathNameByHandle(stream.SafeFileHandle,path,(uint)path.Capacity,0)==0)throw new System.ComponentModel.Win32Exception();string result=path.ToString();return result.StartsWith(@"\\?\")?result.Substring(4):result;}}
  public static string RedirectedRoot(string root) {
   string file=Path.Combine(root,"notes.json");if(!File.Exists(file)){Directory.CreateDirectory(root);file=Path.Combine(root,".storage-context");using(FileStream marker=new FileStream(file,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.ReadWrite|FileShare.Delete)){} }
   string physical=PhysicalFile(file);
   return !String.Equals(Path.GetFullPath(file),physical,StringComparison.OrdinalIgnoreCase)&&physical.IndexOf(@"\AppData\Local\Packages\",StringComparison.OrdinalIgnoreCase)>=0?Path.GetDirectoryName(physical):"";
  }
  public static bool HandoffIfRedirected(string exe,string[] args,string dataRoot) {
   string redirected=RedirectedRoot(dataRoot);if(redirected.Length==0&&PackageName.Length==0)return false;
   if(args.Contains("--desktop-handoff"))throw new InvalidOperationException("Windows 未能切换到桌面运行环境，程序没有改写任何便签。请从桌面快捷方式打开纸间。");
   string[] extra=redirected.Length>0&&!args.Contains("--recover-from")?new[]{"--desktop-handoff","--recover-from",redirected}:new[]{"--desktop-handoff"};LaunchDesktop(exe,args.Concat(extra).ToArray());return true;
  }
  public static string Quote(string value) {
   StringBuilder result=new StringBuilder("\"");int slashes=0;
   foreach(char c in value){if(c=='\\'){slashes++;continue;}if(c=='\"')result.Append('\\',slashes*2+1).Append(c);else result.Append('\\',slashes).Append(c);slashes=0;}
   return result.Append('\\',slashes*2).Append('"').ToString();
  }
  [ComImport,Guid("6D5140C1-7436-11CE-8034-00AA006009FA"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IServiceProvider {
   [PreserveSig]int QueryService(ref Guid service,ref Guid iid,out IntPtr obj);
  }
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int ActiveView(IntPtr self,out IntPtr view);
  [UnmanagedFunctionPointer(CallingConvention.StdCall)]delegate int ItemObject(IntPtr self,uint type,ref Guid iid,out IntPtr item);
  static T Slot<T>(IntPtr obj,int slot) where T:class {return Marshal.GetDelegateForFunctionPointer(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj),slot*IntPtr.Size),typeof(T)) as T;}
  public static void LaunchDesktop(string exe,string[] args) {
   // Ask the existing Explorer desktop to launch. A child of an MSIX host can
   // inherit its AppData/registry virtualization even when this exe is unpackaged.
   object windows=null,desktop=null,folder=null,shell=null;IntPtr browser=IntPtr.Zero,view=IntPtr.Zero,item=IntPtr.Zero;
   try {
    windows=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
    object location=0,root=0;int hwnd;desktop=((dynamic)windows).FindWindowSW(ref location,ref root,8,out hwnd,1);
    if(desktop==null)throw new InvalidOperationException(L.T("Windows 桌面尚未就绪，请从桌面重新打开纸间。"));
    Guid service=new Guid("4C96BE40-915C-11CF-99D3-00AA004AE837"),browserId=new Guid("000214E2-0000-0000-C000-000000000046"),dispatchId=new Guid("00020400-0000-0000-C000-000000000046");
    Marshal.ThrowExceptionForHR(((IServiceProvider)desktop).QueryService(ref service,ref browserId,out browser));
    Marshal.ThrowExceptionForHR(Slot<ActiveView>(browser,15)(browser,out view));
    Marshal.ThrowExceptionForHR(Slot<ItemObject>(view,15)(view,0,ref dispatchId,out item));
    folder=Marshal.GetObjectForIUnknown(item);shell=((dynamic)folder).Application;
    ((dynamic)shell).ShellExecute(exe,String.Join(" ",args.Select(Quote)),System.IO.Path.GetDirectoryName(exe),"open",1);
   } finally {
    foreach(object obj in new[]{shell,folder,desktop,windows})if(obj!=null&&Marshal.IsComObject(obj))Marshal.ReleaseComObject(obj);
    foreach(IntPtr raw in new[]{item,view,browser})if(raw!=IntPtr.Zero)Marshal.Release(raw);
   }
  }
 }
}
