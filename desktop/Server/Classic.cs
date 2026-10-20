namespace RemoteAdmin.Server;

// Standard Windows controls, system colours, compact dialogs and keyboard navigation.
internal static class Classic {
 public static void Dialog(Form form,string title,Size size){form.Text=title;form.Font=new Font("Segoe UI",9);form.BackColor=SystemColors.Control;form.ForeColor=SystemColors.ControlText;form.AutoScaleMode=AutoScaleMode.Dpi;form.ClientSize=size;form.MinimumSize=Size.Empty;form.FormBorderStyle=FormBorderStyle.FixedDialog;form.MaximizeBox=false;form.MinimizeBox=false;form.StartPosition=FormStartPosition.CenterParent;form.Icon=SystemIcons.Application;}
 public static Button Button(string text,EventHandler click,int width=110){var button=new Button{Text=text,Width=width,Height=29,UseVisualStyleBackColor=true,Margin=new Padding(4),AutoSize=false};button.Click+=click;return button;}
 public static Label Label(string text)=>new(){Text=text,AutoSize=true,Margin=new Padding(3,6,3,6)};
 public static FlowLayoutPanel Buttons()=>new(){Dock=DockStyle.Bottom,Height=43,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(6,4,6,4),WrapContents=false};
 public static GroupBox Group(string title)=>new(){Text=title,Dock=DockStyle.Fill,Padding=new Padding(10,20,10,10)};
 public static TableLayoutPanel Stack(){var layout=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=0,Padding=Padding.Empty};layout.ColumnStyles.Add(new(SizeType.Percent,100));return layout;}
 public static void Add(TableLayoutPanel layout,Control item){var row=layout.RowCount++;layout.RowStyles.Add(new(SizeType.AutoSize));item.Anchor=AnchorStyles.Left|AnchorStyles.Right;layout.Controls.Add(item,0,row);}
 public static Control Field(string title,Control input){var stack=Stack();Add(stack,Label(title));input.Dock=DockStyle.Top;input.Margin=new Padding(3,0,3,10);Add(stack,input);return stack;}
}
