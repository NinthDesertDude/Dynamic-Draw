namespace DynamicDraw
{
    /// <summary>
    /// Describes the drawing operation to perform per-pixel to combine one image with another.
    /// </summary>
    public enum BlendMode
    {
        /// <summary>
        /// Interpolates towards the brush stroke colors based on opacity. Opacity only increases.
        /// </summary>
        Normal = 0,

        /// <summary>
        /// Interpolates towards the brush stroke colors and chosen opacity based on the brush image's opacity.
        /// Opacity only increases.
        /// </summary>
        Overwrite = 1
    }
}
