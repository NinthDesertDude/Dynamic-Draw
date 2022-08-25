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
using System.Collections.Generic;
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

        /// <summary>
        /// Half the number of angles to show for angle snapping while shift is held. These angles will be evenly
        /// spaced across the circle.
        /// </summary>
        private const int snapAngleCountPerPi = 12;

        private Bitmap renderBitmap = null;
        private bool mouseHeld = false;
        private bool mouseOver = false;
        bool isCtrlHeld = false;
        bool isShiftHeld = false;

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

        /// <summary>
        /// Generates the bitmap of the color wheel if unset.
        /// </summary>
        private void InitRendering()
        {
            if (renderBitmap == null)
            {
                renderBitmap?.Dispose();
                renderBitmap = new Bitmap(Width, Width, PixelFormat.Format24bppRgb);

                using (Graphics g = Graphics.FromImage(renderBitmap))
                {
                    g.Clear(BackColor);
                    DrawWheel(g, renderBitmap.Width);
                }

                wheelPictureBox.SizeMode = PictureBoxSizeMode.Normal;
                wheelPictureBox.Size = Size;
                wheelPictureBox.Image = renderBitmap;
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

        /// <summary>
        /// Gets the color of the wheel at the given location, which should be the mouse location. Respects saturation
        /// lock and hue angle snapping (the Ctrl and Shift modifier behaviors of Paint.Net).
        /// </summary>
        private void GrabColor(Point mouseLoc)
        {
            // center our coordinate system so the middle is (0,0), and positive Y is facing up
            int radius = Width / 2;
            int cx = mouseLoc.X - radius;
            int cy = mouseLoc.Y - radius;

            double angle = Math.Atan2(cy, cx);

            if (angle < 0)
            {
                angle += 2 * Math.PI;
            }

            // Snap to degree increments equal to 1/x of pi
            if (isShiftHeld)
            {
                double snapAngle = Math.PI / snapAngleCountPerPi;
                angle = snapAngle * Math.Round(angle / snapAngle);
            }

            double alpha = Math.Sqrt((cx * cx) + (cy * cy));
            int h = (int)(angle / (Math.PI * 2) * 360d);
            int s = isCtrlHeld
                ? hsvColor.Saturation
                : (int)Math.Min(100.0, alpha / (Width / 2d) * 100);
            
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
            wheelPictureBox.MouseLeave += WheelPictureBox_MouseLeave;
            #endregion

            #region ColorWheel
            KeyDown += ColorWheel_KeyDown;
            KeyUp += ColorWheel_KeyUp;
            Controls.Add(wheelPictureBox);
            ResumeLayout(false);
            #endregion
        }

        private void ColorWheel_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.ShiftKey)
            {
                isShiftHeld = false;
                Refresh();
            }
            else if (e.KeyCode == Keys.ControlKey)
            {
                isCtrlHeld = false;
                Refresh();
            }
        }

        private void ColorWheel_KeyDown(object sender, KeyEventArgs e)
        {
            if (mouseOver || Focused)
            {
                if (e.KeyCode == Keys.ShiftKey)
                {
                    isShiftHeld = true;
                    Refresh();
                }
                if (e.KeyCode == Keys.ControlKey)
                {
                    isCtrlHeld = true;
                    Refresh();
                }
            }
        }

        private void WheelPictureBox_MouseLeave(object sender, EventArgs e)
        {
            mouseOver = false;
            isShiftHeld = false;
            isCtrlHeld = false;

            OnMouseLeave(e);
        }

        private void ColorWheel_VisibleChanged(object sender, EventArgs e)
        {
            if (!Visible)
            {
                wheelPictureBox.Image = null;
                renderBitmap?.Dispose();
                renderBitmap = null;
                mouseHeld = false;
                mouseOver = false;
                isShiftHeld = false;
                isCtrlHeld = false;
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
            mouseOver = true;
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
            double angle = HsvColor.Hue * Math.PI / 180;
            float satRatio = HsvColor.Saturation / 100.0f;
            float x = (satRatio * (radius - 1) * (float)Math.Cos(angle)) + radius;
            float y = (satRatio * (radius - 1) * (float)Math.Sin(angle)) + radius;
            int ix = (int)x;
            int iy = (int)y;
            
            GraphicsContainer container = e.Graphics.BeginContainer();
            e.Graphics.PixelOffsetMode = PixelOffsetMode.None;
            e.Graphics.SmoothingMode = SmoothingMode.HighQuality;
            using Pen transparentBlack = new Pen(Color.FromArgb(128, Color.Black));

            // Draws a circle for the current radius from the center.
            if (isCtrlHeld)
            {
                float dist = radius * 2 * satRatio;
                float distHalf = dist / 2f;
                e.Graphics.DrawEllipse(transparentBlack, radius - distHalf, radius - distHalf, dist, dist);

                // Draws little circles where the snapping lines would be, on the radius circle.
                if (isShiftHeld)
                {
                    int snapAngleCountPer2Pi = snapAngleCountPerPi * 2;
                    double circleSnapIncrement = Math.PI / snapAngleCountPerPi;

                    for (int i = 0; i < snapAngleCountPer2Pi; i++)
                    {
                        double circleSnapAngle = circleSnapIncrement * i;
                        float xAngle = (float)Math.Cos(circleSnapAngle);
                        float yAngle = (float)Math.Sin(circleSnapAngle);

                        e.Graphics.DrawEllipse(transparentBlack,
                            radius + xAngle * distHalf - 1.5f,
                            radius + yAngle * distHalf - 1.5f,
                            3, 3);
                    }
                }
            }

            // Draws the snapping lines according to how many there are.
            else if (isShiftHeld)
            {
                int snapAngleCountPer2Pi = snapAngleCountPerPi * 2;
                double lineSnapIncrement = Math.PI / snapAngleCountPerPi;

                for (int i = 0; i < snapAngleCountPer2Pi; i++)
                {
                    double lineSnapAngle = lineSnapIncrement * i;
                    float xAngle = (float)Math.Cos(lineSnapAngle);
                    float yAngle = (float)Math.Sin(lineSnapAngle);

                    e.Graphics.DrawLine(transparentBlack,
                        new PointF(radius + xAngle * 6, radius + yAngle * 6),
                        new PointF(radius + xAngle * radius, radius + yAngle * radius));
                }

                e.Graphics.DrawEllipse(transparentBlack, radius - 3, radius - 3, 3, 3);
            }

            // Draw the cursor for the picture box.
            using Brush brush = new SolidBrush(hsvColor.ToColor());
            e.Graphics.FillEllipse(brush, ix - 3, iy - 3, 6, 6);
            e.Graphics.DrawEllipse(Pens.White, ix - 3.5f, iy - 3.5f, 7, 7);
            e.Graphics.DrawEllipse(Pens.Black, ix - 4, iy - 4, 8, 8);

            e.Graphics.EndContainer(container);
        }
    }
}