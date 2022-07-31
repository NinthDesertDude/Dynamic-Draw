using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using DynamicDraw.Properties;
using DynamicDraw.Localization;

namespace DynamicDraw
{
    public class EditKeyboardShortcuts : Form
    {
        private BindingList<Tuple<string, KeyboardShortcut>> shortcutsList;
        private HashSet<KeyboardShortcut> shortcuts;

        private bool isRecordingKeystroke = false;
        private readonly HashSet<Keys> recordedAllKeys = new HashSet<Keys>();
        private readonly HashSet<Keys> recordedHeldKeys = new HashSet<Keys>();

        private ShortcutTarget currentShortcutTarget = ShortcutTarget.None;
        private HashSet<Keys> currentShortcutSequence = new HashSet<Keys>();
        private bool currentShortcutUsesMouseWheelUp = false;
        private bool currentShortcutUsesMouseWheelDown = false;
        private bool currentShortcutRequiresCtrl = false;
        private bool currentShortcutRequiresShift = false;
        private bool currentShortcutRequiresAlt = false;
        private string currentShortcutActionData = "";

        private bool isUnsavedDataEdited = false;
        private bool wereEntriesEdited = false;

        #region Gui Members
        private Font boldFont = new Font("Microsoft Sans Serif", 10f, FontStyle.Bold);
        private IContainer components = null;
        private Label txtKeyboardShortcuts;
        private BasicButton bttnSave;
        private BasicButton bttnCancel;
        private BasicButton bttnAddShortcut;
        private ToolTip tooltip;
        private FlowLayoutPanel panelShortcuts;
        private FlowLayoutPanel panelOuterContainer;
        private FlowLayoutPanel panelShortcutControls;
        private FlowLayoutPanel panelSaveCancel;
        private BasicButton bttnDeleteShortcut;
        private BasicButton bttnRestoreDefaults;
        private ListBox shortcutsListBox;
        private BasicButton bttnEditShortcut;
        private FlowLayoutPanel pnlAddEditBar;
        private ComboBox cmbxShortcutTarget;
        private ToggleButton chkbxShortcutWheelUp;
        private ToggleButton chkbxShortcutWheelDown;
        private BasicButton bttnShortcutSequence;
        private FlowLayoutPanel pnlWheelCheckboxes;
        private FlowLayoutPanel pnlTargetAndShortcut;
        private FlowLayoutPanel pnlShortcutExtraData;
        private Panel panel1;
        private TextBox txtbxShortcutActionData;
        #endregion

        public EditKeyboardShortcuts(HashSet<KeyboardShortcut> shortcuts)
        {
            KeyPreview = true; // for recording keystrokes.
            this.shortcuts = new HashSet<KeyboardShortcut>(shortcuts);

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

                    isUnsavedDataEdited = true;
                    currentShortcutSequence = new HashSet<Keys>(regularKeys);
                    isRecordingKeystroke = false;

                    RefreshViewBasedOnShortcut(false);
                }
            }

