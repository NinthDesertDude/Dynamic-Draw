using System;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using DynamicDraw.Localization;

namespace DynamicDraw.Gui
{
    [System.ComponentModel.DesignerCategory("")]
    public class EditScript : UserControl
    {
        /// <summary>
        /// The associated script. This object is directly mutated by this GUI.
        /// </summary>
        public Script Script { get; private set; }

        /// <summary>
        /// Fires when the script is supposed to move up in a list of scripts.
        /// </summary>
        public Action OnMoveUp { get; set; }

        /// <summary>
        /// Fires when the script is supposed to move down in a list of scripts.
        /// </summary>
        public Action OnMoveDown { get; set; }

        /// <summary>
        /// Fires when the script is supposed to be deleted.
        /// </summary>
        public Action OnDelete { get; set; }

        #region Gui Members
        private FlowLayoutPanel outerContainer = new FlowLayoutPanel();
        private FlowLayoutPanel nameTriggerAndDeleteOrderButtonsContainer = new FlowLayoutPanel();
        private FlowLayoutPanel authorDescriptionAndActionContainer = new FlowLayoutPanel();
        private Label txtScriptName = new Label();
        private TextBox txtbxScriptName = new TextBox();
        private Label txtScriptAuthor = new Label();
        private TextBox txtbxScriptAuthor = new TextBox();
        private ThemedButton bttnScriptMoveUp = new ThemedButton();
        private ThemedButton bttnScriptMoveDown = new ThemedButton();
        private ThemedButton bttnScriptDelete = new ThemedButton();
        private Label txtScriptDescription = new Label();
        private TextBox txtbxScriptDescription = new TextBox();
        private Label txtScriptTrigger = new Label();
        private BindingList<Tuple<string, ScriptTrigger>> triggerOptions;
        private ThemedComboBox cmbxScriptTrigger = new ThemedComboBox();
        private Label txtScriptAction = new Label();
        private TextBox txtbxScriptAction = new TextBox();
        #endregion

        /// <summary>
        /// Creates a script-editing GUI with a mutable script object (not a copy).
        /// </summary>
        /// <param name="script">A script that will be mutated directly as this data changes.</param>
        public EditScript(Script script)
        {
            this.Script = script ?? new Script();

            SetupGui();

            /// These need to match the assigned numeric order in <see cref="ScriptTrigger"/> enum, i.e. the enums in
            /// this list must be 0, 1, ..., n in that order.
            triggerOptions = new()
            {
                new (Strings.ScriptsTriggerDisabled, ScriptTrigger.Disabled),
                new (Strings.ScriptsTriggerStartBrushStroke, ScriptTrigger.StartBrushStroke),
                new (Strings.ScriptsTriggerEndBrushStroke, ScriptTrigger.EndBrushStroke),
                new (Strings.ScriptsTriggerBrushStamp, ScriptTrigger.OnBrushStamp),
                new (Strings.ScriptsTriggerMouseMoved, ScriptTrigger.OnMouseMoved)
            };

            cmbxScriptTrigger.DataSource = triggerOptions;
            cmbxScriptTrigger.DisplayMember = "Item1";
            cmbxScriptTrigger.ValueMember = "Item2";
            cmbxScriptTrigger.SelectedIndex = (int)script.Trigger;

            // Setting DataSource invokes this for no good reason, so we bind it afterwards instead
            cmbxScriptTrigger.SelectedIndexChanged += CmbxScriptTrigger_SelectedIndexChanged;
        }

        #region Methods (not event handlers)
        /// <summary>
        /// Returns whether any textbox in this GUI is focused, so that events like pressing enter can decide if they
        /// want to respond.
        /// </summary>
        public bool IsTextboxFocused()
        {
            return txtbxScriptAction.Focused ||
                txtbxScriptAuthor.Focused ||
                txtbxScriptDescription.Focused ||
                txtbxScriptName.Focused;
        }

