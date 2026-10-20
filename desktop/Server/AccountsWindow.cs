using RemoteAdmin.Shared;

namespace RemoteAdmin.Server;
public sealed class AccountsWindow : Form {
 readonly ServerAccounts store;
 readonly Action changed;
 readonly ListView accounts=new(){Dock=DockStyle.Fill,View=View.Details,FullRowSelect=true,MultiSelect=false,HideSelection=false,BorderStyle=BorderStyle.None};
 public AccountsWindow(ServerAccounts store,Action changed){
  this.store=store;this.changed=changed;Ui.Theme(this);Text="Учётные записи сервера";Width=1000;Height=650;
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=3,Padding=new Padding(24)};root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.Absolute,100));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));
  var header=Ui.Stack();Ui.Add(header,Ui.Heading("Пользователи и права",22));Ui.Add(header,Ui.Description("Это учётные записи RemoteAdmin на этом сервере. Пароли Windows здесь не используются. Изменения прекращают активный сеанс."));root.Controls.Add(header,0,0);
  accounts.Columns.Add("Логин",180);accounts.Columns.Add("Разрешения",410);accounts.Columns.Add("Подтверждение",160);accounts.Columns.Add("Состояние",110);accounts.BackColor=Color.White;root.Controls.Add(accounts,0,1);
  var actions=Ui.Row();actions.Padding=new Padding(0,16,0,0);actions.Controls.Add(Ui.Primary("Создать пользователя",(_,_)=>Edit(null)));actions.Controls.Add(Ui.Button("Изменить",(_,_)=>{if(Selected() is {} a)Edit(a);}));actions.Controls.Add(Ui.Destructive("Удалить",(_,_)=>Delete()));actions.Controls.Add(Ui.Button("Закрыть",(_,_)=>Close()));root.Controls.Add(actions,0,2);Controls.Add(root);accounts.DoubleClick+=(_,_)=>{if(Selected() is {} a)Edit(a);};RefreshAccounts();
 }
 ServerAccount? Selected()=>accounts.SelectedItems.Count==1?accounts.SelectedItems[0].Tag as ServerAccount:null;
 void RefreshAccounts(){accounts.Items.Clear();foreach(var account in store.Accounts.OrderBy(a=>a.Username)){var row=new ListViewItem(account.Username){Tag=account};row.SubItems.Add(Ui.Rights(account.Rights));row.SubItems.Add(account.RequireApproval?"На каждую сессию":"Не требуется");row.SubItems.Add(account.Enabled?"Включён":"Отключён");accounts.Items.Add(row);}}
 void Edit(ServerAccount? account){using var editor=new AccountEditor(store,account);if(editor.ShowDialog(this)==DialogResult.OK){changed();RefreshAccounts();}}
 void Delete(){var account=Selected();if(account==null)return;if(MessageBox.Show(this,"Удалить пользователя «"+account.Username+"» и отозвать его доступ?","Удаление учётной записи",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;try{store.Delete(account.Id);changed();RefreshAccounts();}catch(Exception e){MessageBox.Show(this,e.Message,"Не удалось удалить пользователя");}}
}

sealed class AccountEditor : Form {
 readonly TextBox login=Ui.Text(),password=Ui.Text(),repeat=Ui.Text();
 readonly CheckBox approval=new(){Text="Запрашивать согласие перед каждой сессией",Checked=true,AutoSize=true},enabled=new(){Text="Учётная запись включена",Checked=true,AutoSize=true};
 readonly Dictionary<Permissions,CheckBox> rights=new();
 bool saving;
 public AccountEditor(ServerAccounts store,ServerAccount? account){
  Ui.Theme(this);MinimumSize=new Size(640,640);ClientSize=new Size(660,730);Text=account==null?"Новый пользователь":"Права пользователя";StartPosition=FormStartPosition.CenterParent;
  var scroll=new Panel{Dock=DockStyle.Fill,AutoScroll=true,Padding=new Padding(24)};var content=Ui.Stack();Ui.Add(content,Ui.Heading(Text,21));Ui.Add(content,Ui.Description("Логин и права хранятся на сервере. Пароль не сохраняется в открытом виде."));login.Text=account?.Username??"";password.UseSystemPasswordChar=true;repeat.UseSystemPasswordChar=true;password.MaxLength=256;repeat.MaxLength=256;
  Ui.Add(content,Ui.Field("Логин",login));Ui.Add(content,Ui.Field(account==null?"Пароль (не менее 12 символов)":"Новый пароль (пусто — оставить прежний)",password));Ui.Add(content,Ui.Field("Повторите новый пароль",repeat));
  foreach(var flag in new[]{Permissions.View,Permissions.Control,Permissions.Files,Permissions.Clipboard,Permissions.Chat}){var check=new CheckBox{Text=Ui.Rights(flag),Checked=(account?.Rights??(Permissions.View|Permissions.Control|Permissions.Chat)).HasFlag(flag)};Ui.StyleCheck(check);rights[flag]=check;Ui.Add(content,check);}
  rights[Permissions.Control].CheckedChanged+=(_,_)=>{if(rights[Permissions.Control].Checked)rights[Permissions.View].Checked=true;};rights[Permissions.View].CheckedChanged+=(_,_)=>{if(!rights[Permissions.View].Checked)rights[Permissions.Control].Checked=false;};
  approval.Checked=account?.RequireApproval??true;enabled.Checked=account?.Enabled??true;Ui.StyleCheck(approval);Ui.StyleCheck(enabled);Ui.Add(content,approval);Ui.Add(content,enabled);var error=Ui.Description("");error.ForeColor=Ui.Danger;Ui.Add(content,error);
  var actions=Ui.Row();var save=Ui.Primary("Сохранить пользователя",async(_,_)=>{
   if(password.Text!=repeat.Text){error.Text="Пароли не совпадают.";return;}
   if(!approval.Checked&&(account==null||account.RequireApproval)&&MessageBox.Show(this,"Разрешить этому пользователю постоянный доступ без разового согласия?\n\nОн сможет подключаться с логином и паролем, пока сервер открыт и доступ включён. Сеанс будет виден в окне сервера.","Постоянный доступ",MessageBoxButtons.YesNo,MessageBoxIcon.Warning)!=DialogResult.Yes)return;
   var flags=rights.Where(x=>x.Value.Checked).Aggregate(Permissions.None,(all,x)=>all|x.Key);var user=login.Text;var secret=password.Text;bool confirm=approval.Checked,active=enabled.Checked;actions.Enabled=false;saving=true;
   try{await Task.Run(()=>store.Put(account?.Id,user,secret,flags,confirm,active));password.Clear();repeat.Clear();saving=false;DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}finally{saving=false;actions.Enabled=true;}
  });FormClosing+=(_,e)=>{if(saving)e.Cancel=true;};actions.Controls.Add(save);actions.Controls.Add(Ui.Button("Отмена",(_,_)=>DialogResult=DialogResult.Cancel));Ui.Add(content,actions);scroll.Controls.Add(content);Controls.Add(scroll);
 }
}
