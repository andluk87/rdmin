using System.Drawing.Drawing2D;

namespace RemoteAdmin.Shared;

public static class Ui {
 public static string AppVersion=>typeof(Ui).Assembly.GetName().Version?.ToString(3)??"?";
 public static readonly Color Background=Color.FromArgb(243,246,251);
 public static readonly Color Panel=Color.White;
 public static readonly Color Accent=Color.FromArgb(48,102,235);
 public static readonly Color Ink=Color.FromArgb(27,39,61);
 public static readonly Color Muted=Color.FromArgb(106,119,140);
 public static readonly Color Border=Color.FromArgb(224,230,240);
 public static readonly Color Success=Color.FromArgb(23,128,91);
 public static readonly Color Danger=Color.FromArgb(194,52,70);
 static readonly ToolTip tips=new(){AutoPopDelay=10000,InitialDelay=400,ReshowDelay=100};

 public static void Theme(Form form){
  form.BackColor=Background;form.ForeColor=Ink;form.Font=new Font("Segoe UI",10);
  form.MinimumSize=new Size(840,620);form.StartPosition=FormStartPosition.CenterScreen;
  form.AutoScaleMode=AutoScaleMode.Dpi;
 }
 public static Button Button(string text,EventHandler click){
  var button=new ActionButton{Text=text,AutoSize=true,MinimumSize=new Size(90,40),Margin=new Padding(0,0,8,8)};
  button.Click+=click;return button;
 }
 public static Button Primary(string text,EventHandler click){var button=Button(text,click);button.BackColor=Accent;button.ForeColor=Color.White;return button;}
 public static Button Destructive(string text,EventHandler click){var button=Button(text,click);button.BackColor=Color.FromArgb(255,238,240);button.ForeColor=Danger;return button;}
 public static void Hint(Control control,string message)=>tips.SetToolTip(control,message);
 public static TextBox Text(string value="",int width=260)=>new(){Text=value,Width=width,BackColor=Panel,ForeColor=Ink,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,4,0,12)};
 public static FlowLayoutPanel Row()=>new(){AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Dock=DockStyle.Top,Margin=Padding.Empty,Padding=Padding.Empty,WrapContents=true};
 public static Label Label(string text)=>new(){Text=text,AutoSize=true,ForeColor=Ink,Margin=new Padding(0,3,8,8)};
 public static Label Heading(string text,int size=18)=>new(){Text=text,AutoSize=true,Font=new Font("Segoe UI",size,FontStyle.Bold),ForeColor=Ink,Margin=new Padding(0,0,0,8)};
 public static Label Description(string text)=>new(){Text=text,AutoSize=true,ForeColor=Muted,MaximumSize=new Size(640,0),Margin=new Padding(0,0,0,16)};
 public static TableLayoutPanel Stack(){
  var stack=new TableLayoutPanel{ColumnCount=1,RowCount=0,Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Margin=Padding.Empty,Padding=Padding.Empty};
  stack.ColumnStyles.Add(new(SizeType.Percent,100));return stack;
 }
 public static void Add(TableLayoutPanel stack,Control child){
  var row=stack.RowCount++;stack.RowStyles.Add(new(SizeType.AutoSize));child.Anchor=AnchorStyles.Left|AnchorStyles.Right;stack.Controls.Add(child,0,row);
 }
 public static TableLayoutPanel Card(string title,string? subtitle=null){
  var card=Stack();card.BackColor=Panel;card.Padding=new Padding(22);card.Margin=new Padding(0,0,0,18);
  Add(card,Heading(title,14));if(!string.IsNullOrEmpty(subtitle))Add(card,Description(subtitle));return card;
 }
 public static Control Field(string title,Control input,string? hint=null){
  var field=Stack();field.Margin=new Padding(0,0,0,12);Add(field,Label(title));input.Dock=DockStyle.Top;input.Margin=new Padding(0,0,0,4);Add(field,input);
  if(hint!=null){var label=Description(hint);label.Margin=new Padding(0,4,0,0);Add(field,label);}return field;
 }
 public static TableLayoutPanel FillGrid(int columns=1){
  var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=columns,RowCount=1,Margin=Padding.Empty,Padding=Padding.Empty};
  for(int i=0;i<columns;i++)grid.ColumnStyles.Add(new(SizeType.Percent,100f/columns));grid.RowStyles.Add(new(SizeType.Percent,100));return grid;
 }
 public static void StyleCheck(CheckBox check,string? hint=null){check.AutoSize=true;check.Margin=new Padding(0,8,20,10);check.ForeColor=Ink;if(hint!=null)Hint(check,hint);}
 public static void StyleCombo(ComboBox combo){combo.FlatStyle=FlatStyle.Flat;combo.BackColor=Color.White;combo.ForeColor=Ink;combo.Margin=new Padding(0,4,12,8);combo.IntegralHeight=false;combo.MaxDropDownItems=12;}
 public static void StyleChat(TextBox chat){chat.Dock=DockStyle.Fill;chat.BackColor=Color.White;chat.ForeColor=Ink;chat.BorderStyle=BorderStyle.None;chat.Font=new Font("Segoe UI",10);}
 public static string Rights(Permissions rights){
  var parts=new List<string>();if(rights.HasFlag(Permissions.View))parts.Add("Просмотр экрана");if(rights.HasFlag(Permissions.Control))parts.Add("Управление мышью и клавиатурой");if(rights.HasFlag(Permissions.Files))parts.Add("Передача файлов");if(rights.HasFlag(Permissions.Clipboard))parts.Add("Текстовый буфер обмена");if(rights.HasFlag(Permissions.Chat))parts.Add("Чат");return string.Join(" • ",parts);
 }
 public static string? Ask(Form owner,string title,string description,string value=""){
  using var f=new Form{Text=title,ClientSize=new Size(520,280)};Theme(f);f.MinimumSize=new Size(480,260);f.ClientSize=new Size(520,280);f.FormBorderStyle=FormBorderStyle.FixedDialog;f.MaximizeBox=false;f.MinimizeBox=false;f.StartPosition=FormStartPosition.CenterParent;
  var content=Stack();content.Dock=DockStyle.Fill;content.Padding=new Padding(24);Add(content,Heading(title,16));var input=Text(value);Add(content,Field(description,input));
  var actions=Row();var ok=Primary("Сохранить",(_,_)=>f.DialogResult=DialogResult.OK);actions.Controls.Add(ok);actions.Controls.Add(Button("Отмена",(_,_)=>f.DialogResult=DialogResult.Cancel));Add(content,actions);f.Controls.Add(content);f.AcceptButton=ok;f.CancelButton=(Button)actions.Controls[1];f.Shown+=(_,_)=>{input.Focus();input.SelectAll();};
  return f.ShowDialog(owner)==DialogResult.OK?input.Text:null;
 }
 public static bool Consent(Form owner,string message){
  using var f=new Form{Text="Запрос на подключение",ClientSize=new Size(580,360)};Theme(f);f.MinimumSize=new Size(560,380);f.ClientSize=new Size(580,360);f.StartPosition=FormStartPosition.CenterParent;f.FormBorderStyle=FormBorderStyle.FixedDialog;f.MaximizeBox=false;f.MinimizeBox=false;
  var content=Stack();content.Dock=DockStyle.Fill;content.Padding=new Padding(28);Add(content,Heading("Разрешить подключение?",20));Add(content,Description(message));var countdown=Label("Запрос будет отклонён через 60 секунд");Add(content,countdown);var actions=Row();
  var yes=Primary("Разрешить доступ",(_,_)=>f.DialogResult=DialogResult.Yes);var no=Button("Отклонить",(_,_)=>f.DialogResult=DialogResult.No);actions.Controls.Add(yes);actions.Controls.Add(no);Add(content,actions);f.Controls.Add(content);f.CancelButton=no;f.AcceptButton=no;
  int remaining=60;using var timer=new System.Windows.Forms.Timer{Interval=1000};timer.Tick+=(_,_)=>{remaining--;countdown.Text=$"Автоматический отказ через {remaining} сек.";if(remaining<=0){f.DialogResult=DialogResult.No;f.Close();}};timer.Start();return f.ShowDialog(owner)==DialogResult.Yes;
 }
 public static Task<T> OnUi<T>(Control control,Func<T> f){var tcs=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);if(control.IsDisposed){tcs.SetCanceled();return tcs.Task;}control.BeginInvoke((Action)(()=>{try{tcs.SetResult(f());}catch(Exception e){tcs.SetException(e);}}));return tcs.Task;}
}

