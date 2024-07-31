using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DynamicDraw.Properties;
using PaintDotNet;

namespace DynamicDraw.Gui
{
    [System.ComponentModel.DesignerCategory("")]
    public class EditScriptDialog : PdnBaseForm
    {
        private readonly ToolScripts scripts;

        #region Gui Members
        private System.ComponentModel.IContainer components = null;
        private Label txtScript;
        private TextBox txtbxScript;
        private ThemedButton bttnSave, bttnCancel;
        private FlowLayoutPanel outerContainer;
        private FlowLayoutPanel brushScriptContainer;
        private FlowLayoutPanel cancelSaveContainer;
        #endregion

        /// <summary>
        /// Initializes a new instance of the <see cref="EditScriptDialog" /> class.
        /// </summary>
        /// <param name="scripts">The settings.</param>
        /// <exception cref="ArgumentNullException"><paramref name="scripts"/> is null.</exception>
        public EditScriptDialog(ToolScripts scripts)
        {
            this.scripts = scripts ?? throw new ArgumentNullException(nameof(scripts));
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
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Returns the edited copy of the scripts object, assuming the dialog was closed and accepted.
        /// </summary>
        public ToolScripts GetScriptAfterDialogOK()
        {
            return scripts;
        }

        /// <summary>
        /// Saves values to the preferences file.
        /// </summary>
        public void SaveSettings()
        {
            if (scripts.Scripts.Count == 0)
            {
                scripts.Scripts.Add(new Script());
            }

            scripts.Scripts[0].Action = txtbxScript.Text;
        }

        private void SetupGui()
        {
            components = new System.ComponentModel.Container();
            txtScript = new Label();
            txtbxScript = new TextBox();
            bttnSave = new ThemedButton();
            bttnCancel = new ThemedButton();
            brushScriptContainer = new FlowLayoutPanel();
            outerContainer = new FlowLayoutPanel();
            cancelSaveContainer = new FlowLayoutPanel();
            brushScriptContainer.SuspendLayout();
            outerContainer.SuspendLayout();
            cancelSaveContainer.SuspendLayout();
            SuspendLayout();

            #region txtScript
            txtScript.AutoSize = true;
            txtScript.Location = new System.Drawing.Point(4, 0);
            txtScript.Margin = new Padding(4, 0, 4, 0);
            txtScript.Size = new System.Drawing.Size(123, 15);
            txtScript.TabIndex = 2;
            txtScript.Text = string.Format(Localization.Strings.ScriptData);
            #endregion

            #region txtbxScript
            txtbxScript.BorderStyle = BorderStyle.FixedSingle;
            txtbxScript.AcceptsReturn = true;
            txtbxScript.Location = new System.Drawing.Point(4, 18);
            txtbxScript.Margin = new Padding(4, 3, 4, 3);
            txtbxScript.Multiline = true;
            txtbxScript.Text = scripts?.Scripts?.Count > 0 ? scripts?.Scripts[0].Action : "";
            txtbxScript.ScrollBars = ScrollBars.Vertical;
            txtbxScript.Size = new System.Drawing.Size(554, 191);
            txtbxScript.TabIndex = 1;
            #endregion

            #region bttnSave
            bttnSave.Anchor = AnchorStyles.None;
            bttnSave.Location = new System.Drawing.Point(269, 3);
            bttnSave.Margin = new Padding(4, 3, 4, 3);
            bttnSave.Size = new System.Drawing.Size(140, 40);
            bttnSave.TabIndex = 28;
            bttnSave.Text = Localization.Strings.Save;
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
            bttnCancel.Click += this.BttnCancel_Click;
            #endregion

            #region brushImageControlsContainer
            brushScriptContainer.Controls.Add(txtbxScript);
            brushScriptContainer.Location = new System.Drawing.Point(4, 3);
            brushScriptContainer.Margin = new Padding(4, 3, 4, 3);
            brushScriptContainer.Size = new System.Drawing.Size(537, 207);
            brushScriptContainer.TabIndex = 30;
            #endregion

            #region outerContainer
            outerContainer.Controls.Add(brushScriptContainer);
            outerContainer.Controls.Add(cancelSaveContainer);
            outerContainer.Dock = DockStyle.Fill;
            outerContainer.Location = new System.Drawing.Point(0, 0);
            outerContainer.Margin = new Padding(4, 3, 4, 3);
            outerContainer.Size = new System.Drawing.Size(565, 324);
            outerContainer.TabIndex = 31;
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

            #region EditScripts
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
            Text = Localization.Strings.DialogEditScriptsTitle;
            KeyDown += EditScripts_KeyDown;
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            brushScriptContainer.ResumeLayout(false);
            brushScriptContainer.PerformLayout();
            outerContainer.ResumeLayout(false);
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
                if (txtbxScript.Text != string.Empty && !txtbxScript.Text.EndsWith(Environment.NewLine))
                {
                    txtbxScript.AppendText(Environment.NewLine);
                }

                txtbxScript.AppendText(dlg.SelectedPath);
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
                if (txtbxScript.Text != string.Empty)
                {
                    txtbxScript.AppendText(Environment.NewLine);
                }

                txtbxScript.AppendText(string.Join(Environment.NewLine, dlg.FileNames));
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
        private void EditScripts_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            else if (e.KeyCode == Keys.Enter &&
                !txtbxScript.Focused)
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
            txtScript.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtbxScript.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            txtbxScript.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            Refresh();
        }
        #endregion
    }
}
