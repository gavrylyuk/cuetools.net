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
            config.advanced.encoders.Add(new CUETools.Codecs.CommandLine.EncoderSettings("","wav",false,"","","",""));
            config.advanced.decoders.Add(new CUETools.Codecs.CommandLine.DecoderSettings("","wav","",""));
            config.advanced.decoders.Add(new CUETools.Codecs.CommandLine.DecoderSettings("","wav","custom.exe",""));
            config.advanced.encoders.Add(new CUETools.Codecs.CommandLine.EncoderSettings("Draft encoder","",true,"","","",""));
            config.advanced.decoders.Add(new CUETools.Codecs.CommandLine.DecoderSettings("Draft decoder","","",""));
            config.advanced.encoders.Add(new CUETools.Codecs.CommandLine.EncoderSettings("Custom encoder","flac",true,"8","8","encoder.exe","-%M - -o %O"));
            config.advanced.decoders.Add(new CUETools.Codecs.CommandLine.DecoderSettings("Custom decoder","flac","decoder.exe","%I"));
            string appPath=Path.Combine(dir,"app.exe");var writer=new SettingsWriter("Test","settings.txt",appPath);config.Save(writer);writer.Close();
            var reader=new SettingsReader("Test","settings.txt",appPath);string saved=Path.Combine(reader.ProfilePath,"settings.txt");string before=File.ReadAllText(saved);
            var loaded=new CUEConfig();loaded.Load(reader);Assert.AreEqual(before,File.ReadAllText(saved),"Loading must not rewrite user settings.");
            Assert.IsFalse(loaded.advanced.encoders.OfType<CUETools.Codecs.CommandLine.EncoderSettings>().Any(e=>string.IsNullOrEmpty(e.Name)&&string.IsNullOrEmpty(e.Extension)&&string.IsNullOrEmpty(e.Path)&&string.IsNullOrEmpty(e.Parameters)&&string.IsNullOrEmpty(e.SupportedModes)&&string.IsNullOrEmpty(e.EncoderMode)));
            Assert.IsFalse(loaded.advanced.decoders.OfType<CUETools.Codecs.CommandLine.DecoderSettings>().Any(d=>string.IsNullOrEmpty(d.Name)&&string.IsNullOrEmpty(d.Extension)&&string.IsNullOrEmpty(d.Path)&&string.IsNullOrEmpty(d.Parameters)));
            Assert.IsFalse(loaded.advanced.encoders.OfType<CUETools.Codecs.CommandLine.EncoderSettings>().Any(e=>e.Name=="" && e.Extension=="wav" && e.Path==""));
            Assert.IsFalse(loaded.advanced.decoders.OfType<CUETools.Codecs.CommandLine.DecoderSettings>().Any(d=>d.Name=="" && d.Extension=="wav" && d.Path==""));
            Assert.IsTrue(loaded.advanced.decoders.OfType<CUETools.Codecs.CommandLine.DecoderSettings>().Any(d=>d.Extension=="wav" && d.Path=="custom.exe"));
            Assert.IsTrue(loaded.advanced.encoders.Any(e=>e.Name=="Draft encoder"));Assert.IsTrue(loaded.advanced.decoders.Any(d=>d.Name=="Draft decoder"));
            Assert.IsTrue(loaded.advanced.encoders.OfType<CUETools.Codecs.CommandLine.EncoderSettings>().Any(e=>e.Name=="Custom encoder"&&e.Path=="encoder.exe"&&e.Parameters=="-%M - -o %O"));
            Assert.IsTrue(loaded.advanced.decoders.OfType<CUETools.Codecs.CommandLine.DecoderSettings>().Any(d=>d.Name=="Custom decoder"&&d.Path=="decoder.exe"&&d.Parameters=="%I"));
            foreach(var e in originalEncoders) Assert.IsTrue(loaded.advanced.encoders.Any(x=>(x.Name,x.Extension,x.Lossless)==e),"Missing default encoder: "+e.Name);
            foreach(var d in originalDecoders) Assert.IsTrue(loaded.advanced.decoders.Any(x=>(x.Name,x.Extension)==d),"Missing default decoder: "+d.Name);
            writer=new SettingsWriter("Test","settings.txt",appPath);loaded.Save(writer);writer.Close();var again=new CUEConfig();again.Load(new SettingsReader("Test","settings.txt",appPath));
            Assert.IsTrue(again.advanced.encoders.Any(e=>e.Name=="Draft encoder"));Assert.IsTrue(again.advanced.decoders.Any(d=>d.Name=="Custom decoder"));
        } finally { Directory.Delete(dir,true); }
    }
    [TestMethod] public void PredefinedCodecsAreProtectedAndRemainEditable()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cuetools-presets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var config = new CUEConfig();
            var encoder = config.Encoders.Single(e => e.Name == "flake.exe" && e.Extension == "flac");
            var decoder = config.Decoders.Single(d => d.Name == "takc.exe" && d.Extension == "tak");
            Assert.IsTrue(encoder.CanBeDeleted); Assert.IsFalse(encoder.CanBeRemoved);
            Assert.IsTrue(decoder.CanBeDeleted); Assert.IsFalse(decoder.CanBeRemoved);
            encoder.Name = "My Flake preset"; encoder.Path = "custom-flake.exe";
            decoder.Name = "My TAK preset"; decoder.Path = "custom-tak.exe";
            Assert.IsFalse(encoder.CanBeRemoved); Assert.IsFalse(decoder.CanBeRemoved);
            config.advanced.encoders.Add(new CUETools.Codecs.CommandLine.EncoderSettings("My encoder", "flac", true, "8", "8", "my.exe", "-%M %O"));
            config.advanced.decoders.Add(new CUETools.Codecs.CommandLine.DecoderSettings("My decoder", "flac", "my-decoder.exe", "%I"));
            string app = Path.Combine(dir, "app.exe");
            var writer = new SettingsWriter("Test", "settings.txt", app); config.Save(writer); writer.Close();
            var loaded = new CUEConfig(); loaded.Load(new SettingsReader("Test", "settings.txt", app));
            var savedEncoder = loaded.Encoders.Single(e => e.Name == "My Flake preset");
            var savedDecoder = loaded.Decoders.Single(d => d.Name == "My TAK preset");
            Assert.IsFalse(savedEncoder.CanBeRemoved); Assert.AreEqual("custom-flake.exe", savedEncoder.Path);
            Assert.IsFalse(savedDecoder.CanBeRemoved); Assert.AreEqual("custom-tak.exe", savedDecoder.Path);
            Assert.IsFalse(loaded.Encoders.Any(e => e.Name == "flake.exe" && e.Extension == "flac"));
            Assert.IsFalse(loaded.Decoders.Any(d => d.Name == "takc.exe" && d.Extension == "tak"));
            Assert.IsTrue(loaded.Encoders.Single(e => e.Name == "My encoder").CanBeRemoved);
            Assert.IsTrue(loaded.Decoders.Single(d => d.Name == "My decoder").CanBeRemoved);
            loaded.Encoders.Remove(loaded.Encoders.Single(e => e.Name == "My encoder"));
            loaded.Decoders.Remove(loaded.Decoders.Single(d => d.Name == "My decoder"));
            writer = new SettingsWriter("Test", "settings.txt", app); loaded.Save(writer); writer.Close();
            var afterDelete = new CUEConfig(); afterDelete.Load(new SettingsReader("Test", "settings.txt", app));
            Assert.IsFalse(afterDelete.Encoders.Any(e => e.Name == "My encoder"));
            Assert.IsFalse(afterDelete.Decoders.Any(d => d.Name == "My decoder"));
        } finally { Directory.Delete(dir, true); }
    }
    [TestMethod] public void LegacyAndRemovedDefaultsAreRestoredAndProtected()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cuetools-legacy-presets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var config = new CUEConfig();
            var legacy = (CUETools.Codecs.CommandLine.EncoderSettings)config.Encoders.Single(e => e.Name == "flac.exe").Settings;
            legacy.PresetId = ""; legacy.Path = "my-flac.exe";
            config.Encoders.Remove(config.Encoders.Single(e => e.Name == "flake.exe"));
            string app = Path.Combine(dir, "app.exe");
            var writer = new SettingsWriter("Test", "settings.txt", app); config.Save(writer); writer.Save("RemovedDefaultExternalEncoders", "[]"); writer.Close();
            var loaded = new CUEConfig(); loaded.Load(new SettingsReader("Test", "settings.txt", app));
            Assert.IsFalse(loaded.Encoders.Single(e => e.Name == "flac.exe").CanBeRemoved);
            Assert.AreEqual("my-flac.exe", loaded.Encoders.Single(e => e.Name == "flac.exe").Path);
            Assert.IsFalse(loaded.Encoders.Single(e => e.Name == "flake.exe").CanBeRemoved);
        } finally { Directory.Delete(dir, true); }
    }
}
