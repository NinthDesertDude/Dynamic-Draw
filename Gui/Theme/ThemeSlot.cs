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
        /// The background color of the canvas.
        /// </summary>
        CanvasBg,

        /// <summary>
        /// A highly visible accent color (usually blue) for the interactive parts of some controls.
        /// </summary>
        ControlActive,

        /// <summary>
        /// A less visible accent color (usually blue) to indicate selection.
        /// </summary>
        ControlActiveSelected,

        /// <summary>
        /// A subtle accent color (usually blue) shown on hovering over some interactive controls.
        /// </summary>
        ControlActiveHover,

        /// <summary>
        /// The background color of controls placed in panels outside the canvas.
        /// </summary>
        ControlBg,

        /// <summary>
        /// The background color of a disabled control placed in panels outside the canvas.
        /// </summary>
        ControlBgDisabled,

        /// <summary>
        /// The background color of a control that's highlighted, such as a toggled state.
        /// </summary>
        ControlBgHighlight,

        /// <summary>
        /// The background color of a control that's highlighted but disabled, such as a toggled state.
        /// </summary>
        ControlBgHighlightDisabled,

        /// <summary>
        /// Same as <see cref="ThemeSlot.ControlBg"/>, but at half transparency.
        /// </summary>
        ControlBgTranslucent,

        /// <summary>
        /// The color of the arrow on a menu item, indicating it contains sub-items.
        /// </summary>
        MenuArrow,

        /// <summary>
        /// The background color of panels outside the canvas.
        /// </summary>
        MenuBg,

        /// <summary>
        /// The color of a separator control.
        /// </summary>
        MenuSeparator,

        /// <summary>
        /// A red accent used to draw attention or distinguish controls that need it. This should be used sparingly.
        /// </summary>
        RedAccent,

        /// <summary>
        /// The color of text displays on menu controls.
        /// </summary>
        Text,

        /// <summary>
        /// The color of text belonging to and displayed over a disabled control.
        /// </summary>
        TextDisabled,

        /// <summary>
        /// A more subtle color for text that displays on menu controls.
        /// </summary>
        TextSubtle,
    }
}
