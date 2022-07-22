using System.Windows.Forms;

namespace DynamicDraw
{
    partial class EditKeyboardShortcuts
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.txtKeyboardShortcuts = new System.Windows.Forms.Label();
            this.bttnSave = new System.Windows.Forms.Button();
            this.bttnCancel = new System.Windows.Forms.Button();
            this.bttnAddShortcut = new System.Windows.Forms.Button();
            this.tooltip = new System.Windows.Forms.ToolTip(this.components);
            this.panelShortcuts = new System.Windows.Forms.FlowLayoutPanel();
            this.shortcutsListBox = new System.Windows.Forms.ListBox();
            this.panelOuterContainer = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlAddEditBar = new System.Windows.Forms.FlowLayoutPanel();
            this.pnlTargetAndShortcut = new System.Windows.Forms.FlowLayoutPanel();
            this.cmbxShortcutTarget = new System.Windows.Forms.ComboBox();
            this.bttnShortcutSequence = new System.Windows.Forms.Button();
            this.pnlWheelCheckboxes = new System.Windows.Forms.FlowLayoutPanel();
            this.chkbxShortcutWheelUp = new System.Windows.Forms.CheckBox();
            this.chkbxShortcutWheelDown = new System.Windows.Forms.CheckBox();
            this.pnlShortcutExtraData = new System.Windows.Forms.FlowLayoutPanel();
            this.txtbxShortcutActionData = new System.Windows.Forms.TextBox();
            this.panelShortcutControls = new System.Windows.Forms.FlowLayoutPanel();
            this.bttnEditShortcut = new System.Windows.Forms.Button();
            this.bttnDeleteShortcut = new System.Windows.Forms.Button();
            this.panelSaveCancel = new System.Windows.Forms.FlowLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.bttnRestoreDefaults = new System.Windows.Forms.Button();
            this.panelShortcuts.SuspendLayout();
            this.panelOuterContainer.SuspendLayout();
            this.pnlAddEditBar.SuspendLayout();
            this.pnlTargetAndShortcut.SuspendLayout();
            this.pnlWheelCheckboxes.SuspendLayout();
            this.pnlShortcutExtraData.SuspendLayout();
            this.panelShortcutControls.SuspendLayout();
            this.panelSaveCancel.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtKeyboardShortcuts
            // 
            this.txtKeyboardShortcuts.AutoSize = true;
            this.txtKeyboardShortcuts.Location = new System.Drawing.Point(4, 0);
            this.txtKeyboardShortcuts.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.txtKeyboardShortcuts.Name = "txtKeyboardShortcuts";
            this.txtKeyboardShortcuts.Size = new System.Drawing.Size(110, 15);
            this.txtKeyboardShortcuts.TabIndex = 2;
            this.txtKeyboardShortcuts.Text = "Keyboard Shortcuts";
            // 
            // bttnSave
            // 
            this.bttnSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.bttnSave.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnSave.Location = new System.Drawing.Point(392, 3);
            this.bttnSave.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bttnSave.Name = "bttnSave";
            this.bttnSave.Size = new System.Drawing.Size(140, 40);
            this.bttnSave.TabIndex = 28;
            this.bttnSave.Text = "Save";
            this.bttnSave.UseVisualStyleBackColor = true;
            this.bttnSave.Click += new System.EventHandler(this.BttnSave_Click);
            // 
            // bttnCancel
            // 
            this.bttnCancel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.bttnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bttnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnCancel.Location = new System.Drawing.Point(540, 3);
            this.bttnCancel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bttnCancel.Name = "bttnCancel";
            this.bttnCancel.Size = new System.Drawing.Size(140, 40);
            this.bttnCancel.TabIndex = 27;
            this.bttnCancel.Text = "Cancel";
            this.bttnCancel.UseVisualStyleBackColor = true;
            this.bttnCancel.Click += new System.EventHandler(this.BttnCancel_Click);
            // 
            // bttnAddShortcut
            // 
            this.bttnAddShortcut.Enabled = false;
            this.bttnAddShortcut.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnAddShortcut.Location = new System.Drawing.Point(4, 0);
            this.bttnAddShortcut.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.bttnAddShortcut.Name = "bttnAddShortcut";
            this.bttnAddShortcut.Size = new System.Drawing.Size(90, 27);
            this.bttnAddShortcut.TabIndex = 29;
            this.bttnAddShortcut.Text = "Add";
            this.bttnAddShortcut.UseVisualStyleBackColor = true;
            this.bttnAddShortcut.Click += new System.EventHandler(this.BttnAddShortcut_Click);
            // 
            // panelShortcuts
            // 
            this.panelShortcuts.Controls.Add(this.txtKeyboardShortcuts);
            this.panelShortcuts.Controls.Add(this.shortcutsListBox);
            this.panelShortcuts.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.panelShortcuts.Location = new System.Drawing.Point(4, 3);
            this.panelShortcuts.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.panelShortcuts.Name = "panelShortcuts";
            this.panelShortcuts.Size = new System.Drawing.Size(684, 212);
            this.panelShortcuts.TabIndex = 30;
            // 
            // shortcutsListBox
            // 
            this.shortcutsListBox.DrawMode = System.Windows.Forms.DrawMode.OwnerDrawFixed;
            this.shortcutsListBox.FormattingEnabled = true;
            this.shortcutsListBox.ItemHeight = 32;
            this.shortcutsListBox.Location = new System.Drawing.Point(3, 18);
            this.shortcutsListBox.Name = "shortcutsListBox";
            this.shortcutsListBox.SelectionMode = System.Windows.Forms.SelectionMode.MultiExtended;
            this.shortcutsListBox.Size = new System.Drawing.Size(680, 164);
            this.shortcutsListBox.TabIndex = 3;
            this.shortcutsListBox.DrawItem += new System.Windows.Forms.DrawItemEventHandler(this.ShortcutsListBox_DrawItem);
            this.shortcutsListBox.SelectedIndexChanged += new System.EventHandler(this.ShortcutsListBox_SelectedIndexChanged);
            // 
            // panelOuterContainer
            // 
            this.panelOuterContainer.Controls.Add(this.panelShortcuts);
            this.panelOuterContainer.Controls.Add(this.pnlAddEditBar);
            this.panelOuterContainer.Controls.Add(this.panelSaveCancel);
            this.panelOuterContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelOuterContainer.Location = new System.Drawing.Point(0, 0);
            this.panelOuterContainer.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.panelOuterContainer.Name = "panelOuterContainer";
            this.panelOuterContainer.Size = new System.Drawing.Size(692, 364);
            this.panelOuterContainer.TabIndex = 31;
            // 
            // pnlAddEditBar
            // 
            this.pnlAddEditBar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.pnlAddEditBar.Controls.Add(this.pnlTargetAndShortcut);
            this.pnlAddEditBar.Controls.Add(this.pnlWheelCheckboxes);
            this.pnlAddEditBar.Controls.Add(this.pnlShortcutExtraData);
            this.pnlAddEditBar.Location = new System.Drawing.Point(4, 221);
            this.pnlAddEditBar.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.pnlAddEditBar.Name = "pnlAddEditBar";
            this.pnlAddEditBar.Padding = new System.Windows.Forms.Padding(0, 4, 0, 4);
            this.pnlAddEditBar.Size = new System.Drawing.Size(684, 72);
            this.pnlAddEditBar.TabIndex = 32;
            // 
            // pnlTargetAndShortcut
            // 
            this.pnlTargetAndShortcut.Controls.Add(this.cmbxShortcutTarget);
            this.pnlTargetAndShortcut.Controls.Add(this.bttnShortcutSequence);
            this.pnlTargetAndShortcut.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlTargetAndShortcut.Location = new System.Drawing.Point(4, 7);
            this.pnlTargetAndShortcut.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.pnlTargetAndShortcut.Name = "pnlTargetAndShortcut";
            this.pnlTargetAndShortcut.Size = new System.Drawing.Size(197, 60);
            this.pnlTargetAndShortcut.TabIndex = 36;
            // 
            // cmbxShortcutTarget
            // 
            this.cmbxShortcutTarget.FormattingEnabled = true;
            this.cmbxShortcutTarget.Location = new System.Drawing.Point(3, 3);
            this.cmbxShortcutTarget.Margin = new System.Windows.Forms.Padding(3, 3, 3, 2);
            this.cmbxShortcutTarget.Name = "cmbxShortcutTarget";
            this.cmbxShortcutTarget.Size = new System.Drawing.Size(189, 23);
            this.cmbxShortcutTarget.TabIndex = 0;
            this.cmbxShortcutTarget.SelectedIndexChanged += CmbxShortcutTarget_SelectedIndexChanged;
            // 
            // bttnShortcutSequence
            // 
            this.bttnShortcutSequence.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnShortcutSequence.Location = new System.Drawing.Point(3, 30);
            this.bttnShortcutSequence.Margin = new System.Windows.Forms.Padding(3, 2, 3, 0);
            this.bttnShortcutSequence.Name = "bttnShortcutSequence";
            this.bttnShortcutSequence.Size = new System.Drawing.Size(189, 27);
            this.bttnShortcutSequence.TabIndex = 33;
            this.bttnShortcutSequence.Text = "Shortcut...";
            this.bttnShortcutSequence.UseVisualStyleBackColor = true;
            this.bttnShortcutSequence.Click += BttnShortcutSequence_Click;
            this.bttnShortcutSequence.LostFocus += BttnShortcutSequence_LostFocus;
            // 
            // pnlWheelCheckboxes
            // 
            this.pnlWheelCheckboxes.Controls.Add(this.chkbxShortcutWheelUp);
            this.pnlWheelCheckboxes.Controls.Add(this.chkbxShortcutWheelDown);
            this.pnlWheelCheckboxes.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlWheelCheckboxes.Location = new System.Drawing.Point(209, 7);
            this.pnlWheelCheckboxes.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.pnlWheelCheckboxes.Name = "pnlWheelCheckboxes";
            this.pnlWheelCheckboxes.Size = new System.Drawing.Size(102, 50);
            this.pnlWheelCheckboxes.TabIndex = 35;
            // 
            // chkbxShortcutWheelUp
            // 
            this.chkbxShortcutWheelUp.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkbxShortcutWheelUp.AutoSize = true;
            this.chkbxShortcutWheelUp.Location = new System.Drawing.Point(3, 3);
            this.chkbxShortcutWheelUp.Name = "chkbxShortcutWheelUp";
            this.chkbxShortcutWheelUp.Size = new System.Drawing.Size(76, 19);
            this.chkbxShortcutWheelUp.TabIndex = 2;
            this.chkbxShortcutWheelUp.Text = "Wheel up";
            this.chkbxShortcutWheelUp.UseVisualStyleBackColor = true;
            this.chkbxShortcutWheelUp.CheckedChanged += ChkbxShortcutWheelUp_CheckedChanged;
            // 
            // chkbxShortcutWheelDown
            // 
            this.chkbxShortcutWheelDown.Anchor = System.Windows.Forms.AnchorStyles.Left;
            this.chkbxShortcutWheelDown.AutoSize = true;
            this.chkbxShortcutWheelDown.Location = new System.Drawing.Point(3, 28);
            this.chkbxShortcutWheelDown.Name = "chkbxShortcutWheelDown";
            this.chkbxShortcutWheelDown.Size = new System.Drawing.Size(92, 19);
            this.chkbxShortcutWheelDown.TabIndex = 3;
            this.chkbxShortcutWheelDown.Text = "Wheel down";
            this.chkbxShortcutWheelDown.UseVisualStyleBackColor = true;
            this.chkbxShortcutWheelDown.CheckedChanged += ChkbxShortcutWheelDown_CheckedChanged;
            // 
            // pnlShortcutExtraData
            // 
            this.pnlShortcutExtraData.Controls.Add(this.txtbxShortcutActionData);
            this.pnlShortcutExtraData.Controls.Add(this.panelShortcutControls);
            this.pnlShortcutExtraData.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlShortcutExtraData.Location = new System.Drawing.Point(315, 4);
            this.pnlShortcutExtraData.Margin = new System.Windows.Forms.Padding(0);
            this.pnlShortcutExtraData.Name = "pnlShortcutExtraData";
            this.pnlShortcutExtraData.Size = new System.Drawing.Size(358, 60);
            this.pnlShortcutExtraData.TabIndex = 37;
            // 
            // txtbxShortcutActionData
            // 
            this.txtbxShortcutActionData.Enabled = false;
            this.txtbxShortcutActionData.Location = new System.Drawing.Point(3, 3);
            this.txtbxShortcutActionData.Name = "txtbxShortcutActionData";
            this.txtbxShortcutActionData.PlaceholderText = "Action data";
            this.txtbxShortcutActionData.Size = new System.Drawing.Size(355, 23);
            this.txtbxShortcutActionData.TabIndex = 32;
            this.txtbxShortcutActionData.TextChanged += TxtbxShortcutActionData_TextChanged;
            // 
            // panelShortcutControls
            // 
            this.panelShortcutControls.Controls.Add(this.bttnAddShortcut);
            this.panelShortcutControls.Controls.Add(this.bttnEditShortcut);
            this.panelShortcutControls.Controls.Add(this.bttnDeleteShortcut);
            this.panelShortcutControls.Location = new System.Drawing.Point(0, 29);
            this.panelShortcutControls.Margin = new System.Windows.Forms.Padding(0);
            this.panelShortcutControls.Name = "panelShortcutControls";
            this.panelShortcutControls.Size = new System.Drawing.Size(358, 29);
            this.panelShortcutControls.TabIndex = 31;
            // 
            // bttnEditShortcut
            // 
            this.bttnEditShortcut.Enabled = false;
            this.bttnEditShortcut.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnEditShortcut.Location = new System.Drawing.Point(102, 0);
            this.bttnEditShortcut.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.bttnEditShortcut.Name = "bttnEditShortcut";
            this.bttnEditShortcut.Size = new System.Drawing.Size(90, 27);
            this.bttnEditShortcut.TabIndex = 32;
            this.bttnEditShortcut.Text = "Edit";
            this.bttnEditShortcut.UseVisualStyleBackColor = true;
            this.bttnEditShortcut.Click += BttnEditShortcut_Click;
            // 
            // bttnDeleteShortcut
            // 
            this.bttnDeleteShortcut.Enabled = false;
            this.bttnDeleteShortcut.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnDeleteShortcut.Location = new System.Drawing.Point(200, 0);
            this.bttnDeleteShortcut.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.bttnDeleteShortcut.Name = "bttnDeleteShortcut";
            this.bttnDeleteShortcut.Size = new System.Drawing.Size(90, 27);
            this.bttnDeleteShortcut.TabIndex = 30;
            this.bttnDeleteShortcut.Text = "Delete";
            this.bttnDeleteShortcut.UseVisualStyleBackColor = true;
            this.bttnDeleteShortcut.Click += new System.EventHandler(this.BttnDeleteShortcut_Click);
            // 
            // panelSaveCancel
            // 
            this.panelSaveCancel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panelSaveCancel.Controls.Add(this.bttnCancel);
            this.panelSaveCancel.Controls.Add(this.bttnSave);
            this.panelSaveCancel.Controls.Add(this.panel1);
            this.panelSaveCancel.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.panelSaveCancel.Location = new System.Drawing.Point(4, 314);
            this.panelSaveCancel.Margin = new System.Windows.Forms.Padding(4, 18, 4, 3);
            this.panelSaveCancel.Name = "panelSaveCancel";
            this.panelSaveCancel.Size = new System.Drawing.Size(684, 45);
            this.panelSaveCancel.TabIndex = 3;
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.panel1.Controls.Add(this.bttnRestoreDefaults);
            this.panel1.Location = new System.Drawing.Point(3, 15);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(382, 28);
            this.panel1.TabIndex = 32;
            // 
            // bttnRestoreDefaults
            // 
            this.bttnRestoreDefaults.Dock = System.Windows.Forms.DockStyle.Left;
            this.bttnRestoreDefaults.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnRestoreDefaults.Location = new System.Drawing.Point(0, 0);
            this.bttnRestoreDefaults.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bttnRestoreDefaults.Name = "bttnRestoreDefaults";
            this.bttnRestoreDefaults.Size = new System.Drawing.Size(119, 28);
            this.bttnRestoreDefaults.TabIndex = 31;
            this.bttnRestoreDefaults.Text = "Restore defaults";
            this.bttnRestoreDefaults.UseVisualStyleBackColor = true;
            this.bttnRestoreDefaults.Click += new System.EventHandler(this.BttnRestoreDefaults_Click);
            // 
            // EditKeyboardShortcuts
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(692, 364);
            this.Controls.Add(this.panelOuterContainer);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "EditKeyboardShortcuts";
            this.Text = "Edit Keyboard Shortcuts";
            this.panelShortcuts.ResumeLayout(false);
            this.panelShortcuts.PerformLayout();
            this.panelOuterContainer.ResumeLayout(false);
            this.pnlAddEditBar.ResumeLayout(false);
            this.pnlTargetAndShortcut.ResumeLayout(false);
            this.pnlWheelCheckboxes.ResumeLayout(false);
            this.pnlWheelCheckboxes.PerformLayout();
            this.pnlShortcutExtraData.ResumeLayout(false);
            this.pnlShortcutExtraData.PerformLayout();
            this.panelShortcutControls.ResumeLayout(false);
            this.panelSaveCancel.ResumeLayout(false);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label txtKeyboardShortcuts;
        private System.Windows.Forms.Button bttnSave;
        private System.Windows.Forms.Button bttnCancel;
        private System.Windows.Forms.Button bttnAddShortcut;
        private System.Windows.Forms.ToolTip tooltip;
        private System.Windows.Forms.FlowLayoutPanel panelShortcuts;
        private System.Windows.Forms.FlowLayoutPanel panelOuterContainer;
        private System.Windows.Forms.FlowLayoutPanel panelShortcutControls;
        private System.Windows.Forms.FlowLayoutPanel panelSaveCancel;
        private System.Windows.Forms.Button bttnDeleteShortcut;
        private System.Windows.Forms.Button bttnRestoreDefaults;
        private System.Windows.Forms.ListBox shortcutsListBox;
        private Button bttnEditShortcut;
        private FlowLayoutPanel pnlAddEditBar;
        private ComboBox cmbxShortcutTarget;
        private CheckBox chkbxShortcutWheelUp;
        private CheckBox chkbxShortcutWheelDown;
        private Button bttnShortcutSequence;
        private FlowLayoutPanel pnlWheelCheckboxes;
        private FlowLayoutPanel pnlTargetAndShortcut;
        private FlowLayoutPanel pnlShortcutExtraData;
        private Panel panel1;
        private TextBox txtbxShortcutActionData;
    }
}