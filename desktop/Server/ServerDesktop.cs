using System.Text.Json;
using Microsoft.Win32;

namespace RemoteAdmin.Server;
public sealed record DesktopOptions(bool ShowTray=true);
internal static class ServerDesktop {
 const string RunKey=@"Software\Microsoft\Windows\CurrentVersion\Run";
 const string ValueName="RemoteAdminServer";
 static string Command=>"\""+(Environment.ProcessPath??throw new InvalidOperationException("Не найден путь приложения"))+"\" --auto";
 public static bool Automatic {get{using var key=Registry.CurrentUser.OpenSubKey(RunKey);return string.Equals(key?.GetValue(ValueName) as string,Command,StringComparison.OrdinalIgnoreCase);}}
 public static void SetAutomatic(bool enabled){using var key=Registry.CurrentUser.CreateSubKey(RunKey,true);if(enabled)key.SetValue(ValueName,Command,RegistryValueKind.String);else key.DeleteValue(ValueName,false);}
 public static DesktopOptions Load(string folder){var path=Path.Combine(folder,"desktop.json");return File.Exists(path)?JsonSerializer.Deserialize<DesktopOptions>(File.ReadAllText(path))??new():new();}
 public static void Save(string folder,DesktopOptions options){var path=Path.Combine(folder,"desktop.json");File.WriteAllText(path+".tmp",JsonSerializer.Serialize(options));File.Move(path+".tmp",path,true);}
}

internal sealed class StartupWindow : Form {
 public StartupWindow(){
  Classic.Dialog(this,"Режим запуска",new Size(420,225));var group=Classic.Group("Режим запуска RemoteAdmin Server");group.Dock=DockStyle.Top;group.Height=166;
  var stack=Classic.Stack();var automatic=new RadioButton{Text="Автоматически при входе в Windows",AutoSize=true,Checked=ServerDesktop.Automatic};var manual=new RadioButton{Text="Вручную",AutoSize=true,Checked=!automatic.Checked};Classic.Add(stack,automatic);Classic.Add(stack,manual);var note=Classic.Label("Автоматический режим запускает видимое приложение\nв текущей учётной записи Windows. Это не служба Windows.");Classic.Add(stack,note);group.Controls.Add(stack);
  var buttons=Classic.Buttons();var cancel=Classic.Button("Отмена",(_,_)=>DialogResult=DialogResult.Cancel);var ok=Classic.Button("OK",(_,_)=>{try{ServerDesktop.SetAutomatic(automatic.Checked);DialogResult=DialogResult.OK;}catch(Exception e){MessageBox.Show(this,e.Message,"Не удалось сохранить режим запуска");}});buttons.Controls.Add(cancel);buttons.Controls.Add(ok);Controls.Add(group);Controls.Add(buttons);AcceptButton=ok;CancelButton=cancel;
 }
}
