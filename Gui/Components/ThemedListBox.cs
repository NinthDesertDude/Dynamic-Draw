using System.Windows.Forms;
using DynamicDraw.Interop;

namespace DynamicDraw
{
    /// <summary>
    /// A themed listbox.
    /// </summary>
    public class ThemedListBox : ListBox
    {
        public ThemedListBox()
        {
            HandleCreated += ThemedListBox_HandleCreated;
            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();
        }

        /// <summary>
        /// Scrollbar is themed by this call, and it must be updated whenever the handle is recreated.
        /// </summary>
        private void ThemedListBox_HandleCreated(object sender, System.EventArgs e)
        {
            ExternalOps.UpdateDarkMode(this);
        }

        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
            ExternalOps.UpdateDarkMode(this);
        }
    }
}
