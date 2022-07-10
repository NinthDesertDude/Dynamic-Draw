using PaintDotNet;
using System.Drawing;

namespace DynamicDraw
{
    /// <summary>
    /// Settings passed from paint.net during plugin init
    /// </summary>
    class PdnUserSettings
    {
        #region Fields
        /// <summary>
        /// Stores a copy of the user's primary color to pass to the dialog
        /// when first used.
        /// </summary>
        public static Color userPrimaryColor
        {
            get;
            set;
        }
        #endregion

        #region Constructors
        static PdnUserSettings()
        {
            userPrimaryColor = Color.Transparent;
        }
        #endregion
    }
}
