using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Web.Script.Serialization;
namespace Briefly {
 public static class DesktopSpeech {
  public static void VerifyInstalled(string root) {
   if(!File.Exists(Path.Combine(root,"speech","whisper-cli.exe")))throw new InvalidOperationException("The local speech engine is missing. Install the complete Briefly package.");
   SpeechPreferences.Load().Model(root);
  }
  static string Quote(string value) { if(value.IndexOf('"')>=0)throw new ArgumentException("Invalid speech argument.");int trailing=0;for(int i=value.Length-1;i>=0&&value[i]=='\\';i--)trailing++;return "\""+value+new string('\\',trailing)+"\""; }
  public static string Arguments(string root,string wav,SpeechPreferences preferences){
   preferences.Validate();
   return "-m "+Quote(preferences.Model(root))+" -f "+Quote(wav)+" -l "+preferences.Language+" -oj -of "+Quote(wav+".transcript")+" -np";
  }
  public static string Transcribe(string root,string wav) {
   VerifyInstalled(root);var preferences=SpeechPreferences.Load();string model=preferences.Model(root),output=wav+".transcript";
   ProcessStartInfo start=new ProcessStartInfo();start.FileName=Path.Combine(root,"speech","whisper-cli.exe");
   start.Arguments=Arguments(root,wav,preferences);
   start.UseShellExecute=false;start.CreateNoWindow=true;
   using(Process worker=Process.Start(start)) {
    if(!worker.WaitForExit(1800000)){worker.Kill();throw new IOException("Transcription timed out. Your audio has been saved locally.");}
    if(worker.ExitCode!=0)throw new IOException("Transcription failed. Your audio has been saved locally.");
   }
   JavaScriptSerializer json=new JavaScriptSerializer();json.MaxJsonLength=8*1024*1024;
   var result=json.Deserialize<Dictionary<string,object>>(File.ReadAllText(output+".json"));
   StringBuilder text=new StringBuilder();
   if(result.ContainsKey("transcription"))foreach(object item in (IEnumerable)result["transcription"]) {
    var row=item as Dictionary<string,object>;if(row!=null&&row.ContainsKey("text"))text.Append(row["text"]).Append(' ');
   }
   string transcript=text.ToString().Trim();if(transcript.Length==0)throw new IOException("No speech was recognized. Your audio has been saved locally.");
   return transcript;
  }
 }
}
