namespace DynamicDraw
{
    /// <summary>
    /// The list of available tools.
    /// </summary>
    internal enum Tool
    {
        /// <summary>
        /// The brush tool, which allows the user to draw.
        /// </summary>
        Brush = 0,

        /// <summary>
        /// The color picker tool, which allows the user to select a color from the canvas.
        /// </summary>
        ColorPicker = 1,

        /// <summary>
        /// The eraser tool, which overwrites pixels with the original source image.
        /// </summary>
        Eraser = 2,

        /// <summary>
        /// The set symmetry origin tool, which does exactly as the name implies.
        /// </summary>
        SetSymmetryOrigin = 3
    }
}