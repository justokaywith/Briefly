using System;using System.IO;using System.IO.Compression;using System.Collections.Generic;
namespace BrieflyInstaller {
 public static class SafeInstall {
  public static void EnsureNoLinks(string target) {
   if((File.Exists(target)||Directory.Exists(target))&&(File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0)throw new IOException("Installation through a junction or symbolic link is not allowed.");
   for(var d=new DirectoryInfo(Path.GetFullPath(target));d!=null;d=d.Parent)
    if(d.Exists&&(d.Attributes&FileAttributes.ReparsePoint)!=0)throw new IOException("Installation through a junction or symbolic link is not allowed.");
  }
  public static string EntryPath(string root,string name) {
   if(string.IsNullOrWhiteSpace(name)||name.IndexOf(':')>=0||name.StartsWith("/")||name.StartsWith("\\"))throw new IOException("Invalid package path.");
   string normalized=name.Replace('/',Path.DirectorySeparatorChar);
   foreach(string part in normalized.Split(Path.DirectorySeparatorChar))if(part==".."||part=="."||part.EndsWith(".")||part.EndsWith(" "))throw new IOException("Unsafe package path.");
   string full=Path.GetFullPath(Path.Combine(root,normalized)),prefix=Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
   if(!full.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new IOException("Package entry escapes installation folder.");
   return full;
  }
  // The caller supplies a fixed per-user program directory, never an arbitrary UI path.
  // No files are deleted: previous installation and failed staging stay available for recovery.
  public static string Install(Stream payload,string target,Func<bool> appRunning,Action<int,string> progress) {
   target=Path.GetFullPath(target);EnsureNoLinks(target);
   if(appRunning())throw new IOException("Close Briefly after your recording or transcription finishes, then try again. Setup will not force it to close.");
   string parent=Path.GetDirectoryName(target);Directory.CreateDirectory(parent);
   string stage=target+".staging-"+Guid.NewGuid().ToString("N"),backup=null;
   Directory.CreateDirectory(stage);
   using(var archive=new ZipArchive(payload,ZipArchiveMode.Read,true)) {
    long total=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    if(archive.Entries.Count==0||archive.Entries.Count>1000)throw new IOException("Invalid package size.");
    foreach(var entry in archive.Entries) {
     string dest=EntryPath(stage,entry.FullName);
     if(!seen.Add(dest))throw new IOException("Duplicate package entry.");
     total=checked(total+entry.Length);if(total>1024L*1024*1024)throw new IOException("Package exceeds installation size limit.");
     if((entry.ExternalAttributes>>16&0xF000)==0xA000)throw new IOException("Links are not allowed in packages.");
    }
    int count=0;
    foreach(var entry in archive.Entries) {
     string dest=EntryPath(stage,entry.FullName);
     if(string.IsNullOrEmpty(entry.Name))Directory.CreateDirectory(dest);
     else {Directory.CreateDirectory(Path.GetDirectoryName(dest));using(var input=entry.Open())using(var output=new FileStream(dest,FileMode.CreateNew,FileAccess.Write)){input.CopyTo(output);if(output.Length!=entry.Length)throw new IOException("Incomplete package entry.");}}
     if(progress!=null)progress(10+(++count*65/archive.Entries.Count),"Preparing application files");
    }
   }
   foreach(string required in new[]{"Briefly.exe","Uninstall.exe","installation-files.json","NAudio.Core.dll","NAudio.Wasapi.dll","speech/whisper-cli.exe","speech/ggml-tiny.bin","speech/ggml-small.bin"})
    if(!File.Exists(EntryPath(stage,required)))throw new IOException("Required application component is missing: "+required);
   EnsureNoLinks(target);
   if(appRunning())throw new IOException("Briefly opened during setup. Close it safely and retry.");
   if(Directory.Exists(target)){backup=target+".previous-"+Guid.NewGuid().ToString("N");Directory.Move(target,backup);}
   try {Directory.Move(stage,target);}
   catch {if(backup!=null&&!Directory.Exists(target))Directory.Move(backup,target);throw;}
   if(progress!=null)progress(90,"Application files ready");
   return backup;
  }
 }
}
