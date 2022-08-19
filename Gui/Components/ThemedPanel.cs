using System.Windows.Forms;
using DynamicDraw.Interop;

namespace DynamicDraw
{
    /// <summary>
    /// A themed panel.
    /// </summary>
    public class ThemedPanel : Panel
    {
        public ThemedPanel()
        {
            HandleCreated += ThemedPanel_HandleCreated;
            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();
        }

        /// <summary>
        /// Scrollbar is themed by this call, and it must be updated whenever the handle is recreated.
        /// </summary>
        private void ThemedPanel_HandleCreated(object sender, System.EventArgs e)
        {
            ExternalOps.UpdateDarkMode(this);
        }

        private void HandleTheme()
        {
            ExternalOps.UpdateDarkMode(this);
        }
    }
}
