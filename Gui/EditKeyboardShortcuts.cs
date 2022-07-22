using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using DynamicDraw.Logic;
using DynamicDraw.Properties;
using DynamicDraw.Localization;
using System.Linq;

namespace DynamicDraw
{
    internal partial class EditKeyboardShortcuts : Form
    {
        private BindingList<Tuple<string, KeyboardShortcut>> shortcutsList;
        private HashSet<KeyboardShortcut> shortcuts;

        private bool isRecordingKeystroke = false;
        private readonly HashSet<Keys> recordedAllKeys = new HashSet<Keys>();
        private readonly HashSet<Keys> recordedHeldKeys = new HashSet<Keys>();

        private ShortcutTarget currentShortcutTarget = (ShortcutTarget)(-1);
        private HashSet<Keys> currentShortcutSequence = new HashSet<Keys>();
        private bool currentShortcutUsesMouseWheelUp = false;
        private bool currentShortcutUsesMouseWheelDown = false;
        private bool currentShortcutRequiresCtrl = false;
        private bool currentShortcutRequiresShift = false;
        private bool currentShortcutRequiresAlt = false;
        private string currentShortcutActionData = "";

        public EditKeyboardShortcuts(HashSet<KeyboardShortcut> shortcuts)
        {
            this.KeyPreview = true; // for recording keystrokes.
            this.shortcuts = new HashSet<KeyboardShortcut>(shortcuts);
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
            Text = Strings.DialogKeyboardShortcutsTitle;

            bttnAddShortcut.Text = Strings.Add;
            bttnCancel.Text = Strings.Cancel;
            bttnEditShortcut.Text = Strings.Edit;
            bttnDeleteShortcut.Text = Strings.Delete;
            bttnRestoreDefaults.Text = Strings.RestoreDefaults;
            bttnSave.Text = Strings.Save;
            bttnShortcutSequence.Text = Strings.ShortcutSetTheSequence;

            chkbxShortcutWheelDown.Text = Strings.ShortcutInputWheelDown;
            chkbxShortcutWheelUp.Text = Strings.ShortcutInputWheelUp;
            txtbxShortcutActionData.PlaceholderText = Strings.KeyboardShortcutsActionDataPlaceholder;
            txtKeyboardShortcuts.Text = Strings.KeyboardShortcuts;

            tooltip.SetToolTip(bttnCancel, Strings.CancelTip);
            tooltip.SetToolTip(bttnSave, Strings.SaveKeyboardShortcutsTip);
            tooltip.SetToolTip(shortcutsListBox, Strings.KeyboardShortcutsTip);
            tooltip.SetToolTip(cmbxShortcutTarget, Strings.ShortcutSelectACommand);

            // Populates the list of keyboard shortcuts.
            GenerateShortcutsList();

            // Populates the shortcut target combobox.
            var shortcutTargetOptions = new BindingList<Tuple<string, ShortcutTarget>>();
            foreach (int i in Enum.GetValues(typeof(ShortcutTarget)))
            {
                ShortcutTarget target = (ShortcutTarget)i;
                shortcutTargetOptions.Add(
                    new Tuple<string, ShortcutTarget>(Setting.AllSettings[target].Name, target));
            }
            cmbxShortcutTarget.DataSource = shortcutTargetOptions;
            cmbxShortcutTarget.DisplayMember = "Item1";
            cmbxShortcutTarget.ValueMember = "Item2";
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Returns the unmodified shortcuts object, to be accessed only after this dialog has been shown and accepted
        /// by the user activating the OK button.
        /// </summary>
        public HashSet<KeyboardShortcut> GetShortcutsAfterDialogOK()
        {
            return shortcuts;
        }

        /// <summary>
        /// (Re)generates the shortcuts in the shortcuts list box from the data.
        /// </summary>
        private void GenerateShortcutsList()
        {
            bool firstTimeGenerating = shortcutsList == null;

            if (firstTimeGenerating)
            {
                shortcutsList = new BindingList<Tuple<string, KeyboardShortcut>>();
            }
            else
            {
                shortcutsList?.Clear();
            }

            // Populates the list of keyboard shortcuts.
            foreach (KeyboardShortcut shortcut in shortcuts)
            {
                shortcutsList.Add(new Tuple<string, KeyboardShortcut>("", shortcut));
            }

            if (firstTimeGenerating)
            {
                shortcutsListBox.DataSource = shortcutsList;
                shortcutsListBox.ValueMember = "Item2";
            }

            shortcutsListBox.Refresh();
        }

        /// <summary>
        /// Updates all GUI items according to the current shortcut data.
        /// </summary>
        private void RefreshViewBasedOnShortcut(bool onlyUpdateAddEditButtons)
        {
            // Updates the shortcut combobox.
            int shortcutTargetCmbxIndex = -1;

            if ((int)currentShortcutTarget != -1)
            {
                for (int i = 0; i < cmbxShortcutTarget.Items.Count; i++)
                {
                    if (currentShortcutTarget == ((Tuple<string, ShortcutTarget>)cmbxShortcutTarget.Items[i]).Item2)
                    {
                        shortcutTargetCmbxIndex = i;
                        break;
                    }
                }
            }

            if (!onlyUpdateAddEditButtons)
            {
                if (cmbxShortcutTarget.SelectedIndex != shortcutTargetCmbxIndex)
                {
                    cmbxShortcutTarget.SelectedIndex = shortcutTargetCmbxIndex;
                }

                // Updates the shortcut sequence text.
                if (isRecordingKeystroke)
                {
                    if (recordedHeldKeys.Count == 0)
                    {
                        bttnShortcutSequence.Text = "Recording keys...";
                    }
                    else
                    {
                        HashSet<Keys> sequenceToDisplay = KeyboardShortcut.SeparateKeyModifiers(
                            recordedAllKeys, out bool ctrlHeld, out bool shiftHeld, out bool altHeld);

                        bttnShortcutSequence.Text = KeyboardShortcut.GetShortcutKeysString(
                            sequenceToDisplay, ctrlHeld, shiftHeld, altHeld, false,
                            currentShortcutUsesMouseWheelUp, currentShortcutUsesMouseWheelDown)
                            + "..."; // Tells the user off that keys are still being recorded.
                    }
                }
                else
                {
                    bttnShortcutSequence.Text = KeyboardShortcut.GetShortcutKeysString(
                        currentShortcutSequence,
                        currentShortcutRequiresCtrl,
                        currentShortcutRequiresShift,
                        currentShortcutRequiresAlt,
                        false,
                        currentShortcutUsesMouseWheelUp,
                        currentShortcutUsesMouseWheelDown);
                }

                // Updates the remaining control info.
                chkbxShortcutWheelUp.Checked = currentShortcutUsesMouseWheelUp;
                chkbxShortcutWheelUp.Enabled = !currentShortcutUsesMouseWheelDown;
                chkbxShortcutWheelDown.Checked = currentShortcutUsesMouseWheelDown;
                chkbxShortcutWheelDown.Enabled = !currentShortcutUsesMouseWheelUp;
                txtbxShortcutActionData.Text = currentShortcutActionData;

                // Handles the add, edit, delete button enabled status.
                txtbxShortcutActionData.Enabled = ((int)currentShortcutTarget != -1) &&
                    Setting.AllSettings[currentShortcutTarget].ValueType != ShortcutTargetDataType.Action;

                bttnDeleteShortcut.Enabled = shortcutsListBox.SelectedItems.Count != 0;
            }

            bool isShortcutValid =
                (int)currentShortcutTarget != -1 &&
                shortcutTargetCmbxIndex != -1 &&
                KeyboardShortcut.IsActionValid(currentShortcutTarget, currentShortcutActionData);

            bttnAddShortcut.Enabled = isShortcutValid;
            bttnEditShortcut.Enabled = isShortcutValid && shortcutsListBox.SelectedItems.Count == 1;
        }
        #endregion

        #region Methods (event handlers)
        /// <summary>
        /// While recording a keystroke, all incoming key messages are recorded and stopped from
        /// further processing to avoid side-effects based on which keys are pressed.
        /// </summary>
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (isRecordingKeystroke)
            {
                // Add all keys held down to the list. Keys.KeyCode strips modifiers from the key
                recordedAllKeys.Add(keyData & Keys.KeyCode);
                recordedHeldKeys.Add(keyData & Keys.KeyCode);
                RefreshViewBasedOnShortcut(false);

                return true;
            }

            return base.ProcessCmdKey(ref msg, keyData);
        }

