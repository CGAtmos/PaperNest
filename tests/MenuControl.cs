using System;
using System.IO;
using PaperNest;
class MenuControl {
 static int Main(string[] args){try{string exe=Path.GetFullPath(args[1]);bool test=Array.IndexOf(args,"--live")<0;if(Array.IndexOf(args,"--en")>=0)L.Set("en");if(Array.IndexOf(args,"--zh")>=0)L.Set("zh-CN");if(args[0]=="disable")DesktopMenu.SetEnabled(false,exe,test);else if(args[0]=="enable")DesktopMenu.SetEnabled(true,exe,test);else if(args[0]=="reset"){DesktopMenu.RemoveOwned(exe,test,false);DesktopMenu.SetEnabled(true,exe,test);}else if(args[0]=="wrong-owner"){DesktopMenu.RemoveOwned(Path.Combine(Path.GetDirectoryName(exe),"Unrelated.exe"),test);if(!DesktopMenu.IsEnabled(exe,test))throw new Exception("An unrelated owner removed the menu");}else throw new ArgumentException("Unknown action");return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
