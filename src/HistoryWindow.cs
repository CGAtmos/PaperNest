using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
namespace PaperNest {
 public sealed class HistoryWindow : DialogBase {
  readonly NotesApp app;List<NoteHistory> catalog;ListBox papers;TextBox search;ComboBox versions,filter;StackPanel preview;TextBlock count,detail,hint,result;Button action;NoteHistory selected;bool refreshing;
  internal ListBox PaperList{get{return papers;}} internal ComboBox VersionList{get{return versions;}} internal TextBox SearchBox{get{return search;}} internal Button PrimaryAction{get{return action;}}
  internal TextBlock ResultText{get{return result;}}
  public HistoryWindow(NotesApp owner):base(L.T("纸间 · 找回便签"),930,700){
   app=owner;Height=Math.Min(700,SystemParameters.WorkArea.Height-35);MinWidth=760;
   Header(L.T("把需要的那张纸，找回来"),L.T("先找纸片，再看内容。收起的纸片和以前写过的内容，都在这里。"));
   Grid tools=new Grid{Margin=new Thickness(0,0,0,16)};tools.ColumnDefinitions.Add(new ColumnDefinition());tools.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(150)});
   StackPanel find=new StackPanel{Margin=new Thickness(0,0,16,0)};find.Children.Add(Look.Label(L.T("搜索标题或写过的内容"),12,Look.Muted));search=new TextBox{MinHeight=36,Padding=new Thickness(10,7,10,7),Margin=new Thickness(0,6,0,0),FontSize=14,Background=Brushes.White,BorderBrush=Look.Brush("#DED8CC")};search.TextChanged+=delegate{FilterPapers(null);};find.Children.Add(search);tools.Children.Add(find);
   StackPanel scope=new StackPanel();scope.Children.Add(Look.Label(L.T("显示范围"),12,Look.Muted));filter=new ComboBox{ItemsSource=new[]{L.T("全部纸片"),L.T("已收起"),L.T("废纸篓")},SelectedIndex=0,MinHeight=36,Margin=new Thickness(0,6,0,0),Padding=new Thickness(8),FontSize=13};filter.SelectionChanged+=delegate{FilterPapers(null);};scope.Children.Add(filter);Grid.SetColumn(scope,1);tools.Children.Add(scope);Body.Children.Add(tools);
   Grid workspace=new Grid{Height=340};workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(255)});workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(18)});workspace.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
   Grid left=new Grid();left.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});left.RowDefinitions.Add(new RowDefinition());count=Look.Label(L.T("你的纸片"),12,Look.Muted);count.Margin=new Thickness(0,0,0,8);left.Children.Add(count);
   papers=new ListBox{Background=Brushes.Transparent,BorderThickness=new Thickness(0),HorizontalContentAlignment=HorizontalAlignment.Stretch};ScrollViewer.SetHorizontalScrollBarVisibility(papers,ScrollBarVisibility.Disabled);
   papers.ItemContainerStyle=(Style)System.Windows.Markup.XamlReader.Parse(@"<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Margin' Value='0,0,0,8'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border x:Name='card' Padding='13,11' Background='#FFFEFA' BorderBrush='#E5DFD3' BorderThickness='1' CornerRadius='9'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='card' Property='Background' Value='#EAF0E5'/><Setter TargetName='card' Property='BorderBrush' Value='#657B65'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='card' Property='BorderBrush' Value='#849780'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='card' Property='BorderThickness' Value='2'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
   papers.SelectionChanged+=delegate{if(refreshing)return;ListBoxItem item=papers.SelectedItem as ListBoxItem;selected=item==null?null:item.Tag as NoteHistory;SelectPaper();};Grid.SetRow(papers,1);left.Children.Add(papers);workspace.Children.Add(left);
   Grid right=new Grid();right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});right.RowDefinitions.Add(new RowDefinition());
   detail=Look.Label(L.T("选择一张纸片"),12,Look.Muted);detail.Margin=new Thickness(0,0,0,8);right.Children.Add(detail);
   versions=new ComboBox{MinHeight=36,Padding=new Thickness(8),Margin=new Thickness(0,0,0,12),FontSize=13};versions.SelectionChanged+=delegate{ShowPreview();};Grid.SetRow(versions,1);right.Children.Add(versions);
   preview=new StackPanel{Margin=new Thickness(19,16,19,18)};ScrollViewer reader=new ScrollViewer{Content=preview,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};Border paper=new Border{Background=Look.Brush("#FFF7D9"),BorderBrush=Look.Brush("#E8DDAF"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(5),Child=reader};Grid.SetRow(paper,2);right.Children.Add(paper);Grid.SetColumn(right,2);workspace.Children.Add(right);Body.Children.Add(workspace);
   hint=Look.Label("",12,Look.Muted);hint.Margin=new Thickness(0,13,0,0);Body.Children.Add(hint);
   Grid footer=new Grid();footer.ColumnDefinitions.Add(new ColumnDefinition());footer.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});result=Look.Label("",12,Look.Brush("#4F6A4F"));result.VerticalAlignment=VerticalAlignment.Center;result.Margin=new Thickness(0,0,14,0);footer.Children.Add(result);
   StackPanel buttons=new StackPanel{Orientation=Orientation.Horizontal};buttons.Children.Add(Look.ActionButton(L.T("关闭"),Close,false));action=Look.ActionButton(L.T("选一张纸片"),ApplySelection,true);action.IsEnabled=false;buttons.Children.Add(action);Grid.SetColumn(buttons,1);footer.Children.Add(buttons);SetFooter(footer);RefreshCatalog();
  }
  public void RefreshCatalog(){string id=selected==null?null:selected.Id;if(!app.SaveAll()){result.Text=L.T("当前内容保存未完成，请稍后再试。");return;}catalog=HistoryCatalog.Build(app.Store);FilterPapers(id);}
  void FilterPapers(string preferred){
   if(papers==null||catalog==null)return;refreshing=true;papers.Items.Clear();string query=(search.Text??"").Trim();
   foreach(NoteHistory group in catalog.Where(g=>g.Matches(query)&&(filter.SelectedIndex==0||filter.SelectedIndex==1&&g.Current!=null&&g.Current.Hidden&&!g.Current.Deleted||filter.SelectedIndex==2&&g.Current!=null&&g.Current.Deleted))){
    StackPanel card=new StackPanel();TextBlock title=Look.Label(group.Display.Title??L.T("未命名便签"),14,Look.Ink);title.FontWeight=FontWeights.SemiBold;title.MaxHeight=40;title.LineHeight=20;title.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;title.TextTrimming=TextTrimming.CharacterEllipsis;card.Children.Add(title);
    TextBlock snippet=Look.Label((group.Display.Text??"").Replace("\r","").Replace("\n","  "),11,Look.Muted);snippet.MaxHeight=32;snippet.LineHeight=16;snippet.LineStackingStrategy=LineStackingStrategy.BlockLineHeight;snippet.TextTrimming=TextTrimming.CharacterEllipsis;snippet.Margin=new Thickness(0,5,0,7);card.Children.Add(snippet);
    card.Children.Add(Look.Label(group.Status+"  ·  "+group.Versions.Count+(L.English&&group.Versions.Count==1?" distinct version":L.T(" 份不同内容")),11,Look.Brush("#63775E")));
    papers.Items.Add(new ListBoxItem{Content=card,Tag=group});
   }
   count.Text=L.T("你的纸片  ·  ")+papers.Items.Count+(L.English&&papers.Items.Count==1?" note":L.T(" 张"));refreshing=false;
   ListBoxItem choice=papers.Items.OfType<ListBoxItem>().FirstOrDefault(i=>((NoteHistory)i.Tag).Id==preferred);if(choice!=null)papers.SelectedItem=choice;else if(papers.Items.Count>0)papers.SelectedIndex=0;else{selected=null;SelectPaper();}
  }
  void SelectPaper(){versions.Items.Clear();result.Text="";if(selected==null){ShowPreview();return;}foreach(NoteVersion version in selected.Versions)versions.Items.Add(version);versions.SelectedIndex=0;}
  internal void ShowPreview(){
   if(preview==null||action==null)return;preview.Children.Clear();NoteVersion version=versions.SelectedItem as NoteVersion;action.IsEnabled=version!=null;
   if(version==null){detail.Text=L.T("没有匹配的纸片");preview.Children.Add(Look.Label(L.T("试试其他关键词，或切换到“全部纸片”。"),14,Look.Muted));hint.Text=L.T("便签写过的内容会自动留存，无需手动保存历史。");action.Content=L.T("选一张纸片");return;}
   NoteData note=version.Note;detail.Text=selected.Status+L.T("  ·  相同内容已自动合并");
   TextBlock title=Look.Label(note.Title??L.T("未命名便签"),25,Look.Ink);title.FontFamily=Look.Handwriting;title.Margin=new Thickness(0,0,0,15);preview.Children.Add(title);
   string document=app.Store.VersionDocumentPath(note);if(version.Current&&!File.Exists(document)&&note.ContentUpdated<=note.DocumentSavedAt)document=app.Store.DocumentPath(note);
   if(note.Kind=="photo") {string path=app.Store.ImagePath(note.ImageFile);if(File.Exists(path))try{preview.Children.Add(new Image{Source=Look.LoadImage(path),Stretch=Stretch.Uniform,MaxHeight=195,Margin=new Thickness(0,0,0,12)});}catch{preview.Children.Add(Look.Label(L.T("这张图片暂时无法读取，文字仍可找回。"),12,Look.Muted));}else preview.Children.Add(Look.Label(String.IsNullOrEmpty(note.ImageFile)?L.T("这张纸片还没贴照片。"):L.T("原图片文件未找到。"),12,Look.Muted));}
   bool rich=false;if(note.Kind=="text"&&File.Exists(document))try{RichTextBox content=new RichTextBox{IsReadOnly=true,IsDocumentEnabled=false,Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(0),VerticalScrollBarVisibility=ScrollBarVisibility.Disabled};using(FileStream stream=File.OpenRead(document))new TextRange(content.Document.ContentStart,content.Document.ContentEnd).Load(stream,DataFormats.XamlPackage);content.Document.PagePadding=new Thickness(0);foreach(BlockUIContainer block in content.Document.Blocks.OfType<BlockUIContainer>()){Image img=block.Child as Image;if(img!=null){img.Width=260;img.Stretch=Stretch.Uniform;}}preview.Children.Add(content);rich=true;}catch{}
   if(!rich){string text=note.Text??"";if(note.Kind=="checklist"){List<string> items=note.Items??text.Replace("\r","").Split('\n').ToList();text=String.Join("\n\n",items.Select((s,i)=>(note.Checks!=null&&i<note.Checks.Length&&note.Checks[i]=='1'?"☑  ":"☐  ")+s));}TextBlock body=Look.Label(String.IsNullOrWhiteSpace(text)?L.T("这张纸片暂时没有文字。"):text,16,Look.Ink);body.LineHeight=26;preview.Children.Add(body);}
   NoteData restored=app.Store.Data.Notes.FirstOrDefault(n=>n.RecoverySourceKey==version.Key);
   action.Content=version.Current?(selected.Current.Deleted?L.T("找回到桌面"):selected.Current.Hidden?L.T("重新放到桌面"):L.T("查看这张纸片")):restored!=null?L.T("查看已找回的纸片"):L.T("找回这份内容");
   hint.Text=version.Current?L.T("直接打开这张纸片，保留原来的内容和排版。"):restored!=null?L.T("这份内容已找回，再次点击会打开同一张纸片。"):L.T("将新建一张“")+(note.Title??L.T("便签"))+L.T(" · 找回”，现有纸片保留。");
   if(!version.Current&&note.Kind=="text"&&!File.Exists(document))hint.Text+=L.T(" 较早的记录仅保留文字。");
  }
  void ApplySelection(){NoteVersion version=versions.SelectedItem as NoteVersion;if(version==null)return;if(version.Current&&HistoryCatalog.Key(selected.Current)!=version.Key){RefreshCatalog();result.Text=L.T("内容刚刚有更新，预览已刷新。");return;}action.IsEnabled=false;try{
   if(version.Current&&selected.Current!=null){selected.Current.Deleted=false;app.ShowNote(selected.Current);if(!app.SaveAll())throw new IOException(app.LastSaveError);}else app.RestoreHistoryNote(version.Note);
   ShowPreview();result.Foreground=Look.Brush("#4F6A4F");result.Text=L.T("已放回桌面，可以继续编辑。");
  }catch(Exception ex){app.Store.Log(ex);result.Foreground=Look.Brush("#B34A35");result.Text=L.T("还没有完成：")+ex.Message;}finally{action.IsEnabled=true;}}
 }
}
