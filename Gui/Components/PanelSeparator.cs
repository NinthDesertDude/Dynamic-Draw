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
            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();
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

        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.MenuControlBg);
        }
    }
}
