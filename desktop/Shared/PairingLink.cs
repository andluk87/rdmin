namespace RemoteAdmin.Shared;

public sealed record PairingDetails(string Host,int Port,string Fingerprint,string Code);
public static class PairingLink {
 public static string Create(string host,int port,string fingerprint,string code)=>
  "remoteadmin://connect?host="+Uri.EscapeDataString(host)+"&port="+port+"&fingerprint="+Uri.EscapeDataString(fingerprint)+"&code="+Uri.EscapeDataString(code);
 public static PairingDetails Parse(string text){
  if(text.Length>4096||!Uri.TryCreate(text.Trim(),UriKind.Absolute,out var uri)||uri.Scheme!="remoteadmin"||uri.Host!="connect")throw new FormatException("Скопируйте данные кнопкой «Пригласить оператора» на сервере.");
  var values=new Dictionary<string,string>(StringComparer.Ordinal);
  foreach(var pair in uri.Query.TrimStart('?').Split('&')){var parts=pair.Split('=',2);if(parts.Length!=2||!values.TryAdd(parts[0],Uri.UnescapeDataString(parts[1])))throw new FormatException("Некорректные данные подключения");}
  if(!values.TryGetValue("host",out var host)||string.IsNullOrWhiteSpace(host)||host.Length>255||!values.TryGetValue("port",out var port)||!int.TryParse(port,out var number)||number<1||number>65535||!values.TryGetValue("fingerprint",out var pin)||pin.Length!=64||!pin.All(Uri.IsHexDigit)||!values.TryGetValue("code",out var code)||code.Length!=32||!code.All(Uri.IsHexDigit))throw new FormatException("Неполные данные подключения. Создайте новое приглашение на сервере.");
  return new(host,number,pin.ToUpperInvariant(),code.ToUpperInvariant());
 }
}
