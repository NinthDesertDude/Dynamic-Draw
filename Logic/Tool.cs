namespace DynamicDraw
{
    /// <summary>
    /// The list of available tools.
    /// </summary>
    internal enum Tool
    {
        /// <summary>
        /// This can't be selected by UI, but is used by keyboard shortcut system to switch to the last tool.
        /// </summary>
        PreviousTool = 0,

        /// <summary>
        /// The brush tool, which allows the user to draw.
        /// </summary>
        Brush = 1,

        /// <summary>
        /// The color picker tool, which allows the user to select a color from the canvas.
        /// </summary>
        ColorPicker = 2,

        /// <summary>
        /// The eraser tool, which overwrites pixels with the original source image.
        /// </summary>
        Eraser = 3,

        /// <summary>
        /// The set symmetry origin tool, which does exactly as the name implies.
        /// </summary>
        SetSymmetryOrigin = 4
    }
}