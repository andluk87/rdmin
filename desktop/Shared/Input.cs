using System.Runtime.InteropServices;
namespace RemoteAdmin.Shared;
public sealed class RemoteInput : IDisposable {
 readonly HashSet<int> keys=[];readonly HashSet<int> buttons=[];
 [StructLayout(LayoutKind.Sequential)]struct INPUT{public uint type;public UNION data;}
 [StructLayout(LayoutKind.Explicit)]struct UNION{[FieldOffset(0)]public MOUSE mouse;[FieldOffset(0)]public KEY keyboard;}
 [StructLayout(LayoutKind.Sequential)]struct MOUSE{public int dx,dy;public uint mouseData,dwFlags,time;public UIntPtr extra;}
 [StructLayout(LayoutKind.Sequential)]struct KEY{public ushort vk,scan;public uint flags,time;public UIntPtr extra;}
 [DllImport("user32.dll",SetLastError=true)]static extern uint SendInput(uint count,INPUT[] values,int size);
 static void Send(INPUT i){if(SendInput(1,[i],Marshal.SizeOf<INPUT>())!=1)throw new InvalidOperationException("Windows отклонила ввод (UAC/защищённый рабочий стол)");}
 public void Key(KeyEvent e){if(e.Key<1||e.Key>254)throw new InvalidDataException("Неверная клавиша");if(e.Down)keys.Add(e.Key);else keys.Remove(e.Key);Send(new(){type=1,data=new(){keyboard=new(){vk=(ushort)e.Key,flags=(e.Down?0u:2u)|(e.Key is 33 or 34 or 35 or 36 or 37 or 38 or 39 or 40 or 45 or 46 or 91 or 92 or 111 or 163 or 165?1u:0u)}}});}
 public void Text(string value){
  if(value.Length>8192)throw new InvalidDataException("Слишком большой текст");
  foreach(var ch in value){Send(new(){type=1,data=new(){keyboard=new(){scan=ch,flags=4}}});Send(new(){type=1,data=new(){keyboard=new(){scan=ch,flags=4|2}}});}
 }
 public void Mouse(MouseEvent e,int monitor){
  if(!double.IsFinite(e.X)||!double.IsFinite(e.Y)||e.X<0||e.X>1||e.Y<0||e.Y>1)throw new InvalidDataException("Неверные координаты");
  var displays=DesktopCapture.Displays();if(monitor<0||monitor>=displays.Length)throw new InvalidDataException("Монитор отключён");var d=displays[monitor];var v=SystemInformation.VirtualScreen;
  var x=d.X+(int)Math.Round(e.X*(d.Width-1));var y=d.Y+(int)Math.Round(e.Y*(d.Height-1));
  Send(new(){type=0,data=new(){mouse=new(){dx=(int)((long)(x-v.X)*65535/Math.Max(1,v.Width-1)),dy=(int)((long)(y-v.Y)*65535/Math.Max(1,v.Height-1)),dwFlags=0x8000|0x4000|1}}});
  uint flags=0;if(e.Action=="down"||e.Action=="up"){
   if(e.Button is <0 or >2)throw new InvalidDataException("Неверная кнопка");bool down=e.Action=="down";flags=e.Button switch{0=>down?2u:4u,1=>down?8u:16u,_=>down?32u:64u};if(down)buttons.Add(e.Button);else buttons.Remove(e.Button);
  }else if(e.Action=="wheel")flags=0x800;else if(e.Action!="move")throw new InvalidDataException("Неверное действие");
  if(flags!=0)Send(new(){type=0,data=new(){mouse=new(){dwFlags=flags,mouseData=unchecked((uint)Math.Clamp(e.Delta,-1200,1200))}}});
 }
 public void Release(){foreach(var key in keys.ToArray()){try{Key(new(key,false));}catch{}}keys.Clear();foreach(var b in buttons){try{Send(new(){data=new(){mouse=new(){dwFlags=b==0?4u:b==1?16u:64u}}});}catch{}}buttons.Clear();}
 public void Dispose()=>Release();
}
