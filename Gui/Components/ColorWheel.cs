// Adapted from https://github.com/rivy/OpenPDN/blob/master/src/ColorWheel.cs
// Under the MIT License for Paint.net version 3.36.7 while it was still open source

/////////////////////////////////////////////////////////////////////////////////
// Paint.NET                                                                   //
// Copyright (C) dotPDN LLC, Rick Brewster, Tom Jackson, and contributors.     //
// Portions Copyright (C) Microsoft Corporation. All Rights Reserved.          //
// See src/Resources/Files/License.txt for full licensing and attribution      //
// details.                                                                    //
// .                                                                           //
/////////////////////////////////////////////////////////////////////////////////

using PaintDotNet;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A hue-sat color wheel with hue as the angle and saturation as the magnitude.
    /// </summary>
    public class ColorWheel : UserControl
    {
        /// <summary>
        /// path gradient brush, used to draw hues, takes X points equal to 360 / this. More = better and slower.
        /// </summary>
        private const int colorTesselation = 60;

        private Bitmap renderBitmap = null;
        private bool mouseHeld = false;

        private PictureBox wheelPictureBox;
        private HsvColor hsvColor;

        public HsvColor HsvColor
        {
            get
            {
                return hsvColor;
            }

            set
            {
                if (hsvColor != value)
                {
                    hsvColor = value;
                    Refresh();
                }
            }
        }

        public event EventHandler ColorChanged;

        public ColorWheel()
        {
            SetupGui();
            hsvColor = new HsvColor(0, 0, 0);
            VisibleChanged += ColorWheel_VisibleChanged;
            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();
        }

        private void HandleTheme()
        {
            if (renderBitmap != null && Width != 0)
            {
                renderBitmap?.Dispose();
                renderBitmap = null;
            }

            Invalidate();
        }

        private static PointF[] GetCirclePoints(float r, PointF center)
        {
            PointF[] points = new PointF[colorTesselation];

            for (int i = 0; i < colorTesselation; i++)
            {
                float theta = i / (float)colorTesselation * 2 * (float)Math.PI;
                points[i] = new PointF(r * (float)Math.Cos(theta), r * (float)Math.Sin(theta));
                points[i].X += center.X;
                points[i].Y += center.Y;
            }

            return points;
        }

        private static Color[] GetColors()
        {
            Color[] colors = new Color[colorTesselation];

            for (int i = 0; i < colorTesselation; i++)
            {
                int hue = (i * 360) / colorTesselation;
                colors[i] = new HsvColor(hue, 100, 100).ToColor();
            }

            return colors;
        }

        protected override void OnLoad(EventArgs e)
        {
            if (Visible) { InitRendering(); }
            base.OnLoad(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            InitRendering();
            base.OnPaint(e);
        }

        private void InitRendering()
        {
            if (renderBitmap == null)
            {
                InitRenderSurface();
                wheelPictureBox.SizeMode = PictureBoxSizeMode.Normal;
                wheelPictureBox.Size = Size;
                wheelPictureBox.Image = renderBitmap;
            }
        }

        private void InitRenderSurface()
        {
            renderBitmap?.Dispose();
            renderBitmap = new Bitmap(Width, Width, PixelFormat.Format24bppRgb);

            using (Graphics g1 = Graphics.FromImage(renderBitmap))
            {
                g1.Clear(BackColor);
                DrawWheel(g1, renderBitmap.Width);
            }
        }

        private void DrawWheel(Graphics g, int size)
        {
            float radius = size / 2f;
            PointF[] points = GetCirclePoints(Math.Max(1.0f, (float)radius - 1), new PointF(radius, radius));

            using (PathGradientBrush pgb = new PathGradientBrush(points))
            {
                pgb.CenterColor = new HsvColor(0, 0, 100).ToColor();
                pgb.CenterPoint = new PointF(radius, radius);
                pgb.SurroundColors = GetColors();

                if (!Enabled)
                {
                    g.FillEllipse(SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBgDisabled), 0, 0, size, size);
                }
                else
                {
                    g.FillEllipse(pgb, 0, 0, size, size);
                }
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);

            if (renderBitmap != null && Width != renderBitmap.Width)
            {
                renderBitmap?.Dispose();
                renderBitmap = null;
            }

            Invalidate();
        }

        private void GrabColor(Point mouseXY)
        {
            // center our coordinate system so the middle is (0,0), and positive Y is facing up
            int radius = Width / 2;
            int cx = mouseXY.X - radius;
            int cy = mouseXY.Y - radius;

            double theta = Math.Atan2(cy, cx);

            if (theta < 0)
            {
                theta += 2 * Math.PI;
            }

            double alpha = Math.Sqrt((cx * cx) + (cy * cy));

            int h = (int)(theta / (Math.PI * 2) * 360d);
            int s = (int)Math.Min(100.0, alpha / (Width / 2d) * 100);
            int v = 100;

            hsvColor = new HsvColor(h, s, v);
            ColorChanged?.Invoke(this, EventArgs.Empty);
            Invalidate(true);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button == MouseButtons.Left)
            {
                mouseHeld = true;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (mouseHeld)
            {
                GrabColor(new Point(e.X, e.Y));
            }

            mouseHeld = false;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (mouseHeld)
            {
                GrabColor(new Point(e.X, e.Y));
            }
        }

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // Dispose managed resources here, set big fields to null, etc.
            }

            renderBitmap?.Dispose();

            base.Dispose(disposing);
        }

        private void SetupGui()
        {
            wheelPictureBox = new PictureBox();
            SuspendLayout();

            #region wheelPictureBox
            wheelPictureBox.TabIndex = 0;
            wheelPictureBox.TabStop = false;
            wheelPictureBox.Click += WheelPictureBox_Click;
            wheelPictureBox.Paint += WheelPictureBox_Paint;
            wheelPictureBox.MouseUp += WheelPictureBox_MouseUp;
            wheelPictureBox.MouseMove += WheelPictureBox_MouseMove;
            wheelPictureBox.MouseEnter += WheelPictureBox_MouseEnter;
            wheelPictureBox.MouseDown += WheelPictureBox_MouseDown;
            #endregion

            #region ColorWheel
            Controls.Add(wheelPictureBox);
            ResumeLayout(false);
            #endregion
        }

        private void ColorWheel_VisibleChanged(object sender, EventArgs e)
        {
            if (!Visible)
            {
                wheelPictureBox.Image = null;
                renderBitmap?.Dispose();
                renderBitmap = null;
            }
            else
            {
                InitRendering();
            }
        }

        private void WheelPictureBox_Click(object sender, EventArgs e)
        {
            OnClick(e);
        }

        private void WheelPictureBox_MouseDown(object sender, MouseEventArgs e)
        {
            OnMouseDown(e);
        }

        private void WheelPictureBox_MouseEnter(object sender, EventArgs e)
        {
            OnMouseEnter(e);
        }

        private void WheelPictureBox_MouseMove(object sender, MouseEventArgs e)
        {
            OnMouseMove(e);
        }

        private void WheelPictureBox_MouseUp(object sender, MouseEventArgs e)
        {
            OnMouseUp(e);
        }

        private void WheelPictureBox_Paint(object sender, PaintEventArgs e)
        {
            float radius = Width / 2f;
            float theta = HsvColor.Hue / 360.0f * (float)Math.PI * 2.0f;
            float alpha = HsvColor.Saturation / 100.0f;
            float x = (alpha * (radius - 1) * (float)Math.Cos(theta)) + radius;
            float y = (alpha * (radius - 1) * (float)Math.Sin(theta)) + radius;
            int ix = (int)x;
            int iy = (int)y;

            // Draw the 'target rectangle'
            GraphicsContainer container = e.Graphics.BeginContainer();
            e.Graphics.PixelOffsetMode = PixelOffsetMode.None;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            e.Graphics.DrawEllipse(Pens.Black, ix - 4, iy - 4, 8, 8);
            e.Graphics.EndContainer(container);
        }
    }
}