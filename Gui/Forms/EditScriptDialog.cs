using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DynamicDraw.Properties;
using PaintDotNet;

namespace DynamicDraw.Gui
{
    [System.ComponentModel.DesignerCategory("")]
    public class EditScriptDialog : PdnBaseForm
    {
        private readonly ToolScripts scripts;
        private readonly List<EditScript> scriptsGui;

        #region Gui Members
        private ThemedButton bttnSave, bttnCancel;
        private FlowLayoutPanel outerContainer;
        private FlowLayoutPanel brushScriptsContainer;
        private FlowLayoutPanel cancelSaveContainer;
        #endregion

        /// <summary>
        /// Creates a dialog to edit all scripts in a new copy of the given toolscript instance. After the dialog is
        /// accepted, read the final data with <see cref="GetScriptsAfterDialogOK"/>.
        /// </summary>
        public EditScriptDialog(ToolScripts scripts)
        {
            this.scripts = new ToolScripts(scripts) ?? new ToolScripts();
            scriptsGui = new List<EditScript>();

            // Automatically create a new script if none exist.
            if (scripts.Scripts.Count == 0)
            {
                scripts.Scripts.Add(new Script()
                {
                    Trigger = ScriptTrigger.OnBrushStroke
                });
            }

            SetupGui();
            CenterToScreen();
        }

        #region Methods (overridden)
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
        public ToolScripts GetScriptsAfterDialogOK()
        {
            return scripts;
        }

        private void SetupGui()
        {
            bttnSave = new ThemedButton();
            bttnCancel = new ThemedButton();
            brushScriptsContainer = new FlowLayoutPanel();
            outerContainer = new FlowLayoutPanel();
            cancelSaveContainer = new FlowLayoutPanel();

            // Create the GUI for each script
            foreach (var script in scripts?.Scripts)
            {
                scriptsGui.Add(new EditScript(script));
            }

            brushScriptsContainer.SuspendLayout();
            outerContainer.SuspendLayout();
            cancelSaveContainer.SuspendLayout();
            SuspendLayout();

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

            #region brushScriptsContainer
            // Append each script's GUI controls
            foreach (var gui in scriptsGui)
            {
                brushScriptsContainer.Controls.Add(gui);
            }

            brushScriptsContainer.Location = new System.Drawing.Point(4, 3);
            brushScriptsContainer.Margin = new Padding(4, 3, 4, 3);
            brushScriptsContainer.Size = new System.Drawing.Size(537, 207);
            brushScriptsContainer.TabIndex = 30;
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

            #region outerContainer
            outerContainer.Controls.Add(brushScriptsContainer);
            outerContainer.Controls.Add(cancelSaveContainer);
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

            brushScriptsContainer.ResumeLayout(false);
            brushScriptsContainer.PerformLayout();
            outerContainer.ResumeLayout(false);
            cancelSaveContainer.ResumeLayout(false);
            ResumeLayout(false);
        }

        #region Methods (event handlers)
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
            // Disables the button so it can't accidentally be called twice.
            // Ensures settings will be saved.
            bttnSave.Enabled = false;

            // Dissociate and remove any empty scripts before saving.
            for (int i = 0; i < scriptsGui.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(scriptsGui[i].script.Author) &&
                    string.IsNullOrWhiteSpace(scriptsGui[i].script.Description) &&
                    string.IsNullOrWhiteSpace(scriptsGui[i].script.Action) &&
                    string.IsNullOrWhiteSpace(scriptsGui[i].script.Name) &&
                    scriptsGui[i].script.Trigger == ScriptTrigger.Disabled)
                {
                    scripts.Scripts.Remove(scriptsGui[i].script);
                    brushScriptsContainer.Controls.Remove(scriptsGui[i]);
                    scriptsGui.RemoveAt(i);
                    i--;
                }
            }

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
                !scriptsGui.Any(gui => gui.IsTextboxFocused()))
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
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuBg);
            Refresh();
        }
        #endregion
    }
}
