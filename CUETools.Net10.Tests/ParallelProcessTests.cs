using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using CUETools.Codecs;
using CUETools.Processor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;
[TestClass]
[DoNotParallelize]
public class ParallelProcessTests
{
    private string root;
    [TestInitialize] public void Before(){root=Path.Combine(Path.GetTempPath(),"CUETools-process-"+Guid.NewGuid());Directory.CreateDirectory(root);}
    [TestCleanup] public void After(){Directory.Delete(root,true);}
    private static void Write(IAudioDest dest,int seconds,int bits=16)
    {
        var pcm=new AudioPCMConfig(bits,2,44100);var data=new int[65536,2];var random=new Random(42);int frames=seconds*44100,offset=0;
        while(offset<frames){int n=Math.Min(65536,frames-offset);for(int i=0;i<n;i++){data[i,0]=random.Next(-30000,30000)<<(bits-16);data[i,1]=((offset+i)%2000-1000)<<(bits-16);}dest.Write(new AudioBuffer(pcm,data,n));offset+=n;}
        dest.Close();
    }
    private static void Wait(Func<bool> condition){var timer=Stopwatch.StartNew();while(!condition()&&timer.Elapsed.TotalSeconds<10)Thread.Sleep(5);Assert.IsTrue(condition(),"Encoder did not start.");}
    [TestMethod]
    public void UsesTwoEncoderProcessesWithoutExceedingTheConfiguredLimit()
    {
        var config=ParallelFlacTests.Config();using var batch=new ParallelConversionBatch(2,root);
        using(var session=new ParallelFlacSession(batch,config,Path.Combine(root,"album.cue"),44100L*120))
        {
            for(int i=0;i<4;i++)Write(session.CreateDest(Path.Combine(root,i+".flac"),AudioPCMConfig.RedBook,44100*30,4096),30);
            session.Complete();session.MarkCommitted();
            Assert.AreEqual(2,batch.MaxActiveEncoders);Assert.AreEqual(4,batch.Snapshot().Single().Tracks.Count(t=>t.Complete));
        }
        batch.Finish();
    }
    [TestMethod]
    public void PauseSuspendsOwnedEncoderAndStopWhilePausedKillsItAndCleansStaging()
    {
        var config=ParallelFlacTests.Config();using var batch=new ParallelConversionBatch(1,root);
        string input=Path.Combine(root,"album.cue"),target=Path.Combine(root,"out.flac");
        using(var staged=new ParallelAlbumOutput(batch,input,root,new[]{target},Array.Empty<string>(),Array.Empty<string>()))
        using(var session=new ParallelFlacSession(batch,config,input,44100L*180))
        {
            Write(session.CreateDest(Path.Combine(staged.StageDirectory,"out.flac"),AudioPCMConfig.RedBook,44100*180,4096),180);
            Wait(()=>batch.EncoderProcesses.Length==1);batch.TogglePause();Assert.IsTrue(batch.Paused);
            var process=batch.EncoderProcesses.Single();int id=process.Id;var before=process.TotalProcessorTime;
            Thread.Sleep(250);Assert.IsTrue((process.TotalProcessorTime-before).TotalMilliseconds<60,"Paused encoder continued consuming CPU.");
            batch.Cancel(true);Assert.Throws<StopException>(()=>session.Complete());
            Wait(()=>{try{return Process.GetProcessById(id).HasExited;}catch(ArgumentException){return true;}});
        }
        batch.Finish();Assert.IsFalse(File.Exists(target));Assert.AreEqual(0,Directory.GetDirectories(root,"cuetools-pcm-*").Length);
    }
    [TestMethod]
    public void StopCurrentAlbumWhilePausedDoesNotCancelTheWholeBatch()
    {
        var config=ParallelFlacTests.Config();using var batch=new ParallelConversionBatch(1,root);
        using(var session=new ParallelFlacSession(batch,config,Path.Combine(root,"album.cue"),44100L*120))
        {
            Write(session.CreateDest(Path.Combine(root,"out.flac"),AudioPCMConfig.RedBook,44100*120,4096),120);
            Wait(()=>batch.EncoderProcesses.Length==1);batch.TogglePause();session.CancelCurrent();
            Assert.Throws<OperationCanceledException>(()=>session.Complete());Assert.IsFalse(batch.Paused);batch.Checkpoint();
        }
        batch.Finish();
    }
    [TestMethod]
    [DataRow(16)]
    [DataRow(24)]
    public void PcmVerificationSupportsOutputBitDepth(int bits)
    {
        var config=ParallelFlacTests.Config();using var batch=new ParallelConversionBatch(1,root);
        using(var session=new ParallelFlacSession(batch,config,Path.Combine(root,"album.cue"),44100))
        {
            Write(session.CreateDest(Path.Combine(root,"out.flac"),new AudioPCMConfig(bits,2,44100),44100,4096),1,bits);
            session.Complete();Assert.IsTrue(batch.Snapshot().Single().Tracks.Single().Complete);
        }
        batch.Finish();
    }
    [TestMethod]
    [DataRow("-8 -P %P %I -o %O")]
    [DataRow("-8 -c -")]
    public void SupportsFileInputAndStdoutEncoderTemplates(string parameters)
    {
        var config=ParallelFlacTests.Config();((CUETools.Codecs.CommandLine.EncoderSettings)config.formats["flac"].encoderLossless.Settings).Parameters=parameters;
        using var batch=new ParallelConversionBatch(1,root);
        using(var session=new ParallelFlacSession(batch,config,Path.Combine(root,"album.cue"),44100))
        {
            Write(session.CreateDest(Path.Combine(root,"out.flac"),AudioPCMConfig.RedBook,44100,4096),1);
            session.Complete();Assert.IsTrue(batch.Snapshot().Single().Tracks.Single().Complete);
        }
        batch.Finish();
    }
}
