using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using DynamicDraw.Localization;
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
        private ThemedButton bttnNewScript, bttnSave, bttnCancel;
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

            if (this.scripts == null)
            {
                this.scripts = new ToolScripts();
            }
            else if (this.scripts.Scripts == null)
            {
                this.scripts.Scripts = new List<Script>();
            }

            // Automatically create a new script if none exist.
            if (this.scripts.Scripts.Count == 0)
            {
                this.scripts.Scripts.Add(new Script()
                {
                    Trigger = ScriptTrigger.StartBrushStroke
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
            bttnNewScript = new ThemedButton();
            bttnSave = new ThemedButton();
            bttnCancel = new ThemedButton();
            brushScriptsContainer = new FlowLayoutPanel();
            outerContainer = new FlowLayoutPanel();
            cancelSaveContainer = new FlowLayoutPanel();

            brushScriptsContainer.SuspendLayout();
            outerContainer.SuspendLayout();
            cancelSaveContainer.SuspendLayout();
            SuspendLayout();

            #region bttnNewScript
            bttnNewScript.Margin = new Padding(4, 3, 97, 3);
            bttnNewScript.Size = new System.Drawing.Size(140, 40);
            bttnNewScript.TabIndex = 28;
            bttnNewScript.Text = Localization.Strings.ScriptAddNewScript;
            bttnNewScript.Click += BttnNewScript_Click;
            #endregion

            #region bttnSave
            bttnSave.Margin = new Padding(4, 3, 4, 3);
            bttnSave.Size = new System.Drawing.Size(140, 40);
            bttnSave.TabIndex = 28;
            bttnSave.Text = Localization.Strings.Save;
            bttnSave.Click += BttnSave_Click;
            #endregion

            #region bttnCancel
            bttnCancel.DialogResult = DialogResult.Cancel;
            bttnCancel.Margin = new Padding(4, 3, 4, 3);
            bttnCancel.Size = new System.Drawing.Size(140, 40);
            bttnCancel.TabIndex = 27;
            bttnCancel.Text = Localization.Strings.Cancel;
            bttnCancel.Click += this.BttnCancel_Click;
            #endregion

            #region brushScriptsContainer
            // Create the GUI for each script
            foreach (var script in scripts?.Scripts)
            {
                SetupGuiScript(script);
            }

            brushScriptsContainer.AutoSize = true;
            brushScriptsContainer.TabIndex = 30;
            brushScriptsContainer.Height = 500;
            #endregion

            #region cancelSaveContainer
            cancelSaveContainer.AutoSize = true;
            cancelSaveContainer.Controls.Add(bttnCancel);
            cancelSaveContainer.Controls.Add(bttnSave);
            cancelSaveContainer.Controls.Add(bttnNewScript);
            cancelSaveContainer.FlowDirection = FlowDirection.RightToLeft;
            cancelSaveContainer.Margin = new Padding(6, 3, 0, 3);
            cancelSaveContainer.TabIndex = 3;
            #endregion

            #region outerContainer
            outerContainer.AutoScroll = true;
            outerContainer.Controls.Add(brushScriptsContainer);
            outerContainer.Controls.Add(cancelSaveContainer);
            outerContainer.Dock = DockStyle.Fill;
            outerContainer.Location = new System.Drawing.Point(0, 0);
            outerContainer.Margin = new Padding(0, 3, 0, 3);
            outerContainer.Size = new System.Drawing.Size(555, 324);
            outerContainer.TabIndex = 31;
            #endregion

            #region EditScripts
            AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(572, 324);
            Controls.Add(outerContainer);
            Height = 600;
            Icon = Resources.Icon;
            KeyPreview = true;
            Margin = new Padding(4, 3, 4, 3);
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            Text = Strings.DialogEditScriptsTitle;
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

        /// <summary>
        /// Creates and hooks up a gui script control, adding it to relevant lists/gui so it displays.
        /// </summary>
        private void SetupGuiScript(Script script)
        {
            var scriptGui = new EditScript(script);

            scriptGui.OnMoveDown = new Action(() =>
            {
                int index = scripts.Scripts.IndexOf(script);
                scripts.Scripts.RemoveAt(index);
                scriptsGui.RemoveAt(index);
                scripts.Scripts.Insert(index + 1, script);
                scriptsGui.Insert(index + 1, scriptGui);

                UpdateGuiScriptPosControlsAndDelete();
            });

            scriptGui.OnMoveUp = new Action(() =>
            {
                int index = scripts.Scripts.IndexOf(script);
                if (index != 0)
                {
                    scripts.Scripts.RemoveAt(index);
                    scriptsGui.RemoveAt(index);
                    scripts.Scripts.Insert(index - 1, script);
                    scriptsGui.Insert(index - 1, scriptGui);
                }

                UpdateGuiScriptPosControlsAndDelete();
            });

            scriptGui.OnDelete = new Action(() =>
            {
                ThemedMessageBox dlg = new ThemedMessageBox(Strings.ConfirmDeleteScript, "", MessageBoxButtons.YesNo);
                if (dlg.ShowDialog() == DialogResult.Yes)
                {
                    var guiIndex = scriptsGui.FindIndex((gui) => gui.Script == script);

                    scripts.Scripts.Remove(script);
                    scriptsGui.RemoveAt(guiIndex);

                    UpdateGuiScriptPosControlsAndDelete();
                }
            });

            scriptsGui.Add(scriptGui);
            brushScriptsContainer.Controls.Add(scriptGui);

            scriptsGui[^1].UpdateScriptControls(
                scriptsGui.Count == 1, true, scriptsGui.Count == 1);
        }

        /// <summary>
        /// Updates all gui script's positional controls, including the delete button.
        /// </summary>
        private void UpdateGuiScriptPosControlsAndDelete()
        {
            brushScriptsContainer.Controls.Clear();

            for (int i = 0; i < scriptsGui.Count; i++)
            {
                scriptsGui[i].UpdateScriptControls(
                    i == 0,
                    i == scriptsGui.Count - 1,
                    scriptsGui.Count == 1);

                brushScriptsContainer.Controls.Add(scriptsGui[i]);
            }
        }
        #endregion

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
        /// Creates and adds a new script, copying the most recent author info if available.
        /// </summary>
        private void BttnNewScript_Click(object sender, EventArgs e)
        {
            Script newScript = new Script()
            {
                Author = this.scripts.Scripts.Count > 0 ? this.scripts.Scripts[^1].Author : "",
                Trigger = ScriptTrigger.StartBrushStroke
            };

            scripts.Scripts.Add(newScript);
            SetupGuiScript(newScript);
            UpdateGuiScriptPosControlsAndDelete();
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
                if (string.IsNullOrWhiteSpace(scriptsGui[i].Script.Author) &&
                    string.IsNullOrWhiteSpace(scriptsGui[i].Script.Description) &&
                    string.IsNullOrWhiteSpace(scriptsGui[i].Script.Action) &&
                    string.IsNullOrWhiteSpace(scriptsGui[i].Script.Name) &&
                    scriptsGui[i].Script.Trigger == ScriptTrigger.Disabled)
                {
                    scripts.Scripts.Remove(scriptsGui[i].Script);
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