// Standard Button semantics (keyboard, accessibility, DialogResult) with consistent rendering.
public sealed class ActionButton : Button {
 bool hover;
 public ActionButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;BackColor=Color.White;ForeColor=Ui.Ink;Cursor=Cursors.Hand;Padding=new Padding(15,8,15,8);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);}
 public override Size GetPreferredSize(Size proposedSize){var text=TextRenderer.MeasureText(Text,Font);return new(Math.Max(MinimumSize.Width,text.Width+Padding.Horizontal+8),Math.Max(MinimumSize.Height,text.Height+Padding.Vertical+4));}
 protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
 protected override void OnMouseLeave(EventArgs e){hover=false;Invalidate();base.OnMouseLeave(e);}
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;var rect=new Rectangle(1,1,Math.Max(1,Width-3),Math.Max(1,Height-3));using var path=new GraphicsPath();const int d=14;
  path.AddArc(rect.Left,rect.Top,d,d,180,90);path.AddArc(rect.Right-d,rect.Top,d,d,270,90);path.AddArc(rect.Right-d,rect.Bottom-d,d,d,0,90);path.AddArc(rect.Left,rect.Bottom-d,d,d,90,90);path.CloseFigure();
  var fill=!Enabled?Color.FromArgb(232,237,245):hover?ControlPaint.Dark(BackColor,0.05f):BackColor;using var brush=new SolidBrush(fill);e.Graphics.FillPath(brush,path);using var pen=new Pen(BackColor==Color.White?Ui.Border:fill);e.Graphics.DrawPath(pen,path);
  TextRenderer.DrawText(e.Graphics,Text,Font,rect,Enabled?ForeColor:Ui.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
  if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(rect,-5,-5),ForeColor,fill);
 }
}
