using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server;
public sealed partial class MainWindow {
 readonly Label lifetime=Ui.Label(""),accessTitle=Ui.Heading("Доступ выключен",18),accessHint=Ui.Description("Включите доступ, чтобы оператор мог запросить подключение.");
 readonly TextBox advertisedHost=Ui.Text();
 readonly ComboBox authentication=new(){DropDownStyle=ComboBoxStyle.DropDownList};
 readonly Label userCount=Ui.Description("");
 TableLayoutPanel invitationCard=null!,invitationRights=null!;
 Button startButton=null!,stopButton=null!,endButton=null!,inviteButton=null!,sendButton=null!,newCodeButton=null!;
 readonly System.Windows.Forms.Timer stateTimer=new(){Interval=1000};

 void BuildLayout(){
  Width=1120;Height=850;MinimumSize=new Size(980,720);Text=$"RemoteAdmin {Ui.AppVersion} · Этот компьютер";
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2,Padding=new Padding(28,20,28,20)};
  root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,76));root.RowStyles.Add(new(SizeType.Percent,100));
  var heading=Ui.Stack();Ui.Add(heading,Ui.Heading("Этот компьютер",24));Ui.Add(heading,Ui.Description("RemoteAdmin Server "+Ui.AppVersion+"  /  Вы решаете, кто получает доступ"));root.Controls.Add(heading,0,0);
  var columns=Ui.FillGrid(2);columns.ColumnStyles[0].Width=60;columns.ColumnStyles[1].Width=40;root.Controls.Add(columns,0,1);
  var left=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(0,0,20,0)};var leftStack=Ui.Stack();left.Controls.Add(leftStack);columns.Controls.Add(left,0,0);
  var card=Ui.Card("Удалённый доступ");Ui.Add(card,accessTitle);Ui.Add(card,accessHint);
  var buttons=Ui.Row();startButton=Ui.Primary("Включить доступ",(_,_)=>Start());stopButton=Ui.Button("Выключить",(_,_)=>Stop());buttons.Controls.Add(startButton);buttons.Controls.Add(stopButton);Ui.Add(card,buttons);
  status.MaximumSize=new Size(540,0);status.ForeColor=Ui.Muted;status.Margin=new Padding(0,10,0,0);Ui.Add(card,status);Ui.Add(leftStack,card);

  var authCard=Ui.Card("Учётные записи и вход","Настройте пользователей и права один раз. На клиенте достаточно указать адрес и порт, затем войти под созданной учётной записью.");
  authentication.Items.AddRange(["Учётные записи RemoteAdmin","Одноразовые приглашения","Учётные записи и приглашения"]);Ui.StyleCombo(authentication);authentication.SelectedIndex=(int)accounts.Settings.Authentication;
  Ui.Add(authCard,Ui.Description("Адрес для клиента: "+LocalAddress()));Ui.Add(authCard,Ui.Field("Метод аутентификации",authentication));Ui.Add(authCard,userCount);Ui.Add(authCard,Ui.Primary("Пользователи и права…",(_,_)=>{using var window=new AccountsWindow(accounts,AccountsChanged);window.ShowDialog(this);}));
  var network=Ui.Row();network.Controls.Add(Ui.Label("Порт подключения"));port.Margin=new Padding(10,4,10,0);network.Controls.Add(port);network.Controls.Add(Ui.Button("Сохранить настройки",(_,_)=>{if(SaveAccessSettings())status.Text="Настройки сервера сохранены.";}));Ui.Add(authCard,network);Ui.Add(leftStack,authCard);
  authentication.SelectedIndexChanged+=(_,_)=>SaveAccessSettings();

  var connectionCard=Ui.Card("Разовая техподдержка","Отправьте приглашение человеку, которому доверяете. Подключение начнётся только после вашего подтверждения.");
  invitationCard=connectionCard;
  advertisedHost.Text=LocalAddress();advertisedHost.PlaceholderText="IP, имя компьютера или адрес VPN";
  Ui.Add(connectionCard,Ui.Field("Адрес этого компьютера",advertisedHost,"Для подключения из интернета укажите доступный адрес VPN или внешний IP."));
  code.Font=new Font("Consolas",13,FontStyle.Bold);code.BackColor=Ui.Background;code.TextAlign=HorizontalAlignment.Center;
  Ui.Add(connectionCard,Ui.Field("Одноразовый код",code));lifetime.ForeColor=Ui.Muted;Ui.Add(connectionCard,lifetime);
  var inviteActions=Ui.Row();inviteButton=Ui.Primary("Копировать приглашение",(_,_)=>CopyInvitation());newCodeButton=Ui.Button("Обновить код",(_,_)=>NewCode());inviteActions.Controls.Add(inviteButton);inviteActions.Controls.Add(newCodeButton);Ui.Add(connectionCard,inviteActions);Ui.Add(leftStack,connectionCard);

  var security=Ui.Card("Безопасность подключения","Отпечаток подтверждает, что оператор подключается именно к этому компьютеру.");
  fingerprint.Font=new Font("Consolas",9);Ui.Add(security,Ui.Field("SHA-256 сертификата",fingerprint));
  var settings=Ui.Row();settings.Controls.Add(Ui.Button("Копировать отпечаток",(_,_)=>CopyText(fingerprint.Text)));settings.Controls.Add(Ui.Button("Диагностика",(_,_)=>{try{Process.Start(new ProcessStartInfo(folder){UseShellExecute=true});}catch(Exception e){status.Text=e.Message;}}));Ui.Add(security,settings);Ui.Add(leftStack,security);

  var right=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,AutoScroll=true};right.ColumnStyles.Add(new(SizeType.Percent,100));right.RowStyles.Add(new(SizeType.AutoSize));right.RowStyles.Add(new(SizeType.AutoSize));right.RowStyles.Add(new(SizeType.Percent,100));columns.Controls.Add(right,1,0);
  var permissions=Ui.Card("Права разовой техподдержки","Эти ограничения применяются только к приглашениям. Права постоянных пользователей задаются в их учётных записях.");invitationRights=permissions;
  control.Text="Мышь и клавиатура";files.Text="Передача файлов";clipboard.Text="Текстовый буфер обмена";
  foreach(var check in new[]{control,files,clipboard}){Ui.StyleCheck(check,"Изменение разрешения завершит текущий сеанс.");Ui.Add(permissions,check);}
  var sessionCard=Ui.Card("Текущий сеанс","Все входы видны в окне сервера и записываются в локальный аудит.");endButton=Ui.Destructive("Завершить текущий сеанс",(_,_)=>session?.Cancel());Ui.Add(sessionCard,endButton);
  var permissionStack=Ui.Stack();Ui.Add(permissionStack,sessionCard);Ui.Add(permissionStack,permissions);right.Controls.Add(permissionStack,0,0);
  var shared=Ui.Card("Общая папка","Оператор не получает доступа к другим папкам.");
  var path=Ui.Description(share);path.MaximumSize=new Size(320,0);Ui.Add(shared,path);Ui.Add(shared,Ui.Button("Открыть папку",(_,_)=>{try{Directory.CreateDirectory(share);Process.Start(new ProcessStartInfo(share){UseShellExecute=true});}catch(Exception e){status.Text=e.Message;}}));right.Controls.Add(shared,0,1);

  var chatPanel=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(20),MinimumSize=new Size(0,160)};
  var chatHeading=Ui.Heading("Чат с оператором",14);chatHeading.Dock=DockStyle.Top;Ui.StyleChat(chat);
  var composer=new TableLayoutPanel{Dock=DockStyle.Bottom,ColumnCount=2,RowCount=1,Height=52};composer.ColumnStyles.Add(new(SizeType.Percent,100));composer.ColumnStyles.Add(new(SizeType.AutoSize));text.Dock=DockStyle.Fill;text.PlaceholderText="Сообщение оператору…";text.MaxLength=8192;
  sendButton=Ui.Primary("Отправить",async(_,_)=>await SendChat());composer.Controls.Add(text,0,0);composer.Controls.Add(sendButton,1,0);chatPanel.Controls.Add(chat);chatPanel.Controls.Add(composer);chatPanel.Controls.Add(chatHeading);right.Controls.Add(chatPanel,0,2);
  text.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await SendChat();}};
  control.Checked=accounts.Settings.SupportRights.HasFlag(Permissions.Control);files.Checked=accounts.Settings.SupportRights.HasFlag(Permissions.Files);clipboard.Checked=accounts.Settings.SupportRights.HasFlag(Permissions.Clipboard);
  Controls.Add(root);stateTimer.Tick+=(_,_)=>UpdateState();stateTimer.Start();FormClosed+=(_,_)=>stateTimer.Dispose();UpdateState();
 }
 bool SaveAccessSettings(){
  try{
   var support=Permissions.View|Permissions.Chat;if(control.Checked)support|=Permissions.Control;if(files.Checked)support|=Permissions.Files;if(clipboard.Checked)support|=Permissions.Clipboard;
   var mode=(AuthenticationMode)Math.Max(0,authentication.SelectedIndex);
   bool changed=accounts.Settings.Port!=(int)port.Value||accounts.Settings.Authentication!=mode||accounts.Settings.SupportRights!=support;
   if(changed){accounts.SaveSettings((int)port.Value,mode,support);session?.Cancel();LocalData.Audit(folder,"policy.changed","","success");}UpdateState();return true;
  }catch(Exception e){status.Text="Настройки не сохранены: "+e.Message;return false;}
 }
 void AccountsChanged(){session?.Cancel();LocalData.Audit(folder,"account.changed","","success");UpdateState();}
 static string LocalAddress(){
  try{return NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork&&!a.Address.ToString().StartsWith("169.254.")).Select(a=>a.Address.ToString()).FirstOrDefault()??Environment.MachineName;}catch{return Environment.MachineName;}
 }
 void CopyText(string value){try{Clipboard.SetText(value);status.Text="Скопировано в буфер обмена";}catch{status.Text="Буфер обмена занят. Попробуйте ещё раз.";}}
 void CopyInvitation(){
  if(running==null){status.Text="Сначала включите удалённый доступ.";return;}
  if(string.IsNullOrWhiteSpace(advertisedHost.Text)){advertisedHost.Focus();status.Text="Укажите адрес, доступный оператору.";return;}
  if(pairing.Length==0||DateTimeOffset.UtcNow>=codeExpires)NewCode();
  CopyText(PairingLink.Create(advertisedHost.Text.Trim(),(int)port.Value,fingerprint.Text,pairing));status.Text="Приглашение скопировано. Отправьте его оператору доверенным каналом.";
 }
 async Task SendChat(){
  if(channel==null||session==null||!granted.HasFlag(Permissions.Chat)||string.IsNullOrWhiteSpace(text.Text))return;var message=text.Text;text.Clear();
  try{await channel.Send("chat",new{message},session.Token);chat.AppendText($"Вы · {DateTime.Now:HH:mm}\r\n{message}\r\n\r\n");}catch{session?.Cancel();}
 }
 void UpdateState(){
  if(IsDisposed)return;bool online=running!=null,active=channel!=null;invitationCard.Visible=accounts.AllowsInvitations;invitationRights.Visible=accounts.AllowsInvitations;userCount.Text=$"Учётных записей: {accounts.Accounts.Count(a=>a.Enabled)} включено / {accounts.Accounts.Length} всего";
  startButton.Enabled=!online;stopButton.Enabled=online;endButton.Enabled=active;inviteButton.Enabled=online&&!active;newCodeButton.Enabled=!active;sendButton.Enabled=active&&granted.HasFlag(Permissions.Chat);text.Enabled=sendButton.Enabled;
  accessTitle.Text=active?"Сеанс активен":online?"Готов к подключению":"Доступ выключен";accessTitle.ForeColor=active||online?Ui.Success:Ui.Ink;
  accessHint.Text=active?"Пользователь: "+activeUser+". Вы можете завершить сеанс в любой момент.":online?accounts.AllowsAccounts?"Клиент подключается по адресу и порту. Вход и права определяются настройками сервера.":"Отправьте приглашение оператору. Требуется ваше согласие.":"Никто не может подключиться. Нажмите «Включить доступ», когда будете готовы.";
  var remaining=codeExpires-DateTimeOffset.UtcNow;lifetime.Text=active?"Код использован для текущего сеанса":pairing.Length==0?"Код уже использован":remaining.TotalSeconds<=0?"Код истёк — обновите его перед подключением":$"Код действует ещё {Math.Max(0,(int)remaining.TotalMinutes):00}:{remaining.Seconds:00}";
 }
}
