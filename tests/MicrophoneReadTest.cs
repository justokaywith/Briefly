using System;using System.Diagnostics;using Briefly;
class MicrophoneReadTest {
 static int Main(){var watch=Stopwatch.StartNew();var signal=MeetingSignals.DetectMicrophone();watch.Stop();if(signal==null)throw new Exception("Missing detector result");Console.WriteLine("Read-only Windows mic metadata adapter returned; reliable="+signal.Reliable+"; active="+(signal.Title!=null)+"; elapsedMs="+watch.ElapsedMilliseconds+". No audio capture, private titles or paths logged.");return 0;}
}
