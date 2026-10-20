using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Principal;
namespace RemoteAdmin.Shared;
public static class PowerActions {
 [StructLayout(LayoutKind.Sequential)]struct Luid{public uint Low;public int High;}
 [StructLayout(LayoutKind.Sequential)]struct TokenPrivileges{public uint Count;public Luid Luid;public uint Attributes;}
 [DllImport("advapi32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool LookupPrivilegeValue(string? system,string name,out Luid luid);
 [DllImport("advapi32.dll",SetLastError=true)]static extern bool AdjustTokenPrivileges(Microsoft.Win32.SafeHandles.SafeAccessTokenHandle token,bool disable,ref TokenPrivileges state,uint size,out TokenPrivileges previous,out uint returned);
 [DllImport("user32.dll",SetLastError=true)]static extern bool ExitWindowsEx(uint flags,uint reason);
 [DllImport("user32.dll",SetLastError=true)]static extern bool LockWorkStation();
 public static void Execute(string action){
  if(action=="lock"){if(!LockWorkStation())throw new Win32Exception(Marshal.GetLastWin32Error());return;}
  if(action!="restart"&&action!="shutdown")throw new ArgumentException("Неизвестная операция питания");
  using var identity=WindowsIdentity.GetCurrent();if(!LookupPrivilegeValue(null,"SeShutdownPrivilege",out var luid))throw new Win32Exception(Marshal.GetLastWin32Error());
  var requested=new TokenPrivileges{Count=1,Luid=luid,Attributes=2};if(!AdjustTokenPrivileges(identity.AccessToken,false,ref requested,(uint)Marshal.SizeOf<TokenPrivileges>(),out var previous,out _))throw new Win32Exception(Marshal.GetLastWin32Error());
  int status=Marshal.GetLastWin32Error();if(status!=0)throw new Win32Exception(status);
  try{if(!ExitWindowsEx(action=="restart"?2u:1u,0x80040000))throw new Win32Exception(Marshal.GetLastWin32Error());}
  finally{_ = AdjustTokenPrivileges(identity.AccessToken,false,ref previous,0,out _,out _);}
 }
}
