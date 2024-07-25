using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A button that displays a table of colors in the given number of rows, dividing space evenly. Unused space may
    /// occur on the right and bottom when the swatches don't evenly fill the button dimensions. Extra colors are
    /// divided into the last row if there aren't enough to fill a row.
    /// 
    /// The hovered/focused colors are tracked, and clicking fires the clicked event.
    /// </summary>
    public class SwatchBox : Button
    {
        private int numRows;
        private int selectedIndex;
        private List<Color> swatches;

        /// <summary>
        /// Sets the selected index to the given value, or null to deselect.
        /// </summary>
        public int? SelectedIndex
        {
            get
            {
                return selectedIndex;
            }
            set
            {
                selectedIndex = value ?? -1;
                Refresh();
            }
        }

        /// <summary>
        /// Sets the colors to be displayed in the swatch box. They are drawn left-to-right, top to bottom. There are
        /// (number of swatches / numRows) colors per row, with the remainder fit into the last row.
        /// </summary>
        public List<Color> Swatches
        {
            get
            {
                return swatches;
            }
            set
            {
                swatches = value ?? new List<Color>();
                Refresh();
            }
        }

        /// <summary>
        /// Sets the number of rows to display in the swatch box, minimum 1.
        /// </summary>
        public int NumRows
        {
            get
            {
                return numRows;
            }
            set
            {
                numRows = (value > 0) ? value : 1;
                Refresh();
            }
        }

        /// <summary>
        /// Fires when one of the colors included in the swatch list is clicked, passing the index in the palette.
        /// </summary>
        public event Action<int> SwatchClicked;

        public SwatchBox(List<Color> swatches, int numRows) : base()
        {
            Margin = Padding.Empty;
            Padding = Padding.Empty;
            this.numRows = (numRows > 0) ? numRows : 1;
            this.swatches = swatches ?? new List<Color>();
            selectedIndex = -1;
            MouseMove += SwatchBox_MouseMove;
            Click += SwatchBox_Click;
        }

        /// <summary>
        /// Fires <see cref="SwatchClicked"/> with the clicked color index on mouse down.
        /// </summary>
        private void SwatchBox_Click(object sender, EventArgs e)
        {
            if (swatches.Count == 0 || numRows < 1)
            {
                return;
            }

            Point cursor = PointToClient(Cursor.Position);
            if (cursor.X >= 0 && cursor.Y >= 0 && cursor.X < Width && cursor.Y < Height)
            {
                int ySize = Height / numRows;
                int cursorRow = cursor.Y / ySize;

                int swatchesPerRow = swatches.Count / numRows;
                int leftoverSwatches = swatches.Count % numRows;

                int xSize = (cursorRow == numRows - 1)
                    ? leftoverSwatches + swatchesPerRow
                    : swatchesPerRow;

                int cursorCol = cursor.X / (Width / xSize);
                int matchingIndex = (cursorRow * swatchesPerRow) + cursorCol;

                // clicking in the uneven blank gutters at the end of palette rows can pick indices out of range.
                if (matchingIndex < swatches.Count)
                {
                    SwatchClicked?.Invoke(matchingIndex);
                }
            }
        }

        /// <summary>
        /// Refreshes to redraw, which uses the mouse position.
        /// </summary>
        private void SwatchBox_MouseMove(object sender, MouseEventArgs e)
        {
            Refresh();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (swatches.Count == 0 || numRows == 0 || Width == 0 || Height == 0)
            {
                // Paints the control space to avoid drawing artifacts.
                e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBg),
                    0, 0, Width, Height);

                return;
            }

            Point cursor = PointToClient(Cursor.Position);
            int ySize = Math.Max(Height / numRows, 1);
            int cursorRow = cursor.Y / ySize;

            int swatchesPerRow = Math.Max(swatches.Count / numRows, 1);
            int leftoverSwatches = swatches.Count > numRows
                ? swatches.Count % numRows
                : 0;

            int xSize = (cursorRow == numRows - 1)
                    ? leftoverSwatches + swatchesPerRow
                    : swatchesPerRow;
            int cursorCol = cursor.X / Math.Max(Width / xSize, 1);

            for (int row = 0; row < numRows && row < swatches.Count; row++)
            {
                int swatchesThisRow = (row == numRows - 1)
                    ? swatchesPerRow + leftoverSwatches : swatchesPerRow;

                int swatchW = Width / swatchesThisRow;
                int swatchH = Height / numRows;

                // Fills the unpainted ends, if any, of the palette row so nothing else shows through.
                int leftoverWidth = Width - (swatchesThisRow * swatchW);
                int leftoverHeight = Height - (numRows * swatchH);
                if (leftoverWidth > 0)
                {
                    e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBg),
                        Width - leftoverWidth,
                        0,
                        leftoverWidth,
                        Height);
                }
                if (leftoverHeight > 0)
                {
                    e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBg),
                        0,
                        Height - leftoverHeight,
                        Width,
                        leftoverHeight);
                }

                for (int col = 0; col < swatchesThisRow; col++)
                {
                    Color color = swatches[(swatchesPerRow * row) + col];
                    // Draws the swatch color, with an underlying checkered pattern if it's partially transparent.
                    if (color.A != 255)
                    {
                        e.Graphics.FillRectangle(SemanticTheme.SpecialBrushCheckeredTransparent,
                            col * swatchW,
                            row * swatchH,
                            swatchW,
                            swatchH);
                    }

                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        e.Graphics.FillRectangle(brush,
                            col * swatchW,
                            row * swatchH,
                            swatchW,
                            swatchH);
                    }

                    // Draws a hovered state for the hovered swatch, if any.
                    if (cursorCol == col && cursorRow == row)
                    {
                        bool useWhite = color.GetBrightness() < 0.5f;

                        e.Graphics.DrawRectangle(
                            useWhite ? Pens.White : Pens.Black,
                            col * swatchW,
                            row * swatchH,
                            swatchW - 1,
                            swatchH - 1);
                    }

                    // Draws the focused state on the focused swatch, if any.
                    else if (selectedIndex == (swatchesPerRow * row) + col)
                    {
                        e.Graphics.DrawRectangle(
                            SemanticTheme.Instance.GetPen(ThemeSlot.ControlActive),
                            cursorCol * xSize,
                            cursorRow * ySize,
                            xSize - 1,
                            ySize - 1);
                    }
                }
            }

            // Draws a rectangle indicating focus.
            if (Enabled && Focused && ShowFocusCues)
            {
                e.Graphics.DrawRectangle(
                    SemanticTheme.Instance.GetPen(ThemeSlot.ControlActive), 0, 0, Width - 1, Height - 1);
            }
        }
    }
}
