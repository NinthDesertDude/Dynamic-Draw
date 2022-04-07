
namespace DynamicDraw
{
    partial class EditCurveDialog
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
            this.pnlContainer = new System.Windows.Forms.FlowLayoutPanel();
            this.flowLayoutPanel1 = new System.Windows.Forms.FlowLayoutPanel();
            this.curveGraph = new DynamicDraw.CurveGraph();
            this.pnlButtons = new System.Windows.Forms.FlowLayoutPanel();
            this.bttnPresetConstant = new System.Windows.Forms.Button();
            this.bttnPresetLinear = new System.Windows.Forms.Button();
            this.bttnPresetLinearSmoothEnds = new System.Windows.Forms.Button();
            this.bttnPresetLinearSmoothMid = new System.Windows.Forms.Button();
            this.bttnPresetExp = new System.Windows.Forms.Button();
            this.bttnPresetLog = new System.Windows.Forms.Button();
            this.bttnPresetStep = new System.Windows.Forms.Button();
            this.pnlFormButtonsContainer = new System.Windows.Forms.FlowLayoutPanel();
            this.bttnOK = new System.Windows.Forms.Button();
            this.bttnCancel = new System.Windows.Forms.Button();
            this.tooltip = new System.Windows.Forms.ToolTip(this.components);
            this.pnlContainer.SuspendLayout();
            this.flowLayoutPanel1.SuspendLayout();
            this.pnlButtons.SuspendLayout();
            this.pnlFormButtonsContainer.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlContainer
            // 
            this.pnlContainer.AutoSize = true;
            this.pnlContainer.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlContainer.Controls.Add(this.flowLayoutPanel1);
            this.pnlContainer.Controls.Add(this.pnlFormButtonsContainer);
            this.pnlContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContainer.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.pnlContainer.Location = new System.Drawing.Point(0, 0);
            this.pnlContainer.Name = "pnlContainer";
            this.pnlContainer.Size = new System.Drawing.Size(516, 450);
            this.pnlContainer.TabIndex = 2;
            // 
            // flowLayoutPanel1
            // 
            this.flowLayoutPanel1.AutoSize = true;
            this.flowLayoutPanel1.Controls.Add(this.curveGraph);
            this.flowLayoutPanel1.Controls.Add(this.pnlButtons);
            this.flowLayoutPanel1.Location = new System.Drawing.Point(3, 3);
            this.flowLayoutPanel1.Name = "flowLayoutPanel1";
            this.flowLayoutPanel1.Size = new System.Drawing.Size(200, 280);
            this.flowLayoutPanel1.TabIndex = 3;
            // 
            // curveGraph
            // 
            this.curveGraph.CurveTableResolution = 100;
            this.curveGraph.Location = new System.Drawing.Point(3, 3);
            this.curveGraph.Name = "curveGraph";
            this.curveGraph.Size = new System.Drawing.Size(150, 150);
            this.curveGraph.TabIndex = 2;
            // 
            // pnlButtons
            // 
            this.pnlButtons.Controls.Add(this.bttnPresetConstant);
            this.pnlButtons.Controls.Add(this.bttnPresetLinear);
            this.pnlButtons.Controls.Add(this.bttnPresetLinearSmoothEnds);
            this.pnlButtons.Controls.Add(this.bttnPresetLinearSmoothMid);
            this.pnlButtons.Controls.Add(this.bttnPresetExp);
            this.pnlButtons.Controls.Add(this.bttnPresetLog);
            this.pnlButtons.Controls.Add(this.bttnPresetStep);
            this.pnlButtons.Location = new System.Drawing.Point(159, 3);
            this.pnlButtons.Name = "pnlButtons";
            this.pnlButtons.Size = new System.Drawing.Size(38, 274);
            this.pnlButtons.TabIndex = 3;
            // 
            // bttnPresetConstant
            // 
            this.bttnPresetConstant.Location = new System.Drawing.Point(3, 3);
            this.bttnPresetConstant.Name = "bttnPresetConstant";
            this.bttnPresetConstant.Size = new System.Drawing.Size(32, 32);
            this.bttnPresetConstant.TabIndex = 0;
            this.tooltip.SetToolTip(this.bttnPresetConstant, "Replace curve with a constant value");
            this.bttnPresetConstant.UseVisualStyleBackColor = true;
            // 
            // bttnPresetLinear
            // 
            this.bttnPresetLinear.Location = new System.Drawing.Point(3, 41);
            this.bttnPresetLinear.Name = "bttnPresetLinear";
            this.bttnPresetLinear.Size = new System.Drawing.Size(32, 32);
            this.bttnPresetLinear.TabIndex = 1;
            this.tooltip.SetToolTip(this.bttnPresetLinear, "Use the linear curve preset");
            this.bttnPresetLinear.UseVisualStyleBackColor = true;
            // 
            // bttnPresetLinearSmoothEnds
            // 
            this.bttnPresetLinearSmoothEnds.Location = new System.Drawing.Point(3, 79);
            this.bttnPresetLinearSmoothEnds.Name = "bttnPresetLinearSmoothEnds";
            this.bttnPresetLinearSmoothEnds.Size = new System.Drawing.Size(32, 32);
            this.bttnPresetLinearSmoothEnds.TabIndex = 5;
            this.tooltip.SetToolTip(this.bttnPresetLinearSmoothEnds, "Use the linear + smooth ends curve preset");
            this.bttnPresetLinearSmoothEnds.UseVisualStyleBackColor = true;
            // 
            // bttnPresetLinearSmoothMid
            // 
            this.bttnPresetLinearSmoothMid.Location = new System.Drawing.Point(3, 117);
            this.bttnPresetLinearSmoothMid.Name = "bttnPresetLinearSmoothMid";
            this.bttnPresetLinearSmoothMid.Size = new System.Drawing.Size(32, 32);
            this.bttnPresetLinearSmoothMid.TabIndex = 6;
            this.tooltip.SetToolTip(this.bttnPresetLinearSmoothMid, "Use the linear + smooth mid curve preset");
            this.bttnPresetLinearSmoothMid.UseVisualStyleBackColor = true;
            // 
            // bttnPresetExp
            // 
            this.bttnPresetExp.Location = new System.Drawing.Point(3, 155);
            this.bttnPresetExp.Name = "bttnPresetExp";
            this.bttnPresetExp.Size = new System.Drawing.Size(32, 32);
            this.bttnPresetExp.TabIndex = 2;
            this.tooltip.SetToolTip(this.bttnPresetExp, "Use the exponential curve preset");
            this.bttnPresetExp.UseVisualStyleBackColor = true;
            // 
            // bttnPresetLog
            // 
            this.bttnPresetLog.Location = new System.Drawing.Point(3, 193);
            this.bttnPresetLog.Name = "bttnPresetLog";
            this.bttnPresetLog.Size = new System.Drawing.Size(32, 32);
            this.bttnPresetLog.TabIndex = 3;
            this.tooltip.SetToolTip(this.bttnPresetLog, "Use the logarithmic curve preset");
            this.bttnPresetLog.UseVisualStyleBackColor = true;
            // 
            // bttnPresetStep
            // 
            this.bttnPresetStep.Location = new System.Drawing.Point(3, 231);
            this.bttnPresetStep.Name = "bttnPresetStep";
            this.bttnPresetStep.Size = new System.Drawing.Size(32, 32);
            this.bttnPresetStep.TabIndex = 4;
            this.tooltip.SetToolTip(this.bttnPresetStep, "Use the step function curve preset");
            this.bttnPresetStep.UseVisualStyleBackColor = true;
            // 
            // pnlFormButtonsContainer
            // 
            this.pnlFormButtonsContainer.Anchor = System.Windows.Forms.AnchorStyles.None;
            this.pnlFormButtonsContainer.AutoSize = true;
            this.pnlFormButtonsContainer.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.pnlFormButtonsContainer.Controls.Add(this.bttnOK);
            this.pnlFormButtonsContainer.Controls.Add(this.bttnCancel);
            this.pnlFormButtonsContainer.Location = new System.Drawing.Point(22, 289);
            this.pnlFormButtonsContainer.Name = "pnlFormButtonsContainer";
            this.pnlFormButtonsContainer.Size = new System.Drawing.Size(162, 29);
            this.pnlFormButtonsContainer.TabIndex = 1;
            // 
            // bttnOK
            // 
            this.bttnOK.Location = new System.Drawing.Point(3, 3);
            this.bttnOK.Name = "bttnOK";
            this.bttnOK.Size = new System.Drawing.Size(75, 23);
            this.bttnOK.TabIndex = 0;
            this.bttnOK.Text = "OK";
            this.bttnOK.UseVisualStyleBackColor = true;
            // 
            // bttnCancel
            // 
            this.bttnCancel.Location = new System.Drawing.Point(84, 3);
            this.bttnCancel.Name = "bttnCancel";
            this.bttnCancel.Size = new System.Drawing.Size(75, 23);
            this.bttnCancel.TabIndex = 1;
            this.bttnCancel.Text = "Cancel";
            this.bttnCancel.UseVisualStyleBackColor = true;
            // 
            // EditCurveDialog
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.ClientSize = new System.Drawing.Size(516, 450);
            this.Controls.Add(this.pnlContainer);
            this.Name = "EditCurveDialog";
            this.Text = "Form2";
            this.pnlContainer.ResumeLayout(false);
            this.pnlContainer.PerformLayout();
            this.flowLayoutPanel1.ResumeLayout(false);
            this.pnlButtons.ResumeLayout(false);
            this.pnlFormButtonsContainer.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel pnlContainer;
        private System.Windows.Forms.FlowLayoutPanel pnlFormButtonsContainer;
        private System.Windows.Forms.Button bttnOK;
        private System.Windows.Forms.Button bttnCancel;
        private CurveGraph curveGraph;
        private System.Windows.Forms.FlowLayoutPanel flowLayoutPanel1;
        private System.Windows.Forms.FlowLayoutPanel pnlButtons;
        private System.Windows.Forms.Button bttnPresetConstant;
        private System.Windows.Forms.Button bttnPresetLinear;
        private System.Windows.Forms.Button bttnPresetExp;
        private System.Windows.Forms.Button bttnPresetLog;
        private System.Windows.Forms.Button bttnPresetStep;
        private System.Windows.Forms.Button bttnPresetLinearSmoothEnds;
        private System.Windows.Forms.Button bttnPresetLinearSmoothMid;
        private System.Windows.Forms.ToolTip tooltip;
    }
}