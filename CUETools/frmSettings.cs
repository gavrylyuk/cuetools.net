using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Windows.Forms;
using CUEControls;
using CUETools.Processor;
using CUETools.Codecs;

namespace JDP
{
	public partial class frmSettings : Form {
		bool _reducePriority;
		CUEConfig _config;
		private IIconManager m_icon_mgr;
        private AudioEncoderSettingsViewModel m_encoder;
#if NET10_0_OR_GREATER
        private TextBox invalidFilenameReplacementBox;
        private CheckBox replaceInvalidWithSpaceCheckBox;
        private CheckBox chooseMetadataForEachAlbumCheckBox;
#endif

		public frmSettings() 
        {
			InitializeComponent();
#if NET10_0_OR_GREATER
            InitializeFilenameReplacementSetting(); UiHelp.Attach(this, toolTip1);
#endif
		}

        public frmSettings(AudioEncoderSettingsViewModel encoder)
        {
            InitializeComponent();
#if NET10_0_OR_GREATER
            InitializeFilenameReplacementSetting(); UiHelp.Attach(this, toolTip1);
#endif
            m_encoder = encoder;
        }

		public IIconManager IconMgr
		{
			get
			{
				return m_icon_mgr;
			}
			set
			{
				m_icon_mgr = value;
			}
		}	

#if NET10_0_OR_GREATER
        private void InitializeFilenameReplacementSetting()
        {
            // Let the actual tab content use the extra space when the window is resized.
            propertyGrid1.Dock = DockStyle.Fill; tabPage7.Padding = new Padding(8);
            listViewFormats.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            groupBoxFormat.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            comboFormatLosslessEncoder.Anchor = comboFormatLossyEncoder.Anchor = comboFormatDecoder.Anchor = comboBoxFormatTagger.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupBoxTagging.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            groupBoxAlbumArt.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel3.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            grpAudioFilenames.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            txtSingleFilenameFormat.Anchor = txtTrackFilenameFormat.Anchor = txtSpecialExceptions.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupBox4.Anchor = groupBox5.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel2.SetColumnSpan(chkEncodeWhenZeroOffset, 2);
            chkEncodeWhenZeroOffset.Dock = DockStyle.Fill;
            chkEncodeWhenZeroOffset.RightToLeft = RightToLeft.Yes;
            grpHDCD.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            groupBoxExternalEncoder.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tableLayoutPanel4.RowStyles[0].SizeType = tableLayoutPanel5.RowStyles[0].SizeType = SizeType.Absolute;
            tableLayoutPanel4.RowStyles[0].Height = tableLayoutPanel5.RowStyles[0].Height = 44;
            Font = new Font(Font.FontFamily, 9F, Font.Style);
            FormBorderStyle = FormBorderStyle.Sizable; MinimumSize = new Size(680, 440); ClientSize = new Size(720, 480);
            tabControl1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            tabControl1.Size = new Size(ClientSize.Width, ClientSize.Height - 42);
            var cancelButton = (Button)CancelButton;
            btnOK.Anchor = cancelButton.Anchor = AnchorStyles.Bottom;
            cancelButton.Left = ClientSize.Width / 2 + 5; btnOK.Left = cancelButton.Left - btnOK.Width - 8;
            btnOK.Top = cancelButton.Top = ClientSize.Height - btnOK.Height - 7;
            grpAudioFilenames.Height += 68; labelLanguage.Top += 68; comboLanguage.Top += 68;
            var label = new Label { Text = "Replace invalid with:", AutoSize = true, Location = new Point(12, 212) };
            invalidFilenameReplacementBox = new TextBox { Name = "invalidFilenameReplacementBox", MaxLength = 1, Location = new Point(176, 209), Width = 45 };
            replaceInvalidWithSpaceCheckBox = new CheckBox { Name = "replaceInvalidWithSpaceCheckBox", Text = "Replace with space", AutoSize = true, Location = new Point(12, 238) };
            replaceInvalidWithSpaceCheckBox.CheckedChanged += (sender, e) => invalidFilenameReplacementBox.Enabled = !replaceInvalidWithSpaceCheckBox.Checked;
            grpAudioFilenames.Controls.Add(label); grpAudioFilenames.Controls.Add(invalidFilenameReplacementBox); grpAudioFilenames.Controls.Add(replaceInvalidWithSpaceCheckBox);
            toolTip1.SetToolTip(replaceInvalidWithSpaceCheckBox, "Replace invalid filename characters with spaces instead of the selected symbol.");
            chooseMetadataForEachAlbumCheckBox = new CheckBox { Name = "chooseMetadataForEachAlbumCheckBox", Text = "Choose metadata for each album", AutoSize = false, Size = new Size(210, 38), Location = new Point(6, 154) };
            groupBoxTagging.Controls.Add(chooseMetadataForEachAlbumCheckBox);
            toolTip1.SetToolTip(chooseMetadataForEachAlbumCheckBox, "Show the metadata selection dialog for every album before encoding, including batches. Cancel stops the batch.");
            toolTip1.SetToolTip(invalidFilenameReplacementBox, "Replacement for invalid filename characters, such as / or :. Default: _. Use a visible ASCII character valid in Windows filenames.");
        }
#endif
		private void frmSettings_Load(object sender, EventArgs e) 
		{
			cUEConfigBindingSource.DataSource = _config;
			encodersBindingSource.DataMember = "Encoders"; // for MONO bug (setting BindingSource.DataSource clears DataMember:(
			propertyGrid1.SelectedObject = _config.advanced;
			chkReducePriority.Checked = _reducePriority;
			chkAutoCorrectFilenames.Checked = _config.autoCorrectFilenames;
			numFixWhenConfidence.Value = _config.fixOffsetMinimumConfidence;
			numFixWhenPercent.Value = _config.fixOffsetMinimumTracksPercent;
			numEncodeWhenConfidence.Value = _config.encodeWhenConfidence;
			numEncodeWhenPercent.Value = _config.encodeWhenPercent;
			chkEncodeWhenZeroOffset.Checked = _config.encodeWhenZeroOffset;
			chkWriteArTagsOnConvert.Checked = _config.writeArTagsOnEncode;
			chkWriteARTagsOnVerify.Checked = _config.writeArTagsOnVerify;
			chkKeepOriginalFilenames.Checked = _config.keepOriginalFilenames;
			txtSingleFilenameFormat.Text = _config.singleFilenameFormat;
			txtTrackFilenameFormat.Text = _config.trackFilenameFormat;
			chkRemoveSpecial.Checked = _config.removeSpecial;
			txtSpecialExceptions.Text = _config.specialExceptions;
			chkReplaceSpaces.Checked = _config.replaceSpaces;
#if NET10_0_OR_GREATER
            invalidFilenameReplacementBox.Text = _config.invalidFilenameReplacement;
            replaceInvalidWithSpaceCheckBox.Checked = _config.replaceInvalidWithSpace;
            chooseMetadataForEachAlbumCheckBox.Checked = _config.chooseMetadataForEachAlbum;
            invalidFilenameReplacementBox.Enabled = !replaceInvalidWithSpaceCheckBox.Checked;
#endif
			chkWriteArLogOnConvert.Checked = _config.writeArLogOnConvert;
			chkWriteARLogOnVerify.Checked = _config.writeArLogOnVerify;
			chkEmbedLog.Checked = _config.embedLog;
			chkExtractLog.Checked = _config.extractLog;
			chkFillUpCUE.Checked = _config.fillUpCUE;
			chkFilenamesANSISafe.Checked = _config.filenamesANSISafe;
			chkHDCDDetect.Checked = _config.detectHDCD;
			chkHDCDDecode.Checked = _config.decodeHDCD;
			chkHDCDStopLooking.Checked = _config.wait750FramesForHDCD;
			chkCreateM3U.Checked = _config.createM3U;
			chkCreateCUEFileInTracksMode.Checked = _config.createCUEFileInTracksMode;
			chkCreateCUEFileWhenEmbedded.Checked = _config.createCUEFileWhenEmbedded;
			chkAlwaysWriteUTF8CUEFile.Checked = _config.alwaysWriteUTF8CUEFile;
			chkTruncateExtra4206Samples.Checked = _config.truncate4608ExtraSamples;
			chkHDCDLW16.Checked = _config.decodeHDCDtoLW16;
			chkHDCD24bit.Checked = _config.decodeHDCDto24bit;
			chkOverwriteTags.Checked = _config.overwriteCUEData;
			chkAllowMultipleInstances.Checked = !_config.oneInstance;
			checkBoxWriteCUETags.Checked = _config.writeBasicTagsFromCUEData;
			checkBoxCopyBasicTags.Checked = _config.copyBasicTags;
			checkBoxCopyUnknownTags.Checked = _config.copyUnknownTags;
			//checkBoxCopyAlbumArt.Checked = _config.copyAlbumArt;
			checkBoxExtractAlbumArt.Checked = _config.extractAlbumArt;
			checkBoxEmbedAlbumArt.Checked = _config.embedAlbumArt;
			checkBoxARVerifyUseSourceFolder.Checked = _config.arLogToSourceFolder;
			checkBoxARLogVerbose.Checked = _config.arLogVerbose;
			checkBoxFixToNearest.Checked = _config.fixOffsetToNearest;
			//textBoxARLogExtension.Text = _config.arLogFilenameFormat;
			numericUpDownMaxResolution.Value = _config.maxAlbumArtSize;
			checkBoxSeparateDecodingThread.Checked = _config.separateDecodingThread;
			chkUseHTOALengthThreshold.Checked = _config.useHTOALengthThreshold;

			switch (_config.gapsHandling)
			{
				case CUEStyle.GapsAppended:
					if (_config.preserveHTOA)
						rbGapsPlusHTOA.Checked = true;
					else
						rbGapsAppended.Checked = true;
					break;
				case CUEStyle.GapsPrepended:
					rbGapsPrepended.Checked = true;
					break;
				case CUEStyle.GapsLeftOut:
					rbGapsLeftOut.Checked = true;
					break;
			}

			string[] cultures = { "en-US", "de-DE", "ru-RU" };
			foreach (string culture in cultures)
			{
				try
				{
					CultureInfo info = CultureInfo.GetCultureInfo(culture);
					comboLanguage.Items.Add(info);
					if (culture == _config.language)
						comboLanguage.SelectedItem = info;
				}
				catch
				{
				}
			}
			if (comboLanguage.SelectedItem == null)
				comboLanguage.SelectedItem = comboLanguage.Items[0];
			
			listViewFormats.SmallImageList = m_icon_mgr.ImageList;
			labelEncoderExtension.ImageList = m_icon_mgr.ImageList;
			labelDecoderExtension.ImageList = m_icon_mgr.ImageList;
			foreach (KeyValuePair<string, CUEToolsFormat> format in _config.formats)
			{
				ListViewItem item = new ListViewItem(format.Key, "." + format.Key);
				item.Tag = format.Value;
				listViewFormats.Items.Add(item);
				comboBoxEncoderExtension.Items.Add(format.Key);
				comboBoxDecoderExtension.Items.Add(format.Key);
			}
			//listViewFormats.Items[0].Selected = true;
			comboBoxFormatTagger.Items.Add(CUEToolsTagger.TagLibSharp);
			comboBoxFormatTagger.Items.Add(CUEToolsTagger.APEv2);
			comboBoxFormatTagger.Items.Add(CUEToolsTagger.ID3v2);

            if (m_encoder != null)
            {
                tabControl1.SelectedTab = tabPageEncoders;
                tabControl1.Selecting += new TabControlCancelEventHandler((s, e1) => e1.Cancel = true);
                encodersBindingSource.Position = _config.Encoders.IndexOf(m_encoder);
                listBoxEncoders.Enabled = false;
                buttonEncoderAdd.Enabled = false;
                buttonEncoderDelete.Enabled = false;
                comboBoxEncoderExtension.Enabled = false;
            }

            EnableDisable();
		}

