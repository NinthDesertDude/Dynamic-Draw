using DynamicDraw.Localization;
using System;
using System.Windows.Forms;

namespace DynamicDraw
{
    public class ThemedMessageBox : Form
    {
        private bool showSecondButton = true;

        #region Gui Members
        private FlowLayoutPanel panelFlowContainer;
        private Label txtDescription;
        private FlowLayoutPanel panelBttnContainer;
        private ThemedButton bttnAccept, bttnDecline;
        #endregion

        /// <summary>
        /// Creates a new themed messagebox with no caption, an OK button, and a description.
        /// </summary>
        public ThemedMessageBox(string descrText)
            : this(descrText, "", MessageBoxButtons.OK) {}

        /// <summary>
        /// Creates a new themed messagebox with a caption, description, and an OK button.
        /// </summary>
        public ThemedMessageBox(string descrText, string titleText)
            : this(descrText, titleText, MessageBoxButtons.OK) { }

        /// <summary>
        /// Creates a new themed messagebox with a caption, description, and set of buttons that determine the dialog
        /// result when clicked. Not all values in <see cref="MessageBoxButtons"/> are supported right now.
        /// </summary>
        public ThemedMessageBox(string descrText, string titleText, MessageBoxButtons buttons)
        {
            if (buttons == MessageBoxButtons.OK)
            {
                showSecondButton = false;
            }

            SetupGui();

            if (buttons == MessageBoxButtons.OK)
            {
                bttnAccept.Text = Strings.Ok;
                bttnAccept.DialogResult = DialogResult.OK;
            }
            else if (buttons == MessageBoxButtons.OKCancel)
            {
                bttnAccept.Text = Strings.Ok;
                bttnDecline.Text = Strings.Cancel;
                bttnAccept.DialogResult = DialogResult.OK;
                bttnDecline.DialogResult = DialogResult.Cancel;
            }
            else if (buttons == MessageBoxButtons.YesNo)
            {
                bttnAccept.Text = Strings.Yes;
                bttnDecline.Text = Strings.No;
                bttnAccept.DialogResult = DialogResult.Yes;
                bttnDecline.DialogResult = DialogResult.No;
            }
            else
            {
                throw new Exception("Not all messageboxbutton types are supported, you may want to add this: "
                    + Enum.GetName(typeof(MessageBoxButtons), buttons));
            }

            Text = titleText;
            txtDescription.Margin = new Padding(4, 8, 4, 8);
            txtDescription.Text = descrText;
            txtDescription.Visible = !string.IsNullOrEmpty(descrText);
            bttnAccept.Click += (a, b) => { DialogResult = bttnAccept.DialogResult; Close(); };

            if (showSecondButton)
            {
                bttnDecline.Click += (a, b) => { DialogResult = bttnDecline.DialogResult; Close(); };
            }

            KeyDown += ThemedMessageBox_KeyDown;
            KeyPreview = true;
        }

        #region Methods (event handlers)
        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            txtDescription.ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            Refresh();
        }

        private void ThemedMessageBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = bttnDecline.DialogResult;
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                if (bttnAccept.Enabled)
                {
                    DialogResult = bttnAccept.DialogResult;
                    Close();
                }
            }
        }
        #endregion

        #region Methods (not event handlers)
        /// <summary>
        /// Initial layout logic, should only be called once.
        /// </summary>
        private void SetupGui()
        {
            panelFlowContainer = new FlowLayoutPanel();
            txtDescription = new Label();
            panelBttnContainer = new FlowLayoutPanel();
            bttnAccept = new ThemedButton();
            bttnDecline = new ThemedButton();
            panelFlowContainer.SuspendLayout();
            panelBttnContainer.SuspendLayout();
            SuspendLayout();

            #region panelFlowContainer
            panelFlowContainer.AutoSize = true;
            panelFlowContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelFlowContainer.Controls.Add(this.txtDescription);
            panelFlowContainer.Controls.Add(this.panelBttnContainer);
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

            #region panelBttnContainer
            panelBttnContainer.AutoSize = true;
            panelBttnContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelBttnContainer.Controls.Add(bttnAccept);
            if (showSecondButton)
            {
                panelBttnContainer.Controls.Add(bttnDecline);
            }
            panelBttnContainer.Location = new System.Drawing.Point(3, 16);
            panelBttnContainer.Size = new System.Drawing.Size(260, 29);
            panelBttnContainer.TabIndex = 3;
            #endregion

            #region bttnAccept
            bttnAccept.Location = new System.Drawing.Point(209, 3);
            bttnAccept.Size = new System.Drawing.Size(48, 23);
            bttnAccept.TabIndex = 2;
            #endregion

            #region bttnDecline
            if (showSecondButton)
            {
                bttnDecline.Location = new System.Drawing.Point(209, 3);
                bttnDecline.Size = new System.Drawing.Size(48, 23);
                bttnDecline.TabIndex = 2;
            }
            #endregion

            #region ThemedMessageBox
            AcceptButton = bttnAccept;
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
            panelBttnContainer.ResumeLayout(false);
            panelBttnContainer.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        /// <summary>
        /// Creates a new themed messagebox with no caption, an OK button, and a description.
        /// </summary>
        public static DialogResult Show(string descrText)
        {
            using ThemedMessageBox box = new ThemedMessageBox(descrText);
            return box.ShowDialog();
        }

        /// <summary>
        /// Creates a new themed messagebox with a caption, description, and an OK button.
        /// </summary>
        public static DialogResult Show(string descrText, string titleText)
        {
            using ThemedMessageBox box = new ThemedMessageBox(descrText, titleText);
            return box.ShowDialog();
        }

        /// <summary>
        /// Creates a new themed messagebox with a caption, description, and set of buttons that determine the dialog
        /// result when clicked. Not all values in <see cref="MessageBoxButtons"/> are supported right now.
        /// </summary>
        public static DialogResult Show(string descrText, string titleText, MessageBoxButtons buttons)
        {
            using ThemedMessageBox box = new ThemedMessageBox(descrText, titleText, buttons);
            return box.ShowDialog();
        }
        #endregion
    }
}
