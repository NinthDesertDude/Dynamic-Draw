using System;
using System.Drawing;
using System.Windows.Forms;

namespace DynamicDraw
{
    /// <summary>
    /// A textbox editor for an associated color, taking both 6 or 8 digit hex codes, optionally prefixed by #.
    /// </summary>
    public class ColorTextbox : TextBox
    {
        private Color associatedColor;
        private bool includeAlpha;

        /// <summary>
        /// This event fires whenever the color associated to this textbox changes as a result of the text changing to
        /// a parseable color.
        /// </summary>
        public event Action ColorUpdatedByText;

        /// <summary>
        /// The color bound to the textbox.
        /// </summary>
        public Color AssociatedColor
        {
            get
            {
                return associatedColor;
            }
            set
            {
                if (associatedColor != value)
                {
                    associatedColor = value;
                    Text = "#" + ColorUtils.GetTextFromColor(associatedColor);
                }
            }
        }

        /// <summary>
        /// Whether the textbox allows 8-character input (ARGB instead of RGB).
        /// </summary>
        public bool IncludeAlpha
        {
            get
            {
                return includeAlpha;
            }
            set
            {
                includeAlpha = value;
            }
        }

        public ColorTextbox(Color color, bool includeAlpha) : base()
        {
            this.associatedColor = color;
            this.includeAlpha = includeAlpha;

            SemanticTheme.ThemeChanged += HandleTheme;
            HandleTheme();
            BorderStyle = BorderStyle.FixedSingle;
            TextChanged += ColorTextbox_TextChanged;
        }

        /// <summary>
        /// Updates the stored color whenever a valid color is entered in hex notation.
        /// </summary>
        private void ColorTextbox_TextChanged(object sender, EventArgs e)
        {
            Color? col = ColorUtils.GetColorFromText(Text, includeAlpha, AssociatedColor.A);

            if (col != null && col != associatedColor)
            {
                associatedColor = col.Value;
                ColorUpdatedByText?.Invoke();
            }
        }

        /// <summary>
        /// Any color logic that gets set only once, dependent on the current theme, needs to subscribe to the theme
        /// changed event so it can be recalculated when theme preference loads from asynchronous user settings.
        /// </summary>
        private void HandleTheme()
        {
            BackColor = SemanticTheme.GetColor(ThemeSlot.ControlBg);
            ForeColor = SemanticTheme.GetColor(ThemeSlot.Text);
        }
    }
}
