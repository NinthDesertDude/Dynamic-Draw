namespace DynamicDraw
{
    /// <summary>
    /// Describes how to fill the transparent region underneath the user's image.
    /// </summary>
    public enum BackgroundDisplayMode
    {
        /// <summary>
        /// The area underneath will be filled with a checkerboard pattern similar to what paint.net uses.
        /// </summary>
        Transparent = 0,

        /// <summary>
        /// The area underneath will be filled by the image on the clipboard, centered and squashed to the bounds as
        /// needed. It won't be stretched to fill. Generally the user will copy a picture of all the layers merged
        /// under their current image so they can use this feature to see layers below for e.g. drawing on a blank
        /// layer.
        /// </summary>
        Clipboard = 1,

        /// <summary>
        /// The area underneath will not be filled. The gray of the canvas underneath will occupy the area. This is
        /// fastest because no additional drawing is performed.
        /// </summary>
        Gray = 2,

        /// <summary>
        /// The area underneath will be filled in white.
        /// </summary>
        White = 3,

        /// <summary>
        /// The area underneath will be filled in black.
        /// </summary>
        Black = 4
    }
}
