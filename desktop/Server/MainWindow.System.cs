using RemoteAdmin.Shared;
namespace RemoteAdmin.Server;
public sealed partial class MainWindow {
 readonly List<Form> notifications=[];
 DateTimeOffset lastNotification;
 void ShowNotification(string message){
  if(message.Length==0||message.Length>8192)throw new InvalidDataException("Размер сообщения: 1–8192 символа");
  if(notifications.Count>=3||DateTimeOffset.UtcNow-lastNotification<TimeSpan.FromSeconds(2))throw new InvalidOperationException("Слишком много уведомлений");lastNotification=DateTimeOffset.UtcNow;
  var window=new Form();Classic.Dialog(window,"Сообщение от "+activeUser,new Size(510,300));var text=new TextBox{Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Vertical,Text=message,BackColor=SystemColors.Window};var buttons=Classic.Buttons();buttons.Controls.Add(Classic.Button("Закрыть",(_,_)=>window.Close()));window.Controls.Add(text);window.Controls.Add(buttons);notifications.Add(window);window.FormClosed+=(_,_)=>{notifications.Remove(window);window.Dispose();};window.Show(this);
 }
}
