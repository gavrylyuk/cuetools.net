using System;
using System.IO;
using System.Reflection;
using CUETools.Codecs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;

[TestClass]
public class NativeCodecTests
{
    [TestMethod]
    [DataRow(97, 16)]
    [DataRow(44100, 16)]
    [DataRow(46080, 16)]
    [DataRow(46177, 16)]
    [DataRow(97, 24)]
    [DataRow(46177, 24)]
    public void TtaRoundTripPreservesPartialAndCompleteFrames(int sampleCount, int bitsPerSample)
    {
        string pluginPath = Path.Combine(AppContext.BaseDirectory, "plugins", "x64", "CUETools.Codecs.TTA.dll");
        Assert.IsTrue(File.Exists(pluginPath), "Build the native codecs with build-net10.ps1 before running this suite.");
        var plugin = Assembly.LoadFrom(pluginPath);
        string folder = Path.Combine(Path.GetTempPath(), "CUETools-TTA-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, "roundtrip.tta");
        IAudioDest encoder = null;
        IAudioSource decoder = null;
        try
        {
            var pcm = new AudioPCMConfig(bitsPerSample, 2, 44100);
            var samples = new int[sampleCount, pcm.ChannelCount];
            var random = new Random(1234);
            for (int i = 0; i < sampleCount; i++)
                for (int channel = 0; channel < pcm.ChannelCount; channel++)
                    samples[i, channel] = random.Next(-(1 << (bitsPerSample - 1)), 1 << (bitsPerSample - 1));
            var settings = (IAudioEncoderSettings)Activator.CreateInstance(plugin.GetType("CUETools.Codecs.TTA.EncoderSettings", true));
            settings.PCM = pcm;
            encoder = (IAudioDest)Activator.CreateInstance(settings.EncoderType, settings, path, null);
            encoder.FinalSampleCount = sampleCount;
            encoder.Write(new AudioBuffer(pcm, samples, sampleCount));
            encoder.Close();
            encoder = null;

            var decoderSettings = (IAudioDecoderSettings)Activator.CreateInstance(plugin.GetType("CUETools.Codecs.TTA.DecoderSettings", true));
            decoder = (IAudioSource)Activator.CreateInstance(plugin.GetType("CUETools.Codecs.TTA.AudioDecoder", true), decoderSettings, path, null);
            Assert.AreEqual(sampleCount, decoder.Length);
            var buffer = new AudioBuffer(decoder, 777);
            var expected = new int[sampleCount * pcm.ChannelCount];
            var actual = new int[expected.Length];
            Buffer.BlockCopy(samples, 0, expected, 0, expected.Length * sizeof(int));
            int offset = 0;
            int read;
            while ((read = decoder.Read(buffer, 777)) > 0)
            {
                Buffer.BlockCopy(buffer.Samples, 0, actual, offset * pcm.ChannelCount * sizeof(int), read * pcm.ChannelCount * sizeof(int));
                offset += read;
            }
            Assert.AreEqual(sampleCount, offset);
            CollectionAssert.AreEqual(expected, actual);
        }
        finally
        {
            encoder?.Close();
            decoder?.Close();
            Directory.Delete(folder, true);
        }
    }
}
