using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;

[TestClass]
[DoNotParallelize]
public class MigrationTests
{
    [TestMethod]
    public void CueReaderAcceptsAnsiAndUtf8WithoutLeakingFileHandles()
    {
        string path = Path.Combine(Path.GetTempPath(), "CUETools-encoding-" + Guid.NewGuid() + ".cue");
        try
        {
            string text = "TITLE \"caf\u00e9\"";
            File.WriteAllBytes(path, CUETools.Codecs.AudioTextEncoding.Ansi.GetBytes(text));
            using (var reader = CUETools.Processor.CUESheet.StreamReader_UTF_ANSI(path))
                Assert.AreEqual(text, reader.ReadToEnd());
            File.WriteAllText(path, text, new System.Text.UTF8Encoding(false));
            using (var reader = CUETools.Processor.CUESheet.StreamReader_UTF_ANSI(path))
                Assert.AreEqual(text, reader.ReadToEnd());
            Assert.AreEqual(1250, System.Text.Encoding.GetEncoding(1250).CodePage);
        }
        finally { File.Delete(path); }
    }

    [TestMethod]
    public void SingleInstancePipeForwardsUnicodeAndEmptyArguments()
    {
        string id = "CUETools-test-" + Guid.NewGuid();
        string[] received = null;
        try
        {
            Assert.IsTrue(JDP.SingletonController.IamFirst(id, args => { received = args; return true; }));
            string[] arguments = { @"C:\音楽\album.cue", "", "a path with spaces" };
            Assert.IsTrue(JDP.SingletonController.TrySend(id, arguments));
            CollectionAssert.AreEqual(arguments, received);
            Assert.IsTrue(JDP.SingletonController.TrySend(id, Array.Empty<string>()));
            Assert.AreEqual(0, received.Length);
        }
        finally { JDP.SingletonController.Cleanup(); }
    }
}

