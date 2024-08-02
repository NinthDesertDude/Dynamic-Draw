using System;
using System.Windows.Forms;
using DynamicDraw.Properties;
using PaintDotNet;

namespace DynamicDraw.Gui
{
    [System.ComponentModel.DesignerCategory("")]
    public class EditScript : PdnBaseForm
    {
        public Script script { get; private set; }

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
        private TextBox cmbxScriptTrigger = new TextBox();
        private Label txtScriptAction = new Label();
        private TextBox txtbxScriptAction = new TextBox();
        #endregion

        /// <summary>
        /// Creates a script-editing GUI with a mutable script object (not a copy).
        /// </summary>
        /// <param name="script">A script that will be mutated directly as this data changes.</param>
        public EditScript(Script script)
        {
            this.script = script ?? new Script();
            SetupGui();
            CenterToScreen();
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

            // TODO: Set up txtScriptName, txtbxScriptName, cmbxScriptTrigger here
            // and behaviors for script name and trigger

            #region bttnScriptMoveUp
            bttnScriptMoveUp.Anchor = AnchorStyles.None;
            bttnScriptMoveUp.Margin = new Padding(4, 3, 4, 3);
            bttnScriptMoveUp.Size = new System.Drawing.Size(40, 40);
            bttnScriptMoveUp.TabIndex = 28;
            bttnScriptMoveUp.Text = "▲";
            bttnScriptMoveUp.Click += (a, b) => { OnMoveUp?.Invoke(); };
            #endregion

            #region bttnScriptMoveDown
            bttnScriptMoveDown.Anchor = AnchorStyles.None;
            bttnScriptMoveDown.Margin = new Padding(4, 3, 4, 3);
            bttnScriptMoveDown.Size = new System.Drawing.Size(40, 40);
            bttnScriptMoveDown.TabIndex = 27;
            bttnScriptMoveDown.Text = "▼";
            bttnScriptMoveDown.Click += (a, b) => { OnMoveDown?.Invoke(); };
            #endregion

            #region bttnScriptDelete
            bttnScriptDelete.Anchor = AnchorStyles.None;
            bttnScriptDelete.Margin = new Padding(4, 3, 4, 3);
            bttnScriptDelete.Size = new System.Drawing.Size(40, 40);
            bttnScriptDelete.TabIndex = 27;
            bttnScriptDelete.Text = Localization.Strings.Delete;
            bttnScriptDelete.Click += (a, b) => { OnDelete?.Invoke(); };
            #endregion

            #region nameTriggerAndDeleteOrderButtonsContainer
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(txtScriptName);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(txtbxScriptName);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(txtScriptTrigger);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(cmbxScriptTrigger);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(bttnScriptMoveUp);
            nameTriggerAndDeleteOrderButtonsContainer.Controls.Add(bttnScriptMoveDown);
            nameTriggerAndDeleteOrderButtonsContainer.Margin = new Padding(4, 3, 4, 3);
            nameTriggerAndDeleteOrderButtonsContainer.TabIndex = 31;
            #endregion

            // TODO: Set up author, description, action
            // and behaviors for script action

            #region authorDescriptionAndActionContainer
            authorDescriptionAndActionContainer.Controls.Add(txtScriptAuthor);
            authorDescriptionAndActionContainer.Controls.Add(txtbxScriptAuthor);
            authorDescriptionAndActionContainer.Controls.Add(txtScriptDescription);
            authorDescriptionAndActionContainer.Controls.Add(txtbxScriptDescription);
            authorDescriptionAndActionContainer.Controls.Add(txtScriptAction);
            authorDescriptionAndActionContainer.Controls.Add(txtbxScriptAction);
            authorDescriptionAndActionContainer.FlowDirection = FlowDirection.TopDown;
            authorDescriptionAndActionContainer.Margin = new Padding(4, 3, 4, 3);
            authorDescriptionAndActionContainer.TabIndex = 31;
            #endregion

            #region outerContainer
            outerContainer.Controls.Add(nameTriggerAndDeleteOrderButtonsContainer);
            outerContainer.Controls.Add(authorDescriptionAndActionContainer);
            outerContainer.Dock = DockStyle.Fill;
            outerContainer.Location = new System.Drawing.Point(0, 0);
            outerContainer.Margin = new Padding(4, 3, 4, 3);
            outerContainer.Size = new System.Drawing.Size(565, 324);
            outerContainer.TabIndex = 31;
            #endregion

            #region EditScripts
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(565, 324);
            Controls.Add(outerContainer);
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            Icon = Resources.Icon;
            KeyPreview = true;
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Text = Localization.Strings.DialogEditScriptsTitle;
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            ResumeLayout(false);
        }
        #endregion

        #region Methods (event handlers)
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
            Refresh();
        }
        #endregion
    }
}