		//private void DictionaryToListView(IDictionary<> dict, ListView view)
		//{
		//    foreach (KeyValuePair<string, object> format in dict)
		//    {
		//        ListViewItem item = new ListViewItem(format.Key, format.Key);
		//        item.Tag = format.Value;
		//        listViewFormats.Items.Add(item);
		//    }
		//}

		private void frmSettings_FormClosing(object sender, FormClosingEventArgs e) {
		}

		public bool ReducePriority
		{
			get { return _reducePriority; }
			set { _reducePriority = value; }
		}

		public CUEConfig Config {
			get { return _config; }
			set { _config = value; }
		}

		private void chkWVExtraMode_CheckedChanged(object sender, EventArgs e) {
			EnableDisable();
		}

		private void btnOK_Click(object sender, EventArgs e)
		{
#if NET10_0_OR_GREATER
            if (!replaceInvalidWithSpaceCheckBox.Checked && !CUEConfig.IsValidFilenameReplacement(invalidFilenameReplacementBox.Text))
            {
                DialogResult = DialogResult.None;
                MessageBox.Show(this, "Choose one visible ASCII character that is valid in Windows filenames, such as _, comma, or hyphen.", "Invalid replacement", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                invalidFilenameReplacementBox.Focus(); return;
            }
            _config.replaceInvalidWithSpace = replaceInvalidWithSpaceCheckBox.Checked;
            _config.chooseMetadataForEachAlbum = chooseMetadataForEachAlbumCheckBox.Checked;
            if (CUEConfig.IsValidFilenameReplacement(invalidFilenameReplacementBox.Text))
                _config.invalidFilenameReplacement = invalidFilenameReplacementBox.Text;
#endif
			if (listViewFormats.SelectedIndices.Count > 0)
				listViewFormats.Items[listViewFormats.SelectedIndices[0]].Selected = false;

			_reducePriority = chkReducePriority.Checked;
			_config.preserveHTOA = rbGapsPlusHTOA.Checked;
			_config.useHTOALengthThreshold = chkUseHTOALengthThreshold.Checked;
			_config.gapsHandling = rbGapsPrepended.Checked ? CUEStyle.GapsPrepended :
				rbGapsLeftOut.Checked ? CUEStyle.GapsLeftOut :
				CUEStyle.GapsAppended;
			_config.autoCorrectFilenames = chkAutoCorrectFilenames.Checked;
			_config.fixOffsetMinimumTracksPercent = (uint)numFixWhenPercent.Value;
			_config.fixOffsetMinimumConfidence = (uint)numFixWhenConfidence.Value;
			_config.encodeWhenPercent = (uint)numEncodeWhenPercent.Value;
			_config.encodeWhenConfidence = (uint)numEncodeWhenConfidence.Value;
			_config.encodeWhenZeroOffset = chkEncodeWhenZeroOffset.Checked;
			_config.writeArTagsOnEncode = chkWriteArTagsOnConvert.Checked;
			_config.writeArTagsOnVerify = chkWriteARTagsOnVerify.Checked;
			_config.keepOriginalFilenames = chkKeepOriginalFilenames.Checked;
			_config.singleFilenameFormat = txtSingleFilenameFormat.Text;
			_config.trackFilenameFormat = txtTrackFilenameFormat.Text;
			_config.removeSpecial = chkRemoveSpecial.Checked;
			_config.specialExceptions = txtSpecialExceptions.Text;
			_config.replaceSpaces = chkReplaceSpaces.Checked;
			_config.writeArLogOnConvert = chkWriteArLogOnConvert.Checked;
			_config.writeArLogOnVerify = chkWriteARLogOnVerify.Checked;
			_config.embedLog = chkEmbedLog.Checked;
			_config.extractLog = chkExtractLog.Checked;
			_config.fillUpCUE = chkFillUpCUE.Checked;
			_config.filenamesANSISafe = chkFilenamesANSISafe.Checked;
			_config.detectHDCD = chkHDCDDetect.Checked;
			_config.wait750FramesForHDCD = chkHDCDStopLooking.Checked;
			_config.decodeHDCD = chkHDCDDecode.Checked;
			_config.createM3U = chkCreateM3U.Checked;
			_config.createCUEFileInTracksMode = chkCreateCUEFileInTracksMode.Checked;
			_config.createCUEFileWhenEmbedded = chkCreateCUEFileWhenEmbedded.Checked;
			_config.alwaysWriteUTF8CUEFile = chkAlwaysWriteUTF8CUEFile.Checked;
			_config.truncate4608ExtraSamples = chkTruncateExtra4206Samples.Checked;
			_config.decodeHDCDtoLW16 = chkHDCDLW16.Checked;
			_config.decodeHDCDto24bit = chkHDCD24bit.Checked;
			_config.overwriteCUEData = chkOverwriteTags.Checked;
			_config.oneInstance = !chkAllowMultipleInstances.Checked;
			_config.writeBasicTagsFromCUEData = checkBoxWriteCUETags.Checked ;
			_config.copyBasicTags = checkBoxCopyBasicTags.Checked;
			_config.copyUnknownTags = checkBoxCopyUnknownTags.Checked;
			//_config.copyAlbumArt = checkBoxCopyAlbumArt.Checked;
			_config.extractAlbumArt = checkBoxExtractAlbumArt.Checked;
			_config.embedAlbumArt = checkBoxEmbedAlbumArt.Checked;

			_config.arLogToSourceFolder = checkBoxARVerifyUseSourceFolder.Checked;
			_config.arLogVerbose = checkBoxARLogVerbose.Checked;
			_config.fixOffsetToNearest = checkBoxFixToNearest.Checked;
			//_config.arLogFilenameFormat = textBoxARLogExtension.Text;
			_config.maxAlbumArtSize = (int) numericUpDownMaxResolution.Value;
			_config.separateDecodingThread = checkBoxSeparateDecodingThread.Checked;

			_config.language = ((CultureInfo)comboLanguage.SelectedItem).Name;
		}

		private void EnableDisable()
		{
			grpHDCD.Enabled = chkHDCDDetect.Checked;
			chkHDCDLW16.Enabled = chkHDCDDetect.Checked && chkHDCDDecode.Checked;
			chkHDCD24bit.Enabled = chkHDCDDetect.Checked && chkHDCDDecode.Checked;

			chkRemoveSpecial.Enabled = chkFilenamesANSISafe.Checked;
			txtSpecialExceptions.Enabled = chkRemoveSpecial.Checked && chkFilenamesANSISafe.Checked;

			txtSpecialExceptions.Enabled = chkRemoveSpecial.Checked;

			chkOverwriteTags.Enabled = chkFillUpCUE.Checked;
		}

		private void chkArFixOffset_CheckedChanged(object sender, EventArgs e)
		{
			EnableDisable();
		}

		private void chkArNoUnverifiedAudio_CheckedChanged(object sender, EventArgs e)
		{
			EnableDisable();
		}

		private void chkHDCDDetect_CheckedChanged(object sender, EventArgs e)
		{
			EnableDisable();
		}

		private void chkFilenamesANSISafe_CheckedChanged(object sender, EventArgs e)
		{
			EnableDisable();
		}

		private void chkRemoveSpecial_CheckedChanged(object sender, EventArgs e)
		{
			EnableDisable();
		}

		private void chkHDCDDecode_CheckedChanged(object sender, EventArgs e)
		{
			EnableDisable();
		}

		private void chkFillUpCUE_CheckedChanged(object sender, EventArgs e)
		{
			EnableDisable();
		}

		private void tabControl1_Deselecting(object sender, TabControlCancelEventArgs e)
		{
			if (listViewFormats.SelectedItems.Count > 0)
				listViewFormats.SelectedItems[0].Selected = false;
		}

		private void listViewFormats_BeforeLabelEdit(object sender, LabelEditEventArgs e)
		{
			CUEToolsFormat format = (CUEToolsFormat)listViewFormats.Items[e.Item].Tag;
			if (format.builtin)
				e.CancelEdit = true;
		}

		private void listViewFormats_AfterLabelEdit(object sender, LabelEditEventArgs e)
		{
			CUEToolsFormat format;
			if (e.Label == null || _config.formats.TryGetValue(e.Label, out format))
			{
				e.CancelEdit = true;
				return;
			}
			format = (CUEToolsFormat)listViewFormats.Items[e.Item].Tag;
			if (format.builtin)
			{
				e.CancelEdit = true;
				return;
			}

			foreach (var encoder in _config.Encoders)
				if (encoder.Extension == format.extension)
					encoder.Extension = e.Label;

			foreach (var decoder in _config.Decoders)
				if (decoder.Extension == format.extension)
					decoder.Extension = e.Label;

			comboBoxEncoderExtension.Items.Remove(format.extension);
			comboBoxEncoderExtension.Items.Add(e.Label);
			comboBoxDecoderExtension.Items.Remove(format.extension);
			comboBoxDecoderExtension.Items.Add(e.Label);

			_config.formats.Remove(format.extension);
			format.extension = e.Label;
			_config.formats.Add(format.extension, format);
		}

		private void listViewFormats_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Insert:
					{
						CUEToolsFormat format;
						if (_config.formats.TryGetValue("new", out format))
							return;
						format = new CUEToolsFormat("new", CUEToolsTagger.TagLibSharp, true, true, false, false, null, null, null);
						_config.formats.Add("new", format);
						ListViewItem item = new ListViewItem(format.extension, "." + format.extension);
						item.Tag = format;
						listViewFormats.Items.Add(item);
						comboBoxEncoderExtension.Items.Add(format.extension);
						comboBoxDecoderExtension.Items.Add(format.extension);
						item.BeginEdit();
						break;
					}
				case Keys.Delete:
					{
						if (listViewFormats.SelectedItems.Count <= 0)
							return;
						CUEToolsFormat format = (CUEToolsFormat)listViewFormats.SelectedItems[0].Tag;
						if (format.builtin)
						    return;
                        var decodersToRemove = new List<AudioDecoderSettingsViewModel>();
						foreach (var decoder in _config.Decoders)
							if (decoder.Extension == format.extension)
								decodersToRemove.Add(decoder);
						foreach (var decoder in decodersToRemove)
							_config.Decoders.Remove(decoder);
                        var encodersToRemove = new List<AudioEncoderSettingsViewModel>();
						foreach (var encoder in _config.Encoders)
							if (encoder.Extension == format.extension)
								encodersToRemove.Add(encoder);
						foreach (var encoder in encodersToRemove)
							_config.Encoders.Remove(encoder);
						comboBoxEncoderExtension.Items.Remove(format.extension);
						comboBoxDecoderExtension.Items.Remove(format.extension);
						_config.formats.Remove(format.extension);
						listViewFormats.SelectedItems[0].Remove();
						break;
					}
			}
		}

