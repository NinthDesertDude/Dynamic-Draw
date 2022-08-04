namespace DynamicDraw
{
    /// <summary>
    /// Represents the usecases which each theme applies colors to. This "theme type, theme slot" system allows
    /// controls to generically request colors using <see cref="SemanticTheme.GetColor"/> and receive the correct
    /// color specified by the current / passed-in theme.
    /// </summary>
    public enum ThemeSlot
    {
        /// <summary>
        /// Same as <see cref="ThemeSlot.MenuControlBg"/>, but at half transparency.
        /// </summary>
        HalfAlphaMenuControlBg,

        /// <summary>
        /// The background color of the canvas.
        /// </summary>
        CanvasBg,

        /// <summary>
        /// The background color of panels outside the canvas.
        /// </summary>
        MenuBg,

        /// <summary>
        /// A highly visible accent color (usually blue) for the interactive parts of some controls.
        /// </summary>
        MenuControlActive,

        /// <summary>
        /// A less visible accent color (usually blue) to indicate selection.
        /// </summary>
        MenuControlActiveSelected,

        /// <summary>
        /// A subtle accent color (usually blue) shown on hovering over some interactive controls.
        /// </summary>
        MenuControlActiveHover,

        /// <summary>
        /// The background color of controls placed in panels outside the canvas.
        /// </summary>
        MenuControlBg,

        /// <summary>
        /// The background color of a disabled control placed in panels outside the canvas.
        /// </summary>
        MenuControlBgDisabled,

        /// <summary>
        /// The background color of a control that's highlighted, such as a toggled state.
        /// </summary>
        MenuControlBgHighlight,

        /// <summary>
        /// The background color of a control that's highlighted but disabled, such as a toggled state.
        /// </summary>
        MenuControlBgHighlightDisabled,

        /// <summary>
        /// The color of text displays on menu controls.
        /// </summary>
        MenuControlText,

        /// <summary>
        /// A red accent used to draw attention or distinguish controls that need it. This should be used sparingly.
        /// </summary>
        MenuControlRedAccent,

        /// <summary>
        /// The color of text belonging to and displayed over a disabled control.
        /// </summary>
        MenuControlTextDisabled,

        /// <summary>
        /// A more subtle color for text that displays on menu controls.
        /// </summary>
        MenuControlTextSubtle
    }
}
