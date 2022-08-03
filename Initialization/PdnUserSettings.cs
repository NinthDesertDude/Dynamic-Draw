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
        /// Stores a copy of the user's primary color to pass to the dialog.
        /// </summary>
        public static Color userPrimaryColor
        {
            get;
            set;
        }

        /// <summary>
        /// Stores a copy of the user's secondary color to pass to the dialog.
        /// </summary>
        public static Color userSecondaryColor
        {
            get;
            set;
        }
        #endregion

        #region Constructors
        static PdnUserSettings()
        {
            userPrimaryColor = Color.Transparent;
            userSecondaryColor = Color.Transparent;
        }
        #endregion
    }
}
