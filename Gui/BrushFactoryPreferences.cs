using System;
using System.Linq;
using System.Windows.Forms;
using BrushFactory.Properties;
using Microsoft.Win32;

namespace BrushFactory.Gui
{
    public partial class BrushFactoryPreferences : Form
    {
        public BrushFactoryPreferences()
        {
            InitializeComponent();
            Icon = Resources.Icon;
            CenterToScreen();
        }

        #region Methods (not event handlers)
        /// <summary>
        /// Retrieves values from the registry for the gui.
        /// </summary>
        public void InitSettings()
        {
            RegistryKey key;

            //Opens or creates the key for the plugin.
            key = Registry.CurrentUser
                .CreateSubKey("software", true)
                .CreateSubKey("paint.net_brushfactory", true);

            string useDefBrushes = (string)key.GetValue("useDefaultBrushes");
            string customBrushLocs = (string)key.GetValue("customBrushLocations");

            //Uses the keys if they exist, or creates them otherwise.
            if (useDefBrushes == null)
            {
                key.SetValue("useDefaultBrushes", true);
            }
            else
            {
                chkbxLoadDefaultBrushes.Checked = Boolean.Parse(useDefBrushes);
            }
            if (customBrushLocs == null)
            {
                key.SetValue("customBrushLocations", String.Empty);
            }
            else
            {
                txtbxBrushLocations.Text = customBrushLocs;
            }

            key.Close();
        }

        /// <summary>
        /// Saves values to the registry from the gui.
        /// </summary>
        public void SaveSettings()
        {
            RegistryKey key;

            //Opens or creates the key for the plugin.
            key = Registry.CurrentUser
                .CreateSubKey("software", true)
                .CreateSubKey("paint.net_brushfactory", true);

            key.SetValue("useDefaultBrushes", chkbxLoadDefaultBrushes.Checked);
            key.SetValue("customBrushLocations", txtbxBrushLocations.Text);
            key.Close();
        }
        #endregion

        #region Methods (event handlers)
        /// <summary>
        /// Configures the drawing area and loads text localizations.
        /// </summary>
        private void winBrushFactoryPreferences_DialogLoad(object sender, EventArgs e)
        {
            //Sets the text and tooltips based on language.
            bttnCancel.Text = Globalization.GlobalStrings.Cancel;
            bttnSave.Text = Globalization.GlobalStrings.SavePreferences;
            chkbxLoadDefaultBrushes.Text = Globalization.GlobalStrings.LoadDefaultBrushes;
            bttnAddFolder.Text = Globalization.GlobalStrings.AddFolder;
            txtBrushLocations.Text = Globalization.GlobalStrings.BrushLocations;
            tooltip.SetToolTip(bttnCancel, Globalization.GlobalStrings.CancelTip);
            tooltip.SetToolTip(bttnSave, Globalization.GlobalStrings.SavePreferencesTip);
            tooltip.SetToolTip(chkbxLoadDefaultBrushes, Globalization.GlobalStrings.LoadDefaultBrushesTip);
            tooltip.SetToolTip(bttnAddFolder, Globalization.GlobalStrings.AddFolderTip);
            tooltip.SetToolTip(txtbxBrushLocations, Globalization.GlobalStrings.BrushLocationsTextboxTip);

            InitSettings();
        }

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
                if (txtbxBrushLocations.Text != String.Empty)
                {
                    txtbxBrushLocations.AppendText(Environment.NewLine);
                }

                txtbxBrushLocations.AppendText(dlg.SelectedPath);
            }
        }

        /// <summary>
        /// Cancels and doesn't apply the preference changes.
        /// </summary>
        private void bttnCancel_Click(object sender, EventArgs e)
        {
            //Disables the button so it can't accidentally be called twice.
            bttnCancel.Enabled = false;

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
