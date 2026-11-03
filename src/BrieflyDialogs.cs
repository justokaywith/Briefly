using System;using System.Drawing;using System.Windows.Forms;
namespace Briefly {
 // One native visual surface for alerts, confirmations and editing dialogs.
 public sealed class DialogSurface:Form {
  public static readonly Color Paper=Color.FromArgb(250,249,246),Ink=Color.FromArgb(15,42,46),Cream=Color.FromArgb(246,241,231);
  public readonly Panel Body;
  public readonly FlowLayoutPanel Actions;
  public DialogSurface(string title,Size size){
   Text="Briefly - "+title;ClientSize=size;AutoScaleMode=AutoScaleMode.Dpi;AutoScaleDimensions=new SizeF(96,96);MinimumSize=new Size(340,280);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;ShowInTaskbar=false;BackColor=Paper;ForeColor=Ink;Font=new Font("Segoe UI",10);
   try{Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);}catch{}
   var grid=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Margin=new Padding(0),Padding=new Padding(0)};grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,72));grid.RowStyles.Add(new RowStyle(SizeType.Absolute,64));grid.RowStyles.Add(new RowStyle(SizeType.Percent,100));grid.RowStyles.Add(new RowStyle(SizeType.AutoSize));Controls.Add(grid);
   var header=new Panel{Dock=DockStyle.Fill,BackColor=Ink,Margin=new Padding(0)};header.Controls.Add(new AnimatedLogo{Location=new Point(24,16),Size=new Size(40,40)});header.Controls.Add(new Label{Text="Briefly",Location=new Point(78,18),Size=new Size(240,38),Font=new Font("Segoe UI",18,FontStyle.Bold),ForeColor=Cream,TextAlign=ContentAlignment.MiddleLeft});grid.Controls.Add(header,0,0);
   grid.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,Padding=new Padding(24,8,24,0),Margin=new Padding(0),Font=new Font("Segoe UI",20,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true},0,1);
   Body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(24,8,24,4),AutoScroll=true,Margin=new Padding(0)};grid.Controls.Add(Body,0,2);
   Actions=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=true,FlowDirection=FlowDirection.LeftToRight,Padding=new Padding(24,16,12,16),Margin=new Padding(0),MinimumSize=new Size(0,84)};grid.Controls.Add(Actions,0,3);
   Shown+=(s,e)=>{var area=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(340,area.Width-32),Math.Min(280,area.Height-32));Width=Math.Min(Width,area.Width-32);Height=Math.Min(Height,area.Height-32);Left=Math.Max(area.Left+16,Math.Min(Left,area.Right-Width-16));Top=Math.Max(area.Top+16,Math.Min(Top,area.Bottom-Height-16));};
  }
  public Label SetMessage(string message){var label=new Label{Text=message,AutoSize=true,Dock=DockStyle.Top,Font=new Font("Segoe UI",11),ForeColor=Color.FromArgb(53,75,78),Padding=new Padding(0,0,0,12)};Body.Controls.Add(label);EventHandler fit=(s,e)=>{label.MaximumSize=new Size(Math.Max(120,Body.ClientSize.Width-Body.Padding.Horizontal-16),0);};Body.Resize+=fit;fit(this,EventArgs.Empty);return label;}
  public RoundedButton AddAction(string text,Action action,bool secondary=false,bool danger=false){
   int width=Math.Max(108,Math.Min(250,TextRenderer.MeasureText(text,new Font("Segoe UI",10,FontStyle.Bold)).Width+36));
   var button=new RoundedButton{Text=text,Size=new Size(width,44),Margin=new Padding(0,0,12,8),BackColor=danger?Color.FromArgb(179,38,30):secondary?Paper:Ink,ForeColor=secondary?Ink:Cream,Outlined=secondary,Font=new Font("Segoe UI",10,FontStyle.Bold)};button.Click+=(s,e)=>action();Actions.Controls.Add(button);return button;
  }
 }
 public static class BrieflyDialogs {
  public static DialogResult Show(string text,string caption="Briefly",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.Information,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1){return Show(null,text,caption,buttons,icon,defaultButton);}
  public static DialogResult Show(IWin32Window owner,string text,string caption="Briefly",MessageBoxButtons buttons=MessageBoxButtons.OK,MessageBoxIcon icon=MessageBoxIcon.Information,MessageBoxDefaultButton defaultButton=MessageBoxDefaultButton.Button1){
   bool confirm=buttons==MessageBoxButtons.YesNo;DialogResult result=confirm?DialogResult.No:DialogResult.OK;
   int height=TextRenderer.MeasureText(text,new Font("Segoe UI",11),new Size(520,1000),TextFormatFlags.WordBreak).Height;
   using(var dialog=new DialogSurface(caption,new Size(600,Math.Min(620,Math.Max(340,height+240))))){
    dialog.SetMessage(text);
    if(confirm){
     string yes="Confirm",no="Cancel",c=caption.ToLowerInvariant();
     if(c.Contains("sign out")){yes="Sign out";no="Stay signed in";}
     else if(c.Contains("live listen")){yes="Enable Live Listen";no="Not now";}
     else if(c.Contains("recording")){yes="Stop recording";no="Keep recording";}
     else if(c.Contains("uninstall")){yes="Uninstall Briefly";no="Keep Briefly";}
     else if(c.Contains("ignored")){yes="Reset ignored apps";}
     dialog.AddAction(yes,()=>{result=DialogResult.Yes;dialog.Close();},false,c.Contains("uninstall"));
     var cancel=dialog.AddAction(no,()=>dialog.Close(),true);dialog.CancelButton=cancel;dialog.AcceptButton=cancel;dialog.Shown+=(s,e)=>cancel.Focus();
    }else{var close=dialog.AddAction("OK",()=>dialog.Close());dialog.AcceptButton=close;dialog.CancelButton=close;}
    var control=owner as Control;if(control!=null&&control.IsDisposed)owner=null;
    dialog.ShowDialog(owner??Form.ActiveForm);
   }return result;
  }
 }
}
