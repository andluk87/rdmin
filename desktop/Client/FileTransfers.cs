using System.Security.Cryptography;
using RemoteAdmin.Shared;
namespace RemoteAdmin.Client;

internal sealed class FileTransfers {
 readonly Func<FileCommand,CancellationToken,Task<FileReply>> request;
 readonly Action<long,long,long,DateTime> report;
 int entries;
 public FileTransfers(Func<FileCommand,CancellationToken,Task<FileReply>> request,Action<long,long,long,DateTime> report){this.request=request;this.report=report;}
 static string Join(string folder,string name)=>folder.Length==0?name:folder+"/"+name;
 static void Leaf(string name){if(string.IsNullOrWhiteSpace(name)||name is "." or ".."||name!=Path.GetFileName(name)||name.IndexOfAny(['/', '\\', ':','\0'])>=0||name.EndsWith('.')||name.EndsWith(' '))throw new IOException("Недопустимое имя файла");}
 static void NoLinks(string path){for(string? p=Path.GetFullPath(path);p!=null;p=Path.GetDirectoryName(p))if((File.Exists(p)||Directory.Exists(p))&&(File.GetAttributes(p)&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылки и junction не передаются");}
 void Visit(int depth){if(depth>32||++entries>10000)throw new IOException("Лимит дерева: 32 уровня / 10000 объектов");}
 public async Task UploadTree(string path,string remoteDirectory,CancellationToken ct,int depth=0){
  ct.ThrowIfCancellationRequested();Visit(depth);NoLinks(path);var name=Path.GetFileName(Path.TrimEndingDirectorySeparator(path));Leaf(name);var target=Join(remoteDirectory,name);
  if(Directory.Exists(path)){await request(new("mkdir",target),ct);foreach(var child in Directory.EnumerateFileSystemEntries(path))await UploadTree(child,target,ct,depth+1);}
  else await UploadFile(path,target,ct);
 }
 public async Task UploadFile(string path,string target,CancellationToken ct){
  NoLinks(path);await using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,64*1024,FileOptions.Asynchronous);if(file.Length>FileSandbox.MaxFile)throw new IOException("Лимит файла: 2 GiB");
  var begin=await request(new("begin",target,Size:file.Length),ct);if(begin.Offset<0||begin.Offset>file.Length)throw new IOException("Неверный offset");file.Position=begin.Offset;long initial=file.Position;var started=DateTime.UtcNow;var buffer=new byte[FileSandbox.Chunk];
  while(file.Position<file.Length){int n=await file.ReadAsync(buffer,ct);if(n==0)throw new EndOfStreamException();var reply=await request(new("write",target,Offset:file.Position-n,Data:Convert.ToBase64String(buffer,0,n)),ct);if(reply.Offset!=file.Position)throw new IOException("Не совпадает offset");report(file.Position,file.Length,initial,started);}
  file.Position=0;var hash=Convert.ToHexString(await SHA256.HashDataAsync(file,ct));await request(new("commit",target,Size:file.Length,Sha256:hash),ct);
 }
 public async Task DownloadTree(string remotePath,string name,bool directory,string localDirectory,CancellationToken ct,int depth=0){
  ct.ThrowIfCancellationRequested();Visit(depth);Leaf(name);NoLinks(localDirectory);var target=Path.GetFullPath(Path.Combine(localDirectory,name));NoLinks(target);
  if(directory){Directory.CreateDirectory(target);var listing=await request(new("list",remotePath),ct);foreach(var item in listing.Items??[]){Leaf(item.Name);await DownloadTree(Join(remotePath,item.Name),item.Name,item.Directory,target,ct,depth+1);}}
  else await DownloadFile(remotePath,target,ct);
 }
 public async Task DownloadFile(string remotePath,string target,CancellationToken ct){
  NoLinks(target);if(File.Exists(target))throw new IOException("Файл назначения уже существует. Переименуйте его перед передачей.");
  var expected=await request(new("hash",remotePath),ct);if(expected.Size<0||expected.Size>FileSandbox.MaxFile)throw new IOException("Лимит файла: 2 GiB");var temp=target+".ra-part";NoLinks(temp);
  await using(var file=new FileStream(temp,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,64*1024,FileOptions.Asynchronous)){
   if(file.Length>expected.Size)throw new IOException("Частичный файл больше исходного");file.Position=file.Length;long initial=file.Position;var started=DateTime.UtcNow;
   while(file.Position<expected.Size){var chunk=await request(new("read",remotePath,Offset:file.Position),ct);var data=Convert.FromBase64String(chunk.Data);if(data.Length==0||data.Length>FileSandbox.Chunk||chunk.Size!=expected.Size||chunk.Offset!=file.Position+data.Length||chunk.Offset>expected.Size)throw new IOException("Повреждённый чанк");await file.WriteAsync(data,ct);report(file.Position,expected.Size,initial,started);}
   await file.FlushAsync(ct);file.Position=0;var actual=Convert.ToHexString(await SHA256.HashDataAsync(file,ct));if(!Identity.Equal(actual,expected.Sha256))throw new IOException("SHA-256 не совпадает. Удалите .ra-part и повторите.");
  }
  File.Move(temp,target);
 }
}
