#if NET10_0_OR_GREATER
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using CUETools.Codecs;
using CUETools.Processor;

namespace JDP
{
    public partial class frmCUETools
    {
        private ParallelConversionBatch parallelBatch;
        private ParallelQueueForm parallelQueue;
        private string parallelInput;
        private bool parallelStatusHooked;
        private bool parallelCloseRequested;

        private bool ConfigureParallelConversion(string input)
        {
            if (parallelBatch != null && parallelBatch.Finished)
            {
                var previous = parallelBatch;
                parallelQueue?.Close();
                if (parallelBatch == previous) { previous.Dispose(); parallelBatch = null; parallelQueue = null; }
            }
            if (parallelBatch != null && !parallelBatch.Finished)
            {
                parallelBatch.SetPending(_batchPaths.Count == 0 ? new[] { input } : _batchPaths);
                parallelInput = input; return true;
            }
            var encoder = _profile._config.formats.ContainsKey("flac") ? _profile._config.formats["flac"].encoderLossless : null;
            if (IsCDROM(input) || SelectedAction != CUEAction.Encode || SelectedOutputAudioType != AudioEncoderType.Lossless || SelectedOutputAudioFormat != "flac" ||
                SelectedCUEStyle == CUEStyle.SingleFile || SelectedCUEStyle == CUEStyle.SingleFileWithCUE ||
                !(encoder?.Settings is CUETools.Codecs.CommandLine.EncoderSettings) ||
                (comboBoxScript.SelectedItem is CUEToolsScript script && script.name != "default")) return true;
            using var setup = new Form { Text = "Experimental parallel FLAC conversion", ClientSize = new Size(570, 245), FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent, MinimizeBox = false, MaximizeBox = false };
            var label = new Label { Text = "One album at a time; several external FLAC encoders.\r\nDecoding, gaps, HDCD and disc verification retain their existing order.\r\nTemporary WAV tracks use local disk space, with at most workers + 1 tracks staged.\r\nOutputs are published only after encoding, PCM checking and tagging succeed.", AutoSize = false };
            label.SetBounds(14, 12, 540, 83); setup.Controls.Add(label);
            var countLabel = new Label { Text = "Concurrent encoders", AutoSize = true }; countLabel.SetBounds(14, 105, 160, 23); setup.Controls.Add(countLabel);
            var count = new NumericUpDown { Minimum = 1, Maximum = 16, Value = Math.Min(2, Environment.ProcessorCount) }; count.SetBounds(184, 101, 70, 25); setup.Controls.Add(count);
            var tempLabel = new Label { Text = "Local temporary folder", AutoSize = true }; tempLabel.SetBounds(14, 140, 160, 23); setup.Controls.Add(tempLabel);
            var temp = new TextBox { Text = Path.Combine(Path.GetTempPath(), "CUETools-parallel") }; temp.SetBounds(184, 137, 370, 25); setup.Controls.Add(temp);
            var start = new Button { Text = "Start parallel", DialogResult = DialogResult.Yes }; start.SetBounds(184, 191, 115, 32);
            var original = new Button { Text = "Original mode", DialogResult = DialogResult.No }; original.SetBounds(310, 191, 115, 32);
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel }; cancel.SetBounds(436, 191, 115, 32);
            setup.Controls.AddRange(new Control[] { start, original, cancel }); setup.AcceptButton = start; setup.CancelButton = cancel;
            using var setupTips = new ToolTip(); UiHelp.Attach(setup, setupTips);
            setupTips.SetToolTip(count, "Maximum number of simultaneous track encoding and checking workers."); setupTips.SetToolTip(temp, "Local folder for temporary uncompressed track files.");
            setupTips.SetToolTip(start, "Start the experimental parallel conversion."); setupTips.SetToolTip(original, "Use the original conversion pipeline."); setupTips.SetToolTip(cancel, "Close without starting a conversion.");
            var result = setup.ShowDialog(this);
            if (result == DialogResult.Cancel) return false;
            if (result == DialogResult.No) return true;

