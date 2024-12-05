using System;

namespace DynamicDraw
{
    /// <summary>
    /// A palette will be refreshed when any of these conditions are met.
    /// </summary>
    [Flags]
    public enum PaletteRefreshTriggerFlags
    {
        /// <summary>
        /// This is a special setting. It cannot be combined, it is not a flag.
        /// </summary>
        RefreshNow = -1,

        /// <summary>
        /// Recomputes the palette when the user's color changes.
        /// </summary>
        OnColorChange = 1,

        /// <summary>
        /// Recomputes the palette when the canvas changes, after the end of a brush stroke.
        /// </summary>
        OnCanvasChange = 2
    }
}
