using RemoteAdmin.Shared;
namespace RemoteAdmin.Client;

public sealed partial class FileWindow {
 readonly ProgressBar transferProgress=new(){Dock=DockStyle.Top,Height=7,Style=ProgressBarStyle.Continuous,Maximum=100};
 readonly List<Button> fileActions=[];
 Button cancelTransfer=null!,uploadButton=null!,downloadButton=null!,renameButton=null!,deleteButton=null!;
 ImageList fileIcons=null!;

 void BuildLayout(){
  Width=1200;Height=800;MinimumSize=new Size(960,660);Text=$"RemoteAdmin {Ui.AppVersion} · Файлы";
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(24),ColumnCount=1,RowCount=4};root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,78));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Absolute,70));
  var header=Ui.Stack();Ui.Add(header,Ui.Heading("Передача файлов",22));Ui.Add(header,Ui.Description("Выберите файл и направление передачи. Незавершённая передача продолжится с сохранённого места."));root.Controls.Add(header,0,0);
  var panes=Ui.FillGrid(2);var localPane=BrowserPane("Ваш компьютер",local,left,true);localPane.Margin=new Padding(0,0,10,0);var remotePane=BrowserPane("Удалённый компьютер",remote,right,false);remotePane.Margin=new Padding(10,0,0,0);panes.Controls.Add(localPane,0,0);panes.Controls.Add(remotePane,1,0);root.Controls.Add(panes,0,1);
  fileIcons=new ImageList{ImageSize=new Size(20,20),ColorDepth=ColorDepth.Depth32Bit};fileIcons.Images.Add(FileIcon(true));fileIcons.Images.Add(FileIcon(false));
  foreach(var view in new[]{left,right}){
   view.BackColor=Color.White;view.ForeColor=Ui.Ink;view.BorderStyle=BorderStyle.None;view.HideSelection=false;view.MultiSelect=false;view.SmallImageList=fileIcons;view.Columns.Add("Имя",280);view.Columns.Add("Размер / тип",130);view.HeaderStyle=ColumnHeaderStyle.Nonclickable;
   view.Resize+=(_,_)=>{if(view.Columns.Count==2)view.Columns[0].Width=Math.Max(160,view.ClientSize.Width-145);};view.SelectedIndexChanged+=(_,_)=>UpdateFileActions();
  }
  var actions=Ui.Row();actions.Padding=new Padding(0,14,0,0);
  uploadButton=Ui.Primary("На сервер →",async(_,_)=>await Run(Upload));downloadButton=Ui.Primary("← На компьютер",async(_,_)=>await Run(Download));renameButton=Ui.Button("Переименовать",async(_,_)=>await Run(Rename));deleteButton=Ui.Destructive("Удалить",async(_,_)=>await Run(Delete));
  var mkdir=Ui.Button("Новая папка",async(_,_)=>await Run(Mkdir));cancelTransfer=Ui.Button("Отменить передачу",(_,_)=>operation?.Cancel());foreach(var button in new[]{uploadButton,downloadButton,mkdir,renameButton,deleteButton}){fileActions.Add(button);actions.Controls.Add(button);}actions.Controls.Add(cancelTransfer);root.Controls.Add(actions,0,2);
  var bottom=Ui.Stack();bottom.Dock=DockStyle.Fill;Ui.Add(bottom,transferProgress);progress.AutoSize=false;progress.Height=36;progress.TextAlign=ContentAlignment.MiddleLeft;progress.ForeColor=Ui.Muted;Ui.Add(bottom,progress);root.Controls.Add(bottom,0,3);Controls.Add(root);
  FormClosed+=(_,_)=>fileIcons.Dispose();SetOperationState(false);
 }
 Control BrowserPane(string title,TextBox path,ListView list,bool isLocal){
  var pane=new TableLayoutPanel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(16),ColumnCount=1,RowCount=3};pane.ColumnStyles.Add(new(SizeType.Percent,100));pane.RowStyles.Add(new(SizeType.Absolute,40));pane.RowStyles.Add(new(SizeType.Absolute,54));pane.RowStyles.Add(new(SizeType.Percent,100));pane.Controls.Add(Ui.Heading(title,14),0,0);
  var address=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1};address.ColumnStyles.Add(new(SizeType.AutoSize));address.ColumnStyles.Add(new(SizeType.Percent,100));address.ColumnStyles.Add(new(SizeType.AutoSize));
  var up=Ui.Button("↑",async(_,_)=>await Run(async ct=>{if(isLocal){var parent=Directory.GetParent(local.Text);if(parent!=null)local.Text=parent.FullName;}else remote.Text=ParentFolder(remoteFolder);await RefreshBoth(ct);}));up.MinimumSize=new Size(40,36);Ui.Hint(up,"На уровень выше");address.Controls.Add(up,0,0);path.Dock=DockStyle.Fill;path.Margin=new Padding(0,5,8,4);path.PlaceholderText=isLocal?"Путь на вашем компьютере":"/ — общая папка сервера";address.Controls.Add(path,1,0);
  var refresh=Ui.Button("↻",async(_,_)=>await Run(RefreshBoth));refresh.MinimumSize=new Size(40,36);Ui.Hint(refresh,"Обновить список");address.Controls.Add(refresh,2,0);fileActions.Add(up);fileActions.Add(refresh);path.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Run(RefreshBoth);}};pane.Controls.Add(address,0,1);pane.Controls.Add(list,0,2);return pane;
 }
 static Bitmap FileIcon(bool folder){
  var bitmap=new Bitmap(20,20);using var graphics=Graphics.FromImage(bitmap);graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
  if(folder){using var brush=new SolidBrush(Color.FromArgb(238,181,62));graphics.FillRectangle(brush,1,6,18,12);graphics.FillRectangle(brush,2,3,7,5);}else{using var brush=new SolidBrush(Color.FromArgb(220,232,255));graphics.FillRectangle(brush,4,2,12,16);using var pen=new Pen(Ui.Accent,1);graphics.DrawRectangle(pen,4,2,12,16);graphics.DrawLine(pen,7,8,13,8);graphics.DrawLine(pen,7,11,13,11);}return bitmap;
 }
 static string FormatSize(long bytes){double value=bytes;string[] units=["Б","КиБ","МиБ","ГиБ"];int unit=0;while(value>=1024&&unit<units.Length-1){value/=1024;unit++;}return $"{value:0.#} {units[unit]}";}
 void SetOperationState(bool active){
  foreach(var action in fileActions)action.Enabled=!active;cancelTransfer.Enabled=active;local.ReadOnly=active;remote.ReadOnly=active;left.Enabled=!active;right.Enabled=!active;if(active){transferProgress.Value=0;progress.Text="Выполняется операция…";}UpdateFileActions();
 }
 void UpdateFileActions(){
  if(uploadButton==null)return;bool idle=operation==null;uploadButton.Enabled=idle&&left.SelectedItems.Count==1&&left.SelectedItems[0].Tag is FileItem l&&!l.Directory;
  downloadButton.Enabled=idle&&right.SelectedItems.Count==1&&right.SelectedItems[0].Tag is FileItem rr&&!rr.Directory;bool remoteSelected=idle&&right.SelectedItems.Count==1&&right.SelectedItems[0].Tag is FileItem item&&item.Name!="..";renameButton.Enabled=remoteSelected;deleteButton.Enabled=remoteSelected;
 }
}
