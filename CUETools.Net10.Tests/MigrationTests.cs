using System;
using System.Data;
using System.IO;
using CUEPlayer;
using CUEPlayer.DataSet1TableAdapters;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;

[TestClass]
[DoNotParallelize]
public class MigrationTests
{
    [TestMethod]
    public void PlaylistPersistsUnicodeOrderEditsAndDeletes()
    {
        string folder = Path.Combine(Path.GetTempPath(), "CUETools-tests-" + Guid.NewGuid());
        try
        {
            var store = new PlaylistTableAdapter(Path.Combine(folder, "playlist.sqlite"));
            var original = new DataSet1.PlaylistDataTable();
            Assert.AreEqual(0, store.Fill(original));
            original.AddPlaylistRow(@"C:\音楽\first.flac", "Łódź", "First", "Album", 125, 1);
            original.AddPlaylistRow(@"C:\music\second.flac", "Artist", "Second", "Album", 98, 2);
            Assert.AreEqual(2, store.Update(original));
            var reopened = new DataSet1.PlaylistDataTable();
            Assert.AreEqual(2, new PlaylistTableAdapter(Path.Combine(folder, "playlist.sqlite")).Fill(reopened));
            Assert.AreEqual(@"C:\音楽\first.flac", reopened[0].path);
            Assert.AreEqual("Łódź", reopened[0].artist);
            Assert.AreEqual("Second", reopened[1].title);
            reopened[0].title = "Updated";
            reopened[1].Delete();
            Assert.AreEqual(2, store.Update(reopened));
            var final = new DataSet1.PlaylistDataTable();
            Assert.AreEqual(1, store.Fill(final));
            Assert.AreEqual("Updated", final[0].title);
            Assert.AreEqual(0, store.Update(final));
            // New auto-generated row IDs must not collide with persisted IDs.
            final.AddPlaylistRow("third.wav", "", "Third", "", 15, 3);
            Assert.AreEqual(1, store.Update(final));
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [TestMethod]
    public void PlaylistConcurrentAdditionsDoNotOverwriteOtherInstances()
    {
        string folder = Path.Combine(Path.GetTempPath(), "CUETools-tests-" + Guid.NewGuid());
        try
        {
            var store = new PlaylistTableAdapter(Path.Combine(folder, "playlist.sqlite"));
            var first = new DataSet1.PlaylistDataTable();
            var second = new DataSet1.PlaylistDataTable();
            store.Fill(first);
            store.Fill(second);
            first.AddPlaylistRow("first.wav", "", "First", "", 10, 1);
            second.AddPlaylistRow("second.wav", "", "Second", "", 20, 2);
            store.Update(first);
            store.Update(second);
            var merged = new DataSet1.PlaylistDataTable();
            Assert.AreEqual(2, store.Fill(merged));
            Assert.AreEqual("First", merged[0].title);
            Assert.AreEqual("Second", merged[1].title);
            Assert.AreNotEqual(merged[0].id, merged[1].id);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

    [TestMethod]
    public void PlaylistFailedSaveRollsBackAndRetainsPendingChanges()
    {
        string folder = Path.Combine(Path.GetTempPath(), "CUETools-tests-" + Guid.NewGuid());
        string database = Path.Combine(folder, "playlist.sqlite");
        try
        {
            var store = new PlaylistTableAdapter(database);
            var table = new DataSet1.PlaylistDataTable();
            table.AddPlaylistRow("original.wav", "", "Original", "", 3, 1);
            store.Update(table);
            using (var connection = new SqliteConnection("Data Source=" + database + ";Pooling=False"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = "CREATE TRIGGER fail_save BEFORE UPDATE ON Playlist BEGIN SELECT RAISE(ABORT, 'test save failure'); END";
                command.ExecuteNonQuery();
            }
            table[0].title = "Unsaved";
            Assert.Throws<SqliteException>(() => store.Update(table));
            Assert.AreEqual(DataRowState.Modified, table[0].RowState);
            var restored = new DataSet1.PlaylistDataTable();
            store.Fill(restored);
            Assert.AreEqual("Original", restored[0].title);
        }
        finally { if (Directory.Exists(folder)) Directory.Delete(folder, true); }
    }

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
