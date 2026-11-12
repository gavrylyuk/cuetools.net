#if NET10_0_OR_GREATER
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using CUETools.Processor.Settings;

namespace JDP
{
    internal static class UiHelp
    {
        private static string FileName => Path.Combine(new SettingsReader("CUE Tools", "ui-settings.txt", Application.ExecutablePath).ProfilePath, "ui-settings.txt");
        internal static SystemColorMode LoadTheme()
        {
            try { return Enum.TryParse(new SettingsReader("CUE Tools", "ui-settings.txt", Application.ExecutablePath).Load("Theme"), out SystemColorMode mode) && Enum.IsDefined(mode) ? mode : SystemColorMode.Classic; }
            catch (IOException) { return SystemColorMode.Classic; }
            catch (UnauthorizedAccessException) { return SystemColorMode.Classic; }
        }
        internal static void SetTheme(SystemColorMode mode, Form owner)
        {
            if (mode == LoadTheme()) return;
            try { File.WriteAllText(FileName, "Theme=" + mode + Environment.NewLine); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { MessageBox.Show(owner, e.Message, "Cannot save theme"); return; }
            MessageBox.Show(owner, "The theme will apply when you restart CUETools.", "Theme", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        internal static void AddMenu(ToolStrip bar, Form owner)
        {
            var theme = new ToolStripDropDownButton("Theme") { ToolTipText = "Choose the appearance. Restart CUETools to apply it." };
            foreach (var item in new[] { ("System", SystemColorMode.System), ("Light", SystemColorMode.Classic), ("Dark", SystemColorMode.Dark) })
            {
                var menu = new ToolStripMenuItem(item.Item1) { Checked = LoadTheme() == item.Item2 };
                menu.Click += (s, e) => { SetTheme(item.Item2, owner); foreach (ToolStripMenuItem child in theme.DropDownItems) child.Checked = LoadTheme().ToString() == (string)child.Tag; };
                menu.Tag = item.Item2.ToString(); theme.DropDownItems.Add(menu);
            }
            bar.Items.Add(theme);
            var help = new ToolStripButton("Help (F1)") { ToolTipText = "Open the English CUETools guide." };
            help.Click += (s, e) => Show(owner); bar.Items.Add(help);
        }
        internal static void Attach(Form form, ToolTip tips)
        {
            form.KeyPreview = true;
            form.KeyDown += (s, e) => { if (e.KeyCode == Keys.F1 && e.Modifiers == Keys.None) { e.Handled = true; e.SuppressKeyPress = true; Show(form); } };
            AddTips(form, tips);
        }
        internal static void Attach(Form form) { var tips = new ToolTip(); form.Disposed += (s, e) => tips.Dispose(); Attach(form, tips); }
        private static void AddTips(Control parent, ToolTip tips)
        {
            foreach (Control control in parent.Controls)
            {
                if (control is CUEControls.FileSystemTreeView) continue;
                string hint = Hint(control);
                if (hint != null && string.IsNullOrWhiteSpace(tips.GetToolTip(control))) tips.SetToolTip(control, hint);
                AddTips(control, tips);
            }
        }
        private static string Hint(Control c)
        {
            switch (c.Name)
            {
                case "btnConvert": return "Start the selected action for the input or checked items.";
                case "btnStop": return "Stop the current operation.";
                case "btnPause": return "Pause the current operation.";
                case "btnResume": return "Resume the paused operation.";
                case "buttonEncoderSettings": return "Open settings for the selected encoder.";
                case "trackBarEncoderMode": return "Choose the encoder's compression or quality mode.";
                case "textBatchReport": return "Review the results and messages for processed inputs.";
                case "txtInputPath": return "Choose the CUE sheet, audio file, disc, or folder to process.";
                case "txtOutputPath": return "Choose the destination for generated files.";
                case "comboBoxOutputFormat": return "Choose or edit the output path template.";
                case "textBoxEncoderPath": return "Path to the external encoder executable.";
                case "textBoxDecoderPath": return "Path to the external decoder executable.";
                case "textBoxEncoderParameters": return "Command-line arguments for the external encoder; placeholders supply input, output and mode.";
                case "textBoxDecoderParameters": return "Command-line arguments for the external decoder.";
                case "txtTrackFilenameFormat": return "Template used to name individual output tracks.";
                case "txtSingleFilenameFormat": return "Template used to name a single output image.";
                case "txtSpecialExceptions": return "Characters to preserve when removing special filename characters.";
                case "numericUpDownMaxResolution": return "Maximum resolution for embedded or extracted album artwork.";
                case "textBoxOffset": return "Set the write offset in samples.";
                case "chkWriteArLogOnConvert": return "Write an AccurateRip report while converting.";
                case "chkWriteARLogOnVerify": return "Write an AccurateRip report when verifying.";
                case "chkEncodeWhenZeroOffset": return "Require the AccurateRip match to have zero offset before encoding.";
                case "numEncodeWhenPercent": return "Minimum percentage of matching tracks required by the encode condition.";
                case "numEncodeWhenConfidence": return "Minimum AccurateRip confidence required by the encode condition.";
                case "numFixWhenPercent": return "Minimum percentage of matching tracks required for offset correction.";
                case "numFixWhenConfidence": return "Minimum AccurateRip confidence required for offset correction.";
                case "chkRemoveSpecial": return "Remove special characters from generated filenames, except the characters specified.";
                case "chkReplaceSpaces": return "Replace spaces with underscores in generated filenames.";
                case "checkBoxCopyAlbumArt": return "Copy embedded artwork from the source when supported.";
                case "checkBoxExtractAlbumArt": return "Save embedded album artwork as an image file.";
                case "checkBoxEmbedAlbumArt": return "Embed available folder artwork in output audio files.";
                case "textBoxAlArtFilenameFormat": return "Template used to name extracted album artwork.";
                case "chkExtractLog": return "Extract an embedded log to a separate file.";
                case "checkBoxCopyBasicTags": return "Copy common source tags to the output.";
                case "checkBoxWriteCUETags": return "Write basic tags using metadata from the CUE sheet.";
                case "checkBoxCopyUnknownTags": return "Copy source tags that are not among the basic tags.";
                case "chkOverwriteTags": return "Allow source metadata to overwrite CUE data.";
                case "chkFillUpCUE": return "Fill missing CUE metadata using source tags.";
                case "textBoxARLogExtension": return "Template used for the AccurateRip log filename.";
                case "checkBoxARLogVerbose": return "Include more details in the AccurateRip report.";
                case "checkBoxFixToNearest": return "Choose the nearest suitable offset when correcting offsets.";
                case "checkBoxARVerifyUseSourceFolder": return "Write the verification report in the source folder.";
                case "comboFormatLossyEncoder": return "Choose the default lossy encoder for this format.";
                case "comboFormatLosslessEncoder": return "Choose the default lossless encoder for this format.";
                case "comboFormatDecoder": return "Choose the default decoder for this format.";
                case "comboBoxFormatTagger": return "Choose the tag format used for this audio format.";
                case "checkBoxFormatAllowLossy": return "Allow this format to be used for lossy encoding.";
                case "checkBoxFormatAllowLossless": return "Allow this format to be used for lossless encoding.";
                case "checkBoxFormatEmbedCUESheet": return "Mark this format as supporting embedded CUE sheets.";
                case "listBoxEncoders": return "Select an encoder to inspect or edit its settings.";
                case "listBoxDecoders": return "Select a decoder to inspect or edit its settings.";
                case "buttonEncoderAdd": return "Add an external encoder definition.";
                case "buttonEncoderDelete": return "Remove the selected custom encoder definition.";
                case "buttonDecoderAdd": return "Add an external decoder definition.";
                case "buttonDecoderDelete": return "Remove the selected custom decoder definition.";
                case "comboBoxEncoderExtension": return "File extension associated with this encoder.";
                case "comboBoxDecoderExtension": return "File extension associated with this decoder.";
                case "chkHDCDDetect": return "Detect HDCD information in compatible audio.";
                case "checkBox1": return "Configure verification for the selected encoder.";
                case "btnOK": return "Save the selected settings and close this window.";
                case "propertyGrid1": return "Advanced settings. Select a property to see its description below the list.";
                case "propertyGridEncoderSettings": return "Settings for the selected encoder. Select a property to see its description.";
            }
            if (c is CheckBox || c is RadioButton || c is Button)
            {
                string label = c.Text.Replace("&", "").Trim();
                if (!string.IsNullOrWhiteSpace(label)) return label + ". Press F1 for the guide.";
            }
            if (c is TextBox || c is NumericUpDown || c is ComboBox || c is TrackBar || c is ListBox || c is ListView || c is DataGridView)
            {
                string label = c.AccessibleName;
                if (string.IsNullOrWhiteSpace(label))
                {
                    var siblings = c.Parent?.Controls;
                    if (siblings != null) foreach (Control item in siblings) if (item is Label && item.Bottom <= c.Bottom && item.Right <= c.Left + 10 && Math.Abs(item.Top - c.Top) < 15) label = item.Text;
                }
                if (!string.IsNullOrWhiteSpace(label)) return label.Replace("&", "").Trim().TrimEnd(':') + ". Press F1 for the guide.";
                return "Select or edit this setting. Press F1 for the guide.";
            }
            return null;
        }
        internal static void Show(IWin32Window owner)
        {
            using var window = new Form { Text = "CUETools Help", StartPosition = FormStartPosition.CenterParent, Size = new Size(850, 680), MinimumSize = new Size(520, 360) };
            var text = new TextBox { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, WordWrap = true, Text = Guide.Replace("\r\n", "\n").Replace("\n", "\r\n") };
            window.Controls.Add(text); window.Shown += (s, e) => text.Select(0, 0); window.ShowDialog(owner);
        }
        private const string Guide = @"CUETools Help (English)

QUICK START
Choose a CUE sheet or audio input in the browser, or type its path. Select Encode, Tracks, Lossless and FLAC; choose an encoder and compression level. Check the output folder/template, then click Go. The application can split an image and CUE into tracks or convert a CUE referencing separate tracks. Use copies of your files when experimenting with output settings.

INPUT AND BATCH
Expand the browser to find local drives, network locations and the local database. Checked items form a batch. The input box shows the chosen source; the output box and template control where results are written. Folders, CUE sheets and supported audio files may be selected. Check the batch log for results.

ACTION
Encode creates audio output. Verify checks a disc image or tracks against available verification data without a normal encode. Create CUE Sheet generates a CUE description. Correct filenames repairs references or names according to the selected mode. Scripts control the processing sequence; the default script is the usual choice.

MODE AND AUDIO OUTPUT
Tracks produces separate audio tracks; Image + CUE produces one audio image with a separate CUE; Embedded stores the CUE in a supported output file. Lossless preserves decoded PCM; lossy formats discard audio information. None means no audio output. Select a format, encoder and quality/compression mode. Higher FLAC levels generally trade speed for smaller files without reducing quality. Encoder settings opens the chosen encoder's configuration.

VERIFICATION, METADATA AND LOGS
AccurateRip and CTDB compare disc checksums with database records when enabled and available. A missing match is not proof of damaged audio. Metadata can be chosen or edited before conversion; tags and artwork depend on the settings and output format. Reports and the batch log show verification and processing results.

GAPS, PREGAP AND OFFSET
Settings controls whether track gaps are appended, prepended or omitted, and how hidden track one audio is handled. Pregap and data-track fields describe disc layout; offset changes sample alignment. Alter these only when you understand the source and intended output: they can change track boundaries or verification results.

SETTINGS
General settings include language, filenames, gap handling, CUE and playlist creation, HDCD, artwork, metadata and verification logs. Unicode filenames support Japanese, Cyrillic and accented Latin text. Leave Force ANSI filenames unchecked to preserve them; ANSI mode is for legacy compatibility. Replace invalid with chooses the text used for filename characters that Windows does not allow; spaces around the text are preserved and the default is underscore. Replace with space uses a space for invalid characters instead and disables the symbol field. Replace spaces with underscores remains a separate option. On the Tagging tab, Choose metadata and cover for each album opens metadata and available artwork selection for each album, including batches. You can use Choose cover to select a local image in the metadata dialog. The artwork settings control whether the selection is embedded or extracted. Canceling metadata selection stops the remaining batch. Formats assigns decoders, encoders and taggers to extensions. Encoders and Decoders configure built-in or external tools. External command-line templates use placeholders; consult their field descriptions and the tool's own documentation. Advanced settings expose additional options; select a property to read its description. Changes may affect later conversions.

PARALLEL FLAC PREVIEW
When eligible, Go offers Original mode or parallel FLAC conversion. Select the number of encoder slots and a local temporary folder with enough free space for uncompressed tracks. One album is decoded in order; ready tracks are encoded concurrently and checked before publication. More workers do not guarantee higher speed. The queue displays track stages and progress. Pause/Resume controls the batch. Stop current album skips it. Stop all preserves completed albums. Stop + delete rolls back this batch's outputs and restores approved overwritten files; externally modified outputs are preserved and reported. Close hides a running queue; use the status link to reopen it.

THEME AND HELP
Choose Theme > System, Light, or Dark and restart CUETools. Dark mode availability depends on Windows. Press F1 in the main or settings window to reopen this guide. Hover a control for a short description.
";
    }
}
#endif
