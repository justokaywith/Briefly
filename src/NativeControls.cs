using System;using System.Drawing;using System.Drawing.Drawing2D;using System.Windows.Forms;
namespace Briefly {
 public class AnimatedLogo:Control {
  readonly Timer clock=new Timer();readonly System.Diagnostics.Stopwatch elapsed=System.Diagnostics.Stopwatch.StartNew();Form host;bool logoDisposed;
  [System.Runtime.InteropServices.DllImport("user32.dll",SetLastError=true)]
  static extern bool SystemParametersInfo(uint action,uint parameter,[System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]out bool value,uint flags);
  public AnimatedLogo(){
   SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.SupportsTransparentBackColor,true);
   BackColor=Color.Transparent;TabStop=false;AccessibleName="Briefly";AccessibleRole=AccessibleRole.Graphic;
   clock.Interval=33;clock.Tick+=(s,e)=>{if(!MotionAllowed()){clock.Stop();Invalidate();}else Invalidate();};
  }
  internal static bool MotionAllowed(){bool allowed;return !SystemInformation.HighContrast&&SystemParametersInfo(0x1042,0,out allowed,0)&&allowed;}
  void UpdateMotion(){if(logoDisposed)return;if(IsHandleCreated&&Visible&&!IsDisposed&&(host==null||host.WindowState!=FormWindowState.Minimized)&&MotionAllowed())clock.Start();else clock.Stop();Invalidate();}
  void HostChanged(object sender,EventArgs e){UpdateMotion();}
  void AttachHost(){if(host!=null){host.Resize-=HostChanged;host.VisibleChanged-=HostChanged;}host=FindForm();if(host!=null){host.Resize+=HostChanged;host.VisibleChanged+=HostChanged;}}
  protected override void OnParentChanged(EventArgs e){base.OnParentChanged(e);AttachHost();UpdateMotion();}
  protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);UpdateMotion();}
  protected override void OnHandleCreated(EventArgs e){base.OnHandleCreated(e);AttachHost();UpdateMotion();}
  protected override void OnHandleDestroyed(EventArgs e){if(!logoDisposed)clock.Stop();base.OnHandleDestroyed(e);}
  protected override void Dispose(bool disposing){if(disposing){logoDisposed=true;clock.Dispose();if(host!=null){host.Resize-=HostChanged;host.VisibleChanged-=HostChanged;}}base.Dispose(disposing);}
  static float Pulse(double seconds,double period,double delay,float low,float high){double phase=Math.Max(0,seconds-delay)%period/period;return low+(high-low)*(float)(phase<.5?phase*2:(1-phase)*2);}
  static void Block(Graphics g,float x,float y,float width,float height,float radius,Color color){using(var p=RoundedButton.Shape(new RectangleF(x,y,width,height),Math.Min(radius,Math.Min(width,height)/2)))using(var b=new SolidBrush(color))g.FillPath(b,p);}
  public static void Render(Graphics g,int width,int height,double seconds,bool animate){
   if(width<1||height<1)return;var state=g.Save();try{
    g.SmoothingMode=SmoothingMode.AntiAlias;g.ScaleTransform(width/64f,height/64f);
    Block(g,0,0,64,64,16,Color.FromArgb(15,42,46));
    float h1=animate?Pulse(seconds,1.8,0,12,22):16,h2=animate?Pulse(seconds,1.8,0,32,18):32,h3=animate?Pulse(seconds,1.8,0,16,28):22;
    Color cream=Color.FromArgb(246,241,231),amber=Color.FromArgb(255,181,71);
    Block(g,12,32-h1/2,5,h1,2.5f,cream);Block(g,21,32-h2/2,5,h2,2.5f,cream);Block(g,30,32-h3/2,5,h3,2.5f,cream);
    Block(g,39,22,animate?Pulse(seconds,2.4,0,4,14):14,4,2,amber);Block(g,39,30,animate?Pulse(seconds,2.4,.3,2,10):10,4,2,amber);Block(g,39,38,animate?Pulse(seconds,2.4,.6,3,12):12,4,2,amber);
   }finally{g.Restore(state);}
  }
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Render(e.Graphics,Width,Height,elapsed.Elapsed.TotalSeconds,clock.Enabled);}
 }
 public class RoundedButton:Button {
  bool hover,pressed;float shade,targetShade;readonly Timer motion=new Timer{Interval=16};bool buttonDisposed;public bool Outlined {get;set;}public float CornerRadius {get;set;}
  public RoundedButton(){CornerRadius=12;SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;Cursor=Cursors.Hand;motion.Tick+=(s,e)=>{shade+=(targetShade-shade)*.25f;if(Math.Abs(targetShade-shade)<.2f||!AnimatedLogo.MotionAllowed()){shade=targetShade;motion.Stop();}Invalidate();};}
  void AnimateShade(){targetShade=pressed?25:hover?12:0;if(AnimatedLogo.MotionAllowed())motion.Start();else{shade=targetShade;Invalidate();}}
  internal static GraphicsPath Shape(RectangleF r,float radius){var p=new GraphicsPath();float d=radius*2;p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;}
  protected override void OnMouseEnter(EventArgs e){hover=true;AnimateShade();base.OnMouseEnter(e);}protected override void OnMouseLeave(EventArgs e){hover=false;pressed=false;AnimateShade();base.OnMouseLeave(e);}
  protected override void OnMouseDown(MouseEventArgs e){pressed=true;AnimateShade();base.OnMouseDown(e);}protected override void OnMouseUp(MouseEventArgs e){pressed=false;AnimateShade();base.OnMouseUp(e);}
  protected override void OnGotFocus(EventArgs e){Invalidate();base.OnGotFocus(e);}protected override void OnLostFocus(EventArgs e){Invalidate();base.OnLostFocus(e);}
  protected override void OnPaint(PaintEventArgs e){
   var g=e.Graphics;g.TextRenderingHint=System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;g.SmoothingMode=SmoothingMode.AntiAlias;g.Clear(Parent==null?SystemColors.Control:Parent.BackColor);
   var rect=new RectangleF(1,1,Width-3,Height-3);using(var shape=Shape(rect,Math.Min(CornerRadius,(Height-3)/2))){
    using(var b=new SolidBrush(Enabled?BackColor:Color.FromArgb(232,228,219)))g.FillPath(b,shape);
    if(Enabled&&shade>0)using(var overlay=new SolidBrush(Color.FromArgb((int)shade,Outlined?Color.Black:Color.White)))g.FillPath(overlay,shape);
    if(Outlined)using(var pen=new Pen(Color.FromArgb(150,120,138,140),1))g.DrawPath(pen,shape);
   }
   using(var format=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter})using(var text=new SolidBrush(Enabled?ForeColor:Color.FromArgb(110,120,120)))g.DrawString(Text,Font,text,ClientRectangle,format);
   if(Focused&&ShowFocusCues){var focus=ClientRectangle;focus.Inflate(-6,-6);ControlPaint.DrawFocusRectangle(g,focus,ForeColor,BackColor);}
  }
  protected override void OnVisibleChanged(EventArgs e){base.OnVisibleChanged(e);if(!buttonDisposed&&!Visible){motion.Stop();shade=0;hover=false;pressed=false;}}
  protected override void Dispose(bool disposing){if(disposing&&!buttonDisposed){buttonDisposed=true;motion.Dispose();}base.Dispose(disposing);}
 }
 public class RoundedPanel:Panel {
  public RoundedPanel(){DoubleBuffered=true;}
  protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(Width<42||Height<42)return;using(var p=RoundedButton.Shape(new RectangleF(0,0,Width,Height),20)){var old=Region;Region=new Region(p);if(old!=null)old.Dispose();}}
 }
}
