using PaintDotNet;
using System.Drawing;

namespace BrushFactory
{
    /// <summary>
    /// Stores copies of any number of relevant environment parameters to be
    /// copied to the dialog.
    /// </summary>
    class UserSettings
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
        static UserSettings()
        {
            userPrimaryColor = Color.Transparent;
        }
        #endregion
    }
}