        private void SetupGui()
        {
            SuspendLayout();

            #region txtScriptName
            txtScriptName.AutoSize = true;
            txtScriptName.Text = Strings.ScriptName;
            txtScriptName.TextAlign = ContentAlignment.BottomCenter;
            #endregion

            #region txtbxScriptName
            txtbxScriptName.BorderStyle = BorderStyle.FixedSingle;
            txtbxScriptName.MinimumSize = new Size(64, 20);
            txtbxScriptName.MaximumSize = new Size(200, 20);
            txtbxScriptName.Text = Script.Description;
            txtbxScriptName.TextChanged += (a, b) => { Script.Name = txtbxScriptName.Text; };
            #endregion

            #region txtScriptTrigger
            txtScriptTrigger.AutoSize = true;
            txtScriptTrigger.Text = Strings.ScriptTrigger;
            txtScriptTrigger.TextAlign = ContentAlignment.BottomCenter;
            #endregion

            #region cmbxChosenEffect
            cmbxScriptTrigger.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            cmbxScriptTrigger.FlatStyle = FlatStyle.Flat;
            cmbxScriptTrigger.DropDownHeight = 140;
            cmbxScriptTrigger.DropDownStyle = ComboBoxStyle.DropDownList;
            cmbxScriptTrigger.DropDownWidth = 150;
            cmbxScriptTrigger.FormattingEnabled = true;
            cmbxScriptTrigger.IntegralHeight = false;
            cmbxScriptTrigger.ItemHeight = 13;
            cmbxScriptTrigger.Margin = new Padding(0, 3, 0, 3);
            cmbxScriptTrigger.Size = new Size(121, 21);
            cmbxScriptTrigger.TabIndex = 0;
            #endregion

            #region bttnScriptMoveUp
            bttnScriptMoveUp.Anchor = AnchorStyles.None;
            bttnScriptMoveUp.Margin = new Padding(4, 3, 4, 3);
            bttnScriptMoveUp.Size = new Size(32, 32);
            bttnScriptMoveUp.TabIndex = 28;
            bttnScriptMoveUp.Text = "▲";
            bttnScriptMoveUp.TextAlign = ContentAlignment.MiddleCenter;
            bttnScriptMoveUp.Click += (a, b) => { OnMoveUp?.Invoke(); };
            #endregion

            #region bttnScriptMoveDown
            bttnScriptMoveDown.Anchor = AnchorStyles.None;
            bttnScriptMoveDown.Margin = new Padding(4, 3, 4, 3);
            bttnScriptMoveDown.Size = new Size(32, 32);
            bttnScriptMoveDown.TabIndex = 27;
            bttnScriptMoveDown.Text = "▼";
            bttnScriptMoveDown.TextAlign = ContentAlignment.MiddleCenter;
            bttnScriptMoveDown.Click += (a, b) => { OnMoveDown?.Invoke(); };
            #endregion

            #region bttnScriptDelete
            bttnScriptDelete.Anchor = AnchorStyles.None;
            bttnScriptDelete.Margin = new Padding(4, 3, 4, 3);
            bttnScriptDelete.Size = new Size(52, 32);
            bttnScriptDelete.TabIndex = 27;
            bttnScriptDelete.Text = Strings.Delete;
            bttnScriptDelete.TextAlign = ContentAlignment.MiddleCenter;
            bttnScriptDelete.Click += (a, b) => { OnDelete?.Invoke(); };
            #endregion

            #region nameTriggerAndDeleteOrderButtonsContainer
            nameTriggerAndDeleteOrderButtonsContainer.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            nameTriggerAndDeleteOrderButtonsContainer.AutoSize = true;
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(txtScriptName);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(txtbxScriptName);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(txtScriptTrigger);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(cmbxScriptTrigger);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(bttnScriptMoveUp);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(bttnScriptMoveDown);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(bttnScriptDelete);
            nameTriggerAndDeleteOrderButtonsContainer.TabIndex = 31;
            #endregion

            #region txtScriptAuthor
            txtScriptAuthor.AutoSize = true;
            txtScriptAuthor.Text = Strings.ScriptAuthor;

            #endregion

            #region txtbxScriptAuthor
            txtbxScriptAuthor.BorderStyle = BorderStyle.FixedSingle;
            txtbxScriptAuthor.MinimumSize = new Size(64, 20);
            txtbxScriptAuthor.MaximumSize = new Size(200, 20);
            txtbxScriptAuthor.Margin = new Padding(0, 3, 0, 3);
            txtbxScriptAuthor.Text = Script.Author;
            txtbxScriptAuthor.TextChanged += (a, b) => { Script.Author = txtbxScriptAuthor.Text; };
            #endregion

            #region txtScriptDescription
            txtScriptDescription.AutoSize = true;
            txtScriptDescription.Text = Strings.ScriptDescription;
            #endregion

            #region txtbxScriptDescription
            txtbxScriptDescription.AcceptsReturn = true;
            txtbxScriptDescription.BorderStyle = BorderStyle.FixedSingle;
            txtbxScriptDescription.Size = new Size(529, 50);
            txtbxScriptDescription.Margin = new Padding(0, 3, 0, 3);
            txtbxScriptDescription.Multiline = true;
            txtbxScriptDescription.Text = Script.Description;
            txtbxScriptDescription.TextChanged += (a, b) => { Script.Description = txtbxScriptDescription.Text; };
            #endregion

            #region txtScriptAction
            txtScriptAction.AutoSize = true;
            txtScriptAction.Text = Strings.ScriptData;
            #endregion

            #region txtbxScriptAction
            txtbxScriptAction.AcceptsReturn = true;
            txtbxScriptAction.BorderStyle = BorderStyle.FixedSingle;
            txtbxScriptAction.Size = new Size(529, 80);
            txtbxScriptAction.Margin = new Padding(0, 3, 0, 3);
            txtbxScriptAction.Multiline = true;
            txtbxScriptAction.ScrollBars = ScrollBars.Vertical;
            txtbxScriptAction.Text = Script.Action;
            txtbxScriptAction.TextChanged += (a, b) => { Script.Action = txtbxScriptAction.Text; };
            #endregion

            #region authorDescriptionAndActionContainer
            authorDescriptionAndActionContainer.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            authorDescriptionAndActionContainer.AutoSize = true;
            authorDescriptionAndActionContainer.Controls.Add(txtScriptAuthor);
            authorDescriptionAndActionContainer.Controls.Add(txtbxScriptAuthor);
            authorDescriptionAndActionContainer.Controls.Add(txtScriptDescription);
            authorDescriptionAndActionContainer.Controls.Add(txtbxScriptDescription);
            authorDescriptionAndActionContainer.Controls.Add(txtScriptAction);
            authorDescriptionAndActionContainer.Controls.Add(txtbxScriptAction);
            authorDescriptionAndActionContainer.FlowDirection = FlowDirection.TopDown;
            authorDescriptionAndActionContainer.TabIndex = 31;
            #endregion

            #region outerContainer
            outerContainer.Anchor = AnchorStyles.Top | AnchorStyles.Left;
            outerContainer.AutoSize = true;
            outerContainer.Controls.Add(nameTriggerAndDeleteOrderButtonsContainer);
            outerContainer.Controls.Add(authorDescriptionAndActionContainer);
            outerContainer.FlowDirection = FlowDirection.TopDown;
            outerContainer.Margin = new Padding(4);
            outerContainer.TabIndex = 31;
            #endregion

            #region EditScripts
            AutoSize = true;
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            Controls.Add(outerContainer);
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Margin = new Padding(4, 3, 4, 3);
            Text = Strings.DialogEditScriptsTitle;
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            ResumeLayout(false);
        }

