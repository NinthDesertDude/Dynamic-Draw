using System.Drawing.Drawing2D;

namespace BrushFactory
{
    /// <summary>
    /// Represents a smoothing method to be used by the interpolation combobox.
    /// </summary>
    class InterpolationItem
    {
        #region Properties
        /// <summary>
        /// Returns the item's name.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Returns the associated interpolation method.
        /// </summary>
        public InterpolationMode Method
        {
            get;
            set;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Sets the name to be displayed in the combobox and its method.
        /// </summary>
        public InterpolationItem(string name, InterpolationMode method)
        {
            Name = name;
            Method = method;
        }
        #endregion

        #region Methods
        /// <summary>
        /// Returns the item's name.
        /// </summary>
        public override string ToString()
        {
            return Name;
        }
        #endregion
    }
}