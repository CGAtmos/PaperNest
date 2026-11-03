using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;
namespace PaperNest {
 public static class DesktopMenu {
  const string Id="PaperNest.DesktopNotes";
  public static readonly string[] Kinds={"text","checklist","photo"};
  public static string[] Labels { get { return new[]{L.T("新建文字便签"),L.T("新建待办清单"),L.T("新建照片便签")}; } }
  public static string Classes(bool test){return test?@"Software\PaperNest\QA\Classes":@"Software\Classes";}
  public static string MenuPath(bool test){return Classes(test)+@"\DesktopBackground\Shell\"+Id;}
  public static string VerbsPath(bool test){return Classes(test)+@"\"+Id+".ContextMenu";}
  static string Preferences(bool test){return test?@"Software\PaperNest\QA\Integration":@"Software\PaperNest\Integration";}
  public static bool Preferred(bool test){using(RegistryKey key=Registry.CurrentUser.OpenSubKey(Preferences(test))){object value=key==null?null:key.GetValue("DesktopMenuEnabled");return value==null||Convert.ToInt32(value)!=0;}}
  public static bool IsEnabled(string exe,bool test=false){using(RegistryKey key=Registry.CurrentUser.OpenSubKey(MenuPath(test))){return key!=null&&String.Equals(key.GetValue("PaperNestOwner") as string,Path.GetFullPath(exe),StringComparison.OrdinalIgnoreCase);}}
  public static string Command(string exe,string kind){if(Array.IndexOf(Kinds,kind)<0)throw new ArgumentException("Unknown note kind");return "\""+Path.GetFullPath(exe)+"\" --new-"+kind;}
  public static void SetEnabled(bool enabled,string exe,bool test=false){
   exe=Path.GetFullPath(exe);if(enabled&&!File.Exists(exe))throw new FileNotFoundException(L.T("纸间程序文件不存在。"),exe);
   if(enabled){
    using(RegistryKey verbs=Registry.CurrentUser.CreateSubKey(VerbsPath(test))){verbs.SetValue("PaperNestOwner",exe);for(int i=0;i<Kinds.Length;i++)using(RegistryKey item=verbs.CreateSubKey("shell\\"+(i+1).ToString("00")+"_"+Kinds[i])){item.SetValue("MUIVerb",Labels[i]);item.SetValue("Icon","\""+exe+"\",0");using(RegistryKey command=item.CreateSubKey("command"))command.SetValue("",Command(exe,Kinds[i]));}}
    using(RegistryKey key=Registry.CurrentUser.CreateSubKey(MenuPath(test))){key.DeleteValue("",false);key.SetValue("MUIVerb",L.T("纸间便签"));key.SetValue("Icon","\""+exe+"\",0");key.SetValue("ExtendedSubCommandsKey",Id+".ContextMenu");key.SetValue("PaperNestOwner",exe);key.DeleteValue("SubCommands",false);}
   }else RemoveOwned(exe,test,false);
   using(RegistryKey prefs=Registry.CurrentUser.CreateSubKey(Preferences(test))){prefs.SetValue("DesktopMenuEnabled",enabled?1:0,RegistryValueKind.DWord);prefs.SetValue("Owner",exe);}
   if(!test)RefreshShell();
  }
  public static void RemoveOwned(string exe,bool test=false,bool removePreference=true){
   exe=Path.GetFullPath(exe);
   foreach(string path in new[]{MenuPath(test),VerbsPath(test)}){bool owned;using(RegistryKey key=Registry.CurrentUser.OpenSubKey(path))owned=key!=null&&String.Equals(key.GetValue("PaperNestOwner") as string,exe,StringComparison.OrdinalIgnoreCase);if(owned)Registry.CurrentUser.DeleteSubKeyTree(path,false);}
   if(removePreference){bool owned;using(RegistryKey key=Registry.CurrentUser.OpenSubKey(Preferences(test)))owned=key!=null&&String.Equals(key.GetValue("Owner") as string,exe,StringComparison.OrdinalIgnoreCase);if(owned)Registry.CurrentUser.DeleteSubKeyTree(Preferences(test),false);}
   if(!test)RefreshShell();
  }
  [DllImport("shell32.dll")] static extern void SHChangeNotify(uint id,uint flags,IntPtr item1,IntPtr item2);
  static void RefreshShell(){SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);}
 }
}
