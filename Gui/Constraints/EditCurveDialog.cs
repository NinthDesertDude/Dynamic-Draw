using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    public partial class EditCurveDialog : Form
    {
        Pen pen = new Pen(Color.Black, 2);

        public EditCurveDialog()
        {
            InitializeComponent();
            curveGraph.EditMode = CurveGraph.CurveEditMode.FullEdit;
            curveGraph.SetCurveResolution(1000);
            curveGraph.Width = 400;
            curveGraph.Height = 400;

            MouseMove += EditCurveDialog_MouseMove;
            PreviewKeyDown += EditCurveDialog_PreviewKeyDown;

            bttnOK.TabStop = false;
            bttnCancel.TabStop = false;

            bttnPresetConstant.Click += BttnPresetConstant_Click;
            bttnPresetConstant.Paint += BttnPresetConstant_Paint;
            bttnPresetConstant.PreviewKeyDown += EditCurveDialog_PreviewKeyDown;

            bttnPresetExp.Click += BttnPresetExp_Click;
            bttnPresetExp.Paint += bttnPresetExp_Paint;
            bttnPresetExp.PreviewKeyDown += EditCurveDialog_PreviewKeyDown;

            bttnPresetLinear.Click += BttnPresetLinear_Click;
            bttnPresetLinear.Paint += BttnPresetLinear_Paint;
            bttnPresetLinear.PreviewKeyDown += EditCurveDialog_PreviewKeyDown;

            bttnPresetLinearSmoothEnds.Click += BttnPresetLinearSmoothEnds_Click;
            bttnPresetLinearSmoothEnds.Paint += BttnPresetLinearSmoothEnds_Paint;
            bttnPresetLinearSmoothEnds.PreviewKeyDown += EditCurveDialog_PreviewKeyDown;

            bttnPresetLinearSmoothMid.Click += bttnPresetLinearSmoothMid_Click;
            bttnPresetLinearSmoothMid.Paint += BttnPresetLinearSmoothMid_Paint;
            bttnPresetLinearSmoothMid.PreviewKeyDown += EditCurveDialog_PreviewKeyDown;

            bttnPresetLog.Click += BttnPresetLog_Click;
            bttnPresetLog.Paint += BttnPresetLog_Paint;
            bttnPresetLog.PreviewKeyDown += EditCurveDialog_PreviewKeyDown;

            bttnPresetStep.Click += BttnPresetStep_Click;
            bttnPresetStep.Paint += BttnPresetStep_Paint;
            bttnPresetStep.PreviewKeyDown += EditCurveDialog_PreviewKeyDown;
        }

        public EditCurveDialog(CurveGraph.CurveEditMode editMode, int curveTableResolution, int graphViewSize = 400) : this()
        {
            curveGraph.EditMode = editMode;
            curveGraph.SetCurveResolution(curveTableResolution);
            curveGraph.Width = graphViewSize;
            curveGraph.Height = graphViewSize;
        }

        private void EditCurveDialog_PreviewKeyDown(object sender, PreviewKeyDownEventArgs e)
        {
            Point cursorPos = curveGraph.PointToClient(Cursor.Position);
            
            if (cursorPos.X >= 0 && cursorPos.X < curveGraph.Width &&
                cursorPos.Y >= 0 && cursorPos.Y < curveGraph.Height)
            {
                curveGraph.CurveGraph_PreviewKeyDown(sender, e);
            }
        }

        private void BttnPresetConstant_MouseEnter(object sender, EventArgs e)
        {
            throw new NotImplementedException();
        }

        private void BttnPresetConstant_Click(object sender, EventArgs e)
        {
            curveGraph.SetCurvePoints(new PointF[]
            {
                new PointF(0.5f, 1)
            });
        }

        private void BttnPresetConstant_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawLine(pen, new PointF(4, 16), new PointF(28, 16));
        }

        private void BttnPresetExp_Click(object sender, EventArgs e)
        {
            curveGraph.SetCurvePoints(new PointF[]
            {
                new PointF(0f, 0f),
                new PointF(0.5725f, 0.1325f),
                new PointF(0.85f, 0.465f),
                new PointF(1f, 1f)
            });
        }

        private void bttnPresetExp_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawCurve(pen, new PointF[] {
                new PointF(4, 28),
                new PointF(17.74f, 24.82f),
                new PointF(24.4f, 16.84f),
                new PointF(28, 4)
            });
        }

        private void BttnPresetLinear_Click(object sender, EventArgs e)
        {
            curveGraph.SetCurvePoints(new PointF[]
            {
                new PointF(0, 0),
                new PointF(1, 1)
            });
        }

        private void BttnPresetLinear_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawLine(pen, new Point(4, 28), new Point(28, 4));
        }

        private void BttnPresetLinearSmoothEnds_Click(object sender, EventArgs e)
        {
            curveGraph.SetCurvePoints(new PointF[]
            {
                new PointF(0f, 0f),
                new PointF(0.325f, 0.2075f),
                new PointF(0.695f, 0.815f),
                new PointF(1f, 1f)
            });
        }

        private void BttnPresetLinearSmoothEnds_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawCurve(pen, new PointF[]
            {
                new PointF(4f, 28f),
                new PointF(11.8f, 23.02f),
                new PointF(20.68f, 8.44f),
                new PointF(28f, 4f)
            });
        }

        private void bttnPresetLinearSmoothMid_Click(object sender, EventArgs e)
        {
            curveGraph.SetCurvePoints(new PointF[]
            {
                new PointF(0f, 0f),
                new PointF(0.125f, 0.435f),
                new PointF(0.88f, 0.595f),
                new PointF(1f, 1f)
            });
        }

        private void BttnPresetLinearSmoothMid_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawCurve(pen, new PointF[]
            {
                new PointF(4f, 28f),
                new PointF(7f, 17.56f),
                new PointF(25.12f, 13.7f),
                new PointF(28f, 4f)
            });
        }

        private void BttnPresetLog_Click(object sender, EventArgs e)
        {
            curveGraph.SetCurvePoints(new PointF[]
            {
                new PointF(0, 0),
                new PointF(0.1175f, 0.295f),
                new PointF(0.41f, 0.6825f),
                new PointF(0.7525f, 0.9125f),
                new PointF(1f, 1f)
            });
        }

        private void BttnPresetLog_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            e.Graphics.DrawCurve(pen, new PointF[] {
                new PointF(4f, 28f),
                new PointF(6.82f, 20.92f),
                new PointF(13.84f, 11.62f),
                new PointF(22.06f, 6.1f),
                new PointF(28f, 4f)
            });
        }

        private void BttnPresetStep_Click(object sender, EventArgs e)
        {
            curveGraph.SetCurvePoints(new PointF[]
            {
                new PointF(0f, 0f),
                new PointF(0.5f, 0f),
                new PointF(0.5f, 1f),
                new PointF(1f, 1f)
            });
        }

        private void BttnPresetStep_Paint(object sender, PaintEventArgs e)
        {
            e.Graphics.DrawLines(pen, new PointF[] {
                new PointF(4, 28),
                new PointF(16, 28),
                new PointF(16, 4),
                new PointF(28, 4)
            });
        }

        private void EditCurveDialog_MouseMove(object sender, MouseEventArgs e)
        {
            curveGraph.Canvas_MouseDown(sender, e);
        }
    }
}
