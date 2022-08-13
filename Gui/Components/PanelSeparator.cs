using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// Creates a separator (similar to menu separators) that works in panels.
    /// </summary>
    public class PanelSeparator : Panel
    {
        public PanelSeparator(bool isVertical, int length, int containerLength) : base()
        {
            AutoSize = false;
            Height = isVertical ? length : 2;
            Width = isVertical ? 2 : length;

            if (length < containerLength)
            {
                Margin = isVertical
                    ? new Padding(4, (containerLength - length) / 2, 4, 0)
                    : new Padding(4, 0, 4, (containerLength - length) / 2);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (Width > Height)
            {
                e.Graphics.DrawLine(
                    SemanticTheme.Instance.GetPen(ThemeSlot.MenuSeparator),
                    new Point(0, Height / 2), new Point(Width, Height / 2));
            }
            else
            {
                e.Graphics.DrawLine(
                    SemanticTheme.Instance.GetPen(ThemeSlot.MenuSeparator),
                    new Point(Width / 2, 0), new Point(Width / 2, Height));
            }
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.MenuBg), ClientRectangle);
        }
    }
}