        /// <summary>
        /// Handles whether move up/down are enabled.
        /// </summary>
        /// <param name="isFirst">Pass true if this is the first script among all scripts.</param>
        /// <param name="isLast">Pass true if this is the last script among all scripts.</param>
        /// <param name="isOnlyScript">Pass true if this is there is only one script in the scripts list.</param>
        public void UpdateScriptControls(bool isFirst, bool isLast, bool isOnlyScript)
        {
            bttnScriptMoveUp.Enabled = !isOnlyScript && !isFirst;
            bttnScriptMoveDown.Enabled = !isOnlyScript && !isLast;
        }
        #endregion

        #region Methods (event handlers)
        private void CmbxScriptTrigger_SelectedIndexChanged(object sender, EventArgs e)
        {
            Script.Trigger = triggerOptions[cmbxScriptTrigger.SelectedIndex].Item2;
        }

        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            txtScriptAction.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtScriptAuthor.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtScriptDescription.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtScriptName.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtScriptTrigger.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);

            txtbxScriptAuthor.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            txtbxScriptAuthor.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtbxScriptAction.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            txtbxScriptAction.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtbxScriptDescription.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            txtbxScriptDescription.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            txtbxScriptName.BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            txtbxScriptName.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);

            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            BorderStyle = BorderStyle.FixedSingle;
            Refresh();
        }
        #endregion
    }
}
