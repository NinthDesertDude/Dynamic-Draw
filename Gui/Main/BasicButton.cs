using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw.Gui
{
    /// <summary>
    /// A themed variant of the button control that displays an image (with inverted color for light mode).
    /// </summary>
    public class BasicButton : Button
    {
        private bool hideIdleBgColor = false;
        private bool isHovered = false;

        public BasicButton(bool hideIdleBgColor = false) : base()
        {
            this.hideIdleBgColor = hideIdleBgColor;
            MouseEnter += BasicButton_MouseEnter;
            MouseLeave += BasicButton_MouseLeave;
        }

        private void BasicButton_MouseLeave(object sender, EventArgs e)
        {
            isHovered = false;
        }

        private void BasicButton_MouseEnter(object sender, EventArgs e)
        {
            isHovered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Brush basicButtonBg =
                (Enabled && isHovered)
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgHighlight)
                : hideIdleBgColor
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuBg)
                : (Enabled)
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBg)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgDisabled);

            e.Graphics.FillRectangle(basicButtonBg, 0, 0, Width, Height);

            // Draws the image centered, if any.
            Point imagePos = Point.Empty;

            if (Image != null)
            {
                imagePos = PositionElement(ImageAlign, Image.Width, Image.Height);
                SemanticTheme.DrawImageForTheme(e.Graphics, Image, !Enabled, imagePos.X, imagePos.Y);
            }

            // Draws the text, if any.
            if (!string.IsNullOrEmpty(Text))
            {
                const int padding = 4;
                var measures = e.Graphics.MeasureString(Text, Font);
                Point textPos = PositionElement(TextAlign, (int)measures.Width + padding, (int)measures.Height);

                // Moves text out of the way of the image, if any.
                if (imagePos != Point.Empty)
                {
                    int imageOverlapX = Math.Max(imagePos.X + Image.Width - textPos.X, 0);
                    textPos = new Point(textPos.X + imageOverlapX, textPos.Y);
                }

                e.Graphics.DrawString(
                    Text,
                    Font,
                    Enabled
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlText)
                        : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlTextDisabled),
                    textPos);
            }
        }

        /// <summary>
        /// Returns the position of a rectangle within the bounds of this control, according to the given alignment.
        /// </summary>
        private Point PositionElement(ContentAlignment alignment, int elementWidth, int elementHeight)
        {
            switch (alignment)
            {
                case ContentAlignment.TopLeft:
                    return Point.Empty;
                case ContentAlignment.TopCenter:
                    return new Point((int)((Width - elementWidth) / 2f), 0);
                case ContentAlignment.TopRight:
                    return new Point(Width - elementWidth, 0);
                case ContentAlignment.MiddleLeft:
                    return new Point(0, (int)((Height - elementHeight) / 2f));
                case ContentAlignment.MiddleCenter:
                    return new Point((int)((Width - elementWidth) / 2f), (int)((Height - elementHeight) / 2f));
                case ContentAlignment.MiddleRight:
                    return new Point(Width - elementWidth, (int)((Height - elementHeight) / 2f));
                case ContentAlignment.BottomLeft:
                    return new Point(0, Height - elementHeight);
                case ContentAlignment.BottomCenter:
                    return new Point((int)((Width - elementWidth) / 2f), Height - elementHeight);
                case ContentAlignment.BottomRight:
                    return new Point(Width - elementWidth, Height - elementHeight);
            }

            throw new System.Exception("Unexpected value for ContentAlignment.");
        }
    }
}
