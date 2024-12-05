namespace DynamicDraw
{
    /// <summary>
    /// These are values like primary colors, tertiary, square, etc. as well as color relations that aren't necessarily
    /// a type of color system, like recent colors. This is used in palette / swatch generation.
    /// </summary>
    public enum PaletteSpecialType
    {
        None = 0,

        /// <summary>
        /// The palette that Paint.NET currently uses.
        /// </summary>
        Current = 1,

        /// <summary>
        /// The user's most recent color choices.
        /// </summary>
        Recent = 2,

        /// <summary>
        /// Interpolation from primary to secondary color.
        /// </summary>
        PrimaryToSecondary = 3,

        /// <summary>
        /// A color scheme that starts with a color and creates monochromatic variations of it.
        /// </summary>
        LightToDark = 4,

        /// <summary>
        /// A color scheme that starts with a color and 2 adjacent hues to it, then creates monochromatic variations
        /// of them.
        Similar3 = 5,

        /// <summary>
        /// A color scheme that starts with a color and 3 adjacent hues to it, then creates monochromatic variations
        /// of them.
        /// </summary>
        Similar4 = 6,

        /// <summary>
        /// A color scheme that starts with 2 colors on opposite sides of the color wheen, then creates monochromatic
        /// variations of them.
        /// </summary>
        Complement = 7,

        /// <summary>
        /// A color scheme that starts with 2 colors on opposite sides of the color wheel, then divides those
        /// colors into a few similar hues and creates monochromatic variations of them.
        /// </summary>
        SplitComplement = 8,

        /// <summary>
        /// A color scheme that starts with 3 colors evenly distributed around the color wheel, then creates
        /// monochromatic variations on them.
        /// </summary>
        Triadic = 9,

        /// <summary>
        /// A color scheme that starts with 4 colors evenly distributed around the color wheel, then creates
        /// monochromatic variations on them.
        /// </summary>
        Square = 10,

        /// <summary>
        /// A palette (up to the swatch limit) based on the unique pixels in the image
        /// </summary>
        FromImage = 11
    }
}
