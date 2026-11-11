#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("CUETools.Net10.Tests")]

namespace CUEControls
{
    internal sealed class NetworkLocationsStore
    {
        private readonly string filename;
        internal NetworkLocationsStore(string filename) { this.filename = filename; }

        internal static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Enter a network path such as \\\\NAS\\Music.");
            path = path.Trim().Replace('/', '\\');
            if (!path.StartsWith(@"\\") || path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\"))
                throw new ArgumentException("Use a UNC network path such as \\\\NAS\\Music.");
            var parts = path.Substring(2).Split(new[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0 || parts.Any(part => part == "." || part == ".." || part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0))
                throw new ArgumentException("The network path contains an invalid server, share or folder name.");
            return @"\\" + string.Join(@"\", parts);
        }

        internal string[] Load()
        {
            var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!File.Exists(filename)) return paths.ToArray();
            foreach (string line in File.ReadAllLines(filename))
                try { paths.Add(Normalize(line)); } catch (ArgumentException) { }
            return paths.ToArray();
        }

        internal void Add(string path)
        {
            var paths = new SortedSet<string>(Load(), StringComparer.OrdinalIgnoreCase) { Normalize(path) };
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filename));
            File.WriteAllLines(filename, paths);
        }
    }

    internal sealed class NetworkActionNode : TreeNode
    {
        internal readonly Action Action;
        internal NetworkActionNode(string text, Action action) : base(text) { Action = action; }
    }

    internal static class NetworkBrowser
    {
        internal static readonly NetworkLocationsStore Store = new NetworkLocationsStore(
            System.IO.Path.Combine(File.Exists(System.IO.Path.Combine(Application.StartupPath, "user_profiles_enabled"))
                ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) : Application.StartupPath,
                "CUE Tools", "network-locations.txt"));

        // Shell automation must run on an STA thread. Only plain path strings cross back to the UI.
        internal static string[] Discover(string server)
        {
            var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            object shell = null, folder = null, items = null;
            try
            {
                shell = Activator.CreateInstance(Type.GetTypeFromProgID("Shell.Application", true));
                folder = ((dynamic)shell).NameSpace(server == null ? (object)18 : server);
                if (folder == null) return paths.ToArray();
                items = ((dynamic)folder).Items();
                int count = ((dynamic)items).Count;
                for (int i = 0; i < count; i++)
                {
                    object item = ((dynamic)items).Item(i);
                    try
                    {
                        string path = ((dynamic)item).Path;
                        if (path != null && path.StartsWith(@"\\"))
                        {
                            path = NetworkLocationsStore.Normalize(path);
                            if (server == null || path.Substring(2).Contains('\\')) paths.Add(path);
                        }
                    }
                    catch (ArgumentException) { }
                    finally { Release(item); }
                }
            }
            finally { Release(items); Release(folder); Release(shell); }
            return paths.ToArray();
        }

        private static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }

        internal static void Populate(FileSystemTreeNode node, Func<Action> load)
        {
            var tree = node.TreeView as FileSystemTreeView;
            if (tree == null || tree.IsDisposed) return;
            var loading = new TreeNode("Loading network locations...") { ImageIndex = node.DisplayIcon, SelectedImageIndex = node.DisplayIcon };
            node.Nodes.Add(loading);
            var thread = new Thread(() =>
            {
                Action complete;
                try { complete = load(); }
                catch (Exception error)
                {
                    string message = error.Message;
                    complete = () => node.Nodes.Add(new TreeNode("Unavailable: " + message));
                }
                try
                {
                    if (!tree.IsDisposed && tree.IsHandleCreated) tree.BeginInvoke((Action)(() =>
                    {
                        // Collapsing/refreshing a node invalidates its old request.
                        if (node.TreeView != tree || !node.Nodes.Contains(loading)) return;
                        tree.BeginUpdate();
                        try
                        {
                            node.Nodes.Remove(loading);
                            var existing = new HashSet<TreeNode>(node.Nodes.Cast<TreeNode>());
                            complete();
                            if (tree.CheckBoxes)
                                foreach (TreeNode child in node.Nodes)
                                    if (!existing.Contains(child)) child.Checked = node.Checked;
                        }
                        catch (Exception error) { node.Nodes.Add(new TreeNode("Unavailable: " + error.Message)); }
                        finally { tree.EndUpdate(); }
                    }));
                }
                catch (InvalidOperationException) { }
            });
            thread.IsBackground = true;
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        internal static void ExpandFolder(FileSystemTreeNode node)
        {
            Populate(node, () =>
            {
                var files = new DirectoryInfo(node.Path).GetFileSystemInfos();
                return () => ((FileSystemTreeView)node.TreeView).OnDoExpand(new FileSystemTreeViewNodeExpandEventArgs { node = node, files = files });
            });
        }
    }

    internal sealed class FileSystemTreeNodeNetworkFolder : FileSystemTreeNodeFileSystemInfo
    {
        internal FileSystemTreeNodeNetworkFolder(IIconManager icons, string path) : base(icons, new DirectoryInfo(path)) { }
        public override string DisplayName { get { return File.Name; } }
        public override int DisplayIcon { get { return icon_mgr.GetIconIndex(ExtraSpecialFolder.MyDocuments, false); } }
    }

    internal sealed class FileSystemTreeNodeNetwork : FileSystemTreeNode
    {
        private readonly string server;
        internal bool IsRoot { get { return server == null; } }
        public override string Path { get { return null; } }
        public override string DisplayName { get { return server ?? icon_mgr.GetDisplayName(ExtraSpecialFolder.Network); } }
        public override int DisplayIcon { get { return icon_mgr.GetIconIndex(ExtraSpecialFolder.Network, false); } }

        internal FileSystemTreeNodeNetwork(IIconManager icons, string server = null) : base(icons, true)
        {
            this.server = server;
            Text = DisplayName;
            SelectedImageIndex = ImageIndex = DisplayIcon;
        }

        internal FileSystemTreeNode AddLocation(string path)
        {
            path = NetworkLocationsStore.Normalize(path);
            foreach (TreeNode existing in Nodes)
                if (existing.Name.Equals(path, StringComparison.OrdinalIgnoreCase)) return (FileSystemTreeNode)existing;
            FileSystemTreeNode child = path.Substring(2).Contains('\\')
                ? (FileSystemTreeNode)new FileSystemTreeNodeNetworkFolder(icon_mgr, path)
                : new FileSystemTreeNodeNetwork(icon_mgr, path);
            child.Name = path;
                        string[] parts = path.Substring(2).Split('\\');
            child.Text = parts.Length > 2 ? parts[parts.Length - 1] + " (" + parts[0] + @"\" + parts[1] + ")" : path;
            child.ToolTipText = path;
            child.Checked = Checked;
            Nodes.Add(child);
            return child;
        }

        public override void DoExpand()
        {
            if (server == null)
            {
                Nodes.Add(new NetworkActionNode("Add location...", ShowLocationDialog) { ImageIndex = DisplayIcon, SelectedImageIndex = DisplayIcon });
                try { foreach (string path in NetworkBrowser.Store.Load()) AddLocation(path); }
                catch (IOException error) { Nodes.Add(new TreeNode("Saved locations unavailable: " + error.Message)); }
                catch (UnauthorizedAccessException error) { Nodes.Add(new TreeNode("Saved locations unavailable: " + error.Message)); }
            }
            Nodes.Add(new NetworkActionNode("Refresh", () => { Collapse(); Expand(); }) { ImageIndex = DisplayIcon, SelectedImageIndex = DisplayIcon });
            NetworkBrowser.Populate(this, () =>
            {
                string[] paths = NetworkBrowser.Discover(server);
                return () =>
                {
                    foreach (string path in paths) AddLocation(path);
                    if (paths.Length == 0) Nodes.Add(new TreeNode(server == null
                        ? "No computers discovered. Add a network location above."
                        : "No shared folders found. Add a full share path under Network."));
                };
            });
        }

        private void ShowLocationDialog()
        {
            using (var dialog = new Form())
            using (var textbox = new TextBox())
            using (var label = new Label())
            using (var add = new Button())
            using (var cancel = new Button())
            {
                dialog.Text = "Add network location";
                dialog.ClientSize = new System.Drawing.Size(460, 135);
                dialog.FormBorderStyle = FormBorderStyle.FixedDialog;
                dialog.StartPosition = FormStartPosition.CenterParent;
                dialog.MinimizeBox = dialog.MaximizeBox = false;
                label.Text = "Network path (for example \\\\NAS\\Music).\r\nUses your Windows network credentials.";
                label.SetBounds(12, 12, 436, 40);
                textbox.SetBounds(12, 56, 436, 24);
                textbox.Text = @"\\";
                add.Text = "Add"; add.SetBounds(282, 96, 78, 27);
                cancel.Text = "Cancel"; cancel.SetBounds(370, 96, 78, 27); cancel.DialogResult = DialogResult.Cancel;
                dialog.Controls.AddRange(new Control[] { label, textbox, add, cancel });
                dialog.AcceptButton = add; dialog.CancelButton = cancel;
                add.Click += (sender, args) =>
                {
                    try
                    {
                        string path = NetworkLocationsStore.Normalize(textbox.Text);
                        NetworkBrowser.Store.Add(path);
                        var child = AddLocation(path);
                        TreeView.SelectedNode = child;
                        child.EnsureVisible();
                        dialog.DialogResult = DialogResult.OK;
                    }
                    catch (Exception error) when (error is ArgumentException || error is IOException || error is UnauthorizedAccessException)
                    { MessageBox.Show(dialog, error.Message, "Cannot add network location", MessageBoxButtons.OK, MessageBoxIcon.Warning); }
                };
                textbox.Select(textbox.Text.Length, 0);
                dialog.ShowDialog(TreeView.FindForm());
            }
        }
    }
}
#endif
