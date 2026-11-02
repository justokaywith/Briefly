using System;using System.IO;using System.Reflection;using Briefly;
class ListenPreferencesTest {
 static int Main(){
  var type=typeof(MeetingSignals).Assembly.GetType("Briefly.ListenPreferences");if(type==null)throw new Exception("Live Listen preferences are not persisted");
  string dir=Path.Combine(Path.GetTempPath(),"briefly-listen-test-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);string path=Path.Combine(dir,"listen.json");
  try{
   var prefs=Activator.CreateInstance(type);type.GetField("Enabled").SetValue(prefs,true);
   type.GetMethod("Ignore").Invoke(prefs,new object[]{@"C:\Apps\Chrome\chrome.exe"});type.GetMethod("SaveTo").Invoke(prefs,new object[]{path});
   var restored=type.GetMethod("LoadFrom").Invoke(null,new object[]{path});
   if(!(bool)type.GetField("Enabled").GetValue(restored))throw new Exception("Toggle was lost after restart");
   if(!(bool)type.GetMethod("IsIgnored").Invoke(restored,new object[]{@"c:\apps\chrome\CHROME.exe"}))throw new Exception("Ignored app was lost");
   if((bool)type.GetMethod("IsIgnored").Invoke(restored,new object[]{@"C:\Apps\Edge\msedge.exe"}))throw new Exception("Ignore leaked to another app");
   File.WriteAllText(path,"broken");var fallback=type.GetMethod("LoadFrom").Invoke(null,new object[]{path});if((bool)type.GetField("Enabled").GetValue(fallback))throw new Exception("Corrupt preferences must not enable detection");
   Console.WriteLine("Live Listen toggle/app-specific ignore persistence and corrupt-file opt-out pass; no owner settings changed.");return 0;
  }finally{foreach(string f in Directory.GetFiles(dir))File.Delete(f);Directory.Delete(dir);}
 }
}
