namespace RemoteAdmin.Shared;
public static class Ui {
 public static readonly Color Background=Color.FromArgb(20,25,36),Panel=Color.FromArgb(30,38,53),Accent=Color.FromArgb(75,151,255);
 public static void Theme(Form form){form.BackColor=Background;form.ForeColor=Color.WhiteSmoke;form.Font=new Font("Segoe UI",10);form.MinimumSize=new Size(700,500);form.StartPosition=FormStartPosition.CenterScreen;}
 public static Button Button(string text,EventHandler click){var b=new Button{Text=text,AutoSize=true,BackColor=Panel,ForeColor=Color.WhiteSmoke,FlatStyle=FlatStyle.Flat,Padding=new Padding(9,5,9,5)};b.Click+=click;return b;}
 public static TextBox Text(string value="",int width=260)=>new(){Text=value,Width=width,BackColor=Panel,ForeColor=Color.WhiteSmoke,BorderStyle=BorderStyle.FixedSingle};
 public static FlowLayoutPanel Row()=>new(){AutoSize=true,Dock=DockStyle.Top,Padding=new Padding(10),WrapContents=true};
 public static Label Label(string text)=>new(){Text=text,AutoSize=true,Margin=new Padding(8,10,8,4)};
 public static string? Ask(Form owner,string title,string description,string value=""){
  using var f=new Form{Text=title,Width=560,Height=210,StartPosition=FormStartPosition.CenterParent};Theme(f);f.MinimumSize=new Size(400,190);
  var row=Row();var input=Text(value,480);row.Controls.Add(Label(description));row.Controls.Add(input);var ok=Button("OK",(_,_)=>f.DialogResult=DialogResult.OK);row.Controls.Add(ok);f.Controls.Add(row);f.AcceptButton=ok;
  return f.ShowDialog(owner)==DialogResult.OK?input.Text:null;
 }
 public static bool Consent(Form owner,string message){
  using var f=new Form{Text="Подтверждение удалённого доступа",Width=600,Height=280,StartPosition=FormStartPosition.CenterParent};Theme(f);f.MinimumSize=new Size(500,250);
  var label=new Label{Text=message,Dock=DockStyle.Fill,Padding=new Padding(15)};var row=Row();row.Dock=DockStyle.Bottom;
  var yes=Button("Разрешить",(_,_)=>f.DialogResult=DialogResult.Yes);var no=Button("Отклонить",(_,_)=>f.DialogResult=DialogResult.No);row.Controls.Add(yes);row.Controls.Add(no);f.Controls.Add(label);f.Controls.Add(row);f.CancelButton=no;
  using var timer=new System.Windows.Forms.Timer{Interval=60000};timer.Tick+=(_,_)=>{f.DialogResult=DialogResult.No;f.Close();};timer.Start();
  return f.ShowDialog(owner)==DialogResult.Yes;
 }
 public static Task<T> OnUi<T>(Control control,Func<T> f){var tcs=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);if(control.IsDisposed){tcs.SetCanceled();return tcs.Task;}control.BeginInvoke((Action)(()=>{try{tcs.SetResult(f());}catch(Exception e){tcs.SetException(e);}}));return tcs.Task;}
}
