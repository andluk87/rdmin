using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server;
public sealed partial class MainWindow {
 readonly System.Windows.Forms.Timer stateTimer=new(){Interval=1000};
 readonly Label stateLabel=Classic.Label(""),sessionLabel=Classic.Label("");
 readonly NotifyIcon tray=new(){Icon=RemoteAdmin.Shared.Ui.AppIcon,Text="RemoteAdmin Server"};
 DesktopOptions desktopOptions=new();
 Button startButton=null!,endButton=null!;
 bool exitRequested;
 ChatWindow? chatWindow;

 void BuildLayout(){
  Classic.Dialog(this,"Настройки RemoteAdmin Server",new Size(650,338));status.ForeColor=SystemColors.ControlText;status.MaximumSize=new Size(420,0);StartPosition=FormStartPosition.CenterScreen;desktopOptions=ServerDesktop.Load(folder);
  control.Checked=accounts.Settings.SupportRights.HasFlag(Permissions.Control);files.Checked=accounts.Settings.SupportRights.HasFlag(Permissions.Files);clipboard.Checked=accounts.Settings.SupportRights.HasFlag(Permissions.Clipboard);
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(12)};root.ColumnStyles.Add(new(SizeType.Percent,100));root.ColumnStyles.Add(new(SizeType.Absolute,150));root.RowStyles.Add(new(SizeType.Percent,100));
  var main=Classic.Stack();main.Dock=DockStyle.Fill;var brand=new Label{Text="RemoteAdmin Server "+Ui.AppVersion,AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold),Padding=new Padding(4,5,0,8)};Classic.Add(main,brand);
  var info=Classic.Group("Состояние сервера");info.AutoSize=true;var infoStack=Classic.Stack();Classic.Add(infoStack,stateLabel);Classic.Add(infoStack,Classic.Label("Адрес: "+LocalAddress()));Classic.Add(infoStack,sessionLabel);Classic.Add(infoStack,status);info.Controls.Add(infoStack);Classic.Add(main,info);
  var access=Classic.Group("Управление доступом");access.AutoSize=true;var accessButtons=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,WrapContents=true};startButton=Classic.Button("Включить доступ",(_,_)=>{if(running==null)Start();else Stop();},140);endButton=Classic.Button("Завершить сеанс",(_,_)=>session?.Cancel(),140);accessButtons.Controls.Add(startButton);accessButtons.Controls.Add(endButton);access.Controls.Add(accessButtons);Classic.Add(main,access);
  var rightsNote=Classic.Label("Вход по учётной записи — без разового подтверждения.\nДоступ определяется правами пользователя на сервере.");Classic.Add(main,rightsNote);root.Controls.Add(main,0,0);
  var actions=Classic.Stack();actions.Dock=DockStyle.Fill;Classic.Add(actions,Classic.Button("Режим запуска…",(_,_)=>{using var dialog=new StartupWindow();dialog.ShowDialog(this);},140));Classic.Add(actions,Classic.Button("Настройки…",(_,_)=>{using var dialog=new SettingsWindow(this);dialog.ShowDialog(this);},140));Classic.Add(actions,Classic.Button("Права доступа…",(_,_)=>{using var dialog=new SecurityWindow(accounts,AccountsChanged);dialog.ShowDialog(this);},140));Classic.Add(actions,Classic.Button("Разовая помощь…",(_,_)=>ShowSupport(),140));Classic.Add(actions,Classic.Button("Чат…",(_,_)=>ShowChat(),140));Classic.Add(actions,Classic.Button("Журнал…",(_,_)=>OpenFolder(folder),140));Classic.Add(actions,Classic.Button("Справка",(_,_)=>MessageBox.Show(this,"На сервере создайте пользователя в «Права доступа», задайте порт в «Настройки» и включите доступ.\n\nНа клиенте укажите адрес и порт, затем логин и пароль. Подтверждение нужно только при разовой техподдержке.\n\nWindows NT/AD настраивается через SSPI и SID пользователей/групп. Системная служба пока не реализована.","RemoteAdmin Server "+Ui.AppVersion),140));Classic.Add(actions,Classic.Button("Выход",(_,_)=>ExitApplication(),140));root.Controls.Add(actions,1,0);Controls.Add(root);
  var menu=new ContextMenuStrip();menu.Items.Add("Настройки RemoteAdmin Server",null,(_,_)=>RestoreWindow());var trayEnd=menu.Items.Add("Завершить сеанс",null,(_,_)=>session?.Cancel());menu.Opening+=(_,_)=>trayEnd.Enabled=channel!=null;menu.Items.Add("Выход",null,(_,_)=>ExitApplication());tray.ContextMenuStrip=menu;tray.DoubleClick+=(_,_)=>RestoreWindow();tray.Visible=desktopOptions.ShowTray;
  stateTimer.Tick+=(_,_)=>UpdateState();stateTimer.Start();FormClosing+=(_,e)=>{if(!exitRequested&&desktopOptions.ShowTray&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
  FormClosed+=(_,_)=>{tray.Visible=false;tray.Dispose();menu.Dispose();stateTimer.Dispose();};Shown+=(_,_)=>{if(ServerDesktop.Automatic){Start();if(Environment.GetCommandLineArgs().Contains("--auto")&&desktopOptions.ShowTray)Hide();}};UpdateState();
 }
 void UpdateState(){
  if(IsDisposed)return;bool online=running!=null,active=channel!=null;stateLabel.Text=online?"Доступ включён · TCP "+accounts.Settings.Port:"Доступ выключен";stateLabel.ForeColor=online?Color.DarkGreen:SystemColors.ControlText;
  sessionLabel.Text=active?"Пользователь: "+activeUser:"Активных подключений нет";startButton.Text=online?"Выключить доступ":"Включить доступ";endButton.Enabled=active;tray.Text=active?"RemoteAdmin · активный сеанс":online?"RemoteAdmin · доступ включён":"RemoteAdmin · доступ выключен";
 }
 void AccountsChanged(){session?.Cancel();LocalData.Audit(folder,"account.changed","","success");UpdateState();}
 void OpenFolder(string path){try{Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}catch(Exception e){MessageBox.Show(this,e.Message,"Не удалось открыть папку");}}
 void RestoreWindow(){Show();WindowState=FormWindowState.Normal;Activate();}
 void ExitApplication(){exitRequested=true;Close();}
 static string LocalAddress(){try{return NetworkInterface.GetAllNetworkInterfaces().Where(n=>n.OperationalStatus==OperationalStatus.Up&&n.NetworkInterfaceType!=NetworkInterfaceType.Loopback).SelectMany(n=>n.GetIPProperties().UnicastAddresses).Where(a=>a.Address.AddressFamily==AddressFamily.InterNetwork&&!a.Address.ToString().StartsWith("169.254.")).Select(a=>a.Address.ToString()).FirstOrDefault()??Environment.MachineName;}catch{return Environment.MachineName;}}
 void ShowSupport(){using var dialog=new SupportWindow(this);dialog.ShowDialog(this);}
 void ShowChat(){if(channel==null||!granted.HasFlag(Permissions.Chat)){MessageBox.Show(this,"Нет активного сеанса с правом чата.");return;}if(chatWindow==null||chatWindow.IsDisposed)chatWindow=new ChatWindow(this);chatWindow.Show(this);chatWindow.Activate();}
 async Task SendChat(){if(channel==null||session==null||!granted.HasFlag(Permissions.Chat)){MessageBox.Show(this,"Нет активного сеанса с правом чата.");return;}if(string.IsNullOrWhiteSpace(text.Text))return;var message=text.Text;text.Clear();try{await channel.Send("chat",new{message},session.Token);chat.AppendText($"Вы · {DateTime.Now:HH:mm}\r\n{message}\r\n\r\n");}catch{session?.Cancel();}}

 sealed class ChatWindow : Form {
  public ChatWindow(MainWindow owner){Classic.Dialog(this,"Текстовый чат",new Size(520,360));owner.chat.Dock=DockStyle.Fill;owner.chat.BackColor=SystemColors.Window;owner.chat.ForeColor=SystemColors.WindowText;owner.chat.BorderStyle=BorderStyle.FixedSingle;var bottom=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=42,ColumnCount=2,RowCount=1};bottom.ColumnStyles.Add(new(SizeType.Percent,100));bottom.ColumnStyles.Add(new(SizeType.Absolute,110));owner.text.Dock=DockStyle.Fill;owner.text.MaxLength=8192;bottom.Controls.Add(owner.text,0,0);bottom.Controls.Add(Classic.Button("Отправить",async(_,_)=>await owner.SendChat()),1,0);Controls.Add(owner.chat);Controls.Add(bottom);owner.text.KeyDown+=OnTextKey;FormClosing+=(_,e)=>{if(e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}};
   async void OnTextKey(object? sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await owner.SendChat();}}
  }
 }
}