            base.OnKeyUp(e);
        }

        /// <summary>
        /// Configures the drawing area and loads text localizations.
        /// </summary>
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            // Populates the list of keyboard shortcuts.
            GenerateShortcutsList();

            // Populates the shortcut target combobox.
            var shortcutTargetOptions = new BindingList<Tuple<string, ShortcutTarget>>();
            foreach (int i in Enum.GetValues(typeof(ShortcutTarget)))
            {
                // "None" is not a valid option to pick, so skip it.
                if (i == (int)ShortcutTarget.None)
                {
                    continue;
                }

                ShortcutTarget target = (ShortcutTarget)i;
                shortcutTargetOptions.Add(
                    new Tuple<string, ShortcutTarget>(Setting.AllSettings[target].Name, target));
            }
            cmbxShortcutTarget.DataSource = shortcutTargetOptions;
            cmbxShortcutTarget.DisplayMember = "Item1";
            cmbxShortcutTarget.ValueMember = "Item2";
        }

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

            // Handles escape/enter to close or save the dialog.
            else
            {
                if (keyData == Keys.Escape)
                {
                    BttnCancel_Click(null, null);
                    return true;
                }
                else if (keyData == Keys.Enter)
                {
                    BttnSave_Click(null, null);
                    return true;
                }
            }

            return base.ProcessCmdKey(ref msg, keyData);
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
            shortcutsListBox.Enabled = false;
            shortcutsListBox.DrawItem -= ShortcutsListBox_DrawItem; // unsubscribing for speed, no stutter
            foreach (KeyboardShortcut shortcut in shortcuts)
            {
                shortcutsList.Add(new Tuple<string, KeyboardShortcut>("", shortcut));
            }

            if (firstTimeGenerating)
            {
                shortcutsListBox.DataSource = shortcutsList;
                shortcutsListBox.ValueMember = "Item2";
            }

            shortcutsListBox.Enabled = true;
            shortcutsListBox.DrawItem += ShortcutsListBox_DrawItem;
        }

        /// <summary>
        /// Updates all GUI items according to the current shortcut data.
        /// </summary>
        private void RefreshViewBasedOnShortcut(bool onlyUpdateAddEditButtons)
        {
            // Updates the shortcut combobox.
            int shortcutTargetCmbxIndex = -1;

            if (currentShortcutTarget != ShortcutTarget.None)
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
                        bttnShortcutSequence.Text = Strings.DialogKeyboardShortcutsRecordKeys;
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
                txtbxShortcutActionData.Enabled = (currentShortcutTarget != ShortcutTarget.None) &&
                    Setting.AllSettings[currentShortcutTarget].ValueType != ShortcutTargetDataType.Action;

                bttnDeleteShortcut.Enabled = shortcutsListBox.SelectedItems.Count != 0;
            }

            bool isShortcutValid =
                currentShortcutTarget != ShortcutTarget.None &&
                shortcutTargetCmbxIndex != -1 &&
                KeyboardShortcut.IsActionValid(currentShortcutTarget, currentShortcutActionData);

            bttnAddShortcut.Enabled = isShortcutValid;
            bttnEditShortcut.Enabled = isShortcutValid && shortcutsListBox.SelectedItems.Count == 1;
        }

        private void SetupGui()
        {
            components = new Container();
            txtKeyboardShortcuts = new Label();
            bttnSave = new BasicButton();
            bttnCancel = new BasicButton();
            bttnAddShortcut = new BasicButton();
            tooltip = new ToolTip(components);
            panelShortcuts = new FlowLayoutPanel();
            shortcutsListBox = new ListBox();
            panelOuterContainer = new FlowLayoutPanel();
            pnlAddEditBar = new FlowLayoutPanel();
            pnlTargetAndShortcut = new FlowLayoutPanel();
            cmbxShortcutTarget = new ComboBox();
            bttnShortcutSequence = new BasicButton();
            pnlWheelCheckboxes = new FlowLayoutPanel();
            chkbxShortcutWheelUp = new ToggleButton();
            chkbxShortcutWheelDown = new ToggleButton();
            pnlShortcutExtraData = new FlowLayoutPanel();
            txtbxShortcutActionData = new TextBox();
            panelShortcutControls = new FlowLayoutPanel();
            bttnEditShortcut = new BasicButton();
            bttnDeleteShortcut = new BasicButton();
            panelSaveCancel = new FlowLayoutPanel();
            panel1 = new Panel();
            bttnRestoreDefaults = new BasicButton();
            panelShortcuts.SuspendLayout();
            panelOuterContainer.SuspendLayout();
            pnlAddEditBar.SuspendLayout();
            pnlTargetAndShortcut.SuspendLayout();
            pnlWheelCheckboxes.SuspendLayout();
            pnlShortcutExtraData.SuspendLayout();
            panelShortcutControls.SuspendLayout();
            panelSaveCancel.SuspendLayout();
            panel1.SuspendLayout();
            SuspendLayout();

            #region txtKeyboardShortcuts
            txtKeyboardShortcuts.AutoSize = true;
            txtKeyboardShortcuts.Location = new Point(4, 0);
            txtKeyboardShortcuts.Margin = new Padding(4, 0, 4, 0);
            txtKeyboardShortcuts.Size = new Size(110, 15);
            txtKeyboardShortcuts.TabIndex = 2;
            txtKeyboardShortcuts.Text = Strings.KeyboardShortcuts;
            #endregion

            #region bttnSave
            bttnSave.Anchor = AnchorStyles.None;
            bttnSave.Location = new Point(392, 3);
            bttnSave.Margin = new Padding(4, 3, 4, 3);
            bttnSave.Size = new Size(140, 40);
            bttnSave.TabIndex = 28;
            bttnSave.Text = Strings.Save;
            tooltip.SetToolTip(bttnSave, Strings.SaveKeyboardShortcutsTip);
            bttnSave.Click += BttnSave_Click;
            #endregion

            #region bttnCancel
            bttnCancel.Anchor = AnchorStyles.None;
            bttnCancel.DialogResult = DialogResult.Cancel;
            bttnCancel.Location = new Point(540, 3);
            bttnCancel.Margin = new Padding(4, 3, 4, 3);
            bttnCancel.Size = new Size(140, 40);
            bttnCancel.TabIndex = 27;
            bttnCancel.Text = Strings.Cancel;
            tooltip.SetToolTip(bttnCancel, Strings.CancelTip);
            bttnCancel.Click += BttnCancel_Click;
            #endregion

            #region bttnAddShortcut
            bttnAddShortcut.Enabled = false;
            bttnAddShortcut.Location = new Point(4, 0);
            bttnAddShortcut.Margin = new Padding(4, 0, 4, 0);
            bttnAddShortcut.Size = new Size(90, 27);
            bttnAddShortcut.TabIndex = 29;
            bttnAddShortcut.Text = Strings.Add;
            bttnAddShortcut.Click += BttnAddShortcut_Click;
            #endregion

            #region panelShortcuts
            panelShortcuts.Controls.Add(txtKeyboardShortcuts);
            panelShortcuts.Controls.Add(shortcutsListBox);
            panelShortcuts.FlowDirection = FlowDirection.TopDown;
            panelShortcuts.Location = new Point(4, 3);
            panelShortcuts.Margin = new Padding(4, 3, 4, 3);
            panelShortcuts.Size = new Size(684, 212);
            panelShortcuts.TabIndex = 30;
            #endregion

            #region shortcutsListBox
            shortcutsListBox.DrawMode = DrawMode.OwnerDrawFixed;
            shortcutsListBox.FormattingEnabled = true;
            shortcutsListBox.ItemHeight = 32;
            shortcutsListBox.Location = new Point(3, 18);
            shortcutsListBox.SelectionMode = SelectionMode.MultiExtended;
            shortcutsListBox.Size = new Size(680, 164);
            tooltip.SetToolTip(shortcutsListBox, Strings.KeyboardShortcutsTip);
            shortcutsListBox.TabIndex = 3;
            shortcutsListBox.DrawItem += ShortcutsListBox_DrawItem;
            shortcutsListBox.SelectedIndexChanged += ShortcutsListBox_SelectedIndexChanged;
            #endregion

            #region panelOuterContainer
            panelOuterContainer.Controls.Add(panelShortcuts);
            panelOuterContainer.Controls.Add(pnlAddEditBar);
            panelOuterContainer.Controls.Add(panelSaveCancel);
            panelOuterContainer.Dock = DockStyle.Fill;
            panelOuterContainer.Location = new Point(0, 0);
            panelOuterContainer.Margin = new Padding(4, 3, 4, 3);
            panelOuterContainer.Size = new Size(692, 364);
            panelOuterContainer.TabIndex = 31;
            #endregion

            #region pnlAddEditBar
            pnlAddEditBar.BorderStyle = BorderStyle.FixedSingle;
            pnlAddEditBar.Controls.Add(pnlTargetAndShortcut);
            pnlAddEditBar.Controls.Add(pnlWheelCheckboxes);
            pnlAddEditBar.Controls.Add(pnlShortcutExtraData);
            pnlAddEditBar.Location = new Point(4, 221);
            pnlAddEditBar.Margin = new Padding(4, 3, 4, 3);
            pnlAddEditBar.Padding = new Padding(0, 4, 0, 4);
            pnlAddEditBar.Size = new Size(684, 72);
            pnlAddEditBar.TabIndex = 32;
            #endregion

            #region pnlTargetAndShortcut
            pnlTargetAndShortcut.Controls.Add(cmbxShortcutTarget);
            pnlTargetAndShortcut.Controls.Add(bttnShortcutSequence);
            pnlTargetAndShortcut.FlowDirection = FlowDirection.TopDown;
            pnlTargetAndShortcut.Location = new Point(4, 7);
            pnlTargetAndShortcut.Margin = new Padding(4, 3, 4, 3);
            pnlTargetAndShortcut.Size = new Size(197, 60);
            pnlTargetAndShortcut.TabIndex = 36;
            #endregion

            #region cmbxShortcutTarget
            cmbxShortcutTarget.FlatStyle = FlatStyle.Flat;
            cmbxShortcutTarget.FormattingEnabled = true;
            cmbxShortcutTarget.Location = new Point(3, 3);
            cmbxShortcutTarget.Margin = new Padding(3, 3, 3, 2);
            cmbxShortcutTarget.Size = new Size(189, 23);
            cmbxShortcutTarget.TabIndex = 0;
            tooltip.SetToolTip(cmbxShortcutTarget, Strings.ShortcutSelectACommand);
            cmbxShortcutTarget.SelectedIndexChanged += CmbxShortcutTarget_SelectedIndexChanged;
            #endregion

            #region bttnShortcutSequence
            bttnShortcutSequence.Location = new Point(3, 30);
            bttnShortcutSequence.Margin = new Padding(3, 2, 3, 0);
            bttnShortcutSequence.Size = new Size(189, 27);
            bttnShortcutSequence.TabIndex = 33;
            bttnShortcutSequence.Text = Strings.ShortcutSetTheSequence;
            bttnShortcutSequence.Click += BttnShortcutSequence_Click;
            bttnShortcutSequence.LostFocus += BttnShortcutSequence_LostFocus;
            #endregion

            #region pnlWheelCheckboxes
            pnlWheelCheckboxes.Controls.Add(chkbxShortcutWheelUp);
            pnlWheelCheckboxes.Controls.Add(chkbxShortcutWheelDown);
            pnlWheelCheckboxes.FlowDirection = FlowDirection.TopDown;
            pnlWheelCheckboxes.Location = new Point(209, 7);
            pnlWheelCheckboxes.Margin = new Padding(4, 3, 4, 3);
            pnlWheelCheckboxes.Size = new Size(102, 50);
            pnlWheelCheckboxes.TabIndex = 35;
            #endregion

            #region chkbxShortcutWheelUp
            chkbxShortcutWheelUp.Anchor = AnchorStyles.Left;
            chkbxShortcutWheelUp.AutoSize = true;
            chkbxShortcutWheelUp.Location = new Point(3, 3);
            chkbxShortcutWheelUp.Size = new Size(76, 19);
            chkbxShortcutWheelUp.TabIndex = 2;
            chkbxShortcutWheelUp.Text = Strings.ShortcutInputWheelUp;
            chkbxShortcutWheelUp.CheckedChanged += ChkbxShortcutWheelUp_CheckedChanged;
            #endregion

            #region chkbxShortcutWheelDown
            chkbxShortcutWheelDown.Anchor = AnchorStyles.Left;
            chkbxShortcutWheelDown.AutoSize = true;
            chkbxShortcutWheelDown.Location = new Point(3, 28);
            chkbxShortcutWheelDown.Size = new Size(92, 19);
            chkbxShortcutWheelDown.TabIndex = 3;
            chkbxShortcutWheelDown.Text = Strings.ShortcutInputWheelDown;
            chkbxShortcutWheelDown.CheckedChanged += ChkbxShortcutWheelDown_CheckedChanged;
            #endregion

            #region pnlShortcutExtraData
            pnlShortcutExtraData.Controls.Add(txtbxShortcutActionData);
            pnlShortcutExtraData.Controls.Add(panelShortcutControls);
            pnlShortcutExtraData.FlowDirection = FlowDirection.TopDown;
            pnlShortcutExtraData.Location = new Point(315, 4);
            pnlShortcutExtraData.Margin = new Padding(0);
            pnlShortcutExtraData.Size = new Size(358, 60);
            pnlShortcutExtraData.TabIndex = 37;
            #endregion

            #region txtbxShortcutActionData
            txtbxShortcutActionData.Enabled = false;
            txtbxShortcutActionData.Location = new Point(3, 3);
            txtbxShortcutActionData.PlaceholderText = Strings.KeyboardShortcutsActionDataPlaceholder;
            txtbxShortcutActionData.Size = new Size(355, 23);
            txtbxShortcutActionData.TabIndex = 32;
            txtbxShortcutActionData.TextChanged += TxtbxShortcutActionData_TextChanged;
            #endregion

            #region panelShortcutControls
            panelShortcutControls.Controls.Add(bttnAddShortcut);
            panelShortcutControls.Controls.Add(bttnEditShortcut);
            panelShortcutControls.Controls.Add(bttnDeleteShortcut);
            panelShortcutControls.Location = new Point(0, 29);
            panelShortcutControls.Margin = new Padding(0);
            panelShortcutControls.Size = new Size(358, 29);
            panelShortcutControls.TabIndex = 31;
            #endregion

            #region bttnEditShortcut
            bttnEditShortcut.Enabled = false;
            bttnEditShortcut.Location = new Point(102, 0);
            bttnEditShortcut.Margin = new Padding(4, 0, 4, 0);
            bttnEditShortcut.Size = new Size(90, 27);
            bttnEditShortcut.TabIndex = 32;
            bttnEditShortcut.Text = Strings.Edit;
            bttnEditShortcut.Click += BttnEditShortcut_Click;
            #endregion

            #region bttnDeleteShortcut
            bttnDeleteShortcut.Enabled = false;
            bttnDeleteShortcut.Location = new Point(200, 0);
            bttnDeleteShortcut.Margin = new Padding(4, 0, 4, 0);
            bttnDeleteShortcut.Size = new Size(90, 27);
            bttnDeleteShortcut.TabIndex = 30;
            bttnDeleteShortcut.Text = Strings.Delete;
            bttnDeleteShortcut.Click += BttnDeleteShortcut_Click;
            #endregion

            #region panelSaveCancel
            panelSaveCancel.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            panelSaveCancel.Controls.Add(bttnCancel);
            panelSaveCancel.Controls.Add(bttnSave);
            panelSaveCancel.Controls.Add(panel1);
            panelSaveCancel.FlowDirection = FlowDirection.RightToLeft;
            panelSaveCancel.Location = new Point(4, 314);
            panelSaveCancel.Margin = new Padding(4, 18, 4, 3);
            panelSaveCancel.Size = new Size(684, 45);
            panelSaveCancel.TabIndex = 3;
            #endregion

            #region panel1
            panel1.Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
            panel1.Controls.Add(bttnRestoreDefaults);
            panel1.Location = new Point(3, 15);
            panel1.Size = new Size(382, 28);
            panel1.TabIndex = 32;
            #endregion

            #region bttnRestoreDefaults
            bttnRestoreDefaults.Dock = DockStyle.Left;
            bttnRestoreDefaults.Location = new Point(0, 0);
            bttnRestoreDefaults.Margin = new Padding(4, 3, 4, 3);
            bttnRestoreDefaults.Size = new Size(119, 28);
            bttnRestoreDefaults.TabIndex = 31;
            bttnRestoreDefaults.Text = Strings.RestoreDefaults;
            bttnRestoreDefaults.Click += BttnRestoreDefaults_Click;
            #endregion

            #region EditKeyboardShortcuts
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(692, 364);
            Controls.Add(panelOuterContainer);
            Icon = Resources.Icon;
            Margin = new Padding(4, 3, 4, 3);
            Text = Strings.DialogKeyboardShortcutsTitle;
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            panelShortcuts.ResumeLayout(false);
            panelShortcuts.PerformLayout();
            panelOuterContainer.ResumeLayout(false);
            pnlAddEditBar.ResumeLayout(false);
            pnlTargetAndShortcut.ResumeLayout(false);
            pnlWheelCheckboxes.ResumeLayout(false);
            pnlWheelCheckboxes.PerformLayout();
            pnlShortcutExtraData.ResumeLayout(false);
            pnlShortcutExtraData.PerformLayout();
            panelShortcutControls.ResumeLayout(false);
            panelSaveCancel.ResumeLayout(false);
            panel1.ResumeLayout(false);
            ResumeLayout(false);
        }
        #endregion

        #region Methods (event handlers)
        /// <summary>
        /// Adds the current keyboard shortcut data as a new shortcut.
        /// </summary>
        private void BttnAddShortcut_Click(object sender, EventArgs e)
        {
            isUnsavedDataEdited = false;
            wereEntriesEdited = true;
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
            if (wereEntriesEdited)
            {
                if (MessageBox.Show(Strings.CloseWithoutSaving, Strings.CloseWithoutSavingTitle,
                    MessageBoxButtons.OKCancel) != DialogResult.OK)
                {
                    return;
                }
            }

            shortcuts = null;
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// Deletes the selected shortcut.
        /// </summary>
        private void BttnDeleteShortcut_Click(object sender, EventArgs e)
        {
            isUnsavedDataEdited = false;
            wereEntriesEdited = true;
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
            isUnsavedDataEdited = false;
            wereEntriesEdited = true;
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
        /// Restores keyboard shortcuts to what they'd be upon freshly installing this plugin.
        /// </summary>
        private void BttnRestoreDefaults_Click(object sender, EventArgs e)
        {
            wereEntriesEdited = true;
            var resetStatus = MessageBox.Show(
                Strings.ConfirmResetShortcuts,
                Strings.ConfirmResetShortcutsTitle, MessageBoxButtons.OKCancel);

            if (resetStatus == DialogResult.OK)
            {
                shortcuts = PersistentSettings.GetShallowShortcutsList();
                GenerateShortcutsList();
            }
        }

        /// <summary>
        /// Accepts and applies the preference changes.
        /// </summary>
        private void BttnSave_Click(object sender, EventArgs e)
        {
            if (isUnsavedDataEdited)
            {
                if (MessageBox.Show(Strings.KeyboardShortcutsSaveAndDiscardEdits, Strings.Confirm,
                    MessageBoxButtons.OKCancel) != DialogResult.OK)
                {
                    return;
                }
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// Lets the user live-record a new key sequence, then on confirm, updates the current shortcut sequence.
        /// </summary>
        private void BttnShortcutSequence_Click(object sender, EventArgs e)
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
        private void BttnShortcutSequence_LostFocus(object sender, EventArgs e)
        {
            if (isRecordingKeystroke)
            {
                HashSet<Keys> regularKeys = KeyboardShortcut.SeparateKeyModifiers(
                    recordedAllKeys,
                    out currentShortcutRequiresCtrl,
                    out currentShortcutRequiresShift,
                    out currentShortcutRequiresAlt);

                isUnsavedDataEdited = true;
                currentShortcutSequence = new HashSet<Keys>(regularKeys);
                isRecordingKeystroke = false;

                RefreshViewBasedOnShortcut(false);
            }
        }

        /// <summary>
        /// Updates wheel down and runs validations.
        /// </summary>
        private void ChkbxShortcutWheelDown_CheckedChanged(object sender, EventArgs e)
        {
            isUnsavedDataEdited = true;
            currentShortcutUsesMouseWheelDown = chkbxShortcutWheelDown.Checked;
            RefreshViewBasedOnShortcut(false);
        }

        /// <summary>
        /// Updates wheel up and runs validations.
        /// </summary>
        private void ChkbxShortcutWheelUp_CheckedChanged(object sender, EventArgs e)
        {
            isUnsavedDataEdited = true;
            currentShortcutUsesMouseWheelUp = chkbxShortcutWheelUp.Checked;
            RefreshViewBasedOnShortcut(false);
        }

        /// <summary>
        /// Updates the shortcut target and runs validations.
        /// </summary>
        private void CmbxShortcutTarget_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cmbxShortcutTarget.SelectedIndex == -1)
            {
                currentShortcutTarget = ShortcutTarget.None;
            }
            else
            {
                isUnsavedDataEdited = true;
                currentShortcutTarget = ((Tuple<string, ShortcutTarget>)cmbxShortcutTarget.SelectedItem).Item2;
            }

            RefreshViewBasedOnShortcut(false);
        }

        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            txtKeyboardShortcuts.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            shortcutsListBox.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            cmbxShortcutTarget.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            cmbxShortcutTarget.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            txtbxShortcutActionData.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            txtbxShortcutActionData.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            Refresh();
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
            Brush headerColor = SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlText);
            Brush detailsColor = SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlRedAccent);

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
            isUnsavedDataEdited = false;

            if (shortcutsListBox.SelectedIndices.Count > 1)
            {
                currentShortcutTarget = ShortcutTarget.None;
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
        /// Updates action data and runs validations.
        /// </summary>
        private void TxtbxShortcutActionData_TextChanged(object sender, EventArgs e)
        {
            isUnsavedDataEdited = true;
            currentShortcutActionData = txtbxShortcutActionData.Text;
            RefreshViewBasedOnShortcut(true);
        }
        #endregion
    }
}