            parallelBatch = new ParallelConversionBatch((int)count.Value, temp.Text);
            parallelBatch.SetPending(_batchPaths.Count == 0 ? new[] { input } : _batchPaths); parallelInput = input;
            parallelQueue = new ParallelQueueForm(parallelBatch, UpdateParallelStatus,
                delete => { parallelBatch.Cancel(delete); _workClass?.Stop(); });
            parallelQueue.FormClosed += (sender, args) =>
            {
                if (!parallelBatch.Finished) return;
                try { parallelBatch.Dispose(); } catch (Exception error) { MessageBox.Show(this, error.Message, "Backup cleanup"); }
                parallelBatch = null; parallelQueue = null; toolStripStatusLabel1.IsLink = false;
            };
            if (!parallelStatusHooked)
            {
                parallelStatusHooked = true;
                FormClosing += (sender, args) =>
                {
                    if (parallelBatch != null && !parallelBatch.Finished)
                    {
                        args.Cancel = true; parallelCloseRequested = true;
                        parallelBatch.Cancel(false); _workClass?.Stop();
                        ShowParallelQueue();
                    }
                };
                toolStripStatusLabel1.Click += (sender, args) => ShowParallelQueue();
            }
            bool dark = Application.ColorMode == SystemColorMode.Dark || (Application.ColorMode == SystemColorMode.System && Application.SystemColorMode == SystemColorMode.Dark);
            var linkColor = ParallelQueueForm.StatusLinkColor(dark, SystemInformation.HighContrast);
            toolStripStatusLabel1.LinkColor = toolStripStatusLabel1.ActiveLinkColor = toolStripStatusLabel1.VisitedLinkColor = linkColor;
            toolStripStatusLabel1.IsLink = true;
            return true;
        }
        private void ShowParallelQueue()
        {
            if (parallelQueue == null || parallelQueue.IsDisposed) return;
            ParallelQueueForm.ShowOrActivate(parallelQueue, this);
        }
        private string RunParallelAlbum(CUESheet cue, string input, string output, string[] overwriteApproved, AudioEncoderType type, string format)
        {
            string directory = Path.GetDirectoryName(Path.GetFullPath(output));
            using var staged = new ParallelAlbumOutput(parallelBatch, input, directory, cue.DestPaths.Append(output), cue.SourcePaths, overwriteApproved);
            string stagedOutput = Path.Combine(staged.StageDirectory, Path.GetFileName(output));
            cue.GenerateFilenames(type, format, stagedOutput); staged.ValidatePaths(cue.DestPaths.Append(stagedOutput));
            using var session = new ParallelFlacSession(parallelBatch, cue.Config, input, (long)cue.TOC.AudioLength * 588, staged.StageDirectory, directory);
            cue.ParallelSession = session;
            try
            {
                string status = cue.Go(); session.Checkpoint(); staged.Commit(); session.MarkCommitted(); return status;
            }
            catch (Exception error) { session.MarkFailed(error); throw; }
            finally { cue.ParallelSession = null; cue.GenerateFilenames(type, format, output); }
        }
        private void UpdateParallelStatus()
        {
            if (parallelBatch == null) return;
            if (parallelBatch.Finished && parallelCloseRequested) { parallelCloseRequested = false; BeginInvoke((Action)Close); }
            var albums = parallelBatch.Snapshot(); var tracks = albums.SelectMany(a => a.Tracks).ToList();
            double? fraction = albums.Count > 0 && albums.All(a => a.Samples > 0)
                ? albums.Sum(a => a.Samples * a.Fraction) / albums.Sum(a => (double)a.Samples) : (double?)null;
            int active = tracks.Count(t => t.Stage == "Encoding" || t.Stage == "Checking PCM");
            toolStripStatusLabel1.Text = (parallelBatch.Paused ? "Paused · " : "") + albums.Count(a => a.Complete) + "/" + albums.Count + " albums · " + active + " encoders/checks · " + tracks.Count(t => t.Complete) + "/" + tracks.Count + " tracks checked";
            toolStripProgressBar2.Value = fraction.HasValue ? Math.Min(100, (int)(100 * fraction.Value)) : 0;
            toolStripStatusLabelProcessed.Text = parallelBatch.Elapsed.Elapsed.ToString(@"hh\:mm\:ss") + (fraction.HasValue ? " · " + (100 * fraction.Value).ToString("F0") + "%" : "");
        }
    }

    internal sealed class ParallelQueueForm : Form
    {
        private readonly ParallelConversionBatch batch;
        private readonly Action refreshOwner;
        private readonly Action<bool> stop;
        private readonly DataGridView grid = new DataGridView();
        private readonly Dictionary<string, DataGridViewRow> rows = new Dictionary<string, DataGridViewRow>();
        private readonly Label summary = new Label(), source = new Label();
        private readonly Button pause = new Button(), stopAll = new Button(), deleteAll = new Button(), stopAlbum = new Button(), close = new Button();
        private int renderIndex;
        private readonly Font albumFont;
        private readonly Timer timer = new Timer { Interval = 200 };
        internal ParallelQueueForm(ParallelConversionBatch batch, Action refreshOwner, Action<bool> stop)
        {
            this.batch = batch; this.refreshOwner = refreshOwner; this.stop = stop;
            Text = "CUETools · Experimental parallel conversion"; Size = new Size(950, 560); MinimumSize = new Size(700, 400); StartPosition = FormStartPosition.CenterParent;
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, Padding = new Padding(12) };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 40)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 42)); layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38)); layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
            summary.Dock = source.Dock = DockStyle.Fill; summary.AutoEllipsis = source.AutoEllipsis = true;
            grid.Dock = DockStyle.Fill; grid.ReadOnly = true; grid.AllowUserToAddRows = grid.AllowUserToDeleteRows = false; grid.RowHeadersVisible = false; grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill; grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.BackgroundColor = SystemColors.Window; albumFont = new Font(grid.Font, FontStyle.Bold);
            grid.Columns.Add("job", "Album / track"); grid.Columns.Add("stage", "Stage"); grid.Columns.Add("progress", "Progress"); grid.Columns.Add("error", "Details");
            grid.Columns[0].FillWeight = 140; grid.Columns[1].FillWeight = 110; grid.Columns[2].FillWeight = 45; grid.Columns[3].FillWeight = 140;
            var note = new Label { Dock = DockStyle.Fill, Text = "Stop keeps completed albums. Stop + delete removes this batch's outputs and restores overwritten files.\r\nChanged output files are preserved and reported instead of deleted.", AutoEllipsis = true };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight, WrapContents = true };
            pause.Text = "Pause all"; stopAlbum.Text = "Stop current album"; stopAll.Text = "Stop all"; deleteAll.Text = "Stop + delete outputs"; close.Text = "Hide";
            foreach (var button in new[] { pause, stopAlbum, stopAll, deleteAll, close }) { button.AutoSize = true; button.Height = 30; buttons.Controls.Add(button); }
            pause.Click += (s, e) => { try { batch.TogglePause(); } catch (Exception error) { MessageBox.Show(this, error.Message, "Pause failed"); } };
            stopAlbum.Click += (s, e) => batch.ActiveSession?.CancelCurrent();
            stopAll.Click += (s, e) => stop(false);
            deleteAll.Click += (s, e) => { if (MessageBox.Show(this, "Stop the whole batch and delete only files generated by it? Existing overwritten files will be restored. Source files will be kept.", "Stop and delete", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes) stop(true); };
            close.Click += (s, e) => { if (batch.Finished) Close(); else Hide(); };
            FormClosing += (s, e) => { if (!batch.Finished) { e.Cancel = true; Hide(); } };
            layout.Controls.Add(summary, 0, 0); layout.Controls.Add(source, 0, 1); layout.Controls.Add(grid, 0, 2); layout.Controls.Add(note, 0, 3); layout.Controls.Add(buttons, 0, 4); Controls.Add(layout);
            var tips = new ToolTip(); Disposed += (s, e) => tips.Dispose(); UiHelp.Attach(this, tips);
            tips.SetToolTip(grid, "Review albums and track stages; hover a track for its output path."); tips.SetToolTip(pause, "Pause or resume the batch."); tips.SetToolTip(stopAlbum, "Stop the current album and continue to the next."); tips.SetToolTip(stopAll, "Stop the batch and keep completed albums."); tips.SetToolTip(deleteAll, "Stop and roll back this batch; approved overwritten files are restored."); tips.SetToolTip(close, "Hide the queue while it runs, or close it when finished.");
            timer.Tick += (s, e) => RefreshProgress(); timer.Start(); RefreshProgress();
        }
        internal static Color StatusLinkColor(bool dark, bool highContrast) => highContrast ? SystemColors.HotTrack : dark ? Color.WhiteSmoke : Color.Black;
        internal static void ShowOrActivate(Form queue, IWin32Window owner)
        {
            if (queue == null || queue.IsDisposed) return;
            if (!queue.Visible) queue.Show(owner);
            if (queue.WindowState == FormWindowState.Minimized) queue.WindowState = FormWindowState.Normal;
            queue.Activate();
        }
        private void Row(string key, string job, string stage, string progress, string details, string tooltip = null, bool album = false)
        {
            if (!rows.TryGetValue(key, out var row)) { grid.Rows.Insert(renderIndex, 1); row = grid.Rows[renderIndex]; rows.Add(key, row); }
            renderIndex++;
            row.SetValues(job, stage, progress, details); row.Cells[0].ToolTipText = tooltip ?? key;
            row.Cells[3].ToolTipText = details;
            row.DefaultCellStyle.Font = album ? albumFont : grid.Font;
        }
        private void RefreshProgress()
        {
            var albums = batch.Snapshot(); var tracks = albums.SelectMany(a => a.Tracks).ToList();
            summary.Text = (batch.Finished ? "Finished" : batch.Paused ? "Paused" : "Running") + " · " + albums.Count(a => a.Complete) + "/" + albums.Count + " albums complete · " + batch.Workers + " encoder slots · elapsed " + batch.Elapsed.Elapsed.ToString(@"hh\:mm\:ss");
            var current = albums.Find(a => !a.Complete && a.Stage != "Waiting" && a.Stage != "Stopped" && a.Stage != "Failed");
            source.Text = !string.IsNullOrEmpty(batch.CleanupError) ? "Some outputs could not be removed. See cleanup details below." : current != null
                ? "Source: " + Path.GetFileName(current.Input) + " · decode / disc verification " + current.DecodePercent.ToString("P0") + " · " + current.Stage
                : batch.Finished ? "Review album and track results below." : "Preparing the album queue...";
            renderIndex = 0;
            foreach (var album in albums)
            {
                Row(album.Input, Path.GetFileName(Path.GetDirectoryName(album.Input)) + " · " + Path.GetFileName(album.Input), album.Stage, album.Fraction.ToString("P0"), album.Error ?? "", album: true);
                foreach (var track in album.Tracks) Row(album.Input + "|" + track.Output, "    " + Path.GetFileName(track.Output), track.Stage, track.Stage == "Encoding" && track.Percent == 0 ? "—" : track.Percent.ToString("P0"), track.Error ?? "", track.DisplayOutput ?? track.Output);
            }
            if (!string.IsNullOrEmpty(batch.CleanupError)) Row("cleanup", "Cleanup", "Needs attention", "", batch.CleanupError);
            pause.Text = batch.Paused ? "Resume all" : "Pause all"; pause.Enabled = stopAll.Enabled = deleteAll.Enabled = !batch.Finished; stopAlbum.Enabled = !batch.Finished && batch.ActiveSession != null; close.Text = batch.Finished ? "Close" : "Hide";
            refreshOwner();
        }
        protected override void Dispose(bool disposing) { if (disposing) { timer.Dispose(); albumFont?.Dispose(); } base.Dispose(disposing); }
    }
}
#endif
