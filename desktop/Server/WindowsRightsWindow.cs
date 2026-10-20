using RemoteAdmin.Shared;
namespace RemoteAdmin.Server;
internal sealed class WindowsRightsWindow : Form {
 readonly ServerAccounts store;readonly Action changed;
 readonly List<WindowsAccessRule> rules;
 readonly ListBox list=new(){Dock=DockStyle.Fill,IntegralHeight=false};
 readonly Dictionary<Permissions,CheckBox> allows=new(),denies=new();
 readonly CheckBox enabled=new(){Text="Правило включено",AutoSize=true};
 bool loading;
 public WindowsRightsWindow(ServerAccounts store,Action changed){
  this.store=store;this.changed=changed;rules=store.WindowsRules.ToList();Classic.Dialog(this,"Разрешения Windows NT / AD",new Size(610,585));
  var root=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(12),ColumnCount=1,RowCount=5};root.ColumnStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Percent,100));root.RowStyles.Add(new(SizeType.AutoSize));root.RowStyles.Add(new(SizeType.Absolute,242));root.RowStyles.Add(new(SizeType.Absolute,42));root.Controls.Add(Classic.Label("Пользователи и группы Windows (DOMAIN\\user, SERVER\\user):"),0,0);root.Controls.Add(list,0,1);
  var edit=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,FlowDirection=FlowDirection.RightToLeft};edit.Controls.Add(Classic.Button("Удалить",(_,_)=>{if(list.SelectedIndex>=0){rules.RemoveAt(list.SelectedIndex);RefreshRules();}}));edit.Controls.Add(Classic.Button("Добавить…",(_,_)=>AddRule()));root.Controls.Add(edit,0,2);
  var table=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=9};table.ColumnStyles.Add(new(SizeType.Percent,100));table.ColumnStyles.Add(new(SizeType.Absolute,100));table.ColumnStyles.Add(new(SizeType.Absolute,100));table.Controls.Add(Classic.Label("Права пользователя / группы"),0,0);table.Controls.Add(Classic.Label("Разрешить"),1,0);table.Controls.Add(Classic.Label("Запретить"),2,0);int row=1;
  foreach(var flag in new[]{Permissions.Control,Permissions.View,Permissions.Files,Permissions.Chat,Permissions.Clipboard,Permissions.Power,Permissions.Notify}){
   table.Controls.Add(Classic.Label(Ui.Rights(flag)),0,row);var allow=new CheckBox{AutoSize=true};var deny=new CheckBox{AutoSize=true};allows[flag]=allow;denies[flag]=deny;table.Controls.Add(allow,1,row);table.Controls.Add(deny,2,row++);allow.CheckedChanged+=(_,_)=>SaveSelection();deny.CheckedChanged+=(_,_)=>SaveSelection();
  }
  table.Controls.Add(enabled,0,8);enabled.CheckedChanged+=(_,_)=>SaveSelection();root.Controls.Add(table,0,3);
  var actions=Classic.Buttons();var cancel=Classic.Button("Отмена",(_,_)=>DialogResult=DialogResult.Cancel);var ok=Classic.Button("OK",(_,_)=>{try{store.SaveWindowsRules(rules.ToArray());changed();DialogResult=DialogResult.OK;}catch(Exception e){MessageBox.Show(this,e.Message,"Разрешения не сохранены");}});actions.Controls.Add(cancel);actions.Controls.Add(ok);root.Controls.Add(actions,0,4);Controls.Add(root);AcceptButton=ok;CancelButton=cancel;list.SelectedIndexChanged+=(_,_)=>LoadSelection();RefreshRules();
 }
 void RefreshRules(){loading=true;list.Items.Clear();foreach(var rule in rules)list.Items.Add(rule.Name+"  ["+rule.Sid+"]");loading=false;if(list.Items.Count>0)list.SelectedIndex=0;else LoadSelection();}
 void LoadSelection(){loading=true;var rule=list.SelectedIndex>=0?rules[list.SelectedIndex]:null;foreach(var pair in allows){pair.Value.Enabled=rule!=null;pair.Value.Checked=rule?.Allow.HasFlag(pair.Key)==true;}foreach(var pair in denies){pair.Value.Enabled=rule!=null;pair.Value.Checked=rule?.Deny.HasFlag(pair.Key)==true;}enabled.Enabled=rule!=null;enabled.Checked=rule?.Enabled==true;loading=false;}
 void SaveSelection(){if(loading||list.SelectedIndex<0)return;var allow=allows.Where(x=>x.Value.Checked).Aggregate(Permissions.None,(p,x)=>p|x.Key);var deny=denies.Where(x=>x.Value.Checked).Aggregate(Permissions.None,(p,x)=>p|x.Key);rules[list.SelectedIndex]=rules[list.SelectedIndex] with{Allow=allow,Deny=deny,Enabled=enabled.Checked};}
 void AddRule(){var name=Ui.Ask(this,"Добавить пользователя или группу Windows","DOMAIN\\user, SERVER\\user или имя группы");if(string.IsNullOrWhiteSpace(name))return;try{var rule=WindowsAuthentication.Resolve(name,Permissions.View);if(rules.Any(x=>x.Sid==rule.Sid)){MessageBox.Show(this,"Этот SID уже добавлен.");return;}rules.Add(rule);RefreshRules();list.SelectedIndex=rules.Count-1;}catch(Exception e){MessageBox.Show(this,"Не удалось найти пользователя или группу Windows: "+e.Message,"Проверка имени");}}
}
