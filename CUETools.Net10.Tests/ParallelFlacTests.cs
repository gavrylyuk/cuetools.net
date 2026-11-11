using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using CUETools.Codecs;
using CUETools.Processor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;

[TestClass]
[DoNotParallelize]
public class ParallelFlacTests
{
    private string root;
    internal static CUEConfig Config()
    {
        var config = new CUEConfig { detectHDCD = false, createEACLOG = false, checkForUpdates = false, trackFilenameFormat = "%tracknumber%. %title%", writeArLogOnConvert = false };
        config.formats["flac"].encoderLossless = new AudioEncoderSettingsViewModel(new CUETools.Codecs.CommandLine.EncoderSettings("flac.exe", "flac", true, "0 1 2 3 4 5 6 7 8", "8", Environment.GetEnvironmentVariable("CUETOOLS_TEST_FLAC") ?? "flac.exe", "-%M -P %P - -o %O"));
        config.formats["flac"].decoder = new AudioDecoderSettingsViewModel(new CUETools.Codecs.libFLAC.DecoderSettings());
        config.formats["ape"].encoderLossless = new AudioEncoderSettingsViewModel(new CUETools.Codecs.MACLib.EncoderSettings());
        config.formats["ape"].decoder = new AudioDecoderSettingsViewModel(new CUETools.Codecs.MACLib.DecoderSettings());
        config.formats["wv"].encoderLossless = new AudioEncoderSettingsViewModel(new CUETools.Codecs.libwavpack.EncoderSettings());
        config.formats["wv"].decoder = new AudioDecoderSettingsViewModel(new CUETools.Codecs.libwavpack.DecoderSettings());
        return config;
    }
    [TestInitialize] public void Before() { root=Path.Combine(Path.GetTempPath(),"CUETools-parallel-"+Guid.NewGuid());Directory.CreateDirectory(root); }
    [TestCleanup] public void After() { Directory.Delete(root,true); }
    internal static void WriteSource(CUEConfig config,string path,int frames)
    {
        var encoder=AudioReadWrite.GetAudioDest(AudioEncoderType.Lossless,path,AudioPCMConfig.RedBook,frames,4096,Path.GetExtension(path),config);
        var random=new Random(42);var samples=new int[65536,2];int offset=0;
        try { while(offset<frames) {int n=Math.Min(65536,frames-offset);for(int i=0;i<n;i++){ samples[i,0]=random.Next(-20000,20000);samples[i,1]=(offset+i)%2000-1000; } encoder.Write(new AudioBuffer(AudioPCMConfig.RedBook,samples,n));offset+=n;} }
        finally { encoder.Close(); }
    }
    internal static string PcmHash(CUEConfig config,string path)
    {
        var reader=AudioReadWrite.GetAudioSource(path,null,config);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        try {var buffer=new AudioBuffer(reader,65536);int n;while((n=reader.Read(buffer,-1))!=0)hash.AppendData(buffer.Bytes,0,n*reader.PCM.BlockAlign);return System.Convert.ToHexString(hash.GetHashAndReset());}
        finally {reader.Close();}
    }
    private string Cue(CUEConfig config,string extension,bool separate)
    {
        int frames=44100*12;
        string cue="PERFORMER \"Test artist\"\r\nTITLE \"Test album\"\r\n";
        if(!separate){ WriteSource(config,Path.Combine(root,"source."+extension),frames);cue+="FILE \"source."+extension+"\" WAVE\r\n"; }
        for(int i=0;i<3;i++)
        {
            if(separate){ WriteSource(config,Path.Combine(root,"track"+i+"."+extension),44100*4);cue+="FILE \"track"+i+"."+extension+"\" WAVE\r\n"; }
            cue+="  TRACK "+(i+1).ToString("D2")+" AUDIO\r\n    TITLE \"Song "+(i+1)+"\"\r\n";
            if(i==1&&!separate)cue+="    INDEX 00 00:03:60\r\n";
            cue+="    INDEX 01 00:"+(separate?0:i*4).ToString("D2")+":00\r\n";
        }
        string path=Path.Combine(root,"album.cue");File.WriteAllText(path,cue);return path;
    }
    private string[] Convert(CUEConfig config,string input,string destination,bool parallel,CUEStyle style,int writeOffset)
    {
        Directory.CreateDirectory(destination);
        var cue=new CUESheet(config){Action=CUEAction.Encode,OutputStyle=style,WriteOffset=writeOffset};cue.Open(input);
        string output=Path.Combine(destination,"album.cue");cue.GenerateFilenames(AudioEncoderType.Lossless,"flac",output);
        var finals=cue.DestPaths.ToArray();
        try
        {
            if(parallel)
            {
                using var batch=new ParallelConversionBatch(2,root);cue.ParallelBatch=batch;
                using(var staged=new ParallelAlbumOutput(batch,input,destination,finals.Append(output),cue.SourcePaths,Array.Empty<string>()))
                {
                    cue.GenerateFilenames(AudioEncoderType.Lossless,"flac",Path.Combine(staged.StageDirectory,"album.cue"));
                    using var session=new ParallelFlacSession(batch,config,input,(long)cue.TOC.AudioLength*588);cue.ParallelSession=session;
                    try {cue.Go();session.Checkpoint();staged.Commit();session.MarkCommitted();}
                    finally{cue.ParallelSession=null;}
                }
                batch.Finish();
                Assert.AreEqual(3,batch.Snapshot().Single().Tracks.Count);
                Assert.IsTrue(batch.Snapshot().Single().Complete);
            }
            else cue.Go();
            return finals;
        }
        finally{cue.ParallelBatch=null;cue.Close();}
    }
    [TestMethod]
    [DataRow("wav",false,CUEStyle.GapsAppended,0)]
    [DataRow("flac",false,CUEStyle.GapsAppended,0)]
    [DataRow("ape",false,CUEStyle.GapsAppended,0)]
    [DataRow("wv",false,CUEStyle.GapsAppended,0)]
    [DataRow("wav",true,CUEStyle.GapsAppended,0)]
    [DataRow("flac",true,CUEStyle.GapsAppended,0)]
    [DataRow("ape",true,CUEStyle.GapsAppended,0)]
    [DataRow("wv",true,CUEStyle.GapsAppended,0)]
    [DataRow("wav",false,CUEStyle.GapsPrepended,0)]
    [DataRow("wav",false,CUEStyle.GapsLeftOut,0)]
    [DataRow("wav",false,CUEStyle.GapsAppended,17)]
    [DataRow("wav",false,CUEStyle.GapsAppended,-17)]
    public void ParallelTracksMatchOriginalPcmAndTags(string extension,bool separate,CUEStyle style,int writeOffset)
    {
        var config=Config();string input=Cue(config,extension,separate);
        var baseline=Convert(config,input,Path.Combine(root,"serial"),false,style,writeOffset);
        var parallel=Convert(config,input,Path.Combine(root,"parallel"),true,style,writeOffset);
        Assert.AreEqual(baseline.Length,parallel.Length);
        for(int i=0;i<baseline.Length;i++)
        {
            Assert.AreEqual(PcmHash(config,baseline[i]),PcmHash(config,parallel[i]),"PCM differs for track "+i);
            using var original=TagLib.File.Create(baseline[i]);using var actual=TagLib.File.Create(parallel[i]);
            Assert.AreEqual(original.Tag.Title,actual.Tag.Title);Assert.AreEqual(original.Tag.Album,actual.Tag.Album);CollectionAssert.AreEqual(original.Tag.Performers,actual.Tag.Performers);
        }
        Assert.AreEqual(File.ReadAllText(Path.Combine(root,"serial","album.cue")),File.ReadAllText(Path.Combine(root,"parallel","album.cue")));
        Assert.AreEqual(0,Directory.GetDirectories(root,"cuetools-pcm-*").Length);
    }
    [TestMethod]
    public void EncoderFailurePublishesNothingAndReleasesTemporaryFiles()
    {
        var config=Config();string input=Cue(config,"wav",false);((CUETools.Codecs.CommandLine.EncoderSettings)config.formats["flac"].encoderLossless.Settings).Parameters="--cuetools-invalid-test-option %I -o %O";
        Assert.Throws<Exception>(()=>Convert(config,input,Path.Combine(root,"failed"),true,CUEStyle.GapsAppended,0));
        Assert.AreEqual(0,Directory.GetFiles(Path.Combine(root,"failed"),"*.flac").Length);
        Assert.AreEqual(0,Directory.GetDirectories(root,"cuetools-pcm-*").Length);
        Assert.AreEqual(0,Directory.GetDirectories(Path.Combine(root,"failed"),".cuetools-stage-*").Length);
    }
}
