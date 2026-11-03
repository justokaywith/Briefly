using System;using System.IO;using System.Collections.Generic;using System.Web.Script.Serialization;using System.Drawing;using System.Windows.Forms;
namespace Briefly {
 public class SpeechPreferences {
  // Vocabulary retained only for older settings files; it is not passed to recognition.
  public string Language="auto",Quality="small",Vocabulary="";
  static string Store{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),"Briefly","speech-preferences.json");}}
  public void Validate(){if(!new HashSet<string>{"auto","en","ur","hi","ar","es","fr","de"}.Contains(Language)||!(Quality=="small"||Quality=="tiny"))throw new ArgumentException("Choose a supported language and quality.");if(Vocabulary==null||Vocabulary.Length>600||Vocabulary.IndexOf('"')>=0)throw new ArgumentException("Use up to 600 characters, without double quotes.");foreach(char c in Vocabulary)if(char.IsControl(c))throw new ArgumentException("Vocabulary must be a single line.");}
  public static SpeechPreferences Load(){try{if(!File.Exists(Store))return new SpeechPreferences();if(new FileInfo(Store).Length>8192)throw new IOException();var p=new JavaScriptSerializer().Deserialize<SpeechPreferences>(File.ReadAllText(Store));p.Validate();return p;}catch{return new SpeechPreferences();}}
  public void Save(){Validate();Directory.CreateDirectory(Path.GetDirectoryName(Store));string temp=Store+".new";File.WriteAllText(temp,new JavaScriptSerializer().Serialize(this));if(File.Exists(Store))File.Replace(temp,Store,null);else File.Move(temp,Store);}
  public string Model(string root){Validate();string file=Path.Combine(root,"speech","ggml-"+Quality+".bin");if(!File.Exists(file))throw new IOException("The selected "+Quality+" speech model is missing. Install the updated complete package, or select Fast in Speech settings.");return file;}
  public static void ShowDialog(IWin32Window owner){
   var settings=Load();using(var form=new DialogSurface("Speech recognition",new Size(600,520))){
    var layout=new TableLayoutPanel{Dock=DockStyle.Fill,Padding=new Padding(0),ColumnCount=1,RowCount=5};
    foreach(int height in new[]{26,44,26,44})layout.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
    layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));form.Body.Controls.Add(layout);
    layout.Controls.Add(new Label{Text="Recognition quality",AutoSize=true});var quality=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};quality.Items.AddRange(new object[]{"Accurate — larger local model","Fast — smaller local model"});quality.SelectedIndex=settings.Quality=="small"?0:1;layout.Controls.Add(quality);
    layout.Controls.Add(new Label{Text="Spoken language (English is best for English-only meetings)",AutoSize=true});var language=new ComboBox{Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};string[] languages={"auto","en","ur","hi","ar","es","fr","de"};language.Items.AddRange(new object[]{"Detect automatically","English","Urdu","Hindi","Arabic","Spanish","French","German"});language.SelectedIndex=Math.Max(0,Array.IndexOf(languages,settings.Language));layout.Controls.Add(language);
    layout.Controls.Add(new Label{Text="Recognition uses the audio and selected language, without a product-name word list. Review the transcript: local recognition can mishear speech, especially mixed languages, noise or unfamiliar names.",Dock=DockStyle.Fill,Padding=new Padding(0,16,0,0)});var save=form.AddAction("Save settings",()=>{});var cancel=form.AddAction("Cancel",()=>form.Close(),true);form.AcceptButton=save;form.CancelButton=cancel;save.Click+=(s,e)=>{try{settings.Quality=quality.SelectedIndex==0?"small":"tiny";settings.Language=languages[language.SelectedIndex];settings.Vocabulary="";settings.Save();form.DialogResult=DialogResult.OK;}catch(Exception error){BrieflyDialogs.Show(form,error.Message,"Speech settings");}};form.ShowDialog(owner);
   }
  }
 }
}
