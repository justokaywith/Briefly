using System;
using System.Diagnostics;
using Microsoft.Win32;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using System.IO;
using System.Collections.Generic;
namespace Briefly {
 public sealed class MeetingSignal {
  public string Title,Platform,Key,AppKey,ObservedTitle,Source="none";
  public bool Reliable=true;
 }
 public sealed class LiveListenState {
  int present,absent;bool blocked,stopPrompted,watchEnd;
  string lastKey;
  DateTime? missingSince;
  readonly Func<DateTime> now;
  public LiveListenState():this(()=>DateTime.UtcNow){}
  public LiveListenState(Func<DateTime> clock){now=clock;}
  // Only a recording explicitly accepted from a detected-call prompt owns an end watch.
  // A manual recording is valid even when no supported call is detectable.
  public void RecordingStarted(bool fromDetectedCall){watchEnd=fromDetectedCall;missingSince=null;absent=0;stopPrompted=false;}
  public void ManualStop(){blocked=true;watchEnd=false;missingSince=null;stopPrompted=false;}
  public string Update(bool detected,bool recording,bool enabled,string key=null){
   if(!enabled){present=0;absent=0;blocked=false;watchEnd=false;missingSince=null;stopPrompted=false;return "none";}
   if(detected&&!recording&&key!=null&&key!=lastKey){present=0;blocked=false;lastKey=key;}
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
  [DllImport("kernel32.dll",CharSet=CharSet.Unicode)]static extern int GetPackageFamilyName(IntPtr process,ref uint length,StringBuilder family);
  // Registry timestamps are a signal, not proof of a meeting. The caller must
  // resolve a live process first; a crashed application's stale row is ignored.
  public static MeetingSignal FromMicrophoneUse(string app,string title,long start,long stop,bool running,string self){
   if(!running||start<=0||stop!=0||string.IsNullOrWhiteSpace(app)||string.Equals(app,self,StringComparison.OrdinalIgnoreCase))return null;
   string name=app.StartsWith("package:",StringComparison.Ordinal)?app.Substring(8):Path.GetFileNameWithoutExtension(app);
   if(string.Equals(name,"Briefly",StringComparison.OrdinalIgnoreCase))return null;
   string platform=PlatformFor(name,title)??"Unknown",key=app.ToLowerInvariant();
   bool callTitle=LooksLikeCall(name.ToLowerInvariant(),title);
   return new MeetingSignal{AppKey=key,Key=key,Platform=platform,Source="microphone",ObservedTitle=callTitle?title:null,Title=callTitle?title:"Microphone active in "+name};
  }
  static bool ResolveApp(string app,out string title,out string resolved,ref bool reliable){
   title="";resolved=app;bool packaged=app.StartsWith("package:",StringComparison.Ordinal);
   Process[] candidates=packaged?Process.GetProcesses():Process.GetProcessesByName(Path.GetFileNameWithoutExtension(app));
   bool matched=false;
   foreach(var process in candidates)using(process){
    try{
     if(process.Id==Process.GetCurrentProcess().Id)continue;
     string identity;
     if(packaged){uint length=0;int status=GetPackageFamilyName(process.Handle,ref length,null);if(status!=122||length==0||length>1024)continue;var family=new StringBuilder((int)length);if(GetPackageFamilyName(process.Handle,ref length,family)!=0)continue;identity="package:"+family;}
     else identity=process.MainModule.FileName;
     if(!string.Equals(identity,app,StringComparison.OrdinalIgnoreCase))continue;
     // Use one executable identity across packaged mic records and UIA fallback.
     try{resolved=process.MainModule.FileName;}catch{if(!packaged)reliable=false;}
     matched=true;string candidate=process.MainWindowTitle;
     if(string.IsNullOrWhiteSpace(title)||LooksLikeCall(process.ProcessName.ToLowerInvariant(),candidate))title=candidate;
    }catch{reliable=false;}
   }
   return matched;
  }
  public static MeetingSignal DetectMicrophone(Func<string,bool> ignored=null){
   bool reliable=true;MeetingSignal found=null;int visited=0;
   try{
    using(var root=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\CapabilityAccessManager\ConsentStore\microphone")){
     if(root==null)return new MeetingSignal{Reliable=false,Source="microphone-unavailable"};
     ReadMicrophoneKeys(root,"",0,ref visited,ref reliable,ref found,ignored);
    }
   }catch{reliable=false;}
   return found??new MeetingSignal{Reliable=reliable,Source=reliable?"none":"microphone-unavailable"};
  }
  static void ReadMicrophoneKeys(RegistryKey parent,string relative,int depth,ref int visited,ref bool reliable,ref MeetingSignal found,Func<string,bool> ignored){
   if(depth>3||visited>512){reliable=false;return;}
   foreach(string keyName in parent.GetSubKeyNames()){
    if(++visited>512){reliable=false;return;}
    try{using(var key=parent.OpenSubKey(keyName)){
     if(key==null){reliable=false;continue;}
     object begin=key.GetValue("LastUsedTimeStart"),end=key.GetValue("LastUsedTimeStop");
     if(begin!=null&&end!=null&&Convert.ToInt64(begin)>0&&Convert.ToInt64(end)==0){
      string app=relative=="NonPackaged"?keyName.Replace('#','\\'):relative==""&&keyName!="NonPackaged"?"package:"+keyName:null;
      if(app!=null&&(ignored==null||!ignored(app))){string title,resolved;if(ResolveApp(app,out title,out resolved,ref reliable)&&(ignored==null||!ignored(resolved))){
       var signal=FromMicrophoneUse(resolved,title,Convert.ToInt64(begin),Convert.ToInt64(end),true,ApplicationPath());
       // Prefer an observed call title over generic microphone activity.
       if(signal!=null&&(found==null||signal.ObservedTitle!=null))found=signal;
      }}
     }
     ReadMicrophoneKeys(key,relative.Length==0?keyName:relative+"/"+keyName,depth+1,ref visited,ref reliable,ref found,ignored);
    }}catch{reliable=false;}
   }
  }
  static string ApplicationPath(){return Process.GetCurrentProcess().MainModule.FileName;}
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
  public static MeetingSignal DetectDetailed(Func<string,bool> ignored=null){
   var micSignal=DetectMicrophone(ignored);if(micSignal.Title!=null)return micSignal;
   MeetingSignal found=null;bool reliable=micSignal.Reliable;
   EnumWindows((window,unused)=>{
    if(!IsWindowVisible(window))return true;
    uint pid;GetWindowThreadProcessId(window,out pid);if(pid==Process.GetCurrentProcess().Id)return true;
    try{using(var process=Process.GetProcessById((int)pid)){
     var text=new StringBuilder(1024);GetWindowText(window,text,text.Capacity);string title=text.ToString(),name=process.ProcessName,platform=PlatformFor(name,title);
     if(platform==null)return true;
     try{if(ignored!=null&&ignored(process.MainModule.FileName))return true;}catch{}
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
     if(end&&(microphone||explicitHangup)){string app;try{app=process.MainModule.FileName.ToLowerInvariant();}catch{app="process:"+pid;}found=new MeetingSignal{Title=title,ObservedTitle=title,Platform=platform,AppKey=app,Key=app,Source="call-controls"};return false;}
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
