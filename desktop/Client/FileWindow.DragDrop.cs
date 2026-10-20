using RemoteAdmin.Shared;
namespace RemoteAdmin.Client;
public sealed partial class FileWindow {
 const string RemoteFilesFormat="RemoteAdmin.RemoteFiles.v1";
 readonly Guid dragIdentity=Guid.NewGuid();
 readonly Queue<Func<CancellationToken,Task>> queuedTransfers=new();
 sealed record RemoteDrag(Guid Origin,string Directory,FileItem[] Items);
 void SetupDragDrop(){
  left.MultiSelect=right.MultiSelect=true;left.AllowDrop=right.AllowDrop=true;
  left.ItemDrag+=(_,_)=>{if(operation!=null)return;var paths=left.SelectedItems.Cast<ListViewItem>().Select(i=>i.Tag as FileItem).Where(i=>i!=null&&i.Name!="..").Select(i=>Path.Combine(local.Text,i!.Name)).ToArray();if(paths.Length>0){var data=new DataObject(DataFormats.FileDrop,paths);left.DoDragDrop(data,DragDropEffects.Copy);}};
  right.ItemDrag+=(_,_)=>{if(operation!=null)return;var items=right.SelectedItems.Cast<ListViewItem>().Select(i=>i.Tag as FileItem).Where(i=>i!=null&&i.Name!="..").Cast<FileItem>().ToArray();if(items.Length>0){var data=new DataObject();data.SetData(RemoteFilesFormat,new RemoteDrag(dragIdentity,remoteFolder,items));right.DoDragDrop(data,DragDropEffects.Copy);}};
  right.DragEnter+=(_,e)=>e.Effect=e.Data?.GetDataPresent(DataFormats.FileDrop)==true?DragDropEffects.Copy:DragDropEffects.None;
  right.DragOver+=(_,e)=>e.Effect=e.Data?.GetDataPresent(DataFormats.FileDrop)==true?DragDropEffects.Copy:DragDropEffects.None;
  right.DragDrop+=(_,e)=>{if(e.Data?.GetData(DataFormats.FileDrop) is not string[] paths)return;string target=remoteFolder;var hit=right.GetItemAt(right.PointToClient(new Point(e.X,e.Y)).X,right.PointToClient(new Point(e.X,e.Y)).Y);if(hit?.Tag is FileItem item&&item.Directory)target=item.Name==".."?ParentFolder(remoteFolder):Join(remoteFolder,item.Name);QueueUpload(paths,target);};
  left.DragEnter+=(_,e)=>e.Effect=e.Data?.GetData(RemoteFilesFormat) is RemoteDrag data&&data.Origin==dragIdentity?DragDropEffects.Copy:DragDropEffects.None;
  left.DragOver+=(_,e)=>e.Effect=e.Data?.GetData(RemoteFilesFormat) is RemoteDrag data&&data.Origin==dragIdentity?DragDropEffects.Copy:DragDropEffects.None;
  left.DragDrop+=(_,e)=>{if(e.Data?.GetData(RemoteFilesFormat) is not RemoteDrag data||data.Origin!=dragIdentity)return;var destination=local.Text;var point=left.PointToClient(new Point(e.X,e.Y));if(left.GetItemAt(point.X,point.Y)?.Tag is FileItem item&&item.Directory)destination=Path.GetFullPath(Path.Combine(local.Text,item.Name));QueueDownload(data.Directory,data.Items,destination);};
 }
 public void QueueUpload(IEnumerable<string> paths,string? directory=null){
  var files=paths.Take(10001).ToArray();if(files.Length>10000){progress.Text="Слишком много объектов";return;}string target=directory??remoteFolder;
  queuedTransfers.Enqueue(async ct=>{var transfer=new FileTransfers(request,Report);foreach(var path in files)await transfer.UploadTree(path,target,ct);await RefreshBoth(ct);});DrainTransfers();
 }
 void QueueDownload(string directory,FileItem[] files,string destination){queuedTransfers.Enqueue(async ct=>{var transfer=new FileTransfers(request,Report);foreach(var item in files)await transfer.DownloadTree(Join(directory,item.Name),item.Name,item.Directory,destination,ct);await RefreshBoth(ct);});DrainTransfers();}
 void DrainTransfers(){if(operation!=null||queuedTransfers.Count==0||IsDisposed||Disposing)return;var action=queuedTransfers.Dequeue();_=Run(action);}
 void AfterOperation(){if(queuedTransfers.Count>0&&!IsDisposed&&!Disposing)BeginInvoke((Action)DrainTransfers);}
 void CancelTransfers(){queuedTransfers.Clear();operation?.Cancel();}
}
