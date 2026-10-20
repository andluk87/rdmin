namespace RemoteAdmin.Client;
internal static class ViewerUi {
 public static void Dialog(Form form,string title,Size size){form.Icon=RemoteAdmin.Shared.Ui.AppIcon;form.Text=title;form.Font=new Font("Segoe UI",9);form.BackColor=SystemColors.Control;form.ForeColor=SystemColors.ControlText;form.ClientSize=size;form.MinimumSize=Size.Empty;form.FormBorderStyle=FormBorderStyle.FixedDialog;form.MaximizeBox=false;form.MinimizeBox=false;form.StartPosition=FormStartPosition.CenterParent;form.AutoScaleMode=AutoScaleMode.Dpi;}
 public static Button Button(string text,EventHandler click){var button=new Button{Text=text,Width=105,Height=29,UseVisualStyleBackColor=true,Margin=new Padding(4)};button.Click+=click;return button;}
 public static FlowLayoutPanel Buttons()=>new(){Dock=DockStyle.Bottom,Height=43,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(6,3,6,3),WrapContents=false};
 public static Label Label(string text)=>new(){Text=text,AutoSize=true,Margin=new Padding(4,6,4,6)};
 public static TableLayoutPanel Stack(){var panel=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=1,RowCount=0};panel.ColumnStyles.Add(new(SizeType.Percent,100));return panel;}
 public static void Add(TableLayoutPanel panel,Control control){int row=panel.RowCount++;panel.RowStyles.Add(new(SizeType.AutoSize));control.Anchor=AnchorStyles.Left|AnchorStyles.Right;panel.Controls.Add(control,0,row);}
 public static Control Field(string title,Control control){var panel=Stack();Add(panel,Label(title));control.Dock=DockStyle.Top;Add(panel,control);return panel;}
}
