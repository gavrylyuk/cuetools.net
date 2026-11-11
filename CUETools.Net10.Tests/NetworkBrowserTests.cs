using System;
using System.IO;
using System.Linq;
using CUEControls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;

[TestClass]
public class NetworkBrowserTests
{
    [TestMethod]
    [DataRow(@"\\NAS", @"\\NAS")]
    [DataRow(@" \\NAS\Music\ ", @"\\NAS\Music")]
    [DataRow(@"//nas/Music/Album", @"\\nas\Music\Album")]
    [DataRow(@"\\nas\音楽\Album 1", @"\\nas\音楽\Album 1")]
    public void AcceptsServerShareAndUnicodeFolderPaths(string input, string expected)
        => Assert.AreEqual(expected, NetworkLocationsStore.Normalize(input));

    [TestMethod]
    [DataRow("")]
    [DataRow(@"C:\Music")]
    [DataRow(@"\\")]
    [DataRow(@"\\?\C:\Music")]
    [DataRow(@"\\.\pipe\name")]
    [DataRow(@"\\NAS\Music\..\Private")]
    [DataRow(@"\\NAS\invalid:share")]
    [DataRow("\\\\NAS\\bad\nshare")]
    public void RejectsLocalDeviceAndInvalidPaths(string input)
        => Assert.Throws<ArgumentException>(() => NetworkLocationsStore.Normalize(input));

    [TestMethod]
    public void SavedLocationsSurviveReloadAndIgnoreCaseDuplicatesAndInvalidLines()
    {
        string folder = Path.Combine(Path.GetTempPath(), "CUETools-network-" + Guid.NewGuid());
        try
        {
            string filename = Path.Combine(folder, "locations.txt");
            var store = new NetworkLocationsStore(filename);
            Assert.AreEqual(0, store.Load().Length);
            store.Add(@"\\NAS\Music\");
            store.Add(@"\\nas\music");
            store.Add(@"\\Other\音楽");
            File.AppendAllLines(filename, new[] { @"C:\not-a-network-location", @"\\?\C:\invalid", "" });
            var restored = new NetworkLocationsStore(filename).Load();
            Assert.AreEqual(2, restored.Length);
            Assert.IsTrue(restored.Contains(@"\\NAS\Music", StringComparer.OrdinalIgnoreCase));
            Assert.IsTrue(restored.Contains(@"\\Other\音楽", StringComparer.OrdinalIgnoreCase));
            Assert.Throws<ArgumentException>(() => store.Add(@"C:\invalid"));
            Assert.AreEqual(2, store.Load().Length);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }
}