        /// <summary>
        /// Removes the released key from the list, then takes all keys that were pressed since the user started
        /// holding down keys to when they released the last one, counting all of them as part of the sequence.
        /// The result is set, and keystroke recording mode is turned off.
        /// </summary>
        protected override void OnKeyUp(KeyEventArgs e)
        {
            if (isRecordingKeystroke)
            {
                // Keys.KeyCode strips modifiers from the key
                recordedHeldKeys.Remove(e.KeyCode & Keys.KeyCode);

                if (recordedAllKeys.Count > 0 && recordedHeldKeys.Count == 0)
                {
                    HashSet<Keys> regularKeys = KeyboardShortcut.SeparateKeyModifiers(
                        recordedAllKeys,
                        out currentShortcutRequiresCtrl,
                        out currentShortcutRequiresShift,
                        out currentShortcutRequiresAlt);

                    currentShortcutSequence = new HashSet<Keys>(regularKeys);
                    isRecordingKeystroke = false;

                    RefreshViewBasedOnShortcut(false);
                }
            }

            base.OnKeyUp(e);
        }

        /// <summary>
        /// Allows the user to browse for a folder to add as a directory.
        /// </summary>
        private void BttnAddShortcut_Click(object sender, EventArgs e)
        {
            KeyboardShortcut newShortcut = new KeyboardShortcut
            {
                Target = currentShortcutTarget,
                RequireAlt = currentShortcutRequiresAlt,
                RequireCtrl = currentShortcutRequiresCtrl,
                RequireShift = currentShortcutRequiresShift,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                ContextsDenied = new HashSet<ShortcutContext>() { ShortcutContext.Typing },
                Keys = currentShortcutSequence,
                RequireWheel = false,
                RequireWheelUp = currentShortcutUsesMouseWheelUp,
                RequireWheelDown = currentShortcutUsesMouseWheelDown,
                ActionData = currentShortcutActionData
            };

            // Adds the new entry.
            shortcuts.Add(newShortcut);
            shortcutsList.Add(new Tuple<string, KeyboardShortcut>("", newShortcut));

            // Select the new entry.
            if (shortcutsListBox.Items.Count > 0)
            {
                shortcutsListBox.ClearSelected();
                shortcutsListBox.SelectedIndex = shortcutsListBox.Items.Count - 1;
            }
        }

