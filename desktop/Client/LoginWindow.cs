using RemoteAdmin.Shared;
namespace RemoteAdmin.Client;

public sealed class LoginWindow : Form {
 readonly TextBox username=Ui.Text(),password=Ui.Text();
 readonly CheckBox currentWindows=new(){Text="Текущая учётная запись Windows",AutoSize=true,Checked=true};
 public bool UseCurrentWindows=>currentWindows.Checked;
 public string Username=>username.Text.Trim();
 public string Password=>password.Text;
 public LoginWindow(string server,string lastUsername,bool windows=false){
  ViewerUi.Dialog(this,"Вход на сервер",new Size(450,350));var content=ViewerUi.Stack();content.Dock=DockStyle.Fill;content.Padding=new Padding(16);ViewerUi.Add(content,ViewerUi.Label("Сервер: "+server+(windows?"\nУчётная запись Windows: DOMAIN\\user или SERVER\\user.":"\nВведите учётную запись RemoteAdmin.")));if(windows){ViewerUi.Add(content,currentWindows);currentWindows.CheckedChanged+=(_,_)=>username.Enabled=password.Enabled=!currentWindows.Checked;username.Enabled=password.Enabled=false;}
  username.BackColor=password.BackColor=SystemColors.Window;username.ForeColor=password.ForeColor=SystemColors.WindowText;username.Text=lastUsername;username.MaxLength=64;password.UseSystemPasswordChar=true;password.MaxLength=256;ViewerUi.Add(content,ViewerUi.Field("Имя пользователя:",username));ViewerUi.Add(content,ViewerUi.Field("Пароль:",password));
  var actions=ViewerUi.Buttons();var login=ViewerUi.Button("OK",(_,_)=>{if(windows&&currentWindows.Checked){DialogResult=DialogResult.OK;return;}if(username.Text.Trim().Length==0){username.Focus();return;}if(password.Text.Length==0){password.Focus();return;}DialogResult=DialogResult.OK;});var cancel=ViewerUi.Button("Отмена",(_,_)=>DialogResult=DialogResult.Cancel);actions.Controls.Add(cancel);actions.Controls.Add(login);Controls.Add(content);Controls.Add(actions);AcceptButton=login;CancelButton=cancel;Shown+=(_,_)=>{if(username.Text.Length==0)username.Focus();else password.Focus();};
 }
 public void ClearSecret()=>password.Clear();
}
