namespace RemoteAdmin.Client;
public sealed class RemoteScreen : Control {
 Image? frame;
 public Image? Frame {get=>frame;set{var old=frame;frame=value;old?.Dispose();Invalidate();}}
 public RemoteScreen(){SetStyle(ControlStyles.Selectable|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.AllPaintingInWmPaint,true);TabStop=true;BackColor=Color.FromArgb(18,25,38);}
 public Rectangle Content {
  get {if(frame==null)return Rectangle.Empty;var scale=Math.Min((double)ClientSize.Width/frame.Width,(double)ClientSize.Height/frame.Height);var w=(int)(frame.Width*scale);var h=(int)(frame.Height*scale);return new((Width-w)/2,(Height-h)/2,w,h);}
 }
 public bool Coordinates(Point p,out double x,out double y){var r=Content;x=y=0;if(r.Width<1||r.Height<1||!r.Contains(p))return false;x=Math.Clamp((double)(p.X-r.X)/Math.Max(1,r.Width-1),0,1);y=Math.Clamp((double)(p.Y-r.Y)/Math.Max(1,r.Height-1),0,1);return true;}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);if(frame!=null)e.Graphics.DrawImage(frame,Content);else{
  using var title=new Font("Segoe UI",17,FontStyle.Bold);var area=new Rectangle(20,Height/2-40,Math.Max(0,Width-40),40);TextRenderer.DrawText(e.Graphics,"Ожидаем изображение",title,area,Color.FromArgb(220,229,246),TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
  area.Y+=44;TextRenderer.DrawText(e.Graphics,"Экран появится после подтверждения на сервере",Font,area,Color.FromArgb(137,153,180),TextFormatFlags.HorizontalCenter|TextFormatFlags.WordBreak);
 }}
 protected override bool IsInputKey(Keys keyData)=>true;
 protected override void Dispose(bool disposing){if(disposing)frame?.Dispose();base.Dispose(disposing);}
}
