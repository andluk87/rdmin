using Message = RemoteAdmin.Shared.Message;
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
 var pairing=PairingLink.Parse(PairingLink.Create("machine.example",4899,new string('A',64),new string('B',32)));
 Check(pairing.Host=="machine.example"&&pairing.Port==4899&&pairing.Code==new string('B',32),"connection invitation preserves fields");
 foreach(var invalid in new[]{"https://example.com", "remoteadmin://connect?host=test", "remoteadmin://connect?host=a&host=b"}){
  bool rejected=false;try{PairingLink.Parse(invalid);}catch(FormatException){rejected=true;}Check(rejected,"reject malformed invitation");
 }
 var access=new ServerAccounts(root);
 Check(access.AllowsAccounts&&!access.AllowsInvitations,"default authentication is server accounts");
 var operator1=access.Put(null,"operator","Strong-test-password-123",Permissions.View|Permissions.Chat,false,true);
 Check(access.Authenticate("OPERATOR","Strong-test-password-123")?.Id==operator1.Id,"account login accepts correct password and canonical login");
 Check(access.Authenticate("operator","wrong-password")==null&&access.Authenticate("missing","Strong-test-password-123")==null,"wrong and unknown credentials rejected");
 access.SaveSettings(5399,AuthenticationMode.Both,Permissions.View|Permissions.Chat);
 var reopened=new ServerAccounts(root);
 Check(reopened.Settings.Port==5399&&reopened.Authenticate("operator","Strong-test-password-123")?.RequireApproval==false,"account settings and explicit unattended policy survive reopen");
 reopened.Put(operator1.Id,"operator",null,Permissions.View,true,false);
 Check(reopened.Authenticate("operator","Strong-test-password-123")==null,"disabled account rejected without deleting verifier");
 var limiter=new LoginLimiter();var now=DateTimeOffset.UtcNow;for(int i=0;i<5;i++)Check(limiter.Allow("192.0.2.1",now),"bounded auth attempt accepted");Check(!limiter.Allow("192.0.2.1",now)&&limiter.Allow("192.0.2.1",now.AddMinutes(2)),"rate limit expires");
 var trust=new ServerTrust(root);trust.Remember("server",4899,new string('A',64));bool changedKey=false;try{trust.Remember("server",4899,new string('B',64));}catch(InvalidOperationException){changedKey=true;}Check(changedKey&&new ServerTrust(root).Get("SERVER",4899)==new string('A',64),"trusted server key cannot silently change");
 Console.WriteLine($"{passed} passed; 0 failed");
}finally{Directory.Delete(root,true);}
