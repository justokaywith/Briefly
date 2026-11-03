using System;using System.IO;using System.Reflection;using System.Windows.Forms;using System.Drawing;using Briefly;
class PopupConsistencyTest {
 [STAThread]static int Main(){Application.EnableVisualStyles();var surface=typeof(MainForm).Assembly.GetType("Briefly.DialogSurface");if(surface==null)throw new Exception("Popups have no shared Briefly visual surface");
  string root=Path.GetFullPath("outputs/briefly/installer");foreach(string f in new[]{"AppNative.cs","MeetingActions.cs","ReferenceLayout.cs","SpeechPreferences.cs","NativeSetup.cs","Uninstall.cs"})if(File.ReadAllText(Path.Combine(root,f)).Contains("MessageBox.Show"))throw new Exception("Unbranded system popup remains: "+f);
  var api=typeof(MainForm).Assembly.GetType("Briefly.BrieflyDialogs");if(api==null)throw new Exception("Missing common dialog API");
  var show=api.GetMethod("Show",new[]{typeof(string),typeof(string),typeof(MessageBoxButtons),typeof(MessageBoxIcon),typeof(MessageBoxDefaultButton)});
  using(var timer=new Timer{Interval=100}){timer.Tick+=(s,e)=>{foreach(Form dialog in Application.OpenForms){if(dialog.GetType()!=surface)continue;bool logo=false,negative=false;Inspect(dialog,ref logo,ref negative);if(!logo||!negative||dialog.CancelButton==null)throw new Exception("Brand/safe cancel missing");timer.Stop();dialog.Close();break;}};timer.Start();var result=(DialogResult)show.Invoke(null,new object[]{"Your recording is still active. Stop recording and prepare notes?","Briefly recording",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2});if(result!=DialogResult.No)throw new Exception("Closing confirmation must keep recording");}
  Console.WriteLine("All shipped popup call sites use the common branded surface; recording confirmation closes safely as No.");return 0;
 }
 static void Inspect(Control parent,ref bool logo,ref bool negative){foreach(Control c in parent.Controls){logo|=c is AnimatedLogo;negative|=c is Button&&c.Text=="Keep recording";Inspect(c,ref logo,ref negative);}}
}
