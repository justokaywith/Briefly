using System;using System.IO;using System.Diagnostics;using System.Drawing;using System.Reflection;using System.Threading.Tasks;using System.Windows.Forms;using Microsoft.Win32;using Briefly;
namespace BrieflyInstaller {
 static class SetupProgram {
  [STAThread]static void Main(){bool created;using(var single=new System.Threading.Mutex(true,@"Local\Briefly-Setup",out created)){if(!created){BrieflyDialogs.Show("Briefly setup is already open. Finish that update first.","Briefly setup");return;}Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);Application.Run(new SetupWindow());}}
 }
 public class SetupWindow:Form {
  readonly Label status;readonly ProgressBar progress;readonly RoundedButton install,finish;readonly CheckBox shortcut,openAfter;readonly System.Windows.Forms.Timer welcomeClock=new System.Windows.Forms.Timer();
  bool busy,installed,launchAfterClose;readonly string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Briefly");
  public SetupWindow(){
   bool updating=File.Exists(Path.Combine(target,"Briefly.exe"));
   Text="Briefly 1.3.7 Setup Preview";Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);AutoScaleMode=AutoScaleMode.Dpi;ClientSize=new Size(620,580);MinimumSize=new Size(560,480);StartPosition=FormStartPosition.CenterScreen;BackColor=Color.FromArgb(251,248,242);Font=new Font("Segoe UI",10);ForeColor=Color.FromArgb(15,42,46);AutoScroll=true;
   var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(28),ColumnCount=1,RowCount=8};layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
   layout.RowStyles.Add(new RowStyle(SizeType.Absolute,70));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,58));layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,32));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,48));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,20));layout.RowStyles.Add(new RowStyle(SizeType.Absolute,62));Controls.Add(layout);
   layout.Controls.Add(new AnimatedLogo{Size=new Size(60,60)},0,0);
   layout.Controls.Add(new Label{Text="A little more clarity.",Font=new Font("Segoe UI",24,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,1);
   layout.Controls.Add(new Label{Text=(updating?"Your existing Briefly installation will be updated.":"Install Briefly for this Windows account.")+"\n\nYour sign-in, speech settings and recordings stay in your Windows user profile. Cloud meeting history stays in your account. Previous program files are kept as a backup.\n\nDevelopment preview 1.3.7 — supported-call and audio-device testing continues.\nInstall location: "+target,AutoSize=false,Dock=DockStyle.Fill},0,2);
   shortcut=new CheckBox{Text="Create a desktop shortcut",Checked=true,Dock=DockStyle.Fill,AccessibleName="Create a desktop shortcut"};layout.Controls.Add(shortcut,0,3);
   openAfter=new CheckBox{Text="Open Briefly when setup finishes",Checked=true,Dock=DockStyle.Fill};layout.Controls.Add(openAfter,0,4);
   status=new Label{Text="Setup closes automatically after installation. For an update, first finish your recording and close the Briefly app.",Dock=DockStyle.Fill};layout.Controls.Add(status,0,5);
   progress=new ProgressBar{Dock=DockStyle.Fill,Minimum=0,Maximum=100};layout.Controls.Add(progress,0,6);
   var actions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.RightToLeft,Padding=new Padding(0,12,0,0)};layout.Controls.Add(actions,0,7);
   install=new RoundedButton{Text=updating?"Update Briefly":"Install Briefly",Width=160,Height=44,BackColor=Color.FromArgb(15,42,46),ForeColor=Color.White};install.Click+=Install;actions.Controls.Add(install);
   finish=new RoundedButton{Text="Cancel",Width=120,Height=44,Outlined=true,BackColor=BackColor,ForeColor=ForeColor};finish.Click+=(s,e)=>Close();actions.Controls.Add(finish);
   var welcome=new Panel{Dock=DockStyle.Fill,BackColor=Color.FromArgb(15,42,46)};var center=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=5,BackColor=welcome.BackColor,ForeColor=Color.FromArgb(246,241,231)};center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));center.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));center.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));center.RowStyles.Add(new RowStyle(SizeType.Percent,50));center.RowStyles.Add(new RowStyle(SizeType.Absolute,120));center.RowStyles.Add(new RowStyle(SizeType.Absolute,48));center.RowStyles.Add(new RowStyle(SizeType.Absolute,32));center.RowStyles.Add(new RowStyle(SizeType.Percent,50));center.Controls.Add(new AnimatedLogo{Size=new Size(96,96),Anchor=AnchorStyles.None},1,1);center.Controls.Add(new Label{Text="Briefly",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Segoe UI",26,FontStyle.Bold)},1,2);center.Controls.Add(new Label{Text=updating?"Preparing your update":"Preparing setup",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter},1,3);welcome.Controls.Add(center);Controls.Add(welcome);welcome.BringToFront();layout.Visible=false;
   welcomeClock.Interval=AnimatedLogo.MotionAllowed()?1200:1;welcomeClock.Tick+=(s,e)=>{welcomeClock.Stop();welcome.Dispose();layout.Visible=true;layout.BringToFront();install.Focus();};Shown+=(s,e)=>{var area=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(560,area.Width-24),Math.Min(480,area.Height-24));Width=Math.Min(Width,area.Width-24);Height=Math.Min(Height,area.Height-24);layout.MinimumSize=new Size(0,520);welcomeClock.Start();};
   FormClosing+=(s,e)=>{if(busy)e.Cancel=true;};FormClosed+=(s,e)=>{welcomeClock.Dispose();if(launchAfterClose)try{Process.Start(new ProcessStartInfo(Path.Combine(target,"Briefly.exe")){UseShellExecute=true,WorkingDirectory=target});}catch(Exception error){BrieflyDialogs.Show("Briefly was installed, but could not open: "+error.Message,"Briefly");}};
  }
  static bool Running(){foreach(var process in Process.GetProcessesByName("Briefly")){using(process){if(!process.HasExited)return true;}}return false;}
  async void Install(object sender,EventArgs e){
   if(busy||installed)return;busy=true;install.Enabled=false;finish.Enabled=false;shortcut.Enabled=false;openAfter.Enabled=false;bool createShortcut=shortcut.Checked,launch=openAfter.Checked;
   try{
    string backup=await Task.Run(()=>{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("BrieflyPayload.zip")){if(stream==null)throw new IOException("Installation package is missing.");return SafeInstall.Install(stream,target,Running,(p,message)=>BeginInvoke(new Action(()=>{progress.Value=p;status.Text=message;})));}});
    if(createShortcut){try{CreateShortcut();}catch{status.Text="Installed. Desktop shortcut could not be created; open Briefly.exe from the install folder.";}}
    using(var key=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Briefly")){key.SetValue("DisplayName","Briefly");key.SetValue("DisplayVersion","1.3.7");key.SetValue("Publisher","Pactuno");key.SetValue("InstallLocation",target);key.SetValue("DisplayIcon",Path.Combine(target,"briefly-1.3.7.ico"));key.SetValue("UninstallString","\""+Path.Combine(target,"Uninstall.exe")+"\"");key.SetValue("NoModify",1);key.SetValue("NoRepair",1);}
    installed=true;progress.Value=100;status.Text="Preview installed. "+(backup==null?"":"Previous installation preserved at "+backup);install.Text="Installed";finish.Text="Finish";
   }catch(Exception ex){status.Text=ex.Message;install.Enabled=true;shortcut.Enabled=true;}
   finally{busy=false;finish.Enabled=true;}
   if(installed)FinishInstallation(launch);else openAfter.Enabled=true;
  }
  void FinishInstallation(bool openApp){launchAfterClose=openApp;Close();}
  void CreateShortcut(){
   Type shellType=Type.GetTypeFromProgID("WScript.Shell");if(shellType==null)throw new IOException("Windows shortcuts are unavailable.");
   dynamic shell=Activator.CreateInstance(shellType),link=shell.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Briefly.lnk"));
   link.TargetPath=Path.Combine(target,"Briefly.exe");link.WorkingDirectory=target;link.IconLocation=Path.Combine(target,"briefly-1.3.7.ico");link.Save();
  }
 }
}