        /// <summary>
        /// Cancels and doesn't apply the preference changes.
        /// </summary>
        private void BttnCancel_Click(object sender, EventArgs e)
        {
            shortcuts = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// Deletes the selected shortcut.
        /// </summary>
        private void BttnDeleteShortcut_Click(object sender, EventArgs e)
        {
            shortcutsListBox.SuspendLayout();

            // Creates a copy because SelectedItems is self-editing and unsuitable for direct access during removal.
            var shortcutsCopy = new List<Tuple<string, KeyboardShortcut>>();
            for (int i = 0; i < shortcutsListBox.SelectedItems.Count; i++)
            {
                shortcutsCopy.Add((Tuple<string, KeyboardShortcut>)shortcutsListBox.SelectedItems[i]);
            }

            // Removes in a loop.
            for (int i = 0; i < shortcutsCopy.Count; i++)
            {
                var item = shortcutsCopy[i];
                int index = shortcutsList.IndexOf(item);

                shortcutsList.RemoveAt(index);
                shortcuts.Remove(item.Item2);
            }

            shortcutsListBox.ResumeLayout();
            shortcutsListBox.Refresh();
        }

        /// <summary>
        /// Replaces the selected shortcut with the current shortcut settings. There must be exactly one shortcut
        /// selected for this operation to work.
        /// </summary>
        private void BttnEditShortcut_Click(object sender, EventArgs e)
        {
            var item = (Tuple<string, KeyboardShortcut>)shortcutsListBox.SelectedItem;
            int index = shortcutsList.IndexOf(item);

            shortcutsList[index] = new Tuple<string, KeyboardShortcut>("", new KeyboardShortcut()
            {
                ActionData = currentShortcutActionData,
                Keys = currentShortcutSequence,
                RequireCtrl = currentShortcutRequiresCtrl,
                RequireShift = currentShortcutRequiresShift,
                ContextsRequired = new HashSet<ShortcutContext>() { ShortcutContext.OnCanvas },
                ContextsDenied = new HashSet<ShortcutContext>() { ShortcutContext.Typing },
                RequireAlt = currentShortcutRequiresAlt,
                RequireWheelDown = currentShortcutUsesMouseWheelDown,
                RequireWheelUp = currentShortcutUsesMouseWheelUp,
                Target = currentShortcutTarget
            });
            shortcuts.Remove(item.Item2);
            shortcuts.Add(shortcutsList[index].Item2);
            shortcutsListBox.Refresh();
        }

        /// <summary>
        /// Accepts and applies the preference changes.
        /// </summary>
        private void BttnSave_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// Draws the shortcuts in a more visually legible way.
        /// </summary>
        private void ShortcutsListBox_DrawItem(object sender, DrawItemEventArgs e)
        {
            if (e.Index == -1 || e.Index >= shortcutsList.Count)
            {
                return;
            }

            var item = shortcutsList[e.Index];
            Brush headerColor = Brushes.Black;
            Brush detailsColor = Brushes.DarkRed;

            if (shortcutsListBox.SelectedIndices.Contains(e.Index))
            {
                e.Graphics.FillRectangle(SystemBrushes.Highlight, e.Bounds);
                e.DrawFocusRectangle();
                headerColor = Brushes.White;
                detailsColor = Brushes.Yellow;
            }
            else
            {
                e.DrawBackground();
            }

            Font boldFont = new Font(shortcutsListBox.Font, FontStyle.Bold);
            string shortcutName = item.Item2.GetShortcutName();

            e.Graphics.DrawString(shortcutName, boldFont, headerColor, e.Bounds.X, e.Bounds.Y);

            int endPos = (int)e.Graphics.MeasureString(shortcutName, boldFont).Width;
            const int margin = 8;
            const int secondLinePosition = 16;

            e.Graphics.DrawString(item.Item2.GetShortcutKeysString(),
                shortcutsListBox.Font, headerColor, e.Bounds.X + endPos + margin, e.Bounds.Y);
            e.Graphics.DrawString(item.Item2.ActionData,
                shortcutsListBox.Font, detailsColor, e.Bounds.X, e.Bounds.Y + secondLinePosition);
        }

        /// <summary>
        /// Updates editable info and enabled buttons when changing index.
        /// </summary>
        private void ShortcutsListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (shortcutsListBox.SelectedIndices.Count > 1)
            {
                currentShortcutTarget = (ShortcutTarget)(-1);
                currentShortcutSequence = new HashSet<Keys>();
                currentShortcutUsesMouseWheelUp = false;
                currentShortcutUsesMouseWheelDown = false;
                currentShortcutActionData = "";
                bttnAddShortcut.Enabled = false;
                bttnEditShortcut.Enabled = false;
                currentShortcutRequiresCtrl = false;
                currentShortcutRequiresShift = false;
                currentShortcutRequiresAlt = false;
            }
            else if (shortcutsListBox.SelectedIndices.Count == 1)
            {
                var item = ((Tuple<string, KeyboardShortcut>)shortcutsListBox.SelectedItem).Item2;
                currentShortcutTarget = item.Target;
                currentShortcutSequence = new HashSet<Keys>(item.Keys);
                currentShortcutUsesMouseWheelUp = item.RequireWheelUp;
                currentShortcutUsesMouseWheelDown = item.RequireWheelDown;
                currentShortcutActionData = item.ActionData;
                currentShortcutRequiresCtrl = item.RequireCtrl;
                currentShortcutRequiresShift = item.RequireShift;
                currentShortcutRequiresAlt = item.RequireAlt;
            }

            RefreshViewBasedOnShortcut(false);
            shortcutsListBox.Refresh();
        }

