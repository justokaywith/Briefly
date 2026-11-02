using System;
using Briefly;
class LiveListenTest {
 static void Equal(string actual,string expected){if(actual!=expected)throw new Exception(actual+" != "+expected);}
 static int Main(){try{
  DateTime clock=new DateTime(2026,11,2,0,0,0,DateTimeKind.Utc);var state=new LiveListenState(()=>clock);Equal(state.Update(true,false,false),"none");
  Equal(state.Update(true,false,true),"none");Equal(state.Update(true,false,true),"confirm-start");Equal(state.Update(true,false,true),"none");Equal(state.Update(true,true,true),"none");
  state.ManualStop();Equal(state.Update(true,false,true),"none");
  for(int i=0;i<5;i++)Equal(state.Update(false,false,true),"none");
  Equal(state.Update(true,false,true),"none");Equal(state.Update(true,false,true),"confirm-start");
  state.RecordingStarted(false);for(int i=0;i<50;i++){clock=clock.AddSeconds(3);Equal(state.Update(false,true,true),"none");}
  state.RecordingStarted(true);Equal(state.Update(false,true,true),"none");clock=clock.AddSeconds(23);Equal(state.Update(false,true,true),"none");clock=clock.AddSeconds(1);Equal(state.Update(false,true,true),"confirm-stop");Equal(state.Update(false,true,true),"none");
  Equal(state.Update(true,true,true),"none");clock=clock.AddSeconds(50);Equal(state.Update(false,true,true),"none");clock=clock.AddSeconds(50);Equal(state.Update(false,true,true),"none");
  state.RecordingStarted(true);Equal(state.Update(false,true,true),"none");state.DetectionUnavailable();clock=clock.AddSeconds(60);Equal(state.Update(false,true,true),"none");
  Equal(state.Update(false,true,false),"none");clock=clock.AddSeconds(60);Equal(state.Update(false,true,true),"none");
  if(MeetingSignals.LooksLikeCall("ms-teams","Chat | Microsoft Teams"))throw new Exception("Chat incorrectly treated as meeting");
  if(!MeetingSignals.LooksLikeCall("chrome","Meet - abc-defg-hij"))throw new Exception("Meet title not recognized");
  Console.WriteLine("Live Listen debounce, manual-stop suppression, stop confirmation and call-title checks passed.");return 0;
 }catch(Exception e){Console.Error.WriteLine(e.Message);return 1;}}
}
