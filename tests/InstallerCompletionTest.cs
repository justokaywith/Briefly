using System;using System.IO;using System.Reflection;using System.Threading;using System.Windows.Forms;using BrieflyInstaller;
class InstallerCompletionTest{
 [STAThread]static int Main(string[] args){var flags=BindingFlags.Instance|BindingFlags.NonPublic;string isolated=Path.GetFullPath(args[0]);if(!isolated.StartsWith(Path.GetFullPath("outputs/briefly/work")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe fixture path");string marker=Path.Combine(isolated,"launch.marker");if(File.Exists(marker))File.Delete(marker);Application.EnableVisualStyles();
  using(var form=new SetupWindow()){
   typeof(SetupWindow).GetField("target",flags).SetValue(form,isolated);form.Show();for(int i=0;i<30;i++){Application.DoEvents();Thread.Sleep(50);}
   ((CheckBox)typeof(SetupWindow).GetField("openAfter",flags).GetValue(form)).Checked=false;typeof(SetupWindow).GetField("installed",flags).SetValue(form,true);
   ((Button)typeof(SetupWindow).GetField("finish",flags).GetValue(form)).PerformClick();for(int i=0;i<15;i++){Application.DoEvents();Thread.Sleep(50);}if(form.Visible)throw new Exception("Finished installer did not close");if(File.Exists(marker))throw new Exception("Unchecked Open Briefly was ignored; Finish still launched the app");
  }
  foreach(bool open in new[]{false,true})using(var form=new SetupWindow()){
   if(File.Exists(marker))File.Delete(marker);typeof(SetupWindow).GetField("target",flags).SetValue(form,isolated);form.Show();Application.DoEvents();
   typeof(SetupWindow).GetMethod("FinishInstallation",flags).Invoke(form,new object[]{open});for(int i=0;i<20;i++){Application.DoEvents();Thread.Sleep(50);}if(form.Visible)throw new Exception("Successful setup did not close automatically");if(File.Exists(marker)!=open)throw new Exception("Post-close launch does not match selected option");
  }
  Console.WriteLine("Installer Finish closes without launching when Open Briefly is unchecked; isolated fake app only, no install/registry/profile changes.");return 0;
 }
}
