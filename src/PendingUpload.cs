using System;using System.IO;using System.Web.Script.Serialization;using System.Collections.Generic;
namespace Briefly {
 public static class PendingUpload {
  public static void Save(string file,string owner,string payload){
   if(string.IsNullOrWhiteSpace(owner))throw new IOException("Sign in before saving a meeting for sync.");
   var json=new JavaScriptSerializer();json.MaxJsonLength=1024*1024;
   var parsed=json.Deserialize<Dictionary<string,object>>(payload);
   if(parsed==null||!parsed.ContainsKey("requestId"))throw new IOException("This recording has no upload identifier.");
   string text=json.Serialize(new Dictionary<string,object>{{"ownerId",owner},{"payload",parsed}}),temporary=file+".new";
   File.WriteAllText(temporary,text);if(File.Exists(file))File.Replace(temporary,file,null);else File.Move(temporary,file);
  }
  public static string ReadOwned(string file,string owner){
   if(string.IsNullOrWhiteSpace(owner)||new FileInfo(file).Length>1024*1024)throw new IOException("This saved meeting cannot be uploaded.");
   var json=new JavaScriptSerializer();json.MaxJsonLength=1024*1024;
   var data=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(file));
   if(data==null||!data.ContainsKey("ownerId")||!string.Equals(Convert.ToString(data["ownerId"]),owner,StringComparison.Ordinal)||!data.ContainsKey("payload"))throw new IOException("This saved meeting belongs to another profile or needs manual recovery.");
   return json.Serialize(data["payload"]);
  }
 }
}
