using Message = RemoteAdmin.Shared.Message;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using RemoteAdmin.Shared;
using Channel=RemoteAdmin.Shared.Channel;

namespace RemoteAdmin.Server;
public sealed partial class MainWindow : Form {
 readonly string folder=LocalData.Folder("Server");
 readonly TextBox fingerprint=Ui.Text("",650),code=Ui.Text("",360),chat=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical},text=Ui.Text("",350);
 readonly NumericUpDown port=new(){Minimum=1024,Maximum=65535,Value=4899,Width=85};
 readonly Label status=Ui.Label("Доступ выключен");
 readonly CheckBox control=new(){Text="Управление",Checked=true,AutoSize=true},files=new(){Text="Файлы",Checked=false,AutoSize=true},clipboard=new(){Text="Буфер обмена",Checked=false,AutoSize=true};
 readonly X509Certificate2 certificate;
 readonly ServerAccounts accounts;
 readonly LoginLimiter loginLimiter=new();
 string activeUser="",connectionPhase="accept";
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
  accounts=new ServerAccounts(folder);port.Value=accounts.Settings.Port;
  fingerprint.Text=Identity.Fingerprint(certificate);fingerprint.ReadOnly=true;code.ReadOnly=true;
  BuildLayout();
  control.CheckedChanged+=(_,_)=>SaveAccessSettings();files.CheckedChanged+=(_,_)=>SaveAccessSettings();clipboard.CheckedChanged+=(_,_)=>SaveAccessSettings();
  clipboardTimer.Tick+=async(_,_)=>{if(channel==null||session==null||!granted.HasFlag(Permissions.Clipboard))return;try{if(Clipboard.ContainsText()){var s=Clipboard.GetText();if(s!=lastClipboard&&System.Text.Encoding.UTF8.GetByteCount(s)<=1024*1024){lastClipboard=s;await channel.Send("clipboard",new{text=s},session.Token);}}}catch{}};clipboardTimer.Start();
  FormClosing+=(_,_)=>{closing=true;Stop();certificate.Dispose();clipboardTimer.Dispose();};NewCode();
 }
 void NewCode(){pairing=Identity.Code();code.Text=pairing;codeExpires=DateTimeOffset.UtcNow.AddMinutes(5);}
 void Start(){if(running!=null)return;if(!SaveAccessSettings())return;if(accounts.AllowsAccounts&&!accounts.AllowsInvitations&&!accounts.Accounts.Any(a=>a.Enabled)){status.Text="Создайте и включите хотя бы одну учётную запись сервера.";return;}try{listener=new(IPAddress.IPv6Any,(int)port.Value);listener.Server.DualMode=true;listener.Start(8);running=new();status.Text="Ожидание подключения · порт "+port.Value;port.Enabled=false;_ = AcceptLoop(running.Token);}catch(Exception e){listener?.Stop();listener=null;status.Text=e.Message;}}
 void Stop(){session?.Cancel();running?.Cancel();listener?.Stop();running=null;listener=null;if(!closing){status.Text="Доступ выключен";port.Enabled=true;}}
 async Task AcceptLoop(CancellationToken ct){
  while(!ct.IsCancellationRequested){TcpClient? tcp=null;try{tcp=await listener!.AcceptTcpClientAsync(ct);tcp.NoDelay=true;using(tcp){await Serve(tcp,ct);}}catch(OperationCanceledException) when(ct.IsCancellationRequested){break;}
   catch(OperationCanceledException e){LocalData.Diagnostic(folder,"server",connectionPhase,e);if(!closing)status.Text="Сеанс отменён или истекло время ожидания. Сервер готов к следующему подключению.";}
   catch(Exception e){LocalData.Diagnostic(folder,"server",connectionPhase,e);if(!closing)await Ui.OnUi(this,()=>{status.Text="Ошибка на этапе «"+connectionPhase+"»: "+e.GetType().Name+". Подробности: diagnostics.jsonl";return true;});}finally{tcp?.Dispose();session=null;}}
 }
 async Task Serve(TcpClient tcp,CancellationToken ct){
  // One connection at a time, bounded handshake and pairing deadlines.
  using var sessionCts=CancellationTokenSource.CreateLinkedTokenSource(ct);sessionCts.CancelAfter(TimeSpan.FromHours(1));session=sessionCts;var token=sessionCts.Token;
  connectionPhase="TLS";using var ssl=new SslStream(tcp.GetStream(),false);using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(10));await ssl.AuthenticateAsServerAsync(new SslServerAuthenticationOptions{ServerCertificate=certificate,EnabledSslProtocols=SslProtocols.Tls12|SslProtocols.Tls13,ClientCertificateRequired=false,CertificateRevocationCheckMode=X509RevocationMode.NoCheck},deadline.Token);}
  await using var wire=new Channel(ssl);using var input=new RemoteInput();FileSandbox? sandbox=null;
  Task? frames=null;Task? heartbeat=null;var approved=false;
  try{
   connectionPhase="authentication_options";await wire.Send("auth.options",new AuthenticationOptions(accounts.AllowsAccounts,accounts.AllowsInvitations),token);
   Message first;using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token)){deadline.CancelAfter(TimeSpan.FromSeconds(120));first=await wire.Receive(deadline.Token,120);}
   connectionPhase="login.message_type";if(first.Type!="hello"){
    await wire.Send("error",new{code="unexpected_before_login",message="До авторизации получена команда сеанса. Обновите клиент до версии 0.3.2 или новее."},token);return;
   }
   connectionPhase="login.parameters";var hello=first.Read<Hello>();
   const Permissions all=Permissions.View|Permissions.Control|Permissions.Files|Permissions.Clipboard|Permissions.Chat;
   if(hello.Operator.Length>128||hello.Username.Length>64||hello.Password.Length>256||(hello.Requested&~all)!=0)throw new InvalidDataException("Неверные параметры входа");
   var address=(tcp.Client.RemoteEndPoint as IPEndPoint)?.Address.ToString()??"unknown";
   if(!loginLimiter.Allow(address,DateTimeOffset.UtcNow)){await wire.Send("error",new{message="Слишком много попыток входа. Повторите через минуту."},token);return;}
   bool correct=false,requiresApproval=true;Permissions allowed=Permissions.None;activeUser="";
   if(hello.AuthMode=="account"&&accounts.AllowsAccounts){
    var account=await Task.Run(()=>accounts.Authenticate(hello.Username,hello.Password)).WaitAsync(token);
    if(account!=null){correct=true;activeUser=account.Username;allowed=account.Rights;requiresApproval=account.RequireApproval;}
   }else if(hello.AuthMode=="invitation"&&accounts.AllowsInvitations){
    correct=await Ui.OnUi(this,()=>{bool valid=pairing.Length==32&&DateTimeOffset.UtcNow<codeExpires&&Identity.Equal(pairing,hello.Code);if(valid){pairing="";code.Text="Код использован";}return valid;});
    allowed=accounts.Settings.SupportRights;activeUser="Техподдержка / "+hello.Operator;
   }
   if(!correct){LocalData.Audit(folder,"auth.login","","failed");await wire.Send("error",new{message="Вход отклонён. Проверьте учётные данные и настройки сервера."},token);await Task.Delay(1500,token);return;}
   // Do not retain the password in the session's long-lived Hello reference.
   hello=hello with{Password="",Code=""};first=Message.Of("auth.consumed",new{});
   sessionId=Guid.NewGuid().ToString();granted=hello.Requested&allowed;
   if(granted==Permissions.None){await wire.Send("rejected",new{message="Для этой учётной записи не разрешены функции сеанса."},token);return;}
   connectionPhase="local_approval";if(requiresApproval){
    approved=await Ui.OnUi(this,()=>{
     status.Text="Запрос от "+activeUser;
     return Ui.Consent(this,$"Пользователь: {activeUser}\nРазрешения: {Ui.Rights(granted)}\nРазрешить доступ на срок до 1 часа?\n\nВы можете завершить сеанс в любой момент.");
    }).WaitAsync(TimeSpan.FromSeconds(65),token);
   }else approved=true;
   if(!approved){await wire.Send("rejected",new{message="Доступ отклонён на сервере."},token);return;}
   status.Text="Сеанс активен · "+activeUser;
   connectionPhase="session_start";LocalData.Audit(folder,"session.start",sessionId,"approved",activeUser);channel=wire;monitor=0;fps=10;jpeg=65;
   await wire.Send("welcome",new Welcome(sessionId,granted,DesktopCapture.Displays(),"DXGI / GDI fallback"),token);
   frames=Task.Run(async()=>{using var capture=new DesktopCapture();try{while(!token.IsCancellationRequested){if(granted.HasFlag(Permissions.View)){var frame=capture.Grab(Volatile.Read(ref monitor),Volatile.Read(ref jpeg));if(frame!=null)await wire.Send("frame",frame,token);}await Task.Delay(1000/Math.Clamp(Volatile.Read(ref fps),1,30),token);}}catch(Exception e){try{await wire.Send("error",new{message=e.Message},token);}catch{}sessionCts.Cancel();}},token);
   heartbeat=Task.Run(async()=>{try{while(!token.IsCancellationRequested){await Task.Delay(10000,token);await wire.Send("ping",new{},token);}}catch{sessionCts.Cancel();}},token);
   connectionPhase="session";while(!token.IsCancellationRequested){var msg=await wire.Receive(token);
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
     case "chat":Require(Permissions.Chat);var message=msg.Data.GetProperty("message").GetString()??"";if(message.Length>8192)throw new InvalidDataException();await Ui.OnUi(this,()=>{chat.AppendText($"{activeUser} · {DateTime.Now:HH:mm}\r\n{message}\r\n\r\n");return true;});break;
     case "clipboard":Require(Permissions.Clipboard);var value=msg.Data.GetProperty("text").GetString()??"";if(System.Text.Encoding.UTF8.GetByteCount(value)>1024*1024)throw new InvalidDataException();await Ui.OnUi(this,()=>{lastClipboard=value;if(value.Length==0)Clipboard.Clear();else Clipboard.SetText(value);return true;});break;
     case "file":Require(Permissions.Files);var cmd=msg.Read<FileCommand>();FileReply reply;try{
      if(cmd.Op=="delete"&&requiresApproval&&!await Ui.OnUi(this,()=>MessageBox.Show(this,"Удалить из общей папки: "+cmd.Path+"?","Файловая операция",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)==DialogResult.Yes))reply=new(false,"Удаление отклонено пользователем");else{sandbox??=new FileSandbox(share);reply=await sandbox.Execute(cmd,token);}
      LocalData.Audit(folder,"file."+cmd.Op,sessionId,reply.Ok?"success":"rejected");
     }catch(Exception e) when(e is IOException or UnauthorizedAccessException or FormatException){reply=new(false,e.Message);LocalData.Audit(folder,"file."+cmd.Op,sessionId,"failed");}await wire.Send("file.reply",reply,token,msg.Id);break;
     default:throw new InvalidDataException("Неизвестный тип сообщения");
    }
   }
  }catch(Exception error) when(error is not OperationCanceledException){
   // A TLS session exists: return a bounded, safe reason before closing the stream.
   try{await wire.Send("error",new{code="server_session_error",message="Сервер завершил подключение на этапе «"+connectionPhase+"». Проверьте diagnostics.jsonl на сервере."},token);}catch{}
   throw;
  }finally{
   sessionCts.Cancel();input.Release();sandbox?.Dispose();if(frames!=null)try{await frames;}catch{}if(heartbeat!=null)try{await heartbeat;}catch{}channel=null;session=null;
   if(approved)LocalData.Audit(folder,"session.end",sessionId,"closed",activeUser);if(!closing)await Ui.OnUi(this,()=>{BackColor=Ui.Background;status.Text=running!=null?"Ожидание подключения":"Доступ выключен";NewCode();return true;});
  }
 }
 void Require(Permissions permission){if(!granted.HasFlag(permission))throw new UnauthorizedAccessException("Нет разрешения: "+permission);}
}
