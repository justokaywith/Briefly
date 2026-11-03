using System;using System.Drawing;using System.IO;using System.Net;using System.Text;using System.Threading;using System.Web.Script.Serialization;using System.Windows.Forms;using System.Collections.Generic;
namespace Briefly {
 public partial class MainForm {
  void OpenAuthenticatedPortal(){ProcessPortal();}
  void ProcessPortal(){try{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(apiOrigin+"/api/sso/start"){UseShellExecute=true});}catch{BrieflyDialogs.Show(this,"Open https://briefly.pactuno.online/api/sso/start in your browser to continue with your Pactuno account.","Briefly portal");}}
  void EnableMeetingRename(){var menu=new ContextMenuStrip();menu.Items.Add("Rename meeting…",null,(s,e)=>RenameSelectedMeeting());lvCloudMeetings.ContextMenuStrip=menu;lvCloudMeetings.DoubleClick+=(s,e)=>RenameSelectedMeeting();lvCloudMeetings.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.F2){e.Handled=true;RenameSelectedMeeting();}};lvCloudMeetings.MouseDown+=(s,e)=>{if(e.Button==MouseButtons.Right){var item=lvCloudMeetings.GetItemAt(e.X,e.Y);if(item!=null)item.Selected=true;}};lblCloudDetailTitle.DoubleClick+=(s,e)=>RenameSelectedMeeting();lblCloudDetailTitle.Cursor=Cursors.Hand;lblCloudDetailTitle.AccessibleDescription="Double click to rename this meeting";}
  void RenameSelectedMeeting(){
   if(lvCloudMeetings.SelectedItems.Count!=1)return;var item=lvCloudMeetings.SelectedItems[0];var meeting=item.Tag as Dictionary<string,object>;if(meeting==null||!meeting.ContainsKey("id"))return;
   string id=Convert.ToString(meeting["id"]);using(var dialog=new DialogSurface("Rename meeting",new Size(600,360))){
    var fields=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};fields.RowStyles.Add(new RowStyle(SizeType.Absolute,28));fields.RowStyles.Add(new RowStyle(SizeType.Absolute,44));dialog.Body.Controls.Add(fields);
    var label=new Label{Text="Meeting title",Dock=DockStyle.Fill};var input=new TextBox{Text=item.Text,MaxLength=180,Dock=DockStyle.Fill,Font=new Font("Segoe UI",11)};fields.Controls.Add(label,0,0);fields.Controls.Add(input,0,1);var save=dialog.AddAction("Save title",()=>{});var cancel=dialog.AddAction("Cancel",()=>dialog.Close(),true);dialog.AcceptButton=save;dialog.CancelButton=cancel;dialog.Shown+=(s,e)=>{input.Focus();input.SelectAll();};
    save.Click+=(s,e)=>{string title=input.Text.Trim();if(title.Length==0){input.Focus();return;}save.Enabled=false;input.Enabled=false;
     ThreadPool.QueueUserWorkItem(_=>{try{var request=(HttpWebRequest)WebRequest.Create(apiOrigin+"/api/meetings/"+Uri.EscapeDataString(id));request.Method="PATCH";request.ContentType="application/json";request.Timeout=15000;SetAuthentication(request);byte[] bytes=Encoding.UTF8.GetBytes(new JavaScriptSerializer().Serialize(new{title=title}));using(var body=request.GetRequestStream())body.Write(bytes,0,bytes.Length);using(var response=request.GetResponse())using(var reader=new StreamReader(response.GetResponseStream())){var result=new JavaScriptSerializer().Deserialize<Dictionary<string,object>>(reader.ReadToEnd());if(result==null||!result.ContainsKey("ok")||!(bool)result["ok"])throw new IOException();}
      SafeUi(()=>{meeting["title"]=title;item.Text=title;lblCloudDetailTitle.Text=title;if(!dialog.IsDisposed)dialog.Close();});
     }catch{SafeUi(()=>{if(dialog.IsDisposed)return;label.Text="Could not save. Check your connection and retry.";save.Enabled=true;input.Enabled=true;});}});
    };dialog.ShowDialog(this);
   }
  }
 }
}
