using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using CUEControls;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace CUETools.Net10.Tests;

[TestClass]
public class NetworkLoadingTests
{
    private sealed class TestNode : FileSystemTreeNode
    {
        internal TestNode() : base(null, false) { }
        public override string Path => null;
        public override string DisplayName => "Test";
        public override int DisplayIcon => -1;
    }

    private static void OnSta(Action test)
    {
        Exception failure = null;
        var thread = new Thread(() => { try { test(); } catch (Exception error) { failure = error; } });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        Assert.IsTrue(thread.Join(TimeSpan.FromSeconds(10)), "UI test timed out.");
        if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    private static void PumpUntil(Func<bool> condition)
    {
        var timer = Stopwatch.StartNew();
        while (!condition() && timer.Elapsed < TimeSpan.FromSeconds(5))
        {
            Application.DoEvents();
            Thread.Sleep(5);
        }
        Assert.IsTrue(condition(), "Background completion was not delivered.");
    }

    [TestMethod]
    public void BackgroundLoadPreservesExistingSelectionsAndChecksNewChildren()
    {
        OnSta(() =>
        {
            using var tree = new FileSystemTreeView { CheckBoxes = true };
            _ = tree.Handle;
            var parent = new TestNode { Checked = true };
            tree.Nodes.Add(parent);
            var existing = new TreeNode("Existing") { Checked = false };
            parent.Nodes.Add(existing);
            int uiThread = Environment.CurrentManagedThreadId, workerThread = uiThread;
            var added = new TreeNode("New child");
            bool completed = false;
            NetworkBrowser.Populate(parent, () =>
            {
                workerThread = Environment.CurrentManagedThreadId;
                return () => { Assert.AreEqual(uiThread, Environment.CurrentManagedThreadId); parent.Nodes.Add(added); completed = true; };
            });
            PumpUntil(() => completed);
            Assert.AreNotEqual(uiThread, workerThread);
            Assert.IsFalse(existing.Checked);
            Assert.IsTrue(added.Checked);
        });
    }

    [TestMethod]
    public void RefreshDiscardsAnOlderBackgroundResult()
    {
        OnSta(() =>
        {
            using var tree = new FileSystemTreeView();
            _ = tree.Handle;
            var parent = new TestNode();
            tree.Nodes.Add(parent);
            using var release = new ManualResetEventSlim();
            using var started = new ManualResetEventSlim();
            using var returned = new ManualResetEventSlim();
            bool applied = false;
            NetworkBrowser.Populate(parent, () =>
            {
                started.Set();
                release.Wait(TimeSpan.FromSeconds(3));
                returned.Set();
                return () => applied = true;
            });
            Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(3)));
            parent.Nodes.Clear();
            parent.Nodes.Add(new TreeNode("Refreshed"));
            release.Set();
            Assert.IsTrue(returned.Wait(TimeSpan.FromSeconds(3)));
            // Queue a second completion to prove the UI has drained the stale callback too.
            bool fresh = false;
            NetworkBrowser.Populate(parent, () => () => fresh = true);
            PumpUntil(() => fresh);
            Assert.IsFalse(applied);
            Assert.AreEqual("Refreshed", parent.Nodes[0].Text);
        });
    }

    [TestMethod]
    public void OfflineFolderIsShownAsAnErrorInsteadOfCrashing()
    {
        OnSta(() =>
        {
            using var tree = new FileSystemTreeView();
            _ = tree.Handle;
            var parent = new TestNode();
            tree.Nodes.Add(parent);
            NetworkBrowser.Populate(parent, () => throw new IOException("Network path unavailable"));
            PumpUntil(() => parent.Nodes.Count == 1 && parent.Nodes[0].Text.StartsWith("Unavailable:"));
            StringAssert.Contains(parent.Nodes[0].Text, "Network path unavailable");
        });
    }
}
