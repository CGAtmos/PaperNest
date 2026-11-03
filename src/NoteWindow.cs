using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PaperNest {
    public sealed class NoteWindow : Window {
        public readonly NoteData Data;
        readonly NotesApp app;
        public Grid Frame;
        PaperSurface paper;
        DecorationSurface decoration;
        HeadingDoodle doodle;
        TextBox title;
        RichTextBox editor;
        StackPanel checklist;
        Image photo;
        TextBox caption;
        Border photoFrame;
        TextBlock photoHint;
        bool loading=true;
        bool contentDirty;
        bool geometryDirty;
        TextBlock saveStatus;
        StackPanel actions;
        Border drag;
        Thumb resize;
        Button more, addItem, plus, hideButton, lockButton;
        int openMenus;
        bool keyboardToolbar;
        internal bool ToolbarVisible { get { return actions.Opacity > .5; } }
        internal bool InteractionLocked { get { return Data.Locked; } }
        readonly List<CheckBox> checks=new List<CheckBox>();
        readonly List<TextBox> lines=new List<TextBox>();
        public NoteWindow(NotesApp owner,NoteData data) {
            app=owner;Data=data;Title=L.T("纸间 · ")+Data.Title;
            WindowStyle=WindowStyle.None;AllowsTransparency=true;Background=Brushes.Transparent;ShowInTaskbar=false;ResizeMode=ResizeMode.NoResize;
            Width=Clamp(data.Width,300,1800,410);Height=Clamp(data.Height,225,1500,460);MinWidth=300;MinHeight=225;MaxWidth=1800;MaxHeight=1500;
            Left=Finite(data.Left)?data.Left:100;Top=Finite(data.Top)?data.Top:100;Topmost=data.Pinned;
            TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display);TextOptions.SetTextRenderingMode(this,TextRenderingMode.Auto);
            Build();ApplyTilt();
            SourceInitialized+=delegate { IntPtr handle=new WindowInteropHelper(this).Handle;NativeWindowStyle.ToolWindow(handle);HwndSource.FromHwnd(handle).AddHook(WindowMessages); };
            Loaded+=delegate { KeepVisible();loading=false;AdjustImages(); };
            SizeChanged+=delegate { ApplyTilt();if(!loading){Data.Width=Width;Data.Height=Height;geometryDirty=true;AdjustImages();app.ScheduleSave();} };
            LocationChanged+=delegate {if(!loading){Data.Left=Left;Data.Top=Top;geometryDirty=true;app.ScheduleSave();}};
            Closing+=delegate(object sender,CancelEventArgs e) {if(!app.Exiting){e.Cancel=true;HideNote();}};
            PreviewKeyDown+=OnKey;
            AllowDrop=true;DragOver+=delegate(object s,DragEventArgs e){e.Effects=!Data.Locked&&e.Data.GetDataPresent(DataFormats.FileDrop)?DragDropEffects.Copy:DragDropEffects.None;e.Handled=true;};
            Drop+=OnDrop;
            ApplyZoom();ApplyLock();RefreshLanguage();loading=false;
        }
        public static bool Finite(double x){return !Double.IsNaN(x)&&!Double.IsInfinity(x);}
        public static double Clamp(double x,double min,double max,double fallback){return Finite(x)?Math.Max(min,Math.Min(max,x)):fallback;}
        void Build() {
            Grid outer=new Grid();Content=outer;
            Frame=new Grid { Margin=new Thickness(29,36,29,29),RenderTransformOrigin=new Point(.5,.5),RenderTransform=new RotateTransform(Clamp(Data.Angle,-5,5,0)) };
            outer.Children.Add(Frame);
            paper=new PaperSurface { ColorName=Data.Color,Texture=app.Store.Data.Texture,Effect=new DropShadowEffect { BlurRadius=19,ShadowDepth=7,Direction=275,Opacity=.26,Color=Color.FromRgb(39,36,28),RenderingBias=RenderingBias.Quality } };
            Frame.Children.Add(paper);
            Grid interior=new Grid { Margin=new Thickness(25,11,25,28) };interior.RowDefinitions.Add(new RowDefinition { Height=new GridLength(33) });interior.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });interior.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });Frame.Children.Add(interior);
            Grid bar=new Grid { Background=Brushes.Transparent,Margin=new Thickness(-14,-1,-12,0) };
            drag=new Border { Background=Brushes.Transparent,Cursor=Cursors.SizeAll,ToolTip=L.T("拖动纸条 · 双击可扶正") };
            drag.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(Data.Locked){e.Handled=true;return;}if(e.ClickCount==2){SetAngle(0);}else{try{DragMove();Snap();}catch(InvalidOperationException){}}};
            bar.Children.Add(drag);
            actions=new StackPanel { Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right };
            plus=Look.IconButton("plus",L.T("新建便签 · Ctrl+N"),delegate{app.NewNote("text");});actions.Children.Add(plus);
            more=Look.IconButton("more",L.T("颜色、字号、装饰与更多"),delegate{ContextMenu menu=BuildMenu();menu.PlacementTarget=more;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;});actions.Children.Add(more);
            lockButton=Look.IconButton("unlock",L.T("锁定这张便签"),delegate{SetLocked(!Data.Locked);});actions.Children.Add(lockButton);
            hideButton=Look.IconButton("close",L.T("收起这张便签（保留内容）"),HideNote);actions.Children.Add(hideButton);bar.Children.Add(actions);interior.Children.Add(bar);
            foreach(Button button in actions.Children)button.Focusable=true;
            Frame.MouseEnter+=delegate{keyboardToolbar=false;UpdateToolbar();};Frame.MouseLeave+=delegate{keyboardToolbar=false;UpdateToolbar();};
            PreviewMouseDown+=delegate{keyboardToolbar=false;UpdateToolbar();};
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Tab){keyboardToolbar=true;UpdateToolbar();}};
            actions.LostKeyboardFocus+=delegate{Dispatcher.BeginInvoke(new Action(UpdateToolbar));};
            UpdateToolbar();
            title=new TextBox { Text=Data.Title,Foreground=Look.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),FontFamily=Look.Handwriting,FontSize=29,Padding=new Thickness(0,6,0,8),Margin=new Thickness(0,3,0,5),TextWrapping=TextWrapping.Wrap,AcceptsReturn=false,MaxLength=120,VerticalContentAlignment=VerticalAlignment.Center,ToolTip=L.T("点击修改标题") };
            title.TextChanged+=delegate{if(!loading){Data.Title=title.Text;Title=L.T("纸间 · ")+Data.Title;app.NoteChanged(this,false);}};
            Grid titleHost=new Grid { Margin=new Thickness(0,0,0,8) };titleHost.Children.Add(title);doodle=new HeadingDoodle { ColorName=Data.Color,Height=13,VerticalAlignment=VerticalAlignment.Bottom,IsHitTestVisible=false,Margin=new Thickness(0,0,0,-4) };titleHost.Children.Add(doodle);Grid.SetRow(titleHost,1);interior.Children.Add(titleHost);
            if(Data.Kind=="photo") {
                titleHost.Visibility=Visibility.Collapsed;
                Grid pg=new Grid { Margin=new Thickness(-5,4,-5,0) };pg.RowDefinitions.Add(new RowDefinition { Height=new GridLength(1,GridUnitType.Star) });pg.RowDefinitions.Add(new RowDefinition { Height=GridLength.Auto });
                photo=new Image { Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,HorizontalAlignment=HorizontalAlignment.Center };
                RenderOptions.SetBitmapScalingMode(photo,BitmapScalingMode.HighQuality);
                photoFrame=new Border { Background=Look.Brush("#FAF6EE"),Padding=new Thickness(9),Margin=new Thickness(0,1,0,9),Cursor=Cursors.Hand,ToolTip=L.T("双击更换照片，也可以直接拖入图片") };
                Grid pictureGrid=new Grid();pictureGrid.Children.Add(photo);
                photoHint=Look.Label(L.T("＋\n贴一张喜欢的照片\n\n双击选择图片\n或把图片拖到这里"),15,Look.Muted);photoHint.TextAlignment=TextAlignment.Center;photoHint.VerticalAlignment=VerticalAlignment.Center;pictureGrid.Children.Add(photoHint);photoFrame.Child=pictureGrid;
                photoFrame.MouseLeftButtonDown+=delegate(object s,MouseButtonEventArgs e){if(e.ClickCount==2){if(!Data.Locked)ChoosePicture();else if(photo.Source is BitmapSource)new ImageViewer((BitmapSource)photo.Source).Show();e.Handled=true;}};
                pg.Children.Add(photoFrame);
                caption=new TextBox { Text=String.IsNullOrEmpty(Data.Text)?Data.Title:Data.Text,FontFamily=Look.Handwriting,FontSize=20,Foreground=Look.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(2,5,2,1),TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,MinHeight=35,MaxHeight=130,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };
                caption.TextChanged+=delegate{if(!loading){Data.Text=caption.Text;Data.Title=caption.Text.Split('\n').FirstOrDefault()??L.T("喜欢的瞬间");Title=L.T("纸间 · ")+Data.Title;app.NoteChanged(this,true);}};Grid.SetRow(caption,1);pg.Children.Add(caption);Grid.SetRow(pg,2);interior.Children.Add(pg);RefreshPhoto();
            } else if(Data.Kind=="checklist") {
                ScrollViewer scroll=new ScrollViewer { VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,Margin=new Thickness(0,7,0,0) };
                checklist=new StackPanel();scroll.Content=checklist;Grid.SetRow(scroll,2);interior.Children.Add(scroll);RebuildChecklist();
            } else {
                editor=new RichTextBox { Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Look.Ink,FontFamily=Look.Handwriting,FontSize=20,Padding=new Thickness(0,0,1,5),VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled,AcceptsTab=true,IsDocumentEnabled=true };
                editor.Document=new FlowDocument { FontFamily=Look.Handwriting,FontSize=20,Foreground=Look.Ink,PagePadding=new Thickness(0),LineHeight=31 };
                string document=app.Store.DocumentPath(Data);
                if(File.Exists(document)) {
                    try { using(FileStream stream=File.OpenRead(document))new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Load(stream,DataFormats.XamlPackage); }
                    catch(Exception ex){app.Store.Log(ex);editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(new Run(Data.Text??"")));app.Notify(L.T("一张便签的排版未能读取，已保留文字和原文件。"));}
                } else editor.Document.Blocks.Add(new Paragraph(new Run(Data.Text??"")) { Margin=new Thickness(0) });
                if(Data.ContentUpdated>Data.DocumentSavedAt){
                    BlockUIContainer[] images=editor.Document.Blocks.OfType<BlockUIContainer>().Where(b=>b.Child is Image).ToArray();foreach(BlockUIContainer block in images)editor.Document.Blocks.Remove(block);
                    editor.Document.Blocks.Clear();editor.Document.Blocks.Add(new Paragraph(new Run(Data.Text??"")));foreach(BlockUIContainer block in images)editor.Document.Blocks.Add(block);contentDirty=true;
                }
                editor.TextChanged+=delegate{if(!loading){contentDirty=true;app.NoteChanged(this,true);}};
                foreach(BlockUIContainer block in editor.Document.Blocks.OfType<BlockUIContainer>()){Image image=block.Child as Image;if(image!=null)BindImage(image);}
                editor.AllowDrop=false;Grid.SetRow(editor,2);interior.Children.Add(editor);contentDirty=contentDirty||!File.Exists(document);
            }
            decoration=new DecorationSurface { Kind=Data.Decoration,Height=78,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,-29,0,0),IsHitTestVisible=false };Frame.Children.Add(decoration);
            resize=new Thumb { Width=34,Height=34,HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Bottom,Cursor=Cursors.SizeNWSE,ToolTip=L.T("拖动调整大小 · Ctrl+滚轮调整字号"),Background=Brushes.Transparent };
            FrameworkElementFactory hit=new FrameworkElementFactory(typeof(Border));hit.SetValue(Border.BackgroundProperty,Brushes.Transparent);resize.Template=new ControlTemplate(typeof(Thumb)){VisualTree=hit};
            resize.DragDelta+=delegate(object s,DragDeltaEventArgs e){if(Data.Locked)return;Width=Math.Max(MinWidth,Math.Min(MaxWidth,Width+e.HorizontalChange));Height=Math.Max(MinHeight,Math.Min(MaxHeight,Height+e.VerticalChange));};Frame.Children.Add(resize);
            resize.DragCompleted+=delegate{app.NoteChanged(this,false);};
            saveStatus=new TextBlock { Text=L.T("本地自动保存"),FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=9,Foreground=Look.Muted,Opacity=.8,HorizontalAlignment=HorizontalAlignment.Left,VerticalAlignment=VerticalAlignment.Bottom,Margin=new Thickness(24,0,45,10),IsHitTestVisible=false };Frame.Children.Add(saveStatus);
            Deactivated+=delegate{keyboardToolbar=false;UpdateToolbar();if(!loading&&!app.Exiting)app.SaveAll();};
            Frame.ContextMenu=BuildMenu();Frame.ContextMenuOpening+=delegate { Frame.ContextMenu=BuildMenu(); };
            PreviewMouseWheel+=delegate(object s,MouseWheelEventArgs e){if((Keyboard.Modifiers&ModifierKeys.Control)!=0){SetZoom(Data.Zoom+(e.Delta>0?.1:-.1));e.Handled=true;}};
        }
        internal void UpdateToolbar() {
            if(actions==null)return;
            bool show=Frame.IsMouseOver||openMenus>0||keyboardToolbar;
            if(keyboardToolbar&&!IsKeyboardFocusWithin&&!IsActive){keyboardToolbar=false;show=Frame.IsMouseOver||openMenus>0;}
            actions.Opacity=show?1:0;actions.IsHitTestVisible=show;
        }
        IntPtr WindowMessages(IntPtr hwnd,int msg,IntPtr wParam,IntPtr lParam,ref bool handled){
            if(Data.Locked&&msg==0x112){long command=wParam.ToInt64()&0xFFF0;if(command==0xF010||command==0xF000)handled=true;}
            return IntPtr.Zero;
        }
        public bool SetLocked(bool locked) {
            if(Data.Locked==locked)return true;
            // Commit pending edits before making the note read-only.
            if(!app.SaveAll())return false;
            bool previous=Data.Locked;Data.Locked=locked;Data.Updated=DateTime.UtcNow;ApplyLock();RefreshLanguage();
            if(!app.SaveAll()){Data.Locked=previous;ApplyLock();RefreshLanguage();return false;}
            return true;
        }
        void ApplyLock() {
            if(title!=null)title.IsReadOnly=Data.Locked;
            if(caption!=null)caption.IsReadOnly=Data.Locked;
            if(editor!=null)editor.IsReadOnly=Data.Locked;
            foreach(TextBox line in lines)line.IsReadOnly=Data.Locked;
            foreach(CheckBox check in checks){check.IsEnabled=!Data.Locked;check.Opacity=1;}
            if(addItem!=null){addItem.IsEnabled=!Data.Locked;addItem.Visibility=Data.Locked?Visibility.Collapsed:Visibility.Visible;}
            if(drag!=null)drag.Cursor=Data.Locked?Cursors.Arrow:Cursors.SizeAll;
            if(resize!=null){resize.IsEnabled=!Data.Locked;resize.IsHitTestVisible=!Data.Locked;resize.Cursor=Data.Locked?Cursors.Arrow:Cursors.SizeNWSE;}
            if(photoFrame!=null)photoFrame.Cursor=Data.Locked?Cursors.Arrow:Cursors.Hand;
        }
        public void RefreshLanguage() {
            Title=L.T("纸间 · ")+Data.Title;
            title.ToolTip=Data.Locked?L.T("已锁定 · 可选择并复制文字"):L.T("点击修改标题");
            if(drag!=null)drag.ToolTip=Data.Locked?L.T("已锁定 · Ctrl+L 解锁"):L.T("拖动纸条 · 双击可扶正");
            if(resize!=null)resize.ToolTip=L.T("拖动调整大小 · Ctrl+滚轮调整字号");
            if(plus!=null)plus.ToolTip=L.T("新建便签 · Ctrl+N");
            if(more!=null)more.ToolTip=L.T("颜色、字号、装饰与更多");
            if(hideButton!=null)hideButton.ToolTip=L.T("收起这张便签（保留内容）");
            if(lockButton!=null){lockButton.ToolTip=Data.Locked?L.T("解锁这张便签 · Ctrl+L"):L.T("锁定内容和位置 · Ctrl+L");Button glyph=Look.IconButton(Data.Locked?"lock":"unlock","",delegate{});object visual=glyph.Content;glyph.Content=null;lockButton.Content=visual;}
            foreach(Button button in actions.Children)System.Windows.Automation.AutomationProperties.SetName(button,button.ToolTip as string);
            if(addItem!=null)addItem.Content=L.T("＋  添加一项");
            foreach(CheckBox check in checks)check.ToolTip=L.T("标记完成");
            foreach(TextBox line in lines)line.ToolTip=Data.Locked?L.T("已锁定 · 可选择并复制文字"):L.T("完整粘贴多行文字 · Enter 新建待办 · Shift+Enter 项内换行");
            if(photoHint!=null)photoHint.Text=L.T("＋\n贴一张喜欢的照片\n\n双击选择图片\n或把图片拖到这里");
            if(photoFrame!=null)photoFrame.ToolTip=Data.Locked?L.T("已锁定 · Ctrl+L 解锁"):L.T("双击更换照片，也可以直接拖入图片");
            if(editor!=null)foreach(BlockUIContainer block in editor.Document.Blocks.OfType<BlockUIContainer>()){Image img=block.Child as Image;if(img!=null)img.ToolTip=L.T("双击查看原图");}
            Frame.ContextMenu=BuildMenu();
        }
        void RebuildChecklist() {
            bool wasLoading=loading;loading=true;checklist.Children.Clear();checks.Clear();lines.Clear();
            string[] values=Data.Items!=null&&Data.Items.Count>0?Data.Items.ToArray():(Data.Text??"").Replace("\r","").Split('\n');if(values.Length==0)values=new[]{""};
            for(int i=0;i<values.Length;i++)AddChecklistRow(values[i],Data.Checks!=null&&i<Data.Checks.Length&&Data.Checks[i]=='1');
            Button add=new Button { Content=L.T("＋  添加一项"),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Foreground=Look.Muted,HorizontalAlignment=HorizontalAlignment.Left,Padding=new Thickness(0,12,8,8),FontSize=14,Cursor=Cursors.Hand };
            addItem=add;add.IsEnabled=!Data.Locked;add.Visibility=Data.Locked?Visibility.Collapsed:Visibility.Visible;
            add.Click+=delegate{if(Data.Locked)return;SyncChecklist();Data.Items.Add("");Data.Text=String.Join("\n",Data.Items);Data.Checks=(Data.Checks??"").PadRight(Data.Items.Count,'0');RebuildChecklist();lines[lines.Count-1].Focus();app.NoteChanged(this,true);};checklist.Children.Add(add);loading=wasLoading;ApplyLock();
        }
        void AddChecklistRow(string text,bool done) {
            Grid row=new Grid { Margin=new Thickness(0,4,0,8) };row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(30)});row.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            CheckBox check=new CheckBox { IsChecked=done,Width=21,Height=21,VerticalAlignment=VerticalAlignment.Top,Margin=new Thickness(0,8,0,0),Cursor=Cursors.Hand,ToolTip=L.T("标记完成") };
            ControlTemplate template=new ControlTemplate(typeof(CheckBox));FrameworkElementFactory box=new FrameworkElementFactory(typeof(Border));box.SetValue(Border.BorderBrushProperty,Look.Ink);box.SetValue(Border.BorderThicknessProperty,new Thickness(1.45));box.SetValue(Border.CornerRadiusProperty,new CornerRadius(2));box.SetValue(Border.BackgroundProperty,Brushes.Transparent);
            FrameworkElementFactory tick=new FrameworkElementFactory(typeof(System.Windows.Shapes.Path));tick.Name="tick";tick.SetValue(System.Windows.Shapes.Path.DataProperty,Look.Shape("M2,9 L7,14 L17,2"));tick.SetValue(System.Windows.Shapes.Path.StrokeProperty,Look.Ink);tick.SetValue(System.Windows.Shapes.Path.StrokeThicknessProperty,2.0);tick.SetValue(FrameworkElement.MarginProperty,new Thickness(1));tick.SetValue(UIElement.VisibilityProperty,Visibility.Hidden);box.AppendChild(tick);template.VisualTree=box;
            Trigger on=new Trigger { Property=ToggleButton.IsCheckedProperty,Value=true };on.Setters.Add(new Setter(UIElement.VisibilityProperty,Visibility.Visible,"tick"));template.Triggers.Add(on);check.Template=template;
            TextBox field=new TextBox { Text=text,FontFamily=Look.Handwriting,FontSize=23*Data.Zoom,Foreground=Look.Ink,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(3),TextWrapping=TextWrapping.Wrap,MinHeight=38,AcceptsReturn=true,MaxLength=0,ToolTip=L.T("完整粘贴多行文字 · Enter 新建待办 · Shift+Enter 项内换行") };
            field.TextChanged+=delegate{if(!loading){SyncChecklist();app.NoteChanged(this,true);}};
            field.PreviewKeyDown+=delegate(object s,KeyEventArgs e){if(Data.Locked)return;if(e.Key==Key.Enter&&(Keyboard.Modifiers&ModifierKeys.Shift)==0){SplitChecklistItem(field);e.Handled=true;}else if(e.Key==Key.Back&&field.Text.Length==0&&lines.Count>1){int index=lines.IndexOf(field);SyncChecklist();Data.Items.RemoveAt(index);Data.Text=String.Join("\n",Data.Items);Data.Checks=Data.Checks.Remove(index,1);RebuildChecklist();TextBox previous=lines[Math.Max(0,index-1)];previous.Focus();previous.CaretIndex=previous.Text.Length;app.NoteChanged(this,true);e.Handled=true;}};
            RoutedEventHandler changed=delegate{field.Opacity=check.IsChecked==true?.48:1;if(!loading){SyncChecklist();app.NoteChanged(this,true);}};check.Checked+=changed;check.Unchecked+=changed;field.Opacity=done?.48:1;
            checks.Add(check);lines.Add(field);row.Children.Add(check);Grid.SetColumn(field,1);row.Children.Add(field);checklist.Children.Add(row);
        }
        void SyncChecklist() {if(checklist==null)return;Data.Items=lines.Select(l=>l.Text).ToList();Data.Text=String.Join("\n",Data.Items);Data.Checks=String.Concat(checks.Select(c=>c.IsChecked==true?"1":"0"));Data.Updated=DateTime.UtcNow;}
        internal void SplitChecklistItem(TextBox field){
            if(Data.Locked)return;int index=lines.IndexOf(field);if(index<0)return;SyncChecklist();string value=field.Text;int start=field.SelectionStart,length=field.SelectionLength;
            Data.Items[index]=value.Substring(0,start);Data.Items.Insert(index+1,value.Substring(start+length));Data.Text=String.Join("\n",Data.Items);Data.Checks=(Data.Checks??"").PadRight(Data.Items.Count-1,'0').Insert(index+1,"0");RebuildChecklist();lines[index+1].Focus();lines[index+1].CaretIndex=0;app.NoteChanged(this,true);
        }
        public void SetChecklistItemCompleted(int index,bool complete){if(Data.Locked)return;if(index<0||index>=checks.Count)throw new ArgumentOutOfRangeException("index");checks[index].IsChecked=complete;}
        public void SetZoom(double zoom){if(Data.Locked)return;Data.Zoom=Math.Round(Clamp(zoom,.7,2,1),2);ApplyZoom();app.ScheduleSave();}
        public void SetAngle(double angle){if(Data.Locked)return;Data.Angle=PaperTilt.Normalize(angle);ApplyTilt();app.ScheduleSave();}
        public void RandomizeTilt(){SetAngle(PaperTilt.RandomAngle(Data.Angle));}
        void ApplyTilt(){
            if(Frame==null)return;
            Data.Angle=PaperTilt.Normalize(Data.Angle);Frame.RenderTransform=new RotateTransform(Data.Angle);
            double radians=Math.Abs(Data.Angle)*Math.PI/180,cos=Math.Cos(radians),sin=Math.Sin(radians),w=Math.Max(1,Width-58),h=Math.Max(1,Height-65);
            double extraX=Math.Max(0,(cos*w+sin*h-(Width-18))/(2*cos)),extraY=Math.Max(0,(sin*w+cos*h-(Height-18))/(2*cos));
            Frame.Margin=new Thickness(29+extraX,36+extraY,29+extraX,29+extraY);
        }
        public void ApplyZoom() {
            if(title!=null)title.FontSize=29*Data.Zoom;if(caption!=null)caption.FontSize=20*Data.Zoom;
            if(editor!=null)editor.LayoutTransform=new ScaleTransform(Data.Zoom,Data.Zoom);
            foreach(TextBox line in lines)line.FontSize=23*Data.Zoom;AdjustImages();
        }
        public void ApplyAppearance(){paper.ColorName=Data.Color;paper.Texture=app.Store.Data.Texture;paper.InvalidateVisual();decoration.Kind=Data.Decoration;decoration.InvalidateVisual();if(doodle!=null){doodle.ColorName=Data.Color;doodle.InvalidateVisual();}Topmost=Data.Pinned;ApplyTilt();}
        public void HideNote(){Data.Hidden=true;Data.Updated=DateTime.UtcNow;if(app.SaveAll())Hide();else Data.Hidden=false;}
        public void SetSaveStatus(string text,bool error){if(saveStatus!=null){saveStatus.Text=(Data.Locked?L.T("已锁定")+" · ":"")+text;saveStatus.Foreground=error?Look.Brush("#B34A35"):Look.Muted;}}
        public void CaptureDraft(){if(editor!=null)Data.Text=new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Text;if(checklist!=null)SyncChecklist();if(caption!=null)Data.Text=caption.Text;}
        void Snap() {
            Rect area=SystemParameters.WorkArea;double distance=16;
            if(Math.Abs(Left-area.Left)<distance)Left=area.Left;if(Math.Abs(Top-area.Top)<distance)Top=area.Top;
            if(Math.Abs(Left+Width-area.Right)<distance)Left=area.Right-Width;if(Math.Abs(Top+Height-area.Bottom)<distance)Top=area.Bottom-Height;
            Data.Left=Left;Data.Top=Top;app.NoteChanged(this,false);
        }
        void KeepVisible() {
            Rect rect=new Rect(Left,Top,Width,Height);bool visible=false;
            foreach(System.Windows.Forms.Screen screen in System.Windows.Forms.Screen.AllScreens) {
                double dpi=96;PresentationSource source=PresentationSource.FromVisual(this);if(source!=null)dpi=96*source.CompositionTarget.TransformToDevice.M11;
                Rect bounds=new Rect(screen.WorkingArea.X*96/dpi,screen.WorkingArea.Y*96/dpi,screen.WorkingArea.Width*96/dpi,screen.WorkingArea.Height*96/dpi);bounds.Intersect(rect);if(!bounds.IsEmpty&&bounds.Width>=100&&bounds.Height>=70){visible=true;break;}
            }
            if(!visible){Left=SystemParameters.WorkArea.Right-Width-30;Top=SystemParameters.WorkArea.Top+35;}
        }
        void OnKey(object s,KeyEventArgs e) {
            if((Keyboard.Modifiers&ModifierKeys.Control)==0)return;
            if(e.Key==Key.L){SetLocked(!Data.Locked);e.Handled=true;return;}
            if(e.Key==Key.N){app.NewNote((Keyboard.Modifiers&ModifierKeys.Shift)!=0?"photo":"text");e.Handled=true;}
            else if(e.Key==Key.W){HideNote();e.Handled=true;}
            else if(e.Key==Key.S){SaveContent();app.SaveAll();e.Handled=true;}
            else if(e.Key==Key.Add||e.Key==Key.OemPlus){SetZoom(Data.Zoom+.1);e.Handled=true;}
            else if(e.Key==Key.Subtract||e.Key==Key.OemMinus){SetZoom(Data.Zoom-.1);e.Handled=true;}
            else if(e.Key==Key.V){TextBox focused=Keyboard.FocusedElement as TextBox;if(checklist!=null&&focused!=null&&lines.Contains(focused)&&Clipboard.ContainsText())return;if(TryPasteImage())e.Handled=true;}
        }
        public bool TryPasteImage() {
            if(Data.Locked)return false;
            try {
                if(Clipboard.ContainsFileDropList()) {
                    foreach(string path in Clipboard.GetFileDropList()){if(IsImage(path)){InsertImageFile(path);return true;}}
                }
                if(Clipboard.ContainsImage()) {BitmapSource source=Clipboard.GetImage();if(source==null)return false;string name=Guid.NewGuid().ToString("N")+".png";string path=Path.Combine(app.Store.Root,"Images",name);PngBitmapEncoder enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(source));using(FileStream stream=File.Create(path))enc.Save(stream);if(Data.Kind=="photo"){Data.ImageFile=name;RefreshPhoto();app.NoteChanged(this,true);app.SaveAll();}else InsertImageSource(source);return true;}
            }catch(Exception ex){app.Store.Log(ex);app.Notify(L.T("图片没能粘贴，请试试“添加图片”。"));}return false;
        }
        static bool IsImage(string path){string ext=Path.GetExtension(path).ToLowerInvariant();return new[]{".png",".jpg",".jpeg",".bmp",".gif",".tif",".tiff"}.Contains(ext);}
        void OnDrop(object s,DragEventArgs e){if(Data.Locked){e.Handled=true;return;}if(e.Data.GetDataPresent(DataFormats.FileDrop)){foreach(string path in (string[])e.Data.GetData(DataFormats.FileDrop)){if(IsImage(path))InsertImageFile(path);}e.Handled=true;}}
        public void ChoosePicture(){if(Data.Locked)return;OpenFileDialog dialog=new OpenFileDialog { Filter=L.T("图片|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.tif;*.tiff"),Title=L.T("贴一张图片"),Multiselect=Data.Kind!="photo" };if(dialog.ShowDialog(this)==true){foreach(string path in dialog.FileNames)InsertImageFile(path);}}
        public void InsertImageFile(string path) {
            if(Data.Locked)return;
            try {if(Data.Kind=="photo"){Data.ImageFile=app.Store.ImportImage(path);RefreshPhoto();app.NoteChanged(this,true);app.SaveAll();}else if(editor!=null)InsertImageSource(Look.LoadImage(path));else{NoteWindow note=app.NewNote("photo");note.InsertImageFile(path);}}
            catch(Exception ex){app.Store.Log(ex);MessageBox.Show(L.T("这张图片未能打开。\n")+ex.Message,L.T("纸间"));}
        }
        public void InsertImageSource(BitmapSource source) {
            if(Data.Locked)return;
            if(editor==null){NoteWindow note=app.NewNote("photo");string name=Guid.NewGuid().ToString("N")+".png";PngBitmapEncoder enc=new PngBitmapEncoder();enc.Frames.Add(BitmapFrame.Create(source));using(FileStream stream=File.Create(Path.Combine(app.Store.Root,"Images",name)))enc.Save(stream);note.Data.ImageFile=name;note.RefreshPhoto();app.NoteChanged(note,true);app.SaveAll();return;}
            Image image=new Image { Source=source,Stretch=Stretch.Uniform,Width=Math.Min(source.Width,Math.Max(80,(ActualWidth-125)/Data.Zoom)),Margin=new Thickness(0,8,0,10),ToolTip=L.T("双击以原图大小查看") };RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);
            BindImage(image);
            BlockUIContainer block=new BlockUIContainer(image);Paragraph at=editor.CaretPosition.Paragraph;
            if(at!=null&&at.Parent==editor.Document)editor.Document.Blocks.InsertAfter(at,block);else editor.Document.Blocks.Add(block);
            Paragraph after=new Paragraph(new Run("")){Margin=new Thickness(0)};editor.Document.Blocks.InsertAfter(block,after);editor.CaretPosition=after.ContentStart;editor.Focus();contentDirty=true;app.NoteChanged(this,true);app.SaveAll();
        }
        public int EmbeddedImageCount { get {return editor==null?0:editor.Document.Blocks.OfType<BlockUIContainer>().Count(b=>b.Child is Image);} }
        void BindImage(Image image){image.ToolTip=L.T("双击查看原图");image.MouseLeftButtonDown+=delegate(object sender,MouseButtonEventArgs e){if(e.ClickCount==2){BitmapSource source=image.Source as BitmapSource;if(source!=null)new ImageViewer(source).Show();e.Handled=true;}};}
        void RefreshPhoto(){if(photo==null)return;string path=app.Store.ImagePath(Data.ImageFile);photo.Source=File.Exists(path)?Look.LoadImage(path):null;photoHint.Visibility=photo.Source==null?Visibility.Visible:Visibility.Collapsed;}
        void AdjustImages(){if(editor==null)return;double available=Math.Max(80,(Width-135)/Data.Zoom);foreach(BlockUIContainer block in editor.Document.Blocks.OfType<BlockUIContainer>()){Image image=block.Child as Image;if(image!=null&&image.Source!=null)image.Width=Math.Min(image.Source.Width,available);}}
        public void SaveContent() {
            if(editor!=null) {
                Data.Text=new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Text;
                if(contentDirty) {
                    using(MemoryStream memory=new MemoryStream()){new TextRange(editor.Document.ContentStart,editor.Document.ContentEnd).Save(memory,DataFormats.XamlPackage);byte[] bytes=memory.ToArray();Store.AtomicWrite(app.Store.DocumentPath(Data),bytes,true);app.Store.SaveDocumentVersion(Data,bytes);}
                    contentDirty=false;Data.Updated=DateTime.UtcNow;Data.DocumentSavedAt=DateTime.UtcNow;
                }
                if(String.IsNullOrEmpty(Data.DocumentRevision)&&File.Exists(app.Store.DocumentPath(Data)))app.Store.SaveDocumentVersion(Data,File.ReadAllBytes(app.Store.DocumentPath(Data)));
            }
            if(checklist!=null)SyncChecklist();if(caption!=null)Data.Text=caption.Text;
            if(geometryDirty){Data.Left=Left;Data.Top=Top;Data.Width=Width;Data.Height=Height;geometryDirty=false;}
        }
        void Add(ContextMenu menu,string text,Action action,bool editable=false){MenuItem item=new MenuItem { Header=text,IsEnabled=!editable||!Data.Locked };item.Click+=delegate{action();};menu.Items.Add(item);}
        ContextMenu BuildMenu() {
            ContextMenu menu=new ContextMenu { FontFamily=new FontFamily("Microsoft YaHei UI"),FontSize=13,Padding=new Thickness(5),Background=Look.Brush("#FFFCF5"),BorderBrush=Look.Brush("#DED8CA") };
            Add(menu,L.T("＋ 新建文字便签"),delegate{app.NewNote("text");});Add(menu,L.T("☐ 新建待办便签"),delegate{app.NewNote("checklist");});Add(menu,L.T("▧ 新建照片便签"),delegate{app.NewNote("photo");});
            menu.Opened+=delegate{openMenus++;UpdateToolbar();};menu.Closed+=delegate{openMenus=Math.Max(0,openMenus-1);UpdateToolbar();};
            menu.Items.Add(new Separator());Add(menu,Data.Locked?L.T("解锁这张便签   Ctrl+L"):L.T("锁定这张便签   Ctrl+L"),delegate{SetLocked(!Data.Locked);});
            menu.Items.Add(new Separator());Add(menu,Data.Kind=="photo"?L.T("更换照片…"):L.T("添加图片…"),ChoosePicture,true);
            if(editor!=null){Add(menu,L.T("粘贴文字或截图   Ctrl+V"),delegate{if(!TryPasteImage()){editor.Focus();editor.Paste();}},true);}
            MenuItem colors=new MenuItem { IsEnabled=!Data.Locked,Header=L.T("纸张颜色") };string[] ids={"butter","sage","rose","sky","lilac","cream"};string[] names={L.T("奶油黄"),L.T("鼠尾草绿"),L.T("蜜桃粉"),L.T("雾蓝"),L.T("浅紫"),L.T("暖白")};
            for(int i=0;i<ids.Length;i++){string id=ids[i];MenuItem item=new MenuItem { Header=names[i],IsCheckable=true,IsChecked=Data.Color==id,Icon=new Border { Width=15,Height=15,Background=new SolidColorBrush(Look.PaperColor(id)),CornerRadius=new CornerRadius(3) } };item.Click+=delegate{Data.Color=id;ApplyAppearance();app.ScheduleSave();};colors.Items.Add(item);}menu.Items.Add(colors);
            MenuItem decorations=new MenuItem { IsEnabled=!Data.Locked,Header=L.T("顶部装饰") };string[] kinds={"tape","clip","none"};string[] labels={L.T("半透明纸胶带"),L.T("玫瑰金回形针"),L.T("干净纸条")};
            for(int i=0;i<kinds.Length;i++){string kind=kinds[i];MenuItem item=new MenuItem{Header=labels[i],IsCheckable=true,IsChecked=Data.Decoration==kind};item.Click+=delegate{Data.Decoration=kind;ApplyAppearance();app.ScheduleSave();};decorations.Items.Add(item);}menu.Items.Add(decorations);
            MenuItem zooms=new MenuItem { IsEnabled=!Data.Locked,Header=L.T("内容大小 · ")+Math.Round(Data.Zoom*100)+"%" };foreach(double z in new[]{.8,1,1.25,1.5,2}){double zoom=z;MenuItem item=new MenuItem{Header=(int)(z*100)+"%",IsCheckable=true,IsChecked=Math.Abs(Data.Zoom-z)<.01};item.Click+=delegate{SetZoom(zoom);};zooms.Items.Add(item);}menu.Items.Add(zooms);
            MenuItem tilt=new MenuItem { IsEnabled=!Data.Locked,Header=L.T("纸片斜度 · ")+PaperTilt.Description(Data.Angle) };
            StackPanel tiltPanel=new StackPanel { Width=190,Margin=new Thickness(4,5,4,7) };
            TextBlock tiltValue=Look.Label(PaperTilt.Description(Data.Angle)+L.T("  （左右各 5°）"),12,Look.Muted);tiltPanel.Children.Add(tiltValue);
            Slider tiltSlider=new Slider { Minimum=-5,Maximum=5,Value=Data.Angle,TickFrequency=.5,IsSnapToTickEnabled=true,SmallChange=.5,LargeChange=1,Margin=new Thickness(0,10,0,0),ToolTip=L.T("向左滑左倾，向右滑右倾，0° 为放正") };
            tiltSlider.ValueChanged+=delegate{SetAngle(tiltSlider.Value);tiltValue.Text=PaperTilt.Description(Data.Angle)+L.T("  （左右各 5°）");tilt.Header=L.T("纸片斜度 · ")+PaperTilt.Description(Data.Angle);};tiltPanel.Children.Add(tiltSlider);
            tilt.Items.Add(new MenuItem { Header=tiltPanel,StaysOpenOnClick=true });tilt.Items.Add(new Separator());
            MenuItem randomTilt=new MenuItem { Header=L.T("随机倾斜一次") };randomTilt.Click+=delegate{RandomizeTilt();};tilt.Items.Add(randomTilt);
            MenuItem leftTilt=new MenuItem { Header=L.T("向左一点 · 0.5°") };leftTilt.Click+=delegate{SetAngle(Data.Angle-.5);};tilt.Items.Add(leftTilt);
            MenuItem rightTilt=new MenuItem { Header=L.T("向右一点 · 0.5°") };rightTilt.Click+=delegate{SetAngle(Data.Angle+.5);};tilt.Items.Add(rightTilt);
            MenuItem straightTilt=new MenuItem { Header=L.T("放正 · 0°") };straightTilt.Click+=delegate{SetAngle(0);};tilt.Items.Add(straightTilt);menu.Items.Add(tilt);
            MenuItem pin=new MenuItem { Header=L.T("始终置顶"),IsCheckable=true,IsChecked=Data.Pinned };pin.Click+=delegate{Data.Pinned=!Data.Pinned;ApplyAppearance();app.ScheduleSave();};menu.Items.Add(pin);
            Add(menu,L.T("扶正这张纸条"),delegate{SetAngle(0);},true);
            menu.Items.Add(new Separator());Add(menu,L.T("收起这张便签"),HideNote);Add(menu,L.T("放入废纸篓…"),delegate{app.DeleteNote(this);},true);menu.Items.Add(new Separator());Add(menu,L.T("便签收纳盒"),app.OpenManager);Add(menu,L.T("找回便签"),app.OpenHistory);Add(menu,L.T("设置"),app.OpenSettings);Add(menu,L.T("关于纸间"),app.OpenAbout);return menu;
        }
    }
    public static class NativeWindowStyle {
        [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="GetWindowLongW")] static extern int GetWindowLong(IntPtr hwnd,int index);
        [System.Runtime.InteropServices.DllImport("user32.dll",EntryPoint="SetWindowLongW")] static extern int SetWindowLong(IntPtr hwnd,int index,int value);
        public static void ToolWindow(IntPtr hwnd){int style=GetWindowLong(hwnd,-20);SetWindowLong(hwnd,-20,(style|0x80)&~0x40000);}
    }
}
