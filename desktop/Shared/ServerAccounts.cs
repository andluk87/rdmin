using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RemoteAdmin.Shared;
public enum AuthenticationMode { Accounts, Invitation, Both }
public sealed record ServerAccount(string Id,string Username,string Salt,string PasswordHash,int Iterations,Permissions Rights,bool RequireApproval=true,bool Enabled=true);
public sealed record ServerSettings(int Port=4899,AuthenticationMode Authentication=AuthenticationMode.Accounts,Permissions SupportRights=Permissions.View|Permissions.Control|Permissions.Chat,ServerAccount[]? Accounts=null);

public sealed class ServerAccounts {
 public const int PasswordIterations=600000;
 readonly string path;
 public ServerSettings Settings {get;private set;}
 public ServerAccount[] Accounts=>Settings.Accounts??[];
 public bool AllowsAccounts=>Settings.Authentication is AuthenticationMode.Accounts or AuthenticationMode.Both;
 public bool AllowsInvitations=>Settings.Authentication is AuthenticationMode.Invitation or AuthenticationMode.Both;
 static readonly byte[] DummySalt=RandomNumberGenerator.GetBytes(16);
 public ServerAccounts(string folder){
  path=Path.Combine(folder,"access.dpapi");
  Settings=File.Exists(path)?JsonSerializer.Deserialize<ServerSettings>(Identity.Unseal(File.ReadAllBytes(path)),Wire.Json)??throw new InvalidDataException("Повреждены настройки доступа"):new();
  if(Settings.Port<1024||Settings.Port>65535||!Enum.IsDefined(Settings.Authentication)||Accounts.Length>100||Accounts.Any(a=>!ValidUsername(a.Username)||a.Iterations!=PasswordIterations))throw new InvalidDataException("Некорректные настройки доступа");
 }
 public void SaveSettings(int port,AuthenticationMode mode,Permissions support){
  if(port<1024||port>65535||!Enum.IsDefined(mode))throw new ArgumentException("Недопустимые настройки сервера");Save(Settings with{Port=port,Authentication=mode,SupportRights=support});
 }
 public static bool ValidUsername(string value)=>value.Length is >0 and <=64&&value==value.Trim()&&value.All(c=>char.IsLetterOrDigit(c)||c is '.' or '_' or '-');
 public ServerAccount Put(string? id,string username,string? password,Permissions rights,bool approval,bool enabled){
  username=username.Trim();if(!ValidUsername(username))throw new ArgumentException("Логин: 1–64 буквы, цифры, точка, дефис или подчёркивание.");
  var old=Accounts.FirstOrDefault(a=>a.Id==id);if(Accounts.Any(a=>a.Id!=id&&a.Username.Equals(username,StringComparison.OrdinalIgnoreCase)))throw new ArgumentException("Такой логин уже существует.");
  if(old==null&&Accounts.Length>=100)throw new ArgumentException("Достигнут лимит 100 учётных записей.");
  const Permissions all=Permissions.View|Permissions.Control|Permissions.Files|Permissions.Chat|Permissions.Clipboard;
  if((rights&~all)!=0||(rights.HasFlag(Permissions.Control)&&!rights.HasFlag(Permissions.View)))throw new ArgumentException("Для управления нужно разрешить просмотр экрана.");
  string salt,hash;
  if(!string.IsNullOrEmpty(password)){
   if(password.Length<12||password.Length>256)throw new ArgumentException("Пароль должен содержать от 12 до 256 символов.");
   var bytes=RandomNumberGenerator.GetBytes(16);salt=Convert.ToBase64String(bytes);hash=Convert.ToBase64String(Rfc2898DeriveBytes.Pbkdf2(password,bytes,PasswordIterations,HashAlgorithmName.SHA256,32));
  }else if(old!=null){salt=old.Salt;hash=old.PasswordHash;}else throw new ArgumentException("Укажите пароль новой учётной записи.");
  var account=new ServerAccount(old?.Id??Guid.NewGuid().ToString(),username,salt,hash,PasswordIterations,rights,approval,enabled);
  Save(Settings with{Accounts=Accounts.Where(a=>a.Id!=account.Id).Append(account).ToArray()});return account;
 }
 public void Delete(string id)=>Save(Settings with{Accounts=Accounts.Where(a=>a.Id!=id).ToArray()});
 public ServerAccount? Authenticate(string username,string password){
  if(username.Length>64||password.Length>256)return null;
  // Caller runs this off the UI thread. Missing/disabled users do equivalent KDF work.
  var account=Accounts.FirstOrDefault(a=>a.Username.Equals(username,StringComparison.OrdinalIgnoreCase));
  var salt=account!=null?Convert.FromBase64String(account.Salt):DummySalt;
  var candidate=Rfc2898DeriveBytes.Pbkdf2(password,salt,PasswordIterations,HashAlgorithmName.SHA256,32);
  var expected=account!=null?Convert.FromBase64String(account.PasswordHash):new byte[32];
  bool valid=CryptographicOperations.FixedTimeEquals(candidate,expected);CryptographicOperations.ZeroMemory(candidate);
  return valid&&account?.Enabled==true?account:null;
 }
 void Save(ServerSettings settings){
  var bytes=JsonSerializer.SerializeToUtf8Bytes(settings,Wire.Json);var encrypted=Identity.Seal(bytes);CryptographicOperations.ZeroMemory(bytes);
  var temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try{File.WriteAllBytes(temporary,encrypted);File.Move(temporary,path,true);Settings=settings;}finally{if(File.Exists(temporary))File.Delete(temporary);}
 }
}

public sealed class LoginLimiter {
 readonly Dictionary<string,Queue<DateTimeOffset>> attempts=new();
 readonly Queue<DateTimeOffset> global=new();
 public bool Allow(string address,DateTimeOffset now){
  foreach(var key in attempts.Keys.ToArray()){var q=attempts[key];while(q.TryPeek(out var oldestPeer)&&now-oldestPeer>TimeSpan.FromMinutes(1))q.Dequeue();if(q.Count==0)attempts.Remove(key);}
  while(global.TryPeek(out var oldestGlobal)&&now-oldestGlobal>TimeSpan.FromMinutes(1))global.Dequeue();
  if(global.Count>=60||attempts.Count>=2048)return false;
  if(!attempts.TryGetValue(address,out var queue)){queue=new();attempts[address]=queue;}
  if(queue.Count>=5)return false;queue.Enqueue(now);global.Enqueue(now);return true;
 }
}
