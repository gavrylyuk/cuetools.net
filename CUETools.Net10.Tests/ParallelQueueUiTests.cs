using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using CUETools.Processor;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;
[TestClass]
public class ParallelQueueUiTests
{
    [TestMethod]
    public void TracksAreInsertedUnderTheCorrectAlbumAsTheQueueGrows()
    {
        Exception failure = null;
        var thread = new Thread(() => {
            string dir=Path.Combine(Path.GetTempPath(),"cuetools-queue-test-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try {
                string a=Path.Combine(dir,"CD1","album.cue"), b=Path.Combine(dir,"CD2","album.cue");
                Directory.CreateDirectory(Path.GetDirectoryName(a)); Directory.CreateDirectory(Path.GetDirectoryName(b)); File.WriteAllText(a,""); File.WriteAllText(b,"");
                using var batch=new ParallelConversionBatch(2,dir); batch.SetPending(new[]{a,b});
                var type=Assembly.Load("CUETools").GetType("JDP.ParallelQueueForm",true);
                using var form=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{batch,(Action)(()=>{}),(Action<bool>)(_=>{})},null);
                var refresh=type.GetMethod("RefreshProgress",BindingFlags.Instance|BindingFlags.NonPublic);
                var grid=(DataGridView)type.GetField("grid",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
                var first=batch.Album(a); first.Tracks.Add(new ParallelTrackProgress{Output=Path.Combine(dir,"one.flac"),Stage="Encoding"});
                refresh.Invoke(form,null);
                Assert.AreEqual(3,grid.Rows.Count); Assert.AreEqual("CD1 · album.cue",grid.Rows[0].Cells[0].Value); Assert.AreEqual("CD2 · album.cue",grid.Rows[2].Cells[0].Value); Assert.AreEqual("    one.flac",grid.Rows[1].Cells[0].Value);
                first.Tracks.Add(new ParallelTrackProgress{Output=Path.Combine(dir,"two.flac"),Stage="Queued"});
                batch.Album(b).Tracks.Add(new ParallelTrackProgress{Output=Path.Combine(dir,"three.flac"),Stage="Queued"});
                refresh.Invoke(form,null); refresh.Invoke(form,null);
                Assert.AreEqual(5,grid.Rows.Count); Assert.AreEqual("    two.flac",grid.Rows[2].Cells[0].Value); Assert.AreEqual("    three.flac",grid.Rows[4].Cells[0].Value);
                batch.Finish();
            } catch(Exception error) { failure=error; } finally { Directory.Delete(dir,true); }
        });
        thread.IsBackground=true; thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)),"Queue test timed out.");
        if(failure!=null) throw new AssertFailedException("Queue update failed",failure);
    }
}
