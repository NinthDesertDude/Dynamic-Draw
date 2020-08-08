using System;
using System.Collections.Generic;

namespace BrushFactory
{
    /// <summary>
    /// An interface that defines the available settings.
    /// </summary>
    internal interface IBrushFactorySettings
    {
        /// <summary>
        /// Gets or sets the custom brush directories.
        /// </summary>
        /// <value>
        /// The custom brush directories.
        /// </value>
        /// <exception cref="ArgumentNullException">value is null.</exception>
        HashSet<string> CustomBrushDirectories { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to use the default brushes.
        /// </summary>
        /// <value>
        /// <c>true</c> if the default brushes should be used, <c>false</c> otherwise.
        /// </value>
        bool UseDefaultBrushes { get; set; }
    }
}
