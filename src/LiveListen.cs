using System;
using System.Diagnostics;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
namespace Briefly {
 public sealed class MeetingSignal {
  public string Title,Platform,Key;
  public bool Reliable=true;
 }
 public sealed class LiveListenState {
  int present,absent;bool blocked,stopPrompted,watchEnd;
  DateTime? missingSince;
  readonly Func<DateTime> now;
  public LiveListenState():this(()=>DateTime.UtcNow){}
  public LiveListenState(Func<DateTime> clock){now=clock;}
  // Only a recording explicitly accepted from a detected-call prompt owns an end watch.
  // A manual recording is valid even when no supported call is detectable.
  public void RecordingStarted(bool fromDetectedCall){watchEnd=fromDetectedCall;missingSince=null;absent=0;stopPrompted=false;}
  public void ManualStop(){blocked=true;watchEnd=false;missingSince=null;stopPrompted=false;}
  public string Update(bool detected,bool recording,bool enabled){
   if(!enabled){present=0;absent=0;blocked=false;watchEnd=false;missingSince=null;stopPrompted=false;return "none";}
   if(detected){present++;absent=0;missingSince=null;}else{absent++;present=0;if(absent>=5&&!recording){blocked=false;stopPrompted=false;}if(recording&&watchEnd&&!missingSince.HasValue)missingSince=now();}
   if(!recording&&detected&&present>=2&&!blocked){blocked=true;return "confirm-start";}
   if(recording&&watchEnd&&!detected&&missingSince.HasValue&&(now()-missingSince.Value).TotalSeconds>=24&&!stopPrompted){stopPrompted=true;return "confirm-stop";}
   return "none";
  }
  // A failed/incomplete detector read is not evidence that a call ended.
  public void DetectionUnavailable(){present=0;absent=0;missingSince=null;}
 }
 public static class MeetingSignals {
  delegate bool WindowVisitor(IntPtr window,IntPtr parameter);
  [DllImport("user32.dll")]static extern bool EnumWindows(WindowVisitor visitor,IntPtr parameter);
  [DllImport("user32.dll")]static extern bool IsWindowVisible(IntPtr window);
  [DllImport("user32.dll")]static extern bool IsIconic(IntPtr window);
  [DllImport("user32.dll")]static extern int GetWindowText(IntPtr window,StringBuilder text,int count);
  [DllImport("user32.dll")]static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
  public static string PlatformFor(string process,string title){
   string p=(process??"").ToLowerInvariant(),t=(title??"").ToLowerInvariant();
   if(p.Contains("teams")||t.Contains("microsoft teams"))return "Microsoft Teams";
   if(t.Contains("meet.google.com")||t.Contains("meet - ")||t.Contains("google meet"))return "Google Meet";
   if(p.Contains("zoom")||t.Contains("zoom meeting"))return "Zoom";
   if(p.Contains("webex")||t.Contains("webex meeting"))return "Webex";
   return null;
  }
  public static bool IsEndCallControl(string name,string automationId){
   string n=(name??"").Trim().ToLowerInvariant(),id=(automationId??"").ToLowerInvariant();
   return n=="leave"||n=="leave call"||n=="leave meeting"||n=="hang up"||n=="end call"||n.StartsWith("leave (ctrl+")||id=="hangup-button"||id=="call-hangup";
  }
  // Inspect only candidate app windows, never the whole desktop tree. No audio is read.
  // Active-call controls allow a silent, muted solo call to be detected too.
  public static MeetingSignal DetectDetailed(){
   MeetingSignal found=null;bool reliable=true;
   EnumWindows((window,unused)=>{
    if(!IsWindowVisible(window))return true;
    uint pid;GetWindowThreadProcessId(window,out pid);if(pid==Process.GetCurrentProcess().Id)return true;
    try{using(var process=Process.GetProcessById((int)pid)){
     var text=new StringBuilder(1024);GetWindowText(window,text,text.Capacity);string title=text.ToString(),name=process.ProcessName,platform=PlatformFor(name,title);
     if(platform==null)return true;
     if(IsIconic(window)){reliable=false;return true;}
     var root=AutomationElement.FromHandle(window);
     var buttons=root.FindAll(TreeScope.Descendants,new PropertyCondition(AutomationElement.ControlTypeProperty,ControlType.Button));
     bool end=false,microphone=false,explicitHangup=false;
     for(int i=0;i<Math.Min(buttons.Count,256);i++){
      var b=buttons[i].Current;
      if(!b.IsEnabled||b.IsOffscreen)continue;
      string label=(b.Name??"").ToLowerInvariant();
      if(IsEndCallControl(b.Name,b.AutomationId)){end=true;if(label.Contains("hang")||label.Contains("call")||(b.AutomationId??"").ToLowerInvariant().Contains("hangup"))explicitHangup=true;}
      if(label.Contains("microphone")||label.Contains("mic ")||label=="mute"||label=="unmute"||label.StartsWith("mute (")||label.StartsWith("unmute ("))microphone=true;
     }
     if(end&&(microphone||explicitHangup)){found=new MeetingSignal{Title=title,Platform=platform,Key=pid+":"+window.ToInt64()};return false;}
    }}catch{reliable=false;}
    return true;
   },IntPtr.Zero);
   if(found!=null)return found;
   return new MeetingSignal{Reliable=reliable};
  }
  public static string Detect(){
   using(var enumerator=new MMDeviceEnumerator())
   using(var endpoint=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,Role.Multimedia)){
    var sessions=endpoint.AudioSessionManager.Sessions;
    for(int i=0;i<sessions.Count;i++){
     var session=sessions[i];
     if(session.State!=AudioSessionState.AudioSessionStateActive||session.GetProcessID==Process.GetCurrentProcess().Id)continue;
     try{using(var process=Process.GetProcessById((int)session.GetProcessID)){
      string name=process.ProcessName.ToLowerInvariant(),title=process.MainWindowTitle;
      // Browser audio sessions can run in a renderer process with no window title.
      if(string.IsNullOrWhiteSpace(title))foreach(var window in Process.GetProcessesByName(process.ProcessName)){try{if(!string.IsNullOrWhiteSpace(window.MainWindowTitle)&&LooksLikeCall(name,window.MainWindowTitle)){title=window.MainWindowTitle;break;}}finally{window.Dispose();}}
      if(LooksLikeCall(name,title)&&MicrophoneActive(name))return title;
     }}catch{}
    }
   }
   return null;
  }
  public static bool LooksLikeCall(string process,string title){
   string t=(title??"").ToLowerInvariant();
   if(t.Contains("chat |")||t.Contains("calendar |")||t.Contains("settings |"))return false;
   return t.Contains("meet.google.com")||t.Contains("meet - ")||t.Contains("google meet")||t.Contains("zoom meeting")||t.Contains("webex meeting")||t.Contains("huddle")||t.Contains("video call")||t.Contains("voice call")||(t.Contains("meeting")&&(process.Contains("teams")||process.Contains("zoom")||process.Contains("webex")));
  }
  static bool MicrophoneActive(string process){
   const string root="Software\\Microsoft\\Windows\\CurrentVersion\\CapabilityAccessManager\\ConsentStore\\microphone";
   using(var key=Registry.CurrentUser.OpenSubKey(root)){if(key==null)return false;return ActiveUnder(key,process,0);}
  }
  static bool ActiveUnder(RegistryKey key,string process,int depth){
   if(depth>3)return false;
   foreach(string name in key.GetSubKeyNames())using(var child=key.OpenSubKey(name)){
    if(child==null)continue;
    object begin=child.GetValue("LastUsedTimeStart"),end=child.GetValue("LastUsedTimeStop");
    if(name.ToLowerInvariant().Contains(process)&&begin!=null&&end!=null&&Convert.ToInt64(begin)>0&&Convert.ToInt64(end)==0)return true;
    if(ActiveUnder(child,process,depth+1))return true;
   }
   return false;
  }
 }
}
