using System;using System.Diagnostics;using System.Threading;using System.Windows.Forms;using Briefly;
class MeetingDetectionTest {
 [STAThread]static int Main(string[] args){
  if(args.Length>0){using(var f=new Form{Text="Fixture | Microsoft Teams",Width=400,Height=200}){
   f.Controls.Add(new Button{Text=args[0]=="active"?"Leave":"Join now",Top=20,Left=20});
   f.Controls.Add(new Button{Text="Mute",Top=70,Left=20});Application.Run(f);return 0;
  }}
  foreach(string mode in new[]{"prejoin","active"}){
   using(var child=Process.Start(new ProcessStartInfo(Application.ExecutablePath,mode){UseShellExecute=false})){
    try{for(int i=0;i<40;i++){child.Refresh();if(child.MainWindowHandle!=IntPtr.Zero)break;Thread.Sleep(200);}Thread.Sleep(500);var signal=MeetingSignals.DetectDetailed();
     if(mode=="active"&&(signal.Platform!="Microsoft Teams"||signal.Title!="Fixture | Microsoft Teams"))throw new Exception("Silent solo-call controls not detected");
     if(mode=="prejoin"&&signal.Title=="Fixture | Microsoft Teams")throw new Exception("Prejoin wrongly detected as active");
    }finally{child.CloseMainWindow();if(!child.WaitForExit(3000))child.Kill();}
   }
  }
  if(MeetingSignals.IsEndCallControl("Leave a review",""))throw new Exception("Generic leave link accepted");
  Console.WriteLine("Real Windows UIA fixture: silent call detected without audio; prejoin excluded. Real Teams acceptance still required.");return 0;
 }
}
