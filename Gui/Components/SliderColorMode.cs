namespace DynamicDraw
{
    /// <summary>
    /// Specialized sliders.
    /// </summary>
    public enum SliderSpecialType
    {
        /// <summary>
        /// A slider with a gradient background of the current color going from 0 to 255 red. The min and max values
        /// of this slider are the same.
        /// </summary>
        RedGraph,

        /// <summary>
        /// A slider with a gradient background of the current color going from 0 to 255 green. The min and max values
        /// of this slider are the same.
        /// </summary>
        GreenGraph,

        /// <summary>
        /// A slider with a gradient background of the current color going from 0 to 255 blue. The min and max values
        /// of this slider are the same.
        /// </summary>
        BlueGraph,

        /// <summary>
        /// A slider with a gradient background of the current color overlaying a checkered background (indicating
        /// transparency), going from 0 to 255 alpha. The min and max values of this slider are the same.
        /// </summary>
        AlphaGraph,

        /// <summary>
        /// A slider with a gradient background of the current color going from 0 to 360 hue. The min and max values
        /// of this slider are the same.
        /// </summary>
        HueGraph,

        /// <summary>
        /// A slider with a gradient background of the current color going from 0 to 100 saturation. The min and max
        /// values of this slider are the same.
        /// </summary>
        SatGraph,

        /// <summary>
        /// A slider with a gradient background of the current color going from 0 to 100 value. The min and max values
        /// of this slider are the same.
        /// </summary>
        ValGraph
    }
}
