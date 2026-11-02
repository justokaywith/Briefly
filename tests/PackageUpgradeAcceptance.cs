using System;using System.IO;using BrieflyInstaller;
class PackageUpgradeAcceptance{
 static int Main(string[] args){string workspace=Path.GetFullPath(args[0]),target=Path.GetFullPath(Path.Combine(workspace,"isolated-program"));if(!target.StartsWith(workspace+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("Unsafe test target");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"previous-program.txt"),"preserved previous program");
  string profile=Path.Combine(workspace,"isolated-profile");Directory.CreateDirectory(profile);File.WriteAllText(Path.Combine(profile,"recording-and-session.txt"),"preserve history and sign-in");
  using(var zip=File.OpenRead(args[1])){string backup=SafeInstall.Install(zip,target,()=>false,null);if(File.ReadAllText(Path.Combine(backup,"previous-program.txt"))!="preserved previous program")throw new Exception("Backup missing");Console.WriteLine("Previous program preserved in isolated backup");}
  if(File.ReadAllText(Path.Combine(profile,"recording-and-session.txt"))!="preserve history and sign-in")throw new Exception("Profile changed");
  if(!File.Exists(Path.Combine(target,"briefly-1.3.4.ico")))throw new Exception("Versioned brand icon missing");
  Console.WriteLine("Real package staged upgrade passed; versioned icon included; isolated profile untouched. No owner installation changed.");return 0;
 }
}
