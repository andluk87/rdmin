using RemoteAdmin.Shared;
namespace RemoteAdmin.Server;
public sealed class AccountsWindow : Form {
 readonly ServerAccounts store;
 readonly Action changed;
 readonly ListBox users=new(){Dock=DockStyle.Fill,IntegralHeight=false};
 readonly CheckedListBox rights=new(){Dock=DockStyle.Fill,CheckOnClick=true,IntegralHeight=false};
 readonly CheckBox enabled=new(){Text="Учётная запись включена",AutoSize=true};
 readonly Dictionary<string,AccountPolicy> pending=new();
 readonly Permissions[] map=[Permissions.None,Permissions.Control,Permissions.View,Permissions.Files,Permissions.Chat,Permissions.Clipboard,Permissions.Power,Permissions.Notify];
 bool loading;
 public AccountsWindow(ServerAccounts store,Action changed){
  this.store=store;this.changed=changed;Classic.Dialog(this,"Пользователи RemoteAdmin",new Size(465,555));
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=7};root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,40));root.RowStyles.Add(new(SizeType.Absolute,40));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,60));root.RowStyles.Add(new(SizeType.Absolute,28));root.RowStyles.Add(new(SizeType.Absolute,43));root.Controls.Add(Classic.Label("Пользователи RemoteAdmin"),0,0);root.Controls.Add(users,0,1);
  var edits=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft};edits.Controls.Add(Classic.Button("Удалить",(_,_)=>Delete(),85));edits.Controls.Add(Classic.Button("Изменить…",(_,_)=>Edit(Selected()),100));edits.Controls.Add(Classic.Button("Добавить…",(_,_)=>Edit(null),100));root.Controls.Add(edits,0,2);root.Controls.Add(Classic.Label("Права пользователя                                      Разрешить"),0,3);
  rights.Items.AddRange(["Полный доступ","Управление мышью и клавиатурой","Просмотр экрана","Передача файлов","Текстовый чат","Буфер обмена","Управление питанием","Текстовые сообщения"]);root.Controls.Add(rights,0,4);root.Controls.Add(enabled,0,5);
  var buttons=Classic.Buttons();var cancel=Classic.Button("Отмена",(_,_)=>DialogResult=DialogResult.Cancel);var ok=Classic.Button("OK",(_,_)=>{try{if(pending.Count>0){store.SetPolicies(pending);changed();}DialogResult=DialogResult.OK;}catch(Exception e){MessageBox.Show(this,e.Message,"Не удалось сохранить права");}});buttons.Controls.Add(cancel);buttons.Controls.Add(ok);root.Controls.Add(buttons,0,6);Controls.Add(root);AcceptButton=ok;CancelButton=cancel;
  users.SelectedIndexChanged+=(_,_)=>LoadRights();users.DoubleClick+=(_,_)=>{if(Selected() is {} account)Edit(account);};enabled.CheckedChanged+=(_,_)=>{if(!loading&&Selected() is {} account){var old=pending.GetValueOrDefault(account.Id,new AccountPolicy(account.Rights,account.Enabled));pending[account.Id]=old with{Enabled=enabled.Checked};}};rights.ItemCheck+=RightsChanged;RefreshUsers();
 }
 ServerAccount? Selected()=>users.SelectedIndex<0?null:store.Accounts.OrderBy(a=>a.Username).ElementAtOrDefault(users.SelectedIndex);
 void RefreshUsers(){users.Items.Clear();foreach(var a in store.Accounts.OrderBy(a=>a.Username))users.Items.Add(a.Username);if(users.Items.Count>0)users.SelectedIndex=0;else LoadRights();}
 void LoadRights(){loading=true;var a=Selected();var policy=a==null?new AccountPolicy(Permissions.None,false):pending.GetValueOrDefault(a.Id,new AccountPolicy(a.Rights,a.Enabled));rights.Enabled=a!=null;enabled.Enabled=a!=null;enabled.Checked=policy.Enabled;for(int i=1;i<map.Length;i++)rights.SetItemChecked(i,policy.Rights.HasFlag(map[i]));rights.SetItemChecked(0,Enumerable.Range(1,map.Length-1).All(i=>rights.GetItemChecked(i)));loading=false;}
 void RightsChanged(object? sender,ItemCheckEventArgs e){
  if(loading||Selected() is not {} account)return;loading=true;
  if(e.Index==0){for(int i=1;i<map.Length;i++)rights.SetItemChecked(i,e.NewValue==CheckState.Checked);}
  else{if(e.Index==1&&e.NewValue==CheckState.Checked)rights.SetItemChecked(2,true);if(e.Index==2&&e.NewValue!=CheckState.Checked)rights.SetItemChecked(1,false);}
  Permissions flags=Permissions.None;for(int i=1;i<map.Length;i++)if(e.Index==i?e.NewValue==CheckState.Checked:rights.GetItemChecked(i))flags|=map[i];
  if(e.Index!=0)rights.SetItemChecked(0,flags==(PermissionSets.All));pending[account.Id]=new(flags,enabled.Checked);loading=false;
 }
 void Edit(ServerAccount? account){using var editor=new AccountEditor(store,account);if(editor.ShowDialog(this)==DialogResult.OK){changed();RefreshUsers();}}
 void Delete(){var account=Selected();if(account==null)return;if(MessageBox.Show(this,"Удалить пользователя «"+account.Username+"»?","Удаление пользователя",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;try{store.Delete(account.Id);pending.Remove(account.Id);changed();RefreshUsers();}catch(Exception e){MessageBox.Show(this,e.Message,"Не удалось удалить пользователя");}}
}

internal sealed class AccountEditor : Form {
 public AccountEditor(ServerAccounts store,ServerAccount? account){
  Classic.Dialog(this,account==null?"Добавить пользователя":"Изменить пользователя",new Size(415,340));var content=Classic.Stack();content.Dock=DockStyle.Fill;content.Padding=new Padding(15);
  var login=new TextBox{Text=account?.Username??"",MaxLength=64};var password=new TextBox{UseSystemPasswordChar=true,MaxLength=256};var repeat=new TextBox{UseSystemPasswordChar=true,MaxLength=256};Classic.Add(content,Classic.Field("Имя пользователя:",login));Classic.Add(content,Classic.Field(account==null?"Пароль (от 12 символов):":"Новый пароль (пусто — не менять):",password));Classic.Add(content,Classic.Field("Подтверждение пароля:",repeat));var error=Classic.Label("");error.ForeColor=Color.Firebrick;Classic.Add(content,error);
  var buttons=Classic.Buttons();var cancel=Classic.Button("Отмена",(_,_)=>DialogResult=DialogResult.Cancel);bool saving=false;var ok=Classic.Button("OK",async(_,_)=>{if(password.Text!=repeat.Text){error.Text="Пароли не совпадают.";return;}var name=login.Text;var secret=password.Text;buttons.Enabled=false;saving=true;try{await Task.Run(()=>store.Put(account?.Id,name,secret,account?.Rights??(Permissions.View|Permissions.Control|Permissions.Chat),false,account?.Enabled??true));password.Clear();repeat.Clear();saving=false;DialogResult=DialogResult.OK;}catch(Exception e){error.Text=e.Message;}finally{saving=false;buttons.Enabled=true;}});buttons.Controls.Add(cancel);buttons.Controls.Add(ok);Controls.Add(content);Controls.Add(buttons);AcceptButton=ok;CancelButton=cancel;FormClosing+=(_,e)=>{if(saving)e.Cancel=true;};
 }
}
