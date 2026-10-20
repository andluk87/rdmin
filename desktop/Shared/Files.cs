using System.Security.Cryptography;
namespace RemoteAdmin.Shared;

public sealed class FileSandbox : IDisposable {
 public const long MaxFile=2L*1024*1024*1024;
 public const int Chunk=4*1024*1024;
 readonly string root;
 readonly Dictionary<string,FileStream> writes=new(StringComparer.OrdinalIgnoreCase);
 public FileSandbox(string root){this.root=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar);Directory.CreateDirectory(this.root);EnsureNoLinks(this.root);}
 static void EnsureNoLinks(string path){for(var p=new DirectoryInfo(path);p!=null;p=p.Parent)if(p.Exists&&(p.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылки и junction запрещены");}
 string Resolve(string relative){
  if(relative.Contains(':')||relative.Contains('\\')||relative.Split('/').Any(x=>x is "." or "..")||Path.IsPathRooted(relative)||relative.IndexOfAny(['\0','*','?'])>=0)throw new IOException("Недопустимый путь");
  foreach(var part in relative.Split('/',StringSplitOptions.RemoveEmptyEntries)){
   if(part.EndsWith('.')||part.EndsWith(' ')||part.EndsWith(".ra-part",StringComparison.OrdinalIgnoreCase))throw new IOException("Недопустимое имя");
   var name=part.Split('.')[0].ToUpperInvariant();if(name is "CON" or "PRN" or "AUX" or "NUL"||Enumerable.Range(1,9).Any(i=>name=="COM"+i||name=="LPT"+i))throw new IOException("Зарезервированное имя");
  }
  var full=Path.GetFullPath(Path.Combine(root,relative.Replace('/',Path.DirectorySeparatorChar)));
  if(!full.Equals(root,StringComparison.OrdinalIgnoreCase)&&!full.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new IOException("Выход из разрешённой папки");
  var current=full;while(current!=null){if((File.Exists(current)||Directory.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылки запрещены");current=Path.GetDirectoryName(current);}
  return full;
 }
 public async Task<FileReply> Execute(FileCommand command,CancellationToken ct){
  var p=Resolve(command.Path);
  switch(command.Op){
   case "list":
    var entries=new DirectoryInfo(p).EnumerateFileSystemInfos().Where(x=>(x.Attributes&FileAttributes.ReparsePoint)==0&&!x.Name.EndsWith(".ra-part",StringComparison.OrdinalIgnoreCase)).Take(2001).ToArray();
    if(entries.Length>2000)throw new IOException("В каталоге больше 2000 объектов. Разделите данные на подкаталоги; неполное копирование не выполняется.");
    return new(true,Items:entries.Select(x=>new FileItem(x.Name,x is DirectoryInfo,x is FileInfo f?f.Length:0)).ToArray());
   case "mkdir":Directory.CreateDirectory(p);return new(true);
   case "rename":if(p==root)throw new IOException("Корень защищён");var to=Resolve(command.Target);if(File.Exists(p))File.Move(p,to);else Directory.Move(p,to);return new(true);
   case "delete":if(p==root)throw new IOException("Корень защищён");if(Directory.Exists(p))Directory.Delete(p,false);else File.Delete(p);return new(true);
   case "begin":
    if(command.Size<0||command.Size>MaxFile||p==root||!Directory.Exists(Path.GetDirectoryName(p)))throw new IOException("Неверный размер или каталог");
    if(writes.Remove(p,out var old))old.Dispose();if(writes.Count>=4)throw new IOException("Слишком много передач");
    var part=p+".ra-part";if(File.Exists(part)&&(File.GetAttributes(part)&FileAttributes.ReparsePoint)!=0)throw new IOException("Ссылка запрещена");
    if(File.Exists(part)&&new FileInfo(part).LinkTarget!=null)throw new IOException("Ссылка запрещена");
    // Recreate with CreateNew for new files; refuse multiple hardlinks before opening an existing partial.
    var file=new FileStream(part,FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None,64*1024,FileOptions.Asynchronous);
    if(!SingleLink(file)){file.Dispose();throw new IOException("Hardlink запрещён");}
    if(file.Length>command.Size){file.Dispose();throw new IOException("Частичный файл больше исходного; удалите .ra-part локально");}writes[p]=file;return new(true,Offset:file.Length);
   case "write":
    if(!writes.TryGetValue(p,out var dest)||command.Offset!=dest.Length||command.Data.Length>5592408)throw new IOException("Неверный offset или передача");
    var data=Convert.FromBase64String(command.Data);if(data.Length>Chunk||dest.Length+data.Length>MaxFile)throw new IOException("Превышен лимит");dest.Position=dest.Length;await dest.WriteAsync(data,ct);return new(true,Offset:dest.Length);
   case "commit":
    if(!writes.Remove(p,out var completed))throw new IOException("Передача не открыта");
    try{await completed.FlushAsync(ct);if(completed.Length!=command.Size)throw new IOException("Не совпадает размер");completed.Position=0;var hash=Convert.ToHexString(await SHA256.HashDataAsync(completed,ct));if(!Identity.Equal(hash,command.Sha256.ToUpperInvariant()))throw new IOException("SHA-256 не совпадает");}finally{completed.Dispose();}
    if(File.Exists(p))throw new IOException("Файл существует: переименуйте или удалите его с подтверждением");File.Move(p+".ra-part",p);return new(true);
   case "read":
    await using(var source=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.Read,64*1024,FileOptions.Asynchronous)){
     if(!SingleLink(source)||source.Length>MaxFile||command.Offset<0||command.Offset>source.Length)throw new IOException("Неверный размер/offset или hardlink");
     source.Position=command.Offset;var buffer=new byte[(int)Math.Min(Chunk,source.Length-command.Offset)];await source.ReadExactlyAsync(buffer,ct);return new(true,Data:Convert.ToBase64String(buffer),Offset:source.Position,Size:source.Length);
    }
   case "hash":await using(var source=new FileStream(p,FileMode.Open,FileAccess.Read,FileShare.Read,64*1024,FileOptions.Asynchronous)){if(!SingleLink(source)||source.Length>MaxFile)throw new IOException("Лимит файла");return new(true,Size:source.Length,Sha256:Convert.ToHexString(await SHA256.HashDataAsync(source,ct)));}
   default:throw new IOException("Неизвестная файловая операция");
  }
 }
 [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]struct FileInfoNative {public uint Attributes;public System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;public uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;}
 [System.Runtime.InteropServices.DllImport("kernel32.dll",SetLastError=true)]static extern bool GetFileInformationByHandle(Microsoft.Win32.SafeHandles.SafeFileHandle handle,out FileInfoNative info);
 static bool SingleLink(FileStream file)=>GetFileInformationByHandle(file.SafeFileHandle,out var info)&&info.Links==1;
 public void Dispose(){foreach(var f in writes.Values)f.Dispose();writes.Clear();}
}