		private void listViewFormats_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
		{
			if (e.IsSelected)
			{
				CUEToolsFormat format = (CUEToolsFormat)e.Item.Tag;
				if (format == null)
					return;

				comboFormatLosslessEncoder.Items.Clear();
				foreach (var encoder in _config.Encoders)
					if (encoder.Extension == format.extension && encoder.Lossless)
						comboFormatLosslessEncoder.Items.Add(encoder);
				comboFormatLosslessEncoder.SelectedItem = format.encoderLossless;
				comboFormatLosslessEncoder.Enabled = format.allowLossless;

				comboFormatLossyEncoder.Items.Clear();
				foreach (var encoder in _config.Encoders)
					if (encoder.Extension == format.extension && !encoder.Lossless)
						comboFormatLossyEncoder.Items.Add(encoder);
				comboFormatLossyEncoder.SelectedItem = format.encoderLossy;
				comboFormatLossyEncoder.Enabled = format.allowLossy;

				comboFormatDecoder.Items.Clear();
				foreach (var decoder in _config.Decoders)
					if (decoder.Extension == format.extension)
						comboFormatDecoder.Items.Add(decoder);
				comboFormatDecoder.SelectedItem = format.decoder;
				comboFormatDecoder.Enabled = format.allowLossless;

				comboBoxFormatTagger.SelectedItem = format.tagger;

				checkBoxFormatEmbedCUESheet.Checked = format.allowEmbed;
				checkBoxFormatAllowLossless.Checked = format.allowLossless;
				checkBoxFormatAllowLossy.Checked = format.allowLossy;

				comboBoxFormatTagger.Enabled =
					checkBoxFormatEmbedCUESheet.Enabled =
					checkBoxFormatAllowLossless.Enabled =
					checkBoxFormatAllowLossy.Enabled =
					!format.builtin;

				groupBoxFormat.Visible = true;
			}
			else
			{
				groupBoxFormat.Visible = false;

				CUEToolsFormat format = (CUEToolsFormat)e.Item.Tag;
				if (format == null)
					return;

				format.encoderLossless = (AudioEncoderSettingsViewModel)comboFormatLosslessEncoder.SelectedItem;
				format.encoderLossy = (AudioEncoderSettingsViewModel)comboFormatLossyEncoder.SelectedItem;
                format.decoder = (AudioDecoderSettingsViewModel)comboFormatDecoder.SelectedItem;
				if (!format.builtin)
				{
					format.tagger = (CUEToolsTagger)comboBoxFormatTagger.SelectedItem;
					format.allowEmbed = checkBoxFormatEmbedCUESheet.Checked;
					format.allowLossless = checkBoxFormatAllowLossless.Checked;
					format.allowLossy = checkBoxFormatAllowLossy.Checked;
				}
			}			
		}

