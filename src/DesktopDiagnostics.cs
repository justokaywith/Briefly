using System;using System.IO;using System.Collections.Generic;using System.Web.Script.Serialization;using System.Text.RegularExpressions;
namespace Briefly {
 // Fixed event codes only: never log titles, transcripts, audio, tokens, email or paths.
 public static class DesktopDiagnostics {
  static readonly object Gate=new object();static readonly Queue<string> Recent=new Queue<string>();
  public static string Detection="Not checked",Microphone="Not checked",Capture="Idle",LastError="None";
  public static string Store{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Briefly","diagnostics.jsonl");}}
  public static void Record(string step,string source,string code){
   if(!Regex.IsMatch(step??"","^[a-z-]{1,40}$")||!Regex.IsMatch(source??"","^[a-z-]{1,40}$")||!Regex.IsMatch(code??"","^[a-z-]{1,60}$"))return;
   lock(Gate){try{string row=new JavaScriptSerializer().Serialize(new{at=DateTime.UtcNow.ToString("o"),step=step,source=source,code=code});Recent.Enqueue(row);while(Recent.Count>24)Recent.Dequeue();Directory.CreateDirectory(Path.GetDirectoryName(Store));if(File.Exists(Store)&&new FileInfo(Store).Length>65536){string old=Store+".previous";File.Copy(Store,old,true);File.WriteAllText(Store,"");}File.AppendAllText(Store,row+Environment.NewLine);}catch{}}
  }
  public static string Snapshot(){lock(Gate){return "Detection: "+Detection+"\r\nMicrophone-use evidence: "+Microphone+"\r\nCapture: "+Capture+"\r\nLast error: "+LastError+"\r\n\r\nRecent steps (no meeting content):\r\n"+string.Join("\r\n",Recent.ToArray());}}
 }
}
