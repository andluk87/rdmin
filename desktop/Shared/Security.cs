using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using System.Runtime.InteropServices;

namespace RemoteAdmin.Shared;

public static class LocalData {
 public static string Folder(string app){var p=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"RemoteAdmin",app);Directory.CreateDirectory(p);return p;}
 public static void Audit(string folder,string kind,string session,string outcome){
  // Never log pairing codes, keys, chat, clipboard, file contents, or input.
  var file=Path.Combine(folder,"audit.jsonl");if(File.Exists(file)&&new FileInfo(file).Length>10*1024*1024)File.Move(file,file+".previous",true);
  File.AppendAllText(file,JsonSerializer.Serialize(new{at=DateTimeOffset.UtcNow,kind,session,outcome})+Environment.NewLine);
 }
}
public static class Identity {
 public static X509Certificate2 Load(string folder){
  var path=Path.Combine(folder,"identity.dpapi");
  if(File.Exists(path))return new X509Certificate2(Protect(File.ReadAllBytes(path),false),(string?)null,X509KeyStorageFlags.EphemeralKeySet);
  using var key=ECDsa.Create(ECCurve.NamedCurves.nistP256);
  var req=new CertificateRequest("CN=RemoteAdmin-"+Environment.MachineName,key,HashAlgorithmName.SHA256);
  req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
  req.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature,true));
  var eku=new System.Security.Cryptography.OidCollection();eku.Add(new("1.3.6.1.5.5.7.3.1"));req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(eku,false));
  using var cert=req.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddYears(2));
  var pfx=cert.Export(X509ContentType.Pfx);File.WriteAllBytes(path,Protect(pfx,true));
  return new X509Certificate2(pfx,(string?)null,X509KeyStorageFlags.EphemeralKeySet);
 }
 public static string Fingerprint(X509Certificate2 cert)=>Convert.ToHexString(SHA256.HashData(cert.RawData));
 public static string Code()=>Convert.ToHexString(RandomNumberGenerator.GetBytes(16));
 public static bool Equal(string left,string right)=>CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(left)),SHA256.HashData(Encoding.UTF8.GetBytes(right)));
 [StructLayout(LayoutKind.Sequential)] struct Blob {public int Size;public IntPtr Data;}
 [DllImport("crypt32.dll",SetLastError=true,CharSet=CharSet.Unicode)]static extern bool CryptProtectData(ref Blob data,string description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob result);
 [DllImport("crypt32.dll",SetLastError=true)]static extern bool CryptUnprotectData(ref Blob data,IntPtr description,IntPtr entropy,IntPtr reserved,IntPtr prompt,uint flags,out Blob result);
 [DllImport("kernel32.dll")]static extern IntPtr LocalFree(IntPtr memory);
 static byte[] Protect(byte[] bytes,bool encrypt){
  var input=new Blob{Size=bytes.Length,Data=Marshal.AllocHGlobal(bytes.Length)};Marshal.Copy(bytes,0,input.Data,bytes.Length);
  try{Blob output;bool ok=encrypt?CryptProtectData(ref input,"RemoteAdmin",IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output):CryptUnprotectData(ref input,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,IntPtr.Zero,1,out output);
   if(!ok)throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
   try{var result=new byte[output.Size];Marshal.Copy(output.Data,result,0,result.Length);return result;}finally{LocalFree(output.Data);}
  }finally{Marshal.FreeHGlobal(input.Data);}
 }
}
