namespace DynamicDraw
{
    /// <summary>
    /// Should mirror <see cref="ThemeName"/>, but includes Inherited; this is for indicating what theme
    /// the user has set.
    /// </summary>
    public enum ThemePreference
    {
        /// <summary>
        /// The user hasn't set a preferred theme. The theme is loaded based on what is detected from Paint.NET.
        /// </summary>
        Inherited = 0,

        /// <summary>
        /// A preference to use the light theme.
        /// </summary>
        Light = 1,

        /// <summary>
        /// A preference to use the dark theme.
        /// </summary>
        Dark = 2
    }
}
