using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using NAudio.Wave;
using NAudio.CoreAudioApi;
using NAudio.Wave.SampleProviders;

namespace Briefly {
    // Two actual WASAPI endpoints. Capture only after the user presses Record.
    public sealed class CaptureSession : IDisposable {
        private WasapiCapture microphone;
        private WasapiLoopbackCapture loopback;
        private WaveFileWriter micWriter, outputWriter;
        private readonly object micLock = new object(), outputLock = new object();
        private readonly ManualResetEvent micStopped = new ManualResetEvent(false), outputStopped = new ManualResetEvent(false);
        private readonly Stopwatch clock = new Stopwatch();
        private string micPath, outputPath;
        private volatile float peak;
        private volatile bool running;
        private Exception captureError;
        public float Peak { get { return peak; } }
        public double ElapsedSeconds { get { return clock.Elapsed.TotalSeconds; } }

        public void Start(string directory) {
            if (running) throw new InvalidOperationException("A recording is already running.");
            Directory.CreateDirectory(directory);
            string id = Guid.NewGuid().ToString("N");
            micPath = Path.Combine(directory,id+"-microphone.wav");
            outputPath = Path.Combine(directory,id+"-system.wav");
            microphone = new WasapiCapture(); loopback = new WasapiLoopbackCapture();
            microphone.WaveFormat = new WaveFormat(16000,16,1);
            loopback.WaveFormat = new WaveFormat(16000,16,1);
            micWriter = new WaveFileWriter(micPath,microphone.WaveFormat);
            outputWriter = new WaveFileWriter(outputPath,loopback.WaveFormat);
            microphone.DataAvailable += (s,e) => Write(micWriter,micLock,e);
            loopback.DataAvailable += (s,e) => Write(outputWriter,outputLock,e);
            microphone.RecordingStopped += (s,e) => { if(e.Exception!=null)captureError=e.Exception;micStopped.Set(); };
            loopback.RecordingStopped += (s,e) => { if(e.Exception!=null)captureError=e.Exception;outputStopped.Set(); };
            running=true;clock.Start();
            try { loopback.StartRecording();microphone.StartRecording(); }
            catch { running=false;Dispose();throw; }
        }
        private void Write(WaveFileWriter writer,object gate,WaveInEventArgs args) {
            lock(gate) {
                // WASAPI may emit no loopback packets during silence. Preserve the timeline.
                long expected = Math.Max(0,(long)(clock.Elapsed.TotalSeconds*32000)-args.BytesRecorded);
                expected -= expected%2;
                long gap=expected-writer.Length;
                if(gap>3200) { byte[] silence=new byte[3200];while(gap>0){int count=(int)Math.Min(gap,silence.Length);writer.Write(silence,0,count);gap-=count;} }
                writer.Write(args.Buffer,0,args.BytesRecorded);
                float samplePeak=0;
                for(int i=0;i+1<args.BytesRecorded;i+=2)samplePeak=Math.Max(samplePeak,Math.Abs(BitConverter.ToInt16(args.Buffer,i)/32768f));
                peak=samplePeak;
            }
        }
        public string Stop(string wavPath,string mp3Path) {
            if(!running)throw new InvalidOperationException("No recording is running.");
            running=false;microphone.StopRecording();loopback.StopRecording();
            if(!micStopped.WaitOne(10000)||!outputStopped.WaitOne(10000))throw new IOException("Audio devices did not stop cleanly. Raw recordings have been kept.");
            clock.Stop();lock(micLock){micWriter.Dispose();micWriter=null;}lock(outputLock){outputWriter.Dispose();outputWriter=null;}
            if(captureError!=null)throw new IOException("An audio device disconnected. Raw recordings have been kept.",captureError);
            MixFiles(micPath,outputPath,wavPath,mp3Path);
            return wavPath;
        }
        public static void MixFiles(string first,string second,string wavPath,string mp3Path) {
            using(var a=new WaveFileReader(first))using(var b=new WaveFileReader(second)) {
                var mixer=new MixingSampleProvider(new ISampleProvider[]{a.ToSampleProvider(),b.ToSampleProvider()});
                var volume=new VolumeSampleProvider(mixer);volume.Volume=.5f;
                WaveFileWriter.CreateWaveFile16(wavPath,volume);
            }
            using(var mixed=new WaveFileReader(wavPath))MediaFoundationEncoder.EncodeToMp3(mixed,mp3Path,128000);
            if(new FileInfo(mp3Path).Length<128)throw new IOException("MP3 encoding produced no audio.");
        }
        public void Dispose() {
            if(microphone!=null){try{microphone.StopRecording();}catch{}microphone.Dispose();microphone=null;}
            if(loopback!=null){try{loopback.StopRecording();}catch{}loopback.Dispose();loopback=null;}
            lock(micLock){if(micWriter!=null){micWriter.Dispose();micWriter=null;}}
            lock(outputLock){if(outputWriter!=null){outputWriter.Dispose();outputWriter=null;}}
            running=false;clock.Stop();
        }
    }
}
