using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DynamicDraw.Properties;
using PaintDotNet;

namespace DynamicDraw.Gui
{
    [System.ComponentModel.DesignerCategory("")]
    public class EditCustomAssetDirectories : PdnBaseForm
    {
        private readonly SettingsSerialization settings;

        #region Gui Members
        private System.ComponentModel.IContainer components = null;
        private ToolTip tooltip;
        private Label txtBrushImageLocations, txtPaletteLocations, txtScriptLocations;
        private TextBox txtbxBrushImageLocations, txtbxPaletteLocations, txtbxScriptLocations;
        private ThemedButton bttnAddFolderBrushImages, bttnAddFolderPalettes, bttnAddFolderScripts;
        private ThemedButton bttnAddFilesBrushImages, bttnAddFilesPalettes, bttnAddFilesScripts;
        private ThemedCheckbox chkbxLoadDefaultBrushes;
        private ThemedCheckbox chkbxLoadDefaultPalettes;
        private ThemedButton bttnResetPaletteLocations, bttnResetScriptLocations;
        private ThemedButton bttnSave, bttnCancel;
        private FlowLayoutPanel outerContainer;
        private FlowLayoutPanel brushImageControlsContainer, paletteControlsContainer, scriptControlsContainer;
        private FlowLayoutPanel addFileFolderBrushImageContainer, addFileFolderPaletteContainer, addFileFolderScriptContainer;
        private FlowLayoutPanel cancelSaveContainer;
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="EditCustomAssetDirectories" /> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        public EditCustomAssetDirectories(SettingsSerialization settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            SetupGui();
            CenterToScreen();
        }

        #region Methods (overridden)
        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        /// <summary>
        /// Configures the drawing area and loads text localizations.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            chkbxLoadDefaultBrushes.Checked = settings.UseDefaultBrushes;
            foreach (string item in settings.CustomBrushImageDirectories)
            {
                txtbxBrushImageLocations.AppendText(item + Environment.NewLine);
            }

            chkbxLoadDefaultPalettes.Checked = settings.UseDefaultPalettes;
            foreach (string item in settings.PaletteDirectories)
            {
                txtbxPaletteLocations.AppendText(item + Environment.NewLine);
            }

            foreach (string item in settings.ScriptDirectories)
            {
                txtbxScriptLocations.AppendText(item + Environment.NewLine);
            }
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Saves values to the preferences file.
        /// </summary>
        private void SaveSettings()
        {
            string[] imagePaths = txtbxBrushImageLocations.Text.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries);

            string[] palettePaths = txtbxPaletteLocations.Text.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries);

