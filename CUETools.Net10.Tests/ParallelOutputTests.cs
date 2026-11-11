using System;
using System.IO;
using System.Linq;
using CUETools.Processor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;

[TestClass]
public class ParallelOutputTests
{
    private string root;
    [TestInitialize] public void Before() { root = Path.Combine(Path.GetTempPath(), "CUETools-transaction-" + Guid.NewGuid()); Directory.CreateDirectory(root); }
    [TestCleanup] public void After() { Directory.Delete(root, true); }

    [TestMethod]
    public void StopDeleteRestoresOriginalAndRemovesOnlyGeneratedOutputs()
    {
        string existing = Path.Combine(root, "existing.flac"), created = Path.Combine(root, "new.flac"), untouched = Path.Combine(root, "unrelated.txt"), input = Path.Combine(root, "input.cue");
        File.WriteAllText(existing, "original"); File.WriteAllText(untouched, "keep"); File.WriteAllText(input, "source");
        using var batch = new ParallelConversionBatch(2, root);
        using (var output = new ParallelAlbumOutput(batch, input, root, new[] { existing, created }, new[] { input }, new[] { existing }))
        {
            File.WriteAllText(Path.Combine(output.StageDirectory, "existing.flac"), "encoded");
            File.WriteAllText(Path.Combine(output.StageDirectory, "new.flac"), "new"); output.Commit();
        }
        Assert.AreEqual("encoded", File.ReadAllText(existing)); Assert.IsTrue(File.Exists(created));
        batch.Cancel(true); batch.Finish();
        Assert.AreEqual("original", File.ReadAllText(existing)); Assert.IsFalse(File.Exists(created));
        Assert.AreEqual("keep", File.ReadAllText(untouched)); Assert.AreEqual("source", File.ReadAllText(input));
    }
    [TestMethod]
    public void RejectsOutputCollisionsAcrossDifferentCues()
    {
        using var batch = new ParallelConversionBatch(2, root);
        string target = Path.Combine(root, "same.flac");
        using var first = new ParallelAlbumOutput(batch, Path.Combine(root,"a.cue"), root, new[] { target }, Array.Empty<string>(), Array.Empty<string>());
        Assert.Throws<IOException>(() => new ParallelAlbumOutput(batch, Path.Combine(root,"b.cue"), root, new[] { target }, Array.Empty<string>(), Array.Empty<string>()));
        batch.Finish();
    }
    [TestMethod]
    public void NeverStagesOverAnInputAndNeverOverwritesUnapprovedFiles()
    {
        using var batch = new ParallelConversionBatch(1, root);
        string input = Path.Combine(root,"source.flac"); File.WriteAllText(input,"source");
        Assert.Throws<IOException>(() => new ParallelAlbumOutput(batch, input, root, new[] { input }, new[] { input }, new[] { input }));
        using var output = new ParallelAlbumOutput(batch, Path.Combine(root,"x.cue"), root, new[] { Path.Combine(root,"out.flac") }, new[] { input }, Array.Empty<string>());
        File.WriteAllText(Path.Combine(output.StageDirectory,"out.flac"),"new"); File.WriteAllText(Path.Combine(root,"out.flac"),"external");
        Assert.Throws<IOException>(()=>output.Commit()); Assert.AreEqual("external",File.ReadAllText(Path.Combine(root,"out.flac"))); batch.Finish();
    }
    [TestMethod]
    public void CleanupPreservesFilesChangedAfterConversion()
    {
        using var batch = new ParallelConversionBatch(1,root);
        string target=Path.Combine(root,"out.flac");
        using(var output=new ParallelAlbumOutput(batch,Path.Combine(root,"x.cue"),root,new[]{target},Array.Empty<string>(),Array.Empty<string>()))
        { File.WriteAllText(Path.Combine(output.StageDirectory,"out.flac"),"encoded");output.Commit(); }
        File.WriteAllText(target,"user changed this");batch.Cancel(true);batch.Finish();
        Assert.AreEqual("user changed this",File.ReadAllText(target));StringAssert.Contains(batch.CleanupError,"changed");
    }
}
