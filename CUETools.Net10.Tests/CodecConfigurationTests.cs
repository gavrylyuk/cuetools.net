using System;
using System.IO;
using System.Linq;
using CUETools.Processor;
using CUETools.Processor.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CUETools.Net10.Tests;
[TestClass] public class CodecConfigurationTests
{
    [TestMethod] public void CoreAssemblyIsNotDiscoveredAsAPlugin()
    {
        var encoders = CUEProcessorPlugins.encs.ToArray();
        var decoders = CUEProcessorPlugins.decs.ToArray();
        var scan = typeof(CUEProcessorPlugins).GetMethod("AddPlugin", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
        Assert.IsNotNull(scan);
        scan.Invoke(null, new object[] { typeof(CUETools.Codecs.IAudioEncoderSettings).Assembly.Location });
        CollectionAssert.AreEqual(encoders, CUEProcessorPlugins.encs.ToArray());
        CollectionAssert.AreEqual(decoders, CUEProcessorPlugins.decs.ToArray());
        Assert.IsFalse(CUEProcessorPlugins.encs.OfType<CUETools.Codecs.CommandLine.EncoderSettings>().Any(e => string.IsNullOrWhiteSpace(e.Name) && string.IsNullOrWhiteSpace(e.Extension)));
        Assert.IsFalse(CUEProcessorPlugins.decs.OfType<CUETools.Codecs.CommandLine.DecoderSettings>().Any(e => string.IsNullOrWhiteSpace(e.Name) && string.IsNullOrWhiteSpace(e.Extension)));
    }
    [TestMethod] public void LoadingRemovesOnlyCompletelyEmptyExternalCodecs()
    {
        string dir=Path.Combine(Path.GetTempPath(),"cuetools-codec-settings-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try {
            var config=new CUEConfig();
            var originalEncoders=config.advanced.encoders.Select(e=>(e.Name,e.Extension,e.Lossless)).ToArray();
            var originalDecoders=config.advanced.decoders.Select(d=>(d.Name,d.Extension)).ToArray();
            config.advanced.encoders.Add(new CUETools.Codecs.CommandLine.EncoderSettings("","",false,"","","",""));
            config.advanced.decoders.Add(new CUETools.Codecs.CommandLine.DecoderSettings("","","",""));
            config.advanced.encoders.Add(new CUETools.Codecs.CommandLine.EncoderSettings("Draft encoder","",true,"","","",""));
            config.advanced.decoders.Add(new CUETools.Codecs.CommandLine.DecoderSettings("Draft decoder","","",""));
            config.advanced.encoders.Add(new CUETools.Codecs.CommandLine.EncoderSettings("Custom encoder","flac",true,"8","8","encoder.exe","-%M - -o %O"));
            config.advanced.decoders.Add(new CUETools.Codecs.CommandLine.DecoderSettings("Custom decoder","flac","decoder.exe","%I"));
            string appPath=Path.Combine(dir,"app.exe");var writer=new SettingsWriter("Test","settings.txt",appPath);config.Save(writer);writer.Close();
            var reader=new SettingsReader("Test","settings.txt",appPath);string saved=Path.Combine(reader.ProfilePath,"settings.txt");string before=File.ReadAllText(saved);
            var loaded=new CUEConfig();loaded.Load(reader);Assert.AreEqual(before,File.ReadAllText(saved),"Loading must not rewrite user settings.");
            Assert.IsFalse(loaded.advanced.encoders.OfType<CUETools.Codecs.CommandLine.EncoderSettings>().Any(e=>string.IsNullOrEmpty(e.Name)&&string.IsNullOrEmpty(e.Extension)&&string.IsNullOrEmpty(e.Path)&&string.IsNullOrEmpty(e.Parameters)&&string.IsNullOrEmpty(e.SupportedModes)&&string.IsNullOrEmpty(e.EncoderMode)));
            Assert.IsFalse(loaded.advanced.decoders.OfType<CUETools.Codecs.CommandLine.DecoderSettings>().Any(d=>string.IsNullOrEmpty(d.Name)&&string.IsNullOrEmpty(d.Extension)&&string.IsNullOrEmpty(d.Path)&&string.IsNullOrEmpty(d.Parameters)));
            Assert.IsTrue(loaded.advanced.encoders.Any(e=>e.Name=="Draft encoder"));Assert.IsTrue(loaded.advanced.decoders.Any(d=>d.Name=="Draft decoder"));
            Assert.IsTrue(loaded.advanced.encoders.OfType<CUETools.Codecs.CommandLine.EncoderSettings>().Any(e=>e.Name=="Custom encoder"&&e.Path=="encoder.exe"&&e.Parameters=="-%M - -o %O"));
            Assert.IsTrue(loaded.advanced.decoders.OfType<CUETools.Codecs.CommandLine.DecoderSettings>().Any(d=>d.Name=="Custom decoder"&&d.Path=="decoder.exe"&&d.Parameters=="%I"));
            foreach(var e in originalEncoders) Assert.IsTrue(loaded.advanced.encoders.Any(x=>(x.Name,x.Extension,x.Lossless)==e),"Missing default encoder: "+e.Name);
            foreach(var d in originalDecoders) Assert.IsTrue(loaded.advanced.decoders.Any(x=>(x.Name,x.Extension)==d),"Missing default decoder: "+d.Name);
            writer=new SettingsWriter("Test","settings.txt",appPath);loaded.Save(writer);writer.Close();var again=new CUEConfig();again.Load(new SettingsReader("Test","settings.txt",appPath));
            Assert.IsTrue(again.advanced.encoders.Any(e=>e.Name=="Draft encoder"));Assert.IsTrue(again.advanced.decoders.Any(d=>d.Name=="Custom decoder"));
        } finally { Directory.Delete(dir,true); }
    }
}
