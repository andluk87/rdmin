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
  BuildLayout();
  left.DoubleClick+=async(_,_)=>await Run(async ct=>{if(left.SelectedItems.Count>0&&left.SelectedItems[0].Tag is FileItem item&&item.Directory){local.Text=Path.GetFullPath(Path.Combine(local.Text,item.Name));}await RefreshBoth(ct);});
  right.DoubleClick+=async(_,_)=>await Run(async ct=>{if(right.SelectedItems.Count>0&&right.SelectedItems[0].Tag is FileItem item&&item.Directory){remote.Text=item.Name==".."?ParentFolder(remoteFolder):Join(remoteFolder,item.Name);}await RefreshBoth(ct);});
  Shown+=async(_,_)=>await Run(RefreshBoth);FormClosing+=(_,_)=>operation?.Cancel();
 }
 static string Join(string folder,string name)=>folder.Length==0?name:folder+"/"+name;
 static string ParentFolder(string folder){int i=folder.LastIndexOf('/');return i<0?"":folder[..i];}
 async Task Run(Func<CancellationToken,Task> action){if(operation!=null)return;using var cts=new CancellationTokenSource();operation=cts;SetOperationState(true);try{await action(cts.Token);if(!IsDisposed){progress.Text="Готово";transferProgress.Value=100;}}catch(OperationCanceledException){if(!IsDisposed)progress.Text="Отменено; частичный файл сохранён для докачки";}catch(Exception e){if(!IsDisposed)progress.Text=e.Message;}finally{operation=null;if(!IsDisposed)SetOperationState(false);}}
 static void Fill(ListView view,IEnumerable<FileItem> items){view.Items.Clear();foreach(var item in items.OrderByDescending(x=>x.Directory).ThenBy(x=>x.Name)){var row=new ListViewItem(item.Name){Tag=item,ImageIndex=item.Directory?0:1};row.SubItems.Add(item.Directory?"Папка":FormatSize(item.Size));view.Items.Add(row);}}
 async Task RefreshBoth(CancellationToken ct){
  var dir=new DirectoryInfo(local.Text);var items=dir.EnumerateFileSystemInfos().Take(2000).Select(x=>new FileItem(x.Name,x is DirectoryInfo,x is FileInfo f?f.Length:0)).ToList();if(dir.Parent!=null)items.Insert(0,new("..",true,0));Fill(left,items);
  var reply=await request(new("list",remote.Text),ct);remoteFolder=remote.Text;var list=(reply.Items??[]).ToList();if(remoteFolder!="")list.Insert(0,new("..",true,0));Fill(right,list);
 }
 FileItem Selected(ListView list){if(list.SelectedItems.Count!=1)throw new IOException("Выберите один файл");return (FileItem)list.SelectedItems[0].Tag!;}
 async Task Upload(CancellationToken ct){
  var item=Selected(left);if(item.Directory)throw new IOException("Выберите файл");var path=Path.Combine(local.Text,item.Name);await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,64*1024,FileOptions.Asynchronous);if(file.Length>FileSandbox.MaxFile)throw new IOException("Лимит: 2 GiB");
  var target=Join(remoteFolder,item.Name);var begin=await request(new("begin",target,Size:file.Length),ct);if(begin.Offset<0||begin.Offset>file.Length)throw new IOException("Неверный offset");file.Position=begin.Offset;
  var buffer=new byte[FileSandbox.Chunk];var started=DateTime.UtcNow;var initial=file.Position;
  while(file.Position<file.Length){ct.ThrowIfCancellationRequested();int n=await file.ReadAsync(buffer,ct);var reply=await request(new("write",target,Offset:file.Position-n,Data:Convert.ToBase64String(buffer,0,n)),ct);if(reply.Offset!=file.Position)throw new IOException("Не совпадает offset");Report(file.Position,file.Length,initial,started);}
  file.Position=0;var hash=Convert.ToHexString(await SHA256.HashDataAsync(file,ct));await request(new("commit",target,Size:file.Length,Sha256:hash),ct);await RefreshBoth(ct);
 }
 async Task Download(CancellationToken ct){
  var item=Selected(right);if(item.Directory)throw new IOException("Выберите файл");var remotePath=Join(remoteFolder,item.Name);if(item.Name!=Path.GetFileName(item.Name)||item.Name.Contains(':')||item.Name.Contains('\\')||item.Name.Contains('/'))throw new IOException("Неверное имя файла");var path=Path.Combine(local.Text,item.Name);if(File.Exists(path))throw new IOException("Локальный файл существует — переименуйте его");
  var expected=await request(new("hash",remotePath),ct);if(expected.Size>FileSandbox.MaxFile||expected.Size<0)throw new IOException("Лимит: 2 GiB");
  var temp=path+".ra-part";await using(var file=new FileStream(temp,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,64*1024,FileOptions.Asynchronous)){
   if(file.Length>expected.Size)throw new IOException("Частичный файл больше исходного");file.Position=file.Length;var initial=file.Length;var started=DateTime.UtcNow;
   while(file.Position<expected.Size){ct.ThrowIfCancellationRequested();var chunk=await request(new("read",remotePath,Offset:file.Position),ct);var data=Convert.FromBase64String(chunk.Data);if(data.Length==0||data.Length>FileSandbox.Chunk||chunk.Size!=expected.Size||chunk.Offset!=file.Position+data.Length||chunk.Offset>expected.Size)throw new IOException("Повреждённый чанк");await file.WriteAsync(data,ct);Report(file.Position,expected.Size,initial,started);}
   await file.FlushAsync(ct);file.Position=0;var actual=Convert.ToHexString(await SHA256.HashDataAsync(file,ct));if(!Identity.Equal(actual,expected.Sha256))throw new IOException("SHA-256 не совпадает; удалите локальный .ra-part и повторите");
  }
  File.Move(temp,path);await RefreshBoth(ct);
 }
 void Report(long done,long total,long initial,DateTime start){var elapsed=Math.Max(0.1,(DateTime.UtcNow-start).TotalSeconds);double speed=(done-initial)/elapsed;var seconds=speed>0?(total-done)/speed:0;transferProgress.Value=total==0?100:(int)Math.Clamp(done*100/total,0,100);progress.Text=$"{FormatSize(done)} из {FormatSize(total)}  ·  {FormatSize((long)speed)}/с  ·  осталось {TimeSpan.FromSeconds(Math.Min(seconds,86400)):hh\\:mm\\:ss}";}
 async Task Mkdir(CancellationToken ct){var name=Ui.Ask(this,"Папка","Имя папки");if(string.IsNullOrWhiteSpace(name))return;await request(new("mkdir",Join(remoteFolder,name)),ct);await RefreshBoth(ct);}
 async Task Rename(CancellationToken ct){var item=Selected(right);if(item.Name=="..")return;var name=Ui.Ask(this,"Переименовать","Новое имя",item.Name);if(string.IsNullOrWhiteSpace(name))return;await request(new("rename",Join(remoteFolder,item.Name),Join(remoteFolder,name)),ct);await RefreshBoth(ct);}
 async Task Delete(CancellationToken ct){var item=Selected(right);if(item.Name=="..")return;if(MessageBox.Show(this,"Удалить "+item.Name+"? Сервер также запросит подтверждение.","Удаление",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;await request(new("delete",Join(remoteFolder,item.Name)),ct);await RefreshBoth(ct);}
}
