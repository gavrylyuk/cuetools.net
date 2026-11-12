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
    [TestMethod]
    public void StatusLinkShowsVisibleHiddenAndFinishedQueueSafely()
    {
        Exception failure = null;
        var thread = new Thread(() => {
            string dir=Path.Combine(Path.GetTempPath(),"cuetools-link-test-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(dir);
            try {
                using var batch=new ParallelConversionBatch(1,dir);
                using var owner=new Form { ShowInTaskbar=false, StartPosition=FormStartPosition.Manual, Location=new System.Drawing.Point(-32000,-32000) };
                var type=Assembly.Load("CUETools").GetType("JDP.ParallelQueueForm",true);
                using var queue=(Form)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic,null,new object[]{batch,(Action)(()=>{}),(Action<bool>)(_=>{})},null);
                queue.ShowInTaskbar=false; queue.StartPosition=FormStartPosition.Manual; queue.Location=new System.Drawing.Point(-32000,-32000);
                var show=type.GetMethod("ShowOrActivate",BindingFlags.Static|BindingFlags.NonPublic);
                show.Invoke(null,new object[]{queue,owner}); Assert.IsTrue(queue.Visible);
                show.Invoke(null,new object[]{queue,owner}); Assert.IsTrue(queue.Visible);
                queue.WindowState=FormWindowState.Minimized; show.Invoke(null,new object[]{queue,owner}); Assert.AreEqual(FormWindowState.Normal,queue.WindowState);
                queue.Hide(); Assert.IsFalse(queue.Visible); show.Invoke(null,new object[]{queue,owner}); Assert.IsTrue(queue.Visible);
                batch.Finish(); show.Invoke(null,new object[]{queue,owner}); Assert.IsTrue(queue.Visible);
                var color=type.GetMethod("StatusLinkColor",BindingFlags.Static|BindingFlags.NonPublic);
                Assert.AreEqual(System.Drawing.Color.WhiteSmoke,color.Invoke(null,new object[]{true,false}));
                Assert.AreEqual(System.Drawing.Color.Black,color.Invoke(null,new object[]{false,false}));
                Assert.AreEqual(System.Drawing.SystemColors.HotTrack,color.Invoke(null,new object[]{true,true}));
                queue.Close(); Assert.IsTrue(queue.IsDisposed); show.Invoke(null,new object[]{queue,owner});
            } catch(Exception error){failure=error;} finally{Directory.Delete(dir,true);}
        });
        thread.IsBackground=true; thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(30)),"Status link test timed out.");
        if(failure!=null) throw new AssertFailedException("Status link failed",failure);
    }

    [TestMethod]
    public void QueueFollowsNewRowsUnlessUserScrolledUp()
    {
        Exception failure = null;
        var thread = new Thread(() => {
            string dir = Path.Combine(Path.GetTempPath(), "cuetools-scroll-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dir);
            try {
                string path = Path.Combine(dir, "album.cue"); File.WriteAllText(path, "");
                using var batch = new ParallelConversionBatch(4, dir); batch.SetPending(new[] { path });
                var type = Assembly.Load("CUETools").GetType("JDP.ParallelQueueForm", true);
                using var form = (Form)Activator.CreateInstance(type, BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { batch, (Action)(() => { }), (Action<bool>)(_ => { }) }, null);
                form.ShowInTaskbar = false; form.StartPosition = FormStartPosition.Manual; form.Location = new System.Drawing.Point(-32000, -32000); form.Show(); Application.DoEvents();
                var refresh = type.GetMethod("RefreshProgress", BindingFlags.Instance | BindingFlags.NonPublic);
                var grid = (DataGridView)type.GetField("grid", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);
                var album = batch.Album(path);
                for (int i = 0; i < 40; i++) album.Tracks.Add(new ParallelTrackProgress { Output = Path.Combine(dir, i + ".flac"), Stage = "Encoding" });
                refresh.Invoke(form, null);
                Assert.IsTrue(grid.Rows[grid.Rows.Count - 1].Displayed, "New rows should scroll into view.");
                grid.FirstDisplayedScrollingRowIndex = 0;
                album.Tracks.Add(new ParallelTrackProgress { Output = Path.Combine(dir, "next.flac"), Stage = "Queued" });
                refresh.Invoke(form, null);
                Assert.AreEqual(0, grid.FirstDisplayedScrollingRowIndex, "Manual scrolling must not be interrupted.");
                grid.FirstDisplayedScrollingRowIndex = grid.Rows.Count - 1;
                album.Tracks.Add(new ParallelTrackProgress { Output = Path.Combine(dir, "last.flac"), Stage = "Queued" });
                refresh.Invoke(form, null);
                Assert.IsTrue(grid.Rows[grid.Rows.Count - 1].Displayed, "Following resumes when the user returns to the end.");
                form.Hide();
                album.Tracks.Add(new ParallelTrackProgress { Output = Path.Combine(dir, "hidden.flac"), Stage = "Queued" });
                refresh.Invoke(form, null); form.Show(); Application.DoEvents();
                Assert.IsTrue(grid.Rows[grid.Rows.Count - 1].Displayed, "Following continues after reopening the queue.");
                batch.Finish();
            } catch (Exception error) { failure = error; } finally { Directory.Delete(dir, true); }
        });
        thread.IsBackground = true; thread.SetApartmentState(ApartmentState.STA); thread.Start(); Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(20)), "Queue scroll test timed out.");
        if (failure != null) throw new AssertFailedException("Queue scroll failed", failure);
    }
}
