using System.Text.Json;
namespace RemoteAdmin.Shared;

public sealed class ServerTrust {
 readonly string path;
 readonly Dictionary<string,string> known;
 public ServerTrust(string folder){path=Path.Combine(folder,"trusted-servers.json");known=File.Exists(path)?JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(path))??throw new InvalidDataException("Повреждён список доверенных серверов"):new();}
 static string Endpoint(string host,int port)=>host.Trim().Trim('[',']').ToLowerInvariant()+":"+port;
 public string? Get(string host,int port)=>known.GetValueOrDefault(Endpoint(host,port));
 public void Forget(string host,int port){
  var updated=new Dictionary<string,string>(known);updated.Remove(Endpoint(host,port));var temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(updated));File.Move(temporary,path,true);known.Remove(Endpoint(host,port));
 }
 public void Remember(string host,int port,string fingerprint){
  var key=Endpoint(host,port);if(known.TryGetValue(key,out var previous)&&!Identity.Equal(previous,fingerprint))throw new InvalidOperationException("Ключ сервера изменился. Автоматическая замена запрещена.");
  var updated=new Dictionary<string,string>(known){[key]=fingerprint};var temporary=path+".tmp";File.WriteAllText(temporary,JsonSerializer.Serialize(updated));File.Move(temporary,path,true);known[key]=fingerprint;
 }
}
