using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PaperNest {
    public class DialogBase : Window {
        protected StackPanel Body;
        Border footer;
        public DialogBase(string title,double width,double height) {
            Title=title;Width=width;Height=height;MinWidth=width-70;MinHeight=380;Background=Look.Brush("#FBF8F0");WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new FontFamily("Microsoft YaHei UI");Foreground=Look.Ink;
            string icon=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"PaperNest.ico");if(File.Exists(icon))Icon=BitmapFrame.Create(new Uri(icon));
            ScrollViewer scroll=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled };
            Body=new StackPanel { Margin=new Thickness(30,23,30,20) };scroll.Content=Body;
            Grid root=new Grid();root.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });root.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });root.Children.Add(scroll);
            footer=new Border { Padding=new Thickness(30,14,25,17),Background=Look.Brush("#FBF8F0"),BorderBrush=Look.Brush("#EAE4D9"),BorderThickness=new Thickness(0,1,0,0),Visibility=Visibility.Collapsed };Grid.SetRow(footer,1);root.Children.Add(footer);Content=root;
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){Close();e.Handled=true;}};
        }
        protected void SetFooter(UIElement content){footer.Child=content;footer.Visibility=Visibility.Visible;}
        protected void Header(string title,string subtitle) {
            TextBlock heading=Look.Label(title,29,Look.Ink);heading.FontFamily=Look.Handwriting;heading.Margin=new Thickness(0,0,0,5);Body.Children.Add(heading);
            TextBlock sub=Look.Label(subtitle,12,Look.Muted);sub.Margin=new Thickness(0,0,0,23);Body.Children.Add(sub);
        }
        protected StackPanel Section(string title) {
            TextBlock label=Look.Label(title,12,Look.Muted);label.Margin=new Thickness(1,0,0,8);Body.Children.Add(label);
            StackPanel panel=new StackPanel { Margin=new Thickness(16,13,16,13) };Body.Children.Add(new Border { Background=Brushes.White,BorderBrush=Look.Brush("#EDE7DC"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(12),Child=panel,Margin=new Thickness(0,0,0,20) });return panel;
        }
    }
    public sealed class SettingsWindow : DialogBase {
        readonly NotesApp app;
        CheckBox startup,show,texture,desktopMenu;
        Slider zoom;
        ComboBox colors,language;
        public SettingsWindow(NotesApp owner):base(L.T("纸间 · 设置"),564,705) {
            app=owner;Header(L.T("把小事，轻轻贴在桌面"),L.T("纸间 1.7.1  ·  自动保存，让每张纸都安心留着"));
            StackPanel languages=Section(L.T("界面语言 / Language"));
            language=new ComboBox{ItemsSource=new[]{"简体中文","English"},SelectedIndex=L.English?1:0,MinHeight=34,Padding=new Thickness(8),FontSize=14};languages.Children.Add(language);
            TextBlock languageHint=Look.Label(L.T("保存后立即切换，便签内容保持原样。"),12,Look.Muted);languageHint.Margin=new Thickness(0,8,0,0);languages.Children.Add(languageHint);
            StackPanel behavior=Section(L.T("启动与收起"));
            startup=Option(behavior,L.T("随 Windows 登录一起启动"),L.T("可以随时关闭，默认不自动开启。"),app.Store.Data.StartWithWindows);
            show=Option(behavior,L.T("启动时展开未收起的便签"),L.T("关闭后仅显示右下角托盘图标。"),app.Store.Data.ShowOnLaunch);
            desktopMenu=Option(behavior,L.T("在桌面右键显示“纸间便签”"),L.T("快速新建文字、待办、照片；Windows 11 可点“显示更多选项”。"),DesktopMenu.IsEnabled(Startup.ExePath));
            TextBlock tip=Look.Label(L.T("便签上的 × 只收起这张纸。\n要完全关闭程序，请右键托盘图标 → 彻底退出纸间。"),12,Look.Muted);tip.LineHeight=20;tip.Margin=new Thickness(0,9,0,0);behavior.Children.Add(tip);
            StackPanel style=Section(L.T("纸张与文字"));
            texture=Option(style,L.T("细腻纸张纹理"),L.T("纸张与翘角随尺寸重新绘制。"),app.Store.Data.Texture);
            Grid colorRow=new Grid { Margin=new Thickness(0,11,0,11) };colorRow.ColumnDefinitions.Add(new ColumnDefinition());colorRow.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(160) });colorRow.Children.Add(Look.Label(L.T("新便签默认颜色"),13,Look.Ink));
            colors=new ComboBox { ItemsSource=new[]{L.T("奶油黄"),L.T("鼠尾草绿"),L.T("蜜桃粉"),L.T("雾蓝"),L.T("浅紫"),L.T("暖白")},SelectedIndex=Array.IndexOf(new[]{"butter","sage","rose","sky","lilac","cream"},app.Store.Data.DefaultColor),MinHeight=30 };Grid.SetColumn(colors,1);colorRow.Children.Add(colors);style.Children.Add(colorRow);
            TextBlock zoomText=Look.Label(L.T("新便签内容大小 · ")+Math.Round(app.Store.Data.DefaultZoom*100)+"%",13,Look.Ink);style.Children.Add(zoomText);
            zoom=new Slider { Minimum=.7,Maximum=2,Value=app.Store.Data.DefaultZoom,TickFrequency=.1,IsSnapToTickEnabled=true,Margin=new Thickness(0,9,0,0) };zoom.ValueChanged+=delegate{zoomText.Text=L.T("新便签内容大小 · ")+Math.Round(zoom.Value*100)+"%";};style.Children.Add(zoom);
            StackPanel data=Section(L.T("本地数据"));data.Children.Add(Look.Label(L.T("便签内容、图片和窗口位置都保存在这台电脑。"),12,Look.Muted));
            StackPanel dataButtons=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,12,0,0) };dataButtons.Children.Add(Look.ActionButton(L.T("找回便签"),app.OpenHistory,false));dataButtons.Children.Add(Look.ActionButton(L.T("导出备份"),Export,false));dataButtons.Children.Add(Look.ActionButton(L.T("数据文件夹"),delegate { Process.Start("explorer.exe","\""+app.Store.Root+"\""); },false));data.Children.Add(dataButtons);
            StackPanel bottom=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };bottom.Children.Add(Look.ActionButton(L.T("关于"),app.OpenAbout,false));bottom.Children.Add(Look.ActionButton(L.T("取消"),Close,false));bottom.Children.Add(Look.ActionButton(L.T("保存设置"),Save,true));SetFooter(bottom);
        }
        static CheckBox Option(StackPanel panel,string text,string subtitle,bool value) {
            StackPanel content=new StackPanel();content.Children.Add(Look.Label(text,13,Look.Ink));TextBlock sub=Look.Label(subtitle,11,Look.Muted);sub.Margin=new Thickness(0,4,0,0);content.Children.Add(sub);
            CheckBox box=new CheckBox { Content=content,IsChecked=value,Margin=new Thickness(0,4,0,11),VerticalContentAlignment=VerticalAlignment.Center,Cursor=Cursors.Hand };
            box.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse(@"<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'><Grid><Border x:Name='track' Width='32' Height='19' CornerRadius='10' Background='#D9D8CE' HorizontalAlignment='Left' VerticalAlignment='Center'><Ellipse x:Name='knob' Width='15' Height='15' Fill='White' Margin='2' HorizontalAlignment='Left'/></Border><ContentPresenter Margin='45,0,0,0' VerticalAlignment='Center'/></Grid><ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='track' Property='Background' Value='#657B65'/><Setter TargetName='knob' Property='HorizontalAlignment' Value='Right'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            panel.Children.Add(box);return box;
        }
        void Save() {
            string oldLanguage=L.Code;
            try {
                L.Set(language.SelectedIndex==1?"en":"zh-CN");
                if(!app.TestMode){Startup.Set(startup.IsChecked==true);DesktopMenu.SetEnabled(desktopMenu.IsChecked==true,Startup.ExePath);}
                app.Store.Data.StartWithWindows=startup.IsChecked==true;app.Store.Data.ShowOnLaunch=show.IsChecked==true;app.Store.Data.Texture=texture.IsChecked==true;app.Store.Data.DefaultZoom=zoom.Value;app.Store.Data.DefaultColor=new[]{"butter","sage","rose","sky","lilac","cream"}[Math.Max(0,colors.SelectedIndex)];app.Store.Data.Language=L.Code;
                foreach(NoteWindow note in app.Notes.Values)note.ApplyAppearance();
                if(!app.SaveAll())throw new IOException(app.LastSaveError);
                if(!app.TestMode)L.SavePreference();app.ChangeLanguage(L.Code);Close();
            }
            catch(Exception ex){L.Set(oldLanguage);app.Store.Data.Language=oldLanguage;app.Store.Log(ex);MessageBox.Show(L.T("设置未能保存。\n")+ex.Message,L.T("纸间"));}
        }
        void Export() {
            SaveFileDialog dialog=new SaveFileDialog { Filter=L.T("纸间备份 (*.zip)|*.zip"),FileName=L.T("纸间备份-")+DateTime.Now.ToString("yyyyMMdd-HHmm")+".zip",Title=L.T("导出便签备份") };
            if(dialog.ShowDialog(this)!=true)return;
            try {
                string target=Path.GetFullPath(dialog.FileName);if(target.StartsWith(app.Store.Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException(L.T("请选择数据文件夹之外的位置保存备份。"));
                if(!app.SaveAll())throw new IOException(app.LastSaveError);using(FileStream file=new FileStream(target,FileMode.Create,FileAccess.ReadWrite))using(ZipArchive zip=new ZipArchive(file,ZipArchiveMode.Create)) {
                    foreach(string source in Directory.GetFiles(app.Store.Root,"*",SearchOption.AllDirectories)) {
                        if(source.EndsWith(".new",StringComparison.OrdinalIgnoreCase)||source.EndsWith(".lock",StringComparison.OrdinalIgnoreCase))continue;
                        zip.CreateEntryFromFile(source,source.Substring(app.Store.Root.Length+1),CompressionLevel.Optimal);
                    }
                }
                MessageBox.Show(L.T("备份已导出。\n")+target,L.T("纸间"));
            }catch(Exception ex){app.Store.Log(ex);MessageBox.Show(L.T("备份未能导出。\n")+ex.Message,L.T("纸间"));}
        }
    }
    public sealed class ManagerWindow : DialogBase {
        readonly NotesApp app;
        StackPanel list;
        TextBox search;
        CheckBox trash;
        public ManagerWindow(NotesApp owner):base(L.T("纸间 · 便签收纳盒"),650,630) {
            app=owner;Header(L.T("每一张纸，都替你收好"),L.T("收起的便签在这里重新打开，废纸篓里的便签也能恢复。"));
            Grid filters=new Grid { Margin=new Thickness(0,0,0,18) };filters.ColumnDefinitions.Add(new ColumnDefinition());filters.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(100)});
            search=new TextBox { MinHeight=34,Padding=new Thickness(9,6,9,6),Background=Brushes.White,BorderBrush=Look.Brush("#DDD7CB"),BorderThickness=new Thickness(1),ToolTip=L.T("搜索标题或内容") };search.TextChanged+=delegate{Refresh();};filters.Children.Add(search);
            trash=new CheckBox { Content=L.T("废纸篓"),VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Right };trash.Checked+=delegate{Refresh();};trash.Unchecked+=delegate{Refresh();};Grid.SetColumn(trash,1);filters.Children.Add(trash);Body.Children.Add(filters);
            list=new StackPanel();Body.Children.Add(list);
            StackPanel actions=new StackPanel { Orientation=Orientation.Horizontal };actions.Children.Add(Look.ActionButton(L.T("＋ 新便签"),delegate{app.NewNote("text");Refresh();},true));actions.Children.Add(Look.ActionButton(L.T("设置"),app.OpenSettings,false));SetFooter(actions);Refresh();
            Activated+=delegate{Refresh();};
        }
        public void Refresh() {
            if(list==null)return;list.Children.Clear();string needle=search.Text??"";bool deleted=trash.IsChecked==true;
            foreach(NoteData note in app.Store.Data.Notes.Where(n=>n.Deleted==deleted && ((n.Title??"")+" "+(n.Text??"")).IndexOf(needle,StringComparison.OrdinalIgnoreCase)>=0).OrderByDescending(n=>n.Updated)) {
                Grid row=new Grid { Margin=new Thickness(14,12,12,12) };row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(10) });row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(102) });
                row.Children.Add(new Border { Width=5,Background=new SolidColorBrush(Look.PaperColor(note.Color)),CornerRadius=new CornerRadius(2) });
                StackPanel copy=new StackPanel { Margin=new Thickness(10,0,7,0) };TextBlock heading=Look.Label(String.IsNullOrWhiteSpace(note.Title)?L.T("未命名的小纸条"):note.Title,16,Look.Ink);heading.FontFamily=Look.Handwriting;heading.MaxHeight=48;copy.Children.Add(heading);
                string kind=note.Kind=="photo"?L.T("照片便签"):note.Kind=="checklist"?L.T("待办便签"):L.T("文字便签");TextBlock meta=Look.Label(kind+"  ·  "+(note.Deleted?L.T("可恢复"):note.Hidden?L.T("已收起"):L.T("在桌面上")),11,Look.Muted);meta.Margin=new Thickness(0,5,0,0);if(note.Locked)meta.Text+="  ·  "+L.T("已锁定");copy.Children.Add(meta);Grid.SetColumn(copy,1);row.Children.Add(copy);
                NoteData selected=note;Button open=Look.ActionButton(note.Deleted?L.T("恢复"):L.T("打开"),delegate{selected.Deleted=false;app.ShowNote(selected);Refresh();},false);open.Padding=new Thickness(13,6,13,6);open.Margin=new Thickness(0);open.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(open,2);row.Children.Add(open);
                list.Children.Add(new Border { Background=Brushes.White,BorderBrush=Look.Brush("#EAE4D9"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(10),Margin=new Thickness(0,0,0,9),Child=row });
            }
            if(list.Children.Count==0){TextBlock empty=Look.Label(deleted?L.T("废纸篓空空的。"):L.T("这里还没有匹配的便签。"),14,Look.Muted);empty.Margin=new Thickness(0,25,0,25);empty.TextAlignment=TextAlignment.Center;list.Children.Add(empty);}
        }
    }
    public sealed class ImageViewer : DialogBase {
        readonly BitmapSource source;
        public ImageViewer(BitmapSource source):base(L.T("纸间 · 查看原图"),860,680) {
            this.source=source;RefreshLanguage();
        }
        public void RefreshLanguage(){Body.Children.Clear();Title=L.T("纸间 · 查看原图");Header(L.T("原图查看"),source.PixelWidth+" × "+source.PixelHeight+L.T(" 像素  ·  图片清晰度取决于原始分辨率"));
            Image image=new Image { Source=source,Stretch=Stretch.Uniform,MaxHeight=480 };RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);Body.Children.Add(image);
        }
    }
}
