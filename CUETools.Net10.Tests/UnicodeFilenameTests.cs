using System;
using System.IO;
using CUETools.Processor;
using CUETools.Processor.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CUETools.Net10.Tests;
[TestClass]
public class UnicodeFilenameTests
{
    [TestMethod]
    public void DefaultPreservesJapaneseCyrillicAndAccentedLatin()
    {
        var config = new CUEConfig();
        Assert.IsFalse(config.filenamesANSISafe);
        const string title = "無何有の郷 - Привет - Łódź - café";
        Assert.AreEqual(title, config.CleanseString(title));
        Assert.AreEqual("無何有の郷_Привет", config.CleanseString("無何有の郷/Привет"));
    }
    [TestMethod]
    public void LegacyAnsiPreferenceIsPreservedWhenExplicitlySaved()
    {
        string dir=Path.Combine(Path.GetTempPath(),"cuetools-unicode-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
        try {
            string app=Path.Combine(dir,"app.exe"); var writer=new SettingsWriter("Test","settings.txt",app);
            writer.Save("FilenamesANSISafe",true); writer.Close();
            var config=new CUEConfig();config.Load(new SettingsReader("Test","settings.txt",app));
            Assert.IsTrue(config.filenamesANSISafe);
            writer=new SettingsWriter("Test","settings.txt",app);writer.Close();
            config.Load(new SettingsReader("Test","settings.txt",app));Assert.IsFalse(config.filenamesANSISafe);
        } finally { Directory.Delete(dir,true); }
    }
}
