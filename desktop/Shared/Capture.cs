using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace RemoteAdmin.Shared;

public sealed class DesktopCapture : IDisposable {
 IntPtr native;
 int monitor=-1;
 IntPtr pixels;int pixelsCapacity;
 public string Mode {get;private set;}="DXGI";
 [DllImport("remoteadmin_capture.dll",CallingConvention=CallingConvention.Cdecl,CharSet=CharSet.Unicode)] static extern IntPtr ra_open(string deviceName);
 [DllImport("remoteadmin_capture.dll",CallingConvention=CallingConvention.Cdecl)] static extern int ra_frame(IntPtr context,IntPtr data,int capacity,out int width,out int height,out int rotation);
 [DllImport("remoteadmin_capture.dll",CallingConvention=CallingConvention.Cdecl)] static extern void ra_close(IntPtr context);
 public static DisplayInfo[] Displays()=>Screen.AllScreens.Select((s,i)=>new DisplayInfo(i,s.DeviceName,s.Bounds.X,s.Bounds.Y,s.Bounds.Width,s.Bounds.Height)).ToArray();
 public ScreenFrame? Grab(int index,int quality){
  var screens=Screen.AllScreens;if(index<0||index>=screens.Length)throw new InvalidDataException("Монитор отключён");
  if(monitor!=index){Dispose();monitor=index;try{native=ra_open(screens[index].DeviceName);}catch(DllNotFoundException){}catch(EntryPointNotFoundException){} }
  Bitmap? bitmap=null;
  if(native!=IntPtr.Zero){
   var capacity=checked(screens[index].Bounds.Width*screens[index].Bounds.Height*4);if(pixelsCapacity<capacity){if(pixels!=IntPtr.Zero)Marshal.FreeHGlobal(pixels);pixels=Marshal.AllocHGlobal(capacity);pixelsCapacity=capacity;}var data=pixels;
   {
    var result=ra_frame(native,data,capacity,out var w,out var h,out var rotation);
    if(result==0)return null;
    if(result<0){Dispose();throw new InvalidOperationException("DXGI: рабочий стол недоступен или изменена конфигурация дисплея");}
    using var raw=new Bitmap(w,h,w*4,PixelFormat.Format32bppArgb,data);bitmap=new Bitmap(raw);
    if(rotation==2)bitmap.RotateFlip(RotateFlipType.Rotate90FlipNone);else if(rotation==3)bitmap.RotateFlip(RotateFlipType.Rotate180FlipNone);else if(rotation==4)bitmap.RotateFlip(RotateFlipType.Rotate270FlipNone);
    Mode="DXGI";
   }
  } else {
   Mode="GDI fallback";var b=screens[index].Bounds;bitmap=new Bitmap(b.Width,b.Height,PixelFormat.Format32bppArgb);
   using var g=Graphics.FromImage(bitmap);g.CopyFromScreen(b.Location,Point.Empty,b.Size,CopyPixelOperation.SourceCopy);

  }
  var bounds=screens[index].Bounds;var cursor=Cursor.Position;
  if(bounds.Contains(cursor))using(var graphics=Graphics.FromImage(bitmap!)){Cursors.Default.Draw(graphics,new Rectangle(cursor.X-bounds.X,cursor.Y-bounds.Y,24,24));}
  using(bitmap){using var memory=new MemoryStream();using var parameters=new EncoderParameters(1);parameters.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,(long)Math.Clamp(quality,20,90));bitmap.Save(memory,ImageCodecInfo.GetImageEncoders().First(x=>x.MimeType=="image/jpeg"),parameters);return new(index,bitmap.Width,bitmap.Height,Convert.ToBase64String(memory.ToArray()));}
 }
 public void Dispose(){if(pixels!=IntPtr.Zero){Marshal.FreeHGlobal(pixels);pixels=IntPtr.Zero;pixelsCapacity=0;}if(native!=IntPtr.Zero){ra_close(native);native=IntPtr.Zero;}monitor=-1;}
}
