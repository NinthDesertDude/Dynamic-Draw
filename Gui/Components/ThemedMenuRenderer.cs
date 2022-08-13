using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// Supply this to any menu taking a <see cref="ToolStripProfessionalRenderer"/> to theme it and its submenus.
    /// </summary>
    public class ThemedMenuRenderer : ToolStripProfessionalRenderer
    {
        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            e.Graphics.FillRectangle(SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBg),
                new Rectangle(0, 0, e.Item.Width, e.Item.Height));

            if (e.Vertical)
            {
                e.Graphics.DrawLine(SemanticTheme.Instance.GetPen(ThemeSlot.MenuSeparator),
                    new Point(e.Item.Width / 2, 0), new Point(e.Item.Width / 2, e.Item.Height));
            }
            else
            {
                e.Graphics.DrawLine(SemanticTheme.Instance.GetPen(ThemeSlot.MenuSeparator),
                    new Point(0, e.Item.Height / 2), new Point(e.Item.Width, e.Item.Height / 2));
            }
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            SolidBrush arrowColor = e.Item.Selected
                ? SemanticTheme.Instance.GetBrush(ThemeSlot.ControlActiveHover)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuArrow);

            SolidBrush bgColor = e.Item.Selected
                ? SemanticTheme.Instance.GetBrush(ThemeSlot.ControlActive)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBg);

            string glyph =
                e.Direction == ArrowDirection.Right ? "▶" :
                e.Direction == ArrowDirection.Left ? "◀" :
                e.Direction == ArrowDirection.Up ? "▲" :
                "▼";

            e.Graphics.FillRectangle(bgColor, e.ArrowRectangle);
            e.Graphics.DrawString(glyph, e.Item.Font, arrowColor, e.ArrowRectangle.Location);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e)
        {
            Pen chkbxBorderColor = e.Item.Enabled
                ? SemanticTheme.Instance.GetPen(ThemeSlot.ControlBgHighlight)
                : SemanticTheme.Instance.GetPen(ThemeSlot.ControlBgHighlightDisabled);

            Brush chkbxBackColor = e.Item.Enabled
                ? SemanticTheme.Instance.GetBrush(ThemeSlot.ControlActiveSelected)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBgDisabled);

            // Draws a checkbox background
            e.Graphics.FillRectangle(chkbxBackColor, e.ImageRectangle);
            e.Graphics.DrawRectangle(chkbxBorderColor, e.ImageRectangle);

            var measures = e.Graphics.MeasureString("✓", e.Item.Font);

            // Draws the check for a checked checkbox
            Point chkbxCheckPos = LayoutUtils.PositionElement(
                ContentAlignment.MiddleCenter,
                (int)measures.Width, (int)measures.Height,
                e.ImageRectangle.Width, e.ImageRectangle.Height);

            e.Graphics.DrawString("✓", e.Item.Font, e.Item.Enabled
                ? SemanticTheme.Instance.GetBrush(ThemeSlot.ControlActive)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.TextDisabled),
            e.ImageRectangle.X + chkbxCheckPos.X, e.ImageRectangle.Y + chkbxCheckPos.Y);
        }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            SolidBrush itemForeColor = e.Item.Enabled
                ? SemanticTheme.Instance.GetBrush(ThemeSlot.Text)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.TextDisabled);

            e.Graphics.DrawString(e.Text, e.TextFont, itemForeColor, e.TextRectangle);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            SolidBrush itemBackColor = e.Item.Selected
                ? SemanticTheme.Instance.GetBrush(ThemeSlot.ControlActive)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.ControlBg);

            e.Graphics.FillRectangle(itemBackColor, new Rectangle(Point.Empty, e.Item.Size));
        }
    }
}
