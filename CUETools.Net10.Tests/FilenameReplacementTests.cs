using System;
using System.IO;
using CUETools.Processor;
using CUETools.Processor.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace CUETools.Net10.Tests;
[TestClass] public class FilenameReplacementTests
{
    [TestMethod] public void ReplacementDefaultsToUnderscoreAndCanBeSavedAsComma()
    {
        string dir=Path.Combine(Path.GetTempPath(),"cuetools-replacement-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
        try {
            var config=new CUEConfig();Assert.AreEqual("_",config.invalidFilenameReplacement);Assert.AreEqual("6_8 - 7_8",config.CleanseString("6/8 - 7/8"));
            config.invalidFilenameReplacement=",";Assert.AreEqual("6,8 - 7,8",config.CleanseString("6/8 - 7/8"));Assert.AreEqual(",",new CUEConfig(config).invalidFilenameReplacement);
            string appPath=Path.Combine(dir,"app.exe");var writer=new SettingsWriter("Test","settings.txt",appPath);config.Save(writer);writer.Close();
            var loaded=new CUEConfig();loaded.Load(new SettingsReader("Test","settings.txt",appPath));Assert.AreEqual(",",loaded.invalidFilenameReplacement);
            writer=new SettingsWriter("Test","settings.txt",appPath);writer.Save("InvalidFilenameReplacement","/");writer.Close();loaded.Load(new SettingsReader("Test","settings.txt",appPath));Assert.AreEqual("_",loaded.invalidFilenameReplacement);
            foreach(var bad in new[]{"", "/", "?", ".", " ", "é", new string('a', 33)}) Assert.IsFalse(CUEConfig.IsValidFilenameReplacement(bad),bad);
            config.replaceSpaces=true;Assert.AreEqual("6,8_-_7,8",config.CleanseString("6/8 - 7/8"));
            config.replaceSpaces=false;config.removeSpecial=true;config.specialExceptions="-()";Assert.AreEqual("6,8 - 7,8",config.CleanseString("6/8 - 7/8"));
        } finally { Directory.Delete(dir,true); }
    }
}
