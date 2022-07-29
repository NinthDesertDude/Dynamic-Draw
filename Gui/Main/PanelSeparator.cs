using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// Creates a separator (similar to menu separators) that works in panels.
    /// </summary>
    public class PanelSeparator : Label
    {
        public PanelSeparator(bool isVertical, int length, int containerLength) : base()
        {
            AutoSize = false;
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
            BorderStyle = BorderStyle.FixedSingle;
            Height = isVertical ? length : 2;
            Width = isVertical ? 2 : length;

            if (length < containerLength)
            {
                Margin = isVertical
                    ? new Padding(4, (containerLength - length) / 2, 4, 0)
                    : new Padding(4, 0, 4, (containerLength - length) / 2);
            }
        }
    }
}
