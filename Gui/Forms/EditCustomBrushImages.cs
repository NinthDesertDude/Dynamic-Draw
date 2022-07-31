using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DynamicDraw.Properties;

namespace DynamicDraw.Gui
{
    public class EditCustomBrushImages : Form
    {
        private readonly SettingsSerialization settings;

        #region Gui Members
        private System.ComponentModel.IContainer components = null;
        private Label txtBrushLocations;
        private ToggleButton chkbxLoadDefaultBrushes;
        private TextBox txtbxBrushLocations;
        private BasicButton bttnSave;
        private BasicButton bttnCancel;
        private BasicButton bttnAddFolder;
        private ToolTip tooltip;
        private FlowLayoutPanel flowLayoutPanel1;
        private FlowLayoutPanel flowLayoutPanel2;
        private FlowLayoutPanel flowLayoutPanel3;
        private FlowLayoutPanel flowLayoutPanel4;
        private BasicButton bttnAddFiles;
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="EditCustomBrushImages" /> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        public EditCustomBrushImages(SettingsSerialization settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            SetupGui();
            CenterToScreen();
        }

        #region Methods (overridden)
        /// <summary>
        /// Configures the drawing area and loads text localizations.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            chkbxLoadDefaultBrushes.Checked = settings.UseDefaultBrushes;
            foreach (string item in settings.CustomBrushImageDirectories)
            {
                txtbxBrushLocations.AppendText(item + Environment.NewLine);
            }
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Saves values to the preferences file.
        /// </summary>
        public void SaveSettings()
        {
            string[] values = txtbxBrushLocations.Text.Split(
                new[] { "\r\n", "\r", "\n" },
                StringSplitOptions.RemoveEmptyEntries);

            settings.CustomBrushImageDirectories = new HashSet<string>(values, StringComparer.OrdinalIgnoreCase);
            settings.UseDefaultBrushes = chkbxLoadDefaultBrushes.Checked;
        }

