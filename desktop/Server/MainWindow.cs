using Message = RemoteAdmin.Shared.Message;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using RemoteAdmin.Shared;
using Channel=RemoteAdmin.Shared.Channel;

namespace RemoteAdmin.Server;
public sealed class MainWindow : Form {
 readonly string folder=LocalData.Folder("Server");
 readonly TextBox fingerprint=Ui.Text("",650),code=Ui.Text("",360),chat=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical},text=Ui.Text("",350);
 readonly NumericUpDown port=new(){Minimum=1024,Maximum=65535,Value=4899,Width=85};
 readonly Label status=Ui.Label("Доступ выключен");
 readonly CheckBox control=new(){Text="Управление",Checked=true,AutoSize=true},files=new(){Text="Файлы",Checked=false,AutoSize=true},clipboard=new(){Text="Буфер обмена",Checked=false,AutoSize=true};
 readonly X509Certificate2 certificate;
 readonly string share=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"RemoteAdminShare");
 CancellationTokenSource? running,session;
 TcpListener? listener;
 Channel? channel;
 string pairing="",sessionId="";
 DateTimeOffset codeExpires;
 int monitor=0,fps=10,jpeg=65;
 Permissions granted;
 bool closing;
 readonly System.Windows.Forms.Timer clipboardTimer=new(){Interval=1500};
 string lastClipboard="";
 public MainWindow(){
  Text="RemoteAdmin Server — разрешённый удалённый доступ";Width=900;Height=680;Ui.Theme(this);certificate=Identity.Load(folder);
  fingerprint.Text=Identity.Fingerprint(certificate);fingerprint.ReadOnly=true;code.ReadOnly=true;
  var header=Ui.Row();header.Controls.Add(Ui.Label("REMOTEADMIN / SERVER"));header.Controls.Add(status);
  var identity=Ui.Row();identity.Controls.Add(Ui.Label("SHA-256 сертификата — сравните на клиенте:"));identity.Controls.Add(fingerprint);
  var pairingRow=Ui.Row();pairingRow.Controls.Add(Ui.Label("Одноразовый код (5 минут):"));pairingRow.Controls.Add(code);pairingRow.Controls.Add(Ui.Button("Новый код",(_,_)=>NewCode()));
  var access=Ui.Row();access.Controls.Add(Ui.Label("TCP порт:"));access.Controls.Add(port);access.Controls.Add(control);access.Controls.Add(files);access.Controls.Add(clipboard);
  access.Controls.Add(Ui.Button("Включить доступ",(_,_)=>Start()));access.Controls.Add(Ui.Button("Выключить доступ",(_,_)=>Stop()));access.Controls.Add(Ui.Button("Завершить сеанс",(_,_)=>session?.Cancel()));
  var shared=Ui.Row();shared.Controls.Add(Ui.Label("Файлы доступны только в: "+share));
  var send=Ui.Row();send.Dock=DockStyle.Bottom;send.Controls.Add(text);send.Controls.Add(Ui.Button("Отправить в чат",async(_,_)=>{if(channel!=null&&session!=null){try{await channel.Send("chat",new{message=text.Text},session.Token);chat.AppendText("Вы: "+text.Text+Environment.NewLine);text.Clear();}catch{session.Cancel();}}}));
  Controls.Add(chat);Controls.Add(send);Controls.Add(shared);Controls.Add(access);Controls.Add(pairingRow);Controls.Add(identity);Controls.Add(header);
  control.CheckedChanged+=(_,_)=>session?.Cancel();files.CheckedChanged+=(_,_)=>session?.Cancel();clipboard.CheckedChanged+=(_,_)=>session?.Cancel();
  clipboardTimer.Tick+=async(_,_)=>{if(channel==null||session==null||!granted.HasFlag(Permissions.Clipboard))return;try{if(Clipboard.ContainsText()){var s=Clipboard.GetText();if(s!=lastClipboard&&System.Text.Encoding.UTF8.GetByteCount(s)<=1024*1024){lastClipboard=s;await channel.Send("clipboard",new{text=s},session.Token);}}}catch{}};clipboardTimer.Start();
  FormClosing+=(_,_)=>{closing=true;Stop();certificate.Dispose();clipboardTimer.Dispose();};NewCode();
 }
 void NewCode(){pairing=Identity.Code();code.Text=pairing;codeExpires=DateTimeOffset.UtcNow.AddMinutes(5);}
 void Start(){if(running!=null)return;try{listener=new(IPAddress.IPv6Any,(int)port.Value);listener.Server.DualMode=true;listener.Start(8);running=new();status.Text="Ожидание подключения · порт "+port.Value;port.Enabled=false;_ = AcceptLoop(running.Token);}catch(Exception e){listener?.Stop();listener=null;status.Text=e.Message;}}
 void Stop(){session?.Cancel();running?.Cancel();listener?.Stop();running=null;listener=null;if(!closing){status.Text="Доступ выключен";port.Enabled=true;}}
 async Task AcceptLoop(CancellationToken ct){
  while(!ct.IsCancellationRequested){TcpClient? tcp=null;try{tcp=await listener!.AcceptTcpClientAsync(ct);tcp.NoDelay=true;using(tcp){await Serve(tcp,ct);}}catch(OperationCanceledException){break;}catch(Exception e){if(!closing)await Ui.OnUi(this,()=>{status.Text="Подключение завершено: "+e.Message;return true;});}finally{tcp?.Dispose();session=null;}}
 }
 async Task Serve(TcpClient tcp,CancellationToken ct){
  // One connection at a time, bounded handshake and pairing deadlines.
  using var sessionCts=CancellationTokenSource.CreateLinkedTokenSource(ct);sessionCts.CancelAfter(TimeSpan.FromHours(1));session=sessionCts;var token=sessionCts.Token;
  using var ssl=new SslStream(tcp.GetStream(),false);using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(10));await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13,ClientCertificateRequired=false,CertificateRevocationCheckMode=X509RevocationMode.NoCheck},deadline.Token);}
  await using var wire=new Channel(ssl);using var input=new RemoteInput();using var sandbox=new FileSandbox(share);
  Task? frames=null;Task? heartbeat=null;var approved=false;
  try{
   Message first;using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(15));first=await wire.Receive(deadline.Token);}
   if(first.Type!="hello")return;var hello=first.Read<Hello>();
   var correct=await Ui.OnUi(this,()=>{var ok=DateTimeOffset.UtcNow<codeExpires&&Identity.Equal(pairing,hello.Code);if(ok){pairing="";code.Text="Код использован";}return ok;});
   if(!correct){await wire.Send("error",new{message="Неверный, использованный или просроченный код"},token);await Task.Delay(1500,token);return;}
   if(hello.Operator.Length>128||(hello.Requested&~(Permissions.View|Permissions.Control|Permissions.Files|Permissions.Clipboard|Permissions.Chat))!=0)throw new InvalidDataException("Неверные права");
   sessionId=Guid.NewGuid().ToString();
   approved=await Ui.OnUi(this,()=>{
    var allowed=Permissions.View|Permissions.Chat;if(control.Checked)allowed|=Permissions.Control;if(files.Checked)allowed|=Permissions.Files;if(clipboard.Checked)allowed|=Permissions.Clipboard;
    granted=hello.Requested&allowed;status.Text="Запрос от "+hello.Operator;
    var result=Ui.Consent(this,$"Оператор: {hello.Operator}\nПрава: {granted}\nРазрешить доступ на срок до 1 часа?\n\nВы можете завершить сеанс в любой момент.");
    if(result){status.Text="АКТИВНЫЙ СЕАНС · "+hello.Operator;BackColor=Color.FromArgb(45,30,30);}return result;
   }).WaitAsync(TimeSpan.FromSeconds(65),token);
   if(!approved){await wire.Send("rejected",new{message="Доступ отклонён"},token);return;}
   LocalData.Audit(folder,"session.start",sessionId,"approved");channel=wire;monitor=0;fps=10;jpeg=65;
   await wire.Send("welcome",new Welcome(sessionId,granted,DesktopCapture.Displays(),"DXGI / GDI fallback"),token);
   frames=Task.Run(async()=>{using var capture=new DesktopCapture();try{while(!token.IsCancellationRequested){if(granted.HasFlag(Permissions.View)){var frame=capture.Grab(Volatile.Read(ref monitor),Volatile.Read(ref jpeg));if(frame!=null)await wire.Send("frame",frame,token);}await Task.Delay(1000/Math.Clamp(Volatile.Read(ref fps),1,30),token);}}catch(Exception e){try{await wire.Send("error",new{message=e.Message},token);}catch{}sessionCts.Cancel();}},token);
   heartbeat=Task.Run(async()=>{try{while(!token.IsCancellationRequested){await Task.Delay(10000,token);await wire.Send("ping",new{},token);}}catch{sessionCts.Cancel();}},token);
   while(!token.IsCancellationRequested){var msg=await wire.Receive(token);
    switch(msg.Type){
     case "ping":await wire.Send("pong",new{},token);break;
     case "pong":break;
     case "terminate":return;
     case "mouse":Require(Permissions.Control);input.Mouse(msg.Read<MouseEvent>(),Volatile.Read(ref monitor));break;
     case "key":Require(Permissions.Control);input.Key(msg.Read<KeyEvent>());break;
     case "release":input.Release();break;
     case "viewonly":input.Release();granted&=~Permissions.Control;break;
     case "text":Require(Permissions.Control);input.Text(msg.Data.GetProperty("text").GetString()??"");break;
     case "quality":var q=msg.Read<Quality>();if(q.Monitor<0||q.Monitor>=Screen.AllScreens.Length)throw new InvalidDataException("Неверный монитор");input.Release();Volatile.Write(ref monitor,q.Monitor);Volatile.Write(ref fps,Math.Clamp(q.Fps,1,30));Volatile.Write(ref jpeg,Math.Clamp(q.Jpeg,20,90));break;
     case "chat":Require(Permissions.Chat);var message=msg.Data.GetProperty("message").GetString()??"";if(message.Length>8192)throw new InvalidDataException();await Ui.OnUi(this,()=>{chat.AppendText(hello.Operator+": "+message+Environment.NewLine);return true;});break;
     case "clipboard":Require(Permissions.Clipboard);var value=msg.Data.GetProperty("text").GetString()??"";if(System.Text.Encoding.UTF8.GetByteCount(value)>1024*1024)throw new InvalidDataException();await Ui.OnUi(this,()=>{lastClipboard=value;if(value.Length==0)Clipboard.Clear();else Clipboard.SetText(value);return true;});break;
     case "file":Require(Permissions.Files);var cmd=msg.Read<FileCommand>();FileReply reply;try{
      if(cmd.Op=="delete"&&!await Ui.OnUi(this,()=>MessageBox.Show(this,"Удалить из общей папки: "+cmd.Path+"?","Файловая операция",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes))reply=new(false,"Удаление отклонено пользователем");else reply=await sandbox.Execute(cmd,token);
      LocalData.Audit(folder,"file."+cmd.Op,sessionId,reply.Ok?"success":"rejected");
     }catch(Exception e) when(e is IOException or UnauthorizedAccessException or FormatException){reply=new(false,e.Message);LocalData.Audit(folder,"file."+cmd.Op,sessionId,"failed");}await wire.Send("file.reply",reply,token,msg.Id);break;
     default:throw new InvalidDataException("Неизвестный тип сообщения");
    }
   }
  }finally{
   sessionCts.Cancel();input.Release();if(frames!=null)try{await frames;}catch{}if(heartbeat!=null)try{await heartbeat;}catch{}channel=null;session=null;
   if(approved)LocalData.Audit(folder,"session.end",sessionId,"closed");if(!closing)await Ui.OnUi(this,()=>{BackColor=Ui.Background;status.Text=running!=null?"Ожидание подключения":"Доступ выключен";NewCode();return true;});
  }
 }
 void Require(Permissions permission){if(!granted.HasFlag(permission))throw new UnauthorizedAccessException("Нет разрешения: "+permission);}
}
