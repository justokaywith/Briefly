using System;using System.Drawing;using System.Reflection;using System.Threading;using System.Windows.Forms;using Briefly;
class NativeMotionStabilityTest {
 [STAThread]static int Main(){Application.EnableVisualStyles();using(var rail=new LiquidNavRail{Size=new Size(176,160)}){
 var flags=BindingFlags.NonPublic|BindingFlags.Instance;rail.GetType().GetField("moving",flags).SetValue(rail,true);
 rail.GetType().GetField("to",flags).SetValue(rail,100f);((System.Diagnostics.Stopwatch)rail.GetType().GetField("elapsed",flags).GetValue(rail)).Restart();
 var before=rail.Indicator;Thread.Sleep(80);var after=rail.Indicator;if(before!=after)throw new Exception("Painting must use one cached animation frame, never a separate wall-clock read per child");
 var middle=LiquidNavRail.BoundsAt(0,100,.5f,176);if(middle.Height!=46)throw new Exception("Navigation must not vertically bounce");if(middle.Width>=174)throw new Exception("Bubble should still contract while moving");
 for(int i=0;i<=100;i++){var b=LiquidNavRail.BoundsAt(0,100,i/100f,176);if(b.Y<0||b.Y>100||b.Width<46||b.Width>174)throw new Exception("Motion overshot safe bounds");}
 }Console.WriteLine("Cached-frame motion, no vertical bounce, bounded bubble geometry passed.");return 0;}
}
