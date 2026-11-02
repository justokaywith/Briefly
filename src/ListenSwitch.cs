using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;using System.Diagnostics;
namespace Briefly {
 public sealed class ListenSwitch:CheckBox {
  readonly Timer motion=new Timer{Interval=16};readonly Stopwatch elapsed=new Stopwatch();bool disposed;
  public ListenSwitch(){SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);Cursor=Cursors.Hand;AccessibleRole=AccessibleRole.CheckButton;motion.Tick+=(s,e)=>{if(elapsed.ElapsedMilliseconds>=160||!AnimatedLogo.MotionAllowed())motion.Stop();Invalidate();};}
  protected override void OnCheckedChanged(EventArgs e){base.OnCheckedChanged(e);if(disposed)return;elapsed.Restart();if(AnimatedLogo.MotionAllowed())motion.Start();Invalidate();}
  protected override void OnPaint(PaintEventArgs e){var g=e.Graphics;g.Clear(Parent==null?BackColor:Parent.BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;float t=motion.Enabled?Math.Min(1,elapsed.ElapsedMilliseconds/160f):1,amount=PageMotion.Ease(t),pos=Checked?amount:1-amount;float jiggle=motion.Enabled?(float)Math.Sin(t*Math.PI*4)*(1-t)*1.5f:0;var track=new RectangleF(Width-45,(Height-24)/2f,44,24);using(var p=RoundedButton.Shape(track,12))using(var brush=new SolidBrush(Checked?Color.FromArgb(24,106,87):Color.FromArgb(165,178,171)))g.FillPath(brush,p);using(var b=new SolidBrush(Color.White))g.FillEllipse(b,track.X+3+20*pos+jiggle,track.Y+3,18,18);using(var format=new StringFormat{LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter})using(var ink=new SolidBrush(ForeColor))g.DrawString(Text,Font,ink,new RectangleF(0,0,Math.Max(1,Width-52),Height),format);if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,ClientRectangle);}
  protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;motion.Dispose();}base.Dispose(disposing);}
 }
}
