using System;using System.Reflection;using Briefly;
class MicrophoneActivityTest {
 static MethodInfo method;
 static MeetingSignal Signal(string app,string title,long start=1,long stop=0,bool running=true){return (MeetingSignal)method.Invoke(null,new object[]{app,title,start,stop,running,@"C:\Apps\Briefly\Briefly.exe"});}
 static void Require(bool value,string message){if(!value)throw new Exception(message);}
 static int Main(){
  method=typeof(MeetingSignals).GetMethod("FromMicrophoneUse",BindingFlags.Public|BindingFlags.Static);
  Require(method!=null,"Missing primary microphone-use detection path");
  Require(Signal(@"C:\Apps\Chrome\chrome.exe","Inbox")!=null,"Mic activity in a browser must not require a visible meeting tab");
  Require(Signal(@"C:\Apps\Zoom\Zoom.exe","")!=null,"Minimized app mic activity must remain detectable");
  Require(Signal(@"C:\Apps\Chrome\chrome.exe","Meet - abc-defg-hij").Platform=="Google Meet","Observed Meet title should enrich mic evidence");
  Require(Signal(@"C:\Apps\Chrome\chrome.exe","Inbox").Platform=="Unknown","Unrelated browser title must not invent a meeting platform");
  Require(Signal(@"C:\Apps\Briefly\Briefly.exe","")==null,"Own capture must not detect itself as a meeting");
  Require(Signal(@"C:\Apps\Chrome\chrome.exe","",0)==null,"No start time is not active use");
  Require(Signal(@"C:\Apps\Chrome\chrome.exe","",1,2)==null,"Stopped microphone history is not current activity");
  Require(Signal(@"C:\Apps\Chrome\chrome.exe","",1,0,false)==null,"Crashed/stale registry row must not be active without its process");
  Require(Signal(@"C:\Apps\Chrome\chrome.exe","First tab").Key==Signal(@"c:\apps\chrome\CHROME.exe","Other tab").Key,"Changing tab titles must not change app identity");
  Console.WriteLine("Primary mic activity: generic browser/minimized app, own-process/stale/stopped exclusion, platform evidence and stable identity pass.");return 0;
 }
}
