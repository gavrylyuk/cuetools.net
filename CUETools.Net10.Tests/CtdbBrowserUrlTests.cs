using System;
using CUETools.CTDB;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;

[TestClass]
public class CtdbBrowserUrlTests
{
    private const string TocId = "kuR3Zz3fqRgGyusTggG4IBZ7w6U-";

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("db.cuetools.net")]
    [DataRow(" DB.CUETOOLS.NET/ ")]
    [DataRow("http://db.cuetools.net")]
    [DataRow("https://db.cuetools.net/")]
    [DataRow("db.cue.tools")]
    public void PublicApiServersUseCurrentHttpsBrowserUi(string server)
        => Assert.AreEqual("https://db.cue.tools/ui/?tocid=" + TocId, CUEToolsDB.GetBrowserUrl(server, TocId));

    [TestMethod]
    [DataRow("localhost:8080", "http://localhost:8080/?tocid=")]
    [DataRow("https://ctdb.example.com", "https://ctdb.example.com/?tocid=")]
    [DataRow("http://ctdb.example.com/catalog", "http://ctdb.example.com/catalog/?tocid=")]
    public void KeepsCustomServerSchemePortAndPath(string server, string expected)
        => Assert.AreEqual(expected + TocId, CUEToolsDB.GetBrowserUrl(server, TocId));

    [TestMethod]
    public void EscapesQueryCharactersInDiscId()
        => Assert.AreEqual("https://db.cue.tools/ui/?tocid=a%26artist%3Dother", CUEToolsDB.GetBrowserUrl(null, "a&artist=other"));

    [TestMethod]
    public void RejectsNonWebServerAddresses()
        => Assert.Throws<ArgumentException>(() => CUEToolsDB.GetBrowserUrl("file:///C:/Windows", TocId));
}
