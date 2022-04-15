using PaintDotNet;

namespace DynamicDraw
{
    /// <summary>
    /// Settings used to pass the final image to paint.net to apply the plugin when done.
    /// </summary>
    static class RenderSettings
    {
        #region Fields
        //The surface to render as the final product.
        private static Surface surfaceToRender;

        //Whether or not to save settings for the dialog.
        private static bool doApplyEffect;

        /// <summary>
        /// True if the effect has been applied. Prevents re-rendering.
        /// </summary>
        private static bool effectApplied;
        #endregion

        #region Properties
        /// <summary>
        /// The surface to render as the final product.
        /// </summary>
        public static Surface SurfaceToRender
        {
            get
            {
                return surfaceToRender;
            }
            set
            {
                surfaceToRender?.Dispose();

                surfaceToRender = value;
            }
        }

        /// <summary>
        /// If true, Paint.Net will persist the dialog settings.
        /// </summary>
        public static bool DoApplyEffect
        {
            get
            {
                return doApplyEffect;
            }
            set
            {
                doApplyEffect = value;
            }
        }

        /// <summary>
        /// True if the effect has been applied. Prevents re-rendering.
        /// </summary>
        public static bool EffectApplied
        {
            get
            {
                return effectApplied;
            }
            set
            {
                effectApplied = value;
            }
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Sets the bitmap so it can be locked when used by classes.
        /// </summary>
        static RenderSettings()
        {
            Clear();
        }
        #endregion

        #region Methods
        /// <summary>
        /// Resets all static variables to their defaults.
        /// </summary>
        public static void Clear()
        {
            SurfaceToRender = new Surface(1, 1);
            doApplyEffect = false;
            effectApplied = false;
        }
        #endregion
    }
}
