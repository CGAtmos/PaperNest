using System;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;
class DesktopMenuProbe {
 [ComImport,Guid("FC4801A3-2BA9-11CF-A229-00AA003D7352"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IObjectWithSite {void SetSite([MarshalAs(UnmanagedType.IUnknown)]object site);void GetSite(ref Guid iid,out IntPtr site);}
 static object DesktopContext() {
  dynamic windows=Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("9BA05972-F6A8-11CF-A442-00A0C90A8F39")));
  object location=0,root=0;int hwnd;object desktop=windows.FindWindowSW(ref location,ref root,8,out hwnd,1);
  Guid contextId=new Guid("000214E4-0000-0000-C000-000000000046");IntPtr item=IntPtr.Zero;
  try {IShellFolder folder;Marshal.ThrowExceptionForHR(SHGetDesktopFolder(out folder));try{Marshal.ThrowExceptionForHR(folder.CreateViewObject(new IntPtr(hwnd),ref contextId,out item));object context=Marshal.GetObjectForIUnknown(item);
   // Cascading static verbs need a Shell host site; without it a test host
   // can see the placeholder yet fail to populate the submenu.
   IObjectWithSite site=context as IObjectWithSite;if(site==null)throw new InvalidOperationException("Desktop menu has no host site");site.SetSite(desktop);return context;}finally{Marshal.ReleaseComObject(folder);}}
  finally{if(item!=IntPtr.Zero)Marshal.Release(item);Marshal.ReleaseComObject(desktop);Marshal.ReleaseComObject(windows);}
 }
 [ComImport,Guid("000214E6-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IShellFolder {
  [PreserveSig]int ParseDisplayName(IntPtr h,IntPtr b,[MarshalAs(UnmanagedType.LPWStr)]string n,ref uint eaten,out IntPtr p,ref uint attrs);
  [PreserveSig]int EnumObjects(IntPtr h,uint f,out IntPtr e);
  [PreserveSig]int BindToObject(IntPtr p,IntPtr b,ref Guid i,out IntPtr o);
  [PreserveSig]int BindToStorage(IntPtr p,IntPtr b,ref Guid i,out IntPtr o);
  [PreserveSig]int CompareIDs(IntPtr l,IntPtr a,IntPtr b);
  [PreserveSig]int CreateViewObject(IntPtr h,ref Guid i,out IntPtr o);
  [PreserveSig]int GetAttributesOf(uint c,IntPtr p,ref uint a);
  [PreserveSig]int GetUIObjectOf(IntPtr h,uint c,IntPtr p,ref Guid i,IntPtr r,out IntPtr o);
  [PreserveSig]int GetDisplayNameOf(IntPtr p,uint f,out IntPtr s);
  [PreserveSig]int SetNameOf(IntPtr h,IntPtr p,[MarshalAs(UnmanagedType.LPWStr)]string n,uint f,out IntPtr o);
 }
 [ComImport,Guid("000214E4-0000-0000-C000-000000000046"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IContextMenu {
  [PreserveSig]int QueryContextMenu(IntPtr menu,uint index,uint first,uint last,uint flags);
  [PreserveSig]int InvokeCommand(IntPtr info);
  [PreserveSig]int GetCommandString(UIntPtr id,uint flags,IntPtr reserved,IntPtr text,uint max);
 }
 [ComImport,Guid("BCFCE0A0-EC17-11D0-8D10-00A0C90F2719"),InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] interface IContextMenu3 {
  [PreserveSig]int QueryContextMenu(IntPtr menu,uint index,uint first,uint last,uint flags);
  [PreserveSig]int InvokeCommand(IntPtr info);
  [PreserveSig]int GetCommandString(UIntPtr id,uint flags,IntPtr reserved,IntPtr text,uint max);
  [PreserveSig]int HandleMenuMsg(uint msg,IntPtr w,IntPtr l);
  [PreserveSig]int HandleMenuMsg2(uint msg,IntPtr w,IntPtr l,out IntPtr result);
 }
 [DllImport("shell32.dll")]static extern int SHGetDesktopFolder(out IShellFolder folder);
 [DllImport("user32.dll")]static extern IntPtr CreatePopupMenu();
 [DllImport("user32.dll")]static extern bool DestroyMenu(IntPtr menu);
 [DllImport("user32.dll")]static extern int GetMenuItemCount(IntPtr menu);
 [DllImport("user32.dll")]static extern IntPtr GetSubMenu(IntPtr menu,int position);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int GetMenuString(IntPtr menu,uint position,StringBuilder text,int max,uint flags);
 static string Caption(IntPtr menu,int pos){StringBuilder s=new StringBuilder(512);GetMenuString(menu,(uint)pos,s,s.Capacity,0x400);return s.ToString();}
 [STAThread]static int Main(string[] args){object obj=null;IntPtr popup=IntPtr.Zero;try{
  obj=DesktopContext();int hr;IContextMenu context=(IContextMenu)obj;
  popup=CreatePopupMenu();hr=context.QueryContextMenu(popup,0,1,0x7fff,0x1000);Marshal.ThrowExceptionForHR(hr);
  IContextMenu3 rootMenu=obj as IContextMenu3;if(rootMenu!=null){IntPtr rootResult;rootMenu.HandleMenuMsg2(0x117,popup,IntPtr.Zero,out rootResult);}
  List<string> children=new List<string>();string parent="";bool hasSubmenu=false,hasAdvanced=false;int initHr=-1,before=-1;
  for(int i=0;i<GetMenuItemCount(popup);i++){string label=Caption(popup,i);if(label.IndexOf("纸间便签",StringComparison.Ordinal)>=0){parent=label;IntPtr submenu=GetSubMenu(popup,i);hasSubmenu=submenu!=IntPtr.Zero;before=GetMenuItemCount(submenu);IContextMenu3 advanced=obj as IContextMenu3;hasAdvanced=advanced!=null;if(advanced!=null){IntPtr result;initHr=advanced.HandleMenuMsg2(0x117,submenu,new IntPtr(i),out result);}if(submenu!=IntPtr.Zero)for(int j=0;j<GetMenuItemCount(submenu);j++)children.Add(Caption(submenu,j));break;}}
  bool passed=parent.Length>0&&children.Count==3&&children[0]=="新建文字便签"&&children[1]=="新建待办清单"&&children[2]=="新建照片便签";
  string json="{\"passed\":"+passed.ToString().ToLowerInvariant()+",\"found\":"+(parent.Length>0?"true":"false")+",\"parent\":\""+parent+"\",\"hasSubmenu\":"+hasSubmenu.ToString().ToLowerInvariant()+",\"advanced\":"+hasAdvanced.ToString().ToLowerInvariant()+",\"initHr\":"+initHr+",\"before\":"+before+",\"children\":["+String.Join(",",children.ConvertAll(s=>"\""+s.Replace("\"","\\\"")+"\""))+" ]}";if(args.Length>0)System.IO.File.WriteAllText(args[0],json);else Console.WriteLine(json);return passed?0:2;
 }catch(Exception ex){if(args.Length>0)System.IO.File.WriteAllText(args[0],ex.ToString());else Console.Error.WriteLine(ex);return 1;}finally{if(popup!=IntPtr.Zero)DestroyMenu(popup);if(obj!=null)Marshal.ReleaseComObject(obj);}}
}
