using RemoteAdmin.Shared;
using System.Security.Cryptography;
using System.Text.Json;
int passed=0;
void Check(bool condition,string name){if(!condition)throw new Exception("FAIL: "+name);Console.WriteLine("PASS: "+name);passed++;}
var root=Path.Combine(Path.GetTempPath(),"RemoteAdmin-checks-"+Guid.NewGuid());Directory.CreateDirectory(root);
try{
 using var box=new FileSandbox(root);
 foreach(var bad in new[]{"../escape","C:/Windows/file","\\\\host\\share","file:ads","sub/../escape","CON","NUL.txt","x.","LPT1"}){
  bool rejected=false;try{await box.Execute(new("list",bad),default);}catch(IOException){rejected=true;}Check(rejected,"reject "+bad);
 }
 var bytes=RandomNumberGenerator.GetBytes(1024*1024+7);var hash=Convert.ToHexString(SHA256.HashData(bytes));
 var begin=await box.Execute(new("begin","sample.bin",Size:bytes.Length),default);Check(begin.Offset==0,"new transfer offset");
 await box.Execute(new("write","sample.bin",Offset:0,Data:Convert.ToBase64String(bytes[..1000])),default);
 var resume=await box.Execute(new("begin","sample.bin",Size:bytes.Length),default);Check(resume.Offset==1000,"resume after interruption");
 await box.Execute(new("write","sample.bin",Offset:1000,Data:Convert.ToBase64String(bytes[1000..])),default);
 await box.Execute(new("commit","sample.bin",Size:bytes.Length,Sha256:hash),default);Check(File.ReadAllBytes(Path.Combine(root,"sample.bin")).SequenceEqual(bytes),"SHA verified commit");
 var read=await box.Execute(new("read","sample.bin",Offset:1000),default);Check(Convert.FromBase64String(read.Data).SequenceEqual(bytes[1000..]),"offset download");
 await box.Execute(new("begin","bad.bin",Size:1),default);await box.Execute(new("write","bad.bin",Data:Convert.ToBase64String(new byte[]{1})),default);
 bool badHash=false;try{await box.Execute(new("commit","bad.bin",Size:1,Sha256:new string('0',64)),default);}catch(IOException){badHash=true;}Check(badHash&&!File.Exists(Path.Combine(root,"bad.bin")),"reject checksum mismatch");
 var msg=Message.Of("key",new KeyEvent(65,true));var encoded=JsonSerializer.Serialize(msg,Wire.Json);var decoded=JsonSerializer.Deserialize<Message>(encoded,Wire.Json)!;Check(decoded.Read<KeyEvent>().Key==65,"protocol serialization");
 using var cert1=Identity.Load(root);using var cert2=Identity.Load(root);Check(Identity.Fingerprint(cert1)==Identity.Fingerprint(cert2)&&cert2.HasPrivateKey,"DPAPI persistent identity");
 Console.WriteLine($"{passed} passed; 0 failed");
}finally{Directory.Delete(root,true);}