        private void SetupGui()
        {
            components = new System.ComponentModel.Container();
            txtBrushLocations = new Label();
            chkbxLoadDefaultBrushes = new ToggleButton();
            txtbxBrushLocations = new TextBox();
            bttnSave = new BasicButton();
            bttnCancel = new BasicButton();
            bttnAddFolder = new BasicButton();
            tooltip = new ToolTip(components);
            flowLayoutPanel1 = new FlowLayoutPanel();
            flowLayoutPanel2 = new FlowLayoutPanel();
            flowLayoutPanel3 = new FlowLayoutPanel();
            bttnAddFiles = new BasicButton();
            flowLayoutPanel4 = new FlowLayoutPanel();
            flowLayoutPanel1.SuspendLayout();
            flowLayoutPanel2.SuspendLayout();
            flowLayoutPanel3.SuspendLayout();
            flowLayoutPanel4.SuspendLayout();
            SuspendLayout();

            #region txtBrushLocations
            txtBrushLocations.AutoSize = true;
            txtBrushLocations.Location = new System.Drawing.Point(4, 0);
            txtBrushLocations.Margin = new Padding(4, 0, 4, 0);
            txtBrushLocations.Size = new System.Drawing.Size(123, 15);
            txtBrushLocations.TabIndex = 2;
            txtBrushLocations.Text = Localization.Strings.BrushLocations;
            tooltip.SetToolTip(txtbxBrushLocations, Localization.Strings.BrushLocationsTextboxTip);
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

            #region txtbxBrushLocations
            txtbxBrushLocations.BorderStyle = BorderStyle.FixedSingle;
            txtbxBrushLocations.AcceptsReturn = true;
            txtbxBrushLocations.Location = new System.Drawing.Point(4, 18);
            txtbxBrushLocations.Margin = new Padding(4, 3, 4, 3);
            txtbxBrushLocations.Multiline = true;
            txtbxBrushLocations.ScrollBars = ScrollBars.Vertical;
            txtbxBrushLocations.Size = new System.Drawing.Size(554, 191);
            txtbxBrushLocations.TabIndex = 1;
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

            #region bttnAddFolder
            bttnAddFolder.Location = new System.Drawing.Point(4, 3);
            bttnAddFolder.Margin = new Padding(4, 3, 4, 3);
            bttnAddFolder.Size = new System.Drawing.Size(90, 27);
            bttnAddFolder.TabIndex = 29;
            bttnAddFolder.Text = Localization.Strings.AddFolder;
            tooltip.SetToolTip(bttnAddFolder, Localization.Strings.AddFoldersTip);
            bttnAddFolder.Click += BttnAddFolder_Click;
            #endregion

            #region flowLayoutPanel1
            flowLayoutPanel1.Controls.Add(txtBrushLocations);
            flowLayoutPanel1.Controls.Add(txtbxBrushLocations);
            flowLayoutPanel1.Location = new System.Drawing.Point(4, 3);
            flowLayoutPanel1.Margin = new Padding(4, 3, 4, 3);
            flowLayoutPanel1.Size = new System.Drawing.Size(537, 207);
            flowLayoutPanel1.TabIndex = 30;
            #endregion

            #region flowLayoutPanel2
            flowLayoutPanel2.Controls.Add(flowLayoutPanel1);
            flowLayoutPanel2.Controls.Add(flowLayoutPanel3);
            flowLayoutPanel2.Controls.Add(flowLayoutPanel4);
            flowLayoutPanel2.Dock = DockStyle.Fill;
            flowLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            flowLayoutPanel2.Margin = new Padding(4, 3, 4, 3);
            flowLayoutPanel2.Size = new System.Drawing.Size(565, 324);
            flowLayoutPanel2.TabIndex = 31;
            #endregion

            #region flowLayoutPanel3
            flowLayoutPanel3.Controls.Add(bttnAddFolder);
            flowLayoutPanel3.Controls.Add(bttnAddFiles);
            flowLayoutPanel3.Controls.Add(chkbxLoadDefaultBrushes);
            flowLayoutPanel3.Location = new System.Drawing.Point(4, 216);
            flowLayoutPanel3.Margin = new Padding(4, 3, 4, 3);
            flowLayoutPanel3.Size = new System.Drawing.Size(561, 37);
            flowLayoutPanel3.TabIndex = 31;
            #endregion

            #region bttnAddFiles
            bttnAddFiles.Location = new System.Drawing.Point(102, 3);
            bttnAddFiles.Margin = new Padding(4, 3, 4, 3);
            bttnAddFiles.Size = new System.Drawing.Size(90, 27);
            bttnAddFiles.TabIndex = 30;
            bttnAddFiles.Text = Localization.Strings.AddFiles;
            tooltip.SetToolTip(bttnAddFiles, string.Format(Localization.Strings.AddFilesTip, Localization.Strings.Title));
            bttnAddFiles.Click += BttnAddFiles_Click;
            #endregion

            #region flowLayoutPanel4
            flowLayoutPanel4.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            flowLayoutPanel4.Controls.Add(bttnCancel);
            flowLayoutPanel4.Controls.Add(bttnSave);
            flowLayoutPanel4.FlowDirection = FlowDirection.RightToLeft;
            flowLayoutPanel4.Location = new System.Drawing.Point(4, 259);
            flowLayoutPanel4.Margin = new Padding(4, 3, 4, 3);
            flowLayoutPanel4.Size = new System.Drawing.Size(561, 55);
            flowLayoutPanel4.TabIndex = 3;
            #endregion

            #region EditCustomBrushImages
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(565, 324);
            Controls.Add(flowLayoutPanel2);
            Icon = Resources.Icon;
            KeyPreview = true;
            Margin = new Padding(4, 3, 4, 3);
            Text = Localization.Strings.DialogCustomBrushImagesTitle;
            KeyDown += EditCustomBrushImages_KeyDown;
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            flowLayoutPanel2.ResumeLayout(false);
            flowLayoutPanel3.ResumeLayout(false);
            flowLayoutPanel3.PerformLayout();
            flowLayoutPanel4.ResumeLayout(false);
            ResumeLayout(false);
        }
        #endregion

        #region Methods (event handlers)

        /// <summary>
        /// Allows the user to browse for a folder to add as a directory.
        /// </summary>
        private void BttnAddFolder_Click(object sender, EventArgs e)
        {
            //Opens a folder browser.
            FolderBrowserDialog dlg = new FolderBrowserDialog();
            dlg.RootFolder = Environment.SpecialFolder.Desktop;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                //Appends the chosen directory to the textbox of directories.
                if (txtbxBrushLocations.Text != string.Empty && !txtbxBrushLocations.Text.EndsWith(Environment.NewLine))
                {
                    txtbxBrushLocations.AppendText(Environment.NewLine);
                }

                txtbxBrushLocations.AppendText(dlg.SelectedPath);
            }
        }

        /// <summary>
        /// Allows the user to browse for files to add.
        /// </summary>
        private void BttnAddFiles_Click(object sender, EventArgs e)
        {
            //Opens a folder browser.
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Multiselect = true;
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                //Appends the chosen directory to the textbox of directories.
                if (txtbxBrushLocations.Text != string.Empty)
                {
                    txtbxBrushLocations.AppendText(Environment.NewLine);
                }

                txtbxBrushLocations.AppendText(string.Join("\n", dlg.FileNames));
            }
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
        private void EditCustomBrushImages_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            else if (e.KeyCode == Keys.Enter &&
                !txtbxBrushLocations.Focused)
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
            txtBrushLocations.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            txtbxBrushLocations.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            txtbxBrushLocations.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            Refresh();
        }
        #endregion

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
    }
}
