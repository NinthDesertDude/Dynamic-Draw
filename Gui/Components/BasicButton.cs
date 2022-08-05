using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A themed variant of the button control that displays an image (with inverted color for light mode).
    /// </summary>
    public class BasicButton : Button
    {
        private readonly bool hideIdleBgColor = false;
        private bool isHovered = false;
        private readonly bool redAccented = false;

        public BasicButton(bool hideIdleBgColor = false, bool redAccented = false) : base()
        {
            this.hideIdleBgColor = hideIdleBgColor;
            this.redAccented = redAccented;
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
                : (Enabled && redAccented)
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlRedAccent)
                : (Enabled)
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBg)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgDisabled);

            e.Graphics.FillRectangle(basicButtonBg, 0, 0, Width, Height);

            // Draws the image centered, if any.
            Point imagePos = Point.Empty;

            if (Image != null)
            {
                imagePos = LayoutUtils.PositionElement(ImageAlign, Image.Width, Image.Height, Width, Height);
                SemanticTheme.DrawImageForTheme(e.Graphics, Image, !Enabled, imagePos.X, imagePos.Y);
            }

            // Draws the text, if any.
            if (!string.IsNullOrEmpty(Text))
            {
                const int padding = 4;
                var measures = e.Graphics.MeasureString(Text, Font);
                Point textPos = LayoutUtils.PositionElement(TextAlign, (int)measures.Width + padding, (int)measures.Height, Width, Height);

                // Moves text out of the way of the image, if any.
                if (imagePos != Point.Empty)
                {
                    int imageOverlapX = Math.Max(imagePos.X + Image.Width - textPos.X, 0);
                    textPos = new Point(textPos.X + imageOverlapX, textPos.Y);
                }

                e.Graphics.DrawString(
                    Text,
                    Font,
                    Enabled && redAccented && !isHovered && !hideIdleBgColor
                        ? SemanticTheme.Instance.GetBrush(ThemeName.Dark, ThemeSlot.MenuControlText)
                        : Enabled
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
    }
}
