using System;
using System.Linq;
using PaperNest;
class DesktopLaunch {
 [STAThread]static int Main(string[] args){try{Console.WriteLine("Caller package: "+WindowsHost.PackageName);if(args.Length>0)WindowsHost.LaunchDesktop(args[0],args.Skip(1).ToArray());return 0;}catch(Exception ex){Console.Error.WriteLine(ex);return 1;}}
}
