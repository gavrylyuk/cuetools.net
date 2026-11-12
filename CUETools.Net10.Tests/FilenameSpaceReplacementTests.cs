using System;
using System.IO;
using CUETools.Processor;
using CUETools.Processor.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CUETools.Net10.Tests;
[TestClass]
public class FilenameSpaceReplacementTests
{
    [TestMethod]
    public void SpaceOptionReplacesInvalidCharactersAndPreservesSymbol()
    {
        var config = new CUEConfig();
        Assert.IsFalse(config.replaceInvalidWithSpace);
        config.invalidFilenameReplacement = ",";
        Assert.AreEqual("Six, eight", config.CleanseString("Six/ eight"));
        config.invalidFilenameReplacement = " - ";
        Assert.AreEqual("Artist - Title", config.CleanseString("Artist/Title"));
        config.invalidFilenameReplacement = ",";
        config.replaceInvalidWithSpace = true;
        Assert.AreEqual("Six  eight", config.CleanseString("Six/ eight"));
        config.replaceSpaces = true;
        Assert.AreEqual("Six _eight", config.CleanseString("Six/ eight"));
        var copy = new CUEConfig(config);
        Assert.IsTrue(copy.replaceInvalidWithSpace);
        Assert.AreEqual(",", copy.invalidFilenameReplacement);
        copy.replaceInvalidWithSpace = false;
        Assert.AreEqual("Six,_eight", copy.CleanseString("Six/ eight"));
    }
    [TestMethod]
    public void SpaceOptionPersistsAndOldSettingsDefaultToSymbol()
    {
        string dir = Path.Combine(Path.GetTempPath(), "cuetools-space-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            string app = Path.Combine(dir, "app.exe");
            var config = new CUEConfig { invalidFilenameReplacement = " - ", replaceInvalidWithSpace = true };
            var writer = new SettingsWriter("Test", "settings.txt", app); config.Save(writer); writer.Close();
            var loaded = new CUEConfig(); loaded.Load(new SettingsReader("Test", "settings.txt", app));
            Assert.IsTrue(loaded.replaceInvalidWithSpace);
            Assert.AreEqual(" - ", loaded.invalidFilenameReplacement);
            Assert.AreEqual("a b", loaded.CleanseString("a/b"));
            loaded.replaceInvalidWithSpace = false;
            Assert.AreEqual("a - b", loaded.CleanseString("a/b"));
            writer = new SettingsWriter("Test", "settings.txt", app); writer.Save("InvalidFilenameReplacement", ","); writer.Close();
            loaded.Load(new SettingsReader("Test", "settings.txt", app));
            Assert.IsFalse(loaded.replaceInvalidWithSpace);
            Assert.AreEqual("a,b", loaded.CleanseString("a/b"));
        }
        finally { Directory.Delete(dir, true); }
    }
}
