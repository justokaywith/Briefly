using System;using System.Drawing;using System.IO;using System.Reflection;using System.Windows.Forms;using Briefly;
class RecordingPresentationTest {
 static BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
 [STAThread]static int Main(){Application.EnableVisualStyles();using(var f=new MainForm(false)){
  var record=(Button)typeof(MainForm).GetField("btnRecord",flags).GetValue(f);
  record.Enabled=false;record.Text="Syncing notes...";record.BackColor=Color.FromArgb(15,42,46);
  typeof(MainForm).GetMethod("ResetRecordButton",flags).Invoke(f,null);
  if(!record.Enabled||record.ForeColor.ToArgb()==record.BackColor.ToArgb()||record.BackColor.ToArgb()!=Color.FromArgb(255,183,58).ToArgb()){
   // The brand Amber constant may change; require a visibly light ready-button surface.
   if(!record.Enabled||record.BackColor.GetBrightness()<.5f||record.ForeColor.GetBrightness()>.3f)throw new Exception("Record button remains dark/disabled");
  }
  var heading=(Label)typeof(MainForm).GetField("referenceCaptureTitle",flags).GetValue(f);if(heading.Text!="Ready to capture")throw new Exception("Stale preparing heading");
 }
 string root=Path.Combine(Path.GetTempPath(),"briefly-speech-args-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"speech"));
 try{File.WriteAllBytes(Path.Combine(root,"speech","ggml-small.bin"),new byte[]{0});string args=DesktopSpeech.Arguments(root,"C:\\test audio.wav",new SpeechPreferences{Vocabulary="Pactuno, Briefly"});if(args.Contains("prompt")||args.Contains("Pactuno")||args.Contains("Briefly"))throw new Exception("Legacy word list still biases recognition");}
 finally{File.Delete(Path.Combine(root,"speech","ggml-small.bin"));Directory.Delete(Path.Combine(root,"speech"));Directory.Delete(root);}
 Console.WriteLine("Ready button contrast/state, completion heading, and speech arguments without legacy brand hints pass.");return 0;
 }
}
