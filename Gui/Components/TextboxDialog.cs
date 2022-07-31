using System;
using System.Windows.Forms;

namespace DynamicDraw
{
    public class TextboxDialog : Form
    {
        private Func<string, string> validationFunc;

        #region Gui Members
        private System.ComponentModel.IContainer components = null;
        private FlowLayoutPanel panelFlowContainer;
        private Label txtDescription;
        private TextBox txtbxInput;
        private Label txtError;
        private FlowLayoutPanel flowLayoutPanel1;
        private BasicButton bttnOk;
        #endregion

        /// <summary>
        /// A dialog with a labeled textbox that has input validation; only if valid (null returned), the OK button is
        /// enabled. An empty string is considered an error where nothing is displayed (this is a common usecase if the
        /// text input is empty so that you're not scolding the user who hasn't typed anything yet).
        /// </summary>
        /// <param name="titleText">The caption for the dialog form.</param>
        /// <param name="descrText">The textbox label, which is on the left on the same line as the textbox.</param>
        /// <param name="btnOkText">The text for the OK button.</param>
        /// <param name="validateFunc">
        /// A function taking one argument that is the updated text, and returning a string indicating the error, if
        /// any. Null is error-free. An empty string is an error, but it's hidden. Any other string is shown as the
        /// error message itself.
        /// </param>
        public TextboxDialog(string titleText, string descrText, string btnOkText, Func<string, string> validateFunc)
        {
            SetupGui();

            Text = titleText;
            txtDescription.Text = descrText;
            txtDescription.Visible = !string.IsNullOrEmpty(descrText);
            bttnOk.Text = btnOkText;

            validationFunc = validateFunc;
            txtbxInput.TextChanged += TxtbxInput_TextChanged;
            AcceptButton.DialogResult = DialogResult.OK;
            Load += TxtbxInput_TextChanged; // Run validation when form is displayed.
            KeyDown += TextboxDialog_KeyDown;
            KeyPreview = true;
        }

        #region Methods (overrides)
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
        #endregion

        #region Methods (event handlers)
        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            txtbxInput.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            txtbxInput.BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            txtDescription.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlText);
            txtError.ForeColor = SemanticTheme.GetColor(ThemeSlot.MenuControlRedAccent);
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            Refresh();
        }

        private void TextboxDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (bttnOk.Enabled)
                {
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Gets the text that the user submitted through the textbox field. Intended only to be called after the
        /// dialog has completed with a value of DialogResult.OK.
        /// </summary>
        public string GetSubmittedText()
        {
            return this.txtbxInput.Text;
        }

        /// <summary>
        /// Initial layout logic, should only be called once.
        /// </summary>
        private void SetupGui()
        {
            panelFlowContainer = new FlowLayoutPanel();
            txtDescription = new Label();
            flowLayoutPanel1 = new FlowLayoutPanel();
            txtbxInput = new TextBox();
            bttnOk = new BasicButton();
            txtError = new Label();
            panelFlowContainer.SuspendLayout();
            flowLayoutPanel1.SuspendLayout();
            SuspendLayout();

            #region panelFlowContainer
            panelFlowContainer.AutoSize = true;
            panelFlowContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelFlowContainer.Controls.Add(this.txtDescription);
            panelFlowContainer.Controls.Add(this.flowLayoutPanel1);
            panelFlowContainer.Controls.Add(this.txtError);
            panelFlowContainer.Dock = DockStyle.Fill;
            panelFlowContainer.FlowDirection = FlowDirection.TopDown;
            panelFlowContainer.Size = new System.Drawing.Size(800, 450);
            #endregion

            #region txtDescription
            txtDescription.AutoSize = true;
            txtDescription.Location = new System.Drawing.Point(3, 0);
            txtDescription.MaximumSize = new System.Drawing.Size(260, 0);
            txtDescription.Size = new System.Drawing.Size(60, 13);
            txtDescription.TabIndex = 0;
            #endregion

            #region flowLayoutPanel1
            flowLayoutPanel1.AutoSize = true;
            flowLayoutPanel1.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            flowLayoutPanel1.Controls.Add(this.txtbxInput);
            flowLayoutPanel1.Controls.Add(this.bttnOk);
            flowLayoutPanel1.Location = new System.Drawing.Point(3, 16);
            flowLayoutPanel1.Size = new System.Drawing.Size(260, 29);
            flowLayoutPanel1.TabIndex = 3;
            #endregion

            #region txtbxInput
            txtbxInput.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            txtbxInput.Location = new System.Drawing.Point(3, 4);
            txtbxInput.MinimumSize = new System.Drawing.Size(200, 4);
            txtbxInput.Size = new System.Drawing.Size(200, 20);
            txtbxInput.TabIndex = 1;
            #endregion

            #region bttnOk
            bttnOk.Location = new System.Drawing.Point(209, 3);
            bttnOk.Size = new System.Drawing.Size(48, 23);
            bttnOk.TabIndex = 2;
            #endregion

            #region txtError
            txtError.AutoSize = true;
            txtError.Location = new System.Drawing.Point(3, 48);
            txtError.Size = new System.Drawing.Size(29, 13);
            txtError.TabIndex = 2;
            txtError.Visible = false;
            #endregion

            #region TextboxDialog
            AcceptButton = bttnOk;
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new System.Drawing.Size(800, 450);
            Controls.Add(panelFlowContainer);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;
            TopMost = true;
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            panelFlowContainer.ResumeLayout(false);
            panelFlowContainer.PerformLayout();
            flowLayoutPanel1.ResumeLayout(false);
            flowLayoutPanel1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>
        /// Evaluates whether input is valid or not on each character change.
        /// </summary>
        private void TxtbxInput_TextChanged(object sender, EventArgs e)
        {
            // Runs the provided validation function. If it gives a non-null, non-empty string back, that is treated as
            // an error message and displayed. Otherwise, no error is considered to exist.
            string error = this.validationFunc(txtbxInput.Text);

            txtError.Visible = !string.IsNullOrEmpty(error);
            bttnOk.Enabled = (error == null);

            if (txtError.Visible)
            {
                txtError.Text = error;
            }
        }
        #endregion
    }
}
