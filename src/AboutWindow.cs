using System;
using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace PaperNest {
    public sealed class AboutWindow : DialogBase {
        public AboutWindow(NotesApp app):base(L.T("关于纸间"),600,690) {
            Height=Math.Min(690,SystemParameters.WorkArea.Height-35);
            Grid brand=new Grid { Height=98,Margin=new Thickness(0,0,0,18) };
            Grid icon=new Grid { Width=74,Height=83,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(3,12,0,0) };
            icon.Children.Add(new PaperSurface{ColorName="butter"});icon.Children.Add(new DecorationSurface{Kind="tape",Margin=new Thickness(0,-17,0,0),Height=60,VerticalAlignment=VerticalAlignment.Top});brand.Children.Add(icon);
            StackPanel name=new StackPanel{Margin=new Thickness(90,10,0,0)};name.Children.Add(Look.Label("PaperNest · 纸间",30,Look.Ink));name.Children.Add(Look.Label(L.T("把灵感，轻轻留在桌面。"),13,Look.Muted));brand.Children.Add(name);Body.Children.Add(brand);
            StackPanel info=Section(L.T("独立桌面便签"));
            info.Children.Add(Look.Label(L.T("作者与设计")+"  Kaysar",17,Look.Ink));
            TextBlock version=Look.Label(L.T("版本")+"  "+Assembly.GetExecutingAssembly().GetName().Version.ToString(3)+"  ·  Windows 10 / 11",12,Look.Muted);version.Margin=new Thickness(0,8,0,0);info.Children.Add(version);
            StackPanel privacy=Section(L.T("你的纸片，留在你的电脑"));privacy.Children.Add(Look.Label(L.T("文字、图片、位置和锁定状态自动保存在本机。无需账号，不自动上传便签，也不提供云同步。"),13,Look.Ink));
            TextBlock backup=Look.Label(L.T("换电脑或重装 Windows 前，请在设置中导出备份。卸载程序会保留本地便签。"),12,Look.Muted);backup.Margin=new Thickness(0,8,0,0);privacy.Children.Add(backup);
            StackPanel shortcuts=Section(L.T("顺手的小快捷键"));shortcuts.Children.Add(Look.Label(L.T("Ctrl+N  新建文字便签\nCtrl+Shift+N  新建照片便签\nCtrl+L  锁定 / 解锁当前便签\nCtrl+滚轮  调整内容大小\nCtrl+W  收起当前便签"),13,Look.Ink));
            StackPanel credits=Section(L.T("致谢与授权"));credits.Children.Add(Look.Label("LXGW WenKai Lite · SIL Open Font License 1.1",12,Look.Ink));credits.Children.Add(Look.Label(L.T("手写字体保留原作者署名与完整授权。示例日落图片由 AI 生成。"),12,Look.Muted));
            TextBlock copyright=Look.Label("© "+DateTime.Now.Year+" Kaysar · PaperNest",11,Look.Muted);Body.Children.Add(copyright);
            StackPanel buttons=new StackPanel {Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};
            buttons.Children.Add(Look.ActionButton(L.T("字体授权"),delegate{string file=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Fonts","OFL.txt");if(File.Exists(file))Process.Start(new ProcessStartInfo(file){UseShellExecute=true});},false));
            buttons.Children.Add(Look.ActionButton(L.T("关闭"),Close,true));SetFooter(buttons);
        }
    }
}
