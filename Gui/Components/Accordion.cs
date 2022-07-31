using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A button that hides/shows associated controls when toggled.
    /// </summary>
    public class Accordion : Button
    {
        private bool isHovered = false;

        private string title = "";
        private readonly List<Control> boundControls = new List<Control>();
        private bool isCollapsed = false;
        private readonly bool redAccented = false;

        /// <summary>
        /// Creates a new accordion button.
        /// </summary>
        /// <param name="boundControls">The list of controls bound to the accordion.</param>
        /// <param name="isCollapsed">If true, controls bound to the accordion will not be visible.</param>
        public Accordion(bool redAccented = false)
        {
            this.redAccented = redAccented;

            Click += (a, b) =>
            {
                ToggleCollapsed(!isCollapsed);
                Refresh();
            };

            MouseEnter += Accordion_MouseEnter;
            MouseLeave += Accordion_MouseLeave;
        }

        private void Accordion_MouseLeave(object sender, System.EventArgs e)
        {
            isHovered = false;
        }

        private void Accordion_MouseEnter(object sender, System.EventArgs e)
        {
            isHovered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Brush accordionBg =
                (Enabled && isHovered)
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgHighlight)
                : (Enabled && redAccented)
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlRedAccent)
                : Enabled
                    ? SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBg)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlBgDisabled);

            e.Graphics.FillRectangle(accordionBg, 0, 0, Width, Height);

            var measures = e.Graphics.MeasureString(Text, Font);
            e.Graphics.DrawString(
                Text,
                Font,
                Enabled && redAccented && !isHovered
                ? SemanticTheme.Instance.GetBrush(ThemeName.Dark, ThemeSlot.MenuControlText)
                : SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlText),
                new Point(
                4,
                (int)((Height - measures.Height) / 2f)
            ));

            string collapseStr = isCollapsed ? "⮞" : "⮟";
            measures = e.Graphics.MeasureString(collapseStr, Font);
            e.Graphics.DrawString(
                collapseStr,
                Font,
                SemanticTheme.Instance.GetBrush(ThemeSlot.MenuControlTextSubtle),
                new Point(Width - 16, (int)((Height - measures.Height) / 2f)));

            // Draws a rectangle indicating focus.
            if (Enabled && Focused && ShowFocusCues)
            {
                e.Graphics.DrawRectangle(
                    SemanticTheme.Instance.GetPen(ThemeSlot.MenuControlActive), 0, 0, Width - 1, Height - 1);
            }
        }

        /// <summary>
        /// Replaces the list of controls that get shown/hidden when the button is toggled.
        /// </summary>
        public void UpdateAccordion(string title, bool isCollapsed, IEnumerable<Control> controls)
        {
            this.isCollapsed = isCollapsed;
            this.title = title;
            boundControls.Clear();
            boundControls.AddRange(controls);
            UpdateCollapsedState();
        }

        /// <summary>
        /// Updates the button and bound control(s) visibility.
        /// </summary>
        private void UpdateCollapsedState()
        {
            Text = title;

            for (int i = 0; i < boundControls.Count; i++)
            {
                if (boundControls[i] != null)
                {
                    boundControls[i].Visible = !isCollapsed;
                }
            }
        }

        /// <summary>
        /// Toggles the accordion, same as clicking it.
        /// </summary>
        public void ToggleCollapsed(bool isCollapsed)
        {
            this.isCollapsed = isCollapsed;
            UpdateCollapsedState();
        }
    }
}
