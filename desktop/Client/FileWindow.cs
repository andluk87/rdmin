using System.Security.Cryptography;
using RemoteAdmin.Shared;
namespace RemoteAdmin.Client;
public sealed partial class FileWindow : Form {
 readonly Func<FileCommand,CancellationToken,Task<FileReply>> request;
 readonly TextBox remote=Ui.Text("",400),local=Ui.Text(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),400);
 readonly ListView left=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true},right=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true};
 readonly Label progress=Ui.Label("Готово");CancellationTokenSource? operation;string remoteFolder="";
 public FileWindow(Func<FileCommand,CancellationToken,Task<FileReply>> request){
  this.request=request;Ui.Theme(this);Text="Передача файлов · общая папка сервера";Width=1100;Height=650;
  BuildLayout();SetupDragDrop();
  left.DoubleClick+=async(_,_)=>await Run(async ct=>{if(left.SelectedItems.Count>0&&left.SelectedItems[0].Tag is FileItem item&&item.Directory){local.Text=Path.GetFullPath(Path.Combine(local.Text,item.Name));}await RefreshBoth(ct);});
  right.DoubleClick+=async(_,_)=>await Run(async ct=>{if(right.SelectedItems.Count>0&&right.SelectedItems[0].Tag is FileItem item&&item.Directory){remote.Text=item.Name==".."?ParentFolder(remoteFolder):Join(remoteFolder,item.Name);}await RefreshBoth(ct);});
  Shown+=async(_,_)=>await Run(RefreshBoth);FormClosing+=(_,_)=>CancelTransfers();
 }
 static string Join(string folder,string name)=>folder.Length==0?name:folder+"/"+name;
 static string ParentFolder(string folder){int i=folder.LastIndexOf('/');return i<0?"":folder[..i];}
 async Task Run(Func<CancellationToken,Task> action){if(operation!=null)return;using var cts=new CancellationTokenSource();operation=cts;SetOperationState(true);try{await action(cts.Token);if(!IsDisposed){progress.Text="Готово";transferProgress.Value=100;}}catch(OperationCanceledException){if(!IsDisposed)progress.Text="Отменено; частичный файл сохранён для докачки";}catch(Exception e){if(!IsDisposed)progress.Text=e.Message;}finally{operation=null;if(!IsDisposed)SetOperationState(false);AfterOperation();}}
 static void Fill(ListView view,IEnumerable<FileItem> items){view.Items.Clear();foreach(var item in items.OrderByDescending(x=>x.Directory).ThenBy(x=>x.Name)){var row=new ListViewItem(item.Name){Tag=item,ImageIndex=item.Directory?0:1};row.SubItems.Add(item.Directory?"Папка":FormatSize(item.Size));view.Items.Add(row);}}
 async Task RefreshBoth(CancellationToken ct){
  var dir=new DirectoryInfo(local.Text);var items=dir.EnumerateFileSystemInfos().Take(2000).Select(x=>new FileItem(x.Name,x is DirectoryInfo,x is FileInfo f?f.Length:0)).ToList();if(dir.Parent!=null)items.Insert(0,new("..",true,0));Fill(left,items);
  var reply=await request(new("list",remote.Text),ct);remoteFolder=remote.Text;var list=(reply.Items??[]).ToList();if(remoteFolder!="")list.Insert(0,new("..",true,0));Fill(right,list);
 }
 FileItem Selected(ListView list){if(list.SelectedItems.Count!=1)throw new IOException("Выберите один файл");return (FileItem)list.SelectedItems[0].Tag!;}
 async Task Upload(CancellationToken ct){var transfer=new FileTransfers(request,Report);foreach(ListViewItem row in left.SelectedItems){if(row.Tag is FileItem item&&item.Name!="..")await transfer.UploadTree(Path.Combine(local.Text,item.Name),remoteFolder,ct);}await RefreshBoth(ct);}
 async Task Download(CancellationToken ct){var transfer=new FileTransfers(request,Report);foreach(ListViewItem row in right.SelectedItems){if(row.Tag is FileItem item&&item.Name!="..")await transfer.DownloadTree(Join(remoteFolder,item.Name),item.Name,item.Directory,local.Text,ct);}await RefreshBoth(ct);}
 static void ValidateLeaf(string name){if(name is "." or ".."||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0||name.EndsWith('.')||name.EndsWith(' '))throw new IOException("Укажите имя без разделителей пути");}
 void Report(long done,long total,long initial,DateTime start){var elapsed=Math.Max(0.1,(DateTime.UtcNow-start).TotalSeconds);double speed=(done-initial)/elapsed;var seconds=speed>0?(total-done)/speed:0;transferProgress.Value=total==0?100:(int)Math.Clamp(done*100/total,0,100);progress.Text=$"{FormatSize(done)} из {FormatSize(total)}  ·  {FormatSize((long)speed)}/с  ·  осталось {TimeSpan.FromSeconds(Math.Min(seconds,86400)):hh\\:mm\\:ss}";}
 async Task Mkdir(CancellationToken ct){var name=Ui.Ask(this,"Папка","Имя папки");if(string.IsNullOrWhiteSpace(name))return;ValidateLeaf(name);await request(new("mkdir",Join(remoteFolder,name)),ct);await RefreshBoth(ct);}
 async Task Rename(CancellationToken ct){var item=Selected(right);if(item.Name=="..")return;var name=Ui.Ask(this,"Переименовать","Новое имя",item.Name);if(string.IsNullOrWhiteSpace(name))return;ValidateLeaf(name);await request(new("rename",Join(remoteFolder,item.Name),Join(remoteFolder,name)),ct);await RefreshBoth(ct);}
 async Task Delete(CancellationToken ct){var item=Selected(right);if(item.Name=="..")return;if(MessageBox.Show(this,"Удалить "+item.Name+"? Для приглашений сервер также запросит подтверждение.","Удаление",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;await request(new("delete",Join(remoteFolder,item.Name)),ct);await RefreshBoth(ct);}
}
