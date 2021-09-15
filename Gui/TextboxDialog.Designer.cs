
namespace BrushFactory.Gui
{
    partial class TextboxDialog
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
            this.panelFlowContainer = new System.Windows.Forms.FlowLayoutPanel();
            this.txtDescription = new System.Windows.Forms.Label();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.txtbxInput = new System.Windows.Forms.TextBox();
            this.bttnOk = new System.Windows.Forms.Button();
            this.txtError = new System.Windows.Forms.Label();
            this.panelFlowContainer.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // panelFlowContainer
            // 
            this.panelFlowContainer.AutoSize = true;
            this.panelFlowContainer.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.panelFlowContainer.Controls.Add(this.txtDescription);
            this.panelFlowContainer.Controls.Add(this.flowLayoutPanel1);
            this.panelFlowContainer.Controls.Add(this.txtError);
            this.panelFlowContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.panelFlowContainer.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.panelFlowContainer.Location = new System.Drawing.Point(0, 0);
            this.panelFlowContainer.Name = "panelFlowContainer";
            this.panelFlowContainer.Size = new System.Drawing.Size(800, 450);
            this.panelFlowContainer.TabIndex = 0;
            // 
            // txtDescription
            // 
            this.txtDescription.AutoSize = true;
            this.txtDescription.Location = new System.Drawing.Point(3, 0);
            this.txtDescription.MaximumSize = new System.Drawing.Size(260, 0);
            this.txtDescription.Name = "txtDescription";
            this.txtDescription.Size = new System.Drawing.Size(60, 13);
            this.txtDescription.TabIndex = 0;
            this.txtDescription.Text = "Description";
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.AutoSize = true;
            this.flowLayoutPanel1.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.flowLayoutPanel1.Controls.Add(this.txtbxInput);
            this.flowLayoutPanel1.Controls.Add(this.bttnOk);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(3, 16);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(260, 29);
            this.flowLayoutPanel1.TabIndex = 3;
            // 
            // txtbxInput
            // 
            this.txtbxInput.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right)));
            this.txtbxInput.Location = new System.Drawing.Point(3, 4);
            this.txtbxInput.MinimumSize = new System.Drawing.Size(200, 4);
            this.txtbxInput.Name = "txtbxInput";
            this.txtbxInput.Size = new System.Drawing.Size(200, 20);
            this.txtbxInput.TabIndex = 1;
            // 
            // bttnOk
            // 
            this.bttnOk.Location = new System.Drawing.Point(209, 3);
            this.bttnOk.Name = "bttnOk";
            this.bttnOk.Size = new System.Drawing.Size(48, 23);
            this.bttnOk.TabIndex = 2;
            this.bttnOk.Text = "OK";
            this.bttnOk.UseVisualStyleBackColor = true;
            // 
            // txtError
            // 
            this.txtError.AutoSize = true;
            this.txtError.ForeColor = System.Drawing.Color.Red;
            this.txtError.Location = new System.Drawing.Point(3, 48);
            this.txtError.Name = "txtError";
            this.txtError.Size = new System.Drawing.Size(29, 13);
            this.txtError.TabIndex = 2;
            this.txtError.Text = "Error";
            this.txtError.Visible = false;
            // 
            // TextboxDialog
            // 
            this.AcceptButton = this.bttnOk;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.panelFlowContainer);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TextboxDialog";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.TopMost = true;
            this.panelFlowContainer.ResumeLayout(false);
            this.panelFlowContainer.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.flowLayoutPanel1.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel panelFlowContainer;
        private System.Windows.Forms.Label txtDescription;
        private System.Windows.Forms.TextBox txtbxInput;
        private System.Windows.Forms.Label txtError;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.Button bttnOk;
    }
}