            string[] scriptPaths = txtbxScriptLocations.Text.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries);

            settings.CustomBrushImageDirectories = new HashSet<string>(imagePaths, StringComparer.OrdinalIgnoreCase);
            settings.PaletteDirectories = new HashSet<string>(palettePaths, StringComparer.OrdinalIgnoreCase);
            settings.ScriptDirectories = new HashSet<string>(scriptPaths, StringComparer.OrdinalIgnoreCase);
            settings.UseDefaultBrushes = chkbxLoadDefaultBrushes.Checked;
            settings.UseDefaultPalettes = chkbxLoadDefaultPalettes.Checked;
        }

        private void SetupGui()
        {
            components = new System.ComponentModel.Container();
            txtBrushImageLocations = new Label();
            txtPaletteLocations = new Label();
            txtScriptLocations = new Label();
            chkbxLoadDefaultBrushes = new ThemedCheckbox();
            chkbxLoadDefaultPalettes = new ThemedCheckbox();
            bttnResetPaletteLocations = new ThemedButton();
            bttnResetScriptLocations = new ThemedButton();
            txtbxBrushImageLocations = new TextBox();
            txtbxPaletteLocations = new TextBox();
            txtbxScriptLocations = new TextBox();
            bttnSave = new ThemedButton();
            bttnCancel = new ThemedButton();
            bttnAddFolderBrushImages = new ThemedButton();
            bttnAddFolderPalettes = new ThemedButton();
            bttnAddFolderScripts = new ThemedButton();
            tooltip = new ToolTip(components);
            brushImageControlsContainer = new FlowLayoutPanel();
            paletteControlsContainer = new FlowLayoutPanel();
            scriptControlsContainer = new FlowLayoutPanel();
            outerContainer = new FlowLayoutPanel();
            addFileFolderBrushImageContainer = new FlowLayoutPanel();
            addFileFolderPaletteContainer = new FlowLayoutPanel();
            addFileFolderScriptContainer = new FlowLayoutPanel();
            bttnAddFilesBrushImages = new ThemedButton();
            bttnAddFilesPalettes = new ThemedButton();
            bttnAddFilesScripts = new ThemedButton();
            cancelSaveContainer = new FlowLayoutPanel();
            brushImageControlsContainer.SuspendLayout();
            paletteControlsContainer.SuspendLayout();
            scriptControlsContainer.SuspendLayout();
            outerContainer.SuspendLayout();
            addFileFolderBrushImageContainer.SuspendLayout();
            addFileFolderPaletteContainer.SuspendLayout();
            addFileFolderScriptContainer.SuspendLayout();
            cancelSaveContainer.SuspendLayout();
            SuspendLayout();

            #region txtBrushImageLocations
            txtBrushImageLocations.AutoSize = true;
            txtBrushImageLocations.Location = new System.Drawing.Point(4, 0);
            txtBrushImageLocations.Margin = new Padding(4, 0, 4, 0);
            txtBrushImageLocations.Size = new System.Drawing.Size(123, 15);
            txtBrushImageLocations.TabIndex = 2;
            txtBrushImageLocations.Text = string.Format(Localization.Strings.BrushImageLocations, Localization.Strings.Title);
            #endregion

            #region txtPaletteLocations
            txtPaletteLocations.AutoSize = true;
            txtPaletteLocations.Location = new System.Drawing.Point(4, 0);
            txtPaletteLocations.Margin = new Padding(4, 0, 4, 0);
            txtPaletteLocations.Size = new System.Drawing.Size(123, 15);
            txtPaletteLocations.TabIndex = 2;
            txtPaletteLocations.Text = string.Format(Localization.Strings.PaletteLocations, Localization.Strings.Title);
            #endregion

            #region txtScriptLocations
            txtScriptLocations.AutoSize = true;
            txtScriptLocations.Location = new System.Drawing.Point(4, 0);
            txtScriptLocations.Margin = new Padding(4, 0, 4, 0);
            txtScriptLocations.Size = new System.Drawing.Size(123, 15);
            txtScriptLocations.TabIndex = 2;
            txtScriptLocations.Text = string.Format(Localization.Strings.ScriptLocations, Localization.Strings.Title);
            #endregion

            #region chkbxLoadDefaultBrushes
            chkbxLoadDefaultBrushes.AutoSize = true;
            chkbxLoadDefaultBrushes.Checked = true;
            chkbxLoadDefaultBrushes.CheckState = CheckState.Checked;
            chkbxLoadDefaultBrushes.Location = new System.Drawing.Point(200, 3);
            chkbxLoadDefaultBrushes.Margin = new Padding(4, 3, 4, 3);
            chkbxLoadDefaultBrushes.Padding = new Padding(0, 5, 0, 0);
            chkbxLoadDefaultBrushes.Size = new System.Drawing.Size(172, 24);
            chkbxLoadDefaultBrushes.TabIndex = 3;
            chkbxLoadDefaultBrushes.Text = Localization.Strings.LoadDefaultBrushes;
            tooltip.SetToolTip(chkbxLoadDefaultBrushes, Localization.Strings.LoadDefaultBrushesTip);
            #endregion

            #region bttnResetPaletteLocations
            bttnResetPaletteLocations.Location = new System.Drawing.Point(4, 3);
            bttnResetPaletteLocations.Margin = new Padding(4, 3, 4, 3);
            bttnResetPaletteLocations.Size = new System.Drawing.Size(110, 27);
            bttnResetPaletteLocations.TabIndex = 28;
            bttnResetPaletteLocations.Text = Localization.Strings.RestoreDefaults;
            tooltip.SetToolTip(bttnResetPaletteLocations, Localization.Strings.RestoreDefaultsLocationsTip);
            bttnResetPaletteLocations.Click += BttnResetPaletteLocations_Click;
            #endregion

            #region bttnResetScriptLocations
            bttnResetScriptLocations.Location = new System.Drawing.Point(4, 3);
            bttnResetScriptLocations.Margin = new Padding(4, 3, 4, 3);
            bttnResetScriptLocations.Size = new System.Drawing.Size(110, 27);
            bttnResetScriptLocations.TabIndex = 28;
            bttnResetScriptLocations.Text = Localization.Strings.RestoreDefaults;
            tooltip.SetToolTip(bttnResetScriptLocations, Localization.Strings.RestoreDefaultsLocationsTip);
            bttnResetScriptLocations.Click += BttnResetScriptLocations_Click;
            #endregion

            #region chkbxLoadDefaultPalettes
            chkbxLoadDefaultPalettes.AutoSize = true;
            chkbxLoadDefaultPalettes.Checked = true;
            chkbxLoadDefaultPalettes.CheckState = CheckState.Checked;
            chkbxLoadDefaultPalettes.Location = new System.Drawing.Point(4, 3);
            chkbxLoadDefaultPalettes.Margin = new Padding(4, 3, 4, 3);
            chkbxLoadDefaultPalettes.Padding = new Padding(0, 5, 0, 0);
            chkbxLoadDefaultPalettes.Size = new System.Drawing.Size(172, 24);
            chkbxLoadDefaultPalettes.TabIndex = 3;
            chkbxLoadDefaultPalettes.Text = Localization.Strings.LoadGeneratedPalettes;
            tooltip.SetToolTip(chkbxLoadDefaultPalettes, Localization.Strings.LoadGeneratedPalettesTip);
            #endregion

            #region txtbxBrushLocations
            txtbxBrushImageLocations.BorderStyle = BorderStyle.FixedSingle;
            txtbxBrushImageLocations.AcceptsReturn = true;
            txtbxBrushImageLocations.Location = new System.Drawing.Point(4, 18);
            txtbxBrushImageLocations.Margin = new Padding(4, 3, 4, 3);
            txtbxBrushImageLocations.Multiline = true;
            txtbxBrushImageLocations.ScrollBars = ScrollBars.Vertical;
            txtbxBrushImageLocations.Size = new System.Drawing.Size(554, 191);
            txtbxBrushImageLocations.TabIndex = 1;
            tooltip.SetToolTip(txtbxBrushImageLocations,
                string.Format(Localization.Strings.BrushImageLocationsTextboxTip, Localization.Strings.Title));
            #endregion

            #region txtbxPaletteLocations
            txtbxPaletteLocations.BorderStyle = BorderStyle.FixedSingle;
            txtbxPaletteLocations.AcceptsReturn = true;
            txtbxPaletteLocations.Location = new System.Drawing.Point(4, 18);
            txtbxPaletteLocations.Margin = new Padding(4, 3, 4, 3);
            txtbxPaletteLocations.Multiline = true;
            txtbxPaletteLocations.ScrollBars = ScrollBars.Vertical;
            txtbxPaletteLocations.Size = new System.Drawing.Size(554, 191);
            txtbxPaletteLocations.TabIndex = 1;
            tooltip.SetToolTip(txtbxPaletteLocations,
                string.Format(Localization.Strings.PalettesTextboxTip, Localization.Strings.Title));
            #endregion

            #region txtbxScriptLocations
            txtbxScriptLocations.BorderStyle = BorderStyle.FixedSingle;
            txtbxScriptLocations.AcceptsReturn = true;
            txtbxScriptLocations.Location = new System.Drawing.Point(4, 18);
            txtbxScriptLocations.Margin = new Padding(4, 3, 4, 3);
            txtbxScriptLocations.Multiline = true;
            txtbxScriptLocations.ScrollBars = ScrollBars.Vertical;
            txtbxScriptLocations.Size = new System.Drawing.Size(554, 191);
            txtbxScriptLocations.TabIndex = 1;
            tooltip.SetToolTip(txtbxScriptLocations,
                string.Format(Localization.Strings.ScriptsTextboxTip, Localization.Strings.Title));
            #endregion

            #region bttnSave
            bttnSave.Anchor = AnchorStyles.None;
            bttnSave.Location = new System.Drawing.Point(269, 3);
            bttnSave.Margin = new Padding(4, 3, 4, 3);
            bttnSave.Size = new System.Drawing.Size(140, 40);
            bttnSave.TabIndex = 28;
            bttnSave.Text = Localization.Strings.Save;
            tooltip.SetToolTip(bttnSave, Localization.Strings.SaveTip);
            bttnSave.Click += BttnSave_Click;
            #endregion

            #region bttnCancel
            bttnCancel.Anchor = AnchorStyles.None;
            bttnCancel.DialogResult = DialogResult.Cancel;
            bttnCancel.Location = new System.Drawing.Point(417, 3);
            bttnCancel.Margin = new Padding(4, 3, 4, 3);
            bttnCancel.Size = new System.Drawing.Size(140, 40);
            bttnCancel.TabIndex = 27;
            bttnCancel.Text = Localization.Strings.Cancel;
            tooltip.SetToolTip(bttnCancel, Localization.Strings.CancelTip);
            bttnCancel.Click += this.BttnCancel_Click;
            #endregion

            #region bttnAddFolderBrushImages
            bttnAddFolderBrushImages.Location = new System.Drawing.Point(4, 3);
            bttnAddFolderBrushImages.Margin = new Padding(4, 3, 4, 3);
            bttnAddFolderBrushImages.Size = new System.Drawing.Size(90, 27);
            bttnAddFolderBrushImages.TabIndex = 29;
            bttnAddFolderBrushImages.Text = Localization.Strings.AddFolder;
            tooltip.SetToolTip(bttnAddFolderBrushImages,
                string.Format(Localization.Strings.AddFoldersTip, Localization.Strings.Title));
            bttnAddFolderBrushImages.Click += BttnAddFolderBrushImages_Click;
            #endregion

            #region bttnAddFolderPalettes
            bttnAddFolderPalettes.Location = new System.Drawing.Point(4, 3);
            bttnAddFolderPalettes.Margin = new Padding(4, 3, 4, 3);
            bttnAddFolderPalettes.Size = new System.Drawing.Size(90, 27);
            bttnAddFolderPalettes.TabIndex = 29;
            bttnAddFolderPalettes.Text = Localization.Strings.AddFolder;
            tooltip.SetToolTip(bttnAddFolderPalettes,
                string.Format(Localization.Strings.AddFoldersTip, Localization.Strings.Title));
            bttnAddFolderPalettes.Click += BttnAddFolderPalettes_Click;
            #endregion

            #region bttnAddFolderScripts
            bttnAddFolderScripts.Location = new System.Drawing.Point(4, 3);
            bttnAddFolderScripts.Margin = new Padding(4, 3, 4, 3);
            bttnAddFolderScripts.Size = new System.Drawing.Size(90, 27);
            bttnAddFolderScripts.TabIndex = 29;
            bttnAddFolderScripts.Text = Localization.Strings.AddFolder;
            tooltip.SetToolTip(bttnAddFolderScripts,
                string.Format(Localization.Strings.AddFoldersTip, Localization.Strings.Title));
            bttnAddFolderScripts.Click += BttnAddFolderScripts_Click;
            #endregion

            #region brushImageControlsContainer
            brushImageControlsContainer.Controls.Add(txtBrushImageLocations);
            brushImageControlsContainer.Controls.Add(txtbxBrushImageLocations);
            brushImageControlsContainer.Location = new System.Drawing.Point(4, 3);
            brushImageControlsContainer.Margin = new Padding(4, 3, 4, 3);
            brushImageControlsContainer.Size = new System.Drawing.Size(537, 207);
            brushImageControlsContainer.TabIndex = 30;
            #endregion

            #region paletteControlsContainer
            paletteControlsContainer.Controls.Add(txtPaletteLocations);
            paletteControlsContainer.Controls.Add(txtbxPaletteLocations);
            paletteControlsContainer.Location = new System.Drawing.Point(4, 3);
            paletteControlsContainer.Margin = new Padding(4, 3, 4, 3);
            paletteControlsContainer.Size = new System.Drawing.Size(537, 207);
            paletteControlsContainer.TabIndex = 30;
            #endregion

            #region scriptControlsContainer
            scriptControlsContainer.Controls.Add(txtScriptLocations);
            scriptControlsContainer.Controls.Add(txtbxScriptLocations);
            scriptControlsContainer.Location = new System.Drawing.Point(4, 3);
            scriptControlsContainer.Margin = new Padding(4, 3, 4, 3);
            scriptControlsContainer.Size = new System.Drawing.Size(537, 207);
            scriptControlsContainer.TabIndex = 30;
            #endregion

            #region outerContainer
            outerContainer.Controls.Add(brushImageControlsContainer);
            outerContainer.Controls.Add(addFileFolderBrushImageContainer);
            outerContainer.Controls.Add(paletteControlsContainer);
            outerContainer.Controls.Add(addFileFolderPaletteContainer);
            outerContainer.Controls.Add(cancelSaveContainer);
            outerContainer.Dock = DockStyle.Fill;
            outerContainer.Location = new System.Drawing.Point(0, 0);
            outerContainer.Margin = new Padding(4, 3, 4, 3);
            outerContainer.Size = new System.Drawing.Size(565, 324);
            outerContainer.TabIndex = 31;
            #endregion

            #region addFileFolderBrushImageContainer
            addFileFolderBrushImageContainer.Controls.Add(bttnAddFolderBrushImages);
            addFileFolderBrushImageContainer.Controls.Add(bttnAddFilesBrushImages);
            addFileFolderBrushImageContainer.Controls.Add(chkbxLoadDefaultBrushes);
            addFileFolderBrushImageContainer.Location = new System.Drawing.Point(4, 216);
            addFileFolderBrushImageContainer.Margin = new Padding(4, 3, 4, 3);
            addFileFolderBrushImageContainer.Size = new System.Drawing.Size(561, 37);
            addFileFolderBrushImageContainer.TabIndex = 31;
            #endregion

            #region addFileFolderPaletteContainer
            addFileFolderPaletteContainer.Controls.Add(bttnAddFolderPalettes);
            addFileFolderPaletteContainer.Controls.Add(bttnAddFilesPalettes);
            addFileFolderPaletteContainer.Controls.Add(bttnResetPaletteLocations);
            addFileFolderPaletteContainer.Controls.Add(chkbxLoadDefaultPalettes);
            addFileFolderPaletteContainer.Location = new System.Drawing.Point(4, 216);
            addFileFolderPaletteContainer.Margin = new Padding(4, 3, 4, 3);
            addFileFolderPaletteContainer.Size = new System.Drawing.Size(561, 37);
            addFileFolderPaletteContainer.TabIndex = 32;
            #endregion

            #region addFileFolderScriptContainer
            addFileFolderScriptContainer.Controls.Add(bttnAddFolderScripts);
            addFileFolderScriptContainer.Controls.Add(bttnAddFilesScripts);
            addFileFolderScriptContainer.Controls.Add(bttnResetScriptLocations);
            addFileFolderScriptContainer.Location = new System.Drawing.Point(4, 216);
            addFileFolderScriptContainer.Margin = new Padding(4, 3, 4, 3);
            addFileFolderScriptContainer.Size = new System.Drawing.Size(561, 37);
            addFileFolderScriptContainer.TabIndex = 32;
            #endregion

            #region bttnAddFilesBrushImages
            bttnAddFilesBrushImages.Location = new System.Drawing.Point(102, 3);
            bttnAddFilesBrushImages.Margin = new Padding(4, 3, 4, 3);
            bttnAddFilesBrushImages.Size = new System.Drawing.Size(90, 27);
            bttnAddFilesBrushImages.TabIndex = 30;
            bttnAddFilesBrushImages.Text = Localization.Strings.AddFiles;
            tooltip.SetToolTip(bttnAddFilesBrushImages, string.Format(Localization.Strings.AddFilesTip, Localization.Strings.Title));
            bttnAddFilesBrushImages.Click += BttnAddFilesBrushImages_Click;
            #endregion

            #region bttnAddFilesPalettes
            bttnAddFilesPalettes.Location = new System.Drawing.Point(102, 3);
            bttnAddFilesPalettes.Margin = new Padding(4, 3, 4, 3);
            bttnAddFilesPalettes.Size = new System.Drawing.Size(90, 27);
            bttnAddFilesPalettes.TabIndex = 30;
            bttnAddFilesPalettes.Text = Localization.Strings.AddFiles;
            tooltip.SetToolTip(bttnAddFilesPalettes, string.Format(Localization.Strings.AddFilesTip, Localization.Strings.Title));
            bttnAddFilesPalettes.Click += BttnAddFilesPalettes_Click;
            #endregion

            #region bttnAddFilesScripts
            bttnAddFilesScripts.Location = new System.Drawing.Point(102, 3);
            bttnAddFilesScripts.Margin = new Padding(4, 3, 4, 3);
            bttnAddFilesScripts.Size = new System.Drawing.Size(90, 27);
            bttnAddFilesScripts.TabIndex = 30;
            bttnAddFilesScripts.Text = Localization.Strings.AddFiles;
            tooltip.SetToolTip(bttnAddFilesScripts, string.Format(Localization.Strings.AddFilesTip, Localization.Strings.Title));
            bttnAddFilesScripts.Click += BttnAddFilesScripts_Click;
            #endregion

            #region cancelSaveContainer
            cancelSaveContainer.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            cancelSaveContainer.Controls.Add(bttnCancel);
            cancelSaveContainer.Controls.Add(bttnSave);
            cancelSaveContainer.FlowDirection = FlowDirection.RightToLeft;
            cancelSaveContainer.Location = new System.Drawing.Point(4, 259);
            cancelSaveContainer.Margin = new Padding(4, 3, 4, 3);
            cancelSaveContainer.Size = new System.Drawing.Size(561, 55);
            cancelSaveContainer.TabIndex = 3;
            #endregion

            #region EditCustomAssetDirectories
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(565, 324);
            Controls.Add(outerContainer);
            Height = 600;
            Icon = Resources.Icon;
            KeyPreview = true;
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Text = Localization.Strings.DialogCustomBrushImagesTitle;
            KeyDown += EditCustomAssetDirectories_KeyDown;
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            brushImageControlsContainer.ResumeLayout(false);
            brushImageControlsContainer.PerformLayout();
            paletteControlsContainer.ResumeLayout(false);
            paletteControlsContainer.PerformLayout();
            scriptControlsContainer.ResumeLayout(false);
            scriptControlsContainer.PerformLayout();
            outerContainer.ResumeLayout(false);
            addFileFolderBrushImageContainer.ResumeLayout(false);
            addFileFolderBrushImageContainer.PerformLayout();
            addFileFolderPaletteContainer.ResumeLayout(false);
            addFileFolderPaletteContainer.PerformLayout();
            addFileFolderScriptContainer.ResumeLayout(false);
            addFileFolderScriptContainer.PerformLayout();
            cancelSaveContainer.ResumeLayout(false);
            ResumeLayout(false);
        }
        #endregion

        #region Methods (event handlers)

        /// <summary>
        /// Allows the user to browse for a folder to add as a directory.
        /// </summary>
        private void BttnAddFolderBrushImages_Click(object sender, EventArgs e)
        {
            //Opens a folder browser.
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.RootFolder = Environment.SpecialFolder.Desktop;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                //Appends the chosen directory to the textbox of directories.
                if (txtbxBrushImageLocations.Text != string.Empty && !txtbxBrushImageLocations.Text.EndsWith(Environment.NewLine))
                {
                    txtbxBrushImageLocations.AppendText(Environment.NewLine);
                }

                txtbxBrushImageLocations.AppendText(dlg.SelectedPath);
            }
        }

        /// <summary>
        /// Allows the user to browse for a folder to add as a directory.
        /// </summary>
        private void BttnAddFolderPalettes_Click(object sender, EventArgs e)
        {
            //Opens a folder browser.
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.RootFolder = Environment.SpecialFolder.Desktop;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                //Appends the chosen directory to the textbox of directories.
                if (txtbxPaletteLocations.Text != string.Empty && !txtbxPaletteLocations.Text.EndsWith(Environment.NewLine))
                {
                    txtbxPaletteLocations.AppendText(Environment.NewLine);
                }

                txtbxPaletteLocations.AppendText(dlg.SelectedPath);
            }
        }

        /// <summary>
        /// Allows the user to browse for a folder to add as a directory.
        /// </summary>
        private void BttnAddFolderScripts_Click(object sender, EventArgs e)
        {
            //Opens a folder browser.
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.RootFolder = Environment.SpecialFolder.Desktop;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                //Appends the chosen directory to the textbox of directories.
                if (txtbxScriptLocations.Text != string.Empty && !txtbxScriptLocations.Text.EndsWith(Environment.NewLine))
                {
                    txtbxScriptLocations.AppendText(Environment.NewLine);
                }

                txtbxScriptLocations.AppendText(dlg.SelectedPath);
            }
        }

        /// <summary>
        /// Allows the user to browse for files to add.
        /// </summary>
        private void BttnAddFilesBrushImages_Click(object sender, EventArgs e)
        {
            //Opens a folder browser.
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                //Appends the chosen directory to the textbox of directories.
                if (txtbxBrushImageLocations.Text != string.Empty)
                {
                    txtbxBrushImageLocations.AppendText(Environment.NewLine);
                }

                txtbxBrushImageLocations.AppendText(string.Join(Environment.NewLine, dlg.FileNames));
            }
        }

        /// <summary>
        /// Allows the user to browse for files to add.
        /// </summary>
        private void BttnAddFilesPalettes_Click(object sender, EventArgs e)
        {
            //Opens a folder browser.
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                //Appends the chosen directory to the textbox of directories.
                if (txtbxPaletteLocations.Text != string.Empty)
                {
                    txtbxPaletteLocations.AppendText(Environment.NewLine);
                }

                txtbxPaletteLocations.AppendText(string.Join(Environment.NewLine, dlg.FileNames));
            }
        }

        /// <summary>
        /// Allows the user to browse for files to add.
        /// </summary>
        private void BttnAddFilesScripts_Click(object sender, EventArgs e)
        {
            //Opens a folder browser.
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                //Appends the chosen directory to the textbox of directories.
                if (txtbxScriptLocations.Text != string.Empty)
                {
                    txtbxScriptLocations.AppendText(Environment.NewLine);
                }

                txtbxScriptLocations.AppendText(string.Join(Environment.NewLine, dlg.FileNames));
            }
        }

        /// <summary>
        /// Prepends any of the default palette locations that are missing from the entries.
        /// </summary>
        private void BttnResetPaletteLocations_Click(object sender, EventArgs e)
        {
            string newText = txtbxPaletteLocations.Text.TrimEnd();
            foreach (string path in PersistentSettings.defaultPalettePaths)
            {
                if (!newText.Contains(path))
                {
                    newText = path + Environment.NewLine + newText;
                }
            }

            txtbxPaletteLocations.Text = newText.TrimEnd();
        }

        /// <summary>
        /// Prepends any of the default script locations that are missing from the entries.
        /// </summary>
        private void BttnResetScriptLocations_Click(object sender, EventArgs e)
        {
            string newText = txtbxScriptLocations.Text.TrimEnd();
            if (!newText.Contains(PersistentSettings.defaultScriptPath))
            {
                newText = PersistentSettings.defaultScriptPath + Environment.NewLine + newText;
            }

            txtbxScriptLocations.Text = newText.TrimEnd();
        }

        /// <summary>
        /// Cancels and doesn't apply the preference changes.
        /// </summary>
        private void BttnCancel_Click(object sender, EventArgs e)
        {
            //Disables the button so it can't accidentally be called twice.
            bttnCancel.Enabled = false;

            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// Accepts and applies the preference changes.
        /// </summary>
        private void BttnSave_Click(object sender, EventArgs e)
        {
            //Disables the button so it can't accidentally be called twice.
            //Ensures settings will be saved.
            bttnSave.Enabled = false;

            SaveSettings();

            DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Handles escape/enter to close the dialog easily.
        /// </summary>
        private void EditCustomAssetDirectories_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            else if (e.KeyCode == Keys.Enter &&
                !txtbxBrushImageLocations.Focused &&
                !txtbxPaletteLocations.Focused &&
                !txtbxScriptLocations.Focused)
            {
                DialogResult = DialogResult.OK;
                Close();
            }
        }

        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            txtBrushImageLocations.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtPaletteLocations.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtScriptLocations.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtbxBrushImageLocations.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            txtbxBrushImageLocations.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtbxPaletteLocations.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            txtbxPaletteLocations.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtbxScriptLocations.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            txtbxScriptLocations.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            Refresh();
        }
        #endregion
    }
}
