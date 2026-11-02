using System;using System.IO;using System.Collections.Generic;using System.Web.Script.Serialization;
namespace Briefly {
 public sealed class ListenPreferences {
  public bool Enabled=false;
  public List<string> IgnoredApps=new List<string>();
  static readonly object Gate=new object();
  public static string Store{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Briefly","listen-preferences.json");}}
  static bool Valid(string key){return !string.IsNullOrWhiteSpace(key)&&key.Length<=2048&&key.IndexOf('\r')<0&&key.IndexOf('\n')<0&&key.IndexOf('\0')<0;}
  public bool IsIgnored(string key){return Valid(key)&&IgnoredApps.Exists(v=>string.Equals(v,key,StringComparison.OrdinalIgnoreCase));}
  public void Ignore(string key){if(!Valid(key))throw new ArgumentException("Invalid app identity.");if(!IsIgnored(key)){if(IgnoredApps.Count>=128)throw new IOException("The ignored-app list is full. Reset it in settings first.");IgnoredApps.Add(key.ToLowerInvariant());}}
  public static ListenPreferences LoadFrom(string path){
   lock(Gate){try{if(!File.Exists(path))return new ListenPreferences();if(new FileInfo(path).Length>131072)throw new IOException();var p=new JavaScriptSerializer().Deserialize<ListenPreferences>(File.ReadAllText(path));if(p==null||p.IgnoredApps==null||p.IgnoredApps.Count>128||p.IgnoredApps.Exists(v=>!Valid(v)))throw new IOException();return p;}catch{DesktopDiagnostics.Record("preferences","local","invalid-file");return new ListenPreferences();}}
  }
  public void SaveTo(string path){
   lock(Gate){if(IgnoredApps==null||IgnoredApps.Count>128||IgnoredApps.Exists(v=>!Valid(v)))throw new ArgumentException("Invalid ignored apps.");string data=new JavaScriptSerializer().Serialize(this);if(System.Text.Encoding.UTF8.GetByteCount(data)>131072)throw new IOException("Ignored-app settings are too large.");Directory.CreateDirectory(Path.GetDirectoryName(path));string temporary=path+".new";File.WriteAllText(temporary,data);if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);}
  }
 }
}
