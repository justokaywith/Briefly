using System;using System.IO;using System.IO.Compression;using BrieflyInstaller;
class SafeInstallTest {
 static MemoryStream Package(string extra,bool complete){var m=new MemoryStream();using(var z=new ZipArchive(m,ZipArchiveMode.Create,true)){if(complete)foreach(var f in new[]{"Briefly.exe","Uninstall.exe","installation-files.json","NAudio.Core.dll","NAudio.Wasapi.dll","speech/whisper-cli.exe","speech/ggml-tiny.bin","speech/ggml-small.bin"})using(var w=new StreamWriter(z.CreateEntry(f).Open()))w.Write("fixture");if(extra!=null)using(var w=new StreamWriter(z.CreateEntry(extra).Open()))w.Write("untrusted");}m.Position=0;return m;}
 static void Deny(Action action){bool denied=false;try{action();}catch(IOException){denied=true;}if(!denied)throw new Exception("Unsafe operation accepted");}
 static int Main(string[] args){string root=Path.Combine(args[0],Guid.NewGuid().ToString("N")),target=Path.Combine(root,"Briefly");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"old.txt"),"preserve");
  foreach(string bad in new[]{"../escape","/absolute","C:/outside","speech/../../escape","name:stream","folder./file"})Deny(()=>SafeInstall.EntryPath(root,bad));
  using(var m=Package(null,true))Deny(()=>SafeInstall.Install(m,target,()=>true,null));
  using(var m=Package("../escape",true))Deny(()=>SafeInstall.Install(m,target,()=>false,null));
  using(var m=Package("readme.txt",false))Deny(()=>SafeInstall.Install(m,target,()=>false,null));
  if(!File.Exists(Path.Combine(target,"old.txt")))throw new Exception("Old installation was damaged");
  int polls=0;using(var m=Package(null,true))Deny(()=>SafeInstall.Install(m,target,()=>++polls>1,null));
  string backup;using(var m=Package(null,true))backup=SafeInstall.Install(m,target,()=>false,null);
  if(File.ReadAllText(Path.Combine(backup,"old.txt"))!="preserve"||!File.Exists(Path.Combine(target,"Briefly.exe")))throw new Exception("Installation/backup failed");
  Console.WriteLine("Installer: traversal, running app, incomplete payload, restart race rejected; old files preserved; staged install passed.");return 0;
 }
}
