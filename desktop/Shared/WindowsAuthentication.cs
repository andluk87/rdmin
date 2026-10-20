using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Authentication.ExtendedProtection;
using System.Security.Principal;

namespace RemoteAdmin.Shared;
public sealed record WindowsAccessRule(string Sid,string Name,Permissions Allow,Permissions Deny=Permissions.None,bool Enabled=true);
public static class WindowsAuthentication {
 public static WindowsAccessRule Resolve(string name,Permissions allow,Permissions deny=Permissions.None){
  if(string.IsNullOrWhiteSpace(name)||name.Length>256)throw new ArgumentException("Укажите DOMAIN\\user, SERVER\\user или группу.");
  var sid=(SecurityIdentifier)new NTAccount(name.Trim()).Translate(typeof(SecurityIdentifier));
  var canonical=(NTAccount)sid.Translate(typeof(NTAccount));return new(sid.Value,canonical.Value,allow,deny);
 }
 public static Permissions Rights(WindowsIdentity identity,IEnumerable<WindowsAccessRule> rules){
  if(!identity.IsAuthenticated||identity.IsAnonymous||identity.User==null)return Permissions.None;
  var principal=new WindowsPrincipal(identity);var memberships=new HashSet<string>(identity.Groups?.Select(x=>x.Value)??[],StringComparer.OrdinalIgnoreCase){identity.User.Value};Permissions allowed=Permissions.None,denied=Permissions.None;
  foreach(var rule in rules.Where(x=>x.Enabled)){
   // Deny-only token groups must still match deny ACEs, never allow ACEs.
   if(memberships.Contains(rule.Sid))denied|=rule.Deny;
   if(identity.User.Value==rule.Sid||principal.IsInRole(new SecurityIdentifier(rule.Sid)))allowed|=rule.Allow;
  }
  var result=allowed&~denied;if(!result.HasFlag(Permissions.View))result&=~Permissions.Control;return result;
 }
 public static NetworkCredential Credential(string username,string password){
  int slash=username.IndexOf('\\');return slash>0?new NetworkCredential(username[(slash+1)..],password,username[..slash]):new NetworkCredential(username,password);
 }
 public static async Task<NegotiateStream> Server(SslStream tls,CancellationToken ct){
  var channel=new NegotiateStream(tls,true);
  try{
   using var binding=tls.TransportContext?.GetChannelBinding(ChannelBindingKind.Endpoint)??throw new AuthenticationException("TLS channel binding недоступен для Windows-входа.");
   var policy=new ExtendedProtectionPolicy(PolicyEnforcement.Always,binding);
   await channel.AuthenticateAsServerAsync(CredentialCache.DefaultNetworkCredentials,policy,ProtectionLevel.EncryptAndSign,TokenImpersonationLevel.Impersonation).WaitAsync(TimeSpan.FromSeconds(30),ct);
   if(!channel.IsAuthenticated||!channel.IsEncrypted||!channel.IsSigned||channel.RemoteIdentity is not WindowsIdentity)throw new AuthenticationException("Windows SSPI не подтвердил идентичность.");return channel;
  }catch{channel.Dispose();throw;}
 }
 public static async Task<NegotiateStream> Client(SslStream tls,string host,NetworkCredential credential,CancellationToken ct){
  var channel=new NegotiateStream(tls,true);
  try{
   using var binding=tls.TransportContext?.GetChannelBinding(ChannelBindingKind.Endpoint)??throw new AuthenticationException("TLS channel binding недоступен для Windows-входа.");
   await channel.AuthenticateAsClientAsync(credential,binding,"HOST/"+host.Trim('[',']'),ProtectionLevel.EncryptAndSign,TokenImpersonationLevel.Impersonation).WaitAsync(TimeSpan.FromSeconds(30),ct);
   if(!channel.IsAuthenticated||!channel.IsEncrypted||!channel.IsSigned)throw new AuthenticationException("Не удалось создать защищённый Windows-канал.");return channel;
  }catch{channel.Dispose();throw;}
 }
}
