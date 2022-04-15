using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DynamicDraw.Gui
{
    /// <summary>
    /// A button that hides/shows associated controls when toggled.
    /// </summary>
    public class Accordion : Button
    {
        private string title = "";
        private readonly List<Control> boundControls = new List<Control>();
        private bool isCollapsed = false;

        /// <summary>
        /// Creates a new accordion button.
        /// </summary>
        /// <param name="boundControls">The list of controls bound to the accordion.</param>
        /// <param name="isCollapsed">If true, controls bound to the accordion will not be visible.</param>
        public Accordion()
        {
            Click += (a, b) =>
            {
                ToggleCollapsed(!this.isCollapsed);
            };
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
            Text = (isCollapsed ? "▶ " : "▼ ") + title;

            for (int i = 0; i < boundControls.Count; i++)
            {
                boundControls[i].Visible = !isCollapsed;
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
