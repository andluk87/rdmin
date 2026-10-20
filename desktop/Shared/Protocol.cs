using System.Buffers.Binary;
using System.Net.Security;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RemoteAdmin.Shared;

public sealed record Message {
 public int Version { get; init; } = 1;
 public string Type { get; init; } = "";
 public string Id { get; init; } = Guid.NewGuid().ToString("N");
 public JsonElement Data { get; init; }
 public static Message Of(string type, object data, string? id = null) => new() { Type=type, Data=JsonSerializer.SerializeToElement(data, Wire.Json), Id=id??Guid.NewGuid().ToString("N") };
 public T Read<T>() => Data.Deserialize<T>(Wire.Json) ?? throw new InvalidDataException("Пустое сообщение");
}
public static class Wire {
 public const int MaxMessage = 8 * 1024 * 1024;
 public static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy=JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive=false, UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow };
}
public sealed class Channel : IAsyncDisposable {
 readonly SslStream stream;
 readonly SemaphoreSlim writer = new(1);
 public Channel(SslStream stream) { this.stream=stream; }
 public async Task Send(string type, object data, CancellationToken ct, string? id=null) => await Send(Message.Of(type,data,id),ct);
 public async Task Send(Message message, CancellationToken ct) {
  var bytes=JsonSerializer.SerializeToUtf8Bytes(message,Wire.Json);
  if(bytes.Length>Wire.MaxMessage) throw new InvalidDataException("Слишком большое сообщение");
  var header=new byte[4];BinaryPrimitives.WriteInt32BigEndian(header,bytes.Length);
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(15));
  await writer.WaitAsync(deadline.Token);
  try { await stream.WriteAsync(header,deadline.Token);await stream.WriteAsync(bytes,deadline.Token);await stream.FlushAsync(deadline.Token); }
  finally { writer.Release(); }
 }
 public async Task<Message> Receive(CancellationToken ct, int timeoutSeconds=35) {
  using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
  var header=new byte[4];await stream.ReadExactlyAsync(header,deadline.Token);
  var n=BinaryPrimitives.ReadInt32BigEndian(header);if(n<2||n>Wire.MaxMessage)throw new InvalidDataException("Неверный размер пакета");
  var body=new byte[n];await stream.ReadExactlyAsync(body,deadline.Token);
  var message=JsonSerializer.Deserialize<Message>(body,Wire.Json)??throw new InvalidDataException();
  if(message.Version!=1||message.Type.Length>64||message.Id.Length>64)throw new InvalidDataException("Неверный протокол");return message;
 }
 public async ValueTask DisposeAsync(){ await stream.DisposeAsync(); }
}
[Flags] public enum Permissions { None=0, View=1, Control=2, Clipboard=4, Files=8, Chat=16 }
public sealed record Hello(string Code,string Operator,Permissions Requested,string AuthMode="invitation",string Username="",string Password="");
public sealed record AuthenticationOptions(bool Accounts,bool Invitation);
public sealed record Welcome(string SessionId,Permissions Granted,DisplayInfo[] Displays,string Capture);
public sealed record DisplayInfo(int Index,string Name,int X,int Y,int Width,int Height);
public sealed record ScreenFrame(int Monitor,int Width,int Height,string Jpeg);
public sealed record MouseEvent(string Action,double X,double Y,int Button=0,int Delta=0);
public sealed record KeyEvent(int Key,bool Down);
public sealed record FileCommand(string Op,string Path="",string Target="",long Offset=0,string Data="",long Size=0,string Sha256="");
public sealed record FileItem(string Name,bool Directory,long Size);
public sealed record FileReply(bool Ok,string Error="",FileItem[]? Items=null,long Offset=0,string Data="",long Size=0,string Sha256="");
public sealed record Quality(int Monitor,int Fps,int Jpeg);