        /// <summary>
        /// Restores keyboard shortcuts to what they'd be upon freshly installing this plugin.
        /// </summary>
        private void BttnRestoreDefaults_Click(object sender, EventArgs e)
        {
            var resetStatus = MessageBox.Show(
                "All shortcuts will be reset to installation defaults. Are you sure?",
                "Reset keyboard shortcuts", MessageBoxButtons.OKCancel);

            if (resetStatus == DialogResult.OK)
            {
                shortcuts = PersistentSettings.GetShallowShortcutsList();
                GenerateShortcutsList();
            }
        }

        /// <summary>
        /// Updates action data and runs validations.
        /// </summary>
        private void TxtbxShortcutActionData_TextChanged(object sender, System.EventArgs e)
        {
            currentShortcutActionData = txtbxShortcutActionData.Text;
            RefreshViewBasedOnShortcut(true);
        }

        /// <summary>
        /// Updates wheel down and runs validations.
        /// </summary>
        private void ChkbxShortcutWheelDown_CheckedChanged(object sender, System.EventArgs e)
        {
            currentShortcutUsesMouseWheelDown = chkbxShortcutWheelDown.Checked;
            RefreshViewBasedOnShortcut(false);
        }

        /// <summary>
        /// Updates wheel up and runs validations.
        /// </summary>
        private void ChkbxShortcutWheelUp_CheckedChanged(object sender, System.EventArgs e)
        {
            currentShortcutUsesMouseWheelUp = chkbxShortcutWheelUp.Checked;
            RefreshViewBasedOnShortcut(false);
        }

