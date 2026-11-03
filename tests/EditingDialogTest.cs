using System;using System.IO;using System.Drawing;using System.Reflection;using System.Windows.Forms;using System.Collections.Generic;using Briefly;
class EditingDialogTest{
 [STAThread]static int Main(){Application.EnableVisualStyles();var flags=BindingFlags.Instance|BindingFlags.NonPublic;string dir=Path.GetFullPath("outputs/briefly/work/popup-redesign-137");Directory.CreateDirectory(dir);
  using(var owner=new MainForm(false)){
   owner.Show();typeof(MainForm).GetField("currentUser",flags).SetValue(owner,new Dictionary<string,object>{{"id","fixture"},{"username","sample"},{"email","sample@example.test"},{"firstName","Alex"}});typeof(MainForm).GetMethod("OnAuthSuccess",flags).Invoke(owner,null);typeof(MainForm).GetMethod("SwitchTab",flags).Invoke(owner,new object[]{"cloud"});Application.DoEvents();
   var list=(ListView)typeof(MainForm).GetField("lvCloudMeetings",flags).GetValue(owner);var item=new ListViewItem("Fictional project meeting"){Tag=new Dictionary<string,object>{{"id","fixture_meeting"},{"title","Fictional project meeting"}}};list.Items.Add(item);item.Selected=true;Application.DoEvents();
   foreach(string name in new[]{"speech","rename","diagnostics"}){
    bool found=false;using(var timer=new Timer{Interval=120}){timer.Tick+=(s,e)=>{foreach(Form f in Application.OpenForms){var d=f as DialogSurface;if(d==null)continue;foreach(Control b in d.Actions.Controls)if(b.Right>d.Actions.ClientSize.Width+1||b.Bottom>d.Actions.ClientSize.Height+1)throw new Exception("Editing action clipped");using(var image=new Bitmap(d.Width,d.Height)){d.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,name+".png"));}found=true;timer.Stop();d.Close();break;}};timer.Start();if(name=="speech")SpeechPreferences.ShowDialog(owner);else typeof(MainForm).GetMethod(name=="rename"?"RenameSelectedMeeting":"ShowDiagnostics",flags).Invoke(owner,null);if(!found)throw new Exception("Missing editing popup "+name);}
   }owner.Close();
  }Console.WriteLine("Speech, rename and Diagnostics use the shared branded surface with fitting actions. No profile/speech/meeting change submitted.");return 0;
 }
}
