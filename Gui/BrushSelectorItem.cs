using BrushFactory.Properties;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BrushFactory
{
    /// <summary>
    /// Represents an item to be used by the brush selector combobox.
    /// </summary>
    class BrushSelectorItem
    {
        #region Properties
        /// <summary>
        /// Represents the menu option for the user to import their own
        /// brushes. This is referenced explicitly in the dialog handling
        /// code and therefore requires an explicit location.
        /// </summary>
        public static BrushSelectorItem CustomBrush
        {
            get;
            private set;
        }

        /// <summary>
        /// Returns the item's name.
        /// </summary>
        public string Name
        {
            get;
            set;
        }

        /// <summary>
        /// Returns the associated image.
        /// </summary>
        public Bitmap Brush
        {
            get;
            set;
        }
        #endregion

        #region Constructors
        /// <summary>
        /// Initializes all static variables.
        /// </summary>
        static BrushSelectorItem()
        {
            CustomBrush = new BrushSelectorItem("Add Brushes...", null);
        }

        /// <summary>
        /// Sets the name to be displayed in the combobox and its image.
        /// </summary>
        public BrushSelectorItem(string name, Bitmap brush)
        {
            Name = name;
            Brush = brush;
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
