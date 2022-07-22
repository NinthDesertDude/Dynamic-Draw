using System;
using System.Collections.Generic;
using System.Windows.Forms;
using DynamicDraw.Properties;

namespace DynamicDraw.Gui
{
    internal partial class DynamicDrawPreferences : Form
    {
        private readonly SettingsSerialization settings;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicDrawPreferences" /> class.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <exception cref="ArgumentNullException"><paramref name="settings"/> is null.</exception>
        public DynamicDrawPreferences(SettingsSerialization settings)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings));
            InitializeComponent();
            Icon = Resources.Icon;
            CenterToScreen();
        }

        #region Methods (overridden)
        /// <summary>
        /// Configures the drawing area and loads text localizations.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            //Sets the text and tooltips based on language.
            Text = Localization.Strings.DialogCustomBrushImagesTitle;
            bttnCancel.Text = Localization.Strings.Cancel;
            bttnSave.Text = Localization.Strings.Save;
            chkbxLoadDefaultBrushes.Text = Localization.Strings.LoadDefaultBrushes;
            bttnAddFolder.Text = Localization.Strings.AddFolder;
            bttnAddFiles.Text = Localization.Strings.AddFiles;
            txtBrushLocations.Text = Localization.Strings.BrushLocations;
            tooltip.SetToolTip(bttnCancel, Localization.Strings.CancelTip);
            tooltip.SetToolTip(bttnSave, Localization.Strings.SaveTip);
            tooltip.SetToolTip(chkbxLoadDefaultBrushes, Localization.Strings.LoadDefaultBrushesTip);
            tooltip.SetToolTip(bttnAddFolder, Localization.Strings.AddFoldersTip);
            tooltip.SetToolTip(bttnAddFiles, Localization.Strings.AddFilesTip);
            tooltip.SetToolTip(txtbxBrushLocations, Localization.Strings.BrushLocationsTextboxTip);

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
        #endregion

        #region Methods (event handlers)

        /// <summary>
        /// Allows the user to browse for a folder to add as a directory.
        /// </summary>
        private void bttnAddFolder_Click(object sender, EventArgs e)
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
        private void bttnAddFiles_Click(object sender, EventArgs e)
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
        private void bttnCancel_Click(object sender, EventArgs e)
        {
            //Disables the button so it can't accidentally be called twice.
            bttnCancel.Enabled = false;

            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// Accepts and applies the preference changes.
        /// </summary>
        private void bttnSave_Click(object sender, EventArgs e)
        {
            //Disables the button so it can't accidentally be called twice.
            //Ensures settings will be saved.
            bttnSave.Enabled = false;

            SaveSettings();

            DialogResult = DialogResult.OK;
            this.Close();
        }
        #endregion
    }
}
