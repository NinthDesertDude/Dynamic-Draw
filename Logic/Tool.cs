namespace BrushFactory
{
    /// <summary>
    /// The list of available tools.
    /// </summary>
    internal enum Tool
    {
        /// <summary>
        /// The brush tool, which allows the user to draw.
        /// </summary>
        Brush,

        /// <summary>
        /// The color picker tool, which allows the user to select a color from the canvas.
        /// </summary>
        ColorPicker,

        /// <summary>
        /// The eraser tool, which overwrites pixels with the original source image.
        /// </summary>
        Eraser,

        /// <summary>
        /// The set symmetry origin tool, which does exactly as the name implies.
        /// </summary>
        SetSymmetryOrigin
    }
}