        /// <summary>
        /// Lets the user live-record a new key sequence, then on confirm, updates the current shortcut sequence.
        /// </summary>
        private void BttnShortcutSequence_Click(object sender, System.EventArgs e)
        {
            if (!isRecordingKeystroke)
            {
                recordedAllKeys.Clear();
                recordedHeldKeys.Clear();
                isRecordingKeystroke = true;
                RefreshViewBasedOnShortcut(false);
            }
        }

        /// <summary>
        /// Confirms recorded shortcuts when losing focus.
        /// </summary>
        private void BttnShortcutSequence_LostFocus(object sender, System.EventArgs e)
        {
            if (isRecordingKeystroke)
            {
                HashSet<Keys> regularKeys = KeyboardShortcut.SeparateKeyModifiers(
                    recordedAllKeys,
                    out currentShortcutRequiresCtrl,
                    out currentShortcutRequiresShift,
                    out currentShortcutRequiresAlt);

                currentShortcutSequence = new HashSet<Keys>(regularKeys);
                isRecordingKeystroke = false;

                RefreshViewBasedOnShortcut(false);
            }
        }

        /// <summary>
        /// Updates the shortcut target and runs validations.
        /// </summary>
        private void CmbxShortcutTarget_SelectedIndexChanged(object sender, System.EventArgs e)
        {
            if (cmbxShortcutTarget.SelectedIndex == -1)
            {
                currentShortcutTarget = (ShortcutTarget)(-1);
            }
            else
            {
                currentShortcutTarget = ((Tuple<string, ShortcutTarget>)cmbxShortcutTarget.SelectedItem).Item2;
            }

            RefreshViewBasedOnShortcut(false);
        }
        #endregion
    }
}
