namespace DynamicDraw.Gui
{
    partial class DynamicDrawPreferences
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
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel2 = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel3 = new System.Windows.Forms.FlowLayoutPanel();
            this.bttnAddFiles = new System.Windows.Forms.Button();
            this.flowLayoutPanel4 = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel1.SuspendLayout();
            this.flowLayoutPanel2.SuspendLayout();
            this.flowLayoutPanel3.SuspendLayout();
            this.flowLayoutPanel4.SuspendLayout();
            this.SuspendLayout();
            // 
            // txtBrushLocations
            // 
            this.txtBrushLocations.AutoSize = true;
            this.txtBrushLocations.Location = new System.Drawing.Point(4, 0);
            this.txtBrushLocations.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.txtBrushLocations.Name = "txtBrushLocations";
            this.txtBrushLocations.Size = new System.Drawing.Size(123, 15);
            this.txtBrushLocations.TabIndex = 2;
            this.txtBrushLocations.Text = "Custom Brush Images";
            // 
            // chkbxLoadDefaultBrushes
            // 
            this.chkbxLoadDefaultBrushes.AutoSize = true;
            this.chkbxLoadDefaultBrushes.Checked = true;
            this.chkbxLoadDefaultBrushes.CheckState = System.Windows.Forms.CheckState.Checked;
            this.chkbxLoadDefaultBrushes.Location = new System.Drawing.Point(200, 3);
            this.chkbxLoadDefaultBrushes.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.chkbxLoadDefaultBrushes.Name = "chkbxLoadDefaultBrushes";
            this.chkbxLoadDefaultBrushes.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.chkbxLoadDefaultBrushes.Size = new System.Drawing.Size(172, 24);
            this.chkbxLoadDefaultBrushes.TabIndex = 3;
            this.chkbxLoadDefaultBrushes.Text = "Load Default Brush Images?";
            this.chkbxLoadDefaultBrushes.UseVisualStyleBackColor = true;
            // 
            // txtbxBrushLocations
            // 
            this.txtbxBrushLocations.AcceptsReturn = true;
            this.txtbxBrushLocations.Location = new System.Drawing.Point(4, 18);
            this.txtbxBrushLocations.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.txtbxBrushLocations.Multiline = true;
            this.txtbxBrushLocations.Name = "txtbxBrushLocations";
            this.txtbxBrushLocations.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtbxBrushLocations.Size = new System.Drawing.Size(554, 191);
            this.txtbxBrushLocations.TabIndex = 1;
            // 
            // bttnSave
            // 
            this.bttnSave.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.bttnSave.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnSave.Location = new System.Drawing.Point(269, 3);
            this.bttnSave.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bttnSave.Name = "bttnSave";
            this.bttnSave.Size = new System.Drawing.Size(140, 40);
            this.bttnSave.TabIndex = 28;
            this.bttnSave.Text = "Save";
            this.bttnSave.UseVisualStyleBackColor = true;
            this.bttnSave.Click += new System.EventHandler(this.bttnSave_Click);
            // 
            // bttnCancel
            // 
            this.bttnCancel.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.bttnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.bttnCancel.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnCancel.Location = new System.Drawing.Point(417, 3);
            this.bttnCancel.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bttnCancel.Name = "bttnCancel";
            this.bttnCancel.Size = new System.Drawing.Size(140, 40);
            this.bttnCancel.TabIndex = 27;
            this.bttnCancel.Text = "Cancel";
            this.bttnCancel.UseVisualStyleBackColor = true;
            this.bttnCancel.Click += new System.EventHandler(this.bttnCancel_Click);
            // 
            // bttnAddFolder
            // 
            this.bttnAddFolder.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnAddFolder.Location = new System.Drawing.Point(4, 3);
            this.bttnAddFolder.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bttnAddFolder.Name = "bttnAddFolder";
            this.bttnAddFolder.Size = new System.Drawing.Size(90, 27);
            this.bttnAddFolder.TabIndex = 29;
            this.bttnAddFolder.Text = "Add Folders";
            this.bttnAddFolder.UseVisualStyleBackColor = true;
            this.bttnAddFolder.Click += new System.EventHandler(this.bttnAddFolder_Click);
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.Controls.Add(this.txtBrushLocations);
            this.flowLayoutPanel1.Controls.Add(this.txtbxBrushLocations);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(4, 3);
            this.flowLayoutPanel1.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(537, 207);
            this.flowLayoutPanel1.TabIndex = 30;
            // 
            // flowLayoutPanel2
            // 
            this.flowLayoutPanel2.Controls.Add(this.flowLayoutPanel1);
            this.flowLayoutPanel2.Controls.Add(this.flowLayoutPanel3);
            this.flowLayoutPanel2.Controls.Add(this.flowLayoutPanel4);
            this.flowLayoutPanel2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flowLayoutPanel2.Location = new System.Drawing.Point(0, 0);
            this.flowLayoutPanel2.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.flowLayoutPanel2.Name = "flowLayoutPanel2";
            this.flowLayoutPanel2.Size = new System.Drawing.Size(565, 324);
            this.flowLayoutPanel2.TabIndex = 31;
            // 
            // flowLayoutPanel3
            // 
            this.flowLayoutPanel3.Controls.Add(this.bttnAddFolder);
            this.flowLayoutPanel3.Controls.Add(this.bttnAddFiles);
            this.flowLayoutPanel3.Controls.Add(this.chkbxLoadDefaultBrushes);
            this.flowLayoutPanel3.Location = new System.Drawing.Point(4, 216);
            this.flowLayoutPanel3.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.flowLayoutPanel3.Name = "flowLayoutPanel3";
            this.flowLayoutPanel3.Size = new System.Drawing.Size(561, 37);
            this.flowLayoutPanel3.TabIndex = 31;
            // 
            // bttnAddFiles
            // 
            this.bttnAddFiles.ImeMode = System.Windows.Forms.ImeMode.NoControl;
            this.bttnAddFiles.Location = new System.Drawing.Point(102, 3);
            this.bttnAddFiles.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.bttnAddFiles.Name = "bttnAddFiles";
            this.bttnAddFiles.Size = new System.Drawing.Size(90, 27);
            this.bttnAddFiles.TabIndex = 30;
            this.bttnAddFiles.Text = "Add Files";
            this.bttnAddFiles.UseVisualStyleBackColor = true;
            this.bttnAddFiles.Click += new System.EventHandler(this.bttnAddFiles_Click);
            // 
            // flowLayoutPanel4
            // 
            this.flowLayoutPanel4.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.flowLayoutPanel4.Controls.Add(this.bttnCancel);
            this.flowLayoutPanel4.Controls.Add(this.bttnSave);
            this.flowLayoutPanel4.FlowDirection = System.Windows.Forms.FlowDirection.RightToLeft;
            this.flowLayoutPanel4.Location = new System.Drawing.Point(4, 259);
            this.flowLayoutPanel4.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.flowLayoutPanel4.Name = "flowLayoutPanel4";
            this.flowLayoutPanel4.Size = new System.Drawing.Size(561, 55);
            this.flowLayoutPanel4.TabIndex = 3;
            // 
            // DynamicDrawPreferences
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(565, 324);
            this.Controls.Add(this.flowLayoutPanel2);
            this.Margin = new System.Windows.Forms.Padding(4, 3, 4, 3);
            this.Name = "DynamicDrawPreferences";
            this.Text = "Preferences";
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.flowLayoutPanel2.ResumeLayout(false);
            this.flowLayoutPanel3.ResumeLayout(false);
            this.flowLayoutPanel3.PerformLayout();
            this.flowLayoutPanel4.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion
        private System.Windows.Forms.Label txtBrushLocations;
        private System.Windows.Forms.CheckBox chkbxLoadDefaultBrushes;
        private System.Windows.Forms.TextBox txtbxBrushLocations;
        private System.Windows.Forms.Button bttnSave;
        private System.Windows.Forms.Button bttnCancel;
        private System.Windows.Forms.Button bttnAddFolder;
        private System.Windows.Forms.ToolTip tooltip;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel2;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel3;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel4;
        private System.Windows.Forms.Button bttnAddFiles;
    }
}