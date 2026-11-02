using System;using System.IO;using System.Diagnostics;using System.Windows.Forms;using System.Web.Script.Serialization;using System.Collections.Generic;using Microsoft.Win32;
namespace BrieflyInstaller {
 static class UninstallProgram {
  [STAThread]static void Main(string[] args){
   Application.EnableVisualStyles();string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","Briefly");
   try{
    if(args.Length==0){string helper=Path.Combine(Path.GetTempPath(),"Briefly-Uninstall-"+Guid.NewGuid().ToString("N")+".exe");File.Copy(Application.ExecutablePath,helper);Process.Start(new ProcessStartInfo(helper,"--remove-program-files"){UseShellExecute=true});return;}
    if(args.Length!=1||args[0]!="--remove-program-files")return;
    foreach(var process in Process.GetProcessesByName("Briefly"))using(process)if(!process.HasExited){MessageBox.Show("Close Briefly after recording or transcription finishes, then try again. Your recordings have not been changed.","Briefly uninstall");return;}
    if(MessageBox.Show("Remove Briefly application files? Your saved sign-in, settings and local recordings in your Windows user profile will be kept. Cloud meeting history will not be deleted.","Uninstall Briefly",MessageBoxButtons.YesNo,MessageBoxIcon.Question,MessageBoxDefaultButton.Button2)!=DialogResult.Yes)return;
    SafeInstall.EnsureNoLinks(Path.Combine(target,"installation-files.json"));var json=new JavaScriptSerializer();json.MaxJsonLength=1024*1024;var manifest=json.Deserialize<InstallationFiles>(File.ReadAllText(Path.Combine(target,"installation-files.json")));
    if(manifest==null||manifest.product!="Briefly"||manifest.files==null||manifest.files.Length>1000)throw new IOException("Installation manifest is invalid. Application files were not removed.");
    RemoveFiles(target,manifest.files);
    using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Briefly")){if(key!=null&&string.Equals(Convert.ToString(key.GetValue("InstallLocation")),target,StringComparison.OrdinalIgnoreCase)){key.Close();Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\Briefly",false);}}
    RemoveShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Briefly.lnk"),target);
    RemoveShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"Briefly Meeting Intelligence.lnk"),target);
    MessageBox.Show("Briefly application files were removed. Recordings, settings and cloud history were kept. Previous installation backups were also kept.","Briefly");
   }catch(Exception error){MessageBox.Show("Uninstall could not finish: "+error.Message+" Your user-profile recordings have not been deleted.","Briefly");}
  }
  public class InstallationFiles{public string product{get;set;}public string[] files{get;set;}}
  public static void RemoveFiles(string root,string[] names){
   SafeInstall.EnsureNoLinks(root);SafeInstall.EnsureNoLinks(Path.Combine(root,"installation-files.json"));var paths=new List<string>();foreach(string name in names){string file=SafeInstall.EntryPath(root,name),ext=Path.GetExtension(file).ToLowerInvariant();if(!new HashSet<string>{".exe",".dll",".ico",".png",".bin",".txt",".json"}.Contains(ext))throw new IOException("Unexpected application file type.");SafeInstall.EnsureNoLinks(file);paths.Add(file);}
   foreach(string file in paths)if(File.Exists(file))File.Delete(file);
   string manifest=Path.Combine(root,"installation-files.json");if(File.Exists(manifest))File.Delete(manifest);
   string speech=Path.Combine(root,"speech");if(Directory.Exists(speech)&&Directory.GetFileSystemEntries(speech).Length==0)Directory.Delete(speech);if(Directory.Exists(root)&&Directory.GetFileSystemEntries(root).Length==0)Directory.Delete(root);
  }
  static void RemoveShortcut(string file,string target){if(!File.Exists(file))return;Type shellType=Type.GetTypeFromProgID("WScript.Shell");if(shellType==null)return;dynamic shell=Activator.CreateInstance(shellType),link=shell.CreateShortcut(file);if(string.Equals(Convert.ToString(link.TargetPath),Path.Combine(target,"Briefly.exe"),StringComparison.OrdinalIgnoreCase))File.Delete(file);}
 }
}
