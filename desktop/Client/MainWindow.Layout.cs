using RemoteAdmin.Shared;

namespace RemoteAdmin.Client;
public sealed partial class MainWindow {
 readonly ListBox deviceList=new(){Dock=DockStyle.Fill,BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=66,IntegralHeight=false,BackColor=Color.White};
 readonly TextBox deviceSearch=Ui.Text();
 readonly Label deviceEmpty=Ui.Description("Здесь появятся сохранённые компьютеры.");
 readonly System.Windows.Forms.Timer layoutTimer=new(){Interval=500};
 Button connectButton=null!,disconnectButton=null!,fileButton=null!,inputButton=null!,sendButton=null!;
 Panel connectionPage=null!,sessionPage=null!,chatPane=null!;
 Label sessionHeading=null!;
 TableLayoutPanel workspace=null!;
 bool wasConnected;
 TableLayoutPanel appRoot=null!,mainLayout=null!;
 Button chatButton=null!;

 void BuildLayout(){
  Text="RemoteAdmin · Подключения";Width=1380;Height=900;MinimumSize=new Size(1100,760);
  host.PlaceholderText="Например, 192.168.1.20";pin.PlaceholderText="Отпечаток с экрана сервера";code.PlaceholderText="Одноразовый код с сервера";code.UseSystemPasswordChar=true;code.MaxLength=80;
  pin.Font=new Font("Consolas",9);Ui.StyleCombo(monitors);Ui.StyleCombo(quality);quality.Width=165;quality.Items.AddRange(["Экономичный","Сбалансированный","Качество изображения"]);quality.SelectedIndex=1;
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};appRoot=root;root.ColumnStyles.Add(new(SizeType.Absolute,276));root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Percent,100));
  var sidebar=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(22,26,18,20)};
  var navTop=Ui.Stack();navTop.Dock=DockStyle.Top;Ui.Add(navTop,Ui.Heading("RemoteAdmin",20));Ui.Add(navTop,Ui.Description("Ваши компьютеры"));
  Ui.Add(navTop,Ui.Primary("+ Новое подключение",(_,_)=>NewConnection()));deviceSearch.PlaceholderText="Поиск по имени или адресу";Ui.Add(navTop,deviceSearch);
  deviceSearch.TextChanged+=(_,_)=>RefreshDeviceList();deviceList.DrawItem+=DrawDevice;deviceList.SelectedIndexChanged+=(_,_)=>{if(deviceList.SelectedItem is Bookmark b&&!busy){bookmarks.SelectedIndex=entries.IndexOf(b);host.Focus();}};
  var navBottom=Ui.Stack();navBottom.Dock=DockStyle.Bottom;var navActions=Ui.Row();navActions.Controls.Add(Ui.Button("Сохранить",(_,_)=>SaveBookmark()));navActions.Controls.Add(Ui.Button("Удалить",(_,_)=>DeleteBookmark()));Ui.Add(navBottom,navActions);Ui.Add(navBottom,Ui.Description("Коды подключения не сохраняются."));
  deviceEmpty.Dock=DockStyle.Top;deviceEmpty.Padding=new Padding(0,12,0,0);deviceEmpty.MaximumSize=new Size(220,0);var listPanel=new Panel{Dock=DockStyle.Fill};listPanel.Controls.Add(deviceList);listPanel.Controls.Add(deviceEmpty);sidebar.Controls.Add(listPanel);sidebar.Controls.Add(navBottom);sidebar.Controls.Add(navTop);root.Controls.Add(sidebar,0,0);

  var main=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(26,22,26,16)};mainLayout=main;main.ColumnStyles.Add(new(SizeType.Percent,100));main.RowStyles.Add(new(SizeType.Absolute,68));main.RowStyles.Add(new(SizeType.Percent,100));main.RowStyles.Add(new(SizeType.Absolute,48));
  var title=Ui.Stack();Ui.Add(title,Ui.Heading("Подключение к компьютеру",23));Ui.Add(title,Ui.Description("Прямой защищённый доступ · Windows"));main.Controls.Add(title,0,0);
  workspace=Ui.FillGrid();var pages=new Panel{Dock=DockStyle.Fill};workspace.Controls.Add(pages,0,0);main.Controls.Add(workspace,0,1);
  connectionPage=new Panel{Dock=DockStyle.Fill,AutoScroll=true};var form=Ui.Stack();form.Padding=new Padding(0,0,14,0);
  var quick=Ui.Card("Есть приглашение от владельца?","На сервере нажмите «Копировать приглашение» и отправьте его доверенным каналом. Здесь можно заполнить все поля одним действием.");Ui.Add(quick,Ui.Primary("Вставить приглашение",(_,_)=>PasteInvitation()));Ui.Add(form,quick);
  var details=Ui.Card("Подключиться вручную","Укажите адрес и данные, которые показаны в RemoteAdmin Server.");
  var address=Ui.FillGrid(2);address.AutoSize=true;address.Dock=DockStyle.Top;address.ColumnStyles[0].Width=76;address.ColumnStyles[1].Width=24;address.RowStyles.Clear();address.RowStyles.Add(new(SizeType.AutoSize));var addressField=Ui.Field("Адрес компьютера",host);addressField.Margin=new Padding(0,0,18,12);address.Controls.Add(addressField,0,0);address.Controls.Add(Ui.Field("Порт",port),1,0);Ui.Add(details,address);
  Ui.Add(details,Ui.Field("Ключ безопасности · SHA-256",pin,"Сверьте отпечаток с владельцем компьютера. Приглашение содержит его автоматически."));Ui.Add(details,Ui.Field("Одноразовый код",code,"Код действует 5 минут и запрашивается заново для каждого подключения."));var reveal=new CheckBox{Text="Показать код",AutoSize=true};Ui.StyleCheck(reveal);reveal.CheckedChanged+=(_,_)=>code.UseSystemPasswordChar=!reveal.Checked;Ui.Add(details,reveal);
  foreach(var check in new[]{viewOnly,files,clipboard})Ui.StyleCheck(check);viewOnly.Text="Только просмотр";files.Text="Запросить передачу файлов";clipboard.Text="Общий текстовый буфер";
  var modes=Ui.Row();modes.Controls.Add(viewOnly);modes.Controls.Add(files);modes.Controls.Add(clipboard);Ui.Add(details,modes);
  connectButton=Ui.Primary("Подключиться к компьютеру",async(_,_)=>await Connect());Ui.Add(details,connectButton);Ui.Add(form,details);
  Ui.Add(form,Ui.Description("Владелец увидит запрос и выберет, разрешить ли доступ. Для интернета нужен доступный адрес VPN или настроенный сетевой порт."));connectionPage.Controls.Add(form);

  sessionPage=new Panel{Dock=DockStyle.Fill,Visible=false};var sessionLayout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3};sessionLayout.ColumnStyles.Add(new(SizeType.Percent,100));sessionLayout.RowStyles.Add(new(SizeType.AutoSize));sessionLayout.RowStyles.Add(new(SizeType.AutoSize));sessionLayout.RowStyles.Add(new(SizeType.Percent,100));
  sessionHeading=Ui.Heading("Удалённый рабочий стол",16);sessionLayout.Controls.Add(sessionHeading,0,0);
  var tools=Ui.Row();tools.Padding=new Padding(0,4,0,8);tools.Controls.Add(Ui.Label("Дисплей"));tools.Controls.Add(monitors);tools.Controls.Add(quality);fileButton=Ui.Button("Файлы",(_,_)=>OpenFiles());inputButton=Ui.Button("Ввести текст",async(_,_)=>{if(!CanControl)return;var value=Ui.Ask(this,"Ввести текст","Текст для активного окна на сервере");if(value!=null)await Send("text",new{text=value});});tools.Controls.Add(fileButton);tools.Controls.Add(inputButton);
  chatButton=Ui.Button("Чат",(_,_)=>ToggleChat());tools.Controls.Add(chatButton);tools.Controls.Add(Ui.Button("Полный экран",(_,_)=>ToggleFullscreen()));tools.Controls.Add(Ui.Button("Только просмотр",async(_,_)=>{viewOnly.Checked=true;await Send("release",new{});}));sessionLayout.Controls.Add(tools,0,1);
  var desktop=Ui.FillGrid(2);desktop.ColumnStyles[0].Width=100;desktop.ColumnStyles[1].SizeType=SizeType.Absolute;desktop.ColumnStyles[1].Width=0;desktop.Controls.Add(screen,0,0);chatPane=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(16),Visible=false};Ui.StyleChat(chat);
  var chatTitle=Ui.Heading("Чат",14);chatTitle.Dock=DockStyle.Top;
  var composer=Ui.Stack();composer.Dock=DockStyle.Bottom;outgoing.PlaceholderText="Сообщение…";outgoing.MaxLength=8192;Ui.Add(composer,outgoing);sendButton=Ui.Primary("Отправить",async(_,_)=>await SendChat());Ui.Add(composer,sendButton);chat.TextChanged+=(_,_)=>{if(!chatPane.Visible)chatButton.Text="Чат •";};chatPane.Controls.Add(chat);chatPane.Controls.Add(composer);chatPane.Controls.Add(chatTitle);desktop.Controls.Add(chatPane,1,0);sessionLayout.Controls.Add(desktop,0,2);sessionPage.Controls.Add(sessionLayout);pages.Controls.Add(sessionPage);pages.Controls.Add(connectionPage);
  var footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};footer.ColumnStyles.Add(new(SizeType.Percent,100));footer.ColumnStyles.Add(new(SizeType.AutoSize));status.AutoSize=false;status.Dock=DockStyle.Fill;status.TextAlign=ContentAlignment.MiddleLeft;status.AutoEllipsis=true;status.ForeColor=Ui.Muted;disconnectButton=Ui.Destructive("Отключиться",(_,_)=>Disconnect());footer.Controls.Add(status,0,0);footer.Controls.Add(disconnectButton,1,0);main.Controls.Add(footer,0,2);root.Controls.Add(main,1,0);Controls.Add(root);
  outgoing.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await SendChat();}};code.KeyDown+=async(_,e)=>{if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Connect();}};
  KeyPreview=true;KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Escape&&FormBorderStyle==FormBorderStyle.None){ToggleFullscreen();e.Handled=true;e.SuppressKeyPress=true;}};
  foreach(var input in new[]{host,pin,code})input.TextChanged+=(_,_)=>input.BackColor=Color.White;
  layoutTimer.Tick+=(_,_)=>RefreshLayoutState();layoutTimer.Start();FormClosed+=(_,_)=>layoutTimer.Dispose();RefreshLayoutState();
 }
 void RefreshDeviceList(){
  var filter=deviceSearch.Text.Trim();deviceList.BeginUpdate();deviceList.Items.Clear();foreach(var b in entries.Where(b=>filter.Length==0||b.Name.Contains(filter,StringComparison.OrdinalIgnoreCase)||b.Host.Contains(filter,StringComparison.OrdinalIgnoreCase)))deviceList.Items.Add(b);deviceList.EndUpdate();deviceEmpty.Visible=deviceList.Items.Count==0;deviceEmpty.Text=entries.Count==0?"Сохраните компьютер, чтобы в следующий раз не вводить адрес и ключ заново.":"Ничего не найдено. Попробуйте другое имя или адрес.";
 }
 void DrawDevice(object? sender,DrawItemEventArgs e){
  if(e.Index<0||e.Index>=deviceList.Items.Count)return;var b=(Bookmark)deviceList.Items[e.Index];bool selected=(e.State&DrawItemState.Selected)!=0;using var background=new SolidBrush(selected?Color.FromArgb(232,239,255):Color.White);e.Graphics.FillRectangle(background,e.Bounds);
  using var bold=new Font("Segoe UI",10,FontStyle.Bold);var title=new Rectangle(e.Bounds.X+12,e.Bounds.Y+10,e.Bounds.Width-22,24);TextRenderer.DrawText(e.Graphics,b.Name,bold,title,Ui.Ink,TextFormatFlags.EndEllipsis);var subtitle=new Rectangle(title.X,e.Bounds.Y+35,title.Width,22);TextRenderer.DrawText(e.Graphics,b.Host+":"+b.Port,Font,subtitle,Ui.Muted,TextFormatFlags.EndEllipsis);if((e.State&DrawItemState.Focus)!=0)e.DrawFocusRectangle();
 }
 void NewConnection(){if(busy)return;bookmarks.SelectedIndex=-1;deviceList.ClearSelected();host.Clear();pin.Clear();code.Clear();viewOnly.Checked=false;files.Checked=false;clipboard.Checked=false;status.Text="Введите данные или вставьте приглашение с сервера";host.Focus();}
 void PasteInvitation(){
  if(busy)return;try{var details=PairingLink.Parse(Clipboard.GetText());host.Text=details.Host;port.Value=details.Port;pin.Text=details.Fingerprint;code.Text=details.Code;status.Text="Приглашение заполнено. Проверьте адрес и нажмите «Подключиться».";connectButton.Focus();}catch(Exception e){status.Text=e is FormatException?e.Message:"Не удалось прочитать буфер обмена. Скопируйте приглашение ещё раз.";}
 }
 void RefreshLayoutState(){
  if(IsDisposed)return;bool connected=sessionId.Length>0&&channel!=null;if(connected)status.ForeColor=Ui.Success;else if(!status.Text.Contains("Введите")&&!status.Text.Contains("Нужен")&&!status.Text.Contains("Вставьте"))status.ForeColor=Ui.Muted;connectButton.Enabled=!busy;connectButton.Text=busy?"Подключение…":"Подключиться к компьютеру";disconnectButton.Visible=busy;disconnectButton.Text=connected?"Завершить сеанс":"Отменить подключение";
  foreach(var c in new Control[]{host,port,pin,code,deviceList,deviceSearch,files,clipboard})c.Enabled=!busy;viewOnly.Enabled=!busy;
  fileButton.Enabled=connected&&granted.HasFlag(Permissions.Files);inputButton.Enabled=connected&&CanControl;monitors.Enabled=connected;quality.Enabled=connected;sendButton.Enabled=connected&&granted.HasFlag(Permissions.Chat);
  if(connected!=wasConnected){wasConnected=connected;sessionPage.Visible=connected;connectionPage.Visible=!connected;if(connected){sessionPage.BringToFront();screen.Focus();}else{connectionPage.BringToFront();if(FormBorderStyle==FormBorderStyle.None)ToggleFullscreen();}}
  if(connected)sessionHeading.Text=host.Text+"  /  "+(CanControl?"Управление":"Только просмотр");
 }
 void ToggleChat(){chatPane.Visible=!chatPane.Visible;chatButton.Text="Чат";if(chatPane.Parent is TableLayoutPanel table)table.ColumnStyles[1].Width=chatPane.Visible?285:0;if(chatPane.Visible)outgoing.Focus();}
 void ToggleFullscreen(){bool full=FormBorderStyle!=FormBorderStyle.None;FormBorderStyle=full?FormBorderStyle.None:FormBorderStyle.Sizable;WindowState=full?FormWindowState.Maximized:FormWindowState.Normal;appRoot.ColumnStyles[0].Width=full?0:276;mainLayout.RowStyles[0].Height=full?0:68;}
 void FieldError(TextBox field,string message){field.BackColor=Color.FromArgb(255,238,240);status.ForeColor=Ui.Danger;status.Text=message;field.Focus();}
 static string ConnectionError(Exception error)=>error switch{
  System.Security.Authentication.AuthenticationException=>"Не удалось подтвердить сервер. Сверьте ключ безопасности и дату Windows.",
  System.Net.Sockets.SocketException socket when socket.SocketErrorCode==System.Net.Sockets.SocketError.HostNotFound=>"Компьютер с таким именем не найден. Проверьте адрес.",
  System.Net.Sockets.SocketException=>"Компьютер недоступен. Проверьте адрес, порт, VPN и включён ли доступ на сервере.",
  TimeoutException=>"Сервер не ответил вовремя. Проверьте подключение и повторите попытку.",
  _=>"Подключение завершено: "+error.Message
 };
 async Task SendChat(){if(channel==null||!granted.HasFlag(Permissions.Chat)||string.IsNullOrWhiteSpace(outgoing.Text))return;var message=outgoing.Text;outgoing.Clear();await Send("chat",new{message});chat.AppendText($"Вы · {DateTime.Now:HH:mm}\r\n{message}\r\n\r\n");}
}
