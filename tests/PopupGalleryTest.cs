using System;using System.IO;using System.Drawing;using System.Windows.Forms;using Briefly;
class PopupGalleryTest {
 [STAThread]static int Main(){Application.EnableVisualStyles();string dir=Path.GetFullPath("outputs/briefly/work/popup-redesign-137");Directory.CreateDirectory(dir);
  string[] titles={"Live Listen","Briefly recording","Sign Out","Reset ignored apps","Uninstall Briefly","Update check","Sync unavailable","Briefly setup"};
  for(int index=0;index<titles.Length;index++){
   string title=titles[index];bool confirm=index<5;bool inspected=false;
   using(var timer=new Timer{Interval=120}){timer.Tick+=(s,e)=>{foreach(Form form in Application.OpenForms){var dialog=form as DialogSurface;if(dialog==null)continue;Check(dialog);using(var image=new Bitmap(dialog.Width,dialog.Height)){dialog.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size));image.Save(Path.Combine(dir,"popup-"+index+".png"));}inspected=true;timer.Stop();dialog.Close();break;}};timer.Start();var result=BrieflyDialogs.Show(index==6?new string('W',300)+"\nTry again when your connection returns.":"This is a fictional UI test. Your recording and saved meeting history stay unchanged.",title,confirm?MessageBoxButtons.YesNo:MessageBoxButtons.OK,MessageBoxIcon.Information,MessageBoxDefaultButton.Button2);if(result!=(confirm?DialogResult.No:DialogResult.OK)||!inspected)throw new Exception("Popup safe-close behavior failed");}
  }
  using(var dialog=new DialogSurface("Large window fit",new Size(2400,1800))){dialog.SetMessage("Fictional layout test only.");dialog.AddAction("Close",()=>dialog.Close());dialog.Show();Application.DoEvents();Check(dialog);dialog.Close();}
  Console.WriteLine("Eight branded popup types, long-message wrapping, safe close, button bounds and working-area fit pass; screenshots saved.");return 0;
 }
 static void Check(DialogSurface dialog){var area=Screen.FromControl(dialog).WorkingArea;if(dialog.Right>area.Right||dialog.Bottom>area.Bottom||dialog.Left<area.Left||dialog.Top<area.Top)throw new Exception("Popup outside screen");foreach(Control b in dialog.Actions.Controls)if(b.Right>dialog.Actions.ClientSize.Width+1||b.Bottom>dialog.Actions.ClientSize.Height+1)throw new Exception("Popup action clipped");if(dialog.Body.BackColor!=DialogSurface.Paper)throw new Exception("Popup does not use Briefly paper colour");}
}