		private void encodersBindingSource_CurrentItemChanged(object sender, EventArgs e)
		{
			var encoder = encodersBindingSource.Current as AudioEncoderSettingsViewModel;
			if (encoder == null)
			{
				labelEncoderExtension.Visible =
				comboBoxEncoderExtension.Visible =
				comboBoxEncoderExtension.Enabled =
				groupBoxExternalEncoder.Visible =
				checkBoxEncoderLossless.Enabled =
				propertyGridEncoderSettings.Visible = false;
				propertyGridEncoderSettings.SelectedObject = null;
			}
			else
			{
				CUEToolsFormat format = null;
                if (!string.IsNullOrEmpty(encoder.Extension)) _config.formats.TryGetValue(encoder.Extension, out format);
				labelEncoderExtension.Visible = true;
				comboBoxEncoderExtension.Visible = true;
				comboBoxEncoderExtension.Enabled = encoder.CanBeDeleted;
                groupBoxExternalEncoder.Visible = encoder.CanBeDeleted;
				checkBoxEncoderLossless.Enabled = format != null && format.allowLossless && format.allowLossy;
                propertyGridEncoderSettings.Visible = !encoder.CanBeDeleted && encoder.Settings.HasBrowsableAttributes();
                propertyGridEncoderSettings.SelectedObject = encoder.CanBeDeleted ? null : encoder.Settings;
                if (!checkBoxEncoderLossless.Enabled && format != null && encoder.Lossless != format.allowLossless)
                    encoder.Lossless = format.allowLossless;
                foreach (KeyValuePair<string, CUEToolsFormat> fmtEntry in _config.formats)
				{
					CUEToolsFormat fmt = fmtEntry.Value;
					if (fmt.encoderLossless == encoder && (fmt.extension != encoder.Extension || !encoder.Lossless))
						fmt.encoderLossless = null;
					if (fmt.encoderLossy == encoder && (fmt.extension != encoder.Extension || encoder.Lossless))
						fmt.encoderLossy = null;
				}
			}
		}

