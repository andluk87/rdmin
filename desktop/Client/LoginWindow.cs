using RemoteAdmin.Shared;
namespace RemoteAdmin.Client;

public sealed class LoginWindow : Form {
 readonly TextBox username=Ui.Text(),password=Ui.Text();
 public string Username=>username.Text.Trim();
 public string Password=>password.Text;
 public LoginWindow(string server,string lastUsername){
  Ui.Theme(this);MinimumSize=new Size(480,360);ClientSize=new Size(520,390);FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;StartPosition=FormStartPosition.CenterParent;Text="Вход на сервер";
  var content=Ui.Stack();content.Dock=DockStyle.Fill;content.Padding=new Padding(26);Ui.Add(content,Ui.Heading("Вход на "+server,20));Ui.Add(content,Ui.Description("Введите учётную запись RemoteAdmin, созданную на этом сервере. Права определяются сервером."));
  username.Text=lastUsername;username.MaxLength=64;password.UseSystemPasswordChar=true;password.MaxLength=256;Ui.Add(content,Ui.Field("Логин",username));Ui.Add(content,Ui.Field("Пароль",password));
  var actions=Ui.Row();var login=Ui.Primary("Войти",(_,_)=>{if(username.Text.Trim().Length==0){username.Focus();return;}if(password.Text.Length==0){password.Focus();return;}DialogResult=DialogResult.OK;});actions.Controls.Add(login);var cancel=Ui.Button("Отмена",(_,_)=>DialogResult=DialogResult.Cancel);actions.Controls.Add(cancel);Ui.Add(content,actions);Controls.Add(content);AcceptButton=login;CancelButton=cancel;Shown+=(_,_)=>{if(username.Text.Length==0)username.Focus();else password.Focus();};
 }
 public void ClearSecret()=>password.Clear();
}
