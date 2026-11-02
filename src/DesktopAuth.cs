using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Threading;
namespace Briefly {
 public static class DesktopAuth {
  const string Authority="https://pactuno.online";
  static readonly object Gate=new object();
  static string Access,Refresh;
  static DateTime Expires;
  static string Store { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Briefly","pactuno-device.dat"); } }
  static string Random(){byte[] bytes=new byte[32];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);return Base64(bytes);}
  static string Base64(byte[] bytes){return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+','-').Replace('/','_');}
  static string Hash(string text){using(var sha=SHA256.Create())return Base64(sha.ComputeHash(Encoding.ASCII.GetBytes(text)));}
  public static string SignIn(Action<string> progress) {
   return SignIn(progress,CancellationToken.None);
  }
  public static string SignIn(Action<string> progress,CancellationToken cancellation) {
   ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12;
   string verifier=Random(),state=Random();
   var socket=new TcpListener(IPAddress.Loopback,0);socket.Start();int port=((IPEndPoint)socket.LocalEndpoint).Port;socket.Stop();
   string redirect="http://127.0.0.1:"+port+"/callback";
   using(var listener=new HttpListener()) {
    listener.Prefixes.Add("http://127.0.0.1:"+port+"/");listener.Start();
    string url=Authority+"/api/ecosystem/authorize?client_id=briefly-desktop&redirect_uri="+Uri.EscapeDataString(redirect)+"&code_challenge="+Hash(verifier)+"&code_challenge_method=S256&state="+state;
    Process.Start(new ProcessStartInfo(url){UseShellExecute=true});progress("Waiting for sign-in in your browser...");
    DateTime deadline=DateTime.UtcNow.AddMinutes(5);
    while(DateTime.UtcNow<deadline) {
     cancellation.ThrowIfCancellationRequested();
     var pending=listener.BeginGetContext(null,null);
     int completed=WaitHandle.WaitAny(new[]{pending.AsyncWaitHandle,cancellation.WaitHandle},Math.Max(1,(int)(deadline-DateTime.UtcNow).TotalMilliseconds));
     if(completed==1){listener.Stop();cancellation.ThrowIfCancellationRequested();}
     if(completed==WaitHandle.WaitTimeout)throw new IOException("Sign-in timed out. Please try again.");
     var context=listener.EndGetContext(pending);var request=context.Request;
     if(request.Url.AbsolutePath!="/callback"||request.HttpMethod!="GET"||request.QueryString["state"]!=state||string.IsNullOrEmpty(request.QueryString["code"])) {context.Response.StatusCode=400;context.Response.Close();continue;}
     var tokens=Post("/api/ecosystem/token",new Dictionary<string,object>{{"client_id","briefly-desktop"},{"redirect_uri",redirect},{"code",request.QueryString["code"]},{"code_verifier",verifier}});
     lock(Gate){Save(tokens);}
     byte[] page=Encoding.UTF8.GetBytes("<!doctype html><html lang=\"en\"><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width,initial-scale=1\"><title>Return to Briefly</title><main style=\"font:16px/1.7 system-ui;max-width:520px;margin:10vh auto;padding:24px\"><h1>Browser sign-in complete.</h1><p>Return to the Briefly app to finish connecting your workspace. If the app asks you to link older meeting history, complete that step in the portal first.</p></main></html>");
     context.Response.ContentType="text/html; charset=utf-8";context.Response.Headers["Cache-Control"]="no-store";context.Response.Headers["Referrer-Policy"]="no-referrer";context.Response.OutputStream.Write(page,0,page.Length);context.Response.Close();
     return Access;
    }
    throw new IOException("Sign-in timed out. Please try again.");
   }
  }
  public static string AccessToken() {
   lock(Gate){
    if(Refresh==null&&File.Exists(Store)){
     var bytes=ProtectedData.Unprotect(File.ReadAllBytes(Store),null,DataProtectionScope.CurrentUser);
     Refresh=Encoding.UTF8.GetString(bytes);
    }
    if(Access!=null&&Expires>DateTime.UtcNow.AddSeconds(30))return Access;
    if(string.IsNullOrEmpty(Refresh))return null;
    Save(Post("/api/ecosystem/refresh",new Dictionary<string,object>{{"client_id","briefly-desktop"},{"refresh_token",Refresh}}));return Access;
   }
  }
  public static string SignedInEmail() {
   string access=AccessToken();if(string.IsNullOrEmpty(access))return null;
   var data=Post("/api/ecosystem/introspect",new Dictionary<string,object>{{"client_id","briefly-desktop"}},access);
   object user;var identity=data.TryGetValue("user",out user)?user as Dictionary<string,object>:null;
   return identity!=null&&identity.ContainsKey("email")?Convert.ToString(identity["email"]):null;
  }
  static void Save(Dictionary<string,object> tokens) {
   string access=Convert.ToString(tokens["access_token"]),refresh=Convert.ToString(tokens["refresh_token"]);
   if(!System.Text.RegularExpressions.Regex.IsMatch(access,"^[A-Za-z0-9_-]{43}$")||!System.Text.RegularExpressions.Regex.IsMatch(refresh,"^[A-Za-z0-9_-]{43}$"))throw new IOException("The sign-in response was invalid.");
   Directory.CreateDirectory(Path.GetDirectoryName(Store));
   string temporary=Store+".new";File.WriteAllBytes(temporary,ProtectedData.Protect(Encoding.UTF8.GetBytes(refresh),null,DataProtectionScope.CurrentUser));
   if(File.Exists(Store))File.Replace(temporary,Store,null);else File.Move(temporary,Store);
   Access=access;Refresh=refresh;Expires=DateTime.UtcNow.AddSeconds(900);
  }
  public static void SignOut() {
   lock(Gate){
    try{if(Refresh!=null)Post("/api/ecosystem/revoke",new Dictionary<string,object>{{"client_id","briefly-desktop"}},Refresh);}
    finally{Access=null;Refresh=null;Expires=DateTime.MinValue;if(File.Exists(Store))File.Delete(Store);}
   }
  }
  static Dictionary<string,object> Post(string route,Dictionary<string,object> body,string bearer=null) {
   var json=new JavaScriptSerializer();byte[] bytes=Encoding.UTF8.GetBytes(json.Serialize(body));
   var request=(HttpWebRequest)WebRequest.Create(Authority+route);request.Method="POST";request.ContentType="application/json";request.Timeout=20000;request.AllowAutoRedirect=false;
   if(bearer!=null)request.Headers["Authorization"]="Bearer "+bearer;
   using(var stream=request.GetRequestStream())stream.Write(bytes,0,bytes.Length);
   using(var response=(HttpWebResponse)request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream()))return json.Deserialize<Dictionary<string,object>>(reader.ReadToEnd());
  }
 }
}
