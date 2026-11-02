using System;using System.Drawing;using System.Reflection;using System.Windows.Forms;using Briefly;
class NativeLayoutTest {
 static BindingFlags flags=BindingFlags.NonPublic|BindingFlags.Instance;
 static T Field<T>(object form,string name){return (T)form.GetType().GetField(name,flags).GetValue(form);}
 [STAThread]static int Main(){Application.EnableVisualStyles();using(var form=new MainForm(false)){
  form.GetType().GetField("isAuthenticated",flags).SetValue(form,true);
  foreach(var size in new[]{new Size(800,600),new Size(1024,700),new Size(1440,900),new Size(1920,1040)}){
   form.ClientSize=size;form.GetType().GetMethod("SyncReferenceShell",flags).Invoke(form,new object[]{"record"});form.PerformLayout();var shell=Field<TableLayoutPanel>(form,"referenceShell");shell.PerformLayout();
   Panel sidebar=Field<Panel>(form,"referenceSidebar"),body=Field<Panel>(form,"pnlBody");if(sidebar.Right>body.Left)throw new Exception("Sidebar overlaps body at "+size);
   if(body.Right>shell.ClientSize.Width+1)throw new Exception("Body exceeds shell at "+size);
   foreach(string name in new[]{"pnlViewRecord","pnlViewCloud","pnlViewSettings"}){var page=Field<Panel>(form,name);if(!page.AutoScroll||page.AutoScrollMinSize.Width<=0)throw new Exception("Missing small-window scroll fallback");}
  }
 }
 using(var rail=new LiquidNavRail{Size=new Size(176,160)})using(var nav=new LiquidNavButton(rail){Text="Capture",Size=new Size(176,46)})using(var bitmap=new Bitmap(176,46)){
  rail.Controls.Add(nav);nav.DrawToBitmap(bitmap,new Rectangle(0,0,176,46));var color=bitmap.GetPixel(80,5);if(color.ToArgb()!=Color.FromArgb(15,42,46).ToArgb())throw new Exception("Selected nav must paint its own teal background");
  rail.Width=148;rail.PerformLayout();if(nav.Right>rail.ClientSize.Width)throw new Exception("Compact sidebar must not clip navigation");
  nav.Height=69;if(rail.Indicator.Height!=69)throw new Exception("Highlight must follow scaled button height");
 }
 RectangleF first=LiquidNavRail.BoundsAt(0,100,0,176),last=LiquidNavRail.BoundsAt(0,100,1,176),middle=LiquidNavRail.BoundsAt(0,100,.5f,176);if(first.Y!=0||last.Y!=100||middle.Height!=46||middle.Width>=first.Width)throw new Exception("Bounded bubble highlight interpolation failed");
 Console.WriteLine("Native layout: 800/1024/1440/1920 viewports, non-overlapping sidebar/body, scroll fallback, nav paint and fluid-highlight bounds passed.");return 0;}
}
