namespace DynamicDraw
{
    /// <summary>
    /// Context refers to the scenarios in which a shortcut is available, like when the display canvas has focus, or
    /// the user is drawing. This is used to disable shortcuts based on current conditions.
    /// </summary>
    public enum CommandContext
    {
        /// <summary>
        /// This context is used when the user is in a dedicated typing context.
        /// </summary>
        Typing = 0,

        /// <summary>
        /// When the user is hovered over or drawing/panning the canvas.
        /// </summary>
        OnCanvas = 1,

        /// <summary>
        /// When the user is hovered or focused on the sidebar.
        /// </summary>
        OnSidebar = 2
    }
}