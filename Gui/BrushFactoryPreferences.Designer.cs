namespace BrushFactory.Gui
{
    partial class BrushFactoryPreferences
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
            this.txtBrushLocations = new System.Windows.Forms.Label();
            this.chkbxLoadDefaultBrushes = new System.Windows.Forms.CheckBox();
            this.txtbxBrushLocations = new System.Windows.Forms.TextBox();
            this.bttnSave = new System.Windows.Forms.Button();
            this.bttnCancel = new System.Windows.Forms.Button();
            this.bttnAddFolder = new System.Windows.Forms.Button();
            this.tooltip = new System.Windows.Forms.ToolTip(this.components);
            this.SuspendLayout();
            // 
            // txtBrushLocations
            // 
            this.txtBrushLocations.AutoSize = true;
            this.txtBrushLocations.Location = new System.Drawing.Point(143, 9);
            this.txtBrushLocations.Name = "txtBrushLocations";
            this.txtBrushLocations.Size = new System.Drawing.Size(193, 13);
            this.txtBrushLocations.TabIndex = 2;
            this.txtBrushLocations.Text = "Custom Brush Directories To Auto-Load";
            // 
            // chkbxLoadDefaultBrushes
            // 
            this.chkbxLoadDefaultBrushes.AutoSize = true;
            this.chkbxLoadDefaultBrushes.Checked = true;
            this.chkbxLoadDefaultBrushes.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkbxLoadDefaultBrushes.Location = new System.Drawing.Point(121, 201);
            this.chkbxLoadDefaultBrushes.Name = "chkbxLoadDefaultBrushes";
            this.chkbxLoadDefaultBrushes.Size = new System.Drawing.Size(134, 17);
            this.chkbxLoadDefaultBrushes.TabIndex = 3;
            this.chkbxLoadDefaultBrushes.Text = "Load Default Brushes?";
            this.chkbxLoadDefaultBrushes.UseVisualStyleBackColor = true;
            // 
            // txtbxBrushLocations
            // 
            this.txtbxBrushLocations.AcceptsReturn = true;
            this.txtbxBrushLocations.Location = new System.Drawing.Point(12, 25);
            this.txtbxBrushLocations.Multiline = true;
            this.txtbxBrushLocations.Name = "txtbxBrushLocations";
            this.txtbxBrushLocations.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtbxBrushLocations.Size = new System.Drawing.Size(460, 166);
            this.txtbxBrushLocations.TabIndex = 1;
            // 
            // bttnSave
            // 
            this.bttnSave.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnSave.Location = new System.Drawing.Point(108, 234);
            this.bttnSave.Name = "bttnSave";
            this.bttnSave.Size = new System.Drawing.Size(120, 35);
            this.bttnSave.TabIndex = 28;
            this.bttnSave.Text = "Save";
            this.bttnSave.UseVisualStyleBackColor = true;
            this.bttnSave.Click += new System.EventHandler(this.bttnSave_Click);
            // 
            // bttnCancel
            // 
            this.bttnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.bttnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bttnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnCancel.Location = new System.Drawing.Point(267, 234);
            this.bttnCancel.Name = "bttnCancel";
            this.bttnCancel.Size = new System.Drawing.Size(120, 35);
            this.bttnCancel.TabIndex = 27;
            this.bttnCancel.Text = "Cancel";
            this.bttnCancel.UseVisualStyleBackColor = true;
            this.bttnCancel.Click += new System.EventHandler(this.bttnCancel_Click);
            // 
            // bttnAddFolder
            // 
            this.bttnAddFolder.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnAddFolder.Location = new System.Drawing.Point(12, 197);
            this.bttnAddFolder.Name = "bttnAddFolder";
            this.bttnAddFolder.Size = new System.Drawing.Size(77, 23);
            this.bttnAddFolder.TabIndex = 29;
            this.bttnAddFolder.Text = "Add Folder";
            this.bttnAddFolder.UseVisualStyleBackColor = true;
            this.bttnAddFolder.Click += new System.EventHandler(this.bttnAddFolder_Click);
            // 
            // BrushFactoryPreferences
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(484, 281);
            this.Controls.Add(this.bttnAddFolder);
            this.Controls.Add(this.bttnSave);
            this.Controls.Add(this.bttnCancel);
            this.Controls.Add(this.chkbxLoadDefaultBrushes);
            this.Controls.Add(this.txtBrushLocations);
            this.Controls.Add(this.txtbxBrushLocations);
            this.Name = "BrushFactoryPreferences";
            this.Text = "Preferences";
            this.Load += new System.EventHandler(this.winBrushFactoryPreferences_DialogLoad);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion
        private System.Windows.Forms.Label txtBrushLocations;
        private System.Windows.Forms.CheckBox chkbxLoadDefaultBrushes;
        private System.Windows.Forms.TextBox txtbxBrushLocations;
        private System.Windows.Forms.Button bttnSave;
        private System.Windows.Forms.Button bttnCancel;
        private System.Windows.Forms.Button bttnAddFolder;
        private System.Windows.Forms.ToolTip tooltip;
    }
}