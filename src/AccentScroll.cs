using System;using System.Drawing;using System.Runtime.InteropServices;using System.Windows.Forms;
namespace Briefly {
 // Keep the native editable/selectable text surface; theme only its scroll affordance.
 public sealed class AccentScroll:Control {
  readonly TextBox text;bool dragging;int offset;
  [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr w,IntPtr l);
  int Total{get{return text.IsHandleCreated?Math.Max(1,SendMessage(text.Handle,0xBA,IntPtr.Zero,IntPtr.Zero).ToInt32()):1;}}
  int First{get{return text.IsHandleCreated?SendMessage(text.Handle,0xCE,IntPtr.Zero,IntPtr.Zero).ToInt32():0;}}
  int VisibleLines{get{return Math.Max(1,text.ClientSize.Height/Math.Max(1,text.Font.Height));}}
  int Maximum{get{return Math.Max(0,Total-VisibleLines);}}
  Rectangle Thumb{get{int h=Math.Min(Height,Math.Max(22,(int)((long)Height*VisibleLines/Total)));return new Rectangle(3,Maximum==0?0:(int)((long)(Height-h)*First/Maximum),6,h);}}
  AccentScroll(TextBox t){text=t;Width=14;Dock=DockStyle.Right;Cursor=Cursors.Hand;DoubleBuffered=true;TabStop=true;AccessibleName="Scroll meeting text";AccessibleRole=AccessibleRole.ScrollBar;
   t.TextChanged+=(s,e)=>RefreshState();t.Resize+=(s,e)=>RefreshState();t.KeyUp+=(s,e)=>Invalidate();t.MouseWheel+=(s,e)=>{MoveLines(-Math.Sign(e.Delta)*3);};
  }
  public static void Attach(TextBox text,Control parent){text.ScrollBars=ScrollBars.None;text.WordWrap=true;var bar=new AccentScroll(text){BackColor=parent.BackColor};parent.Controls.Add(bar);bar.BringToFront();bar.RefreshState();}
  void RefreshState(){if(IsDisposed)return;Visible=Maximum>0;Invalidate();}
  void MoveLines(int count){if(!text.IsHandleCreated)return;int target=Math.Max(0,Math.Min(Maximum,First+count));SendMessage(text.Handle,0xB6,IntPtr.Zero,new IntPtr(target-First));Invalidate();}
  protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);using(var track=new SolidBrush(Color.FromArgb(220,231,225)))e.Graphics.FillRectangle(track,5,0,2,Height);using(var shape=RoundedButton.Shape(Thumb,3))using(var ink=new SolidBrush(Color.FromArgb(29,106,88)))e.Graphics.FillPath(ink,shape);}
  protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);Focus();dragging=Thumb.Contains(e.Location);if(dragging){offset=e.Y-Thumb.Y;Capture=true;}else MoveLines(e.Y<Thumb.Top?-VisibleLines:VisibleLines);}
  protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;int span=Height-Thumb.Height;if(span>0){int desired=(int)((long)Math.Max(0,Math.Min(span,e.Y-offset))*Maximum/span);MoveLines(desired-First);}}
  protected override void OnMouseUp(MouseEventArgs e){dragging=false;Capture=false;base.OnMouseUp(e);}
  protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Down)MoveLines(1);else if(e.KeyCode==Keys.Up)MoveLines(-1);else if(e.KeyCode==Keys.PageDown)MoveLines(VisibleLines);else if(e.KeyCode==Keys.PageUp)MoveLines(-VisibleLines);else if(e.KeyCode==Keys.Home)MoveLines(-Total);else if(e.KeyCode==Keys.End)MoveLines(Total);else return;e.Handled=true;e.SuppressKeyPress=true;}
 }
}
