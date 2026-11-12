using System;
using System.IO;
using CUETools.Processor;
using CUETools.Processor.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CUETools.Net10.Tests;
[TestClass]
public class BatchMetadataSettingsTests
{
    [TestMethod]
    public void PromptPolicyPreservesDefaultAndAllowsEveryAlbum()
    {
        var config = new CUEConfig();
        Assert.IsFalse(config.chooseMetadataForEachAlbum);
        Assert.IsTrue(config.ShouldChooseAlbumMetadata(true, true, 0, 0));
        Assert.IsFalse(config.ShouldChooseAlbumMetadata(true, false, 0, 0));
        Assert.IsFalse(config.ShouldChooseAlbumMetadata(true, true, 2, 0));
        Assert.IsFalse(config.ShouldChooseAlbumMetadata(true, true, 1, 1));
        config.chooseMetadataForEachAlbum = true;
        Assert.IsTrue(config.ShouldChooseAlbumMetadata(true, false, 2, 0));
        Assert.IsTrue(config.ShouldChooseAlbumMetadata(true, false, 1, 1));
        Assert.IsFalse(config.ShouldChooseAlbumMetadata(false, true, 2, 0));
        Assert.IsTrue(config.ShouldChooseBatchArtwork(new object[] { new TagLib.Picture() }));
        Assert.IsFalse(config.ShouldChooseBatchArtwork(new object[] { new object() }));
        Assert.IsFalse(config.ShouldChooseBatchArtwork(Array.Empty<object>()));
        config.chooseMetadataForEachAlbum = false;
        Assert.IsFalse(config.ShouldChooseBatchArtwork(new object[] { new TagLib.Picture() }));
        config.chooseMetadataForEachAlbum = true;
        Assert.IsTrue(new CUEConfig(config).chooseMetadataForEachAlbum);
    }
    [TestMethod]
    public void OptionPersistsAndDefaultsOffForOldSettings()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cuetools-batch-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string app = Path.Combine(dir, "app.exe");
            var config = new CUEConfig { chooseMetadataForEachAlbum = true };
            var writer = new SettingsWriter("Test", "settings.txt", app); config.Save(writer); writer.Close();
            var loaded = new CUEConfig(); loaded.Load(new SettingsReader("Test", "settings.txt", app));
            Assert.IsTrue(loaded.chooseMetadataForEachAlbum);
            writer = new SettingsWriter("Test", "settings.txt", app); writer.Close();
            loaded.Load(new SettingsReader("Test", "settings.txt", app));
            Assert.IsFalse(loaded.chooseMetadataForEachAlbum);
        }
        finally { Directory.Delete(dir, true); }
    }
}