		private void listBoxEncoders_KeyDown(object sender, KeyEventArgs e)
		{
			switch (e.KeyCode)
			{
				case Keys.Insert:
					buttonEncoderAdd_Click(sender, e);
					break;
				case Keys.Delete:
					buttonEncoderDelete_Click(sender, e);
					break;
			}
		}


		private void bindingSourceDecoders_CurrentItemChanged(object sender, EventArgs e)
		{
			var decoder = bindingSourceDecoders.Current as AudioDecoderSettingsViewModel;
            if (decoder == null)
            {
                labelDecoderExtension.Visible =
                comboBoxDecoderExtension.Visible =
                groupBoxExternalDecoder.Visible = false;
            }
            else
            {
                //CUEToolsFormat format = _config.formats[decoder.extension]; // _config.formats.TryGetValue(encoder.extension, out format)
                labelDecoderExtension.Visible =
                comboBoxDecoderExtension.Visible = true;
                groupBoxExternalDecoder.Visible = decoder.CanBeDeleted;
                foreach (KeyValuePair<string, CUEToolsFormat> fmtEntry in _config.formats)
                {
                    CUEToolsFormat fmt = fmtEntry.Value;
                    if (fmt.decoder == decoder && fmt.extension != decoder.Extension)
                        fmt.decoder = null;
                }
            }
        }

