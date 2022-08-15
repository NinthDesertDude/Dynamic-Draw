using DynamicDraw.Localization;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Windows.Forms;

namespace DynamicDraw
{
    public class CommandDialog : Form
    {
        private Command target = null;
        private static BindingList<Tuple<string, Command>> queryToTargetMapping = new BindingList<Tuple<string, Command>>();

        #region Gui Members
        private FlowLayoutPanel panelFlowContainer;
        private ThemedComboBox cmbxInput;
        #endregion

        /// <summary>
        /// The command target chosen/identified by the dialog. Defaults to null.
        /// </summary>
        public Command ShortcutToExecute
        {
            get
            {
                return target;
            }
        }

        /// <summary>
        /// The quick command dialog allows you to execute the shortcuts provided by typing them in.
        /// </param>
        public CommandDialog(HashSet<Command> shortcuts)
        {
            SetupGui();

            // Filters shortcuts set to be excluded, sorts alphabetically.
            var orderedShortcuts = new HashSet<Command>(shortcuts).Where((command) => !command.CommandDialogIgnore)
                .OrderBy((command) => command.Name)
                .ToList();

            queryToTargetMapping.Clear();
            foreach (var shortcut in orderedShortcuts)
            {
                queryToTargetMapping.Add(new Tuple<string, Command>(shortcut.Name, shortcut));
            }

            cmbxInput.DisplayMember = "Item1";
            cmbxInput.ValueMember = "Item2";
            cmbxInput.DataSource = queryToTargetMapping;
        }

        private void AcceptAndClose()
        {
            int index = Math.Max(cmbxInput.SelectedIndex, 0);
            target = ((Tuple<string, Command>)cmbxInput.Items[index]).Item2;

            DialogResult = DialogResult.OK;
            Close();
        }

        #region Methods (event handlers)
        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            Refresh();
        }

        private void CommandDialog_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
                Close();
            }
            else if (e.KeyCode == Keys.Enter)
            {
                AcceptAndClose();
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
            cmbxInput = new ThemedComboBox();
            panelFlowContainer.SuspendLayout();
            SuspendLayout();

            #region panelFlowContainer
            panelFlowContainer.AutoSize = true;
            panelFlowContainer.AutoSizeMode = AutoSizeMode.GrowAndShrink;
            panelFlowContainer.Controls.Add(cmbxInput);
            panelFlowContainer.Dock = DockStyle.Fill;
            panelFlowContainer.FlowDirection = FlowDirection.TopDown;
            panelFlowContainer.Size = new System.Drawing.Size(800, 450);
            #endregion

            #region cmbxInput
            cmbxInput.Anchor = AnchorStyles.Left | AnchorStyles.Right;
            cmbxInput.Location = new System.Drawing.Point(3, 4);
            cmbxInput.MinimumSize = new System.Drawing.Size(200, 4);
            cmbxInput.Size = new System.Drawing.Size(200, 20);
            cmbxInput.TabIndex = 1;
            #endregion

            #region TextboxDialog
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = AutoScaleMode.Font;
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;
            ClientSize = new System.Drawing.Size(800, 450);
            Controls.Add(panelFlowContainer);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            KeyPreview = true;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowIcon = false;
            ShowInTaskbar = false;
            SizeGripStyle = SizeGripStyle.Hide;
            StartPosition = FormStartPosition.CenterParent;
            Text = Strings.Command;
            TopMost = true;
            KeyDown += CommandDialog_KeyDown;
            #endregion

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();

            panelFlowContainer.ResumeLayout(false);
            panelFlowContainer.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }
        #endregion
    }
}
