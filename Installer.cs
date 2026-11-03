using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using PaperNest;

[assembly: AssemblyTitle("纸间 · 桌面便签 安装程序")]
[assembly: AssemblyProduct("PaperNest Setup")]
[assembly: AssemblyVersion("1.7.1.0")]
[assembly: AssemblyFileVersion("1.7.1.0")]
[assembly: System.Runtime.Versioning.TargetFramework(".NETFramework,Version=v4.8",FrameworkDisplayName=".NET Framework 4.8")]

static class Setup {
    const string Id="PaperNest.DesktopNotes";
    const string Marker=".papernest-install";
    static string Get(string[] args,string key,string fallback){int i=Array.IndexOf(args,key);return i>=0&&i+1<args.Length?args[i+1]:fallback;}
    public static string DefaultTarget {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","PaperNest");}}
    static bool ExistingStartup(string target){string expected="\""+Path.Combine(target,"PaperNest.exe")+"\" --background";using(RegistryKey run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){return run!=null&&String.Equals(run.GetValue(Id) as string,expected,StringComparison.OrdinalIgnoreCase);}}
    static string RegistryPath(bool test){return @"Software\Microsoft\Windows\CurrentVersion\Uninstall\"+Id+(test?".QA":"");}
    public static void ValidateTarget(string target) {
        string full=Path.GetFullPath(target).TrimEnd(Path.DirectorySeparatorChar);
        if(String.Equals(full,Path.GetPathRoot(full).TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase)||full.Length<12)throw new InvalidOperationException("Invalid installation directory");
        string[] protectedFolders={Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)};
        foreach(string folder in protectedFolders)if(String.Equals(full,folder.TrimEnd(Path.DirectorySeparatorChar),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("The installation directory must be a dedicated app folder");
        if(!Path.GetFileName(full).StartsWith("PaperNest",StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Use a dedicated PaperNest installation folder");
    }
    static string Inside(string root,string relative) {
        string full=Path.GetFullPath(Path.Combine(root,relative));
        if(!full.StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("File path escapes installation directory");
        return full;
    }
    static void Shortcut(string file,string target,string arguments) {
        Directory.CreateDirectory(Path.GetDirectoryName(file));Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic link=shell.CreateShortcut(file);
        link.TargetPath=target;link.WorkingDirectory=Path.GetDirectoryName(target);link.Arguments=arguments;link.Description="纸间 · 有纸张温度的桌面便签";link.IconLocation=target+",0";link.Save();Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);
    }
    static string DesktopLink(bool test,string target){return test?Path.Combine(target,"TestLinks","纸间.lnk"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"纸间 · 桌面便签.lnk");}
    static string MenuLink(bool test,string target){return test?Path.Combine(target,"TestLinks","Menu.lnk"):Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"纸间 · 桌面便签.lnk");}
    static void StopRunning(string target) {
        string exe=Path.Combine(target,"PaperNest.exe");List<Process> own=new List<Process>();
        foreach(Process p in Process.GetProcessesByName("PaperNest")) {try{if(String.Equals(p.MainModule.FileName,exe,StringComparison.OrdinalIgnoreCase))own.Add(p);}catch{}}
        if(own.Count==0)return;
        using(Process p=Process.Start(new ProcessStartInfo(exe,"--exit"){UseShellExecute=false,CreateNoWindow=true})){p.WaitForExit(3500);}
        foreach(Process p in own)if(!p.WaitForExit(5000))throw new IOException("纸间仍在保存。请从托盘彻底退出后重试。");
    }
    public static void Install(string target,bool desktop,bool startup,bool test,bool? contextMenu=null) {
        ValidateTarget(target);target=Path.GetFullPath(target);
        if(Directory.Exists(target)&&Directory.EnumerateFileSystemEntries(target).Any()&&!File.Exists(Path.Combine(target,Marker)))throw new IOException("安装目录已有其他文件，请选择独立文件夹。");
        StopRunning(target);
        Directory.CreateDirectory(target);
        List<string> files=new List<string>();
        using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("Payload.zip")) {
            if(resource==null)throw new InvalidDataException("安装包内容缺失。");
            using(ZipArchive archive=new ZipArchive(resource,ZipArchiveMode.Read)) {
                foreach(ZipArchiveEntry entry in archive.Entries) {
                    if(String.IsNullOrEmpty(entry.Name))continue;
                    string file=Inside(target,entry.FullName);Directory.CreateDirectory(Path.GetDirectoryName(file));
                    using(Stream source=entry.Open())using(FileStream dest=File.Create(file))source.CopyTo(dest);
                    files.Add(entry.FullName.Replace('/',Path.DirectorySeparatorChar));
                }
            }
        }
        string uninstaller=Path.Combine(target,"Uninstall.exe");
        if(!String.Equals(Assembly.GetExecutingAssembly().Location,uninstaller,StringComparison.OrdinalIgnoreCase))File.Copy(Assembly.GetExecutingAssembly().Location,uninstaller,true);
        files.Add("Uninstall.exe");File.WriteAllLines(Path.Combine(target,"installed-files.txt"),files,new UTF8Encoding(false));File.WriteAllText(Path.Combine(target,Marker),Id+(test?".QA":""));
        string exe=Path.Combine(target,"PaperNest.exe");if(desktop)Shortcut(DesktopLink(test,target),exe,"");Shortcut(MenuLink(test,target),exe,"");
        using(RegistryKey key=Registry.CurrentUser.CreateSubKey(RegistryPath(test))) {
            key.SetValue("DisplayName",test?"PaperNest QA":"纸间 · 桌面便签");key.SetValue("DisplayVersion","1.7.1");key.SetValue("Publisher","Kaysar");key.SetValue("InstallLocation",target);key.SetValue("DisplayIcon",exe+",0");
            key.SetValue("UninstallString","\""+uninstaller+"\" --uninstall"+(test?" --test":""));key.SetValue("QuietUninstallString","\""+uninstaller+"\" --uninstall --quiet"+(test?" --test":""));key.SetValue("NoModify",1);key.SetValue("NoRepair",1);key.SetValue("EstimatedSize",(int)(Directory.GetFiles(target,"*",SearchOption.AllDirectories).Sum(f=>new FileInfo(f).Length)/1024));
        }
        if(!test)using(RegistryKey run=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(startup)run.SetValue(Id,"\""+exe+"\" --background");else{string existing=run.GetValue(Id) as string;if(existing!=null&&existing.IndexOf(exe,StringComparison.OrdinalIgnoreCase)>=0)run.DeleteValue(Id,false);}}
        DesktopMenu.SetEnabled(contextMenu??DesktopMenu.Preferred(test),exe,test);
        if(!test)L.SavePreference();
    }
    static void RemoveShortcut(string file,string expected) {
        if(!File.Exists(file))return;Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic link=shell.CreateShortcut(file);string target=link.TargetPath as string;Marshal.FinalReleaseComObject(link);Marshal.FinalReleaseComObject(shell);if(String.Equals(target,expected,StringComparison.OrdinalIgnoreCase))File.Delete(file);
    }
    public static void Uninstall(string target,bool test) {
        ValidateTarget(target);target=Path.GetFullPath(target);
        string marker=Path.Combine(target,Marker);if(!File.Exists(marker)||File.ReadAllText(marker)!=(Id+(test?".QA":"")))throw new IOException(L.T("未找到纸间的安装标记，未删除任何文件。"));
        StopRunning(target);string exe=Path.Combine(target,"PaperNest.exe");
        DesktopMenu.RemoveOwned(exe,test);
        RemoveShortcut(DesktopLink(test,target),exe);RemoveShortcut(MenuLink(test,target),exe);
        if(!test)using(RegistryKey run=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run",true)){if(run!=null){string value=run.GetValue(Id) as string;if(value!=null&&value.IndexOf(exe,StringComparison.OrdinalIgnoreCase)>=0)run.DeleteValue(Id,false);}}
        using(RegistryKey key=Registry.CurrentUser.OpenSubKey(RegistryPath(test))) {
            string registered=key==null?null:key.GetValue("InstallLocation") as string;
            if(String.Equals(registered,target,StringComparison.OrdinalIgnoreCase))Registry.CurrentUser.DeleteSubKeyTree(RegistryPath(test),false);
        }
        string manifest=Path.Combine(target,"installed-files.txt");
        if(!File.Exists(manifest))throw new IOException(L.T("安装文件清单缺失，程序文件已保留。"));
        foreach(string relative in File.ReadAllLines(manifest)) {string path=Inside(target,relative);if(File.Exists(path))File.Delete(path);}
        File.Delete(manifest);File.Delete(marker);
        foreach(string folder in Directory.GetDirectories(target,"*",SearchOption.AllDirectories).OrderByDescending(p=>p.Length)) {Inside(target,folder.Substring(target.Length+1));if(!Directory.EnumerateFileSystemEntries(folder).Any())Directory.Delete(folder);}
        if(!Directory.EnumerateFileSystemEntries(target).Any())Directory.Delete(target);
    }
    [STAThread] public static int Main(string[] args) {
        L.Set(Get(args,"--language",L.PreferredLanguage));
        bool test=args.Contains("--test");bool quiet=args.Contains("--quiet");bool uninstall=args.Contains("--uninstall")||args.Contains("--uninstall-worker");
        string own=Assembly.GetExecutingAssembly().Location;
        string target=Get(args,"--target",uninstall?Path.GetDirectoryName(own):DefaultTarget);
        string report=Get(args,"--report","");
        try {
            if(!test&&WindowsHost.HandoffIfRedirected(own,args,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"PaperNest","Data")))return 0;
            ValidateTarget(target);
            if(uninstall&&!args.Contains("--uninstall-worker")) {
                string temp=Path.Combine(Path.GetTempPath(),"PaperNest-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(own,temp);
                ProcessStartInfo start=new ProcessStartInfo(temp,"--uninstall-worker --target \""+target+"\" --parent "+Process.GetCurrentProcess().Id+(test?" --test":"")+(quiet?" --quiet":"")+(report.Length>0?" --report \""+report+"\"":"")){UseShellExecute=false,CreateNoWindow=quiet};
                Process.Start(start);return 0;
            }
            if(args.Contains("--parent")){int pid; if(Int32.TryParse(Get(args,"--parent","0"),out pid))try{Process.GetProcessById(pid).WaitForExit(7000);}catch(ArgumentException){}}
            if(quiet) {
                if(uninstall)Uninstall(target,test);else Install(target,!args.Contains("--no-shortcuts"),args.Contains("--startup")||(!args.Contains("--no-startup")&&ExistingStartup(target)),test,args.Contains("--no-context-menu")?(bool?)false:args.Contains("--context-menu")?(bool?)true:null);
                if(report.Length>0)File.WriteAllText(report,"{\"success\":true,\"operation\":\""+(uninstall?"uninstall":"install")+"\"}");
                if(!uninstall&&!args.Contains("--no-launch"))Process.Start(Path.Combine(target,"PaperNest.exe"));return 0;
            }
            Application app=new Application();app.Run(new SetupWindow(target,test,uninstall));return 0;
        }catch(Exception ex) {
            if(report.Length>0)File.WriteAllText(report,"{\"success\":false,\"error\":\""+ex.Message.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r"," ").Replace("\n"," ")+"\"}");
            if(!quiet)MessageBox.Show(L.T("操作未完成。\n")+ex.Message,L.T("纸间安装程序"),MessageBoxButton.OK,MessageBoxImage.Information);return 1;
        }
    }
    sealed class SetupWindow : Window {
        readonly string target;readonly bool test,uninstall;
        CheckBox desktop,startup,launch,context;
        TextBlock status;
        Button main;
        bool busy,completed;
        public SetupWindow(string location,bool testing,bool removal) {
            target=location;test=testing;uninstall=removal;Title=uninstall?L.T("卸载纸间"):L.T("安装纸间 · 桌面便签");Width=550;Height=Math.Min(650,SystemParameters.WorkArea.Height-30);ResizeMode=ResizeMode.NoResize;WindowStartupLocation=WindowStartupLocation.CenterScreen;Background=Look.Brush("#FBF8F0");FontFamily=new FontFamily("Microsoft YaHei UI");
            Closing+=delegate(object sender,System.ComponentModel.CancelEventArgs e){if(busy)e.Cancel=true;};
            Grid layout=new Grid();layout.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });layout.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
            StackPanel body=new StackPanel { Margin=new Thickness(34,27,34,12) };layout.Children.Add(new ScrollViewer { Content=body,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled });
            ComboBox language=new ComboBox { ItemsSource=new[]{"简体中文","English"},SelectedIndex=L.English?1:0,MinWidth=125,MinHeight=30,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,0,0,8) };body.Children.Add(language);
            language.SelectionChanged+=delegate{if(busy||completed)return;L.Set(language.SelectedIndex==1?"en":"zh-CN");SetupWindow next=new SetupWindow(target,test,uninstall);next.WindowStartupLocation=WindowStartupLocation.Manual;next.Left=Left;next.Top=Top;if(!uninstall){next.desktop.IsChecked=desktop.IsChecked;next.startup.IsChecked=startup.IsChecked;next.launch.IsChecked=launch.IsChecked;next.context.IsChecked=context.IsChecked;}next.Show();Close();};
            StackPanel footer=new StackPanel { Margin=new Thickness(34,9,34,24) };Grid.SetRow(footer,1);layout.Children.Add(footer);Content=layout;
            Grid logo=new Grid { Height=85,HorizontalAlignment=HorizontalAlignment.Left,Width=92,Margin=new Thickness(0,0,0,17) };logo.Children.Add(new PaperSurface { ColorName="butter" });logo.Children.Add(new DecorationSurface { Kind="tape",Margin=new Thickness(0,-10,0,0) });body.Children.Add(logo);
            TextBlock heading=Look.Label(uninstall?L.T("暂时和纸间说再见"):L.T("给桌面，贴一点生活"),29,Look.Ink);heading.FontFamily=Look.Handwriting;heading.Margin=new Thickness(0,0,0,9);body.Children.Add(heading);
            TextBlock subtitle=Look.Label(uninstall?L.T("会移除程序和入口，保留你的便签、图片和备份。"):L.T("真实纸感便签 · 写字贴图 · 安静待在右下角托盘"),12,Look.Muted);subtitle.Margin=new Thickness(0,0,0,23);body.Children.Add(subtitle);
            Border pathCard=new Border { Background=Brushes.White,CornerRadius=new CornerRadius(10),Padding=new Thickness(14),Margin=new Thickness(0,0,0,20) };StackPanel pathCopy=new StackPanel();pathCopy.Children.Add(Look.Label(uninstall?L.T("程序位置"):L.T("安装位置 · 当前 Windows 用户"),11,Look.Muted));TextBlock path=Look.Label(target,12,Look.Ink);path.Margin=new Thickness(0,5,0,0);pathCopy.Children.Add(path);pathCard.Child=pathCopy;body.Children.Add(pathCard);
            if(!uninstall) {
                desktop=new CheckBox { Content=L.T("创建桌面快捷方式"),IsChecked=true,Margin=new Thickness(0,0,0,10) };startup=new CheckBox { Content=L.T("随 Windows 登录一起启动"),IsChecked=ExistingStartup(target),Margin=new Thickness(0,0,0,10) };launch=new CheckBox { Content=L.T("安装完成后打开纸间"),IsChecked=true,Margin=new Thickness(0,0,0,16) };body.Children.Add(desktop);body.Children.Add(startup);body.Children.Add(launch);
                context=new CheckBox { Content=L.T("添加桌面右键菜单（文字 / 待办 / 照片）"),IsChecked=DesktopMenu.Preferred(test),Margin=new Thickness(0,0,0,10) };body.Children.Insert(body.Children.IndexOf(launch),context);
            }
            status=Look.Label(uninstall?L.T("你的便签数据会留在本机，重新安装可继续使用。"):L.T("Windows 10 / 11 · 本地保存 · 无需管理员权限"),11,Look.Muted);status.Margin=new Thickness(0,4,0,13);footer.Children.Add(status);
            main=Look.ActionButton(uninstall?L.T("卸载程序"):L.T("安装纸间"),Run,true);main.HorizontalAlignment=HorizontalAlignment.Stretch;main.Margin=new Thickness(0);footer.Children.Add(main);
        }
        async void Run() {
            if(completed){Close();return;}busy=true;main.IsEnabled=false;status.Text=uninstall?L.T("正在卸载程序…"):L.T("正在把纸间放到你的电脑…");
            bool createDesktop=!uninstall&&desktop.IsChecked==true;bool enableStartup=!uninstall&&startup.IsChecked==true;bool open=!uninstall&&launch.IsChecked==true;
            bool enableMenu=!uninstall&&context.IsChecked==true;
            try {
                await Task.Run(delegate { if(uninstall)Uninstall(target,test);else Install(target,createDesktop,enableStartup,test,enableMenu); });
                busy=false;completed=true;
                if(uninstall){status.Text=L.T("纸间已卸载，便签数据仍保留。");main.Content=L.T("完成");main.IsEnabled=true;}
                else {status.Text=L.T("安装好了，愿每张纸都装得下你的小想法。");if(open){Process.Start(Path.Combine(target,"PaperNest.exe"));Close();}else{main.Content=L.T("完成");main.IsEnabled=true;}}
            }catch(Exception ex){busy=false;status.Text=ex.Message;main.IsEnabled=true;}
        }
    }
}