        private void listBoxDecoders_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.Insert:
                    {
                        buttonDecoderAdd_Click(sender, e);
                        break;
                    }
                case Keys.Delete:
                    {
                        buttonDecoderDelete_Click(sender, e);
                        break;
                    }
            }
        }

		private void comboBoxDecoderExtension_SelectedIndexChanged(object sender, EventArgs e)
		{
			labelDecoderExtension.ImageKey = "." + (string)comboBoxDecoderExtension.SelectedItem;
		}

		private void buttonEncoderAdd_Click(object sender, EventArgs e)
		{
			encodersBindingSource.AddNew();
		}

		private void buttonEncoderDelete_Click(object sender, EventArgs e)
		{
			var encoder = encodersBindingSource.Current as AudioEncoderSettingsViewModel;
			if (encoder == null || !encoder.CanBeDeleted)
				return;
            foreach (var format in _config.formats.Values)
            {
                if (format.encoderLossless == encoder) format.encoderLossless = null;
                if (format.encoderLossy == encoder) format.encoderLossy = null;
            }
			encodersBindingSource.RemoveCurrent();
        }

        private void buttonDecoderAdd_Click(object sender, EventArgs e)
        {
            bindingSourceDecoders.AddNew();
        }

        private void buttonDecoderDelete_Click(object sender, EventArgs e)
        {
            var decoder = bindingSourceDecoders.Current as AudioDecoderSettingsViewModel;
            if (decoder == null || !decoder.CanBeDeleted)
                return;
            foreach (var format in _config.formats.Values)
                if (format.decoder == decoder) format.decoder = null;
            bindingSourceDecoders.RemoveCurrent();
        }
	}
}