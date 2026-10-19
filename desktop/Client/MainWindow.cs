using Message = RemoteAdmin.Shared.Message;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using RemoteAdmin.Shared;
using Channel=RemoteAdmin.Shared.Channel;

namespace RemoteAdmin.Client;
public sealed record Bookmark(string Name,string Host,int Port,string Fingerprint);
public sealed partial class MainWindow : Form {
 readonly string folder=LocalData.Folder("Client");
 readonly TextBox host=Ui.Text("",180),pin=Ui.Text("",510),code=Ui.Text("",280),outgoing=Ui.Text("",350);
 readonly NumericUpDown port=new(){Minimum=1,Maximum=65535,Value=4899,Width=80};
 readonly ComboBox bookmarks=new(){Width=200,DropDownStyle=ComboBoxStyle.DropDownList},monitors=new(){Width=200,DropDownStyle=ComboBoxStyle.DropDownList},quality=new(){Width=130,DropDownStyle=ComboBoxStyle.DropDownList};
 readonly CheckBox viewOnly=new(){Text="Только просмотр",AutoSize=true},files=new(){Text="Файлы",AutoSize=true},clipboard=new(){Text="Буфер обмена",AutoSize=true};
 readonly Label status=Ui.Label("Нет подключения");readonly RemoteScreen screen=new(){Dock=DockStyle.Fill};
 readonly TextBox chat=new(){Dock=DockStyle.Right,Width=240,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
 readonly ConcurrentDictionary<string,TaskCompletionSource<FileReply>> pending=new();
 readonly List<Bookmark> entries=[];
 CancellationTokenSource? connection;
 Channel? channel;Permissions granted;
 string sessionId="",lastClipboard="";
 readonly System.Windows.Forms.Timer clipboardTimer=new(){Interval=1500};
 long lastMove;
 bool busy,closing;
 public MainWindow(){
  Ui.Theme(this);Text="RemoteAdmin Client";Width=1250;Height=850;
  BuildLayout();
  bookmarks.SelectedIndexChanged+=(_,_)=>{if(bookmarks.SelectedIndex<0)return;var b=entries[bookmarks.SelectedIndex];host.Text=b.Host;port.Value=b.Port;pin.Text=b.Fingerprint;};
  monitors.SelectedIndexChanged+=async(_,_)=>await Quality();quality.SelectedIndexChanged+=async(_,_)=>await Quality();
  viewOnly.CheckedChanged+=async(_,_)=>{await Send("release",new{});if(viewOnly.Checked&&channel!=null){granted&=~Permissions.Control;await Send("viewonly",new{});}else if(channel!=null&&!granted.HasFlag(Permissions.Control)){status.Text="Для возврата управления подключитесь заново с подтверждением";}};
  screen.MouseDown+=async(_,e)=>{screen.Focus();await Mouse("down",e);};screen.MouseUp+=async(_,e)=>await Mouse("up",e);screen.MouseWheel+=async(_,e)=>await Mouse("wheel",e);
  screen.MouseMove+=async(_,e)=>{long now=Environment.TickCount64;if(now-lastMove<30)return;lastMove=now;await Mouse("move",e);};
  screen.KeyDown+=async(_,e)=>{if(CanControl){e.Handled=true;e.SuppressKeyPress=true;await Send("key",new KeyEvent((int)e.KeyCode,true));}};
  screen.KeyUp+=async(_,e)=>{if(CanControl){e.Handled=true;e.SuppressKeyPress=true;await Send("key",new KeyEvent((int)e.KeyCode,false));}};
  screen.LostFocus+=async(_,_)=>await Send("release",new{});Deactivate+=async(_,_)=>await Send("release",new{});
  clipboardTimer.Tick+=async(_,_)=>{if(!clipboard.Checked||!granted.HasFlag(Permissions.Clipboard)||channel==null)return;try{if(Clipboard.ContainsText()){var value=Clipboard.GetText();if(value!=lastClipboard&&System.Text.Encoding.UTF8.GetByteCount(value)<=1024*1024){lastClipboard=value;await Send("clipboard",new{text=value});}}}catch{}};clipboardTimer.Start();
  FormClosing+=(_,_)=>{closing=true;Disconnect();clipboardTimer.Dispose();};LoadBookmarks();
 }
 bool CanControl=>!viewOnly.Checked&&granted.HasFlag(Permissions.Control)&&channel!=null;
 void LoadBookmarks(){try{var file=Path.Combine(folder,"addressbook.json");if(File.Exists(file))entries.AddRange(JsonSerializer.Deserialize<List<Bookmark>>(File.ReadAllText(file))??[]);}catch{status.Text="Не удалось прочитать адресную книгу";}RefreshBookmarks();}
 void RefreshBookmarks(){bookmarks.Items.Clear();bookmarks.Items.AddRange(entries.Select(x=>(object)x.Name).ToArray());RefreshDeviceList();}
 void SaveBookmark(){
  if(string.IsNullOrWhiteSpace(host.Text)){status.Text="Введите адрес компьютера, который хотите сохранить.";host.Focus();return;}
  if(!ValidPin(pin.Text)){status.Text="Для сохранения нужен ключ безопасности с сервера.";pin.Focus();return;}
  var name=Ui.Ask(this,"Сохранить компьютер","Название в списке компьютеров",host.Text);if(string.IsNullOrWhiteSpace(name))return;
  try{entries.RemoveAll(x=>x.Host==host.Text.Trim()&&x.Port==(int)port.Value);entries.Add(new(name.Trim(),host.Text.Trim(),(int)port.Value,NormalizePin(pin.Text)));File.WriteAllText(Path.Combine(folder,"addressbook.json"),JsonSerializer.Serialize(entries));RefreshBookmarks();status.Text="Компьютер сохранён. В следующий раз понадобится только новый код.";}catch(Exception e){status.Text="Не удалось сохранить компьютер: "+e.Message;}
 }
 void DeleteBookmark(){
  if(deviceList.SelectedItem is not Bookmark selected)return;
  if(MessageBox.Show(this,"Удалить «"+selected.Name+"» из списка? Это не удалит файлы на компьютере.","Удалить компьютер",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
  try{entries.Remove(selected);File.WriteAllText(Path.Combine(folder,"addressbook.json"),JsonSerializer.Serialize(entries));RefreshBookmarks();status.Text="Компьютер удалён из списка.";}catch(Exception e){status.Text="Не удалось сохранить список: "+e.Message;}
 }
 static string NormalizePin(string value)=>value.Replace(" ","").Replace(":","").ToUpperInvariant();
 static bool ValidPin(string value){var v=NormalizePin(value);return v.Length==64&&v.All(Uri.IsHexDigit);}
 async Task Connect(){
  if(busy||connection!=null)return;
  code.Text=code.Text.Replace(" ","").Replace("-","").Trim().ToUpperInvariant();
  if(string.IsNullOrWhiteSpace(host.Text)){FieldError(host,"Введите IP-адрес или имя удалённого компьютера.");return;}
  if(!ValidPin(pin.Text)){FieldError(pin,"Вставьте ключ безопасности с сервера или используйте приглашение.");return;}
  if(code.Text.Length!=32||!code.Text.All(Uri.IsHexDigit)){FieldError(code,"Нужен одноразовый код с сервера: 32 символа. Проще вставить приглашение.");return;}
  busy=true;RefreshLayoutState();var cts=new CancellationTokenSource();connection=cts;var token=cts.Token;Task? heartbeat=null;
  try{
   status.Text="TLS: подключение и проверка сертификата…";using var tcp=new TcpClient();tcp.NoDelay=true;await tcp.ConnectAsync(host.Text.Trim(),(int)port.Value,token).AsTask().WaitAsync(TimeSpan.FromSeconds(10),token);
   var expected=NormalizePin(pin.Text);
   using var ssl=new SslStream(tcp.GetStream(),false,(_,cert,_,_)=>{if(cert==null)return false;using var x=new X509Certificate2(cert);return DateTime.UtcNow>=x.NotBefore.ToUniversalTime()&&DateTime.UtcNow<=x.NotAfter.ToUniversalTime()&&Identity.Equal(expected,Identity.Fingerprint(x));});
   using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(10));await ssl.AuthenticateAsClientAsync(new SslClientAuthenticationOptions{TargetHost=host.Text.Trim(),EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13,CertificateRevocationCheckMode=X509RevocationMode.NoCheck},deadline.Token);}
   await using var wire=new Channel(ssl);channel=wire;
   var requested=Permissions.View|Permissions.Chat;if(!viewOnly.Checked)requested|=Permissions.Control;if(files.Checked)requested|=Permissions.Files;if(clipboard.Checked)requested|=Permissions.Clipboard;
   await wire.Send("hello",new Hello(code.Text.Trim().ToUpperInvariant(),Environment.UserName,requested),token);code.Clear();status.Text="Ожидание подтверждения на сервере…";
   Message first;using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(75));first=await wire.Receive(deadline.Token,75);}
   if(first.Type!="welcome")throw new IOException(first.Data.TryGetProperty("message",out var error)?error.GetString():"Доступ отклонён");
   var welcome=first.Read<Welcome>();granted=welcome.Granted;sessionId=welcome.SessionId;monitors.Items.Clear();monitors.Items.AddRange(welcome.Displays.Select(d=>(object)$"{d.Index+1}: {d.Width}×{d.Height}").ToArray());if(monitors.Items.Count>0)monitors.SelectedIndex=0;
   LocalData.Audit(folder,"session.start",sessionId,"connected");status.Text="Защищённое соединение · "+(granted.HasFlag(Permissions.Control)?"Управление разрешено":"Только просмотр");Ui.Hint(status,Ui.Rights(granted));RefreshLayoutState();
   heartbeat=Task.Run(async()=>{try{while(!token.IsCancellationRequested){await Task.Delay(10000,token);await wire.Send("ping",new{},token);}}catch{cts.Cancel();}},token);
   while(!token.IsCancellationRequested){var msg=await wire.Receive(token);switch(msg.Type){
    case "frame":var frame=msg.Read<ScreenFrame>();var data=Convert.FromBase64String(frame.Jpeg);using(var ms=new MemoryStream(data))using(var image=Image.FromStream(ms)){if(image.Width>7680||image.Height>4320)throw new InvalidDataException("Слишком большой кадр");screen.Frame=new Bitmap(image);}break;
    case "ping":await wire.Send("pong",new{},token);break;
    case "pong":break;
    case "chat":var text=msg.Data.GetProperty("message").GetString()??"";if(text.Length>8192)throw new InvalidDataException();chat.AppendText($"Сервер · {DateTime.Now:HH:mm}\r\n{text}\r\n\r\n");break;
    case "clipboard":if(granted.HasFlag(Permissions.Clipboard)&&clipboard.Checked){var textValue=msg.Data.GetProperty("text").GetString()??"";if(System.Text.Encoding.UTF8.GetByteCount(textValue)>1024*1024)throw new InvalidDataException();lastClipboard=textValue;if(textValue.Length==0)Clipboard.Clear();else Clipboard.SetText(textValue);}break;
    case "file.reply":if(pending.TryRemove(msg.Id,out var promise))promise.TrySetResult(msg.Read<FileReply>());break;
    case "error":throw new IOException(msg.Data.GetProperty("message").GetString());
    case "terminate":return;
    default:throw new InvalidDataException("Неизвестный ответ сервера");
   }}
  }catch(OperationCanceledException){if(!closing)status.Text="Соединение завершено";}catch(Exception e){if(!closing)status.Text=ConnectionError(e);}
  finally{cts.Cancel();if(heartbeat!=null)try{await heartbeat;}catch{}foreach(var item in pending.Values)item.TrySetCanceled();pending.Clear();channel=null;granted=Permissions.None;connection=null;cts.Dispose();busy=false;screen.Frame=null;if(sessionId!=""){LocalData.Audit(folder,"session.end",sessionId,"closed");sessionId="";}if(!closing){RefreshLayoutState();status.Text+=" · отключено";}}
 }
 void Disconnect(){connection?.Cancel();}
 async Task Send(string type,object data){var wire=channel;var ct=connection;if(wire==null||ct==null)return;try{await wire.Send(type,data,ct.Token);}catch{ct.Cancel();}}
 async Task Mouse(string action,MouseEventArgs e){if(!CanControl)return;if(!screen.Coordinates(e.Location,out var x,out var y)){if(action=="up")await Send("release",new{});return;}int button=e.Button==MouseButtons.Right?1:e.Button==MouseButtons.Middle?2:0;await Send("mouse",new MouseEvent(action,x,y,button,e.Delta));}
 async Task Quality(){if(channel==null||monitors.SelectedIndex<0)return;var q=quality.SelectedIndex;await Send("quality",new Quality(monitors.SelectedIndex,q==0?5:q==2?20:10,q==0?35:q==2?85:65));}
 void OpenFiles(){if(channel==null||!granted.HasFlag(Permissions.Files)){status.Text="Передача файлов не разрешена сервером";return;}new FileWindow(RequestFile).Show(this);}
 async Task<FileReply> RequestFile(FileCommand command,CancellationToken ct){
  var wire=channel??throw new IOException("Нет подключения");var id=Guid.NewGuid().ToString("N");var tcs=new TaskCompletionSource<FileReply>(TaskCreationOptions.RunContinuationsAsynchronously);pending[id]=tcs;
  try{await wire.Send("file",command,ct,id);var reply=await tcs.Task.WaitAsync(TimeSpan.FromMinutes(5),ct);if(!reply.Ok)throw new IOException(reply.Error);return reply;}finally{pending.TryRemove(id,out _);}
 }
}
