using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A themed variant of the checkbox control that displays an image instead of a check mark, and may use different
    /// images depending on the check state.
    /// </summary>
    public class ToggleButton : CheckBox
    {
        private bool isHovered = false;

        /// <summary>
        /// When set, the given images will be used to toggle. These are not disposed by the control. When unset, the
        /// regular image field will be used instead if possible.
        /// </summary>
        public (Image onState, Image offState) ToggleImage { get; set; }

        /// <summary>
        /// If false, the background color is drawn to the whole bounds of the control, making it look like one big
        /// button. If true, a checkbox is automatically drawn on the left side.
        /// </summary>
        public bool RenderAsCheckbox { get; set; }

        public ToggleButton(bool renderAsCheckbox = true) : base()
        {
            ToggleImage = (null, null);
            RenderAsCheckbox = renderAsCheckbox;
            MouseEnter += ToggleButton_MouseEnter;
            MouseLeave += ToggleButton_MouseLeave;
        }

        private void ToggleButton_MouseLeave(object sender, EventArgs e)
        {
            isHovered = false;
        }

        private void ToggleButton_MouseEnter(object sender, EventArgs e)
        {
            isHovered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            const int padding = 4;
            const int chkbxSize = 16;

            if (RenderAsCheckbox)
            {
                Pen chkbxBorderColor =
                (Enabled && isHovered)
                    ? SemanticTheme.Instance.GetPen(ThemeSlot.MenuControlActive)
                : (Enabled)
                    ? SemanticTheme.Instance.GetPen(ThemeSlot.MenuControlBgHighlight)
                : SemanticTheme.Instance.GetPen(ThemeSlot.MenuControlBgHighlightDisabled);

                Brush chkbxBackColor =
                    (Enabled && isHovered)
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlActiveHover)
                    : (Enabled && Checked)
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlActiveSelected)
                    : (Enabled)
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBg)
                    : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgDisabled);

                // Clears the paint zone (required to avoid visual issues).
                e.Graphics.FillRectangle(
                    SemanticTheme.Instance.GetBrush(ThemeSlot.MenuBg), 0, 0, Width, Height);

                // Draws a checkbox background
                Point chkbxPos = PositionElement(
                    ContentAlignment.MiddleLeft, chkbxSize, chkbxSize, Width, Height);

                e.Graphics.FillRectangle(
                    chkbxBackColor, chkbxPos.X, chkbxPos.Y, chkbxSize, chkbxSize);

                e.Graphics.DrawRectangle(chkbxBorderColor, chkbxPos.X, chkbxPos.Y, chkbxSize, chkbxSize);

                if (Checked)
                {
                    var measures = e.Graphics.MeasureString("✓", Font);

                    // Draws the check for a checked checkbox
                    Point chkbxCheckPos = PositionElement(
                        ContentAlignment.MiddleCenter, (int)measures.Width, (int)measures.Height, chkbxSize, chkbxSize);

                    e.Graphics.DrawString("✓", Font, Enabled
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlActive)
                        : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlTextDisabled),
                    chkbxPos.X + chkbxCheckPos.X, chkbxPos.Y + chkbxCheckPos.Y);
                }
            }
            else
            {
                Brush chkbxBackColor =
                    (Enabled && isHovered)
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgHighlight)
                    : (Enabled && Checked)
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlActive)
                    : (Enabled)
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBg)
                    : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgDisabled);

                e.Graphics.FillRectangle(chkbxBackColor, 0, 0, Width, Height);
            }

            // Draws the image, if any.
            Image imageToDraw = 
                (Checked && ToggleImage.onState != null)
                    ? ToggleImage.onState
                : (!Checked && ToggleImage.offState != null)
                    ? ToggleImage.offState
                : Image ?? null;

            Point imagePos = Point.Empty;
            if (imageToDraw != null)
            {
                imagePos = PositionElement(ImageAlign, imageToDraw.Width, imageToDraw.Height, Width, Height);
                SemanticTheme.DrawImageForTheme(e.Graphics, imageToDraw, !Enabled, imagePos.X, imagePos.Y);
            }

            // Draws the text, if any.
            if (!string.IsNullOrEmpty(Text))
            {
                var measures = e.Graphics.MeasureString(Text, Font);
                Point textPos = PositionElement(TextAlign, (int)measures.Width + padding, (int)measures.Height, Width, Height);

                // Moves text out of the way of the image, if any.
                if (imagePos != Point.Empty)
                {
                    int imageOverlapX = Math.Max(imagePos.X + imageToDraw.Width - textPos.X, 0);
                    textPos = new Point(textPos.X + imageOverlapX, textPos.Y);
                }

                // Moves text out of the way of the checkbox drawing, if set.
                if (RenderAsCheckbox)
                {
                    textPos = new Point(textPos.X + chkbxSize + padding, textPos.Y);
                }

                e.Graphics.DrawString(Text, Font,
                    Enabled
                        ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlText)
                        : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlTextDisabled),
                    textPos);
            }

            // Draws a rectangle indicating focus.
            if (Enabled && Focused && ShowFocusCues)
            {
                e.Graphics.DrawRectangle(
                    SemanticTheme.Instance.GetPen(ThemeSlot.MenuControlActive), 0, 0, Width - 1, Height - 1);
            }
        }

        /// <summary>
        /// Returns the position of a rectangle within the bounds of this control, according to the given alignment.
        /// </summary>
        private Point PositionElement(
            ContentAlignment alignment,
            int elementWidth, int elementHeight,
            int containerWidth, int containerHeight)
        {
            switch (alignment)
            {
                case ContentAlignment.TopLeft:
                    return Point.Empty;
                case ContentAlignment.TopCenter:
                    return new Point((int)((containerWidth - elementWidth) / 2f), 0);
                case ContentAlignment.TopRight:
                    return new Point(containerWidth - elementWidth, 0);
                case ContentAlignment.MiddleLeft:
                    return new Point(0, (int)((containerHeight - elementHeight) / 2f));
                case ContentAlignment.MiddleCenter:
                    return new Point((int)((containerWidth - elementWidth) / 2f), (int)((containerHeight - elementHeight) / 2f));
                case ContentAlignment.MiddleRight:
                    return new Point(containerWidth - elementWidth, (int)((containerHeight - elementHeight) / 2f));
                case ContentAlignment.BottomLeft:
                    return new Point(0, containerHeight - elementHeight);
                case ContentAlignment.BottomCenter:
                    return new Point((int)((containerWidth - elementWidth) / 2f), containerHeight - elementHeight);
                case ContentAlignment.BottomRight:
                    return new Point(containerWidth - elementWidth, containerHeight - elementHeight);
            }

            throw new Exception("Unexpected value for ContentAlignment.");
        }
    }
}
